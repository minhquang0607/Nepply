$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/inno/Nepply.iss') -Raw
$repo=Split-Path $PSScriptRoot -Parent
[xml]$appProject=Get-Content -LiteralPath (Join-Path $repo 'src/Nepply/Nepply.csproj') -Raw
[xml]$packageManifest=Get-Content -LiteralPath (Join-Path $repo 'packaging/msix/AppxManifest.xml') -Raw
$minimumMatch=[regex]::Match($source,'(?m)^MinVersion=(\d+\.\d+\.\d+)\s*$')
$minimum=if($minimumMatch.Success){[version]($minimumMatch.Groups[1].Value+'.0')}else{$null}
$checks=[ordered]@{
    'Installer OS minimum matches app and MSIX'=$null -ne $minimum -and $minimum -eq [version]$appProject.Project.PropertyGroup.SupportedOSPlatformVersion -and $minimum -eq [version]$packageManifest.Package.Dependencies.TargetDeviceFamily.MinVersion
    'Installer runs per user'=$source.Contains('PrivilegesRequired=lowest')
    'QA uses a separate app identity'=$source.Contains('AppId=Nepply.Internal.InstallerQA')
    'Production recovery executes before removal'=$source.Contains('function InitializeUninstall(): Boolean;') -and $source.Contains("'--restore-desktop'")
    'Recovery failure blocks uninstall'=$source.Contains('Result := RecoveryExitCode = 0;')
    'Recovery only applies to owned journal'=$source.Contains('desktop-visibility.lease')
    'Manifest and optional symbols are both packaged'=$source.Contains('release-manifest.json') -and $source.Contains('*.pdb')
    'Guardian executable is required in its own directory'=$source.Contains('Source: "{#PayloadRoot}\guardian\Nepply.DesktopGuardian.exe"; DestDir: "{app}\guardian"; Flags: ignoreversion')
    'Startup cleanup checks ownership'=$source.Contains('CompareText(CurrentCommand, OwnedCommand) = 0')
    'Update waits for normal exit without killing app'=$source.Contains("CheckForMutexes('Local\HoverBox-SetupGuard')") -and $source.Contains('Attempts < 600') -and $source.Contains('CloseApplications=no')
    'Upgrade recovery runs before copying'=$source.Contains('function PrepareToInstall(var NeedsRestart: Boolean): String;') -and $source.Contains("'--restore-desktop'")
    'Automatic relaunch is update-only'=$source.Contains('Flags: nowait skipifnotsilent; Check: IsNepplyUpdate')
}
foreach($entry in $checks.GetEnumerator()) { if(-not $entry.Value) { throw "FAIL $($entry.Key)" }; Write-Output "PASS $($entry.Key)" }
Write-Output "RESULT: $($checks.Count) source-contract checks passed. Not an installer execution or crash-recovery certification."
