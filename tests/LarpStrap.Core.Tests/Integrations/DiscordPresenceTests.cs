using System.Text.Json.Nodes;
using LarpStrap.Core.Integrations;

namespace LarpStrap.Core.Tests.Integrations;

// Framing and payload only: these tests never open Discord's real pipe, which may be running on the test machine.
public sealed class DiscordPresenceTests
{
    [Fact]
    public void Frames_round_trip_with_a_little_endian_header()
    {
        using var stream = new MemoryStream();
        DiscordPresence.Write(stream, 1, new JsonObject { ["cmd"] = "X" });

        var bytes = stream.ToArray();
        Assert.Equal([1, 0, 0, 0], bytes[..4]);
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4));

        stream.Position = 0;
        Assert.Equal((1, """{"cmd":"X"}"""), DiscordPresence.Read(stream));
    }

    [Fact]
    public void A_close_frame_or_an_absurd_length_is_rejected()
    {
        using var close = new MemoryStream();
        DiscordPresence.Write(close, 2, new JsonObject { ["code"] = 4000 });
        close.Position = 0;
        Assert.Throws<InvalidDataException>(() => DiscordPresence.Read(close));

        using var huge = new MemoryStream([1, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0x7F]);
        Assert.Throws<InvalidDataException>(() => DiscordPresence.Read(huge));
    }

    [Fact]
    public void The_activity_says_only_that_you_are_in_roblox_and_since_when()
    {
        var since = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

        var activity = DiscordPresence.Activity(since)["args"]!["activity"]!.AsObject();

        Assert.Equal(["details", "timestamps"], activity.Select(p => p.Key));
        Assert.Equal("En Roblox", activity["details"]!.ToString());
        Assert.Equal(new DateTimeOffset(since).ToUnixTimeSeconds(), activity["timestamps"]!["start"]!.GetValue<long>());
    }

    [Fact]
    public void Clearing_sends_a_null_activity()
    {
        var args = DiscordPresence.Activity(null)["args"]!.AsObject();

        Assert.True(args.ContainsKey("activity"));
        Assert.Null(args["activity"]);
    }
}
