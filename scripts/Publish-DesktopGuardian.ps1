param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$version = Get-NepplyReleaseVersion -Repository $repo
$output = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
Push-Location $repo
try {
    & dotnet restore src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Desktop guardian restore failed.' }
    & dotnet publish src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj -c Release --no-restore `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false `
        "-p:Version=$($version.Version)" "-p:FileVersion=$($version.FileVersion)" -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Desktop guardian publish failed.' }
    $files = @(Get-ChildItem -LiteralPath $output -Force)
    if ($files.Count -ne 1 -or $files[0].Name -cne 'Nepply.DesktopGuardian.exe' -or $files[0].PSIsContainer -or
        ($files[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or $files[0].Length -le 0) {
        throw 'The guardian must be exactly one self-contained executable; never ship a missing or unexpected payload.'
    }
    if ($files[0].VersionInfo.ProductVersion.Split('+')[0] -cne $version.Version -or
        $files[0].VersionInfo.FileVersion -cne $version.FileVersion) { throw 'Guardian version does not match the app.' }
    Write-Output "Packaged Desktop guardian: $($files[0].Length) bytes. Not signed or runtime-tested."
} finally { Pop-Location }
