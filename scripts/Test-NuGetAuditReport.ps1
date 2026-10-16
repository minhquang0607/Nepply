function Assert-NuGetAuditReport {
    param([Parameter(Mandatory)][string]$Json, [Parameter(Mandatory)][string]$ExpectedProject)
    $report = $Json | ConvertFrom-Json -AsHashtable -ErrorAction Stop
    if ($report -isnot [System.Collections.IDictionary] -or $report.version -ne 1) { throw 'Unsupported NuGet audit report.' }
    if ($report.parameters -notmatch '(^|\s)--vulnerable(\s|$)' -or $report.parameters -notmatch '(^|\s)--include-transitive(\s|$)') {
        throw 'Audit must include vulnerable direct and transitive packages.'
    }
    if (@($report.sources).Count -eq 0) { throw 'Audit has no reported sources.' }
    if ($report.logs) { throw 'NuGet reported diagnostics; audit completeness must be reviewed.' }
    $projects = @($report.projects)
    if ($projects.Count -ne 1 -or -not $projects[0].path) { throw 'Audit must contain exactly the requested project.' }
    if (-not [IO.Path]::GetFullPath($projects[0].path).Equals([IO.Path]::GetFullPath($ExpectedProject),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Audit project differs from the requested project.'
    }
    $findings = [Collections.Generic.List[string]]::new()
    foreach ($project in $projects) {
        if ($project.logs) { throw 'Project audit reported diagnostics.' }
        foreach ($framework in @($project.frameworks)) {
            if ($null -eq $framework) { continue }
            if (-not $framework.framework -or $framework.logs) { throw 'Invalid or incomplete framework audit.' }
            foreach ($kind in @('topLevelPackages','transitivePackages')) {
                foreach ($package in @($framework[$kind])) {
                    if ($null -eq $package) { continue }
                    if (-not $package.id -or -not $package.resolvedVersion) { throw 'Malformed audited package.' }
                    foreach ($finding in @($package.vulnerabilities)) {
                        if ($null -eq $finding) { continue }
                        if (-not $finding.severity -or -not $finding.advisoryurl) { throw 'Malformed vulnerability finding.' }
                        $findings.Add("$($package.id) $($package.resolvedVersion): $($finding.severity) $($finding.advisoryurl)")
                    }
                }
            }
        }
    }
    if ($findings.Count -gt 0) { throw ("Known vulnerable dependencies block packaging:`n" + ($findings -join "`n")) }
}
