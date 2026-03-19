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

/* ============================================================================
 * CesiumAccessorData — resolved accessor data for zero-copy buffer access
 * ========================================================================= */

/**
 * @brief Resolved accessor data providing zero-copy access to vertex/index
 * buffer data. Returned by cesium_gltf_accessor_get_data.
 */
typedef struct CesiumAccessorData {
    /** Pointer to the first element in the buffer (owned by the model). */
    const void* data;
    /** Byte stride between consecutive elements. */
    size_t stride;
    /** Number of elements. */
    int32_t count;
    /** glTF component type (5126=FLOAT, 5123=UNSIGNED_SHORT, 5125=UNSIGNED_INT, etc.). */
    int32_t componentType;
    /** Number of components per element (1=SCALAR, 2=VEC2, 3=VEC3, 4=VEC4, 9=MAT3, 16=MAT4). */
    int32_t numberOfComponents;
    /** Total byte length of the accessible data region. */
    size_t byteLength;
} CesiumAccessorData;

/* ============================================================================
 * Mesh / Primitive accessors
 * ========================================================================= */

/**
 * @brief Gets the number of primitives in a mesh.
 */
CESIUM_API int cesium_gltf_mesh_get_primitive_count(
    const CesiumGltfModel* model,
    int meshIndex);

/**
 * @brief Gets the rendering mode of a primitive (0=POINTS, 1=LINES, 4=TRIANGLES, etc.).
 */
CESIUM_API int cesium_gltf_primitive_get_mode(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex);

/**
 * @brief Gets the material index of a primitive. Returns -1 if no material is assigned.
 */
CESIUM_API int cesium_gltf_primitive_get_material_index(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex);

/**
 * @brief Gets the accessor index for the indices of a primitive. Returns -1 if the primitive
 * has no index buffer (non-indexed rendering).
 */
CESIUM_API int cesium_gltf_primitive_get_indices_accessor_index(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex);

/**
 * @brief Gets the number of vertex attributes on a primitive.
 */
CESIUM_API int cesium_gltf_primitive_get_attribute_count(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex);

/**
 * @brief Gets the semantic name of a vertex attribute by index (e.g. "POSITION", "NORMAL").
 * The iteration order is unspecified. Returns empty string on invalid index.
 */
CESIUM_API const char* cesium_gltf_primitive_get_attribute_name(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex,
    int attributeIndex);

/**
 * @brief Gets the accessor index of a vertex attribute by its iteration index.
 * Returns -1 on invalid index.
 */
CESIUM_API int cesium_gltf_primitive_get_attribute_accessor_index(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex,
    int attributeIndex);

/**
 * @brief Finds the accessor index for a named vertex attribute (e.g. "POSITION").
 * Returns -1 if the attribute is not present.
 */
CESIUM_API int cesium_gltf_primitive_find_attribute_accessor_index(
    const CesiumGltfModel* model,
    int meshIndex,
    int primitiveIndex,
    const char* attributeName);

/* ============================================================================
 * Accessor data resolution
 * ========================================================================= */

/**
 * @brief Resolves an accessor into a zero-copy data pointer.
 * Follows the accessor -> bufferView -> buffer chain and fills the output struct.
 * @param model The model.
 * @param accessorIndex Index of the accessor.
 * @param out Output struct to fill with the resolved data pointer and metadata.
 * @return 1 on success, 0 on failure (invalid index or missing buffer data).
 */
CESIUM_API int cesium_gltf_accessor_get_data(
    const CesiumGltfModel* model,
    int accessorIndex,
    CesiumAccessorData* out);

/* ============================================================================
 * Scene graph / Node accessors
 * ========================================================================= */

/**
 * @brief Gets the default scene index. Returns -1 if none specified.
 */
CESIUM_API int cesium_gltf_model_get_default_scene(const CesiumGltfModel* model);

