using System.Text.RegularExpressions;
using KittyClaw.Core.Automation;

namespace KittyClaw.Core.TeamChat;

public partial class TeamCommandRouter : ITeamCommandRouter
{
    private readonly AgentRunRegistry _runRegistry;

    public TeamCommandRouter(AgentRunRegistry runRegistry)
    {
        _runRegistry = runRegistry;
    }

    public TeamCommandRouteResult Route(TeamChatMessage message)
    {
        var targetType = message.TargetType;
        var targetId = message.TargetId;
        var body = message.Body;

        // Parse @mentions from body if target is team
        if (targetType == "team" && string.IsNullOrEmpty(targetId))
        {
            var mention = ParseMention(body);
            if (mention is not null)
            {
                targetType = "role";
                targetId = mention;
            }
        }

        // Parse #ticket references
        var ticketRef = ParseTicketRef(body);
        if (ticketRef is not null)
        {
            targetType = "ticket";
            targetId = ticketRef;
        }

        // Parse run:<id> references
        var runRef = ParseRunRef(body);
        if (runRef is not null)
        {
            targetType = "run";
            targetId = runRef;
        }

        // Try to find active run for role-based routing
        if (targetType == "role" && targetId is not null)
        {
            var activeRun = FindActiveRunForRole(message.ProjectSlug, targetId);
            if (activeRun is not null)
            {
                return new TeamCommandRouteResult(
                    Routed: true,
                    TargetType: "run",
                    TargetId: activeRun.RunId,
                    DeliveryStatus: "pending",
                    ErrorMessage: null
                );
            }

            return new TeamCommandRouteResult(
                Routed: true,
                TargetType: targetType,
                TargetId: targetId,
                DeliveryStatus: "pending",
                ErrorMessage: $"No active {targetId} run found"
            );
        }

        // Ticket-based routing
        if (targetType == "ticket" && targetId is not null)
        {
            if (int.TryParse(targetId, out var ticketId))
            {
                var activeRun = _runRegistry.ActiveForTicket(message.ProjectSlug, ticketId).FirstOrDefault();
                if (activeRun is not null)
                {
                    return new TeamCommandRouteResult(
                        Routed: true,
                        TargetType: "run",
                        TargetId: activeRun.RunId,
                        DeliveryStatus: "pending",
                        ErrorMessage: null
                    );
                }
            }

            return new TeamCommandRouteResult(
                Routed: true,
                TargetType: targetType,
                TargetId: targetId,
                DeliveryStatus: "pending",
                ErrorMessage: null
            );
        }

        // Run-based routing
        if (targetType == "run" && targetId is not null)
        {
            var run = _runRegistry.Get(targetId);
            if (run is null)
            {
                return new TeamCommandRouteResult(
                    Routed: false,
                    TargetType: targetType,
                    TargetId: targetId,
                    DeliveryStatus: "failed",
                    ErrorMessage: $"Run {targetId} not found"
                );
            }

            return new TeamCommandRouteResult(
                Routed: true,
                TargetType: targetType,
                TargetId: targetId,
                DeliveryStatus: "pending",
                ErrorMessage: null
            );
        }

        // Default: team broadcast
        return new TeamCommandRouteResult(
            Routed: true,
            TargetType: "team",
            TargetId: null,
            DeliveryStatus: "delivered",
            ErrorMessage: null
        );
    }

    private static string? ParseMention(string body)
    {
        var match = MentionRegex().Match(body);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static string? ParseTicketRef(string body)
    {
        var match = TicketRefRegex().Match(body);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ParseRunRef(string body)
    {
        var match = RunRefRegex().Match(body);
        return match.Success ? match.Groups[1].Value : null;
    }

    private AgentRun? FindActiveRunForRole(string projectSlug, string role)
    {
        var activeRuns = _runRegistry.ActiveForProject(projectSlug);
        return activeRuns.FirstOrDefault(r =>
            r.AgentName?.Equals(role, StringComparison.OrdinalIgnoreCase) == true);
    }

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionRegex();

    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex TicketRefRegex();

    [GeneratedRegex(@"run:(\w+)")]
    private static partial Regex RunRefRegex();
}
