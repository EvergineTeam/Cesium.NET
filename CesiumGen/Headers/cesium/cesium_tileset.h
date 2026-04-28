/**
 * @file cesium_tileset.h
 * @brief C API for Cesium3DTilesSelection: Tileset, TilesetOptions, ViewState,
 *        ViewUpdateResult, Tile, AsyncSystem, AssetAccessor, CreditSystem,
 *        and IPrepareRendererResources bridging.
 */

#ifndef CESIUM_TILESET_H
#define CESIUM_TILESET_H

#include "cesium_common.h"
#include "cesium_geospatial.h"
#include "cesium_gltf.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ============================================================================
 * Opaque handle types
 * ========================================================================= */

typedef struct CesiumAsyncSystem CesiumAsyncSystem;
typedef struct CesiumAssetAccessor CesiumAssetAccessor;
typedef struct CesiumCreditSystem CesiumCreditSystem;
typedef struct CesiumTilesetExternals CesiumTilesetExternals;
typedef struct CesiumTilesetOptions CesiumTilesetOptions;
typedef struct CesiumTileset CesiumTileset;
typedef struct CesiumViewState CesiumViewState;
typedef struct CesiumViewUpdateResult CesiumViewUpdateResult;
typedef struct CesiumTile CesiumTile;

/* ============================================================================
 * Tile load state enum
 * ========================================================================= */

typedef enum CesiumTileLoadState {
    CESIUM_TILE_LOAD_STATE_UNLOADING = -2,
    CESIUM_TILE_LOAD_STATE_FAILED_TEMPORARILY = -1,
    CESIUM_TILE_LOAD_STATE_UNLOADED = 0,
    CESIUM_TILE_LOAD_STATE_CONTENT_LOADING = 1,
    CESIUM_TILE_LOAD_STATE_CONTENT_LOADED = 2,
    CESIUM_TILE_LOAD_STATE_DONE = 3,
    CESIUM_TILE_LOAD_STATE_FAILED = 4
} CesiumTileLoadState;

/* ============================================================================
 * Callback types
 * ========================================================================= */

/**
 * @brief Callback invoked when a tileset resource fails to load.
 * @param userData User-provided context.
 * @param message Error description.
 */
typedef void (*CesiumTilesetLoadErrorCallback)(void* userData, const char* message);

/**
 * @brief Callback invoked when the root tile becomes available.
 * @param userData User-provided context.
 */
typedef void (*CesiumRootTileAvailableCallback)(void* userData);

/* ============================================================================
 * IPrepareRendererResources — C function pointer bridge
 *
 * If not set, a default no-op implementation is used.
 * ========================================================================= */

/**
 * @brief A set of function pointers implementing IPrepareRendererResources.
 *
 * All callbacks receive userData as the first argument. Any callback may be
 * NULL, in which case a no-op default is used for that operation.
 */
