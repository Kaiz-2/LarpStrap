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

    [Fact]
    public void The_updater_command_installs_silently_after_that_process_and_reopens() =>
        Assert.Equal(new SetupArguments(true, false, null, true, true, true, 4242), SetupArguments.Parse(["--update", "4242"]));

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--dir")]                          // missing value
    [InlineData("--dir", "--silent")]              // a switch is not a folder
    [InlineData("--silent", "--silent")]           // repeated
    [InlineData("--uninstall", "--launch")]        // install-only switch
    [InlineData("--update")]                       // missing pid
    [InlineData("--update", "0")]
    [InlineData("--update", "-5")]
    [InlineData("--update", "12", "--dir", @"D:\Otro")] // an update never moves the install
    [InlineData("--update", "12", "--no-desktop-shortcut")] // nor changes its shortcuts
    [InlineData("--update", "12", "--uninstall")]
    [InlineData("roblox-player:1+launchmode:app")] // not the app: the installer opens no links
    public void Anything_else_is_rejected(params string[] args) => Assert.Null(SetupArguments.Parse(args));
}
