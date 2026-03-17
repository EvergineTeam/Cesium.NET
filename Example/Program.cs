using Evergine.Bindings.CesiumNative;
using System.Runtime.InteropServices;

namespace Example;

internal unsafe static class Program
{
    const string IonAccessToken = "";

    public static CesiumViewState createViewState(CesiumCartographic worldCoordinates)
    {
        CesiumEllipsoid wgs84 = CesiumEllipsoid.Wgs84();
        CesiumVec3 position = wgs84.CartographicToCartesian(worldCoordinates);

        Console.WriteLine($"Camera Position (Cartesian): ({position.x}, {position.y}, {position.z})");

        CesiumVec3 direction = new CesiumVec3
        {
            x = 0,
            y = -1,
            z = 0
        };


        Console.WriteLine($"Camera Direction (Cartesian): ({direction.x}, {direction.y}, {direction.z})");

        CesiumVec3 up = wgs84.GeodeticSurfaceNormalCartesian(position);
        CesiumVec2 viewport = new CesiumVec2 { x = 1920, y = 1080 };
        return CesiumViewState.CreatePerspective(position, direction, up, viewport, 45.0, 45.0, wgs84);
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("Starting CesiumNative...");

        // Initialize core Cesium systems with automatic disposal via 'using'.
        using CesiumAsyncSystem asyncSystem = CesiumAsyncSystem.Create();
        using CesiumAssetAccessor assetAccessor = CesiumAssetAccessor.Create("EvergineCesiumNative/1.0");
        using CesiumCreditSystem creditSystem = CesiumCreditSystem.Create();

        using CesiumTilesetExternals externals = CesiumTilesetExternals.Create(asyncSystem, assetAccessor, creditSystem);

        // Configure tileset options using C# properties.
        var options = CesiumTilesetOptions.Create();
        options.MaximumScreenSpaceError = 32;
        options.EnableFrustumCulling = true;
        options.EnableOcclusionCulling = true;
        options.PreloadSiblings = true;
        options.MaximumSimultaneousTileLoads = 8;
        options.SetLoadErrorCallback(ErrorCallback, null);

        // Create tileset from Ion.
        using CesiumTileset tileset = CesiumTileset.CreateFromIon(externals, 1, IonAccessToken, options, null);

        if (tileset == CesiumTileset.Null)
        {
            string errorMessage = CesiumNativeApi.GetLastError() ?? "Unknown error";
            Console.WriteLine($"Failed to create tileset: {errorMessage}");
            return;
        }


        // Wait for the tileset to load.
        Console.WriteLine("Loading root tile...");
        while (!tileset.IsRootTileAvailable())
        {
            asyncSystem.DispatchMainThreadTasks();
            Thread.Sleep(10);
        }

        Console.WriteLine("Root tile loaded successfully!");
        // Coordinates in degrees for interpolation
        double initLon = -6, initLat = 37.38, initHeight = 10.0;
        double lastLon = -3.78, lastLat = 37.7787, lastHeight = 10.0;
        int numSteps = 200;
        for (int i = 0; i <= numSteps; i++)
        {
            double lon = initLon + (lastLon - initLon) * i / numSteps;
            double lat = initLat + (lastLat - initLat) * i / numSteps;
            double height = initHeight + (lastHeight - initHeight) * i / numSteps;

            // CesiumCartographic expects radians, so use FromDegrees to convert
            CesiumCartographic interpolatedPos = CesiumCartographic.FromDegrees(lon, lat, height);
            CesiumViewState state = createViewState(interpolatedPos);
            CesiumViewUpdateResult result = tileset.UpdateView(&state, 1, 0.016f);
            
            Console.WriteLine($"Frame number: {result.FrameNumber}");
            Console.WriteLine($"Number of tiles to render: {result.TilesToRenderCount}");
            Console.WriteLine($"TilesVisited: {result.TilesVisited}");
            Console.WriteLine($"TilesCulled: {result.TilesCulled}");
            Console.WriteLine($"MaxDepth: {result.MaxDepthVisited}");
            for (int j = 0; j < result.TilesToRenderCount; j++)
            {
                CesiumTile tile = result.GetTileToRender(j);
                Console.WriteLine($"  Tile {j}: LoadState={tile.LoadState}, GeometricError={tile.GeometricError}, HasRenderContent={tile.HasRenderContent()}");
            }
            Console.WriteLine("----------------------------------------------------------------");
            Thread.Sleep(16);
        }
    }

    private static void ErrorCallback(void* userData, byte* message)
    {
        string errorMessage = Marshal.PtrToStringUTF8((IntPtr)message) ?? "Unknown error";
        Console.WriteLine($"CesiumAPI: {errorMessage}");
    }
}

