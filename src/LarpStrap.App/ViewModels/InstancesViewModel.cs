using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LarpStrap.App.Services;
using LarpStrap.Core.Instances;
using LarpStrap.Core.Logging;

namespace LarpStrap.App.ViewModels;

/// <summary>Instance panel. All process work goes through <see cref="InstancePanelService"/>; this class only shapes it for the view.</summary>
public sealed partial class InstancesViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(15);

    private readonly IDialogService _dialogs;
    private readonly AppLog _log;
    private readonly Action<string> _announce;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };
    private InstancePanelService _service;
    private CancellationTokenSource? _launchCts;
    private DateTime? _lastRefreshUtc;
    private bool _refreshing;
    private int _refreshFailures;
    private bool _closeFirst;
    private bool _blockedByBackground;
    private bool _blockedByStarting;

    public InstancesViewModel(InstancePanelService service, IDialogService dialogs, AppLog log, Action<string> announce)
    {
        _service = service;
        _dialogs = dialogs;
        _log = log;
        _announce = announce;
        _timer.Tick += async (_, _) => await RefreshAsync();
        ApplyServiceState();
    }

    public ObservableCollection<InstanceItemViewModel> Items { get; } = [];
    public ObservableCollection<ActivityItem> Activity { get; } = [];

    [ObservableProperty] public partial bool IsSimulated { get; set; }
    [ObservableProperty] public partial string InstallationText { get; set; } = "";
    [ObservableProperty] public partial string InstallationDetail { get; set; } = "";
    [ObservableProperty] public partial bool InstallationFound { get; set; }
    [ObservableProperty] public partial string RefreshText { get; set; } = "Actualizando…";
    [ObservableProperty] public partial bool IsStale { get; set; }
    [ObservableProperty] public partial bool IsLaunching { get; set; }
    [ObservableProperty] public partial string PrimaryActionText { get; set; } = "Lanzar";
    [ObservableProperty] public partial string LaunchHint { get; set; } = "";

    /// <summary>One-line state for the home hero; never claims a launch is possible when it is not.</summary>
    [ObservableProperty] public partial string StatusHeadline { get; set; } = "";
    [ObservableProperty] public partial string SummaryText { get; set; } = "";

    public bool IsEmpty => Items.Count == 0;
    public bool HasActivity => Activity.Count > 0;
    public ActivityItem? LastActivity => Activity.FirstOrDefault();

    /// <summary>After each applied refresh: the snapshot and whether it came from SIMULATED clients.</summary>
    public event Action<PanelSnapshot, bool>? Refreshed;

    public void Start()
    {
        _ = RefreshAsync();
        _timer.Start();
    }

    /// <summary>Switches between real and simulated clients (demo mode) without restarting the app.</summary>
    public void UseService(InstancePanelService service)
    {
        _launchCts?.Cancel();
        _service = service;
        Items.Clear();
        ApplyServiceState();
        _ = RefreshAsync();
    }

    private void ApplyServiceState()
    {
        IsSimulated = _service.IsSimulated;
        var installation = _service.FindInstallation();
        InstallationFound = installation is not null;
        InstallationText = installation is null ? "Roblox no encontrado" : "Roblox detectado";
        InstallationDetail = installation?.VersionFolder ?? "";
        UpdatePrimaryAction();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var service = _service;
            var snapshot = await Task.Run(service.Refresh);
            // Demo mode was toggled meanwhile: these rows belong to the other provider and would carry the wrong label.
            if (service != _service) return;
            Apply(snapshot);
            Refreshed?.Invoke(snapshot, service.IsSimulated);
            _lastRefreshUtc = snapshot.RefreshedAtUtc;
            IsStale = false;
            if (_refreshFailures > 0)
                _log.Write(LogLevel.Info, "Panel.RefreshRecovered", ("count", _refreshFailures));
            _refreshFailures = 0;
        }
        catch (Exception ex)
        {
            // Runs from a timer: any failure must degrade to "stale", never take the app down.
            IsStale = _lastRefreshUtc is not { } last || DateTime.UtcNow - last > StaleAfter;
            // Logged on the first failure only; the recovery line carries how many refreshes failed (every 2 s).
            if (_refreshFailures++ == 0)
                _log.Write(LogLevel.Warning, "Panel.RefreshFailed", ("exception", ex));
        }
        finally
        {
            _refreshing = false;
            RefreshText = (_lastRefreshUtc, IsStale) switch
            {
                (null, _) => "Todavía no se ha podido leer el estado de los procesos. Reintentando…",
                ({ } last, true) => $"Sin actualizar desde {Presentation.Ago(DateTime.UtcNow - last)}. Reintentando…",
                ({ } last, false) => $"Actualizado {Presentation.Ago(DateTime.UtcNow - last)}",
            };
        }
    }

    // Updates rows in place (keyed by PID) so keyboard focus and screen-reader position survive refreshes.
    private void Apply(PanelSnapshot snapshot)
    {
        foreach (var gone in Items.Where(i => snapshot.Instances.All(s => s.Pid != i.Pid)).ToList())
            Items.Remove(gone);

        foreach (var instance in snapshot.Instances)
        {
            var row = Items.FirstOrDefault(i => i.Pid == instance.Pid);
            if (row is null)
            {
                row = new InstanceItemViewModel(instance.Pid, this);
                Items.Add(row);
            }
            var previous = row.Status;
            row.Update(instance, snapshot.RefreshedAtUtc, IsSimulated);
            // Instance.Exited is logged by the tracker, which sees every exit even when a launch refreshed first.
            if (instance.Status == previous) continue;
            if (instance.Status == InstanceStatus.ExitedUnexpectedly)
                AddActivity($"El cliente {instance.Pid} se cerró fuera de LarpStrap.", "Si no lo cerraste tú, revisa Diagnóstico.", StatusKind.Neutral, "");
            else if (instance.Status == InstanceStatus.Crashed)
                AddActivity($"El cliente {instance.Pid} se cerró por un error de Roblox.",
                    "Roblox guardó un informe de error. Vuelve a abrir el juego; si se repite, actualiza los controladores gráficos.", StatusKind.Error, "");
            else if (instance.Status == InstanceStatus.NotResponding)
                AddActivity($"El cliente {instance.Pid} no responde.",
                    "Puede ser una carga larga. Espera un poco; si no vuelve, usa «Cerrar» o «Forzar cierre…».", StatusKind.Warn, "");
        }

        var running = Items.Count(i => i.HasWindow);
        var background = Items.Count(i => i.Status == InstanceStatus.Background);
        SummaryText = running switch
        {
            0 when background > 0 => "Ningún cliente con ventana · 1 proceso en segundo plano",
            0 => "Ningún cliente abierto",
            1 => "1 cliente abierto",
            _ => $"{running} clientes abiertos",
        };
        OnPropertyChanged(nameof(IsEmpty));
        UpdatePrimaryAction();
    }

    private void UpdatePrimaryAction()
    {
        var windowed = Items.Count(i => i.HasWindow);
        var background = Items.Count(i => i.Status == InstanceStatus.Background);
        var active = Items.Count(i => i.IsActive);
        _closeFirst = InstallationFound && windowed > 0;
        _blockedByBackground = InstallationFound && background > 0 && windowed == 0;
        _blockedByStarting = InstallationFound && active > 0 && windowed == 0 && background == 0
                             && Items.Any(i => i.CanForceKill);

        StatusHeadline = !InstallationFound ? "Roblox no encontrado"
            : windowed > 0 ? (windowed == 1 ? "Roblox está abierto" : $"{windowed} clientes abiertos")
            : _blockedByBackground ? "Roblox está en segundo plano"
            : _blockedByStarting ? "Roblox se está iniciando"
            : "Listo para jugar";

        if (!InstallationFound)
        {
            PrimaryActionText = "Lanzar";
            LaunchHint = "No se encontró Roblox. Instálalo para poder lanzarlo desde aquí.";
        }
        else if (_closeFirst)
        {
            PrimaryActionText = "Cerrar y lanzar…";
            LaunchHint = "Roblox ya está abierto: se cerrará antes de abrirlo de nuevo.";
        }
        else if (_blockedByStarting)
        {
            PrimaryActionText = "Lanzar";
            LaunchHint = "Un cliente lanzado por LarpStrap sigue iniciándose sin ventana. Espera a que aparezca o usa «Forzar cierre…» en su fila.";
        }
        else if (_blockedByBackground)
        {
            PrimaryActionText = "Lanzar";
            LaunchHint = "Hay un proceso de Roblox en segundo plano. LarpStrap no lo cierra: ciérralo desde la bandeja de Windows.";
        }
        else
        {
            PrimaryActionText = "Lanzar";
            LaunchHint = "Abre Roblox en su página de inicio. Para entrar a un juego, usa tus accesos directos o Jugar en roblox.com.";
        }
        LaunchCommand.NotifyCanExecuteChanged();
    }

    private bool CanLaunch() => !IsLaunching && InstallationFound && !_blockedByBackground && !_blockedByStarting;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private Task LaunchAsync() => RunLaunchAsync(link: null);

    /// <summary>
    /// A launch link handed over by the browser. Same checks and confirmation as «Lanzar»; a link that cannot be
    /// opened now is reported, never queued (its ticket expires).
    /// </summary>
    public async Task LaunchLinkAsync(string link)
    {
        if (!CanLaunch())
        {
            AddActivity("No se abrió el juego del navegador.",
                IsLaunching ? "Ya había un lanzamiento en curso. Cuando termine, pulsa Jugar otra vez en la web." : LaunchHint,
                StatusKind.Warn, "");
            _announce(Simulated(LastActivity!.Summary, _service.IsSimulated));
            return;
        }
        await RunLaunchAsync(link);
    }

    private async Task RunLaunchAsync(string? link)
    {
        var closeFirst = _closeFirst;
        if (closeFirst && !_dialogs.Confirm(
                "¿Cerrar el cliente abierto y lanzar uno nuevo?",
                "Se cerrará el cliente que está abierto. Si estás en una partida, saldrás de ella.",
                "Cerrar y lanzar", "Mantener el actual", isDanger: false))
            return;

        _launchCts = new CancellationTokenSource();
        IsLaunching = true;
        CancelLaunchCommand.NotifyCanExecuteChanged();
        LaunchCommand.NotifyCanExecuteChanged();
        var what = link is null ? "lanzando Roblox" : "abriendo el juego del navegador";
        _announce(Simulated(closeFirst ? $"Cerrando el cliente abierto y {what}…" : $"{char.ToUpperInvariant(what[0])}{what[1..]}…", _service.IsSimulated));
        try
        {
            var service = _service;
            var token = _launchCts.Token;
            var simulated = service.IsSimulated; // the mode may change while launching (defect C)
            var result = await Task.Run(() => closeFirst ? service.CloseAndLaunchAsync(CloseTimeout, token, link)
                : link is null ? service.LaunchAsync(token)
                : service.LaunchLinkAsync(link, token));

            var (summary, guidance, kind) = Presentation.Describe(result);
            AddActivity(summary, guidance, kind, result.Diagnostic, simulated);
            _log.Write(result.Outcome == LaunchOutcome.Started ? LogLevel.Info : LogLevel.Warning, "Launch.Result",
                ("result", result.Outcome), ("pid", result.Pid), ("durationMs", result.Duration),
                ("launchMode", link is null ? "app" : "link"), ("placeId", link is null ? null : RobloxUri.PlaceId(link)));
            _announce(Simulated(LastActivity?.Summary ?? "", simulated));
        }
        finally
        {
            IsLaunching = false;
            _launchCts.Dispose();
            _launchCts = null;
            CancelLaunchCommand.NotifyCanExecuteChanged();
            await RefreshAsync();
        }
    }

    // The status bar is a live region: a screen reader must never hear a simulated launch as a real one.
    private static string Simulated(string message, bool simulated) => simulated ? $"{message} (simulado)" : message;

    private bool CanCancelLaunch() => IsLaunching;

    [RelayCommand(CanExecute = nameof(CanCancelLaunch))]
    private void CancelLaunch() => _launchCts?.Cancel();

    internal void Focus(InstanceItemViewModel row)
    {
        var ok = _service.Focus(row.Pid);
        _announce(ok ? $"Cliente {row.Pid} en primer plano." : $"Windows no permitió traer el cliente {row.Pid} al frente. Haz clic en su ventana en la barra de tareas.");
    }

    internal async Task CloseAsync(InstanceItemViewModel row)
    {
        var ok = _service.Close(row.Pid);
        AddActivity(ok ? $"Cierre solicitado al cliente {row.Pid}." : $"El cliente {row.Pid} no aceptó el cierre.",
            ok ? "" : "Puede estar ocupado o mostrando un diálogo. Espera o usa «Forzar cierre».",
            ok ? StatusKind.Neutral : StatusKind.Warn, "");
        await RefreshAsync();
    }

    internal async Task ForceKillAsync(InstanceItemViewModel row)
    {
        var message = row.HasWindow
            ? $"El cliente {row.Pid} se cerrará sin guardar y saldrás de la partida en curso. Úsalo solo si «Cerrar» no funciona."
            : $"El cliente {row.Pid} no llegó a mostrar su ventana. Se cerrará a la fuerza.";
        if (!_dialogs.Confirm("¿Forzar el cierre de este cliente?", message, "Forzar cierre", "Cancelar", isDanger: true))
            return;

        var ok = _service.ForceKill(row.Pid);
        AddActivity(ok ? $"Cliente {row.Pid} cerrado a la fuerza." : $"No se pudo cerrar el cliente {row.Pid}.",
            ok ? "" : "Puede que ya se hubiera cerrado o que Windows lo impida.",
            ok ? StatusKind.Neutral : StatusKind.Error, "");
        await RefreshAsync();
    }

    private void AddActivity(string summary, string guidance, StatusKind kind, string diagnostic, bool? simulated = null)
    {
        Activity.Insert(0, new ActivityItem(DateTime.Now, summary, guidance, kind, diagnostic, simulated ?? IsSimulated));
        while (Activity.Count > 50) Activity.RemoveAt(Activity.Count - 1);
        OnPropertyChanged(nameof(HasActivity));
        OnPropertyChanged(nameof(LastActivity));
    }

    partial void OnIsLaunchingChanged(bool value) => UpdatePrimaryAction();
}

