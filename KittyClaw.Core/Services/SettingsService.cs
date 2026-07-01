namespace KittyClaw.Core.Services;

using System.Text.Json;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly string _settingsDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SettingsData? _cached;

    public SettingsService()
    {
        _settingsDir = BeaverBoardPaths.DataDir;
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
        Directory.CreateDirectory(_settingsDir);
    }

    public async Task<SettingsData> LoadAsync()
    {
        if (_cached is not null) return _cached;
        await _lock.WaitAsync();
        try
        {
            if (_cached is not null) return _cached;
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _cached = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
            else
            {
                _cached = new SettingsData();
            }
            return _cached;
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(SettingsData data)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            _cached = data;
        }
        finally { _lock.Release(); }
    }

    public string SettingsPath => _settingsPath;

    /// <summary>
    /// Synchronous load for use in middleware/filters where async is not available.
    /// </summary>
    public SettingsData LoadSync()
    {
        if (_cached is not null) return _cached;
        _lock.Wait();
        try
        {
            if (_cached is not null) return _cached;
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _cached = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
            else
            {
                _cached = new SettingsData();
            }
            return _cached;
        }
        finally { _lock.Release(); }
    }
}

public sealed class SettingsData
{
    public string DefaultProject { get; set; } = "";
    public string Theme { get; set; } = "system"; // system, light, dark
    public bool ConfirmDestructive { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public string AwsProfile { get; set; } = "";
    public string AwsRegion { get; set; } = "us-east-1";
    public bool SecurityBannerDismissed { get; set; } = false;
    
    // Runner preferences
    public string PreferredRunner { get; set; } = "auto"; // "auto", "opencode", "claude"
    public bool SkipClaudeSetup { get; set; } = false;
    public OpenCodeConfigData OpenCode { get; set; } = new();
    
    // API token for IDE/integration access (SHA256 hash, never plain text)
    public string? ApiTokenHash { get; set; }
    public string? ApiTokenScopes { get; set; } = "read";
}

/// <summary>
/// OpenCode-specific settings
/// </summary>
public sealed class OpenCodeConfigData
{
    public bool UseServer { get; set; } = false;
    public string? ServerUrl { get; set; }
    public string? CliCommand { get; set; }
    public string? DefaultProvider { get; set; } = "openrouter";
    public string? DefaultModel { get; set; } = "openrouter/anthropic/claude-3-5-sonnet";
    public string? DefaultAgent { get; set; } = "build";
}
