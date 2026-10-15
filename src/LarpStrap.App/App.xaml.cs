using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.Core;
using LarpStrap.Core.Instances;
using LarpStrap.Core.Integrations;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Settings;

namespace LarpStrap.App;

public partial class App : Application
{
    private AppLog? _log;
    private SingleInstance? _instance;
    private DiscordPresence? _discord;
    private Task _discordQueue = Task.CompletedTask; // updates run off the UI thread, one at a time and in order

    // Before WPF loads its native parts (DLL planting, see NativeLibraryHardening).
    static App() => NativeLibraryHardening.Apply(typeof(App).Assembly);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A launch link arrives as the first argument when the browser starts LarpStrap. Anything else is ignored.
        var link = e.Args.FirstOrDefault(RobloxUri.IsLaunchLink);
        _instance = SingleInstance.TryAcquire(SingleInstance.DefaultName);
        if (_instance is null)
        {
            // Already open: that LarpStrap opens the link (or just comes to the front) and this one exits.
            AllowSetForegroundWindow(AsfwAny);
            if (!SingleInstance.Send(SingleInstance.DefaultName, link ?? "", TimeSpan.FromSeconds(3)))
                MessageBox.Show("LarpStrap ya está abierto, pero no responde. Ciérralo desde el Administrador de tareas y vuelve a intentarlo.",
                    "LarpStrap", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LarpStrap");
        var logDir = Path.Combine(dataDir, "Logs");
        AppLog log;
        try
        {
            log = new AppLog(logDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked or redirected profile folder must not stop the app; keep logging somewhere writable.
            logDir = Path.Combine(Path.GetTempPath(), "LarpStrap", "Logs");
            log = new AppLog(logDir);
        }
        _log = log;
        log.Write(LogLevel.Info, "App.Started", ("appVersion", AboutViewModel.AppVersion));
        WatchUnhandledExceptions(log);

        var settings = SettingsService.Load(Path.Combine(dataDir, "Settings.json"), log);
        var theme = new ThemeService();
        theme.Apply(settings.Current.Theme, settings.Current.Accent, settings.Current.Backdrop, settings.Current.Logo,
            settings.Current.CustomAccentHex, settings.Current.ThemePreset);
        theme.WatchSystemTheme();
        var dialogs = new DialogService(theme);
        var locator = InstallationLocator.ForCurrentUser();
        var installation = locator.Find();
        log.Write(LogLevel.Info, "App.Configured", ("demoMode", settings.Current.DemoMode), ("theme", settings.Current.Theme),
            ("robloxVersion", installation?.VersionFolder));
        if (settings.RecoveredFromCorruptFile)
            log.Write(LogLevel.Warning, "Settings.RecoveredFromCorruptFile");

        var shell = new ShellViewModel
        {
            TextScale = settings.Current.TextScale,
            IsSimulated = settings.Current.DemoMode,
            IsAdvancedMode = settings.Current.AdvancedMode,
            IsNavCompact = settings.Current.CompactNavigation,
        };
        void Announce(string message) => shell.Announce(message);

        // Real launches only: demo mode never writes into a Roblox installation.
        void ApplyFastFlags(RobloxInstallation installation)
        {
            try
            {
                FastFlags.Apply(Path.GetDirectoryName(installation.PlayerPath)!, settings.Current.FastFlagValues);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Write(LogLevel.Warning, "FastFlags.ApplyFailed", ("exception", ex));
                Dispatcher.BeginInvoke(() => shell.Announce("No se pudieron aplicar los FastFlags: Roblox se abre con los anteriores."));
            }
        }

        var instances = new InstancesViewModel(CreatePanelService(settings.Current, locator, log, ApplyFastFlags), dialogs, log, Announce);
        shell.Launcher = instances;
        var storeLauncher = new RobloxStoreLauncher();
        var shortcuts = new ShortcutsViewModel(settings, dialogs, Announce, new ExperienceLookupService(), storeLauncher);
        // Order and groups of the navigation pane: play first, technical pages only in advanced mode, the rest at the bottom.
        shell.Add("Inicio", "\uE80F", new HomeViewModel(instances, shortcuts, shell), "_Inicio");
        shell.Add("Clientes abiertos", "\uE7FC", instances, "C_lientes abiertos");
        shell.Add("Accesos directos", "\uE734", shortcuts, "Accesos _directos");
        shell.Add("FastFlags", "\uE9E9", new FastFlagsViewModel(settings, dialogs, Announce), "_FastFlags", NavGroup.Advanced);
        shell.Add("Diagnóstico", "\uE9D9", new DiagnosticsViewModel(logDir, installation?.PlayerPath, dialogs, Announce), "Dia_gnóstico", NavGroup.Advanced);
        shell.Add("Dos cuentas", "\uE716", new AccountsViewModel(dialogs, storeLauncher), "Dos _cuentas");
        shell.Add("Personalización", "\uE790", new CustomizationViewModel(settings, Announce), "Pe_rsonalización", NavGroup.Footer);
        shell.Add("Ajustes", "\uE713", new SettingsViewModel(settings, dialogs, Announce), "A_justes", NavGroup.Footer);
        // Updates: Roblox counts as in use while a client is open or starting, during a launch, and until the first real
        // refresh (in demo mode the real clients are unknown). The background tray process does not count.
        PanelSnapshot? lastReal = null;
        var updater = new AppUpdater(log, Path.Combine(dataDir, "Updates"),
            () => instances.IsLaunching || lastReal is null
                || lastReal.Instances.Any(i => i.IsActive && i.Status != InstanceStatus.Background),
            Announce, Shutdown);
        var checkedAtStart = false;
        instances.Refreshed += (snapshot, simulated) =>
        {
            lastReal = simulated ? null : snapshot;
            if (simulated || checkedAtStart) return;
            checkedAtStart = true;
            if (settings.Current.AutoUpdate && AppUpdater.IsInstalled) _ = updater.CheckAsync(manual: false);
        };
        shell.Add("Acerca de", "\uE946", new AboutViewModel(settings, updater), "Acer_ca de", NavGroup.Footer);
        shell.Navigate(shell.Pages[0]);

        // The pane preferences are remembered between sessions.
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not (nameof(ShellViewModel.IsAdvancedMode) or nameof(ShellViewModel.IsNavCompact))) return;
            if (settings.Update(s => s with { AdvancedMode = shell.IsAdvancedMode, CompactNavigation = shell.IsNavCompact }) is { } error)
                shell.Announce(error);
        };

        var previous = settings.Current;
        settings.Changed += current =>
        {
            if (current.Theme != previous.Theme || current.Accent != previous.Accent || current.Backdrop != previous.Backdrop
                || current.Logo != previous.Logo || current.CustomAccentHex != previous.CustomAccentHex || current.ThemePreset != previous.ThemePreset)
                theme.Apply(current.Theme, current.Accent, current.Backdrop, current.Logo, current.CustomAccentHex, current.ThemePreset);
            shell.TextScale = current.TextScale;
            if (current.DemoMode != previous.DemoMode
                || current.LaunchTimeoutSeconds != previous.LaunchTimeoutSeconds)
            {
                log.Write(LogLevel.Info, "Panel.ModeChanged", ("demoMode", current.DemoMode));
                instances.UseService(CreatePanelService(current, locator, log, ApplyFastFlags));
                shell.IsSimulated = current.DemoMode;
            }
            previous = current;
        };

        var window = new MainWindow(shell);
        window.SourceInitialized += (_, _) => theme.ApplyWindowFrame(window);
        MainWindow = window;
        window.Show();
        instances.Start();

        WatchDiscord(instances, settings, log);
        _instance.Listen(message => Dispatcher.BeginInvoke(() => OnHandover(window, instances, log, message)));
        if (link is not null)
            OnLink(instances, log, link, "args");

        if (settings.RecoveredFromCorruptFile)
            shell.Announce("No se pudo leer la configuración guardada; se usan los valores por defecto. La copia dañada está junto a Settings.json (.bad).");
    }

