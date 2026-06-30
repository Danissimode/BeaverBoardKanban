using System.Text;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runners;
using KittyClaw.Core.Models;
using KittyClaw.Core.Services;

namespace KittyClaw.Web.Api;

public static partial class Endpoints
{
    private static void MapChat(RouteGroupBuilder api)
    {
        // Get available runners for chat (to populate runner selector)
        api.MapGet("/chat/runners", (RunnerRegistry registry) =>
        {
            var runners = registry.GetAvailableRunners()
                .Select(r => new ChatRunnerDto(r.Kind, r.DisplayName, r.IsAvailable))
                .ToList();
            return Results.Ok(runners);
        }).WithTags("Chat");

        // Owner chat (ad-hoc AI session with the default runner)
        api.MapGet("/projects/{slug}/chat/targets", async (string slug, ProjectService ps, MemberService ms, ChatService cs, RunnerRegistry registry) =>
        {
            var project = await ps.GetProjectAsync(slug);
            if (project is null) return Results.NotFound();

            var defaultRunner = registry.GetDefaultRunner();
            var targets = new List<ChatTargetDto>
            {
                new("owner-chat", defaultRunner.DisplayName, defaultRunner.Kind),
            };
            var members = await ms.ListMembersAsync(slug);
            foreach (var m in members)
                targets.Add(new ChatTargetDto(m.Slug, m.Name, "member"));

            var lastTarget = await cs.LastTargetAsync(slug);
            return Results.Ok(new ChatTargetsResponse(lastTarget, targets));
        }).WithTags("Chat");

        api.MapGet("/projects/{slug}/chat/messages", async (string slug, string target, ChatService cs) =>
        {
            var rows = await cs.ListAsync(slug, target);
            var dtos = rows.Select(r => new ChatMessageDto(r.Role, r.Text, r.ToolName, r.Detail, r.CreatedAt)).ToList();
            return Results.Ok(dtos);
        }).WithTags("Chat");

        // Returns the runId of an in-flight chat run for (slug, target), or null.
        // Used by the drawer to reattach the SSE stream when reopened mid-run, so that
        // assistant turns emitted while the drawer was closed (and any subsequent ones)
        // surface in the UI.
        api.MapGet("/projects/{slug}/chat/active", (string slug, string target, AgentRunRegistry reg) =>
        {
            var group = $"chat:{slug}:{target}";
            var active = reg.ActiveForProject(slug)
                .FirstOrDefault(r => r.ConcurrencyGroup == group);
            return Results.Ok(new { runId = active?.RunId });
        }).WithTags("Chat");

        api.MapDelete("/projects/{slug}/chat/session", async (string slug, string target, ProjectService ps, ChatService cs, SessionRegistry sessions) =>
        {
            var project = await ps.GetProjectAsync(slug);
            if (project is null) return Results.NotFound();
            var workspacePath = ps.ResolveWorkspacePath(project);
            await cs.ClearAsync(slug, target);
            sessions.Clear(workspacePath, $"chat:{target}", null);
            return Results.NoContent();
        }).WithTags("Chat");

        // v1 — DEPRECATED. Delegates to /chat/start-v2 internals with "claude" as default runner.
        // The old ClaudeRunner-only path is no longer maintained; use /chat/start-v2 directly.
        api.MapPost("/projects/{slug}/chat/start", async (
            string slug,
            ChatStartRequest req,
            ProjectService ps,
            MemberService ms,
            ChatService cs,
            TicketService ts,
            RunnerRegistry registry,
            AgentRunRegistry runReg,
            HttpContext http) =>
        {
            http.Response.Headers.Append("X-Chat-Version", "2");
            // Route to v2 internals: force "claude" runner if none specified
            var v2Req = req with { Runner = req.Runner ?? "claude" };
            return await StartChatV2Async(slug, v2Req, ps, ms, cs, ts, registry, runReg, http);
        }).WithTags("Chat");

        // v2 — PRIMARY. Uses RunnerRegistry with explicit runner override.
        // Supports: claude, opencode, or any registered runner via the "runner" field.
        api.MapPost("/projects/{slug}/chat/start-v2", async (
            string slug,
            ChatStartRequest req,
            ProjectService ps,
            MemberService ms,
            ChatService cs,
            TicketService ts,
            RunnerRegistry registry,
            AgentRunRegistry runReg,
            HttpContext http) =>
        {
            return await StartChatV2Async(slug, req, ps, ms, cs, ts, registry, runReg, http);
        }).WithTags("Chat");

        // Shared implementation for both v1 and v2
        static async Task<IResult> StartChatV2Async(
            string slug,
            ChatStartRequest req,
            ProjectService ps,
            MemberService ms,
            ChatService cs,
            TicketService ts,
            RunnerRegistry registry,
            AgentRunRegistry runReg,
            HttpContext http)
        {
            var project = await ps.GetProjectAsync(slug);
            if (project is null) return Results.NotFound();

            var target = string.IsNullOrWhiteSpace(req.Target) ? "owner-chat" : req.Target;
            var runId = Guid.NewGuid().ToString("N");
            var workspacePath = ps.ResolveWorkspacePath(project);

            var (baseAgent, parsedTicketId) = ParseChatTarget(target);
            var effectiveTicketId = req.TicketId ?? parsedTicketId;

            // Drain pending steer messages from the last completed run for this chat target
            var pendingSteerMessages = runReg.LastCompletedForChatTarget(slug, target)?.DrainPendingSteerMessages();

            if (req.ForceNew)
            {
                await cs.ClearAsync(slug, target);
            }

            // Validate and persist images
            var (imagePaths, imageError) = await PersistChatImagesAsync(req.Images, workspacePath, runId);
            if (imageError is not null)
                return Results.BadRequest(new { error = "image_rejected", reason = imageError });

            await cs.AppendAsync(slug, target, "user", req.Message);

            // Build ticket context
            string? ticketContext = null;
            if (effectiveTicketId is int tid)
            {
                var ticket = await ts.GetTicketAsync(slug, tid);
                if (ticket is not null)
                {
                    ticketContext = BuildTicketContextString(slug, tid, ticket);
                }
            }

            // Build the prompt for the runner
            var prompt = req.Message;
            if (ticketContext is not null)
            {
                prompt = ticketContext + "\n\n## User Message\n\n" + req.Message;
            }

            // Resolve runner: explicit choice from UI → registry kind → mode fallback → default
            var runner = registry.ResolveRunner(req.Runner, ExecutionMode.DirectOpenCode);
            if (runner is null || !runner.IsAvailable)
            {
                runner = registry.GetDefaultRunner();
            }

            var skillFile = target == "owner-chat" ? "chat" : $"{baseAgent}/SKILL.md";

            // Build AgentRunRequest
            var request = new AgentRunRequest
            {
                ProjectSlug = slug,
                WorkspacePath = workspacePath,
                AgentName = baseAgent,
                SkillFile = skillFile,
                TicketId = effectiveTicketId,
                TicketTitle = ticketContext is not null ? $"Ticket #{effectiveTicketId}" : null,
                TicketStatus = null,
                TicketDescription = ticketContext,
                Prompt = prompt,
                ConcurrencyGroup = $"chat:{slug}:{target}",
                RunId = runId,
                MaxTurns = 20,
                ExecutionMode = ExecutionMode.DirectOpenCode,
                OnEventHook = ev => PersistChatEvent(cs, slug, target, ev),
                ImagePaths = imagePaths,
                ChatTarget = target,
                PendingSteerMessages = pendingSteerMessages, // Pass drained steer messages
                Environment = new Dictionary<string, string>
                {
                    ["BEAVER_CHAT_TARGET"] = target,
                    ["BEAVER_BASE_URL"] = $"{http.Request.Scheme}://{http.Request.Host}"
                }
            };

            _ = runner.StartAsync(request, CancellationToken.None);
            return Results.Ok(new { runId, runner = runner.Kind });
        }
    } // end MapChat

