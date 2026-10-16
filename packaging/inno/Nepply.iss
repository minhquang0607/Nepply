#ifndef PayloadRoot
  #error "Use scripts/Build-Distribution.ps1: PayloadRoot is required."
#endif
#ifndef ReleaseOutputDir
  #error "Use scripts/Build-Distribution.ps1: ReleaseOutputDir is required."
#endif
#ifndef ReleaseVersion
  #error "ReleaseVersion must come from the source project."
#endif
#ifndef ReleaseFileVersion
  #error "ReleaseFileVersion must come from the source project."
#endif
[Setup]
#ifdef InstallerQA
AppId=Nepply.Internal.InstallerQA
UsePreviousAppDir=no
AppName=Nepply Internal QA
DefaultDirName={localappdata}\Programs\Nepply-Internal-QA
OutputBaseFilename=Nepply-Internal-QA-Setup
#else
AppId={{5E9F4A2B-1A8D-4C9C-8A5E-F7C4E8A1D3B2}
AppName=Nepply
DefaultDirName={localappdata}\Programs\Nepply
OutputBaseFilename=Nepply-{#ReleaseVersion}-Setup
#endif
AppVersion={#ReleaseVersion}
VersionInfoVersion={#ReleaseFileVersion}
AppPublisher=Minh Quang
AppSupportURL=https://github.com/minhquang0607/Nepply/issues
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
OutputDir={#ReleaseOutputDir}
InfoBeforeFile=..\..\src\Nepply\Resources\BETA_NOTICE.txt
SetupIconFile=..\..\src\Nepply\Resources\icon.ico
UninstallDisplayIcon={app}\Nepply.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Let the user finish editing and restore Desktop through a normal app exit.
; Stable across rename: also block installation while a legacy build owns Desktop.
AppMutex=Local\HoverBox-SetupGuard
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PayloadRoot}\Nepply.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\guardian\Nepply.DesktopGuardian.exe"; DestDir: "{app}\guardian"; Flags: ignoreversion
Source: "{#PayloadRoot}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Keep packaged files consistent with the manifest when debug symbols are present.
Source: "{#PayloadRoot}\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#PayloadRoot}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\SECURITY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\release-manifest.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadRoot}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\src\Nepply\Resources\BETA_NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion

#ifndef InstallerQA
[InstallDelete]
; Exact legacy payload names only; never delete user settings or whole directories.
Type: files; Name: "{app}\HoverBox.exe"
Type: files; Name: "{app}\HoverBox.dll"
Type: files; Name: "{app}\HoverBox.pdb"
Type: files; Name: "{app}\HoverBox.deps.json"
Type: files; Name: "{app}\HoverBox.runtimeconfig.json"

[Icons]
Name: "{autoprograms}\Nepply"; Filename: "{app}\Nepply.exe"
Name: "{autodesktop}\Nepply"; Filename: "{app}\Nepply.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Nepply.exe"; Parameters: "--show"; Description: "{cm:LaunchProgram,Nepply}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\Nepply.exe"; Parameters: "--show"; Flags: nowait skipifnotsilent; Check: IsNepplyUpdate
#endif

[Code]
function IsNepplyUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:NEPPLYUPDATE|0}') = '1';
end;

function InitializeSetup(): Boolean;
var
  Attempts: Integer;
begin
  Result := True;
  if not IsNepplyUpdate() then Exit;
  { Parent starts the verified Setup before a normal shutdown. Never kill it. }
  Attempts := 0;
  while CheckForMutexes('Local\HoverBox-SetupGuard') and (Attempts < 600) do
  begin
    Sleep(100);
    Attempts := Attempts + 1;
  end;
  Result := not CheckForMutexes('Local\HoverBox-SetupGuard');
  if not Result then
    MsgBox('Nepply has not exited safely. No update was installed. Close Nepply and retry.', mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  RecoveryExitCode: Integer;
begin
  Result := '';
#ifdef InstallerQA
  Exit;
#endif
  { Recovery is mandatory for every upgrade with an outstanding owned lease,
    including a manual upgrade after a previous crash. Never overwrite first. }
  if not FileExists(ExpandConstant('{localappdata}\HoverBox\desktop-visibility.lease')) then Exit;
  if not FileExists(ExpandConstant('{app}\Nepply.exe')) then
  begin
    Result := 'Desktop recovery is pending. Restore Desktop icons with the previous Nepply installation before continuing.';
    Exit;
  end;
  if not Exec(ExpandConstant('{app}\Nepply.exe'), '--restore-desktop', '', SW_HIDE,
    ewWaitUntilTerminated, RecoveryExitCode) then
    Result := 'Nepply could not start Desktop recovery. No update was installed.';
  if (Result = '') and (RecoveryExitCode <> 0) then
    Result := 'Nepply could not restore Desktop icons. No update was installed.';
end;

procedure RetireLegacyShortcut(const ShortcutPath: String);
var
  Shell: Variant;
  Link: Variant;
begin
  if not FileExists(ShortcutPath) then Exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Link := Shell.CreateShortcut(ShortcutPath);
    if (CompareText(Link.TargetPath, ExpandConstant('{app}\HoverBox.exe')) = 0) and
       (Trim(Link.Arguments) = '') then
      if not DeleteFile(ShortcutPath) then Log('Could not retire legacy shortcut: ' + ShortcutPath);
  except
    Log('Preserved legacy shortcut because ownership could not be verified: ' + ShortcutPath);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CurrentCommand: String;
  NewCommand: String;
begin
#ifdef InstallerQA
  Exit;
#endif
  if CurStep <> ssPostInstall then Exit;
  NewCommand := '"' + ExpandConstant('{app}\Nepply.exe') + '"';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HoverBox', CurrentCommand) then
    if CompareText(CurrentCommand, '"' + ExpandConstant('{app}\HoverBox.exe') + '"') = 0 then
    begin
      if not RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nepply') then
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nepply', NewCommand);
      if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nepply', CurrentCommand) then
        if CompareText(CurrentCommand, NewCommand) = 0 then
          RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'HoverBox');
    end;
  RetireLegacyShortcut(ExpandConstant('{autoprograms}\HoverBox.lnk'));
  RetireLegacyShortcut(ExpandConstant('{autodesktop}\HoverBox.lnk'));
end;

function InitializeUninstall(): Boolean;
var
  RecoveryExitCode: Integer;
begin
  Result := True;
#ifdef InstallerQA
  { Packaging-only fixture: never read or restore the production Desktop lease. }
  Exit;
#endif
  if not FileExists(ExpandConstant('{localappdata}\HoverBox\desktop-visibility.lease')) then
    Exit;
  Result := Exec(ExpandConstant('{app}\Nepply.exe'), '--restore-desktop', '',
    SW_HIDE, ewWaitUntilTerminated, RecoveryExitCode);
  if Result then
    Result := RecoveryExitCode = 0;
  if not Result then
    MsgBox('Nepply could not safely restore your Desktop icons. Close Nepply and try again. If Windows blocked Nepply, do not disable protection: obtain an approved build, or restore icons through Desktop > View > Show desktop icons and contact support. No application files have been removed.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentCommand: String;
  OwnedCommand: String;
begin
#ifdef InstallerQA
  { Internal packaging tests must not modify production startup registration. }
  Exit;
#endif
  if CurUninstallStep <> usUninstall then
    Exit;
  OwnedCommand := '"' + ExpandConstant('{app}\Nepply.exe') + '"';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
    'Nepply', CurrentCommand) then
  begin
    if CompareText(CurrentCommand, OwnedCommand) = 0 then
    begin
      if not RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nepply') then
        Log('Could not remove this installation''s Nepply startup entry.');
    end
    else
      Log('Preserved Nepply startup entry: it does not belong to this installation.');
  end;
end;
