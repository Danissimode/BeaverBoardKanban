using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Integrations.OpenCode;

/// <summary>
/// OpenCode-specific implementation of IProviderModelCatalog.
/// This integrates with OpenCode's provider/model ecosystem.
/// </summary>
public sealed class OpenCodeProviderModelCatalog : IProviderModelCatalog
{
    private readonly OpenCodeConfig _config;
    private readonly ILogger<OpenCodeProviderModelCatalog>? _logger;
    private readonly List<ProviderInfo> _providers = new();
    private readonly Dictionary<string, List<ModelInfo>> _modelsByProvider = new();
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    
    public OpenCodeProviderModelCatalog(
        OpenCodeConfig config,
        ILogger<OpenCodeProviderModelCatalog>? logger = null)
    {
        _config = config;
        _logger = logger;
        
        // Initialize with default providers
        InitializeDefaultProviders();
    }
    
    private void InitializeDefaultProviders()
    {
        // Default providers supported by OpenCode
        var defaultProviders = new List<ProviderInfo>
        {
            new ProviderInfo
            {
                Id = "openai",
                Name = "OpenAI",
                Description = "OpenAI models including GPT-4, GPT-3.5",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = true,
                SupportsLocal = false,
                CostTier = "high",
                HealthStatus = "unknown"
            },
            new ProviderInfo
            {
                Id = "anthropic",
                Name = "Anthropic",
                Description = "Anthropic Claude models",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = true,
                SupportsLocal = false,
                CostTier = "high",
                HealthStatus = "unknown"
            },
            new ProviderInfo
            {
                Id = "openrouter",
                Name = "OpenRouter",
                Description = "OpenRouter multi-provider gateway",
                IsConfigured = true, // Assume configured by default
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = true,
                SupportsLocal = false,
                CostTier = "medium",
                HealthStatus = "healthy"
            },
            new ProviderInfo
            {
                Id = "ollama",
                Name = "Ollama",
                Description = "Local LLM models via Ollama",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = false,
                SupportsVision = false,
                SupportsLocal = true,
                CostTier = "low",
                HealthStatus = "unknown"
            },
            new ProviderInfo
            {
                Id = "mistral",
                Name = "Mistral",
                Description = "Mistral AI models",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = true,
                SupportsLocal = false,
                CostTier = "medium",
                HealthStatus = "unknown"
            },
            new ProviderInfo
            {
                Id = "gemini",
                Name = "Gemini",
                Description = "Google Gemini models",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = true,
                SupportsLocal = false,
                CostTier = "medium",
                HealthStatus = "unknown"
            },
            new ProviderInfo
            {
                Id = "deepseek",
                Name = "DeepSeek",
                Description = "DeepSeek models including DeepSeek-V4",
                IsConfigured = false,
                IsAuthenticated = false,
                SupportsTools = true,
                SupportsVision = false,
                SupportsLocal = false,
                CostTier = "medium",
                HealthStatus = "unknown"
            }
        };
        
        _providers.AddRange(defaultProviders);
        
        // Initialize default models for each provider
        InitializeDefaultModels();
    }
    
    private void InitializeDefaultModels()
    {
        // OpenAI models
        _modelsByProvider["openai"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "gpt-4o", ProviderId = "openai", Name = "GPT-4o", SupportsTools = true, SupportsVision = true, CostTier = "high", ContextLength = 128000 },
            new ModelInfo { Id = "gpt-4o-mini", ProviderId = "openai", Name = "GPT-4o Mini", SupportsTools = true, SupportsVision = true, CostTier = "medium", ContextLength = 128000 },
            new ModelInfo { Id = "gpt-4", ProviderId = "openai", Name = "GPT-4", SupportsTools = true, SupportsVision = true, CostTier = "high", ContextLength = 128000 },
            new ModelInfo { Id = "gpt-3.5-turbo", ProviderId = "openai", Name = "GPT-3.5 Turbo", SupportsTools = true, SupportsVision = false, CostTier = "low", ContextLength = 16384 }
        };
        
