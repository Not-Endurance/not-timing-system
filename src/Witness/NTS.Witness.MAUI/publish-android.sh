 while getopts "p:" option; do
  case $option in
    p) pass=$OPTARG;;
    \?) echo "Error: Invalid option"
      exit;;
   esac
done

# Require -p argument
if [ -z "$pass" ]; then
  echo "Error: Keystore password (-p) is required to build and sign Android APK"
  exit 1
fi

rm -rf bin/$build/$target

target=net8.0-android
build=Release
keystore_path=C:/Work/secrets/Android/EMS.Apps/EMS.Apps.keystore

dotnet publish \
 -f "$target" \
 -c "$build" \
 -p:AndroidSigningKeyStore="$keystore_path" \
 -p:AndroidSigningKeyPass="$pass" \
 -p:AndroidSigningStorePass="$pass"

if [ $? -eq 1 ]; then
    echo 'publish failed'
else
    cd "bin/$build/$target"
    explorer .
    cd -
fi
