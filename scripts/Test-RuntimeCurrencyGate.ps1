#requires -Version 7.4
# Offline checks for Runtime-Currency.ps1. The live feed is read by Verify-RuntimeCurrency.ps1.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Runtime-Currency.ps1')
function New-Release([string]$Version, [bool]$Security, [string[]]$Cves = @()) {
    @{ 'release-version'=$Version; 'release-date'='2026-01-01'; security=$Security
       'cve-list'=@($Cves | ForEach-Object { @{ 'cve-id'=$_ } })
       runtime=@{ version=$Version }; windowsdesktop=@{ version=$Version } }
}
function New-Feed {
    @{ 'channel-version'='10.0'; 'support-phase'='active'; releases=@(
        (New-Release '10.0.14' $true @('CVE-1')), (New-Release '10.0.13' $false), (New-Release '10.0.12' $true @('CVE-0')),
        (New-Release '10.0.0-rc.2.1' $false)) }
}
$checks = 0
function Pass([string]$Name) { Write-Output "PASS runtime gate $Name"; $script:checks++ }

if ((Assert-RuntimeCurrency -Feed (New-Feed) -Pinned '10.0.14') -notmatch 'latest') { throw 'FAIL latest pin not reported as latest' }
Pass 'accepts the latest release'

$feed = New-Feed; $feed.releases[0].security = $false
if ((Assert-RuntimeCurrency -Feed $feed -Pinned '10.0.12') -notmatch 'without security fixes') { throw 'FAIL non-security update blocked' }
Pass 'allows newer releases without security fixes'

$message = $null
try { Assert-RuntimeCurrency -Feed (New-Feed) -Pinned '10.0.12' } catch { $message = $_.Exception.Message }
if ($message -notmatch '10\.0\.14' -or $message -notmatch 'CVE-1') { throw "FAIL missed security release not named: $message" }
Pass 'rejects a pin behind a security release and names it'

foreach ($scenario in @('unknown-pin','inexact-pin','other-channel','eol','no-releases','desktop-mismatch','missing-security-flag','malformed-entry')) {
    $feed = New-Feed; $pin = '10.0.14'
    switch ($scenario) {
        'unknown-pin' { $pin = '10.0.15' }
        'inexact-pin' { $pin = '10.0' }
        'other-channel' { $feed['channel-version'] = '9.0' }
        'eol' { $feed['support-phase'] = 'eol' }
        'no-releases' { $feed.releases = @() }
        'desktop-mismatch' { $feed.releases[0].windowsdesktop.version = '10.0.13' }
        'missing-security-flag' { $feed.releases[0].Remove('security') }
        'malformed-entry' { $feed.releases += 'text' }
    }
    $rejected = $false
    try { Assert-RuntimeCurrency -Feed $feed -Pinned $pin | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "FAIL runtime gate accepted $scenario" }
    Pass "rejects $scenario"
}

$pin = Get-NepplyRuntimePin -Repository (Split-Path $PSScriptRoot -Parent)
if ($pin -notmatch '^10\.0\.\d+$') { throw "FAIL repository pin '$pin' is not a .NET 10 runtime" }
Pass 'reads the repository pin'
Write-Output "RESULT: $checks runtime currency fixture checks passed. The live feed is checked by Verify-RuntimeCurrency.ps1."
