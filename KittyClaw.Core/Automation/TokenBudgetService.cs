using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Token budget and cost economy service for Beaver Board agents.
///
/// Provides:
/// - Context size estimation for cards and chat messages
/// - Budget enforcement per role
/// - Fallback model suggestions when approaching limits
/// - Daily cost cap enforcement via <see cref="CostTracker"/>
/// - Broadcast fanout cost warnings
/// </summary>
public sealed class TokenBudgetService
{
    private readonly CostTracker _costTracker;
    private readonly ILogger<TokenBudgetService>? _logger;

    // Average tokens per character ratio for LLM tokenization (conservative: ~4 chars/token)
    private const double CharsPerToken = 4.0;

    // Per-role budgets (can be overridden per-project)
    private readonly Dictionary<string, RoleBudgetConfig> _roleBudgets;

    // Daily cost cap per workspace (USD)
    private decimal _dailyCostCapPerWorkspace = 10.0m;

    public TokenBudgetService(CostTracker costTracker, ILogger<TokenBudgetService>? logger = null)
    {
        _costTracker = costTracker;
        _logger = logger;
        _roleBudgets = new Dictionary<string, RoleBudgetConfig>(RoleBudgetConfig.Defaults, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets a custom budget for a role, overriding the default.
    /// </summary>
    public void SetRoleBudget(string roleId, RoleBudgetConfig config)
    {
        _roleBudgets[roleId] = config;
    }

    /// <summary>
    /// Sets the daily cost cap across all workspaces.
    /// </summary>
    public void SetDailyCostCap(decimal usd) => _dailyCostCapPerWorkspace = usd;

    /// <summary>
    /// Estimates the token count for a card's full execution context.
    /// Used to warn before sending a large context to an agent.
    /// </summary>
    public TokenEstimate EstimateCardContext(
        string? title,
        string? description,
        IReadOnlyList<string>? labels,
        string? assignee,
        string? status,
        string? lastEvidenceSummary,
        int? previousRunCount)
    {
        var parts = new List<(string Label, int Chars)>();

        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(("title", title.Length));
        if (!string.IsNullOrWhiteSpace(description))
            parts.Add(("description", description.Length));
        if (labels is { Count: > 0 })
            parts.Add(("labels", string.Join(",", labels).Length));
        if (!string.IsNullOrWhiteSpace(assignee))
            parts.Add(("assignee", assignee.Length));
        if (!string.IsNullOrWhiteSpace(status))
            parts.Add(("status", status.Length));
        if (!string.IsNullOrWhiteSpace(lastEvidenceSummary))
            parts.Add(("evidence", lastEvidenceSummary.Length));

        var totalChars = 0;
        foreach (var (_, chars) in parts)
            totalChars += chars;

        // Protocol overhead: skill preamble, SKILL.md, board instructions
        var overhead = 1200;
        var total = totalChars + overhead;

        return new TokenEstimate
        {
            EstimatedTokens = (int)(total / CharsPerToken),
            Breakdown = parts,
            HasLargeContext = totalChars > 5000,
            ContextSizeLevel = totalChars switch
            {
                > 10000 => ContextSizeLevel.Large,
                > 5000 => ContextSizeLevel.Medium,
                _ => ContextSizeLevel.Normal,
            }
        };
    }

    /// <summary>
    /// Estimates token cost for a chat broadcast to multiple agents.
    /// Returns the fanout cost so the UI can show a confirmation warning.
    /// </summary>
    public ChatFanoutEstimate EstimateChatFanout(
        string body,
        int recipientCount,
        bool includesFullBoardHistory)
    {
        var bodyTokens = (int)(body.Length / CharsPerToken);

        // Base per-recipient cost (agent processing overhead)
        var recipientOverhead = 300;
        var baseCost = bodyTokens + recipientOverhead;

        // Full board history adds significant cost
        var historyOverhead = includesFullBoardHistory ? 8000 : 0;

        var total = (baseCost + historyOverhead) * recipientCount;

        return new ChatFanoutEstimate
        {
            SingleMessageTokens = bodyTokens,
            PerRecipientTokens = baseCost,
            TotalTokensWithRecipients = total,
            RecipientCount = recipientCount,
            RequiresWarning = recipientCount > 3 || total > 50000,
            RequiresConfirmation = total > 100000,
        };
    }

    /// <summary>
    /// Estimates context size for an MCP tool call result.
    /// </summary>
    public int EstimateMcpContext(string rawOutput, int maxRecommended = 15000)
    {
        var tokens = (int)(rawOutput.Length / CharsPerToken);
        return tokens > maxRecommended ? maxRecommended : tokens;
    }

    /// <summary>
    /// Checks whether a run is within budget for the given role.
    /// Returns null if within budget, or a warning message if over/near limit.
    /// </summary>
    public BudgetCheckResult EnforceRunBudget(
        string workspacePath,
        string roleId,
        string model,
        int estimatedInputTokens,
        int estimatedOutputTokens)
    {
        var budget = _roleBudgets.TryGetValue(roleId, out var b) ? b : RoleBudgetConfig.For(roleId);

        var inputOk = estimatedInputTokens <= budget.MaxInputTokens;
        var outputOk = estimatedOutputTokens <= budget.MaxOutputTokens;
        var costOk = !_costTracker.IsBudgetExceeded(workspacePath, _dailyCostCapPerWorkspace);

        if (inputOk && outputOk && costOk)
            return new BudgetCheckResult { Allowed = true };

        return new BudgetCheckResult
        {
            Allowed = false,
            Reason = !inputOk
                ? $"Input tokens ({estimatedInputTokens:N0}) exceed role budget ({budget.MaxInputTokens:N0}) for '{roleId}'"
                : !outputOk
                    ? $"Output tokens ({estimatedOutputTokens:N0}) exceed role budget ({budget.MaxOutputTokens:N0}) for '{roleId}'"
                    : $"Daily cost cap (${_dailyCostCapPerWorkspace:F2}) exceeded for this workspace",
            Suggestions = new[]
            {
                !inputOk || !outputOk
                    ? $"Consider using {budget.FallbackModel} for this role"
                    : "Wait until tomorrow or increase daily cost cap",
                "Reduce context size by focusing on the specific task",
            }
        };
    }

    /// <summary>
    /// Records actual cost after a run completes.
    /// </summary>
    public void RecordActualCost(string workspacePath, CostLogEntry entry)
    {
        _costTracker.LogRun(workspacePath, entry);
    }

    /// <summary>
    /// Suggests a fallback model when the primary model is unavailable or over budget.
    /// </summary>
    public string SuggestFallbackModel(string roleId, string? currentModel = null)
    {
        var budget = RoleBudgetConfig.For(roleId);

        // If current model is the premium one and role allows it, stay on it
        if (!string.IsNullOrEmpty(currentModel)
            && budget.AllowPremiumModels
            && !string.IsNullOrEmpty(budget.DefaultModel)
            && currentModel.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            // Downgrade from opus to sonnet
            return "anthropic/claude-3-5-sonnet-20241022";
        }

        return !string.IsNullOrEmpty(budget.FallbackModel)
            ? budget.FallbackModel
            : budget.DefaultModel;
    }

    /// <summary>
    /// Returns a model recommendation for a role based on context size.
    /// </summary>
    public string GetRecommendedModelForContext(string roleId, ContextSizeLevel size)
    {
        var budget = RoleBudgetConfig.For(roleId);

        return size switch
        {
            ContextSizeLevel.Large when budget.AllowPremiumModels => budget.DefaultModel,
            ContextSizeLevel.Large => budget.DefaultModel,
            ContextSizeLevel.Medium => budget.DefaultModel,
            _ => budget.FallbackModel ?? budget.DefaultModel,
        };
    }
}

/// <summary>
/// Result of a token/context estimate.
/// </summary>
public sealed class TokenEstimate
{
    public int EstimatedTokens { get; init; }
    public IReadOnlyList<(string Label, int Chars)> Breakdown { get; init; } = Array.Empty<(string, int)>();
    public bool HasLargeContext { get; init; }
    public ContextSizeLevel ContextSizeLevel { get; init; } = ContextSizeLevel.Normal;
}

/// <summary>
/// Result of a chat fanout estimate.
/// </summary>
public sealed class ChatFanoutEstimate
{
    public int SingleMessageTokens { get; init; }
    public int PerRecipientTokens { get; init; }
    public int TotalTokensWithRecipients { get; init; }
    public int RecipientCount { get; init; }
    public bool RequiresWarning { get; init; }
    public bool RequiresConfirmation { get; init; }
}

/// <summary>
/// Result of a budget check.
/// </summary>
public sealed class BudgetCheckResult
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// How large a context is relative to model limits.
/// </summary>
public enum ContextSizeLevel
{
    /// <summary>Context is within normal limits. No special handling needed.</summary>
    Normal,
    /// <summary>Context is moderately large. Consider a stronger model.</summary>
    Medium,
    /// <summary>Context is very large. Confirmation or truncation recommended.</summary>
    Large,
}
