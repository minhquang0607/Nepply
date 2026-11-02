param(
    [Parameter(Mandatory)][string]$Renders,
    [string]$Output = 'packaging/msix/store/screenshots'
)
# Places real WPF renders from the UI tests on a 1920 x 1080 canvas for the Store
# listing. Sample data comes from the test fixture; nothing is painted in by hand.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath((Join-Path $repo $Renders))
$target = [IO.Path]::GetFullPath((Join-Path $repo $Output))
New-Item -ItemType Directory -Path $target -Force | Out-Null

function Add-Picture($graphics, [string]$file, [int]$x, [int]$height) {
    $image = [Drawing.Image]::FromFile((Join-Path $source $file))
    try {
        $width = [int][Math]::Round($image.Width * $height / $image.Height)
        $left = if ($x -lt 0) { [int]((1920 - $width) / 2) } else { $x }
        $graphics.DrawImage($image, $left, [int]((1080 - $height) / 2), $width, $height)
    }
    finally { $image.Dispose() }
}
function New-Shot([string]$name, [string]$from, [string]$to, [scriptblock]$draw) {
    $bitmap = [Drawing.Bitmap]::new(1920, 1080)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
            $area = [Drawing.Rectangle]::new(0, 0, 1920, 1080)
            $brush = [Drawing.Drawing2D.LinearGradientBrush]::new($area, [Drawing.ColorTranslator]::FromHtml($from), [Drawing.ColorTranslator]::FromHtml($to), 35)
            try { $graphics.FillRectangle($brush, $area) } finally { $brush.Dispose() }
            & $draw $graphics
        }
        finally { $graphics.Dispose() }
        $bitmap.Save((Join-Path $target $name), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }
}
New-Shot 'nepply-panel-dark.png' '#1B2A3A' '#3A2F2A' { param($g) Add-Picture $g 'store-panel-dark.png' 24 1040 }
New-Shot 'nepply-panel-light.png' '#C9D6E2' '#E9E1D3' { param($g) Add-Picture $g 'store-panel-light.png' 24 1040 }
New-Shot 'nepply-settings.png' '#1B2A3A' '#3A2F2A' { param($g) Add-Picture $g 'store-settings.png' -1 1000 }
Get-ChildItem -LiteralPath $target -Filter '*.png' | ForEach-Object { '{0} {1}' -f $_.Name, (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
