; ============================================================
;  Ollama Dashboard — Inno Setup Script
;  Build: iscc installer\setup.iss
;  Requires: Inno Setup 6.2+ (https://jrsoftware.org/isdl.php)
; ============================================================

#define MyAppName        "Ollama Dashboard"
#define MyAppVersion     "0.1.0"
#define MyAppPublisher   "YourCompanyName"
#define MyAppURL         "https://github.com/yourusername/ollama-dashboard"
#define MyAppExeName     "OllamaDashboard.exe"
#define BuildOutputDir   "..\src\OllamaDashboard\bin\Release\net8.0-windows\publish"
#define UpdaterOutputDir "..\src\OllamaDashboard.Updater\bin\Release\net8.0-windows\publish"

[Setup]
AppId={{6C7E1D2F-4B3A-4E5D-9A1B-8F7E6D5C4B3A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=OllamaDashboard-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
DisableWelcomePage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
MinVersion=10.0
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=..\src\OllamaDashboard\Assets\icon.ico

[Languages]
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Startmenü-Eintrag erstellen"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main app — all published files
Source: "{#BuildOutputDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Updater — separate helper
Source: "{#UpdaterOutputDir}\OllamaDashboard.Updater.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean logs and settings on uninstall (ask user first if preferred)
Type: filesandordirs; Name: "{userappdata}\OllamaDashboard\logs"

; =============  Prerequisite check: .NET 8 Desktop Runtime  =============

[Code]
function IsDotNet8DesktopInstalled: Boolean;
var
  InstallPath: string;
  ResultCode: Integer;
  ExecOk: Boolean;
  TempFile: string;
  FileContents: AnsiString;
begin
  Result := False;
  // Use `dotnet --list-runtimes` and grep for Microsoft.WindowsDesktop.App 8.
  TempFile := ExpandConstant('{tmp}\dotnet-runtimes.txt');
  ExecOk := Exec(ExpandConstant('{cmd}'),
                 '/C dotnet --list-runtimes > "' + TempFile + '" 2>&1',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if ExecOk and FileExists(TempFile) then
  begin
    if LoadStringFromFile(TempFile, FileContents) then
    begin
      if Pos('Microsoft.WindowsDesktop.App 8.', FileContents) > 0 then
        Result := True;
    end;
    DeleteFile(TempFile);
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDotNet8DesktopInstalled then
  begin
    if MsgBox(
         '.NET 8 Desktop Runtime ist nicht installiert.' + #13#10#13#10 +
         'Möchtest du die Download-Seite öffnen? Die Installation der App wird danach abgebrochen.',
         mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open',
        'https://dotnet.microsoft.com/download/dotnet/8.0/runtime',
        '', '', SW_SHOW, ewNoWait, ResultCode := 0);
    end;
    Result := False;
  end;
end;
