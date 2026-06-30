using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KittyClaw.Core.Data;

namespace KittyClaw.Core.TeamChat;

public class TeamChatService : ITeamChatService
{
    private readonly string _dataDir;

    public TeamChatService(string dataDir)
    {
        _dataDir = dataDir;
    }

    private string DbPath(string projectSlug)
    {
        var projectsDir = Path.Combine(_dataDir, "projects");
        Directory.CreateDirectory(projectsDir);
        return Path.Combine(projectsDir, $"{projectSlug}.db");
    }

    private static TeamChatMessage ReadMessage(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        ProjectSlug = r.GetString(r.GetOrdinal("ProjectSlug")),
        TicketId = r.IsDBNull(r.GetOrdinal("TicketId")) ? null : r.GetInt32(r.GetOrdinal("TicketId")),
        RunId = r.IsDBNull(r.GetOrdinal("RunId")) ? null : r.GetString(r.GetOrdinal("RunId")),
        AuthorType = r.GetString(r.GetOrdinal("AuthorType")),
        AuthorId = r.GetString(r.GetOrdinal("AuthorId")),
        Body = r.GetString(r.GetOrdinal("Body")),
        MessageType = r.GetString(r.GetOrdinal("MessageType")),
        TargetType = r.GetString(r.GetOrdinal("TargetType")),
        TargetId = r.IsDBNull(r.GetOrdinal("TargetId")) ? null : r.GetString(r.GetOrdinal("TargetId")),
        DeliveryStatus = r.GetString(r.GetOrdinal("DeliveryStatus")),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        ResolvedAt = r.IsDBNull(r.GetOrdinal("ResolvedAt")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("ResolvedAt"))),
        ResolvedBy = r.IsDBNull(r.GetOrdinal("ResolvedBy")) ? null : r.GetString(r.GetOrdinal("ResolvedBy")),
        CorrelationId = r.IsDBNull(r.GetOrdinal("CorrelationId")) ? null : r.GetString(r.GetOrdinal("CorrelationId")),
        MetadataJson = r.IsDBNull(r.GetOrdinal("MetadataJson")) ? null : r.GetString(r.GetOrdinal("MetadataJson")),
    };

    public async Task<TeamChatMessage> PostMessageAsync(string projectSlug, PostTeamChatMessageRequest req, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await TodoDbContext.EnsureTeamChatTablesAsync(dbPath);

        var message = new TeamChatMessage
        {
            ProjectSlug = projectSlug,
            AuthorType = req.AuthorType,
            AuthorId = req.AuthorId,
            Body = req.Body,
            MessageType = req.MessageType,
            TargetType = req.TargetType,
            TargetId = req.TargetId,
            TicketId = req.TicketId,
            RunId = req.RunId,
            CorrelationId = req.CorrelationId,
            MetadataJson = req.MetadataJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TeamChatMessages (Id, ProjectSlug, TicketId, RunId, AuthorType, AuthorId, Body, MessageType, TargetType, TargetId, DeliveryStatus, CreatedAt, ResolvedAt, ResolvedBy, CorrelationId, MetadataJson)
            VALUES ($id, $slug, $ticket, $run, $authorType, $authorId, $body, $msgType, $targetType, $targetId, $delivery, $created, $resolved, $resolvedBy, $corr, $meta)
        """;
        cmd.Parameters.AddWithValue("$id", message.Id);
        cmd.Parameters.AddWithValue("$slug", message.ProjectSlug);
        cmd.Parameters.AddWithValue("$ticket", (object?)message.TicketId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$run", (object?)message.RunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$authorType", message.AuthorType);
        cmd.Parameters.AddWithValue("$authorId", message.AuthorId);
        cmd.Parameters.AddWithValue("$body", message.Body);
        cmd.Parameters.AddWithValue("$msgType", message.MessageType);
        cmd.Parameters.AddWithValue("$targetType", message.TargetType);
        cmd.Parameters.AddWithValue("$targetId", (object?)message.TargetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$delivery", message.DeliveryStatus);
        cmd.Parameters.AddWithValue("$created", message.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$resolved", (object?)message.ResolvedAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$resolvedBy", (object?)message.ResolvedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$corr", (object?)message.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$meta", (object?)message.MetadataJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<TeamChatMessage>> ListMessagesAsync(string projectSlug, TeamChatQuery query, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await TodoDbContext.EnsureTeamChatTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TeamChatMessages WHERE ProjectSlug = $slug ORDER BY CreatedAt DESC";
        cmd.Parameters.AddWithValue("$slug", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var all = new List<TeamChatMessage>();
        while (await reader.ReadAsync(ct))
            all.Add(ReadMessage(reader));

        var filtered = all.AsEnumerable();

        if (query.Filter == "needs-human")
            filtered = filtered.Where(m => m.MessageType == "question" && m.DeliveryStatus == "open");
        else if (query.Filter == "running")
            filtered = filtered.Where(m => m.RunId != null && m.DeliveryStatus == "delivered");
        else if (query.Filter == "failures")
            filtered = filtered.Where(m => m.MessageType == "failure");
        else if (query.Filter == "mentions")
            filtered = filtered.Where(m => m.TargetType == "role" || m.TargetType == "agent");
        else if (query.Filter == "ai-activity")
            // Show messages related to AI runs: agent messages, status updates, run events
            filtered = filtered.Where(m => 
                m.AuthorType == "agent" || 
                m.MessageType == "run_started" || 
                m.MessageType == "run_completed" || 
                m.MessageType == "run_failed" ||
                m.MessageType == "run_stopped" ||
                (m.RunId != null && m.DeliveryStatus == "delivered"));

        if (query.TicketId.HasValue)
            filtered = filtered.Where(m => m.TicketId == query.TicketId);
        if (!string.IsNullOrEmpty(query.RunId))
            filtered = filtered.Where(m => m.RunId == query.RunId);
        if (!string.IsNullOrEmpty(query.AuthorId))
            filtered = filtered.Where(m => m.AuthorId == query.AuthorId);

        return filtered.Skip(query.Offset).Take(query.Limit).ToList();
    }

    public async Task<TeamChatMessage?> GetMessageAsync(string projectSlug, string messageId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TeamChatMessages WHERE Id = $id AND ProjectSlug = $slug";
        cmd.Parameters.AddWithValue("$id", messageId);
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadMessage(reader) : null;
    }

    public async Task ResolveMessageAsync(string projectSlug, string messageId, string resolvedBy, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE TeamChatMessages SET DeliveryStatus = 'resolved', ResolvedAt = $now, ResolvedBy = $by WHERE Id = $id AND ProjectSlug = $slug";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$by", resolvedBy);
        cmd.Parameters.AddWithValue("$id", messageId);
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TeamChatMessage>> GetInboxAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await TodoDbContext.EnsureTeamChatTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM TeamChatMessages
            WHERE ProjectSlug = $slug AND (DeliveryStatus = 'open' OR MessageType = 'question' OR MessageType = 'failure')
            ORDER BY CreatedAt DESC LIMIT 50
        """;
        cmd.Parameters.AddWithValue("$slug", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<TeamChatMessage>();
        while (await reader.ReadAsync(ct))
            results.Add(ReadMessage(reader));
        return results;
    }

    public async Task<TeamChatMessage> AddSystemEventAsync(string projectSlug, SystemEventRequest req, CancellationToken ct = default)
    {
        return await PostMessageAsync(projectSlug, new PostTeamChatMessageRequest(
            Body: req.Body,
            AuthorId: req.AuthorId,
            AuthorType: "system",
            MessageType: req.MessageType,
            TargetType: req.TargetType ?? "team",
            TargetId: req.TargetId,
            TicketId: req.TicketId,
            RunId: req.RunId,
            MetadataJson: req.MetadataJson
        ), ct);
    }

    public async Task<int> GetUnreadCountAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await TodoDbContext.EnsureTeamChatTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM TeamChatMessages WHERE ProjectSlug = $slug AND DeliveryStatus = 'open' AND AuthorType != 'human'";
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<TeamChatMessage>> GetMessagesForAgentAsync(string projectSlug, string agentId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await TodoDbContext.EnsureTeamChatTablesAsync(dbPath);

        var role = AgentRoles.GetRole(agentId);
        if (role is null) return [];

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM TeamChatMessages
            WHERE ProjectSlug = $slug
            AND (
                TargetType = 'team'
                OR (TargetType = 'role' AND TargetId = $roleId)
                OR (TargetType = 'agent' AND TargetId = $agentId)
                OR (TargetType = 'ticket' AND AuthorType = 'human')
                OR (AuthorId = $agentId)
            )
            ORDER BY CreatedAt DESC LIMIT 100
        """;
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        cmd.Parameters.AddWithValue("$roleId", role.Id);
        cmd.Parameters.AddWithValue("$agentId", agentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var messages = new List<TeamChatMessage>();
        while (await reader.ReadAsync(ct))
            messages.Add(ReadMessage(reader));
        return messages;
    }
}
