namespace KittyClaw.Core.TeamChat;

/// <summary>
/// A single message in the team chat.
/// </summary>
public sealed class TeamChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string ProjectSlug { get; set; }
    public int? TicketId { get; set; }
    public string? RunId { get; set; }

    /// <summary>human | agent | system | runner</summary>
    public string AuthorType { get; set; } = "human";
    public required string AuthorId { get; set; }
    public required string Body { get; set; } = "";

    /// <summary>message | command | question | answer | status | failure | broadcast | steer</summary>
    public string MessageType { get; set; } = "message";

    /// <summary>team | role | agent | ticket | run</summary>
    public string TargetType { get; set; } = "team";
    public string? TargetId { get; set; }

    /// <summary>open | delivered | resolved | failed | unsupported</summary>
    public string DeliveryStatus { get; set; } = "open";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}