/**
 * @file cesium_ion.h
 * @brief C API for CesiumIonClient: authentication, asset listing, and tokens.
 */

#ifndef CESIUM_ION_H
#define CESIUM_ION_H

#include "cesium_common.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Forward declarations — defined in cesium_tileset.h */
typedef struct CesiumAsyncSystem CesiumAsyncSystem;
typedef struct CesiumAssetAccessor CesiumAssetAccessor;

/* ============================================================================
 * Opaque handle types
 * ========================================================================= */

typedef struct CesiumIonConnection CesiumIonConnection;
typedef struct CesiumIonAssetList CesiumIonAssetList;
typedef struct CesiumIonTokenList CesiumIonTokenList;

/* ============================================================================
 * Callback types
 * ========================================================================= */

/**
 * @brief Callback invoked when asset listing completes.
 * @param userData User-provided context.
 * @param assetList The asset list result (caller must destroy), or NULL on error.
 */
typedef void (*CesiumIonAssetsCompleteCallback)(void* userData, CesiumIonAssetList* assetList);

/**
 * @brief Callback invoked when token listing completes.
 * @param userData User-provided context.
 * @param tokenList The token list result (caller must destroy), or NULL on error.
 */
typedef void (*CesiumIonTokensCompleteCallback)(void* userData, CesiumIonTokenList* tokenList);

/**
 * @brief Callback invoked during OAuth with the authorization URL.
 * The application should open this URL in a browser.
 * @param userData User-provided context.
 * @param url The URL to open.
 */
typedef void (*CesiumIonAuthorizeUrlCallback)(void* userData, const char* url);

/**
 * @brief Callback invoked when OAuth authorization completes.
 * @param userData User-provided context.
 * @param connection The resulting connection (caller must destroy), or NULL on error.
 */
typedef void (*CesiumIonAuthorizeCompleteCallback)(void* userData, CesiumIonConnection* connection);

/* ============================================================================
 * IonConnection
 * ========================================================================= */

/**
 * @brief Creates an Ion connection from an existing access token.
 * @param asyncSystem The async system.
 * @param accessor The asset accessor for HTTP requests.
 * @param accessToken The Cesium Ion access token.
 * @param apiUrl The Ion API URL, or NULL for "https://api.cesium.com/".
 */
CESIUM_API CesiumIonConnection* cesium_ion_connection_create(
    CesiumAsyncSystem* asyncSystem,
    CesiumAssetAccessor* accessor,
    const char* accessToken,
    const char* apiUrl);

/**
 * @brief Destroys an Ion connection.
 */
CESIUM_API void cesium_ion_connection_destroy(CesiumIonConnection* connection);

/**
 * @brief Starts the OAuth authorization flow.
 * @param asyncSystem The async system.
 * @param accessor The asset accessor.
 * @param appID The application's OAuth client ID.
 * @param redirectPath The redirect path (e.g., "/cesium-callback").
 * @param scopes Space-separated OAuth scopes.
 * @param urlCallback Called with the authorization URL to open in a browser.
 * @param urlCallbackUserData User data for urlCallback.
 * @param completeCallback Called when authorization completes.
 * @param completeCallbackUserData User data for completeCallback.
 */
CESIUM_API void cesium_ion_connection_authorize(
    CesiumAsyncSystem* asyncSystem,
    CesiumAssetAccessor* accessor,
    const char* appID,
    const char* redirectPath,
    const char* scopes,
    CesiumIonAuthorizeUrlCallback urlCallback,
    void* urlCallbackUserData,
    CesiumIonAuthorizeCompleteCallback completeCallback,
    void* completeCallbackUserData);

/* ============================================================================
 * Ion Asset List
 * ========================================================================= */

/**
 * @brief Requests the list of assets from Cesium Ion (async).
 */
CESIUM_API void cesium_ion_connection_list_assets(
    CesiumIonConnection* connection,
    CesiumIonAssetsCompleteCallback callback,
    void* userData);

/**
 * @brief Gets the number of assets in the list.
 */
CESIUM_API int cesium_ion_asset_list_get_count(const CesiumIonAssetList* list);

/**
 * @brief Gets an asset's ID.
 */
CESIUM_API int64_t cesium_ion_asset_list_get_asset_id(const CesiumIonAssetList* list, int index);

/**
 * @brief Gets an asset's name.
 */
CESIUM_API const char* cesium_ion_asset_list_get_asset_name(const CesiumIonAssetList* list, int index);

/**
 * @brief Gets an asset's type (e.g., "3DTILES", "TERRAIN", "IMAGERY").
 */
CESIUM_API const char* cesium_ion_asset_list_get_asset_type(const CesiumIonAssetList* list, int index);

/**
 * @brief Destroys an asset list.
 */
CESIUM_API void cesium_ion_asset_list_destroy(CesiumIonAssetList* list);

/* ============================================================================
 * Ion Token List
 * ========================================================================= */

/**
 * @brief Requests the list of tokens from Cesium Ion (async).
 */
CESIUM_API void cesium_ion_connection_list_tokens(
    CesiumIonConnection* connection,
    CesiumIonTokensCompleteCallback callback,
    void* userData);

/**
 * @brief Gets the number of tokens in the list.
 */
CESIUM_API int cesium_ion_token_list_get_count(const CesiumIonTokenList* list);

/**
 * @brief Gets a token's name.
 */
CESIUM_API const char* cesium_ion_token_list_get_token_name(const CesiumIonTokenList* list, int index);

/**
 * @brief Gets a token's value string.
 */
CESIUM_API const char* cesium_ion_token_list_get_token_value(const CesiumIonTokenList* list, int index);

/**
 * @brief Destroys a token list.
 */
CESIUM_API void cesium_ion_token_list_destroy(CesiumIonTokenList* list);

#ifdef __cplusplus
}
#endif

#endif /* CESIUM_ION_H */
