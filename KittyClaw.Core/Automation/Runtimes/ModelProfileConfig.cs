namespace KittyClaw.Core.Automation.Runtimes;

public sealed class ModelProfileConfig
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = "";
    public string Model { get; init; } = "";
    public string Provider { get; init; } = "litellm";
    public string? BaseUrl { get; init; }
    public string? ApiKeyEnv { get; init; }
    public bool HighRiskAllowed { get; init; } = false;

    // ── Control Plane fields ──────────────────────────────────────────
    /// <summary>
    /// Full OpenCode model string in format "provider/model-id" (e.g., "kimi/kimi-2.7-code").
    /// This is what gets passed to OpenCode for execution.
    /// </summary>
    public string? OpencodeModel { get; init; }
    
    /// <summary>
    /// What this model is used for: coding, planning, review, cheap-subtasks, local-coding
    /// </summary>
    public string Purpose { get; init; } = "coding";
    
    /// <summary>Cost tier: free, low, medium, high, premium</summary>
    public string CostTier { get; init; } = "medium";
    
    /// <summary>Speed tier: instant, fast, medium, slow</summary>
    public string SpeedTier { get; init; } = "medium";
    
    /// <summary>Quality tier: basic, standard, strong, best</summary>
    public string QualityTier { get; init; } = "standard";
    
    /// <summary>Whether this model supports tool use (function calling)</summary>
    public bool SupportsTools { get; init; } = true;
    
    /// <summary>Whether this profile is active and available for selection</summary>
    public bool Enabled { get; init; } = true;
}
