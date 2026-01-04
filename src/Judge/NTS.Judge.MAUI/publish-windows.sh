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

# Open publish directory in Explorer
cd "$publish_dir"
explorer .
cd -