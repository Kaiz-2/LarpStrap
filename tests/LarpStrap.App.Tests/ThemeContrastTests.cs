using System.Windows.Media;
using LarpStrap.App.Services;

namespace LarpStrap.App.Tests;

/// <summary>
/// WCAG 2.x contrast of the design tokens, read straight from Themes/*.xaml (the only source of colour), so a palette
/// change that breaks legibility fails the build instead of being found by eye. Every interface theme (Presets.xaml)
/// replaces the surfaces of its mode, so each rule runs for every theme × mode, and every accent seed for each of them.
/// </summary>
public sealed class ThemeContrastTests
{
    private static readonly string[] Presets = ["Nebula", "Midnight", "Ocean", "Forest", "Sunset", "Rose", "Slate"];

    public static TheoryData<string, string> ThemesAndModes()
    {
        var data = new TheoryData<string, string>();
        foreach (var preset in Presets)
        {
            data.Add(preset, "Dark");
            data.Add(preset, "Light");
        }
        return data;
    }

    /// <summary>The colours the app uses for one theme and mode: Colors.{mode}.xaml with the theme's surfaces on top.</summary>
    private static Dictionary<string, string> Palette(string preset, string mode)
    {
        var c = Colors($"Colors.{mode}.xaml");
        var p = Colors("Presets.xaml");
        foreach (var role in new[] { "Bg", "Surface", "SurfaceAlt", "SurfaceHover", "BorderSubtle" })
            c[$"{role}Brush"] = p[$"Preset{preset}{mode}{role}Brush"];
        return c;
    }

    [Fact]
    public void Every_interface_theme_defines_all_its_surfaces_in_both_modes() =>
        Assert.Equal(Presets.Length * 2 * 5, Colors("Presets.xaml").Keys.Count(k => k.StartsWith("Preset", StringComparison.Ordinal)));

    [Theory]
    [MemberData(nameof(ThemesAndModes))]
    public void Body_text_reaches_aaa_on_backgrounds_and_aa_everywhere(string preset, string mode)
    {
        var c = Palette(preset, mode);

        AssertContrast(c["TextBrush"], c["BgBrush"], 7.0);
        AssertContrast(c["TextBrush"], c["SurfaceBrush"], 7.0);
        AssertContrast(c["TextBrush"], c["SurfaceAltBrush"], 4.5);
        foreach (var surface in new[] { "BgBrush", "SurfaceBrush", "SurfaceAltBrush" })
            AssertContrast(c["TextMutedBrush"], c[surface], 4.5);
    }

    [Theory]
    [MemberData(nameof(ThemesAndModes))]
    public void Control_borders_and_the_focus_ring_are_visible(string preset, string mode)
    {
        var c = Palette(preset, mode);

        foreach (var surface in new[] { "BgBrush", "SurfaceBrush", "SurfaceAltBrush" })
        {
            AssertContrast(c["BorderStrongBrush"], c[surface], 3.0);
            AssertContrast(c["FocusRingBrush"], c[surface], 3.0);
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndModes))]
    public void Status_colors_work_as_text_on_cards_and_menus_and_soft_backgrounds_keep_text_legible(string preset, string mode)
    {
        var c = Palette(preset, mode);

        foreach (var status in new[] { "Ok", "Warn", "Error", "Info" })
        {
            // SurfaceAlt too: menus and popups use it, e.g. «Forzar cierre…» in red.
            AssertContrast(c[$"{status}Brush"], c["SurfaceBrush"], 4.5);
            AssertContrast(c[$"{status}Brush"], c["SurfaceAltBrush"], 4.5);
            AssertContrast(c["TextBrush"], c[$"{status}SoftBrush"], 4.5);
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndModes))]
    public void Every_accent_seed_becomes_a_legible_fill_and_text(string preset, string mode)
    {
        var c = Palette(preset, mode);
        var surface = Parse(c["SurfaceBrush"]);
        var surfaceAlt = Parse(c["SurfaceAltBrush"]);
        var seeds = Colors("Accents.xaml").Where(e => e.Key.StartsWith("AccentSeed", StringComparison.Ordinal)).ToList();
        Assert.Equal(23, seeds.Count);

        foreach (var (name, hex) in seeds)
        {
            var (fill, onFill, text) = AccentPalette.For(Parse(hex), surface, surfaceAlt, mode == "Dark");
            Assert.True(AccentPalette.Contrast(onFill, fill) >= 4.5, $"{name} {preset} {mode}: texto sobre el relleno");
            Assert.True(AccentPalette.Contrast(fill, surface) >= 3.0, $"{name} {preset} {mode}: relleno sobre Surface");
            Assert.True(AccentPalette.Contrast(text, surface) >= 4.5, $"{name} {preset} {mode}: texto sobre Surface");
            Assert.True(AccentPalette.Contrast(text, surfaceAlt) >= 4.5, $"{name} {preset} {mode}: texto sobre SurfaceAlt");
        }
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#777777")]
    [InlineData("#FFFF00")]
    public void Even_an_extreme_custom_accent_stays_legible(string custom)
    {
        foreach (var (preset, mode) in Presets.SelectMany(p => new[] { (p, "Dark"), (p, "Light") }))
        {
            var c = Palette(preset, mode);
            var (fill, onFill, text) = AccentPalette.For(Parse(custom), Parse(c["SurfaceBrush"]), Parse(c["SurfaceAltBrush"]), mode == "Dark");
            Assert.True(AccentPalette.Contrast(onFill, fill) >= 4.5, $"{custom} {preset} {mode}");
            Assert.True(AccentPalette.Contrast(text, Parse(c["SurfaceAltBrush"])) >= 4.5, $"{custom} {preset} {mode}");
        }
    }

    [Fact]
    public void White_text_on_the_brand_gradient_is_legible()
    {
        var a = Colors("Accents.xaml");

        AssertContrast("#FFFFFF", a["BrandGradientStart"], 4.5);
        AssertContrast("#FFFFFF", a["BrandGradientEnd"], 4.5);
    }

    private static void AssertContrast(string foreground, string background, double minimum)
    {
        var ratio = AccentPalette.Contrast(Parse(foreground), Parse(background));
        Assert.True(ratio >= minimum, $"{foreground} sobre {background}: {ratio:0.00}:1, mínimo {minimum}:1");
    }

    // #AARRGGBB: contrast is measured on the opaque colour.
    private static Color Parse(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8) h = h[2..];
        return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..], 16));
    }

    private static Dictionary<string, string> Colors(string file) => ThemeFiles.Colors(file);
}
