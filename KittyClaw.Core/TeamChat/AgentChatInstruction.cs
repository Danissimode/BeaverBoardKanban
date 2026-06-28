namespace KittyClaw.Core.TeamChat;

/// <summary>
/// A normalized instruction delivered to an agent after routing.
/// Represents an action the agent should take based on a team chat message.
/// </summary>
public sealed class AgentChatInstruction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string MessageId { get; set; }
    public required string ProjectSlug { get; set; }

    /// <summary>Target agent id</summary>
    public required string AgentId { get; set; }

    /// <summary>Target role id</summary>
    public required string RoleId { get; set; }

    /// <summary>
    /// answer-human | review-agent-message | continue-run | stop-run |
    /// plan | summarize | ask-question | acknowledge | implement | test
    /// </summary>
    public required string InstructionType { get; set; }

    /// <summary>The instruction body/context</summary>
    public required string Body { get; set; }

    /// <summary>Related ticket id (if any)</summary>
    public int? TicketId { get; set; }

    /// <summary>Related run id (if any)</summary>
    public string? RunId { get; set; }

    /// <summary>pending | delivered | accepted | ignored | completed | failed | unsupported</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Reason for status (if failed/unsupported)</summary>
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
