using System.IO.Compression;
using LarpStrap.Core.Logging;

namespace LarpStrap.Core.Tests.Logging;

public sealed class LogFilesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-logfiles-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Export_contains_only_larpstrap_logs()
    {
        File.WriteAllText(Path.Combine(_dir, "LarpStrap_20261013_100000_1.log"), "a");
        File.WriteAllText(Path.Combine(_dir, "0.742.0_Player_ABCDE_last.log"), "roblox log");
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "x");
        var zipPath = Path.Combine(_dir, "export.zip");

        var count = LogFiles.ExportZip(_dir, zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        Assert.Equal(1, count);
        Assert.Equal(["LarpStrap_20261013_100000_1.log"], zip.Entries.Select(e => e.FullName));
    }

    [Fact]
    public void Tail_can_be_read_while_the_log_is_open_for_writing()
    {
        using (var log = new AppLog(_dir))
        {
            log.Write(LogLevel.Info, "Test.Event", ("count", 1));
            var text = LogFiles.ReadTail(LogFiles.List(_dir).Single().FullName);
            Assert.Contains("Test.Event", text);
        }
    }

    [Fact]
    public void Failed_export_leaves_no_temporary_file()
    {
        File.WriteAllText(Path.Combine(_dir, "LarpStrap_20261013_100000_1.log"), "a");
        var zipPath = Path.Combine(_dir, "export.zip");
        Directory.CreateDirectory(zipPath); // the final move cannot replace a directory

        var ex = Record.Exception(() => LogFiles.ExportZip(_dir, zipPath));
        Assert.True(ex is IOException or UnauthorizedAccessException, $"unexpected {ex?.GetType().Name}");
        Assert.False(File.Exists(zipPath + ".tmp"));
    }

    [Fact]
    public void Failed_export_never_deletes_a_file_the_user_already_had()
    {
        File.WriteAllText(Path.Combine(_dir, "LarpStrap_20261013_100000_1.log"), "a");
        var zipPath = Path.Combine(_dir, "export.zip");
        File.WriteAllText(zipPath + ".tmp", "user data");

        Record.Exception(() => LogFiles.ExportZip(_dir, zipPath));

        Assert.Equal("user data", File.ReadAllText(zipPath + ".tmp"));
    }

    [Fact]
    public void Missing_directory_lists_nothing()
    {
        Assert.Empty(LogFiles.List(Path.Combine(_dir, "missing")));
    }
}
