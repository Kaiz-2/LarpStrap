using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

// SIMULATED: process state comes from FakeProcessProvider, not from real Roblox clients.
public sealed class InstanceTrackerTests
{
    private readonly FakeProcessProvider _processes = new();

    [Fact]
    public void Discovers_clients_that_were_already_running_as_external()
    {
        var pid = _processes.AddExistingClient();

        var instances = new InstanceTracker(_processes).Refresh();

        var instance = Assert.Single(instances);
        Assert.Equal(pid, instance.Pid);
        Assert.Equal(InstanceOrigin.External, instance.Origin);
        Assert.Equal(InstanceStatus.Running, instance.Status);
    }

    [Fact]
    public void A_restarted_app_rediscovers_running_clients()
    {
        var first = new InstanceTracker(_processes);
        var launched = _processes.Start("RobloxPlayerBeta.exe", "--app");
        first.MarkLaunched(launched);

        var afterRestart = new InstanceTracker(_processes).Refresh();

        Assert.Contains(afterRestart, i => i.Pid == launched && i.Status is InstanceStatus.Running or InstanceStatus.Starting);
    }

    [Fact]
    public void Client_without_window_is_reported_as_starting()
    {
        _processes.AddExistingClient(hasWindow: false);

        var instance = Assert.Single(new InstanceTracker(_processes).Refresh());

        Assert.Equal(InstanceStatus.Starting, instance.Status);
    }

    [Fact]
    public void Unexpected_exit_is_reported_as_exited_not_closed()
    {
        var tracker = new InstanceTracker(_processes);
        var pid = _processes.AddExistingClient();
        tracker.Refresh();

        _processes.SimulateCrash(pid);
        var instance = Assert.Single(tracker.Refresh());

        Assert.Equal(InstanceStatus.ExitedUnexpectedly, instance.Status);
    }

    [Fact]
    public void An_exit_with_a_new_crash_report_is_reported_as_a_crash()
    {
        var started = DateTime.UtcNow.AddMinutes(-3);
        DateTime? askedSince = null;
        var tracker = new InstanceTracker(_processes, crashedSince: since => { askedSince = since; return true; });
        var pid = _processes.AddExistingClient(startTimeUtc: started);
        tracker.Refresh();

        _processes.SimulateCrash(pid);

        Assert.Equal(InstanceStatus.Crashed, Assert.Single(tracker.Refresh()).Status);
        Assert.Equal(started, askedSince); // only reports written after this client started count
    }

    [Fact]
    public void A_close_LarpStrap_asked_for_is_never_reported_as_a_crash()
    {
        var tracker = new InstanceTracker(_processes, crashedSince: _ => true);
        var pid = _processes.AddExistingClient();
        tracker.Refresh();

        tracker.Close(pid);
        _processes.SimulateCrash(pid);

        Assert.Equal(InstanceStatus.Closed, Assert.Single(tracker.Refresh()).Status);
    }

    [Fact]
    public void Closing_one_instance_leaves_the_others_running()
    {
        var tracker = new InstanceTracker(_processes);
        var a = _processes.AddExistingClient();
        var b = _processes.AddExistingClient();
        var c = _processes.AddExistingClient();
        tracker.Refresh();

        Assert.True(tracker.Close(b));
        var instances = tracker.Refresh();

        Assert.Equal([b], _processes.CloseRequests);
        Assert.Equal(InstanceStatus.Closed, instances.Single(i => i.Pid == b).Status);
        Assert.All(instances.Where(i => i.Pid != b), i => Assert.Equal(InstanceStatus.Running, i.Status));
        Assert.True(_processes.IsRunning(a) && _processes.IsRunning(c));
    }

    [Fact]
    public void Force_kill_targets_only_the_given_instance()
    {
        var tracker = new InstanceTracker(_processes);
        var a = _processes.AddExistingClient();
        var b = _processes.AddExistingClient();
        tracker.Refresh();

        tracker.ForceKill(a);

        Assert.Equal([a], _processes.Kills);
        Assert.True(_processes.IsRunning(b));
    }

