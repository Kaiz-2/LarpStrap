# Exports the design tokens from src/LarpStrap.App/Themes/*.xaml (the only source of colour) to
#   assets/design-tokens.json  and  assets/design-tokens.css
# for other surfaces (a future website, docs). Never edit those files by hand: change the XAML and re-run this.
# DesignTokensTests fails if the exported files and the XAML drift apart.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$themes = Join-Path $root 'src/LarpStrap.App/Themes'
$xamlNs = 'http://schemas.microsoft.com/winfx/2006/xaml'

function Read-Tokens([string]$file) {
    [xml]$x = Get-Content (Join-Path $themes $file) -Raw
    $map = [ordered]@{}
    foreach ($n in $x.ResourceDictionary.ChildNodes) {
        if (-not $n.Attributes) { continue }
        $key = $n.GetAttribute('Key', $xamlNs)
        if (-not $key) { continue }
        $value = switch ($n.LocalName) {
            'SolidColorBrush' { $n.GetAttribute('Color') }
            'Color' { $n.InnerText.Trim() }
            'Double' { $n.InnerText.Trim() }
            'CornerRadius' { $n.InnerText.Trim() }
            'FontFamily' { $n.InnerText.Trim() }
            default { $null }
        }
        if ($value -and -not $value.StartsWith('{')) { $map[$key] = $value }
    }
    $map
}
function Css([string]$hex) {
    # XAML #AARRGGBB -> CSS #RRGGBBAA
    if ($hex.Length -eq 9) { '#' + $hex.Substring(3) + $hex.Substring(1, 2) } else { $hex }
}
function Kebab([string]$s) { ($s -creplace '(?<!^)([A-Z])', '-$1').ToLowerInvariant() }

$dark = Read-Tokens 'Colors.Dark.xaml'
$light = Read-Tokens 'Colors.Light.xaml'
$accents = Read-Tokens 'Accents.xaml'
$presets = Read-Tokens 'Presets.xaml'
$tokens = Read-Tokens 'Tokens.xaml'

$json = [ordered]@{
    '$description' = 'LarpStrap «Nebulosa» design tokens, exported from src/LarpStrap.App/Themes by eng/Export-DesignTokens.ps1. Do not edit.'
    brand = 'LarpStrap'
    color = [ordered]@{
        dark = [ordered]@{}; light = [ordered]@{}
    }
    accent = [ordered]@{}
    theme = [ordered]@{}
    gradient = [ordered]@{ start = $accents['BrandGradientStart']; end = $accents['BrandGradientEnd'] }
    font = [ordered]@{}
    size = [ordered]@{}
    radius = [ordered]@{}
}
foreach ($k in $dark.Keys) { $json.color.dark[(Kebab ($k -replace 'Brush$', ''))] = (Css $dark[$k]) }
foreach ($k in $light.Keys) { $json.color.light[(Kebab ($k -replace 'Brush$', ''))] = (Css $light[$k]) }
# Accent seeds: the app derives fill and text from each seed for the active theme (AccentPalette).
foreach ($k in $accents.Keys | Where-Object { $_ -match '^AccentSeed(\w+)$' }) {
    $null = $k -match '^AccentSeed(\w+)$'
    $json.accent[$Matches[1].ToLowerInvariant()] = (Css $accents[$k])
}
# Interface themes: surfaces per mode (text, borders and states stay in color.dark / color.light).
foreach ($k in $presets.Keys) {
    $null = $k -match '^Preset(\w+?)(Dark|Light)(\w+)Brush$'
    $name = $Matches[1].ToLowerInvariant(); $mode = $Matches[2].ToLowerInvariant()
    if (-not $json.theme.Contains($name)) { $json.theme[$name] = [ordered]@{ dark = [ordered]@{}; light = [ordered]@{} } }
    $json.theme[$name][$mode][(Kebab $Matches[3])] = (Css $presets[$k])
}
foreach ($k in $tokens.Keys) {
    if ($k -like 'Font*' -and $k -notlike 'FontSize*') { $json.font[(Kebab ($k -replace '^Font', ''))] = $tokens[$k] }
    elseif ($k -like 'FontSize*') { $json.size[(Kebab ($k -replace '^FontSize', ''))] = [double]$tokens[$k] }
    elseif ($k -like 'Radius*') { $json.radius[(Kebab ($k -replace '^Radius', ''))] = [double]$tokens[$k] }
}

$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $assets 'design-tokens.json'), (($json | ConvertTo-Json -Depth 6) -replace "`r`n", "`n") + "`n", $utf8)

$css = New-Object System.Text.StringBuilder
[void]$css.AppendLine('/* LarpStrap «Nebulosa» design tokens: exported from src/LarpStrap.App/Themes by eng/Export-DesignTokens.ps1. Do not edit. */')
[void]$css.AppendLine(':root {')
foreach ($k in $json.color.dark.Keys) { [void]$css.AppendLine("  --color-$k`: $($json.color.dark[$k]);") }
foreach ($a in $json.accent.Keys) { [void]$css.AppendLine("  --accent-$a`: $($json.accent[$a]);") }
[void]$css.AppendLine("  --gradient-brand: linear-gradient(135deg, $($json.gradient.start), $($json.gradient.end));")
foreach ($k in $json.font.Keys) { [void]$css.AppendLine("  --font-$k`: $($json.font[$k]);") }
foreach ($k in $json.size.Keys) { [void]$css.AppendLine("  --size-$k`: $($json.size[$k])px;") }
foreach ($k in $json.radius.Keys) { [void]$css.AppendLine("  --radius-$k`: $($json.radius[$k])px;") }
[void]$css.AppendLine('}')
[void]$css.AppendLine('[data-theme="light"] {')
foreach ($k in $json.color.light.Keys) { [void]$css.AppendLine("  --color-$k`: $($json.color.light[$k]);") }
[void]$css.AppendLine('}')
[IO.File]::WriteAllText((Join-Path $assets 'design-tokens.css'), ($css.ToString() -replace "`r`n", "`n"), $utf8)
"Tokens exportados: assets/design-tokens.json y assets/design-tokens.css"
