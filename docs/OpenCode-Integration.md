# OpenCode Integration Guide

This document explains how OpenCode integration works in Beaver Board and how to configure and use it.

## Overview

Beaver Board integrates with OpenCode to provide:

- **Direct OpenCode execution** - Run OpenCode agents directly from tickets
- **Provider/model selection** - Choose from multiple AI providers and models
- **OpenCode agents** - Use OpenCode's specialized agents (build, plan, etc.)
- **Worktree per card** - Each executable ticket runs in its own isolated worktree
- **Execution metadata** - Track provider, model, session, and other execution details
- **Policy gates** - Control when DirectOpenCode vs CAO-governed execution is allowed

## Architecture

The integration follows an **upstream-friendly plugin architecture**:

```
Beaver Board UI
  ↓
Ticket / Card Execution Panel
  ↓
AutomationEngine
  ↓
RunnerRegistry (Zone A - Generic)
  ├── ClaudeRunnerAdapter → ClaudeRunner (existing)
  └── OpenCodeRunner → OpenCode CLI/Server (new)
  ↓
WorktreeService
  ↓
OpenCode session / CLI / server
  ↓
Run stream → Beaver Board run drawer
  ↓
Ticket metadata / comments / activity
```

## Execution Modes

### 1. LegacyClaude (Default)

- Uses existing ClaudeRunner
- No changes to existing workflow
- All existing automations continue to work

### 2. DirectOpenCode

- Executes via OpenCode CLI or server
- Runs in per-card worktree
- Supports provider/model selection
- Supports OpenCode agent selection

### 3. CaoGoverned (Future)

- CAO-governed execution
- Requires CAO closeout for Done
- Not implemented yet (stub)

### 4. TeamWorkflow (Future)

- Team-based decomposition
- Parent/child ticket orchestration
- Not implemented yet (stub)

### 5. Manual

- Manual execution mode
- No automatic execution

## Configuration

### Project Settings

Add OpenCode configuration to your project:

```json
{
  "opencode": {
    "useServer": false,
    "cliCommand": "opencode",
    "defaultProvider": "openrouter",
    "defaultModel": "qwen/qwen3.5-coder",
    "defaultAgent": "build",
    "timeoutSeconds": 3600
  },
  "worktree": {
    "worktreeRoot": null,
    "autoCreate": true,
    "autoCleanup": false,
    "branchTemplate": "kc/KC-{ticketId}"
  },
  "policies": {
    "directOpenCode": {
      "enabled": true,
      "allowHighRisk": false,
      "allowMediumRisk": false,
      "forbiddenLabels": ["security", "rls", "payment", "privacy", "sre", "provider-proof"],
      "allowedProviders": ["openai", "anthropic", "openrouter", "ollama", "mistral", "gemini", "deepseek"]
    },
    "caoRequired": {
      "forHighRisk": true,
      "forMediumRisk": true,
      "labels": ["security", "rls", "payment", "privacy", "sre", "provider-proof"]
    },
    "doneGate": {
      "requireForDirectOpenCode": false,
      "requireForCaoGoverned": true,
      "requireSummary": true,
      "requireLightweightChecks": false,
      "requireCaoCloseout": true
    },
    "worktree": {
      "requireForLegacyClaude": false,
      "requireForDirectOpenCode": true,
      "requireForCaoGoverned": true
    }
  }
}
```

### Environment Configuration

Set OpenCode CLI command (if not in PATH):

```bash
# In appsettings.json or environment
export OPENCODE_CLI_COMMAND="/path/to/opencode"
```

Or configure server mode:

```bash
export OPENCODE_USE_SERVER=true
export OPENCODE_SERVER_URL="http://localhost:8080"
```

## Usage

### 1. Using DirectOpenCode in Automations

