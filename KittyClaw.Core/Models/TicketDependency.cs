namespace KittyClaw.Core.Models;

/// <summary>
/// Dependency relationship between two tickets.
/// Defines execution order independent of parent/child hierarchy.
/// </summary>
public sealed class TicketDependency
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    
    /// <summary>The ticket that must complete first</summary>
    public required int FromTicketId { get; init; }
    
    /// <summary>The ticket that waits for FromTicket</summary>
    public required int ToTicketId { get; init; }
    
    /// <summary>Dependency type:
    /// finish_to_start - B starts after A Done
    /// start_to_start - B may start after A starts
    /// finish_to_finish - B can finish only after A finishes
    /// blocks - A blocks B
    /// soft_dependency - warning only
    /// </summary>
    public string DependencyType { get; init; } = "finish_to_start";
    
    /// <summary>Scope: same_parent, cross_parent, global</summary>
    public string Scope { get; init; } = "same_parent";
    
    /// <summary>Whether this dependency is required or just advisory</summary>
    public bool Required { get; init; } = true;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Parallel execution group for sibling tickets.
/// </summary>
public sealed class ParallelGroup
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string ProjectSlug { get; init; }
    
    /// <summary>Parent ticket that owns this group</summary>
    public required int ParentTicketId { get; init; }
    
    /// <summary>Human-readable name for the group</summary>
    public required string Name { get; init; }
    
    /// <summary>Join policy: all_done, any_done, manual_join, best_result</summary>
    public string JoinPolicy { get; init; } = "all_done";
    
    /// <summary>Max concurrent tasks in this group</summary>
    public int MaxConcurrency { get; set; } = 2;
    
    /// <summary>Ticket to start after group completes (join task)</summary>
    public int? OnCompleteTicketId { get; init; }
    
    /// <summary>Group status: pending, active, completed, failed</summary>
    public string Status { get; set; } = "pending";
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Ticket kinds.
/// </summary>
public static class TicketKinds
{
    public const string Epic = "epic";
    public const string Task = "task";
    public const string Subtask = "subtask";
    public const string ChecklistItem = "checklist_item";
    public const string Decision = "decision";
    public const string Review = "review";
    public const string Qa = "qa";
    public const string Release = "release";
}

/// <summary>
/// Execution roles for tickets in hierarchy.
/// </summary>
public static class ExecutionRoles
{
    public const string Orchestrator = "orchestrator";
    public const string Planner = "planner";
    public const string Worker = "worker";
    public const string Reviewer = "reviewer";
    public const string Joiner = "joiner";
}

/// <summary>
/// Execution modes for tickets.
/// </summary>
public static class ExecutionModes
{
    public const string Manual = "manual";
    public const string Sequential = "sequential";
    public const string Parallel = "parallel";
    public const string Join = "join";
}
