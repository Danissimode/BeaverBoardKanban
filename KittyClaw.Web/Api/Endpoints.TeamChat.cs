using KittyClaw.Core.TeamChat;

namespace KittyClaw.Web.Api;

public static partial class Endpoints
{
    private static void MapTeamChat(RouteGroupBuilder api)
    {
        var teamChat = api.MapGroup("/projects/{slug}/team-chat").WithTags("TeamChat");

        // List messages with filters
        teamChat.MapGet("/messages", async (
            string slug,
            string? filter,
            int? ticketId,
            string? runId,
            string? authorId,
            int limit,
            int offset,
            ITeamChatService svc) =>
        {
            var query = new TeamChatQuery(filter, ticketId, runId, authorId, limit > 0 ? limit : 50, offset);
            var messages = await svc.ListMessagesAsync(slug, query);
            return Results.Ok(messages);
        });

        // Post a message
        teamChat.MapPost("/messages", async (
            string slug,
            PostTeamChatMessageRequest req,
            ITeamChatService svc,
            ITeamCommandRouter router,
            IRunSteeringBridge bridge) =>
        {
            var message = await svc.PostMessageAsync(slug, req);

            // Route the message
            var route = router.Route(message);

            // Try to deliver to active run if routed to a run
            if (route.Routed && route.TargetType == "run" && route.TargetId is not null)
            {
                if (bridge.IsSteeringSupported(route.TargetId))
                {
                    var steerResult = await bridge.SendToRunAsync(route.TargetId, req.Body);
                    if (steerResult.Success)
                    {
                        message.DeliveryStatus = "delivered";
                    }
                    else
                    {
                        message.DeliveryStatus = "failed";
                        message.MetadataJson = $"{{\"error\":\"{steerResult.Error}\"}}";
                    }
                }
                else
                {
                    message.DeliveryStatus = "unsupported";
                    message.MetadataJson = "{\"reason\":\"steering not supported\"}";
                }
            }
            else if (route.Routed)
            {
                message.DeliveryStatus = route.DeliveryStatus;
                if (route.ErrorMessage is not null)
                    message.MetadataJson = $"{{\"note\":\"{route.ErrorMessage}\"}}";
            }

            await svc.PostMessageAsync(slug, req with { });
            return Results.Ok(message);
        });

        // Get inbox (needs-human + failures + open)
        teamChat.MapGet("/inbox", async (string slug, ITeamChatService svc) =>
        {
            var inbox = await svc.GetInboxAsync(slug);
            return Results.Ok(inbox);
        });

        // Get unread count
        teamChat.MapGet("/unread", async (string slug, ITeamChatService svc) =>
        {
            var count = await svc.GetUnreadCountAsync(slug);
            return Results.Ok(new { count });
        });

        // Resolve a message
        teamChat.MapPost("/messages/{id}/resolve", async (
            string slug,
            string id,
            ResolveMessageRequest req,
            ITeamChatService svc) =>
        {
            await svc.ResolveMessageAsync(slug, id, req.ResolvedBy);
            return Results.NoContent();
        });

        // Get messages for a specific agent
        teamChat.MapGet("/agent/{agentId}", async (
            string slug,
            string agentId,
            ITeamChatService svc) =>
        {
            var messages = await svc.GetMessagesForAgentAsync(slug, agentId);
            return Results.Ok(messages);
        });

        // Ticket-specific messages
        teamChat.MapGet("/tickets/{ticketId:int}", async (
            string slug,
            int ticketId,
            ITeamChatService svc) =>
        {
            var query = new TeamChatQuery(TicketId: ticketId);
            var messages = await svc.ListMessagesAsync(slug, query);
            return Results.Ok(messages);
        });

        teamChat.MapPost("/tickets/{ticketId:int}", async (
            string slug,
            int ticketId,
            PostTeamChatMessageRequest req,
            ITeamChatService svc,
            ITeamCommandRouter router,
            IRunSteeringBridge bridge) =>
        {
            var message = await svc.PostMessageAsync(slug, req with
            {
                TicketId = ticketId,
                TargetType = "ticket",
                TargetId = ticketId.ToString()
            });

            // Try to steer active run for this ticket
            var route = router.Route(message);

            if (route.Routed && route.TargetType == "run" && route.TargetId is not null)
            {
                if (bridge.IsSteeringSupported(route.TargetId))
                {
                    var steerResult = await bridge.SendToRunAsync(route.TargetId, req.Body);
                    message.DeliveryStatus = steerResult.Success ? "delivered" : "failed";
                }
                else
                {
                    message.DeliveryStatus = "unsupported";
                }
            }

            return Results.Ok(message);
        });

        // Agent chat profiles
        var agentChat = api.MapGroup("/projects/{slug}/agent-chat").WithTags("AgentChat");

        agentChat.MapGet("/profiles", async (
            string slug,
            IAgentChatPolicyService svc) =>
        {
            var profiles = await svc.GetAllProfilesAsync(slug);
            return Results.Ok(profiles);
        });

        agentChat.MapGet("/profiles/{agentId}", async (
            string slug,
            string agentId,
            IAgentChatPolicyService svc) =>
        {
            var profile = await svc.GetProfileAsync(slug, agentId);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        agentChat.MapPut("/profiles/{agentId}", async (
            string slug,
            string agentId,
            AgentChatProfile profile,
            IAgentChatPolicyService svc) =>
        {
            profile.ProjectSlug = slug;
            profile.AgentId = agentId;
            await svc.UpsertProfileAsync(profile);
            return Results.Ok(profile);
        });

        // Role chat policies
        agentChat.MapGet("/role-policies", async (
            string slug,
            IAgentChatPolicyService svc) =>
        {
            var policies = await svc.GetAllRolePoliciesAsync(slug);
            return Results.Ok(policies);
        });

        agentChat.MapGet("/role-policies/{roleId}", async (
            string slug,
            string roleId,
            IAgentChatPolicyService svc) =>
        {
            var policy = await svc.GetRolePolicyAsync(slug, roleId);
            return policy is null ? Results.NotFound() : Results.Ok(policy);
        });

        agentChat.MapPut("/role-policies/{roleId}", async (
            string slug,
            string roleId,
            AgentRoleChatPolicy policy,
            IAgentChatPolicyService svc) =>
        {
            policy.ProjectSlug = slug;
            policy.RoleId = roleId;
            await svc.UpsertRolePolicyAsync(policy);
            return Results.Ok(policy);
        });

        // Initialize default policies for a project
        agentChat.MapPost("/init-defaults", async (
            string slug,
            IAgentChatPolicyService svc) =>
        {
            await svc.EnsureDefaultPoliciesAsync(slug);
            return Results.Ok(new { initialized = true });
        });
    }
}

public sealed record ResolveMessageRequest(string ResolvedBy = "human");
