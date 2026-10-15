using LarpStrap.Core.Logging;

namespace LarpStrap.Core.Tests.Logging;

public sealed class AppLogTests : IDisposable
{
    private const string FakeLaunchUri = "roblox-player:1+launchmode:play+gameinfo:FAKE_TICKET_VALUE";
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-log-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteAndReadBack(Action<AppLog> write)
    {
        using (var log = new AppLog(_dir))
            write(log);
        return File.ReadAllText(Directory.GetFiles(_dir, "*.log").Single());
    }

    [Fact]
    public void Writes_event_name_and_allowed_fields()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Settings.Loaded", ("theme", "Dark"), ("count", 3)));

        Assert.Contains("Settings.Loaded", text);
        Assert.Contains("theme=Dark", text);
        Assert.Contains("count=3", text);
    }

    [Fact]
    public void Drops_fields_whose_key_is_not_allowed()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Launch.Requested", ("uri", "visible-value")));

        Assert.DoesNotContain("visible-value", text);
    }

    [Fact]
    public void Never_writes_a_launch_uri_even_under_an_allowed_key()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Launch.Requested", ("launchMode", FakeLaunchUri)));

        Assert.DoesNotContain("roblox-player", text);
        Assert.DoesNotContain("FAKE_TICKET_VALUE", text);
    }

    [Fact]
    public void Never_writes_long_token_like_strings()
    {
        var token = new string('A', 200);

        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Launch.Requested", ("launchMode", token)));

        Assert.DoesNotContain(token[..40], text);
    }

    [Fact]
    public void Exceptions_are_logged_by_type_only()
    {
        var ex = new InvalidOperationException($"failed to launch {FakeLaunchUri}");

        var text = WriteAndReadBack(log => log.Write(LogLevel.Error, "Launch.Failed", ("exception", ex)));

        Assert.Contains("InvalidOperationException", text);
        Assert.DoesNotContain("FAKE_TICKET_VALUE", text);
    }

    [Fact]
    public void Invalid_event_names_are_not_written_verbatim()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, FakeLaunchUri));

        Assert.DoesNotContain("FAKE_TICKET_VALUE", text);
    }

    [Fact]
    public void Stops_writing_once_the_size_cap_is_reached()
    {
        using (var log = new AppLog(_dir, maxBytes: 2_000))
        {
            for (var i = 0; i < 200; i++)
                log.Write(LogLevel.Warning, "Panel.RefreshFailed", ("count", i));
        }

        var file = new FileInfo(Directory.GetFiles(_dir, "*.log").Single());
        Assert.True(file.Length < 2_500, $"log grew to {file.Length} bytes");
        Assert.Contains("Log.SizeLimitReached", File.ReadAllText(file.FullName));
    }

    [Fact]
    public void Timestamps_carry_the_utc_offset_so_exported_logs_are_unambiguous()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Test.Event"));

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} ", text);
    }

    [Fact]
    public void Writes_origin_and_demo_mode_fields()
    {
        var text = WriteAndReadBack(log => log.Write(LogLevel.Info, "Test.Event", ("origin", LarpStrap.Core.Instances.InstanceOrigin.Launched), ("demoMode", true)));

        Assert.Contains("origin=Launched", text);
        Assert.Contains("demoMode=True", text);
    }

    [Fact]
    public void Keeps_at_most_ten_log_files()
    {
        for (var i = 0; i < 12; i++)
            File.WriteAllText(Path.Combine(_dir, $"LarpStrap_20260101_0000{i:00}_1.log"), "old");

        using (new AppLog(_dir)) { }

        Assert.Equal(10, Directory.GetFiles(_dir, "*.log").Length);
        Assert.DoesNotContain(Path.Combine(_dir, "LarpStrap_20260101_000000_1.log"), Directory.GetFiles(_dir, "*.log"));
    }
}
