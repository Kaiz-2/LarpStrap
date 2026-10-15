namespace LarpStrap.Core.Instances;

/// <summary>
/// SIMULATED clients for the app's demo mode: one open client and Roblox's background process, as on a real PC, plus
/// launches that sometimes fail, so the panel states can be seen without Roblox. Nothing here is a real process; the
/// UI must label it as simulated.
/// </summary>
public sealed class SimulatedProcessProvider : IProcessProvider
{
    private const long Mb = 1024L * 1024;

    private readonly Lock _gate = new();
    private readonly Dictionary<int, Sim> _sims = [];
    private readonly TimeProvider _time;
    private int _nextPid = 41000;
    private int _starts;

    public SimulatedProcessProvider(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        var now = _time.GetUtcNow().UtcDateTime;
        Add(new Sim { HasWindow = true, Memory = 1180 * Mb, Started = now.AddMinutes(-42) });
        Add(new Sim { HasWindow = false, Memory = 96 * Mb, Started = now.AddHours(-3) });
    }

    public IReadOnlyList<ClientProcess> FindClients()
    {
        lock (_gate)
        {
            foreach (var sim in _sims.Values.ToList())
            {
                sim.Polls++;
                if (sim.Polls >= sim.ExitAfterPolls) _sims.Remove(sim.Pid);
                else if (sim.Polls >= sim.WindowAfterPolls) sim.HasWindow = true;
            }
            return [.. _sims.Values.Select(s => new ClientProcess(s.Pid, s.HasWindow, s.Responding, s.Memory, s.Started))];
        }
    }

    /// <summary>Every third simulated launch fails during start-up, to show the error state.</summary>
    public int Start(string exePath, string arguments)
    {
        lock (_gate)
        {
            _starts++;
            var fails = _starts % 3 == 0;
            return Add(new Sim
            {
                Memory = 640 * Mb,
                Started = _time.GetUtcNow().UtcDateTime,
                WindowAfterPolls = fails ? int.MaxValue : 4,
                ExitAfterPolls = fails ? 3 : int.MaxValue,
            });
        }
    }

    public bool IsRunning(int pid)
    {
        lock (_gate) return _sims.ContainsKey(pid);
    }

    public bool RequestClose(int pid)
    {
        lock (_gate) return _sims.TryGetValue(pid, out var s) && s.HasWindow && _sims.Remove(pid);
    }

    public bool Kill(int pid)
    {
        lock (_gate) return _sims.Remove(pid);
    }

    public bool Focus(int pid)
    {
        lock (_gate) return _sims.TryGetValue(pid, out var s) && s.HasWindow;
    }

    private int Add(Sim sim)
    {
        sim.Pid = _nextPid++;
        _sims[sim.Pid] = sim;
        return sim.Pid;
    }

    private sealed class Sim
    {
        public int Pid { get; set; }
        public bool HasWindow { get; set; }
        public bool Responding { get; init; } = true;
        public long Memory { get; init; }
        public DateTime Started { get; init; }
        public int Polls { get; set; }
        public int WindowAfterPolls { get; init; } = int.MaxValue;
        public int ExitAfterPolls { get; init; } = int.MaxValue;
    }
}
