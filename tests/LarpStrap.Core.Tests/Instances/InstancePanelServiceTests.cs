using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

// SIMULATED: FakeProcessProvider / SimulatedProcessProvider, never real Roblox clients.
public sealed class InstancePanelServiceTests : IDisposable
{
    private readonly string _installDir = Directory.CreateTempSubdirectory("larpstrap-panel-").FullName;
    private readonly FakeProcessProvider _processes = new();

    public InstancePanelServiceTests()
    {
        var versionDir = Directory.CreateDirectory(Path.Combine(_installDir, "version-test"));
        File.WriteAllText(Path.Combine(versionDir.FullName, "RobloxPlayerBeta.exe"), "fake");
    }

    public void Dispose() => Directory.Delete(_installDir, recursive: true);

    private InstancePanelService Service() => new(
        _processes,
        new InstallationLocator([_installDir]),
        new LaunchOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(5)),
        isSimulated: true);

    [Fact]
    public async Task Close_and_launch_closes_the_open_client_then_starts_a_new_one()
    {
        var old = _processes.AddExistingClient();
        var service = Service();
        service.Refresh();

        var result = await service.CloseAndLaunchAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal([old], _processes.CloseRequests);
        Assert.Equal(LaunchOutcome.Started, result.Outcome);
        Assert.NotEqual(old, result.Pid);
    }

    [Fact]
    public async Task Close_and_launch_never_touches_the_background_process()
    {
        var background = _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow.AddMinutes(-10));
        var service = Service();

        var result = await service.CloseAndLaunchAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Empty(_processes.CloseRequests);
        Assert.Empty(_processes.Kills);
        Assert.True(_processes.IsRunning(background));
        Assert.Equal(LaunchOutcome.BlockedByBackground, result.Outcome);
    }

    [Fact]
    public async Task An_open_client_next_to_the_background_process_is_reported_as_already_open()
    {
        _processes.AddExistingClient();
        _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow.AddMinutes(-10));

        var result = await Service().LaunchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LaunchOutcome.AlreadyOpen, result.Outcome);
    }

    [Fact]
    public void Snapshot_carries_resources_and_refresh_time()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        _processes.AddExistingClient(workingSet: 1_000_000, startTimeUtc: started);

        var snapshot = Service().Refresh();

        var instance = Assert.Single(snapshot.Instances);
        Assert.Equal(1_000_000, instance.WorkingSetBytes);
        Assert.Equal(started, instance.StartTimeUtc);
        Assert.True(DateTime.UtcNow - snapshot.RefreshedAtUtc < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Simulated_demo_provider_shows_one_client_and_the_background_process()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var demo = new InstancePanelService(new SimulatedProcessProvider(), new InstallationLocator([_installDir]), LaunchOptions.Default, isSimulated: true, time);

        demo.Refresh();
        time.Now += InstanceTracker.NotRespondingAfter;
        var statuses = demo.Refresh().Instances.Select(i => i.Status).ToList();

        Assert.True(demo.IsSimulated);
        Assert.Equal([InstanceStatus.Running, InstanceStatus.Background], statuses);
    }

    [Fact]
    public async Task Cancelling_close_and_launch_while_waiting_returns_cancelled_instead_of_throwing()
    {
        _processes.AddExistingClient(ignoresClose: true);
        var service = Service();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var result = await service.CloseAndLaunchAsync(TimeSpan.FromSeconds(10), cts.Token);

        Assert.Equal(LaunchOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, _processes.StartCalls);
    }
}
