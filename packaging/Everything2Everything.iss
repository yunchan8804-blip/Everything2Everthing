; Everything2Everything Inno Setup Script
#ifndef MyAppVersion
#define MyAppVersion "1.0.8"
#endif

#define MyAppName "Everything2Everything"
#define MyAppPublisher "YunChan"
#define MyAppURL "https://git.chanpaca.net/yunchan/Everything2Everything"
#define MyAppExeName "Everything2Everything.exe"

[Setup]
AppId={{D3E4F5A6-B7C8-4901-2345-6789ABCDEF01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\Everything2Everything.App\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=Everything2Everything-{#MyAppVersion}-Setup-x64
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 설치 완료 후 윈도우 우클릭 탐색기 메뉴 자동 등록
Filename: "{app}\{#MyAppExeName}"; Parameters: "register"; Flags: runhidden
; 앱 실행 (사일런트 설치가 아닐 때만)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 프로그램 삭제 전 윈도우 우클릭 탐색기 메뉴 자동 해제
Filename: "{app}\{#MyAppExeName}"; Parameters: "unregister"; Flags: runhidden
