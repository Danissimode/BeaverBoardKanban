namespace KittyClaw.Core.TeamChat;

/// <summary>
/// A chat thread — a conversation channel scoped to a project and optionally to a ticket, run, or role.
/// </summary>
public sealed class TeamChatThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string ProjectSlug { get; set; }
    public string ThreadType { get; set; } = "team"; // team | ticket | run | role | incident
    public int? TicketId { get; set; }
    public string? RunId { get; set; }
    public string? RoleId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "open"; // open | archived
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}