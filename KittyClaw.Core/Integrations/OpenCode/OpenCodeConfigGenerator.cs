using System.Text.Json;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runtimes;

namespace KittyClaw.Core.Integrations.OpenCode;

/// <summary>
/// Generates OpenCode configuration (opencode.json) with explicit models for each subagent.
/// This ensures subagents do NOT inherit models from parent agents.
/// 
/// Key rule: If parent has smart model, subagents do NOT inherit automatically.
/// Each subagent slot must have explicit model.
/// </summary>
public sealed class OpenCodeConfigGenerator
{
    private readonly RosterStore _rosterStore;
    private readonly Dictionary<string, ModelProfileConfig> _profiles;

    public OpenCodeConfigGenerator(
        RosterStore rosterStore,
        Dictionary<string, ModelProfileConfig> profiles)
    {
        _rosterStore = rosterStore;
        _profiles = profiles;
    }

    /// <summary>
    /// Generate OpenCode agent configuration with explicit models for each slot.
    /// </summary>
    public OpenCodeAgentConfig GenerateConfig()
    {
        var config = new OpenCodeAgentConfig
        {
            Agent = new Dictionary<string, OpenCodeAgentEntry>()
        };

        var activePreset = _rosterStore.GetActivePreset();
        if (activePreset is null)
        {
            // No active preset - generate minimal config
            return config;
        }

        foreach (var (slotId, slotConfig) in activePreset.Slots)
        {
            var slot = _rosterStore.GetSlot(slotId);
            if (slot is null) continue;

            var profileId = slotConfig.ModelProfileId ?? slot.ActiveModelProfileId;
            if (string.IsNullOrEmpty(profileId) || !_profiles.TryGetValue(profileId, out var profile))
            {
                continue;
            }

            // Determine agent mode based on role
            var mode = slot.Role == "orchestrator" ? "primary" : "subagent";
            
            // Get the full model string (provider/model-id)
            var model = profile.OpencodeModel ?? profile.Model;
            if (string.IsNullOrEmpty(model)) continue;

            config.Agent[slotConfig.OpencodeAgent ?? slot.OpencodeAgent] = new OpenCodeAgentEntry
            {
                Mode = mode,
                Model = model,
                Prompt = $"{{file:./.beaver/agents/{slot.Role}.md}}",
                Permission = GeneratePermissions(slot.Role)
            };
        }

        return config;
    }

    /// <summary>
    /// Generate permissions for a given role.
    /// </summary>
    private OpenCodePermissions GeneratePermissions(string role)
    {
        return role switch
        {
            "orchestrator" => new OpenCodePermissions
            {
                Edit = "ask",
                Bash = "ask",
                Task = new Dictionary<string, string>
                {
                    ["*"] = "deny",
                    ["beaver-programmer-*"] = "allow",
                    ["beaver-reviewer"] = "ask"
                }
            },
            "programmer" => new OpenCodePermissions
            {
                Edit = "allow",
                Bash = new Dictionary<string, string>
                {
                    ["*"] = "ask",
                    ["npm test*"] = "allow",
                    ["dotnet test*"] = "allow",
                    ["git diff*"] = "allow",
                    ["git status*"] = "allow",
                    ["git push*"] = "deny"
                }
            },
            "reviewer" => new OpenCodePermissions
            {
                Edit = "deny",
                Bash = new Dictionary<string, string>
                {
                    ["*"] = "ask",
                    ["git diff*"] = "allow",
                    ["grep *"] = "allow"
                }
            },
            _ => new OpenCodePermissions
            {
                Edit = "ask",
                Bash = "ask"
            }
        };
    }

    /// <summary>
    /// Serialize config to JSON for opencode.json.
    /// </summary>
    public string ToJson(bool indent = true)
    {
        var config = GenerateConfig();
        var options = new JsonSerializerOptions
        {
            WriteIndented = indent,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(config, options);
    }
}

// ── OpenCode config serialization models ──────────────────────────────

public sealed class OpenCodeAgentConfig
{
    public Dictionary<string, OpenCodeAgentEntry> Agent { get; set; } = new();
}

public sealed class OpenCodeAgentEntry
{
    public string Mode { get; set; } = "all"; // primary, subagent, all
    public string Model { get; set; } = "";
    public string? Prompt { get; set; }
    public OpenCodePermissions? Permission { get; set; }
}

public sealed class OpenCodePermissions
{
    public string? Edit { get; set; }
    
    // Bash can be a simple string ("ask", "allow", "deny") or a dictionary of patterns
    public object? Bash { get; set; }
    
    // Task permissions for spawning subagents
    public Dictionary<string, string>? Task { get; set; }
}
