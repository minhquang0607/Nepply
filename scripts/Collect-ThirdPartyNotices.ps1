param(
    [string]$Assets = 'src/Nepply/obj/project.assets.json',
    [string]$Destination = 'artifacts/release/licenses',
    [switch]$RequireComplete
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$assetsPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Assets))
$outputPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $Destination))
if (-not $outputPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Notice output must stay inside the project.' }
$graph = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
$target = $graph.targets['net10.0-windows10.0.19041.0/win-x64']
if (-not $target) { throw 'Restore the Windows x64 target first.' }
. (Join-Path $PSScriptRoot 'Test-ExcludedPackages.ps1')
Assert-NepplyExcludedPackages -Graph $graph
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$entries = [Collections.Generic.List[object]]::new()
$packageKeys = @($target.Keys | Where-Object {
    $item = $target[$_]
    @($item.runtime.Keys) + @($item.native.Keys) | Where-Object { $_ -and $_ -notmatch '/_\._$' }
})
[xml]$props = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
$runtimeVersion = [string]$props.Project.PropertyGroup.NepplyRuntimeVersion
if ($runtimeVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Runtime version must be explicit.' }
$packageKeys += "Microsoft.NETCore.App.Runtime.win-x64/$runtimeVersion", "Microsoft.WindowsDesktop.App.Runtime.win-x64/$runtimeVersion"
. (Join-Path $PSScriptRoot 'Get-WindowsSdkNoticePackage.ps1')
$packageKeys += Get-WindowsSdkNoticePackage $graph
foreach ($packageKey in ($packageKeys | Sort-Object -Unique)) {
    $relative = $packageKey.ToLowerInvariant()
    $packageDir = $null
    foreach ($cacheRoot in $graph.packageFolders.Keys) {
        $candidate = Join-Path $cacheRoot $relative
        if (Test-Path -LiteralPath $candidate) { $packageDir = $candidate; break }
    }
    if (-not $packageDir) { throw "Missing restored package: $packageKey" }
    $nuspec = Get-ChildItem -LiteralPath $packageDir -Filter *.nuspec | Select-Object -First 1
    [xml]$metadataXml = Get-Content -LiteralPath $nuspec.FullName -Raw
    $metadata = $metadataXml.package.metadata
    $licenseNode = $metadata.SelectSingleNode('*[local-name()="license"]')
    $license = if ($licenseNode) { $licenseNode.InnerText } else { '' }
    $repository = $metadata.SelectSingleNode('*[local-name()="repository"]')
    $source = if ($repository) { $repository.GetAttribute('url') -replace '^git://','https://' } else { '' }
    $commit = if ($repository) { $repository.GetAttribute('commit') } else { '' }
    if ($source -match '^https://github.com/' -and $commit -match '^[a-fA-F0-9]{40}$') { $source = $source.TrimEnd('/') + '/tree/' + $commit }
    $packageOutput = Join-Path $outputPath ($relative -replace '/', '-')
    New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
    $copied = [Collections.Generic.List[string]]::new()
    $hasLicense = $false
    # NuGet may declare a license file below the package root. Do not substitute
    # an expression or URL for the actual redistribution text.
    if ($licenseNode -and $licenseNode.GetAttribute('type') -eq 'file') {
        if ([string]::IsNullOrWhiteSpace($license) -or [IO.Path]::IsPathRooted($license)) { throw "Invalid declared license path: $packageKey" }
        $declaredPath = [IO.Path]::GetFullPath((Join-Path $packageDir $license))
        $packagePrefix = [IO.Path]::GetFullPath($packageDir).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
        if (-not $declaredPath.StartsWith($packagePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw "License path leaves package directory: $packageKey" }
        if (-not (Test-Path -LiteralPath $declaredPath -PathType Leaf)) { throw "Declared license file is missing: $packageKey" }
        Copy-Item -LiteralPath $declaredPath -Destination (Join-Path $packageOutput 'DECLARED-LICENSE.txt') -Force
        $copied.Add('DECLARED-LICENSE.txt'); $hasLicense = $true
    }
    foreach ($notice in (Get-ChildItem -LiteralPath $packageDir -File | Where-Object Name -Match '^(license|copying|third.party.notices|notice)(\..*)?$')) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $packageOutput $notice.Name) -Force
        $copied.Add($notice.Name)
        if ($notice.Name -match '^(license|copying)') { $hasLicense = $true }
    }
    if ($license -eq 'MPL-2.0') {
        Copy-Item -LiteralPath (Join-Path $projectRoot 'src/Nepply/Resources/Licenses/MPL-2.0.txt') -Destination (Join-Path $packageOutput 'MPL-2.0.txt') -Force
        $copied.Add('MPL-2.0.txt'); $hasLicense = $true
    }
    if ($packageKey -in @('System.IO.Ports/10.0.3','System.Management/10.0.2')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot 'src/Nepply/Resources/Licenses/DotNet-MIT.txt') -Destination (Join-Path $packageOutput 'LICENSE.TXT') -Force
        $copied.Add('LICENSE.TXT'); $hasLicense = $true
    }
    $isWindowsSdk = $packageKey -eq 'Microsoft.Windows.SDK.NET.Ref/10.0.19041.57'
    if ($isWindowsSdk) {
        $sdkLicense = Join-Path $projectRoot 'src/Nepply/Resources/Licenses/Windows-SDK-License.rtf'
        if ((Get-FileHash -LiteralPath $sdkLicense -Algorithm SHA256).Hash -cne 'DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62') { throw 'Windows SDK license evidence changed; review required.' }
        foreach ($name in @('Windows-SDK-License.rtf','Windows-SDK-PROVENANCE.md')) {
            Copy-Item -LiteralPath (Join-Path $projectRoot ('src/Nepply/Resources/Licenses/'+$name)) -Destination (Join-Path $packageOutput $name) -Force
            $copied.Add($name)
        }
        $license = 'Microsoft Windows SDK license terms (WIN10SDK.RTM.AUG_2018_en-US)'
        $hasLicense = $true
    }
    $assetEvidence = [Collections.Generic.List[object]]::new()
    $packageAssets = $target[$packageKey]
    $resolvedAssets = if ($isWindowsSdk) { @('lib/net8.0/Microsoft.Windows.SDK.NET.dll','lib/net8.0/WinRT.Runtime.dll') }
        else { @($packageAssets.runtime.Keys) + @($packageAssets.native.Keys) | Where-Object { $_ -and $_ -notmatch '/_\._$' } | Sort-Object -Unique }
    if ($packageAssets -or $isWindowsSdk) {
        foreach ($asset in $resolvedAssets) {
            $assetPath = [IO.Path]::GetFullPath((Join-Path $packageDir $asset))
            if (-not $assetPath.StartsWith([IO.Path]::GetFullPath($packageDir).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
                throw "Asset path leaves package directory: $packageKey"
            }
            $file = Get-Item -LiteralPath $assetPath
            $assetEvidence.Add([ordered]@{ Asset=$asset; Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash })
        }
    }
    $licenseUrlNode = $metadata.SelectSingleNode('*[local-name()="licenseUrl"]')
    $projectUrlNode = $metadata.SelectSingleNode('*[local-name()="projectUrl"]')
    $copyrightNode = $metadata.SelectSingleNode('*[local-name()="copyright"]')
    $entries.Add([ordered]@{
        Package=$packageKey; License=$license;
        DeclaredLicenseUrl=$(if($licenseUrlNode){$licenseUrlNode.InnerText}else{''});
        DeclaredProjectUrl=$(if($projectUrlNode){$projectUrlNode.InnerText}else{''});
        Copyright=$(if($copyrightNode){$copyrightNode.InnerText}else{''});
        Source=$source; RepositoryCommit=$commit; HasPinnedSource=($commit -match '^[a-fA-F0-9]{40}$');
        PackageSha512=$graph.libraries[$packageKey].sha512;
        ResolvedAssets=@($assetEvidence.ToArray());
        AssetScope=$(if($isWindowsSdk){'Two net8.0 projection DLLs supplied by the Windows SDK pack'}elseif($packageAssets){'Resolved runtime/native assets for Windows x64'}else{'SDK runtime pack: asset-level inventory not included'});
        Files=@($copied | Sort-Object -Unique);
        NoticeEvidence=@($copied | Sort-Object -Unique | ForEach-Object {
            $noticePath = Join-Path $packageOutput $_
            [ordered]@{ File=$_; Bytes=(Get-Item -LiteralPath $noticePath).Length; SHA256=(Get-FileHash -LiteralPath $noticePath -Algorithm SHA256).Hash }
        });
        MissingLicenseText=(-not $hasLicense)
    })
}
$manifest = [ordered]@{ Target='net10.0-windows10.0.19041.0/win-x64'; RuntimeVersion=$runtimeVersion; Scope='Runtime/native assets in resolved target plus self-contained runtime packs. Source-notice review still required; not legal approval.'; Packages=@($entries.ToArray()) }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputPath 'inventory.json') -Encoding utf8
$missing = @($entries | Where-Object MissingLicenseText)
Write-Output "Collected $($entries.Count) runtime packages; $($missing.Count) missing license texts."
$missing | ForEach-Object { Write-Warning "Missing license text: $($_.Package)" }
if ($RequireComplete -and $missing.Count -gt 0) { throw 'Notice gate failed: missing license texts.' }
