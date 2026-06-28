using KittyClaw.Core.TeamChat;
using Xunit;

namespace KittyClaw.Core.Tests.TeamChat;

public class MentionParserTests
{
    private readonly TeamChatMentionParser _parser = new();

    [Fact]
    public void Parse_TeamMention_FindsTeam()
    {
        var mentions = _parser.Parse("@team check current blockers");
        Assert.Single(mentions);
        Assert.Equal("team", mentions[0].Type);
        Assert.Equal("team", mentions[0].Value);
        Assert.False(mentions[0].RequiresResponse);
    }

    [Fact]
    public void Parse_HumanMention_FindsHuman()
    {
        var mentions = _parser.Parse("@human need approval for KC-42");
        Assert.Single(mentions);
        Assert.Equal("human", mentions[0].Type);
        Assert.True(mentions[0].RequiresResponse);
    }

    [Fact]
    public void Parse_RoleMention_FindsRole()
    {
        var mentions = _parser.Parse("@reviewer check KC-42 for risks");
        Assert.Single(mentions);
        Assert.Equal("role", mentions[0].Type);
        Assert.Equal("reviewer", mentions[0].Value);
        Assert.True(mentions[0].RequiresResponse);
    }

    [Fact]
    public void Parse_MultipleRoles_FindsAll()
    {
        var mentions = _parser.Parse("@planner create plan, @builder implement it");
        Assert.Equal(2, mentions.Count);
        Assert.Equal("planner", mentions[0].Value);
        Assert.Equal("builder", mentions[1].Value);
    }

    [Fact]
    public void Parse_AgentMention_FindsAgent()
    {
        var mentions = _parser.Parse("@opencode_developer continue working");
        Assert.Single(mentions);
        Assert.Equal("agent", mentions[0].Type);
        Assert.Equal("opencode_developer", mentions[0].Value);
    }

    [Fact]
    public void Parse_TicketRef_FindsTicket()
    {
        var mentions = _parser.Parse("#KC-42 don't change migrations");
        Assert.Single(mentions);
        Assert.Equal("ticket", mentions[0].Type);
        Assert.Equal("KC-42", mentions[0].Value);
    }

    [Fact]
    public void Parse_RunRef_FindsRun()
    {
        var mentions = _parser.Parse("run:abc123 stop execution");
        Assert.Single(mentions);
        Assert.Equal("run", mentions[0].Type);
        Assert.Equal("abc123", mentions[0].Value);
    }

    [Fact]
    public void Parse_CombinedMentions_FindsAll()
    {
        var mentions = _parser.Parse("@team @reviewer check #KC-42 run:abc123");
        Assert.Equal(4, mentions.Count);
        Assert.Contains(mentions, m => m.Type == "team");
        Assert.Contains(mentions, m => m.Type == "role" && m.Value == "reviewer");
        Assert.Contains(mentions, m => m.Type == "ticket" && m.Value == "KC-42");
        Assert.Contains(mentions, m => m.Type == "run" && m.Value == "abc123");
    }

    [Fact]
    public void Parse_NoMentions_ReturnsEmpty()
    {
        var mentions = _parser.Parse("Hello, this is a normal message without mentions.");
        Assert.Empty(mentions);
    }

    [Fact]
    public void Parse_CaseInsensitive_FindsRoles()
    {
        var mentions = _parser.Parse("@REVIEWER check this");
        Assert.Single(mentions);
        Assert.Equal("reviewer", mentions[0].Value);
    }

    [Fact]
    public void ToMentions_ConvertsCorrectly()
    {
        var parsed = _parser.Parse("@builder implement #KC-42");
        var mentions = _parser.ToMentions("msg-1", "test-project", parsed);

        Assert.Equal(2, mentions.Count);
        Assert.Equal("msg-1", mentions[0].MessageId);
        Assert.Equal("test-project", mentions[0].ProjectSlug);
        Assert.Equal("role", mentions[0].MentionType);
        Assert.Equal("builder", mentions[0].MentionValue);
        Assert.Equal("ticket", mentions[1].MentionType);
        Assert.Equal("KC-42", mentions[1].MentionValue);
    }
}