public sealed record ActivityItem(DateTime Time, string Summary, string Guidance, StatusKind Kind, string Diagnostic, bool IsSimulated)
{
    public string TimeText => Time.ToString("HH:mm:ss", Presentation.Spanish);
    public string KindName => Kind.ToString();
    public bool HasGuidance => Guidance.Length > 0;
    public bool HasDiagnostic => Diagnostic.Length > 0;
}

public sealed partial class InstanceItemViewModel(int pid, InstancesViewModel owner) : ObservableObject
{
    public int Pid { get; } = pid;

    [ObservableProperty] public partial InstanceStatus Status { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial StatusKind StatusKind { get; set; }
    [ObservableProperty] public partial string OriginText { get; set; } = "";
    [ObservableProperty] public partial string MemoryText { get; set; } = "—";
    [ObservableProperty] public partial string SinceText { get; set; } = "—";
    [ObservableProperty] public partial bool IsSimulated { get; set; }

    public InstanceOrigin Origin { get; private set; }
    public bool HasWindow => Status is InstanceStatus.Running or InstanceStatus.NotResponding;
    public bool IsActive => !Status.HasEnded();
    public bool IsReadOnly => Status == InstanceStatus.Background;
    public bool CanAct => HasWindow;

    /// <summary>
    /// Windowed clients, plus one LarpStrap launched that never showed a window (O-01: otherwise it blocks the single
    /// slot with no way out). An external window-less process may be Roblox's tray process and is never offered.
    /// </summary>
    public bool CanForceKill => HasWindow || (Origin == InstanceOrigin.Launched && Status == InstanceStatus.Starting);
    public string ReadOnlyText => "Solo lectura: proceso de Roblox en segundo plano. LarpStrap no actúa sobre él.";
    public string PidText => $"PID {Pid}";
    public string AccessibleSummary => $"Cliente {Pid}, {StatusText}, {OriginText}";
    public string FocusName => $"Enfocar cliente {Pid}";
    public string CloseName => $"Cerrar cliente {Pid}";
    public string KillName => $"Forzar cierre del cliente {Pid}";

    public void Update(TrackedInstance instance, DateTime nowUtc, bool simulated)
    {
        Origin = instance.Origin;
        Status = instance.Status;
        (StatusText, StatusKind) = Presentation.Describe(instance.Status);
        OriginText = instance.Origin == InstanceOrigin.Launched ? "Lanzado por LarpStrap" : "Externo";
        MemoryText = Presentation.Bytes(instance.WorkingSetBytes);
        SinceText = Presentation.Since(instance.StartTimeUtc, nowUtc);
        IsSimulated = simulated;
        OnPropertyChanged(nameof(HasWindow));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanAct));
        OnPropertyChanged(nameof(CanForceKill));
        OnPropertyChanged(nameof(AccessibleSummary));
    }

    [RelayCommand]
    private void Focus() => owner.Focus(this);

    [RelayCommand]
    private Task CloseAsync() => owner.CloseAsync(this);

    [RelayCommand]
    private Task ForceKillAsync() => owner.ForceKillAsync(this);
}
