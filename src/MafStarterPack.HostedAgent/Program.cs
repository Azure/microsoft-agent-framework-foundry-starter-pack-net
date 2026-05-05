using System.Data.Common;

using Azure.AI.Projects;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var builder = AgentHost.CreateBuilder(args);

// AgentHost.CreateBuilder already wires up OpenTelemetry via signal-specific
// AddOtlpExporter calls, which is incompatible with ServiceDefaults' cross-cutting
// UseOtlpExporter. Register only what the hosted agent actually needs from
// ServiceDefaults: service discovery (so https+http://mcp-todo resolves to the
// Aspire-assigned endpoint) plus the standard HTTP resilience handler.
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddStandardResilienceHandler();
    http.AddServiceDiscovery();
});

// Aspire's PublishAsHostedAgent surfaces /liveness and /readiness probes on the
// dashboard and runs an HTTP health check against /liveness. AgentHost does not
// map those paths by default, so register them here.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

Console.WriteLine("Starting Hosted Agent...");

var config = builder.Configuration;

var endpoint = config["FOUNDRY_PROJECT_ENDPOINT"] ?? config.GetProjectEndpoint("project") ?? throw new InvalidOperationException("Foundry Project Endpoint is not set.");
var deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"] ?? config.GetDeploymentName("chat") ?? throw new InvalidOperationException("Foundry Project Deployment Name is not set.");
var tenantId = config["AZURE_TENANT_ID"] ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is not set.");
var mcpTodoFqdn = config["AZURE_RESOURCE_MCP_TODO_FQDN"];

Console.WriteLine($"Using Azure OpenAI Endpoint: {endpoint}");
Console.WriteLine($"Using Azure OpenAI Deployment Name: {deploymentName}");
Console.WriteLine($"Using Azure Tenant ID: {tenantId}");

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

    mcpTodoFqdn ??= endpoint;
    Console.WriteLine($"Using MCP Todo FQDN: {mcpTodoFqdn}");

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

// AITool serverTool = new HostedMcpServerTool(
//     serverName: "mcp-todo",
//     serverAddress: $"https://{mcpTodoFqdn.TrimEnd('/')}/mcp")
// {
//     ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire
// };

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = tenantId });
AIAgent agent = new AIProjectClient(new Uri(endpoint), credential)
                    .AsAIAgent(
                        model: deploymentName,
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
app.App.MapHealthChecks("/readiness");
app.App.MapHealthChecks("/liveness", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

await app.RunAsync();


public static class ConnectionStringExtensions
{
    public static string? GetProjectEndpoint(this IConfiguration config, string key)
    {
        var connectionString = config.GetConnectionString(key) ?? throw new InvalidOperationException($"Connection string '{key}' is not configured.");
        var connection = new DbConnectionStringBuilder() { ConnectionString = connectionString };
        var endpoint = connection.TryGetValue("Endpoint", out var endpointValue)
                     ? endpointValue?.ToString()
                     : throw new InvalidOperationException("Missing Foundry Project Endpoint");

        return endpoint;
    }

    public static string? GetDeploymentName(this IConfiguration config, string key)
    {
        var connectionString = config.GetConnectionString(key) ?? throw new InvalidOperationException($"Connection string '{key}' is not configured.");
        var connection = new DbConnectionStringBuilder() { ConnectionString = connectionString };
        var deploymentName = connection.TryGetValue("Deployment", out var deploymentNameValue)
                           ? deploymentNameValue?.ToString()
                           : throw new InvalidOperationException("Missing Foundry Project Deployment Name");

        return deploymentName;
    }
}