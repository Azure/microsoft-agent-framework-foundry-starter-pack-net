using Aspire.Hosting.Foundry;

using Azure.Provisioning.Authorization;
using Azure.Provisioning.CognitiveServices;
using Azure.Provisioning.Expressions;

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var config = builder.Configuration;

var mcpTodo = builder.AddProject<MafStarterPack_McpTodo>("mcp-todo");

var foundry = builder.AddFoundry("foundry");
var starterProject = foundry.AddProject("starter-project")
                            .ConfigureInfrastructure(infra =>
                            {
                                var project = infra.GetProvisionableResources().OfType<CognitiveServicesProject>().Single();
                                var foundryAccount = foundry.Resource.AddAsExistingResource(infra);

                                var cognitiveServiceUserRoleAssignment = foundryAccount.CreateRoleAssignment(
                                    role: CognitiveServicesBuiltInRole.CognitiveServicesUser,
                                    principalType: RoleManagementPrincipalType.ServicePrincipal,
                                    principalId: project.Identity.PrincipalId);
                                cognitiveServiceUserRoleAssignment.Name = BicepFunction.CreateGuid(
                                    foundryAccount.Id,
                                    project.Id,
                                    cognitiveServiceUserRoleAssignment.RoleDefinitionId);

                                infra.Add(cognitiveServiceUserRoleAssignment);
                            });
var gpt5Mini = starterProject.AddModelDeployment("gpt-5-mini", FoundryModel.OpenAI.Gpt5Mini);

var hostedAgent = builder.AddProject<MafStarterPack_HostedAgent>("hosted-agent")
                        //  .WithHttpsEndpoint(targetPort: 45388)
                        //  .WithHttpEndpoint(targetPort: 5388)
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
                   .WithReference(hostedAgent)
                   .WithEnvironment("AZURE_TENANT_ID", config["Azure:TenantId"] ?? "")
                   .WaitFor(starterProject)
                   .WaitFor(gpt5Mini)
                   .WaitFor(hostedAgent);

var webUI = builder.AddProject<MafStarterPack_WebUI>("webui")
                   .WithExternalHttpEndpoints()
                   .WithReference(agent)
                   .WaitFor(agent);

await builder.Build().RunAsync();
