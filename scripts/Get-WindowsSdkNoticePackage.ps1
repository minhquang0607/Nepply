function Get-WindowsSdkNoticePackage([System.Collections.IDictionary]$Graph) {
    $pins = @($Graph.project.frameworks.Values | ForEach-Object { $_.downloadDependencies } |
        Where-Object name -EQ 'Microsoft.Windows.SDK.NET.Ref' | Select-Object -ExpandProperty version -Unique)
    if ($pins.Count -ne 1 -or $pins[0] -notmatch '^\[(\d+\.\d+\.\d+\.\d+), \1\]$') {
        throw 'Windows SDK projection notice requires exactly one pinned framework pack.'
    }
    return 'Microsoft.Windows.SDK.NET.Ref/' + $Matches[1]
}
