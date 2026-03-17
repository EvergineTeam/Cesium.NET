using Evergine.Bindings.CesiumNative;
using System.Runtime.InteropServices;

namespace Example;

internal unsafe static class Program
{
    const string IonAccessToken = "";

    public static CesiumViewState createViewState(CesiumVec3 worldCoordinates)
    {
        CesiumEllipsoid wgs84 = CesiumEllipsoid.Wgs84();
        CesiumVec3 pos = wgs84.CartographicToCartesian(new CesiumCartographic
        {
            longitude = -71.8711433798,
            latitude = 41.06701636,
            height = 0.0
        });

        CesiumVec3 position = worldCoordinates;

        CesiumVec3 direction = new CesiumVec3
        {
            x = -position.x,
            y = -position.y,
            z = -position.z
        };

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
        options.MaximumScreenSpaceError = 16.0;
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
        CesiumVec3 initialPos = new CesiumVec3 { x = 1333698, y = -465184, z = 4138241 };
        CesiumVec3 lastPos = new CesiumVec3 { x = 1494255, y = -4579991, z = 4165880 };

        for (int i = 0; i <= 200; i++)
        {
            CesiumVec3 interpolatedPos = new CesiumVec3
            {
                x = initialPos.x + (lastPos.x - initialPos.x) * i / 200,
                y = initialPos.y + (lastPos.y - initialPos.y) * i / 200,
                z = initialPos.z + (lastPos.z - initialPos.z) * i / 200
            };
            CesiumViewState state = createViewState(initialPos);
            CesiumViewUpdateResult result = tileset.UpdateView(&state, 1, 0.2f);
            Console.WriteLine($"Camera: Position=({interpolatedPos.x}, {interpolatedPos.y}, {interpolatedPos.z})");
            Console.WriteLine($"Frame number: {result.FrameNumber}");
            Console.WriteLine($"Number of tiles to render: {result.TilesToRenderCount}");
            Console.WriteLine($"TilesVisited: {result.TilesVisited}");
            Console.WriteLine($"TilesCulled: {result.TilesCulled}");
            Console.WriteLine($"MaxDepth: {result.MaxDepthVisited}");
            for (int j = 0; j < result.TilesToRenderCount; j++)
            {
                CesiumTile tile = result.GetTileToRender(j);
                Console.WriteLine($"  Tile {j}: LoadState={tile.LoadState}, GeometricError={tile.GeometricError}, HasRenderContent={tile.HasRenderContent()}, BoundingVolume={tile.BoundingVolume.volume.region}");
            }
            Console.WriteLine("----------------------------------------------------------------");
            Thread.Sleep(200);
        }
    }

    private static void ErrorCallback(void* userData, byte* message)
    {
        string errorMessage = Marshal.PtrToStringUTF8((IntPtr)message) ?? "Unknown error";
        Console.WriteLine($"CesiumAPI: {errorMessage}");
    }
}

