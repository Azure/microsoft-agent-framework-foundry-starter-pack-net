using System.Data.Common;

using Microsoft.Agents.AI;

namespace MafStarterPack.Agent.Extensions;

public static class ConnectionStringExtensions
{
    public static (string? endpoint, string? deploymentName, string? agentName, string? agentVersion) GetAgentDetails(this IConfiguration config, string key)
    {
        var connectionString = config.GetConnectionString(key) ?? throw new InvalidOperationException($"Connection string '{key}' is not configured.");
        var connection = new DbConnectionStringBuilder() { ConnectionString = connectionString };

        var endpoint = connection.TryGetValue("Endpoint", out var endpointValue)
                     ? endpointValue?.ToString()
                     : throw new InvalidOperationException("Missing Foundry Project Endpoint");

        var deploymentName = connection.TryGetValue("Deployment", out var deploymentNameValue)
                           ? deploymentNameValue?.ToString()
                           : throw new InvalidOperationException("Missing Foundry Project Deployment Name");

        var agentName = connection.TryGetValue("AgentName", out var agentNameValue)
                        ? agentNameValue?.ToString()
                        : throw new InvalidOperationException("Missing Foundry Project Agent Name");

        var agentVersion = connection.TryGetValue("AgentVersion", out var agentVersionValue)
                           ? agentVersionValue?.ToString()
                           : throw new InvalidOperationException("Missing Foundry Project Agent Version");

        return (endpoint, deploymentName, agentName, agentVersion);
    }
}