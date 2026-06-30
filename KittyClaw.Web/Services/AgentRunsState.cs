using KittyClaw.Core.Automation;

namespace KittyClaw.Web.Services;

/// <summary>
/// Bridges the in-process AgentRunRegistry to Blazor components so the board
/// can display a spinner on tickets with an active run and open the drawer.
/// Also emits toast notifications for run events.
/// </summary>
public sealed class AgentRunsState
{
    private readonly AgentRunRegistry _registry;
    private readonly ToastService? _toastService;
    public event Action? OnChange;

    public AgentRunsState(AgentRunRegistry registry, ToastService? toastService = null)
    {
        _registry = registry;
        _toastService = toastService;
        
        _registry.OnRunStarted += OnRunStarted;
        _registry.OnRunEnded += OnRunEnded;
    }

    private void OnRunStarted(AgentRun run)
    {
        _toastService?.ShowRunStarted(
            run.AgentName,
            run.RunnerKind,
            run.RunId,
            run.TicketId
        );
        OnChange?.Invoke();
    }

    private void OnRunEnded(AgentRun run)
    {
        var duration = run.EndedAt.HasValue 
            ? run.EndedAt.Value - run.StartedAt 
            : TimeSpan.Zero;
            
        switch (run.Status)
        {
            case AgentRunStatus.Completed:
                _toastService?.ShowRunCompleted(
                    run.AgentName,
                    run.RunnerKind,
                    duration,
                    run.RunId,
                    run.TicketId
                );
                break;
            case AgentRunStatus.Failed:
                _toastService?.ShowRunFailed(
                    run.AgentName,
                    run.RunnerKind,
                    "Run failed", // Error details would come from run events
                    run.RunId,
                    run.TicketId
                );
                break;
            case AgentRunStatus.Stopped:
                _toastService?.ShowRunStopped(
                    run.AgentName,
                    run.RunnerKind,
                    run.RunId,
                    run.TicketId
                );
                break;
        }
        OnChange?.Invoke();
    }

    public IEnumerable<AgentRun> ActiveForProject(string slug) => _registry.ActiveForProject(slug);

    public AgentRun? ActiveForTicket(string slug, int ticketId) =>
        _registry.ActiveForTicket(slug, ticketId).FirstOrDefault();

    /// <summary>
    /// Most recent run for a ticket (any status — used to offer a "log" button
    /// after a run has completed so the user can inspect what happened).
    /// </summary>
    public AgentRun? LastForTicket(string slug, int ticketId) =>
        _registry.AllForTicket(slug, ticketId)
            .OrderByDescending(r => r.EndedAt ?? r.StartedAt)
            .FirstOrDefault();

    public AgentRun? Get(string runId) => _registry.Get(runId);

    public IEnumerable<AgentRun> AllForProject(string slug) => _registry.AllForProject(slug);
}
