using LarpStrap.Core.Settings;

namespace LarpStrap.Core.Tests.Settings;

public sealed class GameLinksTests
{
    [Theory]
    [InlineData("920587237", 920587237)]
    [InlineData("  1818  ", 1818)]
    [InlineData("https://www.roblox.com/games/920587237/Adopt-Me", 920587237)]
    [InlineData("https://roblox.com/games/1818", 1818)]
    [InlineData("https://www.roblox.com/games/1818?privateServerLinkCode=x", 1818)]
    public void Accepts_place_ids_and_roblox_game_links(string input, long expected)
    {
        Assert.True(GameLinks.TryParsePlaceId(input, out var placeId));
        Assert.Equal(expected, placeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("https://evil.example/games/1818")]
    [InlineData("https://www.roblox.com.evil.example/games/1818")]
    [InlineData("roblox-player:1+launchmode:play")]
    [InlineData("12345678901234567890123")]
    public void Rejects_anything_that_is_not_a_place_id_or_a_roblox_game_link(string? input)
    {
        Assert.False(GameLinks.TryParsePlaceId(input, out _));
    }

    [Fact]
    public void Game_page_always_points_to_roblox_com()
    {
        Assert.Equal("https://www.roblox.com/games/1818", GameLinks.GamePage(1818).ToString());
    }
}
