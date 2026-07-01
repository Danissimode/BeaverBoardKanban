using Microsoft.EntityFrameworkCore;
using KittyClaw.Core.Models;
using KittyClaw.Core.TeamChat;

namespace KittyClaw.Core.Data;

public class TodoDbContext : DbContext
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<ChatMessageRow> ChatMessages => Set<ChatMessageRow>();
    public DbSet<TeamChatMessage> TeamChatMessages => Set<TeamChatMessage>();
    public DbSet<TeamChatThread> TeamChatThreads => Set<TeamChatThread>();

    private readonly string _dbPath;

    public TodoDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}")
            .AddInterceptors(new SqliteConcurrencyInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasMany(t => t.Comments).WithOne(c => c.Ticket).HasForeignKey(c => c.TicketId);
            e.HasMany(t => t.Activities).WithOne(a => a.Ticket).HasForeignKey(a => a.TicketId);
            e.HasMany(t => t.Labels).WithMany(l => l.Tickets).UsingEntity("TicketLabels");
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.HasKey(c => c.Id);
        });

        modelBuilder.Entity<ActivityEntry>(e =>
        {
            e.HasKey(a => a.Id);
        });

        modelBuilder.Entity<Label>(e =>
        {
            e.HasKey(l => l.Id);
        });

        modelBuilder.Entity<BoardColumn>(e =>
        {
            e.HasKey(c => c.Id);
        });

        modelBuilder.Entity<Member>(e =>
        {
            e.HasKey(m => m.Id);
        });

        modelBuilder.Entity<ChatMessageRow>(e =>
        {
            e.HasKey(m => m.Id);
        });

        modelBuilder.Entity<TeamChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.ProjectSlug, m.CreatedAt });
            e.HasIndex(m => m.TicketId);
            e.HasIndex(m => m.DeliveryStatus);
        });

        modelBuilder.Entity<TeamChatThread>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.ProjectSlug, t.ThreadType });
        });
    }

    public static async Task EnsureTeamChatTablesAsync(string dbPath)
    {
        using var db = new TodoDbContext(dbPath);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS TeamChatMessages (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectSlug TEXT NOT NULL,
                TicketId INTEGER NULL,
                RunId TEXT NULL,
                AuthorType TEXT NOT NULL DEFAULT 'human',
                AuthorId TEXT NOT NULL,
                Body TEXT NOT NULL DEFAULT '',
                MessageType TEXT NOT NULL DEFAULT 'message',
                TargetType TEXT NOT NULL DEFAULT 'team',
                TargetId TEXT NULL,
                DeliveryStatus TEXT NOT NULL DEFAULT 'open',
                CreatedAt TEXT NOT NULL,
                ResolvedAt TEXT NULL,
                ResolvedBy TEXT NULL,
                CorrelationId TEXT NULL,
                MetadataJson TEXT NULL
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS TeamChatThreads (
                Id TEXT NOT NULL PRIMARY KEY,
                ProjectSlug TEXT NOT NULL,
                ThreadType TEXT NOT NULL DEFAULT 'team',
                TicketId INTEGER NULL,
                RunId TEXT NULL,
                RoleId TEXT NULL,
                Title TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL DEFAULT 'open',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatMessages_Project_Created
            ON TeamChatMessages (ProjectSlug, CreatedAt)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatMessages_TicketId
            ON TeamChatMessages (TicketId)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatMessages_DeliveryStatus
            ON TeamChatMessages (DeliveryStatus)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatThreads_Project_Type
            ON TeamChatThreads (ProjectSlug, ThreadType)
        """);
        await db.Database.ExecuteSqlRawAsync("""
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
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
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
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
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
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
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
            )
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatMentions_Message
            ON TeamChatMentions (MessageId)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_TeamChatMentions_Project_Type
            ON TeamChatMentions (ProjectSlug, MentionType)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_AgentChatProfiles_Project_Agent
            ON AgentChatProfiles (ProjectSlug, AgentId)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_AgentRoleChatPolicies_Project_Role
            ON AgentRoleChatPolicies (ProjectSlug, RoleId)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_AgentChatInstructions_Project_Agent
            ON AgentChatInstructions (ProjectSlug, AgentId)
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_AgentChatInstructions_Status
            ON AgentChatInstructions (Status)
        """);
    }
}
