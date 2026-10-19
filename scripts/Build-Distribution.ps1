param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$InnoCompiler = 'C:\Program Files\Inno Setup 7\ISCC.exe',
    # Persistent directory shared across builds; holds Inno's uninst-*.e32 for external signing.
    [string]$SignedUninstallerDirectory,
    [switch]$InternalOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$releaseVersion = Get-NepplyReleaseVersion -Repository $repo
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be a new directory under artifacts.'
}
if (Test-Path -LiteralPath $output) { throw 'Output already exists; choose a new directory to preserve previous evidence.' }
$ancestor = Split-Path $output -Parent
while ($ancestor -and $ancestor.Length -ge $repo.Length) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Output ancestors must not be links.' }
    $ancestor = Split-Path $ancestor -Parent
}
if (-not $InternalOnly) { throw 'Public release is not enabled: first-party license, notices and signing must be approved first.' }
if (-not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) { throw 'Inno compiler was not found.' }
if ((Get-AuthenticodeSignature -LiteralPath $InnoCompiler).Status -ne 'Valid') { throw 'Compiler signature is not valid.' }
function Invoke-Checked([scriptblock]$Action) {
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "External command failed: $LASTEXITCODE" }
}
$signedUninstaller = $null
if ($SignedUninstallerDirectory) {
    $signedUninstaller = [IO.Path]::GetFullPath($SignedUninstallerDirectory, $repo)
    if (-not (Test-Path -LiteralPath $signedUninstaller -PathType Container)) { throw 'Signed uninstaller directory must already exist.' }
    if ((Get-Item -LiteralPath $signedUninstaller).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Signed uninstaller directory must not be a link.' }
    if ($signedUninstaller.StartsWith($output + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Signed uninstaller directory must outlive this output directory.'
    }
}
# Untrusted test roots are tolerated only for internal builds; tampered or broken signatures never are.
function Assert-UninstallerSignature([string]$Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    $accepted = $signature.Status -eq 'Valid' -or ($InternalOnly -and $signature.Status -eq 'UnknownError' -and $signature.SignerCertificate)
    if (-not $accepted) { throw "Signed uninstaller rejected ($($signature.Status)): $Path" }
    $signature
}
Push-Location $repo
try {
    Invoke-Checked { dotnet restore Nepply.slnx --locked-mode --disable-parallel -m:1 }
    Invoke-Checked { dotnet build Nepply.slnx -c Release --no-restore -m:1 }
    & "$PSScriptRoot/Test-BuildBoundary.ps1"
    & "$PSScriptRoot/Test-LanguageResources.ps1"
    & "$PSScriptRoot/Test-InstallerContract.ps1"
    New-Item -ItemType Directory -Path $output | Out-Null
    $portable = Join-Path $output 'portable'
    Invoke-Checked { dotnet publish src/Nepply/Nepply.csproj -c Release --no-restore -p:DebugType=None -p:DebugSymbols=false -o $portable }
    & "$PSScriptRoot/Publish-DesktopGuardian.ps1" -OutputDirectory (Join-Path $portable 'guardian')
    Copy-Item -LiteralPath 'src/Nepply/Resources/BETA_NOTICE.txt' -Destination $portable
    & "$PSScriptRoot/Collect-ThirdPartyNotices.ps1" -Destination ([IO.Path]::GetRelativePath($repo,(Join-Path $portable 'licenses'))) -RequireComplete
    & "$PSScriptRoot/Release-Manifest.ps1" -Directory $portable -Mode Create
    $innoArguments = @("/DPayloadRoot=$portable", "/DReleaseOutputDir=$(Join-Path $output 'installer')", "/DReleaseVersion=$($releaseVersion.Version)", "/DReleaseFileVersion=$($releaseVersion.FileVersion)")
    if ($signedUninstaller) { $innoArguments += "/DSignedUninstallerDir=$signedUninstaller" }
    $innoLog = @(& $InnoCompiler @innoArguments 'packaging/inno/Nepply.iss' 2>&1 | ForEach-Object { "$_" })
    $innoExit = $LASTEXITCODE
    $innoLog | Set-Content -LiteralPath (Join-Path $output 'inno-compile.log') -Encoding utf8
    $innoLog
    if ($signedUninstaller) {
        # Inno aborts both when it creates the file and when a later compile still finds it unsigned.
        $pending = [regex]::Match(($innoLog -join "`n"), 'Signed uninstaller mode is enabled\..*?(?<path>[A-Za-z]:\\[^\r\n]*?uninst-[^\\\r\n]+\.e32)', 'Singleline')
        if ($innoExit -ne 0 -and $pending.Success) {
            throw "SIGNATURE PENDING: sign $($pending.Groups['path'].Value) (SignPath or scripts/Sign-Artifact.ps1), then rerun with a new -OutputDirectory. No Setup was produced."
        }
    }
    if ($innoExit -ne 0) { throw "External command failed: $innoExit" }
    if ($signedUninstaller) {
        $used = $innoLog | Select-String -Pattern 'Using existing signed uninstaller file: (.+\.e32)\s*$' | Select-Object -Last 1
        if (-not $used) { throw 'Inno did not report which signed uninstaller it embedded.' }
        # Inno may print a long path for a short (8.3) input, so match by name and content, not by prefix.
        $reported = $used.Matches[0].Groups[1].Value.Trim()
        $usedPath = Join-Path $signedUninstaller (Split-Path $reported -Leaf)
        if (-not (Test-Path -LiteralPath $usedPath -PathType Leaf) -or -not (Test-Path -LiteralPath $reported -PathType Leaf) -or
            (Get-FileHash -LiteralPath $usedPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $reported -Algorithm SHA256).Hash) {
            throw 'Embedded uninstaller came from an unexpected directory.'
        }
        $uninstallerSignature = Assert-UninstallerSignature $usedPath
        [pscustomobject]@{ File = Split-Path $usedPath -Leaf; Signature = [string]$uninstallerSignature.Status; SignerThumbprint = $uninstallerSignature.SignerCertificate.Thumbprint;
            SHA256 = (Get-FileHash -LiteralPath $usedPath -Algorithm SHA256).Hash } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'signed-uninstaller.json') -Encoding utf8
    }
    [xml]$project = Get-Content 'src/Nepply/Nepply.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $installer = Join-Path $output "installer/Nepply-$version-Setup.exe"
    & "$PSScriptRoot/Verify-Release.ps1" -Application (Join-Path $portable 'Nepply.exe') -Installer $installer
    $zip = Join-Path $output "Nepply-$version-win-x64-portable.zip"
    Compress-Archive -Path "$portable/*" -DestinationPath $zip
    @($zip,$installer) | ForEach-Object { Get-FileHash -LiteralPath $_ -Algorithm SHA256 } |
        Select-Object @{n='File';e={Split-Path $_.Path -Leaf}},Hash |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'SHA256.json') -Encoding utf8
    Write-Output 'INTERNAL ONLY: unsigned artifacts; license/notice/publication gates remain open. Installer runtime testing is separate.'
} finally { Pop-Location }
