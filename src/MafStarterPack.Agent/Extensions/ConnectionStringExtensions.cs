using System.Data.Common;

namespace MafStarterPack.Agent.Extensions;

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