using KittyClaw.Core.Data;
using KittyClaw.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// Service for hierarchical ticket tree operations.
/// Uses raw SQLite for schema migrations (consistent with other stores).
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

    private string DbPath(string projectSlug)
    {
        var projectsDir = Path.Combine(_projects.DataDir, "projects");
        Directory.CreateDirectory(projectsDir);
        return Path.Combine(projectsDir, $"{projectSlug}.db");
    }

    public async Task<List<Ticket>> GetChildrenAsync(string projectSlug, int parentId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        return await db.Tickets.Where(t => t.ParentId == parentId).OrderBy(t => t.TreeOrder).ToListAsync(ct);
    }

    public async Task<List<Ticket>> GetSubtreeAsync(string projectSlug, int rootId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        var root = await db.Tickets.FindAsync(rootId);
        if (root?.Path is null) return new List<Ticket> { root! };
        return await db.Tickets.Where(t => t.Path != null && t.Path.StartsWith(root.Path) && t.Id != rootId).OrderBy(t => t.Path).ToListAsync(ct);
    }

    public async Task<bool> WouldCreateCycleAsync(string projectSlug, int ticketId, int newParentId, CancellationToken ct = default)
    {
        if (ticketId == newParentId) return true;
        await using var db = _projects.GetProjectDb(projectSlug);
        var current = await db.Tickets.FindAsync(newParentId);
        while (current?.ParentId is not null)
        {
            if (current.ParentId == ticketId) return true;
            current = await db.Tickets.FindAsync(current.ParentId);
        }
        return false;
    }

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
            Waiting = children.Count(c => c.Status is "Ready" or "Backlog")
        };
    }

    public async Task<bool> ReparentAsync(string projectSlug, int ticketId, int? newParentId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null) return false;

        if (newParentId.HasValue && await WouldCreateCycleAsync(projectSlug, ticketId, newParentId.Value, ct))
        {
            _logger?.LogWarning("Reparent would create cycle: ticket {Id} -> parent {ParentId}", ticketId, newParentId);
            return false;
        }

        var oldParentId = ticket.ParentId;
        ticket.ParentId = newParentId;
        ticket.Depth = newParentId.HasValue ? (await db.Tickets.FindAsync(newParentId.Value))?.Depth ?? 0 + 1 : 0;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (newParentId.HasValue)
        {
            var newParent = await db.Tickets.FindAsync(newParentId.Value);
            ticket.RootId = newParent?.RootId ?? newParentId;
        }
        else
        {
            ticket.RootId = null;
        }

        await db.SaveChangesAsync(ct);
        _logger?.LogInformation("Reparented ticket {Id} from {OldParent} to {NewParent}", ticketId, oldParentId, newParentId);
        return true;
    }

    public async Task<bool> MoveSubtreeAsync(string projectSlug, int rootId, int? newParentId, CancellationToken ct = default)
    {
        return await ReparentAsync(projectSlug, rootId, newParentId, ct);
    }

    public async Task EnsureHierarchyColumnsAsync(string projectSlug, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        // Ensure hierarchy columns exist in Tickets table
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Tickets ADD COLUMN ParentId INTEGER;",
                ct);
        }
        catch { /* may already exist */ }
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Tickets ADD COLUMN TreeOrder INTEGER DEFAULT 0;",
                ct);
        }
        catch { /* may already exist */ }
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Tickets ADD COLUMN Path TEXT;",
                ct);
        }
        catch { /* may already exist */ }
    }
}

public sealed class TreeProgress
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Running { get; set; }
    public int Blocked { get; set; }
    public int Failed { get; set; }
    public int Waiting { get; set; }
}
