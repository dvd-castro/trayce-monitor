; Trayce Monitor - per-user installer (Inno Setup 6). No administrator rights, no UAC prompt.
; Built by tools/build-release.ps1 (and by the release workflow). Compile by hand with:
;   ISCC.exe /DAppVersion=0.1.0 /DNumericVersion=0.1.0.0 /DSourceDir=<publish folder> /DOutputDir=<out folder> installer\TrayceMonitor.iss
; Keep AppId fixed: it is how Windows (and upgrades) recognise this program.

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\out\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\out\release"
#endif
#define AppName "Trayce Monitor"
#define AppExe "TrayceMonitor.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{545A4B8E-5106-4ED4-AD11-0A5731D82B60}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=David Castro
AppPublisherURL=https://github.com/dvd-castro/trayce-monitor
AppSupportURL=https://github.com/dvd-castro/trayce-monitor/issues
AppUpdatesURL=https://github.com/dvd-castro/trayce-monitor/releases
VersionInfoVersion={#NumericVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
VersionInfoCompany=David Castro
; Per user: installs under %LOCALAPPDATA%\Programs, writes only to HKCU, never asks for elevation.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
SetupIconFile=..\assets\brand\trayce.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=TrayceMonitor-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The app is closed by hand (see [Code]) with --quit instead of the generic Restart Manager prompt.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.AutostartTask=Start {#AppName} with Windows
english.LaunchApp=Open {#AppName}
brazilianportuguese.AutostartTask=Iniciar o {#AppName} com o Windows
brazilianportuguese.LaunchApp=Abrir o {#AppName}

[Tasks]
; Unchecked by default: the user opts in.
Name: "autostart"; Description: "{cm:AutostartTask}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\{#AppExe}"

[Registry]
; Same value the app writes from its own tray menu ("Iniciar com o Windows"): HKCU Run, quoted path.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "TrayceMonitor"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
// Asks a running instance to close (TrayceMonitor.exe --quit posts WM_CLOSE to its tray window).
// Exit code 1 just means no instance was running, so the result is ignored.
procedure QuitRunningApp(const Dir: String);
var
  Exe: String;
  Code: Integer;
begin
  Exe := AddBackslash(Dir) + '{#AppExe}';
  if FileExists(Exe) then
  begin
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Sleep(1500); // let the process finish closing so the files are not locked
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  QuitRunningApp(WizardDirValue); // upgrades: close the old version before overwriting it
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
    QuitRunningApp(ExpandConstant('{app}'))
  else if CurUninstallStep = usPostUninstall then
  begin
    // The app can also enable "start with Windows" by itself, so remove that value too if it points here.
    if RegQueryStringValue(HKCU, '{#RunKey}', 'TrayceMonitor', Value) then
      if CompareText(Value, '"' + ExpandConstant('{app}\{#AppExe}') + '"') = 0 then
        RegDeleteValue(HKCU, '{#RunKey}', 'TrayceMonitor');
  end;
end;