/**
 * @brief Gets the number of root nodes in a scene.
 */
CESIUM_API int cesium_gltf_scene_get_node_count(
    const CesiumGltfModel* model,
    int sceneIndex);

/**
 * @brief Gets a root node index from a scene.
 */
CESIUM_API int cesium_gltf_scene_get_node(
    const CesiumGltfModel* model,
    int sceneIndex,
    int index);

/**
 * @brief Gets the mesh index assigned to a node. Returns -1 if the node has no mesh.
 */
CESIUM_API int cesium_gltf_node_get_mesh(
    const CesiumGltfModel* model,
    int nodeIndex);

/**
 * @brief Gets the number of children of a node.
 */
CESIUM_API int cesium_gltf_node_get_children_count(
    const CesiumGltfModel* model,
    int nodeIndex);

/**
 * @brief Gets a child node index from a parent node.
 */
CESIUM_API int cesium_gltf_node_get_child(
    const CesiumGltfModel* model,
    int nodeIndex,
    int childIndex);

/**
 * @brief Gets the 4x4 column-major transformation matrix of a node.
 * @param out Array of 16 doubles to receive the matrix.
 * @return 1 if the node uses an explicit matrix, 0 if it uses TRS decomposition (out is still filled
 *         with the identity default in that case if the node has no explicit matrix set).
 */
CESIUM_API int cesium_gltf_node_get_matrix(
    const CesiumGltfModel* model,
    int nodeIndex,
    double out[16]);

/**
 * @brief Gets the translation of a node.
 * @param out Array of 3 doubles (x, y, z).
 */
CESIUM_API void cesium_gltf_node_get_translation(
    const CesiumGltfModel* model,
    int nodeIndex,
    double out[3]);

/**
 * @brief Gets the rotation quaternion of a node.
 * @param out Array of 4 doubles (x, y, z, w).
 */
CESIUM_API void cesium_gltf_node_get_rotation(
    const CesiumGltfModel* model,
    int nodeIndex,
    double out[4]);

/**
 * @brief Gets the scale of a node.
 * @param out Array of 3 doubles (x, y, z).
 */
CESIUM_API void cesium_gltf_node_get_scale(
    const CesiumGltfModel* model,
    int nodeIndex,
    double out[3]);

/* ============================================================================
 * Texture info — a texture reference used by materials
 * ========================================================================= */

/**
 * @brief A reference to a texture, as used by material properties.
 */
typedef struct CesiumTextureInfo {
    /** Index into Model.textures (-1 if not set). */
    int32_t textureIndex;
    /** Texture coordinate set index (e.g. 0 for TEXCOORD_0). */
    int32_t texCoord;
    /** Per-property extra scale (normalTexture.scale or occlusionTexture.strength, 1.0 otherwise). */
    double scale;
} CesiumTextureInfo;

/* ============================================================================
 * Material data — PBR metallic-roughness properties
 * ========================================================================= */

/**
 * @brief PBR metallic-roughness material data.
 */
typedef struct CesiumMaterialData {
    /** Base color factor (RGBA, linear). */
    double baseColorFactor[4];
    /** Metallic factor [0..1]. */
    double metallicFactor;
    /** Roughness factor [0..1]. */
    double roughnessFactor;
    /** Emissive factor (RGB, linear). */
    double emissiveFactor[3];
    /** Alpha cutoff threshold (used when alphaMode == 1). */
    double alphaCutoff;
    /** Alpha mode: 0 = OPAQUE, 1 = MASK, 2 = BLEND. */
    int32_t alphaMode;
    /** 1 if double-sided, 0 otherwise. */
    int32_t doubleSided;
    /** Base color texture. textureIndex == -1 if not set. */
    CesiumTextureInfo baseColorTexture;
    /** Metallic-roughness texture. textureIndex == -1 if not set. */
    CesiumTextureInfo metallicRoughnessTexture;
    /** Normal map texture. textureIndex == -1 if not set. */
    CesiumTextureInfo normalTexture;
    /** Occlusion texture. textureIndex == -1 if not set. */
    CesiumTextureInfo occlusionTexture;
    /** Emissive texture. textureIndex == -1 if not set. */
    CesiumTextureInfo emissiveTexture;
} CesiumMaterialData;

