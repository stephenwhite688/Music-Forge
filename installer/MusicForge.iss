; MusicForge installer - Inno Setup 6
; Packages the self-contained win-x64 publish output (MusicForge.exe + Assets).
;
; Build steps:
;   1. dotnet publish src\UI\MusicForge.UI.csproj -c Release -r win-x64 ^
;        --self-contained true -p:PublishSingleFile=true ^
;        -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
;   2. ISCC installer\MusicForge.iss        (from repo root)
;
; Output: installer\Output\MusicForge-Setup.exe

#define AppName      "MusicForge"
#define AppVersion   "1.0.0"
#define AppPublisher "MusicForge"
#define AppComments  "Music player with 31-band EQ, visualisers, internet radio and network speaker output"
#define AppExeName   "MusicForge.exe"
#define PublishDir   "..\publish\win-x64"

[Setup]
AppId={{8F2E4A1B-6C3D-4E5F-9A7B-2D1C0E4F6A8B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppComments={#AppComments}
LicenseFile=License.rtf
InfoBeforeFile=InfoBefore.rtf
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=MusicForge-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
UninstallDisplayIcon={app}\{#AppExeName}

; Branding
SetupIconFile=Wizard\Setup.ico
WizardImageFile=Wizard\Wizard.bmp
WizardSmallImageFile=Wizard\Small.bmp
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) 2025 {#AppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"
Name: "taskbaricon"; Description: "Pin to &taskbar"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\visualisers\*"; DestDir: "{app}\visualisers"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: taskbaricon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent
