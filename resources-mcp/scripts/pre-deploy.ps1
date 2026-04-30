$REPOSITORY_ROOT = git rev-parse --show-toplevel

pushd $REPOSITORY_ROOT/resources-mcp

azd env set AZURE_AI_PROJECT_ENDPOINT "foundry-project-endpoint"

popd

pushd $REPOSITORY_ROOT/src/MafStarterPack.McpTodo

Copy-Item $REPOSITORY_ROOT/Directory.Build.props .
Copy-Item $REPOSITORY_ROOT/Directory.Packages.props .

popd
