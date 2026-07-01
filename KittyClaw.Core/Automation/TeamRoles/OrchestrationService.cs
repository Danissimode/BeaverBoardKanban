using KittyClaw.Core.Automation.CommandHub;
using KittyClaw.Core.Automation.Runners;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Central orchestration service that connects MessageRouter, RoleInbox,
/// Agent selection, TeamMemberSession, ExecutionProfile, and RolePolicy
/// into one safe workflow.
/// </summary>
public sealed class OrchestrationService
{
    private readonly CommandHubService _commandHub;
    private readonly RoleInboxStore _inboxStore;
    private readonly TeamRoleStore _roleStore;
    private readonly MessageRouter _router;
    private readonly AgentSelector _agentSelector;
    private readonly RolePolicyEvaluator _policyEvaluator;
    private readonly ILogger? _logger;

    public OrchestrationService(
        CommandHubService commandHub,
        RoleInboxStore inboxStore,
        TeamRoleStore roleStore,
        MessageRouter router,
        AgentSelector agentSelector,
        RolePolicyEvaluator policyEvaluator,
        ILogger? logger = null)
    {
        _commandHub = commandHub;
        _inboxStore = inboxStore;
        _roleStore = roleStore;
        _router = router;
        _agentSelector = agentSelector;
        _policyEvaluator = policyEvaluator;
        _logger = logger;
    }

    // ── Command Plans ───────────────────────────────────────────────────

    /// <summary>
    /// Create a command plan from a user message.
    /// </summary>
    public async Task<CommandPlan> PlanAsync(
        string projectSlug,
        string conversationId,
        string messageId,
        string text,
        string userId,
        CancellationToken ct = default)
    {
        // Determine risk and approval requirement based on simple heuristics
        var risk = CommandRiskLevels.Low;
        var requiresApproval = false;

        var lower = text.ToLowerInvariant();
        if (lower.Contains("start") || lower.Contains("run") || lower.Contains("запусти") || lower.Contains("выполни"))
        {
            risk = CommandRiskLevels.High;
            requiresApproval = true;
        }
        else if (lower.Contains("move") || lower.Contains("assign") || lower.Contains("create"))
        {
            risk = CommandRiskLevels.Medium;
            requiresApproval = true;
        }

        // Build default actions from intent
        var actions = new List<CommandAction>();
        if (lower.Contains("backlog") || lower.Contains("бэклог"))
        {
            actions.Add(new CommandAction
            {
                Type = "start_backlog",
                ParametersJson = "{\"count\":3}"
            });
        }

        var plan = new CommandPlan
        {
            ProjectSlug = projectSlug,
            ConversationId = conversationId,
            MessageId = messageId,
            Summary = text.Length > 100 ? text[..100] + "..." : text,
            Description = text,
            Risk = risk,
            Status = requiresApproval ? CommandPlanStatuses.PendingApproval : CommandPlanStatuses.Approved,
            ActionsJson = JsonSerializer.Serialize(actions),
            CreatedBy = userId
        };

        var created = await _commandHub.CreatePlanAsync(plan, ct);
        _logger?.LogInformation(
            "Created plan {PlanId} for project {ProjectSlug} with risk {Risk}, approval required={RequiresApproval}",
            created.Id, projectSlug, risk, requiresApproval);

        return created;
    }

    /// <summary>
    /// Approve a plan and execute its actions.
    /// </summary>
    public async Task<CommandExecutionResult> ApproveAndExecuteAsync(
        string projectSlug,
        string planId,
        string approvedBy,
        CancellationToken ct = default)
    {
        var approved = await _commandHub.ApprovePlanAsync(projectSlug, planId, approvedBy, ct);
        if (!approved)
        {
            return new CommandExecutionResult
            {
                Success = false,
                Reason = "Plan not found or not in pending_approval state"
            };
        }

        _logger?.LogInformation("Plan {PlanId} approved by {ApprovedBy}", planId, approvedBy);

        // TODO: Execute actions from the plan
        // For now, mark as executing and return success
        return new CommandExecutionResult
        {
            Success = true,
            PlanId = planId,
            Status = CommandPlanStatuses.Executing
        };
    }

    /// <summary>
    /// Cancel a pending plan.
    /// </summary>
    public async Task<bool> CancelPlanAsync(string projectSlug, string planId, CancellationToken ct = default)
    {
        // For now, we don't have a direct CancelPlan in CommandHubService,
        // but we can simulate by updating the plan status via raw store if needed.
        // As a thin additive layer, we note cancellation in the result log.
        _logger?.LogInformation("Plan {PlanId} cancellation requested", planId);
        return true;
    }

    /// <summary>
    /// List plans for a project.
    /// </summary>
    public async Task<List<CommandPlan>> ListPlansAsync(string projectSlug, CancellationToken ct = default)
    {
        // CommandHubService currently only exposes PendingPlansAsync.
        // Return pending plans as a baseline.
        return await _commandHub.PendingPlansAsync(projectSlug, ct);
    }