/**
 * @brief Fills a CesiumMaterialData struct with the properties of a material.
 * @return 1 on success, 0 on invalid index.
 */
CESIUM_API int cesium_gltf_material_get_data(
    const CesiumGltfModel* model,
    int materialIndex,
    CesiumMaterialData* out);

/* ============================================================================
 * Texture / Sampler accessors
 * ========================================================================= */

/**
 * @brief Gets the image (source) index of a texture. Returns -1 if not set.
 */
CESIUM_API int cesium_gltf_texture_get_source(
    const CesiumGltfModel* model,
    int textureIndex);

/**
 * @brief Gets the sampler index of a texture. Returns -1 if not set.
 */
CESIUM_API int cesium_gltf_texture_get_sampler(
    const CesiumGltfModel* model,
    int textureIndex);

/**
 * @brief Sampler data with filter and wrap modes.
 */
typedef struct CesiumSamplerData {
    /** Magnification filter (9728=NEAREST, 9729=LINEAR), -1 if not set. */
    int32_t magFilter;
    /** Minification filter (9728..9987), -1 if not set. */
    int32_t minFilter;
    /** Wrap mode S (10497=REPEAT, 33071=CLAMP_TO_EDGE, 33648=MIRRORED_REPEAT). */
    int32_t wrapS;
    /** Wrap mode T. */
    int32_t wrapT;
} CesiumSamplerData;

/**
 * @brief Fills a CesiumSamplerData struct with the properties of a sampler.
 * @return 1 on success, 0 on invalid index.
 */
CESIUM_API int cesium_gltf_sampler_get_data(
    const CesiumGltfModel* model,
    int samplerIndex,
    CesiumSamplerData* out);

/* ============================================================================
 * Image accessors
 * ========================================================================= */

/**
 * @brief Decoded image data (pixel buffer).
 */
typedef struct CesiumImageData {
    /** Pointer to the pixel data (owned by the model). */
    const void* pixelData;
    /** Total size of the pixel data in bytes. */
    size_t pixelDataSize;
    /** Image width in pixels. */
    int32_t width;
    /** Image height in pixels. */
    int32_t height;
    /** Number of channels (1=grey, 2=grey+alpha, 3=RGB, 4=RGBA). */
    int32_t channels;
    /** Bytes per channel (typically 1). */
    int32_t bytesPerChannel;
} CesiumImageData;

/**
 * @brief Fills a CesiumImageData struct with the decoded pixel data of an image.
 * @return 1 on success, 0 on failure (invalid index or no pixel data available).
 */
CESIUM_API int cesium_gltf_image_get_data(
    const CesiumGltfModel* model,
    int imageIndex,
    CesiumImageData* out);

/* ============================================================================
 * GLB serialization — export model back to binary glTF
 * ========================================================================= */

/**
 * @brief Serializes a CesiumGltfModel to GLB (binary glTF 2.0) format.
 * The returned buffer is heap-allocated and must be freed with cesium_gltf_free_glb.
 * @param model The model to serialize.
 * @param out_data Receives a pointer to the GLB byte buffer.
 * @param out_size Receives the size of the GLB buffer in bytes.
 * @return 1 on success, 0 on failure.
 */
CESIUM_API int cesium_gltf_model_write_glb(
    const CesiumGltfModel* model,
    uint8_t** out_data,
    size_t* out_size);

/**
 * @brief Frees a GLB buffer previously returned by cesium_gltf_model_write_glb.
 */
CESIUM_API void cesium_gltf_free_glb(uint8_t* data);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_GLTF_H */