Create an automation with DirectOpenCode execution mode:

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "executionMode": "DirectOpenCode",
  "provider": "openrouter",
  "model": "qwen/qwen3.5-coder",
  "opencodeAgent": "build",
  "profile": "developer",
  "useWorktree": true,
  "maxTurns": 200
}
```

### 2. Using Effective Configuration

Let the system resolve configuration from hierarchy:

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "executionMode": "DirectOpenCode",
  "useEffectiveConfig": true,
  "useWorktree": true
}
```

This will use:
1. Agent-level config (if exists)
2. Ticket-level config (if exists)
3. Project-level config (from above)
4. Global defaults

### 3. Legacy Claude (Backward Compatible)

Existing automations work without changes:

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "maxTurns": 200
}
```

## Provider/Model Catalog

The system provides a catalog of supported providers and models:

### Supported Providers

| Provider | Supports Tools | Supports Vision | Supports Local | Cost Tier |
|----------|---------------|----------------|---------------|-----------|
| OpenAI | ✅ | ✅ | ❌ | High |
| Anthropic | ✅ | ✅ | ❌ | High |
| OpenRouter | ✅ | ✅ | ❌ | Medium |
| Ollama | ❌ | ❌ | ✅ | Low |
| Mistral | ✅ | ❌ | ❌ | Medium |
| Gemini | ✅ | ✅ | ❌ | Medium |
| DeepSeek | ✅ | ❌ | ❌ | Medium |

### Popular Models

**OpenRouter:**
- `openai/gpt-4o` - GPT-4o (OpenRouter)
- `anthropic/claude-3-5-sonnet` - Claude 3.5 Sonnet
- `qwen/qwen3.5-coder` - Qwen 3.5 Coder
- `deepseek/deepseek-v4-pro` - DeepSeek V4 Pro
- `mistral/mistral-large` - Mistral Large

**Anthropic:**
- `claude-3-5-sonnet` - Claude 3.5 Sonnet
- `claude-3-5-haiku` - Claude 3.5 Haiku
- `claude-3-opus` - Claude 3 Opus

**OpenAI:**
- `gpt-4o` - GPT-4o
- `gpt-4o-mini` - GPT-4o Mini
- `gpt-4` - GPT-4
- `gpt-3.5-turbo` - GPT-3.5 Turbo

## OpenCode Agents

OpenCode provides specialized agents with custom prompts and tool access:

| Agent | Description | Best For |
|-------|-------------|----------|
| `build` | Development/implementation | Coding tasks |
| `plan` | Planning/decomposition | Large tasks, planning |
| `review` | Code review | Pull requests, code review |
| `test` | Testing | Writing tests, debugging |
| `custom` | Custom agent | Project-specific tasks |

### Using OpenCode Agents

Specify the agent in your automation:

```json
{
  "type": "runAgent",
  "agent": "programmer",
  "executionMode": "DirectOpenCode",
  "opencodeAgent": "build",
  "provider": "openrouter",
  "model": "qwen/qwen3.5-coder"
}
```

Or use different agents for different workflows:

```json
# Planning column automation
{
  "type": "runAgent",
  "agent": "planner",
  "executionMode": "DirectOpenCode",
  "opencodeAgent": "plan"
}

# Implementation column automation
{
  "type": "runAgent",
  "agent": "programmer",
  "executionMode": "DirectOpenCode",
  "opencodeAgent": "build"
}

