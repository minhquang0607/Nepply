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
#ifdef SignedUninstallerDir
; Two-pass external signing (remote signers such as SignPath): the first compile
; writes an unsigned uninst-*.e32 here and aborts; sign it, then compile again.
SignedUninstaller=yes
SignedUninstallerDir={#SignedUninstallerDir}
#else
SignedUninstaller=no
#endif
UninstallDisplayIcon={app}\Nepply.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Follow the Windows display language; ask only when it is not one of [Languages].
ShowLanguageDialog=auto
; Let the user finish editing and restore Desktop through a normal app exit.
; Stable across rename: also block installation while a legacy build owns Desktop.
AppMutex=Local\HoverBox-SetupGuard
CloseApplications=no
RestartApplications=no

[Languages]
; Inno Setup ships these translations except Vietnamese, kept in Languages\.
; Messages\Nepply.*.isl adds Nepply's own text.
Name: "english"; MessagesFile: "compiler:Default.isl,Messages\Nepply.en.isl"
Name: "vietnamese"; MessagesFile: "Languages\Vietnamese.isl,Messages\Nepply.vi.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl,Messages\Nepply.ja.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl,Messages\Nepply.de.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl,Messages\Nepply.zh.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl,Messages\Nepply.ru.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl,Messages\Nepply.fr.isl"
Name: "thai"; MessagesFile: "compiler:Languages\Thai.isl,Messages\Nepply.th.isl"

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
const
  DesktopLease = '{localappdata}\HoverBox\desktop-visibility.lease';
  DesktopLeaseMarker = 'restore-visible-v1';
  ExplorerAdvancedKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced';
  RecoveryAppRunning = 2;

{ HWND-typed WindowName keeps the NULL title pointer-sized on every Setup architecture. }
function FindWindowEx(Parent, ChildAfter: HWND; ClassName: String; WindowName: HWND): HWND;
  external 'FindWindowExW@user32.dll stdcall';
function IsWindowVisible(Wnd: HWND): Boolean;
  external 'IsWindowVisible@user32.dll stdcall';

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
    MsgBox(CustomMessage('UpdateNotExited'), mbError, MB_OK);
end;

function LocateDesktopView(var View, List: HWND): Boolean;
var
  Worker: HWND;
begin
  { Same lookup as ExplorerDesktopVisibility.Locate. }
  View := FindWindowEx(FindWindowByClassName('Progman'), 0, 'SHELLDLL_DefView', 0);
  Worker := 0;
  while View = 0 do
  begin
    Worker := FindWindowEx(0, Worker, 'WorkerW', 0);
    if Worker = 0 then Break;
    View := FindWindowEx(Worker, 0, 'SHELLDLL_DefView', 0);
  end;
  List := 0;
  if View <> 0 then List := FindWindowEx(View, 0, 'SysListView32', 0);
  Result := List <> 0;
end;

function RestoreDesktopIconsNatively(): Boolean;
var
  View: HWND;
  List: HWND;
  Attempts: Integer;
begin
  Result := False;
  if not LocateDesktopView(View, List) then
  begin
    Log('Desktop icon view was not found; Explorer may not be running.');
    Exit;
  end;
  { Explorer exposes a toggle, not a setter: post it only while icons are hidden,
    asynchronously so a hung Explorer cannot hang Setup, then verify. }
  if not IsWindowVisible(List) then
  begin
    if not PostMessage(View, $0111, $7402, 0) then Exit;
    Attempts := 0;
    while (not IsWindowVisible(List)) and (Attempts < 30) do
    begin
      Sleep(100);
      Attempts := Attempts + 1;
    end;
  end;
  Result := IsWindowVisible(List);
end;

{ Fallback when Windows blocks Nepply.exe (Smart App Control, App Control, AppLocker)
  or it is missing: restore from Setup itself, but only for a lease Nepply wrote. }
function RecoverDesktopWithoutApp(): Boolean;
var
  Marker: AnsiString;
begin
  Result := False;
  if not LoadStringFromFile(ExpandConstant(DesktopLease), Marker) or (Marker <> DesktopLeaseMarker) then
  begin
    Log('Desktop lease has no restore marker; Setup will not toggle Desktop icons.');
    Exit;
  end;
  Result := RestoreDesktopIconsNatively();
  if Result then
  begin
    Log('Setup restored Desktop icons without Nepply.exe.');
    if not DeleteFile(ExpandConstant(DesktopLease)) then Log('Could not remove the completed Desktop lease.');
  end;
end;

{ Last resort: Explorer reads HideIcons at sign-in, so icons return then even if
  the live toggle failed. The lease is kept for Nepply to reconcile later. }
procedure ClearPersistedHiddenDesktop();
var
  Hidden: Cardinal;
begin
  if RegQueryDWordValue(HKCU, ExplorerAdvancedKey, 'HideIcons', Hidden) and (Hidden <> 0) then
    if not RegWriteDWordValue(HKCU, ExplorerAdvancedKey, 'HideIcons', 0) then
      Log('Could not reset the persisted HideIcons value.');
end;

{ True only when Nepply ran and restored Desktop. ExitCode is -1 when it could not start. }
function RunAppRecovery(var ExitCode: Integer): Boolean;
begin
  Result := False;
  ExitCode := -1;
  if not FileExists(ExpandConstant('{app}\Nepply.exe')) then
  begin
    Log('Nepply.exe is missing; Desktop recovery falls back to Setup.');
    Exit;
  end;
  if not Exec(ExpandConstant('{app}\Nepply.exe'), '--restore-desktop', '', SW_HIDE,
    ewWaitUntilTerminated, ExitCode) then
  begin
    Log('Nepply.exe could not start Desktop recovery: ' + SysErrorMessage(ExitCode));
    ExitCode := -1;
    Exit;
  end;
  Log('Nepply Desktop recovery exit code: ' + IntToStr(ExitCode));
  Result := ExitCode = 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  RecoveryExitCode: Integer;
begin
  Result := '';
#ifdef InstallerQA
  Exit;
#endif
  { Recovery runs for every upgrade with an outstanding owned lease, including a
    manual upgrade after a previous crash, before any file is overwritten. }
  if not FileExists(ExpandConstant(DesktopLease)) then Exit;
  if RunAppRecovery(RecoveryExitCode) then Exit;
  if RecoveryExitCode = RecoveryAppRunning then
  begin
    Result := CustomMessage('UpgradeAppRunning');
    Exit;
  end;
  if RecoverDesktopWithoutApp() then Exit;
  { Never strand the user on a blocked old build: the new Nepply restores the
    retained lease at its next start (App.OnStartup). }
  ClearPersistedHiddenDesktop();
  SuppressibleMsgBox(CustomMessage('UpgradeRecoveryDeferred'), mbInformation, MB_OK, IDOK);
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
  if not FileExists(ExpandConstant(DesktopLease)) then
    Exit;
  if RunAppRecovery(RecoveryExitCode) then Exit;
  if RecoveryExitCode = RecoveryAppRunning then
  begin
    MsgBox(CustomMessage('UninstallAppRunning'), mbError, MB_OK);
    Result := False;
    Exit;
  end;
  if RecoverDesktopWithoutApp() then Exit;
  { A blocked or broken Nepply.exe must never make uninstall impossible. }
  ClearPersistedHiddenDesktop();
  Result := SuppressibleMsgBox(CustomMessage('UninstallRecoveryFailed'), mbConfirmation, MB_YESNO, IDYES) = IDYES;
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
