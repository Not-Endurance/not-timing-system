#define FileHandle
#define FileLine
#define AppVersion "unknown"

#sub ProcessLine
  #define FileLine FileRead(FileHandle)
  #if Pos("DisplayVersion=", FileLine) > 0
    #define AppVersion Copy(FileLine, Pos("=", FileLine) + 1, Len(FileLine))
    #pragma message "Detected version: " + AppVersion
  #endif
#endsub

#for {FileHandle = FileOpen("app-version.txt"); FileHandle && !FileEof(FileHandle); ""} ProcessLine

#if FileHandle
  #expr FileClose(FileHandle)
#endif

[Setup]
AppName=NTS Judge
AppVersion={#AppVersion}
DefaultDirName={commonpf}\NTS Judge
DefaultGroupName=NTS Judge
UninstallDisplayIcon={app}\NTS Judge .exe
OutputBaseFilename=NTS Judge Installer
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
Source: "bin\Release\net8.0-windows10.0.19041.0\win10-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NTS Judge"; Filename: "{app}\NTS Judge.exe"
Name: "{commondesktop}\NTS Judge"; Filename: "{app}\Judge.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Registry]
Root: HKLM; Subkey: "Software\NotACompany\NTS Judge"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Code]
function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'Software\NotACompany\NTS Judge', 'Version', OldVersion) then
  begin
    if OldVersion < '{#AppVersion}' then
    begin
      Result := MsgBox('An older version (' + OldVersion + ') is installed. Do you want to update it?', mbConfirmation, MB_YESNO) = IDYES;
    end
    else
    begin
      MsgBox('The same or newer version is already installed.', mbInformation, MB_OK);
      Result := False;
    end;
  end
  else
  begin
    Result := True;
  end;
end;
