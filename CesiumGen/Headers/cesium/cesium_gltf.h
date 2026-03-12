/**
 * @file cesium_gltf.h
 * @brief C API for CesiumGltfReader and read-only Model accessors.
 */

#ifndef CESIUM_GLTF_H
#define CESIUM_GLTF_H

#include "cesium_common.h"

#ifdef __cplusplus
extern "C" {
#endif

/* ============================================================================
 * Opaque handle types
 * ========================================================================= */

typedef struct CesiumCGltfReader CesiumCGltfReader;
typedef struct CesiumGltfModel CesiumGltfModel;
typedef struct CesiumCGltfReaderResult CesiumCGltfReaderResult;

/* ============================================================================
 * CesiumGltfReader — GltfReader
 * ========================================================================= */

/**
 * @brief Creates a new GltfReader instance.
 */
CESIUM_API CesiumCGltfReader* cesium_gltf_reader_create(void);

/**
 * @brief Destroys a GltfReader instance.
 */
CESIUM_API void cesium_gltf_reader_destroy(CesiumCGltfReader* reader);

/**
 * @brief Reads a glTF or GLB from a byte buffer.
 * @param reader The reader instance.
 * @param data Pointer to the glTF/GLB data.
 * @param data_size Size of the data in bytes.
 * @return A result handle (must be destroyed with cesium_gltf_reader_result_destroy).
 */
CESIUM_API CesiumCGltfReaderResult* cesium_gltf_reader_read(
    const CesiumCGltfReader* reader,
    const uint8_t* data,
    size_t data_size);

/* ============================================================================
 * CesiumGltfReader — GltfReaderResult
 * ========================================================================= */

/**
 * @brief Destroys a GltfReaderResult and any model it contains.
 */
CESIUM_API void cesium_gltf_reader_result_destroy(CesiumCGltfReaderResult* result);

/**
 * @brief Returns 1 if the result contains a valid model.
 */
CESIUM_API int cesium_gltf_reader_result_has_model(const CesiumCGltfReaderResult* result);

/**
 * @brief Returns the number of errors in the result.
 */
CESIUM_API int cesium_gltf_reader_result_get_error_count(const CesiumCGltfReaderResult* result);

/**
 * @brief Returns the error message at the given index.
 */
CESIUM_API const char* cesium_gltf_reader_result_get_error(
    const CesiumCGltfReaderResult* result,
    int index);

/**
 * @brief Returns the number of warnings in the result.
 */
CESIUM_API int cesium_gltf_reader_result_get_warning_count(const CesiumCGltfReaderResult* result);

/**
 * @brief Returns the warning message at the given index.
 */
CESIUM_API const char* cesium_gltf_reader_result_get_warning(
    const CesiumCGltfReaderResult* result,
    int index);

/* ============================================================================
 * CesiumGltf — Model (read-only accessors through the reader result)
 * ========================================================================= */

/**
 * @brief Gets the model from a reader result. The pointer is owned by the result.
 * @return The model pointer, or NULL if no model was read.
 */
CESIUM_API const CesiumGltfModel* cesium_gltf_reader_result_get_model(
    const CesiumCGltfReaderResult* result);

/**
 * @brief Gets the number of meshes in the model.
 */
CESIUM_API int cesium_gltf_model_get_mesh_count(const CesiumGltfModel* model);

/**
 * @brief Gets the name of a mesh. Returns empty string if no name.
 */
CESIUM_API const char* cesium_gltf_model_get_mesh_name(
    const CesiumGltfModel* model,
    int meshIndex);

/**
 * @brief Gets the number of materials in the model.
 */
CESIUM_API int cesium_gltf_model_get_material_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of textures in the model.
 */
CESIUM_API int cesium_gltf_model_get_texture_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of images in the model.
 */
CESIUM_API int cesium_gltf_model_get_image_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of nodes in the model.
 */
CESIUM_API int cesium_gltf_model_get_node_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of accessors in the model.
 */
CESIUM_API int cesium_gltf_model_get_accessor_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of buffers in the model.
 */
CESIUM_API int cesium_gltf_model_get_buffer_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of buffer views in the model.
 */
CESIUM_API int cesium_gltf_model_get_buffer_view_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of scenes in the model.
 */
CESIUM_API int cesium_gltf_model_get_scene_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of animations in the model.
 */
CESIUM_API int cesium_gltf_model_get_animation_count(const CesiumGltfModel* model);

/**
 * @brief Gets the number of skins in the model.
 */
CESIUM_API int cesium_gltf_model_get_skin_count(const CesiumGltfModel* model);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_GLTF_H */
