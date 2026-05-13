using System.Data.Common;

namespace MafStarterPack.Agent.Extensions;

public static class ConnectionStringExtensions
{
    public static (string? projectEndpoint, string? deploymentName) GetFoundryConnectionDetails(this IConfiguration config, string projectName, string modelName)
    {
        var projectEndpoint = config.GetProjectEndpoint(projectName);
        var deploymentName = config.GetDeploymentName(modelName);

        return (projectEndpoint, deploymentName);
    }

    private static string? GetProjectEndpoint(this IConfiguration config, string key)
    {
        var connectionString = config.GetConnectionString(key) ?? throw new InvalidOperationException($"Connection string '{key}' is not configured.");
        var connection = new DbConnectionStringBuilder() { ConnectionString = connectionString };
        var endpoint = connection.TryGetValue("Endpoint", out var endpointValue)
                     ? endpointValue?.ToString()
                     : throw new InvalidOperationException("Missing Foundry Project Endpoint");

        return endpoint ?? throw new InvalidOperationException("Foundry Project Endpoint is null or empty.");
    }

    private static string? GetDeploymentName(this IConfiguration config, string key)
    {
        var connectionString = config.GetConnectionString(key) ?? throw new InvalidOperationException($"Connection string '{key}' is not configured.");
        var connection = new DbConnectionStringBuilder() { ConnectionString = connectionString };
        var deploymentName = connection.TryGetValue("Deployment", out var deploymentNameValue)
                           ? deploymentNameValue?.ToString()
                           : throw new InvalidOperationException("Missing Foundry Project Deployment Name");

        return deploymentName ?? throw new InvalidOperationException("Foundry Project Deployment Name is null or empty.");
    }
}