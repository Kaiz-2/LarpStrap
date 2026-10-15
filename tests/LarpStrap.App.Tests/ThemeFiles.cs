using System.IO;
using System.Xml.Linq;

namespace LarpStrap.App.Tests;

/// <summary>Reads design tokens straight from the repository's Themes/*.xaml, without loading WPF.</summary>
internal static class ThemeFiles
{
    public static readonly string Root = FindRoot();
    public static readonly string Themes = Path.Combine(Root, "src", "LarpStrap.App", "Themes");

    /// <summary>Colour by x:Key, from &lt;SolidColorBrush x:Key Color&gt; and &lt;Color x:Key&gt;value&lt;/Color&gt;.</summary>
    public static Dictionary<string, string> Colors(string file)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var doc = XDocument.Load(Path.Combine(Themes, file));
        var result = new Dictionary<string, string>();
        foreach (var e in doc.Root!.Elements())
        {
            var key = (string?)e.Attribute(x + "Key");
            if (key is null) continue;
            var value = e.Name.LocalName switch
            {
                "SolidColorBrush" => (string?)e.Attribute("Color"),
                "Color" => e.Value.Trim(),
                _ => null,
            };
            if (value is not null && value.StartsWith('#')) result[key] = value;
        }
        return result;
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "LarpStrap.slnx"))) return dir.FullName;
        throw new InvalidOperationException("No se encontró LarpStrap.slnx por encima del directorio de los tests.");
    }
}
