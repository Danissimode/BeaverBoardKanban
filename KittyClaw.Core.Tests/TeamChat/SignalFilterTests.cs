using KittyClaw.Core.TeamChat;
using Xunit;

namespace KittyClaw.Core.Tests.TeamChat;

public class SignalFilterTests
{
    private readonly AgentChatSignalFilter _filter = new();

    private AgentChatProfile CreateProfile(string signalPolicy = "important-only") => new()
    {
        ProjectSlug = "test",
        AgentId = "builder",
        RoleId = "builder",
        SignalPolicy = signalPolicy
    };

    private AgentRoleChatPolicy CreatePolicy(string[]? mustReport = null, string[]? suppress = null) => new()
    {
        ProjectSlug = "test",
        RoleId = "builder",
        MustReportEvents = mustReport ?? [],
        ShouldSuppressEvents = suppress ?? ["stdout-line", "debug-log", "tool-call-start"]
    };

    [Fact]
    public void ShouldPublish_RunStarted_ImportantSignal()
    {
        var signal = new AgentChatSignal { SignalType = "run-started", AgentId = "builder" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.True(decision.ShouldPublish);
    }

    [Fact]
    public void ShouldPublish_NeedsHuman_ImportantSignal()
    {
        var signal = new AgentChatSignal { SignalType = "needs-human", AgentId = "builder" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.True(decision.ShouldPublish);
    }

    [Fact]
    public void ShouldSuppress_StdoutLine_NoisySignal()
    {
        var signal = new AgentChatSignal { SignalType = "stdout-line", AgentId = "builder" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.False(decision.ShouldPublish);
        // stdout-line is in policy's suppress list, so it's suppressed by role-policy
        Assert.Equal("role-policy", decision.SuppressedBy);
    }

    [Fact]
    public void ShouldSuppress_DebugLog_NoisySignal()
    {
        var signal = new AgentChatSignal { SignalType = "debug-log", AgentId = "builder" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.False(decision.ShouldPublish);
    }

    [Fact]
    public void ShouldPublish_MustReportEvent_AlwaysPublishes()
    {
        var signal = new AgentChatSignal { SignalType = "blocker", AgentId = "builder" };
        var policy = CreatePolicy(mustReport: ["blocker", "scope-change"]);
        var decision = _filter.ShouldPublish(signal, CreateProfile(), policy);
        Assert.True(decision.ShouldPublish);
        Assert.Equal("Must-report event", decision.Reason);
    }

    [Fact]
    public void ShouldSuppress_SuppressedByPolicy()
    {
        var signal = new AgentChatSignal { SignalType = "file-read", AgentId = "builder" };
        var policy = CreatePolicy(suppress: ["file-read", "tool-call-start"]);
        var decision = _filter.ShouldPublish(signal, CreateProfile(), policy);
        Assert.False(decision.ShouldPublish);
        Assert.Equal("role-policy", decision.SuppressedBy);
    }

    [Fact]
    public void ShouldSuppress_SilentAgent()
    {
        var signal = new AgentChatSignal { SignalType = "run-completed", AgentId = "builder" };
        var profile = CreateProfile(signalPolicy: "silent");
        var decision = _filter.ShouldPublish(signal, profile, CreatePolicy());
        Assert.False(decision.ShouldPublish);
        Assert.Equal("signal-policy", decision.SuppressedBy);
    }

    [Fact]
    public void ShouldPublish_VerboseMode_PublishesNoisy()
    {
        var signal = new AgentChatSignal { SignalType = "stdout-line", AgentId = "builder" };
        var profile = CreateProfile(signalPolicy: "verbose");
        var decision = _filter.ShouldPublish(signal, profile, CreatePolicy());
        Assert.True(decision.ShouldPublish);
        Assert.Equal("Verbose mode", decision.Reason);
    }

    [Fact]
    public void ShouldPublish_TestFailed_ImportantSignal()
    {
        var signal = new AgentChatSignal { SignalType = "test-failed", AgentId = "tester" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.True(decision.ShouldPublish);
    }

    [Fact]
    public void ShouldPublish_PlanReady_ImportantSignal()
    {
        var signal = new AgentChatSignal { SignalType = "plan-ready", AgentId = "planner" };
        var decision = _filter.ShouldPublish(signal, CreateProfile(), CreatePolicy());
        Assert.True(decision.ShouldPublish);
    }
}
