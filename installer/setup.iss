#define MyAppName "DS 电池指示器"
#define MyAppEnglishName "DsBatteryIndicator"
#define MyAppVersion "1.0.6"
#define MyAppPublisher "DS Battery Indicator"
#define MyAppExeName "DsBatteryIndicator.exe"

[Setup]
AppId={{3E8B7C2A-1F9D-4A3E-B6C5-8D2A9E1F4B7C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppEnglishName}
DefaultGroupName={#MyAppName}
OutputDir=..\publish\installer
OutputBaseFilename=DS-Battery-Indicator-Setup
SetupIconFile=..\DsBatteryIndicator\DsBatteryIndicator.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他快捷方式:"
Name: "startup"; Description: "开机自启"; GroupDescription: "其他快捷方式:"

[Files]
Source: "..\publish\self-contained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