typedef struct CesiumRendererResourceCallbacks {
    void* userData;

    /**
     * @brief Called in a worker thread to prepare render resources from a glTF model.
     * @param userData User context.
     * @param model Pointer to the glTF model (valid only during this call).
     * @param transform The tile's 4x4 transform matrix.
     * @return Opaque pointer to load-thread render resources, or NULL.
     */
    void* (*prepareInLoadThread)(void* userData, const CesiumGltfModel* model, CesiumMat4 transform);

    /**
     * @brief Called in the main thread to finalize render resources.
     * @param userData User context.
     * @param tile The tile being prepared.
     * @param pLoadThreadResult The result from prepareInLoadThread.
     * @return Opaque pointer to main-thread render resources, or NULL.
     */
    void* (*prepareInMainThread)(void* userData, const CesiumTile* tile, void* pLoadThreadResult);

    /**
     * @brief Called in the main thread to free render resources.
     * @param userData User context.
     * @param tile The tile.
     * @param pLoadThreadResult Result from prepareInLoadThread (NULL if prepareInMainThread was called).
     * @param pMainThreadResult Result from prepareInMainThread (NULL if not yet called).
     */
    void (*freeResources)(void* userData, const CesiumTile* tile, void* pLoadThreadResult, void* pMainThreadResult);

    /**
     * @brief Called in a worker thread to prepare raster overlay resources.
     * @param userData User context.
     * @param imageData Pointer to decoded image data.
     * @param imageDataSize Size of the image data in bytes.
     * @param imageData Pointer to decoded pixel data.
     * @param imageDataSize Size of the pixel data in bytes.
     * @param width Image width in pixels.
     * @param height Image height in pixels.
     * @param channels Number of channels (e.g., 4 for RGBA).
     * @param bytesPerChannel Bytes per channel (typically 1).
     * @return Opaque pointer to raster load-thread resources, or NULL.
     */
    void* (*prepareRasterInLoadThread)(void* userData, const uint8_t* imageData, size_t imageDataSize, int32_t width, int32_t height, int32_t channels, int32_t bytesPerChannel);

    /**
     * @brief Called in the main thread to finalize raster overlay resources.
     * @param userData User context.
     * @param pLoadThreadResult The result from prepareRasterInLoadThread.
     * @return Opaque pointer to raster main-thread resources, or NULL.
     */
    void* (*prepareRasterInMainThread)(void* userData, void* pLoadThreadResult);

    /**
     * @brief Called in the main thread to free raster resources.
     * @param userData User context.
     * @param pMainThreadResult The main-thread raster resources to free.
     */
    void (*freeRasterResources)(void* userData, void* pMainThreadResult);

    /**
     * @brief Called in the main thread to attach a raster overlay to a tile.
     * @param userData User context.
     * @param tile The geometry tile.
     * @param overlayTextureCoordinateID The texture coordinate set index.
     * @param pMainThreadRasterResources The raster resources from prepareRasterInMainThread.
     * @param translation Texture coordinate translation (x, y).
     * @param scale Texture coordinate scale (x, y).
     */
    void (*attachRasterInMainThread)(
        void* userData,
        const CesiumTile* tile,
        int32_t overlayTextureCoordinateID,
        void* pMainThreadRasterResources,
        CesiumVec2 translation,
        CesiumVec2 scale);

    /**
     * @brief Called in the main thread to detach a raster overlay from a tile.
     * @param userData User context.
     * @param tile The geometry tile.
     * @param overlayTextureCoordinateID The texture coordinate set index.
     * @param pMainThreadRasterResources The raster resources to detach.
     */
    void (*detachRasterInMainThread)(
        void* userData,
        const CesiumTile* tile,
        int32_t overlayTextureCoordinateID,
        void* pMainThreadRasterResources);

} CesiumRendererResourceCallbacks;

/* ============================================================================
 * AsyncSystem
 * ========================================================================= */

/**
 * @brief Creates an async system with a built-in thread pool.
 */
CESIUM_API CesiumAsyncSystem* cesium_async_system_create(void);

/**
 * @brief Destroys the async system.
 */
CESIUM_API void cesium_async_system_destroy(CesiumAsyncSystem* asyncSystem);

/**
 * @brief Dispatches pending main-thread tasks. Must be called each frame
 * from the main thread.
 */
CESIUM_API void cesium_async_system_dispatch_main_thread_tasks(CesiumAsyncSystem* asyncSystem);

/* ============================================================================
 * AssetAccessor (HTTP client via libcurl)
 * ========================================================================= */

/**
 * @brief Creates an asset accessor using libcurl.
 * @param userAgent The User-Agent header string, or NULL for default.
 */
CESIUM_API CesiumAssetAccessor* cesium_asset_accessor_create(const char* userAgent);

/**
 * @brief Destroys the asset accessor.
 */
CESIUM_API void cesium_asset_accessor_destroy(CesiumAssetAccessor* accessor);

/* ============================================================================
 * CreditSystem
 * ========================================================================= */

/**
 * @brief Creates a new credit system.
 */
CESIUM_API CesiumCreditSystem* cesium_credit_system_create(void);

/**
 * @brief Destroys the credit system.
 */
CESIUM_API void cesium_credit_system_destroy(CesiumCreditSystem* creditSystem);

/**
 * @brief Returns the number of credits that should be shown on screen.
 */
CESIUM_API int cesium_credit_system_get_credits_to_show_on_screen_count(const CesiumCreditSystem* creditSystem);

/**
 * @brief Returns the HTML text of a credit that should be shown on screen.
 * @param index The credit index (0 to count-1).
 */
CESIUM_API const char* cesium_credit_system_get_credit_to_show_on_screen(
    const CesiumCreditSystem* creditSystem,
    int index);

/**
 * @brief Signals the start of a new frame. Call before updateView.
 */
CESIUM_API void cesium_credit_system_start_next_frame(CesiumCreditSystem* creditSystem);

