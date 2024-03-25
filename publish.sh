remote=true;

while getopts "lv:" option; do
  case $option in
    l) remote=false;;
    v) version=$OPTARG;;
    \?) echo "Error: Invalid option"
      exit;;
   esac
done

# bash string replace is being stupid
# echo "bumping version"
# settings_path="src/Judge/EMS.Judge/judge-settings.json"
# cat $settings_path | sed -r "s/([0-9]+\.[0-9]+\.[0-9]+)/$version/" > temp && mv temp $settings_path
# if $comit; then
#     echo "wtf"
#     git add .
#     git commit -m "Set Judge version to $version"
# fi

echo "clearing release directory..."
output_dir="endurance-judge-$version"
rm -rf "$output_dir"
mkdir "$output_dir"
cd "$output_dir"

echo "release directory cleared."
echo "======================================================================================================================================="
echo "DOTNET PUBLISH"
if ! dotnet publish  -c Release -o . ../src/Judge/EMS.Judge/EMS.Judge.csproj
then
  exit 1
fi

echo "======================================================================================================================================="
echo "opening explorer..."
mkdir logs
touch logs/witness-events.log
touch logs/witness-event-errors.log
mkdir logs-clients
cd ..
explorer "$output_dir"
echo "publish complete."
