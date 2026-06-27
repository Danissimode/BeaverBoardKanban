using System.Threading;
using System.Threading.Tasks;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// Interface for AI providers that can execute agent runs.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "opencode", "claude", "openrouter")
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Human-readable display name for this provider
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Whether this provider is available and configured
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Execute an agent run with the specified configuration
    /// </summary>
    Task<AIProviderResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken);
    
    /// <summary>
    /// Stop an ongoing run
    /// </summary>
    Task<bool> StopAsync(string runId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get the status of a run
    /// </summary>
    Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>
/// Request model for AI provider execution
/// </summary>
public sealed class AIProviderRequest
{
    public required string ProjectSlug { get; init; }
    public required string WorkspacePath { get; init; }
    public required string AgentName { get; init; }
    public required string SkillFile { get; init; }
    public int? TicketId { get; init; }
    public string? TicketTitle { get; init; }
    public string? TicketStatus { get; init; }
    public string? Model { get; init; }
    public string? Profile { get; init; }
    public string? ExtraContext { get; init; }
    public int MaxTurns { get; init; } = 200;
    public string ConcurrencyGroup { get; init; } = "";
    public System.Collections.Generic.IDictionary<string, string> Env { get; init; } = new System.Collections.Generic.Dictionary<string, string>();
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.LegacyClaude;
    public string? SessionScope { get; init; }
    public bool PersistSession { get; init; } = true;
    public System.Action<StreamEvent>? OnEventHook { get; init; }
    public string? RunId { get; init; }
}

/// <summary>
/// Result model from AI provider execution
/// </summary>
public sealed class AIProviderResult
{
    public required AgentRunStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string? Stdout { get; init; }
    public string? Stderr { get; init; }
    public System.DateTimeOffset StartedAt { get; init; }
    public System.DateTimeOffset FinishedAt { get; init; }
    public System.TimeSpan Duration { get; init; }
    public string ProviderId { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string? RunId { get; init; }
    public string CommandDisplay { get; init; } = string.Empty;
    public string[] Artifacts { get; init; } = System.Array.Empty<string>();
}

/// <summary>
/// Status model for AI provider runs
/// </summary>
public sealed class AIProviderStatus
{
    public required string RunId { get; init; }
    public required AgentRunStatus Status { get; init; }
    public string? SessionId { get; init; }
    public string? Model { get; init; }
    public string? ProviderId { get; init; }
    public System.DateTimeOffset? StartedAt { get; init; }
    public System.DateTimeOffset? FinishedAt { get; init; }
}
