param(
    [string]$Source = 'src/Nepply/Resources/nepply.png',
    [string]$Destination = 'src/Nepply/Resources/icon.ico'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourcePath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Source))
$destinationPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Destination))
if (-not $destinationPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Icon output must stay inside the project.'
}
# Format conversion only: preserve the approved artwork, palette and composition.
$original = [Drawing.Image]::FromFile($sourcePath)
$frames = [Collections.Generic.List[object]]::new()
try {
    foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
        $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $png = [IO.MemoryStream]::new()
        $attributes = [Drawing.Imaging.ImageAttributes]::new()
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $graphics.DrawImage($original,[Drawing.Rectangle]::new(0,0,$size,$size),0,0,$original.Width,$original.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
            $bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{Size=$size;Bytes=$png.ToArray()})
        } finally { $attributes.Dispose(); $png.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $original.Dispose() }
$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    $writer.Flush()
    [IO.File]::WriteAllBytes($destinationPath,$stream.ToArray())
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "Created $destinationPath with $($frames.Count) PNG frames (16–256 px)."
