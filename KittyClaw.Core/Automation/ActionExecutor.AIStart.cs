using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.AI;
using KittyClaw.Core.Automation.Runtimes;
using KittyClaw.Core.Services;

namespace KittyClaw.Core.Automation;

/// <summary>
/// AI Provider integration for StartAgentRunAsync in ActionExecutor.
/// This partial class contains the updated StartAgentRunAsync method with AI provider support.
/// </summary>
internal sealed partial class ActionExecutor
{
    // Updated StartAgentRunAsync with AI Provider integration
    private async Task<(bool skip, Task<AgentRun>? runTask, string agentName)> StartAgentRunAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec a,
        CancellationToken ct)
    {
        var agentName = a.Agent;
        if (agentName.Contains("{assignee}"))
        {
            if (firing.TicketId is null)
            {
                _logger.LogWarning("Placeholder {{assignee}} in Agent but no ticketId in firing — skipping");
                return (true, null, agentName);
            }
            var t = await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value);
            var assignee = t?.AssignedTo;
            if (string.IsNullOrEmpty(assignee))
            {
                _logger.LogWarning("Placeholder {{assignee}} in Agent but ticket #{Id} has no assignee — skipping", firing.TicketId);
                return (true, null, agentName);
            }
            agentName = agentName.Replace("{assignee}", assignee);
        }

        var skillFile = $"{agentName}/SKILL.md";
        var group = string.IsNullOrEmpty(a.ConcurrencyGroup)
            ? agentName
            : a.ConcurrencyGroup
                .Replace("{assignee}", agentName)
                .Replace("{ticketId}", firing.TicketId?.ToString() ?? "none");

        if (await _runState.ShouldSkipAsync(rt, a, firing, agentName, group)) return (true, null, agentName);

        var ticket = firing.TicketId is not null
            ? await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value)
            : null;
        var labels = ticket?.Labels.Select(l => l.Name).ToList() ?? new List<string>();

        // Resolve AI configuration
        var aiConfig = await ResolveAIConfigAsync(rt, firing, a, agentName, ct);
        
        // Determine execution path
        if (ShouldUseAIProviderPath(aiConfig))
        {
            // Use AI Provider path
            return await StartAIProviderRunAsync(rt, firing, a, agentName, skillFile, group, aiConfig, ct);
        }
        else
        {
            // Use legacy Claude path
            return await StartLegacyClaudeRunAsync(rt, firing, a, agentName, skillFile, group, ticket, labels, ct);
        }
    }
    
    /// <summary>
    /// Start agent run through AI Provider path
    /// </summary>
    private async Task<(bool skip, Task<AgentRun>? runTask, string agentName)> StartAIProviderRunAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec a,
        string agentName,
        string skillFile,
        string group,
        EffectiveAIConfig aiConfig,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting AI Provider run for agent {AgentName} with provider {ProviderId}", 
            agentName, aiConfig.ProviderId);
        
        // Build the request for AI provider
        var request = new AgentRunRequest
        {
            ProjectSlug = rt.Slug,
            WorkspacePath = rt.Workspace!,
            TicketId = firing.TicketId,
            TicketTitle = firing.TicketTitle ?? "",
            TicketDescription = null, // Will be set below
            Labels = new List<string>(),
            Assignee = agentName,
            CurrentColumn = firing.TicketStatus,
            Prompt = "", // Will be built below
            RuntimeConfig = new AgentRuntimeConfig
            {
                Id = aiConfig.ProviderId,
                Enabled = true,
                Command = aiConfig.ProviderId,
                Model = aiConfig.Model,
                MaxTurns = a.MaxTurns,
                ConcurrencyGroup = group,
                Environment = a.Env
            },
            RuntimeId = aiConfig.ProviderId,
            RoleId = aiConfig.Profile,
            RoleConfig = null,
            ModelProfileId = aiConfig.Model,
            ModelProfileConfig = null
        };
        
        // Get ticket for additional context
        var ticket = firing.TicketId is not null
            ? await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value)
            : null;
        
        if (ticket is not null)
        {
            request.TicketDescription = ticket.Description;
            request.Labels = ticket.Labels.Select(l => l.Name).ToList();
        }
        
        // Build prompt
        var prompt = _promptBuilder.BuildPrompt(request);
        request.Prompt = prompt;
        
        // Execute via AI Provider
        var run = await ExecuteViaAIProviderAsync(rt, firing, a, agentName, skillFile, group, aiConfig, request, ct);
        
        return (false, Task.FromResult(run), agentName);
    }
    
    /// <summary>
    /// Start agent run through legacy Claude path
    /// </summary>
    private async Task<(bool skip, Task<AgentRun>? runTask, string agentName)> StartLegacyClaudeRunAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec a,
        string agentName,
        string skillFile,
        string group,
        Ticket? ticket,
        List<string> labels,
        CancellationToken ct)
    {
        // Original legacy logic
        var config = _configLoader.Load(rt.Slug, rt.Workspace!) ?? _configLoader.CreateDefault(rt.Slug, rt.Workspace!);
        var orchestration = new AgentOrchestrationResolver(config);
        var runtimeId = ticket is not null ? orchestration.ResolveRuntime(ticket) : config.DefaultRuntime;
        var runtime = _runtimes.FirstOrDefault(r => string.Equals(r.Id, runtimeId, StringComparison.OrdinalIgnoreCase));
        if (runtime is null)
            throw new InvalidOperationException($"Runtime '{runtimeId}' is not registered.");

        var roleId = ticket is not null ? orchestration.ResolveRole(ticket, runtimeId) : config.DefaultRole;
        var roleConfig = orchestration.GetRoleConfig(roleId);
        var modelProfileId = ticket is not null ? orchestration.ResolveModelProfile(ticket, roleId, runtimeId) : config.DefaultModelProfile;
        var modelProfileConfig = orchestration.GetModelProfileConfig(modelProfileId);

        var isHighRisk = orchestration.IsHighRisk(labels);
        if (isHighRisk && !roleConfig.AllowHighRisk)
        {
            _logger.LogWarning("High-risk ticket #{Id} with role '{Role}' does not allow high-risk execution — blocking", firing.TicketId, roleId);
            // Rollback: if the ticket was already moved to InProgress, move it to Blocked with evidence.
            if (firing.TicketId is not null)
            {
                try
                {
                    var t = await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value);
                    if (t is not null && string.Equals(t.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                    {
                        await _tickets.MoveTicketAsync(rt.Slug, firing.TicketId.Value, "Blocked", "automation");
                        await _tickets.AddCommentAsync(rt.Slug, firing.TicketId.Value,
                            $"Blocked: role '{roleId}' cannot execute high-risk labels [{string.Join(", ", labels)}]. " +
                            "Human review required. Select a role with allowHighRisk=true (e.g., security-reviewer, reviewer).", "automation");
                        _logger.LogInformation("Moved blocked high-risk ticket #{Id} from InProgress to Blocked", firing.TicketId.Value);
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to rollback blocked ticket #{Id} to Blocked", firing.TicketId); }
            }
            return (true, null, agentName);
        }
        
        var prompt = _promptBuilder.BuildPrompt(new AgentRunRequest(
            ProjectSlug: rt.Slug,
            WorkspacePath: rt.Workspace!,
            TicketId: firing.TicketId,
            TicketTitle: firing.TicketTitle ?? "",
            TicketDescription: ticket?.Description,
            Labels: labels,
            Assignee: agentName,
            CurrentColumn: firing.TicketStatus,
            Prompt: "", // built below after config resolution
            RuntimeConfig: config.Runtimes.TryGetValue(runtime.Id, out var rc) ? rc : new AgentRuntimeConfig
            {
                Id = runtime.Id,
                Enabled = true,
                Command = runtime.Id,
            },
            RuntimeId: runtimeId,
            RoleId: roleId,
            RoleConfig: roleConfig,
            ModelProfileId: modelProfileId,
            ModelProfileConfig: modelProfileConfig
        ));

        var runtimeConfig = config.Runtimes.TryGetValue(runtime.Id, out var rconf) ? rconf : new AgentRuntimeConfig
        {
            Id = runtime.Id,
            Enabled = true,
            Command = runtime.Id,
            MaxTurns = a.MaxTurns,
            ConcurrencyGroup = group,
        };

        // Overlay action-specific settings and model profile
        runtimeConfig = new AgentRuntimeConfig
        {
            Id = runtimeConfig.Id,
            Enabled = runtimeConfig.Enabled,
            Command = runtimeConfig.Command,
            Args = runtimeConfig.Args,
            PromptMode = runtimeConfig.PromptMode,
            TimeoutSeconds = runtimeConfig.TimeoutSeconds,
            Experimental = runtimeConfig.Experimental,
            WorkingDirectoryOverride = runtimeConfig.WorkingDirectoryOverride,
            Model = a.Model ?? modelProfileConfig.Model ?? runtimeConfig.Model,
            Agent = runtimeConfig.Agent,
            OutputFormat = runtimeConfig.OutputFormat,
            DangerouslySkipPermissions = runtimeConfig.DangerouslySkipPermissions,
            Environment = a.Env.Count > 0 ? a.Env : runtimeConfig.Environment,
            MaxTurns = a.MaxTurns > 0 ? a.MaxTurns : runtimeConfig.MaxTurns,
            ConcurrencyGroup = group,
        };

        var request = new AgentRunRequest(
            ProjectSlug: rt.Slug,
            WorkspacePath: rt.Workspace!,
            TicketId: firing.TicketId,
            TicketTitle: firing.TicketTitle ?? "",
            TicketDescription: ticket?.Description,
            Labels: labels,
            Assignee: agentName,
            CurrentColumn: firing.TicketStatus,
            Prompt: prompt,
            RuntimeConfig: runtimeConfig,
            RuntimeId: runtimeId,
            RoleId: roleId,
            RoleConfig: roleConfig,
            ModelProfileId: modelProfileId,
            ModelProfileConfig: modelProfileConfig
        );

        var runId = Guid.NewGuid().ToString("N");
        var run = new AgentRun
        {
            RunId = runId,
            ProjectSlug = rt.Slug,
            TicketId = firing.TicketId,
            AgentName = agentName,
            SkillFile = skillFile,
            ConcurrencyGroup = group,
            StartedAt = DateTime.UtcNow,
            RuntimeId = runtimeId,
            RoleId = roleId,
            ModelProfileId = modelProfileId,
            ExecutionMetadata = new ExecutionMetadata
            {
                Mode = ExecutionMode.LegacyClaude.ToString(),
                Runner = "claude",
                Provider = "claude",
                Model = runtimeConfig.Model ?? "claude-3-5-sonnet",
                Profile = "default",
                RunId = runId,
                WorktreePath = rt.Workspace,
                ProjectId = rt.Slug
            }
        };
        _runs.Register(run);
        _sessions.SetLastDispatched(rt.Workspace!, agentName, DateTime.UtcNow);
        if (firing.TicketId is not null)
        {
            try { await _tickets.AddActivityAsync(rt.Slug, firing.TicketId.Value, _loc.Get("ActAgentStarted", agentName), "automation"); }
            catch { /* non-blocking */ }

            // Move Ready → InProgress before execution.
            try
            {
                var readyTicket = await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value);
                if (readyTicket is not null && string.Equals(readyTicket.Status, "Ready", StringComparison.OrdinalIgnoreCase))
                {
                    await _tickets.MoveTicketAsync(rt.Slug, firing.TicketId.Value, "InProgress", "automation");
                    _logger.LogInformation("Moved ticket #{Id} from Ready to InProgress before run", firing.TicketId.Value);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Pre-run status transition failed for ticket #{Id}", firing.TicketId); }
        }

        // Fire-and-forget wrapper: starts the runtime, updates the local AgentRun, and returns it.
        var runTask = Task.Run(async () =>
        {
            try
            {
                var req = request with { RunId = runId };
                var result = await runtime.RunAsync(req, ct);

                // Resolve the canonical run (ClaudeRunner may have replaced ours in the registry).
                var canonicalRun = _runs.Get(runId) ?? run;
                if (canonicalRun.Status == AgentRunStatus.Running)
                {
                    canonicalRun.Status = result.Status;
                    canonicalRun.ExitCode = result.ExitCode;
                    canonicalRun.EndedAt = result.FinishedAt.UtcDateTime;
                    canonicalRun.RuntimeId = result.RuntimeId;
                    canonicalRun.CommandDisplay = result.CommandDisplay;
                    if (!string.IsNullOrEmpty(result.Stdout))
                        canonicalRun.Push(new StreamEvent(result.StartedAt.UtcDateTime, "stdout", result.Stdout));
                    if (!string.IsNullOrEmpty(result.Stderr))
                        canonicalRun.Push(new StreamEvent(result.StartedAt.UtcDateTime, "stderr", result.Stderr));
                    _runs.Complete(runId, result.Status, result.ExitCode);
                }
                return canonicalRun;
            }
            catch (OperationCanceledException)
            {
                var canonicalRun = _runs.Get(runId) ?? run;
                if (canonicalRun.Status == AgentRunStatus.Running)
                {
                    canonicalRun.Status = AgentRunStatus.Stopped;
                    canonicalRun.EndedAt = DateTime.UtcNow;
                    _runs.Complete(runId, AgentRunStatus.Stopped, null);
                }
                return canonicalRun;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "runAgent {Agent} crashed for ticket #{Id}", agentName, firing.TicketId);
                var canonicalRun = _runs.Get(runId) ?? run;
                if (canonicalRun.Status == AgentRunStatus.Running)
                {
                    canonicalRun.Status = AgentRunStatus.Failed;
                    canonicalRun.EndedAt = DateTime.UtcNow;
                    _runs.Complete(runId, AgentRunStatus.Failed, null);
                }
                return canonicalRun;
            }
        });

        return (false, runTask, agentName);
    }
}
