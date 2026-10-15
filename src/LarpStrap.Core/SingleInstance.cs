using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace LarpStrap.Core;

/// <summary>
/// One LarpStrap per Windows user. A second start (e.g. the browser opening a launch link) hands its argument to the
/// first over a named pipe that only the current user can create or open, then exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const int MaxMessageBytes = 16 * 1024;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string name)
    {
        _mutex = mutex;
        _name = name;
    }

    /// <summary>Per user: pipe names are machine-wide, so the SID keeps two signed-in users apart.</summary>
    public static string DefaultName => $"LarpStrap-{WindowsIdentity.GetCurrent().User?.Value}";

    /// <summary>The instance for this user, or null when another LarpStrap of this user already runs.</summary>
    public static SingleInstance? TryAcquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{name}", out var created);
        if (created) return new(mutex, name);
        try
        {
            if (mutex.WaitOne(0)) return new(mutex, name);
        }
        catch (AbandonedMutexException)
        {
            return new(mutex, name); // the previous LarpStrap died holding it: ours now
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>Calls <paramref name="onMessage"/> on a background thread for each message; oversized ones are dropped.</summary>
    public void Listen(Action<string> onMessage) => _ = Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                if (await ReadAsync(server) is { } message)
                    onMessage(message);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                // A sender that disconnects, stalls or overflows is ignored; keep listening.
            }
        }
    });

    /// <summary>Sends one message to the running instance. False if none answered in time.</summary>
    public static bool Send(string name, string message, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeout);
            client.Write(Encoding.UTF8.GetBytes(message));
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<string?> ReadAsync(Stream stream)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(ReadTimeout);
        var buffer = new byte[MaxMessageBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token)) > 0)
            length += read;
        return length > MaxMessageBytes ? null : Encoding.UTF8.GetString(buffer, 0, length);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.Dispose();
    }
}