        // Anthropic models
        _modelsByProvider["anthropic"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "claude-3-5-sonnet", ProviderId = "anthropic", Name = "Claude 3.5 Sonnet", SupportsTools = true, SupportsVision = true, CostTier = "medium", ContextLength = 200000 },
            new ModelInfo { Id = "claude-3-5-haiku", ProviderId = "anthropic", Name = "Claude 3.5 Haiku", SupportsTools = true, SupportsVision = true, CostTier = "low", ContextLength = 200000 },
            new ModelInfo { Id = "claude-3-opus", ProviderId = "anthropic", Name = "Claude 3 Opus", SupportsTools = true, SupportsVision = true, CostTier = "high", ContextLength = 200000 }
        };
        
        // OpenRouter models (popular ones)
        _modelsByProvider["openrouter"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "openai/gpt-4o", ProviderId = "openrouter", Name = "GPT-4o (OpenRouter)", SupportsTools = true, SupportsVision = true, CostTier = "high", ContextLength = 128000 },
            new ModelInfo { Id = "anthropic/claude-3-5-sonnet", ProviderId = "openrouter", Name = "Claude 3.5 Sonnet (OpenRouter)", SupportsTools = true, SupportsVision = true, CostTier = "medium", ContextLength = 200000 },
            new ModelInfo { Id = "qwen/qwen3.5-coder", ProviderId = "openrouter", Name = "Qwen 3.5 Coder (OpenRouter)", SupportsTools = true, SupportsVision = false, CostTier = "low", ContextLength = 128000 },
            new ModelInfo { Id = "deepseek/deepseek-v4-pro", ProviderId = "openrouter", Name = "DeepSeek V4 Pro (OpenRouter)", SupportsTools = true, SupportsVision = false, CostTier = "medium", ContextLength = 128000 },
            new ModelInfo { Id = "mistral/mistral-large", ProviderId = "openrouter", Name = "Mistral Large (OpenRouter)", SupportsTools = true, SupportsVision = false, CostTier = "medium", ContextLength = 128000 }
        };
        
        // Ollama models
        _modelsByProvider["ollama"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "llama3.2", ProviderId = "ollama", Name = "Llama 3.2", SupportsTools = false, SupportsVision = false, CostTier = "low", ContextLength = 128000, IsAvailable = false },
            new ModelInfo { Id = "mistral", ProviderId = "ollama", Name = "Mistral", SupportsTools = false, SupportsVision = false, CostTier = "low", ContextLength = 128000, IsAvailable = false },
            new ModelInfo { Id = "phi3", ProviderId = "ollama", Name = "Phi 3", SupportsTools = false, SupportsVision = false, CostTier = "low", ContextLength = 128000, IsAvailable = false }
        };
        
        // Mistral models
        _modelsByProvider["mistral"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "mistral-large", ProviderId = "mistral", Name = "Mistral Large", SupportsTools = true, SupportsVision = false, CostTier = "medium", ContextLength = 128000 },
            new ModelInfo { Id = "mistral-small", ProviderId = "mistral", Name = "Mistral Small", SupportsTools = true, SupportsVision = false, CostTier = "low", ContextLength = 32000 }
        };
        
        // Gemini models
        _modelsByProvider["gemini"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "gemini-1.5-pro", ProviderId = "gemini", Name = "Gemini 1.5 Pro", SupportsTools = true, SupportsVision = true, CostTier = "medium", ContextLength = 1048576 },
            new ModelInfo { Id = "gemini-1.5-flash", ProviderId = "gemini", Name = "Gemini 1.5 Flash", SupportsTools = true, SupportsVision = true, CostTier = "low", ContextLength = 1048576 }
        };
        
        // DeepSeek models
        _modelsByProvider["deepseek"] = new List<ModelInfo>
        {
            new ModelInfo { Id = "deepseek-v4-pro", ProviderId = "deepseek", Name = "DeepSeek V4 Pro", SupportsTools = true, SupportsVision = false, CostTier = "medium", ContextLength = 128000 },
            new ModelInfo { Id = "deepseek-v4", ProviderId = "deepseek", Name = "DeepSeek V4", SupportsTools = true, SupportsVision = false, CostTier = "medium", ContextLength = 128000 }
        };
    }
    
    public async Task<IReadOnlyList<ProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        // If we haven't refreshed recently, try to refresh from OpenCode
        if ((DateTimeOffset.UtcNow - _lastRefresh).TotalMinutes > 5)
        {
            await RefreshAsync(cancellationToken);
        }
        
        return _providers.ToList();
    }
    
    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(string provider, CancellationToken cancellationToken = default)
    {
        if (_modelsByProvider.TryGetValue(provider, out var models))
        {
            return models.ToList();
        }
        
        return new List<ModelInfo>();
    }
    
    public async Task<ModelInfo?> GetModelAsync(string provider, string modelId, CancellationToken cancellationToken = default)
    {
        var models = await GetModelsAsync(provider, cancellationToken);
        return models.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }
    
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Implement refresh from OpenCode server or CLI
        _logger?.LogInformation("Refreshing OpenCode provider/model catalog");
        
        // For now, just update the timestamp
        _lastRefresh = DateTimeOffset.UtcNow;
        
        // In a real implementation, this would:
        // 1. Call OpenCode server API to get providers/models
        // 2. Or parse OpenCode CLI output
        // 3. Update _providers and _modelsByProvider
    }
    
    public async Task<bool> IsProviderConfiguredAsync(string provider, CancellationToken cancellationToken = default)
    {
        var providers = await GetProvidersAsync(cancellationToken);
        var providerInfo = providers.FirstOrDefault(p => string.Equals(p.Id, provider, StringComparison.OrdinalIgnoreCase));
        
        return providerInfo?.IsConfigured ?? false;
    }
    
    public async Task<bool> IsModelAvailableAsync(string provider, string modelId, CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(provider, modelId, cancellationToken);
        return model?.IsAvailable ?? false;
    }
}
