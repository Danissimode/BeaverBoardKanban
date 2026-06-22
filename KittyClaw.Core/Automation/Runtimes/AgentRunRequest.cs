namespace KittyClaw.Core.Automation.Runtimes;

public sealed record AgentRunRequest(
    string ProjectSlug,
    string WorkspacePath,
    int? TicketId,
    string TicketTitle,
    string? TicketDescription,
    IReadOnlyList<string> Labels,
    string Assignee,
    string? CurrentColumn,
    string Prompt,
    AgentRuntimeConfig RuntimeConfig,
    string RuntimeId,
    string RoleId,
    CaoRoleConfig RoleConfig,
    string ModelProfileId,
    ModelProfileConfig ModelProfileConfig,
    string? RunId = null
);
