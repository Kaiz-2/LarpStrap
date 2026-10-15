using System.IO;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.Core.Instances;
using LarpStrap.Core.Logging;

namespace LarpStrap.App.Tests;

// SIMULATED: process state comes from in-memory providers, never from real Roblox clients.
public sealed class InstancesViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-vm-").FullName;
    private readonly AppLog _log;

    public InstancesViewModelTests()
    {
        var versionDir = Directory.CreateDirectory(Path.Combine(_dir, "Versions", "version-test"));
        File.WriteAllText(Path.Combine(versionDir.FullName, InstallationLocator.PlayerExecutable), "fake");
        _log = new AppLog(Path.Combine(_dir, "Logs"));
    }

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private InstancePanelService Service(IProcessProvider processes, bool simulated, LaunchOptions? options = null) => new(
        processes,
        new InstallationLocator([Path.Combine(_dir, "Versions")]),
        options ?? LaunchOptions.Default,
        isSimulated: simulated);

    private InstancesViewModel ViewModel(InstancePanelService service, bool confirm = false) => new(service, new NoDialogs { Answer = confirm }, _log, _ => { });

    [Fact]
    public async Task Switching_to_demo_mode_during_a_refresh_never_shows_real_clients_as_simulated()
    {
        var real = new GatedProvider(new ClientProcess(7, HasWindow: true));
        var vm = ViewModel(Service(real, simulated: false));

        var pending = vm.RefreshCommand.ExecuteAsync(null);
        real.Entered.Wait(TestContext.Current.CancellationToken);
        vm.UseService(Service(new GatedProvider(), simulated: true));
        real.Release.Set();
        await pending;

        Assert.DoesNotContain(vm.Items, i => i.Pid == 7);
    }

    [Fact]
    public async Task One_open_client_turns_the_primary_action_into_close_and_launch()
    {
        var vm = ViewModel(Service(new GatedProvider(new ClientProcess(7, HasWindow: true)) { Open = true }, simulated: false));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Cerrar y lanzar…", vm.PrimaryActionText);
        Assert.True(vm.LaunchCommand.CanExecute(null));
    }

    [Fact]
    public async Task Only_the_background_process_disables_launch()
    {
        var tray = new ClientProcess(9, HasWindow: false, StartTimeUtc: DateTime.UtcNow.AddMinutes(-10));
        var vm = ViewModel(Service(new GatedProvider(tray) { Open = true }, simulated: false));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.Contains("segundo plano", vm.LaunchHint);
    }

    /// <summary>Returns fixed clients; FindClients blocks until <see cref="Release"/> unless <see cref="Open"/>.</summary>
    private sealed class GatedProvider(params ClientProcess[] clients) : IProcessProvider
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();
        public bool Open { get; init; }

        public IReadOnlyList<ClientProcess> FindClients()
        {
            Entered.Set();
            if (!Open) Release.Wait(TimeSpan.FromSeconds(10));
            return clients;
        }

        public int Start(string exePath, string arguments) => throw new InvalidOperationException("not used");
        public bool IsRunning(int pid) => clients.Any(c => c.Pid == pid);
        public bool RequestClose(int pid) => false;
        public bool Kill(int pid) => false;
        public bool Focus(int pid) => false;
    }

    private sealed class NoDialogs : IDialogService
    {
        public bool Answer { get; init; }
        public bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger) => Answer;
        public string? AskSavePath(string suggestedName, string filter) => null;
        public void OpenUri(Uri uri) { }
        public void OpenFolder(string path) { }
    }

    [Fact]
    public async Task A_launch_interrupted_by_a_mode_switch_keeps_the_label_of_the_mode_it_ran_in()
    {
        var real = new NeverShowsWindowProvider();
        var vm = ViewModel(Service(real, simulated: false));
        await vm.RefreshCommand.ExecuteAsync(null);

        var launch = vm.LaunchCommand.ExecuteAsync(null);
        real.Started.Wait(TestContext.Current.CancellationToken);
        vm.UseService(Service(new GatedProvider() { Open = true }, simulated: true));
        await launch;

        Assert.False(Assert.Single(vm.Activity).IsSimulated);
    }

    /// <summary>A real-mode client that starts but never shows a window (the launch keeps polling until cancelled).</summary>
    private sealed class NeverShowsWindowProvider : IProcessProvider
    {
        private ClientProcess[] _clients = [];
        public ManualResetEventSlim Started { get; } = new();

        public IReadOnlyList<ClientProcess> FindClients() => _clients;

        public int Start(string exePath, string arguments)
        {
            _clients = [new ClientProcess(55, HasWindow: false, StartTimeUtc: DateTime.UtcNow)];
            Started.Set();
            return 55;
        }

        public bool IsRunning(int pid) => _clients.Any(c => c.Pid == pid);
        public bool RequestClose(int pid) => false;
        public List<int> Kills { get; } = [];

        public bool Kill(int pid)
        {
            Kills.Add(pid);
            _clients = [.. _clients.Where(c => c.Pid != pid)];
            return true;
        }

        public bool Focus(int pid) => false;
    }

    [Fact]
    public async Task A_launched_client_that_never_shows_a_window_can_be_force_closed_and_blocks_another_launch()
    {
        var real = new NeverShowsWindowProvider();
        var vm = ViewModel(Service(real, simulated: false,
            new LaunchOptions(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20))), confirm: true);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.LaunchCommand.ExecuteAsync(null); // times out: the process stays open without a window

        var row = Assert.Single(vm.Items);
        Assert.Equal(InstanceStatus.Starting, row.Status);
        Assert.True(row.CanForceKill);
        Assert.False(row.CanAct);
        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.Contains("Forzar cierre", vm.LaunchHint);

        await row.ForceKillCommand.ExecuteAsync(null);

        Assert.Equal([55], real.Kills);
    }

    [Fact]
    public async Task An_external_window_less_process_is_never_offered_force_close()
    {
        var vm = ViewModel(Service(new GatedProvider(new ClientProcess(9, HasWindow: false, StartTimeUtc: DateTime.UtcNow)) { Open = true }, simulated: false));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(vm.Items).CanForceKill);
    }

    [Fact]
    public async Task A_simulated_launch_is_announced_as_simulated()
    {
        var announced = new List<string>();
        var vm = new InstancesViewModel(Service(new StartsWithWindowProvider(), simulated: true), new NoDialogs(), _log, announced.Add);
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.LaunchCommand.ExecuteAsync(null);

        // The status bar is a live region: a screen reader must not hear a simulated launch as a real one.
        Assert.All(announced, a => Assert.Contains("simulado", a, StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(announced);
    }

    /// <summary>Starts one client that shows its window straight away.</summary>
    private sealed class StartsWithWindowProvider : IProcessProvider
    {
        private ClientProcess[] _clients = [];

        public IReadOnlyList<ClientProcess> FindClients() => _clients;

        public string? LastArguments { get; private set; }

        public int Start(string exePath, string arguments)
        {
            LastArguments = arguments;
            _clients = [new ClientProcess(77, HasWindow: true)];
            return 77;
        }

        public bool IsRunning(int pid) => _clients.Any(c => c.Pid == pid);
        public bool RequestClose(int pid) => false;
        public bool Kill(int pid) => false;
        public bool Focus(int pid) => false;
    }

    private const string Link = "roblox-player:1+launchmode:play+gameinfo:SECRET-TICKET+placelauncherurl:https%3A%2F%2Fwww.roblox.com%2FGame%2FPlaceLauncher.ashx%3FplaceId%3D920587237";

    [Fact]
    public async Task A_browser_link_reaches_the_client_as_one_argument_and_its_ticket_never_reaches_the_log()
    {
        var provider = new StartsWithWindowProvider();
        var vm = ViewModel(Service(provider, simulated: false));
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.LaunchLinkAsync(Link);

        Assert.Equal($"\"{Link}\"", provider.LastArguments);
        _log.Dispose();
        var logged = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(_dir, "Logs"))));
        Assert.Contains("launchMode=link", logged);
        Assert.Contains("placeId=920587237", logged);
        Assert.DoesNotContain("SECRET-TICKET", logged);
    }

    [Fact]
    public async Task A_browser_link_that_arrives_mid_launch_is_reported_not_queued()
    {
        var real = new NeverShowsWindowProvider();
        var vm = ViewModel(Service(real, simulated: false));
        await vm.RefreshCommand.ExecuteAsync(null);
        var launch = vm.LaunchCommand.ExecuteAsync(null);
        real.Started.Wait(TestContext.Current.CancellationToken);

        await vm.LaunchLinkAsync(Link);

        Assert.Equal("No se abrió el juego del navegador.", vm.LastActivity?.Summary);
        vm.CancelLaunchCommand.Execute(null);
        await launch;
    }

    [Fact]
    public async Task The_home_headline_follows_the_real_state_and_never_promises_a_launch_it_cannot_do()
    {
        var idle = ViewModel(Service(new GatedProvider() { Open = true }, simulated: false));
        await idle.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Listo para jugar", idle.StatusHeadline);

        var open = ViewModel(Service(new GatedProvider(new ClientProcess(7, HasWindow: true)) { Open = true }, simulated: false));
        await open.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Roblox está abierto", open.StatusHeadline);

        var tray = ViewModel(Service(new GatedProvider(new ClientProcess(9, HasWindow: false, StartTimeUtc: DateTime.UtcNow.AddMinutes(-10))) { Open = true }, simulated: false));
        await tray.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Roblox está en segundo plano", tray.StatusHeadline);

        var missing = new InstancesViewModel(
            new InstancePanelService(new GatedProvider() { Open = true }, new InstallationLocator([Path.Combine(_dir, "nowhere")]), LaunchOptions.Default, isSimulated: false),
            new NoDialogs(), _log, _ => { });
        Assert.Equal("Roblox no encontrado", missing.StatusHeadline);
    }
}
