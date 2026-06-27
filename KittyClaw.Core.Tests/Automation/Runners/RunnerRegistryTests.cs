using System;
using System.Linq;
using KittyClaw.Core.Automation.Runners;
using Xunit;

namespace KittyClaw.Core.Tests.Automation.Runners;

public class RunnerRegistryTests
{
    [Fact]
    public void RegisterRunner_AddsRunnerToRegistry()
    {
        var registry = new RunnerRegistry();
        var mockRunner = new MockAgentRunner("test", "Test Runner");
        
        registry.RegisterRunner(mockRunner);
        
        var runner = registry.GetRunner("test");
        Assert.NotNull(runner);
        Assert.Equal("test", runner.Kind);
        Assert.Equal("Test Runner", runner.DisplayName);
    }
    
    [Fact]
    public void GetDefaultRunner_ReturnsFirstRegisteredRunner()
    {
        var registry = new RunnerRegistry();
        var runner1 = new MockAgentRunner("runner1", "Runner 1");
        var runner2 = new MockAgentRunner("runner2", "Runner 2");
        
        registry.RegisterRunner(runner1);
        registry.RegisterRunner(runner2);
        
        var defaultRunner = registry.GetDefaultRunner();
        Assert.Equal("runner1", defaultRunner.Kind);
    }
    
    [Fact]
    public void SetDefaultRunner_SetsExplicitDefault()
    {
        var registry = new RunnerRegistry();
        var runner1 = new MockAgentRunner("runner1", "Runner 1");
        var runner2 = new MockAgentRunner("runner2", "Runner 2");
        
        registry.RegisterRunner(runner1);
        registry.RegisterRunner(runner2);
        registry.SetDefaultRunner("runner2");
        
        var defaultRunner = registry.GetDefaultRunner();
        Assert.Equal("runner2", defaultRunner.Kind);
    }
    
    [Fact]
    public void GetAllRunners_ReturnsAllRegisteredRunners()
    {
        var registry = new RunnerRegistry();
        var runner1 = new MockAgentRunner("runner1", "Runner 1");
        var runner2 = new MockAgentRunner("runner2", "Runner 2");
        
        registry.RegisterRunner(runner1);
        registry.RegisterRunner(runner2);
        
        var allRunners = registry.GetAllRunners().ToList();
        Assert.Equal(2, allRunners.Count);
        Assert.Contains(runner1, allRunners);
        Assert.Contains(runner2, allRunners);
    }
    
    [Fact]
    public void GetAvailableRunners_ReturnsOnlyAvailableRunners()
    {
        var registry = new RunnerRegistry();
        var availableRunner = new MockAgentRunner("available", "Available Runner", true);
        var unavailableRunner = new MockAgentRunner("unavailable", "Unavailable Runner", false);
        
        registry.RegisterRunner(availableRunner);
        registry.RegisterRunner(unavailableRunner);
        
        var availableRunners = registry.GetAvailableRunners().ToList();
        Assert.Single(availableRunners);
        Assert.Equal("available", availableRunners[0].Kind);
    }
    
    [Fact]
    public void ResolveRunner_ByExecutionMode_ReturnsAppropriateRunner()
    {
        var registry = new RunnerRegistry();
        var claudeRunner = new MockAgentRunner("claude", "Claude Runner");
        var opencodeRunner = new MockAgentRunner("opencode", "OpenCode Runner");
        
        registry.RegisterRunner(claudeRunner);
        registry.RegisterRunner(opencodeRunner);
        
        var legacyRunner = registry.ResolveRunner(ExecutionMode.LegacyClaude);
        Assert.Equal("claude", legacyRunner.Kind);
        
        var directOpenCodeRunner = registry.ResolveRunner(ExecutionMode.DirectOpenCode);
        Assert.Equal("opencode", directOpenCodeRunner.Kind);
    }
    
    [Fact]
    public void ResolveRunner_ByKindAndMode_FallsBackToMode()
    {
        var registry = new RunnerRegistry();
        var claudeRunner = new MockAgentRunner("claude", "Claude Runner");
        var opencodeRunner = new MockAgentRunner("opencode", "OpenCode Runner");
        
        registry.RegisterRunner(claudeRunner);
        registry.RegisterRunner(opencodeRunner);
        
        // Request a runner kind that doesn't exist, should fall back to mode
        var runner = registry.ResolveRunner("nonexistent", ExecutionMode.DirectOpenCode);
        Assert.Equal("opencode", runner.Kind);
    }
    
    [Fact]
    public void ResolveRunner_WithUnavailableRunner_FallsBackToDefault()
    {
        var registry = new RunnerRegistry();
        var claudeRunner = new MockAgentRunner("claude", "Claude Runner");
        var unavailableRunner = new MockAgentRunner("opencode", "OpenCode Runner", false);
        
        registry.RegisterRunner(claudeRunner);
        registry.RegisterRunner(unavailableRunner);
        
        // Request DirectOpenCode but OpenCode runner is unavailable, should fall back to default
        var runner = registry.ResolveRunner(ExecutionMode.DirectOpenCode);
        Assert.Equal("claude", runner.Kind); // Falls back to default (claude)
    }
}

/// <summary>
/// Mock IAgentRunner for testing
/// </summary>
internal sealed class MockAgentRunner : IAgentRunner
{
    public string Kind { get; }
    public string DisplayName { get; }
    public bool IsAvailable { get; }
    
    public MockAgentRunner(string kind, string displayName, bool isAvailable = true)
    {
        Kind = kind;
        DisplayName = displayName;
        IsAvailable = isAvailable;
    }
    
    public Task<AgentRunResult> StartAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AgentRunResult
        {
            Status = AgentRunStatus.Completed,
            ExitCode = 0,
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            RunnerKind = Kind
        });
    }
    
    public Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
    
    public Task<bool> SteerAsync(string runId, string message, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
    
    public Task<AgentRunStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        return Task.FromResult(AgentRunStatus.Running);
    }
}
