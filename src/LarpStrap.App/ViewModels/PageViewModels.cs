using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LarpStrap.App.Services;
using LarpStrap.Core.Logging;
using LarpStrap.Core.Settings;

namespace LarpStrap.App.ViewModels;

public sealed partial class HomeViewModel(InstancesViewModel instances, ShortcutsViewModel shortcuts, ShellViewModel shell) : ObservableObject
{
    public InstancesViewModel Instances { get; } = instances;

    /// <summary>Saved games, shown as quick tiles on the home screen.</summary>
    public ShortcutsViewModel Shortcuts { get; } = shortcuts;

    [RelayCommand]
    private void OpenPanel() => shell.NavigateTo<InstancesViewModel>();

    [RelayCommand]
    private void OpenShortcuts() => shell.NavigateTo<ShortcutsViewModel>();
}

/// <summary>Opens Roblox's separate official Store app and web sessions without collecting credentials.</summary>
public sealed partial class AccountsViewModel(IDialogService dialogs, RobloxStoreLauncher storeLauncher) : ObservableObject
{
    [ObservableProperty] public partial bool IsOpeningStore { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Cada aplicación mantiene su propia sesión de Roblox.";

    [RelayCommand]
    private async Task OpenRobloxWindowsAsync()
    {
        if (IsOpeningStore) return;
        IsOpeningStore = true;
        try
        {
            var installed = await storeLauncher.OpenAsync();
            StatusText = installed
                ? "Roblox - Windows se ha abierto. Inicia sesión con la primera cuenta."
                : "Roblox - Windows no está instalado. Se ha abierto su ficha oficial de Microsoft Store.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            StatusText = "Windows no pudo abrir Roblox - Windows. Comprueba la instalación y vuelve a intentarlo.";
        }
        finally
        {
            IsOpeningStore = false;
        }
    }

    [RelayCommand]
    private void OpenRobloxWeb()
    {
        dialogs.OpenUri(new Uri("https://www.roblox.com/games/"));
        StatusText = "Se ha abierto Roblox en el navegador. Inicia sesión con la segunda cuenta y pulsa Jugar.";
    }
}

public sealed partial class ShortcutsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Action<string> _announce;
    private readonly ExperienceLookupService _experienceLookup;
    private readonly RobloxStoreLauncher _storeLauncher;
    private CancellationTokenSource? _lookupCancellation;

    /// <summary>Pause after the last change of the game field before asking Roblox for its title and icon.</summary>
    private static readonly TimeSpan LookupDelay = TimeSpan.FromMilliseconds(350);
    private Task _lookupTask = Task.CompletedTask;

    public ShortcutsViewModel(SettingsService settings, IDialogService dialogs, Action<string> announce,
        ExperienceLookupService experienceLookup, RobloxStoreLauncher storeLauncher)
    {
        _settings = settings;
        _dialogs = dialogs;
        _announce = announce;
        _experienceLookup = experienceLookup;
        _storeLauncher = storeLauncher;
        Reload();
    }

    public ObservableCollection<FavoriteItem> Favorites { get; } = [];

    [ObservableProperty] public partial string NewGame { get; set; } = "";
    [ObservableProperty] public partial string NewName { get; set; } = "";
    [ObservableProperty] public partial string ValidationMessage { get; set; } = "";
    [ObservableProperty] public partial string LookupStatus { get; set; } = "Pega un enlace o escribe el place ID para ver la experiencia.";
    [ObservableProperty] public partial string? PreviewImageUrl { get; set; }
    [ObservableProperty] public partial string PreviewPlaceText { get; set; } = "";
    [ObservableProperty] public partial bool HasPreview { get; set; }
    [ObservableProperty] public partial bool IsLookingUp { get; set; }

    partial void OnNewGameChanged(string value) => _lookupTask = ResolveExperienceAsync(value);

