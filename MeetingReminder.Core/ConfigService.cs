using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingReminder.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeetingReminder.Core;

/// <summary>
/// JSON-on-disk config store. One file: %LOCALAPPDATA%\MeetingReminder\config.json.
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<ConfigService> _logger;
    private readonly object _writeLock = new();

    public ConfigService(string? configPath = null, ILogger<ConfigService>? logger = null)
    {
        _logger = logger ?? NullLogger<ConfigService>.Instance;

        if (configPath is not null)
        {
            ConfigPath = configPath;
            ConfigDirectory = Path.GetDirectoryName(configPath)!;
        }
        else
        {
            ConfigDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeetingReminder");
            ConfigPath = Path.Combine(ConfigDirectory, "config.json");
        }

        Directory.CreateDirectory(ConfigDirectory);
        LogsDirectory = Path.Combine(ConfigDirectory, "logs");
        Directory.CreateDirectory(LogsDirectory);
    }

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }
    public string LogsDirectory { get; }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            _logger.LogInformation("No config found, writing defaults to {Path}", ConfigPath);
            var fresh = AppConfig.Default();
            Save(fresh);
            return fresh;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, Json);
            if (loaded is null)
                throw new InvalidDataException("Config deserialised to null.");
            return loaded.Normalised();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config corrupt at {Path}; backing up and resetting.", ConfigPath);
            try
            {
                File.Copy(ConfigPath, ConfigPath + ".bak", overwrite: true);
            }
            catch (Exception copyEx)
            {
                _logger.LogWarning(copyEx, "Could not back up corrupt config.");
            }

            var fresh = AppConfig.Default();
            Save(fresh);
            return fresh;
        }
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var normalised = config.Normalised();
        var json = JsonSerializer.Serialize(normalised, Json);

        lock (_writeLock)
        {
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json);
            try
            {
                File.Move(tmp, ConfigPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); }
                    catch { /* best effort */ }
                }
                throw;
            }
        }
    }

    /// <summary>Round-trip helper used in tests.</summary>
    public static string Serialize(AppConfig config) => JsonSerializer.Serialize(config, Json);

    /// <summary>Round-trip helper used in tests.</summary>
    public static AppConfig? Deserialize(string json) =>
        JsonSerializer.Deserialize<AppConfig>(json, Json);
}
