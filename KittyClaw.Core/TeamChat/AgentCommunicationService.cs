using System.Text.Json;

namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Handles agent-to-agent and agent-to-human communication in team chat.
/// Manages role-based message filtering, routing, and response generation.
/// </summary>
public class AgentCommunicationService : IAgentCommunicationService
{
    private readonly ITeamChatService _chatService;
    private readonly ITeamCommandRouter _router;

    public AgentCommunicationService(ITeamChatService chatService, ITeamCommandRouter router)
    {
        _chatService = chatService;
        _router = router;
    }

    public async Task<AgentMessageResult> SendAgentMessageAsync(
        string projectSlug,
        string agentId,
        string body,
        string messageType,
        string targetType,
        string? targetId,
        int? ticketId = null,
        CancellationToken ct = default)
    {
        var role = AgentRoles.GetRole(agentId);
        if (role is null)
        {
            return new AgentMessageResult(
                Success: false,
                Error: $"Unknown agent role: {agentId}",
                DeliveryStatus: "failed");
        }

        // Validate agent can communicate with target
        if (!CanAgentCommunicateWith(role, targetType, targetId))
        {
            return new AgentMessageResult(
                Success: false,
                Error: $"Agent {agentId} cannot communicate with {targetType}:{targetId}",
                DeliveryStatus: "unsupported");
        }

        // Validate message type is allowed for this role
        if (!role.OutputTypes.Contains(messageType) && messageType != "message")
        {
            messageType = "status"; // Default to status if type not allowed
        }

        var req = new PostTeamChatMessageRequest(
            Body: body,
            AuthorId: agentId,
            AuthorType: "agent",
            MessageType: messageType,
            TargetType: targetType,
            TargetId: targetId,
            TicketId: ticketId);

        var message = await _chatService.PostMessageAsync(projectSlug, req, ct);

        // Route the message
        var route = _router.Route(message);

        return new AgentMessageResult(
            Success: true,
            MessageId: message.Id,
            DeliveryStatus: route.DeliveryStatus,
            RouteResult: route);
    }

    public async Task<AgentMessageResult> SendTeamBroadcastAsync(
        string projectSlug,
        string agentId,
        string body,
        string messageType = "status",
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, body, messageType, "team", null, null, ct);
    }

    public async Task<AgentMessageResult> SendRoleMessageAsync(
        string projectSlug,
        string agentId,
        string targetRole,
        string body,
        string messageType = "status",
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, body, messageType, "role", targetRole, null, ct);
    }

    public async Task<AgentMessageResult> SendTicketMessageAsync(
        string projectSlug,
        string agentId,
        int ticketId,
        string body,
        string messageType = "status",
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, body, messageType, "ticket", ticketId.ToString(), ticketId, ct);
    }

    public async Task<AgentMessageResult> SendQuestionAsync(
        string projectSlug,
        string agentId,
        string question,
        string targetType,
        string? targetId,
        int? ticketId = null,
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, question, "question", targetType, targetId, ticketId, ct);
    }

    public async Task<AgentMessageResult> SendAnswerAsync(
        string projectSlug,
        string agentId,
        string answer,
        string targetType,
        string? targetId,
        int? ticketId = null,
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, answer, "answer", targetType, targetId, ticketId, ct);
    }

    public async Task<AgentMessageResult> SendBlockerAsync(
        string projectSlug,
        string agentId,
        string blocker,
        int? ticketId = null,
        CancellationToken ct = default)
    {
        return await SendAgentMessageAsync(
            projectSlug, agentId, blocker, "blocker", "team", null, ticketId, ct);
    }

    public IReadOnlyList<AgentMessage> FilterMessagesForAgent(
        IReadOnlyList<TeamChatMessage> messages,
        string agentId)
    {
        var role = AgentRoles.GetRole(agentId);
        if (role is null) return [];

        var filtered = new List<AgentMessage>();

        foreach (var msg in messages)
        {
            var relevance = CalculateMessageRelevance(msg, role);
            if (relevance > 0)
            {
                filtered.Add(new AgentMessage
                {
                    OriginalMessage = msg,
                    Relevance = relevance,
                    RequiresAction = RequiresAgentAction(msg, role),
                    SuggestedResponse = GenerateSuggestedResponse(msg, role)
                });
            }
        }

        return filtered.OrderByDescending(m => m.Relevance).ToList();
    }

    public AgentContext GetAgentContext(string agentId, IReadOnlyList<TeamChatMessage> recentMessages)
    {
        var role = AgentRoles.GetRole(agentId);
        if (role is null)
        {
            return new AgentContext
            {
                AgentId = agentId,
                Role = null,
                ActiveTickets = [],
                PendingQuestions = [],
                RecentActivity = []
            };
        }

        var relevantMessages = FilterMessagesForAgent(recentMessages, agentId);

        return new AgentContext
        {
            AgentId = agentId,
            Role = role,
            ActiveTickets = relevantMessages
                .Where(m => m.OriginalMessage.TicketId.HasValue)
                .Select(m => m.OriginalMessage.TicketId!.Value)
                .Distinct()
                .ToList(),
            PendingQuestions = relevantMessages
                .Where(m => m.OriginalMessage.MessageType == "question" && m.RequiresAction)
                .Select(m => m.OriginalMessage)
                .ToList(),
            RecentActivity = relevantMessages.Take(10).ToList()
        };
    }

    private bool CanAgentCommunicateWith(AgentRole role, string targetType, string? targetId)
    {
        return targetType switch
        {
            "team" => true,
            "role" => role.CanCommunicateWith.Contains("team") || role.CanCommunicateWith.Contains(targetId),
            "agent" => role.CanCommunicateWith.Contains(targetId),
            "ticket" => role.CanCommunicateWith.Contains("team") || role.DefaultChannels.Contains("ticket"),
            "run" => role.CanCommunicateWith.Contains("team"),
            _ => false
        };
    }

    private int CalculateMessageRelevance(TeamChatMessage message, AgentRole role)
    {
        // Direct mention = highest relevance
        if (message.TargetType == "agent" && message.TargetId == role.Id)
            return 100;

        // Role mention = high relevance
        if (message.TargetType == "role" && message.TargetId == role.Id)
            return 90;

        // Team broadcast = medium relevance
        if (message.TargetType == "team")
            return 50;

        // Ticket message = medium relevance if agent works on tickets
        if (message.TargetType == "ticket" && role.DefaultChannels.Contains("ticket"))
            return 40;

        // Message from same role = lower relevance
        if (message.AuthorType == "agent" && message.AuthorId == role.Id)
            return 10;

        // Message from another agent = low relevance
        if (message.AuthorType == "agent")
            return 20;

        // Human message = medium relevance
        if (message.AuthorType == "human")
            return 60;

        return 0;
    }

    private bool RequiresAgentAction(TeamChatMessage message, AgentRole role)
    {
        // Questions directed at this agent require action
        if (message.MessageType == "question" &&
            (message.TargetType == "agent" && message.TargetId == role.Id ||
             message.TargetType == "role" && message.TargetId == role.Id))
            return true;

        // Commands directed at this agent require action
        if (message.MessageType == "command" &&
            (message.TargetType == "agent" && message.TargetId == role.Id ||
             message.TargetType == "role" && message.TargetId == role.Id))
            return true;

        // Steer messages require action
        if (message.MessageType == "steer" && message.RunId != null)
            return true;

        return false;
    }

    private string? GenerateSuggestedResponse(TeamChatMessage message, AgentRole role)
    {
        if (message.MessageType != "question") return null;

        // Generate context-aware response suggestions based on role
        return message.TargetId switch
        {
            "planner" => "I'll analyze this and provide an update.",
            "builder" => "Working on it. Will report progress shortly.",
            "reviewer" => "Starting review. Will provide feedback.",
            "tester" => "Running tests. Results incoming.",
            _ => "Acknowledged. Processing."
        };
    }
}

