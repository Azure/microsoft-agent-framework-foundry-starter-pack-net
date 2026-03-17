# Microsoft Agent Framework and Foundry Starter Pack in .NET

This is a starter template to build a .NET-based agentic AI app using [Microsoft Agent Framework](https://aka.ms/agent-framework) and [Microsoft Foundry](https://aka.ms/microsoft-foundry) with [Aspire](https://aspire.dev).

## Features

![Architecture](./assets/architecture.png)

This stater template provides the following features:

- [Blazor](https://blazor.net) frontend for chat UI
- [ASP.NET](https://asp.net) backend with Microsoft Agent Framework
- [Microsoft Foundry Hosted Agents](https://aka.ms/microsoft-foundry/hosted-agents) service for agent hosting
- [To-do list management MCP server](https://aka.ms/mcp/dotnet/samples/todolist) for tooling support to agent
- Aspire for cloud-native app orchestration

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or higher
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) or [VS Code](https://code.visualstudio.com/download) + [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
- [Docker Desktop](https://docs.docker.com/desktop/) or equivalent
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure subscription (free)](http://azure.microsoft.com/free)

## Quickstart

### Get repository root

1. Get the repository root.

    ```bash
    # bash/zsh
    REPOSITORY_ROOT=$(git rev-parse --show-toplevel)
    ```

    ```powershell
    # PowerShell
    $REPOSITORY_ROOT = git rev-parse --show-toplevel
    ```

### Login to Azure

1. Login to Azure using `azd`.

    ```bash
    azd auth login
    ```

1. Login to Azure using `az`.

    ```bash
    az login
    ```

### Deploy Microsoft Foundry Hosted Agents

1. Navigate to the `resources-foundry` directory.

    ```bash
    cd $REPOSITORY_ROOT/resources-foundry
    ```

1. Initialize resources on Azure.

    ```bash
    azd init
    ```

   When you get a prompt, enter the environment name.

1. Initialize hosted agent on Microsoft Foundry.

    ```bash
    azd ai agent init -m $REPOSITORY_ROOT/src/MafStarterPack.HostedAgent/agent.yaml --no-prompt
    ```

1. Deploy the hosted agent to Microsoft Foundry.

    ```bash
    azd up
    ```

   > **NOTE**: You may have to set the environment variable, `AZURE_TENANT_ID`.
   >
   > ```bash
   > # bash/zsh
   > AZURE_TENANT_ID=$(az account show --query "tenantId" -o tsv)
   > ```
   >
   > ```bash
   > # PowerShell
   > $env:AZURE_TENANT_ID = az account show --query "tenantId" -o tsv
   > ```

### Deploy apps to Azure

TBD

### Run agent locally

1. Add Microsoft Foundry endpoint.

    ```bash
    dotnet user-secrets --project $REPOSITORY_ROOT/src/MafStarterPack.HostedAgent set AZURE_OPENAI_ENDPOINT $(azd env get-value AZURE_OPENAI_ENDPOINT)
    dotnet user-secrets --project $REPOSITORY_ROOT/src/MafStarterPack.HostedAgent set AZURE_OPENAI_DEPLOYMENT_NAME gpt-5-mini
    ```

1. Run the agent app.

    ```bash
    dotnet run --project $REPOSITORY_ROOT/src/MafStarterPack.HostedAgent
    ```

1. Send test prompts.

    ```bash
    # health check
    curl http://localhost:8088/readiness
    ```

    ```bash
    # simple prompt
    curl -X POST http://localhost:8088/responses -H "Content-Type: application/json" -d '{"input": "show me the list to do"}'
    ```

   > **NOTE**: You may have to set the environment variable, `AZURE_TENANT_ID`.
   >
   > ```bash
   > # bash/zsh
   > AZURE_TENANT_ID=$(az account show --query "tenantId" -o tsv)
   > ```
   >
   > ```bash
   > # PowerShell
   > $env:AZURE_TENANT_ID = az account show --query "tenantId" -o tsv
   > ```

## Resources

- [Microsoft Agent Framework](https://aka.ms/agent-framework)
- [Microsoft Foundry](https://aka.ms/microsoft-foundry)
- [Microsoft Foundry Hosted Agents](https://aka.ms/microsoft-foundry/hosted-agents)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io)
- [Aspire](https://aspire.dev)
