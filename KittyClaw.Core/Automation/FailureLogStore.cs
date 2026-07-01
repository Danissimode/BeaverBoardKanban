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
                UpdatedAt TEXT NOT NULL,
                ResolvedAt TEXT,
                MetadataJson TEXT,
                Agent TEXT,
                Runner TEXT,
                Provider TEXT,
                Model TEXT,
                ExecutionMode TEXT,
                ErrorType TEXT,
                ExitCode INTEGER,
                FallbackUsed INTEGER NOT NULL DEFAULT 0,
                Resolution TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_FailureLog_Project_Ticket
            ON FailureLogEntries (ProjectSlug, TicketId);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_Resolved
            ON FailureLogEntries (Resolved);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_RunId
            ON FailureLogEntries (RunId);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_ErrorType
            ON FailureLogEntries (ErrorType);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_CreatedAt
            ON FailureLogEntries (CreatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_FailureLog_Agent
            ON FailureLogEntries (Agent);
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
            (Id, ProjectSlug, TicketId, Kind, Message, RequiredAction, RunId, StackTrace, 
             Resolved, CreatedAt, UpdatedAt, ResolvedAt, MetadataJson,
             Agent, Runner, Provider, Model, ExecutionMode, ErrorType, ExitCode, FallbackUsed, Resolution)
            VALUES ($id, $project, $ticket, $kind, $message, $action, $run, $stack, 
                    $resolved, $created, $updatedAt, $resolvedAt, $metadata,
                    $agent, $runner, $provider, $model, $executionMode, $errorType, $exitCode, $fallbackUsed, $resolution)
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
        cmd.Parameters.AddWithValue("$updatedAt", entry.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$resolvedAt", (object?)entry.ResolvedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$metadata", (object?)entry.MetadataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$agent", (object?)entry.Agent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$runner", (object?)entry.Runner ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$provider", (object?)entry.Provider ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$model", (object?)entry.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$executionMode", (object?)entry.ExecutionMode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$errorType", (object?)entry.ErrorType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$exitCode", (object?)entry.ExitCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fallbackUsed", entry.FallbackUsed ? 1 : 0);
        cmd.Parameters.AddWithValue("$resolution", (object?)entry.Resolution ?? DBNull.Value);
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

    public async Task<bool> ResolveAsync(string projectSlug, string entryId, string? resolution = null, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE FailureLogEntries 
            SET Resolved = 1, ResolvedAt = $now, UpdatedAt = $now, Resolution = $resolution 
            WHERE Id = $id AND ProjectSlug = $project
        """;
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", entryId);
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$resolution", (object?)resolution ?? DBNull.Value);
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

    public async Task<bool> HasRecentEntryAsync(string projectSlug, int ticketId, string kind, TimeSpan window, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        var cutoff = DateTimeOffset.UtcNow.Subtract(window).ToString("o");
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM FailureLogEntries WHERE ProjectSlug = $project AND TicketId = $ticket AND Kind = $kind AND CreatedAt > $cutoff LIMIT 1";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$ticket", ticketId);
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task<FailureLogEntry?> LatestUnresolvedAsync(string projectSlug, int ticketId, CancellationToken ct = default)
    {
        var unresolved = await UnresolvedForTicketAsync(projectSlug, ticketId, ct);
        return unresolved.FirstOrDefault();
    }

    public async Task<IReadOnlyList<FailureLogEntry>> ForRunAsync(string projectSlug, string runId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project AND RunId = $run ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$run", runId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<IReadOnlyList<FailureLogEntry>> ByErrorTypeAsync(string projectSlug, string errorType, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project AND ErrorType = $errorType ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$errorType", errorType);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<IReadOnlyList<FailureLogEntry>> RecentAsync(string projectSlug, int limit = 50, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTableAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM FailureLogEntries WHERE ProjectSlug = $project ORDER BY CreatedAt DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$project", projectSlug);
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<FailureLogEntry>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadEntry(reader));
        return results;
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
        UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt"))),
        ResolvedAt = r.IsDBNull(r.GetOrdinal("ResolvedAt")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("ResolvedAt"))),
        MetadataJson = r.IsDBNull(r.GetOrdinal("MetadataJson")) ? null : r.GetString(r.GetOrdinal("MetadataJson")),
        Agent = r.IsDBNull(r.GetOrdinal("Agent")) ? null : r.GetString(r.GetOrdinal("Agent")),
        Runner = r.IsDBNull(r.GetOrdinal("Runner")) ? null : r.GetString(r.GetOrdinal("Runner")),
        Provider = r.IsDBNull(r.GetOrdinal("Provider")) ? null : r.GetString(r.GetOrdinal("Provider")),
        Model = r.IsDBNull(r.GetOrdinal("Model")) ? null : r.GetString(r.GetOrdinal("Model")),
        ExecutionMode = r.IsDBNull(r.GetOrdinal("ExecutionMode")) ? null : r.GetString(r.GetOrdinal("ExecutionMode")),
        ErrorType = r.IsDBNull(r.GetOrdinal("ErrorType")) ? null : r.GetString(r.GetOrdinal("ErrorType")),
        ExitCode = r.IsDBNull(r.GetOrdinal("ExitCode")) ? null : r.GetInt32(r.GetOrdinal("ExitCode")),
        FallbackUsed = r.GetInt32(r.GetOrdinal("FallbackUsed")) == 1,
        Resolution = r.IsDBNull(r.GetOrdinal("Resolution")) ? null : r.GetString(r.GetOrdinal("Resolution"))
    };
}
