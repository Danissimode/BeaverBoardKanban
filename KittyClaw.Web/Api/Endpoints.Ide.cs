using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runners;
using KittyClaw.Core.Services;
using KittyClaw.Core.TeamChat;
using KittyClaw.Web.Services;

namespace KittyClaw.Web.Api;

// Aliases for types with long namespace paths
using AgentRunRequest = KittyClaw.Core.Automation.Runners.AgentRunRequest;
using ApiTokenService = KittyClaw.Web.Services.ApiTokenService;
using ITicketExecutionMetadataStore = KittyClaw.Core.Automation.Runners.ITicketExecutionMetadataStore;

/// <summary>
/// IDE/API Bridge v1 endpoints.
/// Provides a clean integration surface for IDEs (VS Code, Cursor, etc.)
/// and external tools to interact with Beaver Board.
///
/// All endpoints require a bearer token (set via Settings → API Token).
/// Scopes: read (board/tickets), write (+ create/modify), execute (+ start runs), admin.
/// </summary>
public static class EndpointsIde
{
    public static void MapIdeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/ide").WithTags("IDE / API v1");

        // Projects
        group.MapGet("/projects", ListProjects)
            .RequireAuthorization("ApiToken")
            .WithDescription("List all projects accessible to the API token.");

        group.MapGet("/projects/{slug}/board", GetBoard)
            .RequireAuthorization("ApiToken")
            .WithDescription("Get the full board state (columns, tickets, labels, members).");

        // Plan import
        group.MapPost("/projects/{slug}/plans/import", ImportPlan)
            .RequireAuthorization("ApiToken")
            .WithDescription("Import a structured plan and optionally create tickets.");

        // Tickets
        group.MapPost("/projects/{slug}/cards/{id:int}/comments", AddComment)
            .RequireAuthorization("ApiToken")
            .WithDescription("Add a comment to a card.");

        // Execution
        group.MapPost("/projects/{slug}/cards/{id:int}/execution/start", StartExecution)
            .RequireAuthorization("ApiToken")
            .WithDescription("Start an agent run for a card.");

        group.MapGet("/projects/{slug}/cards/{id:int}/execution/status", GetExecutionStatus)
            .RequireAuthorization("ApiToken")
            .WithDescription("Get the current execution status for a card.");

        // Evidence
        group.MapPost("/projects/{slug}/cards/{id:int}/evidence", AttachEvidence)
            .RequireAuthorization("ApiToken")
            .WithDescription("Attach evidence to a card (summary, files, checks, risks).");

        // Chat
        group.MapPost("/projects/{slug}/chat/messages", SendChatMessage)
            .RequireAuthorization("ApiToken")
            .WithDescription("Send a chat message to the team chat.");

        // Token management (admin scope)
        group.MapPost("/projects/{slug}/api-token/generate", GenerateToken)
            .RequireAuthorization("ApiToken")
            .WithDescription("Generate a new API token. Requires admin scope.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IResult? RequireScope(ApiTokenResult? token, ApiScopes required)
    {
        if (token is null) return Results.Unauthorized();
        if ((token.Scopes & required) != 0) return null;
        return Results.Forbid();
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListProjects(
        ProjectService ps,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Read);
        if (scopeCheck is not null) return scopeCheck;

        var projects = await ps.ListProjectsAsync();
        return Results.Ok(projects.Select(p => new
        {
            p.Slug,
            p.Name,
            p.CreatedAt,
            p.UpdatedAt,
        }));
    }

    private static async Task<IResult> GetBoard(
        string slug,
        ProjectService ps,
        TicketService ts,
        ColumnService cs,
        LabelService ls,
        MemberService ms,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Read);
        if (scopeCheck is not null) return scopeCheck;

        var project = await ps.GetProjectAsync(slug);
        if (project is null) return Results.NotFound();

        var columns = await cs.ListColumnsAsync(slug);
        var tickets = await ts.ListTicketsAsync(slug);
        var labels = await ls.ListLabelsAsync(slug);
        var members = await ms.ListMembersAsync(slug);

        return Results.Ok(new
        {
            project = new { project.Slug, project.Name },
            columns,
            tickets,
            labels,
            members,
        });
    }

