namespace LarpStrap.Core.Setup;

/// <summary>
/// Command line of LarpStrap-Setup.exe. No arguments opens the wizard. Silent use, e.g. for testing or scripted installs:
/// <c>--silent [--dir &lt;carpeta&gt;] [--no-start-menu-shortcut] [--no-desktop-shortcut] [--launch]</c>, and
/// <c>--uninstall [--silent] [--dir &lt;carpeta&gt;]</c> (what Windows' «Desinstalar» runs).
/// </summary>
public sealed record SetupArguments(bool Silent, bool Uninstall, string? Directory, bool StartMenuShortcut, bool DesktopShortcut, bool Launch)
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
                default: return null;
            }
        }
        // Install-only switches make no sense when uninstalling.
        return parsed.Uninstall && (parsed.Launch || !parsed.StartMenuShortcut || !parsed.DesktopShortcut) ? null : parsed;
    }
}
