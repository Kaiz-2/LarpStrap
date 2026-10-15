using System.Reflection;
using System.Runtime.InteropServices;

namespace LarpStrap.Core;

/// <summary>
/// DLL planting: dwmapi.dll is not a KnownDLL, and a copy planted next to the exe (e.g. in Downloads) was loaded in the
/// phase 6 security review. Call first thing, before WPF loads its native parts, from every exe (app and installer),
/// so name-based loads come from System32.
/// </summary>
public static class NativeLibraryHardening
{
    private const uint LoadLibrarySearchUserDirs = 0x400;
    private const uint LoadLibrarySearchSystem32 = 0x800;

    public static void Apply(Assembly assembly)
    {
        // Self-contained builds probe the exe's folder for P/Invokes whatever the search flags say: resolve them explicitly.
        NativeLibrary.SetDllImportResolver(assembly, (name, _, _) => NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, name)));
        SetDefaultDllDirectories(LoadLibrarySearchSystem32 | LoadLibrarySearchUserDirs);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);
}
