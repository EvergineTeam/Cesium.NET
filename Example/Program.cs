using Evergine.Bindings.CesiumNative;
using System.Runtime.InteropServices;
using SkiaSharp; // For validating raster data output (requires SkiaSharp package)

namespace Example;

internal unsafe static class Program
{
    const string IonAccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiI1MDFhMjM5Ny04YjZiLTRhYTUtYmIwMy00NGUzZjhmZGIwYTAiLCJpZCI6Mzk2NjYxLCJpYXQiOjE3NzQyNjg5Mjd9.14FZkunpT9N4c-YKnJgvleu6kZGg-7b1_HNwJbDlVkA";

    private static readonly CesiumRendererResourceCallbacksSet RendererCallbacks = new()
    {
        PrepareInLoadThread = PrepareInLoadThread,
        PrepareInMainThread = PrepareInMainThread,
        PrepareRasterInLoadThread = PrepareRasterInLoadThread,
        PrepareRasterInMainThread = PrepareRasterInMainThread,
        AttachRasterInMainThread = AttachRasterInMainThread,
    };

    struct UserData
    {
        public int CurrentTileID; // Example of user data that could be passed to callbacks. This could be a memory allocator or other context needed for resource preparation.
    }

    public static CesiumViewState createViewState(CesiumCartographic worldCoordinates)
    {
        CesiumEllipsoid wgs84 = CesiumEllipsoid.Wgs84();
        CesiumVec3 position = wgs84.CartographicToCartesian(worldCoordinates);

        //Console.WriteLine($"Camera Position (Cartesian): ({position.x}, {position.y}, {position.z})");

        CesiumVec3 direction = new CesiumVec3
        {
            x = 0,
            y = -1,
            z = 0
        };


        //Console.WriteLine($"Camera Direction (Cartesian): ({direction.x}, {direction.y}, {direction.z})");

        CesiumVec3 up = wgs84.GeodeticSurfaceNormalCartesian(position);
        CesiumVec2 viewport = new CesiumVec2 { x = 1920, y = 1080 };
        return CesiumViewState.CreatePerspective(position, direction, up, viewport, 45.0, 45.0, wgs84);
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("Starting CesiumNative...");

        void* userData = Marshal.AllocHGlobal(Marshal.SizeOf<UserData>()).ToPointer();
        ((UserData*)userData)->CurrentTileID = 0;

        // Initialize core Cesium systems with automatic disposal via 'using'.
        using CesiumAsyncSystem asyncSystem = CesiumAsyncSystem.Create();
        using CesiumAssetAccessor assetAccessor = CesiumAssetAccessor.Create("EvergineCesiumNative/1.0");
        using CesiumCreditSystem creditSystem = CesiumCreditSystem.Create();

        using CesiumTilesetExternals externals = CesiumTilesetExternals.Create(asyncSystem, assetAccessor, creditSystem);
        externals.SetRendererResourceCallbacks(RendererCallbacks, userData);

        CesiumRasterOverlay overlay = CesiumRasterOverlay.IonRasterOverlayCreate(3, IonAccessToken, null);

        try
        {
            // Configure tileset options using C# properties.
            var options = CesiumTilesetOptions.Create();
            options.MaximumScreenSpaceError = 32;
            options.EnableFrustumCulling = true;
            options.EnableOcclusionCulling = true;
            options.PreloadSiblings = true;
            options.MaximumSimultaneousTileLoads = 8;
            options.SetLoadErrorCallback(ErrorCallback, null);

            bool printFrameDetails = false;

            // Create tileset from Ion.
            using CesiumTileset tileset = CesiumTileset.CreateFromIon(externals, 1, IonAccessToken, options, null);
            tileset.Overlays.Add(overlay);

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

                if (printFrameDetails)
                {
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
                }
                Thread.Sleep(500);
            }
        }
        finally
        {
            externals.ClearRendererResourceCallbacks();
            Marshal.FreeHGlobal((IntPtr)userData);
        }
    }

    struct RasterData
    {
        public byte* ImageData;
        public nuint ImageDataSize;
        public uint Width;
        public uint Height;
        public uint Channels;
        public uint BytesPerChannel;
    }

    private static void* PrepareInLoadThread(void* userData, CesiumGltfModel model, CesiumMat4 transform)
    {
        UserData* data = (UserData*)userData;
        int currentTile = data->CurrentTileID++;
        if (model != CesiumGltfModel.Null)
        {
            Console.WriteLine($"[Callback] ID{currentTile} glTF model received. Meshes={model.MeshCount}, Materials={model.MaterialCount}, Buffers={model.BufferCount}, Images={model.ImageCount}");
        }
        byte* dataPtr;
        nuint glbSize;

        if (model.WriteGlb(&dataPtr, &glbSize) == 1)
        {
            Console.WriteLine($"[LoadThread] Geometry ready – Meshes={model.MeshCount}, GLB={glbSize} bytes");
            File.WriteAllBytes($"output_{currentTile}.glb", new ReadOnlySpan<byte>(dataPtr, (int)glbSize));
        }
        int* id = (int*)Marshal.AllocHGlobal(sizeof(int)).ToPointer();
        *id = currentTile;
        return id; //Whatever we return here will be passed to the PrepareInMainThread callback. This could be a pointer to prepared GPU resources, or any other data needed for final preparation on the main thread.
    }

    private static void* PrepareInMainThread(void* userData, CesiumTile tile, void* pLoadData)
    {
        int* id = (int*)pLoadData;
        return pLoadData; //Whatever we return here, will be saved into Tile.RenderResources and can be used in rendering or in the AttachRasterInMainThread callback. In this example, we just pass the data through without modification.
    }

    private static void* PrepareRasterInLoadThread(void* userData, byte* imageData, nuint imageDataSize, int width, int height, int channels, int bytesPerChannel)
    {
        Console.WriteLine($"[Raster Callback] Raster overlay image received. Size={imageDataSize} bytes");
        // Allocate unmanaged memory for RasterData
        RasterData* rasterData = (RasterData*)Marshal.AllocHGlobal(sizeof(RasterData));

        // Allocate unmanaged memory for the image data
        rasterData->ImageData = (byte*)Marshal.AllocHGlobal((int)imageDataSize);
        rasterData->ImageDataSize = imageDataSize;
        rasterData->Width = (uint)width;
        rasterData->Height = (uint)height;
        rasterData->Channels = (uint)channels;
        rasterData->BytesPerChannel = (uint)bytesPerChannel;

        // Copy image data to unmanaged buffer
        Buffer.MemoryCopy(imageData, rasterData->ImageData, imageDataSize, imageDataSize);

        return rasterData;
    }

    private static void* PrepareRasterInMainThread(void* userData, void* pLoadThreadResult)
    {
        RasterData* rasterData = (RasterData*)pLoadThreadResult;
        Console.WriteLine($"[Raster Main Thread] Preparing raster for rendering. Size={rasterData->ImageDataSize} bytes");
        // Here you would typically create GPU resources from the image data and return a pointer to those resources.
        // For this example, we'll just return the same data pointer.
        return pLoadThreadResult;
    }

    private static void AttachRasterInMainThread(void* userData, CesiumTile tile, int overlayTextureCoordinateID, void* pMainThreadRasterResources, CesiumVec2 translation, CesiumVec2 scale)
    {
        // This function would be called after PrepareRasterInMainThread to associate the prepared raster resources with a specific tile.
        // The implementation would depend on how your rendering system manages tile and raster resources.
        RasterData* rasterData = (RasterData*)pMainThreadRasterResources;
        Console.WriteLine($"[Attach Raster] Attaching raster to tile. TileID={*((int*)tile.RenderResources)}, TextureCoordID={overlayTextureCoordinateID}, RasterSize={rasterData->ImageDataSize} bytes");

        // Validate output by writing the raster data to a png with SkiaSharp (requires SkiaSharp package):
        SKData skdata = SKData.CreateCopy(new ReadOnlySpan<byte>(rasterData->ImageData, (int)rasterData->ImageDataSize));
        SKImageInfo imageInfo = new SKImageInfo((int)rasterData->Width, (int)rasterData->Height, SKColorType.Rgb888x);
        SKImage sKImage = SKImage.FromPixels(imageInfo, skdata);
        SKData encodedData = sKImage.Encode(SKEncodedImageFormat.Png, 100);
        using (var stream = File.OpenWrite($"output_{*((int*)tile.RenderResources)}.png"))
        {
            encodedData.SaveTo(stream);
        }
    }

    private static void ErrorCallback(void* userData, byte* message)
    {
        string errorMessage = Marshal.PtrToStringUTF8((IntPtr)message) ?? "Unknown error";
        Console.WriteLine($"CesiumAPI: {errorMessage}");
    }
}

