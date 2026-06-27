using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Integrations.OpenCode;

/// <summary>
/// OpenCode-specific execution policy service.
/// Implements policy decisions for DirectOpenCode and CAO-governed execution.
/// </summary>
public sealed class OpenCodeExecutionPolicyService : IExecutionPolicyService
{
    private readonly OpenCodePolicyConfig _config;
    private readonly ILogger<OpenCodeExecutionPolicyService>? _logger;
    
    public OpenCodeExecutionPolicyService(
        OpenCodePolicyConfig config,
        ILogger<OpenCodeExecutionPolicyService>? logger = null)
    {
        _config = config;
        _logger = logger;
    }
    
    public async Task<PolicyDecision> CanExecuteDirectAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        string? provider = null,
        string? model = null,
        IReadOnlyList<string>? labels = null,
        CancellationToken cancellationToken = default)
    {
        // Check if execution mode is allowed
        if (executionMode == ExecutionMode.LegacyClaude)
        {
            return PolicyDecision.Allow();
        }
        
        if (executionMode == ExecutionMode.DirectOpenCode)
        {
            // Check if DirectOpenCode is allowed
            if (!_config.DirectOpenCode.Enabled)
            {
                return PolicyDecision.Deny(
                    "Direct OpenCode execution is disabled",
                    "Enable DirectOpenCode in project settings");
            }
            
            // Check risk level
            var riskLevel = GetRiskLevel(labels);
            if (riskLevel == "high" && !_config.DirectOpenCode.AllowHighRisk)
            {
                return PolicyDecision.Deny(
                    "High-risk tickets require CAO governance",
                    "Use CaoGoverned mode or reduce risk level");
            }
            
            if (riskLevel == "medium" && !_config.DirectOpenCode.AllowMediumRisk)
            {
                return PolicyDecision.Deny(
                    "Medium-risk tickets require CAO governance",
                    "Use CaoGoverned mode or reduce risk level");
            }
            
            // Check forbidden paths
            if (labels is not null)
            {
                foreach (var forbiddenLabel in _config.DirectOpenCode.ForbiddenLabels)
                {
                    if (labels.Contains(forbiddenLabel))
                    {
                        return PolicyDecision.Deny(
                            $"Label '{forbiddenLabel}' requires CAO governance",
                            "Use CaoGoverned mode");
                    }
                }
            }
            
            return PolicyDecision.Allow();
        }
        
        if (executionMode == ExecutionMode.CaoGoverned)
        {
            // CAO is always allowed if configured
            return PolicyDecision.Allow();
        }
        
        return PolicyDecision.Deny("Execution mode not supported");
    }
    
    public async Task<PolicyDecision> CanMoveToDoneAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        AgentRunStatus runStatus,
        ExecutionMetadata? executionMetadata = null,
        CancellationToken cancellationToken = default)
    {
        // Check if run was successful
        if (runStatus != AgentRunStatus.Completed)
        {
            return PolicyDecision.Deny(
                "Cannot move to Done: run did not complete successfully",
                "Fix errors and retry");
        }
        
        // Check execution mode specific requirements
        if (executionMode == ExecutionMode.DirectOpenCode)
        {
            // For DirectOpenCode, check if done gate is enabled
            if (_config.DoneGate.RequireForDirectOpenCode)
            {
                // Check if lightweight checks are required
                if (_config.DoneGate.RequireSummary && string.IsNullOrEmpty(executionMetadata?.SessionId))
                {
                    return PolicyDecision.Deny(
                        "Done gate: summary required",
                        "Add execution summary");
                }
                
                if (_config.DoneGate.RequireLightweightChecks)
                {
                    // TODO: Implement lightweight checks
                    return PolicyDecision.Deny(
                        "Done gate: lightweight checks not yet implemented",
                        "Wait for implementation");
                }
            }
        }
        
        if (executionMode == ExecutionMode.CaoGoverned)
        {
            // For CAO, require closeout
            if (_config.DoneGate.RequireCaoCloseout)
            {
                return PolicyDecision.Deny(
                    "Done gate: CAO closeout required",
                    "Complete CAO closeout process");
            }
        }
        
        return PolicyDecision.Allow();
    }
    
    public async Task<bool> IsWorktreeRequiredAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        CancellationToken cancellationToken = default)
    {
        if (executionMode == ExecutionMode.LegacyClaude)
        {
            return _config.Worktree.RequireForLegacyClaude;
        }
        
        if (executionMode == ExecutionMode.DirectOpenCode)
        {
            return _config.Worktree.RequireForDirectOpenCode;
        }
        
        if (executionMode == ExecutionMode.CaoGoverned)
        {
            return _config.Worktree.RequireForCaoGoverned;
        }
        
        return false;
    }
    
    public async Task<bool> IsCaoRequiredAsync(
        string projectSlug, 
        int ticketId, 
        IReadOnlyList<string>? labels = null,
        CancellationToken cancellationToken = default)
    {
        if (labels is null)
        {
            return false;
        }
        
        // Check if any label requires CAO
        foreach (var caoLabel in _config.CaoRequired.Labels)
        {
            if (labels.Contains(caoLabel))
            {
                return true;
            }
        }
        
        // Check risk level
        var riskLevel = GetRiskLevel(labels);
        if (riskLevel == "high" && _config.CaoRequired.ForHighRisk)
        {
            return true;
        }
        
        if (riskLevel == "medium" && _config.CaoRequired.ForMediumRisk)
        {
            return true;
        }
        
        return false;
    }
    
    public async Task<IReadOnlyList<string>> GetAllowedProvidersAsync(
        string projectSlug, 
        CancellationToken cancellationToken = default)
    {
        return _config.DirectOpenCode.AllowedProviders;
    }
    
    public async Task<IReadOnlyList<string>> GetAllowedModelsAsync(
        string projectSlug, 
        string provider,
        CancellationToken cancellationToken = default)
    {
        if (_config.DirectOpenCode.AllowedModelsByProvider.TryGetValue(provider, out var models))
        {
            return models;
        }
        
        return new List<string>();
    }
    
    private string GetRiskLevel(IReadOnlyList<string>? labels)
    {
        if (labels is null)
        {
            return "low";
        }
        
        foreach (var label in labels)
        {
            if (_config.RiskLabels.HighRisk.Contains(label))
            {
                return "high";
            }
            
            if (_config.RiskLabels.MediumRisk.Contains(label))
            {
                return "medium";
            }
        }
        
        return "low";
    }
}

