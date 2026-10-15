using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LarpStrap.App.Services;
using LarpStrap.Core.Settings;

namespace LarpStrap.App.ViewModels;

/// <summary>
/// FastFlags editor limited to Roblox's allowlist (<see cref="FastFlags"/>). Values are saved at once and written into
/// the Roblox installation on the next real launch from LarpStrap; demo mode never writes them.
/// </summary>
public sealed partial class FastFlagsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly Action<string> _announce;

    public FastFlagsViewModel(SettingsService settings, IDialogService dialogs, Action<string> announce)
    {
        _settings = settings;
        _dialogs = dialogs;
        _announce = announce;
        Flags = [.. FastFlags.Allowlist.Select(f => new FastFlagItem(f, this))];
        RefreshVisibleFlags();
    }

    public IReadOnlyList<FastFlagItem> Flags { get; }
    public ObservableCollection<FastFlagItem> VisibleFlags { get; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool ShowConfiguredOnly { get; set; }

    public bool HasNoVisibleFlags => VisibleFlags.Count == 0;
    public string ResultsText => ShowConfiguredOnly
        ? $"{VisibleFlags.Count} de {Flags.Count} flags configurados"
        : $"{VisibleFlags.Count} de {Flags.Count} flags";

    partial void OnSearchTextChanged(string value) => RefreshVisibleFlags();
    partial void OnShowConfiguredOnlyChanged(bool value) => RefreshVisibleFlags();

    internal void RefreshVisibleFlags()
    {
        if (Flags is null) return; // Observable properties can be initialized before the constructor body.
        var query = SearchText.Trim();
        var matches = Flags.Where(flag =>
            (!ShowConfiguredOnly || _settings.Current.FastFlagValues?.ContainsKey(flag.Info.Name) == true)
            && (query.Length == 0
                || flag.Info.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || flag.Info.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || flag.Info.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

        VisibleFlags.Clear();
        foreach (var flag in matches) VisibleFlags.Add(flag);
        OnPropertyChanged(nameof(HasNoVisibleFlags));
        OnPropertyChanged(nameof(ResultsText));
    }

    public string SummaryText => ConfiguredCount switch
    {
        0 => "Ningún flag cambiado: Roblox usa sus valores.",
        1 => "1 flag cambiado.",
        var n => $"{n} flags cambiados.",
    };

    private int ConfiguredCount => _settings.Current.FastFlagValues?.Count ?? 0;

    internal string? Get(string name) => _settings.Current.FastFlagValues?.GetValueOrDefault(name);

    internal void Set(FastFlagInfo flag, string? value)
    {
        var flags = new Dictionary<string, string>(_settings.Current.FastFlagValues ?? new Dictionary<string, string>());
        if (value is null) flags.Remove(flag.Name);
        else flags[flag.Name] = value;
        var error = _settings.Update(s => s with { FastFlagValues = flags });
        OnPropertyChanged(nameof(SummaryText));
        ResetAllCommand.NotifyCanExecuteChanged();
        RefreshVisibleFlags();
        _announce(error ?? $"{flag.Title}: {(value is null ? "valor de Roblox" : value)}. Se aplica la próxima vez que lances Roblox desde LarpStrap.");
    }

    private bool CanResetAll() => ConfiguredCount > 0;

    [RelayCommand(CanExecute = nameof(CanResetAll))]
    private void ResetAll()
    {
        if (!_dialogs.Confirm("¿Volver a los valores de Roblox?", "Se quitan todos los FastFlags que cambiaste. Se aplica la próxima vez que lances Roblox desde LarpStrap.",
                "Restablecer", "Cancelar", isDanger: false))
            return;
        var error = _settings.Update(s => s with { FastFlagValues = null });
        foreach (var flag in Flags) flag.Reload();
        RefreshVisibleFlags();
        OnPropertyChanged(nameof(SummaryText));
        ResetAllCommand.NotifyCanExecuteChanged();
        _announce(error ?? "FastFlags restablecidos.");
    }
}

public sealed partial class FastFlagItem : ObservableObject
{
    /// <summary>Choice value meaning «not set: Roblox's own value».</summary>
    public const string Default = "Default";

    private readonly FastFlagsViewModel _owner;

    public FastFlagItem(FastFlagInfo info, FastFlagsViewModel owner)
    {
        Info = info;
        _owner = owner;
        Reload();
    }

    public FastFlagInfo Info { get; }
    public bool IsBool => Info.Kind == FastFlagKind.Bool;
    public string Hint => IsBool ? "" : $"Número de {Info.Min} a {Info.Max}. Déjalo vacío para usar el valor de Roblox.";

    /// <summary>Bool flags: <see cref="Default"/>, "True" or "False".</summary>
    public string Choice
    {
        get => _owner.Get(Info.Name) ?? Default;
        set
        {
            if (value == Choice) return;
            _owner.Set(Info, value == Default ? null : value);
            OnPropertyChanged();
        }
    }

    /// <summary>Int flags: the typed text, saved when valid (empty clears it).</summary>
    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial string Error { get; set; } = "";

    public bool HasError => Error.Length > 0;

    internal void Reload()
    {
        OnPropertyChanged(nameof(Choice));
        Text = _owner.Get(Info.Name) ?? "";
        Error = "";
    }

    partial void OnTextChanged(string value)
    {
        if (IsBool) return;
        var trimmed = value.Trim();
        var normalized = trimmed.Length == 0 ? null : FastFlags.Normalize(Info.Name, trimmed);
        if (trimmed.Length > 0 && normalized is null)
        {
            Error = $"«{trimmed}» no vale: escribe un número de {Info.Min} a {Info.Max}.";
            return;
        }
        Error = "";
        if (normalized != _owner.Get(Info.Name))
            _owner.Set(Info, normalized);
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
}
