using System.IO;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.Core.Instances;
using LarpStrap.Core.Logging;

namespace LarpStrap.App.Tests;

// SIMULATED processes. Checks the log answers "what did LarpStrap do to my clients, and when did the panel go blind?"
public sealed class PanelLogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-panellog-").FullName;
    private readonly AppLog _log;
    private readonly ScriptedProvider _processes = new();

    public PanelLogTests()
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

    private InstancesViewModel ViewModel() => new(
        new InstancePanelService(_processes, new InstallationLocator([Path.Combine(_dir, "Versions")]), LaunchOptions.Default, isSimulated: true, log: _log),
        new AlwaysConfirm(), _log, _ => { });

    private string[] LogLines() =>
        LogFiles.ReadTail(LogFiles.List(Path.Combine(_dir, "Logs")).Single().FullName).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task A_failing_refresh_is_logged_once_and_its_recovery_with_the_failure_count()
    {
        var vm = ViewModel();
        _processes.Fail = true;
        for (var i = 0; i < 3; i++) await vm.RefreshCommand.ExecuteAsync(null);
        _processes.Fail = false;
        await vm.RefreshCommand.ExecuteAsync(null);

        var lines = LogLines();
        Assert.Single(lines, l => l.Contains("Panel.RefreshFailed"));
        Assert.Contains(lines, l => l.Contains("Panel.RefreshRecovered") && l.Contains("count=3"));
    }

    [Fact]
    public async Task Force_kill_is_logged_with_pid_and_result()
    {
        _processes.Clients = [new ClientProcess(7, HasWindow: true)];
        var vm = ViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.Items.Single().ForceKillCommand.ExecuteAsync(null);

        Assert.Contains(LogLines(), l => l.Contains("Instance.ForceKill") && l.Contains("pid=7") && l.Contains("result=True"));
    }

    [Fact]
    public async Task Close_request_is_logged_with_pid_and_result()
    {
        _processes.Clients = [new ClientProcess(7, HasWindow: true)];
        var vm = ViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.Items.Single().CloseCommand.ExecuteAsync(null);

        Assert.Contains(LogLines(), l => l.Contains("Instance.CloseRequested") && l.Contains("pid=7") && l.Contains("result=True"));
    }

    [Fact]
    public async Task A_client_that_exits_on_its_own_is_logged_as_exited_unexpectedly()
    {
        _processes.Clients = [new ClientProcess(7, HasWindow: true)];
        var vm = ViewModel();
        await vm.RefreshCommand.ExecuteAsync(null);
        _processes.Clients = [];

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Contains(LogLines(), l => l.Contains("Instance.Exited") && l.Contains("pid=7") && l.Contains("result=ExitedUnexpectedly"));
    }

    private sealed class ScriptedProvider : IProcessProvider
    {
        public ClientProcess[] Clients { get; set; } = [];
        public bool Fail { get; set; }

        public IReadOnlyList<ClientProcess> FindClients() => Fail ? throw new InvalidOperationException("simulated") : Clients;
        public int Start(string exePath, string arguments)
        {
            Clients = [.. Clients, new ClientProcess(100 + Clients.Length, HasWindow: true)];
            return Clients[^1].Pid;
        }

        public bool IsRunning(int pid) => Clients.Any(c => c.Pid == pid);

        public bool RequestClose(int pid)
        {
            Clients = [.. Clients.Where(c => c.Pid != pid)];
            return true;
        }

        public bool Kill(int pid) => true;
        public bool Focus(int pid) => true;
    }

    private sealed class AlwaysConfirm : IDialogService
    {
        public bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger) => true;
        public string? AskSavePath(string suggestedName, string filter) => null;
        public void OpenUri(Uri uri) { }
        public void OpenFolder(string path) { }
    }

    [Fact]
    public async Task Close_and_launch_logs_the_client_it_closes()
    {
        _processes.Clients = [new ClientProcess(7, HasWindow: true)];
        var vm = new InstancesViewModel(
            new InstancePanelService(_processes, new InstallationLocator([Path.Combine(_dir, "Versions")]), LaunchOptions.Default, isSimulated: false, log: _log),
            new AlwaysConfirm(), _log, _ => { });
        await vm.RefreshCommand.ExecuteAsync(null);

        await vm.LaunchCommand.ExecuteAsync(null);

        var lines = LogLines();
        Assert.Contains(lines, l => l.Contains("Instance.CloseRequested") && l.Contains("pid=7") && l.Contains("result=True"));
        Assert.Contains(lines, l => l.Contains("Instance.Exited") && l.Contains("pid=7") && l.Contains("result=Closed"));
    }
}