    public bool IsEmpty => Favorites.Count == 0;

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!TryCurrentPlaceId(out var placeId))
        {
            ValidationMessage = "Escribe el número del juego (place ID) o pega su enlace de roblox.com/games/…";
            return;
        }
        if (IsLookingUp) await _lookupTask;
        if (!TryCurrentPlaceId(out var resolvedId) || resolvedId != placeId) return;
        var name = NewName.Trim();
        if (name.Length == 0) name = $"Experiencia {placeId}";
        if (_settings.Current.FavoriteGames.Any(f => f.PlaceId == placeId))
        {
            ValidationMessage = "Ese juego ya está en tus accesos directos.";
            return;
        }

        var error = _settings.Update(s => s with { Favorites = [.. s.FavoriteGames, new FavoriteGame(placeId, name, PreviewImageUrl)] });
        ValidationMessage = error ?? "";
        NewGame = "";
        NewName = "";
        Reload();
        _announce($"«{name}» añadido a los accesos directos.");
    }

    [RelayCommand]
    private Task JoinWebAsync() => JoinCurrentExperienceAsync("Web");

    [RelayCommand]
    private Task JoinMicrosoftAsync() => JoinCurrentExperienceAsync("Microsoft");

    private async Task JoinCurrentExperienceAsync(string method)
    {
        if (!TryCurrentPlaceId(out var placeId))
        {
            ValidationMessage = "Pega un enlace de roblox.com/games/… o escribe un place ID válido.";
            return;
        }
        ValidationMessage = "";
        await JoinPlaceAsync(placeId, method);
    }

    private async Task JoinPlaceAsync(long placeId, string method)
    {
        try
        {
            if (method == "Microsoft")
            {
                var opened = await _storeLauncher.OpenExperienceAsync(placeId);
                LookupStatus = opened
                    ? "Se ha pedido a Windows que abra la experiencia con el programa de los enlaces roblox: (normalmente Roblox - Windows). Juega con la cuenta abierta en ese programa."
                    : "Roblox - Windows no está instalado. Se abrió su ficha en Microsoft Store.";
            }
            else
            {
                _dialogs.OpenUri(GameLinks.GameStart(placeId));
                LookupStatus = "Se ha pedido al navegador que inicie la experiencia. Si Roblox no se abre, pulsa Jugar en la página.";
            }
            _announce(LookupStatus);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LookupStatus = "Windows no pudo abrir Roblox. Comprueba la instalación o el navegador.";
        }
    }

    internal void Open(FavoriteItem item)
    {
        _ = JoinPlaceAsync(item.PlaceId, "Web");
    }

    internal void Join(FavoriteItem item, string method) => _ = JoinPlaceAsync(item.PlaceId, method);

    internal void Remove(FavoriteItem item)
    {
        var error = _settings.Update(s => s with { Favorites = [.. s.FavoriteGames.Where(f => f.PlaceId != item.PlaceId)] });
        Reload();
        _announce(error ?? $"«{item.Name}» quitado de los accesos directos.");
    }

    private void Reload()
    {
        Favorites.Clear();
        foreach (var favorite in _settings.Current.FavoriteGames)
        {
            var item = new FavoriteItem(favorite.PlaceId, favorite.Name, favorite.ThumbnailUrl, this);
            Favorites.Add(item);
            if (string.IsNullOrWhiteSpace(favorite.ThumbnailUrl)) _ = RefreshFavoriteAsync(item);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task RefreshFavoriteAsync(FavoriteItem item)
    {
        var info = await _experienceLookup.LookupAsync(item.PlaceId, CancellationToken.None);
        var name = item.Name == $"Experiencia {item.PlaceId}" && !string.IsNullOrWhiteSpace(info?.Name) ? info.Name : item.Name;
        var image = info?.ImageUrl;
        if (image is null && name == item.Name) return;
        item.Update(name, image);
        _settings.Update(s => s with
        {
            Favorites = [.. s.FavoriteGames.Select(f => f.PlaceId == item.PlaceId ? f with { Name = name, ThumbnailUrl = image ?? f.ThumbnailUrl } : f)],
        });
    }

    private bool TryCurrentPlaceId(out long placeId) =>
        GameLinks.TryParsePlaceId(NewGame, out placeId);

    // The last name LarpStrap filled in by itself; a name the user typed is never replaced.
    private string _autoName = "";

    private bool NameIsAutomatic => NewName.Length == 0 || NewName == _autoName;

    private void SetAutomaticName(string name)
    {
        if (!NameIsAutomatic) return;
        _autoName = name;
        NewName = name;
    }

    private async Task ResolveExperienceAsync(string input)
    {
        _lookupCancellation?.Cancel();
        _lookupCancellation?.Dispose();
        _lookupCancellation = null;
        PreviewImageUrl = null;
        HasPreview = GameLinks.TryParsePlaceId(input, out var placeId);
        if (!HasPreview)
        {
            PreviewPlaceText = "";
            LookupStatus = string.IsNullOrWhiteSpace(input) ? "Pega un enlace o escribe el place ID para ver la experiencia." : "No reconozco ese enlace. Usa roblox.com/games/… o un place ID.";
            IsLookingUp = false;
            return;
        }

        PreviewPlaceText = $"Place ID {placeId}";
        SetAutomaticName($"Experiencia {placeId}");
        LookupStatus = "Buscando título e imagen públicos en Roblox…";
        IsLookingUp = true;
        var cancellation = new CancellationTokenSource();
        _lookupCancellation = cancellation;
        try
        {
            // Wait for typing to pause: one request per game, not one per keystroke.
            await Task.Delay(LookupDelay, cancellation.Token);
            var info = await _experienceLookup.LookupAsync(placeId, cancellation.Token);
            if (cancellation.IsCancellationRequested || !GameLinks.TryParsePlaceId(NewGame, out var current) || current != placeId) return;
            if (!string.IsNullOrWhiteSpace(info?.Name)) SetAutomaticName(info.Name);
            PreviewImageUrl = info?.ImageUrl;
            LookupStatus = info is null ? "ID detectado. Roblox no devolvió título o imagen; puedes unirte de todos modos." : "Experiencia detectada. Puedes unirte o guardarla.";
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_lookupCancellation, cancellation)) IsLookingUp = false;
        }
    }
}

