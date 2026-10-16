param(
    [Parameter(Mandatory)][string]$Application,
    [Parameter(Mandatory)][string]$Installer,
    [switch]$RequireSigned,
    [switch]$RequireStable,
    [string]$ExpectedVersion,
    [string[]]$AllowedSignerThumbprints,
    [string]$Uninstaller
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-ReleaseMetadata.ps1')
if ($RequireSigned -or $RequireStable) {
    Assert-SignerPolicy -AllowedSignerThumbprints $AllowedSignerThumbprints
    if ([string]::IsNullOrWhiteSpace($Uninstaller)) { throw 'Signed release also requires the uninstaller from an isolated installation of this exact Setup.' }
}
$releaseDirectory = Split-Path ([IO.Path]::GetFullPath($Application)) -Parent
& (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $releaseDirectory
if ($RequireSigned -or $RequireStable) {
    # The release manifest already rejects links throughout this payload.
    # Check notice completeness separately: hashing an incomplete bundle is not enough.
    . (Join-Path $PSScriptRoot 'Test-NoticeInventory.ps1')
    $sourceRoot = Split-Path $PSScriptRoot -Parent
    $graph = Get-Content -LiteralPath (Join-Path $sourceRoot 'src/Nepply/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $target = $graph.targets['net10.0-windows10.0.19041.0/win-x64']
    if (-not $target) { throw 'Restore the release Windows x64 dependency graph first.' }
    [xml]$props = Get-Content -LiteralPath (Join-Path $sourceRoot 'Directory.Build.props') -Raw
    $runtime = [string]$props.Project.PropertyGroup.NepplyRuntimeVersion
    $expectedPackages = @($target.Keys | Where-Object {
        $item = $target[$_]
        @($item.runtime.Keys) + @($item.native.Keys) | Where-Object { $_ -and $_ -notmatch '/_\._$' }
    }) + @("Microsoft.NETCore.App.Runtime.win-x64/$runtime", "Microsoft.WindowsDesktop.App.Runtime.win-x64/$runtime")
    $noticeRoot = Join-Path $releaseDirectory 'licenses'
    . (Join-Path $PSScriptRoot 'Get-WindowsSdkNoticePackage.ps1')
    $expectedPackages += Get-WindowsSdkNoticePackage $graph
    $inventoryFile = Get-Item -LiteralPath (Join-Path $noticeRoot 'inventory.json')
    if ($inventoryFile.Length -gt 8MB) { throw 'Notice inventory exceeds size limit.' }
    $inventory = Get-Content -LiteralPath $inventoryFile.FullName -Raw | ConvertFrom-Json -AsHashtable
    Assert-NoticeInventory -Inventory $inventory -ExpectedPackages ($expectedPackages | Sort-Object -Unique) -RuntimeVersion $runtime
    foreach ($package in $inventory.Packages) {
        $packageDirectory = Join-Path $noticeRoot ($package.Package.ToLowerInvariant() -replace '/', '-')
        foreach ($notice in $package.NoticeEvidence) {
            $noticeFile = Get-Item -LiteralPath (Join-Path $packageDirectory $notice.File)
            if ($noticeFile.PSIsContainer -or $noticeFile.Length -ne $notice.Bytes -or
                (Get-FileHash -LiteralPath $noticeFile.FullName -Algorithm SHA256).Hash -ine $notice.SHA256) {
                throw "Notice content mismatch: $($package.Package)/$($notice.File)"
            }
        }
    }
    Write-Output 'PASS: notice inventory covers the restored graph and its files match. Legal/source-obligation review is still required.'
}
if (-not $ExpectedVersion) {
    [xml]$project=Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Nepply/Nepply.csproj') -Raw
    $ExpectedVersion=[string]$project.Project.PropertyGroup.Version
}
$artifacts=[Collections.Generic.List[object]]::new()
foreach ($artifact in @($Application, $Installer)) {
    $file = Get-Item -LiteralPath $artifact
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
    $artifacts.Add([pscustomobject]@{
        File = $file.Name
        Bytes = $file.Length
        Version = $file.VersionInfo.ProductVersion
        SHA256 = $hash.Hash
        Signature = $signature.Status
        SignerThumbprint = $signature.SignerCertificate.Thumbprint
        TimestampThumbprint = $signature.TimeStamperCertificate.Thumbprint
    })
}
$artifacts.ToArray()
Assert-ReleaseMetadata -Artifacts $artifacts.ToArray() -ExpectedVersion $ExpectedVersion -RequireSigned:$RequireSigned -RequireStable:$RequireStable -AllowedSignerThumbprints $AllowedSignerThumbprints
$guardian = Get-Item -LiteralPath (Join-Path $releaseDirectory 'guardian/Nepply.DesktopGuardian.exe')
if ($guardian.PSIsContainer -or $guardian.Length -le 0 -or
    $guardian.VersionInfo.ProductVersion.Split('+')[0] -cne $ExpectedVersion) { throw 'Desktop guardian thiếu hoặc không cùng phiên bản app.' }
if ($RequireSigned -or $RequireStable) {
    $signature = Get-AuthenticodeSignature -LiteralPath $guardian.FullName
    Assert-ArtifactSigner -Artifact ([pscustomobject]@{
        File=$guardian.Name; Signature=$signature.Status; SignerThumbprint=$signature.SignerCertificate.Thumbprint
        TimestampThumbprint=$signature.TimeStamperCertificate.Thumbprint
    }) -AllowedSignerThumbprints $AllowedSignerThumbprints
}
if ($RequireSigned -or $RequireStable) {
    $file=Get-Item -LiteralPath $Uninstaller
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Uninstaller must be a regular file.' }
    $signature=Get-AuthenticodeSignature -LiteralPath $file.FullName
    $uninstallerMetadata=[pscustomobject]@{
        File=$file.Name; Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        Signature=$signature.Status; SignerThumbprint=$signature.SignerCertificate.Thumbprint; TimestampThumbprint=$signature.TimeStamperCertificate.Thumbprint
    }
    Assert-ArtifactSigner -Artifact $uninstallerMetadata -AllowedSignerThumbprints $AllowedSignerThumbprints
    $uninstallerMetadata
    Write-Output 'Signer policy passed. Confirm the supplied uninstaller was produced by this exact Setup; metadata alone cannot establish that provenance.'
}
