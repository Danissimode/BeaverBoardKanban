using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Subscribes to <see cref="AgentRunRegistry.OnRunEnded"/> and persists
/// <see cref="TicketExecutionMetadata"/> to <see cref="ITicketExecutionMetadataStore"/>
/// for every completed run that has a TicketId.
///
/// This ensures the Run tab in the card drawer shows correct metadata
/// (provider, model, worktree, etc.) even after an app restart,
/// regardless of whether the run was triggered by automation or ad-hoc from the drawer.
///
/// Also back-fills metadata for any runs loaded from <see cref="RunLogStore"/>
/// on startup that don't yet have a corresponding entry in the metadata store.
/// </summary>
public sealed class TicketExecutionPersistenceService : IHostedService, IDisposable
{
    private readonly AgentRunRegistry _runRegistry;
    private readonly ITicketExecutionMetadataStore _store;
    private readonly ILogger<TicketExecutionPersistenceService>? _logger;
    private CancellationTokenSource? _cts;

    public TicketExecutionPersistenceService(
        AgentRunRegistry runRegistry,
        ITicketExecutionMetadataStore store,
        ILogger<TicketExecutionPersistenceService>? logger = null)
    {
        _runRegistry = runRegistry;
        _store = store;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runRegistry.OnRunEnded += OnRunEnded;
        
        // Back-fill any runs that were loaded from RunLogStore but never had
        // their metadata saved (e.g., runs from before this service existed).
        _ = BackfillMissingMetadataAsync(_cts.Token);
        
        _logger?.LogInformation("TicketExecutionPersistenceService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _runRegistry.OnRunEnded -= OnRunEnded;
        _logger?.LogInformation("TicketExecutionPersistenceService stopped");
        return Task.CompletedTask;
    }

    private void OnRunEnded(AgentRun run)
    {
        if (run.TicketId is null) return;
        
        var ct = _cts?.Token ?? CancellationToken.None;
        _ = SaveMetadataAsync(run, ct);
    }

    private async Task SaveMetadataAsync(AgentRun run, CancellationToken ct)
    {
        try
        {
            var em = run.ExecutionMetadata;
            var metadata = new TicketExecutionMetadata
            {
                ProjectSlug = run.ProjectSlug,
                TicketId = run.TicketId.Value,
                RunId = run.RunId,
                ExecutionMode = em?.Mode ?? "LegacyClaude",
                RunnerKind = run.RunnerKind,
                Provider = em?.Provider,
                Model = em?.Model ?? run.Model,
                Profile = em?.Profile,
                OpenCodeAgent = em?.OpenCodeAgent,
                SessionId = em?.SessionId ?? run.SessionId,
                WorktreePath = em?.WorktreePath,
                BranchName = em?.BranchName,
                Status = run.Status,
                StartedAt = new DateTimeOffset(run.StartedAt),
                FinishedAt = run.EndedAt.HasValue ? new DateTimeOffset(run.EndedAt.Value) : null,
                LastError = em?.LastError,
            };

            await _store.SaveAsync(metadata, ct);
            _logger?.LogDebug(
                "Saved TicketExecutionMetadata for run {RunId}, ticket {TicketId}",
                run.RunId, run.TicketId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Failed to save TicketExecutionMetadata for run {RunId}", run.RunId);
        }
    }

    private async Task BackfillMissingMetadataAsync(CancellationToken ct)
    {
        try
        {
            // Check all runs currently in the registry
            foreach (var run in _runRegistry.GetAllRuns())
            {
                if (ct.IsCancellationRequested) break;
                if (run.TicketId is null) continue;
                
                var existing = await _store.GetAsync(run.ProjectSlug, run.TicketId.Value, ct);
                if (existing is not null) continue; // already have metadata
                
                // Only back-fill completed runs
                if (run.Status is AgentRunStatus.Running) continue;
                
                await SaveMetadataAsync(run, ct);
                _logger?.LogInformation(
                    "Back-filled TicketExecutionMetadata for run {RunId} (completed {Status})",
                    run.RunId, run.Status);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during TicketExecutionMetadata back-fill");
        }
    }

    public void Dispose() => _cts?.Dispose();
}
