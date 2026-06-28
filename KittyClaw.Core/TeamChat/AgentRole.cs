namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Defines an agent's role, capabilities, and communication behavior in team chat.
/// </summary>
public sealed class AgentRole
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Capabilities { get; set; } = [];
    public string[] CanCommunicateWith { get; set; } = [];
    public string[] DefaultChannels { get; set; } = ["team"];
    public bool CanRunAutonomously { get; set; } = true;
    public bool RequiresHumanApproval { get; set; } = false;
    public string[] OutputTypes { get; set; } = ["status", "question", "answer"];
    public string[] InputTypes { get; set; } = ["command", "question", "steer"];
}

/// <summary>
/// Predefined agent roles for the team.
/// </summary>
public static class AgentRoles
{
    public static readonly AgentRole Planner = new()
    {
        Id = "planner",
        Name = "Planner",
        Description = "Creates and maintains project plans, breaks down tasks, manages scope",
        Capabilities = ["plan", "scope", "estimate", "prioritize"],
        CanCommunicateWith = ["team", "builder", "reviewer", "tester"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "question", "plan-update"],
        InputTypes = ["command", "question", "scope-change"]
    };

    public static readonly AgentRole Builder = new()
    {
        Id = "builder",
        Name = "Builder",
        Description = "Implements features, writes code, fixes bugs",
        Capabilities = ["implement", "fix", "refactor", "test"],
        CanCommunicateWith = ["team", "planner", "reviewer", "tester"],
        DefaultChannels = ["team", "ticket"],
        OutputTypes = ["status", "question", "code-update", "blocker"],
        InputTypes = ["command", "question", "steer", "review-request"]
    };

    public static readonly AgentRole Reviewer = new()
    {
        Id = "reviewer",
        Name = "Reviewer",
        Description = "Reviews code, identifies risks, approves or requests changes",
        Capabilities = ["review", "approve", "request-changes", "security-audit"],
        CanCommunicateWith = ["team", "builder", "planner"],
        DefaultChannels = ["team", "ticket"],
        OutputTypes = ["status", "question", "review-comment", "approval", "risk-alert"],
        InputTypes = ["command", "question", "review-request"]
    };

    public static readonly AgentRole Tester = new()
    {
        Id = "tester",
        Name = "Tester",
        Description = "Writes and runs tests, validates functionality, reports bugs",
        Capabilities = ["test", "validate", "report-bug", "regression-test"],
        CanCommunicateWith = ["team", "builder", "reviewer"],
        DefaultChannels = ["team", "ticket"],
        OutputTypes = ["status", "question", "test-result", "bug-report"],
        InputTypes = ["command", "question", "test-request"]
    };

    public static readonly AgentRole Committer = new()
    {
        Id = "committer",
        Name = "Committer",
        Description = "Manages git commits, branches, and merges",
        Capabilities = ["commit", "merge", "branch", "resolve-conflicts"],
        CanCommunicateWith = ["team", "builder", "reviewer"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "commit-update", "merge-conflict"],
        InputTypes = ["command", "commit-request"]
    };

    public static readonly AgentRole Documentalist = new()
    {
        Id = "documentalist",
        Name = "Documentalist",
        Description = "Writes and maintains documentation, README, API docs",
        Capabilities = ["document", "update-readme", "api-docs"],
        CanCommunicateWith = ["team", "planner", "builder"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "doc-update", "question"],
        InputTypes = ["command", "question", "doc-request"]
    };

    public static readonly AgentRole CodeJanitor = new()
    {
        Id = "code-janitor",
        Name = "Code Janitor",
        Description = "Cleans up code, removes dead code, improves formatting",
        Capabilities = ["cleanup", "format", "remove-dead-code", "refactor"],
        CanCommunicateWith = ["team", "builder", "reviewer"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "cleanup-report"],
        InputTypes = ["command", "cleanup-request"]
    };

    public static readonly AgentRole Evaluator = new()
    {
        Id = "evaluator",
        Name = "Evaluator",
        Description = "Evaluates quality, performance, and provides metrics",
        Capabilities = ["evaluate", "measure", "benchmark", "report"],
        CanCommunicateWith = ["team", "planner", "builder", "reviewer"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "evaluation-report", "metric"],
        InputTypes = ["command", "question", "evaluation-request"]
    };

    public static readonly AgentRole Producer = new()
    {
        Id = "producer",
        Name = "Producer",
        Description = "Coordinates team activities, manages workflow, resolves blockers",
        Capabilities = ["coordinate", "unblock", "manage-workflow", "escalate"],
        CanCommunicateWith = ["team", "planner", "builder", "reviewer", "tester"],
        DefaultChannels = ["team"],
        OutputTypes = ["status", "directive", "blocker-resolution"],
        InputTypes = ["command", "question", "escalation"]
    };

    private static readonly Dictionary<string, AgentRole> _roles = new()
    {
        ["planner"] = Planner,
        ["builder"] = Builder,
        ["reviewer"] = Reviewer,
        ["tester"] = Tester,
        ["committer"] = Committer,
        ["documentalist"] = Documentalist,
        ["code-janitor"] = CodeJanitor,
        ["evaluator"] = Evaluator,
        ["producer"] = Producer,
    };

    public static AgentRole? GetRole(string agentId) =>
        _roles.TryGetValue(agentId.ToLowerInvariant(), out var role) ? role : null;

    public static IReadOnlyList<AgentRole> GetAllRoles() => _roles.Values.ToList();
}