# Review column automation
{
  "type": "runAgent",
  "agent": "reviewer",
  "executionMode": "DirectOpenCode",
  "opencodeAgent": "review"
}
```

## Worktree per Card

Each executable ticket automatically gets its own worktree:

```
Ticket KC-42
├── branch: kc/KC-42
└── worktree: .worktrees/KC-42/
```

### Worktree Configuration

```json
{
  "worktree": {
    "worktreeRoot": ".worktrees",  // Relative to workspace
    "autoCreate": true,            // Create worktree automatically
    "autoCleanup": false,          // Don't cleanup automatically
    "branchTemplate": "kc/KC-{ticketId}"  // Branch naming
  }
}
```

### Custom Branch Naming

Use placeholders in branch template:

```json
{
  "branchTemplate": "kc/KC-{ticketId}-{ticketTitle}"
}
```

This creates branches like: `kc/KC-42-fix-authentication`

## Execution Metadata

Every run stores execution metadata:

```json
{
  "ticketId": "KC-42",
  "executionMode": "DirectOpenCode",
  "runner": "OpenCodeRunner",
  "provider": "openrouter",
  "model": "qwen/qwen3.5-coder",
  "opencodeAgent": "build",
  "profile": "developer",
  "runId": "run_abc123",
  "sessionId": "opencode_session_xyz",
  "worktreePath": ".worktrees/KC-42",
  "branchName": "kc/KC-42",
  "status": "completed",
  "startedAt": "2024-01-15T10:30:00Z",
  "finishedAt": "2024-01-15T10:45:00Z",
  "lastError": null
}
```

### Metadata Storage

Metadata is stored in:
- SQLite database (future)
- JSON files: `.kittyclaw/execution/{ticketId}.json`
- Export format: `.cao/cards/{ticketId}.execution.json` (for CAO)

## Policies

### Direct OpenCode Policy

Control when DirectOpenCode execution is allowed:

```json
{
  "directOpenCode": {
    "enabled": true,
    "allowHighRisk": false,
    "allowMediumRisk": false,
    "forbiddenLabels": ["security", "rls", "payment", "privacy", "sre", "provider-proof"],
    "allowedProviders": ["openai", "anthropic", "openrouter", "ollama", "mistral", "gemini", "deepseek"]
  }
}
```

### Risk Levels

Risk is determined by ticket labels:

**High Risk Labels:**
- `security`
- `rls` (Row-Level Security)
- `payment`
- `privacy`
- `sre` (Site Reliability Engineering)

**Medium Risk Labels:**
- `provider-proof`
- `infrastructure`
- `database`

### CAO Required Policy

Automatically require CAO governance for certain tickets:

```json
{
  "caoRequired": {
    "forHighRisk": true,
    "forMediumRisk": true,
    "labels": ["security", "rls", "payment", "privacy", "sre", "provider-proof"]
  }
}
```

### Done Gate Policy

Control when tickets can be moved to Done:

```json
{
  "doneGate": {
    "requireForDirectOpenCode": false,  // Allow Done without closeout
    "requireForCaoGoverned": true,      // Require CAO closeout
    "requireSummary": true,              // Require execution summary
    "requireLightweightChecks": false,  // Require lightweight checks
    "requireCaoCloseout": true          // Require CAO closeout for CAO mode
  }
}
```

## Failure Handling

### Failure Logbook

Every failed run creates a failure logbook entry:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "ticketId": "KC-42",
  "runId": "run_abc123",
  "executionMode": "DirectOpenCode",
  "runner": "OpenCodeRunner",
  "provider": "openrouter",
  "model": "qwen/qwen3.5-coder",
  "errorType": "provider-not-configured",
  "message": "OpenCode provider 'openrouter' is not configured",
  "exitCode": null,
  "fallbackUsed": false,
  "resolution": "needs-human"
}
```

### Error Types

| Error Type | Description | Resolution |
|------------|-------------|------------|
| `opencode-not-installed` | OpenCode CLI not found | Install OpenCode |
| `opencode-server-unavailable` | OpenCode server not reachable | Start OpenCode server |
| `provider-not-configured` | Provider not configured | Configure provider auth |
| `model-not-found` | Model not available | Select different model |
| `quota-exhausted` | API quota exceeded | Wait or upgrade quota |
| `auth-failed` | Authentication failed | Re-authenticate |
| `transport-failed` | Network/transport error | Check connection |
| `timeout` | Execution timed out | Increase timeout |
| `empty-output` | No meaningful output | Check prompt/model |
| `worktree-create-failed` | Worktree creation failed | Check permissions |
| `git-conflict` | Git merge conflict | Resolve conflicts |
| `steer-not-supported` | Steering not supported | Use different mode |
| `unsafe-route` | Unsafe execution path | Use CAO mode |
| `done-gate-failed` | Done gate checks failed | Complete requirements |

