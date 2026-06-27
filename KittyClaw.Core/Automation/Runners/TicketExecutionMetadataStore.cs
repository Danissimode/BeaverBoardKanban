using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Core.Automation.Runners;

/// <summary>
/// Default implementation of ITicketExecutionMetadataStore.
/// Stores execution metadata as JSON files in the project's .kittyclaw/execution directory.
/// </summary>
public sealed class TicketExecutionMetadataStore : ITicketExecutionMetadataStore
{
    private readonly ILogger<TicketExecutionMetadataStore>? _logger;
    private readonly string? _customStoragePath;
    
    public TicketExecutionMetadataStore(ILogger<TicketExecutionMetadataStore>? logger = null, string? customStoragePath = null)
    {
        _logger = logger;
        _customStoragePath = customStoragePath;
    }
    
    private string GetStoragePath(string projectSlug, string workspacePath)
    {
        if (!string.IsNullOrEmpty(_customStoragePath))
        {
            return Path.Combine(_customStoragePath, projectSlug, "execution");
        }
        
        return Path.Combine(workspacePath, ".kittyclaw", "execution");
    }
    
    private string GetMetadataFilePath(string projectSlug, int ticketId, string workspacePath)
    {
        var storagePath = GetStoragePath(projectSlug, workspacePath);
        return Path.Combine(storagePath, $"{ticketId}.json");
    }
    
    private string GetMetadataFilePathByRunId(string runId, string projectSlug, string workspacePath)
    {
        var storagePath = GetStoragePath(projectSlug, workspacePath);
        return Path.Combine(storagePath, $"{runId}.json");
    }
    
    public async Task SaveAsync(TicketExecutionMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetMetadataFilePath(metadata.ProjectSlug, metadata.TicketId, metadata.RootPath ?? metadata.WorktreePath ?? "");
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Save as JSON
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger?.LogInformation("Saved execution metadata for ticket {TicketId} to {FilePath}", metadata.TicketId, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save execution metadata for ticket {TicketId}", metadata.TicketId);
            throw;
        }
    }
    
    public async Task<TicketExecutionMetadata?> GetAsync(string projectSlug, int ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            // We need workspace path to find the file, but we don't have it here
            // For now, we'll try to find it in common locations
            var possiblePaths = new[]
            {
                Path.Combine(".kittyclaw", "execution", $"{ticketId}.json"),
                Path.Combine("..", ".kittyclaw", "execution", $"{ticketId}.json"),
                Path.Combine("...", ".kittyclaw", "execution", $"{ticketId}.json")
            };
            
            foreach (var relativePath in possiblePaths)
            {
                var fullPath = Path.GetFullPath(Path.Combine(projectSlug, relativePath));
                if (File.Exists(fullPath))
                {
                    var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
                    var metadata = JsonSerializer.Deserialize<TicketExecutionMetadata>(json);
                    if (metadata is not null)
                    {
                        return metadata;
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get execution metadata for ticket {TicketId}", ticketId);
            return null;
        }
    }
    
    public async Task<TicketExecutionMetadata?> GetByRunIdAsync(string runId, CancellationToken cancellationToken = default)
    {
        // This is more complex without knowing the project
        // For now, return null
        _logger?.LogWarning("GetByRunIdAsync not fully implemented - need project context");
        return null;
    }
    
    public async Task UpdateAsync(TicketExecutionMetadata metadata, CancellationToken cancellationToken = default)
    {
        // For now, just save again (overwrite)
        await SaveAsync(metadata, cancellationToken);
    }
    
    public async Task<IReadOnlyList<TicketExecutionMetadata>> GetByProjectAsync(string projectSlug, CancellationToken cancellationToken = default)
    {
        var result = new List<TicketExecutionMetadata>();
        
        try
        {
            // Find all JSON files in the project's execution directory
            var storagePath = GetStoragePath(projectSlug, projectSlug); // Simplified
            
            if (Directory.Exists(storagePath))
            {
                foreach (var file in Directory.GetFiles(storagePath, "*.json"))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file, cancellationToken);
                        var metadata = JsonSerializer.Deserialize<TicketExecutionMetadata>(json);
                        if (metadata is not null)
                        {
                            result.Add(metadata);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to read execution metadata file {File}", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get execution metadata for project {ProjectSlug}", projectSlug);
        }
        
        return result;
    }
    
    public async Task DeleteAsync(string projectSlug, int ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetMetadataFilePath(projectSlug, ticketId, projectSlug);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger?.LogInformation("Deleted execution metadata for ticket {TicketId}", ticketId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete execution metadata for ticket {TicketId}", ticketId);
        }
    }
}
