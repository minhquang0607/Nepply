param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$IdentityFile,
    [string]$MakeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe',
    [switch]$InternalOnly,
    # Partner Center identity; the Store signs the package after certification.
    [switch]$Store,
    # Dry runs only: the first Store package must carry the stable source version.
    [switch]$AllowPrereleaseSource
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
if ($InternalOnly -eq $Store) { throw 'Choose exactly one of -InternalOnly or -Store.' }
if ([string]::IsNullOrWhiteSpace($IdentityFile)) { $IdentityFile = if ($Store) { 'packaging/msix/identity.store.json' } else { 'packaging/msix/identity.internal.json' } }
$identity = Get-Content -LiteralPath (Join-Path $repo $IdentityFile) -Raw | ConvertFrom-Json
if ($Store) {
    $core = ([string](Get-NepplyReleaseVersion -Repository $repo).Version -split '-')[0]
    $prerelease = [string](Get-NepplyReleaseVersion -Repository $repo).Version -ne $core
    if ($identity.InternalOnly -ne $false -or $identity.Name -notmatch '^[A-Za-z0-9.-]{3,50}$' -or $identity.Name -match 'Internal' -or
        $identity.Publisher -notmatch '^CN=[^,]+$' -or $identity.Publisher -cmatch 'Nepply Internal' -or
        [string]::IsNullOrWhiteSpace($identity.PublisherDisplayName) -or [string]::IsNullOrWhiteSpace($identity.DisplayName) -or
        $identity.Version -notmatch '^[1-9]\d*\.\d+\.\d+\.0$') { throw 'The Store needs the exact identity from Partner Center and a version whose first part is not 0 and last part is 0.' }
    if ($identity.Version -cne "$core.0") { throw "Package version $($identity.Version) does not match source version $core." }
    if ($prerelease -and -not $AllowPrereleaseSource) { throw 'The source is still a prerelease; bump it before building for the Store.' }
}
elseif ($identity.InternalOnly -ne $true -or $identity.Name -cne 'Nepply.Internal.Preview' -or
    $identity.Publisher -cne 'CN=Nepply Internal Preview' -or
    $identity.Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Only the separate QA identity is accepted; never impersonate the Store identity.' }
$parsedVersion = [version]$identity.Version
if ($parsedVersion.Major -gt 65535 -or $parsedVersion.Minor -gt 65535 -or $parsedVersion.Build -gt 65535) {
    throw 'Package version is out of range.'
}
if (-not (Test-Path -LiteralPath $MakeAppx -PathType Leaf) -or (Get-AuthenticodeSignature -LiteralPath $MakeAppx).Status -ne 'Valid') {
    throw 'Requires the official, validly signed MakeAppx from the Windows SDK.'
}
$output = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
$layout = Join-Path $output 'layout'
$app = Join-Path $layout 'App'
Push-Location $repo
try {
    & dotnet restore src/Nepply/Nepply.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'MSIX restore failed.' }
    # MSIX already compresses the payload: no need to self-extract DLLs to temp like the portable single file.
    & dotnet publish src/Nepply/Nepply.csproj -c Release --no-restore -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=None -p:DebugSymbols=false -o $app
    if ($LASTEXITCODE -ne 0) { throw 'MSIX publish failed.' }
    # The Store edition cannot hide Desktop icons, so it has nothing to guard.
    if (-not $Store) { & (Join-Path $PSScriptRoot 'Publish-DesktopGuardian.ps1') -OutputDirectory (Join-Path $app 'guardian') }
    & (Join-Path $PSScriptRoot 'Verify-RuntimePayload.ps1') -Directory $app
    & (Join-Path $PSScriptRoot 'Collect-ThirdPartyNotices.ps1') -Destination ([IO.Path]::GetRelativePath($repo,(Join-Path $app 'licenses')))
    if (-not $Store) { Copy-Item -LiteralPath 'src/Nepply/Resources/RELEASE_NOTICE.txt' -Destination $app }
    $assets = Join-Path $layout 'Assets'
    New-Item -ItemType Directory -Path $assets | Out-Null
    # Only resizes existing assets for packaging; does not redesign the icon.
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
    if ($Store) {
        $manifest.Package.Identity.SetAttribute('Name',$identity.Name)
        $manifest.Package.Identity.SetAttribute('Publisher',$identity.Publisher)
        $manifest.Package.Properties.DisplayName = [string]$identity.DisplayName
        $manifest.Package.Properties.PublisherDisplayName = [string]$identity.PublisherDisplayName
        $manifest.Package.Applications.Application.VisualElements.SetAttribute('DisplayName',$identity.DisplayName)
    }
    $manifest.Save((Join-Path $layout 'AppxManifest.xml'))
    & (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $layout -Mode Create
    & (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $layout -Mode Verify
    $package = Join-Path $output $(if ($Store) { "Nepply-$($identity.Version)-x64.msix" } else { "Nepply-Internal-$($identity.Version)-x64.msix" })
    & $MakeAppx pack /d $layout /p $package /o *> (Join-Path $output 'makeappx-pack.log')
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx validation/pack failed.' }
    $unpacked = Join-Path $output 'verification-unpacked'
    & $MakeAppx unpack /p $package /d $unpacked /o *> (Join-Path $output 'makeappx-unpack.log')
    if ($LASTEXITCODE -ne 0) { throw 'Could not unpack the MSIX for verification.' }
    # Generated Appx block map/content types are packaging metadata, not part of the payload baseline.
    $baseline = Get-Content -LiteralPath (Join-Path $layout 'release-manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $baseline.Files) {
        $actual = Join-Path $unpacked $file.Path
        if (-not (Test-Path -LiteralPath $actual -PathType Leaf) -or
            (Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash -cne $file.SHA256) {
            throw "MSIX payload mismatch: $($file.Path)"
        }
    }
    [ordered]@{
        Package=(Split-Path $package -Leaf); SHA256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
        Identity=$identity; SourceVersion=(Get-NepplyReleaseVersion -Repository $repo).Version
        SemanticValidation=$true; PayloadHashesVerified=$true; Signed=$false; Installed=$false
        StoreReady=$false; Pending=$(if ($Store) { @('Windows App Certification Kit','Packaged runtime QA','Store certification') } else { @('StartupTask settings in a real package and at logon','Packaged runtime QA','QA identity only; not for the Store') })
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'msix-build.json') -Encoding utf8
    Write-Output "$(if ($Store) { 'Store' } else { 'Internal' }) MSIX packed, unpacked and payload verified; not signed, installed or submitted to the Store."
} finally { Pop-Location }
