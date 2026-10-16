param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$IdentityFile = 'packaging/msix/identity.internal.json',
    [string]$MakeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe',
    [switch]$InternalOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
if (-not $InternalOnly) { throw 'MSIX public chưa bật: identity Store, notices và nghiệm thu lifecycle chưa hoàn tất.' }
$identity = Get-Content -LiteralPath (Join-Path $repo $IdentityFile) -Raw | ConvertFrom-Json
if ($identity.InternalOnly -ne $true -or $identity.Name -cne 'Nepply.Internal.Preview' -or
    $identity.Publisher -cne 'CN=Nepply Internal Preview' -or
    $identity.Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Chỉ chấp nhận identity QA riêng; không giả mạo identity Store.' }
$parsedVersion = [version]$identity.Version
if ($parsedVersion.Major -gt 65535 -or $parsedVersion.Minor -gt 65535 -or $parsedVersion.Build -gt 65535) {
    throw 'Package version vượt giới hạn.'
}
if (-not (Test-Path -LiteralPath $MakeAppx -PathType Leaf) -or (Get-AuthenticodeSignature -LiteralPath $MakeAppx).Status -ne 'Valid') {
    throw 'Cần MakeAppx chính thức có chữ ký hợp lệ từ Windows SDK.'
}
$output = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
$layout = Join-Path $output 'layout'
$app = Join-Path $layout 'App'
Push-Location $repo
try {
    & dotnet restore src/Nepply/Nepply.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore MSIX thất bại.' }
    # MSIX đã nén payload: không cần self-extract DLL ra temp như portable single-file.
    & dotnet publish src/Nepply/Nepply.csproj -c Release --no-restore -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=None -p:DebugSymbols=false -o $app
    if ($LASTEXITCODE -ne 0) { throw 'Publish MSIX thất bại.' }
    & (Join-Path $PSScriptRoot 'Publish-DesktopGuardian.ps1') -OutputDirectory (Join-Path $app 'guardian')
    & (Join-Path $PSScriptRoot 'Verify-RuntimePayload.ps1') -Directory $app
    & (Join-Path $PSScriptRoot 'Collect-ThirdPartyNotices.ps1') -Destination ([IO.Path]::GetRelativePath($repo,(Join-Path $app 'licenses')))
    Copy-Item -LiteralPath 'src/Nepply/Resources/BETA_NOTICE.txt' -Destination $app
    $assets = Join-Path $layout 'Assets'
    New-Item -ItemType Directory -Path $assets | Out-Null
    # Chỉ đổi kích thước asset hiện có để đóng gói, không thiết kế lại icon.
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Image]::FromFile((Join-Path $repo 'src/Nepply/Resources/nepply.png'))
    try {
        foreach ($asset in @(@('StoreLogo.png',50),@('Square150x150Logo.png',150),@('Square44x44Logo.png',44))) {
            $size = [int]$asset[1]
            $bitmap = [Drawing.Bitmap]::new($size,$size)
            try {
                $graphics = [Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.Clear([Drawing.Color]::Transparent)
                    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $scale = [Math]::Min($size / $image.Width,$size / $image.Height)
                    $width = [int][Math]::Round($image.Width * $scale)
                    $height = [int][Math]::Round($image.Height * $scale)
                    $graphics.DrawImage($image,[int](($size-$width)/2),[int](($size-$height)/2),$width,$height)
                } finally { $graphics.Dispose() }
                $bitmap.Save((Join-Path $assets $asset[0]),[Drawing.Imaging.ImageFormat]::Png)
            } finally { $bitmap.Dispose() }
        }
    } finally { $image.Dispose() }
    [xml]$manifest = Get-Content -LiteralPath 'packaging/msix/AppxManifest.xml' -Raw
    $manifest.Package.Identity.SetAttribute('Version',$identity.Version)
    $manifest.Save((Join-Path $layout 'AppxManifest.xml'))
    & (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $layout -Mode Create
    & (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $layout -Mode Verify
    $package = Join-Path $output "Nepply-Internal-$($identity.Version)-x64.msix"
    & $MakeAppx pack /d $layout /p $package /o *> (Join-Path $output 'makeappx-pack.log')
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx validation/pack thất bại.' }
    $unpacked = Join-Path $output 'verification-unpacked'
    & $MakeAppx unpack /p $package /d $unpacked /o *> (Join-Path $output 'makeappx-unpack.log')
    if ($LASTEXITCODE -ne 0) { throw 'Không giải kiểm tra được MSIX.' }
    # Generated Appx block map/content types là packaging metadata, không thuộc payload baseline.
    $baseline = Get-Content -LiteralPath (Join-Path $layout 'release-manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $baseline.Files) {
        $actual = Join-Path $unpacked $file.Path
        if (-not (Test-Path -LiteralPath $actual -PathType Leaf) -or
            (Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash -cne $file.SHA256) {
            throw "MSIX payload không khớp: $($file.Path)"
        }
    }
    [ordered]@{
        Package=(Split-Path $package -Leaf); SHA256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
        Identity=$identity; SourceVersion=(Get-NepplyReleaseVersion -Repository $repo).Version
        SemanticValidation=$true; PayloadHashesVerified=$true; Signed=$false; Installed=$false
        StoreReady=$false; Pending=@('StartupTask Settings trong package thật và logon','Desktop recovery khi gỡ/update','Packaged runtime QA','Notices','Identity Store','License decision trước open-source')
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'msix-build.json') -Encoding utf8
    Write-Output 'MSIX nội bộ đã pack/unpack và đối chiếu payload; chưa ký/cài/submit Store.'
} finally { Pop-Location }
