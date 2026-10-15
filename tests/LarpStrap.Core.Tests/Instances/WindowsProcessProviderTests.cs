using LarpStrap.Core.Instances;

namespace LarpStrap.Core.Tests.Instances;

public sealed class WindowsProcessProviderTests
{
    private const string Exe = @"C:\Roblox\Versions\version-x\RobloxPlayerBeta.exe";

    [Fact]
    public void Client_is_started_through_the_shell_so_it_inherits_no_launcher_handles()
    {
        var info = WindowsProcessProvider.CreateStartInfo(Exe, "--app");

        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void Client_runs_from_its_own_version_folder()
    {
        var info = WindowsProcessProvider.CreateStartInfo(Exe, "--app");

        Assert.Equal(@"C:\Roblox\Versions\version-x", info.WorkingDirectory);
        Assert.Equal("--app", info.Arguments);
    }
}
