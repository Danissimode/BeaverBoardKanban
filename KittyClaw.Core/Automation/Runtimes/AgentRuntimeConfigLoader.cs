using System.Text.Json;

namespace KittyClaw.Core.Automation.Runtimes;

public class AgentRuntimeConfigLoader
{
    private readonly string _dataDir;

    public AgentRuntimeConfigLoader(string dataDir)
    {
        _dataDir = dataDir;
    }

    public AgentRuntimeProjectConfig? Load(string projectSlug)
    {
        var paths = new List<string>();
        paths.Add(Path.Combine(_dataDir, "runtimes", $"{projectSlug}.json"));

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<AgentRuntimeProjectConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                    });
                    if (config is not null) return config;
                }
                catch { /* best-effort fallback */ }
            }
        }

        return null;
    }

    public AgentRuntimeProjectConfig? Load(string projectSlug, string workspacePath)
    {
        var paths = new List<string>();
        if (!string.IsNullOrEmpty(workspacePath))
        {
            paths.Add(Path.Combine(workspacePath, ".kittyclaw", "runtimes.json"));
        }
        paths.Add(Path.Combine(_dataDir, "runtimes", $"{projectSlug}.json"));

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<AgentRuntimeProjectConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                    });
                    if (config is not null) return config;
                }
                catch { /* best-effort fallback */ }
            }
        }

        return null;
    }

    public virtual AgentRuntimeProjectConfig CreateDefault(string projectSlug, string workspacePath, string defaultRuntime = "mimo-code")
    {
        return new AgentRuntimeProjectConfig
        {
            ProjectSlug = projectSlug,
            WorkspacePath = workspacePath,
            DefaultRuntime = defaultRuntime,
            DefaultRole = CaoRoleIds.Developer,
            DefaultModelProfile = "petpals-coder",
            HighRiskLabels = new[] { "security", "rls", "payments", "stripe", "critical" },
            RuntimeByMember = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "codex", "mimo-code" },
                { "mimo", "mimo-code" },
                { "opencode", "opencode" },
                { "qa", "script" },
                { "copilot", "github-copilot" },
                { "vibe", "vibe" },
                { "kimi", "kimi-code" },
                { "antigravity", "antigravity" },
            },
            RoleByMember = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "mimo", CaoRoleIds.Developer },
                { "codex", CaoRoleIds.Developer },
                { "opencode", CaoRoleIds.Developer },
                { "vibe", CaoRoleIds.Planner },
                { "kimi", CaoRoleIds.Researcher },
                { "qa", CaoRoleIds.Qa },
                { "antigravity", CaoRoleIds.Reviewer },
            },
            RoleByRuntime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "mimo-code", CaoRoleIds.Developer },
                { "codex", CaoRoleIds.Developer },
                { "opencode", CaoRoleIds.Developer },
                { "vibe", CaoRoleIds.Planner },
                { "kimi-code", CaoRoleIds.Researcher },
                { "github-copilot", CaoRoleIds.Developer },
                { "antigravity", CaoRoleIds.Reviewer },
                { "script", CaoRoleIds.Qa },
                { "claude-code", CaoRoleIds.Developer },
            },
            ModelProfileByRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { CaoRoleIds.Planner, "petpals-planner" },
                { CaoRoleIds.Developer, "petpals-coder" },
                { CaoRoleIds.Reviewer, "petpals-reviewer" },
                { CaoRoleIds.SecurityReviewer, "petpals-security-reviewer" },
                { CaoRoleIds.Researcher, "petpals-researcher" },
                { CaoRoleIds.Qa, "petpals-cheap" },
            },
            ModelProfileByRuntime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "mimo-code", "petpals-coder" },
                { "codex", "petpals-coder" },
                { "kimi-code", "petpals-researcher" },
                { "script", "petpals-cheap" },
            },
            Runtimes = new Dictionary<string, AgentRuntimeConfig>(StringComparer.OrdinalIgnoreCase)
            {
                // ... existing runtime configs
            },
            Roles = new Dictionary<string, CaoRoleConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    CaoRoleIds.Supervisor, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Supervisor,
                        DisplayName = "Supervisor",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = false,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "assign", "handoff", "comment", "status-read" }
                    }
                },
                {
                    CaoRoleIds.Planner, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Planner,
                        DisplayName = "Planner",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = false,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "comment", "status-read" }
                    }
                },
                {
                    CaoRoleIds.Developer, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Developer,
                        DisplayName = "Developer",
                        CanEditFiles = true,
                        CanRunShell = true,
                        CanRunTests = true,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = false,
                        AllowedTools = new[] { "read", "edit", "test", "comment" }
                    }
                },
                {
                    CaoRoleIds.Reviewer, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Reviewer,
                        DisplayName = "Reviewer",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = true,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "test", "comment", "request-changes" }
                    }
                },
                {
                    CaoRoleIds.Qa, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Qa,
                        DisplayName = "QA",
                        CanEditFiles = false,
                        CanRunShell = true,
                        CanRunTests = true,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "test", "comment", "request-changes" }
                    }
                },
                {
                    CaoRoleIds.SecurityReviewer, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.SecurityReviewer,
                        DisplayName = "Security Reviewer",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = true,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "test", "comment", "security-review" }
                    }
                },
                {
                    CaoRoleIds.Researcher, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Researcher,
                        DisplayName = "Researcher",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = false,
                        CanUseNetwork = true,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "research", "comment" }
                    }
                },
                {
                    CaoRoleIds.Critic, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Critic,
                        DisplayName = "Critic",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = false,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "comment", "challenge", "request-changes" }
                    }
                },
                {
                    CaoRoleIds.Explainer, new CaoRoleConfig
                    {
                        Id = CaoRoleIds.Explainer,
                        DisplayName = "Explainer",
                        CanEditFiles = false,
                        CanRunShell = false,
                        CanRunTests = false,
                        CanUseNetwork = false,
                        CanApprove = false,
                        AllowHighRisk = true,
                        AllowedTools = new[] { "read", "comment", "summarize" }
                    }
                },
            },
            ModelProfiles = new Dictionary<string, ModelProfileConfig>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "petpals-planner", new ModelProfileConfig
                    {
                        Id = "petpals-planner",
                        DisplayName = "Planner",
                        Model = "petpals-planner",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                    }
                },
                {
                    "petpals-coder", new ModelProfileConfig
                    {
                        Id = "petpals-coder",
                        DisplayName = "Coder",
                        Model = "petpals-coder",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                    }
                },
                {
                    "petpals-reviewer", new ModelProfileConfig
                    {
                        Id = "petpals-reviewer",
                        DisplayName = "Reviewer",
                        Model = "petpals-reviewer",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                    }
                },
                {
                    "petpals-security-reviewer", new ModelProfileConfig
                    {
                        Id = "petpals-security-reviewer",
                        DisplayName = "Security Reviewer",
                        Model = "petpals-security-reviewer",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                        HighRiskAllowed = true,
                    }
                },
                {
                    "petpals-researcher", new ModelProfileConfig
                    {
                        Id = "petpals-researcher",
                        DisplayName = "Researcher",
                        Model = "petpals-researcher",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                    }
                },
                {
                    "petpals-cheap", new ModelProfileConfig
                    {
                        Id = "petpals-cheap",
                        DisplayName = "Cheap",
                        Model = "petpals-cheap",
                        Provider = "litellm",
                        BaseUrl = "http://localhost:4000/v1",
                        ApiKeyEnv = "LITELLM_API_KEY",
                    }
                },
            }
        };
    }

    public void Save(AgentRuntimeProjectConfig config)
    {
        var dir = Path.Combine(_dataDir, "runtimes");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{config.ProjectSlug}.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        File.WriteAllText(path, json);
    }
}
