[Setup]
AppName=蹂쇰쭅 ?곸긽 ?몄쭛湲?
AppVersion=1.0.0
AppPublisher=BowlingVideoEditor
AppPublisherURL=https://github.com/YOUR_GITHUB_USERNAME/BowlingVideoEditor
DefaultDirName={autopf}\BowlingVideoEditor
DefaultGroupName=蹂쇰쭅 ?곸긽 ?몄쭛湲?
OutputDir=installer_output
OutputBaseFilename=BowlingVideoEditor_Setup_v1.0.0
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
Name: "desktopicon"; Description: "諛뷀깢?붾㈃??諛붾줈媛湲?留뚮뱾湲?; GroupDescription: "異붽? ?듭뀡:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\蹂쇰쭅 ?곸긽 ?몄쭛湲?; Filename: "{app}\BowlingVideoEditor.exe"
Name: "{group}\蹂쇰쭅 ?곸긽 ?몄쭛湲??쒓굅"; Filename: "{uninstallexe}"
Name: "{autodesktop}\蹂쇰쭅 ?곸긽 ?몄쭛湲?; Filename: "{app}\BowlingVideoEditor.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BowlingVideoEditor.exe"; Description: "蹂쇰쭅 ?곸긽 ?몄쭛湲??ㅽ뻾"; Flags: nowait postinstall skipifsilent
