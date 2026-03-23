using Evergine.Bindings.CesiumNative;
using System.Runtime.InteropServices;

namespace Example;

/// <summary>
/// Holds geometry + raster overlay data for a single tile.
/// Returned as opaque pointer through the renderer resource callbacks.
/// </summary>
internal class TileRenderData
{
    public byte[]? GlbData;
    public CesiumMat4 Transform;

    /// <summary>Raster overlay images keyed by overlayTextureCoordinateID.</summary>
    public Dictionary<int, RasterAttachment> Overlays = new();
}

internal struct RasterAttachment
{
    public required byte[] ImageData;
    public int Width;
    public int Height;
    public int Channels;
    public int BytesPerChannel;
    public CesiumVec2 Translation;
    public CesiumVec2 Scale;
}

/// <summary>Holds raw image bytes + dimensions for a raster overlay tile.</summary>
internal class RasterData
{
    public required byte[] ImageBytes;
    public int Width;
    public int Height;
    public int Channels;
    public int BytesPerChannel;
}

internal unsafe static class Program
{
    const string IonAccessToken = "";

    private static readonly CesiumRendererResourceCallbacksSet RendererCallbacks = new()
    {
        PrepareInLoadThread = PrepareInLoadThread,
        PrepareInMainThread = PrepareInMainThread,
        FreeResources = FreeResources,
        PrepareRasterInLoadThread = PrepareRasterInLoadThread,
        PrepareRasterInMainThread = PrepareRasterInMainThread,
        FreeRasterResources = FreeRasterResources,
        AttachRasterInMainThread = AttachRasterInMainThread,
        DetachRasterInMainThread = DetachRasterInMainThread,
    };

