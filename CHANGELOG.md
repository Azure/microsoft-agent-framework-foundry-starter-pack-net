# Changelog

## <a name="1-0-0"></a>1.0.0 (2026-04-03)

### Features

- Add .NET Aspire AppHost with custom `FoundryResource` for orchestration
- Add Foundry prompt agent app (`MafStarterPack.Agent`) with Azure AI Foundry integration
- Add Blazor chat web UI (`MafStarterPack.WebUI`) with streaming response support
- Add MCP todo server (`MafStarterPack.McpTodo`) with in-memory SQLite
- Add `MafStarterPack.ServiceDefaults` for shared Aspire service configuration
- Add `FoundryResource` custom Aspire resource with config validation and dashboard property display
- Add `AgentRecordShimChatClient` workaround for `Azure.AI.Projects.Agents` 2.0.0 type rename breaking change
- Add MCP tool integration for Foundry prompt agent
- Add `create-agent.cs` script for provisioning prompt agents in Foundry
- Add dev container configuration for Codespaces support
- Add Bicep infrastructure for Foundry, Container Apps, and managed identity
- Add post-deploy scripts (PowerShell and shell) for role assignment to managed identity
- Add user-secrets cleanup scripts (PowerShell and shell)
- Add `azd` templates for deploying MCP server as a Container App

### Bug Fixes

- Fix loading spinner not showing during chat response streaming by adding `StateHasChanged()` calls
