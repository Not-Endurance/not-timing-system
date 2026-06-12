#!/bin/bash

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
solution_dir="$(cd "$script_dir/../../.." && pwd)"
cd "$script_dir"

# Configuration
target="net8.0-windows10.0.19041.0"
build="Release"
platform="x64"
runtime_identifier="win-x64"
icon_path="appicon.ico"
rcedit_path="rcedit.exe"
app_exe="NTS.Judge.MAUI.exe"
shortcut_name="NTS Judge.lnk"
target_environment="${1:-${NTS_TARGET_ENVIRONMENT:-Production}}"

case "${target_environment,,}" in
    production)
        target_environment="Production"
        ;;
    staging)
        target_environment="Staging"
        ;;
    development)
        echo "❌ Development is not a supported publish target. Use Staging or Production."
        exit 1
        ;;
    *)
        echo "❌ Unknown publish target '$target_environment'. Use Staging or Production."
        exit 1
        ;;
esac

publish_dir="bin/$platform/$build/$target/$runtime_identifier/publish/$target_environment"

echo "Publishing NTS Judge for $target_environment..."

# Clean previous build
rm -rf "$publish_dir"

# Publish the app
dotnet publish \
 -f "$target" \
 -r "$runtime_identifier" \
 --self-contained \
 -c "$build" \
 -p:Platform="$platform" \
 -property:SolutionDir="$solution_dir/" \
 -p:NtsTargetEnvironment="$target_environment" \
 -o "$publish_dir"

if [ $? -ne 0 ]; then
    echo '❌ Publish failed'
    exit 1
fi

# Define publish paths
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
