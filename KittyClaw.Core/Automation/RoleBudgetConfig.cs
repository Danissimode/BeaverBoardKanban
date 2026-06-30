using System;
using System.Collections.Generic;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Per-role token and cost budgets for Beaver Board agents.
/// Used by <see cref="TokenBudgetService"/> to enforce limits and suggest fallbacks.
/// </summary>
public sealed class RoleBudgetConfig
{
    /// <summary>
    /// Maximum input tokens per run for this role.
    /// </summary>
    public int MaxInputTokens { get; init; } = 80000;
    
    /// <summary>
    /// Maximum output tokens per run for this role.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 8000;
    
    /// <summary>
    /// Maximum cost in USD per card/run for this role.
    /// </summary>
    public decimal MaxCostPerCard { get; init; } = 0.50m;
    
    /// <summary>
    /// Default model to use for this role.
    /// </summary>
    public string DefaultModel { get; init; } = "";
    
    /// <summary>
    /// Fallback model to use when primary model is unavailable or too expensive.
    /// </summary>
    public string FallbackModel { get; init; } = "";
    
    /// <summary>
    /// Whether this role is allowed to use the most expensive models.
    /// </summary>
    public bool AllowPremiumModels { get; init; } = false;

    /// <summary>
    /// Built-in role budgets. Key is the role id (e.g., "planner", "builder").
    /// </summary>
    public static readonly Dictionary<string, RoleBudgetConfig> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["planner"] = new()
        {
            MaxInputTokens = 40000,
            MaxOutputTokens = 6000,
            MaxCostPerCard = 0.20m,
            DefaultModel = "anthropic/claude-3-5-haiku-20250514",
            FallbackModel = "openai/gpt-4o-mini",
            AllowPremiumModels = false,
        },
        ["builder"] = new()
        {
            MaxInputTokens = 80000,
            MaxOutputTokens = 10000,
            MaxCostPerCard = 0.80m,
            DefaultModel = "anthropic/claude-sonnet-4-20250514",
            FallbackModel = "anthropic/claude-3-5-haiku-20250514",
            AllowPremiumModels = true,
        },
        ["reviewer"] = new()
        {
            MaxInputTokens = 50000,
            MaxOutputTokens = 5000,
            MaxCostPerCard = 0.30m,
            DefaultModel = "anthropic/claude-3-5-sonnet-20241022",
            FallbackModel = "anthropic/claude-3-5-haiku-20250514",
            AllowPremiumModels = false,
        },
        ["security"] = new()
        {
            MaxInputTokens = 60000,
            MaxOutputTokens = 8000,
            MaxCostPerCard = 0.50m,
            DefaultModel = "anthropic/claude-opus-4-20250514",
            FallbackModel = "anthropic/claude-3-5-sonnet-20241022",
            AllowPremiumModels = true,
        },
        ["qa"] = new()
        {
            MaxInputTokens = 60000,
            MaxOutputTokens = 6000,
            MaxCostPerCard = 0.30m,
            DefaultModel = "anthropic/claude-3-5-sonnet-20241022",
            FallbackModel = "openai/gpt-4o-mini",
            AllowPremiumModels = false,
        },
        ["docs"] = new()
        {
            MaxInputTokens = 30000,
            MaxOutputTokens = 4000,
            MaxCostPerCard = 0.10m,
            DefaultModel = "openai/gpt-4o-mini",
            FallbackModel = "openai/gpt-4o-mini",
            AllowPremiumModels = false,
        },
        ["supervisor"] = new()
        {
            MaxInputTokens = 20000,
            MaxOutputTokens = 2000,
            MaxCostPerCard = 0.10m,
            DefaultModel = "anthropic/claude-3-5-haiku-20250514",
            FallbackModel = "openai/gpt-4o-mini",
            AllowPremiumModels = false,
        },
    };

    /// <summary>
    /// Gets the budget for a role, returning a default budget if not found.
    /// </summary>
    public static RoleBudgetConfig For(string roleId) =>
        Defaults.TryGetValue(roleId, out var config) ? config : new();
}
