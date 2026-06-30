using System.Collections.Concurrent;

namespace KittyClaw.Web.Services;

/// <summary>
/// Toast notification service for displaying temporary notifications to the user.
/// </summary>
public sealed class ToastService
{
    private readonly ConcurrentDictionary<string, Toast> _toasts = new();
    public event Action<Toast>? OnToast;
    public event Action<string>? OnDismiss;

    public void Show(string title, string body, ToastKind kind = ToastKind.Info, string? runId = null, int? ticketId = null)
    {
        var toast = new Toast(
            Id: Guid.NewGuid().ToString("N")[..8],
            Title: title,
            Body: body,
            Kind: kind,
            RunId: runId,
            TicketId: ticketId,
            CreatedAt: DateTime.UtcNow
        );
        
        _toasts[toast.Id] = toast;
        OnToast?.Invoke(toast);
        
        // Auto-dismiss after 8 seconds for non-errors, 15 seconds for errors
        var delay = kind == ToastKind.Error ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(8);
        _ = DismissAfterAsync(toast.Id, delay);
    }

    public void ShowRunStarted(string agentName, string runnerKind, string? runId = null, int? ticketId = null)
    {
        Show(
            "Run Started",
            $"{GetRunnerIcon(runnerKind)} {agentName} ({runnerKind}) started",
            ToastKind.Info,
            runId,
            ticketId
        );
    }

    public void ShowRunCompleted(string agentName, string runnerKind, TimeSpan duration, string? runId = null, int? ticketId = null)
    {
        var durationStr = duration.TotalMinutes >= 1 
            ? $"{(int)duration.TotalMinutes}m{duration.Seconds:D2}s" 
            : $"{duration.TotalSeconds:F1}s";
            
        Show(
            "Run Completed",
            $"{GetRunnerIcon(runnerKind)} {agentName} finished in {durationStr}",
            ToastKind.Success,
            runId,
            ticketId
        );
    }

    public void ShowRunFailed(string agentName, string runnerKind, string error, string? runId = null, int? ticketId = null)
    {
        Show(
            "Run Failed",
            $"{GetRunnerIcon(runnerKind)} {agentName} failed: {Truncate(error, 80)}",
            ToastKind.Error,
            runId,
            ticketId
        );
    }

    public void ShowRunStopped(string agentName, string runnerKind, string? runId = null, int? ticketId = null)
    {
        Show(
            "Run Stopped",
            $"{GetRunnerIcon(runnerKind)} {agentName} was stopped",
            ToastKind.Warning,
            runId,
            ticketId
        );
    }

    public void Dismiss(string id)
    {
        if (_toasts.TryRemove(id, out _))
        {
            OnDismiss?.Invoke(id);
        }
    }

    public IReadOnlyCollection<Toast> GetActiveToasts() => _toasts.Values.ToList();

    private async Task DismissAfterAsync(string id, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            Dismiss(id);
        }
        catch { /* Task was cancelled */ }
    }

    private static string GetRunnerIcon(string kind) => kind.ToLowerInvariant() switch
    {
        "opencode" => "🦫",
        "claude" => "🤖",
        "cao" => "🎛️",
        "team" => "👥",
        "manual" => "⚪",
        _ => "⚙️"
    };

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}

public sealed record Toast(
    string Id,
    string Title,
    string Body,
    ToastKind Kind,
    string? RunId = null,
    int? TicketId = null,
    DateTime CreatedAt = default
);

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}
