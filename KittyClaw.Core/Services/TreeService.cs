using KittyClaw.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// Service for hierarchical ticket tree operations.
/// Handles parent/child relationships, subtree queries, and tree integrity.
/// </summary>
public sealed class TreeService
{
    private readonly ProjectService _projects;
    private readonly ILogger? _logger;

    public TreeService(ProjectService projects, ILogger? logger = null)
    {
        _projects = projects;
        _logger = logger;
    }

    /// <summary>
    /// Get all children of a ticket.
    /// </summary>
    public async Task<List<Ticket>> GetChildrenAsync(string projectSlug, int parentId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureHierarchyColumnsAsync(db);
        
        return await db.Tickets
            .Where(t => t.ParentId == parentId)
            .OrderBy(t => t.TreeOrder)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Get entire subtree rooted at a ticket.
    /// </summary>
    public async Task<List<Ticket>> GetSubtreeAsync(string projectSlug, int rootId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureHierarchyColumnsAsync(db);
        
        var root = await db.Tickets.FindAsync(rootId);
        if (root?.Path is null) return new List<Ticket> { root! };
        
        return await db.Tickets
            .Where(t => t.Path != null && t.Path.StartsWith(root.Path) && t.Id != rootId)
            .OrderBy(t => t.Path)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Check if adding a parent would create a cycle.
    /// </summary>
    public async Task<bool> WouldCreateCycleAsync(string projectSlug, int ticketId, int newParentId, CancellationToken ct = default)
    {
        if (ticketId == newParentId) return true;
        
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureHierarchyColumnsAsync(db);
        
        // Walk up from newParentId to see if we hit ticketId
        var current = await db.Tickets.FindAsync(newParentId);
        while (current?.ParentId is not null)
        {
            if (current.ParentId == ticketId) return true;
            current = await db.Tickets.FindAsync(current.ParentId);
        }
        
        return false;
    }

    /// <summary>
    /// Get max depth from a ticket to its root.
    /// </summary>
    public async Task<int> GetDepthAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureHierarchyColumnsAsync(db);
        
        var ticket = await db.Tickets.FindAsync(ticketId);
        return ticket?.Depth ?? 0;
    }

    /// <summary>
    /// Reparent a ticket to a new parent.
    /// </summary>
    public async Task<bool> ReparentAsync(
        string projectSlug,
        int ticketId,
        int? newParentId,
        CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureHierarchyColumnsAsync(db);
        
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null) return false;
        
        // Check for cycle
        if (newParentId.HasValue && await WouldCreateCycleAsync(projectSlug, ticketId, newParentId.Value, ct))
        {
            _logger?.LogWarning("Reparent would create cycle: ticket {Id} -> parent {ParentId}", ticketId, newParentId);
            return false;
        }
        
        // Check max depth
        int newDepth = 0;
        if (newParentId.HasValue)
        {
            var newParent = await db.Tickets.FindAsync(newParentId.Value);
            newDepth = (newParent?.Depth ?? 0) + 1;
            if (newDepth > 3)
            {
                _logger?.LogWarning("Max depth exceeded: ticket {Id} would be at depth {Depth}", ticketId, newDepth);
                // Allow but warn - don't block
            }
        }
        
        // Update ticket
        var oldParentId = ticket.ParentId;
        ticket.ParentId = newParentId;
        ticket.Depth = newDepth;
        ticket.UpdatedAt = DateTime.UtcNow;
        
        // Update root
        if (newParentId.HasValue)
        {
            var newParent = await db.Tickets.FindAsync(newParentId.Value);
            ticket.RootId = newParent?.RootId ?? newParentId;
        }
        else
        {
            ticket.RootId = null; // This is a root ticket
        }
        
        // Recalculate path
        await RecalculatePathAsync(db, ticket);
        
        // Update parent counts
        if (oldParentId.HasValue)
            await UpdateParentCountsAsync(db, oldParentId.Value);
        if (newParentId.HasValue)
            await UpdateParentCountsAsync(db, newParentId.Value);
        
        await db.SaveChangesAsync(ct);
        
        _logger?.LogInformation("Reparented ticket {Id} from {OldParent} to {NewParent}", 
            ticketId, oldParentId, newParentId);
        
        return true;
    }

