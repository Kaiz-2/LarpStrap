using System.IO.Compression;
using System.Text;

namespace LarpStrap.Core.Logging;

/// <summary>Read-only access to LarpStrap's own logs (already allow-listed by <see cref="AppLog"/>).</summary>
public static class LogFiles
{
    public const string Pattern = "LarpStrap_*.log";

    public static IReadOnlyList<FileInfo> List(string directory) =>
        Directory.Exists(directory)
            ? [.. new DirectoryInfo(directory).GetFiles(Pattern).OrderByDescending(f => f.Name, StringComparer.Ordinal)]
            : [];

    /// <summary>Reads at most the last <paramref name="maxChars"/> characters; works while the log is being written.</summary>
    public static string ReadTail(string path, int maxChars = 64_000)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return text.Length <= maxChars ? text : text[^maxChars..];
    }

    /// <summary>
    /// Zips only LarpStrap's own log files (never Roblox logs, which can hold account data).
    /// Returns how many files were exported.
    /// </summary>
    public static int ExportZip(string directory, string zipPath)
    {
        var files = List(directory);
        var tmp = zipPath + ".tmp";
        // Create mode fails if the file exists, so anything deleted below is a file this call created.
        var zip = ZipFile.Open(tmp, ZipArchiveMode.Create);
        try
        {
            using (zip)
            {
                foreach (var file in files)
                {
                    var entry = zip.CreateEntry(file.Name, CompressionLevel.Optimal);
                    using var source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var target = entry.Open();
                    source.CopyTo(target);
                }
            }
            File.Move(tmp, zipPath, overwrite: true);
            return files.Count;
        }
        catch
        {
            File.Delete(tmp); // never leave a partial archive next to the user's file
            throw;
        }
    }
}
