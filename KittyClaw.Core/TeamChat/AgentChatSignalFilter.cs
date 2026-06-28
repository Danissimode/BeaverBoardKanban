namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Signal types that can be published to team chat.
/// </summary>
public static class AgentChatSignals
{
    // Important signals (published by default)
    public const string RunStarted = "run-started";
    public const string RunCompleted = "run-completed";
    public const string RunFailed = "run-failed";
    public const string NeedsHuman = "needs-human";
    public const string Blocker = "blocker";
    public const string ScopeChange = "scope-change";
    public const string RiskFound = "risk-found";
    public const string ReviewRequested = "review-requested";
    public const string ReviewCompleted = "review-completed";
    public const string TestFailed = "test-failed";
    public const string TestPassed = "test-passed";
    public const string ProviderDegraded = "provider-degraded";
    public const string WorktreeFailed = "worktree-failed";
    public const string PlanReady = "plan-ready";
    public const string PlanApproved = "plan-approved";
    public const string CloseoutBlocked = "closeout-blocked";
    public const string CloseoutReady = "closeout-ready";

    // Noisy signals (suppressed by default)
    public const string StdoutLine = "stdout-line";
    public const string DebugLog = "debug-log";
    public const string ToolCallStart = "tool-call-start";
    public const string ToolCallSuccess = "tool-call-success";
    public const string FileRead = "file-read";
    public const string MinorProgress = "minor-progress";
    public const string InternalReasoning = "internal-reasoning";
    public const string TokenUsageUpdate = "token-usage-update";

    public static readonly HashSet<string> ImportantSignals = new()
    {
        RunStarted, RunCompleted, RunFailed, NeedsHuman, Blocker,
        ScopeChange, RiskFound, ReviewRequested, ReviewCompleted,
        TestFailed, TestPassed, ProviderDegraded, WorktreeFailed,
        PlanReady, PlanApproved, CloseoutBlocked, CloseoutReady
    };

    public static readonly HashSet<string> NoisySignals = new()
    {
        StdoutLine, DebugLog, ToolCallStart, ToolCallSuccess,
        FileRead, MinorProgress, InternalReasoning, TokenUsageUpdate
    };
}

/// <summary>
/// Input signal from a runner/agent.
/// </summary>
public sealed class AgentChatSignal
{
    public string SignalType { get; init; } = "";
    public string AgentId { get; init; } = "";
    public string RoleId { get; init; } = "";
    public string Body { get; init; } = "";
    public int? TicketId { get; init; }
    public string? RunId { get; init; }
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Decision from the signal filter.
/// </summary>
public sealed class AgentChatSignalDecision
{
    public bool ShouldPublish { get; init; }
    public string Reason { get; init; } = "";
    public string? SuppressedBy { get; init; }
}

/// <summary>
/// Filters agent signals before publishing to team chat.
/// Based on agent profile and role policy.
/// </summary>
public interface IAgentChatSignalFilter
{
    AgentChatSignalDecision ShouldPublish(AgentChatSignal signal, AgentChatProfile profile, AgentRoleChatPolicy policy);
}

public class AgentChatSignalFilter : IAgentChatSignalFilter
{
    public AgentChatSignalDecision ShouldPublish(AgentChatSignal signal, AgentChatProfile profile, AgentRoleChatPolicy policy)
    {
        // If agent is silent, suppress everything
        if (profile.SignalPolicy == "silent")
        {
            return new AgentChatSignalDecision
            {
                ShouldPublish = false,
                Reason = "Agent signal policy is silent",
                SuppressedBy = "signal-policy"
            };
        }

        // If signal is in role's must-report list, always publish
        if (policy.MustReportEvents.Contains(signal.SignalType))
        {
            return new AgentChatSignalDecision { ShouldPublish = true, Reason = "Must-report event" };
        }

        // If signal is in role's suppress list, suppress (unless verbose)
        if (policy.ShouldSuppressEvents.Contains(signal.SignalType) && profile.SignalPolicy != "verbose")
        {
            return new AgentChatSignalDecision
            {
                ShouldPublish = false,
                Reason = "Signal suppressed by role policy",
                SuppressedBy = "role-policy"
            };
        }

        // If signal is in global noisy list and policy is not verbose, suppress
        if (AgentChatSignals.NoisySignals.Contains(signal.SignalType) && profile.SignalPolicy != "verbose")
        {
            return new AgentChatSignalDecision
            {
                ShouldPublish = false,
                Reason = "Noisy signal suppressed",
                SuppressedBy = "global-noisy"
            };
        }

        // If signal is important, publish
        if (AgentChatSignals.ImportantSignals.Contains(signal.SignalType))
        {
            return new AgentChatSignalDecision { ShouldPublish = true, Reason = "Important signal" };
        }

        // Default: publish if verbose, suppress otherwise
        if (profile.SignalPolicy == "verbose")
        {
            return new AgentChatSignalDecision { ShouldPublish = true, Reason = "Verbose mode" };
        }

        return new AgentChatSignalDecision
        {
            ShouldPublish = false,
            Reason = "Default: not important enough",
            SuppressedBy = "default"
        };
    }
}