    // Helper methods — class-level (MapChat is a void instance method, static helpers must be outside it)
    
    /// <summary>
    /// Builds a ticket context string for chat
    /// </summary>
    private static string BuildTicketContextString(string slug, int ticketId, Ticket ticket)
    {
        var tb = new StringBuilder();
        tb.AppendLine($"## Current Ticket: #{ticket.Id} — {ticket.Title}");
        tb.AppendLine();
        tb.AppendLine($"- Status: `{ticket.Status}`");
        tb.AppendLine($"- Priority: `{ticket.Priority}`");
        if (!string.IsNullOrWhiteSpace(ticket.AssignedTo))
            tb.AppendLine($"- Assigned to: `{ticket.AssignedTo}`");
        tb.AppendLine();
        tb.AppendLine("### Description");
        tb.AppendLine(string.IsNullOrWhiteSpace(ticket.Description) ? "_(empty)_" : ticket.Description);
        if (ticket.Comments.Count > 0)
        {
            tb.AppendLine();
            tb.AppendLine("### Comments");
            foreach (var c in ticket.Comments.OrderBy(c => c.CreatedAt))
                tb.AppendLine($"- **{c.Author}** ({c.CreatedAt:g}): {c.Content}");
        }
        return tb.ToString();
    }

    /// <summary>
    /// Parses a chat target slug. A bare slug like "programmer" or "owner-chat" is returned
    /// as (slug, null). A ticket-scoped target like "programmer#ticket-42" returns
    /// ("programmer", 42). Unknown suffix shapes are passed through as bare.
    /// </summary>
    private static (string BaseAgent, int? TicketId) ParseChatTarget(string target)
    {
        var hashIdx = target.IndexOf('#');
        if (hashIdx < 0) return (target, null);
        var head = target[..hashIdx];
        var tail = target[(hashIdx + 1)..];
        const string prefix = "ticket-";
        if (tail.StartsWith(prefix) && int.TryParse(tail.AsSpan(prefix.Length), out var id))
            return (head, id);
        return (target, null);
    }

