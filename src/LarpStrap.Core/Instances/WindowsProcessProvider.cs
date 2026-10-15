using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LarpStrap.Core.Instances;

/// <summary>
/// Real process provider. Every Process object is disposed immediately, so no handle to a Roblox
/// client outlives a single call. It never reads client command lines (they carry the auth ticket).
/// </summary>
public sealed partial class WindowsProcessProvider : IProcessProvider
{
    private const string ClientProcessName = "RobloxPlayerBeta";
    private const int SwRestore = 9;

    public IReadOnlyList<ClientProcess> FindClients()
    {
        var clients = new List<ClientProcess>();
        foreach (var process in Process.GetProcessesByName(ClientProcessName))
        {
            using (process)
            {
                try { clients.Add(Snapshot(process)); }
                catch (InvalidOperationException) { /* exited while enumerating */ }
            }
        }
        return clients;
    }

    private static ClientProcess Snapshot(Process process)
    {
        var hasWindow = process.MainWindowHandle != IntPtr.Zero;
        return new ClientProcess(
            process.Id,
            hasWindow,
            Responding: !hasWindow || IsResponding(process.MainWindowHandle),
            WorkingSetBytes: TryRead<long?>(() => process.WorkingSet64, null),
            StartTimeUtc: TryRead<DateTime?>(() => process.StartTime.ToUniversalTime(), null));
    }

    // Process.Responding waits up to 5 s per hung window; a 500 ms probe keeps refreshes and launch polls responsive.
    private static bool IsResponding(IntPtr window) =>
        SendMessageTimeout(window, WmNull, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, 500, out _) != IntPtr.Zero;

    // Resource reads open a short-lived query handle; Windows may deny them, which only hides the value.
    private static T TryRead<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { return fallback; }
    }

    public int Start(string exePath, string arguments)
    {
        using var process = Process.Start(CreateStartInfo(exePath, arguments))
            ?? throw new InvalidOperationException("Process.Start no devolvió ningún proceso.");
        return process.Id;
    }

    // Shell execution creates the client without inheriting LarpStrap's standard handles; with
    // UseShellExecute=false a captured console stayed open until the client exited (found in a real run).
    internal static ProcessStartInfo CreateStartInfo(string exePath, string arguments) => new(exePath, arguments)
    {
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(exePath)!,
    };

    public bool IsRunning(int pid) => WithProcess(pid, p => !p.HasExited);

    public bool RequestClose(int pid) => WithProcess(pid, p => p.CloseMainWindow());

    public bool Kill(int pid) => WithProcess(pid, p =>
    {
        p.Kill(entireProcessTree: true); // also ends its RobloxCrashHandler child
        return true;
    });

    public bool Focus(int pid) => WithProcess(pid, p =>
    {
        var window = p.MainWindowHandle;
        if (window == IntPtr.Zero) return false;
        if (IsIconic(window)) ShowWindow(window, SwRestore);
        // Windows may refuse foreground changes (foreground lock); the caller reports that as "not focused".
        return SetForegroundWindow(window);
    });

    private static bool WithProcess(int pid, Func<Process, bool> action)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName.Equals(ClientProcessName, StringComparison.OrdinalIgnoreCase) && action(process);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private const uint WmNull = 0x0000;
    private const uint SmtoAbortIfHung = 0x0002;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);
}
