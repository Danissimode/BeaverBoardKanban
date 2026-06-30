using KittyClaw.Core.Automation;
using KittyClaw.Core.TeamChat;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Subscribes to <see cref="AgentRunRegistry.OnRunStarted"/> and <see cref="AgentRunRegistry.OnRunEnded"/>
/// and posts system events into the project's TeamChat via <see cref="ITeamChatService"/>.
///
/// Posts structured activity messages when:
/// - A run starts: "🚀 @{agent} started on ticket #{id}"
/// - A run completes: "✅ @{agent} completed on ticket #{id} ({duration})"
/// - A run fails: "❌ @{agent} failed on ticket #{id}"
/// - A run is stopped: "🛑 @{agent} stopped on ticket #{id}"
///
/// This gives users a live feed of what agents are doing without opening the agent drawer.
/// </summary>
public sealed class TeamChatRunNotifier : IDisposable
{
    private readonly AgentRunRegistry _runRegistry;
    private readonly ITeamChatService _chat;
    private readonly ILogger<TeamChatRunNotifier>? _logger;

    public TeamChatRunNotifier(
        AgentRunRegistry runRegistry,
        ITeamChatService chat,
        ILogger<TeamChatRunNotifier>? logger = null)
    {
        _runRegistry = runRegistry;
        _chat = chat;
        _logger = logger;

        _runRegistry.OnRunStarted += HandleRunStarted;
        _runRegistry.OnRunEnded += HandleRunEnded;
    }

    private void HandleRunStarted(AgentRun run)
    {
        if (string.IsNullOrEmpty(run.ProjectSlug)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var agent = run.AgentName ?? "agent";
                var ticketRef = run.TicketId is int tid ? $" on ticket #{tid}" : "";
                var body = $"🚀 **@{agent}** started{ticketRef}";
                
                await _chat.AddSystemEventAsync(run.ProjectSlug, new SystemEventRequest(
                    Body: body,
                    AuthorId: "system",
                    MessageType: "ai-activity",
                    TicketId: run.TicketId,
                    RunId: run.RunId,
                    MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        kind = "run_started",
                        agent = agent,
                        runnerKind = run.RunnerKind
                    })
                ));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to post run-started event for {RunId}", run.RunId);
            }
        });
    }

    private void HandleRunEnded(AgentRun run)
    {
        if (string.IsNullOrEmpty(run.ProjectSlug)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var agent = run.AgentName ?? "agent";
                var ticketRef = run.TicketId is int tid ? $" on ticket #{tid}" : "";
                var duration = run.EndedAt.HasValue
                    ? $" in {(run.EndedAt.Value - run.StartedAt).TotalMinutes:F0}m"
                    : "";

                var (emoji, messageType) = run.Status switch
                {
                    AgentRunStatus.Completed => ("✅", "ai-activity"),
                    AgentRunStatus.Failed => ("❌", "ai-activity"),
                    AgentRunStatus.Stopped => ("🛑", "ai-activity"),
                    _ => ("ℹ️", "status")
                };

                var body = $"{emoji} **@{agent}** {run.Status.ToString().ToLowerInvariant()}{ticketRef}{duration}";

                var elapsedMs = run.EndedAt.HasValue
                    ? (run.EndedAt.Value - run.StartedAt).TotalMilliseconds
                    : 0.0;

                await _chat.AddSystemEventAsync(run.ProjectSlug, new SystemEventRequest(
                    Body: body,
                    AuthorId: "system",
                    MessageType: messageType,
                    TicketId: run.TicketId,
                    RunId: run.RunId,
                    MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        kind = run.Status switch
                        {
                            AgentRunStatus.Completed => "run_completed",
                            AgentRunStatus.Failed => "run_failed",
                            AgentRunStatus.Stopped => "run_stopped",
                            _ => "run_ended"
                        },
                        agent,
                        durationMs = elapsedMs,
                        exitCode = run.ExitCode
                    })
                ));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to post run-ended event for {RunId}", run.RunId);
            }
        });
    }

    public void Dispose()
    {
        _runRegistry.OnRunStarted -= HandleRunStarted;
        _runRegistry.OnRunEnded -= HandleRunEnded;
    }
}
