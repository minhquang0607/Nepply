param([string]$Report='artifacts/public-source-review.json')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$output=[IO.Path]::GetFullPath((Join-Path $repo $Report))
if(!$output.StartsWith((Join-Path $repo 'artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Report must stay in artifacts.'}
$issues=[Collections.Generic.List[object]]::new()
Push-Location $repo
try {
    $paths=@(git -c core.quotePath=false ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    if($LASTEXITCODE -ne 0 -or !$paths.Count){throw 'Cannot enumerate Git candidate files.'}
    foreach($path in $paths){
        $file=Get-Item -LiteralPath $path -Force
        if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Source link requires review: $path"}
        $kind=$null
        if($path -match '(?i)(^|/)(bin|obj|artifacts|publish|TestResults|\.vs)/|\.(pfx|p12|key|pem|dmp|log|exe|dll|zip|msix)$|(^|/)(\.env(\..+)?|\.npmrc|\.pypirc|\.netrc|credentials\.json)$'){$kind='private-or-generated-file'}
        if($file.Length -gt 5MB){$kind='large-file-needs-review'}
        if($kind){$issues.Add(@{Path=$path;Line=0;Kind=$kind});continue}
        if($file.Extension -notin @('.cs','.ps1','.md','.txt','.json','.xml','.xaml','.yml','.yaml','.props','.targets','.iss','.config','.csproj') -and $file.Name -notin @('LICENSE','.gitignore','.gitattributes')){continue}
        $lines=[IO.File]::ReadAllLines($file.FullName)
        for($n=0;$n -lt $lines.Length;$n++){
            $kind=$null
            if($lines[$n] -match 'gh[pousr]_[A-Za-z0-9]{25,}|github_pat_[A-Za-z0-9_]{25,}|AKIA[A-Z0-9]{16}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|xox[baprs]-[A-Za-z0-9-]{20,}'){$kind='credential-pattern'}
            elseif($lines[$n] -match '(?i)[A-Z]:[\\/](Users|dự án)[\\/]|MINHQU[~]'){$kind='personal-machine-path'}
            if($kind){$issues.Add(@{Path=$path;Line=$n+1;Kind=$kind})}
        }
    }
    $parent=Split-Path $output -Parent
    $probe=$parent
    while($probe -and $probe.Length -ge $repo.Length){
        if((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Report ancestors must not be links.'}
        $probe=Split-Path $probe -Parent
    }
    if(Test-Path -LiteralPath $output){throw 'Preserve the previous report; use a new path.'}
    New-Item -ItemType Directory -Path $parent -Force|Out-Null
    [ordered]@{CandidateCount=$paths.Count;Findings=@($issues.ToArray());Scope='Git tracked/untracked candidates only; heuristic scan, not proof of absence of secrets; no values included.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $output -Encoding utf8
    if($issues.Count){throw "$($issues.Count) findings require review; see $Report (values not printed)."}
    Write-Output "PASS: $($paths.Count) Git candidates; no configured secret/path/generated-file patterns found."
} finally {Pop-Location}
