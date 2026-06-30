using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KittyClaw.Core.Automation.Runners;
using KittyClaw.Core.Integrations.OpenCode;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.Runners;

/// <summary>
/// Report from a runner availability check
/// </summary>
public sealed record RunnerAvailabilityReport(
    string RunnerKind,
    string DisplayName,
    bool IsAvailable,
    bool IsDefault = false,
    string? Version = null,
    string? Provider = null,
    string? Model = null,
    string? Error = null,
    bool IsRecommended = false
);

/// <summary>
/// Checks availability of all registered runners.
/// Used by the UI to show runner status and by onboarding to suggest the best runner.
/// </summary>
public sealed class RunnerAvailabilityChecker
{
    private readonly RunnerRegistry _registry;
    private readonly ILogger<RunnerAvailabilityChecker>? _logger;

    public RunnerAvailabilityChecker(RunnerRegistry registry, ILogger<RunnerAvailabilityChecker>? logger = null)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Check all registered runners
    /// </summary>
    public async Task<IReadOnlyList<RunnerAvailabilityReport>> CheckAllAsync()
    {
        var reports = new List<RunnerAvailabilityReport>();
        var defaultRunner = _registry.GetDefaultRunner();
        
        foreach (var runner in _registry.GetAllRunners())
        {
            var report = await CheckAsync(runner);
            report = report with { IsDefault = runner.Kind == defaultRunner.Kind };
            reports.Add(report);
        }
        
        return reports;
    }

    /// <summary>
    /// Check a specific runner
    /// </summary>
    public async Task<RunnerAvailabilityReport> CheckAsync(IAgentRunner runner)
    {
        try
        {
            // OpenCode-specific deep check
            if (runner is OpenCodeRunner ocr)
            {
                var (available, version, provider, model) = await ocr.DeepCheckAsync();
                return new RunnerAvailabilityReport(
                    runner.Kind, 
                    runner.DisplayName, 
                    available,
                    Version: version,
                    Provider: provider,
                    Model: model,
                    IsRecommended: available // OpenCode is recommended when available
                );
            }

            // Claude - CLI presence is enough
            if (runner.Kind == "claude")
            {
                return new RunnerAvailabilityReport(
                    runner.Kind, 
                    runner.DisplayName, 
                    runner.IsAvailable,
                    IsRecommended: false // Claude is fallback
                );
            }

            // Generic runner
            return new RunnerAvailabilityReport(
                runner.Kind, 
                runner.DisplayName, 
                runner.IsAvailable
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Health check failed for {Runner}", runner.Kind);
            return new RunnerAvailabilityReport(
                runner.Kind, 
                runner.DisplayName, 
                false,
                Error: ex.Message
            );
        }
    }
    
    /// <summary>
    /// Get the recommended runner (first available, preferring OpenCode)
    /// </summary>
    public async Task<IAgentRunner?> GetRecommendedRunnerAsync()
    {
        var reports = await CheckAllAsync();
        
        // Prefer recommended runners
        foreach (var report in reports)
        {
            if (report.IsRecommended && report.IsAvailable)
            {
                return _registry.GetRunner(report.RunnerKind);
            }
        }
        
        // Fallback to first available
        foreach (var report in reports)
        {
            if (report.IsAvailable)
            {
                return _registry.GetRunner(report.RunnerKind);
            }
        }
        
        return null;
    }
}
