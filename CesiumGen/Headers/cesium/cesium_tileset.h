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

/**
 * @brief Callback invoked when sampleHeightMostDetailed completes.
 * @param userData User-provided context.
 * @param positions Sampled positions (lon/lat from input, sampled height output).
 *                  Pointer valid only for callback duration.
 * @param sampleSuccess Per-position success flags (1=true, 0=false).
 *                      Pointer valid only for callback duration.
 * @param positionCount Number of items in positions/sampleSuccess.
 */
typedef void (*CesiumSampleHeightMostDetailedCallback)(
    void* userData,
    const CesiumCartographic* positions,
    const int* sampleSuccess,
    int positionCount);

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
     * @param imageData Pointer to decoded pixel data (raw pixels, or GPU-compressed
     *        blocks when compressedPixelFormat is not NONE).
     * @param imageDataSize Size of the pixel data in bytes.
     * @param width Image width in pixels.
     * @param height Image height in pixels.
     * @param channels Number of channels (e.g., 4 for RGBA). Meaningful for uncompressed data.
     * @param bytesPerChannel Bytes per channel (typically 1). Meaningful for uncompressed data.
     * @param compressedPixelFormat GPU-compressed format of imageData, or
     *        CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_NONE if it is raw pixels.
     * @param mips Array of mip level byte ranges within imageData. If mipCount
     *        is 0, imageData is a single image and mips should be generated by
     *        the renderer if needed. The first entry is the full-resolution image.
     * @param mipCount Number of entries in the mips array.
     * @return Opaque pointer to raster load-thread resources, or NULL.
     */
    void* (*prepareRasterInLoadThread)(
        void* userData,
        const uint8_t* imageData,
        size_t imageDataSize,
        int32_t width,
        int32_t height,
        int32_t channels,
        int32_t bytesPerChannel,
        CesiumGpuCompressedPixelFormat compressedPixelFormat,
        const CesiumImageMipPosition* mips,
        int32_t mipCount);

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
     * @param pLoadThreadResult Result from prepareRasterInLoadThread.
     *        NULL if prepareRasterInMainThread has already been called.
     * @param pMainThreadResult Result from prepareRasterInMainThread.
     *        NULL if prepareRasterInMainThread has not yet been called.
     */
    void (*freeRasterResources)(void* userData, void* pLoadThreadResult, void* pMainThreadResult);
    
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
 * @brief Dispatches pending main-thread tasks. Must be called each frame from the main thread.
 *
 * On a platform with threads this runs continuations that were scheduled back to the main thread,
 * while the work itself happens on background workers. On a single-threaded build -- Emscripten
 * without -pthread, which is how .NET's browser-wasm links native code -- there are no workers, so
 * this call is what runs the background work as well. Skipping it there does not merely delay
 * callbacks: nothing loads at all.
 */
CESIUM_API void cesium_async_system_dispatch_main_thread_tasks(CesiumAsyncSystem* asyncSystem);

/* ============================================================================
 * AssetAccessor (HTTP client)
 * ========================================================================= */

/**
 * @brief Creates an asset accessor backed by libcurl.
 *
 * Available on every platform where CesiumCurl is, which upstream defines as everything
 * except wasm32. On Emscripten there is no libcurl, so this returns an accessor with no
 * transport attached: every request fails with status 0. A browser consumer wants
 * cesium_asset_accessor_create_from_callbacks instead.
 *
 * @param userAgent The User-Agent header string, or NULL for default. Ignored on Emscripten.
 */
CESIUM_API CesiumAssetAccessor* cesium_asset_accessor_create(const char* userAgent);

/**
 * @brief Identifies one in-flight request handed to the host.
 *
 * A counter rather than a pointer, and deliberately so. The host holds this value across an
 * asynchronous round trip that may outlive the request, the accessor, and the tileset that
 * asked for it -- and there is no ordering the host can impose to prevent that, because its
 * continuation is scheduled by its own runtime rather than by its frame code. A pointer would
 * dangle. An id that is never reused turns "answered too late" into a lookup miss, which is a
 * no-op.
 *
 * Unique across every accessor in the process. 0 is never issued.
 */
typedef uint64_t CesiumAssetRequestId;

#define CESIUM_ASSET_REQUEST_ID_INVALID ((CesiumAssetRequestId)0)

