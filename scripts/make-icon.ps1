$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$repo = Split-Path -Parent $PSScriptRoot
$dictionaryPath = Join-Path $repo 'src\WookText.App\Themes\Branding.xaml'
$reader = [System.Xml.XmlReader]::Create($dictionaryPath)
try { $resources = [System.Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Close() }
$drawing = $resources['AppMark'].Drawing
$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform(($size / 64.0), ($size / 64.0))))
    $context.DrawDrawing($drawing)
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32))
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    try { $encoder.Save($stream); $frames += @{ Size = $size; Bytes = $stream.ToArray() } } finally { $stream.Dispose() }
}
$iconPath = Join-Path $repo 'src\WookText.App\Assets\wText.ico'
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create($iconPath))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
[System.IO.File]::WriteAllBytes((Join-Path $repo 'src\WookText.App\Assets\wText.png'), $frames[-1].Bytes)
Write-Host 'Generated wText document icon (16-256 px) from the shared vector drawing.'