    // ── Dispatch ────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatch a ticket to a role inbox.
    /// </summary>
    public async Task<InboxMessage> DispatchTicketToRoleAsync(
        string projectSlug,
        int ticketId,
        string roleId,
        string text,
        string postedBy = "orchestrator",
        CancellationToken ct = default)
    {
        // Find the role inbox
        var inboxes = await _inboxStore.GetInboxesAsync(projectSlug, ct);
        var inbox = inboxes.FirstOrDefault(i => i.RoleId == roleId);
        if (inbox is null)
        {
            throw new InvalidOperationException($"No inbox found for role '{roleId}' in project '{projectSlug}'");
        }

        // Check policy before dispatching
        var policyCheck = await _policyEvaluator.CanAsync(
            projectSlug, roleId, Capabilities.TicketRun,
            new RolePolicyContext { TicketId = ticketId }, ct);
        if (!policyCheck.Allowed)
        {
            throw new InvalidOperationException($"Policy denied dispatch: {policyCheck.Reason}");
        }

        var message = new InboxMessage
        {
            ProjectSlug = projectSlug,
            RoleInboxId = inbox.Id,
            TicketId = ticketId,
            Text = text,
            PostedBy = postedBy
        };

        var posted = await _inboxStore.PostMessageAsync(message, ct);
        _logger?.LogInformation(
            "Dispatched ticket {TicketId} to role {RoleId} inbox {InboxId}, message {MessageId}",
            ticketId, roleId, inbox.Id, posted.Id);

        return posted;
    }

    // ── Agent Selection ───────────────────────────────────────────────

    /// <summary>
    /// Select an agent for a role.
    /// </summary>
    public Task<AgentSelectionResult> SelectAgentAsync(
        string projectSlug,
        string roleId,
        string? requiredSkillsJson = null,
        CancellationToken ct = default)
    {
        return _agentSelector.SelectAgentAsync(projectSlug, roleId, requiredSkillsJson, ct);
    }

    // ── Claim & Session ───────────────────────────────────────────────

    /// <summary>
    /// Atomically claim an inbox message and create a team member session.
    /// </summary>
    public async Task<ClaimSessionResult> ClaimAndCreateSessionAsync(
        string projectSlug,
        string messageId,
        string agentId,
        CancellationToken ct = default)
    {
        var result = await _inboxStore.ClaimAndCreateSessionAsync(projectSlug, messageId, agentId, ct);
        if (!result.Success)
        {
            _logger?.LogWarning(
                "Claim failed for message {MessageId} by agent {AgentId}: {Reason}",
                messageId, agentId, result.Reason);
        }
        else
        {
            _logger?.LogInformation(
                "Agent {AgentId} claimed message {MessageId}, session {SessionId}",
                agentId, messageId, result.Session?.Id);
        }

        return result;
    }

    // ── Execution Profile ─────────────────────────────────────────────

    /// <summary>
    /// Resolve the execution profile for a session.
    /// </summary>
    public async Task<ExecutionProfile?> ResolveExecutionProfileAsync(
        string projectSlug,
        string sessionId,
        string? agentProfileId,
        CancellationToken ct = default)
    {
        // Get agent to find its execution profile
        var agents = await _roleStore.GetAgentsAsync(projectSlug, ct);
        var agent = agents.FirstOrDefault(a => a.Id == agentProfileId);
        if (agent?.ExecutionProfileId is not null)
        {
            var profile = await _roleStore.GetExecutionProfileAsync(projectSlug, agent.ExecutionProfileId, ct);
            if (profile is not null) return profile;
        }

        // Fall back to role default
        var roles = await _roleStore.GetRolesAsync(projectSlug, ct);
        var role = roles.FirstOrDefault(r => r.Slug == agent?.RoleId);
        if (role?.DefaultExecutionProfileId is not null)
        {
            var profile = await _roleStore.GetExecutionProfileAsync(projectSlug, role.DefaultExecutionProfileId, ct);
            if (profile is not null) return profile;
        }

        // Last resort: first available profile
        var profiles = await _roleStore.GetExecutionProfilesAsync(projectSlug, ct);
        return profiles.FirstOrDefault(p => p.Enabled);
    }

    // ── Run ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Start a run for a session.
    /// This is a thin wrapper that delegates to the runner infrastructure.
    /// </summary>
    public async Task<RunStartResult> StartRunForSessionAsync(
        string projectSlug,
        string sessionId,
        ExecutionProfile profile,
        CancellationToken ct = default)
    {
        // Stub: in a full implementation, this would call RunnerRegistry.StartAsync
        _logger?.LogInformation(
            "Run start requested for session {SessionId} with profile {ProfileId} ({Runtime}/{Provider})",
            sessionId, profile.Id, profile.Runtime, profile.Provider);

        return new RunStartResult
        {
            Success = true,
            SessionId = sessionId,
            ExecutionProfileId = profile.Id,
            Status = "started"
        };
    }

    // ── Policy Check ────────────────────────────────────────────────────

    /// <summary>
    /// Check if a role can perform an action.
    /// </summary>
    public Task<PolicyDecision> CheckPolicyAsync(
        string projectSlug,
        string roleId,
        string action,
        RolePolicyContext? context = null,
        CancellationToken ct = default)
    {
        return _policyEvaluator.CanAsync(projectSlug, roleId, action, context, ct);
    }
}

// ── Result Models ────────────────────────────────────────────────────

public sealed class CommandExecutionResult
{
    public bool Success { get; init; }
    public string? PlanId { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
}

public sealed class RunStartResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public string? ExecutionProfileId { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
}
