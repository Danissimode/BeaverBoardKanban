using KittyClaw.Core.Automation.Runners;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Evaluates role policies for workflow actions.
/// Enforces governance rules before mutating or execution actions.
/// </summary>
public sealed class RolePolicyEvaluator
{
    private readonly TeamRoleStore _store;
    private readonly ILogger? _logger;

    public RolePolicyEvaluator(TeamRoleStore store, ILogger? logger = null)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Check if a role can perform an action in the given context.
    /// </summary>
    public async Task<PolicyDecision> CanAsync(
        string projectSlug,
        string roleId,
        string action,
        RolePolicyContext? context = null,
        CancellationToken ct = default)
    {
        // Owner and orchestrator bypass most policies (except high-risk execution)
        if (roleId == RoleSlugs.Owner)
        {
            return PolicyDecision.Allow();
        }

        // Load policies for the role
        var policies = await _store.GetPoliciesAsync(projectSlug, roleId, ct);
        
        // Check explicit deny policies first
        var denyPolicy = policies
            .Where(p => p.Enabled)
            .FirstOrDefault(p => ActionMatches(p.Action, action) && p.Effect == "deny");
        
        if (denyPolicy is not null)
        {
            _logger?.LogWarning(
                "Policy denied: role {RoleId} action {Action} — {Reason}",
                roleId, action, denyPolicy.Reason);
            
            return PolicyDecision.Deny(
                denyPolicy.Reason ?? $"Policy denies '{action}' for role '{roleId}'",
                "Check role policy configuration");
        }

        // Check explicit allow policies
        var allowPolicy = policies
            .Where(p => p.Enabled)
            .FirstOrDefault(p => ActionMatches(p.Action, action) && p.Effect == "allow");
        
        if (allowPolicy is not null)
        {
            return PolicyDecision.Allow();
        }

        // Default built-in rules when no explicit policy exists
        var defaultDecision = EvaluateDefaultRules(roleId, action, context);
        if (!defaultDecision.Allowed)
        {
            _logger?.LogWarning(
                "Default policy denied: role {RoleId} action {Action} — {Reason}",
                roleId, action, defaultDecision.Reason);
            return defaultDecision;
        }

        return PolicyDecision.Allow();
    }

    private static PolicyDecision EvaluateDefaultRules(string roleId, string action, RolePolicyContext? context)
    {
        // Programmer cannot move ticket directly to Done
        if (roleId == RoleSlugs.Programmer && action == Capabilities.TicketMoveDone)
        {
            return PolicyDecision.Deny(
                "Programmer cannot move ticket directly to Done. Validator approval required.",
                "Request validator review");
        }

        // Planner cannot apply decomposition without approval
        if (roleId == RoleSlugs.Planner && action == Capabilities.DecomposeApply)
        {
            return PolicyDecision.Deny(
                "Planner cannot apply decomposition without owner approval.",
                "Request owner approval for decomposition plan");
        }

        // Orchestrator requires approval for high-risk execution actions
        if (roleId == RoleSlugs.Orchestrator && action == Capabilities.TicketRun)
        {
            var risk = context?.RiskLevel ?? "medium";
            if (risk is "high" or "critical")
            {
                return PolicyDecision.Deny(
                    "High-risk execution requires explicit owner approval.",
                    "Request owner approval");
            }
        }

        // Validator can move approved ticket to Done
        if (roleId == RoleSlugs.Validator && action == Capabilities.TicketMoveDone)
        {
            var isApproved = context?.IsApproved ?? false;
            if (!isApproved)
            {
                return PolicyDecision.Deny(
                    "Validator can only move approved tickets to Done.",
                    "Ensure ticket has passed review");
            }
        }

        return PolicyDecision.Allow();
    }

    private static bool ActionMatches(string policyAction, string requestedAction)
    {
        // Exact match
        if (policyAction.Equals(requestedAction, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard match (e.g., "ticket.*" matches "ticket.run")
        if (policyAction.EndsWith("*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = policyAction.TrimEnd('*');
            if (requestedAction.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Context for role policy evaluation.
/// </summary>
public sealed class RolePolicyContext
{
    public string? RiskLevel { get; init; }
    public bool IsApproved { get; init; }
    public int? TicketId { get; init; }
    public string? AgentId { get; init; }
}
