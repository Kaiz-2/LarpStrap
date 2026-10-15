using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

public sealed class RobloxUriTests
{
    [Theory]
    [InlineData("roblox-player:1+launchmode:play+gameinfo:abc+placelauncherurl:https%3A%2F%2Fwww.roblox.com%2FGame%2FPlaceLauncher.ashx%3FplaceId%3D1")]
    [InlineData("ROBLOX-PLAYER:1+launchmode:app")]
    [InlineData("roblox://placeId=1818")]
    public void Accepts_launch_links(string link) => Assert.True(RobloxUri.IsLaunchLink(link));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("--app")]
    [InlineData("https://www.roblox.com/games/1")]
    [InlineData("roblox-player:1\" --injected")]   // a quote would end the argument and add others
    [InlineData("roblox-player:1 --injected")]
    [InlineData("roblox-player:1\t")]
    [InlineData("roblox-player:ñ")]
    public void Rejects_anything_else(string? value) => Assert.False(RobloxUri.IsLaunchLink(value));

    [Fact]
    public void Rejects_oversized_links() =>
        Assert.False(RobloxUri.IsLaunchLink("roblox-player:" + new string('a', RobloxUri.MaxLength)));

    [Fact]
    public void Quotes_the_link_as_a_single_argument_and_refuses_invalid_ones()
    {
        Assert.Equal("\"roblox-player:1+launchmode:app\"", RobloxUri.ToArguments("roblox-player:1+launchmode:app"));
        Assert.Throws<ArgumentException>(() => RobloxUri.ToArguments("roblox-player:1\" x"));
    }

    [Theory]
    [InlineData("roblox-player:1+placelauncherurl:https%3A%2F%2Fwww.roblox.com%2FGame%2FPlaceLauncher.ashx%3Frequest%3DRequestGame%26placeId%3D920587237%26isPlayTogetherGame%3Dfalse", 920587237L)]
    [InlineData("roblox://placeId=1818", 1818L)]
    [InlineData("roblox-player:1+launchmode:app", null)]
    public void Reads_the_place_id(string link, long? expected) => Assert.Equal(expected, RobloxUri.PlaceId(link));
}
