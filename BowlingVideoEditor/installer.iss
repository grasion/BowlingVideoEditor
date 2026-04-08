[Setup]
AppName=볼링 영상 편집기
AppVersion=1.0.1
AppPublisher=BowlingVideoEditor
AppPublisherURL=https://github.com/grasion/BowlingVideoEditor
DefaultDirName={autopf}\BowlingVideoEditor
DefaultGroupName=볼링 영상 편집기
OutputDir=installer_output
OutputBaseFilename=BowlingVideoEditor_Setup_v1.0.1
Compression=lzma2
SolidCompression=yes
SetupIconFile=Resources\icon.ico
UninstallDisplayIcon={app}\BowlingVideoEditor.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 옵션:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\볼링 영상 편집기"; Filename: "{app}\BowlingVideoEditor.exe"
Name: "{group}\볼링 영상 편집기 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\볼링 영상 편집기"; Filename: "{app}\BowlingVideoEditor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BowlingVideoEditor.exe"; Description: "볼링 영상 편집기 실행"; Flags: nowait postinstall skipifsilent
