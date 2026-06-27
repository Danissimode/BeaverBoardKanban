using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

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
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public string? ChatTarget { get; set; }
    public string? RuntimeId { get; set; }
    public string? RoleId { get; set; }
    public string? ModelProfileId { get; set; }
    public string? CommandDisplay { get; set; }
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;
    public DateTime? EndedAt { get; set; }
    public int? ExitCode { get; set; }
    public ExecutionMetadata? ExecutionMetadata { get; set; }

    private readonly object _logLock = new();
    private readonly LinkedList<StreamEvent> _buffer = new();
    private const int MaxBuffer = 500;

    public Channel<string> SteeringQueue { get; } = Channel.CreateUnbounded<string>();
    public CancellationTokenSource Cancellation { get; } = new();
    public bool IsAwaitingUserAnswer { get; set; }
    public event Action<StreamEvent>? OnEvent;

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
        lock (_logLock)
        {
            _buffer.AddLast(ev);
            while (_buffer.Count > MaxBuffer) _buffer.RemoveFirst();
        }
        OnEvent?.Invoke(ev);
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
    public ExecutionMetadata? ExecutionMetadata { get; set; }
}

/// <summary>Persists completed runs as JSON files on disk.</summary>
public sealed class RunLogStore
