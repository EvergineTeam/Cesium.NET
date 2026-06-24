/**
 * @file cesium_common.h
 * @brief Common types, macros, and error handling for the CesiumNativeC API.
 */

#ifndef CESIUM_COMMON_H
#define CESIUM_COMMON_H

#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#  ifdef CESIUM_NATIVE_C_EXPORTS
#    define CESIUM_API __declspec(dllexport)
#  else
#    define CESIUM_API __declspec(dllimport)
#  endif
#else
#  define CESIUM_API __attribute__((visibility("default")))
#endif

/* ============================================================================
 * Error handling
 * ========================================================================= */

/**
 * @brief Returns the last error message, or NULL if no error has occurred.
 * The returned pointer is valid until the next API call on the same thread.
 */
CESIUM_API const char* cesium_get_last_error(void);

/**
 * @brief Clears the last error.
 */
CESIUM_API void cesium_clear_last_error(void);

/* ============================================================================
 * Blittable value types (C structs that map 1:1 across the interop boundary)
 * ========================================================================= */

/**
 * @brief A 2D vector (x, y).
 */
typedef struct CesiumVec2 {
    double x;
    double y;
} CesiumVec2;

/**
 * @brief A 3D Cartesian coordinate (x, y, z).
 */
typedef struct CesiumVec3 {
    double x;
    double y;
    double z;
} CesiumVec3;

/**
 * @brief A 4x4 matrix stored in column-major order (matches glm and OpenGL).
 */
typedef struct CesiumMat4 {
    double m[16];
} CesiumMat4;

/**
 * @brief A position defined by longitude, latitude, and height.
 * Longitude and latitude are in radians; height in meters.
 */
typedef struct CesiumCartographic {
    double longitude;
    double latitude;
    double height;
} CesiumCartographic;

/**
 * @brief A globe rectangle defined by west, south, east, north in radians.
 */
typedef struct CesiumGlobeRectangle {
    double west;
    double south;
    double east;
    double north;
} CesiumGlobeRectangle;

/* ============================================================================
 * Bounding volume (tagged union)
 * ========================================================================= */

/**
 * @brief Type discriminator for CesiumBoundingVolume.
 */
typedef enum CesiumBoundingVolumeType {
    CESIUM_BOUNDING_VOLUME_SPHERE = 0,
    CESIUM_BOUNDING_VOLUME_ORIENTED_BOX = 1,
    CESIUM_BOUNDING_VOLUME_REGION = 2
} CesiumBoundingVolumeType;

/**
 * @brief A bounding sphere: center (x,y,z) and radius.
 */
typedef struct CesiumBoundingSphere {
    CesiumVec3 center;
    double radius;
} CesiumBoundingSphere;

/**
 * @brief An oriented bounding box: center and half-axes (3x3 matrix, column-major).
 */
typedef struct CesiumOrientedBoundingBox {
    CesiumVec3 center;
    double halfAxes[9]; /**< 3x3 matrix, column-major */
} CesiumOrientedBoundingBox;

/**
 * @brief A bounding region: globe rectangle with min/max heights.
 */
typedef struct CesiumBoundingRegion {
    CesiumGlobeRectangle rectangle;
    double minimumHeight;
    double maximumHeight;
} CesiumBoundingRegion;

/**
 * @brief A bounding volume represented as a tagged union.
 */
typedef struct CesiumBoundingVolume {
    CesiumBoundingVolumeType type;
    union {
        CesiumBoundingSphere sphere;
        CesiumOrientedBoundingBox orientedBox;
        CesiumBoundingRegion region;
    } volume;
} CesiumBoundingVolume;

/* ============================================================================
 * Image / GPU texture types
 * ========================================================================= */

/**
 * @brief GPU compressed pixel format of an image, or NONE if uncompressed.
 *
 * Values mirror CesiumGltf::GpuCompressedPixelFormat one-to-one (same order).
 */
typedef enum CesiumGpuCompressedPixelFormat {
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_NONE = 0,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_ETC1_RGB,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_ETC2_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_BC1_RGB,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_BC3_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_BC4_R,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_BC5_RG,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_BC7_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_PVRTC1_4_RGB,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_PVRTC1_4_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_ASTC_4x4_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_PVRTC2_4_RGB,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_PVRTC2_4_RGBA,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_ETC2_EAC_R11,
    CESIUM_GPU_COMPRESSED_PIXEL_FORMAT_ETC2_EAC_RG11
} CesiumGpuCompressedPixelFormat;

/**
 * @brief The byte range of a single mip level within an image's pixel data.
 *
 * Layout-compatible with CesiumGltf::ImageAssetMipPosition.
 */
typedef struct CesiumImageMipPosition {
    size_t byteOffset; /**< Byte index where this mip begins. */
    size_t byteSize;   /**< Size in bytes of this mip. */
} CesiumImageMipPosition;

/**
 * @brief Target GPU-compressed formats to transcode KTX2 textures into.
 *
 * Mirrors CesiumGltf::Ktx2TranscodeTargets. A field set to NONE means images of
 * that source type are fully decompressed to raw pixels instead of transcoded.
 */
typedef struct CesiumKtx2TranscodeTargets {
    CesiumGpuCompressedPixelFormat ETC1S_R;
    CesiumGpuCompressedPixelFormat ETC1S_RG;
    CesiumGpuCompressedPixelFormat ETC1S_RGB;
    CesiumGpuCompressedPixelFormat ETC1S_RGBA;
    CesiumGpuCompressedPixelFormat UASTC_R;
    CesiumGpuCompressedPixelFormat UASTC_RG;
    CesiumGpuCompressedPixelFormat UASTC_RGB;
    CesiumGpuCompressedPixelFormat UASTC_RGBA;
} CesiumKtx2TranscodeTargets;

/* ============================================================================
 * Callback function pointer convention
 *
 * All callbacks include a void* userData parameter for closure capture.
 * ========================================================================= */

/**
 * @brief Generic callback for log messages.
 * @param userData User-provided context.
 * @param level Log level (0=trace, 1=debug, 2=info, 3=warn, 4=error, 5=critical).
 * @param message The log message string.
 */
typedef void (*CesiumLogCallback)(void* userData, int level, const char* message);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_COMMON_H */
