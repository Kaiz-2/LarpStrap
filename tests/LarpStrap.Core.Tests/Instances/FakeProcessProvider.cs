using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

/// <summary>
/// SIMULATED process provider. Nothing here starts a real Roblox client; results obtained with it
/// never count as real-client verification.
/// </summary>
internal sealed class FakeProcessProvider : IProcessProvider
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, FakeProcess> _processes = [];
    private readonly Queue<Func<FakeProcess>> _startScript = new();
    private int _nextPid = 1000;

    public List<int> CloseRequests { get; } = [];
    public List<int> Kills { get; } = [];
    public int StartCalls { get; private set; }

    /// <summary>A client that was already running before LarpStrap started.</summary>
    public int AddExistingClient(bool hasWindow = true, bool responding = true, DateTime? startTimeUtc = null, long? workingSet = null, bool ignoresClose = false)
    {
        lock (_gate)
        {
            var pid = _nextPid++;
            _processes[pid] = new FakeProcess(pid) { HasWindow = hasWindow, Responding = responding, StartTimeUtc = startTimeUtc, WorkingSet = workingSet, IgnoresClose = ignoresClose };
            return pid;
        }
    }

    /// <summary>Next Start() shows a window after <paramref name="polls"/> FindClients calls.</summary>
    public void NextStartShowsWindowAfter(int polls) => _startScript.Enqueue(() => new FakeProcess(0) { PollsUntilWindow = polls });

    public void NextStartNeverShowsWindow() => _startScript.Enqueue(() => new FakeProcess(0) { PollsUntilWindow = int.MaxValue });

    public void NextStartExitsAfter(int polls) => _startScript.Enqueue(() => new FakeProcess(0) { PollsUntilExit = polls, PollsUntilWindow = int.MaxValue });

    public void NextStartThrows() => _startScript.Enqueue(() => throw new System.ComponentModel.Win32Exception(2));

    public void SetResponding(int pid, bool responding)
    {
        lock (_gate) _processes[pid].Responding = responding;
    }

    public void SimulateCrash(int pid)
    {
        lock (_gate) _processes.Remove(pid);
    }

    public IReadOnlyList<ClientProcess> FindClients()
    {
        lock (_gate)
        {
            foreach (var p in _processes.Values.ToList())
            {
                p.Polls++;
                if (p.Polls >= p.PollsUntilExit) _processes.Remove(p.Pid);
                else if (p.Polls >= p.PollsUntilWindow) p.HasWindow = true;
            }

            return [.. _processes.Values.Select(p => new ClientProcess(p.Pid, p.HasWindow, p.Responding, p.WorkingSet, p.StartTimeUtc))];
        }
    }

    public int Start(string exePath, string arguments)
    {
        StartCalls++;
        var template = _startScript.Count > 0 ? _startScript.Dequeue()() : new FakeProcess(0) { PollsUntilWindow = 1 };
        lock (_gate)
        {
            var pid = _nextPid++;
            _processes[pid] = template with { Pid = pid };
            return pid;
        }
    }

    public bool IsRunning(int pid)
    {
        lock (_gate) return _processes.ContainsKey(pid);
    }

    public bool RequestClose(int pid)
    {
        CloseRequests.Add(pid);
        lock (_gate) return _processes.TryGetValue(pid, out var p) && !p.IgnoresClose && _processes.Remove(pid);
    }

    public bool Kill(int pid)
    {
        Kills.Add(pid);
        lock (_gate) return _processes.Remove(pid);
    }

    public bool Focus(int pid) => IsRunning(pid);

    private sealed record FakeProcess(int Pid)
    {
        public bool HasWindow { get; set; }
        public bool Responding { get; set; } = true;
        public DateTime? StartTimeUtc { get; init; }
        public long? WorkingSet { get; init; }
        public bool IgnoresClose { get; init; }
        public int Polls { get; set; }
        public int PollsUntilWindow { get; init; } = int.MaxValue;
        public int PollsUntilExit { get; init; } = int.MaxValue;
    }
}
