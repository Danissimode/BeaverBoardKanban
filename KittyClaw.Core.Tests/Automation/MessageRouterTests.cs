using KittyClaw.Core.Automation.TeamRoles;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class MessageRouterTests
{
    private readonly RoleInboxStore _inboxStore;
    private readonly MessageRouter _router;
    private readonly string _testDir;

    public MessageRouterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"beaverboard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _inboxStore = new RoleInboxStore(_testDir);
        _router = new MessageRouter(_inboxStore);
    }

    [Fact]
    public async Task RouteUserMessage_DefaultRoutesToOrchestrator()
    {
        var routed = await _router.RouteUserMessageAsync(
            "test-project", "give me status", "owner");

        Assert.Equal("orchestrator", routed.TargetRole);
        Assert.Equal(MessageVisibility.UserVisible, routed.Visibility);
    }

    [Fact]
    public async Task RouteUserMessage_DirectMentionRoutesToRole()
    {
        var routed = await _router.RouteUserMessageAsync(
            "test-project", "@programmer what's the status?", "owner");

        Assert.Equal("programmer", routed.TargetRole);
        Assert.True(routed.WasDirectMention);
    }

    [Fact]
    public async Task RouteUserMessage_DirectAgentMentionRoutesToRole()
    {
        var routed = await _router.RouteUserMessageAsync(
            "test-project", "@programmer-1 stop", "owner");

        Assert.Equal("programmer", routed.TargetRole);
        Assert.Equal("programmer-1", routed.TargetAgentId);
        Assert.True(routed.WasDirectMention);
    }

    [Fact]
    public async Task RouteUserMessage_MediatedPolicy_SuppressesNonOrchestratorVisibility()
    {
        var policy = new ConversationPolicy
        {
            ProjectSlug = "test-project",
            ReplyPolicy = ReplyPolicy.MediatedRoles
        };

        var routed = await _router.RouteUserMessageAsync(
            "test-project", "@programmer status", "owner", policy);

        Assert.Equal(MessageVisibility.OrchestratorSummary, routed.Visibility);
    }

    [Fact]
    public async Task RouteRoleResponse_OrchestratorAlwaysVisible()
    {
        var routed = await _router.RouteRoleResponseAsync(
            "test-project", "orchestrator", "orchestrator-main", "Status report", null);

        Assert.Equal(MessageVisibility.UserVisible, routed.Visibility);
    }

    [Fact]
    public async Task RouteRoleResponse_MediatedPolicy_SuppressesProgrammer()
    {
        var policy = new ConversationPolicy
        {
            ProjectSlug = "test-project",
            ReplyPolicy = ReplyPolicy.MediatedRoles
        };

        var routed = await _router.RouteRoleResponseAsync(
            "test-project", "programmer", "programmer-1", "BB-142 done", null, policy);

        Assert.Equal(MessageVisibility.TeamActivity, routed.Visibility);
    }

    [Fact]
    public async Task RouteRoleResponse_DebugPolicy_AllowsAll()
    {
        var policy = new ConversationPolicy
        {
            ProjectSlug = "test-project",
            ReplyPolicy = ReplyPolicy.DebugAllAgents
        };

        var routed = await _router.RouteRoleResponseAsync(
            "test-project", "programmer", "programmer-1", "BB-142 done", null, policy);

        Assert.Equal(MessageVisibility.UserVisible, routed.Visibility);
    }

    [Fact]
    public void CanRoleReplyToMain_OrchestratorAlwaysCan()
    {
        var policy = new ConversationPolicy { ProjectSlug = "test-project", ReplyPolicy = ReplyPolicy.OrchestratorOnly };
        Assert.True(_router.CanRoleReplyToMain("orchestrator", false, false, policy));
    }

    [Fact]
    public void CanRoleReplyToMain_ProgrammerNeedsDirectMention()
    {
        var policy = new ConversationPolicy { ProjectSlug = "test-project", ReplyPolicy = ReplyPolicy.MediatedRoles };
        Assert.False(_router.CanRoleReplyToMain("programmer", false, false, policy));
        Assert.True(_router.CanRoleReplyToMain("programmer", true, false, policy));
    }

    [Fact]
    public void CanRoleReplyToMain_DebugPolicyAllowsAll()
    {
        var policy = new ConversationPolicy { ProjectSlug = "test-project", ReplyPolicy = ReplyPolicy.DebugAllAgents };
        Assert.True(_router.CanRoleReplyToMain("programmer", false, false, policy));
    }
}