    public static CesiumViewState createViewState(CesiumCartographic worldCoordinates)
    {
        CesiumEllipsoid wgs84 = CesiumEllipsoid.Wgs84();
        CesiumVec3 position = wgs84.CartographicToCartesian(worldCoordinates);
        CesiumVec3 direction = new CesiumVec3
        {
            x = 0,
            y = -1,
            z = 0
        };

        //Console.WriteLine($"Camera Position (Cartesian): ({position.x}, {position.y}, {position.z})");
        //Console.WriteLine($"Camera Direction (Cartesian): ({direction.x}, {direction.y}, {direction.z})");

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
        using CesiumRasterOverlay overlay = CesiumRasterOverlay.IonRasterOverlayCreate(2, IonAccessToken, null);
        externals.SetRendererResourceCallbacks(RendererCallbacks);

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
                Thread.Sleep(16);
            }
        }
        finally
        {
            externals.ClearRendererResourceCallbacks();
        }
    }

    // ── Geometry tile callbacks ────────────────────────────────────────────

    /// <summary>Worker thread: export GLB and wrap in TileRenderData.</summary>
    private static void* PrepareInLoadThread(void* userData, CesiumGltfModel model, CesiumMat4 transform)
    {
        if (model == CesiumGltfModel.Null)
            return null;

        var data = new TileRenderData { Transform = transform };

        byte* dataPtr;
        nuint glbSize;
        if (model.WriteGlb(&dataPtr, &glbSize) == 1)
        {
            data.GlbData = new byte[(int)glbSize];
            Marshal.Copy((IntPtr)dataPtr, data.GlbData, 0, (int)glbSize);
            CesiumNativeApi.GltfFreeGlb(dataPtr);
        }

        Console.WriteLine($"[LoadThread] Geometry ready – Meshes={model.MeshCount}, GLB={data.GlbData?.Length ?? 0} bytes");

        // Return a GCHandle so the pointer survives across callbacks
        var handle = GCHandle.Alloc(data);
        return (void*)GCHandle.ToIntPtr(handle);
    }

    /// <summary>Main thread: pass the load-thread result through (or do GPU upload).</summary>
    private static void* PrepareInMainThread(void* userData, CesiumTile tile, void* pLoadThreadResult)
    {
        // Simply forward the same GCHandle pointer — in a real renderer you'd
        // create GPU resources here.
        return pLoadThreadResult;
    }

    /// <summary>Main thread: free the GCHandle holding TileRenderData.</summary>
    private static void FreeResources(void* userData, CesiumTile tile, void* pLoadThreadResult, void* pMainThreadResult)
    {
        void* active = pMainThreadResult != null ? pMainThreadResult : pLoadThreadResult;
        if (active != null)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)active);
            handle.Free();
        }
    }

    // ── Raster overlay callbacks ─────────────────────────────────────────

    /// <summary>Worker thread: copy the raw overlay image pixels.</summary>
    private static void* PrepareRasterInLoadThread(void* userData, byte* imageData, nuint imageDataSize, int width, int height, int channels, int bytesPerChannel)
    {
        if (imageData == null || imageDataSize == 0)
            return null;

        var raster = new RasterData
        {
            ImageBytes = new byte[(int)imageDataSize],
            Width = width,
            Height = height,
            Channels = channels,
            BytesPerChannel = bytesPerChannel,
        };
        Marshal.Copy((IntPtr)imageData, raster.ImageBytes, 0, (int)imageDataSize);

        Console.WriteLine($"[LoadThread] Raster overlay image ready – {imageDataSize} bytes ({width}x{height}, {channels}ch)");

        var handle = GCHandle.Alloc(raster);
        return (void*)GCHandle.ToIntPtr(handle);
    }

    /// <summary>Main thread: finalize raster resources (GPU upload in a real renderer).</summary>
    private static void* PrepareRasterInMainThread(void* userData, void* pLoadThreadResult)
    {
        return pLoadThreadResult;
    }

    /// <summary>Main thread: free the raster GCHandle.</summary>
    private static void FreeRasterResources(void* userData, void* pMainThreadResult)
    {
        if (pMainThreadResult != null)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)pMainThreadResult);
            handle.Free();
        }
    }

    /// <summary>
    /// Main thread: CORRELATION POINT – attaches a raster overlay to a geometry tile.
    /// tile.RenderResources returns our TileRenderData pointer.
    /// </summary>
    private static void AttachRasterInMainThread(
        void* userData,
        CesiumTile tile,
        int overlayTextureCoordinateID,
        void* pMainThreadRasterResources,
        CesiumVec2 translation,
        CesiumVec2 scale)
    {
        // Get our TileRenderData from the tile
        void* tileResPtr = tile.RenderResources;
        if (tileResPtr == null || pMainThreadRasterResources == null)
            return;

        var tileData = (TileRenderData)GCHandle.FromIntPtr((IntPtr)tileResPtr).Target!;
        var rasterData = (RasterData)GCHandle.FromIntPtr((IntPtr)pMainThreadRasterResources).Target!;

        tileData.Overlays[overlayTextureCoordinateID] = new RasterAttachment
        {
            ImageData = rasterData.ImageBytes,
            Width = rasterData.Width,
            Height = rasterData.Height,
            Channels = rasterData.Channels,
            BytesPerChannel = rasterData.BytesPerChannel,
            Translation = translation,
            Scale = scale,
        };

        Console.WriteLine($"[MainThread] Attached raster overlay #{overlayTextureCoordinateID} to tile " +
                          $"({rasterData.ImageBytes.Length} bytes {rasterData.Width}x{rasterData.Height}, " +
                          $"translate=({translation.x:F3},{translation.y:F3}), scale=({scale.x:F3},{scale.y:F3}))");

        // Export a GLB with the overlay baked in using the tile's model
        CesiumGltfModel model = tile.RenderContentModel;
        if (model != CesiumGltfModel.Null && tileData.Overlays.Count > 0)
        {
            // Pin all image byte arrays so pointers remain valid during the native call
            var pins = new List<GCHandle>();
            var overlayInfos = new CesiumRasterOverlayInfo[tileData.Overlays.Count];
            int idx = 0;
            foreach (var (coordId, att) in tileData.Overlays)
            {
                var pin = GCHandle.Alloc(att.ImageData, GCHandleType.Pinned);
                pins.Add(pin);
                overlayInfos[idx++] = new CesiumRasterOverlayInfo
                {
                    pixelData = (byte*)pin.AddrOfPinnedObject(),
                    pixelDataSize = (nuint)att.ImageData.Length,
                    width = att.Width,
                    height = att.Height,
                    channels = att.Channels,
                    bytesPerChannel = att.BytesPerChannel,
                    textureCoordinateIndex = coordId,
                    translation = att.Translation,
                    scale = att.Scale,
                };
            }

            try
            {
                byte* dataPtr;
                nuint glbSize;
                fixed (CesiumRasterOverlayInfo* pOverlays = overlayInfos)
                {
                    if (model.WriteGlbWithOverlays(pOverlays, overlayInfos.Length, &dataPtr, &glbSize) == 1)
                    {
                        string filePath = $"tile_textured_{Guid.NewGuid():N}.glb";
                        File.WriteAllBytes(filePath, new ReadOnlySpan<byte>(dataPtr, (int)glbSize));
                        Console.WriteLine($"[MainThread] Textured GLB written: {filePath} ({glbSize} bytes)");
                        CesiumNativeApi.GltfFreeGlb(dataPtr);
                    }
                }
            }
            finally
            {
                foreach (var pin in pins) pin.Free();
            }
        }
    }

    /// <summary>Main thread: detach a raster overlay from a tile.</summary>
    private static void DetachRasterInMainThread(
        void* userData,
        CesiumTile tile,
        int overlayTextureCoordinateID,
        void* pMainThreadRasterResources)
    {
        void* tileResPtr = tile.RenderResources;
        if (tileResPtr == null)
            return;

        var tileData = (TileRenderData)GCHandle.FromIntPtr((IntPtr)tileResPtr).Target!;
        tileData.Overlays.Remove(overlayTextureCoordinateID);

        Console.WriteLine($"[MainThread] Detached raster overlay #{overlayTextureCoordinateID}");
    }

    private static void ErrorCallback(void* userData, byte* message)
    {
        string errorMessage = Marshal.PtrToStringUTF8((IntPtr)message) ?? "Unknown error";
        Console.WriteLine($"CesiumAPI: {errorMessage}");
    }
}


