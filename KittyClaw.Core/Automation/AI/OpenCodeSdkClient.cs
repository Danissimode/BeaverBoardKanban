using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.AI;

/// <summary>
/// HTTP client for OpenCode server API.
/// This provides a safer, more controllable interface to OpenCode than the CLI.
/// </summary>
public sealed class OpenCodeSdkClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenCodeSdkClient>? _logger;
    private readonly string _serverUrl;
    private readonly string? _apiKey;
    
    public OpenCodeSdkClient(
        HttpClient httpClient,
        string serverUrl,
        string? apiKey = null,
        ILogger<OpenCodeSdkClient>? logger = null)
    {
        _httpClient = httpClient;
        _serverUrl = serverUrl.TrimEnd('/');
        _apiKey = apiKey;
        _logger = logger;
    }
    
    public async Task<SdkExecutionResult> ExecuteAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        var requestModel = new OpenCodeApiRequest
        {
            Agent = request.AgentName,
            Skill = request.SkillFile,
            Model = request.Model,
            Profile = request.Profile,
            MaxTurns = request.MaxTurns,
            Context = request.ExtraContext,
            WorkingDirectory = request.WorkspacePath,
            SessionScope = request.SessionScope,
            PersistSession = request.PersistSession
        };
        
        var jsonContent = JsonSerializer.Serialize(requestModel, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/execute")
        {
            Content = content
        };
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
        
        try
        {
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("OpenCode API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                
                return new SdkExecutionResult
                {
                    Status = AgentRunStatus.Failed,
                    ExitCode = (int)response.StatusCode,
                    Stdout = string.Empty,
                    Stderr = errorContent,
                    StartedAt = DateTimeOffset.UtcNow,
                    FinishedAt = DateTimeOffset.UtcNow,
                    SessionId = null
                };
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<OpenCodeApiResponse>(responseContent);
            
            if (apiResponse is null)
            {
                return new SdkExecutionResult
                {
                    Status = AgentRunStatus.Failed,
                    ExitCode = 1,
                    Stdout = string.Empty,
                    Stderr = "Invalid response from OpenCode server",
                    StartedAt = DateTimeOffset.UtcNow,
                    FinishedAt = DateTimeOffset.UtcNow,
                    SessionId = null
                };
            }
            
            return new SdkExecutionResult
            {
                Status = apiResponse.Status switch
                {
                    "completed" => AgentRunStatus.Completed,
                    "failed" => AgentRunStatus.Failed,
                    "stopped" => AgentRunStatus.Stopped,
                    _ => AgentRunStatus.Running
                },
                ExitCode = apiResponse.ExitCode,
                Stdout = apiResponse.Stdout ?? string.Empty,
                Stderr = apiResponse.Stderr ?? string.Empty,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
                SessionId = apiResponse.SessionId,
                Artifacts = apiResponse.Artifacts?.ToArray() ?? Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OpenCode SDK client execution failed");
            return new SdkExecutionResult
            {
                Status = AgentRunStatus.Failed,
                ExitCode = 1,
                Stdout = string.Empty,
                Stderr = ex.Message,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
                SessionId = null
            };
        }
    }
    
    public async Task<bool> StopAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/runs/{runId}/stop");
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
            
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop OpenCode run {RunId}", runId);
            return false;
        }
    }
    
    public async Task<AIProviderStatus> GetStatusAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_serverUrl}/api/runs/{runId}/status");
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }
            
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new AIProviderStatus
                {
                    RunId = runId,
                    Status = AgentRunStatus.Failed,
                    ProviderId = "opencode"
                };
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<OpenCodeStatusResponse>(responseContent);
            
            if (apiResponse is null)
            {
                return new AIProviderStatus
                {
                    RunId = runId,
                    Status = AgentRunStatus.Failed,
                    ProviderId = "opencode"
                };
            }
            
            return new AIProviderStatus
            {
                RunId = runId,
                Status = apiResponse.Status switch
                {
                    "completed" => AgentRunStatus.Completed,
                    "failed" => AgentRunStatus.Failed,
                    "stopped" => AgentRunStatus.Stopped,
                    _ => AgentRunStatus.Running
                },
                SessionId = apiResponse.SessionId,
                Model = apiResponse.Model,
                ProviderId = "opencode",
                StartedAt = apiResponse.StartedAt,
                FinishedAt = apiResponse.FinishedAt
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get status for OpenCode run {RunId}", runId);
            return new AIProviderStatus
            {
                RunId = runId,
                Status = AgentRunStatus.Failed,
                ProviderId = "opencode"
            };
        }
    }
}

/// <summary>
/// Result from SDK execution
/// </summary>
public sealed class SdkExecutionResult
{
    public required AgentRunStatus Status { get; init; }
    public int? ExitCode { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public string? SessionId { get; init; }
    public string[] Artifacts { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Request model for OpenCode API
/// </summary>
public sealed class OpenCodeApiRequest
{
    public string? Agent { get; set; }
    public string? Skill { get; set; }
    public string? Model { get; set; }
    public string? Profile { get; set; }
    public int MaxTurns { get; set; } = 200;
    public string? Context { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? SessionScope { get; set; }
    public bool PersistSession { get; set; } = true;
}

/// <summary>
/// Response model from OpenCode API
/// </summary>
public sealed class OpenCodeApiResponse
{
    public string? Status { get; set; }
    public int? ExitCode { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public string? SessionId { get; set; }
    public System.Collections.Generic.List<string>? Artifacts { get; set; }
}

/// <summary>
/// Status response from OpenCode API
/// </summary>
public sealed class OpenCodeStatusResponse
{
    public string? Status { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
