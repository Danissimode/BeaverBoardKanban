using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// Adapter that allows OpenCode to work with Claude models.
/// This provides compatibility between the OpenCode execution path and Claude models.
/// </summary>
public sealed class OpenCodeClaudeAdapter : IAIProvider
{
    private readonly IAIProvider _innerProvider;
    private readonly ILogger<OpenCodeClaudeAdapter>? _logger;
    
    public string Id => "opencode-claude";
    public string Name => "OpenCode with Claude";
    public bool IsAvailable => _innerProvider.IsAvailable;
    
    public OpenCodeClaudeAdapter(IAIProvider innerProvider, ILogger<OpenCodeClaudeAdapter>? logger = null)
    {
        _innerProvider = innerProvider;
        _logger = logger;
    }
    
    public async Task<AIProviderResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("OpenCodeClaudeAdapter executing with model {Model}", request.Model);
        
        // Adapt the request for Claude models
        var adaptedRequest = new AIProviderRequest
        {
            ProjectSlug = request.ProjectSlug,
            WorkspacePath = request.WorkspacePath,
            AgentName = request.AgentName,
            SkillFile = request.SkillFile,
            TicketId = request.TicketId,
            TicketTitle = request.TicketTitle,
            TicketStatus = request.TicketStatus,
            Model = request.Model ?? "claude-3-5-sonnet",
            Profile = request.Profile ?? "claude",
            ExtraContext = request.ExtraContext,
            MaxTurns = request.MaxTurns,
            ConcurrencyGroup = request.ConcurrencyGroup,
            Env = request.Env,
            ExecutionMode = ExecutionMode.DirectOpenCode,
            SessionScope = request.SessionScope,
            PersistSession = request.PersistSession,
            OnEventHook = request.OnEventHook,
            RunId = request.RunId
        };
        
        var result = await _innerProvider.ExecuteAsync(adaptedRequest, cancellationToken);
        
        // Adapt the result
        return new AIProviderResult
        {
            Status = result.Status,
            ExitCode = result.ExitCode,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            Duration = result.Duration,
            ProviderId = Id,
            Model = result.Model,
            Profile = result.Profile,
            SessionId = result.SessionId,
            RunId = result.RunId,
            CommandDisplay = result.CommandDisplay,
            Artifacts = result.Artifacts
        };
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        return await _innerProvider.StopAsync(runId, cancellationToken);
    }
    
    public async Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        var status = await _innerProvider.GetStatusAsync(runId, cancellationToken);
        return new AIProviderStatus
        {
            RunId = status.RunId,
            Status = status.Status,
            SessionId = status.SessionId,
            Model = status.Model,
            ProviderId = Id,
            StartedAt = status.StartedAt,
            FinishedAt = status.FinishedAt
        };
    }
}
