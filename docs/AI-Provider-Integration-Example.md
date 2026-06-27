# AI Provider Integration - Usage Example

This document demonstrates how to use the new AI Provider integration in KittyClawOpen automations.

## Overview

The AI Provider system allows you to execute agents using different AI providers (OpenCode, Claude, etc.) instead of just the legacy ClaudeRunner. This provides flexibility in choosing models and providers for different use cases.

## Execution Modes

The system supports the following execution modes:

- **LegacyClaude**: Existing ClaudeRunner behavior (default)
- **DirectOpenCode**: Direct execution through OpenCode provider
- **CaoGoverned**: CAO-governed execution (stub for future implementation)
- **TeamWorkflow**: Team workflow execution (stub for future implementation)
- **Manual**: Manual execution mode

## Configuration Hierarchy

AI Provider configuration is resolved in the following hierarchy:

1. **Agent-level config** (most specific)
2. **Ticket-level config**
3. **Project-level config**
4. **Global config** (least specific)

If no configuration is found, the system falls back to **LegacyClaude** mode.

## Example Automation Configurations

### 1. Using Effective Config (Hierarchy-based)

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "maxTurns": 200,
  "useEffectiveConfig": true
}
```

This configuration will:
- Use the AI provider configuration from the hierarchy (agent → ticket → project → global)
- Fall back to LegacyClaude if no configuration is found

### 2. Explicit Provider Configuration

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "executionMode": "DirectOpenCode",
  "provider": "opencode",
  "model": "deepseek-v4-pro",
  "profile": "developer",
  "useEffectiveConfig": false,
  "maxTurns": 200
}
```

This configuration will:
- Use OpenCode provider directly
- Use the specified model (`deepseek-v4-pro`)
- Use the specified profile (`developer`)
- Ignore hierarchy-based configuration

### 3. Legacy Claude Configuration (Backward Compatible)

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "model": "claude-3-5-sonnet",
  "maxTurns": 200
}
```

This configuration maintains backward compatibility with existing automations and will use the legacy ClaudeRunner.

## Execution Metadata

When an agent run completes, the system stores execution metadata that includes:

```json
{
  "execution": {
    "mode": "DirectOpenCode",
    "runner": "opencode",
    "provider": "openrouter",
    "model": "qwen/qwen3.5-coder",
    "profile": "developer",
    "runId": "run_123",
    "sessionId": "opencode_abc",
    "worktreePath": ".worktrees/KC-42",
    "branchName": "kc/KC-42",
    "ticketId": "42",
    "projectId": "test-project"
  }
}
```

This metadata is available in:
- AgentRun.ExecutionMetadata
- AgentRunSnapshot.ExecutionMetadata
- Ticket activity timeline
- Run drawer UI

## Provider Configuration

### OpenCode Provider

The OpenCode provider can be configured to use either:

1. **OpenCode CLI**: Uses the `opencode` or `oc` command-line tool
2. **OpenCode SDK**: Uses HTTP API to communicate with a local OpenCode server

#### CLI Configuration

Ensure the OpenCode CLI is installed and available in PATH. The system will automatically detect it.

#### SDK Configuration

Configure the OpenCode server URL and API key in your application settings:

```csharp
// In Program.cs or configuration
builder.Services.AddSingleton<OpenCodeSdkClient>(new OpenCodeSdkClient(
    new HttpClient(),
    "http://localhost:8080",
    "your-api-key"
));
```

## Fallback Behavior

The system includes safe fallback mechanisms:

1. **Provider not available**: Falls back to LegacyClaude
2. **OpenCode server unavailable**: Creates a failed run with error message, does NOT mark ticket as Done
3. **Missing configuration**: Uses LegacyClaude
4. **Execution failure**: Adds comment to ticket with error details

## Migration Guide

### Step 1: Update Existing Automations

Existing automations will continue to work without changes. The system automatically uses LegacyClaude mode for backward compatibility.

### Step 2: Add AI Provider Configuration

Add project-level or global AI provider configuration:

```json
{
  "aiProvider": {
    "defaultProvider": "opencode",
    "defaultModel": "deepseek-v4-pro",
    "defaultProfile": "developer"
  }
}
```

### Step 3: Create New Automations with AI Providers

Create new automations that explicitly use AI providers:

```json
{
  "type": "runAgent",
  "agent": "code-reviewer",
  "executionMode": "DirectOpenCode",
  "provider": "opencode",
  "model": "deepseek-v4-pro",
  "profile": "reviewer",
  "useEffectiveConfig": false
}
```

### Step 4: Test and Validate

1. Test that existing automations still work
2. Test that new AI provider automations work
3. Verify that execution metadata is correctly stored
4. Check that fallback behavior works as expected

## Troubleshooting

### Common Issues

1. **Provider not available**: Ensure the provider is properly registered and configured
2. **OpenCode CLI not found**: Install OpenCode CLI or configure SDK client
3. **Model not found**: Verify the model name is correct for the selected provider
4. **Execution failures**: Check the run drawer and ticket comments for error details

### Debug Logging

Enable debug logging to see AI provider resolution and execution details:

```csharp
// In appsettings.json or configuration
{
  "Logging": {
    "LogLevel": {
      "KittyClaw.Core.Automation.AI": "Debug",
      "KittyClaw.Core.Automation.ActionExecutor": "Debug"
    }
  }
}
```

## Future Enhancements

The following features are planned for future PRs:

- **PR-5**: Worktree per card default
- **PR-6**: Direct OpenCode policy (risk-based routing)
- **PR-7**: CaoRunner implementation
- **PR-8**: Done gate implementation
- **PR-9**: Plan-to-card execution

## API Reference

### AIProviderService

- `ResolveEffectiveConfigAsync`: Resolve AI configuration from hierarchy
- `ResolveFromActionAsync`: Resolve AI configuration from action parameters
- `GetAvailableProviders`: Get list of available providers
- `IsProviderAvailable`: Check if a specific provider is available

### IAIProvider

- `Id`: Provider identifier
- `Name`: Human-readable provider name
- `IsAvailable`: Whether the provider is available
- `ExecuteAsync`: Execute an agent run
- `StopAsync`: Stop an ongoing run
- `GetStatusAsync`: Get run status

### ExecutionMode

- `LegacyClaude`: Legacy ClaudeRunner path
- `DirectOpenCode`: Direct OpenCode execution
- `CaoGoverned`: CAO-governed execution (stub)
- `TeamWorkflow`: Team workflow execution (stub)
- `Manual`: Manual execution
