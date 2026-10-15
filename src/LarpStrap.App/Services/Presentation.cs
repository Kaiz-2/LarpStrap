using System.Globalization;
using LarpStrap.Core.Instances;

namespace LarpStrap.App.Services;

public enum StatusKind
{
    Neutral,
    Ok,
    Info,
    Warn,
    Error,
}

/// <summary>User-facing wording (docs/design/ux-copy.md). Technical detail stays in the diagnostic line.</summary>
public static class Presentation
{
    public static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static (string Summary, string Guidance, StatusKind Kind) Describe(LaunchResult result) => result.Outcome switch
    {
        LaunchOutcome.Started => ("Roblox está abierto.", "", StatusKind.Ok),
        LaunchOutcome.InstallationNotFound => ("No se encontró Roblox.", "Instala Roblox y vuelve a intentarlo.", StatusKind.Error),
        LaunchOutcome.BlockedByBackground =>
            ("Hay un proceso de Roblox en segundo plano.", "LarpStrap no lo cierra. Ciérralo desde el icono de Roblox en la bandeja de Windows y vuelve a intentarlo.", StatusKind.Warn),
        LaunchOutcome.CloseTimedOut =>
            ("El cliente abierto no se cerró.", "Ciérralo desde Clientes abiertos o con «Forzar cierre» y vuelve a intentarlo.", StatusKind.Warn),
        LaunchOutcome.AlreadyOpen =>
            ("Ya hay un cliente abierto.", "Ciérralo o usa «Cerrar y lanzar».", StatusKind.Warn),
        LaunchOutcome.StartFailed => ("No se pudo iniciar Roblox.", "Windows rechazó abrir el programa. Comprueba que el antivirus no lo bloquea y vuelve a intentarlo.", StatusKind.Error),
        LaunchOutcome.ExitedDuringLaunch => ("Roblox se cerró al arrancar.", "Vuelve a intentarlo; si se repite, revisa Diagnóstico.", StatusKind.Error),
        LaunchOutcome.TimedOut => ("Roblox tarda en abrirse.", "Sigue arrancando en segundo plano; puedes esperar o usar «Forzar cierre…» en su fila.", StatusKind.Warn),
        LaunchOutcome.Cancelled when result.Pid is not null => ("Lanzamiento cancelado.", "El cliente que ya se había abierto sigue en ejecución.", StatusKind.Neutral),
        LaunchOutcome.Cancelled => ("Lanzamiento cancelado.", "", StatusKind.Neutral),
        _ => ("Resultado desconocido.", "", StatusKind.Neutral),
    };

    public static (string Text, StatusKind Kind) Describe(InstanceStatus status) => status switch
    {
        InstanceStatus.Starting => ("Iniciando", StatusKind.Info),
        InstanceStatus.Running => ("En ejecución", StatusKind.Ok),
        InstanceStatus.NotResponding => ("No responde", StatusKind.Warn),
        InstanceStatus.Background => ("En segundo plano", StatusKind.Neutral),
        InstanceStatus.Closed => ("Cerrado", StatusKind.Neutral),
        // Without a crash report it may simply be the user closing Roblox: neutral, not an error.
        InstanceStatus.ExitedUnexpectedly => ("Se cerró fuera de LarpStrap", StatusKind.Neutral),
        InstanceStatus.Crashed => ("Se cerró por un error", StatusKind.Error),
        _ => ("Desconocido", StatusKind.Neutral),
    };

    public static string Bytes(long? bytes) => bytes switch
    {
        null => "—",
        >= 1L << 30 => string.Format(Spanish, "{0:0.0} GB", bytes.Value / (double)(1L << 30)),
        _ => string.Format(Spanish, "{0:0} MB", bytes.Value / (double)(1L << 20)),
    };

    public static string Since(DateTime? startUtc, DateTime nowUtc)
    {
        if (startUtc is not { } start) return "—";
        var age = nowUtc - start;
        var duration = age.TotalMinutes < 1 ? "menos de 1 min"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min"
            : $"{(int)age.TotalHours} h {age.Minutes} min";
        return $"desde las {start.ToLocalTime():HH:mm} · {duration}";
    }

    public static string Ago(TimeSpan span) => span.TotalSeconds < 60
        ? $"hace {Math.Max(0, (int)span.TotalSeconds)} s"
        : $"hace {(int)span.TotalMinutes} min";
}
