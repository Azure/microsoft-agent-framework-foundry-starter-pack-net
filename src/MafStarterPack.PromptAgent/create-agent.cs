#!/usr/bin/env dotnet

#:sdk Microsoft.NET.Sdk
#:package Azure.AI.Projects@2.*
#:package Azure.Identity@1.*
#:package Microsoft.Extensions.Hosting@10.*
#:property UserSecretsId=a9901e31-0f2d-4b0d-a630-555cb5adaffe

using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;

using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
                 .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                 .AddEnvironmentVariables()
                 .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
                 .Build();

var projectEndpoint = config["Foundry:Project:Endpoint"] ?? throw new InvalidOperationException("Project endpoint is not configured");
var model = config["Foundry:Project:Agent:Model"] ?? "gpt-5-mini";
var agentName = config["Foundry:Project:Agent:Name"] ?? "todo-agent";
var agentVersion = config["Foundry:Project:Agent:Version"] ?? "1";

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = config["AZURE_TENANT_ID"] });
var projectClient = new AIProjectClient(endpoint: new Uri(projectEndpoint), tokenProvider: credential);
var agentClient = projectClient.AgentAdministrationClient;

var definition = ProjectsAgentDefinition.CreatePromptAgentDefinition(model)
                                        .AddInstruction("""
    You are a helpful assistant for managing a todo list.
    You can add, remove, and list tasks in the todo list.
    Always confirm the user's request before making changes to the todo list.
    """);

var agent = await agentClient.CreateAgentVersionAsync(
    agentName: agentName,
    options: new ProjectsAgentVersionCreationOptions(definition));

Console.WriteLine($"Agent created (id: {agent.Value.Id}, name: {agent.Value.Name}, version: {agent.Value.Version})");

internal static class ProjectsAgentDefinitionExtensions
{
    internal static DeclarativeAgentDefinition AddInstruction(this DeclarativeAgentDefinition definition, string instruction)
    {
        definition.Instructions = instruction;

        return definition;
    }
}