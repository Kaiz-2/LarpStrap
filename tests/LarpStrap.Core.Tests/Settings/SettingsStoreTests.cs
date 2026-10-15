using LarpStrap.Core.Settings;

namespace LarpStrap.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("larpstrap-tests-").FullName;
    private string SettingsPath => Path.Combine(_dir, "Settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_returns_defaults_when_file_does_not_exist()
    {
        var result = new SettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings(), result.Settings);
        Assert.False(result.RecoveredFromCorruptFile);
    }

    [Fact]
    public void Interface_preferences_round_trip_and_older_files_open_in_simple_mode()
    {
        var store = new SettingsStore(SettingsPath);
        File.WriteAllText(SettingsPath, """{ "SchemaVersion": 2, "Theme": "Dark" }""");

        var older = store.Load().Settings;
        Assert.False(older.AdvancedMode);
        Assert.False(older.CompactNavigation);

        store.Save(older with { AdvancedMode = true, CompactNavigation = true });
        var loaded = store.Load().Settings;
        Assert.True(loaded.AdvancedMode);
        Assert.True(loaded.CompactNavigation);
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
    }

    [Fact]
    public void Save_then_Load_round_trips_all_values()
    {
        var store = new SettingsStore(SettingsPath);
        var saved = new AppSettings(Theme: ThemeMode.Dark, CloseToTray: false);

        store.Save(saved);

        Assert.Equal(saved, store.Load().Settings);
    }

    [Fact]
    public void Save_creates_missing_parent_directory()
    {
        var nestedPath = Path.Combine(_dir, "nested", "Settings.json");

        new SettingsStore(nestedPath).Save(new AppSettings());

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Save_leaves_no_temporary_file_behind()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings());

        Assert.Equal(["Settings.json"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void Load_of_corrupt_file_returns_defaults_and_flags_recovery()
    {
        File.WriteAllText(SettingsPath, "{ this is not json");

        var result = new SettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings(), result.Settings);
        Assert.True(result.RecoveredFromCorruptFile);
    }

    [Fact]
    public void Load_of_corrupt_file_keeps_a_copy_for_diagnosis()
    {
        const string corrupt = "{ this is not json";
        File.WriteAllText(SettingsPath, corrupt);

        new SettingsStore(SettingsPath).Load();

        Assert.Equal(corrupt, File.ReadAllText(SettingsPath + ".bad"));
    }

    [Fact]
    public void Load_of_locked_file_returns_defaults_instead_of_throwing()
    {
        File.WriteAllText(SettingsPath, """{ "Theme": "Dark" }""");
        using var exclusiveLock = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = new SettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings(), result.Settings);
        Assert.True(result.RecoveredFromCorruptFile);
    }

    [Fact]
    public void Load_ignores_unknown_properties_from_newer_versions()
    {
        File.WriteAllText(SettingsPath, """{ "Theme": "Light", "SomeFutureSetting": 42 }""");

        var result = new SettingsStore(SettingsPath).Load();

        Assert.Equal(ThemeMode.Light, result.Settings.Theme);
        Assert.False(result.RecoveredFromCorruptFile);
    }
}
