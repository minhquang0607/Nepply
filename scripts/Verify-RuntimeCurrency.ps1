#requires -Version 7.4
# Fails when Microsoft has shipped a .NET security release newer than the runtime Nepply
# bundles. Needs the network unless -FeedPath points at a saved releases.json.
param([string]$FeedPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Runtime-Currency.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$pinned = Get-NepplyRuntimePin -Repository $repo
if ($FeedPath) { $json = Get-Content -LiteralPath $FeedPath -Raw }
else {
    $channel = ([version]$pinned).ToString(2)
    $uri = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$channel/releases.json"
    $json = (Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 60 -MaximumRetryCount 3 -RetryIntervalSec 10).Content
}
$feed = $json | ConvertFrom-Json -AsHashtable -ErrorAction Stop
Write-Output ('PASS: ' + (Assert-RuntimeCurrency -Feed $feed -Pinned $pinned))
