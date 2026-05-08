using Azure.AI.Projects;
using Azure.Identity;

using MafStarterPack.HostedAgent.Extensions;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var builder = AgentHost.CreateBuilder(args);

Console.WriteLine("Starting Hosted Agent...");

var config = builder.Configuration;

var (projectEndpoint, deploymentName) = config.GetFoundryConnectionDetails("starter-project", "gpt-5-mini");
var tenantId = config["AZURE_TENANT_ID"] ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is not set.");

Console.WriteLine($"Using Azure OpenAI Endpoint: {projectEndpoint}");
Console.WriteLine($"Using Azure OpenAI Deployment Name: {deploymentName}");
Console.WriteLine($"Using Azure Tenant ID: {tenantId}");

builder.WebApplicationBuilder.AddServiceDefaults();

builder.Services.AddHttpClient("mcp-todo", client =>
{
    client.BaseAddress = new Uri("https+http://mcp-todo");
});

builder.Services.AddKeyedSingleton<McpClient>("mcp-todo", (sp, obj) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                       .CreateClient("mcp-todo");
    var endpoint = builder.WebApplicationBuilder.Environment.IsDevelopment() == true
                 ? $"{httpClient.BaseAddress!.ToString().Replace("https+", string.Empty).TrimEnd('/')}"
                 : $"{httpClient.BaseAddress!.ToString().Replace("+http", string.Empty).TrimEnd('/')}";

    Console.WriteLine($"Using MCP Todo FQDN: {endpoint}");

    var clientTransportOptions = new HttpClientTransportOptions()
    {
        Endpoint = new Uri($"{endpoint!}/mcp")
    };
    var clientTransport = new HttpClientTransport(clientTransportOptions, httpClient, loggerFactory);

    var clientOptions = new McpClientOptions()
    {
        ClientInfo = new Implementation()
        {
            Name = "MCP Todo Client",
            Version = "1.0.0",
        }
    };

    return McpClient.CreateAsync(clientTransport, clientOptions, loggerFactory).GetAwaiter().GetResult();
});

var sp = builder.Services.BuildServiceProvider();
var mcpClient = sp.GetRequiredKeyedService<McpClient>("mcp-todo");
var tools = await mcpClient.ListToolsAsync();

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = tenantId });
AIAgent agent = new AIProjectClient(new Uri(projectEndpoint!), credential)
                    .AsAIAgent(
                        model: deploymentName!,
                        instructions: """
                            You are a helpful assistant for managing a todo list.
                            You can add, remove, and list tasks in the todo list.
                            """,
                        name: "todo-agent",
                        description: "A hosted agent that manages to-do list items.",
                        tools: [.. tools.Select(tool => (AITool)tool) ]);

builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();

app.App.MapDefaultEndpoints();

await app.RunAsync();