### Graceful Fallback

When OpenCode is unavailable:
1. **Provider not available**: Falls back to LegacyClaude
2. **OpenCode CLI not found**: Creates failed run, doesn't mark ticket as Done
3. **OpenCode server unavailable**: Creates failed run, doesn't mark ticket as Done
4. **Model not found**: Creates failed run with error message

**Important**: Failed runs **never** automatically mark tickets as Done.

## UI Integration

### Ticket Drawer: Execution Tab

The execution tab shows:

- **Execution Mode**: LegacyClaude, DirectOpenCode, CaoGoverned, etc.
- **Runner**: ClaudeRunner, OpenCodeRunner, etc.
- **Provider**: openrouter, anthropic, etc.
- **Model**: qwen/qwen3.5-coder, claude-3-5-sonnet, etc.
- **OpenCode Agent**: build, plan, review, etc.
- **Profile**: developer, planner, reviewer, etc.
- **Worktree Path**: .worktrees/KC-42
- **Branch**: kc/KC-42
- **Run ID**: run_abc123
- **Session ID**: opencode_session_xyz
- **Plan Status**: approved, pending, etc.
- **Risk**: low, medium, high
- **Review Required**: yes/no
- **Proof Required**: yes/no

### Buttons

- **Generate Plan**: Create execution plan
- **Approve Plan**: Approve generated plan
- **Start**: Start execution
- **Stop**: Stop ongoing run
- **Steer**: Send steering message (if supported)
- **Retry**: Retry failed run
- **Open Worktree**: Open worktree in file explorer
- **Open Run Logs**: View run logs
- **Mark Needs Human**: Flag for human intervention

### Project Settings: OpenCode

Configure OpenCode integration in project settings:

- **OpenCode Mode**: server, cli, auto
- **Server URL**: http://localhost:8080
- **CLI Command**: opencode, oc, /path/to/opencode
- **Default Provider**: openrouter, anthropic, etc.
- **Default Model**: qwen/qwen3.5-coder, etc.
- **Default OpenCode Agent**: build, plan, etc.
- **Auth Status**: Configured/Not configured
- **MCP Status**: Enabled/Disabled
- **Model Catalog Refresh**: Refresh button
- **Health Check**: Check OpenCode health

## Security

### No Arbitrary Shell

By default, arbitrary shell execution is disabled:

- `executePowerShell` actions are guarded
- Custom command templates are restricted
- Only owner can edit command templates
- Warnings are shown for potentially dangerous operations

### Secrets Management

**Do NOT store provider API keys in Beaver Board database.**

Instead, use:

1. **OpenCode auth/config**: OpenCode manages its own authentication
   ```bash
   opencode auth login
   ```

2. **Environment variables**: Set in system environment
   ```bash
   export OPENROUTER_API_KEY="your-key"
   ```

3. **OS keychain**: Use system keychain if configured by OpenCode

### Worktree Isolation

OpenCode always runs inside the worktree:

```
worktreePath = .worktrees/KC-42
cwd = worktreePath
```

Not in project root (unless explicitly manual mode).

## Command Templates

Configure OpenCode CLI arguments with templates:

```json
{
  "commandTemplate": [
    "run",
    "--model",
    "{model}",
    "--agent",
    "{opencodeAgent}",
    "--profile",
    "{profile}",
    "--max-turns",
    "{maxTurns}",
    "--working-directory",
    "{worktreePath}",
    "--prompt-file",
    "{promptFile}"
  ]
}
```

**Placeholders:**
- `{model}` - Resolved model
- `{opencodeAgent}` - OpenCode agent
- `{profile}` - Profile
- `{maxTurns}` - Max turns
- `{worktreePath}` - Worktree path
- `{workspacePath}` - Workspace path
- `{prompt}` - Prompt text
- `{promptFile}` - Prompt file path (if used)

**Why templates?** OpenCode CLI args may evolve. Templates allow updates without source changes.

