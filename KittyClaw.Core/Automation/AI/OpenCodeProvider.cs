using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// OpenCode AI provider implementation.
/// Executes agent runs through the OpenCode CLI or HTTP API.
/// </summary>
public sealed class OpenCodeProvider : IAIProvider
{
    private readonly ILogger<OpenCodeProvider>? _logger;
    private readonly OpenCodeSdkClient? _sdkClient;
    private readonly string? _opencodeCommand;
    
    public string Id => "opencode";
    public string Name => "OpenCode";
    
    public bool IsAvailable => _sdkClient is not null || !string.IsNullOrEmpty(_opencodeCommand);
    
    public OpenCodeProvider(
        ILogger<OpenCodeProvider>? logger = null,
        OpenCodeSdkClient? sdkClient = null,
        string? opencodeCommand = null)
    {
        _logger = logger;
        _sdkClient = sdkClient;
        _opencodeCommand = opencodeCommand ?? FindOpenCodeCommand();
    }
    
    private static string? FindOpenCodeCommand()
    {
        // Try common locations for OpenCode CLI
        var candidates = new[] {
            "opencode",
            "oc",
            "/usr/local/bin/opencode",
            "/usr/bin/opencode",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "opencode"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenCode", "opencode.exe")
        };
        
        foreach (var cmd in candidates)
        {
            try
            {
                if (File.Exists(cmd) || (Environment.OSVersion.Platform != PlatformID.Win32CE && Environment.OSVersion.Platform != PlatformID.Win32S && TryFindInPath(cmd)))
                {
                    return cmd;
                }
            }
            catch { /* ignore */ }
        }
        return null;
    }
    
    private static bool TryFindInPath(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(processInfo);
            return process is not null;
        }
        catch { return false; }
    }
    
    public async Task<AIProviderResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("OpenCode provider is not available. Please configure OpenCode CLI or SDK client.");
        }
        
        var startedAt = DateTimeOffset.UtcNow;
        var runId = request.RunId ?? Guid.NewGuid().ToString("N");
        
        try
        {
            // Try SDK client first, fallback to CLI
            if (_sdkClient is not null)
            {
                return await ExecuteViaSdkAsync(request, runId, startedAt, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(_opencodeCommand))
            {
                return await ExecuteViaCliAsync(request, runId, startedAt, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("No OpenCode execution method available.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OpenCode provider execution failed for run {RunId}", runId);
            var finishedAt = DateTimeOffset.UtcNow;
            return new AIProviderResult
            {
                Status = AgentRunStatus.Failed,
                ExitCode = 1,
                Stdout = string.Empty,
                Stderr = ex.Message,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Duration = finishedAt - startedAt,
                ProviderId = Id,
                Model = request.Model ?? string.Empty,
                Profile = request.Profile ?? string.Empty,
                RunId = runId,
                CommandDisplay = $"{_opencodeCommand} ..."
            };
        }
    }
    
    private async Task<AIProviderResult> ExecuteViaSdkAsync(
        AIProviderRequest request, 
        string runId, 
        DateTimeOffset startedAt, 
        CancellationToken cancellationToken)
    {
        if (_sdkClient is null)
        {
            throw new InvalidOperationException("SDK client not available.");
        }
        
        var result = await _sdkClient.ExecuteAsync(request, cancellationToken);
        var finishedAt = DateTimeOffset.UtcNow;
        
        return new AIProviderResult
        {
            Status = result.Status,
            ExitCode = result.ExitCode,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Duration = finishedAt - startedAt,
            ProviderId = Id,
            Model = request.Model ?? string.Empty,
            Profile = request.Profile ?? string.Empty,
            SessionId = result.SessionId,
            RunId = runId,
            CommandDisplay = "OpenCode SDK execution",
            Artifacts = result.Artifacts ?? Array.Empty<string>()
        };
    }
    
    private async Task<AIProviderResult> ExecuteViaCliAsync(
        AIProviderRequest request, 
        string runId, 
        DateTimeOffset startedAt, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_opencodeCommand))
        {
            throw new InvalidOperationException("OpenCode command not available.");
        }
        
        var workingDir = request.WorkspacePath;
        var arguments = BuildCliArguments(request);
        
        var commandDisplay = $"{_opencodeCommand} {string.Join(" ", arguments.Select(a => $"\"{a}\""))}";
        
        var processInfo = new ProcessStartInfo
        {
            FileName = _opencodeCommand,
            Arguments = string.Join(" ", arguments),
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = request.Env
        };
        
        using var process = Process.Start(processInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start OpenCode process.");
        }
        
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stdoutBuilder.AppendLine(e.Data);
                request.OnEventHook?.Invoke(new StreamEvent(DateTimeOffset.UtcNow.UtcDateTime, "stdout", e.Data));
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stderrBuilder.AppendLine(e.Data);
                request.OnEventHook?.Invoke(new StreamEvent(DateTimeOffset.UtcNow.UtcDateTime, "stderr", e.Data));
            }
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        var finishedAt = DateTimeOffset.UtcNow;
        var exitCode = process.ExitCode;
        
        var status = exitCode == 0 ? AgentRunStatus.Completed : AgentRunStatus.Failed;
        
        return new AIProviderResult
        {
            Status = status,
            ExitCode = exitCode,
            Stdout = stdoutBuilder.ToString(),
            Stderr = stderrBuilder.ToString(),
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Duration = finishedAt - startedAt,
            ProviderId = Id,
            Model = request.Model ?? string.Empty,
            Profile = request.Profile ?? string.Empty,
            RunId = runId,
            CommandDisplay = commandDisplay
        };
    }
    
    private List<string> BuildCliArguments(AIProviderRequest request)
    {
        var arguments = new List<string>();
        
        // Add model if specified
        if (!string.IsNullOrEmpty(request.Model))
        {
            arguments.Add("--model");
            arguments.Add(request.Model);
        }
        
        // Add profile if specified
        if (!string.IsNullOrEmpty(request.Profile))
        {
            arguments.Add("--profile");
            arguments.Add(request.Profile);
        }
        
        // Add max turns
        arguments.Add("--max-turns");
        arguments.Add(request.MaxTurns.ToString());
        
        // Add skill file
        if (!string.IsNullOrEmpty(request.SkillFile))
        {
            arguments.Add("--skill");
            arguments.Add(request.SkillFile);
        }
        
        // Add extra context if provided
        if (!string.IsNullOrEmpty(request.ExtraContext))
        {
            arguments.Add("--context");
            arguments.Add(request.ExtraContext);
        }
        
        // Add working directory if different from workspace
        if (!string.IsNullOrEmpty(request.WorkspacePath))
        {
            arguments.Add("--working-directory");
            arguments.Add(request.WorkspacePath);
        }
        
        return arguments;
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        // Implementation for stopping OpenCode runs
        // This would typically call the SDK or send a signal to the CLI process
        _logger?.LogInformation("Stop requested for OpenCode run {RunId}", runId);
        return false; // Not implemented yet
    }
    
    public async Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        // Implementation for getting OpenCode run status
        return new AIProviderStatus
        {
            RunId = runId,
            Status = AgentRunStatus.Running,
            ProviderId = Id
        };
    }
}
