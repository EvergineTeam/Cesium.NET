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

/**
 * @brief Destroys a raster overlay.
 * Remove it from any collections first.
 */
CESIUM_API void cesium_raster_overlay_destroy(CesiumRasterOverlay* overlay);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_RASTER_OVERLAYS_H */