    [Fact]
    public void Ended_instances_are_dropped_after_being_reported_once()
    {
        var tracker = new InstanceTracker(_processes);
        var pid = _processes.AddExistingClient();
        tracker.Refresh();
        _processes.SimulateCrash(pid);
        tracker.Refresh();

        Assert.Empty(tracker.Refresh());
    }

    [Fact]
    public void A_refresh_whose_snapshot_predates_a_launch_does_not_report_the_new_client_as_exited()
    {
        var racing = new SnapshotThenLaunchProvider(_processes);
        var tracker = new InstanceTracker(racing);
        racing.AfterSnapshot = () => tracker.MarkLaunched(_processes.Start("RobloxPlayerBeta.exe", "--app"));

        var stale = tracker.Refresh();
        var next = tracker.Refresh();

        Assert.DoesNotContain(stale, i => i.Status == InstanceStatus.ExitedUnexpectedly);
        Assert.Equal(InstanceOrigin.Launched, Assert.Single(next).Origin);
    }

    /// <summary>Starts a client between taking the process snapshot and applying it, as the UI refresh and a launch can.</summary>
    private sealed class SnapshotThenLaunchProvider(FakeProcessProvider inner) : IProcessProvider
    {
        public Action? AfterSnapshot { get; set; }

        public IReadOnlyList<ClientProcess> FindClients()
        {
            var snapshot = inner.FindClients();
            var after = AfterSnapshot;
            AfterSnapshot = null;
            after?.Invoke();
            return snapshot;
        }

        public int Start(string exePath, string arguments) => inner.Start(exePath, arguments);
        public bool IsRunning(int pid) => inner.IsRunning(pid);
        public bool RequestClose(int pid) => inner.RequestClose(pid);
        public bool Kill(int pid) => inner.Kill(pid);
        public bool Focus(int pid) => inner.Focus(pid);
    }

    [Fact]
    public async Task Overlapping_refreshes_never_resurrect_a_client_that_larpstrap_closed()
    {
        var pid = _processes.AddExistingClient();
        var slow = new HeldSnapshotProvider(_processes);
        var tracker = new InstanceTracker(slow);
        tracker.Refresh(); // the panel already lists the client, as it must before it can be closed
        slow.HoldNext = true;

        // A takes its snapshot (client still open) and is held before applying it; meanwhile LarpStrap closes the
        // client and B refreshes. Applying A after B used to re-add the closed client as a new external one.
        var a = Task.Run(tracker.Refresh, TestContext.Current.CancellationToken);
        slow.SnapshotTaken.Wait(TestContext.Current.CancellationToken);
        tracker.Close(pid);
        var b = Task.Run(tracker.Refresh, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        slow.Release.Set();
        await Task.WhenAll(a, b);
        var after = tracker.Refresh();

        Assert.DoesNotContain(after, i => i.Pid == pid && i.Status is InstanceStatus.Running or InstanceStatus.ExitedUnexpectedly);
        Assert.Contains(await b, i => i.Pid == pid && i.Status == InstanceStatus.Closed);
    }

    /// <summary>Once <see cref="HoldNext"/> is set, the next FindClients returns its snapshot only after <see cref="Release"/>.</summary>
    private sealed class HeldSnapshotProvider(FakeProcessProvider inner) : IProcessProvider
    {
        private int _hold;
        public bool HoldNext { set => Volatile.Write(ref _hold, value ? 1 : 0); }
        public ManualResetEventSlim SnapshotTaken { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public IReadOnlyList<ClientProcess> FindClients()
        {
            var snapshot = inner.FindClients();
            if (Interlocked.Exchange(ref _hold, 0) == 1)
            {
                SnapshotTaken.Set();
                Release.Wait(TimeSpan.FromSeconds(10));
            }
            return snapshot;
        }

        public int Start(string exePath, string arguments) => inner.Start(exePath, arguments);
        public bool IsRunning(int pid) => inner.IsRunning(pid);
        public bool RequestClose(int pid) => inner.RequestClose(pid);
        public bool Kill(int pid) => inner.Kill(pid);
        public bool Focus(int pid) => inner.Focus(pid);
    }
}
