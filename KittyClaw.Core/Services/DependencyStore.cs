using KittyClaw.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// SQLite-backed store for TicketDependency and ParallelGroup records.
/// Uses raw SQLite for consistency with other stores.
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

    private string DbPath(string projectSlug)
    {
        var projectsDir = Path.Combine(_projects.DataDir, "projects");
        Directory.CreateDirectory(projectsDir);
        return Path.Combine(projectsDir, $"{projectSlug}.db");
    }

    public async Task EnsureTablesAsync(string projectSlug)
    {
        var dbPath = DbPath(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
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
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<TicketDependency> AddDependencyAsync(TicketDependency dep, CancellationToken ct = default)
    {
        await EnsureTablesAsync(dep.ProjectSlug);
        await using var conn = new SqliteConnection($"Data Source={DbPath(dep.ProjectSlug)}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO TicketDependencies (Id, ProjectSlug, FromTicketId, ToTicketId, DependencyType, Scope, Required, CreatedAt) VALUES ($id, $project, $from, $to, $type, $scope, $required, $createdAt)";
        cmd.Parameters.AddWithValue("$id", dep.Id);
        cmd.Parameters.AddWithValue("$project", dep.ProjectSlug);
        cmd.Parameters.AddWithValue("$from", dep.FromTicketId);
        cmd.Parameters.AddWithValue("$to", dep.ToTicketId);
        cmd.Parameters.AddWithValue("$type", dep.DependencyType);
        cmd.Parameters.AddWithValue("$scope", dep.Scope);
        cmd.Parameters.AddWithValue("$required", dep.Required ? 1 : 0);
        cmd.Parameters.AddWithValue("$createdAt", dep.CreatedAt.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
        return dep;
    }

    public async Task<ParallelGroup> CreateParallelGroupAsync(ParallelGroup group, CancellationToken ct = default)
    {
        await EnsureTablesAsync(group.ProjectSlug);
        await using var conn = new SqliteConnection($"Data Source={DbPath(group.ProjectSlug)}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO ParallelGroups (Id, ProjectSlug, ParentTicketId, Name, JoinPolicy, MaxConcurrency, Status, CreatedAt) VALUES ($id, $project, $parent, $name, $joinPolicy, $maxConcurrency, $status, $createdAt)";
        cmd.Parameters.AddWithValue("$id", group.Id);
        cmd.Parameters.AddWithValue("$project", group.ProjectSlug);
        cmd.Parameters.AddWithValue("$parent", group.ParentTicketId);
        cmd.Parameters.AddWithValue("$name", group.Name);
        cmd.Parameters.AddWithValue("$joinPolicy", group.JoinPolicy);
        cmd.Parameters.AddWithValue("$maxConcurrency", group.MaxConcurrency);
        cmd.Parameters.AddWithValue("$status", group.Status);
        cmd.Parameters.AddWithValue("$createdAt", group.CreatedAt.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
        return group;
    }

    public async Task<List<TicketDependency>> GetDependenciesForAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        await EnsureTablesAsync(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={DbPath(projectSlug)}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TicketDependencies WHERE ProjectSlug = $project AND (FromTicketId = $ticket OR ToTicketId = $ticket)";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$ticket", ticketId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<TicketDependency>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new TicketDependency
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                ProjectSlug = reader.GetString(reader.GetOrdinal("ProjectSlug")),
                FromTicketId = reader.GetInt32(reader.GetOrdinal("FromTicketId")),
                ToTicketId = reader.GetInt32(reader.GetOrdinal("ToTicketId")),
                DependencyType = reader.GetString(reader.GetOrdinal("DependencyType")),
                Scope = reader.GetString(reader.GetOrdinal("Scope")),
                Required = reader.GetInt32(reader.GetOrdinal("Required")) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return results;
    }

    public async Task<List<TicketDependency>> GetBlockingDependenciesAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        await EnsureTablesAsync(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={DbPath(projectSlug)}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TicketDependencies WHERE ProjectSlug = $project AND ToTicketId = $ticket AND Required = 1";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$ticket", ticketId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<TicketDependency>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new TicketDependency
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                ProjectSlug = reader.GetString(reader.GetOrdinal("ProjectSlug")),
                FromTicketId = reader.GetInt32(reader.GetOrdinal("FromTicketId")),
                ToTicketId = reader.GetInt32(reader.GetOrdinal("ToTicketId")),
                DependencyType = reader.GetString(reader.GetOrdinal("DependencyType")),
                Scope = reader.GetString(reader.GetOrdinal("Scope")),
                Required = true,
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return results;
    }

    public async Task<List<ParallelGroup>> GetGroupsForParentAsync(string projectSlug, int parentTicketId, CancellationToken ct = default)
    {
        await EnsureTablesAsync(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={DbPath(projectSlug)}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ParallelGroups WHERE ProjectSlug = $project AND ParentTicketId = $parent";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$parent", parentTicketId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ParallelGroup>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ParallelGroup
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                ProjectSlug = reader.GetString(reader.GetOrdinal("ProjectSlug")),
                ParentTicketId = reader.GetInt32(reader.GetOrdinal("ParentTicketId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                JoinPolicy = reader.GetString(reader.GetOrdinal("JoinPolicy")),
                MaxConcurrency = reader.GetInt32(reader.GetOrdinal("MaxConcurrency")),
                OnCompleteTicketId = reader.IsDBNull(reader.GetOrdinal("OnCompleteTicketId")) ? null : reader.GetInt32(reader.GetOrdinal("OnCompleteTicketId")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return results;
    }
}
