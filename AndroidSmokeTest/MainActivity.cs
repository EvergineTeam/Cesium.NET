// Exists so the package has to be consumed by something that really builds for Android.
//
// It is not run. No GitHub runner can execute an APK without an emulator, and standing one up per
// release is a cost the fleet decided against. What the CI leg asserts instead is that
// libCesiumNativeC.so ends up inside the APK under lib/arm64-v8a/ -- which is the thing that was
// actually broken in Evergine.Bindings.JoltPhysics for months, and the thing a build that merely
// compiles would not notice.
//
// The calls below are here so the binding assembly is genuinely referenced rather than trimmed
// away as unused. They run on a device and never in CI.

using Android.App;
using Android.OS;
using Android.Widget;
using Evergine.Bindings.CesiumNative;

namespace AndroidSmokeTest;

[Activity(Label = "Cesium smoke", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The same two the desktop leg starts with. Reaching them at all means the native library
        // was found and loaded out of the APK.
        AsyncSystem async = AsyncSystem.Create();
        CreditSystem credits = CreditSystem.Create();

        bool ok = async.Handle != System.IntPtr.Zero && credits.Handle != System.IntPtr.Zero;

        var text = new TextView(this)
        {
            Text = ok
                ? $"CesiumNativeC loaded: async=0x{async.Handle:x} credits=0x{credits.Handle:x}"
                : "CesiumNativeC did not load",
        };

        SetContentView(text);
    }
}
