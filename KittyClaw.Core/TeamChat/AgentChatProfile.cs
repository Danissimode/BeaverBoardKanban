namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Per-agent chat behavior profile. Controls how an agent behaves in team chat.
/// </summary>
public sealed class AgentChatProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string ProjectSlug { get; set; }
    public required string AgentId { get; set; }
    public required string RoleId { get; set; }
    public string DisplayName { get; set; } = "";
    public string RoleName { get; set; } = "";

    /// <summary>Agent can read team chat messages</summary>
    public bool CanReadTeamChat { get; set; } = true;

    /// <summary>Agent can post to team chat</summary>
    public bool CanPostToTeamChat { get; set; } = true;

    /// <summary>Agent can reply to other agents</summary>
    public bool CanReplyToAgents { get; set; } = true;

    /// <summary>Agent can ask human questions</summary>
    public bool CanAskHuman { get; set; } = true;

    /// <summary>Agent can receive direct @mentions</summary>
    public bool CanReceiveDirectMentions { get; set; } = true;

    /// <summary>Agent can receive @team broadcasts</summary>
    public bool CanReceiveTeamMentions { get; set; } = true;

    /// <summary>quiet | normal | verbose</summary>
    public string Verbosity { get; set; } = "normal";

    /// <summary>silent | important-only | checkpoints | verbose</summary>
    public string SignalPolicy { get; set; } = "important-only";

    /// <summary>only-when-mentioned | when-addressed-or-relevant | proactive</summary>
    public string ResponsePolicy { get; set; } = "when-addressed-or-relevant";

    public string? SystemPromptAddon { get; set; }
    public string? ChatRulesMarkdown { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Default agent chat profiles for all roles.
/// </summary>
public static class DefaultAgentChatProfiles
{
    public static AgentChatProfile CreateDefault(string projectSlug, string agentId, string roleId) => new()
    {
        ProjectSlug = projectSlug,
        AgentId = agentId,
        RoleId = roleId,
        DisplayName = agentId,
        RoleName = roleId,
        Verbosity = "normal",
        SignalPolicy = "important-only",
        ResponsePolicy = "when-addressed-or-relevant"
    };

    public static readonly Dictionary<string, (string Verbosity, string SignalPolicy, string ResponsePolicy)> Defaults = new()
    {
        ["planner"] = ("normal", "checkpoints", "when-addressed-or-relevant"),
        ["builder"] = ("normal", "important-only", "when-addressed-or-relevant"),
        ["reviewer"] = ("normal", "important-only", "when-addressed-or-relevant"),
        ["tester"] = ("normal", "important-only", "when-addressed-or-relevant"),
        ["committer"] = ("quiet", "checkpoints", "only-when-mentioned"),
        ["documentalist"] = ("quiet", "checkpoints", "only-when-mentioned"),
        ["code-janitor"] = ("quiet", "important-only", "only-when-mentioned"),
        ["evaluator"] = ("normal", "checkpoints", "when-addressed-or-relevant"),
        ["producer"] = ("normal", "checkpoints", "proactive"),
    };
}
