using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Services;

/// <summary>
/// Executes optional tile scripts (.ps1, .sh, .js, .py) whose stdout is written to the tile's
/// result file. Runs with full user rights; working directory is the project workspace root.
/// </summary>
public sealed class DashboardScriptRunner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ps1", ".sh", ".js", ".py" };

    private readonly ILogger<DashboardScriptRunner> _logger;

    public DashboardScriptRunner(ILogger<DashboardScriptRunner> logger)
    {
        _logger = logger;
    }

    public static bool IsSupported(string scriptFileName)
    {
        var ext = Path.GetExtension(scriptFileName);
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>
    /// Runs the given script file and returns its stdout, or null on failure.
    /// <paramref name="scriptPath"/> is the absolute path to the script.
    /// <paramref name="workspace"/> is used as the working directory.
    /// </summary>
    public async Task<ScriptResult> RunAsync(string scriptPath, string workspace, CancellationToken ct)
    {
        // ── Sandbox: script must live inside the workspace (or .dashboard/ subfolder) ──
        if (!IsPathWithinWorkspace(scriptPath, workspace))
        {
            _logger.LogWarning("Dashboard script rejected: path outside workspace. Script={Script} Workspace={Workspace}", scriptPath, workspace);
            return ScriptResult.FromConfigError("Script path is outside the project workspace.");
        }

        var ext = Path.GetExtension(scriptPath);

        string interpreter;

        switch (ext.ToLowerInvariant())
        {
            case ".ps1":
                interpreter = ResolvePowerShell();
                break;
            case ".sh":
                interpreter = ResolveBash();
                break;
            case ".js":
                interpreter = "node";
                break;
            case ".py":
                interpreter = "python";
                break;
            default:
                return ScriptResult.FromConfigError($"Unsupported script extension: {ext}. Supported: .ps1, .sh, .js, .py");
        }

        _logger.LogInformation("Running dashboard script {Script} with {Interpreter}", scriptPath, interpreter);

        var psi = new ProcessStartInfo
        {
            FileName = interpreter,
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Use ArgumentList to avoid shell-injection via scriptPath
        switch (ext.ToLowerInvariant())
        {
            case ".ps1":
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(scriptPath);
                break;
            default:
                psi.ArgumentList.Add(scriptPath);
                break;
        }

        // Hard timeout: 5 minutes (dashboard scripts should be quick)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var runCt = linkedCts.Token;

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {interpreter}");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(runCt);
            var stderrTask = process.StandardError.ReadToEndAsync(runCt);

            await process.WaitForExitAsync(runCt);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _logger.LogInformation("Script {Script} exited with code {Code}", scriptPath, process.ExitCode);

            if (process.ExitCode != 0)
                return ScriptResult.Failure(process.ExitCode, stderr);

            return ScriptResult.Success(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Dashboard script {Script} was cancelled or exceeded the 5-minute timeout", scriptPath);
            return ScriptResult.FromConfigError("Script execution was cancelled or exceeded the 5-minute timeout.");
        }
        catch (Exception ex)
        {
            var msg = $"Failed to start interpreter '{interpreter}': {ex.Message}";
            _logger.LogWarning(ex, "Dashboard script {Script} could not be started", scriptPath);
            return ScriptResult.FromConfigError(msg);
        }
    }

    /// <summary>
    /// Validates that the script path is inside the workspace tree (no path traversal).
    /// </summary>
    private static bool IsPathWithinWorkspace(string scriptPath, string workspace)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || string.IsNullOrWhiteSpace(workspace))
            return false;
        if (!Path.IsPathRooted(scriptPath) || !Path.IsPathRooted(workspace))
            return false;
        if (scriptPath.Contains(".."))
            return false;
        var fullScript = Path.GetFullPath(scriptPath);
        var fullWorkspace = Path.GetFullPath(workspace);
        // Also allow .dashboard/ subfolder
        var dashboardDir = Path.Combine(fullWorkspace, ".dashboard");
        return fullScript.StartsWith(fullWorkspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullScript.StartsWith(dashboardDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePowerShell() => ShellResolver.ResolvePowerShell();

    private static string ResolveBash()
    {
        if (ShellResolver.TryFindOnPath("bash")) return "bash";
        // Git Bash on Windows common location.
        var gitBash = @"C:\Program Files\Git\bin\bash.exe";
        if (File.Exists(gitBash)) return gitBash;
        return "bash";
    }

}

public sealed record ScriptResult(bool IsSuccess, string Stdout, string Stderr, int ExitCode, string? ConfigError)
{
    public static ScriptResult Success(string stdout, string stderr) =>
        new(true, stdout, stderr, 0, null);

    public static ScriptResult Failure(int exitCode, string stderr) =>
        new(false, "", stderr, exitCode, null);

    public static ScriptResult FromConfigError(string message) =>
        new(false, "", message, -1, message);
}
