namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Represents an agent's actual participation in a specific task/run.
/// 
/// Two-layer identity:
/// - Role = permanent chat address and permissions (e.g., @programmer)
/// - AgentProfile = concrete team member (e.g., programmer-1)
/// - TeamMemberSession = actual participation in a task (born when task starts, leaves when done)
/// 
/// Metaphor:
/// - Role = job position and chat address
/// - Agent = employee who can fill the position
/// - Session = shift/task where the employee actually works
/// - ExecutionProfile = employee's tools (provider/model/runtime)
/// - OpenCodeSession = employee's workspace
/// </summary>
public sealed class TeamMemberSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    
    /// <summary>Which role this session belongs to</summary>
    public required string RoleId { get; init; }
    
    /// <summary>Which specific agent is participating</summary>
    public required string AgentProfileId { get; init; }
    
    /// <summary>Which ticket/task is being worked on</summary>
    public required int TicketId { get; init; }
    
    /// <summary>Associated run ID (if a run was started)</summary>
    public string? RunId { get; init; }
    
    /// <summary>OpenCode session ID (if OpenCode is used)</summary>
    public string? OpencodeSessionId { get; init; }
    
    /// <summary>Execution profile used for this session</summary>
    public string? ExecutionProfileId { get; init; }
    
    /// <summary>
    /// Session state:
    /// joined - agent joined the task
    /// assigned - assigned but not started
    /// running - actively working
    /// waiting - waiting for something (dependency, approval, review)
    /// blocked - blocked by issue
    /// handoff - transferring to another agent
    /// completed - work finished successfully
    /// failed - work failed
    /// left - agent left the session
    /// archived - session history preserved
    /// </summary>
    public string State { get; set; } = "joined";
    
    /// <summary>Brief status message</summary>
    public string? StatusMessage { get; init; }
    
    // ── Timestamps ─────────────────────────────────────────────────────
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }
    public DateTime? StartedRunAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    
    // ── Output ─────────────────────────────────────────────────────────
    /// <summary>Summary of work done</summary>
    public string? Summary { get; set; }
    
    /// <summary>Evidence attached (JSON)</summary>
    public string? EvidenceJson { get; set; }
    
    /// <summary>Exit status: success, failed, timeout, cancelled</summary>
    public string? ExitStatus { get; set; }
}

/// <summary>
/// Session states.
/// </summary>
public static class SessionStates
{
    public const string Joined = "joined";
    public const string Assigned = "assigned";
    public const string Running = "running";
    public const string Waiting = "waiting";
    public const string Blocked = "blocked";
    public const string Handoff = "handoff";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Left = "left";
    public const string Archived = "archived";
}

/// <summary>
/// Summary of team presence for display.
/// </summary>
public sealed class TeamPresenceSummary
{
    public string RoleId { get; init; } = "";
    public string RoleName { get; init; } = "";
    public List<AgentPresence> Agents { get; init; } = new();
    public int ActiveCount { get; set; }
    public int IdleCount { get; set; }
}

/// <summary>
/// Presence status for a single agent.
/// </summary>
public sealed class AgentPresence
{
    public string AgentId { get; init; } = "";
    public string AgentName { get; init; } = "";
    public string Status { get; init; } = "idle"; // idle, assigned, running, blocked, reviewing
    public int? CurrentTicketId { get; init; }
    public string? CurrentTicketTitle { get; init; }
    public string? CurrentSessionId { get; init; }
    public DateTime? LastActivityAt { get; init; }
}
