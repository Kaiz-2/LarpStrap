using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using LarpStrap.App.Services;
using LarpStrap.App.ViewModels;
using LarpStrap.Core.Logging;

namespace LarpStrap.App.Tests;

// No network: a fake handler plays Roblox's public game APIs.
public sealed class ShortcutsViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-shortcuts-").FullName;
    private readonly AppLog _log;
    private readonly SettingsService _settings;

    public ShortcutsViewModelTests()
    {
        _log = new AppLog(Path.Combine(_dir, "Logs"));
        _settings = SettingsService.Load(Path.Combine(_dir, "Settings.json"), _log);
    }

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private ShortcutsViewModel ViewModel(FakeRoblox roblox) =>
        new(_settings, new Dialogs(), _ => { }, new ExperienceLookupService(new HttpClient(roblox)), new RobloxStoreLauncher());

    private static async Task Settled(ShortcutsViewModel vm)
    {
        for (var i = 0; i < 100 && (vm.IsLookingUp || vm.LookupStatus.StartsWith("Buscando", StringComparison.Ordinal)); i++)
            await Task.Delay(20, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_title_from_Roblox_fills_the_name_only_when_the_user_typed_none()
    {
        var vm = ViewModel(new FakeRoblox("Adopt Me!"));

        vm.NewGame = "920587237";
        await Settled(vm);

        Assert.Equal("Adopt Me!", vm.NewName);
    }

    [Fact]
    public async Task A_name_the_user_typed_is_never_replaced()
    {
        var vm = ViewModel(new FakeRoblox("Adopt Me!"));

        vm.NewName = "Mi juego";
        vm.NewGame = "https://www.roblox.com/games/920587237/x";
        await Settled(vm);

        Assert.Equal("Mi juego", vm.NewName);
    }

    [Fact]
    public async Task Typing_quickly_asks_Roblox_once_for_the_final_game()
    {
        var roblox = new FakeRoblox("Adopt Me!");
        var vm = ViewModel(roblox);

        foreach (var partial in new[] { "9", "92", "920", "9205", "920587237" })
            vm.NewGame = partial;
        await Settled(vm);

        Assert.All(roblox.Requests, r => Assert.DoesNotMatch(@"places/(9|92|920|9205)/|placeIds=(9|92|920|9205)&", r));
        Assert.Equal(3, roblox.Requests.Count); // universe, game details, icon: once, for 920587237 only
    }

    [Fact]
    public void Looking_up_Roblox_Windows_uses_no_external_process()
    {
        // On a PC without the Store app this is null; with it, an app id like Package!App. Either way, no PowerShell.
        var appId = RobloxStoreLauncher.FindStoreAppId();

        Assert.True(appId is null || Regex.IsMatch(appId, "^[A-Za-z0-9._-]+![A-Za-z0-9._-]+$"), appId);
    }

    private sealed class FakeRoblox(string title) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            lock (Requests) Requests.Add(uri);
            var body = uri.Contains("/universe", StringComparison.Ordinal) ? """{"universeId":383310974}"""
                : uri.Contains("games.roblox.com", StringComparison.Ordinal) ? $$"""{"data":[{"name":"{{title}}"}]}"""
                : """{"data":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class Dialogs : IDialogService
    {
        public bool Confirm(string title, string message, string confirmText, string cancelText, bool isDanger) => true;
        public string? AskSavePath(string suggestedName, string filter) => null;
        public void OpenUri(Uri uri) { }
        public void OpenFolder(string path) { }
    }
}
