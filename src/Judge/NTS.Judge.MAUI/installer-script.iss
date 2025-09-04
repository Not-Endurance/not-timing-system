#define AppExe ".\\bin\\Release\\net8.0-windows10.0.19041.0\\win-x64\\publish\\NTS.Judge.MAUI.exe"

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
AppId={{YOUR-STATIC-GUID-HERE-DO-NOT-CHANGE}}
AppName=NTS Judge
AppVersion={#AppDisplayVersion}
AppVerName=NTS Judge {#AppDisplayVersion}
VersionInfoVersion={#AppNumericVersionRaw}
DefaultDirName={commonpf}\NTS Judge
DefaultGroupName=NTS Judge
UninstallDisplayIcon={app}\NTS.Judge.MAUI.exe
OutputBaseFilename=NTS-Judge-{#AppDisplayVersion}-setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: ".\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\NTS Judge"; Filename: "{app}\NTS.Judge.MAUI.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\NTS Judge"; Filename: "{app}\NTS.Judge.MAUI.exe"; Tasks: desktopicon; WorkingDir: "{app}"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Registry]
Root: HKLM; Subkey: "Software\NotACompany\NTS Judge"; ValueType: string; ValueName: "Version"; ValueData: {#AppDisplayVersionQuoted}; Flags: uninsdeletekey

; [Code]
; You generally don't need custom version blocking; keeping AppId stable lets Inno upgrade in-place.
; If you still want to warn on downgrade, implement a proper version compare (not simple string compare).