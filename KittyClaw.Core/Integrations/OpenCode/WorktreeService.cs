using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Integrations.OpenCode;

/// <summary>
/// Default implementation of IWorktreeService.
/// Handles creation and management of per-ticket worktrees.
/// </summary>
public sealed class WorktreeService : IWorktreeService
{
    private readonly ILogger<WorktreeService>? _logger;
    private readonly WorktreeConfig _config;
    
    public WorktreeService(ILogger<WorktreeService>? logger = null, WorktreeConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new WorktreeConfig();
    }
    
    public async Task<WorktreeInfo> EnsureForTicketAsync(TicketExecutionContext context, CancellationToken cancellationToken = default)
    {
        var worktreeRoot = GetWorktreeRoot(context.ProjectSlug, context.WorkspacePath);
        var ticketSlug = GetTicketSlug(context.TicketId);
        var branchName = GetBranchName(context, ticketSlug);
        var worktreePath = Path.Combine(worktreeRoot, ticketSlug);
        
        // Check if worktree already exists
        var existingWorktree = await GetForTicketAsync(context.ProjectSlug, context.TicketId, cancellationToken);
        if (existingWorktree is not null && existingWorktree.Exists)
        {
            return existingWorktree with { LastUsedAt = DateTimeOffset.UtcNow };
        }
        
        // Create worktree directory
        try
        {
            if (!Directory.Exists(worktreeRoot))
            {
                Directory.CreateDirectory(worktreeRoot);
                _logger?.LogInformation("Created worktree root: {WorktreeRoot}", worktreeRoot);
            }
            
            if (!Directory.Exists(worktreePath))
            {
                Directory.CreateDirectory(worktreePath);
                _logger?.LogInformation("Created worktree: {WorktreePath}", worktreePath);
            }
            
            return new WorktreeInfo
            {
                ProjectSlug = context.ProjectSlug,
                TicketId = context.TicketId,
                WorktreePath = worktreePath,
                BranchName = branchName,
                RootPath = worktreeRoot,
                Exists = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create worktree for ticket {TicketId}", context.TicketId);
            throw;
        }
    }
    
    public async Task<WorktreeInfo?> GetForTicketAsync(string projectSlug, int ticketId, CancellationToken cancellationToken = default)
    {
        // For now, just check if the worktree directory exists
        var worktreeRoot = GetWorktreeRoot(projectSlug, ""); // We don't have workspace path here
        var ticketSlug = GetTicketSlug(ticketId);
        var worktreePath = Path.Combine(worktreeRoot, ticketSlug);
        
        if (Directory.Exists(worktreePath))
        {
            return new WorktreeInfo
            {
                ProjectSlug = projectSlug,
                TicketId = ticketId,
                WorktreePath = worktreePath,
                BranchName = GetBranchName(new TicketExecutionContext
                {
                    ProjectSlug = projectSlug,
                    WorkspacePath = "",
                    TicketId = ticketId
                }, ticketSlug),
                RootPath = worktreeRoot,
                Exists = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
        
        return null;
    }
    
    public async Task<WorktreeMergeResult> MergeAsync(string projectSlug, int ticketId, WorktreeMergeOptions options, CancellationToken cancellationToken = default)
    {
        // TODO: Implement worktree merge
        _logger?.LogWarning("Worktree merge not yet implemented");
        return new WorktreeMergeResult
        {
            Success = false,
            ErrorMessage = "Not implemented yet",
            WorktreeDeleted = false
        };
    }
    
    public async Task<WorktreeCleanupResult> CleanupAsync(string projectSlug, int ticketId, CancellationToken cancellationToken = default)
    {
        // TODO: Implement worktree cleanup
        _logger?.LogWarning("Worktree cleanup not yet implemented");
        return new WorktreeCleanupResult
        {
            Success = false,
            ErrorMessage = "Not implemented yet",
            WorktreeDeleted = false,
            BranchDeleted = false
        };
    }
    
    public string GetWorktreeRoot(string projectSlug, string workspacePath)
    {
        // Use configured worktree root or default to .worktrees in workspace
        if (!string.IsNullOrEmpty(_config.WorktreeRoot))
        {
            return Path.Combine(_config.WorktreeRoot, projectSlug);
        }
        
        return Path.Combine(workspacePath, ".worktrees", projectSlug);
    }
    
    private static string GetTicketSlug(int ticketId)
    {
        return $"KC-{ticketId}";
    }
    
    private static string GetBranchName(TicketExecutionContext context, string ticketSlug)
    {
        // Format: kc/KC-42 or kc/KC-42-fix-auth
        var baseName = $"kc/{ticketSlug}";
        
        if (!string.IsNullOrEmpty(context.TicketTitle))
        {
            // Create slug from title
            var titleSlug = context.TicketTitle
                .ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace("..", ".")
                .Replace(".", "")
                .Trim('-', '.');
            
            if (!string.IsNullOrEmpty(titleSlug))
            {
                return $"{baseName}-{titleSlug}";
            }
        }
        
        return baseName;
    }
}

/// <summary>
/// Configuration for worktree service
/// </summary>
public sealed class WorktreeConfig
{
    /// <summary>
    /// Custom worktree root directory (optional)
    /// If not set, defaults to .worktrees in each project workspace
    /// </summary>
    public string? WorktreeRoot { get; set; }
    
    /// <summary>
    /// Whether to automatically create worktrees for all executable tickets
    /// </summary>
    public bool AutoCreate { get; set; } = true;
    
    /// <summary>
    /// Whether to automatically cleanup worktrees after merge
    /// </summary>
    public bool AutoCleanup { get; set; } = false;
    
    /// <summary>
    /// Branch naming template
    /// Use placeholders: {ticketId}, {ticketSlug}, {ticketTitle}
    /// </summary>
    public string BranchTemplate { get; set; } = "kc/KC-{ticketId}";
}
