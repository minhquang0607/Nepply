param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$root = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
$relative = [IO.Path]::GetRelativePath($repo,$root)
& (Join-Path $PSScriptRoot 'Test-ReleaseLayout.ps1')
# Sequential: both publish modes share obj; never build one project concurrently.
& (Join-Path $PSScriptRoot 'Build-Distribution.ps1') -OutputDirectory (Join-Path $relative '02-installer') -InternalOnly
& (Join-Path $PSScriptRoot 'Build-Msix.ps1') -OutputDirectory (Join-Path $relative '03-msix-store') -InternalOnly
& (Join-Path $PSScriptRoot 'Build-SourceBundle.ps1') -OutputDirectory (Join-Path $relative '01-source')
Copy-Item -LiteralPath (Join-Path $repo 'packaging/README.md') -Destination (Join-Path $root 'README.md')
Write-Output "Prepared 3 outputs in $root. Internal only: not signed, installed, submitted or approved for public release."
