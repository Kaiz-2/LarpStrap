namespace LarpStrap.Core.Instances;

public sealed record RobloxInstallation(string PlayerPath, string VersionFolder);

/// <summary>Finds the newest RobloxPlayerBeta.exe under a set of "Versions" roots (read-only).</summary>
public sealed class InstallationLocator(IEnumerable<string> versionRoots)
{
    public const string PlayerExecutable = "RobloxPlayerBeta.exe";

    public IReadOnlyList<string> Roots { get; } = [.. versionRoots];

    // ponytail: reuses the official client already installed by Roblox or by another bootstrapper
    // until the deployment module installs LarpStrap's own copy.
    public static InstallationLocator ForCurrentUser()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new([
            Path.Combine(local, "Roblox", "Versions"),
            Path.Combine(local, "Bloxstrap", "Versions"),
            Path.Combine(local, "Fishstrap", "Versions"),
        ]);
    }

    public RobloxInstallation? Find() =>
        Roots.SelectMany(VersionFolders)
            .Select(dir => new FileInfo(Path.Combine(dir, PlayerExecutable)))
            .Where(exe => exe.Exists)
            .OrderByDescending(exe => exe.LastWriteTimeUtc)
            .Select(exe => new RobloxInstallation(exe.FullName, exe.Directory!.Name))
            .FirstOrDefault();

    // A root another launcher is replacing or has locked must not hide the others, nor crash the callers (startup, panel, launch).
    private static string[] VersionFolders(string root)
    {
        try { return Directory.Exists(root) ? Directory.GetDirectories(root) : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }
}
