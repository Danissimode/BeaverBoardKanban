using System;
using KittyClaw.Core.Automation;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class ExecutionModeTests
{
    [Fact]
    public void ExecutionMode_EnumValues_AreCorrect()
    {
        // Test that all expected execution modes are present
        var allModes = Enum.GetValues(typeof(ExecutionMode));
        
        Assert.Contains(ExecutionMode.LegacyClaude, allModes);
        Assert.Contains(ExecutionMode.DirectOpenCode, allModes);
        Assert.Contains(ExecutionMode.CaoGoverned, allModes);
        Assert.Contains(ExecutionMode.TeamWorkflow, allModes);
        Assert.Contains(ExecutionMode.Manual, allModes);
    }
    
    [Fact]
    public void ExecutionMode_DefaultValue_IsLegacyClaude()
    {
        var defaultMode = default(ExecutionMode);
        Assert.Equal(ExecutionMode.LegacyClaude, defaultMode);
    }
    
    [Fact]
    public void ExecutionMode_ToString_ReturnsCorrectValues()
    {
        Assert.Equal("LegacyClaude", ExecutionMode.LegacyClaude.ToString());
        Assert.Equal("DirectOpenCode", ExecutionMode.DirectOpenCode.ToString());
        Assert.Equal("CaoGoverned", ExecutionMode.CaoGoverned.ToString());
        Assert.Equal("TeamWorkflow", ExecutionMode.TeamWorkflow.ToString());
        Assert.Equal("Manual", ExecutionMode.Manual.ToString());
    }
}
