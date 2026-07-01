using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.Runners;
using KittyClaw.Core.Automation.Runtimes;
using KittyClaw.Core.Automation.Triggers;
using KittyClaw.Core.Integrations.OpenCode;
using KittyClaw.Core.Services;
using RunnerRequest = KittyClaw.Core.Automation.Runners.AgentRunRequest;
using RunnerResult = KittyClaw.Core.Automation.Runners.AgentRunResult;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Runner integration for ActionExecutor.
/// This partial class contains methods for working with the RunnerRegistry.
/// </summary>
internal sealed partial class ActionExecutor
{
    private RunnerRegistry? _runnerRegistry;
    private ITicketExecutionMetadataStore? _metadataStore;
    private IExecutionPolicyService? _policyService;
    private IWorktreeService? _worktreeService;
    private RosterStore? _rosterStore;
    
    /// <summary>
    /// Initialize runner services (called from constructor)
    /// </summary>
    private void InitializeRunnerServices(
        RunnerRegistry? runnerRegistry = null,
        ITicketExecutionMetadataStore? metadataStore = null,
        IExecutionPolicyService? policyService = null,
        IWorktreeService? worktreeService = null,
        RosterStore? rosterStore = null)
    {
        _runnerRegistry = runnerRegistry;
        _metadataStore = metadataStore;
        _policyService = policyService;
        _worktreeService = worktreeService;
        _rosterStore = rosterStore;
        
        if (_runnerRegistry is not null)
        {
            _logger.LogInformation("RunnerRegistry initialized with {RunnerCount} runners", 
                _runnerRegistry.GetAllRunners().Count());
        }
    }
    
    /// <summary>
    /// Resolve execution plan from roster (Control Plane).
    /// Returns null if roster is not configured or ticket has no slot assignment.
    /// </summary>
    private ResolvedExecution? ResolveFromRoster(
        int? ticketId,
        string? assignedSlotId,
        string? overrideModelProfileId,
        bool lockExecutor)
    {
        if (_rosterStore is null || ticketId is null)
        {
            return null;
        }
        
        // Load profiles from config
        var profiles = new Dictionary<string, Runtimes.ModelProfileConfig>();
        // TODO: Load from AgentRuntimeProjectConfig
        
        var resolver = new ExecutionResolver(
            _rosterStore.Slots.ToDictionary(s => s.Key),
            _rosterStore.Presets.ToDictionary(p => p.Key),
            _rosterStore.Fallbacks.ToDictionary(f => f.Key),
            profiles,
            _rosterStore.ActivePresetId);
        
        return resolver.Resolve(
            ticketId.Value,
            assignedSlotId,
            overrideModelProfileId,
            lockExecutor);
    }
    
    /// <summary>
    /// Apply resolved execution plan to runner request.
    /// Sets model, agent, and other fields from the roster resolution.
    /// </summary>
    private RunnerRequest ApplyRosterResolution(RunnerRequest request, ResolvedExecution resolved)
    {
        // Only apply if roster resolved a model/agent
        if (string.IsNullOrEmpty(resolved.ResolvedModel) && string.IsNullOrEmpty(resolved.ResolvedAgent))
        {
            return request;
        }
        
        return request with
        {
            Model = resolved.ResolvedModel ?? request.Model,
            // Provider is extracted from model string (e.g., "kimi/kimi-2.7-code" -> provider is implicit)
            // The runner will handle provider resolution from the model string
        };
    }
    
    /// <summary>
    /// Resolve the appropriate runner for a run
    /// </summary>
    private IAgentRunner ResolveRunner(RunAgentActionSpec action, string projectSlug, int? ticketId)
    {
        if (_runnerRegistry is null)
        {
            // Fallback to legacy behavior - create adapter from first runtime
            var runtime = _runtimes.FirstOrDefault();
            return new LegacyRunnerAdapter(runtime);
        }
        
        // Try to resolve by explicit runner kind
        if (!string.IsNullOrEmpty(action.RunnerKind))
        {
            var runner = _runnerRegistry.GetRunner(action.RunnerKind);
            if (runner is not null && runner.IsAvailable)
            {
                return runner;
            }
        }
        
        // Try to resolve by execution mode (parse string to enum)
        if (!string.IsNullOrEmpty(action.ExecutionMode) && Enum.TryParse<ExecutionMode>(action.ExecutionMode, true, out var mode))
        {
            var runner = _runnerRegistry.ResolveRunner(mode);
            if (runner is not null && runner.IsAvailable)
            {
                return runner;
            }
        }
        
        // Fall back to default runner
        return _runnerRegistry.GetDefaultRunner();
    }
    
