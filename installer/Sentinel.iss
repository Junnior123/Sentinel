#ifndef AppVersion
  #define AppVersion "0.6.0"
#endif
#define AppSource "..\artifacts\sentinel-app"

[Setup]
AppId={{AF20DE2F-1545-4B11-9C1D-C8FC25699041}
AppName=Sentinel
AppVersion={#AppVersion}
AppPublisher=Sentinel contributors
DefaultDirName={localappdata}\Programs\Sentinel
DefaultGroupName=Sentinel
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern dynamic
WizardSizePercent=115
OutputDir=..\artifacts
OutputBaseFilename=Sentinel-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Sentinel.exe
AppMutex=Sentinel.Desktop.Running
CloseApplications=no
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}
LicenseFile=..\LICENSE

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Excludes: "*.pdb,docs\*,Sentinel.service.json,Watchblock.service.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; Site-specific configuration may be bundled, but an existing operator setting wins.
Source: "{#AppSource}\Sentinel.service.json"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist uninsneveruninstall
Source: "{#AppSource}\Watchblock.service.json"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist uninsneveruninstall

[Icons]
Name: "{group}\Sentinel"; Filename: "{app}\Sentinel.exe"; WorkingDir: "{app}"
Name: "{group}\Sentinel 시작 안내"; Filename: "{app}\README.md"
Name: "{autodesktop}\Sentinel"; Filename: "{app}\Sentinel.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Sentinel.exe"; Description: "{cm:LaunchProgram,Sentinel}"; Flags: nowait postinstall skipifsilent
