using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

// All tests in this file are SIMULATED (FakeProcessProvider): they check the launch logic, not a real Roblox client.
public sealed class LaunchOrchestratorTests : IDisposable
{
    private readonly string _installDir = Directory.CreateTempSubdirectory("larpstrap-install-").FullName;
    private readonly FakeProcessProvider _processes = new();

    public LaunchOrchestratorTests()
    {
        var versionDir = Directory.CreateDirectory(Path.Combine(_installDir, "version-test"));
        File.WriteAllText(Path.Combine(versionDir.FullName, "RobloxPlayerBeta.exe"), "fake");
    }

    public void Dispose() => Directory.Delete(_installDir, recursive: true);

    private static LaunchOptions FastOptions() => new(WindowTimeout: TimeSpan.FromMilliseconds(300), PollInterval: TimeSpan.FromMilliseconds(5));

    private LaunchOrchestrator Orchestrator(LaunchOptions options, string? installRoot = null, Action<RobloxInstallation>? prepare = null) =>
        new(_processes, new InstallationLocator([installRoot ?? _installDir]), new InstanceTracker(_processes), options, prepare);

    private static readonly LaunchRequest App = new("--app");

    [Fact]
    public async Task A_launch_starts_the_client_and_waits_for_its_window()
    {
        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.Started, result.Outcome);
        Assert.NotNull(result.Pid);
        Assert.Equal(1, _processes.StartCalls);
    }

    [Fact]
    public async Task The_installation_is_prepared_only_when_a_launch_happens()
    {
        var prepared = new List<int>();
        await Orchestrator(FastOptions(), prepare: _ => prepared.Add(_processes.StartCalls)).LaunchAsync(App, TestContext.Current.CancellationToken);
        Assert.Equal([0], prepared); // before the client starts

        prepared.Clear();
        await Orchestrator(FastOptions(), prepare: _ => prepared.Add(_processes.StartCalls)).LaunchAsync(App, TestContext.Current.CancellationToken);
        Assert.Empty(prepared); // a client is already open: nothing launched, nothing written
    }

    [Fact]
    public async Task Missing_installation_starts_nothing()
    {
        var result = await Orchestrator(FastOptions(), Path.Combine(_installDir, "does-not-exist")).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.InstallationNotFound, result.Outcome);
        Assert.Equal(0, _processes.StartCalls);
    }

    [Fact]
    public async Task An_open_client_means_nothing_is_launched()
    {
        _processes.AddExistingClient();

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.AlreadyOpen, result.Outcome);
        Assert.Equal(0, _processes.StartCalls);
    }

    [Fact]
    public async Task Only_the_background_process_blocks_with_its_own_outcome()
    {
        _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow.AddMinutes(-10));

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.BlockedByBackground, result.Outcome);
        Assert.Equal(0, _processes.StartCalls);
    }

    [Fact]
    public async Task Window_that_never_appears_times_out()
    {
        _processes.NextStartNeverShowsWindow();

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.TimedOut, result.Outcome);
        Assert.NotNull(result.Pid);
    }

    [Fact]
    public async Task Process_that_exits_during_launch_is_reported_with_diagnostics()
    {
        _processes.NextStartExitsAfter(1);

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.ExitedDuringLaunch, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
    }

    [Fact]
    public async Task Start_failure_is_reported_with_exception_type()
    {
        _processes.NextStartThrows();

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.StartFailed, result.Outcome);
        Assert.Contains("Win32Exception", result.Diagnostic);
    }

    [Fact]
    public async Task Cancelling_does_not_kill_a_client_that_already_started()
    {
        _processes.NextStartNeverShowsWindow();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var result = await Orchestrator(FastOptions() with { WindowTimeout = TimeSpan.FromSeconds(10) }).LaunchAsync(App, cts.Token);

        Assert.Equal(LaunchOutcome.Cancelled, result.Outcome);
        Assert.Empty(_processes.Kills);
        Assert.True(_processes.IsRunning(result.Pid!.Value));
    }

    [Fact]
    public async Task Cancelled_before_starting_launches_nothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await Orchestrator(FastOptions()).LaunchAsync(App, cts.Token);

        Assert.Equal(LaunchOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, _processes.StartCalls);
    }
}
