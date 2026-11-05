# Removes CodeQL results located in generated sources from a SARIF file before upload.
# paths-ignore only applies when a compiled language is analyzed without a build, and
# query-filters select rules rather than files, so with build-mode: manual the WPF
# *.g.cs files under obj/ can only be dropped from the results. Results of a rule with
# a security-severity are always kept, wherever they are.
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
$resolved=(Resolve-Path -LiteralPath $Path).Path
$root=[System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($resolved))
if($null -eq $root['runs']) {throw "No SARIF runs in $resolved"}
$generated='(^|/)obj/|\.g(\.i)?\.cs$'
$total=0
$removed=0
foreach($run in $root['runs']) {
    $security=[Collections.Generic.HashSet[string]]::new()
    $components=[Collections.Generic.List[object]]::new()
    $components.Add($run['tool']['driver'])
    if($null -ne $run['tool']['extensions']) { foreach($extension in $run['tool']['extensions']) {$components.Add($extension)} }
    foreach($component in $components) {
        if($null -eq $component['rules']) {continue}
        foreach($rule in $component['rules']) {
            $properties=$rule['properties']
            if($null -ne $properties -and $null -ne $properties['security-severity']) {$null=$security.Add($rule['id'].ToString())}
        }
    }
    $results=$run['results']
    if($null -eq $results) {continue}
    $total+=$results.Count
    for($i=$results.Count-1;$i -ge 0;$i--) {
        $result=$results[$i]
        $ruleId=if($null -ne $result['ruleId']) {$result['ruleId'].ToString()} elseif($null -ne $result['rule']) {$result['rule']['id'].ToString()} else {''}
        if($security.Contains($ruleId)) {continue}
        $locations=$result['locations']
        if($null -eq $locations -or $locations.Count -eq 0) {continue}
        $uri=$locations[0]['physicalLocation']?['artifactLocation']?['uri']
        if($null -ne $uri -and $uri.ToString() -match $generated) {
            $results.RemoveAt($i)
            $removed++
        }
    }
}
[IO.File]::WriteAllText($resolved,$root.ToJsonString(),[Text.UTF8Encoding]::new($false))
Write-Output "Removed $removed of $total CodeQL results located in generated sources."
