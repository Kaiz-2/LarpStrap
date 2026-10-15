using LarpStrap.Core.Logging;

namespace LarpStrap.Core.Instances;

public enum InstanceOrigin
{
    /// <summary>Started by this LarpStrap session.</summary>
    Launched,

    /// <summary>Already running, or started by something else (another launcher, Roblox itself).</summary>
    External,
}

public enum InstanceStatus
{
    Starting,
    Running,
    NotResponding,

    /// <summary>No window long after starting: Roblox's tray/background process. LarpStrap never acts on it.</summary>
    Background,
    Closed,

    /// <summary>Exited without LarpStrap asking it to (user closed it, or another tool). Not necessarily an error.</summary>
    ExitedUnexpectedly,

    /// <summary>Exited without LarpStrap asking it to, and Roblox wrote a crash report meanwhile (<see cref="CrashReports"/>).</summary>
    Crashed,
}

public static class InstanceStatusExtensions
{
    public static bool HasEnded(this InstanceStatus status) =>
        status is InstanceStatus.Closed or InstanceStatus.ExitedUnexpectedly or InstanceStatus.Crashed;
}

public sealed record TrackedInstance(
    int Pid,
    InstanceOrigin Origin,
    InstanceStatus Status,
    long? WorkingSetBytes = null,
    DateTime? StartTimeUtc = null)
{
    public bool IsActive => !Status.HasEnded();
    public bool HasWindow => Status is InstanceStatus.Running or InstanceStatus.NotResponding;
}

/// <summary>
/// Tracks each client independently by PID, polling the process provider (no held handles). Close requests and
/// exits are logged here, the one place every path (panel, close-and-launch, launch polling) goes through.
/// </summary>
/// <param name="crashedSince">Whether Roblox wrote a crash report at or after a UTC time; null when crashes can't be told apart (demo).</param>
public sealed class InstanceTracker(IProcessProvider processes, TimeProvider? time = null, AppLog? log = null, Func<DateTime, bool>? crashedSince = null)
{
    /// <summary>A window-less process older than this is treated as Roblox's background process, not a starting client.</summary>
    public static readonly TimeSpan BackgroundAfter = TimeSpan.FromSeconds(30);

    /// <summary>A window must stay hung this long before it shows as «No responde»: a loading screen often stalls for a moment.</summary>
    public static readonly TimeSpan NotRespondingAfter = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Lock _refreshGate = new();
    private readonly Dictionary<int, Entry> _entries = [];
    private long _launches;

    public void MarkLaunched(int pid)
    {
        lock (_gate)
            _entries[pid] = new Entry(InstanceOrigin.Launched) { Status = InstanceStatus.Starting, LaunchNumber = ++_launches, FirstSeenUtc = Now };
    }

    public IReadOnlyList<TrackedInstance> Refresh()
    {
        // The UI timer and a launch both refresh: one at a time, or an older snapshot applied after a newer one
        // brings back a client that was just closed (defect A of the /ship review).
        lock (_refreshGate)
            return RefreshSerialized();
    }

    private IReadOnlyList<TrackedInstance> RefreshSerialized()
    {
        // A launch can run while the snapshot is taken; clients marked after this point may be missing from it.
        var launchesBefore = Interlocked.Read(ref _launches);
        var snapshot = processes.FindClients().ToDictionary(c => c.Pid);

        lock (_gate)
        {
            // Ended instances are reported once, then forgotten.
            foreach (var pid in _entries.Where(e => e.Value.IsEnded).Select(e => e.Key).ToList())
                _entries.Remove(pid);

            foreach (var client in snapshot.Values)
            {
                if (!_entries.TryGetValue(client.Pid, out var entry))
                    _entries[client.Pid] = entry = new Entry(InstanceOrigin.External) { FirstSeenUtc = Now };
                entry.Status = StatusOf(client, entry);
                entry.WorkingSetBytes = client.WorkingSetBytes;
                entry.StartTimeUtc = client.StartTimeUtc;
            }

            foreach (var (pid, entry) in _entries.Where(e => !e.Value.IsEnded && !snapshot.ContainsKey(e.Key) && e.Value.LaunchNumber <= launchesBefore))
            {
                entry.Status = entry.CloseRequested ? InstanceStatus.Closed
                    : crashedSince?.Invoke(entry.StartTimeUtc ?? entry.FirstSeenUtc) == true ? InstanceStatus.Crashed
                    : InstanceStatus.ExitedUnexpectedly;
                log?.Write(LogLevel.Info, "Instance.Exited", ("pid", pid), ("result", entry.Status), ("origin", entry.Origin));
            }

            return [.. _entries.OrderBy(e => e.Key)
                .Select(e => new TrackedInstance(e.Key, e.Value.Origin, e.Value.Status, e.Value.WorkingSetBytes, e.Value.StartTimeUtc))];
        }
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private InstanceStatus StatusOf(ClientProcess client, Entry entry)
    {
        entry.UnrespondingSinceUtc = client is { HasWindow: true, Responding: false } ? entry.UnrespondingSinceUtc ?? Now : null;
        if (client.HasWindow)
            return Now - entry.UnrespondingSinceUtc >= NotRespondingAfter ? InstanceStatus.NotResponding : InstanceStatus.Running;

        // A client LarpStrap launched may load slowly; only external window-less processes can be the tray process.
        if (entry.Origin == InstanceOrigin.Launched)
            return InstanceStatus.Starting;

        var age = client.StartTimeUtc is { } started ? Now - started : TimeSpan.Zero;
        return age > BackgroundAfter ? InstanceStatus.Background : InstanceStatus.Starting;
    }

    public bool Close(int pid)
    {
        MarkCloseRequested(pid);
        var ok = processes.RequestClose(pid);
        log?.Write(ok ? LogLevel.Info : LogLevel.Warning, "Instance.CloseRequested", ("pid", pid), ("result", ok));
        return ok;
    }

    /// <summary>Terminates one client. The UI must obtain explicit user confirmation first.</summary>
    public bool ForceKill(int pid)
    {
        MarkCloseRequested(pid);
        var ok = processes.Kill(pid);
        log?.Write(ok ? LogLevel.Info : LogLevel.Warning, "Instance.ForceKill", ("pid", pid), ("result", ok));
        return ok;
    }

    public bool Focus(int pid) => processes.Focus(pid);

    private void MarkCloseRequested(int pid)
    {
        lock (_gate)
            if (_entries.TryGetValue(pid, out var entry))
                entry.CloseRequested = true;
    }

    private sealed class Entry(InstanceOrigin origin)
    {
        public InstanceOrigin Origin { get; } = origin;
        public InstanceStatus Status { get; set; }
        public bool CloseRequested { get; set; }
        public long LaunchNumber { get; init; }
        public long? WorkingSetBytes { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime FirstSeenUtc { get; init; }
        public DateTime? UnrespondingSinceUtc { get; set; }
        public bool IsEnded => Status.HasEnded();
    }
}
