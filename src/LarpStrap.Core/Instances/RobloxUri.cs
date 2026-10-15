using System.Text.RegularExpressions;

namespace LarpStrap.Core.Instances;

/// <summary>
/// Launch links from roblox.com (<c>roblox-player:</c>) and from the Roblox app (<c>roblox:</c>). A link carries an
/// auth ticket (ADR 0004): it is handed to the client as-is and never logged or shown; only the fields below are read.
/// </summary>
public static partial class RobloxUri
{
    public const int MaxLength = 8192;

    private static readonly string[] Schemes = ["roblox-player:", "roblox:"];

    /// <summary>
    /// A link that can reach the client as a single quoted argument: known scheme, bounded length, and only printable
    /// ASCII without spaces or quotes (links are percent-encoded), so it cannot add arguments of its own.
    /// </summary>
    public static bool IsLaunchLink(string? value) =>
        value is { Length: > 0 and <= MaxLength }
        && Schemes.Any(s => value.StartsWith(s, StringComparison.OrdinalIgnoreCase))
        && value.All(c => c is > ' ' and < '\x7F' and not '"');

    /// <summary>The client's own protocol handler receives the link as its one argument.</summary>
    public static string ToArguments(string link) =>
        IsLaunchLink(link) ? $"\"{link}\"" : throw new ArgumentException("No es un enlace de Roblox válido.", nameof(link));

    /// <summary>The game's place ID, safe to log and show (not part of the ticket).</summary>
    public static long? PlaceId(string link) =>
        PlaceIdPattern().Match(link) is { Success: true } m && long.TryParse(m.Groups[1].Value, out var id) ? id : null;

    [GeneratedRegex(@"placeId(?:=|%3D)(\d{1,19})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceIdPattern();
}
