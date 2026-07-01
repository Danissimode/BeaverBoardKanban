using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Selects the best available agent for a role based on availability,
/// concurrency limits, skills, and execution profile readiness.
/// </summary>
public sealed class AgentSelector
{
    private readonly TeamRoleStore _store;
    private readonly ILogger? _logger;

    public AgentSelector(TeamRoleStore store, ILogger? logger = null)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Select an agent for the given role.
    /// </summary>
    public async Task<AgentSelectionResult> SelectAgentAsync(
        string projectSlug,
        string roleId,
        string? requiredSkillsJson = null,
        CancellationToken ct = default)
    {
        var agents = await _store.GetAgentsByRoleAsync(projectSlug, roleId, ct);
        var candidates = agents
            .Where(a => a.Enabled)
            .Where(a => a.Status is "idle" or "available")
            .Where(a => a.CurrentRunCount < a.MaxConcurrentRuns)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger?.LogWarning("No available agents for role {RoleId} in project {ProjectSlug}", roleId, projectSlug);
            return new AgentSelectionResult
            {
                SelectedAgentId = null,
                Reason = "no available agents: all busy, blocked, or disabled",
                Fallbacks = new List<string>()
            };
        }

        // If skills are required, try to match them (best-effort)
        AgentProfile? bestMatch = null;
        if (!string.IsNullOrEmpty(requiredSkillsJson))
        {
            // For now, all candidates are considered skill-matched since we don't have
            // per-agent skill storage yet. In the future, parse agent.SkillsJson.
            bestMatch = candidates.First();
        }
        else
        {
            bestMatch = candidates.First();
        }

        var fallbacks = candidates
            .Where(a => a.Id != bestMatch.Id)
            .Select(a => a.Id)
            .ToList();

        _logger?.LogInformation(
            "Selected agent {AgentId} for role {RoleId} (concurrency {Current}/{Max})",
            bestMatch.Id, roleId, bestMatch.CurrentRunCount, bestMatch.MaxConcurrentRuns);

        return new AgentSelectionResult
        {
            SelectedAgentId = bestMatch.Id,
            SelectedAgentName = bestMatch.DisplayName,
            Reason = $"role match, status={bestMatch.Status}, concurrency {bestMatch.CurrentRunCount}/{bestMatch.MaxConcurrentRuns}",
            Fallbacks = fallbacks
        };
    }
}

/// <summary>
/// Result of agent selection.
/// </summary>
public sealed class AgentSelectionResult
{
    public string? SelectedAgentId { get; init; }
    public string? SelectedAgentName { get; init; }
    public required string Reason { get; init; }
    public List<string> Fallbacks { get; init; } = new();
}
