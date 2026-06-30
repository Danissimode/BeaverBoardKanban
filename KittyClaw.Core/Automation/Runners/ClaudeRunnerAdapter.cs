using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Automation.Runners;

/// <summary>
/// Adapter that wraps the existing ClaudeRunner to implement IAgentRunner interface.
/// This preserves backward compatibility while allowing the new runner registry to work.
/// </summary>
public sealed class ClaudeRunnerAdapter : IAgentRunner
{
    private readonly ClaudeRunner _claudeRunner;
    private readonly ILogger<ClaudeRunnerAdapter>? _logger;
    
    public string Kind => "claude";
    public string DisplayName => "Claude (Legacy)";
    public bool IsAvailable => true; // Always available for backward compatibility
    
    public ClaudeRunnerAdapter(ClaudeRunner claudeRunner, ILogger<ClaudeRunnerAdapter>? logger = null)
    {
        _claudeRunner = claudeRunner;
        _logger = logger;
    }
    
    public async Task<AgentRunResult> StartAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting Claude runner for agent {AgentName}", request.AgentName);
        
        // Convert AgentRunRequest to ClaudeRunContext
        var context = new ClaudeRunContext
        {
            ProjectSlug = request.ProjectSlug,
            WorkspacePath = request.WorkspacePath,
            AgentName = request.AgentName,
            SkillFile = request.SkillFile,
            TicketId = request.TicketId,
            TicketTitle = request.TicketTitle,
            TicketStatus = request.TicketStatus,
            MaxTurns = request.MaxTurns,
            ConcurrencyGroup = request.ConcurrencyGroup ?? string.Empty,
            Env = request.Environment,
            Model = request.Model,
            FallbackModel = null,
            ExtraContext = null,
            InlineSkillContent = null,
            PresetRunId = request.RunId,
            SessionScope = request.ChatTarget is not null ? "chat" : null,
            RetryOnResumeFailure = false,
            PersistSession = true,
            OnEventHook = request.OnEventHook,
            ChatTarget = request.ChatTarget,
            PendingSteerMessages = request.PendingSteerMessages,
            ImagePaths = request.ImagePaths
        };
        
        // Execute via ClaudeRunner
        var run = await _claudeRunner.RunAsync(context, cancellationToken);
        
        // Convert AgentRun to AgentRunResult
        return new AgentRunResult
        {
            Status = run.Status,
            ExitCode = run.ExitCode,
            Stdout = string.Empty, // ClaudeRunner streams directly to run object
            Stderr = string.Empty,
            StartedAt = run.StartedAt,
            FinishedAt = run.EndedAt ?? DateTimeOffset.UtcNow,
            Duration = run.EndedAt.HasValue ? (run.EndedAt.Value - run.StartedAt) : TimeSpan.Zero,
            RunnerKind = Kind,
            RunId = run.RunId,
            SessionId = run.SessionId,
            CommandDisplay = run.CommandDisplay ?? "Claude CLI",
            ExecutionMetadata = new ExecutionMetadata
            {
                Mode = request.ExecutionMode.ToString(),
                Runner = Kind,
                Provider = "anthropic",
                Model = request.Model ?? "claude-3-5-sonnet",
                Profile = request.Profile ?? "default",
                RunId = run.RunId,
                SessionId = run.SessionId,
                WorktreePath = request.WorktreePath,
                BranchName = request.BranchName,
                TicketId = request.TicketId?.ToString(),
                ProjectId = request.ProjectSlug,
                SteerSupported = true
            }
        };
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stop requested for Claude run {RunId}", runId);
        // ClaudeRunner doesn't have a direct StopAsync, but we can cancel via the AgentRun
        return false; // Let the caller fall back to CancellationTokenSource
    }
    
    public async Task<bool> SteerAsync(string runId, string message, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Steer requested for Claude run {RunId}: {Message}", runId, message);
        // Steering is handled directly via AgentRun.SteeringQueue in the endpoints
        return false;
    }
    
    public async Task<AgentRunStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        // Status is tracked in AgentRunRegistry; the caller should query that directly
        return AgentRunStatus.Running;
    }
}