    /// <summary>
    /// Move entire subtree to a new parent.
    /// </summary>
    public async Task<bool> MoveSubtreeAsync(
        string projectSlug,
        int rootId,
        int? newParentId,
        CancellationToken ct = default)
    {
        var subtree = await GetSubtreeAsync(projectSlug, rootId, ct);
        
        // Check if any ticket in subtree has an active run
        await using var db = _projects.GetProjectDb(projectSlug);
        foreach (var ticket in subtree)
        {
            if (ticket.SubtreeHasActiveRun)
            {
                _logger?.LogWarning("Cannot move subtree: ticket {Id} has active run", ticket.Id);
                return false;
            }
        }
        
        // Move root first
        if (!await ReparentAsync(projectSlug, rootId, newParentId, ct))
            return false;
        
        // All children paths will be recalculated automatically
        return true;
    }

    /// <summary>
    /// Get child count and progress for a parent ticket.
    /// </summary>
    public async Task<TreeProgress> GetProgressAsync(string projectSlug, int parentId, CancellationToken ct = default)
    {
        var children = await GetChildrenAsync(projectSlug, parentId, ct);
        
        return new TreeProgress
        {
            Total = children.Count,
            Done = children.Count(c => c.Status == "Done"),
            Running = children.Count(c => c.Status == "InProgress"),
            Blocked = children.Count(c => c.Status == "Blocked"),
            Failed = children.Count(c => c.Status == "Failed"),
            Waiting = children.Count(c => c.Status == "Ready" || c.Status == "Backlog")
        };
    }

    private async Task RecalculatePathAsync(TodoDbContext db, Ticket ticket)
    {
        var parts = new List<int> { ticket.Id };
        var current = ticket;
        
        while (current.ParentId is not null)
        {
            current = await db.Tickets.FindAsync(current.ParentId.Value);
            if (current is not null)
                parts.Insert(0, current.Id);
        }
        
        ticket.Path = string.Join("/", parts);
    }

    private async Task UpdateParentCountsAsync(TodoDbContext db, int parentId)
    {
        var parent = await db.Tickets.FindAsync(parentId);
        if (parent is null) return;
        
        var children = await db.Tickets.Where(t => t.ParentId == parentId).ToListAsync();
        
        parent.ChildCount = children.Count;
        parent.ChildrenDoneCount = children.Count(c => c.Status == "Done");
        parent.ChildrenFailedCount = children.Count(c => c.Status == "Failed");
        parent.ChildrenBlockedCount = children.Count(c => c.Status == "Blocked");
        parent.SubtreeHasActiveRun = children.Any(c => c.Status == "InProgress");
        
        parent.UpdatedAt = DateTime.UtcNow;
    }

    internal static async Task EnsureHierarchyColumnsAsync(TodoDbContext db)
    {
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN RootId INTEGER NULL"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN Depth INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN Path TEXT NULL"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN TreeOrder INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN Kind TEXT NOT NULL DEFAULT 'task'"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ExecutionRole TEXT NULL"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ExecutionMode TEXT NOT NULL DEFAULT 'manual'"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ParallelGroupId TEXT NULL"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN DependencyPolicy TEXT NOT NULL DEFAULT 'all_dependencies_done'"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN AggregatePolicy TEXT NOT NULL DEFAULT 'children_define_progress'"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ChildCount INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ChildrenDoneCount INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ChildrenFailedCount INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN ChildrenBlockedCount INTEGER NOT NULL DEFAULT 0"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tickets ADD COLUMN SubtreeHasActiveRun INTEGER NOT NULL DEFAULT 0"); }
        catch { }
    }
}

/// <summary>
/// Progress summary for a subtree.
/// </summary>
public sealed class TreeProgress
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Running { get; set; }
    public int Blocked { get; set; }
    public int Failed { get; set; }
    public int Waiting { get; set; }
    public double ProgressPercent => Total > 0 ? (double)Done / Total * 100 : 0;
}
