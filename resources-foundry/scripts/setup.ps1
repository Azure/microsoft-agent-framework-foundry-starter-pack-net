$REPOSITORY_ROOT = git rev-parse --show-toplevel

pushd $REPOSITORY_ROOT/resources-foundry

$endpoint = azd env get-value AZURE_AI_PROJECT_ENDPOINT
$deploymentName = ((azd env get-value AI_PROJECT_DEPLOYMENTS).Replace('\', '') | ConvertFrom-Json).name
$agentName = azd env get-value AGENT_TODO_AGENT_NAME
$agentVersion = azd env get-value AGENT_TODO_AGENT_VERSION
$connectionString = "Endpoint=$endpoint/;DeploymentId=$deploymentName;AgentName=$agentName;AgentVersion=$agentVersion"

dotnet user-secrets --project "$REPOSITORY_ROOT/src/MafStarterPack.AppHost" set "ConnectionStrings:foundry" $connectionString

popd
