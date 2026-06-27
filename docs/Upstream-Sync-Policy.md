# Upstream Sync Policy

This document explains how KittyClawOpen maintains compatibility with the official KittyClaw repository while adding OpenCode integration.

## Architecture Overview

The integration follows a **three-zone architecture** to ensure upstream compatibility:

### Zone A: Core-Safe Abstractions (KittyClaw.Core/Automation/Runners/)

These are **stable extension points** that are generic and not OpenCode-specific:

- `IAgentRunner` - Generic interface for all agent runners
- `RunnerRegistry` - Registry for managing runners
- `ITicketExecutionMetadataStore` - Interface for execution metadata storage
- `IExecutionPolicyService` - Interface for execution policy decisions
- `IProviderModelCatalog` - Interface for provider/model catalog
- `IWorktreeService` - Interface for worktree management

**Rule**: Core does NOT know about:
- OpenCode
- CAO
- Qwen
- OpenRouter
- LiteLLM
- Any other specific providers

Core only knows about:
- runner
- provider
- model
- execution mode
- ticket metadata
- run lifecycle

### Zone B: OpenCode Integration Package (KittyClaw.Core/Integrations/OpenCode/)

All OpenCode-specific code is isolated here:

- `OpenCodeRunner` - IAgentRunner implementation for OpenCode
- `OpenCodeConfig` - Configuration for OpenCode runner
- `OpenCodeProviderModelCatalog` - IProviderModelCatalog implementation
- `OpenCodeExecutionPolicyService` - IExecutionPolicyService implementation
- `WorktreeService` - IWorktreeService implementation
- `IWorktreeService` - Interface (also used by core)

### Zone C: CAO-Specific Layer (Future)

Will be added in:
- `KittyClaw.Core/Integrations/Cao/`

CAO mode will be a separate runner/policy, not mandatory for all OpenCode tasks.

## Branching Model

```
main                  tracks your stable fork
upstream/main         official KittyClaw
feature/opencode-*    integration PRs
```

### Add Upstream Remote

```bash
git remote add upstream https://github.com/Ekioo/KittyClaw.git
```

### Update Flow

```bash
# Fetch latest from upstream
git fetch upstream

# Checkout your main branch
git checkout main

# Merge upstream changes
git merge upstream/main

# Run tests
dotnet test

# Resolve any conflicts (should be minimal)
git add .
git commit -m "Merge upstream changes"

# Push to your fork
git push origin main
```

## Isolation Rules

### Do NOT Modify

- Existing `ClaudeRunner` logic (except for adapter extraction)
- Existing `AutomationEngine` core logic (except for adding generic interfaces)
- Existing project templates
- Existing schema without migrations

### Only Modify to Add

- Generic interfaces in Zone A
- New files in Zone B
- Configuration extensions
- New services that implement generic interfaces

### Upstream-Friendly Changes

✅ **Allowed**:
- Adding new interfaces in `Runners/` namespace
- Adding new implementation files in `Integrations/OpenCode/`
- Adding new configuration classes
- Adding new services that implement generic interfaces
- Modifying `ActionExecutor` to use generic interfaces (additive)
- Modifying `AutomationEngine` to accept new optional parameters

❌ **Not Allowed**:
- Adding OpenCode-specific logic to `ClaudeRunner`
- Adding OpenCode-specific logic to core `AutomationEngine`
- Modifying existing project templates to require OpenCode
- Changing existing schema without backward compatibility
- Removing existing functionality

## Merge Conflict Resolution

### Common Conflict Areas

1. **Program.cs** - Service registration
   - Keep existing registrations
   - Add new registrations for OpenCode services

2. **ActionExecutor** - Constructor changes
   - Keep existing constructors for backward compatibility
   - Add new constructors with optional parameters

3. **AutomationEngine** - Constructor changes
   - Keep existing constructors
   - Add new optional parameters

### Conflict Resolution Strategy

