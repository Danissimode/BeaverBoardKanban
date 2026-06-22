using KittyClaw.Core.Models;

namespace KittyClaw.Core.Automation.Runtimes;

/// <summary>
/// Resolves three independent orchestration dimensions for a ticket:
/// CLI Runtime, CAO Role, and Model Profile.
/// Each dimension has its own fallback chain (ticket → member → runtime/role → project default).
/// </summary>
public sealed class AgentOrchestrationResolver
{
    private readonly AgentRuntimeProjectConfig _config;

    public AgentOrchestrationResolver(AgentRuntimeProjectConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Resolves the CLI runtime to use for a ticket.
    /// Priority: ticket.CliRuntimeId → RuntimeByMember[assignee] → project.DefaultRuntime
    /// </summary>
    public string ResolveRuntime(Ticket ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.CliRuntimeId))
            return ticket.CliRuntimeId;

        if (!string.IsNullOrWhiteSpace(ticket.AssignedTo)
            && _config.RuntimeByMember.TryGetValue(ticket.AssignedTo, out var mappedRuntime))
            return mappedRuntime;

        return _config.DefaultRuntime;
    }

    /// <summary>
    /// Resolves the CAO role to apply for a ticket.
    /// Priority: ticket.CaoRoleId → RoleByMember[assignee] → RoleByRuntime[runtimeId] → project.DefaultRole
    /// </summary>
    public string ResolveRole(Ticket ticket, string runtimeId)
    {
        if (!string.IsNullOrWhiteSpace(ticket.CaoRoleId))
            return ticket.CaoRoleId;

        if (!string.IsNullOrWhiteSpace(ticket.AssignedTo)
            && _config.RoleByMember.TryGetValue(ticket.AssignedTo, out var mappedRole))
            return mappedRole;

        if (_config.RoleByRuntime.TryGetValue(runtimeId, out var runtimeRole))
            return runtimeRole;

        return _config.DefaultRole;
    }

    /// <summary>
    /// Resolves the model profile to use for a ticket.
    /// Priority: ticket.ModelProfileId → ModelProfileByRole[roleId] → ModelProfileByRuntime[runtimeId] → project.DefaultModelProfile
    /// </summary>
    public string ResolveModelProfile(Ticket ticket, string roleId, string runtimeId)
    {
        if (!string.IsNullOrWhiteSpace(ticket.ModelProfileId))
            return ticket.ModelProfileId;

        if (_config.ModelProfileByRole.TryGetValue(roleId, out var mappedByRole))
            return mappedByRole;

        if (_config.ModelProfileByRuntime.TryGetValue(runtimeId, out var mappedByRuntime))
            return mappedByRuntime;

        return _config.DefaultModelProfile;
    }

    /// <summary>
    /// Checks if any of the ticket labels match the project's high-risk label set.
    /// </summary>
    public bool IsHighRisk(IReadOnlyList<string> labels) =>
        labels.Any(l => _config.HighRiskLabels.Contains(l, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the resolved role config, or a default safe config if not found.
    /// </summary>
    public CaoRoleConfig GetRoleConfig(string roleId)
    {
        if (_config.Roles.TryGetValue(roleId, out var cfg))
            return cfg;

        return new CaoRoleConfig
        {
            Id = roleId,
            DisplayName = roleId,
            Description = "Fallback role config (no explicit policy defined).",
        };
    }

    /// <summary>
    /// Gets the resolved model profile config, or a default if not found.
    /// </summary>
    public ModelProfileConfig GetModelProfileConfig(string profileId)
    {
        if (_config.ModelProfiles.TryGetValue(profileId, out var cfg))
            return cfg;

        return new ModelProfileConfig
        {
            Id = profileId,
            DisplayName = profileId,
            Model = profileId,
        };
    }
}
