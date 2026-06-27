using System;
using System.Threading;
using System.Threading.Tasks;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.AI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KittyClaw.Core.Tests.Automation.AI;

public class AIProviderServiceTests
{
    private readonly AIProviderService _service;
    private readonly MockAIProvider _mockProvider;
    
    public AIProviderServiceTests()
    {
        var factory = new AIProviderFactory();
        _mockProvider = new MockAIProvider();
        factory.RegisterProvider(_mockProvider);
        
        _service = new AIProviderService(factory);
    }
    
    [Fact]
    public async Task ResolveEffectiveConfigAsync_WithNoConfigs_ReturnsLegacyClaude()
    {
        var result = await _service.ResolveEffectiveConfigAsync(
            "test-project",
            null,
            "test-agent",
            null, null, null, null,
            CancellationToken.None);
        
        Assert.Equal(ExecutionMode.LegacyClaude, result.ExecutionMode);
        Assert.Equal("claude", result.ProviderId);
        Assert.Equal("default", result.Profile);
    }
    
    [Fact]
    public async Task ResolveEffectiveConfigAsync_WithActionProvider_UsesActionProvider()
    {
        var action = new RunAgentActionSpec
        {
            Agent = "test-agent",
            Provider = "mock",
            Model = "test-model",
            Profile = "test-profile",
            UseEffectiveConfig = false
        };
        
        var result = await _service.ResolveFromActionAsync(
            action,
            "test-project",
            null,
            "test-agent",
            null, null, null, null,
            CancellationToken.None);
        
        Assert.Equal("mock", result.ProviderId);
        Assert.Equal("test-model", result.Model);
        Assert.Equal("test-profile", result.Profile);
    }
    
    [Fact]
    public async Task ResolveFromActionAsync_WithExplicitProvider_UsesProvider()
    {
        var action = new RunAgentActionSpec
        {
            Agent = "test-agent",
            Provider = "mock",
            Model = "explicit-model",
            Profile = "explicit-profile",
            UseEffectiveConfig = false
        };
        
        var result = await _service.ResolveFromActionAsync(
            action,
            "test-project",
            123,
            "test-agent",
            null, null, null, null,
            CancellationToken.None);
        
        Assert.Equal("mock", result.ProviderId);
        Assert.Equal("explicit-model", result.Model);
        Assert.Equal("explicit-profile", result.Profile);
        Assert.Equal("action", result.Source);
    }
    
    [Fact]
    public void GetAvailableProviders_ReturnsAllProviders()
    {
        var providers = _service.GetAvailableProviders();
        Assert.Contains(_mockProvider, providers);
    }
    
    [Fact]
    public void IsProviderAvailable_WithExistingProvider_ReturnsTrue()
    {
        Assert.True(_service.IsProviderAvailable("mock"));
    }
    
    [Fact]
    public void IsProviderAvailable_WithNonExistingProvider_ReturnsFalse()
    {
        Assert.False(_service.IsProviderAvailable("nonexistent"));
    }
}

// Mock provider for testing
internal sealed class MockAIProvider : IAIProvider
{
    public string Id => "mock";
    public string Name => "Mock Provider";
    public bool IsAvailable => true;
    
    public Task<AIProviderResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIProviderResult
        {
            Status = AgentRunStatus.Completed,
            ExitCode = 0,
            Stdout = "Mock output",
            Stderr = string.Empty,
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.Zero,
            ProviderId = Id,
            Model = request.Model ?? "mock-model",
            Profile = request.Profile ?? "mock-profile",
            RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
            CommandDisplay = "mock command"
        });
    }
    
    public Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
    
    public Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIProviderStatus
        {
            RunId = runId,
            Status = AgentRunStatus.Completed,
            ProviderId = Id
        });
    }
}
