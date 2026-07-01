using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using KittyClaw.Core.Automation.Runners;

namespace KittyClaw.Core.Automation;

public enum AgentRunStatus { Running, Completed, Failed, Stopped }

public sealed class AgentRun
{
    public required string RunId { get; init; }
    public required string ProjectSlug { get; init; }
    public required int? TicketId { get; init; }
    public required string AgentName { get; init; }
    public required string SkillFile { get; init; }
    public required string ConcurrencyGroup { get; init; }
    public required DateTime StartedAt { get; init; }
    
    /// <summary>
    /// Which runner is executing this run (e.g., "claude", "opencode").
    /// Set by the runner when it registers the run.
    /// </summary>
    public string RunnerKind { get; set; } = "claude";
    
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public string? ChatTarget { get; set; }
    public string? RuntimeId { get; set; }
    public string? RoleId { get; set; }
    public string? ModelProfileId { get; set; }
    public string? CommandDisplay { get; set; }
    
    /// <summary>
    /// Extended execution metadata (provider, model, worktree, etc.) set by the runner.
    /// Null for runs that predate this field.
    /// </summary>
    public ExecutionMetadata? ExecutionMetadata { get; set; }
    
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;
    public DateTime? EndedAt { get; set; }
    public int? ExitCode { get; set; }

    private readonly object _logLock = new();
    private readonly LinkedList<StreamEvent> _buffer = new();
    private const int MaxBuffer = 500;

    public Channel<string> SteeringQueue { get; } = Channel.CreateUnbounded<string>();
    public CancellationTokenSource Cancellation { get; } = new();
    public bool IsAwaitingUserAnswer { get; set; }

    private event Action<StreamEvent>? _onEvent;
    public event Action<StreamEvent>? OnEvent
    {
        add { lock (_logLock) _onEvent += value; }
        remove { lock (_logLock) _onEvent -= value; }
    }

    private readonly List<string> _pendingSteerMessages = new();
    public IReadOnlyList<string> PendingSteerMessages => _pendingSteerMessages;

    public void AddPendingSteerMessage(string msg)
    {
        lock (_logLock) _pendingSteerMessages.Add(msg);
    }

    public IReadOnlyList<string> DrainPendingSteerMessages()
    {
        lock (_logLock)
        {
            var result = _pendingSteerMessages.ToList();
            _pendingSteerMessages.Clear();
            return result;
        }
    }

    public IReadOnlyList<StreamEvent> SnapshotBuffer()
    {
        lock (_logLock) return _buffer.ToList();
    }

    public void Push(StreamEvent ev)
    {
        Action<StreamEvent>? handler;
        lock (_logLock)
        {
            _buffer.AddLast(ev);
            while (_buffer.Count > MaxBuffer) _buffer.RemoveFirst();
            handler = _onEvent;
        }
        handler?.Invoke(ev);
    }

    /// <summary>Appends an event to the buffer without invoking subscribers. Used during
    /// deserialization so restored runs don't re-fire events into the live UI.</summary>
    internal void PushWithoutEvent(StreamEvent ev)
    {
        lock (_logLock)
        {
            _buffer.AddLast(ev);
            while (_buffer.Count > MaxBuffer) _buffer.RemoveFirst();
        }
    }
}

public sealed record StreamEvent(DateTime At, string Kind, string Text, string? Detail = null);

