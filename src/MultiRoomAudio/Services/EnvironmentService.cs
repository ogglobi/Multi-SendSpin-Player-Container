using System.Text.Json;

namespace MultiRoomAudio.Services;

/// <summary>
/// Detects runtime environment (HAOS vs standalone Docker) and provides
/// appropriate paths and configuration for each deployment mode.
/// </summary>
public class EnvironmentService
{
    private readonly ILogger<EnvironmentService> _logger;
    private readonly bool _isHaos;
    private readonly string _configPath;
    private readonly string _logPath;
    private readonly string _audioBackend;
    private readonly Dictionary<string, JsonElement>? _haosOptions;

    public const string EnvStandalone = "standalone";
    public const string EnvHaos = "haos";

    private const string HaosOptionsFile = "/data/options.json";
    private const string HaosSupervisorTokenEnv = "SUPERVISOR_TOKEN";

    public EnvironmentService(ILogger<EnvironmentService> logger)
    {
        _logger = logger;
        _isHaos = DetectHaos();

        if (_isHaos)
        {
            _haosOptions = LoadHaosOptions();
            _configPath = "/data";
            _logPath = "/share/multiroom-audio/logs";
            _audioBackend = "pulse";
            _logger.LogInformation("Environment: Home Assistant OS");
        }
        else
        {
            _haosOptions = null;
            _configPath = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/app/config";
            _logPath = Environment.GetEnvironmentVariable("LOG_PATH") ?? "/app/logs";
            _audioBackend = Environment.GetEnvironmentVariable("AUDIO_BACKEND")?.ToLower() ?? "alsa";
            _logger.LogInformation("Environment: Standalone Docker");
        }

        // Allow explicit override via environment variable
        var backendOverride = Environment.GetEnvironmentVariable("AUDIO_BACKEND");
        if (!string.IsNullOrEmpty(backendOverride))
        {
            _audioBackend = backendOverride.ToLower();
            _logger.LogInformation("Audio backend override: {Backend}", _audioBackend);
        }

        _logger.LogInformation("Config path: {ConfigPath}", _configPath);
        _logger.LogInformation("Log path: {LogPath}", _logPath);
        _logger.LogInformation("Audio backend: {AudioBackend}", _audioBackend);
    }

    /// <summary>
    /// Whether running in Home Assistant OS add-on mode.
    /// </summary>
    public bool IsHaos => _isHaos;

    /// <summary>
    /// Current environment name ("haos" or "standalone").
    /// </summary>
    public string EnvironmentName => _isHaos ? EnvHaos : EnvStandalone;

    /// <summary>
    /// Path to configuration directory.
    /// </summary>
    public string ConfigPath => _configPath;

    /// <summary>
    /// Full path to players.yaml configuration file.
    /// </summary>
    public string PlayersConfigPath => Path.Combine(_configPath, "players.yaml");

    /// <summary>
    /// Path to log directory.
    /// </summary>
    public string LogPath => _logPath;

    /// <summary>
    /// Audio backend (pulse or alsa).
    /// </summary>
    public string AudioBackend => _audioBackend;

    /// <summary>
    /// Whether PulseAudio is the active backend.
    /// </summary>
    public bool UsePulseAudio => _audioBackend == "pulse";

    /// <summary>
    /// Get HAOS option value by key.
    /// </summary>
    public T? GetHaosOption<T>(string key, T? defaultValue = default)
    {
        if (_haosOptions == null || !_haosOptions.TryGetValue(key, out var element))
            return defaultValue;

        try
        {
            return element.Deserialize<T>();
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Get the volume control method appropriate for this environment.
    /// </summary>
    public string VolumeControlMethod => _isHaos ? "pactl" : "amixer";

    /// <summary>
    /// Ensure required directories exist.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        try
        {
            if (!Directory.Exists(_configPath))
            {
                Directory.CreateDirectory(_configPath);
                _logger.LogInformation("Created config directory: {Path}", _configPath);
            }

            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
                _logger.LogInformation("Created log directory: {Path}", _logPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create directories");
        }
    }

    private bool DetectHaos()
    {
        // Check for HAOS-specific markers
        if (File.Exists(HaosOptionsFile))
        {
            _logger.LogDebug("Detected HAOS: {File} exists", HaosOptionsFile);
            return true;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(HaosSupervisorTokenEnv)))
        {
            _logger.LogDebug("Detected HAOS: {EnvVar} is set", HaosSupervisorTokenEnv);
            return true;
        }

        return false;
    }

    private Dictionary<string, JsonElement>? LoadHaosOptions()
    {
        if (!File.Exists(HaosOptionsFile))
            return null;

        try
        {
            var json = File.ReadAllText(HaosOptionsFile);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load HAOS options from {File}", HaosOptionsFile);
            return null;
        }
    }
}