/* ============================================================================
 * TilesetExternals
 * ========================================================================= */

/**
 * @brief Creates the externals bundle required to construct a Tileset.
 * None of the parameters are consumed; the externals hold shared references.
 * @warning The caller must keep asyncSystem, accessor, and creditSystem alive
 * for the entire lifetime of any Tileset created with these externals.
 */
CESIUM_API CesiumTilesetExternals* cesium_tileset_externals_create(
    CesiumAsyncSystem* asyncSystem,
    CesiumAssetAccessor* accessor,
    CesiumCreditSystem* creditSystem);

/**
 * @brief Destroys the tileset externals.
 */
CESIUM_API void cesium_tileset_externals_destroy(CesiumTilesetExternals* externals);

/**
 * @brief Sets the renderer resource callbacks on the externals.
 * Pass NULL for callbacks to revert to the default no-op implementation.
 */
CESIUM_API void cesium_tileset_externals_set_renderer_resource_callbacks(
    CesiumTilesetExternals* externals,
    const CesiumRendererResourceCallbacks* callbacks);

/* ============================================================================
 * TilesetOptions
 * ========================================================================= */

/**
 * @brief Creates a new TilesetOptions with default values.
 */
CESIUM_API CesiumTilesetOptions* cesium_tileset_options_create(void);

/**
 * @brief Destroys the tileset options.
 */
CESIUM_API void cesium_tileset_options_destroy(CesiumTilesetOptions* options);

/* --- Setters --- */