## MCP Support

OpenCode supports MCP (Model Context Protocol) servers:

- **MCP enabled**: yes/no (inherited from OpenCode config)
- **MCP profile**: inherited from OpenCode

Beaver Board doesn't manage MCP directly - it passes through OpenCode's MCP environment/config.

## Tool Permissions

High-level permission modes:

| Mode | Description | OpenCode Mapping |
|------|-------------|------------------|
| Read-only planning | No file modifications | Read-only agent |
| Safe edit | Limited file modifications | Safe agent |
| Full edit | Full file modifications | Full agent |
| CAO governed | CAO-controlled execution | CAO policy |

Mapping to OpenCode agents/config happens in adapter config.

## Session Steering

Beaver Board's steer/stop UI works with OpenCode:

- **Server mode**: Steering sent to OpenCode server API
- **CLI mode**: Steering creates follow-up run/comment (not real-time)

**Important**: UI never pretends steering worked if runner cannot accept it.

## Best Practices

### 1. Start with LegacyClaude

Keep existing automations using LegacyClaude until OpenCode is fully tested.

### 2. Test DirectOpenCode on Low-Risk Tickets

Start with low-risk tickets to validate OpenCode integration.

### 3. Configure Worktrees

Enable worktree per card for isolation and safety.

### 4. Set Up Policies

Configure policies to require CAO for high-risk tickets.

### 5. Monitor Failure Logbook

Regularly check failure logbook for issues.

### 6. Keep OpenCode Updated

Update OpenCode CLI/server regularly for latest features and fixes.

## Troubleshooting

### OpenCode Not Found

**Error**: `opencode-not-installed`

**Solution**:
```bash
# Install OpenCode CLI
npm install -g @opencode-ai/cli
# or
pnpm add -g @opencode-ai/cli
```

### Provider Not Configured

**Error**: `provider-not-configured`

**Solution**:
```bash
# Configure provider auth
opencode auth login
# Select your provider and authenticate
```

### Model Not Found

**Error**: `model-not-found`

**Solution**:
1. Check model name is correct
2. Check provider supports the model
3. Refresh model catalog
4. Select a different model

### Worktree Creation Failed

**Error**: `worktree-create-failed`

**Solution**:
1. Check workspace is a git repository
2. Check write permissions
3. Check disk space
4. Try manual worktree creation

### Steering Not Supported

**Error**: `steer-not-supported`

**Solution**:
1. Use server mode for steering support
2. Or accept that CLI mode doesn't support real-time steering
3. Use follow-up comments instead

## Migration from PR-3

If you were using the previous PR-3 implementation:

### Changes Required

1. **Update RunAgentActionSpec**:
   - `ExecutionMode` replaces `executionMode`
   - `Provider` replaces `provider`
   - `Profile` replaces `profile`
   - `UseEffectiveConfig` is new
   - `RunnerKind` is new (optional)

2. **Update Configuration**:
   - Move OpenCode config to `Integrations/OpenCode/`
   - Use new configuration classes
   - Update service registration

3. **Update Tests**:
   - Use new interfaces and classes
   - Update namespace references

### Backward Compatibility

- Existing Claude automations work without changes
- New fields are optional
- Fallback to LegacyClaude when OpenCode unavailable

## Future Enhancements

### PR-5: Worktree per Card Integration
- Full git worktree support
- Branch creation and management
- Merge and cleanup workflows

### PR-6: OpenCode Provider/Model Catalog
- Dynamic catalog refresh
- Provider health checks
- Model availability detection

### PR-7: Steering Compatibility
- Server mode steering
- CLI mode fallback
- UI state management

### PR-8: Policies and Done Gate
- Full policy implementation
- Done gate enforcement
- Failure logbook

### PR-9: CAO Compatibility Mode
- CaoRunner implementation
- CAO task/run/evidence
- Closeout integration

### PR-10: Orchestration Center
- Centralized orchestration UI
- Role routing
- Skills matrix
- Health monitoring
