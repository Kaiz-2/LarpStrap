using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LarpStrap.Core.Logging;

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// File log that only writes allow-listed data (ADR 0004): launch URIs, command lines and
/// auth tickets must never reach disk, so arbitrary strings are rejected instead of redacted.
/// </summary>
public sealed partial class AppLog : IDisposable
{
    private const int MaxFiles = 10;
    private const int MaxStringLength = 32;

    // Extend per module when a new field is genuinely needed; never add free-text keys.
    private static readonly HashSet<string> AllowedKeys =
    [
        "appVersion", "robloxVersion", "placeId", "launchMode", "pid", "count", "durationMs", "exception", "result", "theme", "origin", "demoMode", "version", "reason",
    ];

    /// <summary>Per-file cap so a repeating warning cannot fill the disk; rotation only bounds the file count.</summary>
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    private readonly StreamWriter _writer;
    private readonly Lock _gate = new();
    private readonly long _maxBytes;
    private long _written;
    private bool _capped;

    public AppLog(string directory, long maxBytes = DefaultMaxBytes)
    {
        _maxBytes = maxBytes;
        Directory.CreateDirectory(directory);
        PruneOldFiles(directory);

        var name = $"LarpStrap_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.log";
        _writer = new StreamWriter(Path.Combine(directory, name), append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public void Write(LogLevel level, string eventName, params (string Key, object? Value)[] fields)
    {
        var line = new StringBuilder()
            .Append(Timestamp())
            .Append(' ').Append(level)
            .Append(' ').Append(EventNamePattern().IsMatch(eventName) ? eventName : "InvalidEventName");

        foreach (var (key, value) in fields)
        {
            if (!AllowedKeys.Contains(key)) continue;
            line.Append(' ').Append(key).Append('=').Append(Render(value));
        }

        lock (_gate)
        {
            if (_capped) return;
            var text = line.ToString();
            if (_written + text.Length > _maxBytes)
            {
                _capped = true;
                text = $"{Timestamp()} Warning Log.SizeLimitReached";
            }
            _writer.WriteLine(text);
            _written += text.Length + Environment.NewLine.Length;
        }
    }

    public void Dispose() => _writer.Dispose();

    // Local time with its UTC offset: readable, and still unambiguous in logs exported across a DST change.
    private static string Timestamp() => DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    private static string Render(object? value) => value switch
    {
        null => "null",
        Exception ex => ex.GetType().Name, // messages may embed paths or URIs
        bool or Enum or Version => value.ToString()!,
        IFormattable number when value.GetType().IsPrimitive || value is decimal
            => number.ToString(null, CultureInfo.InvariantCulture),
        TimeSpan span => span.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture),
        string s when s.Length <= MaxStringLength && SafeStringPattern().IsMatch(s) => s,
        _ => "[omitted]",
    };

    private static void PruneOldFiles(string directory)
    {
        // Names embed a sortable timestamp, so ordering by name is ordering by age.
        var stale = Directory.GetFiles(directory, "LarpStrap_*.log")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(MaxFiles - 1); // leave room for the file this instance creates

        foreach (var file in stale)
        {
            try { File.Delete(file); }
            catch (IOException ex) { Debug.WriteLine($"Could not prune {Path.GetFileName(file)}: {ex.GetType().Name}"); }
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9.]{0,63}$")]
    private static partial Regex EventNamePattern();

    [GeneratedRegex("^[A-Za-z0-9._-]*$")]
    private static partial Regex SafeStringPattern();
}