    /// <summary>
    /// Check if execution is allowed by policy
    /// </summary>
    private async Task<PolicyDecision> CheckExecutionPolicyAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec action,
        IAgentRunner runner,
        CancellationToken ct)
    {
        if (_policyService is null)
        {
            return PolicyDecision.Allow();
        }
        
        var ticket = firing.TicketId is not null
            ? await _tickets.GetTicketAsync(rt.Slug, firing.TicketId.Value)
            : null;
        
        var labels = ticket?.Labels.Select(l => l.Name).ToList() ?? new List<string>();
        
        var executionMode = Enum.TryParse<ExecutionMode>(action.ExecutionMode, true, out var mode) 
            ? mode 
            : ExecutionMode.LegacyClaude;
        var provider = action.Provider ?? runner.Kind;
        var model = action.Model;
        
        return await _policyService.CanExecuteDirectAsync(
            rt.Slug,
            firing.TicketId ?? 0,
            executionMode,
            provider,
            model,
            labels,
            ct);
    }
    
    /// <summary>
    /// Ensure worktree exists for a ticket if required
    /// </summary>
    private async Task<WorktreeInfo?> EnsureWorktreeAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec action,
        IAgentRunner runner,
        string resolvedAgentName,
        CancellationToken ct)
    {
        if (_worktreeService is null || _policyService is null)
        {
            return null;
        }
        
        var executionMode = Enum.TryParse<ExecutionMode>(action.ExecutionMode, true, out var mode) 
            ? mode 
            : ExecutionMode.LegacyClaude;
        var worktreeRequired = await _policyService.IsWorktreeRequiredAsync(
            rt.Slug,
            firing.TicketId ?? 0,
            executionMode,
            ct);
        
        if (!worktreeRequired)
        {
            return null;
        }
        
        var context = new TicketExecutionContext
        {
            ProjectSlug = rt.Slug,
            WorkspacePath = rt.Workspace!,
            TicketId = firing.TicketId ?? 0,
            TicketTitle = firing.TicketTitle,
            Assignee = resolvedAgentName,
            ExecutionMode = executionMode,
            ForceWorktree = true
        };
        
        return await _worktreeService.EnsureForTicketAsync(context, ct);
    }
    
    /// <summary>
    /// Save execution metadata for a run
    /// </summary>
    private async Task SaveExecutionMetadataAsync(
        ProjectRuntime rt,
        TriggerFiring firing,
        RunAgentActionSpec action,
        IAgentRunner runner,
        AgentRun run,
        ExecutionMetadata? executionMetadata = null,
        CancellationToken ct = default)
    {
        if (_metadataStore is null)
        {
            return;
        }
        
        var metadata = new TicketExecutionMetadata
        {
            ProjectSlug = rt.Slug,
            TicketId = firing.TicketId ?? 0,
            RunId = run.RunId,
            ExecutionMode = action.ExecutionMode?.ToString() ?? ExecutionMode.LegacyClaude.ToString(),
            RunnerKind = runner.Kind,
            Provider = executionMetadata?.Provider ?? action.Provider,
            Model = executionMetadata?.Model ?? action.Model,
            Profile = executionMetadata?.Profile ?? action.Profile,
            OpenCodeAgent = executionMetadata?.OpenCodeAgent,
            SessionId = executionMetadata?.SessionId ?? run.SessionId,
            WorktreePath = executionMetadata?.WorktreePath,
            BranchName = executionMetadata?.BranchName,
            Status = run.Status,
            StartedAt = run.StartedAt,
            FinishedAt = run.EndedAt,
            LastError = null,
            PlanStatus = null,
            RiskLevel = null,
            ReviewRequired = false,
            ProofRequired = false
        };
        
        try
        {
            await _metadataStore.SaveAsync(metadata, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save execution metadata for run {RunId}", run.RunId);
        }
    }
}

/// <summary>
/// Legacy runner adapter for backward compatibility
/// </summary>
internal sealed class LegacyRunnerAdapter : IAgentRunner
{
    private readonly IAgentRuntime? _runtime;
    
    public string Kind => _runtime?.Id ?? "claude";
    public string DisplayName => "Claude (Legacy)";
    public bool IsAvailable => _runtime is not null;
    
    public LegacyRunnerAdapter(IAgentRuntime? runtime)
    {
        _runtime = runtime;
    }
    
    public async Task<RunnerResult> StartAsync(RunnerRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Legacy runner adapter should not be used directly");
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        return false;
    }
    
    public async Task<bool> SteerAsync(string runId, string message, CancellationToken cancellationToken)
    {
        return false;
    }
    
    public async Task<AgentRunStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        return AgentRunStatus.Running;
    }
}
