using System.Text.Json;
using KittyClaw.Core.Automation;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class RunAgentActionSpecTests
{
    [Fact]
    public void RunAgentActionSpec_WithNewFields_SerializesCorrectly()
    {
        var spec = new RunAgentActionSpec
        {
            Agent = "programmer",
            MaxTurns = 50,
            ConcurrencyGroup = "code",
            ExecutionMode = ExecutionMode.DirectOpenCode,
            Provider = "opencode",
            Profile = "developer",
            Model = "deepseek-v4-pro",
            UseEffectiveConfig = false
        };
        
        var json = JsonSerializer.Serialize(spec);
        var deserialized = JsonSerializer.Deserialize<RunAgentActionSpec>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal("programmer", deserialized.Agent);
        Assert.Equal(50, deserialized.MaxTurns);
        Assert.Equal("code", deserialized.ConcurrencyGroup);
        Assert.Equal(ExecutionMode.DirectOpenCode, deserialized.ExecutionMode);
        Assert.Equal("opencode", deserialized.Provider);
        Assert.Equal("developer", deserialized.Profile);
        Assert.Equal("deepseek-v4-pro", deserialized.Model);
        Assert.False(deserialized.UseEffectiveConfig);
    }
    
    [Fact]
    public void RunAgentActionSpec_WithLegacyFields_SerializesCorrectly()
    {
        var spec = new RunAgentActionSpec
        {
            Agent = "programmer",
            MaxTurns = 200,
            Model = "claude-3-5-sonnet"
        };
        
        var json = JsonSerializer.Serialize(spec);
        var deserialized = JsonSerializer.Deserialize<RunAgentActionSpec>(json);
        
        Assert.NotNull(deserialized);
        Assert.Equal("programmer", deserialized.Agent);
        Assert.Equal(200, deserialized.MaxTurns);
        Assert.Equal("claude-3-5-sonnet", deserialized.Model);
        // New fields should have default values
        Assert.Null(deserialized.ExecutionMode);
        Assert.Null(deserialized.Provider);
        Assert.Null(deserialized.Profile);
        Assert.True(deserialized.UseEffectiveConfig);
    }
    
    [Fact]
    public void RunAgentActionSpec_UiTypeKey_ReturnsRunAgent()
    {
        var spec = new RunAgentActionSpec { Agent = "test" };
        Assert.Equal("runAgent", spec.UiTypeKey);
    }
}
