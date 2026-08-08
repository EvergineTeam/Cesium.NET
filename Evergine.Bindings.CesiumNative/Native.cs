using System.Runtime.InteropServices;

namespace Evergine.Bindings.CesiumNative
{
    /// <summary>
    /// The name every <see cref="DllImportAttribute"/> in the generated code resolves against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On iOS the library is not loaded, it is linked. Apple only lets an application load
    /// dynamic libraries that ship inside its own bundle as frameworks, so CesiumC builds a
    /// static archive for that platform and the package links it into the application at build
    /// time. Once linked, the symbols live in the executable itself, and the name that reaches
    /// them is <c>__Internal</c> rather than the library's own.
    /// </para>
    /// <para>
    /// Everywhere else the library is loaded at run time out of <c>runtimes/&lt;rid&gt;/native</c>
    /// and the name is the file's. browser-wasm is the exception that proves the rule from the
    /// other side: it is also linked rather than loaded, but there the module name comes from the
    /// file name, which is why the package renames the archive to CesiumNativeC.a instead of
    /// switching this constant.
    /// </para>
    /// <para>
    /// Modelled on Evergine.Bindings.Vuforia, which is the one package in this fleet confirmed to
    /// work on iOS inside a real Evergine project.
    /// </para>
    /// </remarks>
    internal static class Native
    {
#if __IOS__
        public const string Dll = "__Internal";
#else
        public const string Dll = "CesiumNativeC";
#endif
    }
}
