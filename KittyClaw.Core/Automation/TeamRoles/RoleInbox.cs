namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Role Inbox — the "shared mailbox" for a team role.
/// 
/// Metaphor:
/// - @programmer = programmer-inbox (shared mailbox for all programmer agents)
/// - When you write @programmer, you post to the Programmer Inbox
/// - An available agent claims the task from the inbox
/// - The agent then works on it in a TeamMemberSession
/// 
/// This preserves chat simplicity (@programmer) while enabling parallel execution.
/// </summary>
public sealed class RoleInbox
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    
    /// <summary>Which role this inbox belongs to</summary>
    public required string RoleId { get; init; }
    
    /// <summary>Inbox display name (e.g., "Programmer Inbox")</summary>
    public required string Name { get; init; }
    
    /// <summary>Chat address (e.g., "@programmer")</summary>
    public required string ChatAddress { get; init; }
    
    /// <summary>Base skills required for tasks in this inbox</summary>
    public string? BaseSkillsJson { get; init; }
    
    /// <summary>Whether this inbox is active</summary>
    public bool Enabled { get; init; } = true;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A claim on a task from a role inbox.
/// Represents the moment an agent "picks up the mail" from the inbox.
/// </summary>
public sealed class AssignmentClaim
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    
    /// <summary>Which inbox the task was posted to</summary>
    public required string RoleInboxId { get; init; }
    
    /// <summary>Which agent claimed the task</summary>
    public required string AgentProfileId { get; init; }

    /// <summary>Which inbox message was claimed (links to the exact instruction)</summary>
    public string? InboxMessageId { get; init; }
    
    /// <summary>Which ticket was claimed</summary>
    public required int TicketId { get; init; }
    
    /// <summary>Claim status: pending, claimed, completed, failed, released</summary>
    public string Status { get; set; } = "pending";
    
    /// <summary>Why this agent was selected (skill match, availability, etc.)</summary>
    public string? SelectionReason { get; init; }
    
    /// <summary>Associated session ID</summary>
    public string? SessionId { get; set; }
    
    public DateTime ClaimedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// A message/task posted to a role inbox.
/// </summary>
public sealed class InboxMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    public required string RoleInboxId { get; init; }
    
    /// <summary>Which ticket this message is about</summary>
    public required int TicketId { get; init; }
    
    /// <summary>Message text or instruction</summary>
    public required string Text { get; init; }
    
    /// <summary>Posted by (user ID or agent ID)</summary>
    public required string PostedBy { get; init; }
    
    /// <summary>Required skills for this task</summary>
    public string? RequiredSkillsJson { get; init; }
    
    /// <summary>Message status: pending, claimed, expired</summary>
    public string Status { get; set; } = "pending";
    
    /// <summary>Who claimed this message</summary>
    public string? ClaimedByAgentId { get; set; }
    
    /// <summary>When claimed</summary>
    public DateTime? ClaimedAt { get; set; }
    
    /// <summary>Expiration time (for pending messages)</summary>
    public DateTime? ExpiresAt { get; init; }
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Inbox message statuses.
/// </summary>
public static class InboxMessageStatuses
{
    public const string Pending = "pending";
    public const string Claimed = "claimed";
    public const string Expired = "expired";
}

/// <summary>
/// Assignment claim statuses.
/// </summary>
public static class ClaimStatuses
{
    public const string Pending = "pending";
    public const string Claimed = "claimed";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Released = "released";
}
