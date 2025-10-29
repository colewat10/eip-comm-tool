using System.Collections.ObjectModel;
using System.Windows.Input;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;
using Serilog;

namespace EtherNetIPTool.ViewModels;

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
    private NetworkAdapterInfo? _selectedAdapter;
    private string _statusText = "Ready";
    private string _currentTime = string.Empty;
    private bool _isAdministrator;
    private bool _isScanning;

    /// <summary>
    /// Constructor for MainWindowViewModel
    /// </summary>
    public MainWindowViewModel()
    {
        // Initialize services
        var logger = Log.Logger;
        _activityLogger = new ActivityLogger(logger);
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

            // Create new discovery service for selected adapter
            _discoveryService?.Dispose();
            _discoveryService = new DeviceDiscoveryService(_activityLogger, SelectedAdapter);

            // Subscribe to collection changes to update device count
            _discoveryService.Devices.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(DeviceCountText));
            };

            // Notify property changes
            OnPropertyChanged(nameof(SelectedAdapter));
            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));
            OnPropertyChanged(nameof(CanScan));
        }
        else
        {
            _discoveryService?.Dispose();
            _discoveryService = null;
            StatusText = "No adapter selected";

            OnPropertyChanged(nameof(Devices));
            OnPropertyChanged(nameof(DeviceCountText));
            OnPropertyChanged(nameof(CanScan));
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
            StatusText = "Scanning for devices...";

            var devicesFound = await _discoveryService.ScanAsync();

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

    #endregion
}
