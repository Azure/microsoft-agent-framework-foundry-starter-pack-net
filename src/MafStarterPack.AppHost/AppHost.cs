using Aspire.Hosting.Foundry;

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var config = builder.Configuration;

var foundry = builder.AddFoundry("foundry");
var project = foundry.AddProject("project");
var chat = project.AddModelDeployment("chat", FoundryModel.OpenAI.Gpt5Mini);

var mcptodo = builder.AddProject<MafStarterPack_McpTodo>("mcp-todo");

// var agenttodo = builder.AddProject<MafStarterPack_HostedAgent>("agent-todo")
//                        .WithReference(chat)
//                        .WithReference(mcptodo)
//                        .PublishAsHostedAgent(project);

var agenttodo = builder.AddExecutable(
                           name: "agent-todo",
                           command: "dotnet",
                           workingDirectory: "../MafStarterPack.HostedAgent",
                           args: ["run", "--project", "MafStarterPack.HostedAgent.csproj", "--no-launch-profile"])
                       .WithReference(project)
                       .WithReference(chat)
                       .WithReference(mcptodo)
                       .WithEnvironment("AZURE_TENANT_ID", config["Azure:TenantId"] ?? "")
                       .PublishAsDockerFile(c => c.WithDockerfile("../MafStarterPack.HostedAgent"))
                       .PublishAsHostedAgent(project)
                       .WaitFor(chat)
                       .WaitFor(mcptodo);

var agent = builder.AddProject<MafStarterPack_Agent>("agent")
                   .WithReference(project)
                   .WithReference(chat)
                   .WaitFor(chat);

var webUI = builder.AddProject<MafStarterPack_WebUI>("webui")
                   .WithExternalHttpEndpoints()
                   .WithReference(agent)
                   .WaitFor(agent);

await builder.Build().RunAsync();

// internal class FoundryResource(string name) : Resource(name), IResourceWithEnvironment
// {
//     public string? ProjectEndpoint { get; set; }
//     public string? Model { get; set; }
//     public string? AgentName { get; set; }
//     public string? AgentVersion { get; set; }

//     public List<string> GetMissingConfigKeys()
//     {
//         var placeholders = new[] { "{FOUNDRY_NAME}", "{FOUNDRY_PROJECT_NAME}" };
//         var missing = new List<string>();
//         if (string.IsNullOrWhiteSpace(ProjectEndpoint) || placeholders.Any(p => ProjectEndpoint.Contains(p)))
//         {
//             missing.Add("Foundry:Project:Endpoint");
//         }
//         if (string.IsNullOrWhiteSpace(Model))
//         {
//             missing.Add("Foundry:Project:Model");
//         }
//         if (string.IsNullOrWhiteSpace(AgentName))
//         {
//             missing.Add("Foundry:Project:Agent:Name");
//         }
//         if (string.IsNullOrWhiteSpace(AgentVersion))
//         {
//             missing.Add("Foundry:Project:Agent:Version");
//         }
//         return missing;
//     }
// }

// internal static class FoundryResourceExtensions
// {
//     internal static IResourceBuilder<FoundryResource> AddFoundry(this IDistributedApplicationBuilder builder, string name)
//     {
//         var config = builder.Configuration;

//         var resource = new FoundryResource(name)
//         {
//             ProjectEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"],
//             Model = config["FOUNDRY_MODEL_DEPLOYMENT_NAME"],
//             AgentName = config["TODO_AGENT_NAME"],
//             AgentVersion = config["TODO_AGENT_VERSION"],
//         };

//         var resourceBuilder = builder.AddResource(resource)
//                                      .ExcludeFromManifest();
//         resourceBuilder.OnInitializeResource((res, e, ct) =>
//         {
//             var missing = res.GetMissingConfigKeys();
//             if (missing.Count > 0)
//             {
//                 return e.Notifications.PublishUpdateAsync(res, state => state with
//                 {
//                     State = new ResourceStateSnapshot(
//                         $"Missing config: {string.Join(", ", missing)}",
//                         KnownResourceStateStyles.Error)
//                 });
//             }

//             return e.Notifications.PublishUpdateAsync(res, state => state with
//             {
//                 State = new ResourceStateSnapshot(
//                     "Running",
//                     KnownResourceStateStyles.Success)
//             });
//         });

//         return resourceBuilder;
//     }

//     internal static IResourceBuilder<T> WithReference<T>(this IResourceBuilder<T> builder, IResourceBuilder<FoundryResource> foundry)
//         where T : IResourceWithEnvironment
//     {
//         var resource = foundry.Resource;

//         return builder.WithEnvironment("Foundry__Project__Endpoint", resource.ProjectEndpoint ?? "")
//                       .WithEnvironment("Foundry__Project__Model", resource.Model ?? "")
//                       .WithEnvironment("Foundry__Project__Agent__Name", resource.AgentName ?? "")
//                       .WithEnvironment("Foundry__Project__Agent__Version", resource.AgentVersion ?? "");
//     }
// }