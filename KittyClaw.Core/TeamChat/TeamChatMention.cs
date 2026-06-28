namespace KittyClaw.Core.TeamChat;

/// <summary>
/// A parsed mention in a team chat message. Stores routing information.
/// </summary>
public sealed class TeamChatMention
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string MessageId { get; set; }
    public required string ProjectSlug { get; set; }

    /// <summary>team | role | agent | ticket | run | human</summary>
    public required string MentionType { get; set; }

    /// <summary>The mention value: role name, agent id, ticket number, run id, or "team"</summary>
    public required string MentionValue { get; set; }

    /// <summary>Whether this mention requires a response from the target</summary>
    public bool RequiresResponse { get; set; }

    /// <summary>Whether the mention has been resolved/answered</summary>
    public bool IsResolved { get; set; }

    /// <summary>Message ID of the response (if any)</summary>
    public string? ResponseMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}