    // A second LarpStrap start: bring this window to the front and open its link, if it brought one.
    private static void OnHandover(Window window, InstancesViewModel instances, AppLog log, string message)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        if (RobloxUri.IsLaunchLink(message))
            OnLink(instances, log, message, "pipe");
        else if (message.Length > 0)
            log.Write(LogLevel.Warning, "App.LinkRejected");
    }

    private static void OnLink(InstancesViewModel instances, AppLog log, string link, string origin)
    {
        log.Write(LogLevel.Info, "App.LinkReceived", ("origin", origin), ("placeId", RobloxUri.PlaceId(link)));
        _ = instances.LaunchLinkAsync(link);
    }

    // Rich Presence follows real clients only, and only while the user has it on with a valid application ID.
    private void WatchDiscord(InstancesViewModel instances, SettingsService settings, AppLog log)
    {
        DateTime? Since(PanelSnapshot snapshot, bool simulated) =>
            simulated || !settings.Current.DiscordPresence ? null
            : snapshot.Instances.Where(i => i.HasWindow).Select(i => i.StartTimeUtc ?? snapshot.RefreshedAtUtc).Order().Cast<DateTime?>().FirstOrDefault();

        instances.Refreshed += (snapshot, simulated) =>
        {
            var appId = settings.Current.DiscordAppId;
            if (_discord is not null && (appId is null || _discord.AppId != appId))
            {
                var old = _discord;
                _discord = null;
                _discordQueue = _discordQueue.ContinueWith(_ => old.Dispose(), TaskScheduler.Default);
            }
            if (appId is null || (_discord is null && !settings.Current.DiscordPresence)) return;
            var presence = _discord ??= new DiscordPresence(appId, log);
            var since = Since(snapshot, simulated);
            _discordQueue = _discordQueue.ContinueWith(_ => presence.Update(since), TaskScheduler.Default);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _discord?.Dispose();
        _instance?.Dispose();
        _log?.Dispose();
        base.OnExit(e);
    }

    // Without these, an unexpected exception closes the app and leaves nothing in the log (type only: messages may hold paths).
    private void WatchUnhandledExceptions(AppLog log)
    {
        DispatcherUnhandledException += (_, e) =>
        {
            log.Write(LogLevel.Error, "App.UnhandledException", ("exception", e.Exception));
            MessageBox.Show("LarpStrap encontró un error inesperado y se cerrará. Los detalles están en Diagnóstico.", "LarpStrap",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write(LogLevel.Error, "App.UnhandledException", ("exception", e.ExceptionObject as Exception));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write(LogLevel.Warning, "App.UnobservedTaskException", ("exception", e.Exception.InnerException ?? e.Exception));
            e.SetObserved();
        };
    }

    private const int AsfwAny = -1;

    // The browser-started copy owns the foreground right; it lends it so the open window can come to the front.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    private static InstancePanelService CreatePanelService(AppSettings settings, InstallationLocator locator, AppLog log, Action<RobloxInstallation> prepare) => settings.DemoMode
        // Demo: SIMULATED clients with short waits so every state can be seen quickly.
        ? new InstancePanelService(new SimulatedProcessProvider(), locator,
            new LaunchOptions(TimeSpan.FromSeconds(Math.Min(settings.LaunchTimeoutSeconds, 15)), TimeSpan.FromMilliseconds(300)),
            isSimulated: true, log: log)
        : new InstancePanelService(new WindowsProcessProvider(), locator,
            LaunchOptions.Default with { WindowTimeout = TimeSpan.FromSeconds(settings.LaunchTimeoutSeconds) },
            isSimulated: false, log: log, crashes: CrashReports.ForCurrentUser(), prepare: prepare);
}
