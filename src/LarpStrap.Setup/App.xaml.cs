using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using LarpStrap.App.Services;
using LarpStrap.Core;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Settings;
using LarpStrap.Core.Setup;

namespace LarpStrap.Setup;

public partial class App : Application
{
    // First thing: this exe is usually run from Downloads, the folder where planted DLLs wait (NativeLibraryHardening).
    static App() => NativeLibraryHardening.Apply(typeof(App).Assembly);

    /// <summary>The LarpStrap version inside this installer (same build, Directory.Build.props).</summary>
    internal static Version Version { get; } = typeof(App).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    /// <summary>Running elevated would install into the administrator's profile and launch LarpStrap elevated (H-2): refused.</summary>
    internal static bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    internal static AppLog? Log { get; private set; }

    /// <summary>The app to install; null in a development build made without eng/Publish-Release.ps1.</summary>
    internal static Stream? OpenPayload() => typeof(App).Assembly.GetManifestResourceStream("LarpStrap.payload.zip");

    internal static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LarpStrap", "Logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { Log = new AppLog(LogDirectory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log = null; } // installing still works without a log

        var args = SetupArguments.Parse(e.Args);
        Log?.Write(LogLevel.Info, "Setup.Started", ("appVersion", Version), ("result", args is null ? "invalidArguments" : args.Uninstall ? "uninstall" : "install"));
        if (args is null)
        {
            MessageBox.Show("Argumentos no válidos. Uso: LarpStrap-Setup.exe [--silent] [--dir <carpeta>] [--no-start-menu-shortcut] [--no-desktop-shortcut] [--launch] | --uninstall [--silent] [--dir <carpeta>]",
                "Instalador de LarpStrap", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(SetupArguments.InvalidArguments);
            return;
        }
        if (args.Uninstall && RelaunchOutsideInstallation(args))
        {
            Shutdown(SetupArguments.Ok);
            return;
        }
        if (args.Silent)
        {
            Shutdown(RunSilent(args));
            return;
        }

        var theme = new ThemeService();
        theme.Apply(LarpStrap.Core.Settings.ThemeMode.System, AccentColor.Violet, WindowBackdrop.Solid);
        var window = new SetupWindow(args);
        window.SourceInitialized += (_, _) => theme.ApplyWindowFrame(window);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Windows' «Desinstalar» runs the copy inside the installation, which can't delete itself: it copies itself to a new
    /// folder in %TEMP%, starts that copy and exits. That copy stays in %TEMP% (documented).
    /// </summary>
    private static bool RelaunchOutsideInstallation(SetupArguments args)
    {
        var dir = args.Directory ?? Installer.FindExisting(InstallTarget.UserUninstallKey)?.Directory;
        var self = Environment.ProcessPath;
        if (dir is null || self is null || !Path.GetFullPath(self).StartsWith(Path.GetFullPath(dir).TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase))
            return false;

        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"LarpStrap-Uninstall-{Guid.NewGuid():N}")).FullName;
        var copy = Path.Combine(temp, InstallTarget.SetupExe);
        File.Copy(self, copy);
        var start = new ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = temp };
        start.ArgumentList.Add("--uninstall");
        start.ArgumentList.Add("--dir");
        start.ArgumentList.Add(Path.GetFullPath(dir));
        if (args.Silent) start.ArgumentList.Add("--silent");
        Process.Start(start)?.Dispose();
        return true;
    }

    private static int RunSilent(SetupArguments args)
    {
        if (IsElevated)
        {
            Log?.Write(LogLevel.Warning, "Setup.Refused", ("result", "elevated"));
            return SetupArguments.Elevated;
        }
        try
        {
            var existing = Installer.FindExisting(InstallTarget.UserUninstallKey);
            if (args.Uninstall)
            {
                var dir = args.Directory ?? existing?.Directory ?? throw new InstallerException("LarpStrap no está instalado.");
                var removed = new Installer(InstallTarget.ForCurrentUser(dir)).Uninstall();
                Log?.Write(LogLevel.Info, "Setup.Uninstalled", ("count", removed.FilesRemoved));
                return SetupArguments.Ok;
            }

            using var payload = OpenPayload();
            if (payload is null) return SetupArguments.NoPayload;
            var (startMenu, desktop) = (args.StartMenuShortcut, args.DesktopShortcut);
            if (args.WaitForProcess is { } pid)
            {
                // Started by LarpStrap's updater: only update an existing installation, after LarpStrap has closed.
                if (existing is null) throw new InstallerException("La actualización solo se aplica sobre una instalación existente.");
                WaitForExit(pid);
                var current = InstallTarget.ForCurrentUser(existing.Directory);
                startMenu = existing.Shortcuts.Contains(current.StartMenuShortcut, StringComparer.OrdinalIgnoreCase);
                desktop = existing.Shortcuts.Contains(current.DesktopShortcut, StringComparer.OrdinalIgnoreCase);
            }
            var target = existing?.Directory ?? args.Directory ?? InstallTarget.DefaultDirectory;
            if (existing is not null && args.Directory is { } wanted && !string.Equals(Path.GetFullPath(wanted).TrimEnd('\\'), Path.GetFullPath(existing.Directory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new InstallerException($"LarpStrap ya está instalado en {existing.Directory}. Desinstálalo antes de instalarlo en otra carpeta.");
            var problem = Installer.ValidateDirectory(target, existing) ?? Installer.CheckWritable(target, Installer.PayloadSize(payload));
            if (problem is not null) throw new InstallerException(problem);

            payload.Position = 0;
            var result = new Installer(InstallTarget.ForCurrentUser(target))
                .Install(payload, new InstallOptions(startMenu, desktop, Version, Environment.ProcessPath));
            Log?.Write(LogLevel.Info, "Setup.Installed", ("appVersion", Version), ("count", result.FilesVerified));
            if (args.Launch) LaunchApp(result.AppPath);
            return SetupArguments.Ok;
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            Log?.Write(LogLevel.Error, "Setup.Failed", ("exception", ex.InnerException ?? ex));
            return SetupArguments.Failed;
        }
    }

    /// <summary>Waits up to a minute for the LarpStrap that started the update to close; if it does not, the install refuses as usual.</summary>
    private static void WaitForExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (string.Equals(process.ProcessName, Path.GetFileNameWithoutExtension(InstallTarget.AppExe), StringComparison.OrdinalIgnoreCase))
                process.WaitForExit(TimeSpan.FromMinutes(1));
        }
        catch (ArgumentException) { } // already gone
    }

    /// <summary>Starts the installed app as the current (non-elevated) user.</summary>
    internal static void LaunchApp(string appPath) =>
        Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(appPath)! })?.Dispose();
}
