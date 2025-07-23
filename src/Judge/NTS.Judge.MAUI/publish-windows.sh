#!/bin/bash

# Configuration
target="net8.0-windows10.0.19041.0"
build="Release"
architecture="win10-x64"
icon_path="appicon.ico"
rcedit_path="rcedit.exe"
app_exe="NTS.Judge.MAUI.exe"
shortcut_name="NTS Judge.lnk"

# Clean previous build
rm -rf "bin/$build/$target"

# Set path to your .csproj
CSPROJ="./NTS.Judge.MAUI.csproj"
# Extract ApplicationDisplayVersion using grep and sed
APP_DISPLAY_VERSION=$(grep '<ApplicationDisplayVersion>' "$CSPROJ" | sed -E 's/.*<ApplicationDisplayVersion>(.*)<\/ApplicationDisplayVersion>.*/\1/')
APP_VERSION=$(grep '<ApplicationVersion>' "$CSPROJ" | sed -E 's/.*<ApplicationVersion>(.*)<\/ApplicationVersion>.*/\1/')
# Write to app-version.txt
echo "[App]" > app-version.txt
echo "DisplayVersion=$APP_DISPLAY_VERSION" > app-version.txt
echo "ApplicationVersion=$APP_VERSION" >> app-version.txt


# Publish the app
dotnet publish \
 -f "$target" \
 --self-contained \
 -c $build \
 -property:SolutionDir="$nts/src"

if [ $? -ne 0 ]; then
    echo '❌ Publish failed'
    exit 1
fi

# Define publish directory and paths
publish_dir="bin/$build/$target/$architecture/publish"
exe_path="$(pwd)/$publish_dir/$app_exe"
ico_path="$publish_dir/$icon_path"

# Apply icon with rcedit if available
if [[ -f "$ico_path" ]]; then
    echo "🔧 Applying icon using rcedit..."
    rcedit "$exe_path" --set-icon "$ico_path"
    echo "✅ Icon applied."
else
    echo "⚠️ Skipping rcedit: icon or rcedit.exe not found."
fi

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer-script.iss

# Open publish directory in Explorer
cd "$publish_dir"
explorer .  
cd -