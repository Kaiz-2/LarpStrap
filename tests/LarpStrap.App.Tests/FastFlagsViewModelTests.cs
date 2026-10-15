using System.IO;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.Core.Logging;

namespace LarpStrap.App.Tests;

public sealed class FastFlagsViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-ffvm-").FullName;
    private readonly AppLog _log;
    private readonly SettingsService _settings;

    public FastFlagsViewModelTests()
    {
        _log = new AppLog(Path.Combine(_dir, "Logs"));
        _settings = SettingsService.Load(Path.Combine(_dir, "Settings.json"), _log);
    }

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private FastFlagItem Item(FastFlagsViewModel vm, string name) => vm.Flags.Single(f => f.Info.Name == name);

    [Fact]
    public void An_invalid_number_shows_an_error_and_is_not_saved()
    {
        var msaa = Item(new FastFlagsViewModel(_settings, new Dialogs(), _ => { }), "FIntDebugForceMSAASamples");

        msaa.Text = "64";

        Assert.True(msaa.HasError);
        Assert.Null(_settings.Current.FastFlagValues?.GetValueOrDefault("FIntDebugForceMSAASamples"));

        msaa.Text = "4";
        Assert.False(msaa.HasError);
        Assert.Equal("4", _settings.Current.FastFlagValues!["FIntDebugForceMSAASamples"]);
    }

    [Fact]
    public void Choosing_the_roblox_value_clears_the_flag_and_reset_all_clears_every_one()
    {
        var vm = new FastFlagsViewModel(_settings, new Dialogs(), _ => { });
        var sky = Item(vm, "FFlagDebugSkyGray");

        sky.Choice = "True";
        Assert.Equal("True", _settings.Current.FastFlagValues!["FFlagDebugSkyGray"]);
        sky.Choice = FastFlagItem.Default;
        Assert.Empty(_settings.Current.FastFlagValues!);

        sky.Choice = "False";
        vm.ResetAllCommand.Execute(null);
        Assert.Null(_settings.Current.FastFlagValues);
        Assert.Equal(FastFlagItem.Default, sky.Choice);
    }

    private sealed class Dialogs : IDialogService
    {
        public bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger) => true;
        public string? AskSavePath(string suggestedName, string filter) => null;
        public void OpenUri(Uri uri) { }
        public void OpenFolder(string path) { }
    }
}
