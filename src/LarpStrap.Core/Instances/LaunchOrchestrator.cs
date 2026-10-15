using System.ComponentModel;
using System.Diagnostics;

namespace LarpStrap.Core.Instances;

public sealed record LaunchOptions(TimeSpan WindowTimeout, TimeSpan PollInterval)
{
    public static LaunchOptions Default { get; } = new(
        WindowTimeout: TimeSpan.FromSeconds(60),
        PollInterval: TimeSpan.FromMilliseconds(500));
}

/// <summary>Arguments passed to the client. May carry an auth ticket: never log or display it.</summary>
public sealed record LaunchRequest(string Arguments);

public enum LaunchOutcome
{
    Started,
    InstallationNotFound,

    /// <summary>A client is already open or starting. Roblox allows one at a time (ADR 0003).</summary>
    AlreadyOpen,

    /// <summary>Only Roblox's background (tray) process holds the single slot; LarpStrap never closes it.</summary>
    BlockedByBackground,

    /// <summary>Close-and-launch: the open client did not exit in time, so nothing was launched.</summary>
    CloseTimedOut,
    StartFailed,
    ExitedDuringLaunch,
    TimedOut,
    Cancelled,
}

public sealed record LaunchResult(LaunchOutcome Outcome, int? Pid, TimeSpan Duration, string Diagnostic);

/// <summary>Launches the Roblox client, only when no other client is open or starting (Roblox allows one).</summary>
/// <param name="prepare">Runs on the installation about to launch (e.g. writing FastFlags), only when a launch will happen; must not throw.</param>
public sealed class LaunchOrchestrator(
    IProcessProvider processes,
    InstallationLocator locator,
    InstanceTracker tracker,
    LaunchOptions options,
    Action<RobloxInstallation>? prepare = null)
{
    public async Task<LaunchResult> LaunchAsync(LaunchRequest request, CancellationToken ct)
    {
        var installation = locator.Find();
        if (installation is null)
            return new(LaunchOutcome.InstallationNotFound, null, TimeSpan.Zero,
                $"No se encontró {InstallationLocator.PlayerExecutable} en: {string.Join("; ", locator.Roots)}");
        if (ct.IsCancellationRequested)
            return new(LaunchOutcome.Cancelled, null, TimeSpan.Zero, "Cancelado antes de iniciarse.");

        var active = tracker.Refresh().Where(t => t.IsActive).ToList();
        if (active.Count > 0)
        {
            var background = active.All(t => t.Status == InstanceStatus.Background);
            return new(background ? LaunchOutcome.BlockedByBackground : LaunchOutcome.AlreadyOpen, null, TimeSpan.Zero,
                background ? "Solo hay un proceso de Roblox en segundo plano." : "Ya hay un cliente de Roblox abierto o iniciándose.");
        }

        prepare?.Invoke(installation);
        return await LaunchOneAsync(installation, request, ct);
    }

    private async Task<LaunchResult> LaunchOneAsync(RobloxInstallation installation, LaunchRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        int pid;
        try
        {
            pid = processes.Start(installation.PlayerPath, request.Arguments);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(LaunchOutcome.StartFailed, null, clock.Elapsed, $"{ex.GetType().Name}: {ex.Message}");
        }

        tracker.MarkLaunched(pid);

        try
        {
            while (true)
            {
                var client = processes.FindClients().FirstOrDefault(c => c.Pid == pid);
                if (client is null)
                    return new(LaunchOutcome.ExitedDuringLaunch, pid, clock.Elapsed,
                        $"El proceso {pid} terminó a los {clock.ElapsedMilliseconds} ms sin mostrar ventana.");
                if (client.HasWindow)
                    return new(LaunchOutcome.Started, pid, clock.Elapsed, "Ventana del cliente detectada.");
                if (clock.Elapsed >= options.WindowTimeout)
                    return new(LaunchOutcome.TimedOut, pid, clock.Elapsed,
                        $"Sin ventana tras {options.WindowTimeout.TotalSeconds:0.#} s; el proceso {pid} sigue abierto.");

                await Task.Delay(options.PollInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // The client already started is left alone: cancelling a launch must not kill a game session.
            return new(LaunchOutcome.Cancelled, pid, clock.Elapsed, $"Cancelado; el proceso {pid} ya iniciado sigue abierto.");
        }
    }

    internal static async Task<bool> TryDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
