using System.IO;
using System.Text.Json;

namespace EtherNetIPTool.Services;

/// <summary>
/// Application settings model
/// </summary>
public class ApplicationSettings
{
    // Discovery settings
    public int ScanIntervalSeconds { get; set; } = 5;
    public bool AutoBrowseEnabled { get; set; } = true;
    public int DiscoveryTimeoutMilliseconds { get; set; } = 3000;

    // CIP protocol settings
    public int CipMessageTimeoutMilliseconds { get; set; } = 3000;
    public int InterMessageDelayMilliseconds { get; set; } = 100;
    public int SocketTimeoutMilliseconds { get; set; } = 5000;

    // BootP/DHCP settings
    public int BootPTransactionTimeoutMilliseconds { get; set; } = 10000;
    public bool DisableDhcpAfterAssignment { get; set; } = true;

    // Device management settings
    public int DeviceRemovalScans { get; set; } = 3; // Remove after N missed scans
    public int MaxDeviceListSize { get; set; } = 256;

    // UI settings
    public string? LastSelectedAdapterId { get; set; }
    public bool WindowMaximized { get; set; } = false;
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;

    // Logging settings
    public bool VerboseLogging { get; set; } = false;
    public int MaxLogEntries { get; set; } = 10000;
}

/// <summary>
/// Service for managing application settings persistence
/// Settings are stored in JSON format in user's LocalApplicationData folder
/// </summary>
public class ApplicationSettingsService
{
    private readonly ActivityLogger _logger;
    private readonly string _settingsPath;
    private ApplicationSettings _currentSettings;

    private const string SettingsFileName = "settings.json";
    private const string AppDataFolder = "EtherNetIPTool";

    public ApplicationSettingsService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Determine settings file location
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, AppDataFolder);
        Directory.CreateDirectory(appFolder);

        _settingsPath = Path.Combine(appFolder, SettingsFileName);
        _currentSettings = new ApplicationSettings();

        _logger.LogInfo($"Settings file location: {_settingsPath}");
    }

    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public ApplicationSettings Settings => _currentSettings;

    /// <summary>
    /// Load settings from disk, or create default settings if file doesn't exist
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);

                if (settings != null)
                {
                    _currentSettings = settings;
                    _logger.LogInfo("Settings loaded successfully");
                    ValidateSettings();
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                    _currentSettings = new ApplicationSettings();
                }
            }
            else
            {
                _logger.LogInfo("Settings file not found, using defaults");
                _currentSettings = new ApplicationSettings();
                SaveSettings(); // Create default settings file
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load settings, using defaults", ex);
            _currentSettings = new ApplicationSettings();
        }
    }

    /// <summary>
    /// Save current settings to disk
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            ValidateSettings();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(_currentSettings, options);
            File.WriteAllText(_settingsPath, json);

            _logger.LogInfo("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save settings", ex);
            throw;
        }
    }

    /// <summary>
    /// Validate and constrain settings to valid ranges
    /// </summary>
    private void ValidateSettings()
    {
        // Constrain scan interval to 1-60 seconds (REQ-3.3.2-003)
        if (_currentSettings.ScanIntervalSeconds < 1)
            _currentSettings.ScanIntervalSeconds = 1;
        if (_currentSettings.ScanIntervalSeconds > 60)
            _currentSettings.ScanIntervalSeconds = 60;

        // Validate timeouts (minimum 1 second, maximum 30 seconds)
        _currentSettings.DiscoveryTimeoutMilliseconds =
            Math.Clamp(_currentSettings.DiscoveryTimeoutMilliseconds, 1000, 30000);
        _currentSettings.CipMessageTimeoutMilliseconds =
            Math.Clamp(_currentSettings.CipMessageTimeoutMilliseconds, 1000, 30000);
        _currentSettings.SocketTimeoutMilliseconds =
            Math.Clamp(_currentSettings.SocketTimeoutMilliseconds, 1000, 30000);
        _currentSettings.BootPTransactionTimeoutMilliseconds =
            Math.Clamp(_currentSettings.BootPTransactionTimeoutMilliseconds, 1000, 60000);

        // Validate device management settings
        _currentSettings.DeviceRemovalScans = Math.Clamp(_currentSettings.DeviceRemovalScans, 1, 10);
        _currentSettings.MaxDeviceListSize = Math.Clamp(_currentSettings.MaxDeviceListSize, 10, 1000);

        // Validate logging settings
        _currentSettings.MaxLogEntries = Math.Clamp(_currentSettings.MaxLogEntries, 100, 50000);
    }

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        _logger.LogInfo("Resetting settings to defaults");
        _currentSettings = new ApplicationSettings();
        SaveSettings();
    }

    /// <summary>
    /// Update a specific setting (fluent interface)
    /// </summary>
    public ApplicationSettingsService UpdateSetting(Action<ApplicationSettings> updateAction)
    {
        updateAction(_currentSettings);
        return this;
    }
}
