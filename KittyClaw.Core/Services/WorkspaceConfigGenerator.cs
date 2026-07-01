using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// Generates workspace configuration files for OpenCode integration.
/// Creates opencode.json with explicit models for each agent.
/// </summary>
public sealed class WorkspaceConfigGenerator
{
    private readonly ILogger? _logger;

    public WorkspaceConfigGenerator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate OpenCode configuration for a workspace.
    /// </summary>
    public async Task GenerateOpenCodeConfigAsync(
        string workspacePath,
        string projectSlug,
        string? defaultProvider = null,
        string? defaultModel = null,
        CancellationToken ct = default)
    {
        var configPath = Path.Combine(workspacePath, "opencode.json");
        
        // Don't overwrite if exists
        if (File.Exists(configPath))
        {
            _logger?.LogInformation("opencode.json already exists at {Path}, skipping", configPath);
            return;
        }

        var config = new OpenCodeWorkspaceConfig
        {
            Project = new OpenCodeProjectConfig
            {
                Name = projectSlug,
                Description = $"Beaver Board project: {projectSlug}"
            },
            Providers = new Dictionary<string, OpenCodeProviderConfig>(),
            Agents = new Dictionary<string, OpenCodeAgentConfig>()
        };

        // Add default provider if specified
        if (!string.IsNullOrEmpty(defaultProvider))
        {
            config.Providers[defaultProvider] = new OpenCodeProviderConfig
            {
                Enabled = true,
                Model = defaultModel ?? "default"
            };
        }

        // Add agent configurations with explicit models
        config.Agents["orchestrator"] = new OpenCodeAgentConfig
        {
            Mode = "primary",
            Model = defaultModel ?? "anthropic/claude-3-5-sonnet",
            Prompt = "{file:./.agents/orchestrator/SKILL.md}",
            Permissions = new Dictionary<string, string>
            {
                ["read"] = "allow",
                ["edit"] = "ask",
                ["bash"] = "ask"
            }
        };

        config.Agents["programmer"] = new OpenCodeAgentConfig
        {
            Mode = "subagent",
            Model = defaultModel ?? "anthropic/claude-3-5-sonnet",
            Prompt = "{file:./.agents/programmer/SKILL.md}",
            Permissions = new Dictionary<string, string>
            {
                ["read"] = "allow",
                ["edit"] = "allow",
                ["bash"] = "ask"
            }
        };

        config.Agents["reviewer"] = new OpenCodeAgentConfig
        {
            Mode = "subagent",
            Model = defaultModel ?? "anthropic/claude-3-5-sonnet",
            Prompt = "{file:./.agents/reviewer/SKILL.md}",
            Permissions = new Dictionary<string, string>
            {
                ["read"] = "allow",
                ["edit"] = "deny",
                ["bash"] = "ask"
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(configPath, json, ct);

        _logger?.LogInformation("Generated opencode.json at {Path}", configPath);
    }

    /// <summary>
    /// Generate project rules file (CLAUDE.md update or .beaverboard/rules.md).
    /// </summary>
    public async Task GenerateProjectRulesAsync(
        string workspacePath,
        string projectSlug,
        CancellationToken ct = default)
    {
        var rulesDir = Path.Combine(workspacePath, ".beaverboard");
        Directory.CreateDirectory(rulesDir);

        var rulesPath = Path.Combine(rulesDir, "rules.md");
        
        // Don't overwrite if exists
        if (File.Exists(rulesPath))
        {
            _logger?.LogInformation("rules.md already exists at {Path}, skipping", rulesPath);
            return;
        }

        var rules = $@"# {projectSlug} — Project Rules

## Beaver Board Integration

This project is managed by Beaver Board Kanban.

### Agent Roles

- **Orchestrator**: Manages workflow, assigns tasks, reviews results
- **Programmer**: Implements code changes
- **Reviewer**: Reviews code, runs tests, validates changes

### Execution Rules

1. Each card can be decomposed into child cards
2. Parent completion requires all required leaf tasks to be done
3. Parallel tasks require separate worktrees
4. All changes must pass review before Done

### Evidence Requirements

- Code changes: diff + test results
- Architecture changes: decision record
- Bug fixes: reproduction steps + fix verification

### Health Rules

- Failed runs create Health Center events
- Stuck runs (>10 min no output) trigger warnings
- Missing evidence blocks Done gate

---

Generated by Beaver Board Kanban
";

        await File.WriteAllTextAsync(rulesPath, rules, ct);
        _logger?.LogInformation("Generated rules.md at {Path}", rulesPath);
    }
}

// ── Configuration models ──────────────────────────────────────────────

public sealed class OpenCodeWorkspaceConfig
{
    public OpenCodeProjectConfig? Project { get; set; }
    public Dictionary<string, OpenCodeProviderConfig> Providers { get; set; } = new();
    public Dictionary<string, OpenCodeAgentConfig> Agents { get; set; } = new();
}

public sealed class OpenCodeProjectConfig
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class OpenCodeProviderConfig
{
    public bool Enabled { get; set; } = true;
    public string Model { get; set; } = "default";
}

public sealed class OpenCodeAgentConfig
{
    public string Mode { get; set; } = "subagent"; // primary, subagent, all
    public string Model { get; set; } = "";
    public string? Prompt { get; set; }
    public Dictionary<string, string>? Permissions { get; set; }
}