1. **Always keep existing code** - Don't remove upstream functionality
2. **Use partial classes** - Split changes into separate files
3. **Use optional parameters** - Don't break existing callers
4. **Add, don't modify** - Prefer adding new methods over modifying existing ones

## Testing After Upstream Sync

### Manual Smoke Tests

1. **Existing Claude automation still works**
   ```bash
   # Create a ticket
   # Move to InProgress
   # Verify Claude runner starts
   # Verify output appears in run drawer
   ```

2. **OpenCode direct execution works**
   ```bash
   # Create a ticket with DirectOpenCode mode
   # Move to InProgress
   # Verify OpenCode runner starts
   # Verify output appears in run drawer
   ```

3. **Stop/steer still works**
   ```bash
   # Start a run
   # Click Stop button
   # Verify run stops
   # Try steering (if supported)
   ```

4. **Worktree creation works**
   ```bash
   # Create a ticket with worktree enabled
   # Move to InProgress
   # Verify worktree is created
   # Verify run executes in worktree
   ```

### Automated Tests

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test KittyClaw.Core.Tests
```

## Folder Structure

```
KittyClaw.Core/
├── Automation/
│   ├── Runners/                  # Zone A - Generic interfaces
│   │   ├── IAgentRunner.cs
│   │   ├── RunnerRegistry.cs
│   │   ├── ITicketExecutionMetadataStore.cs
│   │   ├── IExecutionPolicyService.cs
│   │   ├── IProviderModelCatalog.cs
│   │   └── IWorktreeService.cs
│   ├── ActionExecutor.Runners.cs # Runner integration
│   └── ActionExecutor.NewConstructor.cs # New constructor
│
└── Integrations/
    └── OpenCode/                # Zone B - OpenCode-specific
        ├── OpenCodeRunner.cs
        ├── OpenCodeConfig.cs
        ├── OpenCodeProviderModelCatalog.cs
        ├── OpenCodeExecutionPolicyService.cs
        ├── IWorktreeService.cs
        └── WorktreeService.cs

KittyClaw.Web/
└── Program.cs                  # Updated with service registration
```

## Version Compatibility

| KittyClawOpen Version | Official KittyClaw Version | Notes |
|------------------------|---------------------------|-------|
| Current | Latest | Full compatibility |

### Breaking Changes

None. All changes are additive and backward compatible.

### Migration Path

1. **From official KittyClaw**: Just clone KittyClawOpen and use it
2. **From older KittyClawOpen**: Pull latest changes

## Contributing

### For OpenCode Integration

1. Add new files in `KittyClaw.Core/Integrations/OpenCode/`
2. Implement generic interfaces from `KittyClaw.Core/Automation/Runners/`
3. Register services in `Program.cs`
4. Test with both Claude and OpenCode runners

### For Core Changes

1. Only add generic interfaces in `Runners/` namespace
2. Don't add provider-specific logic
3. Maintain backward compatibility
4. Test with existing Claude functionality

## Troubleshooting

### Merge Conflicts

If you get merge conflicts:

1. **Check what changed upstream**
   ```bash
   git diff upstream/main...HEAD
   ```

2. **Resolve conflicts carefully**
   - Keep upstream changes
   - Add your changes alongside
   - Don't remove upstream functionality

3. **Test thoroughly**
   - Run all tests
   - Manual smoke tests
   - Verify both Claude and OpenCode work

### Build Errors

If you get build errors after upstream merge:

1. **Check for API changes**
   - Upstream may have changed interfaces
   - Update your implementations accordingly

2. **Check for dependency changes**
   - Upstream may have updated dependencies
   - Update your project files

3. **Check for namespace changes**
   - Upstream may have reorganized code
   - Update your using statements

## Summary

**Key Principle**: KittyClawOpen should be able to merge official KittyClaw updates with **low conflict risk**.

**How**: By keeping all OpenCode-specific code isolated in Zone B and only adding generic extension points in Zone A.

**Result**: You can update from official KittyClaw regularly while maintaining all OpenCode integration features.
