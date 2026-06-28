using Microsoft.Data.Sqlite;
using KittyClaw.Core.Data;

namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Manages agent chat profiles and role chat policies.
/// </summary>
public class AgentChatPolicyService : IAgentChatPolicyService
{
    private readonly string _dataDir;

    public AgentChatPolicyService(string dataDir)
    {
        _dataDir = dataDir;
    }

    private string DbPath(string projectSlug)
    {
        var projectsDir = Path.Combine(_dataDir, "projects");
        Directory.CreateDirectory(projectsDir);
        return Path.Combine(projectsDir, $"{projectSlug}.db");
    }

    public async Task<AgentChatProfile?> GetProfileAsync(string projectSlug, string agentId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AgentChatProfiles WHERE ProjectSlug = $slug AND AgentId = $agentId";
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        cmd.Parameters.AddWithValue("$agentId", agentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadProfile(reader) : null;
    }

    public async Task<IReadOnlyList<AgentChatProfile>> GetAllProfilesAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AgentChatProfiles WHERE ProjectSlug = $slug";
        cmd.Parameters.AddWithValue("$slug", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var profiles = new List<AgentChatProfile>();
        while (await reader.ReadAsync(ct))
            profiles.Add(ReadProfile(reader));
        return profiles;
    }

    public async Task UpsertProfileAsync(AgentChatProfile profile, CancellationToken ct = default)
    {
        var dbPath = DbPath(profile.ProjectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO AgentChatProfiles
            (Id, ProjectSlug, AgentId, RoleId, DisplayName, RoleName,
             CanReadTeamChat, CanPostToTeamChat, CanReplyToAgents, CanAskHuman,
             CanReceiveDirectMentions, CanReceiveTeamMentions,
             Verbosity, SignalPolicy, ResponsePolicy, SystemPromptAddon, ChatRulesMarkdown,
             CreatedAt, UpdatedAt)
            VALUES
            ($id, $slug, $agentId, $roleId, $displayName, $roleName,
             $canRead, $canPost, $canReply, $canAsk,
             $canReceiveDirect, $canReceiveTeam,
             $verbosity, $signalPolicy, $responsePolicy, $systemPrompt, $chatRules,
             $created, $updated)
        """;
        cmd.Parameters.AddWithValue("$id", profile.Id);
        cmd.Parameters.AddWithValue("$slug", profile.ProjectSlug);
        cmd.Parameters.AddWithValue("$agentId", profile.AgentId);
        cmd.Parameters.AddWithValue("$roleId", profile.RoleId);
        cmd.Parameters.AddWithValue("$displayName", profile.DisplayName);
        cmd.Parameters.AddWithValue("$roleName", profile.RoleName);
        cmd.Parameters.AddWithValue("$canRead", profile.CanReadTeamChat);
        cmd.Parameters.AddWithValue("$canPost", profile.CanPostToTeamChat);
        cmd.Parameters.AddWithValue("$canReply", profile.CanReplyToAgents);
        cmd.Parameters.AddWithValue("$canAsk", profile.CanAskHuman);
        cmd.Parameters.AddWithValue("$canReceiveDirect", profile.CanReceiveDirectMentions);
        cmd.Parameters.AddWithValue("$canReceiveTeam", profile.CanReceiveTeamMentions);
        cmd.Parameters.AddWithValue("$verbosity", profile.Verbosity);
        cmd.Parameters.AddWithValue("$signalPolicy", profile.SignalPolicy);
        cmd.Parameters.AddWithValue("$responsePolicy", profile.ResponsePolicy);
        cmd.Parameters.AddWithValue("$systemPrompt", (object?)profile.SystemPromptAddon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$chatRules", (object?)profile.ChatRulesMarkdown ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", profile.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<AgentRoleChatPolicy?> GetRolePolicyAsync(string projectSlug, string roleId, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AgentRoleChatPolicies WHERE ProjectSlug = $slug AND RoleId = $roleId";
        cmd.Parameters.AddWithValue("$slug", projectSlug);
        cmd.Parameters.AddWithValue("$roleId", roleId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadPolicy(reader) : null;
    }

    public async Task<IReadOnlyList<AgentRoleChatPolicy>> GetAllRolePoliciesAsync(string projectSlug, CancellationToken ct = default)
    {
        var dbPath = DbPath(projectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM AgentRoleChatPolicies WHERE ProjectSlug = $slug";
        cmd.Parameters.AddWithValue("$slug", projectSlug);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var policies = new List<AgentRoleChatPolicy>();
        while (await reader.ReadAsync(ct))
            policies.Add(ReadPolicy(reader));
        return policies;
    }

    public async Task UpsertRolePolicyAsync(AgentRoleChatPolicy policy, CancellationToken ct = default)
    {
        var dbPath = DbPath(policy.ProjectSlug);
        await EnsureTablesAsync(dbPath);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO AgentRoleChatPolicies
            (Id, ProjectSlug, RoleId, MustReportEvents, ShouldSuppressEvents,
             CanRespondToRoles, CanCommandRoles,
             RequiresHumanApprovalBeforeAction, CanCreateTasks, CanMoveTickets, CanStopRuns,
             DefaultTone, CreatedAt, UpdatedAt)
            VALUES
            ($id, $slug, $roleId, $mustReport, $suppress,
             $respondTo, $commandRoles,
             $requiresApproval, $canCreate, $canMove, $canStop,
             $tone, $created, $updated)
        """;
        cmd.Parameters.AddWithValue("$id", policy.Id);
        cmd.Parameters.AddWithValue("$slug", policy.ProjectSlug);
        cmd.Parameters.AddWithValue("$roleId", policy.RoleId);
        cmd.Parameters.AddWithValue("$mustReport", string.Join(",", policy.MustReportEvents));
        cmd.Parameters.AddWithValue("$suppress", string.Join(",", policy.ShouldSuppressEvents));
        cmd.Parameters.AddWithValue("$respondTo", string.Join(",", policy.CanRespondToRoles));
        cmd.Parameters.AddWithValue("$commandRoles", string.Join(",", policy.CanCommandRoles));
        cmd.Parameters.AddWithValue("$requiresApproval", policy.RequiresHumanApprovalBeforeAction);
        cmd.Parameters.AddWithValue("$canCreate", policy.CanCreateTasks);
        cmd.Parameters.AddWithValue("$canMove", policy.CanMoveTickets);
        cmd.Parameters.AddWithValue("$canStop", policy.CanStopRuns);
        cmd.Parameters.AddWithValue("$tone", policy.DefaultTone);
        cmd.Parameters.AddWithValue("$created", policy.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EnsureDefaultPoliciesAsync(string projectSlug, CancellationToken ct = default)
    {
        var existing = await GetAllRolePoliciesAsync(projectSlug, ct);
        var existingRoles = existing.Select(p => p.RoleId).ToHashSet();

        foreach (var (roleId, defaultPolicy) in DefaultRoleChatPolicies.Defaults)
        {
            if (!existingRoles.Contains(roleId))
            {
                var policy = new AgentRoleChatPolicy
                {
                    ProjectSlug = projectSlug,
                    RoleId = roleId,
                    MustReportEvents = defaultPolicy.MustReportEvents,
                    ShouldSuppressEvents = defaultPolicy.ShouldSuppressEvents,
                    CanRespondToRoles = defaultPolicy.CanRespondToRoles,
                    CanCommandRoles = defaultPolicy.CanCommandRoles,
                    RequiresHumanApprovalBeforeAction = defaultPolicy.RequiresHumanApprovalBeforeAction,
                    CanCreateTasks = defaultPolicy.CanCreateTasks,
                    CanMoveTickets = defaultPolicy.CanMoveTickets,
                    CanStopRuns = defaultPolicy.CanStopRuns,
                    DefaultTone = defaultPolicy.DefaultTone
                };
                await UpsertRolePolicyAsync(policy, ct);
            }
        }
    }

    private static AgentChatProfile ReadProfile(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        ProjectSlug = r.GetString(r.GetOrdinal("ProjectSlug")),
        AgentId = r.GetString(r.GetOrdinal("AgentId")),
        RoleId = r.GetString(r.GetOrdinal("RoleId")),
        DisplayName = r.GetString(r.GetOrdinal("DisplayName")),
        RoleName = r.GetString(r.GetOrdinal("RoleName")),
        CanReadTeamChat = r.GetBoolean(r.GetOrdinal("CanReadTeamChat")),
        CanPostToTeamChat = r.GetBoolean(r.GetOrdinal("CanPostToTeamChat")),
        CanReplyToAgents = r.GetBoolean(r.GetOrdinal("CanReplyToAgents")),
        CanAskHuman = r.GetBoolean(r.GetOrdinal("CanAskHuman")),
        CanReceiveDirectMentions = r.GetBoolean(r.GetOrdinal("CanReceiveDirectMentions")),
        CanReceiveTeamMentions = r.GetBoolean(r.GetOrdinal("CanReceiveTeamMentions")),
        Verbosity = r.GetString(r.GetOrdinal("Verbosity")),
        SignalPolicy = r.GetString(r.GetOrdinal("SignalPolicy")),
        ResponsePolicy = r.GetString(r.GetOrdinal("ResponsePolicy")),
        SystemPromptAddon = r.IsDBNull(r.GetOrdinal("SystemPromptAddon")) ? null : r.GetString(r.GetOrdinal("SystemPromptAddon")),
        ChatRulesMarkdown = r.IsDBNull(r.GetOrdinal("ChatRulesMarkdown")) ? null : r.GetString(r.GetOrdinal("ChatRulesMarkdown")),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt")))
    };

    private static AgentRoleChatPolicy ReadPolicy(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        ProjectSlug = r.GetString(r.GetOrdinal("ProjectSlug")),
        RoleId = r.GetString(r.GetOrdinal("RoleId")),
        MustReportEvents = r.GetString(r.GetOrdinal("MustReportEvents")).Split(',', StringSplitOptions.RemoveEmptyEntries),
        ShouldSuppressEvents = r.GetString(r.GetOrdinal("ShouldSuppressEvents")).Split(',', StringSplitOptions.RemoveEmptyEntries),
        CanRespondToRoles = r.GetString(r.GetOrdinal("CanRespondToRoles")).Split(',', StringSplitOptions.RemoveEmptyEntries),
        CanCommandRoles = r.GetString(r.GetOrdinal("CanCommandRoles")).Split(',', StringSplitOptions.RemoveEmptyEntries),
        RequiresHumanApprovalBeforeAction = r.GetBoolean(r.GetOrdinal("RequiresHumanApprovalBeforeAction")),
        CanCreateTasks = r.GetBoolean(r.GetOrdinal("CanCreateTasks")),
        CanMoveTickets = r.GetBoolean(r.GetOrdinal("CanMoveTickets")),
        CanStopRuns = r.GetBoolean(r.GetOrdinal("CanStopRuns")),
        DefaultTone = r.GetString(r.GetOrdinal("DefaultTone")),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt")))
    };

    private static async Task EnsureTablesAsync(string dbPath)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AgentChatProfiles (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectSlug TEXT NOT NULL,
                AgentId TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                DisplayName TEXT NOT NULL DEFAULT '',
                RoleName TEXT NOT NULL DEFAULT '',
                CanReadTeamChat INTEGER NOT NULL DEFAULT 1,
                CanPostToTeamChat INTEGER NOT NULL DEFAULT 1,
                CanReplyToAgents INTEGER NOT NULL DEFAULT 1,
                CanAskHuman INTEGER NOT NULL DEFAULT 1,
                CanReceiveDirectMentions INTEGER NOT NULL DEFAULT 1,
                CanReceiveTeamMentions INTEGER NOT NULL DEFAULT 1,
                Verbosity TEXT NOT NULL DEFAULT 'normal',
                SignalPolicy TEXT NOT NULL DEFAULT 'important-only',
                ResponsePolicy TEXT NOT NULL DEFAULT 'when-addressed-or-relevant',
                SystemPromptAddon TEXT NULL,
                ChatRulesMarkdown TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AgentRoleChatPolicies (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectSlug TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                MustReportEvents TEXT NOT NULL DEFAULT '',
                ShouldSuppressEvents TEXT NOT NULL DEFAULT '',
                CanRespondToRoles TEXT NOT NULL DEFAULT '',
                CanCommandRoles TEXT NOT NULL DEFAULT '',
                RequiresHumanApprovalBeforeAction INTEGER NOT NULL DEFAULT 0,
                CanCreateTasks INTEGER NOT NULL DEFAULT 0,
                CanMoveTickets INTEGER NOT NULL DEFAULT 0,
                CanStopRuns INTEGER NOT NULL DEFAULT 0,
                DefaultTone TEXT NOT NULL DEFAULT 'concise',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS TeamChatMentions (
                Id TEXT NOT NULL PRIMARY KEY,
                MessageId TEXT NOT NULL,
                ProjectSlug TEXT NOT NULL,
                MentionType TEXT NOT NULL,
                MentionValue TEXT NOT NULL,
                RequiresResponse INTEGER NOT NULL DEFAULT 0,
                IsResolved INTEGER NOT NULL DEFAULT 0,
                ResponseMessageId TEXT NULL,
                CreatedAt TEXT NOT NULL,
                ResolvedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS AgentChatInstructions (
                Id TEXT NOT NULL PRIMARY KEY,
                MessageId TEXT NOT NULL,
                ProjectSlug TEXT NOT NULL,
                AgentId TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                InstructionType TEXT NOT NULL,
                Body TEXT NOT NULL DEFAULT '',
                TicketId INTEGER NULL,
                RunId TEXT NULL,
                Status TEXT NOT NULL DEFAULT 'pending',
                Reason TEXT NULL,
                CreatedAt TEXT NOT NULL,
                DeliveredAt TEXT NULL,
                CompletedAt TEXT NULL
            );
        """;
        await cmd.ExecuteNonQueryAsync();
    }
}

public interface IAgentChatPolicyService
{
    Task<AgentChatProfile?> GetProfileAsync(string projectSlug, string agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentChatProfile>> GetAllProfilesAsync(string projectSlug, CancellationToken ct = default);
    Task UpsertProfileAsync(AgentChatProfile profile, CancellationToken ct = default);
    Task<AgentRoleChatPolicy?> GetRolePolicyAsync(string projectSlug, string roleId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRoleChatPolicy>> GetAllRolePoliciesAsync(string projectSlug, CancellationToken ct = default);
    Task UpsertRolePolicyAsync(AgentRoleChatPolicy policy, CancellationToken ct = default);
    Task EnsureDefaultPoliciesAsync(string projectSlug, CancellationToken ct = default);
}
