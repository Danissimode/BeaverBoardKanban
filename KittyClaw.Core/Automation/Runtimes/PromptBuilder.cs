namespace KittyClaw.Core.Automation.Runtimes;

public sealed class PromptBuilder : IAgentPromptBuilder
{
    public string BuildPrompt(AgentRunRequest request)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Beaver Board Agent Task");
        sb.AppendLine($"Project: {request.ProjectSlug}");
        sb.AppendLine($"Workspace: {request.WorkspacePath}");
        sb.AppendLine($"Ticket: #{request.TicketId}");
        sb.AppendLine($"Assignee: {request.Assignee}");
        sb.AppendLine($"CLI Runtime: {request.RuntimeId}");
        sb.AppendLine($"CAO Role: {request.RoleId}");
        sb.AppendLine($"Model Profile: {request.ModelProfileId}");
        sb.AppendLine($"Column: {request.CurrentColumn}");
        sb.AppendLine($"Labels: {string.Join(", ", request.Labels)}");
        sb.AppendLine("## Role Contract");
        sb.AppendLine($"You are acting as: {request.RoleId}");
        sb.AppendLine("Role permissions:");
        sb.AppendLine($"- Can edit files: {request.RoleConfig.CanEditFiles}");
        sb.AppendLine($"- Can run shell: {request.RoleConfig.CanRunShell}");
        sb.AppendLine($"- Can run tests: {request.RoleConfig.CanRunTests}");
        sb.AppendLine($"- Can use network: {request.RoleConfig.CanUseNetwork}");
        sb.AppendLine($"- Can approve: {request.RoleConfig.CanApprove}");
        sb.AppendLine($"- Can move to Verified: {request.RoleConfig.CanMoveToVerified}");
        sb.AppendLine($"- Can move to Done: {request.RoleConfig.CanMoveToDone}");
        if (request.RoleConfig.AllowedTools.Count > 0)
        {
            sb.AppendLine($"- Allowed tools: {string.Join(", ", request.RoleConfig.AllowedTools)}");
        }
        if (request.RoleConfig.PromptRules.Count > 0)
        {
            foreach (var rule in request.RoleConfig.PromptRules)
                sb.AppendLine($"- {rule}");
        }
        sb.AppendLine("## Model Contract");
        sb.AppendLine($"Model profile: {request.ModelProfileId}");
        sb.AppendLine($"Provider: {request.ModelProfileConfig.Provider}");
        sb.AppendLine($"Model: {request.ModelProfileConfig.Model}");
        sb.AppendLine("## Task Contract");
        sb.AppendLine(request.TicketDescription ?? "No description provided.");
        sb.AppendLine("## Mandatory Rules");
        sb.AppendLine("- Do not use Next.js.");
        sb.AppendLine("- Use the existing project stack.");
        sb.AppendLine("- Respect project governance.");
        sb.AppendLine("- Do not create duplicate architecture.");
        sb.AppendLine("- Do not auto-mark this task as Done.");
        sb.AppendLine("- For high-risk labels, produce evidence and request human review.");
        sb.AppendLine("- DESTRUCTIVE OPERATIONS: Before executing any destructive operation (rm -rf, git push --force, git reset --hard, DROP TABLE, DELETE without WHERE, rm -rf ~, modifying .env/secrets, changing API keys), STOP and ask for explicit human confirmation.");
        sb.AppendLine("- Never execute destructive commands automatically. Always explain the risk and wait for confirmation.");
        sb.AppendLine("## Required Final Output");
        sb.AppendLine("Return:");
        sb.AppendLine("1. role used;");
        sb.AppendLine("2. runtime used;");
        sb.AppendLine("3. model profile used;");
        sb.AppendLine("4. changed files;");
        sb.AppendLine("5. commands run;");
        sb.AppendLine("6. validation results;");
        sb.AppendLine("7. risks;");
        sb.AppendLine("8. recommended next status.");
        return sb.ToString();
    }
}
