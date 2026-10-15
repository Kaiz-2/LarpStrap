using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using LarpStrap.Core.Logging;

namespace LarpStrap.Core.Integrations;

/// <summary>
/// Discord Rich Presence over Discord's local IPC pipe (<c>discord-ipc-0..9</c>): frames of a little-endian opcode and
/// length followed by UTF-8 JSON (0 = handshake, 1 = command). No dependency. Only «En Roblox» and since when are sent:
/// never the game, the account or anything from a launch link. Opt-in, off by default.
/// </summary>
public sealed class DiscordPresence(string appId, AppLog? log = null, TimeProvider? time = null) : IDisposable
{
    /// <summary>Looking for Discord takes up to 3 s (10 pipes); when it isn't running, wait this long before looking again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    public string AppId => appId;

    private const int Handshake = 0;
    private const int Frame = 1;
    private const int MaxFrameBytes = 64 * 1024;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>A Discord that stops answering must not hold the caller (or the next update) forever.</summary>
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private NamedPipeClientStream? _pipe;
    private DateTime? _sent;
    private bool _hasSent;
    private DateTimeOffset _nextConnect = DateTimeOffset.MinValue;
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Shows «En Roblox» since <paramref name="playingSinceUtc"/>, or clears it with null. Repeats are skipped.</summary>
    /// <returns>False when Discord isn't running or refused it (normal: it is retried on the next change).</returns>
    public bool Update(DateTime? playingSinceUtc)
    {
        lock (_gate)
        {
            if (_hasSent && _sent == playingSinceUtc) return true;
            try
            {
                if (_pipe is null)
                {
                    if (_time.GetUtcNow() < _nextConnect) return false;
                    if (!Connect())
                    {
                        _nextConnect = _time.GetUtcNow() + RetryAfter;
                        return false;
                    }
                }
                Write(_pipe!, Frame, Activity(playingSinceUtc));
                Read(_pipe!); // Discord answers every command; an error reply is not worth acting on.
                (_sent, _hasSent) = (playingSinceUtc, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException or OperationCanceledException)
            {
                log?.Write(LogLevel.Info, "Discord.Disconnected", ("exception", ex));
                Disconnect();
                return false;
            }
        }
    }

    private bool Connect()
    {
        for (var i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(ConnectTimeout);
                Write(pipe, Handshake, new JsonObject { ["v"] = 1, ["client_id"] = appId });
                Read(pipe); // READY, or an error frame for a wrong application ID (then Discord closes the pipe)
                _pipe = pipe;
                log?.Write(LogLevel.Info, "Discord.Connected");
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or InvalidDataException or OperationCanceledException)
            {
                pipe.Dispose();
            }
        }
        return false;
    }

    internal static JsonObject Activity(DateTime? playingSinceUtc) => new()
    {
        ["cmd"] = "SET_ACTIVITY",
        ["nonce"] = Guid.NewGuid().ToString(),
        ["args"] = new JsonObject
        {
            ["pid"] = Environment.ProcessId,
            ["activity"] = playingSinceUtc is not { } since ? null : new JsonObject
            {
                ["details"] = "En Roblox",
                ["timestamps"] = new JsonObject { ["start"] = new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc)).ToUnixTimeSeconds() },
            },
        },
    };

    internal static void Write(Stream stream, int opcode, JsonObject payload)
    {
        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        stream.Write(frame);
        stream.Flush();
    }

    internal static (int Opcode, string Json) Read(Stream stream)
    {
        using var timeout = new CancellationTokenSource(ReplyTimeout);
        var header = new byte[8];
        stream.ReadExactlyAsync(header, timeout.Token).AsTask().GetAwaiter().GetResult();
        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length is < 0 or > MaxFrameBytes) throw new InvalidDataException("Trama de Discord no válida.");
        var body = new byte[length];
        stream.ReadExactlyAsync(body, timeout.Token).AsTask().GetAwaiter().GetResult();
        if (opcode == 2) throw new InvalidDataException("Discord cerró la conexión."); // CLOSE: e.g. unknown application ID
        return (opcode, Encoding.UTF8.GetString(body));
    }

    private void Disconnect()
    {
        _pipe?.Dispose();
        _pipe = null;
        _hasSent = false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_pipe is not null && _hasSent && _sent is not null)
            {
                try { Write(_pipe, Frame, Activity(null)); }
                catch (IOException) { }
            }
            Disconnect();
        }
    }
}
