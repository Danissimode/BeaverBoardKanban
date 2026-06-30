using KittyClaw.Core.Automation.Runners;
using System.Collections.Generic;

namespace KittyClaw.Web.Services;

/// <summary>
/// Provides runner availability state for the Blazor UI.
/// Uses RunnerRegistry and RunnerAvailabilityChecker directly via DI.
/// </summary>
public sealed class RunnerStatusState
{
    private readonly RunnerRegistry _registry;
    private readonly RunnerAvailabilityChecker _availabilityChecker;
    private List<RunnerStatusInfo> _runners = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    
    public event Action? OnChange;

    public RunnerStatusState(RunnerRegistry registry, RunnerAvailabilityChecker availabilityChecker)
    {
        _registry = registry;
        _availabilityChecker = availabilityChecker;
    }

    public IReadOnlyList<RunnerStatusInfo> Runners => _runners;

    public async Task RefreshAsync(bool force = false)
    {
        // Throttle refreshes
        if (!force && DateTime.UtcNow - _lastRefresh < RefreshInterval)
            return;

        try
        {
            var reports = await _availabilityChecker.CheckAllAsync();
            _runners = reports.Select(h => new RunnerStatusInfo(
                h.RunnerKind,
                h.DisplayName,
                h.IsAvailable,
                h.Version,
                h.Error
            )).ToList();
            _lastRefresh = DateTime.UtcNow;
            OnChange?.Invoke();
        }
        catch
        {
            // Fallback: use registry directly
            _runners = _registry.GetAllRunners()
                .Select(r => new RunnerStatusInfo(
                    r.Kind,
                    r.DisplayName,
                    r.IsAvailable,
                    null,
                    null
                )).ToList();
            _lastRefresh = DateTime.UtcNow;
            OnChange?.Invoke();
        }
    }
}

public sealed record RunnerStatusInfo(
    string Kind,
    string DisplayName,
    bool IsAvailable,
    string? Version,
    string? Error
);
