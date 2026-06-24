/**
 * @file cesium_raster_overlays.h
 * @brief C API for CesiumRasterOverlays: adding imagery layers to tilesets.
 */

#ifndef CESIUM_RASTER_OVERLAYS_H
#define CESIUM_RASTER_OVERLAYS_H

#include "cesium_common.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ============================================================================
 * Opaque handle types
 * ========================================================================= */

typedef struct CesiumRasterOverlay CesiumRasterOverlay;
typedef struct CesiumRasterOverlayCollection CesiumRasterOverlayCollection;

/* Forward declaration — CesiumTileset is defined in cesium_tileset.h */
typedef struct CesiumTileset CesiumTileset;

/* ============================================================================
 * RasterOverlayCollection (obtained from a Tileset)
 * ========================================================================= */

/**
 * @brief Gets the raster overlay collection from a tileset.
 * The returned pointer is owned by the tileset.
 */
CESIUM_API CesiumRasterOverlayCollection* cesium_tileset_get_overlays(CesiumTileset* tileset);

/**
 * @brief Adds a raster overlay to the collection.
 */
CESIUM_API void cesium_raster_overlay_collection_add(
    CesiumRasterOverlayCollection* collection,
    CesiumRasterOverlay* overlay);

/**
 * @brief Removes a raster overlay from the collection.
 */
CESIUM_API void cesium_raster_overlay_collection_remove(
    CesiumRasterOverlayCollection* collection,
    CesiumRasterOverlay* overlay);

/* ============================================================================
 * Concrete RasterOverlay factories
 *
 * Each factory returns a CesiumRasterOverlay* that can be added to a
 * collection. Call cesium_raster_overlay_destroy when done.
 * ========================================================================= */

/**
 * @brief Creates a Cesium Ion raster overlay.
 * @param assetID The Ion asset ID for the imagery.
 * @param accessToken The Ion access token.
 * @param ionAssetEndpointUrl The Ion API endpoint, or NULL for default.
 */
CESIUM_API CesiumRasterOverlay* cesium_ion_raster_overlay_create(
    int64_t assetID,
    const char* accessToken,
    const char* ionAssetEndpointUrl);

/**
 * @brief Creates a URL-template raster overlay (e.g., XYZ tiles).
 * @param name Display name for the overlay.
 * @param urlTemplate URL template with {x}, {y}, {z} placeholders.
 * @param minimumLevel Minimum zoom level.
 * @param maximumLevel Maximum zoom level.
 * @param tileWidth Tile width in pixels (e.g., 256).
 * @param tileHeight Tile height in pixels (e.g., 256).
 */
CESIUM_API CesiumRasterOverlay* cesium_url_template_raster_overlay_create(
    const char* name,
    const char* urlTemplate,
    uint32_t minimumLevel,
    uint32_t maximumLevel,
    uint32_t tileWidth,
    uint32_t tileHeight);

/**
 * @brief Creates a TMS (Tile Map Service) raster overlay.
 * @param name Display name for the overlay.
 * @param url The TMS service URL.
 */
CESIUM_API CesiumRasterOverlay* cesium_tile_map_service_raster_overlay_create(
    const char* name,
    const char* url);

/**
 * @brief Creates a WMS (Web Map Service) raster overlay.
 * @param name Display name for the overlay.
 * @param url The WMS service URL.
 * @param layers Comma-separated list of WMS layers.
 * @param tileWidth Tile width in pixels.
 * @param tileHeight Tile height in pixels.
 */
CESIUM_API CesiumRasterOverlay* cesium_web_map_service_raster_overlay_create(
    const char* name,
    const char* url,
    const char* layers,
    int32_t tileWidth,
    int32_t tileHeight);

/* ============================================================================
 * RasterOverlayOptions — tuning parameters shared by all overlay types
 * ========================================================================= */

/**
 * @brief Tuning options common to every raster overlay type.
 *
 * Mirrors the C++ CesiumRasterOverlays::RasterOverlayOptions. Initialize with
 * cesium_raster_overlay_options_default to obtain the library defaults, override
 * the fields you care about, then apply them with cesium_raster_overlay_set_options
 * before adding the overlay to a collection (the options are read when the
 * overlay is activated).
 */
typedef struct CesiumRasterOverlayOptions {
    /** @brief Max overlay tiles loading simultaneously. Default 20. */
    int32_t maximumSimultaneousTileLoads;
    /** @brief Max bytes used to cache sub-tiles in memory. Default 16 MiB. */
    int64_t subTileCacheBytes;
    /** @brief Max overlay texture size in pixels, in either direction. Default 2048. */
    int32_t maximumTextureSize;
    /** @brief Max screen-space error in pixels, used for level-of-detail. Default 2.0. */
    double maximumScreenSpaceError;
    /** @brief Non-zero to display credits on screen. Default 0. */
    int32_t showCreditsOnScreen;
    /**
     * @brief Target GPU-compressed formats for KTX2 imagery.
     *
     * Set the fields matching the formats your renderer supports so KTX2
     * overlay textures are transcoded directly to a GPU format (reported back
     * through the prepareRasterInLoadThread callback). All-NONE (the default)
     * fully decompresses KTX2 textures to raw pixels.
     */
    CesiumKtx2TranscodeTargets ktx2TranscodeTargets;
} CesiumRasterOverlayOptions;

/**
 * @brief Fills out with the library default raster overlay options.
 */
CESIUM_API void cesium_raster_overlay_options_default(CesiumRasterOverlayOptions* out);

/**
 * @brief Reads the current options of an overlay into out.
 * @return 1 on success, 0 on failure.
 */
CESIUM_API int cesium_raster_overlay_get_options(
    const CesiumRasterOverlay* overlay,
    CesiumRasterOverlayOptions* out);

/**
 * @brief Applies options to an overlay. Call before adding it to a collection.
 * @return 1 on success, 0 on failure.
 */
CESIUM_API int cesium_raster_overlay_set_options(
    CesiumRasterOverlay* overlay,
    const CesiumRasterOverlayOptions* options);

/**
 * @brief Destroys a raster overlay.
 * Remove it from any collections first.
 */
CESIUM_API void cesium_raster_overlay_destroy(CesiumRasterOverlay* overlay);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_RASTER_OVERLAYS_H */
