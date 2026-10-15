using System.Text.Json.Serialization;

namespace LarpStrap.Core.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>Color identity of the interface, independent from its light/dark mode.</summary>
public enum UiThemePreset
{
    Nebula,
    Midnight,
    Ocean,
    Forest,
    Sunset,
    Rose,
    Slate,
}

public enum AccentColor
{
    Amber,
    Teal,
    Coral,
    Lime,

    /// <summary>Brand accent since the 0.3 redesign (appended so saved numbers keep their meaning).</summary>
    Violet,
    Blue,
    Rose,
    Cyan,
    Mint,
    Emerald,
    Forest,
    Sky,
    Ocean,
    Indigo,
    Lavender,
    Magenta,
    Pink,
    Crimson,
    Orange,
    Gold,
    Bronze,
    Slate,
    Graphite,
    Custom,
}

/// <summary>Selectable in-app logo mark. The installer and executable retain the default brand mark.</summary>
public enum LogoStyle
{
    Classic,
    Orbit,
    Monogram,
}

/// <summary>Window material on Windows 11; Solid everywhere else or when the user prefers it.</summary>
public enum WindowBackdrop
{
    Mica,
    Acrylic,
    Solid,
}

/// <summary>A saved game: Roblox place id, display name, and optional public Roblox icon URL.</summary>
public sealed record FavoriteGame(long PlaceId, string Name, string? ThumbnailUrl = null);

/// <summary>
/// Persisted settings. <see cref="SchemaVersion"/> lets future versions migrate older files;
/// files without it are version 1 and simply get defaults for the new values.
/// </summary>
public sealed record AppSettings(
    ThemeMode Theme = ThemeMode.System,
    bool CloseToTray = true,
    AccentColor Accent = AccentColor.Violet,
    double TextScale = 1.0,
    bool DemoMode = false,
    int LaunchTimeoutSeconds = 60,
    IReadOnlyList<FavoriteGame>? Favorites = null,
    WindowBackdrop Backdrop = WindowBackdrop.Mica,
    IReadOnlyDictionary<string, string>? FastFlagValues = null,
    bool DiscordPresence = false,
    string? DiscordAppId = null,
    bool AdvancedMode = false,
    bool CompactNavigation = false,
    LogoStyle Logo = LogoStyle.Classic,
    string? CustomAccentHex = null,
    UiThemePreset ThemePreset = UiThemePreset.Nebula)
{
    public const int CurrentSchemaVersion = 2;
    public const double MinTextScale = 1.0;
    public const double MaxTextScale = 1.5;
    public const int MaxFavoriteNameLength = 80;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonIgnore]
    public IReadOnlyList<FavoriteGame> FavoriteGames => Favorites ?? [];

    /// <summary>Clamps out-of-range values and drops invalid favorites, so a hand-edited file can't break the app.</summary>
    public AppSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        // The JSON enum converter also accepts numbers, so an edited file can hold values with no theme resources.
        Theme = Enum.IsDefined(Theme) ? Theme : ThemeMode.System,
        ThemePreset = Enum.IsDefined(ThemePreset) ? ThemePreset : UiThemePreset.Nebula,
        Accent = Enum.IsDefined(Accent) ? Accent : AccentColor.Violet,
        Logo = Enum.IsDefined(Logo) ? Logo : LogoStyle.Classic,
        CustomAccentHex = IsHexColor(CustomAccentHex) ? CustomAccentHex : null,
        Backdrop = Enum.IsDefined(Backdrop) ? Backdrop : WindowBackdrop.Mica,
        TextScale = double.IsFinite(TextScale) ? Math.Clamp(TextScale, MinTextScale, MaxTextScale) : MinTextScale,
        LaunchTimeoutSeconds = Math.Clamp(LaunchTimeoutSeconds, 10, 300),
        FastFlagValues = FastFlagValues is null ? null : FastFlags.Clean(FastFlagValues),
        DiscordAppId = DiscordAppId is { } id && IsDiscordAppId(id.Trim()) ? id.Trim() : null,
        Favorites = Favorites is null ? null :
        [
            .. Favorites
                .Where(f => f is not null && f.PlaceId > 0 && !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => f with
                {
                    Name = Truncate(f.Name.Trim(), MaxFavoriteNameLength),
                    ThumbnailUrl = IsSafeRobloxImage(f.ThumbnailUrl) ? f.ThumbnailUrl : null,
                })
                .DistinctBy(f => f.PlaceId),
        ],
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static bool IsSafeRobloxImage(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.EndsWith(".rbxcdn.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>A Discord application ID: a snowflake, 17 to 20 digits.</summary>
    public static bool IsDiscordAppId(string value) => value.Length is >= 17 and <= 20 && value.All(char.IsAsciiDigit);

    public static bool IsHexColor(string? value) => value is { Length: 7 } && value[0] == '#'
        && value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}

public sealed record SettingsLoadResult(AppSettings Settings, bool RecoveredFromCorruptFile);

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
