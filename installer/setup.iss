#ifndef AppVersion
#define AppVersion "1.1.1"
#endif
[Setup]
AppId={{C5B5BA5E-39E5-46E5-9875-51FD53120226}
AppName=Zman Lenagen - זמן לנגן
AppVersion={#AppVersion}
DefaultDirName={autopf}\ZmanLenagen
DefaultGroupName=Zman Lenagen
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=ZmanLenagen-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ZmanLenagen.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{commonprograms}\Zman Lenagen\זמן לנגן"; Filename: "{app}\ZmanLenagen.exe"
[UninstallRun]
; Only remove notification registration for the current user, never other users' data.
Filename: "{app}\ZmanLenagen.exe"; Parameters: "--unregister"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterNotifications"
