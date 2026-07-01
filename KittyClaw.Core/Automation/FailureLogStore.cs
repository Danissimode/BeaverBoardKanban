using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation;

/// <summary>
/// SQLite-backed store for FailureLogEntry records.
/// Persists failures across restarts.
/// </summary>
public sealed class FailureLogStore
{
    private readonly string _dataDir;
    private readonly ILogger? _logger;

    public FailureLogStore(string dataDir, ILogger? logger = null)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    private string DbPath(string projectSlug)
    {
        var projectsDir = Path.Combine(_dataDir, "projects");
        Directory.CreateDirectory(projectsDir);
        return Path.Combine(projectsDir, $"{projectSlug}.db");
    }

    public static async Task EnsureTableAsync(string dbPath)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS FailureLogEntries (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectSlug TEXT NOT NULL,
                TicketId INTEGER NOT NULL,
                Kind TEXT NOT NULL,
                Message TEXT NOT NULL,
                RequiredAction TEXT,
                RunId TEXT,
                StackTrace TEXT,
                Resolved INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                ResolvedAt TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_FailureLog_Project_Ticket
            ON FailureLogEntries (ProjectSlug, TicketId);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_Resolved
            ON FailureLogEntries (Resolved);
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<FailureLogEntry> RecordAsync(FailureLogEntry entry, CancellationToken ct = default)
    {
        var dbPath = DbPath(entry.ProjectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO FailureLogEntries
            (Id, ProjectSlug, TicketId, Kind, Message, RequiredAction, RunId, StackTrace, Resolved, CreatedAt, ResolvedAt)
            VALUES ($id, $project, $ticket, $kind, $message, $action, $run, $stack, $resolved, $created, $resolvedAt)
        """;
        cmd.Parameters.AddWithValue("$id", entry.Id);
        cmd.Parameters.AddWithValue("$project", entry.ProjectSlug);
        cmd.Parameters.AddWithValue("$ticket", entry.TicketId);
        cmd.Parameters.AddWithValue("$kind", entry.Kind);
        cmd.Parameters.AddWithValue("$message", entry.Message);
        cmd.Parameters.AddWithValue("$action", (object?)entry.RequiredAction ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$run", (object?)entry.RunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$stack", (object?)entry.StackTrace ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$resolved", entry.Resolved ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", entry.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$resolvedAt", (object?)entry.ResolvedAt?.ToString("o") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);

        _logger?.LogInformation("[FAILURE] ticket #{TicketId} [{Kind}]: {Message}",
            entry.TicketId, entry.Kind, entry.Message);

        return entry;
    }

    public async Task<IReadOnlyList<FailureLogEntry>> ForTicketAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project AND TicketId = $ticket ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$ticket", ticketId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<IReadOnlyList<FailureLogEntry>> UnresolvedForTicketAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        var all = await ForTicketAsync(projectSlug, ticketId, ct);
        return all.Where(e => !e.Resolved).ToList();
    }

    public async Task<IReadOnlyList<FailureLogEntry>> UnresolvedForProjectAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project AND Resolved = 0 ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$project", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<IReadOnlyList<FailureLogEntry>> ForProjectAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$project", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<bool> ResolveAsync(string projectSlug, string entryId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE FailureLogEntries SET Resolved = 1, ResolvedAt = $now WHERE Id = $id AND ProjectSlug = $project";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", entryId);
        cmd.Parameters.AddWithValue("$project", projectSlug);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    public async Task ClearForTicketAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM FailureLogEntries WHERE ProjectSlug = $project AND TicketId = $ticket";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$ticket", ticketId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FailureLogEntry?> LatestUnresolvedAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        var unresolved = await UnresolvedForTicketAsync(projectSlug, ticketId, ct);
        return unresolved.FirstOrDefault();
    }

    private static FailureLogEntry ReadEntry(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        ProjectSlug = r.GetString(r.GetOrdinal("ProjectSlug")),
        TicketId = r.GetInt32(r.GetOrdinal("TicketId")),
        Kind = r.GetString(r.GetOrdinal("Kind")),
        Message = r.GetString(r.GetOrdinal("Message")),
        RequiredAction = r.IsDBNull(r.GetOrdinal("RequiredAction")) ? null : r.GetString(r.GetOrdinal("RequiredAction")),
        RunId = r.IsDBNull(r.GetOrdinal("RunId")) ? null : r.GetString(r.GetOrdinal("RunId")),
        StackTrace = r.IsDBNull(r.GetOrdinal("StackTrace")) ? null : r.GetString(r.GetOrdinal("StackTrace")),
        Resolved = r.GetInt32(r.GetOrdinal("Resolved")) == 1,
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        ResolvedAt = r.IsDBNull(r.GetOrdinal("ResolvedAt")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("ResolvedAt")))
    };
}
