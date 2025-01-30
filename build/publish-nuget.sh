rm -rf ./.nupkg

projects=(
  "./dotnet/fsharp/Pxl.Ui/Pxl.Ui.fsproj"
  "./dotnet/Pxl/Pxl.fsproj"
)

for project in "${projects[@]}"; do
  echo "Processing $project"

  dotnet restore "$project"
  dotnet build "$project" --configuration Release
  dotnet pack "$project" --configuration Release --output ./.nupkg
done

dotnet nuget push ./.nupkg/*.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json
