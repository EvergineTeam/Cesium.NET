using Evergine.Bindings.CesiumNative;
using Evergine.Bindings.CesiumNative.Tileset;
using System.Runtime.InteropServices;

namespace Example;

internal unsafe static class Program
{
    const string IonAccessToken = "";

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
        Console.WriteLine("Loading tile...");
        while (!tileset.IsRootTileAvailable())
        {
            asyncSystem.DispatchMainThreadTasks();
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

