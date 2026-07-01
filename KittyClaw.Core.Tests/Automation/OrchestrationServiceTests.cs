using KittyClaw.Core.Automation.CommandHub;
using KittyClaw.Core.Automation.TeamRoles;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class OrchestrationServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly CommandHubService _commandHub;
    private readonly RoleInboxStore _inboxStore;
    private readonly TeamRoleStore _roleStore;
    private readonly MessageRouter _router;
    private readonly AgentSelector _agentSelector;
    private readonly RolePolicyEvaluator _policyEvaluator;
    private readonly OrchestrationService _orchestrator;

    public OrchestrationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"beaverboard-orch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _commandHub = new CommandHubService(_testDir);
        _inboxStore = new RoleInboxStore(_testDir);
        _roleStore = new TeamRoleStore(_testDir);
        _router = new MessageRouter(_inboxStore);
        _agentSelector = new AgentSelector(_roleStore);
        _policyEvaluator = new RolePolicyEvaluator(_roleStore);
        _orchestrator = new OrchestrationService(
            _commandHub, _inboxStore, _roleStore, _router,
            _agentSelector, _policyEvaluator);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { /* ignore */ }
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
    public async Task RouteUserMessage_DirectMention_MediatedPolicy_SuppressesVisibility()
    {
        var policy = new ConversationPolicy
        {
            ProjectSlug = "test-project",
            ReplyPolicy = ReplyPolicy.MediatedRoles
        };

        var routed = await _router.RouteUserMessageAsync(
            "test-project", "@programmer what's the status?", "owner", policy);

        Assert.Equal("programmer", routed.TargetRole);
        Assert.Equal(MessageVisibility.OrchestratorSummary, routed.Visibility);
    }

    [Fact]
    public async Task RouteRoleResponse_MediatedPolicy_SuppressesProgrammerToTeamActivity()
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
    public async Task DispatchTicket_CreatesInboxMessage()
    {
        // Arrange: create role inbox
        await _inboxStore.UpsertInboxAsync(new RoleInbox
        {
            ProjectSlug = "test-project",
            RoleId = "programmer",
            Name = "Programmer Inbox",
            ChatAddress = "@programmer"
        });

        // Act
        var message = await _orchestrator.DispatchTicketToRoleAsync(
            "test-project", 142, "programmer", "Fix BB-142");

        // Assert
        Assert.Equal(142, message.TicketId);
        Assert.Equal("Fix BB-142", message.Text);
        Assert.Equal("pending", message.Status);
    }

    [Fact]
    public async Task ClaimAndCreateSession_Atomic_PreventsDuplicateClaim()
    {
        // Arrange
        await _inboxStore.UpsertInboxAsync(new RoleInbox
        {
            ProjectSlug = "test-project",
            RoleId = "programmer",
            Name = "Programmer Inbox",
            ChatAddress = "@programmer"
        });

        var message = await _inboxStore.PostMessageAsync(new InboxMessage
        {
            ProjectSlug = "test-project",
            RoleInboxId = (await _inboxStore.GetInboxesAsync("test-project")).First().Id,
            TicketId = 142,
            Text = "Fix BB-142",
            PostedBy = "orchestrator"
        });

        // Act — first claim succeeds
        var result1 = await _inboxStore.ClaimAndCreateSessionAsync(
            "test-project", message.Id, "agent-1");

        // Assert
        Assert.True(result1.Success);
        Assert.NotNull(result1.Claim);
        Assert.NotNull(result1.Session);

        // Act — second claim fails
        var result2 = await _inboxStore.ClaimAndCreateSessionAsync(
            "test-project", message.Id, "agent-2");

        // Assert
        Assert.False(result2.Success);
        Assert.Contains("already claimed", result2.Reason);
    }

    [Fact]
    public async Task AgentSelector_RespectsMaxConcurrentRuns()
    {
        // Arrange
        await _roleStore.UpsertAgentAsync(new AgentProfile
        {
            ProjectSlug = "test-project",
            DisplayName = "programmer-1",
            RoleId = "programmer",
            MaxConcurrentRuns = 1,
            CurrentRunCount = 1,
            Status = "running"
        });

        // Act
        var result = await _agentSelector.SelectAgentAsync(
            "test-project", "programmer");

        // Assert
        Assert.Null(result.SelectedAgentId);
        Assert.Contains("no available agents", result.Reason);
    }

    [Fact]
    public async Task RolePolicyEvaluator_ProgrammerCannotMoveToDone()
    {
        var decision = await _policyEvaluator.CanAsync(
            "test-project", RoleSlugs.Programmer, Capabilities.TicketMoveDone);

        Assert.False(decision.Allowed);
        Assert.Contains("Validator approval required", decision.Reason);
    }

    [Fact]
    public async Task RolePolicyEvaluator_ValidatorCanMoveApprovedTicketToDone()
    {
        var decision = await _policyEvaluator.CanAsync(
            "test-project", RoleSlugs.Validator, Capabilities.TicketMoveDone,
            new RolePolicyContext { IsApproved = true });

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task RolePolicyEvaluator_ValidatorCannotMoveUnapprovedTicketToDone()
    {
        var decision = await _policyEvaluator.CanAsync(
            "test-project", RoleSlugs.Validator, Capabilities.TicketMoveDone,
            new RolePolicyContext { IsApproved = false });

        Assert.False(decision.Allowed);
        Assert.Contains("approved tickets", decision.Reason);
    }

    [Fact]
    public async Task TeamRoleStore_GetExecutionProfiles_ReturnsStoredProfiles()
    {
        // Arrange
        var profile = new ExecutionProfile
        {
            ProjectSlug = "test-project",
            Name = "Default OpenCode",
            Runtime = "opencode",
            Provider = "openai",
            Model = "gpt-4o"
        };
        await _roleStore.UpsertExecutionProfileAsync(profile);

        // Act
        var profiles = await _roleStore.GetExecutionProfilesAsync("test-project");

        // Assert
        Assert.Single(profiles);
        Assert.Equal("Default OpenCode", profiles[0].Name);
    }

    [Fact]
    public async Task TeamRoleStore_GetExecutionProfile_ById_ReturnsProfile()
    {
        // Arrange
        var profile = new ExecutionProfile
        {
            ProjectSlug = "test-project",
            Name = "Claude Profile",
            Runtime = "claude",
            Provider = "anthropic",
            Model = "claude-3-5-sonnet"
        };
        var created = await _roleStore.UpsertExecutionProfileAsync(profile);

        // Act
        var fetched = await _roleStore.GetExecutionProfileAsync("test-project", created.Id);

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal("Claude Profile", fetched.Name);
    }

    [Fact]
    public async Task OrchestrationService_PlanAsync_CreatesPlanWithRisk()
    {
        var plan = await _orchestrator.PlanAsync(
            "test-project", "conv-1", "msg-1", "start 3 backlog tasks", "owner");

        Assert.NotNull(plan);
        Assert.Equal("test-project", plan.ProjectSlug);
        Assert.Equal("start 3 backlog tasks", plan.Summary);
        Assert.Equal(CommandRiskLevels.High, plan.Risk);
        Assert.Equal(CommandPlanStatuses.PendingApproval, plan.Status);
    }

    [Fact]
    public async Task OrchestrationService_ApproveAndExecuteAsync_ApprovesPlan()
    {
        var plan = await _orchestrator.PlanAsync(
            "test-project", "conv-1", "msg-1", "start 3 backlog tasks", "owner");

        var result = await _orchestrator.ApproveAndExecuteAsync(
            "test-project", plan.Id, "owner");

        Assert.True(result.Success);
        Assert.Equal(CommandPlanStatuses.Executing, result.Status);
    }
}