    private static async Task<IResult> ImportPlan(
        string slug,
        ImportPlanRequest req,
        ProjectService ps,
        TicketService ts,
        ColumnService cs,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Write);
        if (scopeCheck is not null) return scopeCheck;

        var project = await ps.GetProjectAsync(slug);
        if (project is null) return Results.NotFound();

        var columns = await cs.ListColumnsAsync(slug);
        var defaultCol = columns.FirstOrDefault(c =>
            string.Equals(c.Name, "Todo", StringComparison.OrdinalIgnoreCase))
            ?? columns.FirstOrDefault();

        if (defaultCol is null)
            return Results.BadRequest(new { error = $"No columns found in project '{slug}'." });

        var created = new List<object>();
        foreach (var task in req.Plan.Tasks)
        {
            var assignee = req.Options.AssignAgents
                ? ResolveAgentMember(task.SuggestedAgent, slug)
                : null;

            var ticket = await ts.CreateTicketAsync(
                slug,
                task.Title,
                task.Description ?? "",
                createdBy: "api",
                status: defaultCol.Name,
                assignedTo: assignee);

            created.Add(new { ticket.Id, ticket.Title, ticket.Status });
        }

        return Results.Ok(new
        {
            projectSlug = slug,
            tasksCreated = created.Count,
            tickets = created,
        });
    }

    private static async Task<IResult> AddComment(
        string slug,
        int id,
        AddCommentRequest req,
        TicketService ts,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Write);
        if (scopeCheck is not null) return scopeCheck;

        var ticket = await ts.GetTicketAsync(slug, id);
        if (ticket is null) return Results.NotFound();

        await ts.AddCommentAsync(slug, id, req.Body, req.Author ?? "api");
        return Results.Ok(new { cardId = id, comment = req.Body });
    }

    private static async Task<IResult> StartExecution(
        string slug,
        int id,
        StartExecutionRequest? req,
        TicketService ts,
        RunnerRegistry runners,
        AgentRunRegistry runRegistry,
        ProjectService ps,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Execute);
        if (scopeCheck is not null) return scopeCheck;

        var ticket = await ts.GetTicketAsync(slug, id);
        if (ticket is null) return Results.NotFound();

        // Resolve runner
        var runnerKind = req?.RunnerKind ?? runners.GetDefaultRunner().Kind;
        var runner = runners.GetRunner(runnerKind) ?? runners.GetDefaultRunner();

        if (!runner.IsAvailable)
            return Results.BadRequest(new { error = $"Runner '{runner.Kind}' is not available." });

        var project = await ps.GetProjectAsync(slug);
        var workspace = project is not null ? ps.ResolveWorkspacePath(project) : "";

        var request = new AgentRunRequest
        {
            ProjectSlug = slug,
            WorkspacePath = workspace,
            AgentName = ticket.AssignedTo ?? "builder",
            SkillFile = $"{(ticket.AssignedTo ?? "builder")}/SKILL.md",
            TicketId = ticket.Id,
            TicketTitle = ticket.Title,
            TicketStatus = ticket.Status,
            TicketDescription = ticket.Description,
            Prompt = req?.Prompt ?? ticket.Description ?? ticket.Title,
            ConcurrencyGroup = $"ticket-{id}",
        };

        var result = await runner.StartAsync(request, CancellationToken.None);
        return Results.Ok(new
        {
            runId = result.RunId ?? "",
            status = result.Status.ToString(),
            runner = runner.Kind,
        });
    }

    private static async Task<IResult> GetExecutionStatus(
        string slug,
        int id,
        TicketService ts,
        AgentRunRegistry reg,
        ITicketExecutionMetadataStore metaStore,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Read);
        if (scopeCheck is not null) return scopeCheck;

        var activeRuns = reg.ActiveForTicket(slug, id).ToList();
        var meta = await metaStore.GetAsync(slug, id);

        return Results.Ok(new
        {
            cardId = id,
            hasActiveRun = activeRuns.Count > 0,
            activeRuns = activeRuns.Select(r => new
            {
                r.RunId,
                r.Status,
                r.StartedAt,
                r.RunnerKind,
            }),
            lastRun = meta is null ? null : new
            {
                meta.RunId,
                meta.Status,
                meta.StartedAt,
                meta.FinishedAt,
                meta.RunnerKind,
                meta.Model,
                meta.Provider,
                meta.WorktreePath,
                meta.SessionId,
            },
        });
    }

    private static async Task<IResult> AttachEvidence(
        string slug,
        int id,
        AttachEvidenceRequest req,
        TicketService ts,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Write);
        if (scopeCheck is not null) return scopeCheck;

        var ticket = await ts.GetTicketAsync(slug, id);
        if (ticket is null) return Results.NotFound();

        // Build evidence string
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(req.Summary)) parts.Add($"Summary: {req.Summary}");
        if (req.ChangedFiles?.Count > 0) parts.Add($"Files: {string.Join(", ", req.ChangedFiles)}");
        if (req.Checks?.Count > 0) parts.Add($"Checks: {string.Join(", ", req.Checks)}");
        if (req.Risks?.Count > 0) parts.Add($"Risks: {string.Join(", ", req.Risks)}");
        if (req.FollowUps?.Count > 0) parts.Add($"Follow-ups: {string.Join(", ", req.FollowUps)}");

        var evidence = string.Join("\n", parts);
        await ts.AddCommentAsync(slug, id, $"**Evidence**\n\n{evidence}", req.Author ?? "api");

        return Results.Ok(new { cardId = id, evidenceParts = parts.Count });
    }

    private static async Task<IResult> SendChatMessage(
        string slug,
        SendChatMessageRequest req,
        ITeamChatService chat,
        ApiTokenResult? token)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Write);
        if (scopeCheck is not null) return scopeCheck;

        var postReq = new PostTeamChatMessageRequest(
            Body: req.Body,
            AuthorId: req.From ?? "api",
            AuthorType: "human",
            MessageType: req.Intent ?? "message",
            TargetType: req.To ?? "team",
            TicketId: req.CardId,
            TargetId: req.To);
        
        var msg = await chat.PostMessageAsync(slug, postReq);
        return Results.Ok(new { messageId = msg.Id });
    }

    private static async Task<IResult> GenerateToken(
        string slug,
        ProjectService ps,
        SettingsService settings,
        ApiTokenResult? token,
        ApiTokenService apiToken)
    {
        var scopeCheck = RequireScope(token, ApiScopes.Admin);
        if (scopeCheck is not null) return scopeCheck;

        var project = await ps.GetProjectAsync(slug);
        if (project is null) return Results.NotFound();

        var (raw, hash) = apiToken.GenerateToken();
        var current = await settings.LoadAsync();
        current.ApiTokenHash = hash;
        current.ApiTokenScopes = "read,write,execute";
        await settings.SaveAsync(current);

        return Results.Ok(new
        {
            token = raw,
            note = "Store this token securely. It will not be shown again.",
            scopes = "read,write,execute",
        });
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    public sealed record ImportPlanRequest(
        string Source,
        PlanBody Plan,
        ImportOptions? Options = null);

    public sealed record PlanBody(
        string Title,
        List<PlanTask> Tasks);

    public sealed record PlanTask(
        string Title,
        string? Description,
        string? SuggestedAgent,
        string? Risk,
        List<string>? AcceptanceCriteria,
        List<string>? Dependencies);

    public sealed record ImportOptions(
        bool CreateCards = true,
        bool AssignAgents = true,
        bool CreateDependencies = true);

    public sealed record AddCommentRequest(
        [Required] string Body,
        string? Author = null);

    public sealed record StartExecutionRequest(
        string? Prompt = null,
        string? RunnerKind = null);

    public sealed record AttachEvidenceRequest(
        string? Summary,
        List<string>? ChangedFiles,
        List<string>? Checks,
        List<string>? Risks,
        List<string>? FollowUps,
        string? Author = null);

    public sealed record SendChatMessageRequest(
        [Required] string Body,
        string? From = null,
        string? Intent = null,
        string? Priority = null,
        int? CardId = null,
        string? To = null);

    // ── Internal ─────────────────────────────────────────────────────────────

    private static string? ResolveAgentMember(string? suggestedAgent, string projectSlug)
    {
        if (string.IsNullOrWhiteSpace(suggestedAgent)) return null;
        // Normalize: "builder" → "builder", "planner" → "planner"
        return suggestedAgent.ToLowerInvariant();
    }
}
