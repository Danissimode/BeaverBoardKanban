namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Role-level chat policy: what events to report, what to suppress, permissions.
/// </summary>
public sealed class AgentRoleChatPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string ProjectSlug { get; set; }
    public required string RoleId { get; set; }

    /// <summary>Events this role MUST report to team chat</summary>
    public string[] MustReportEvents { get; set; } = [];

    /// <summary>Events this role should suppress (not publish)</summary>
    public string[] ShouldSuppressEvents { get; set; } = [];

    /// <summary>Roles this agent can respond to</summary>
    public string[] CanRespondToRoles { get; set; } = [];

    /// <summary>Roles this agent can command/direct</summary>
    public string[] CanCommandRoles { get; set; } = [];

    /// <summary>Requires human approval before taking action</summary>
    public bool RequiresHumanApprovalBeforeAction { get; set; } = false;

    /// <summary>Can create new tasks/tickets</summary>
    public bool CanCreateTasks { get; set; } = false;

    /// <summary>Can move tickets between columns</summary>
    public bool CanMoveTickets { get; set; } = false;

    /// <summary>Can stop running agents</summary>
    public bool CanStopRuns { get; set; } = false;

    /// <summary>concise | detailed | minimal</summary>
    public string DefaultTone { get; set; } = "concise";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Default role chat policies.
/// </summary>
public static class DefaultRoleChatPolicies
{
    public static AgentRoleChatPolicy CreateDefault(string projectSlug, string roleId) => new()
    {
        ProjectSlug = projectSlug,
        RoleId = roleId
    };

    public static readonly Dictionary<string, AgentRoleChatPolicy> Defaults = new()
    {
        ["planner"] = new()
        {
            RoleId = "planner",
            ProjectSlug = "",
            MustReportEvents = ["plan-ready", "plan-approved", "scope-change", "blocker", "risk-found"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["builder", "reviewer", "tester"],
            CanCommandRoles = ["builder"],
            CanCreateTasks = true,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "concise"
        },
        ["builder"] = new()
        {
            RoleId = "builder",
            ProjectSlug = "",
            MustReportEvents = ["run-started", "run-completed", "run-failed", "blocker", "scope-change", "needs-human"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "tool-call-success", "file-read", "internal-reasoning"],
            CanRespondToRoles = ["planner", "reviewer", "tester"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "concise"
        },
        ["reviewer"] = new()
        {
            RoleId = "reviewer",
            ProjectSlug = "",
            MustReportEvents = ["review-requested", "review-completed", "risk-found", "blocker"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["builder", "planner"],
            CanCommandRoles = ["builder"],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "concise"
        },
        ["tester"] = new()
        {
            RoleId = "tester",
            ProjectSlug = "",
            MustReportEvents = ["test-failed", "test-passed", "blocker", "needs-human"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["builder", "reviewer"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "concise"
        },
        ["committer"] = new()
        {
            RoleId = "committer",
            ProjectSlug = "",
            MustReportEvents = ["run-completed", "blocker"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read", "minor-progress"],
            CanRespondToRoles = ["builder"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "minimal"
        },
        ["documentalist"] = new()
        {
            RoleId = "documentalist",
            ProjectSlug = "",
            MustReportEvents = ["run-completed", "needs-human"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["planner", "builder"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "minimal"
        },
        ["code-janitor"] = new()
        {
            RoleId = "code-janitor",
            ProjectSlug = "",
            MustReportEvents = ["run-completed"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read", "minor-progress"],
            CanRespondToRoles = ["builder", "reviewer"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "minimal"
        },
        ["evaluator"] = new()
        {
            RoleId = "evaluator",
            ProjectSlug = "",
            MustReportEvents = ["run-completed", "risk-found", "blocker"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["planner", "builder", "reviewer"],
            CanCommandRoles = [],
            CanCreateTasks = false,
            CanMoveTickets = false,
            CanStopRuns = false,
            DefaultTone = "concise"
        },
        ["producer"] = new()
        {
            RoleId = "producer",
            ProjectSlug = "",
            MustReportEvents = ["blocker", "needs-human", "provider-degraded", "worktree-failed", "closeout-blocked"],
            ShouldSuppressEvents = ["stdout-line", "debug-log", "tool-call-start", "file-read"],
            CanRespondToRoles = ["planner", "builder", "reviewer", "tester"],
            CanCommandRoles = ["planner", "builder", "reviewer", "tester"],
            CanCreateTasks = true,
            CanMoveTickets = true,
            CanStopRuns = true,
            DefaultTone = "concise"
        },
    };
}
