using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LarpStrap.Core.Settings;
using Microsoft.Win32;
using ThemeMode = LarpStrap.Core.Settings.ThemeMode;

namespace LarpStrap.App.Services;

/// <summary>
/// Swaps the colour dictionary (light/dark), sets the accent brushes for that theme and keeps every window frame in
/// sync: dark title bar, and on the main window the Mica/Acrylic material (Windows 11 22H2+) or a solid background.
/// </summary>
public sealed class ThemeService
{
    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE exists from Windows 11 22H2 (build 22621); older systems get a solid window.</summary>
    public static bool MaterialSupported { get; } = Environment.OSVersion.Version.Build >= 22621;

    private ThemeMode _mode;
    private AccentColor _accent;
    private WindowBackdrop _backdrop;
    private LogoStyle _logo;
    private string? _customAccentHex;
    private UiThemePreset _preset;

    public bool IsDark { get; private set; }

    public void Apply(ThemeMode mode, AccentColor accent, WindowBackdrop backdrop, LogoStyle logo = LogoStyle.Classic,
        string? customAccentHex = null, UiThemePreset preset = UiThemePreset.Nebula)
    {
        _mode = mode;
        _accent = accent;
        _backdrop = backdrop;
        _logo = Enum.IsDefined(logo) ? logo : LogoStyle.Classic;
        _customAccentHex = AppSettings.IsHexColor(customAccentHex) ? customAccentHex : null;
        _preset = Enum.IsDefined(preset) ? preset : UiThemePreset.Nebula;
        IsDark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemPrefersDark());

        var resources = Application.Current.Resources;
        resources.MergedDictionaries[0] = new ResourceDictionary
        {
            // Assembly-qualified: this file is compiled into both the app and the installer, each with its own copy of Themes/.
            Source = new Uri($"pack://application:,,,/{typeof(ThemeService).Assembly.GetName().Name};component/Themes/Colors.{(IsDark ? "Dark" : "Light")}.xaml"),
        };
        ApplyThemePalette(resources, _preset, IsDark);

        // Themes/Accents.xaml holds one seed per accent; a custom accent is the user's validated #RRGGBB.
        var seed = accent == AccentColor.Custom && _customAccentHex is not null
            ? (Color)ColorConverter.ConvertFromString(_customAccentHex)!
            : resources[$"AccentSeed{(accent == AccentColor.Custom ? AccentColor.Violet : accent)}"] is Color c ? c : (Color)resources["AccentSeedViolet"];
        var surface = ((SolidColorBrush)resources["SurfaceBrush"]).Color;
        var surfaceAlt = ((SolidColorBrush)resources["SurfaceAltBrush"]).Color;
        var (fill, onFill, text) = AccentPalette.For(seed, surface, surfaceAlt, IsDark);
        resources["AccentBrush"] = Frozen(fill);
        resources["OnAccentBrush"] = Frozen(onFill);
        resources["AccentTextBrush"] = Frozen(text);
        resources["AccentSoftBrush"] = Frozen(Color.FromArgb(0x2E, fill.R, fill.G, fill.B)); // selection tint, decorative
        resources["ActiveLogoMark"] = resources[_logo switch
        {
            LogoStyle.Orbit => "LogoMarkOrbit",
            LogoStyle.Monogram => "LogoMarkMonogram",
            _ => "LogoMark",
        }];

        foreach (Window window in Application.Current.Windows)
            ApplyWindowFrame(window);
    }

    /// <summary>Follows Windows' light/dark switch while the theme is "System".</summary>
    public void WatchSystemTheme() =>
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _mode == ThemeMode.System)
                Application.Current.Dispatcher.Invoke(() => Apply(_mode, _accent, _backdrop, _logo, _customAccentHex, _preset));
        };

    /// <summary>Title bar colour for every window; window material only for the main window (dialogs stay solid).</summary>
    public void ApplyWindowFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var useDark = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref useDark, sizeof(int));

        if (window != Application.Current.MainWindow) return;
        var material = !MaterialSupported || _backdrop == WindowBackdrop.Solid ? BackdropNone
            : _backdrop == WindowBackdrop.Acrylic ? BackdropAcrylic : BackdropMica;
        if (MaterialSupported)
            _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref material, sizeof(int));

        // The material is drawn behind the client area, so WPF must leave it transparent; a solid window paints its own background.
        var solid = material == BackdropNone;
        var background = (SolidColorBrush)Application.Current.Resources["BgBrush"];
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
            target.BackgroundColor = solid ? background.Color : Colors.Transparent;
        window.Background = solid ? background : Brushes.Transparent;
    }

    private static bool SystemPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>The interface theme replaces only the surfaces (Themes/Presets.xaml); text, borders and states stay in Colors.*.xaml.</summary>
    private static void ApplyThemePalette(ResourceDictionary resources, UiThemePreset preset, bool dark)
    {
        var prefix = $"Preset{(Enum.IsDefined(preset) ? preset : UiThemePreset.Nebula)}{(dark ? "Dark" : "Light")}";
        foreach (var role in new[] { "Bg", "Surface", "SurfaceAlt", "SurfaceHover", "BorderSubtle" })
            resources[$"{role}Brush"] = resources[$"{prefix}{role}Brush"];
        var bg = ((SolidColorBrush)resources[$"{prefix}BgBrush"]).Color;
        resources["LayerBrush"] = Frozen(Color.FromArgb(0xD9, bg.R, bg.G, bg.B)); // 85 % over Mica/Acrylic
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmSystemBackdropType = 38;
    private const int BackdropNone = 1;
    private const int BackdropMica = 2;
    private const int BackdropAcrylic = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
