namespace LarpStrap.Core.Instances;

/// <summary>Snapshot of one running Roblox client process. Resource values are null when Windows did not provide them.</summary>
public sealed record ClientProcess(
    int Pid,
    bool HasWindow,
    bool Responding = true,
    long? WorkingSetBytes = null,
    DateTime? StartTimeUtc = null);

/// <summary>
/// Everything LarpStrap does to client processes goes through this seam, so orchestration can be
/// tested without starting real clients. Implementations must not keep handles to Roblox processes open.
/// </summary>
public interface IProcessProvider
{
    IReadOnlyList<ClientProcess> FindClients();

    /// <summary>Starts the client and returns its PID. Throws if the process cannot be started.</summary>
    int Start(string exePath, string arguments);

    bool IsRunning(int pid);

    /// <summary>Asks the client to close its main window (normal close).</summary>
    bool RequestClose(int pid);

    /// <summary>Terminates the client. Callers must have explicit user confirmation.</summary>
    bool Kill(int pid);

    bool Focus(int pid);
}
