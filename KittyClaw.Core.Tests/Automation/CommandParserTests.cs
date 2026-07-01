using KittyClaw.Core.Automation.CommandHub;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Theory]
    [InlineData("status", "status", "low")]
    [InlineData("health", "health", "low")]
    [InlineData("report", "report", "low")]
    [InlineData("backlog", "backlog_next", "low")]
    [InlineData("tree BB-100", "tree", "low")]
    [InlineData("start BB-142", "start_ticket", "high")]
    [InlineData("run BB-142", "start_ticket", "high")]
    [InlineData("stop", "stop_run", "high")]
    [InlineData("decompose BB-100", "decompose", "medium")]
    [InlineData("split BB-100", "decompose", "medium")]
    public void Parse_DeterministicCommands(string input, string expectedType, string expectedRisk)
    {
        var intent = _parser.Parse("test-project", "msg-1", input);
        
        Assert.Equal(expectedType, intent.Type);
        Assert.Equal(expectedRisk, intent.Risk);
        Assert.Equal(1.0, intent.Confidence);
    }

    [Theory]
    [InlineData("дай отчёт по проекту", "report")]
    [InlineData("что сейчас в работе?", "status")]
    [InlineData("какие задачи зависли?", "health")]
    [InlineData("покажи ошибки", "health")]
    [InlineData("запусти BB-142", "start_ticket")]
    [InlineData("разбей BB-100 на подзадачи", "decompose")]
    [InlineData("останови все run", "stop_run")]
    public void Parse_NaturalLanguage(string input, string expectedType)
    {
        var intent = _parser.Parse("test-project", "msg-1", input);
        
        Assert.Equal(expectedType, intent.Type);
        Assert.True(intent.Confidence >= 0.5 && intent.Confidence <= 0.9);
    }

    [Fact]
    public void Parse_StatusCommand_RequiresNoApproval()
    {
        var intent = _parser.Parse("test-project", "msg-1", "status");
        Assert.False(intent.RequiresApproval);
    }

    [Fact]
    public void Parse_StartCommand_RequiresApproval()
    {
        var intent = _parser.Parse("test-project", "msg-1", "start BB-142");
        Assert.True(intent.RequiresApproval);
    }

    [Fact]
    public void Parse_BacklogExtractsCount()
    {
        var intent = _parser.Parse("test-project", "msg-1", "backlog 3");
        Assert.Contains("3", intent.ParametersJson ?? "");
    }

    [Fact]
    public void Parse_TreeExtractsTicketId()
    {
        var intent = _parser.Parse("test-project", "msg-1", "tree BB-100");
        Assert.Contains("100", intent.ParametersJson ?? "");
    }

    [Fact]
    public void Parse_UnknownCommand_LowConfidence()
    {
        var intent = _parser.Parse("test-project", "msg-1", "xyzzy foobar");
        Assert.Equal("unknown", intent.Type);
        Assert.True(intent.Confidence < 0.5);
    }

    [Fact]
    public void Parse_WithMention_ExtractsTarget()
    {
        var intent = _parser.Parse("test-project", "msg-1", "@orchestrator status");
        Assert.Equal("status", intent.Type);
    }
}
