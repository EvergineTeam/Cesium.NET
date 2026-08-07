// Proves that the published package works from a .NET wasm application.
//
// The point is not to exercise the API broadly -- CesiumC has 71 tests for that, in C++. What
// this checks is the part those tests cannot reach: that the archive inside the NuGet links
// into a .NET wasm module, that every DllImport resolves against it, and that a callback
// implemented in C# is reached from native code and its answer arrives back in the tileset.
//
// The callbacks are [UnmanagedCallersOnly] static methods rather than delegates, and that is
// not a style choice. On browser-wasm the runtime refuses a managed method reached from native
// code unless it carries that attribute: AOT has to know the callback at build time to place it
// in the table. Marshal.GetFunctionPointerForDelegate, which the generated
// AssetAccessorCallbacksSet.ToNative helper uses, dies with
//
//     No native to managed transition for method ..., missing [UnmanagedCallersOnly] attribute
//
// so that helper is desktop-only and a wasm host has to fill the struct itself.
//
// No network. HttpClient under node is a separate unknown, and mixing two unverified things in
// one test means learning nothing when it fails. The host here serves a canned document from
// memory, which is exactly the shape a real host has -- only the source of the bytes differs.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Evergine.Bindings.CesiumNative;

internal static unsafe class Program
{
    // A 3D Tiles document whose root has no content, so loading it is exactly one request.
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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnBeginRequest(
        void* userData, ulong requestId, byte* method, byte* url,
        HttpHeader* headers, int headerCount, byte* body, nuint bodySize)
    {
        _beginCalls++;
        string requested = Marshal.PtrToStringUTF8((IntPtr)url) ?? "";
        Console.WriteLine($"  host: request {requestId} for {requested}");

        // Answering from inside this callback is allowed -- the accessor marshals the
        // resolution -- but queueing is what a real host does, so that is what is exercised.
        Queued.Add(requestId);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnCancelRequest(void* userData, ulong requestId)
    {
    }

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
        Console.WriteLine("CesiumNativeC under browser-wasm");

        // Reaching native code at all. If the archive did not link, or a DllImport did not
        // resolve against it, this is where the module dies.
        AsyncSystem async = AsyncSystem.Create();
        Check(async.Handle != IntPtr.Zero, "the async system was created, so the archive linked");

        // Filled by hand rather than through AssetAccessorCallbacksSet, for the reason at the
        // top of the file.
        var native = new AssetAccessorCallbacks
        {
            UserData = null,
            BeginRequest = (IntPtr)(delegate* unmanaged[Cdecl]<
                void*, ulong, byte*, byte*, HttpHeader*, int, byte*, nuint, void>)&OnBeginRequest,
            CancelRequest = (IntPtr)(delegate* unmanaged[Cdecl]<void*, ulong, void>)&OnCancelRequest,
            Tick = IntPtr.Zero,
            Destroy = IntPtr.Zero,
            AllowBeginRequestOnWorkerThread = 0,
        };

        AssetAccessor accessor = AssetAccessor.CreateFromCallbacks(&native);
        Check(accessor.Handle != IntPtr.Zero, "the host accessor was created");

        CreditSystem credits = CreditSystem.Create();
        TilesetExternals externals = TilesetExternals.Create(async, accessor, credits);
        TilesetOptions options = TilesetOptions.Create();
        Tileset tileset = Tileset.CreateFromUrl(externals, TilesetUrl, options);
        Check(tileset.Handle != IntPtr.Zero, "the tileset was created");

        // A view looking straight down at the region the document describes, so the root is
        // worth loading.
        var view = new ViewState();
        ViewState* views = &view;

        bool rootAvailable = false;
        for (int i = 0; i < 200 && !rootAvailable; i++)
        {
            async.DispatchMainThreadTasks();
            ServeQueued();
            tileset.UpdateView(views, 1, 0.016f);
            rootAvailable = tileset.IsRootTileAvailable();
        }

        Check(_beginCalls >= 1, $"the request reached the host ({_beginCalls} call(s))");
        Check(rootAvailable, "the root tile became available, so the answer reached the tileset");

        // Cancel, pump, then let go: an outstanding request holds the accessor alive, so the
        // destructor cannot be what cancels. Documented on the C function for the same reason.
        accessor.CancelAllRequests();
        for (int i = 0; i < 20; i++)
        {
            async.DispatchMainThreadTasks();
        }

        Console.WriteLine(_failures == 0 ? "PASS" : $"FAIL ({_failures})");
        return _failures == 0 ? 0 : 1;
    }
}
