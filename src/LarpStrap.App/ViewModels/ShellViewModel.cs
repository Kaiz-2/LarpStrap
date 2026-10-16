using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LarpStrap.App.ViewModels;

/// <summary>Where a page lives in the navigation pane.</summary>
public enum NavGroup
{
    /// <summary>Always shown: launching, clients and games.</summary>
    Play,

    /// <summary>Only in advanced mode: technical tools.</summary>
    Advanced,

    /// <summary>Always shown, at the bottom: appearance, settings and about.</summary>
    Footer,
}

public sealed partial class ShellViewModel : ObservableObject
{
    public ObservableCollection<NavItem> Pages { get; } = [];
    public ObservableCollection<NavItem> PlayPages { get; } = [];
    public ObservableCollection<NavItem> AdvancedPages { get; } = [];
    public ObservableCollection<NavItem> FooterPages { get; } = [];

    [ObservableProperty] public partial NavItem? Current { get; set; }

    /// <summary>Announced to screen readers through a live region in the main window.</summary>
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    [ObservableProperty] public partial double TextScale { get; set; } = 1.0;

    [ObservableProperty] public partial bool IsSimulated { get; set; }

    /// <summary>Simple mode shows launching, clients and games; advanced adds the technical pages and details.</summary>
    [ObservableProperty] public partial bool IsAdvancedMode { get; set; }

    /// <summary>Navigation pane with icons only.</summary>
    [ObservableProperty] public partial bool IsNavCompact { get; set; }

    /// <summary>The instance panel, so the window can show the launch dialog wherever the launch started.</summary>
    public InstancesViewModel? Launcher { get; set; }

    public NavItem Add(string title, string glyph, object page, string accessKeyTitle, NavGroup group = NavGroup.Play)
    {
        var item = new NavItem(title, glyph, page, accessKeyTitle, group, this);
        Pages.Add(item);
        (group switch { NavGroup.Advanced => AdvancedPages, NavGroup.Footer => FooterPages, _ => PlayPages }).Add(item);
        return item;
    }

    public void Navigate(NavItem item)
    {
        foreach (var page in Pages) page.SetSelected(page == item);
        Current = item;
    }

    public void NavigateTo<T>() where T : class
    {
        var item = Pages.FirstOrDefault(p => p.Page is T);
        if (item is not null) Navigate(item);
    }

    public void Announce(string message) => StatusMessage = message;

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsAdvancedModeChanged(bool value)
    {
        // A hidden page must not stay on screen: back to the first page of the simple mode.
        if (!value && Current?.Group == NavGroup.Advanced && PlayPages.Count > 0)
            Navigate(PlayPages[0]);
        if (Pages.Count == 0) return; // restored from settings at startup: nothing to announce
        Announce(value
            ? "Modo avanzado: se muestran FastFlags, Diagnóstico y los detalles técnicos."
            : "Modo simple: solo jugar, clientes y juegos.");
    }
}

public sealed partial class NavItem(string title, string glyph, object page, string accessKeyTitle, NavGroup group, ShellViewModel shell) : ObservableObject
{
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public object Page { get; } = page;
    public NavGroup Group { get; } = group;

    /// <summary>Title with an access key marker (_), e.g. "_Instancias" → Alt+I.</summary>
    public string AccessKeyTitle { get; } = accessKeyTitle;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value) shell.Navigate(this);
        }
    }

    internal void SetSelected(bool value) => SetProperty(ref _isSelected, value, nameof(IsSelected));
}
