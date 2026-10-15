using System.Security.AccessControl;
using System.Security.Principal;
using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

public sealed class InstallationLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("larpstrap-locator-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AddPlayer(string versionsRoot, string version, DateTime writeTimeUtc)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, versionsRoot, version)).FullName;
        var exe = Path.Combine(dir, "RobloxPlayerBeta.exe");
        File.WriteAllText(exe, "fake");
        File.SetLastWriteTimeUtc(exe, writeTimeUtc);
        return exe;
    }

    [Fact]
    public void Returns_null_when_no_root_contains_a_player()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty", "version-x"));

        Assert.Null(new InstallationLocator([Path.Combine(_root, "empty"), Path.Combine(_root, "missing")]).Find());
    }

    [Fact]
    public void Picks_the_most_recent_player_across_all_roots()
    {
        AddPlayer("a", "version-old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newest = AddPlayer("b", "version-new", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        var found = new InstallationLocator([Path.Combine(_root, "a"), Path.Combine(_root, "b")]).Find();

        Assert.Equal(newest, found?.PlayerPath);
        Assert.Equal("version-new", found?.VersionFolder);
    }

    [Fact]
    public void Ignores_version_folders_without_a_player_executable()
    {
        Directory.CreateDirectory(Path.Combine(_root, "a", "version-studio-only"));
        var player = AddPlayer("a", "version-player", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(player, new InstallationLocator([Path.Combine(_root, "a")]).Find()?.PlayerPath);
    }

    [Fact]
    public void An_unreadable_versions_folder_is_skipped_instead_of_throwing()
    {
        var player = AddPlayer("readable", "version-ok", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var locked = new DirectoryInfo(Path.Combine(_root, "locked"));
        locked.Create();
        // Same failure Windows gives for a Versions folder another launcher is replacing or has locked down.
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var acl = locked.GetAccessControl();
        acl.AddAccessRule(deny);
        locked.SetAccessControl(acl);
        try
        {
            var found = new InstallationLocator([locked.FullName, Path.Combine(_root, "readable")]).Find();

            Assert.Equal(player, found?.PlayerPath);
        }
        finally
        {
            acl.RemoveAccessRule(deny);
            locked.SetAccessControl(acl);
        }
    }
}
