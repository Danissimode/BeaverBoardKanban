namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Tracks delivery of a message to a specific target (role, agent, run, etc.).
/// </summary>
public sealed class TeamChatDelivery
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string MessageId { get; set; }
    public required string TargetType { get; set; } // role | agent | ticket | run
    public required string TargetId { get; set; }
    public string DeliveryStatus { get; set; } = "pending"; // pending | delivered | failed | unsupported
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
}