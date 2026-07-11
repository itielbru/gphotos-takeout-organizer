; Inno Setup script for the GPhotos Takeout Organizer installer.
; Compiled by the Release workflow with:
;   ISCC.exe /DAppVersion=<x.y.z> installer\setup.iss
; It packages the App's self-contained publish folder (publish\app) plus the
; single-file CLI (publish\cli\gptakeout.exe) into one setup EXE. Per-user
; install (no admin / UAC prompt), Start Menu shortcut, optional desktop icon,
; and a standard uninstaller.
;
; Why an installer at all: the App cannot ship as a single EXE (WinUI 3
; PublishSingleFile crashes on startup — microsoft/WindowsAppSDK#2597), so the
; alternative is a zip the user must extract manually. The installer does the
; extraction + shortcuts for them.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "GPhotos Takeout Organizer"
#define AppExeName "GPhotosTakeout.App.exe"
#define AppPublisher "itielbru"
#define AppURL "https://github.com/itielbru/gphotos-takeout-organizer"

[Setup]
; Fixed GUID identifying this product across versions (upgrades reuse it).
AppId={{8B1F4E7A-3C52-4D9B-9E6F-2A70D1C5B8E4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
; Per-user install: no admin rights, no UAC prompt. {autopf} resolves to
; %LocalAppData%\Programs under lowest privileges.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=GPhotosTakeout-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The App's entire self-contained publish folder.
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; The CLI, installed alongside the App so one setup covers both.
Source: "..\publish\cli\gptakeout.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