/// <summary>Serializable snapshot of a completed AgentRun for disk persistence.</summary>
public sealed class AgentRunSnapshot
{
    public string RunId { get; set; } = "";
    public string ProjectSlug { get; set; } = "";
    public int? TicketId { get; set; }
    public string AgentName { get; set; } = "";
    public string SkillFile { get; set; } = "";
    public string ConcurrencyGroup { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public string RunnerKind { get; set; } = "claude";
    public DateTime? EndedAt { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public string? RuntimeId { get; set; }
    public string? RoleId { get; set; }
    public string? ModelProfileId { get; set; }
    public string? CommandDisplay { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentRunStatus Status { get; set; }
    public int? ExitCode { get; set; }
    public List<StreamEvent> Events { get; set; } = [];
    public List<string> PendingSteerMessages { get; set; } = [];
    
    /// <summary>
    /// Extended execution metadata set by the runner.
    /// </summary>
    public ExecutionMetadata? ExecutionMetadata { get; set; }
}

/// <summary>Persists completed runs as JSON files on disk.</summary>
public sealed class RunLogStore
{
    private readonly string _dir;
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public RunLogStore(string dataDir)
    {
        _dir = Path.Combine(dataDir, "runs");
        Directory.CreateDirectory(_dir);
    }

    public void Save(AgentRun run)
    {
        var snapshot = new AgentRunSnapshot
        {
            RunId = run.RunId,
            ProjectSlug = run.ProjectSlug,
            TicketId = run.TicketId,
            AgentName = run.AgentName,
            SkillFile = run.SkillFile,
            ConcurrencyGroup = run.ConcurrencyGroup,
            StartedAt = run.StartedAt,
            RunnerKind = run.RunnerKind,
            EndedAt = run.EndedAt,
            SessionId = run.SessionId,
            Model = run.Model,
            RuntimeId = run.RuntimeId,
            RoleId = run.RoleId,
            ModelProfileId = run.ModelProfileId,
            CommandDisplay = run.CommandDisplay,
            Status = run.Status,
            ExitCode = run.ExitCode,
            Events = run.SnapshotBuffer().ToList(),
            PendingSteerMessages = run.PendingSteerMessages.ToList(),
            ExecutionMetadata = run.ExecutionMetadata,
        };
        var path = Path.Combine(_dir, $"{run.RunId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, s_json));
    }

    public void Delete(string runId)
    {
        var path = Path.Combine(_dir, $"{runId}.json");
        if (File.Exists(path)) File.Delete(path);
    }

    public IEnumerable<AgentRun> LoadAll()
    {
        if (!Directory.Exists(_dir)) yield break;
        foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
        {
            AgentRunSnapshot? snapshot;
            try
            {
                var json = File.ReadAllText(file);
                snapshot = JsonSerializer.Deserialize<AgentRunSnapshot>(json, s_json);
            }
            catch (Exception) { /* skip corrupted run log */ continue; }
            if (snapshot is null) continue;

            var run = new AgentRun
            {
                RunId = snapshot.RunId,
                ProjectSlug = snapshot.ProjectSlug,
                TicketId = snapshot.TicketId,
                AgentName = snapshot.AgentName,
                SkillFile = snapshot.SkillFile,
                ConcurrencyGroup = snapshot.ConcurrencyGroup,
                StartedAt = snapshot.StartedAt,
            };
            run.RunnerKind = snapshot.RunnerKind;
            run.SessionId = snapshot.SessionId;
            run.Model = snapshot.Model;
            run.RuntimeId = snapshot.RuntimeId;
            run.RoleId = snapshot.RoleId;
            run.ModelProfileId = snapshot.ModelProfileId;
            run.CommandDisplay = snapshot.CommandDisplay;
            run.Status = snapshot.Status;
            run.EndedAt = snapshot.EndedAt;
            run.ExitCode = snapshot.ExitCode;
            foreach (var ev in snapshot.Events)
                run.PushWithoutEvent(ev);
            foreach (var msg in snapshot.PendingSteerMessages)
                run.AddPendingSteerMessage(msg);
            run.ExecutionMetadata = snapshot.ExecutionMetadata;
            yield return run;
        }
    }
}

public sealed class AgentRunRegistry
{
    private readonly ConcurrentDictionary<string, AgentRun> _runs = new();
    private readonly RunLogStore? _store;

    public event Action<AgentRun>? OnRunStarted;
    public event Action<AgentRun>? OnRunEnded;

    public AgentRunRegistry() { }

    public AgentRunRegistry(RunLogStore store)
    {
        _store = store;
        foreach (var run in store.LoadAll())
        {
            // A live run is never persisted — any snapshot with Status=Running is stale
            // (process exited before Complete was called). Reconcile to Stopped so the UI
            // never shows permanently-Running runs after a restart.
            if (run.Status == AgentRunStatus.Running)
            {
                run.Status = AgentRunStatus.Stopped;
                run.EndedAt = DateTime.UtcNow;
                store.Save(run);
            }
            _runs[run.RunId] = run;
        }
    }

    public AgentRun Register(AgentRun run)
    {
        _runs[run.RunId] = run;
        OnRunStarted?.Invoke(run);
        return run;
    }

    public void Complete(string runId, AgentRunStatus status, int? exitCode)
    {
        if (!_runs.TryGetValue(runId, out var run)) return;
        // Idempotent: a terminal status must never be downgraded by a stray second call.
        if (run.Status != AgentRunStatus.Running) return;
        run.Status = status;
        run.EndedAt = DateTime.UtcNow;
        run.ExitCode = exitCode;
        _store?.Save(run);
        OnRunEnded?.Invoke(run);
    }

    public AgentRun? Get(string runId) => _runs.TryGetValue(runId, out var r) ? r : null;

    public IEnumerable<AgentRun> ActiveForProject(string projectSlug) =>
        _runs.Values.Where(r => r.ProjectSlug == projectSlug && r.Status == AgentRunStatus.Running);

    public IEnumerable<AgentRun> ActiveForTicket(string projectSlug, int ticketId) =>
        _runs.Values.Where(r => r.ProjectSlug == projectSlug && r.TicketId == ticketId && r.Status == AgentRunStatus.Running);

    public IEnumerable<AgentRun> AllForTicket(string projectSlug, int ticketId) =>
        _runs.Values.Where(r => r.ProjectSlug == projectSlug && r.TicketId == ticketId);

    public IEnumerable<AgentRun> AllForProject(string projectSlug) =>
        _runs.Values.Where(r => r.ProjectSlug == projectSlug);
    
    /// <summary>
    /// Returns all runs currently in memory (including completed runs loaded from disk on startup).
    /// </summary>
    public IEnumerable<AgentRun> GetAllRuns() => _runs.Values;

    public bool HasActiveInGroup(string projectSlug, string concurrencyGroup) =>
        _runs.Values.Any(r => r.ProjectSlug == projectSlug && r.ConcurrencyGroup == concurrencyGroup && r.Status == AgentRunStatus.Running);

    public bool HasActiveAny(string projectSlug, IEnumerable<string> concurrencyGroups)
    {
        var set = new HashSet<string>(concurrencyGroups);
        return _runs.Values.Any(r => r.ProjectSlug == projectSlug && set.Contains(r.ConcurrencyGroup) && r.Status == AgentRunStatus.Running);
    }

    public void Remove(string runId) => _runs.TryRemove(runId, out _);

    public AgentRun? LastCompletedForChatTarget(string projectSlug, string chatTarget) =>
        _runs.Values
            .Where(r => r.ProjectSlug == projectSlug && r.ChatTarget == chatTarget && r.Status != AgentRunStatus.Running && r.EndedAt is not null)
            .MaxBy(r => r.EndedAt);

    /// <summary>Purge runs that ended more than N minutes ago.</summary>
    public void PurgeOld(TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        foreach (var r in _runs.Values.Where(r => r.Status != AgentRunStatus.Running && r.EndedAt is not null && r.EndedAt < cutoff).ToList())
        {
            _runs.TryRemove(r.RunId, out _);
            _store?.Delete(r.RunId);
        }
    }
}
