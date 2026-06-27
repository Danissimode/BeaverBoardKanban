using System;
using System.Text.Json;
using KittyClaw.Core.Automation;
using Xunit;

namespace KittyClaw.Core.Tests.Automation;

public class AgentRunExecutionMetadataTests
{
    [Fact]
    public void AgentRun_WithExecutionMetadata_SerializesCorrectly()
    {
        var executionMetadata = new ExecutionMetadata
        {
            Mode = "DirectOpenCode",
            Runner = "opencode",
            Provider = "openrouter",
            Model = "qwen/qwen3.5-coder",
            Profile = "developer",
            RunId = "run_123",
            SessionId = "opencode_abc",
            WorktreePath = ".worktrees/KC-42",
            BranchName = "kc/KC-42",
            TicketId = "42",
            ProjectId = "test-project"
        };
        
        var run = new AgentRun
        {
            RunId = "test-run",
            ProjectSlug = "test-project",
            TicketId = 42,
            AgentName = "programmer",
            SkillFile = "programmer/SKILL.md",
            ConcurrencyGroup = "code",
            StartedAt = DateTime.UtcNow,
            Status = AgentRunStatus.Completed,
            ExecutionMetadata = executionMetadata
        };
        
        // Test that ExecutionMetadata is preserved
        Assert.NotNull(run.ExecutionMetadata);
        Assert.Equal("DirectOpenCode", run.ExecutionMetadata.Mode);
        Assert.Equal("opencode", run.ExecutionMetadata.Runner);
        Assert.Equal("openrouter", run.ExecutionMetadata.Provider);
        Assert.Equal("qwen/qwen3.5-coder", run.ExecutionMetadata.Model);
        Assert.Equal("developer", run.ExecutionMetadata.Profile);
        Assert.Equal("run_123", run.ExecutionMetadata.RunId);
        Assert.Equal("opencode_abc", run.ExecutionMetadata.SessionId);
        Assert.Equal(".worktrees/KC-42", run.ExecutionMetadata.WorktreePath);
        Assert.Equal("kc/KC-42", run.ExecutionMetadata.BranchName);
        Assert.Equal("42", run.ExecutionMetadata.TicketId);
        Assert.Equal("test-project", run.ExecutionMetadata.ProjectId);
    }
    
    [Fact]
    public void AgentRunSnapshot_WithExecutionMetadata_SerializesCorrectly()
    {
        var executionMetadata = new ExecutionMetadata
        {
            Mode = "DirectOpenCode",
            Runner = "opencode",
            Provider = "openrouter",
            Model = "qwen/qwen3.5-coder",
            Profile = "developer",
            RunId = "run_123",
            SessionId = "opencode_abc",
            WorktreePath = ".worktrees/KC-42",
            BranchName = "kc/KC-42"
        };
        
        var snapshot = new AgentRunSnapshot
        {
            RunId = "test-run",
            ProjectSlug = "test-project",
            TicketId = 42,
            AgentName = "programmer",
            StartedAt = DateTime.UtcNow,
            Status = AgentRunStatus.Completed,
            ExecutionMetadata = executionMetadata
        };
        
        var json = JsonSerializer.Serialize(snapshot);
        var deserialized = JsonSerializer.Deserialize<AgentRunSnapshot>(json);
        
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.ExecutionMetadata);
        Assert.Equal("DirectOpenCode", deserialized.ExecutionMetadata.Mode);
        Assert.Equal("opencode", deserialized.ExecutionMetadata.Runner);
        Assert.Equal("openrouter", deserialized.ExecutionMetadata.Provider);
        Assert.Equal("qwen/qwen3.5-coder", deserialized.ExecutionMetadata.Model);
    }
    
    [Fact]
    public void AgentRun_WithoutExecutionMetadata_HasNullMetadata()
    {
        var run = new AgentRun
        {
            RunId = "test-run",
            ProjectSlug = "test-project",
            TicketId = 42,
            AgentName = "programmer",
            SkillFile = "programmer/SKILL.md",
            ConcurrencyGroup = "code",
            StartedAt = DateTime.UtcNow,
            Status = AgentRunStatus.Completed,
            ExecutionMetadata = null
        };
        
        Assert.Null(run.ExecutionMetadata);
    }
}
