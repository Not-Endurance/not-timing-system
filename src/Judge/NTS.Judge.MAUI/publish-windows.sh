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

# Create desktop shortcut using PowerShell
# Convert paths to Windows-style
win_exe_path=$(cd "$(dirname "$exe_path")" && pwd -W)\\$(basename "$exe_path")
win_icon_path=$(cd "$(dirname "$ico_path")" && pwd -W)\\$(basename "$ico_path")
win_work_dir=$(cd "$(dirname "$exe_path")" && pwd -W)

echo "📎 Creating desktop shortcut..."
powershell.exe -Command "
\$WshShell = New-Object -ComObject WScript.Shell;
\$Shortcut = \$WshShell.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\\$shortcut_name');
\$Shortcut.TargetPath = \"$win_exe_path\";
\$Shortcut.IconLocation = \"$win_icon_path\";
\$Shortcut.WorkingDirectory = \"$win_work_dir\";
\$Shortcut.Save();
"
echo "✅ Shortcut created on desktop."

# Open publish directory in Explorer
cd "$publish_dir"
explorer .
cd -