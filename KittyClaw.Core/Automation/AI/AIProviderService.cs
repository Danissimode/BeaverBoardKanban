using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// Service for resolving and managing AI provider configurations.
/// Handles the hierarchy: agent config -> ticket config -> project config -> global config.
/// </summary>
public sealed class AIProviderService
{
    private readonly AIProviderFactory _providerFactory;
    private readonly ILogger<AIProviderService>? _logger;
    
    public AIProviderService(
        AIProviderFactory providerFactory,
        ILogger<AIProviderService>? logger = null)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }
    
    /// <summary>
    /// Resolve the effective AI configuration for a run.
    /// Uses hierarchy: agent config -> ticket config -> project config -> global config.
    /// </summary>
    public async Task<EffectiveAIConfig> ResolveEffectiveConfigAsync(
        string projectSlug,
        int? ticketId,
        string agentName,
        AIProviderConfig? agentConfig = null,
        AIProviderConfig? ticketConfig = null,
        AIProviderConfig? projectConfig = null,
        AIProviderConfig? globalConfig = null,
        CancellationToken cancellationToken = default)
    {
        // Build the hierarchy
        var configs = new List<(AIProviderConfig config, string source)>
        {
            (globalConfig ?? new AIProviderConfig(), "global"),
            (projectConfig ?? new AIProviderConfig(), "project"),
            (ticketConfig ?? new AIProviderConfig(), "ticket"),
            (agentConfig ?? new AIProviderConfig(), "agent")
        };
        
        // Find the most specific configuration
        AIProviderConfig? effectiveConfig = null;
        string effectiveSource = "default";
        
        for (int i = configs.Count - 1; i >= 0; i--)
        {
            var (config, source) = configs[i];
            if (config.IsConfigured)
            {
                effectiveConfig = config;
                effectiveSource = source;
                break;
            }
        }
        
        // Determine execution mode
        var executionMode = effectiveConfig?.ExecutionMode ?? ExecutionMode.LegacyClaude;
        
        // Determine provider
        var providerId = effectiveConfig?.Provider ?? 
                        (executionMode == ExecutionMode.DirectOpenCode ? "opencode" : "claude");
        
        // Determine model
        var model = effectiveConfig?.Model ?? 
                   (executionMode == ExecutionMode.DirectOpenCode ? "deepseek-v4-pro" : "claude-3-5-sonnet");
        
        // Determine profile
        var profile = effectiveConfig?.Profile ?? "developer";
        
        // Get the provider
        var provider = _providerFactory.GetProvider(providerId);
        
        // If provider not found or not available, fall back to legacy
        if (provider is null || !provider.IsAvailable)
        {
            _logger?.LogWarning("Provider {ProviderId} not available, falling back to LegacyClaude", providerId);
            provider = _providerFactory.GetProvider("claude");
            executionMode = ExecutionMode.LegacyClaude;
            providerId = "claude";
            model = "claude-3-5-sonnet";
        }
        
        return new EffectiveAIConfig
        {
            ExecutionMode = executionMode,
            ProviderId = providerId,
            Provider = provider,
            Model = model,
            Profile = profile,
            Source = effectiveSource,
            UseEffectiveConfig = true
        };
    }
    
    /// <summary>
    /// Resolve AI configuration from explicit action parameters.
    /// </summary>
    public async Task<EffectiveAIConfig> ResolveFromActionAsync(
        RunAgentActionSpec action,
        string projectSlug,
        int? ticketId,
        string agentName,
        AIProviderConfig? agentConfig = null,
        AIProviderConfig? ticketConfig = null,
        AIProviderConfig? projectConfig = null,
        AIProviderConfig? globalConfig = null,
        CancellationToken cancellationToken = default)
    {
        // If action explicitly specifies provider, use it
        if (!string.IsNullOrEmpty(action.Provider))
        {
            var provider = _providerFactory.GetProvider(action.Provider);
            if (provider is not null && provider.IsAvailable)
            {
                return new EffectiveAIConfig
                {
                    ExecutionMode = action.ExecutionMode ?? ExecutionMode.DirectOpenCode,
                    ProviderId = action.Provider,
                    Provider = provider,
                    Model = action.Model ?? "deepseek-v4-pro",
                    Profile = action.Profile ?? "developer",
                    Source = "action",
                    UseEffectiveConfig = false
                };
            }
            else
            {
                _logger?.LogWarning("Requested provider {Provider} not available, falling back to effective config", action.Provider);
            }
        }
        
        // Otherwise use effective config resolution
        return await ResolveEffectiveConfigAsync(
            projectSlug, ticketId, agentName, agentConfig, ticketConfig, projectConfig, globalConfig, cancellationToken);
    }
    
    /// <summary>
    /// Get all available providers
    /// </summary>
    public IEnumerable<IAIProvider> GetAvailableProviders() => _providerFactory.GetAllProviders();
    
    /// <summary>
    /// Check if a specific provider is available
    /// </summary>
    public bool IsProviderAvailable(string providerId) => 
        _providerFactory.GetProvider(providerId)?.IsAvailable ?? false;
}

/// <summary>
/// Configuration for AI provider selection
/// </summary>
public sealed class AIProviderConfig
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Profile { get; set; }
    public ExecutionMode? ExecutionMode { get; set; }
    
    public bool IsConfigured => !string.IsNullOrEmpty(Provider) || !string.IsNullOrEmpty(Model) || ExecutionMode.HasValue;
}

/// <summary>
/// Effective AI configuration resolved from hierarchy
/// </summary>
public sealed class EffectiveAIConfig
{
    public required ExecutionMode ExecutionMode { get; init; }
    public required string ProviderId { get; init; }
    public required IAIProvider? Provider { get; init; }
    public required string Model { get; init; }
    public required string Profile { get; init; }
    public required string Source { get; init; }
    public required bool UseEffectiveConfig { get; init; }
}
