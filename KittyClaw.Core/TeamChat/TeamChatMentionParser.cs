using System.Text.RegularExpressions;

namespace KittyClaw.Core.TeamChat;

/// <summary>
/// Parses mentions from team chat message bodies.
/// Extracts @team, @role, @agent, #ticket, run:id patterns.
/// </summary>
public partial class TeamChatMentionParser : ITeamChatMentionParser
{
    public IReadOnlyList<ParsedMention> Parse(string body)
    {
        var mentions = new List<ParsedMention>();

        // @team
        foreach (Match match in TeamMentionRegex().Matches(body))
        {
            mentions.Add(new ParsedMention
            {
                Type = "team",
                Value = "team",
                Raw = match.Value,
                RequiresResponse = false
            });
        }

        // @human
        foreach (Match match in HumanMentionRegex().Matches(body))
        {
            mentions.Add(new ParsedMention
            {
                Type = "human",
                Value = "human",
                Raw = match.Value,
                RequiresResponse = true
            });
        }

        // @role (planner, builder, reviewer, tester, sre, closeout, producer, evaluator, committer, documentalist, code-janitor)
        foreach (Match match in RoleMentionRegex().Matches(body))
        {
            var role = match.Groups[1].Value.ToLowerInvariant();
            mentions.Add(new ParsedMention
            {
                Type = "role",
                Value = role,
                Raw = match.Value,
                RequiresResponse = true
            });
        }

        // @agent-id (specific agent, not a role)
        foreach (Match match in AgentMentionRegex().Matches(body))
        {
            var agentId = match.Groups[1].Value;
            // Skip if it's a known role (already handled above)
            if (!IsKnownRole(agentId))
            {
                mentions.Add(new ParsedMention
                {
                    Type = "agent",
                    Value = agentId,
                    Raw = match.Value,
                    RequiresResponse = true
                });
            }
        }

        // #ticket (e.g., #KC-42, #42)
        foreach (Match match in TicketRefRegex().Matches(body))
        {
            var ticketRef = match.Groups[1].Value;
            mentions.Add(new ParsedMention
            {
                Type = "ticket",
                Value = ticketRef,
                Raw = match.Value,
                RequiresResponse = false
            });
        }

        // run:id
        foreach (Match match in RunRefRegex().Matches(body))
        {
            var runId = match.Groups[1].Value;
            mentions.Add(new ParsedMention
            {
                Type = "run",
                Value = runId,
                Raw = match.Value,
                RequiresResponse = false
            });
        }

        return mentions;
    }

    public IReadOnlyList<TeamChatMention> ToMentions(string messageId, string projectSlug, IReadOnlyList<ParsedMention> parsed)
    {
        return parsed.Select(p => new TeamChatMention
        {
            MessageId = messageId,
            ProjectSlug = projectSlug,
            MentionType = p.Type,
            MentionValue = p.Value,
            RequiresResponse = p.RequiresResponse
        }).ToList();
    }

    private static bool IsKnownRole(string id)
    {
        var knownRoles = new[] { "planner", "builder", "reviewer", "tester", "sre", "closeout",
            "producer", "evaluator", "committer", "documentalist", "code-janitor", "human", "team" };
        return knownRoles.Contains(id.ToLowerInvariant());
    }

    [GeneratedRegex(@"@team\b", RegexOptions.IgnoreCase)]
    private static partial Regex TeamMentionRegex();

    [GeneratedRegex(@"@human\b", RegexOptions.IgnoreCase)]
    private static partial Regex HumanMentionRegex();

    [GeneratedRegex(@"@(planner|builder|reviewer|tester|sre|closeout|producer|evaluator|committer|documentalist|code-janitor)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RoleMentionRegex();

    [GeneratedRegex(@"@(\w[\w-]*)\b")]
    private static partial Regex AgentMentionRegex();

    [GeneratedRegex(@"#(\w[\w-]*)")]
    private static partial Regex TicketRefRegex();

    [GeneratedRegex(@"run:(\w+)")]
    private static partial Regex RunRefRegex();
}

public interface ITeamChatMentionParser
{
    IReadOnlyList<ParsedMention> Parse(string body);
    IReadOnlyList<TeamChatMention> ToMentions(string messageId, string projectSlug, IReadOnlyList<ParsedMention> parsed);
}

public sealed class ParsedMention
{
    public string Type { get; init; } = "";
    public string Value { get; init; } = "";
    public string Raw { get; init; } = "";
    public bool RequiresResponse { get; init; }
}
