#ifndef Architecture
  #define Architecture "x64"
#endif
#ifndef SourceDir
  #error SourceDir must point to a self-contained TaskbarLyriz publish directory.
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "TaskbarLyriz"
#define AppExeName "TaskbarLyriz.App.exe"
#define AppPublisher "TaskbarLyriz"
#define AppUrl "https://github.com/rathodpratham-dev/TaskbarLyriz"

[Setup]
AppId={{80D08AC8-8884-4D26-9A25-ABAD8047D249}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
DefaultDirName={localappdata}\Programs\TaskbarLyriz
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.17763
#if Architecture == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif Architecture == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x86compatible
#endif
OutputBaseFilename=TaskbarLyriz-Setup-{#Architecture}-{#AppVersion}
SetupIconFile=..\src\TaskbarLyriz.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
DirExistsWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
