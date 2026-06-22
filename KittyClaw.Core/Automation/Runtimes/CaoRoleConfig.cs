namespace KittyClaw.Core.Automation.Runtimes;

public sealed class CaoRoleConfig
{
    public required string Id { get; init; }
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public bool CanEditFiles { get; init; } = false;
    public bool CanRunShell { get; init; } = false;
    public bool CanRunTests { get; init; } = false;
    public bool CanUseNetwork { get; init; } = false;
    public bool CanApprove { get; init; } = false;
    public bool CanMoveToVerified { get; init; } = false;
    public bool CanMoveToDone { get; init; } = false;
    public bool AllowHighRisk { get; init; } = false;
    public IReadOnlyList<string> AllowedTools { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PromptRules { get; init; } = Array.Empty<string>();
}
