// The desktop counterpart of WasmSmokeTest: same package, same path through it, one deliberate
// difference.
//
// This one uses AssetAccessorCallbacksSet, the generated helper. WasmSmokeTest cannot -- the
// wasm runtime rejects a delegate reached from native code -- and the remark added to that
// helper says it is desktop only. That claim was written without being tested, so this is what
// tests it. If the helper is broken here too, the remark is wrong and so is the package's
// documentation.
//
// No network, for the same reason as the wasm test: the host serves a canned document from
// memory, which is the shape a real host has with a different source of bytes.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Evergine.Bindings.CesiumNative;

internal static unsafe class Program
{
    private const string TilesetJson = """
    {
      "asset": { "version": "1.0" },
      "geometricError": 100.0,
      "root": {
        "boundingVolume": { "region": [-0.001, -0.001, 0.001, 0.001, 0.0, 10.0] },
        "geometricError": 50.0,
        "refine": "REPLACE"
      }
    }
    """;

    private const string TilesetUrl = "https://fake.test/tileset.json";

    private static int _beginCalls;
    private static int _cancelCalls;
    private static int _failures;
    private static readonly List<ulong> Queued = new();

    private static void Check(bool condition, string what)
    {
        Console.WriteLine((condition ? "  ok    " : "  FAIL  ") + what);
        if (!condition)
        {
            _failures++;
        }
    }

    private static void OnBeginRequest(
        void* userData, ulong requestId, byte* method, byte* url,
        HttpHeader* headers, int headerCount, byte* body, nuint bodySize)
    {
        _beginCalls++;
        string requested = Marshal.PtrToStringUTF8((IntPtr)url) ?? "";
        Console.WriteLine($"  host: request {requestId} for {requested}");
        Queued.Add(requestId);
    }

    private static void OnCancelRequest(void* userData, ulong requestId) => _cancelCalls++;

    private static void ServeQueued()
    {
        ulong[] pending = Queued.ToArray();
        Queued.Clear();

        byte[] payload = Encoding.UTF8.GetBytes(TilesetJson);
        foreach (ulong id in pending)
        {
            fixed (byte* p = payload)
            {
                int accepted = CesiumNativeApi.AssetRequestComplete(
                    id, 200, null, 0, p, (nuint)payload.Length);
                Check(accepted == 1, $"the accessor accepted the answer to request {id}");
            }
        }
    }

    private static int Main()
    {
        Console.WriteLine($"CesiumNativeC on {RuntimeInformation.RuntimeIdentifier}");

        AsyncSystem async = AsyncSystem.Create();
        Check(async.Handle == IntPtr.Zero, "DELIBERATELY BROKEN: proving that a red leg blocks publishing");

        // The generated helper, which is the point of this test.
        var callbacks = new AssetAccessorCallbacksSet
        {
            BeginRequest = OnBeginRequest,
            CancelRequest = OnCancelRequest,
        };
        AssetAccessorCallbacks native = callbacks.ToNative();
        AssetAccessor accessor = AssetAccessor.CreateFromCallbacks(&native);
        Check(accessor.Handle != IntPtr.Zero, "the host accessor was created through AssetAccessorCallbacksSet");

        CreditSystem credits = CreditSystem.Create();
        TilesetExternals externals = TilesetExternals.Create(async, accessor, credits);
        TilesetOptions options = TilesetOptions.Create();
        Tileset tileset = Tileset.CreateFromUrl(externals, TilesetUrl, options);
        Check(tileset.Handle != IntPtr.Zero, "the tileset was created");

        var view = new ViewState();
        ViewState* views = &view;

        // Desktop has real worker threads: cesium-native parses the tileset in one, so the pump
        // has to yield or it spends its two hundred turns in a couple of milliseconds and gives
        // up before the parse finishes.
        bool rootAvailable = false;
        for (int i = 0; i < 200 && !rootAvailable; i++)
        {
            async.DispatchMainThreadTasks();
            ServeQueued();
            tileset.UpdateView(views, 1, 0.016f);
            rootAvailable = tileset.IsRootTileAvailable();
            if (!rootAvailable)
            {
                System.Threading.Thread.Sleep(5);
            }
        }

        Check(_beginCalls >= 1, $"the request reached the host ({_beginCalls} call(s))");
        Check(rootAvailable, "the root tile became available, so the answer reached the tileset");

        accessor.CancelAllRequests();
        for (int i = 0; i < 20; i++)
        {
            async.DispatchMainThreadTasks();
        }

        // Keeps the delegates alive to here. Without it nothing stops the collector from taking
        // them while native code still holds their addresses, and the crash would land far from
        // the cause.
        GC.KeepAlive(callbacks);

        Console.WriteLine(_failures == 0 ? "PASS" : $"FAIL ({_failures})");
        return _failures == 0 ? 0 : 1;
    }
}
