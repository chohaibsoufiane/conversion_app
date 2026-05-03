[Setup]
AppName=Eezy Pro
AppVersion=1.0
AppPublisher=Eezy Pro
DefaultDirName={autopf}\Eezy Pro
DefaultGroupName=Eezy Pro
UninstallDisplayIcon={app}\ConversionApp.exe
OutputDir=out
OutputBaseFilename=EezyPro_Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=compiler:SetupClassicIcon.ico

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; App core files
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; LibreOffice Portable engine
Source: "..\_copies_\LibreOfficePortable\*"; DestDir: "{app}\_copies_\LibreOfficePortable"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Eezy Pro"; Filename: "{app}\ConversionApp.exe"
Name: "{group}\{cm:UninstallProgram,Eezy Pro}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Eezy Pro"; Filename: "{app}\ConversionApp.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ConversionApp.exe"; Description: "{cm:LaunchProgram,Eezy Pro}"; Flags: nowait postinstall skipifsilent
