param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$version = Get-NepplyReleaseVersion -Repository $repo
$output = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
Push-Location $repo
try {
    & dotnet restore src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore Desktop guardian thất bại.' }
    & dotnet publish src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj -c Release --no-restore `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false `
        "-p:Version=$($version.Version)" "-p:FileVersion=$($version.FileVersion)" -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Publish Desktop guardian thất bại.' }
    $files = @(Get-ChildItem -LiteralPath $output -Force)
    if ($files.Count -ne 1 -or $files[0].Name -cne 'Nepply.DesktopGuardian.exe' -or $files[0].PSIsContainer -or
        ($files[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -or $files[0].Length -le 0) {
        throw 'Guardian phải có đúng một executable self-contained; không phân phối payload thiếu hoặc ngoài dự kiến.'
    }
    if ($files[0].VersionInfo.ProductVersion.Split('+')[0] -cne $version.Version -or
        $files[0].VersionInfo.FileVersion -cne $version.FileVersion) { throw 'Version guardian không khớp app.' }
    Write-Output "Đã đóng gói Desktop guardian: $($files[0].Length) byte. Chưa ký hoặc kiểm thử runtime."
} finally { Pop-Location }
