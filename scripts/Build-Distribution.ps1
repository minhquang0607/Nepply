param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$InnoCompiler = 'C:\Program Files\Inno Setup 7\ISCC.exe',
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
    Invoke-Checked { & $InnoCompiler "/DPayloadRoot=$portable" "/DReleaseOutputDir=$(Join-Path $output 'installer')" "/DReleaseVersion=$($releaseVersion.Version)" "/DReleaseFileVersion=$($releaseVersion.FileVersion)" 'packaging/inno/Nepply.iss' }
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