CESIUM_API void cesium_tileset_options_set_maximum_screen_space_error(CesiumTilesetOptions* options, double value);
CESIUM_API void cesium_tileset_options_set_maximum_simultaneous_tile_loads(CesiumTilesetOptions* options, uint32_t value);
CESIUM_API void cesium_tileset_options_set_maximum_cached_bytes(CesiumTilesetOptions* options, int64_t value);
CESIUM_API void cesium_tileset_options_set_preload_ancestors(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_preload_siblings(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_forbid_holes(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_enable_frustum_culling(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_enable_fog_culling(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_enable_occlusion_culling(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_enable_lod_transition_period(CesiumTilesetOptions* options, int value);
CESIUM_API void cesium_tileset_options_set_lod_transition_length(CesiumTilesetOptions* options, float value);
CESIUM_API void cesium_tileset_options_set_load_error_callback(
    CesiumTilesetOptions* options,
    CesiumTilesetLoadErrorCallback callback,
    void* userData);

/* --- Getters --- */

CESIUM_API double cesium_tileset_options_get_maximum_screen_space_error(const CesiumTilesetOptions* options);
CESIUM_API uint32_t cesium_tileset_options_get_maximum_simultaneous_tile_loads(const CesiumTilesetOptions* options);
CESIUM_API int64_t cesium_tileset_options_get_maximum_cached_bytes(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_preload_ancestors(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_preload_siblings(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_forbid_holes(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_enable_frustum_culling(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_enable_fog_culling(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_enable_occlusion_culling(const CesiumTilesetOptions* options);
CESIUM_API int cesium_tileset_options_get_enable_lod_transition_period(const CesiumTilesetOptions* options);
CESIUM_API float cesium_tileset_options_get_lod_transition_length(const CesiumTilesetOptions* options);

/* ============================================================================
 * ViewState
 * ========================================================================= */

/**
 * @brief Creates a view state with a symmetric perspective projection.
 * @param position Camera position in ECEF coordinates.
 * @param direction Camera look direction.
 * @param up Camera up vector.
 * @param viewportSize Viewport size in pixels (width, height).
 * @param horizontalFieldOfView Horizontal FOV in radians.
 * @param verticalFieldOfView Vertical FOV in radians.
 * @param ellipsoid The ellipsoid, or NULL for WGS84.
 */
CESIUM_API CesiumViewState* cesium_view_state_create_perspective(
    CesiumVec3 position,
    CesiumVec3 direction,
    CesiumVec3 up,
    CesiumVec2 viewportSize,
    double horizontalFieldOfView,
    double verticalFieldOfView,
    const CesiumEllipsoid* ellipsoid);

/**
 * @brief Creates a view state from view and projection matrices.
 * @param viewMatrix 4x4 view matrix (inverse of camera pose).
 * @param projectionMatrix 4x4 projection matrix.
 * @param viewportSize Viewport size in pixels (width, height).
 * @param ellipsoid The ellipsoid, or NULL for WGS84.
 */
CESIUM_API CesiumViewState* cesium_view_state_create_from_matrices(
    CesiumMat4 viewMatrix,
    CesiumMat4 projectionMatrix,
    CesiumVec2 viewportSize,
    const CesiumEllipsoid* ellipsoid);

/**
 * @brief Creates a view state with an orthographic projection.
 * @param position Camera position in ECEF coordinates.
 * @param direction Camera look direction.
 * @param up Camera up vector.
 * @param viewportSize Viewport size in pixels (width, height).
 * @param left Left distance of near plane edge from center.
 * @param right Right distance of near plane edge from center.
 * @param bottom Bottom distance of near plane edge from center.
 * @param top Top distance of near plane edge from center.
 * @param ellipsoid The ellipsoid, or NULL for WGS84.
 */
CESIUM_API CesiumViewState* cesium_view_state_create_orthographic(
    CesiumVec3 position,
    CesiumVec3 direction,
    CesiumVec3 up,
    CesiumVec2 viewportSize,
    double left,
    double right,
    double bottom,
    double top,
    const CesiumEllipsoid* ellipsoid);

/**
 * @brief Destroys a view state.
 */
CESIUM_API void cesium_view_state_destroy(CesiumViewState* viewState);

/* ============================================================================
 * Tileset
 * ========================================================================= */

/**
 * @brief Creates a tileset from a tileset.json URL.
 * @param externals The externals bundle (async, HTTP, credits).
 * @param url The URL of the tileset.json.
 * @param options The tileset options, or NULL for defaults.
 */
CESIUM_API CesiumTileset* cesium_tileset_create_from_url(
    CesiumTilesetExternals* externals,
    const char* url,
    const CesiumTilesetOptions* options);

/**
 * @brief Creates a tileset from a Cesium Ion asset.
 * @param externals The externals bundle.
 * @param ionAssetID The Cesium Ion asset ID.
 * @param ionAccessToken The Cesium Ion access token.
 * @param options The tileset options, or NULL for defaults.
 * @param ionAssetEndpointUrl The Ion API endpoint, or NULL for "https://api.cesium.com/".
 */
CESIUM_API CesiumTileset* cesium_tileset_create_from_ion(
    CesiumTilesetExternals* externals,
    int64_t ionAssetID,
    const char* ionAccessToken,
    const CesiumTilesetOptions* options,
    const char* ionAssetEndpointUrl);

/**
 * @brief Destroys a tileset.
 */
CESIUM_API void cesium_tileset_destroy(CesiumTileset* tileset);

/**
 * @brief Updates the tileset's default view group and loads tiles.
 *
 * This is the main per-frame call. Pass one or more view states describing
 * the camera(s). The returned result pointer is valid until the next call
 * to this function or until the tileset is destroyed.
 *
 * @param tileset The tileset.
 * @param viewStates Array of view state pointers.
 * @param viewStateCount Number of view states.
 * @param deltaTime Time elapsed since last update, in seconds.
 * @return Borrowed pointer to the update result.
 */
CESIUM_API const CesiumViewUpdateResult* cesium_tileset_update_view(
    CesiumTileset* tileset,
    const CesiumViewState* const* viewStates,
    int viewStateCount,
    float deltaTime);

/**
 * @brief Gets the root tile, or NULL if not yet available.
 */
CESIUM_API const CesiumTile* cesium_tileset_get_root_tile(const CesiumTileset* tileset);

/**
 * @brief Returns 1 if the root tile is available, 0 otherwise (polling).
 */
CESIUM_API int cesium_tileset_is_root_tile_available(const CesiumTileset* tileset);

/**
 * @brief Sets a callback that fires when the root tile becomes available.
 * Pass NULL to clear.
 */
CESIUM_API void cesium_tileset_set_root_tile_available_callback(
    CesiumTileset* tileset,
    CesiumRootTileAvailableCallback callback,
    void* userData);

/**
 * @brief Computes the percentage of tiles loaded for the default view group.
 * @return A value between 0.0 and 100.0.
 */
CESIUM_API float cesium_tileset_compute_load_progress(CesiumTileset* tileset);

/**
 * @brief Gets the total number of tiles currently loaded.
 */
CESIUM_API int32_t cesium_tileset_get_number_of_tiles_loaded(const CesiumTileset* tileset);

/**
 * @brief Gets the total data bytes of loaded tile and overlay data.
 */
CESIUM_API int64_t cesium_tileset_get_total_data_bytes(const CesiumTileset* tileset);

/* ============================================================================
 * ViewUpdateResult (borrowed pointer from cesium_tileset_update_view)
 * ========================================================================= */

/**
 * @brief Gets the number of tiles to render this frame.
 */
CESIUM_API int cesium_view_update_result_get_tiles_to_render_count(const CesiumViewUpdateResult* result);

/**
 * @brief Gets a tile to render by index.
 * @param result The update result.
 * @param index Index in [0, count).
 * @return Borrowed pointer to the tile.
 */
CESIUM_API const CesiumTile* cesium_view_update_result_get_tile_to_render(
    const CesiumViewUpdateResult* result,
    int index);

/**
 * @brief Gets the number of tiles fading out.
 */
CESIUM_API int cesium_view_update_result_get_tiles_fading_out_count(const CesiumViewUpdateResult* result);

/**
 * @brief Gets a fading-out tile by index.
 * @param result The update result.
 * @param index Index in [0, count).
 * @return Borrowed pointer to the tile.
 */
CESIUM_API const CesiumTile* cesium_view_update_result_get_tile_fading_out(
    const CesiumViewUpdateResult* result,
    int index);

/**
 * @brief Gets the current frame number.
 */
CESIUM_API int32_t cesium_view_update_result_get_frame_number(const CesiumViewUpdateResult* result);

/**
 * @brief Gets the number of tiles visited during traversal.
 */
CESIUM_API uint32_t cesium_view_update_result_get_tiles_visited(const CesiumViewUpdateResult* result);

/**
 * @brief Gets the number of tiles culled during traversal.
 */
CESIUM_API uint32_t cesium_view_update_result_get_tiles_culled(const CesiumViewUpdateResult* result);

/**
 * @brief Gets the maximum tree depth visited.
 */
CESIUM_API uint32_t cesium_view_update_result_get_max_depth_visited(const CesiumViewUpdateResult* result);

/**
 * @brief Gets the worker thread tile load queue length.
 */
CESIUM_API int32_t cesium_view_update_result_get_worker_thread_load_queue_length(const CesiumViewUpdateResult* result);

/**
 * @brief Gets the main thread tile load queue length.
 */
CESIUM_API int32_t cesium_view_update_result_get_main_thread_load_queue_length(const CesiumViewUpdateResult* result);

/* ============================================================================
 * Tile (read-only accessors on borrowed pointers)
 * ========================================================================= */

/**
 * @brief Gets the tile's geometric error in meters.
 */
CESIUM_API double cesium_tile_get_geometric_error(const CesiumTile* tile);

/**
 * @brief Gets the tile's 4x4 transform matrix.
 */
CESIUM_API CesiumMat4 cesium_tile_get_transform(const CesiumTile* tile);

/**
 * @brief Gets the tile's load state.
 */
CESIUM_API CesiumTileLoadState cesium_tile_get_load_state(const CesiumTile* tile);

/**
 * @brief Returns 1 if the tile has renderable glTF content.
 */
CESIUM_API int cesium_tile_has_render_content(const CesiumTile* tile);

/**
 * @brief Gets the glTF model from a tile's render content.
 * @return Borrowed model pointer, or NULL if the tile has no render content.
 */
CESIUM_API const CesiumGltfModel* cesium_tile_get_render_content_model(const CesiumTile* tile);

/**
 * @brief Gets the renderer resources pointer set by prepareInMainThread.
 * @return The opaque renderer resources, or NULL.
 */
CESIUM_API void* cesium_tile_get_render_resources(const CesiumTile* tile);

/**
 * @brief Gets the number of child tiles.
 */
CESIUM_API int cesium_tile_get_children_count(const CesiumTile* tile);

/**
 * @brief Gets a child tile by index.
 */
CESIUM_API const CesiumTile* cesium_tile_get_child(const CesiumTile* tile, int index);

/**
 * @brief Gets the tile's bounding volume.
 */
CESIUM_API CesiumBoundingVolume cesium_tile_get_bounding_volume(const CesiumTile* tile);

/**
 * @brief Gets the LOD transition fade percentage (0.0 to 1.0).
 * Only meaningful when LOD transitions are enabled.
 */
CESIUM_API float cesium_tile_get_lod_transition_fade_percentage(const CesiumTile* tile);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_TILESET_H */
