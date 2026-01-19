; Script Inno Setup untuk Desktop Music Player
; Optimized for Size & Clean Install

#define MyAppName "Crescendo Music Player"
#define MyAppVersion "1.0.0-beta.2"
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
OutputBaseFilename=Crescendo_Setup_v1.0.0-beta.2
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=Assets\ocean-wave-blue.ico
WizardSmallImageFile=Assets\wizard-small.bmp

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

[Registry]
; === File Type Registration ===
; Create our own file type identifier
Root: HKCR; Subkey: "CrescendoMusicPlayer.mp3"; ValueType: string; ValueData: "MP3 Audio File"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CrescendoMusicPlayer.mp3\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "CrescendoMusicPlayer.mp3\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKCR; Subkey: "CrescendoMusicPlayer.mp3\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKCR; Subkey: "CrescendoMusicPlayer.m4a"; ValueType: string; ValueData: "MPEG-4 Audio File"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CrescendoMusicPlayer.m4a\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "CrescendoMusicPlayer.m4a\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKCR; Subkey: "CrescendoMusicPlayer.m4a\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKCR; Subkey: "CrescendoMusicPlayer.wav"; ValueType: string; ValueData: "WAVE Audio File"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CrescendoMusicPlayer.wav\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "CrescendoMusicPlayer.wav\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKCR; Subkey: "CrescendoMusicPlayer.wav\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKCR; Subkey: "CrescendoMusicPlayer.flac"; ValueType: string; ValueData: "FLAC Audio File"; Flags: uninsdeletekey
Root: HKCR; Subkey: "CrescendoMusicPlayer.flac\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "CrescendoMusicPlayer.flac\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"
Root: HKCR; Subkey: "CrescendoMusicPlayer.flac\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; === Register in Windows Default Apps ===
; This allows Windows 10/11 "Default Apps" to show Crescendo as an option
Root: HKLM; Subkey: "SOFTWARE\RegisteredApplications"; ValueType: string; ValueName: "CrescendoMusicPlayer"; ValueData: "SOFTWARE\Crescendo\Capabilities"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "A modern music player for Windows"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "CrescendoMusicPlayer.mp3"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "CrescendoMusicPlayer.m4a"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "CrescendoMusicPlayer.wav"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "CrescendoMusicPlayer.flac"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "CrescendoMusicPlayer.mp3"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "CrescendoMusicPlayer.m4a"
Root: HKLM; Subkey: "SOFTWARE\Crescendo\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "CrescendoMusicPlayer.mp3"

; === "Open With" Context Menu ===
Root: HKCR; Subkey: "Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCR; Subkey: ".mp3\OpenWithProgids"; ValueType: string; ValueName: "CrescendoMusicPlayer.mp3"; Flags: uninsdeletevalue

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
