using Aspire.Hosting.Foundry;

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var config = builder.Configuration;

var mcpTodo = builder.AddProject<MafStarterPack_McpTodo>("mcp-todo");

var foundry = builder.AddFoundry("foundry");
var starterProject = foundry.AddProject("starter-project");
var gpt5Mini = starterProject.AddModelDeployment("gpt-5-mini", FoundryModel.OpenAI.Gpt5Mini);

var hostedAgent = builder.AddProject<MafStarterPack_HostedAgent>("hosted-agent")
                         .WithHttpEndpoint(targetPort: 5388)
                         .WithReference(starterProject)
                         .WithReference(gpt5Mini)
                         .WithReference(mcpTodo)
                         .WithEnvironment("AZURE_TENANT_ID", config["Azure:TenantId"] ?? "")
                         .WaitFor(starterProject)
                         .WaitFor(gpt5Mini)
                         .WaitFor(mcpTodo)
                         .PublishAsHostedAgent(starterProject);

var agent = builder.AddProject<MafStarterPack_Agent>("agent")
                   .WithReference(starterProject)
                   .WithReference(gpt5Mini)
                   .WaitFor(hostedAgent);

var webUI = builder.AddProject<MafStarterPack_WebUI>("webui")
                   .WithExternalHttpEndpoints()
                   .WithReference(agent)
                   .WaitFor(agent);

await builder.Build().RunAsync();
