target=net8.0-windows10.0.19041.0
build=Release
architecture=win10-x64

rm -rf "bin/$build/$target"

dotnet publish \
 -f "$target" \
 --self-contained \
 -property:SolutionDir="$nts/src"

if [ $? -eq 1 ]; then
    echo 'publish failed'
else
    rm -rf "bin/$build/$target/$architecture/publish"
    cd "bin/$build/$target/"
    explorer .
    cd -
fi
