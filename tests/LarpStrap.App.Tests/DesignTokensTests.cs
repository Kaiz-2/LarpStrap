using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LarpStrap.App.Tests;

/// <summary>
/// assets/design-tokens.json is an export of Themes/*.xaml (eng/Export-DesignTokens.ps1), never a second source of
/// colour: every exported value must match the XAML. Re-run the script after changing a theme.
/// </summary>
public sealed partial class DesignTokensTests
{
    private static readonly JsonElement Json =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(ThemeFiles.Root, "assets", "design-tokens.json"))).RootElement;

    [Theory]
    [InlineData("Colors.Dark.xaml", "dark")]
    [InlineData("Colors.Light.xaml", "light")]
    public void Every_theme_colour_is_exported_with_the_same_value(string file, string theme)
    {
        var exported = Json.GetProperty("color").GetProperty(theme);

        foreach (var (key, value) in ThemeFiles.Colors(file))
            Assert.Equal(Css(value), exported.GetProperty(Kebab(key.Replace("Brush", ""))).GetString());
    }

    [Fact]
    public void Every_accent_seed_and_the_brand_gradient_are_exported_with_the_same_value()
    {
        var accents = ThemeFiles.Colors("Accents.xaml");

        foreach (var (key, value) in accents)
        {
            var m = AccentSeedKey().Match(key);
            if (!m.Success) continue;
            Assert.Equal(Css(value), Json.GetProperty("accent").GetProperty(m.Groups[1].Value.ToLowerInvariant()).GetString());
        }
        Assert.Equal(accents["BrandGradientStart"], Json.GetProperty("gradient").GetProperty("start").GetString());
        Assert.Equal(accents["BrandGradientEnd"], Json.GetProperty("gradient").GetProperty("end").GetString());
        Assert.Equal("LarpStrap", Json.GetProperty("brand").GetString());
    }

    [Fact]
    public void Every_interface_theme_surface_is_exported_with_the_same_value()
    {
        foreach (var (key, value) in ThemeFiles.Colors("Presets.xaml"))
        {
            var m = PresetKey().Match(key);
            Assert.True(m.Success, key);
            var exported = Json.GetProperty("theme").GetProperty(m.Groups[1].Value.ToLowerInvariant())
                .GetProperty(m.Groups[2].Value.ToLowerInvariant()).GetProperty(Kebab(m.Groups[3].Value)).GetString();
            Assert.Equal(Css(value), exported);
        }
    }

    [Fact]
    public void The_css_export_declares_the_same_brand_accent()
    {
        var css = File.ReadAllText(Path.Combine(ThemeFiles.Root, "assets", "design-tokens.css"));

        Assert.Contains($"--accent-violet: {ThemeFiles.Colors("Accents.xaml")["AccentSeedViolet"]};", css);
    }

    // XAML #AARRGGBB -> CSS #RRGGBBAA, as the export writes it.
    private static string Css(string hex) => hex.Length == 9 ? "#" + hex[3..] + hex[1..3] : hex;

    private static string Kebab(string s) => KebabBoundary().Replace(s, "-$1").ToLowerInvariant();

    [GeneratedRegex("^AccentSeed(\\w+)$")]
    private static partial Regex AccentSeedKey();

    [GeneratedRegex("^Preset(\\w+?)(Dark|Light)(\\w+)Brush$")]
    private static partial Regex PresetKey();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex KebabBoundary();
}
