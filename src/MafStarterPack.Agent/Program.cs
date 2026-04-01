using System.Data.Common;

using Azure.AI.Projects;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;

using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

var connection = new DbConnectionStringBuilder() { ConnectionString = config.GetConnectionString("foundry") };
var endpoint = connection.TryGetValue("Endpoint", out var endpointValue) ? endpointValue?.ToString() : throw new InvalidOperationException("Missing Foundry Endpoint");
var model = connection.TryGetValue("DeploymentId", out var modelValue) ? modelValue?.ToString() : throw new InvalidOperationException("Missing Foundry Model");
var agentName = connection.TryGetValue("AgentName", out var agentNameValue) ? agentNameValue?.ToString() : throw new InvalidOperationException("Missing Foundry Agent Name");
var agentVersion = connection.TryGetValue("AgentVersion", out var agentVersionValue) ? agentVersionValue?.ToString() : "1";

if (builder.Environment.IsDevelopment() == true)
{
    var logger = new LoggerFactory().CreateLogger("MafStarterPack.Agent.Program");
    logger.LogInformation("Using configuration: {config}", config.GetDebugView());
    logger.LogInformation("Parsed connection string values: Endpoint={endpoint}", endpoint);
    logger.LogInformation("Parsed connection string values: Model={model}", model);
    logger.LogInformation("Parsed connection string values: AgentName={agentName}", agentName);
    logger.LogInformation("Parsed connection string values: AgentVersion={agentVersion}", agentVersion);
}

builder.AddServiceDefaults();

// var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = config["AZURE_TENANT_ID"] });
var credential = new AzureCliCredential(new AzureCliCredentialOptions() { TenantId = config["AZURE_TENANT_ID"] });

// Foundry Agent Client
// var projectClient = new AIProjectClient(new Uri(endpoint!), credential);
// var hostedAgent = await projectClient.Agents.GetAgentAsync(agentName!);
// var agent = projectClient.AsAIAgent(hostedAgent);

// builder.Services.AddKeyedSingleton<AIAgent>(agentName!, agent);

// Azure OpenAI Client
// builder.AddAzureOpenAIClient("foundry")
//        .AddChatClient(model!);

// OpenAI Client
builder.AddOpenAIClientFromConfiguration("foundry")
       .AddChatClient(model!);


builder.AddAIAgent(agentName!, (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var agent = new ChatClientAgent(
        chatClient: chatClient,
        name: key,
        instructions: """
            You are a helpful assistant for managing a todo list.
            You can add, remove, and list tasks in the todo list.
            Always confirm the user's request before making changes to the todo list.
            """
    );

    return agent;
});

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

builder.Services.AddAGUI();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapAGUI(
    pattern: "ag-ui",
    aiAgent: app.Services.GetRequiredKeyedService<AIAgent>(agentName!)
);

if (builder.Environment.IsDevelopment() == true)
{
    app.MapDevUI();
}
else
{
    app.UseHttpsRedirection();
}

app.Run();