/**
 * @brief A set of function pointers by which the host supplies HTTP transport.
 *
 * All callbacks receive userData as the first argument, and any may be NULL, in which case a
 * no-op default is used -- the same convention as CesiumRendererResourceCallbacks. With
 * beginRequest NULL every request fails with status 0, which is what a consumer reading only
 * local data still wants.
 *
 * This is how the browser gets HTTP: browser-wasm has no libcurl, and the host already has a
 * working stack. It is not browser-only, though. Any platform may use it to own networking
 * for authentication, caching or a proxy.
 */
typedef struct CesiumAssetAccessorCallbacks {
    void* userData;

    /**
     * @brief Starts one HTTP request. The host must eventually call exactly one of
     *        cesium_asset_request_complete or cesium_asset_request_fail with this requestId --
     *        or neither, if cancelRequest arrives first.
     *
     * Must return promptly. Performing the I/O here blocks the caller, and on a
     * single-threaded build that is the only thread there is.
     *
     * @param requestId   Identifies this request in the completion call.
     * @param method      "GET", "POST", ... Borrowed; valid only during this call.
     * @param url         Absolute URL, already resolved. Borrowed.
     * @param headers     Request headers, borrowed. May be NULL when headerCount is 0.
     * @param headerCount Number of entries in headers.
     * @param body        Request payload or NULL, borrowed.
     * @param bodySize    Size of body in bytes; 0 when body is NULL.
     *
     * Called on the main thread by default -- from inside
     * cesium_async_system_dispatch_main_thread_tasks -- so a host sees one thread on all
     * platforms. See allowBeginRequestOnWorkerThread to opt out.
     */
    void (*beginRequest)(
        void* userData,
        CesiumAssetRequestId requestId,
        const char* method,
        const char* url,
        const CesiumHttpHeader* headers,
        int32_t headerCount,
        const uint8_t* body,
        size_t bodySize);

    /**
     * @brief Called when a request will no longer be waited on, so the host can abort it and
     *        drop whatever it holds for requestId. Completing it afterwards is harmless: the
     *        id is already retired and the completion call returns 0.
     *
     * Today the only thing that cancels is the accessor being destroyed with requests in
     * flight; cesium-native 0.63.0 has no per-request cancellation to forward.
     */
    void (*cancelRequest)(void* userData, CesiumAssetRequestId requestId);

    /**
     * @brief Gives a host that polls its transport somewhere to poll from. A host driven by
     *        its own event loop can leave this NULL.
     */
    void (*tick)(void* userData);

    /**
     * @brief Called once, when the last reference to the accessor goes away, after every
     *        in-flight request has been cancelled and failed. Nothing in this struct is
     *        called again afterwards, so userData may be freed.
     *
     * Without this a managed host has no signal for when to release the handle backing
     * userData.
     */
    void (*destroy)(void* userData);

    /**
     * @brief 0 to have beginRequest marshalled to the main thread; non-zero to have it called
     *        on whichever thread cesium-native asked from, which is lower latency and harder
     *        to get right.
     *
     * The polarity is chosen so a zeroed struct gets the safe behaviour, like NULL meaning
     * no-op. On a single-threaded build there is one thread and the two are the same.
     */
    int allowBeginRequestOnWorkerThread;
} CesiumAssetAccessorCallbacks;

/**
 * @brief Creates an asset accessor whose transport is supplied by the host.
 *
 * @param callbacks Copied by value. NULL, or a struct with beginRequest NULL, produces an
 *        accessor that fails every request with status 0.
 */
CESIUM_API CesiumAssetAccessor* cesium_asset_accessor_create_from_callbacks(
    const CesiumAssetAccessorCallbacks* callbacks);

/**
 * @brief Delivers a response for an in-flight request.
 *
 * May be called from any thread, at any time, including re-entrantly from inside
 * beginRequest. The response reaches cesium-native on the main thread during the next
 * cesium_async_system_dispatch_main_thread_tasks.
 *
 * @param statusCode  The HTTP status. 0 means a transport-level failure, which is what
 *                    callers already handle.
 * @param headers     Response headers or NULL. **Copied** before this returns.
 * @param body        Response bytes or NULL. **Copied** before this returns, so the caller
 *                    may free or reuse the buffer on the next line. It cannot be borrowed:
 *                    cesium-native holds the response for the whole load pipeline and there
 *                    is no callback announcing when it lets go.
 *
 * @return 1 if requestId identified an in-flight request and it was completed; 0 if it did
 *         not -- unknown, already completed, cancelled, or belonging to an accessor that no
 *         longer exists. 0 is not an error and nothing is logged: an id arriving too late is
 *         the normal outcome of a race the host cannot win.
 */
