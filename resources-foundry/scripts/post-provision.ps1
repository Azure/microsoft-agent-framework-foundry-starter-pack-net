$REPOSITORY_ROOT = git rev-parse --show-toplevel

pushd $REPOSITORY_ROOT/resources-foundry

$tenantId = azd env get-value AZURE_TENANT_ID
$projectEndpoint = azd env get-value FOUNDRY_PROJECT_ENDPOINT

dotnet user-secrets --file "$REPOSITORY_ROOT/src/MafStarterPack.PromptAgent/create-agent.cs" set "AZURE_TENANT_ID" $tenantId
dotnet user-secrets --file "$REPOSITORY_ROOT/src/MafStarterPack.PromptAgent/create-agent.cs" set "Foundry:Project:Endpoint" $projectEndpoint

dotnet run --file "$REPOSITORY_ROOT/src/MafStarterPack.PromptAgent/create-agent.cs"

popd
