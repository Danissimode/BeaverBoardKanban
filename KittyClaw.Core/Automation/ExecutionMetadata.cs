namespace KittyClaw.Core.Automation;

/// <summary>
/// Execution metadata for AI runs.
/// Contains information about the provider, model, and execution context.
/// </summary>
public sealed class ExecutionMetadata
{
    public string? Mode { get; set; }
    public string? Runner { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Profile { get; set; }
    public string? RunId { get; set; }
    public string? SessionId { get; set; }
    public string? WorktreePath { get; set; }
    public string? BranchName { get; set; }
    public string? TicketId { get; set; }
    public string? ProjectId { get; set; }
}