    private const long ChatImageMaxBytes = 5 * 1024 * 1024;
    private const int ChatImageMaxCount = 5;
    private static readonly HashSet<string> ChatImageAllowedMime = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
    };

    /// <summary>
    /// Validates and persists pasted images to <c>&lt;workspace&gt;/.agents/channel/tmp/</c>.
    /// Returns the list of absolute paths to forward to <see cref="ClaudeRunContext.ImagePaths"/>,
    /// or a non-null reason string for a 400 "image_rejected" response.
    /// </summary>
    private static async Task<(IReadOnlyList<string>? Paths, string? RejectReason)> PersistChatImagesAsync(
        IReadOnlyList<ChatImageDto>? images, string workspacePath, string runId)
    {
        if (images is null || images.Count == 0) return (null, null);
        if (images.Count > ChatImageMaxCount)
            return (null, $"too many images (max {ChatImageMaxCount})");

        var tmpDir = Path.Combine(workspacePath, ".agents", "channel", "tmp");
        Directory.CreateDirectory(tmpDir);

        var paths = new List<string>(images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var img = images[i];
            if (string.IsNullOrWhiteSpace(img.Mime) || !ChatImageAllowedMime.Contains(img.Mime))
                return (null, $"unsupported MIME type: {img.Mime}");
            if (img.SizeBytes > ChatImageMaxBytes)
                return (null, $"image too large (max {ChatImageMaxBytes} bytes)");
            if (string.IsNullOrWhiteSpace(img.DataUrl))
                return (null, "empty data URL");

            // data:image/png;base64,XXXX  →  XXXX
            var commaIdx = img.DataUrl.IndexOf(',');
            var base64 = commaIdx > 0 ? img.DataUrl[(commaIdx + 1)..] : img.DataUrl;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { return (null, "malformed base64 payload"); }
            if (bytes.LongLength > ChatImageMaxBytes)
                return (null, $"image too large (max {ChatImageMaxBytes} bytes)");

            var ext = img.Mime switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/gif" => "gif",
                "image/webp" => "webp",
                _ => "bin",
            };
            var path = Path.Combine(tmpDir, $"chat-{runId}-{i}.{ext}");
            await File.WriteAllBytesAsync(path, bytes);
            paths.Add(path);
        }
        return (paths, null);
    }

    private static void PersistChatEvent(ChatService cs, string slug, string target, StreamEvent ev)
    {
        // "inject" events are persisted directly by the steer endpoint — skip here to avoid double-write.
        // Only persist what the drawer actually renders to the user.
        if (ev.Kind == "assistant")
        {
            const string prefix = "[assistant] ";
            var text = ev.Text.StartsWith(prefix) ? ev.Text[prefix.Length..] : ev.Text;
            text = text.Trim();
            if (string.IsNullOrEmpty(text) || text.StartsWith("tool:")) return;
            _ = cs.AppendAsync(slug, target, "assistant", text);
        }
        else if (ev.Kind == "tool_use")
        {
            _ = cs.AppendAsync(slug, target, "tool_use", ev.Text, toolName: ev.Text, detail: ev.Detail);
        }
        else if (ev.Kind == "ask_user_question")
        {
            _ = cs.AppendAsync(slug, target, "ask_user_question", ev.Text ?? "", toolName: ev.Text, detail: ev.Detail);
        }
        else if (ev.Kind == "reset")
        {
            _ = cs.AppendAsync(slug, target, "reset", ev.Text);
        }
    }
}
