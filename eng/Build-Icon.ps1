# Renders the LarpStrap logo (LogoTile + LogoGloss + LogoMark from src/LarpStrap.App/Themes/Tokens.xaml, brand gradient from
# Accents.xaml) into a multi-size Windows icon: src/LarpStrap.App/Assets/LarpStrap.ico (PNG-compressed entries).
# Run it after changing the logo; the .ico is committed so normal builds do not depend on this script.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$themes = Join-Path $root 'src/LarpStrap.App/Themes'

function Read-Key([string]$file, [string]$key) {
    [xml]$x = Get-Content (Join-Path $themes $file) -Raw
    $node = $x.ResourceDictionary.ChildNodes | Where-Object { $_.Attributes -and $_.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml') -eq $key }
    if (-not $node) { throw "No se encontró $key en $file" }
    $node.InnerText.Trim()
}
$tile = [System.Windows.Media.Geometry]::Parse((Read-Key 'Tokens.xaml' 'LogoTile'))
$mark = [System.Windows.Media.Geometry]::Parse((Read-Key 'Tokens.xaml' 'LogoMark'))
$gloss = [System.Windows.Media.Geometry]::Parse((Read-Key 'Tokens.xaml' 'LogoGloss'))
$glossBrush = New-Object System.Windows.Media.LinearGradientBrush([System.Windows.Media.Color]::FromArgb(0x38, 255, 255, 255), [System.Windows.Media.Color]::FromArgb(0, 255, 255, 255), 90)
$start = [System.Windows.Media.ColorConverter]::ConvertFromString((Read-Key 'Accents.xaml' 'BrandGradientStart'))
$end = [System.Windows.Media.ColorConverter]::ConvertFromString((Read-Key 'Accents.xaml' 'BrandGradientEnd'))
$gradient = New-Object System.Windows.Media.LinearGradientBrush($start, $end, (New-Object System.Windows.Point(0, 0)), (New-Object System.Windows.Point(1, 1)))

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $scale = $size / 64.0  # separate: in an argument list the comma binds tighter than '/'
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))
    $dc.DrawGeometry($gradient, $null, $tile)
    $dc.DrawGeometry($glossBrush, $null, $gloss)
    $dc.DrawGeometry([System.Windows.Media.Brushes]::White, $null, $mark)
    $dc.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    , @($size, $stream.ToArray())
}

# ICONDIR + ICONDIRENTRY[] + PNG data (supported since Windows Vista).
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $size = $img[0]; $data = $img[1]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img[1]) }
$w.Flush()
$target = Join-Path $root 'src/LarpStrap.App/Assets/LarpStrap.ico'
New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
# A 256 px PNG of the same logo for documentation.
[System.IO.File]::WriteAllBytes((Join-Path $root 'docs/design/logo-256.png'), ($images | Where-Object { $_[0] -eq 256 })[1])
"Icono: $target ($($sizes -join ', ') px, $((Get-Item $target).Length) bytes)"
