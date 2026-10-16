$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-NuGetAuditReport.ps1')
$projectPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/Nepply/Nepply.csproj'
$base = @{ version=1; parameters='--vulnerable --include-transitive'; sources=@('https://api.nuget.org/v3/index.json'); projects=@(@{path=$projectPath}) }
Assert-NuGetAuditReport -Json ($base | ConvertTo-Json -Depth 10) -ExpectedProject $projectPath
$count=1
foreach ($scenario in @('direct','transitive','warning','project-warning','wrong-project','wrong-version','missing-transitive','invalid-json')) {
    $fixture = ($base | ConvertTo-Json -Depth 10) | ConvertFrom-Json -AsHashtable
    switch ($scenario) {
        'direct' { $fixture.projects[0].frameworks=@(@{framework='net10.0-windows';topLevelPackages=@(@{id='Fixture';resolvedVersion='1.0.0';vulnerabilities=@(@{severity='Low';advisoryurl='https://example.invalid/advisory'})})}) }
        'transitive' { $fixture.projects[0].frameworks=@(@{framework='net10.0-windows';transitivePackages=@(@{id='Fixture';resolvedVersion='1.0.0';vulnerabilities=@(@{severity='Critical';advisoryurl='https://example.invalid/advisory'})})}) }
        'warning' { $fixture.logs=@(@{level='Warning';message='Audit feed unavailable'}) }
        'project-warning' { $fixture.projects[0].logs=@(@{level='Error';message='Unable to audit'}) }
        'wrong-project' { $fixture.projects[0].path=Join-Path (Split-Path $projectPath) 'Other.csproj' }
        'wrong-version' { $fixture.version=99 }
        'missing-transitive' { $fixture.parameters='--vulnerable' }
    }
    $json=if ($scenario -eq 'invalid-json') { '{broken' } else { $fixture | ConvertTo-Json -Depth 10 }
    $rejected=$false
    try { Assert-NuGetAuditReport -Json $json -ExpectedProject $projectPath } catch { $rejected=$true }
    if (-not $rejected) { throw "FAIL: dependency gate accepted $scenario" }
    Write-Output "PASS: dependency gate rejects $scenario"
    $count++
}
Write-Output "RESULT: $count dependency gate checks passed."
