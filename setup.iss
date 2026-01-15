; Script Inno Setup untuk Desktop Music Player
; Optimized for Size & Clean Install

#define MyAppName "Crescendo Music Player"
#define MyAppVersion "1.0"
#define MyAppPublisher "Crescendo Inc."
#define MyAppExeName "DesktopMusicPlayer.exe"
#define MyAppId "{{8B306052-1678-4F11-8935-D3505F66E7A9}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Crescendo
DisableProgramGroupPage=yes
; Force Admin for Program Files install
PrivilegesRequired=admin
OutputDir=.\InnoOutput
OutputBaseFilename=Crescendo_Setup_v19
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Source from 'dist' folder (Loose Files)
Source: "d:\Project\DesktopMusic\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

// Helper to kill process
procedure KillProcess(const ProcessName: String);
var
  WbemLocator: Variant;
  WbemServices: Variant;
  WbemObjectSet: Variant;
  WbemObject: Variant;
  I: Integer;
begin
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemServices := WbemLocator.ConnectServer('.', 'root\CIMV2');
    WbemObjectSet := WbemServices.ExecQuery(Format('SELECT * FROM Win32_Process WHERE Name="%s"', [ProcessName]));
    for I := 0 to WbemObjectSet.Count - 1 do
    begin
      WbemObject := WbemObjectSet.ItemIndex(I);
      WbemObject.Terminate();
    end;
  except
  end;
end;

// Clean up old data to ensure FRESH START
procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDataPath: String;
begin
  if CurStep = ssInstall then
  begin
    // 1. Kill running app
    KillProcess('DesktopMusicPlayer.exe');
    KillProcess('Setup.exe'); 
    
    // 2. Delete AppData (New Folder Name) - BOTH Roaming and Local
    AppDataPath := ExpandConstant('{userappdata}\CrescendoMusicPlayer');
    if DirExists(AppDataPath) then DelTree(AppDataPath, True, True, True);

    AppDataPath := ExpandConstant('{localappdata}\CrescendoMusicPlayer');
    if DirExists(AppDataPath) then DelTree(AppDataPath, True, True, True);

    // 3. Delete AppData (Old Folder Names - Cleanup)
    AppDataPath := ExpandConstant('{userappdata}\DesktopMusicPlayer');
    if DirExists(AppDataPath) then DelTree(AppDataPath, True, True, True);

    AppDataPath := ExpandConstant('{userappdata}\Crescendo');
    if DirExists(AppDataPath) then DelTree(AppDataPath, True, True, True);
  end;
end;
