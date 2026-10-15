using System.Diagnostics;
using LarpStrap.Core.Logging;

namespace LarpStrap.Core.Instances;

public sealed record PanelSnapshot(IReadOnlyList<TrackedInstance> Instances, DateTime RefreshedAtUtc);

/// <summary>
/// Application service behind the instance panel: everything the UI can do to clients goes through here,
/// so view models stay free of process logic. <see cref="IsSimulated"/> marks demo data that must never
/// be presented as real clients.
/// </summary>
public sealed class InstancePanelService
{
    /// <summary>Opens the Roblox app home. Verified with a real client on 0.742 (AT-18a).</summary>
    public const string AppLaunchArguments = "--app";

    private readonly IProcessProvider _processes;
    private readonly InstallationLocator _locator;
    private readonly InstanceTracker _tracker;
    private readonly LaunchOrchestrator _orchestrator;
    private readonly TimeProvider _time;

    public InstancePanelService(IProcessProvider processes, InstallationLocator locator, LaunchOptions options, bool isSimulated, TimeProvider? time = null, AppLog? log = null,
        CrashReports? crashes = null, Action<RobloxInstallation>? prepare = null)
    {
        _processes = processes;
        _locator = locator;
        _time = time ?? TimeProvider.System;
        _tracker = new InstanceTracker(processes, _time, log, crashes is null ? null : crashes.AnySince);
        _orchestrator = new LaunchOrchestrator(processes, locator, _tracker, options, prepare);
        Options = options;
        IsSimulated = isSimulated;
    }

    public bool IsSimulated { get; }
    public LaunchOptions Options { get; }

    public RobloxInstallation? FindInstallation() => _locator.Find();

    public PanelSnapshot Refresh() => new(_tracker.Refresh(), _time.GetUtcNow().UtcDateTime);

    public Task<LaunchResult> LaunchAsync(CancellationToken ct) => _orchestrator.LaunchAsync(new LaunchRequest(AppLaunchArguments), ct);

    /// <summary>Opens a launch link from the browser (<see cref="RobloxUri"/>): the link goes to the client only.</summary>
    public Task<LaunchResult> LaunchLinkAsync(string link, CancellationToken ct) =>
        _orchestrator.LaunchAsync(new LaunchRequest(RobloxUri.ToArguments(link)), ct);

    /// <summary>
    /// Asks every windowed client to close, waits for them to exit, then launches one. Roblox's background
    /// (tray) process is never touched; if it alone blocks the launch, the result says so. With a <paramref name="link"/>,
    /// the new client opens that game.
    /// </summary>
    public async Task<LaunchResult> CloseAndLaunchAsync(TimeSpan closeTimeout, CancellationToken ct, string? link = null)
    {
        var windowed = _tracker.Refresh().Where(i => i.HasWindow).Select(i => i.Pid).ToList();
        foreach (var pid in windowed)
            _tracker.Close(pid);

        var clock = Stopwatch.StartNew();
        while (windowed.Any(_processes.IsRunning))
        {
            if (clock.Elapsed >= closeTimeout)
                return new LaunchResult(LaunchOutcome.CloseTimedOut, null, clock.Elapsed,
                    $"El cliente abierto no se cerró en {closeTimeout.TotalSeconds:0} s; no se lanzó uno nuevo.");
            // Same contract as LaunchAsync: cancelling is a result, not an exception.
            if (!await LaunchOrchestrator.TryDelayAsync(Options.PollInterval, ct))
                return new LaunchResult(LaunchOutcome.Cancelled, null, clock.Elapsed, "Cancelado mientras se esperaba el cierre del cliente abierto.");
        }

        return link is null ? await LaunchAsync(ct) : await LaunchLinkAsync(link, ct);
    }

    public bool Focus(int pid) => _tracker.Focus(pid);

    public bool Close(int pid) => _tracker.Close(pid);

    /// <summary>Terminates one client. The UI must obtain explicit user confirmation first.</summary>
    public bool ForceKill(int pid) => _tracker.ForceKill(pid);
}
