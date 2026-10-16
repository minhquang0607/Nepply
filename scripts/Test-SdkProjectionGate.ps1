$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-SdkProjectionPayload.ps1')
. (Join-Path $PSScriptRoot 'Get-WindowsSdkNoticePackage.ps1')
$repo=Split-Path $PSScriptRoot -Parent
$graph=Get-Content -LiteralPath (Join-Path $repo 'src/Nepply/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$sdkKey=Get-WindowsSdkNoticePackage $graph
$pack=@($graph.packageFolders.Keys | ForEach-Object { Join-Path $_ $sdkKey.ToLowerInvariant() } | Where-Object { Test-Path -LiteralPath $_ -PathType Container }) | Select-Object -First 1
if (-not $pack) { throw 'Restore app first.' }
$root=Join-Path $repo ('artifacts/sdk-projection-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
function Reject([string]$Name) {
    $rejected=$false
    try { Assert-SdkProjectionPayload -Directory $root -PackageDirectory $pack } catch { $rejected=$true }
    if (-not $rejected) { throw "FAIL $Name" }
    Write-Output "PASS $Name"
}
Reject 'Rejects empty payload'
Copy-Item -LiteralPath (Join-Path $pack 'lib/net8.0/Microsoft.Windows.SDK.NET.dll') -Destination $root
Reject 'Rejects missing WinRT.Runtime'
Copy-Item -LiteralPath (Join-Path $pack 'lib/net8.0/WinRT.Runtime.dll') -Destination $root
Assert-SdkProjectionPayload -Directory $root -PackageDirectory $pack
Write-Output 'PASS accepts exact restored DLLs'
# Corrupt only this new fixture with another known DLL, never the restored pack.
Copy-Item -LiteralPath (Join-Path $pack 'lib/net8.0/Microsoft.Windows.SDK.NET.dll') -Destination (Join-Path $root 'WinRT.Runtime.dll') -Force
Reject 'Rejects changed WinRT.Runtime'
Write-Output 'RESULT: 4 SDK payload checks passed; fixture retained under artifacts.'
