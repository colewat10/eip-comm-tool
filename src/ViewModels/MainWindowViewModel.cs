using System.Collections.ObjectModel;
using System.Windows.Input;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;
using EtherNetIPTool.Core.BootP;
using Serilog;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// Operating modes for the application (REQ-3.2)
/// </summary>
public enum OperatingMode
{
    /// <summary>
    /// EtherNet/IP device discovery mode (default)
    /// </summary>
    EtherNetIP,

    /// <summary>
    /// BootP/DHCP server mode for commissioning factory-default devices
    /// </summary>
    BootP
}

/// <summary>
/// ViewModel for the main application window
/// Manages UI state, network interface selection, device discovery, and application commands
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ActivityLogger _activityLogger;
    private readonly NetworkInterfaceService _networkService;
    private readonly ApplicationSettingsService _settingsService;
    private readonly PrivilegeDetectionService _privilegeService;

    private DeviceDiscoveryService? _discoveryService;
    private AutoBrowseService? _autoBrowseService;
    private BootPServer? _bootpServer;
    private BootPConfigurationService? _bootpConfigurationService;
    private NetworkAdapterInfo? _selectedAdapter;
    private Device? _selectedDevice;
    private string _statusText = "Ready";
    private string _currentTime = string.Empty;
    private bool _isAdministrator;
    private bool _isScanning;
    private OperatingMode _operatingMode = OperatingMode.EtherNetIP;
    private bool _isAutoBrowseEnabled = true; // REQ-3.3.2-002: Enabled by default
    private int _scanIntervalSeconds = 5; // REQ-3.3.2-003: Default 5 seconds

    /// <summary>
    /// Constructor for MainWindowViewModel
    /// </summary>
    public MainWindowViewModel()
    {
        // Initialize services
        var logger = Log.Logger;
        _activityLogger = new ActivityLogger(logger);
        ActivityLogger.GlobalLogger = _activityLogger; // Set global logger for static contexts
        _networkService = new NetworkInterfaceService(_activityLogger);
        _settingsService = new ApplicationSettingsService(_activityLogger);
        _privilegeService = new PrivilegeDetectionService();

        // Initialize collections
        NetworkAdapters = new ObservableCollection<NetworkAdapterInfo>();

        // Initialize commands
        RefreshNicListCommand = new RelayCommand(_ => RefreshNetworkAdapters());
        ScanNowCommand = new AsyncRelayCommand(_ => ScanNowAsync());
        ClearDeviceListCommand = new RelayCommand(_ => ClearDeviceList(), _ => Devices.Any());
        ExitApplicationCommand = new RelayCommand(_ => ExitApplication());
        ShowActivityLogCommand = new RelayCommand(_ => ShowActivityLog());
        ShowAboutCommand = new RelayCommand(_ => ShowAbout());

        // Device commands (Phase 3)
        ConfigureDeviceCommand = new RelayCommand(_ => ConfigureDevice(), _ => SelectedDevice != null);
        CopyMacAddressCommand = new RelayCommand(_ => CopyMacAddress(), _ => SelectedDevice != null);
        CopyIpAddressCommand = new RelayCommand(_ => CopyIpAddress(), _ => SelectedDevice != null);
        PingDeviceCommand = new RelayCommand(_ => PingDevice(), _ => SelectedDevice != null);
        RefreshDeviceInfoCommand = new RelayCommand(_ => RefreshDeviceInfo(), _ => SelectedDevice != null);

        // Initialize
        Initialize();
    }

    #region Properties

    /// <summary>
    /// Collection of available network adapters
    /// </summary>
    public ObservableCollection<NetworkAdapterInfo> NetworkAdapters { get; }

    /// <summary>
    /// Currently selected network adapter
    /// </summary>
    public NetworkAdapterInfo? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (SetProperty(ref _selectedAdapter, value))
            {
                OnAdapterSelected();
            }
        }
    }

    /// <summary>
    /// Status bar text
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Current time display (HH:MM:SS AM/PM)
    /// </summary>
    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    /// <summary>
    /// Indicates if application is running as Administrator
    /// </summary>
    public bool IsAdministrator
    {
        get => _isAdministrator;
        set => SetProperty(ref _isAdministrator, value);
    }

    /// <summary>
    /// Application title with privilege indicator
    /// </summary>
    public string WindowTitle => IsAdministrator
        ? "EtherNet/IP Commissioning Tool [Administrator]"
        : "EtherNet/IP Commissioning Tool";

    /// <summary>
    /// Privilege status text for status bar
    /// </summary>
    public string PrivilegeStatus => _privilegeService.GetPrivilegeLevelDescription();

    /// <summary>
    /// Collection of discovered devices (bound from discovery service)
    /// </summary>
    public ObservableCollection<Device> Devices =>
        _discoveryService?.Devices ?? new ObservableCollection<Device>();

    /// <summary>
    /// Device count text for display
    /// </summary>
    public string DeviceCountText => $"{Devices.Count} device(s)";

    /// <summary>
    /// Indicates if a device scan is in progress
    /// </summary>
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
            }
        }
    }

    /// <summary>
    /// Can perform scan (not already scanning and adapter selected)
    /// </summary>
    public bool CanScan => !IsScanning && SelectedAdapter != null;

    /// <summary>
    /// Current operating mode (REQ-3.2-001)
    /// </summary>
    public OperatingMode OperatingMode
    {
        get => _operatingMode;
        set
        {
            if (SetProperty(ref _operatingMode, value))
            {
                OnOperatingModeChanged();
            }
        }
    }

    /// <summary>
    /// Indicates if currently in EtherNet/IP mode (REQ-3.2-003)
    /// </summary>
    public bool IsEtherNetIPMode => OperatingMode == OperatingMode.EtherNetIP;

    /// <summary>
    /// Indicates if currently in BootP/DHCP mode (REQ-3.2-004)
    /// </summary>
    public bool IsBootPMode => OperatingMode == OperatingMode.BootP;

    /// <summary>
    /// Auto-browse enabled state (REQ-3.3.2-001, REQ-3.3.2-002)
    /// Default: true (enabled by default on startup)
    /// </summary>
    public bool IsAutoBrowseEnabled
    {
        get => _isAutoBrowseEnabled;
        set
        {
            if (SetProperty(ref _isAutoBrowseEnabled, value))
            {
                if (_autoBrowseService != null)
                {
                    _autoBrowseService.IsEnabled = value;
                    _activityLogger.LogInfo($"Auto-browse {(value ? "enabled" : "disabled")}");
                }
            }
        }
    }

    /// <summary>
    /// Scan interval in seconds (REQ-3.3.2-003)
    /// Range: 1-60 seconds, Default: 5 seconds
    /// </summary>
    public int ScanIntervalSeconds
    {
        get => _scanIntervalSeconds;
        set
        {
            // Validate range 1-60
            if (value < 1) value = 1;
            if (value > 60) value = 60;

            if (SetProperty(ref _scanIntervalSeconds, value))
            {
                if (_autoBrowseService != null)
                {
                    _autoBrowseService.ScanIntervalSeconds = value;
                    _activityLogger.LogInfo($"Auto-browse interval changed to {value} seconds");
                }
            }
        }
    }

    /// <summary>
    /// Currently selected device in the table (REQ-3.4-006)
    /// </summary>
    public Device? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnDeviceSelectionChanged();
            }
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to refresh network adapter list
    /// </summary>
    public ICommand RefreshNicListCommand { get; }

    /// <summary>
    /// Command to exit the application
    /// </summary>
    public ICommand ExitApplicationCommand { get; }

    /// <summary>
    /// Command to show activity log viewer
    /// </summary>
    public ICommand ShowActivityLogCommand { get; }

    /// <summary>
    /// Command to show about dialog
    /// </summary>
    public ICommand ShowAboutCommand { get; }

    /// <summary>
    /// Command to perform device scan (REQ-3.3.3-001)
    /// </summary>
    public ICommand ScanNowCommand { get; }

    /// <summary>
    /// Command to clear device list (REQ-3.3.4-001)
    /// </summary>
    public ICommand ClearDeviceListCommand { get; }

    /// <summary>
    /// Command to configure selected device (REQ-3.4-010, REQ-3.5.1-001)
    /// </summary>
    public ICommand ConfigureDeviceCommand { get; }

    /// <summary>
    /// Command to copy MAC address to clipboard (REQ-3.4-011)
    /// </summary>
    public ICommand CopyMacAddressCommand { get; }

    /// <summary>
    /// Command to copy IP address to clipboard (REQ-3.4-011)
    /// </summary>
    public ICommand CopyIpAddressCommand { get; }

    /// <summary>
    /// Command to ping device (REQ-3.4-011)
    /// </summary>
    public ICommand PingDeviceCommand { get; }

    /// <summary>
    /// Command to refresh device information (REQ-3.4-011)
    /// </summary>
    public ICommand RefreshDeviceInfoCommand { get; }

    #endregion

    #region Private Methods

    /// <summary>
    /// Initialize the ViewModel
    /// </summary>
    private void Initialize()
    {
        _activityLogger.LogInfo("Initializing main window");

        // Check privilege level
        IsAdministrator = _privilegeService.IsRunningAsAdministrator();
        _activityLogger.LogInfo(PrivilegeStatus);

        // Load settings
        _settingsService.LoadSettings();

        // Load network adapters
        RefreshNetworkAdapters();

        // Auto-select adapter
        AutoSelectAdapter();

        // Start clock update timer
        StartClockTimer();

        StatusText = "Ready";
    }

    /// <summary>
    /// Refresh the list of network adapters
    /// </summary>
    private void RefreshNetworkAdapters()
    {
        try
        {
            _activityLogger.LogInfo("Refreshing network adapter list...");
            StatusText = "Refreshing network adapters...";

            var adapters = _networkService.EnumerateAdapters();

            NetworkAdapters.Clear();
            foreach (var adapter in adapters)
            {
                NetworkAdapters.Add(adapter);
            }

            _activityLogger.LogInfo($"Found {NetworkAdapters.Count} suitable network adapter(s)");
            StatusText = $"Found {NetworkAdapters.Count} network adapter(s)";
        }
        catch (Exception ex)
        {
            _activityLogger.LogError("Failed to refresh network adapters", ex);
            StatusText = "Error refreshing network adapters";
        }
    }

    /// <summary>
    /// Auto-select first suitable adapter
    /// </summary>
    private void AutoSelectAdapter()
    {
        // Try to restore last selected adapter from settings
        if (!string.IsNullOrEmpty(_settingsService.Settings.LastSelectedAdapterId))
        {
            var savedAdapter = NetworkAdapters
                .FirstOrDefault(a => a.Id == _settingsService.Settings.LastSelectedAdapterId);

            if (savedAdapter != null)
            {
                SelectedAdapter = savedAdapter;
                _activityLogger.LogInfo($"Restored previous adapter selection: {savedAdapter.Name}");
                return;
            }
        }

        // Auto-select first adapter
        if (NetworkAdapters.Any())
        {
            SelectedAdapter = NetworkAdapters.First();
            _activityLogger.LogInfo($"Auto-selected adapter: {SelectedAdapter.Name}");
        }
        else
        {
            _activityLogger.LogWarning("No network adapters available for selection");
            StatusText = "No network adapters found";
        }
    }

    /// <summary>
    /// Handle adapter selection change
    /// </summary>
    private void OnAdapterSelected()
    {
        if (SelectedAdapter != null)
        {
            _activityLogger.LogInfo($"Network adapter selected: {SelectedAdapter.Name} ({SelectedAdapter.IPAddress})");

            // Save selection to settings
            _settingsService.UpdateSetting(s => s.LastSelectedAdapterId = SelectedAdapter.Id);
            _settingsService.SaveSettings();

            StatusText = $"Selected: {SelectedAdapter.DisplayName}";

            // Dispose existing services
            _autoBrowseService?.Dispose();
            _discoveryService?.Dispose();

            // Create new discovery service for selected adapter
            _discoveryService = new DeviceDiscoveryService(_activityLogger, SelectedAdapter);

            // Subscribe to collection changes to update device count
            _discoveryService.Devices.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(DeviceCountText));
            };

            // REQ-3.3.2: Create and configure auto-browse service
            _autoBrowseService = new AutoBrowseService(_activityLogger, _discoveryService);
            _autoBrowseService.ScanIntervalSeconds = _scanIntervalSeconds;

            // REQ-3.1-005: Restart discovery if auto-browse is enabled
            // Only enable in EtherNet/IP mode
            if (OperatingMode == OperatingMode.EtherNetIP && _isAutoBrowseEnabled)
            {
                _autoBrowseService.IsEnabled = true;
                _activityLogger.LogInfo("Auto-browse started on adapter change");
            }

            // If in BootP mode, restart server on new adapter
            if (OperatingMode == OperatingMode.BootP)
            {
                StopBootPServer();
                StartBootPServer();
            }

            // Notify property changes
            OnPropertyChanged(nameof(SelectedAdapter));
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));
            OnPropertyChanged(nameof(CanScan));
        }
        else
        {
            // Stop auto-browse service
            _autoBrowseService?.Dispose();
            _autoBrowseService = null;

            _discoveryService?.Dispose();
            _discoveryService = null;

            // Stop BootP server if no adapter selected
            StopBootPServer();

            StatusText = "No adapter selected";

            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));
            OnPropertyChanged(nameof(CanScan));
        }
    }

    /// <summary>
    /// Handle operating mode change (REQ-3.2-001)
    /// </summary>
    private void OnOperatingModeChanged()
    {
        _activityLogger.LogInfo($"Operating mode changed to: {OperatingMode}");

        if (OperatingMode == OperatingMode.EtherNetIP)
        {
            // REQ-3.2-003: EtherNet/IP mode - enable device discovery
            StopBootPServer();

            // REQ-3.2-004: Re-enable auto-browse and trigger immediate scan if enabled
            if (_autoBrowseService != null && _isAutoBrowseEnabled)
            {
                _autoBrowseService.IsEnabled = true;
                _activityLogger.LogInfo("Auto-browse re-enabled (switched to EtherNet/IP mode)");
            }

            StatusText = "Ready - EtherNet/IP Discovery Mode";
        }
        else if (OperatingMode == OperatingMode.BootP)
        {
            // REQ-3.2-003: Disable auto-browse controls and clear device table
            if (_autoBrowseService != null)
            {
                _autoBrowseService.IsEnabled = false;
                _activityLogger.LogInfo("Auto-browse disabled (switched to BootP mode)");
            }

            // Clear device list when switching to BootP mode
            _discoveryService?.ClearDevices();

            // REQ-3.2-004: BootP/DHCP mode - start server
            StartBootPServer();
        }

        // Notify property changes for mode-dependent UI elements
        OnPropertyChanged(nameof(IsEtherNetIPMode));
        OnPropertyChanged(nameof(IsBootPMode));
        OnPropertyChanged(nameof(CanScan));
    }

    /// <summary>
    /// Start BootP server (REQ-3.6.1)
    /// </summary>
    private void StartBootPServer()
    {
        if (SelectedAdapter == null)
        {
            _activityLogger.LogWarning("Cannot start BootP server: No network adapter selected");
            StatusText = "BootP/DHCP Mode: Please select a network adapter";
            return;
        }

        // REQ-3.6.1-002: Check Administrator privileges
        if (!IsAdministrator)
        {
            _activityLogger.LogWarning("Cannot start BootP server: Administrator privileges required");
            StatusText = "BootP/DHCP Mode: Administrator privileges required";

            System.Windows.MessageBox.Show(
                "BootP/DHCP server mode requires Administrator privileges.\n\n" +
                "Port 68 is a privileged port that requires elevated permissions.\n\n" +
                "Please restart the application as Administrator to use this feature.",
                "Administrator Privileges Required",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);

            return;
        }

        try
        {
            // Create BootP server if not already created
            if (_bootpServer == null)
            {
                _bootpServer = new BootPServer(_activityLogger);
                _bootpConfigurationService = new BootPConfigurationService(_activityLogger, _bootpServer);

                // REQ-3.6.2-003: Wire up RequestReceived event
                _bootpServer.RequestReceived += OnBootPRequestReceived;
            }

            // REQ-3.6.1-001: Start UDP server listening on port 68
            _bootpServer.Start(SelectedAdapter.IPAddress!);

            // REQ-3.6.1-003: Update status bar text
            StatusText = "BootP/DHCP Mode: Listening for factory-default device requests...";
            _activityLogger.LogInfo($"BootP server started successfully on {SelectedAdapter.IPAddress}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _activityLogger.LogError($"Failed to start BootP server: {ex.Message}");
            StatusText = "BootP/DHCP Mode: Access denied - Administrator privileges required";

            System.Windows.MessageBox.Show(
                "Failed to start BootP/DHCP server:\n\n" +
                "Access denied. Port 68 requires Administrator privileges.\n\n" +
                "Please restart the application as Administrator.",
                "BootP Server Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Failed to start BootP server: {ex.Message}");
            StatusText = $"BootP/DHCP Mode: Server failed to start - {ex.Message}";

            System.Windows.MessageBox.Show(
                $"Failed to start BootP/DHCP server:\n\n{ex.Message}",
                "BootP Server Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Stop BootP server (REQ-3.6.1-004)
    /// </summary>
    private void StopBootPServer()
    {
        if (_bootpServer == null || !_bootpServer.IsListening)
            return;

        try
        {
            _bootpServer.Stop();
            _activityLogger.LogInfo("BootP server stopped");
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Error stopping BootP server: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle BootP request received event (REQ-3.6.2-003, REQ-3.6.3)
    /// </summary>
    private void OnBootPRequestReceived(object? sender, BootPRequestEventArgs e)
    {
        // Must be invoked on UI thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                _activityLogger.LogBootP($"BootP request received - displaying configuration dialog");

                // Create ViewModel with request information
                var viewModel = new BootPConfigurationViewModel(e);

                // REQ-3.6.3-001: Display modal configuration dialog
                var configDialog = new Views.BootPConfigurationDialog(viewModel)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                var dialogResult = configDialog.ShowDialog();

                // REQ-3.6.3-011: User clicked "Ignore Request"
                if (dialogResult != true || viewModel.Result == null)
                {
                    _activityLogger.LogBootP("BootP request ignored by user");
                    StatusText = "BootP/DHCP Mode: Request ignored - Listening...";
                    return;
                }

                // REQ-3.6.3-012: User clicked "Assign & Configure"
                var result = viewModel.Result;
                _activityLogger.LogBootP($"User confirmed BootP configuration: IP={result.AssignedIP}, Subnet={result.SubnetMask}");

                // Execute BootP configuration workflow (REQ-3.6.4)
                ExecuteBootPConfigurationAsync(e.Request, result);
            }
            catch (Exception ex)
            {
                _activityLogger.LogError($"Error handling BootP request: {ex.Message}");
                StatusText = $"BootP/DHCP Mode: Error - {ex.Message}";
            }
        });
    }

    /// <summary>
    /// Execute complete BootP configuration workflow (REQ-3.6.4)
    /// </summary>
    private async void ExecuteBootPConfigurationAsync(BootPPacket request, BootPConfigurationResult configResult)
    {
        if (_bootpConfigurationService == null)
        {
            _activityLogger.LogError("BootP configuration service not initialized");
            return;
        }

        try
        {
            StatusText = "BootP/DHCP Mode: Sending configuration to device...";

            // REQ-3.6.4: Complete workflow (send reply → wait 2s → disable DHCP if requested)
            var result = await _bootpConfigurationService.ConfigureDeviceAsync(
                request,
                configResult.AssignedIP!,
                configResult.SubnetMask!,
                configResult.Gateway,
                configResult.DisableDhcp);

            // Display result message
            var statusMessage = result.GetStatusMessage();
            StatusText = $"BootP/DHCP Mode: {statusMessage}";

            if (result.Success)
            {
                _activityLogger.LogInfo($"BootP configuration completed successfully");

                System.Windows.MessageBox.Show(
                    statusMessage,
                    "BootP Configuration Successful",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                _activityLogger.LogError($"BootP configuration failed: {result.ErrorMessage}");

                System.Windows.MessageBox.Show(
                    statusMessage,
                    "BootP Configuration Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            // Return to listening state
            StatusText = "BootP/DHCP Mode: Listening for factory-default device requests...";
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"BootP configuration error: {ex.Message}");
            StatusText = $"BootP/DHCP Mode: Configuration error - {ex.Message}";

            System.Windows.MessageBox.Show(
                $"BootP configuration failed:\n\n{ex.Message}",
                "BootP Configuration Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            // Return to listening state
            StatusText = "BootP/DHCP Mode: Listening for factory-default device requests...";
        }
    }

    /// <summary>
    /// Start the clock update timer
    /// </summary>
    private void StartClockTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("hh:mm:ss tt");
        timer.Start();

        // Initialize immediately
        CurrentTime = DateTime.Now.ToString("hh:mm:ss tt");
    }

    /// <summary>
    /// Exit the application
    /// </summary>
    private void ExitApplication()
    {
        _activityLogger.LogInfo("User requested application exit");

        // Cleanup resources
        _autoBrowseService?.Dispose();
        StopBootPServer();
        _bootpServer?.Dispose();
        _discoveryService?.Dispose();

        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Show activity log viewer window
    /// </summary>
    private void ShowActivityLog()
    {
        _activityLogger.LogInfo("Opening activity log viewer");
        // TODO: Open activity log window (Phase 8)
        StatusText = "Activity log viewer (coming in Phase 8)";
    }

    /// <summary>
    /// Show about dialog
    /// </summary>
    private void ShowAbout()
    {
        _activityLogger.LogInfo("Opening about dialog");
        var version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
        System.Windows.MessageBox.Show(
            $"EtherNet/IP Commissioning Tool\n\n" +
            $"Version: {version}\n\n" +
            $"A Windows desktop application for industrial Ethernet device\n" +
            $"commissioning and troubleshooting.\n\n" +
            $"© 2025 EtherNet/IP Commissioning Tool",
            "About",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// Perform device scan (REQ-3.3.3-001)
    /// </summary>
    private async Task ScanNowAsync()
    {
        if (_discoveryService == null || SelectedAdapter == null)
        {
            _activityLogger.LogWarning("Cannot scan: No network adapter selected");
            StatusText = "Please select a network adapter";
            return;
        }

        try
        {
            IsScanning = true;
            StatusText = "Scanning for devices..."; // REQ-3.3.3-003

            // REQ-3.3.3-002: Manual scan shall clear existing device list before populating new results
            _discoveryService.ClearDevices();
            _activityLogger.LogInfo("Manual scan: Cleared existing device list");

            // REQ-3.3.3-001: Trigger immediate discovery broadcast regardless of auto-browse state
            // Use autoBrowseMode=false to indicate manual scan (doesn't increment missed scans)
            var devicesFound = await _discoveryService.ScanAsync(autoBrowseMode: false);

            StatusText = devicesFound > 0
                ? $"Scan complete. Found {devicesFound} device(s)"
                : "Scan complete. No devices found";

            OnPropertyChanged(nameof(DeviceCountText));
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Scan failed: {ex.Message}", ex);
            StatusText = $"Scan failed: {ex.Message}";

            System.Windows.MessageBox.Show(
                $"Device scan failed:\n\n{ex.Message}",
                "Scan Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Clear all devices from list (REQ-3.3.4-001)
    /// </summary>
    private void ClearDeviceList()
    {
        _discoveryService?.ClearDevices();
        StatusText = "Device list cleared";
        OnPropertyChanged(nameof(DeviceCountText));
    }

    /// <summary>
    /// Handle device selection changed (REQ-3.4-006)
    /// </summary>
    private void OnDeviceSelectionChanged()
    {
        // Update status bar with device information
        if (SelectedDevice != null)
        {
            StatusText = $"Selected: {SelectedDevice.VendorName} {SelectedDevice.ProductName} at {SelectedDevice.IPAddressString}";
            _activityLogger.LogInfo($"Device selected: {SelectedDevice.MacAddressString} ({SelectedDevice.IPAddressString})");
        }
        else
        {
            StatusText = "No device selected";
        }

        // Notify command state changes
        OnPropertyChanged(nameof(SelectedDevice));
    }

    /// <summary>
    /// Configure selected device (REQ-3.4-010, REQ-3.5.1-002)
    /// </summary>
    private void ConfigureDevice()
    {
        if (SelectedDevice == null)
            return;

        try
        {
            _activityLogger.LogInfo($"Opening configuration dialog for device: {SelectedDevice.MacAddressString}");

            // REQ-3.5.1-002: Open modal configuration dialog
            var configDialog = new Views.ConfigurationDialog(SelectedDevice, _activityLogger)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var configResult = configDialog.ShowDialog();

            // User cancelled configuration dialog
            if (configResult != true)
            {
                _activityLogger.LogInfo("Device configuration cancelled by user");
                StatusText = "Configuration cancelled";
                return;
            }

            // Get the new configuration
            var newConfig = configDialog.GetConfiguration();
            if (newConfig == null)
            {
                _activityLogger.LogError("Configuration dialog returned null configuration");
                StatusText = "Configuration error";
                return;
            }

            _activityLogger.LogInfo($"User entered new configuration: IP={newConfig.IPAddress}, Subnet={newConfig.SubnetMask}");

            // REQ-3.5.4-001: Display confirmation dialog showing current vs. new configuration
            var confirmDialog = new Views.ConfirmationDialog(SelectedDevice, newConfig, _activityLogger)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var confirmResult = confirmDialog.ShowDialog();

            // REQ-3.5.4-003: User must explicitly click "Apply" to proceed
            if (confirmResult != true)
            {
                _activityLogger.LogInfo("Configuration changes not confirmed by user");
                StatusText = "Configuration not applied";
                return;
            }

            _activityLogger.LogInfo("User confirmed configuration changes");
            StatusText = "Writing configuration to device...";

            // Phase 5: Send CIP Set_Attribute_Single commands (REQ-3.5.5)
            WriteConfigurationToDeviceAsync(SelectedDevice, newConfig);
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Error during device configuration: {ex.Message}", ex);
            StatusText = $"Configuration error: {ex.Message}";

            System.Windows.MessageBox.Show(
                $"Failed to configure device:\n\n{ex.Message}",
                "Configuration Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Copy MAC address to clipboard (REQ-3.4-011)
    /// </summary>
    private void CopyMacAddress()
    {
        if (SelectedDevice == null)
            return;

        try
        {
            System.Windows.Clipboard.SetText(SelectedDevice.MacAddressString);
            _activityLogger.LogInfo($"Copied MAC address to clipboard: {SelectedDevice.MacAddressString}");
            StatusText = $"Copied MAC address: {SelectedDevice.MacAddressString}";
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Failed to copy MAC address: {ex.Message}", ex);
            StatusText = "Failed to copy MAC address";
        }
    }

    /// <summary>
    /// Copy IP address to clipboard (REQ-3.4-011)
    /// </summary>
    private void CopyIpAddress()
    {
        if (SelectedDevice == null)
            return;

        try
        {
            System.Windows.Clipboard.SetText(SelectedDevice.IPAddressString);
            _activityLogger.LogInfo($"Copied IP address to clipboard: {SelectedDevice.IPAddressString}");
            StatusText = $"Copied IP address: {SelectedDevice.IPAddressString}";
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Failed to copy IP address: {ex.Message}", ex);
            StatusText = "Failed to copy IP address";
        }
    }

    /// <summary>
    /// Ping selected device (REQ-3.4-011)
    /// </summary>
    private void PingDevice()
    {
        if (SelectedDevice == null)
            return;

        try
        {
            _activityLogger.LogInfo($"Pinging device: {SelectedDevice.IPAddressString}");
            StatusText = $"Pinging {SelectedDevice.IPAddressString}...";

            var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send(SelectedDevice.IPAddress, 2000);

            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                _activityLogger.LogInfo($"Ping successful: {SelectedDevice.IPAddressString} ({reply.RoundtripTime}ms)");
                StatusText = $"Ping successful: {reply.RoundtripTime}ms";

                System.Windows.MessageBox.Show(
                    $"Ping to {SelectedDevice.IPAddressString} successful\n\n" +
                    $"Round-trip time: {reply.RoundtripTime}ms\n" +
                    $"TTL: {reply.Options?.Ttl ?? 0}",
                    "Ping Result",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                _activityLogger.LogWarning($"Ping failed: {SelectedDevice.IPAddressString} - {reply.Status}");
                StatusText = $"Ping failed: {reply.Status}";

                System.Windows.MessageBox.Show(
                    $"Ping to {SelectedDevice.IPAddressString} failed\n\n" +
                    $"Status: {reply.Status}",
                    "Ping Result",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Ping error: {ex.Message}", ex);
            StatusText = $"Ping error: {ex.Message}";

            System.Windows.MessageBox.Show(
                $"Ping failed:\n\n{ex.Message}",
                "Ping Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Refresh device information (REQ-3.4-011)
    /// </summary>
    private void RefreshDeviceInfo()
    {
        if (SelectedDevice == null)
            return;

        _activityLogger.LogInfo($"Refreshing device info: {SelectedDevice.MacAddressString}");
        // TODO: Send List Identity request to specific device (future enhancement)
        StatusText = "Refresh device info (coming in future phase)";
    }

    /// <summary>
    /// Write configuration to device via CIP Set_Attribute_Single (REQ-3.5.5)
    /// Phase 5: Sequential attribute writes with progress tracking
    /// </summary>
    private async void WriteConfigurationToDeviceAsync(Device device, DeviceConfiguration config)
    {
        Views.ProgressDialog? progressDialog = null;
        ConfigurationWriteResult? writeResult = null;

        try
        {
            // Create configuration write service
            var writeService = new ConfigurationWriteService(_activityLogger);

            // REQ-3.5.5-006: Show progress dialog "Sending configuration... (X/Y)"
            progressDialog = new Views.ProgressDialog(_activityLogger)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            // Subscribe to progress updates
            writeService.ProgressUpdated += (current, total, operationName) =>
            {
                progressDialog.UpdateProgress(current, total, operationName);
            };

            // Show progress dialog (non-blocking)
            progressDialog.Show();

            // REQ-3.5.5-002: Sequential writes (IP → Subnet → Gateway → Hostname → DNS)
            // REQ-3.5.5-003: Use Unconnected Send via UCMM
            // REQ-3.5.5-004: 3-second timeout per write
            // REQ-3.5.5-005: 100ms delay between writes
            // REQ-3.5.5-007: Stop on first failure
            writeResult = await writeService.WriteConfigurationAsync(device, config);

            // Close progress dialog
            progressDialog.Complete();
            progressDialog = null;

            // REQ-3.5.5-008: Display result dialog with success/failure details
            var resultDialog = new Views.ConfigurationResultDialog(writeResult, _activityLogger)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            resultDialog.ShowDialog();

            if (writeResult.Success)
            {
                // REQ-3.5.5-009: Remove device from list on successful configuration
                _activityLogger.LogInfo($"Configuration successful. Removing device from list: {device.MacAddressString}");
                _discoveryService?.RemoveDevice(device);
                OnPropertyChanged(nameof(DeviceCountText));

                StatusText = $"Device configured successfully and removed from list";
            }
            else
            {
                _activityLogger.LogWarning($"Configuration failed: {writeResult.GetFirstErrorMessage()}");
                StatusText = $"Configuration failed: {writeResult.GetFirstErrorMessage()}";
            }
        }
        catch (Exception ex)
        {
            _activityLogger.LogError($"Configuration write error: {ex.Message}", ex);
            StatusText = $"Configuration write error: {ex.Message}";

            // Close progress dialog if still open
            progressDialog?.Complete();

            System.Windows.MessageBox.Show(
                $"Failed to write configuration:\n\n{ex.Message}",
                "Configuration Write Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion
}
