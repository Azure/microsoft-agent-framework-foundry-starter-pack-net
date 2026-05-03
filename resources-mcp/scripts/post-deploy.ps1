$REPOSITORY_ROOT = git rev-parse --show-toplevel

pushd $REPOSITORY_ROOT/resources-mcp

$tenantId = azd env get-value AZURE_TENANT_ID
$mcpTodoFqdn = azd env get-value AZURE_RESOURCE_MCP_TODO_FQDN

dotnet user-secrets --project "$REPOSITORY_ROOT/src/MafStarterPack.HostedAgent" set "AZURE_TENANT_ID" $tenantId
dotnet user-secrets --project "$REPOSITORY_ROOT/src/MafStarterPack.HostedAgent" set "AZURE_RESOURCE_MCP_TODO_FQDN" $mcpTodoFqdn

popd
