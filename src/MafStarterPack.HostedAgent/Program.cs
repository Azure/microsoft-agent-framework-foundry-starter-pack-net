using Azure.AI.AgentServer.AgentFramework.Extensions;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var config = builder.Configuration
                    .AddEnvironmentVariables()
                    .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
                    .Build();

Console.WriteLine("Starting Hosted Agent...");

var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set");
var deploymentName = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-5-mini";
var tenantId = config["AZURE_TENANT_ID"];

Console.WriteLine($"Using Azure OpenAI Endpoint: {endpoint}");
Console.WriteLine($"Using Azure OpenAI Deployment Name: {deploymentName}");
Console.WriteLine($"Using Azure Tenant ID: {tenantId}");

var app = builder.Build();

TokenCredential credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = tenantId });

var chatClient = new AzureOpenAIClient(new Uri($"{endpoint.TrimEnd('/')}/openai/v1/"), credential)
                     .GetResponsesClient()
                     .AsIChatClient(deploymentName)
                     .AsBuilder()
                     .UseLogging()
                     .UseOpenTelemetry(sourceName: "Agents")
                     .Build(app.Services);

var agent = new ChatClientAgent(
                    chatClient: chatClient,
                    name: "TodoAgent",
                    instructions: """
                        You are a helpful assistant for managing a todo list.
                        You can add, remove, and list tasks in the todo list.
                        Always confirm the user's request before making changes to the todo list.
                        """)
                .AsBuilder()
                .Build();

await agent.RunAIAgentAsync(telemetrySourceName: "Agents");
