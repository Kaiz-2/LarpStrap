using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.RealClient;

// REAL-CLIENT tests, read-only: they inspect the real machine but never start, close or kill a client.
// Skipped unless LARPSTRAP_REAL_CLIENT=1, so CI and normal runs stay deterministic.
public sealed class ReadOnlyRealClientTests(ITestOutputHelper output)
{
    private static void RequireOptIn() =>
        Assert.SkipUnless(Environment.GetEnvironmentVariable("LARPSTRAP_REAL_CLIENT") == "1", "Set LARPSTRAP_REAL_CLIENT=1 to run real-client checks.");

    [Fact]
    public void Real_installation_detection_reports_what_it_finds()
    {
        RequireOptIn();

        var found = InstallationLocator.ForCurrentUser().Find();

        output.WriteLine(found is null ? "No installation found" : $"Found player in folder {found.VersionFolder}");
        Assert.NotNull(found);
        Assert.True(File.Exists(found.PlayerPath));
    }

    [Fact]
    public void Real_discovery_lists_running_clients_as_external()
    {
        RequireOptIn();
        var processes = new WindowsProcessProvider();

        var instances = new InstanceTracker(processes).Refresh();

        output.WriteLine($"Running clients: {instances.Count}");
        foreach (var i in instances)
            output.WriteLine($"pid={i.Pid} origin={i.Origin} status={i.Status}");
        Assert.All(instances, i => Assert.Equal(InstanceOrigin.External, i.Origin));
        Assert.All(instances, i => Assert.True(processes.IsRunning(i.Pid)));
    }
}
