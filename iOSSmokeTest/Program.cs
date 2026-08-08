// Proves that the package links on iOS, which nothing else in this repository can.
//
// The desktop legs load a library out of runtimes/<rid>/native at run time. iOS does not load
// anything: Apple only lets an application load dynamic libraries that ship inside its own bundle
// as frameworks, so CesiumC builds a static archive and the package's targets file links it into
// the executable. Two things have to be true for that to work, and neither is visible anywhere
// else:
//
//   the targets file adds the archive to the link, and
//   Native.Dll compiled to "__Internal" rather than "CesiumNativeC",
//
// because once linked the symbols live in the executable and only that name reaches them. Get
// either wrong and the build still succeeds; the failure arrives at the first P/Invoke, on a
// device, in somebody else's application.
//
// A build is the assertion here rather than a run. The linker resolving every P/Invoke against
// the archive is exactly the property under test, and it is checked at link time, so a successful
// build of this project is a real result and not a consolation prize. What it does not prove is
// behaviour, and the desktop and wasm legs already cover that against the same sources.

using Foundation;
using UIKit;
using Evergine.Bindings.CesiumNative;

namespace iOSSmokeTest;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // The same two the desktop leg starts with. These are what force the linker to keep the
        // members carrying them, which is why ForceLoad is set in the package's targets file.
        AsyncSystem async = AsyncSystem.Create();
        CreditSystem credits = CreditSystem.Create();

        bool ok = async.Handle != System.IntPtr.Zero && credits.Handle != System.IntPtr.Zero;

        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new UIViewController
            {
                View = { BackgroundColor = ok ? UIColor.SystemGreen : UIColor.SystemRed },
            },
        };
        Window.MakeKeyAndVisible();
        return true;
    }
}

public static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
