using System.Text.Json.Nodes;
using LarpStrap.Core.Settings;

namespace LarpStrap.Core.Tests.Settings;

public sealed class FastFlagsTests : IDisposable
{
    private readonly string _version = Directory.CreateTempSubdirectory("larpstrap-ff-").FullName;

    private string FilePath => Path.Combine(_version, "ClientSettings", FastFlags.FileName);

    public void Dispose() => Directory.Delete(_version, recursive: true);

    [Fact]
    public void The_allowlist_is_the_18_flags_of_the_official_announcement()
    {
        Assert.Equal(18, FastFlags.Allowlist.Count);
        Assert.Equal(18, FastFlags.Allowlist.Select(f => f.Name).Distinct().Count());
        // Roblox's prefixes give the type: FFlag/DFFlag are booleans, FInt/DFInt whole numbers.
        Assert.All(FastFlags.Allowlist, f => Assert.Equal(f.Name.Contains("Flag", StringComparison.Ordinal) && !f.Name.Contains("Int", StringComparison.Ordinal),
            f.Kind == FastFlagKind.Bool));
    }

    [Theory]
    [InlineData("FFlagDebugSkyGray", "true", "True")]
    [InlineData("FFlagDebugSkyGray", " False ", "False")]
    [InlineData("FFlagDebugSkyGray", "1", null)]
    [InlineData("FIntDebugForceMSAASamples", "4", "4")]
    [InlineData("FIntDebugForceMSAASamples", "16", null)]
    [InlineData("FIntDebugForceMSAASamples", "-1", null)]
    [InlineData("FIntDebugForceMSAASamples", "4.5", null)]
    [InlineData("DFIntTaskSchedulerTargetFps", "240", null)] // not on the allowlist
    public void Normalizes_values_or_rejects_them(string name, string value, string? expected) =>
        Assert.Equal(expected, FastFlags.Normalize(name, value));

    [Fact]
    public void Writes_the_flags_and_keeps_keys_it_does_not_manage()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "FFlagSomeoneElse": "True", "FFlagDebugSkyGray": "True" }""");

        FastFlags.Apply(_version, new Dictionary<string, string> { ["FIntDebugForceMSAASamples"] = "4", ["DFIntTaskSchedulerTargetFps"] = "999" });

        var json = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        Assert.Equal("True", json["FFlagSomeoneElse"]!.ToString());
        Assert.Equal("4", json["FIntDebugForceMSAASamples"]!.ToString());
        Assert.False(json.ContainsKey("FFlagDebugSkyGray"));          // cleared by the user
        Assert.False(json.ContainsKey("DFIntTaskSchedulerTargetFps")); // never written: not on the allowlist
    }

    [Fact]
    public void Does_not_create_or_touch_anything_when_nothing_changes()
    {
        FastFlags.Apply(_version, null);
        Assert.False(File.Exists(FilePath));

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        const string other = """{"FFlagSomeoneElse":true}""";
        File.WriteAllText(FilePath, other);
        FastFlags.Apply(_version, new Dictionary<string, string>());
        Assert.Equal(other, File.ReadAllText(FilePath));
    }

    [Fact]
    public void Never_overwrites_a_file_that_is_not_a_json_object()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "not json");

        FastFlags.Apply(_version, new Dictionary<string, string> { ["FFlagDebugSkyGray"] = "True" });

        Assert.Equal("not json", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Settings_drop_flags_off_the_allowlist_and_invalid_values()
    {
        var settings = new AppSettings(FastFlagValues: new Dictionary<string, string>
        {
            ["FFlagDebugSkyGray"] = "true",
            ["FIntDebugForceMSAASamples"] = "99",
            ["FFlagNotAllowed"] = "True",
        }).Normalize();

        Assert.Equal(new Dictionary<string, string> { ["FFlagDebugSkyGray"] = "True" }, settings.FastFlagValues);
    }

    [Theory]
    [InlineData(" 123456789012345678 ", "123456789012345678")]
    [InlineData("1234", null)]
    [InlineData("12345678901234567x", null)]
    public void Settings_keep_only_a_valid_discord_application_id(string id, string? expected) =>
        Assert.Equal(expected, new AppSettings(DiscordAppId: id).Normalize().DiscordAppId);
}
