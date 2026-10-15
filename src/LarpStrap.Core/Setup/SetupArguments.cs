namespace LarpStrap.Core.Setup;

/// <summary>
/// Command line of LarpStrap-Setup.exe. No arguments opens the wizard. Silent use, e.g. for testing or scripted installs:
/// <c>--silent [--dir &lt;carpeta&gt;] [--no-start-menu-shortcut] [--no-desktop-shortcut] [--launch]</c>, and
/// <c>--uninstall [--silent] [--dir &lt;carpeta&gt;]</c> (what Windows' «Desinstalar» runs), and
/// <c>--update &lt;pid&gt;</c> (what LarpStrap's updater runs: waits for that process to exit, installs silently keeping
/// the shortcuts the user had, and opens LarpStrap again; docs/adr/0009).
/// </summary>
public sealed record SetupArguments(bool Silent, bool Uninstall, string? Directory, bool StartMenuShortcut, bool DesktopShortcut, bool Launch,
    int? WaitForProcess = null)
{
    /// <summary>Process exit codes, for scripts and for Windows' uninstall entry.</summary>
    public const int Ok = 0, Failed = 1, InvalidArguments = 2, Elevated = 4, NoPayload = 5;

    /// <summary>Null when an argument is unknown, repeated or missing its value.</summary>
    public static SetupArguments? Parse(IReadOnlyList<string> args)
    {
        var parsed = new SetupArguments(false, false, null, true, true, false);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!seen.Add(arg)) return null;
            switch (arg.ToLowerInvariant())
            {
                case "--silent": parsed = parsed with { Silent = true }; break;
                case "--uninstall": parsed = parsed with { Uninstall = true }; break;
                case "--no-start-menu-shortcut": parsed = parsed with { StartMenuShortcut = false }; break;
                case "--no-desktop-shortcut": parsed = parsed with { DesktopShortcut = false }; break;
                case "--launch": parsed = parsed with { Launch = true }; break;
                case "--dir" when i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal):
                    parsed = parsed with { Directory = args[++i] };
                    break;
                case "--update" when i + 1 < args.Count && int.TryParse(args[i + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) && pid > 0:
                    parsed = parsed with { Silent = true, Launch = true, WaitForProcess = pid };
                    i++;
                    break;
                default: return null;
            }
        }
        // Install-only switches make no sense when uninstalling; an update keeps the shortcuts already chosen.
        if (parsed.Uninstall && (parsed.Launch || !parsed.StartMenuShortcut || !parsed.DesktopShortcut)) return null;
        if (parsed.WaitForProcess is not null && (parsed.Uninstall || parsed.Directory is not null || !parsed.StartMenuShortcut || !parsed.DesktopShortcut)) return null;
        return parsed;
    }
}
