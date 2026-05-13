using System.ClientModel;
using System.ClientModel.Primitives;

using Azure.AI.Extensions.OpenAI;
using Azure.Identity;

using MafStarterPack.Agent.Extensions;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

using OpenAI;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

var (projectEndpoint, deploymentName) = config.GetFoundryConnectionDetails(projectName: "starter-project", modelName: "gpt-5-mini");
var tenantId = config["AZURE_TENANT_ID"] ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is not set.");
var agentName = config["Foundry:Project:Agent:Name"] ?? "todo-agent";
var agentVersion = config["Foundry:Project:Agent:Version"] ?? "1";

if (builder.Environment.IsDevelopment() == true)
{
    var logger = new LoggerFactory().CreateLogger("MafStarterPack.Agent.Program");
    logger.LogInformation("Using configuration: {config}", config.GetDebugView());
    logger.LogInformation("Parsed connection string values: Endpoint={endpoint}", projectEndpoint);
    logger.LogInformation("Parsed connection string values: Model={model}", deploymentName);
    logger.LogInformation("Parsed connection string values: AgentName={agentName}", agentName);
    logger.LogInformation("Parsed connection string values: AgentVersion={agentVersion}", agentVersion);
}

builder.AddServiceDefaults();

IChatClient chatClient = default!;
if (builder.Environment.IsDevelopment() == true)
{
    builder.Services.AddTransient<HostedAgentLoggingHandler>();
    builder.Services.AddHttpClient("hosted-agent", client =>
    {
        // Keep the "https+http" scheme so the Aspire service-discovery DelegatingHandler
        // (added by AddServiceDefaults -> ConfigureHttpClientDefaults) resolves "hosted-agent"
        // to a real endpoint at request time.
        client.BaseAddress = new Uri("https+http://hosted-agent");
    })
    .AddHttpMessageHandler<HostedAgentLoggingHandler>();

    var httpClient = builder.Services
                            .BuildServiceProvider()
                            .GetRequiredService<IHttpClientFactory>()
                            .CreateClient("hosted-agent");

    // The OpenAIClient builds its own HTTP pipeline by default and therefore would NOT pick up
    // the service-discovery handler. Pass the factory-built HttpClient as the transport so the
    // request flows through Aspire's discovery + resilience handlers, and keep the "https+http"
    // scheme on the Endpoint so the discovery handler can rewrite the host.
    chatClient = new OpenAIClient(
                         credential: new ApiKeyCredential("fake-key-for-development"),
                         options: new OpenAIClientOptions()
                         {
                             Endpoint = httpClient.BaseAddress!,
                             Transport = new HttpClientPipelineTransport(httpClient)
                         })
                     .GetResponsesClient()
                     .AsIChatClient(deploymentName);

    // Chain calls via the OpenAI Responses `previous_response_id` instead of replaying full chat
    // history. This avoids re-serializing prior assistant `output_text` items into the next request,
    // which would otherwise be rejected by strict Responses servers (e.g. Foundry/AgentServer) with
    // "Required property 'logprobs' is missing". See PreviousResponseIdChatClient for details.
    var wrapperLogger = builder.Services
                               .BuildServiceProvider()
                               .GetRequiredService<ILoggerFactory>()
                               .CreateLogger<PreviousResponseIdChatClient>();
    chatClient = new PreviousResponseIdChatClient(chatClient, wrapperLogger);
}
else
{
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = tenantId });
    var projectClientOptions = new ProjectOpenAIClientOptions { AgentName = agentName };
    var projectClient = new ProjectOpenAIClient(
        projectEndpoint: new Uri(projectEndpoint!),
        tokenProvider: credential,
        options: projectClientOptions);

    chatClient = projectClient.GetResponsesClient()
                              .AsIChatClient(deploymentName);
}

var agentOptions = new ChatClientAgentOptions { Name = agentName };
var agent = new ChatClientAgent(
    chatClient: chatClient,
    options: agentOptions);

builder.Services.AddKeyedSingleton<AIAgent>(agentName, agent);

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

builder.Services.AddAGUI();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapAGUI(
    pattern: "ag-ui",
    aiAgent: app.Services.GetRequiredKeyedService<AIAgent>(agentName)
);

if (builder.Environment.IsDevelopment() == true)
{
    app.MapDevUI();
}
else
{
    app.UseHttpsRedirection();
}

await app.RunAsync();
