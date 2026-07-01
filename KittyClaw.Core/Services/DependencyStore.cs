using KittyClaw.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// SQLite-backed store for TicketDependency and ParallelGroup records.
/// </summary>
public sealed class DependencyStore
{
    private readonly ProjectService _projects;
    private readonly ILogger? _logger;

    public DependencyStore(ProjectService projects, ILogger? logger = null)
    {
        _projects = projects;
        _logger = logger;
    }

    public async Task EnsureTablesAsync(TodoDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS TicketDependencies (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ProjectSlug TEXT NOT NULL,
                    FromTicketId INTEGER NOT NULL,
                    ToTicketId INTEGER NOT NULL,
                    DependencyType TEXT NOT NULL DEFAULT 'finish_to_start',
                    Scope TEXT NOT NULL DEFAULT 'same_parent',
                    Required INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_TicketDependencies_From ON TicketDependencies (FromTicketId);
                CREATE INDEX IF NOT EXISTS IX_TicketDependencies_To ON TicketDependencies (ToTicketId);
                CREATE INDEX IF NOT EXISTS IX_TicketDependencies_Project ON TicketDependencies (ProjectSlug);
            """);
        }
        catch { /* columns already exist */ }
        
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS ParallelGroups (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ProjectSlug TEXT NOT NULL,
                    ParentTicketId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    JoinPolicy TEXT NOT NULL DEFAULT 'all_done',
                    MaxConcurrency INTEGER NOT NULL DEFAULT 2,
                    OnCompleteTicketId INTEGER,
                    Status TEXT NOT NULL DEFAULT 'pending',
                    CreatedAt TEXT NOT NULL,
                    CompletedAt TEXT
                );
                CREATE INDEX IF NOT EXISTS IX_ParallelGroups_Parent ON ParallelGroups (ParentTicketId);
                CREATE INDEX IF NOT EXISTS IX_ParallelGroups_Project ON ParallelGroups (ProjectSlug);
            """);
        }
        catch { /* columns already exist */ }
    }

    // ── Dependencies ───────────────────────────────────────────────────

    public async Task<TicketDependency> AddDependencyAsync(TicketDependency dep, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(dep.ProjectSlug);
        await EnsureTablesAsync(db);
        
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO TicketDependencies (Id, ProjectSlug, FromTicketId, ToTicketId, DependencyType, Scope, Required, CreatedAt)
            VALUES ($id, $project, $from, $to, $type, $scope, $required, $createdAt)
        """,
            dep.Id, dep.ProjectSlug, dep.FromTicketId, dep.ToTicketId, 
            dep.DependencyType, dep.Scope, dep.Required ? 1 : 0, dep.CreatedAt.ToString("o"));
        
        return dep;
    }

    public async Task<List<TicketDependency>> GetDependenciesForAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureTablesAsync(db);
        
        return await db.Database
            .SqlQueryRaw<TicketDependency>(
                "SELECT * FROM TicketDependencies WHERE ProjectSlug = $project AND (FromTicketId = $ticket OR ToTicketId = $ticket)",
                projectSlug, ticketId)
            .ToListAsync(ct);
    }

    public async Task<List<TicketDependency>> GetBlockingDependenciesAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureTablesAsync(db);
        
        return await db.Database
            .SqlQueryRaw<TicketDependency>(
                "SELECT * FROM TicketDependencies WHERE ProjectSlug = $project AND ToTicketId = $ticket AND Required = 1",
                projectSlug, ticketId)
            .ToListAsync(ct);
    }

    public async Task<bool> RemoveDependencyAsync(string projectSlug, string dependencyId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureTablesAsync(db);
        
        var affected = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM TicketDependencies WHERE Id = $id AND ProjectSlug = $project",
            dependencyId, projectSlug);
        return affected > 0;
    }

    // ── Parallel Groups ────────────────────────────────────────────────

    public async Task<ParallelGroup> CreateParallelGroupAsync(ParallelGroup group, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(group.ProjectSlug);
        await EnsureTablesAsync(db);
        
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO ParallelGroups (Id, ProjectSlug, ParentTicketId, Name, JoinPolicy, MaxConcurrency, OnCompleteTicketId, Status, CreatedAt)
            VALUES ($id, $project, $parent, $name, $joinPolicy, $maxConcurrency, $onComplete, $status, $createdAt)
        """,
            group.Id, group.ProjectSlug, group.ParentTicketId, group.Name,
            group.JoinPolicy, group.MaxConcurrency, group.OnCompleteTicketId,
            group.Status, group.CreatedAt.ToString("o"));
        
        return group;
    }

    public async Task<List<ParallelGroup>> GetGroupsForParentAsync(string projectSlug, int parentTicketId, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureTablesAsync(db);
        
        return await db.Database
            .SqlQueryRaw<ParallelGroup>(
                "SELECT * FROM ParallelGroups WHERE ProjectSlug = $project AND ParentTicketId = $parent",
                projectSlug, parentTicketId)
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateGroupStatusAsync(string projectSlug, string groupId, string status, CancellationToken ct = default)
    {
        await using var db = _projects.GetProjectDb(projectSlug);
        await EnsureTablesAsync(db);
        
        var now = DateTimeOffset.UtcNow.ToString("o");
        var affected = await db.Database.ExecuteSqlRawAsync(
            "UPDATE ParallelGroups SET Status = $status, CompletedAt = $now WHERE Id = $id AND ProjectSlug = $project",
            status, now, groupId, projectSlug);
        return affected > 0;
    }
}
