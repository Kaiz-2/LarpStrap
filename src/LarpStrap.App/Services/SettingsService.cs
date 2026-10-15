using System.IO;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Settings;

namespace LarpStrap.App.Services;

/// <summary>Holds the current settings, persists every change and notifies listeners.</summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly AppLog _log;

    private SettingsService(string path, SettingsLoadResult loaded, AppLog log)
    {
        FilePath = path;
        _log = log;
        _store = new SettingsStore(path);
        Current = loaded.Settings;
        RecoveredFromCorruptFile = loaded.RecoveredFromCorruptFile;
    }

    public string FilePath { get; }
    public AppSettings Current { get; private set; }
    public bool RecoveredFromCorruptFile { get; }

    public event Action<AppSettings>? Changed;

    public static SettingsService Load(string path, AppLog log) => new(path, new SettingsStore(path).Load(), log);

    /// <summary>Applies the change in memory even if saving fails; returns an error message in that case.</summary>
    public string? Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current).Normalize();
        string? error = null;
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(LogLevel.Warning, "Settings.SaveFailed", ("exception", ex));
            error = "No se pudo guardar la configuración. El cambio se aplica hasta que cierres LarpStrap.";
        }
        Changed?.Invoke(Current);
        return error;
    }
}
