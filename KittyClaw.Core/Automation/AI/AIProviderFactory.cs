using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// Factory for creating AI providers based on configuration.
/// </summary>
public sealed class AIProviderFactory
{
    private readonly ILogger<AIProviderFactory>? _logger;
    private readonly Dictionary<string, IAIProvider> _providers = new();
    
    public AIProviderFactory(ILogger<AIProviderFactory>? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Register a provider with the factory
    /// </summary>
    public void RegisterProvider(IAIProvider provider)
    {
        _providers[provider.Id] = provider;
        _logger?.LogInformation("Registered AI provider: {ProviderId} - {ProviderName}", provider.Id, provider.Name);
    }
    
    /// <summary>
    /// Get a provider by ID
    /// </summary>
    public IAIProvider? GetProvider(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var provider))
        {
            return provider;
        }
        return null;
    }
    
    /// <summary>
    /// Get all registered providers
    /// </summary>
    public IEnumerable<IAIProvider> GetAllProviders() => _providers.Values;
    
    /// <summary>
    /// Create the default set of providers
    /// </summary>
    public static AIProviderFactory CreateDefault(
        ILogger<AIProviderFactory>? logger = null,
        OpenCodeSdkClient? sdkClient = null,
        string? opencodeCommand = null)
    {
        var factory = new AIProviderFactory(logger);
        
        // Register OpenCode provider
        var opencodeProvider = new OpenCodeProvider(logger, sdkClient, opencodeCommand);
        factory.RegisterProvider(opencodeProvider);
        
        // Register legacy Claude provider wrapper
        var claudeProvider = new LegacyClaudeProvider(logger);
        factory.RegisterProvider(claudeProvider);
        
        return factory;
    }
}

/// <summary>
/// Legacy Claude provider that wraps the existing ClaudeRunner.
/// This provides compatibility with the existing execution path.
/// </summary>
public sealed class LegacyClaudeProvider : IAIProvider
{
    private readonly ILogger<LegacyClaudeProvider>? _logger;
    
    public string Id => "claude";
    public string Name => "Claude (Legacy)";
    public bool IsAvailable => true; // Always available for backward compatibility
    
    public LegacyClaudeProvider(ILogger<LegacyClaudeProvider>? logger = null)
    {
        _logger = logger;
    }
    
    public async Task<AIProviderResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        // This provider is a marker for the legacy path
        // Actual execution is handled by ClaudeRunner in the existing flow
        _logger?.LogInformation("Legacy Claude provider selected for run {RunId}", request.RunId);
        
        // Return a placeholder result - actual execution happens elsewhere
        return new AIProviderResult
        {
            Status = AgentRunStatus.Running,
            ExitCode = null,
            Stdout = string.Empty,
            Stderr = string.Empty,
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            ProviderId = Id,
            Model = request.Model ?? "claude-3-5-sonnet",
            Profile = request.Profile ?? "default",
            RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
            CommandDisplay = "Legacy Claude execution"
        };
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stop requested for legacy Claude run {RunId}", runId);
        return false; // Not implemented - handled by existing mechanisms
    }
    
    public async Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        return new AIProviderStatus
        {
            RunId = runId,
            Status = AgentRunStatus.Running,
            ProviderId = Id
        };
    }
}
