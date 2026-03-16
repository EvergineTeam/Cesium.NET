using Evergine.Bindings.CesiumNative;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Example;

internal unsafe static class Program
{
    const string IonAccessToken = "";

    public static CesiumViewState createViewState()
    {
        CesiumEllipsoid wgs84 = CesiumAPI.EllipsoidWgs84();
        CesiumCartographic cam = CesiumAPI.CartographicFromDegrees(-74.006, 40.7128, 1000.0); // New york city at 1000m height

        CesiumVec3 position = CesiumAPI.EllipsoidCartographicToCartesian(wgs84, cam);

        CesiumVec3 direction = new CesiumVec3
        {
            x = -position.x,
            y = -position.y,
            z = -position.z
        };

        CesiumVec3 up = CesiumAPI.EllipsoidGeodeticSurfaceNormalCartesian(wgs84, position);
        CesiumVec2 viewport = new CesiumVec2 { x = 1920, y = 1080 };
        return CesiumAPI.ViewStateCreatePerspective(position, direction, up, viewport, 45.0, 45.0, wgs84);
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("Starting CesiumNative...");

        // Initialize core Cesium systems. Notice that they have to be kept alive for as long as you intend to use the API. They can be disposed of by calling the corresponding CesiumAPI.*Destroy() method
        CesiumAsyncSystem asyncSystem = CesiumAPI.AsyncSystemCreate();
        CesiumAssetAccessor assetAccessor = CesiumAPI.AssetAccessorCreate("EvergineCesiumNative/1.0");
        CesiumCreditSystem creditSystem = CesiumAPI.CreditSystemCreate();

        CesiumTilesetExternals externals = CesiumAPI.TilesetExternalsCreate(asyncSystem, assetAccessor, creditSystem);

        // Configure tileset options.
        CesiumTilesetOptions options = CesiumAPI.TilesetOptionsCreate();
        CesiumAPI.TilesetOptionsSetMaximumScreenSpaceError(options, 16.0);
        CesiumAPI.TilesetOptionsSetMaximumSimultaneousTileLoads(options, 8);
        CesiumAPI.TilesetOptionsSetLoadErrorCallback(options, ErrorCallback, null);

        // Get root tileset
        CesiumTileset tileset = CesiumAPI.TilesetCreateFromIon(externals, 1, IonAccessToken, options, null);

        if(tileset == CesiumTileset.Null)
        {
            string errorMessage = Marshal.PtrToStringUTF8((IntPtr)CesiumAPI.GetLastError()) ?? "Unknown error";
            Console.WriteLine($"Failed to create tileset: {errorMessage}");
            return;
        }

        CesiumViewState state = createViewState();

        CesiumAPI.TilesetUpdateView(tileset, (nint*)&state, 1, 0.016f);

        // Wait for the tileset to load. In a real application, you would typically want to do this in a separate thread and render the tileset as it loads.
        Console.WriteLine("Loading tile...");
        bool isLoaded = false;
        while (!isLoaded)
        {
            CesiumAPI.AsyncSystemDispatchMainThreadTasks(asyncSystem);
            isLoaded = CesiumAPI.TilesetIsRootTileAvailable(tileset) == 1 ? true : false;
            Thread.Sleep(10);
        }

        Console.WriteLine("Tile loaded successfully!");
    }

    private static void ErrorCallback(void* userData, byte* message)
    {
        string errorMessage = Marshal.PtrToStringUTF8((IntPtr)message) ?? "Unknown error";
        Console.WriteLine($"CesiumAPI: {errorMessage}");
    }
}

