using System.Diagnostics;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using LarpStrap.Core.Setup;
using Microsoft.Win32;

namespace LarpStrap.Core.Tests.Setup;

// Real file system, registry and shortcuts, but only in temp folders and a throwaway HKCU\Software\LarpStrap.Tests key.
public sealed class InstallerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("larpstrap-setup-").FullName;
    private readonly string _key = $@"Software\LarpStrap.Tests\{Guid.NewGuid():N}\Uninstall\LarpStrap";

    // Any PE file with a version resource stands in for LarpStrap.exe; the installer checks the installed version.
    private static readonly string FakeExe = typeof(Installer).Assembly.Location;
    private static readonly Version FakeVersion = Version.Parse(FileVersionInfo.GetVersionInfo(FakeExe).FileVersion!);

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key[..(_key.IndexOf(@"\Uninstall", StringComparison.Ordinal))], throwOnMissingSubKey: false);
        using (var parent = Registry.CurrentUser.OpenSubKey(@"Software\LarpStrap.Tests"))
            if (parent is { SubKeyCount: 0 }) Registry.CurrentUser.DeleteSubKey(@"Software\LarpStrap.Tests", throwOnMissingSubKey: false);
        Directory.Delete(_root, recursive: true);
    }

    private InstallTarget Target(string dir = "App") => new(Path.Combine(_root, dir), Path.Combine(_root, "StartMenu"), Path.Combine(_root, "Desktop"), _key);

    private static InstallOptions Options(bool desktop = true) => new(StartMenuShortcut: true, DesktopShortcut: desktop, FakeVersion);

    private static MemoryStream Payload(params (string Name, string Content)[] extra)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntryFromFile(FakeExe, InstallTarget.AppExe);
            foreach (var (name, content) in extra)
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
                    writer.Write(content);
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_clean_install_copies_files_creates_shortcuts_registers_and_verifies()
    {
        var target = Target();

        var result = new Installer(target, _ => false).Install(Payload(("LICENSE", "MIT"), ("licenses/x.txt", "n")), Options(), null, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.FilesVerified);
        Assert.Equal("MIT", File.ReadAllText(Path.Combine(target.Directory, "LICENSE")));
        Assert.True(File.Exists(Path.Combine(target.Directory, "licenses", "x.txt")));
        Assert.Equal(target.AppPath, ShellLink.ReadTarget(target.StartMenuShortcut), ignoreCase: true);
        Assert.Equal(target.AppPath, ShellLink.ReadTarget(target.DesktopShortcut), ignoreCase: true);
        var existing = Installer.FindExisting(_key);
        Assert.NotNull(existing);
        Assert.Equal(target.Directory, existing.Directory, ignoreCase: true);
        Assert.Equal(FakeVersion, existing.Version);
        Assert.Empty(Directory.GetFileSystemEntries(target.Directory, ".larpstrap*"));
    }

    [Fact]
    public void An_upgrade_replaces_files_drops_stale_ones_and_keeps_files_it_did_not_install()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(("old-only.txt", "1"), ("LICENSE", "v1")), Options(), null, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(target.Directory, "user-notes.txt"), "mine");

        new Installer(target, _ => false).Install(Payload(("LICENSE", "v2")), Options(desktop: false), null, TestContext.Current.CancellationToken);

        Assert.Equal("v2", File.ReadAllText(Path.Combine(target.Directory, "LICENSE")));
        Assert.False(File.Exists(Path.Combine(target.Directory, "old-only.txt")));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(target.Directory, "user-notes.txt")));
        Assert.False(File.Exists(target.DesktopShortcut)); // unticked this time: our shortcut is removed
        Assert.True(File.Exists(target.StartMenuShortcut));
    }

    [Fact]
    public void A_failure_while_swapping_files_restores_the_previous_installation()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(("LICENSE", "v1"), ("a.txt", "a1")), Options(), null, TestContext.Current.CancellationToken);

        var failing = new Installer(target, _ => false) { BeforeReplace = f => { if (f == "a.txt") throw new IOException("locked"); } };
        var error = Assert.Throws<InstallerException>(() => failing.Install(Payload(("LICENSE", "v2"), ("a.txt", "a2"), ("new.txt", "n")), Options(), null, TestContext.Current.CancellationToken));

        Assert.Contains("como estaba", error.Message);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(target.Directory, "LICENSE")));
        Assert.Equal("a1", File.ReadAllText(Path.Combine(target.Directory, "a.txt")));
        Assert.False(File.Exists(Path.Combine(target.Directory, "new.txt")));
        Assert.Empty(Directory.GetFileSystemEntries(target.Directory, ".larpstrap*"));
    }

    [Fact]
    public void An_install_interrupted_mid_swap_is_rolled_back_on_the_next_run()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(("LICENSE", "v1")), Options(), null, TestContext.Current.CancellationToken);

        // A crash is not an IOException: nothing catches it, as when the process dies.
        var crashing = new Installer(target, _ => false) { BeforeReplace = f => { if (f == InstallTarget.AppExe) throw new SimulatedCrash(); } };
        Assert.Throws<SimulatedCrash>(() => crashing.Install(Payload(("LICENSE", "v2")), Options(), null, TestContext.Current.CancellationToken));
        Assert.Contains(Directory.GetFiles(target.Directory), f => f.EndsWith(".larpstrap-old", StringComparison.Ordinal));

        Assert.True(new Installer(target, _ => false).RecoverInterrupted());

        Assert.Equal("v1", File.ReadAllText(Path.Combine(target.Directory, "LICENSE")));
        Assert.Empty(Directory.GetFileSystemEntries(target.Directory, ".larpstrap*"));
        Assert.Empty(Directory.GetFiles(target.Directory, "*.larpstrap-old"));
    }

    [Fact]
    public void Cancelling_during_extraction_leaves_nothing_behind()
    {
        var target = Target();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => new Installer(target, _ => false).Install(Payload(("LICENSE", "x")), Options(), null, cts.Token));

        Assert.False(Directory.Exists(target.Directory));
        Assert.Null(Installer.FindExisting(_key));
    }

    [Fact]
    public void Nothing_happens_while_LarpStrap_is_running_from_the_folder()
    {
        var error = Assert.Throws<InstallerException>(() => new Installer(Target(), _ => true).Install(Payload(), Options(), null, TestContext.Current.CancellationToken));

        Assert.Contains("está abierto", error.Message);
        Assert.False(Directory.Exists(Target().Directory));
    }

    [Fact]
    public void A_package_with_a_path_outside_the_folder_is_refused()
    {
        var target = Target();

        var error = Assert.Throws<InstallerException>(() => new Installer(target, _ => false).Install(Payload(("../evil.txt", "x")), Options(), null, TestContext.Current.CancellationToken));

        Assert.Contains("ruta no válida", error.Message);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.False(Directory.Exists(target.Directory));
    }

    [Fact]
    public void Uninstall_removes_only_what_it_installed_and_never_the_settings()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(("LICENSE", "x"), ("licenses/a.txt", "a")), Options(), null, TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(target.Directory, "user-notes.txt"), "mine");
        File.WriteAllText(Path.Combine(_root, "Desktop", "Other.lnk"), "not ours");
        var settings = Directory.CreateDirectory(Path.Combine(_root, "LarpStrapData")).FullName;
        File.WriteAllText(Path.Combine(settings, "Settings.json"), "{}");

        var result = new Installer(target, _ => false).Uninstall();

        Assert.Equal(3, result.FilesRemoved);
        Assert.False(result.DirectoryRemoved); // the user's own file keeps the folder
        Assert.Equal(["user-notes.txt"], Directory.GetFileSystemEntries(target.Directory).Select(Path.GetFileName));
        Assert.False(File.Exists(target.StartMenuShortcut));
        Assert.False(File.Exists(target.DesktopShortcut));
        Assert.True(File.Exists(Path.Combine(_root, "Desktop", "Other.lnk")));
        Assert.True(File.Exists(Path.Combine(settings, "Settings.json")));
        Assert.Null(Installer.FindExisting(_key));
    }

    [Fact]
    public void Uninstall_removes_the_folder_when_only_its_files_were_there()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(("LICENSE", "x")), Options(), null, TestContext.Current.CancellationToken);

        Assert.True(new Installer(target, _ => false).Uninstall().DirectoryRemoved);
        Assert.False(Directory.Exists(target.Directory));
    }

    [Fact]
    public void Uninstall_never_follows_a_tampered_manifest_outside_the_folder()
    {
        var target = Target();
        new Installer(target, _ => false).Install(Payload(), Options(), null, TestContext.Current.CancellationToken);
        var victim = Path.Combine(_root, "victim.txt");
        File.WriteAllText(victim, "keep");
        File.WriteAllText(Path.Combine(target.Directory, InstallTarget.ManifestFile),
            $$"""{"Version":"1.0","Files":["LarpStrap.exe","..\\victim.txt","{{victim.Replace(@"\", @"\\")}}"],"Shortcuts":[]}""");

        new Installer(target, _ => false).Uninstall();

        Assert.True(File.Exists(victim));
    }

    [Theory]
    [InlineData("", "Elige una carpeta")]
    [InlineData(@"relative\path", "ruta completa")]
    [InlineData(@"\\server\share\LarpStrap", "de la red")]
    [InlineData(@"C:\", "raíz")]
    [InlineData(@"C:\Lar*Strap", "no válidos")]
    public void Invalid_destinations_are_explained(string path, string expected) =>
        Assert.Contains(expected, Installer.ValidateDirectory(path, null));

    [Fact]
    public void System_folders_that_need_administrator_rights_are_refused()
    {
        Assert.Contains("administrador", Installer.ValidateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LarpStrap"), null));
        Assert.Contains("administrador", Installer.ValidateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "LarpStrap"), null));
    }

    [Fact]
    public void A_non_empty_folder_that_is_not_an_installation_is_refused_but_an_installation_is_accepted()
    {
        var foreign = Directory.CreateDirectory(Path.Combine(_root, "Foreign")).FullName;
        File.WriteAllText(Path.Combine(foreign, "something.txt"), "x");
        Assert.Contains("no está vacía", Installer.ValidateDirectory(foreign, null));

        var target = Target();
        new Installer(target, _ => false).Install(Payload(), Options(), null, TestContext.Current.CancellationToken);
        Assert.Null(Installer.ValidateDirectory(target.Directory, Installer.FindExisting(_key)));
        Assert.Null(Installer.ValidateDirectory(Path.Combine(_root, "New", "LarpStrap"), null));
    }

    [Fact]
    public void A_folder_without_write_permission_is_reported()
    {
        // A deny rule for the current user, not a system folder: CI runs as administrator, who can write in System32.
        var denied = Directory.CreateDirectory(Path.Combine(_root, "Denied"));
        var user = WindowsIdentity.GetCurrent().User!;
        var rule = new FileSystemAccessRule(user, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.WriteData, AccessControlType.Deny);
        var acl = denied.GetAccessControl();
        acl.AddAccessRule(rule);
        denied.SetAccessControl(acl);
        try
        {
            Assert.Contains("permiso", Installer.CheckWritable(Path.Combine(denied.FullName, "LarpStrap"), 1));
        }
        finally
        {
            acl.RemoveAccessRule(rule);
            denied.SetAccessControl(acl);
        }

        // A writable folder passes, and the probe file is never left behind.
        Assert.Null(Installer.CheckWritable(Path.Combine(_root, "Writable", "Deep"), 1));
        Assert.False(Directory.Exists(Path.Combine(_root, "Writable")));
        Assert.Empty(denied.GetFileSystemInfos());
    }

    private sealed class SimulatedCrash : Exception;
}
