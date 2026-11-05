$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/inno/Nepply.iss') -Raw
$repo=Split-Path $PSScriptRoot -Parent
[xml]$appProject=Get-Content -LiteralPath (Join-Path $repo 'src/Nepply/Nepply.csproj') -Raw
[xml]$packageManifest=Get-Content -LiteralPath (Join-Path $repo 'packaging/msix/AppxManifest.xml') -Raw
$minimumMatch=[regex]::Match($source,'(?m)^MinVersion=(\d+\.\d+\.\d+)\s*$')
$minimum=if($minimumMatch.Success){[version]($minimumMatch.Groups[1].Value+'.0')}else{$null}
function Get-PascalRoutine([string]$header) {
    $match=[regex]::Match($source,'(?ms)^'+[regex]::Escape($header)+'.*?^end;')
    if($match.Success){$match.Value}else{''}
}
$uninstall=Get-PascalRoutine 'function InitializeUninstall(): Boolean;'
$prepare=Get-PascalRoutine 'function PrepareToInstall(var NeedsRestart: Boolean): String;'
$fallback=Get-PascalRoutine 'function RecoverDesktopWithoutApp(): Boolean;'
$native=Get-PascalRoutine 'function RestoreDesktopIconsNatively(): Boolean;'
$distribution=Get-Content -LiteralPath (Join-Path $repo 'scripts/Build-Distribution.ps1') -Raw
# Nepply's Setup text lives in Messages\Nepply.<code>.isl, one file per [Languages] entry.
$messages=@{}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $repo 'packaging/inno/Messages') -Filter 'Nepply.*.isl') {
    $code=$file.BaseName.Substring('Nepply.'.Length)
    $messages[$code]=@{}
    foreach($line in [IO.File]::ReadAllLines($file.FullName)) { if($line -match '^([A-Za-z]+)=(.+)$') { $messages[$code][$Matches[1]]=$Matches[2] } }
}
$languageFiles=@([regex]::Matches($source,'(?m)^Name: "[a-z]+"; MessagesFile: "[^,"]+\.isl,Messages\\Nepply\.([a-z]+)\.isl"') | ForEach-Object { $_.Groups[1].Value })
$noticeFiles=@([regex]::Matches($source,'(?m)^Name: "[a-z]+"; MessagesFile: "[^,"]+\.isl,Messages\\Nepply\.([a-z]+)\.isl"; InfoBeforeFile: "Notices\\Notice\.\1\.txt"\s*$') | ForEach-Object { $_.Groups[1].Value })
$notices=@{}
foreach($code in $noticeFiles) {
    $path=Join-Path $repo "packaging/inno/Notices/Notice.$code.txt"
    if(Test-Path -LiteralPath $path) { $notices[$code]=Get-Content -LiteralPath $path -Raw -Encoding utf8 }
}
# {cm:Name} also reaches Inno Setup's own messages, so only Nepply's keys count.
$usedMessages=@(@([regex]::Matches($source,"CustomMessage\('([A-Za-z]+)'\)") | ForEach-Object { $_.Groups[1].Value }) +
    @([regex]::Matches($source,'\{cm:([A-Za-z]+)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $messages['en'].ContainsKey($_) }) | Sort-Object -Unique)
$checks=[ordered]@{
    'Every Setup language has its own complete Nepply messages'=$messages.ContainsKey('en') -and $languageFiles.Count -eq $messages.Count -and
        @($languageFiles | Where-Object { -not $messages.ContainsKey($_) }).Count -eq 0 -and
        @($messages.Values | Where-Object { @(Compare-Object @($_.Keys) @($messages['en'].Keys)).Count -ne 0 }).Count -eq 0 -and
        @($usedMessages | Where-Object { -not $messages['en'].ContainsKey($_) }).Count -eq 0 -and $usedMessages.Count -eq $messages['en'].Count
    'Uninstaller signing is opt-in through SignedUninstallerDir'=$source -match '(?ms)^#ifdef SignedUninstallerDir\r?\n(;[^\r\n]*\r?\n)*SignedUninstaller=yes\r?\nSignedUninstallerDir=\{#SignedUninstallerDir\}\r?\n#else\r?\nSignedUninstaller=no\r?\n#endif'
    'Installer does not invoke a signing tool itself'=$source -notmatch '(?m)^\s*SignTool\s*='
    'Distribution passes and verifies the signed uninstaller'=$distribution.Contains('"/DSignedUninstallerDir=$signedUninstaller"') -and $distribution.Contains('SIGNATURE PENDING') -and $distribution.Contains('Assert-UninstallerSignature $usedPath')
    'Installer OS minimum matches app and MSIX'=$null -ne $minimum -and $minimum -eq [version]$appProject.Project.PropertyGroup.SupportedOSPlatformVersion -and $minimum -eq [version]$packageManifest.Package.Dependencies.TargetDeviceFamily.MinVersion
    'Installer runs per user'=$source.Contains('PrivilegesRequired=lowest')
    'QA uses a separate app identity'=$source.Contains('AppId=Nepply.Internal.InstallerQA')
    'Production recovery executes before removal'=$uninstall.Contains('RunAppRecovery(RecoveryExitCode)') -and $source.Contains("'--restore-desktop'")
    'Running app still blocks uninstall'=$uninstall.Contains('if RecoveryExitCode = RecoveryAppRunning then') -and $uninstall.Contains('Result := False;')
    'Blocked app falls back to Setup recovery on uninstall'=$uninstall.Contains('if RecoverDesktopWithoutApp() then Exit;')
    'Failed recovery never makes uninstall impossible'=$uninstall.Contains('SuppressibleMsgBox(') -and $uninstall.Contains('MB_YESNO, IDYES) = IDYES') -and $uninstall.Contains("CustomMessage('UninstallRecoveryFailed')") -and $messages['en']['UninstallRecoveryFailed'].Contains('View > Show desktop icons')
    'Fallback only toggles a lease Nepply wrote'=$fallback.Contains('(Marker <> DesktopLeaseMarker)') -and $source.Contains("DesktopLeaseMarker = 'restore-visible-v1';")
    'Fallback toggles only while hidden and verifies'=$native.Contains('if not IsWindowVisible(List) then') -and $native.Contains('PostMessage(View, $0111, $7402, 0)') -and $native.Contains('Result := IsWindowVisible(List);')
    'Failed recovery resets persisted HideIcons'=$source.Contains("RegWriteDWordValue(HKCU, ExplorerAdvancedKey, 'HideIcons', 0)") -and $uninstall.Contains('ClearPersistedHiddenDesktop();') -and $prepare.Contains('ClearPersistedHiddenDesktop();')
    'Recovery only applies to owned journal'=$source.Contains("DesktopLease = '{localappdata}\Nepply\desktop-visibility.lease';")
    'Manifest and optional symbols are both packaged'=$source.Contains('release-manifest.json') -and $source.Contains('*.pdb')
    'Guardian executable is required in its own directory'=$source.Contains('Source: "{#PayloadRoot}\guardian\Nepply.DesktopGuardian.exe"; DestDir: "{app}\guardian"; Flags: ignoreversion')
    'Startup cleanup checks ownership'=$source.Contains('CompareText(CurrentCommand, OwnedCommand) = 0')
    'Update waits for normal exit without killing app'=$source.Contains("CheckForMutexes('Local\Nepply-SetupGuard')") -and $source.Contains('Attempts < 600') -and $source.Contains('CloseApplications=no')
    'Upgrade recovery runs before copying'=$prepare.Contains('RunAppRecovery(RecoveryExitCode)') -and $prepare.Contains('RecoverDesktopWithoutApp()')
    'Running app still blocks upgrade'=$prepare.Contains('if RecoveryExitCode = RecoveryAppRunning then') -and $prepare.Contains("CustomMessage('UpgradeAppRunning')") -and $messages['en']['UpgradeAppRunning'].Contains('No update was installed.')
    'Setup always offers the language choice'=$source -match '(?m)^ShowLanguageDialog=yes\s*$'
    'Every Setup language hands its own code to the app'=@($messages.Keys | Where-Object { $messages[$_]['LanguageId'] -cne $_ }).Count -eq 0
    'Removing user data is opt-in, defaults to No and skips silent runs'=$source.Contains("(not UninstallSilent()) and (MsgBox(CustomMessage('UninstallRemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES)")
    'Desktop lease survives data removal'=$source.Contains("if FileExists(ExpandConstant(DesktopLease)) then") -and $source.Contains("DelTree(ExpandConstant('{localappdata}\Nepply\Logs')") -and $source.Contains("DelTree(ExpandConstant('{localappdata}\Nepply\Updates')")
    'Every Setup language shows only its own notice'=$noticeFiles.Count -eq $languageFiles.Count -and $notices.Count -eq $languageFiles.Count -and
        -not ($source -match '(?m)^InfoBeforeFile=') -and
        @($notices.Values | Where-Object { -not $_.Contains('https://github.com/minhquang0607/Nepply/releases') -or -not $_.Contains('LICENSE') }).Count -eq 0 -and
        @($notices.Keys | Where-Object { $_ -ne 'en' -and $notices[$_].Contains('Never turn off Windows security') }).Count -eq 0 -and
        @($notices.Keys | Where-Object { $_ -ne 'vi' -and $notices[$_].Contains('Không tắt các tính năng') }).Count -eq 0
    'Startup task is offered on the first install only and starts ticked'=$source -match '(?m)^Name: "startup";[^\r\n]*; Check: IsFirstInstall\s*$' -and -not ($source -match '(?m)^Name: "startup";[^\r\n]*Flags:')
    'Setup hands over the startup choice on the first install only'=$source.Contains('ValueData: 1; Tasks: startup; Check: IsFirstInstall') -and $source.Contains('ValueData: 0; Tasks: not startup; Check: IsFirstInstall')
    'First install is decided before Setup writes its own entry'=$source.Contains("FirstInstall := not RegKeyExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + UninstallEntry);") -and $source.Contains("UninstallEntry = '{5E9F4A2B-1A8D-4C9C-8A5E-F7C4E8A1D3B2}_is1';")
    'Uninstall removes the Windows startup switch once its entry is gone'=$source.Contains("if not RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nepply') then") -and $source.Contains("RegDeleteValue(HKCU, StartupApprovedKey, 'Nepply');")
    'Automatic relaunch is update-only'=$source.Contains('Flags: nowait skipifnotsilent; Check: IsNepplyUpdate')
}
foreach($entry in $checks.GetEnumerator()) { if(-not $entry.Value) { throw "FAIL $($entry.Key)" }; Write-Output "PASS $($entry.Key)" }
Write-Output "RESULT: $($checks.Count) source-contract checks passed. Not an installer execution or crash-recovery certification."
