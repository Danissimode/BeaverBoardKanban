namespace KittyClaw.Core.Automation.TeamRoles;

/// <summary>
/// Result of an atomic claim-and-create-session operation.
/// </summary>
public sealed class ClaimSessionResult
{
    public bool Success { get; init; }
    public string? Reason { get; init; }
    public InboxMessage? Message { get; init; }
    public AssignmentClaim? Claim { get; init; }
    public TeamMemberSession? Session { get; init; }
}
