using KittyClaw.Core.Automation;
using KittyClaw.Core.Services;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Automatically updates ticket status when a run starts or ends.
/// Mapping:
/// - Run started → "In Progress" (if column exists)
/// - Run completed → "Review" (if column exists), else "In Review"
/// - Run failed → "Failed" (if column exists)
/// - Run stopped → no auto-move (user intentionally stopped)
/// </summary>
public sealed class AutoTicketStatusUpdater : IDisposable
{
    private readonly AgentRunRegistry _runRegistry;
    private readonly TicketService _ticketService;
    private readonly ColumnService _columnService;
    private readonly ILogger<AutoTicketStatusUpdater>? _logger;

    // Status targets tried in order of preference
    private static readonly string[] InProgressTargets = { "In Progress", "InProgress", "Doing" };
    private static readonly string[] ReviewTargets = { "Review", "In Review", "InReview" };
    private static readonly string[] FailedTargets = { "Failed", "Fail", "Blocked" };

    public AutoTicketStatusUpdater(
        AgentRunRegistry runRegistry,
        TicketService ticketService,
        ColumnService columnService,
        ILogger<AutoTicketStatusUpdater>? logger = null)
    {
        _runRegistry = runRegistry;
        _ticketService = ticketService;
        _columnService = columnService;
        _logger = logger;

        _runRegistry.OnRunStarted += HandleRunStarted;
        _runRegistry.OnRunEnded += HandleRunEnded;
    }

    private void HandleRunStarted(AgentRun run)
    {
        if (string.IsNullOrEmpty(run.ProjectSlug) || run.TicketId is null)
            return;

        // Skip chat runs — they are conversations, not work execution
        if (!string.IsNullOrEmpty(run.ChatTarget))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var target = await FindExistingColumnAsync(run.ProjectSlug, InProgressTargets);
                if (target is null)
                {
                    _logger?.LogDebug("No 'In Progress' column found for project {Slug}; skipping auto-move on run start", run.ProjectSlug);
                    return;
                }

                // Only move if not already in a progress-like state
                var ticket = await _ticketService.GetTicketAsync(run.ProjectSlug, run.TicketId.Value);
                if (ticket is null) return;
                if (IsProgressLike(ticket.Status)) return;

                await _ticketService.MoveTicketAsync(run.ProjectSlug, run.TicketId.Value, target, "system");
                _logger?.LogInformation("Auto-moved ticket #{TicketId} to '{Target}' on run start", run.TicketId.Value, target);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-move ticket #{TicketId} on run start", run.TicketId);
            }
        });
    }

    private void HandleRunEnded(AgentRun run)
    {
        if (string.IsNullOrEmpty(run.ProjectSlug) || run.TicketId is null)
            return;

        // Skip chat runs
        if (!string.IsNullOrEmpty(run.ChatTarget))
            return;

        // Stopped = user intentionally stopped, don't auto-move
        if (run.Status == AgentRunStatus.Stopped)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                string[]? targets = run.Status switch
                {
                    AgentRunStatus.Completed => ReviewTargets,
                    AgentRunStatus.Failed => FailedTargets,
                    _ => null
                };

                if (targets is null) return;

                var target = await FindExistingColumnAsync(run.ProjectSlug, targets);
                if (target is null)
                {
                    _logger?.LogDebug("No target column found for project {Slug} (status={Status}); skipping auto-move", run.ProjectSlug, run.Status);
                    return;
                }

                // Only move if not already in target state
                var ticket = await _ticketService.GetTicketAsync(run.ProjectSlug, run.TicketId.Value);
                if (ticket is null) return;
                if (string.Equals(ticket.Status, target, StringComparison.OrdinalIgnoreCase)) return;

                await _ticketService.MoveTicketAsync(run.ProjectSlug, run.TicketId.Value, target, "system");
                _logger?.LogInformation("Auto-moved ticket #{TicketId} to '{Target}' on run {Status}", run.TicketId.Value, target, run.Status);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to auto-move ticket #{TicketId} on run end (status={Status})", run.TicketId, run.Status);
            }
        });
    }

    /// <summary>
    /// Finds the first column name from the candidates that actually exists in the project.
    /// </summary>
    private async Task<string?> FindExistingColumnAsync(string projectSlug, string[] candidates)
    {
        try
        {
            var columns = await _columnService.ListColumnsAsync(projectSlug);
            var existing = columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (existing.Contains(candidate))
                    return candidate;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to list columns for project {Slug}", projectSlug);
        }
        return null;
    }

    private static bool IsProgressLike(string? status) =>
        status is not null && (
            string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Doing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _runRegistry.OnRunStarted -= HandleRunStarted;
        _runRegistry.OnRunEnded -= HandleRunEnded;
    }
}
