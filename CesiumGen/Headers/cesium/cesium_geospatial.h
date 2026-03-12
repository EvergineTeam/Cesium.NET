/**
 * @file cesium_geospatial.h
 * @brief C API for CesiumGeospatial: Ellipsoid, Cartographic, GlobeRectangle,
 *        GlobeTransforms.
 */

#ifndef CESIUM_GEOSPATIAL_H
#define CESIUM_GEOSPATIAL_H

#include "cesium_common.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ============================================================================
 * Opaque handle types
 * ========================================================================= */

typedef struct CesiumEllipsoid CesiumEllipsoid;

/* ============================================================================
 * CesiumGeospatial — Ellipsoid
 * ========================================================================= */

/**
 * @brief Creates an ellipsoid with the given radii.
 */
CESIUM_API CesiumEllipsoid* cesium_ellipsoid_create(double radiusX, double radiusY, double radiusZ);

/**
 * @brief Returns the WGS84 ellipsoid (singleton — do NOT destroy).
 */
CESIUM_API const CesiumEllipsoid* cesium_ellipsoid_wgs84(void);

/**
 * @brief Returns the unit sphere ellipsoid (singleton — do NOT destroy).
 */
CESIUM_API const CesiumEllipsoid* cesium_ellipsoid_unit_sphere(void);

/**
 * @brief Destroys a user-created ellipsoid. Do not call on WGS84 or UNIT_SPHERE.
 */
CESIUM_API void cesium_ellipsoid_destroy(CesiumEllipsoid* ellipsoid);

/**
 * @brief Gets the radii of the ellipsoid.
 */
CESIUM_API CesiumVec3 cesium_ellipsoid_get_radii(const CesiumEllipsoid* ellipsoid);

/**
 * @brief Gets the maximum radius.
 */
CESIUM_API double cesium_ellipsoid_get_maximum_radius(const CesiumEllipsoid* ellipsoid);

/**
 * @brief Gets the minimum radius.
 */
CESIUM_API double cesium_ellipsoid_get_minimum_radius(const CesiumEllipsoid* ellipsoid);

/**
 * @brief Converts a cartographic position to Cartesian coordinates.
 */
CESIUM_API CesiumVec3 cesium_ellipsoid_cartographic_to_cartesian(
    const CesiumEllipsoid* ellipsoid,
    CesiumCartographic cartographic);

/**
 * @brief Converts Cartesian coordinates to a cartographic position.
 * @return 1 on success, 0 if the point is at the center (result is invalid).
 */
CESIUM_API int cesium_ellipsoid_cartesian_to_cartographic(
    const CesiumEllipsoid* ellipsoid,
    CesiumVec3 cartesian,
    CesiumCartographic* out_result);

/**
 * @brief Computes the geodetic surface normal at the given Cartesian position.
 */
CESIUM_API CesiumVec3 cesium_ellipsoid_geodetic_surface_normal_cartesian(
    const CesiumEllipsoid* ellipsoid,
    CesiumVec3 cartesian);

/**
 * @brief Computes the geodetic surface normal at the given cartographic position.
 */
CESIUM_API CesiumVec3 cesium_ellipsoid_geodetic_surface_normal_cartographic(
    const CesiumEllipsoid* ellipsoid,
    CesiumCartographic cartographic);

/**
 * @brief Scales the position along the geodetic normal to the ellipsoid surface.
 * @return 1 on success, 0 if the point is at the center (result is invalid).
 */
CESIUM_API int cesium_ellipsoid_scale_to_geodetic_surface(
    const CesiumEllipsoid* ellipsoid,
    CesiumVec3 cartesian,
    CesiumVec3* out_result);

/**
 * @brief Scales the position along the geocentric normal to the ellipsoid surface.
 * @return 1 on success, 0 if the point is at the center (result is invalid).
 */
CESIUM_API int cesium_ellipsoid_scale_to_geocentric_surface(
    const CesiumEllipsoid* ellipsoid,
    CesiumVec3 cartesian,
    CesiumVec3* out_result);

/* ============================================================================
 * CesiumGeospatial — Cartographic helpers
 * ========================================================================= */

/**
 * @brief Creates a cartographic position from degrees (converted to radians).
 */
CESIUM_API CesiumCartographic cesium_cartographic_from_degrees(
    double longitudeDegrees,
    double latitudeDegrees,
    double heightMeters);

/* ============================================================================
 * CesiumGeospatial — GlobeRectangle helpers
 * ========================================================================= */

/**
 * @brief Creates a globe rectangle from degrees (converted to radians).
 */
CESIUM_API CesiumGlobeRectangle cesium_globe_rectangle_from_degrees(
    double westDegrees,
    double southDegrees,
    double eastDegrees,
    double northDegrees);

/**
 * @brief Computes the width of the globe rectangle in radians.
 */
CESIUM_API double cesium_globe_rectangle_compute_width(CesiumGlobeRectangle rect);

/**
 * @brief Computes the height of the globe rectangle in radians.
 */
CESIUM_API double cesium_globe_rectangle_compute_height(CesiumGlobeRectangle rect);

/**
 * @brief Computes the center of the globe rectangle.
 */
CESIUM_API CesiumCartographic cesium_globe_rectangle_compute_center(CesiumGlobeRectangle rect);

/**
 * @brief Returns 1 if the rectangle contains the given cartographic point.
 */
CESIUM_API int cesium_globe_rectangle_contains(
    CesiumGlobeRectangle rect,
    CesiumCartographic point);

/* ============================================================================
 * CesiumGeospatial — GlobeTransforms
 * ========================================================================= */

/**
 * @brief Computes the east-north-up to fixed frame transformation matrix.
 */
CESIUM_API CesiumMat4 cesium_globe_transforms_east_north_up_to_fixed_frame(
    CesiumVec3 origin,
    const CesiumEllipsoid* ellipsoid);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_GEOSPATIAL_H */
