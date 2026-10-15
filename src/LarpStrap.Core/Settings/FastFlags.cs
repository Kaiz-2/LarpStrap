using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LarpStrap.Core.Settings;

public enum FastFlagKind
{
    Bool,
    Int,
}

public sealed record FastFlagInfo(string Name, FastFlagKind Kind, string Title, string Description, int Min = 0, int Max = 100_000);

/// <summary>
/// The only FastFlags Roblox's Player reads from <c>ClientAppSettings.json</c> since 2025-09-29: the 18 of the official
/// announcement (https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569).
/// Roblox may change it without notice; a new list ships with a new LarpStrap version. The type comes from the name
/// prefix (FFlag/DFFlag = true/false, FInt/DFInt = whole number), Roblox's naming convention.
/// </summary>
public static class FastFlags
{
    public static IReadOnlyList<FastFlagInfo> Allowlist { get; } =
    [
        new("FFlagDebugGraphicsPreferD3D11", FastFlagKind.Bool, "Preferir Direct3D 11", "Pide a Roblox que dibuje con Direct3D 11. Activa solo una API gráfica."),
        new("FFlagDebugGraphicsPreferVulkan", FastFlagKind.Bool, "Preferir Vulkan", "Pide a Roblox que dibuje con Vulkan. Activa solo una API gráfica."),
        new("FFlagDebugGraphicsPreferOpenGL", FastFlagKind.Bool, "Preferir OpenGL", "Pide a Roblox que dibuje con OpenGL. Activa solo una API gráfica."),
        new("DFIntDebugFRMQualityLevelOverride", FastFlagKind.Int, "Nivel de calidad gráfica forzado", "Fija el nivel de calidad gráfica de Roblox (1 a 21).", 1, 21),
        new("FIntDebugForceMSAASamples", FastFlagKind.Int, "Antialiasing (MSAA)", "Muestras de antialiasing: 0, 1, 2, 4 u 8.", 0, 8),
        new("DFFlagTextureQualityOverrideEnabled", FastFlagKind.Bool, "Forzar la calidad de texturas", "Hace que Roblox use la calidad de texturas de abajo."),
        new("DFIntTextureQualityOverride", FastFlagKind.Int, "Calidad de texturas", "0 (la más baja) a 3 (la más alta). Requiere «Forzar la calidad de texturas».", 0, 3),
        new("DFFlagDisableDPIScale", FastFlagKind.Bool, "Desactivar el escalado por DPI", "Roblox ignora el escalado de pantalla de Windows."),
        new("FFlagHandleAltEnterFullscreenManually", FastFlagKind.Bool, "Gestión propia de Alt+Intro", "Desactivado, Alt+Intro usa la pantalla completa exclusiva de Windows."),
        new("DFIntCSGLevelOfDetailSwitchingDistance", FastFlagKind.Int, "Distancia de detalle CSG (inicial)", "Distancia a la que las uniones CSG empiezan a perder detalle."),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL12", FastFlagKind.Int, "Distancia de detalle CSG (nivel 1 a 2)", "Distancia del cambio del nivel de detalle 1 al 2."),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL23", FastFlagKind.Int, "Distancia de detalle CSG (nivel 2 a 3)", "Distancia del cambio del nivel de detalle 2 al 3."),
        new("DFIntCSGLevelOfDetailSwitchingDistanceL34", FastFlagKind.Int, "Distancia de detalle CSG (nivel 3 a 4)", "Distancia del cambio del nivel de detalle 3 al 4."),
        new("FIntFRMMinGrassDistance", FastFlagKind.Int, "Distancia mínima de la hierba", "Desde dónde se dibuja la hierba."),
        new("FIntFRMMaxGrassDistance", FastFlagKind.Int, "Distancia máxima de la hierba", "Hasta dónde se dibuja la hierba; 0 la quita."),
        new("FIntGrassMovementReducedMotionFactor", FastFlagKind.Int, "Movimiento de la hierba", "Cuánto se mueve la hierba con el viento; 0 la deja quieta."),
        new("FFlagDebugSkyGray", FastFlagKind.Bool, "Cielo gris", "Sustituye el cielo por un fondo gris."),
        new("DFFlagDebugPauseVoxelizer", FastFlagKind.Bool, "Pausar el voxelizador", "Detiene la actualización de la iluminación por vóxeles; puede dejar la luz desfasada."),
    ];

    public const string FileName = "ClientAppSettings.json";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static readonly Dictionary<string, FastFlagInfo> ByName = Allowlist.ToDictionary(f => f.Name, StringComparer.Ordinal);

    /// <summary>The value as Roblox reads it ("True"/"False" or a whole number in range), or null when not valid for that flag.</summary>
    public static string? Normalize(string name, string? value)
    {
        if (!ByName.TryGetValue(name, out var info) || value is null) return null;
        value = value.Trim();
        return info.Kind switch
        {
            FastFlagKind.Bool when bool.TryParse(value, out var b) => b ? "True" : "False",
            FastFlagKind.Int when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= info.Min && n <= info.Max =>
                n.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>Keeps only allow-listed flags with valid values (a hand-edited settings file can't smuggle others in).</summary>
    public static IReadOnlyDictionary<string, string> Clean(IReadOnlyDictionary<string, string>? flags) =>
        flags is null ? new Dictionary<string, string>()
            : flags.Select(f => (f.Key, Value: Normalize(f.Key, f.Value))).Where(f => f.Value is not null)
                .ToDictionary(f => f.Key, f => f.Value!, StringComparer.Ordinal);

    /// <summary>
    /// Writes the flags into <c>ClientSettings\ClientAppSettings.json</c> of the version about to launch. Keys LarpStrap
    /// doesn't manage (e.g. another bootstrapper's) are kept; allow-listed ones are replaced by the user's choice, so
    /// clearing a flag removes it. Nothing is written when nothing changes. Throws IOException/UnauthorizedAccessException.
    /// </summary>
    public static void Apply(string versionDirectory, IReadOnlyDictionary<string, string>? flags)
    {
        var path = Path.Combine(versionDirectory, "ClientSettings", FileName);
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        var json = Parse(existing);
        if (json is null) return; // not a JSON object: someone else's format, never overwritten

        var wanted = Clean(flags);
        var changed = false;
        foreach (var name in ByName.Keys)
        {
            var current = json.TryGetPropertyValue(name, out var node) ? node?.ToString() : null;
            var want = wanted.GetValueOrDefault(name);
            if (current == want) continue;
            changed = true;
            if (want is null) json.Remove(name);
            else json[name] = want;
        }
        if (!changed) return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json.ToJsonString(Indented));
        File.Move(temp, path, overwrite: true);
    }

    private static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
