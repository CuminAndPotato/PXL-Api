projects=(
  "./node/typescript/pxl-ui"
)

for project in "${projects[@]}"; do
  echo "Processing $project"

  pushd "$project"
  npm i
  npm run build

  npm pack

  echo "//registry.npmjs.org/:_authToken=$NPM_API_KEY" > .npmrc
  npm publish
  rm .npmrc

  popd
done

# todo
# dotnet nuget push ./.nupkg/*.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json
