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
        _settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kittyclaw");
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
}

public sealed class SettingsData
{
    public string DefaultProject { get; set; } = "";
    public string Theme { get; set; } = "system"; // system, light, dark
    public bool ConfirmDestructive { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public string AwsProfile { get; set; } = "";
    public string AwsRegion { get; set; } = "us-east-1";
}
