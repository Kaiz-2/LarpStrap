using LarpStrap.Core.Setup;

namespace LarpStrap.Core.Tests.Setup;

public sealed class SetupArgumentsTests
{
    [Fact]
    public void No_arguments_opens_the_wizard_with_both_shortcuts() =>
        Assert.Equal(new SetupArguments(false, false, null, true, true, false), SetupArguments.Parse([]));

    [Fact]
    public void Silent_install_options_are_read()
    {
        var args = SetupArguments.Parse(["--silent", "--dir", @"D:\Juegos\LarpStrap", "--no-desktop-shortcut", "--launch"]);

        Assert.Equal(new SetupArguments(true, false, @"D:\Juegos\LarpStrap", true, false, true), args);
    }

    [Fact]
    public void The_registered_uninstall_commands_are_accepted()
    {
        Assert.Equal(new SetupArguments(false, true, null, true, true, false), SetupArguments.Parse(["--uninstall"]));
        Assert.Equal(new SetupArguments(true, true, null, true, true, false), SetupArguments.Parse(["--uninstall", "--silent"]));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--dir")]                          // missing value
    [InlineData("--dir", "--silent")]              // a switch is not a folder
    [InlineData("--silent", "--silent")]           // repeated
    [InlineData("--uninstall", "--launch")]        // install-only switch
    [InlineData("roblox-player:1+launchmode:app")] // not the app: the installer opens no links
    public void Anything_else_is_rejected(params string[] args) => Assert.Null(SetupArguments.Parse(args));
}
