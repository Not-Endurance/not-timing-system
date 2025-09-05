#define AppExe ".\\bin\\Release\\net8.0-windows10.0.19041.0\\win-x64\\publish\\NTS.Judge.MAUI.exe"
#define APP_ID "{{1E77FD41-17C0-4D75-A08F-00D4390F5485}}"

#pragma message "AppExe: " + AppExe
#if FileExists(AppExe)
  #pragma message "AppExe exists: YES"
#else
  #pragma message "AppExe exists: NO"
#endif

; ---- Display version (for UI) ----
#define AppDisplayVersionRaw GetStringFileInfo(AppExe, "ProductVersion")
#if AppDisplayVersionRaw == ""
  #define AppDisplayVersionRaw GetVersionNumbersString(AppExe)
  #if AppDisplayVersion == ""
    #define AppDisplayVersionRaw "1.0.0"
  #endif
#endif
#define AppDisplayVersionQuoted AddQuotes(AppDisplayVersionRaw)

; ---- Numeric version (for VersionInfoVersion) ----
#define AppNumericVersionRaw GetVersionNumbersString(AppExe)
#if AppNumericVersionRaw == ""
  #define AppNumericVersionRaw "1.0.0.0"
#endif

#if Pos("+", AppDisplayVersionRaw) > 0
  #define AppDisplayVersion Copy(AppDisplayVersionRaw, 1, Pos("+", AppDisplayVersionRaw) - 1)
  #define AppBuildMeta      Copy(AppDisplayVersionRaw, Pos("+", AppDisplayVersionRaw) + 1, Len(AppDisplayVersionRaw))
  #define AppBuildMetaShort Copy(AppBuildMeta, 1, 7)    ; optional
#else
  #define AppDisplayVersion AppDisplayVersionRaw
  #define AppBuildMeta ""
  #define AppBuildMetaShort ""
#endif

#pragma message "Display (clean): " + AppDisplayVersion
#pragma message "Numeric:        " + AppNumericVersion
#pragma message "BuildMeta:      " + AppBuildMetaShort

#pragma message "Display: " + AppDisplayVersion
#pragma message "Numeric:  " + AppNumericVersionRaw
; ---------- Setup ----------
[Setup]
; Generate once (Tools → Generate GUID) and KEEP it stable for upgrades:
AppId={#APP_ID}
AppName=NTS Judge
AppVersion={#AppDisplayVersion}
AppVerName=NTS Judge {#AppDisplayVersion}
VersionInfoVersion={#AppNumericVersionRaw}
DefaultDirName={commonpf}\NTS Judge
DefaultGroupName=NTS Judge
UninstallDisplayIcon={app}\NTS.Judge.MAUI.exe
OutputBaseFilename=NTS-Judge-{#AppDisplayVersion}-Installer
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: ".\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\NTS Judge"; Filename: "{app}\NTS.Judge.MAUI.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\NTS Judge"; Filename: "{app}\NTS.Judge.MAUI.exe"; Tasks: desktopicon; WorkingDir: "{app}"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Messages]
BeveledLabel=Installing NTS Judge {#AppDisplayVersion}

[Code]
function CompareVerStrings(const A, B: string): Integer;
var i, pa, pb, va, vb: Integer; sa, sb, aa, bb: string;
begin
  Result := 0; sa := A; sb := B;
  for i := 1 to 4 do begin
    pa := Pos('.', sa); if pa = 0 then pa := Length(sa) + 1;
    pb := Pos('.', sb); if pb = 0 then pb := Length(sb) + 1;
    aa := Copy(sa, 1, pa-1); bb := Copy(sb, 1, pb-1);
    try va := StrToInt(aa) except va := 0 end;
    try vb := StrToInt(bb) except vb := 0 end;
    if va <> vb then begin if va < vb then Result := -1 else Result := 1; Exit; end;
    Delete(sa, 1, pa); Delete(sb, 1, pb);
    if (sa = '') and (sb = '') then Break;
  end;
end;

function InitializeSetup(): Boolean;
var
  UninstKey, InstalledVer, UninstallCmd: string;
  Res: Integer;
begin
  Result := True;
  UninstKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + '{#APP_ID}' + '_is1';

  if RegQueryStringValue(HKLM, UninstKey, 'DisplayVersion', InstalledVer) then
  begin
    case CompareVerStrings(InstalledVer, '{#AppDisplayVersion}') of
      -1: begin
            Res := MsgBox(
              'A previous version (' + InstalledVer + ') is installed.'#13#13 +
              'Update to {#AppDisplayVersion}?', mbConfirmation, MB_YESNO);
            if Res <> IDYES then begin Result := False; Exit; end;
          end;
       0, 1: begin
            if RegQueryStringValue(HKLM, UninstKey, 'UninstallString', UninstallCmd) then
            begin
              Res := MsgBox(
                'Version ' + InstalledVer + ' is already installed.'#13#13 +
                'Uninstall it now?', mbConfirmation, MB_YESNO);
              if Res = IDYES then
              begin
                if ShellExec('', UninstallCmd, '', '', SW_SHOWNORMAL,
                             ewWaitUntilTerminated, Res) then
                  Result := False  // exit; user can rerun this installer afterward
                else begin
                  MsgBox('Could not start the uninstaller.', mbError, MB_OK);
                  Result := False;
                end;
              end
              else Result := False;
            end
            else begin
              MsgBox('A same or newer version is already installed.', mbInformation, MB_OK);
              Result := False;
            end;
          end;
    end;
  end;
end;