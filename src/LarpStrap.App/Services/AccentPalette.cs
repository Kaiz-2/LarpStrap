using System.Windows.Media;

namespace LarpStrap.App.Services;

/// <summary>
/// Turns an accent seed into the three colours the interface uses, for one theme's surfaces: a fill with at least 3:1
/// against the surface, white or black on that fill (whichever contrasts more, always at least 4.5:1) and the accent as
/// text with at least 4.5:1 on both surfaces. The seed is lightened (dark themes) or darkened (light themes) only as
/// much as needed.
/// </summary>
public static class AccentPalette
{
    public static (Color Fill, Color OnFill, Color Text) For(Color seed, Color surface, Color surfaceAlt, bool dark)
    {
        var fill = AdjustUntil(seed, [surface], 3.0, lighten: dark);
        var onFill = Contrast(fill, Colors.White) >= Contrast(fill, Colors.Black) ? Colors.White : Colors.Black;
        var text = AdjustUntil(seed, [surface, surfaceAlt], 4.5, lighten: dark);
        return (fill, onFill, text);
    }

    public static double Contrast(Color a, Color b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static Color AdjustUntil(Color source, Color[] backgrounds, double minimum, bool lighten)
    {
        var target = lighten ? Colors.White : Colors.Black;
        for (var step = 0; step <= 50; step++)
        {
            var candidate = Blend(source, target, step / 50.0);
            if (backgrounds.All(background => Contrast(candidate, background) >= minimum)) return candidate;
        }
        return target;
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
