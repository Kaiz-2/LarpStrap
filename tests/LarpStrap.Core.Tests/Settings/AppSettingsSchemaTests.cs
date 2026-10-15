using LarpStrap.Core.Settings;

namespace LarpStrap.Core.Tests.Settings;

public sealed class AppSettingsSchemaTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-schema-").FullName;
    private string SettingsPath => Path.Combine(_dir, "Settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Version_1_file_loads_with_defaults_for_new_values_and_is_upgraded()
    {
        File.WriteAllText(SettingsPath, """{ "Theme": "Dark", "CloseToTray": false }""");

        var settings = new SettingsStore(SettingsPath).Load().Settings;

        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.False(settings.CloseToTray);
        Assert.Equal(AccentColor.Violet, settings.Accent);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Empty(settings.FavoriteGames);
    }

    [Fact]
    public void Out_of_range_values_from_a_hand_edited_file_are_clamped()
    {
        // LaunchDelaySeconds is a setting older versions saved; it no longer exists and is ignored on load.
        File.WriteAllText(SettingsPath, """{ "TextScale": 9, "LaunchTimeoutSeconds": 1, "LaunchDelaySeconds": -4 }""");

        var result = new SettingsStore(SettingsPath).Load();

        Assert.False(result.RecoveredFromCorruptFile);
        Assert.Equal(AppSettings.MaxTextScale, result.Settings.TextScale);
        Assert.Equal(10, result.Settings.LaunchTimeoutSeconds);
    }

    [Fact]
    public void Invalid_and_duplicate_favorites_are_dropped_on_load()
    {
        File.WriteAllText(SettingsPath, """
            { "Favorites": [
                { "PlaceId": 920587237, "Name": "  Adopt Me  " },
                { "PlaceId": 920587237, "Name": "Duplicate" },
                { "PlaceId": 0, "Name": "Zero" },
                { "PlaceId": 5, "Name": "   " }
            ] }
            """);

        var favorites = new SettingsStore(SettingsPath).Load().Settings.FavoriteGames;

        var only = Assert.Single(favorites);
        Assert.Equal(920587237, only.PlaceId);
        Assert.Equal("Adopt Me", only.Name);
    }

    [Fact]
    public void Favorites_round_trip_through_the_store()
    {
        var store = new SettingsStore(SettingsPath);

        store.Save(new AppSettings(Favorites: [new FavoriteGame(1818, "Classic Crossroads")]));

        Assert.Equal(new FavoriteGame(1818, "Classic Crossroads"), Assert.Single(store.Load().Settings.FavoriteGames));
    }

    [Fact]
    public void Undefined_theme_and_accent_numbers_fall_back_to_defaults()
    {
        File.WriteAllText(SettingsPath, """{ "Theme": 7, "Accent": 99 }""");

        var settings = new SettingsStore(SettingsPath).Load().Settings;

        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.Equal(AccentColor.Violet, settings.Accent);
    }

    [Fact]
    public void New_settings_default_to_the_violet_brand_accent_and_the_mica_backdrop()
    {
        var settings = new SettingsStore(SettingsPath).Load().Settings;

        Assert.Equal(AccentColor.Violet, settings.Accent);
        Assert.Equal(WindowBackdrop.Mica, settings.Backdrop);
    }

    [Fact]
    public void A_saved_accent_from_an_older_version_is_kept()
    {
        File.WriteAllText(SettingsPath, """{ "Accent": "Amber" }""");

        Assert.Equal(AccentColor.Amber, new SettingsStore(SettingsPath).Load().Settings.Accent);
    }

    [Fact]
    public void An_undefined_backdrop_number_falls_back_to_mica()
    {
        File.WriteAllText(SettingsPath, """{ "Backdrop": 42 }""");

        Assert.Equal(WindowBackdrop.Mica, new SettingsStore(SettingsPath).Load().Settings.Backdrop);
    }
}
