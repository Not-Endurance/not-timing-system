#!/bin/bash
set -euo pipefail

# --- Configuration ---
TFM="net8.0-windows10.0.19041.0"   # target framework
CONFIG="Release"
RID="win-x64"                    # runtime identifier
ICON_FILE="appicon.ico"
RCEDIT_EXE="C:\Tools\\rcedit.exe"            # or full path if needed
APP_EXE="NTS.Judge.MAUI.exe"
ISS="installer-script.iss"
ISCC="C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe"  # Inno compiler
CSPROJ="./NTS.Judge.MAUI.csproj"

# --- Clean previous build for this TFM/RID/Config ---
rm -rf "bin/$CONFIG/$TFM/$RID"

# --- Extract versions from csproj (optional; see notes below) ---
APP_DISPLAY_VERSION=$(grep '<ApplicationDisplayVersion>' "$CSPROJ" | sed -E 's/.*<ApplicationDisplayVersion>(.*)<\/ApplicationDisplayVersion>.*/\1/')
APP_VERSION=$(grep '<ApplicationVersion>' "$CSPROJ" | sed -E 's/.*<ApplicationVersion>(.*)<\/ApplicationVersion>.*/\1/')

# --- Write app-version.txt (append properly, don’t overwrite mid-file) ---
{
  echo "[App]"
  echo "DisplayVersion=$APP_DISPLAY_VERSION"
  echo "ApplicationVersion=$APP_VERSION"
} > app-version.txt

# --- Publish (must include RID when --self-contained) ---
dotnet publish "$CSPROJ" \
  -f "$TFM" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true

# --- Paths after publish ---
publish_dir="bin/$CONFIG/$TFM/$RID/publish"
exe_path="$(pwd)/$publish_dir/$APP_EXE"
ico_path="$publish_dir/$ICON_FILE"

if [[ ! -f "$exe_path" ]]; then
  echo "❌ Built app not found at: $exe_path"
  exit 1
fi

# --- Apply icon (only if both icon and rcedit exist) ---
if [[ -f "$ico_path" && -f "$RCEDIT_EXE" ]]; then
  echo "🔧 Applying icon using rcedit..."
  "$RCEDIT_EXE" "$exe_path" --set-icon "$ico_path"
  echo "✅ Icon applied."
else
  echo "⚠️ Skipping rcedit: missing $ico_path or $RCEDIT_EXE"
fi

# --- Compile installer (let Inno read version from EXE; see .iss snippet below) ---
"$ISCC" "$ISS"

# --- Open publish directory in Explorer ---
( cd "$publish_dir" && explorer . )