CESIUM_API int cesium_asset_request_complete(
    CesiumAssetRequestId requestId,
    uint16_t statusCode,
    const CesiumHttpHeader* headers,
    int32_t headerCount,
    const uint8_t* body,
    size_t bodySize);

/**
 * @brief Fails an in-flight request. Equivalent to completing it with status 0 and no body.
 *
 * @param message Optional diagnostic, readable through cesium_get_last_error on the calling
 *        thread. It travels no further: cesium-native's response type has no error channel,
 *        so the tileset's own load error will say "status code 0" and nothing about why.
 *
 * @return 1 if the request was in flight, 0 otherwise.
 */
CESIUM_API int cesium_asset_request_fail(
    CesiumAssetRequestId requestId,
    const char* message);

/**
 * @brief Cancels and fails every request in flight on this accessor.
 *
 * Each cancelled request gets its cancelRequest callback and is failed with status 0, and its
 * id becomes inert -- completing it afterwards returns 0 rather than reaching a tileset that
 * has moved on.
 *
 * @warning Call this when the host stops answering. Do not rely on destruction to do it.
 *
 * The accessor's destructor does cancel, but it cannot help in the case that matters. An
 * unanswered request leaves a continuation pending inside cesium-native; that continuation
 * holds a copy of TilesetExternals, and TilesetExternals holds this accessor. So while any
 * request is outstanding the accessor cannot be destroyed, and the cancellation in its
 * destructor cannot run. Destroying every handle you own is not enough -- measured, not
 * assumed, in test_host_accessor_never_answered_is_released.
 *
 * There is no timeout. Adding one would mean this library owns a clock and decides how long
 * a host is allowed to take, which is the host's call. Use
 * cesium_asset_accessor_get_pending_request_count to notice a host that has gone quiet.
 *
 * @note Keep dispatching after this returns, and before you tear the async system down.
 * Cancelling resolves each promise, but resolution is marshalled to the main thread like
 * every other completion, so the continuations that hold the accessor only let go once they
 * run. Cancel, pump, then destroy.
 */
CESIUM_API void cesium_asset_accessor_cancel_all_requests(CesiumAssetAccessor* accessor);

/**
 * @brief The number of requests handed to the host and not yet answered.
 *
 * For diagnostics, and for a host that wants to drain before shutting down. Nothing enforces
 * a timeout, so this is how a host notices one that will never be answered.
 */
CESIUM_API int32_t cesium_asset_accessor_get_pending_request_count(
    const CesiumAssetAccessor* accessor);

/**
 * @brief Destroys the asset accessor.
 *
 * Releases this handle's reference. The accessor itself lives while a CesiumTilesetExternals
 * still holds it, so cancelRequest and destroy fire when the last reference drops rather than
 * necessarily here.
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
 * @brief Unloads all tile content that is currently eligible for unloading.
 *
 * Tiles that are in use or are loading asynchronously will not be unloaded.
 * Raster overlays are deactivated and immediately reactivated, releasing their
 * renderer resources. Raster imagery needed by visible tiles will be loaded
 * again during subsequent view updates. Resources for raster loads already in
 * flight are released when those loads complete.
 * This function must be called from the main thread.
 *
 * @param tileset The tileset to trim.
 */
CESIUM_API void cesium_tileset_trim_memory(CesiumTileset* tileset);

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
 * @brief Starts asynchronous sampleHeightMostDetailed request.
 *
 * Callback runs in main-thread task queue. Caller must pump
 * cesium_async_system_dispatch_main_thread_tasks.
 *
 * @param tileset The tileset.
 * @param positions Input cartographic positions.
 * @param positionCount Number of positions.
 * @param callback Completion callback.
 * @param userData User context passed to callback.
 */
CESIUM_API void cesium_tileset_sample_height_most_detailed(
    CesiumTileset* tileset,
    const CesiumCartographic* positions,
    int positionCount,
    CesiumSampleHeightMostDetailedCallback callback,
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
