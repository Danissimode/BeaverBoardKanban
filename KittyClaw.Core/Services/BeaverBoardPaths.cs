using System.Runtime.InteropServices;

namespace KittyClaw.Core.Services;

/// <summary>
/// Platform-aware path resolver for Beaver Board data, logs, cache, and runtime files.
/// Replaces hardcoded dev paths and legacy KittyClaw paths with standard platform directories.
/// </summary>
public static class BeaverBoardPaths
{
    private static string? _dataDir;
    private static string? _logsDir;
    private static string? _cacheDir;
    private static string? _runtimeDir;

    /// <summary>
    /// Primary data directory (registry.db, projects, settings, runs, uploads).
    /// </summary>
    public static string DataDir
    {
        get
        {
            if (_dataDir is not null) return _dataDir;

            var env = Environment.GetEnvironmentVariable("BEAVERBOARD_DATA_DIR");
            if (!string.IsNullOrEmpty(env))
            {
                _dataDir = env;
                return _dataDir;
            }

            // Backward compat: KITTYCLAW_DATA_DIR
            env = Environment.GetEnvironmentVariable("KITTYCLAW_DATA_DIR");
            if (!string.IsNullOrEmpty(env))
            {
                _dataDir = env;
                return _dataDir;
            }

            _dataDir = GetPlatformDataDir();
            return _dataDir;
        }
    }

    /// <summary>
    /// Log directory.
    /// </summary>
    public static string LogsDir
    {
        get
        {
            if (_logsDir is not null) return _logsDir;

            var env = Environment.GetEnvironmentVariable("BEAVERBOARD_LOGS_DIR");
            if (!string.IsNullOrEmpty(env))
            {
                _logsDir = env;
                return _logsDir;
            }

            _logsDir = GetPlatformLogsDir();
            return _logsDir;
        }
    }

    /// <summary>
    /// Cache directory (temp files, image previews, compiled assets).
    /// </summary>
    public static string CacheDir
    {
        get
        {
            if (_cacheDir is not null) return _cacheDir;

            var env = Environment.GetEnvironmentVariable("BEAVERBOARD_CACHE_DIR");
            if (!string.IsNullOrEmpty(env))
            {
                _cacheDir = env;
                return _cacheDir;
            }

            _cacheDir = GetPlatformCacheDir();
            return _cacheDir;
        }
    }

    /// <summary>
    /// Runtime directory (pid files, port files, lock files, sockets).
    /// </summary>
    public static string RuntimeDir
    {
        get
        {
            if (_runtimeDir is not null) return _runtimeDir;
            _runtimeDir = Path.Combine(DataDir, "runtime");
            return _runtimeDir;
        }
    }

    /// <summary>
    /// Full path to the runtime lock file.
    /// </summary>
    public static string LockFile => Path.Combine(RuntimeDir, "app.lock");

    /// <summary>
    /// Full path to the port file written by the backend.
    /// </summary>
    public static string PortFile => Path.Combine(RuntimeDir, "port.json");

    /// <summary>
    /// Full path to the backend pid file.
    /// </summary>
    public static string PidFile => Path.Combine(RuntimeDir, "backend.pid");

    /// <summary>
    /// Ensures all platform directories exist.
    /// </summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(RuntimeDir);
    }

    /// <summary>
    /// One-time migration from legacy KittyClaw paths to BeaverBoard paths.
    /// Copies (does not move) data so the original is preserved.
    /// </summary>
    public static void MigrateFromLegacy()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var legacyDataDir = Path.Combine(appData, "BeaverBoard"); // already BeaverBoard in Program.cs
        var legacyKittyClawDir = Path.Combine(appData, "KittyClaw");
        var legacyTodoAppDir = Path.Combine(appData, "TodoApp");
        var legacyDotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kittyclaw");

        // Migrate from .kittyclaw settings
        if (!Directory.Exists(DataDir) && Directory.Exists(legacyDotDir))
        {
            EnsureDirectories();
            foreach (var file in Directory.GetFiles(legacyDotDir))
            {
                var dest = Path.Combine(DataDir, Path.GetFileName(file));
                if (!File.Exists(dest)) File.Copy(file, dest, overwrite: false);
            }
        }

        // Migrate from old TodoApp
        if (!Directory.Exists(DataDir) && Directory.Exists(legacyTodoAppDir))
        {
            EnsureDirectories();
            CopyDirectoryRecursive(legacyTodoAppDir, DataDir);
        }

        // Migrate from legacy KittyClaw app data
        if (!Directory.Exists(DataDir) && Directory.Exists(legacyKittyClawDir))
        {
            EnsureDirectories();
            CopyDirectoryRecursive(legacyKittyClawDir, DataDir);
        }
    }

    private static string GetPlatformDataDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "BeaverBoard");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "beaver-board");
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "beaver-board");
        }
        // Windows
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BeaverBoard");
    }

    private static string GetPlatformLogsDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "BeaverBoard");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "beaver-board", "logs");
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "state", "beaver-board", "logs");
        }
        // Windows
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BeaverBoard", "Logs");
    }

    private static string GetPlatformCacheDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Caches", "BeaverBoard");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg, "beaver-board");
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache", "beaver-board");
        }
        // Windows
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BeaverBoard", "Cache");
    }

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target, overwrite: false);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, target);
        }
    }
}

/// <summary>
/// Port descriptor written to the runtime directory so the launcher can discover the backend.
/// </summary>
public sealed class PortDescriptor
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5230;
    public string Url => $"http://{Host}:{Port}";
    public int Pid { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public void Write(string path)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static PortDescriptor? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<PortDescriptor>(json);
        }
        catch
        {
            return null;
        }
    }
}
