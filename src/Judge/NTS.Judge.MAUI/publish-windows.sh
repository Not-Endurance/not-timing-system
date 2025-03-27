target=net8.0-windows10.0.19041.0
build=Release

dotnet publish \
 -f "$target" \
 --self-contained \
 -property:SolutionDir="$nts/src"

if [ $? -eq 1 ]; then
    echo 'publish failed'
else
    cd "bin/$build/$target"
    explorer .
    cd -
fi
