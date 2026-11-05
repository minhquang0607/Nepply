$ErrorActionPreference='Stop'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('Nepply-sarif-test-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $fixture
$script=Join-Path $PSScriptRoot 'Filter-CodeQlSarif.ps1'
$sarif=Join-Path $fixture 'csharp.sarif'
$checks=0
function Check([bool]$Condition,[string]$Name) {
    if(-not $Condition) {throw "FAIL: $Name"}
    Write-Output "PASS: $Name"
    $script:checks++
}
function Result([string]$RuleId,[string]$Uri) {
    @{ruleId=$RuleId;message=@{text=$Uri};locations=@(@{physicalLocation=@{artifactLocation=@{uri=$Uri;uriBaseId='%SRCROOT%'}}})}
}
try {
    $wpf='src/Nepply/obj/Release/net10.0-windows10.0.19041.0/win-x64/App.g.cs'
    $quality='cs/useless-assignment-to-local'
    $document=@{
        version='2.1.0'
        runs=@(
            @{
                tool=@{
                    driver=@{name='CodeQL';rules=@()}
                    extensions=@(@{name='codeql/csharp-queries';rules=@(
                        @{id=$quality;properties=@{tags=@('quality')}}
                        @{id='cs/path-injection';properties=@{tags=@('security');'security-severity'='7.5'}}
                    )})
                }
                properties=@{timestamp='2026-11-05T01:02:03Z';note='Thông báo 日本語'}
                results=@(
                    (Result $quality $wpf)
                    (Result $quality 'src/Nepply/Generated/Other.g.cs')
                    (Result $quality 'src/Nepply/Other.g.i.cs')
                    (Result $quality 'src/Nepply/OBJ/Upper.cs')
                    (Result $quality 'src/Nepply/App.xaml.cs')
                    (Result $quality 'src/Nepply/object/Thing.cs')
                    (Result $quality 'src/Nepply/Thing.gcs')
                    (Result $quality 'src/Nepply/Features/Đổi tên.cs')
                    (Result 'cs/path-injection' $wpf)
                    @{ruleId=$quality;message=@{text='no location'}}
                )
            }
            @{tool=@{driver=@{name='CodeQL'}};results=@((Result 'actions/x' '.github/workflows/verify.yml'),(Result 'actions/x' 'obj/Second.cs'))}
            @{tool=@{driver=@{name='CodeQL'}}}
        )
    }
    [IO.File]::WriteAllText($sarif,($document | ConvertTo-Json -Depth 20))
    $summary=& $script -Path $sarif
    Check ($summary -eq 'Removed 5 of 12 CodeQL results located in generated sources.') "Reports what it removed: $summary"
    $after=[System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($sarif))
    $kept=[Collections.Generic.List[string]]::new()
    foreach($result in $after['runs'][0]['results']) {
        $kept.Add($result['ruleId'].ToString()+'|'+$result['message']['text'].ToString())
    }
    $expected=@(
        "$quality|src/Nepply/App.xaml.cs"
        "$quality|src/Nepply/object/Thing.cs"
        "$quality|src/Nepply/Thing.gcs"
        "$quality|src/Nepply/Features/Đổi tên.cs"
        "cs/path-injection|$wpf"
        "$quality|no location"
    )
    Check (($kept -join "`n") -ceq ($expected -join "`n")) 'Keeps source files and security results, drops generated ones'
    Check ($after['runs'][1]['results'].Count -eq 1) 'Filters every run'
    Check ($null -eq $after['runs'][2]['results']) 'Accepts a run without results'
    Check ($after['runs'][0]['properties']['timestamp'].ToString() -ceq '2026-11-05T01:02:03Z') 'Leaves timestamps as written'
    Check ($after['runs'][0]['properties']['note'].ToString() -ceq 'Thông báo 日本語') 'Keeps Unicode text'
    Check ($after['runs'][0]['tool']['extensions'][0]['rules'].Count -eq 2) 'Keeps rule metadata'
    Check ([IO.File]::ReadAllBytes($sarif)[0] -ne 0xEF) 'Writes no byte order mark'
    $unchanged=& $script -Path $sarif
    Check ($unchanged -eq 'Removed 0 of 7 CodeQL results located in generated sources.') 'Running again removes nothing'
    foreach($bad in @(
        @{Name='missing file';Path=(Join-Path $fixture 'missing.sarif');Content=$null}
        @{Name='invalid JSON';Path=(Join-Path $fixture 'broken.sarif');Content='{'}
        @{Name='no runs';Path=(Join-Path $fixture 'empty.sarif');Content='{}'}
    )) {
        if($null -ne $bad.Content) {[IO.File]::WriteAllText($bad.Path,$bad.Content)}
        $rejected=$false
        try { & $script -Path $bad.Path | Out-Null } catch { $rejected=$true }
        Check $rejected "Rejects $($bad.Name)"
    }
    Write-Output "CodeQL SARIF filter checks passed ($checks)."
} finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
