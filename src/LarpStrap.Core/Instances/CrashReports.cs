namespace LarpStrap.Core.Instances;

/// <summary>
/// Roblox's crash reports: Crashpad minidumps (<c>*.dmp</c>) in <c>logs\crashes\reports</c>. Seen on this project's PC
/// (0.742/0.743): three dumps there against many sessions that closed normally and left none. That a crash always
/// leaves one is an inference to confirm with a real crash; without a dump an exit stays «se cerró fuera de LarpStrap».
/// Read-only: only file names and times are read.
/// </summary>
public sealed class CrashReports(string folder)
{
    public static CrashReports ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs", "crashes", "reports"));

    public bool AnySince(DateTime utc)
    {
        try
        {
            return Directory.Exists(folder) && new DirectoryInfo(folder).EnumerateFiles("*.dmp").Any(f => f.LastWriteTimeUtc >= utc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
