using System.Text.Json;

namespace LarpStrap.Core.Settings;

public sealed class SettingsStore(string path)
{
    public SettingsLoadResult Load()
    {
        if (!File.Exists(path))
            return new SettingsLoadResult(new AppSettings(), RecoveredFromCorruptFile: false);

        try
        {
            var settings = JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.AppSettings);
            return new SettingsLoadResult((settings ?? new AppSettings()).Normalize(), RecoveredFromCorruptFile: false);
        }
        catch (JsonException)
        {
            // Keep the broken file for diagnosis; a bad settings file must never stop the app from starting.
            TryKeepCopy();
            return new SettingsLoadResult(new AppSettings(), RecoveredFromCorruptFile: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked (e.g. by an antivirus scan) or unreadable: same rule, start with defaults.
            return new SettingsLoadResult(new AppSettings(), RecoveredFromCorruptFile: true);
        }
    }

    private void TryKeepCopy()
    {
        try { File.Copy(path, path + ".bad", overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* diagnosis copy is best effort */ }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        // Write next to the target and swap, so a crash mid-write never leaves a truncated file.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        File.Move(tmp, path, overwrite: true);
    }
}
