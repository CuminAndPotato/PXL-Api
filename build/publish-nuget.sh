rm -rf ./.nupkg

projects=(
  "./dotnet/Pxl.Ui/Pxl.Ui.fsproj"
  "./dotnet/Pxl.Ui.CSharp/Pxl.Ui.CSharp.fsproj"
  "./dotnet/Pxl/Pxl.fsproj"
)

for project in "${projects[@]}"; do
  echo "Processing $project"

  dotnet restore "$project"
  dotnet build "$project" --configuration Release
  dotnet pack "$project" --configuration Release --output ./.nupkg
done

dotnet nuget push ./.nupkg/*.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json
