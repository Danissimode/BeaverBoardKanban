using KittyClaw.Core.Automation;

namespace KittyClaw.Core.TeamChat;

public record TeamChatQuery(
    string? Filter = null,     // all | needs-human | running | blocked | failures | mentions | ai-activity
    int? TicketId = null,
    string? RunId = null,
    string? AuthorId = null,
    int Limit = 50,
    int Offset = 0
);

public interface ITeamChatService
{
    Task<TeamChatMessage> PostMessageAsync(string projectSlug, PostTeamChatMessageRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<TeamChatMessage>> ListMessagesAsync(string projectSlug, TeamChatQuery query, CancellationToken ct = default);
    Task<TeamChatMessage?> GetMessageAsync(string projectSlug, string messageId, CancellationToken ct = default);
    Task ResolveMessageAsync(string projectSlug, string messageId, string resolvedBy, CancellationToken ct = default);
    Task<IReadOnlyList<TeamChatMessage>> GetInboxAsync(string projectSlug, CancellationToken ct = default);
    Task<TeamChatMessage> AddSystemEventAsync(string projectSlug, SystemEventRequest req, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string projectSlug, CancellationToken ct = default);
    Task<IReadOnlyList<TeamChatMessage>> GetMessagesForAgentAsync(string projectSlug, string agentId, CancellationToken ct = default);
}

public interface ITeamCommandRouter
{
    TeamCommandRouteResult Route(TeamChatMessage message);
}

public interface IRunSteeringBridge
{
    Task<SteerResult> SendToRunAsync(string runId, string message, CancellationToken ct = default);
    StopResult StopRun(string runId);
    bool IsSteeringSupported(string runId);
}

public sealed record SteerResult(bool Success, string? Error, string? RunId);
public sealed record StopResult(bool Success, string? Error);

public sealed record PostTeamChatMessageRequest(
    string Body,
    string AuthorId,
    string AuthorType = "human",
    string MessageType = "message",
    string TargetType = "team",
    string? TargetId = null,
    int? TicketId = null,
    string? RunId = null,
    string? CorrelationId = null,
    string? MetadataJson = null
);

public sealed record SystemEventRequest(
    string Body,
    string AuthorId = "system",
    string MessageType = "status",
    string DeliveryStatus = "delivered",
    int? TicketId = null,
    string? RunId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? MetadataJson = null
);

public sealed record TeamCommandRouteResult(
    bool Routed,
    string TargetType,
    string? TargetId,
    string DeliveryStatus,
    string? ErrorMessage
);