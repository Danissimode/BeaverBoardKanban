using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.AI;
using KittyClaw.Core.Automation.Runtimes;
using KittyClaw.Core.Services;

namespace KittyClaw.Core.Automation;

/// <summary>
/// AI Provider integration extension for ActionExecutor.
/// This partial class contains methods for resolving and executing AI provider configurations.
/// </summary>
internal sealed partial class ActionExecutor
{
    private AIProviderService? _aiProviderService;
    
    // AI Provider configuration cache
    private readonly ConcurrentDictionary<string, EffectiveAIConfig> _aiConfigCache = new();
    
    /// <summary>
    /// Initialize AI Provider Service (called from main constructor)
    /// </summary>
    private void InitializeAIProviderService(AIProviderService? aiProviderService)
    {
        _aiProviderService = aiProviderService;
        if (_aiProviderService is not null)
        {
            _logger.LogInformation("AI Provider Service initialized with {ProviderCount} providers", 
                _aiProviderService.GetAvailableProviders().Count());
        }
    }
    
    /// <summary>
    /// Resolve AI configuration for a run based on hierarchy and action parameters.
    /// </summary>
    private async Task<EffectiveAIConfig> ResolveAIConfigAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec action,
        string resolvedAgentName,
        CancellationToken ct)
    {
        if (_aiProviderService is null)
        {
            // Fallback to legacy behavior
            return new EffectiveAIConfig
            {
                ExecutionMode = ExecutionMode.LegacyClaude,
                ProviderId = "claude",
                Provider = null,
                Model = action.Model ?? "claude-3-5-sonnet",
                Profile = "default",
                Source = "legacy",
                UseEffectiveConfig = false
            };
        }
        
        var cacheKey = $"{rt.Slug}:{firing.TicketId}:{resolvedAgentName}";
        
        if (_aiConfigCache.TryGetValue(cacheKey, out var cachedConfig))
        {
            return cachedConfig;
        }
        
        try
        {
            // Get configs from hierarchy
            var agentConfig = await GetAgentAIConfigAsync(rt, resolvedAgentName, ct);
            var ticketConfig = firing.TicketId.HasValue 
                ? await GetTicketAIConfigAsync(rt.Slug, firing.TicketId.Value, ct) 
                : null;
            var projectConfig = await GetProjectAIConfigAsync(rt.Slug, ct);
            var globalConfig = await GetGlobalAIConfigAsync(ct);
            
            EffectiveAIConfig config;
            
            if (action.UseEffectiveConfig)
            {
                // Use hierarchy resolution
                config = await _aiProviderService.ResolveEffectiveConfigAsync(
                    rt.Slug,
                    firing.TicketId,
                    resolvedAgentName,
                    agentConfig,
                    ticketConfig,
                    projectConfig,
                    globalConfig,
                    ct);
            }
            else
            {
                // Use explicit action parameters
                config = await _aiProviderService.ResolveFromActionAsync(
                    action,
                    rt.Slug,
                    firing.TicketId,
                    resolvedAgentName,
                    agentConfig,
                    ticketConfig,
                    projectConfig,
                    globalConfig,
                    ct);
            }
            
            _aiConfigCache.TryAdd(cacheKey, config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve AI config for {CacheKey}, falling back to legacy", cacheKey);
            return new EffectiveAIConfig
            {
                ExecutionMode = ExecutionMode.LegacyClaude,
                ProviderId = "claude",
                Provider = null,
                Model = action.Model ?? "claude-3-5-sonnet",
                Profile = "default",
                Source = "fallback",
                UseEffectiveConfig = false
            };
        }
    }
    
    /// <summary>
    /// Get AI configuration for an agent
    /// </summary>
    private async Task<AIProviderConfig?> GetAgentAIConfigAsync(
        ProjectRuntime rt, 
        string agentName, 
        CancellationToken ct)
    {
        // TODO: Implement agent-level AI config from project files
        // For now, return null to use higher-level configs
        return null;
    }
    
    /// <summary>
    /// Get AI configuration for a ticket
    /// </summary>
    private async Task<AIProviderConfig?> GetTicketAIConfigAsync(
        string projectSlug, 
        int ticketId, 
        CancellationToken ct)
    {
        // TODO: Implement ticket-level AI config from ticket metadata
        // For now, return null to use higher-level configs
        return null;
    }
    
    /// <summary>
    /// Get AI configuration for a project
    /// </summary>
    private async Task<AIProviderConfig?> GetProjectAIConfigAsync(
        string projectSlug, 
        CancellationToken ct)
    {
        // TODO: Implement project-level AI config
        // For now, return null to use global config
        return null;
    }
    
    /// <summary>
    /// Get global AI configuration
    /// </summary>
    private async Task<AIProviderConfig?> GetGlobalAIConfigAsync(CancellationToken ct)
    {
        // TODO: Implement global AI config from settings
        // For now, return null to use defaults
        return null;
    }
    
    /// <summary>
    /// Create execution metadata for a run
    /// </summary>
    private ExecutionMetadata CreateExecutionMetadata(
        EffectiveAIConfig aiConfig,
        ProjectRuntime rt,
        TriggerFiring firing,
        string agentName,
        string runId)
    {
        return new ExecutionMetadata
        {
            Mode = aiConfig.ExecutionMode.ToString(),
            Runner = aiConfig.ProviderId,
            Provider = aiConfig.ProviderId,
            Model = aiConfig.Model,
            Profile = aiConfig.Profile,
            RunId = runId,
            SessionId = null, // Will be set during execution
            WorktreePath = rt.Workspace,
            BranchName = null, // Will be set if worktree is used
            TicketId = firing.TicketId?.ToString(),
            ProjectId = rt.Slug
        };
    }
    
    /// <summary>
    /// Execute agent run through AI provider path
    /// </summary>
    private async Task<AgentRun> ExecuteViaAIProviderAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec action,
        string agentName,
        string skillFile,
        string group,
        EffectiveAIConfig aiConfig,
        AgentRunRequest request,
        CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N");
        var executionMetadata = CreateExecutionMetadata(aiConfig, rt, firing, agentName, runId);
        
        var run = new AgentRun
        {
            RunId = runId,
            ProjectSlug = rt.Slug,
            TicketId = firing.TicketId,
            AgentName = agentName,
            SkillFile = skillFile,
            ConcurrencyGroup = group,
            StartedAt = DateTime.UtcNow,
            RuntimeId = aiConfig.ProviderId,
            RoleId = aiConfig.Profile,
            ModelProfileId = aiConfig.Model,
            ExecutionMetadata = executionMetadata
        };
        
        _runs.Register(run);
        
        if (firing.TicketId is not null)
        {
            try { await _tickets.AddActivityAsync(rt.Slug, firing.TicketId.Value, _loc.Get("ActAgentStarted", agentName), "automation"); }
            catch { /* non-blocking */ }
        }
        
        // Fire-and-forget wrapper
        var runTask = Task.Run(async () =>
        {
            try
            {
                if (aiConfig.Provider is null)
                {
                    throw new InvalidOperationException("No AI provider available");
                }
                
                var providerRequest = new AIProviderRequest
                {
                    ProjectSlug = rt.Slug,
                    WorkspacePath = rt.Workspace!,
                    AgentName = agentName,
                    SkillFile = skillFile,
                    TicketId = firing.TicketId,
                    TicketTitle = firing.TicketTitle,
                    TicketStatus = firing.TicketStatus,
                    Model = aiConfig.Model,
                    Profile = aiConfig.Profile,
                    ExtraContext = action.Context,
                    MaxTurns = action.MaxTurns,
                    ConcurrencyGroup = group,
                    Env = action.Env,
                    ExecutionMode = aiConfig.ExecutionMode,
                    SessionScope = null,
                    PersistSession = true,
                    OnEventHook = (e) => run.Push(e),
                    RunId = runId
                };
                
                var result = await aiConfig.Provider.ExecuteAsync(providerRequest, ct);
                
                // Update run with results
                run.Status = result.Status;
                run.ExitCode = result.ExitCode;
                run.EndedAt = result.FinishedAt.UtcDateTime;
                run.RuntimeId = aiConfig.ProviderId;
                run.CommandDisplay = result.CommandDisplay;
                
                if (!string.IsNullOrEmpty(result.Stdout))
                    run.Push(new StreamEvent(result.StartedAt.UtcDateTime, "stdout", result.Stdout));
                if (!string.IsNullOrEmpty(result.Stderr))
                    run.Push(new StreamEvent(result.StartedAt.UtcDateTime, "stderr", result.Stderr));
                
                // Update execution metadata
                if (run.ExecutionMetadata is not null)
                {
                    run.ExecutionMetadata.SessionId = result.SessionId;
                }
                
                _runs.Complete(runId, result.Status, result.ExitCode);
                return run;
            }
            catch (OperationCanceledException)
            {
                if (run.Status == AgentRunStatus.Running)
                {
                    run.Status = AgentRunStatus.Stopped;
                    run.EndedAt = DateTime.UtcNow;
                    _runs.Complete(runId, AgentRunStatus.Stopped, null);
                }
                return run;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Provider execution failed for run {RunId}", runId);
                if (run.Status == AgentRunStatus.Running)
                {
                    run.Status = AgentRunStatus.Failed;
                    run.EndedAt = DateTime.UtcNow;
                    run.Push(new StreamEvent(DateTime.UtcNow, "stderr", ex.Message));
                    _runs.Complete(runId, AgentRunStatus.Failed, null);
                }
                
                // Add comment to ticket about failure
                if (firing.TicketId is not null)
                {
                    try
                    {
                        await _tickets.AddCommentAsync(
                            rt.Slug, 
                            firing.TicketId.Value,
                            $"AI Provider execution failed: {ex.Message}",
                            "automation");
                    }
                    catch { /* non-blocking */ }
                }
                
                return run;
            }
        });
        
        return await runTask;
    }
    
    /// <summary>
    /// Determine if we should use AI provider path or legacy path
    /// </summary>
    private bool ShouldUseAIProviderPath(EffectiveAIConfig aiConfig)
    {
        // Use AI provider path if:
        // 1. Provider is available
        // 2. Execution mode is not LegacyClaude
        // 3. We have a valid provider
        return aiConfig.Provider is not null && 
               aiConfig.Provider.IsAvailable &&
               aiConfig.ExecutionMode != ExecutionMode.LegacyClaude;
    }
}
