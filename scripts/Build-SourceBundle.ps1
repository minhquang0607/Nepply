param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$version = Get-NepplyReleaseVersion -Repository $repo
$output = New-NepplyArtifactDirectory -Repository $repo -Path $OutputDirectory
$source = Join-Path $output 'Nepply-source'
New-Item -ItemType Directory -Path $source | Out-Null
# Includes all app/test/tool/build sources; never copies reference material or build caches.
$entries = @('src','tests','tools','scripts','packaging','.github','docs',
    '.gitattributes','.gitignore','CHANGELOG.md','CONTRIBUTING.md','Directory.Build.props','Directory.Build.targets','global.json','Nepply.slnx','README.md')
if (Test-Path -LiteralPath (Join-Path $repo 'LICENSE')) { $entries += 'LICENSE' }
foreach ($entry in $entries) {
    Copy-NepplySourceTree -Source (Join-Path $repo $entry) -Destination (Join-Path $source $entry)
}
& (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $source -Mode Create
& (Join-Path $PSScriptRoot 'Release-Manifest.ps1') -Directory $source -Mode Verify
$zip = Join-Path $output "Nepply-$($version.Version)-source-INTERNAL.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($source,$zip)
$baseline = Get-Content -LiteralPath (Join-Path $source 'release-manifest.json') -Raw | ConvertFrom-Json
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $files = @($archive.Entries | Where-Object { $_.Name.Length -gt 0 })
    if ($files.Count -ne $baseline.Files.Count + 1) { throw 'Source ZIP has missing or extra files.' }
    foreach ($entry in $baseline.Files) {
        $compressed = $archive.GetEntry($entry.Path)
        if ($null -eq $compressed -or $compressed.Length -ne $entry.Bytes) { throw "Source ZIP sai entry: $($entry.Path)" }
        $stream = $compressed.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -cne $entry.SHA256) { throw "Source ZIP sai hash: $($entry.Path)" }
    }
} finally { $archive.Dispose() }
$info = [ordered]@{
    Version=$version.Version; SourceZip=(Split-Path $zip -Leaf)
    SHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    ZipPayloadVerified=$true
    LicenseDecision='Not approved for public release; a LICENSE file does not replace a distribution-rights review.'
    Scope='Full source with historical documents; no .git/bin/obj/artifacts. Personal-data review not completed; never uploaded automatically.'
}
$info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-bundle.json') -Encoding utf8
Write-Output "Created internal source bundle: $zip"
