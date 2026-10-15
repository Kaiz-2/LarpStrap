using System.Text.RegularExpressions;

namespace LarpStrap.Core.Settings;

/// <summary>Validates what the user types as a game and builds the only URL LarpStrap opens for it.</summary>
public static partial class GameLinks
{
    /// <summary>Accepts a bare place id or a https://www.roblox.com/games/&lt;id&gt;/... link. Anything else is rejected.</summary>
    public static bool TryParsePlaceId(string? input, out long placeId)
    {
        placeId = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var match = PlaceIdOrGameUrl().Match(input.Trim());
        return match.Success
               && long.TryParse(match.Groups["id"].Value, out placeId)
               && placeId > 0;
    }

    /// <summary>The game's page on roblox.com; the user starts the game from there with their own session.</summary>
    public static Uri GamePage(long placeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(placeId);
        return new Uri($"https://www.roblox.com/games/{placeId}");
    }

    /// <summary>
    /// roblox.com route that asks the browser to start the experience with the signed-in web session. Not covered by
    /// LarpStrap's tests: Roblox may change it, and without a session it only shows the game page.
    /// </summary>
    public static Uri GameStart(long placeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(placeId);
        return new Uri($"https://www.roblox.com/games/start?placeId={placeId}");
    }

    [GeneratedRegex(@"^(?:(?<id>\d{1,18})|https?://(?:www\.)?roblox\.com/games/(?<id>\d{1,18})(?:[/?#].*)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceIdOrGameUrl();
}