public interface IAgentCommunicationService
{
    Task<AgentMessageResult> SendAgentMessageAsync(
        string projectSlug, string agentId, string body, string messageType,
        string targetType, string? targetId, int? ticketId = null, CancellationToken ct = default);

    Task<AgentMessageResult> SendTeamBroadcastAsync(
        string projectSlug, string agentId, string body,
        string messageType = "status", CancellationToken ct = default);

    Task<AgentMessageResult> SendRoleMessageAsync(
        string projectSlug, string agentId, string targetRole, string body,
        string messageType = "status", CancellationToken ct = default);

    Task<AgentMessageResult> SendTicketMessageAsync(
        string projectSlug, string agentId, int ticketId, string body,
        string messageType = "status", CancellationToken ct = default);

    Task<AgentMessageResult> SendQuestionAsync(
        string projectSlug, string agentId, string question,
        string targetType, string? targetId, int? ticketId = null, CancellationToken ct = default);

    Task<AgentMessageResult> SendAnswerAsync(
        string projectSlug, string agentId, string answer,
        string targetType, string? targetId, int? ticketId = null, CancellationToken ct = default);

    Task<AgentMessageResult> SendBlockerAsync(
        string projectSlug, string agentId, string blocker,
        int? ticketId = null, CancellationToken ct = default);

    IReadOnlyList<AgentMessage> FilterMessagesForAgent(
        IReadOnlyList<TeamChatMessage> messages, string agentId);

    AgentContext GetAgentContext(string agentId, IReadOnlyList<TeamChatMessage> recentMessages);
}

public sealed record AgentMessageResult(
    bool Success,
    string? MessageId = null,
    string? Error = null,
    string DeliveryStatus = "pending",
    TeamCommandRouteResult? RouteResult = null);

public sealed class AgentMessage
{
    public required TeamChatMessage OriginalMessage { get; init; }
    public int Relevance { get; init; }
    public bool RequiresAction { get; init; }
    public string? SuggestedResponse { get; init; }
}

public sealed class AgentContext
{
    public required string AgentId { get; init; }
    public AgentRole? Role { get; init; }
    public List<int> ActiveTickets { get; init; } = [];
    public List<TeamChatMessage> PendingQuestions { get; init; } = [];
    public List<AgentMessage> RecentActivity { get; init; } = [];
}
