using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

// SIMULATED: process state comes from FakeProcessProvider.
public sealed class InstanceStatusTests
{
    private readonly FakeProcessProvider _processes = new();

    [Fact]
    public void A_window_hung_for_the_grace_period_is_reported_as_not_responding()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var tracker = new InstanceTracker(_processes, time);
        _processes.AddExistingClient(responding: false);

        Assert.Equal(InstanceStatus.Running, Assert.Single(tracker.Refresh()).Status);
        time.Now += InstanceTracker.NotRespondingAfter;
        Assert.Equal(InstanceStatus.NotResponding, Assert.Single(tracker.Refresh()).Status);
    }

    [Fact]
    public void A_short_hang_that_recovers_restarts_the_grace_period()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var tracker = new InstanceTracker(_processes, time);
        var pid = _processes.AddExistingClient(responding: false);
        tracker.Refresh();

        time.Now += TimeSpan.FromSeconds(3);
        _processes.SetResponding(pid, true);
        tracker.Refresh();
        _processes.SetResponding(pid, false);
        tracker.Refresh();
        time.Now += TimeSpan.FromSeconds(3);

        Assert.Equal(InstanceStatus.Running, Assert.Single(tracker.Refresh()).Status);
    }

    [Fact]
    public void Old_window_less_process_is_reported_as_background()
    {
        _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow.AddMinutes(-5));

        Assert.Equal(InstanceStatus.Background, Assert.Single(new InstanceTracker(_processes).Refresh()).Status);
    }

    [Fact]
    public void Launched_client_without_window_stays_starting_even_after_the_background_threshold()
    {
        var tracker = new InstanceTracker(_processes);
        var pid = _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow.AddMinutes(-2));
        tracker.MarkLaunched(pid);

        Assert.Equal(InstanceStatus.Starting, Assert.Single(tracker.Refresh()).Status);
    }

    [Fact]
    public void Fresh_window_less_process_is_still_starting()
    {
        _processes.AddExistingClient(hasWindow: false, startTimeUtc: DateTime.UtcNow);

        Assert.Equal(InstanceStatus.Starting, Assert.Single(new InstanceTracker(_processes).Refresh()).Status);
    }
}
