using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

public sealed class CrashReportsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-crash-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Only_minidumps_written_since_the_given_time_count()
    {
        var since = DateTime.UtcNow.AddMinutes(-1);
        var old = Path.Combine(_dir, "old.dmp");
        File.WriteAllText(old, "");
        File.SetLastWriteTimeUtc(old, since.AddHours(-1));
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "");
        var reports = new CrashReports(_dir);

        Assert.False(reports.AnySince(since));

        File.WriteAllText(Path.Combine(_dir, "new.dmp"), "");
        Assert.True(reports.AnySince(since));
    }

    [Fact]
    public void A_missing_folder_means_no_crash() =>
        Assert.False(new CrashReports(Path.Combine(_dir, "nowhere")).AnySince(DateTime.MinValue));
}
