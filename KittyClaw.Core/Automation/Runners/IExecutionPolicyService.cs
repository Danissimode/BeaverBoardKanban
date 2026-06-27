using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KittyClaw.Core.Automation.Runners;

/// <summary>
/// Interface for execution policy decisions.
/// This is a stable extension point that allows adding policy logic without modifying core.
/// </summary>
public interface IExecutionPolicyService
{
    /// <summary>
    /// Check if direct execution is allowed for a ticket
    /// </summary>
    Task<PolicyDecision> CanExecuteDirectAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        string? provider = null,
        string? model = null,
        IReadOnlyList<string>? labels = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a ticket can be moved to Done
    /// </summary>
    Task<PolicyDecision> CanMoveToDoneAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        AgentRunStatus runStatus,
        ExecutionMetadata? executionMetadata = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if worktree is required for execution
    /// </summary>
    Task<bool> IsWorktreeRequiredAsync(
        string projectSlug, 
        int ticketId, 
        ExecutionMode executionMode,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if CAO governance is required
    /// </summary>
    Task<bool> IsCaoRequiredAsync(
        string projectSlug, 
        int ticketId, 
        IReadOnlyList<string>? labels = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get allowed providers for a project
    /// </summary>
    Task<IReadOnlyList<string>> GetAllowedProvidersAsync(
        string projectSlug, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get allowed models for a provider
    /// </summary>
    Task<IReadOnlyList<string>> GetAllowedModelsAsync(
        string projectSlug, 
        string provider,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Policy decision result
/// </summary>
public sealed class PolicyDecision
{
    public required bool Allowed { get; init; }
    public string? Reason { get; init; }
    public string? RequiredAction { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    
    public static PolicyDecision Allow() => new() { Allowed = true };
    public static PolicyDecision Deny(string reason, string? requiredAction = null) => 
        new() { Allowed = false, Reason = reason, RequiredAction = requiredAction };
}
