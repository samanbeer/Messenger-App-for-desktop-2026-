#define MyAppName "MessengeR"
#define MyAppVersion "2026.1.0"
#define MyAppPublisher "MessengeR"
#define MyAppExeName "MessengeR.exe"

[Setup]
AppId={{5E305C9A-594E-4B07-A961-A79B11394B26}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=D:\!Documents\VSCode\Messenger\output
OutputBaseFilename=MessengeR_Setup
SetupIconFile=D:\!Documents\VSCode\Messenger\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes

[Languages]
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Vytvořit zástupce na ploše"; GroupDescription: "Další možnosti:"; Flags: unchecked
Name: "startmenuicon"; Description: "Vytvořit zástupce v nabídce Start"; GroupDescription: "Další možnosti:"

[Files]
Source: "D:\!Documents\VSCode\Messenger\publish_standalone\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\!Documents\VSCode\Messenger\publish_standalone\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Spustit aplikaci {#MyAppName}"; Flags: nowait postinstall skipifsilent
