using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KittyClaw.Core.Automation.Runners;

/// <summary>
/// Interface for provider/model catalog.
/// This is a stable extension point that allows integrating with OpenCode's provider/model ecosystem.
/// </summary>
public interface IProviderModelCatalog
{
    /// <summary>
    /// Get all available providers
    /// </summary>
    Task<IReadOnlyList<ProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all available models for a provider
    /// </summary>
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string provider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get model information by ID
    /// </summary>
    Task<ModelInfo?> GetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Refresh the catalog from external sources
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a provider is configured and authenticated
    /// </summary>
    Task<bool> IsProviderConfiguredAsync(string provider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a model is available for a provider
    /// </summary>
    Task<bool> IsModelAvailableAsync(string provider, string modelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a provider
/// </summary>
public sealed class ProviderInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsConfigured { get; init; }
    public bool IsAuthenticated { get; init; }
    public bool SupportsTools { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsLocal { get; init; }
    public string? CostTier { get; init; }
    public string? HealthStatus { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
}

/// <summary>
/// Information about a model
/// </summary>
public sealed class ModelInfo
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool SupportsTools { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsLocal { get; init; }
    public string? CostTier { get; init; }
    public long? ContextLength { get; init; }
    public string? Pricing { get; init; }
    public bool IsAvailable { get; init; }
    public string? HealthStatus { get; init; }
}
