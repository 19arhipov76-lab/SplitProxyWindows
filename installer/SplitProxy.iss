#define MyAppName "Split Proxy"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Split Proxy"
#define MyAppExeName "SplitProxy.exe"

[Setup]
AppId={{8F3D76A0-8142-4E5E-96A0-D51C2BD4B523}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\SplitProxy
DefaultGroupName=Split Proxy
OutputDir=..\dist
OutputBaseFilename=SplitProxySetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\SplitProxy.exe
SetupLogging=yes

[Dirs]
Name: "{commonappdata}\SplitProxy"; Permissions: users-modify
Name: "{commonappdata}\SplitProxy\engine"; Permissions: users-modify

[Files]
Source: "..\dist\stage\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\stage\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\stage\updater\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\stage\engine\*"; DestDir: "{app}\service\engine"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\stage\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Split Proxy"; Filename: "{app}\SplitProxy.exe"
Name: "{autodesktop}\Split Proxy"; Filename: "{app}\SplitProxy.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SplitProxy"; ValueData: """{app}\SplitProxy.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create SplitProxyService binPath= ""{app}\service\SplitProxy.Service.exe"" start= auto DisplayName= ""Split Proxy Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description SplitProxyService ""Маршрутизация выбранных приложений Split Proxy"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure SplitProxyService reset= 86400 actions= restart/5000/restart/10000/""/0"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start SplitProxyService"; Flags: runhidden waituntilterminated
Filename: "{app}\SplitProxy.exe"; Description: "Запустить Split Proxy"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop SplitProxyService"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete SplitProxyService"; Flags: runhidden waituntilterminated

[Code]
function ServiceExists(const Name: string): Boolean;
var ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'query ' + Name, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    if ServiceExists('SplitProxyService') then
    begin
      Exec(ExpandConstant('{sys}\sc.exe'), 'stop SplitProxyService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(700);
      Exec(ExpandConstant('{sys}\sc.exe'), 'delete SplitProxyService', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(500);
    end;
  end;
end;