public sealed partial class FavoriteItem(long placeId, string name, string? thumbnailUrl, ShortcutsViewModel owner) : ObservableObject
{
    public long PlaceId { get; } = placeId;
    private string _name = name;
    public string Name
    {
        get => _name;
        private set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(OpenName));
                OnPropertyChanged(nameof(RemoveName));
            }
        }
    }

    [ObservableProperty] public partial string? ThumbnailUrl { get; set; } = thumbnailUrl;
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);
    public string PlaceText => $"Place ID {PlaceId}";
    public string OpenName => $"Unirme a {Name}";
    public string RemoveName => $"Quitar {Name}";

    public void Update(string name, string? image)
    {
        Name = name;
        ThumbnailUrl = image;
        OnPropertyChanged(nameof(HasThumbnail));
    }

    partial void OnThumbnailUrlChanged(string? value) => OnPropertyChanged(nameof(HasThumbnail));

    [RelayCommand]
    private void JoinWeb() => owner.Join(this, "Web");

    [RelayCommand]
    private void JoinMicrosoft() => owner.Join(this, "Microsoft");

    [RelayCommand]
    private void Open() => owner.Open(this);

    [RelayCommand]
    private void Remove() => owner.Remove(this);
}

public sealed partial class CustomizationViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly Action<string> _announce;

    public CustomizationViewModel(SettingsService settings, Action<string> announce)
    {
        _settings = settings;
        _announce = announce;
    }

    public ThemeMode Theme
    {
        get => _settings.Current.Theme;
        set => Change(s => s with { Theme = value }, "Tema cambiado.");
    }

    public UiThemePreset ThemePreset
    {
        get => _settings.Current.ThemePreset;
        set => Change(s => s with { ThemePreset = value }, $"Tema {value} aplicado.");
    }

    public AccentColor Accent
    {
        get => _settings.Current.Accent;
        set => Change(s => s with { Accent = value }, "Color de acento cambiado.");
    }

    private string? _customAccentText;
    public string CustomAccentHex
    {
        get => _customAccentText ?? _settings.Current.CustomAccentHex ?? "#9475F5";
        set
        {
            _customAccentText = value.Trim();
            if (AppSettings.IsHexColor(_customAccentText))
            {
                var error = _settings.Update(s => s with { Accent = AccentColor.Custom, CustomAccentHex = _customAccentText });
                _announce(error ?? "Color personalizado aplicado.");
                _customAccentText = null;
            }
            OnPropertyChanged(nameof(CustomAccentHex));
            OnPropertyChanged(nameof(CustomAccentHexError));
        }
    }

    public string CustomAccentHexError => _customAccentText is { Length: > 0 } text && !AppSettings.IsHexColor(text)
        ? "Usa el formato #RRGGBB, por ejemplo #7C5CE7." : "";

    public LogoStyle Logo
    {
        get => _settings.Current.Logo;
        set => Change(s => s with { Logo = value }, "Logotipo cambiado.");
    }

    public double TextScale
    {
        get => _settings.Current.TextScale;
        set => Change(s => s with { TextScale = value }, $"Tamaño del texto: {value:P0}.");
    }

    public WindowBackdrop Backdrop
    {
        get => _settings.Current.Backdrop;
        set => Change(s => s with { Backdrop = value }, "Material de la ventana cambiado.");
    }

    /// <summary>Mica and Acrylic need Windows 11 22H2 or later; elsewhere only the solid window is offered.</summary>
    public bool MaterialSupported => ThemeService.MaterialSupported;

    private void Change(Func<AppSettings, AppSettings> change, string done)
    {
        var error = _settings.Update(change);
        OnPropertyChanged(string.Empty);
        _announce(error ?? done);
    }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Action<string> _announce;

    public SettingsViewModel(SettingsService settings, IDialogService dialogs, Action<string> announce)
    {
        _settings = settings;
        _dialogs = dialogs;
        _announce = announce;
    }

    public string SettingsPath => _settings.FilePath;
    public int LaunchTimeoutSeconds
    {
        get => _settings.Current.LaunchTimeoutSeconds;
        set => Change(s => s with { LaunchTimeoutSeconds = value }, $"Tiempo máximo de arranque: {value} s.");
    }

    public bool DemoMode
    {
        get => _settings.Current.DemoMode;
        set => Change(s => s with { DemoMode = value },
            value ? "Modo demostración activado: Clientes abiertos muestra clientes SIMULADOS." : "Modo demostración desactivado: Clientes abiertos muestra clientes reales.");
    }

    public bool DiscordPresence
    {
        get => _settings.Current.DiscordPresence;
        set => Change(s => s with { DiscordPresence = value },
            value ? "Discord Rich Presence activado." : "Discord Rich Presence desactivado.");
    }

    /// <summary>Saved only when it is a valid application ID; anything else shows <see cref="DiscordAppIdError"/>.</summary>
    public string DiscordAppId
    {
        get => _discordAppIdText ?? _settings.Current.DiscordAppId ?? "";
        set
        {
            _discordAppIdText = value.Trim();
            if (_discordAppIdText.Length == 0 || AppSettings.IsDiscordAppId(_discordAppIdText))
            {
                Change(s => s with { DiscordAppId = _discordAppIdText.Length == 0 ? null : _discordAppIdText },
                    _discordAppIdText.Length == 0 ? "ID de aplicación de Discord borrado." : "ID de aplicación de Discord guardado.");
                _discordAppIdText = null;
            }
            OnPropertyChanged(nameof(DiscordAppIdError));
            OnPropertyChanged(nameof(DiscordStatus));
        }
    }

    private string? _discordAppIdText;

    public string DiscordAppIdError => _discordAppIdText is { Length: > 0 }
        ? "Ese no es un ID de aplicación: son 17 a 20 cifras, en la página General Information de tu aplicación." : "";

    public string DiscordStatus => !_settings.Current.DiscordPresence ? "Desactivado."
        : _settings.Current.DiscordAppId is null ? "Falta el ID de aplicación: sin él, Discord no muestra nada."
        : _settings.Current.DemoMode ? "En el modo demostración no se envía nada a Discord."
        : "Activo: Discord muestra «En Roblox» mientras tengas un cliente abierto.";

    [RelayCommand]
    private void OpenDiscordDevelopers() => _dialogs.OpenUri(new Uri("https://discord.com/developers/applications"));

    [RelayCommand]
    private void OpenSettingsFolder() => _dialogs.OpenFolder(Path.GetDirectoryName(_settings.FilePath)!);

    private void Change(Func<AppSettings, AppSettings> change, string done)
    {
        var error = _settings.Update(change);
        OnPropertyChanged(string.Empty);
        _announce(error ?? done);
    }
}

