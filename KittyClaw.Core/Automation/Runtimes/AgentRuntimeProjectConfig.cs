namespace KittyClaw.Core.Automation.Runtimes;

public sealed class AgentRuntimeProjectConfig
{
    public required string ProjectSlug { get; init; }
    public required string WorkspacePath { get; init; }
    public required string DefaultRuntime { get; init; } = "mimo-code";
    public string DefaultRole { get; init; } = CaoRoleIds.Developer;
    public string DefaultModelProfile { get; init; } = "default";
    public IReadOnlyList<string> HighRiskLabels { get; init; } = new[] { "security", "rls", "payments", "stripe", "critical" };
    public IReadOnlyDictionary<string, string> RuntimeByMember { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> RoleByMember { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> RoleByRuntime { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> ModelProfileByRole { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> ModelProfileByRuntime { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, AgentRuntimeConfig> Runtimes { get; init; } = new Dictionary<string, AgentRuntimeConfig>();
    public IReadOnlyDictionary<string, CaoRoleConfig> Roles { get; init; } = new Dictionary<string, CaoRoleConfig>();
    public IReadOnlyDictionary<string, ModelProfileConfig> ModelProfiles { get; init; } = new Dictionary<string, ModelProfileConfig>();
}
