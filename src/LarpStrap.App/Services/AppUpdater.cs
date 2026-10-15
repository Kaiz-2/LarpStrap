using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Setup;
using LarpStrap.Core.Updates;

namespace LarpStrap.App.Services;

/// <summary>
/// Checks GitHub for a signed update and, when Roblox is not in use, closes LarpStrap and lets the downloaded installer
/// update it silently (docs/adr/0009). Only for copies installed with the installer; the portable zip never updates itself.
/// </summary>
/// <param name="robloxBusy">True while a client is open or starting, or a launch is in progress: never interrupt a game.</param>
public sealed class AppUpdater(AppLog log, string downloadDirectory, Func<bool> robloxBusy, Action<string> announce, Action exit)
{
    private static readonly Version Current = typeof(AppUpdater).Assembly.GetName().Version ?? new Version(0, 0, 0);
    private bool _running;

    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, InstallTarget.ManifestFile));

    /// <summary>Returns what happened, in words for the user. At startup (<paramref name="manual"/> false) "nothing new" stays quiet.</summary>
    public async Task<string> CheckAsync(bool manual)
    {
        if (!IsInstalled)
            return "Esta es la copia portable (zip): solo la versión instalada se actualiza sola. Descarga la nueva desde GitHub.";
        if (_running) return "Ya se está buscando una actualización.";
        _running = true;
        try
        {
            using var http = new HttpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var updater = new Updater(http, UpdateTrust.PublicKey, Current, downloadDirectory);
            updater.DeleteOldDownloads();
            var check = await updater.CheckAndDownloadAsync(cts.Token);
            log.Write(check.Status is UpdateStatus.Rejected ? LogLevel.Warning : LogLevel.Info, "Update.Checked",
                ("result", check.Status), ("version", check.Available?.ToString()), ("reason", check.Reason), ("origin", manual ? "manual" : "start"));

            var message = check.Status switch
            {
                UpdateStatus.Ready => await InstallAsync(check),
                UpdateStatus.Rejected => "Se ha descartado una actualización que no superó la verificación (firma, origen o contenido). No se ha instalado nada.",
                UpdateStatus.UpToDate => manual ? $"LarpStrap está al día (versión {Format(Current)})." : "",
                UpdateStatus.NoRelease => manual ? "Todavía no hay ninguna versión publicada que se instale sola." : "",
                _ => manual ? "No se pudo comprobar si hay actualizaciones (sin conexión o GitHub no responde). Se volverá a intentar al iniciar." : "",
            };
            if (message.Length > 0) announce(message);
            return message;
        }
        finally
        {
            _running = false;
        }
    }

    private async Task<string> InstallAsync(UpdateCheck check)
    {
        var version = Format(check.Available!);
        var later = $"LarpStrap {version} está descargado y verificado. Se instalará la próxima vez que abras LarpStrap sin Roblox abierto.";
        if (robloxBusy()) return later;

        announce($"Actualizando a LarpStrap {version}: se cerrará y volverá a abrirse en unos segundos.");
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (robloxBusy()) return later; // a launch started meanwhile

        // Checked again right before running: the file sat on disk since it was verified.
        if (!Updater.StillMatches(check.SetupPath!, check.Sha256!))
        {
            log.Write(LogLevel.Warning, "Update.ChangedOnDisk");
            try { File.Delete(check.SetupPath!); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return "La actualización descargada cambió en el disco y se ha descartado. Se volverá a descargar al iniciar.";
        }
        try
        {
            var start = new ProcessStartInfo(check.SetupPath!) { UseShellExecute = false };
            start.ArgumentList.Add("--update");
            start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            Process.Start(start)?.Dispose();
        }
        catch (Win32Exception ex)
        {
            log.Write(LogLevel.Warning, "Update.StartFailed", ("exception", ex));
            return "No se pudo abrir el instalador de la actualización (¿lo ha bloqueado el antivirus?). Se volverá a intentar al iniciar.";
        }
        log.Write(LogLevel.Info, "Update.Started", ("version", version));
        exit();
        return "";
    }

    private static string Format(Version v) => $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
}
