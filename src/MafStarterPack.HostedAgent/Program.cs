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
Console.WriteLine($"Using Azure OpenAI Endpoint: {config["AZURE_OPENAI_ENDPOINT"]}");
Console.WriteLine($"Using Azure OpenAI Deployment Name: {config["AZURE_OPENAI_DEPLOYMENT_NAME"]}");
Console.WriteLine($"Using Azure Tenant ID: {config["AZURE_TENANT_ID"]}");

var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set");
var deploymentName = config["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-5-mini";

var host = builder.Build();

TokenCredential credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = config["AZURE_TENANT_ID"] });

var chatClient = new AzureOpenAIClient(new Uri($"{endpoint.TrimEnd('/')}/openai/v1/"), credential)
                     .GetResponsesClient(deploymentName)
                     .AsIChatClient()
                     .AsBuilder()
                     .UseLogging()
                     .UseOpenTelemetry(sourceName: "Agents")
                     .Build(host.Services);

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
