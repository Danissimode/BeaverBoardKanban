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
}