public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly string _logDirectory;
    private readonly IDialogService _dialogs;
    private readonly Action<string> _announce;

    public DiagnosticsViewModel(string logDirectory, string? robloxPath, IDialogService dialogs, Action<string> announce)
    {
        _logDirectory = logDirectory;
        _dialogs = dialogs;
        _announce = announce;
        SystemInfo =
        [
            new("LarpStrap", AboutViewModel.AppVersion),
            new(".NET", RuntimeInformation.FrameworkDescription),
            new("Windows", Environment.OSVersion.VersionString),
            new("Roblox", robloxPath ?? "No encontrado"),
            new("Logs", logDirectory),
        ];
        Reload();
    }

    public IReadOnlyList<InfoRow> SystemInfo { get; }
    public ObservableCollection<FileInfo> Files { get; } = [];

    [ObservableProperty] public partial FileInfo? SelectedFile { get; set; }
    [ObservableProperty] public partial string Content { get; set; } = "";

    public bool IsEmpty => Files.Count == 0;

    partial void OnSelectedFileChanged(FileInfo? value)
    {
        try
        {
            Content = value is null ? "" : LogFiles.ReadTail(value.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Content = "No se pudo leer este log. Puede que se haya borrado; pulsa Actualizar.";
        }
    }

    [RelayCommand]
    private void Reload()
    {
        Files.Clear();
        foreach (var file in LogFiles.List(_logDirectory)) Files.Add(file);
        SelectedFile = Files.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void OpenFolder() => _dialogs.OpenFolder(_logDirectory);

    [RelayCommand]
    private void Export()
    {
        var target = _dialogs.AskSavePath($"LarpStrap-logs-{DateTime.Now:yyyyMMdd-HHmm}.zip", "Archivo ZIP (*.zip)|*.zip");
        if (target is null) return;
        try
        {
            var count = LogFiles.ExportZip(_logDirectory, target);
            _announce($"Exportados {count} archivo(s) de log a {Path.GetFileName(target)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _announce("No se pudo exportar: comprueba que tienes permiso para escribir en esa carpeta.");
        }
    }
}

public sealed record InfoRow(string Label, string Value);

public sealed partial class AboutViewModel(SettingsService settings, AppUpdater updater) : ObservableObject
{
    public static string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public string Version => AppVersion;

    public bool IsInstalled => AppUpdater.IsInstalled;

    public bool AutoUpdate
    {
        get => settings.Current.AutoUpdate;
        set
        {
            UpdateStatus = settings.Update(s => s with { AutoUpdate = value })
                ?? (value ? "LarpStrap se actualizará solo al iniciarse." : "Actualización automática desactivada: usa «Buscar ahora».");
            OnPropertyChanged();
        }
    }

    [ObservableProperty] public partial string UpdateStatus { get; set; } = "";

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        UpdateStatus = "Buscando actualizaciones…";
        UpdateStatus = await updater.CheckAsync(manual: true);
    }
}