/// <summary>
/// Configuration for OpenCode execution policies
/// </summary>
public sealed class OpenCodePolicyConfig
{
    public DirectOpenCodePolicy DirectOpenCode { get; set; } = new();
    public CaoRequiredPolicy CaoRequired { get; set; } = new();
    public DoneGatePolicy DoneGate { get; set; } = new();
    public WorktreePolicy Worktree { get; set; } = new();
    public RiskLabelsConfig RiskLabels { get; set; } = new();
}

public sealed class DirectOpenCodePolicy
{
    public bool Enabled { get; set; } = true;
    public bool AllowHighRisk { get; set; } = false;
    public bool AllowMediumRisk { get; set; } = false;
    public List<string> ForbiddenLabels { get; set; } = new()
    {
        "security", "rls", "payment", "privacy", "sre", "provider-proof"
    };
    public List<string> AllowedProviders { get; set; } = new()
    {
        "openai", "anthropic", "openrouter", "ollama", "mistral", "gemini", "deepseek"
    };
    public Dictionary<string, List<string>> AllowedModelsByProvider { get; set; } = new();
}

public sealed class CaoRequiredPolicy
{
    public bool ForHighRisk { get; set; } = true;
    public bool ForMediumRisk { get; set; } = true;
    public List<string> Labels { get; set; } = new()
    {
        "security", "rls", "payment", "privacy", "sre", "provider-proof"
    };
}

public sealed class DoneGatePolicy
{
    public bool RequireForDirectOpenCode { get; set; } = false;
    public bool RequireForCaoGoverned { get; set; } = true;
    public bool RequireSummary { get; set; } = true;
    public bool RequireLightweightChecks { get; set; } = false;
    public bool RequireCaoCloseout { get; set; } = true;
}

public sealed class WorktreePolicy
{
    public bool RequireForLegacyClaude { get; set; } = false;
    public bool RequireForDirectOpenCode { get; set; } = true;
    public bool RequireForCaoGoverned { get; set; } = true;
}

public sealed class RiskLabelsConfig
{
    public List<string> HighRisk { get; set; } = new()
    {
        "security", "rls", "payment", "privacy", "sre"
    };
    
    public List<string> MediumRisk { get; set; } = new()
    {
        "provider-proof", "infrastructure", "database"
    };
}
