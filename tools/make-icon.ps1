# Generates the FocusLock app icon (a focus ring on the accent color) with System.Drawing. No packages needed.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\FocusLock.App\Assets\FocusLock.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Accent disc
    $accent = [System.Drawing.Color]::FromArgb(255, 61, 122, 156)
    $brush = New-Object System.Drawing.SolidBrush $accent
    $g.FillEllipse($brush, 0, 0, $size - 1, $size - 1)

    # Progress ring with a gap, round caps
    $penWidth = [Math]::Max(1.6, $size * 0.11)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penWidth
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $inset = $size * 0.24
    $g.DrawArc($pen, [float]$inset, [float]$inset, [float]($size - 2 * $inset), [float]($size - 2 * $inset), -90, 300)

    # Center dot
    $dot = $size * 0.085
    $g.FillEllipse([System.Drawing.Brushes]::White, [float]($size / 2 - $dot), [float]($size / 2 - $dot), [float](2 * $dot), [float](2 * $dot))

    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $pen.Dispose(); $brush.Dispose()
    @{ Size = $size; Bytes = $stream.ToArray() }
}

# Pack the PNGs into an .ico container.
$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dim = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Flush()

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
[System.IO.File]::WriteAllBytes($Out, $ico.ToArray())
"wrote $Out ($($ico.Length) bytes)"
