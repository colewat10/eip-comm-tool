using System.Windows.Threading;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for managing automatic periodic device discovery (REQ-3.3.2)
/// Handles background timer, scan scheduling, and device staleness tracking
/// </summary>
public class AutoBrowseService : IDisposable
{
    private readonly ActivityLogger _logger;
    private readonly DeviceDiscoveryService _discoveryService;
    private DispatcherTimer? _scanTimer;
    private bool _isEnabled;
    private int _scanIntervalSeconds = 5; // REQ-3.3.2-003: Default 5 seconds
    private const int MissedScansThreshold = 3; // REQ-3.3.2-006: Remove after 3 missed scans
    private bool _disposed;

    /// <summary>
    /// Event raised when a scan starts
    /// </summary>
    public event EventHandler? ScanStarted;

    /// <summary>
    /// Event raised when a scan completes
    /// </summary>
    public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;

    /// <summary>
    /// Indicates if auto-browse is currently enabled
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                if (_isEnabled)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }
    }

    /// <summary>
    /// Scan interval in seconds (1-60, default 5)
    /// REQ-3.3.2-003
    /// </summary>
    public int ScanIntervalSeconds
    {
        get => _scanIntervalSeconds;
        set
        {
            // Validate range 1-60 seconds
            if (value < 1 || value > 60)
                throw new ArgumentOutOfRangeException(nameof(value), "Scan interval must be between 1 and 60 seconds");

            if (_scanIntervalSeconds != value)
            {
                _scanIntervalSeconds = value;

                // Restart timer with new interval if running
                if (_isEnabled)
                {
                    Stop();
                    Start();
                }
            }
        }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public AutoBrowseService(ActivityLogger logger, DeviceDiscoveryService discoveryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
    }

    /// <summary>
    /// Start auto-browse timer (REQ-3.3.2-001)
    /// </summary>
    private void Start()
    {
        if (_scanTimer != null)
            return;

        _logger.LogInfo($"Starting auto-browse with {_scanIntervalSeconds}s interval");

        // REQ-3.3.2-004: Use background thread (DispatcherTimer runs on UI thread but async scan doesn't block)
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_scanIntervalSeconds)
        };
        _scanTimer.Tick += OnScanTimerTick;
        _scanTimer.Start();

        // Trigger immediate scan on start
        _ = PerformScanAsync();
    }

    /// <summary>
    /// Stop auto-browse timer
    /// </summary>
    private void Stop()
    {
        if (_scanTimer == null)
            return;

        _logger.LogInfo("Stopping auto-browse");

        _scanTimer.Stop();
        _scanTimer.Tick -= OnScanTimerTick;
        _scanTimer = null;
    }

    /// <summary>
    /// Timer tick event handler
    /// </summary>
    private async void OnScanTimerTick(object? sender, EventArgs e)
    {
        await PerformScanAsync();
    }

    /// <summary>
    /// Perform a single scan cycle (REQ-3.3.2-004)
    /// </summary>
    private async Task PerformScanAsync()
    {
        try
        {
            // Raise scan started event
            ScanStarted?.Invoke(this, EventArgs.Empty);

            // Perform discovery scan
            var devicesFound = await _discoveryService.ScanAsync(autoBrowseMode: true);

            // REQ-3.3.2-006: Remove stale devices (3 consecutive missed scans)
            RemoveStaleDevices();

            // Raise scan completed event
            ScanCompleted?.Invoke(this, new ScanCompletedEventArgs(devicesFound));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Auto-browse scan error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Remove devices that haven't responded for 3 consecutive scans
    /// REQ-3.3.2-006: Devices not responding for 3 consecutive scans shall be removed from table
    /// </summary>
    private void RemoveStaleDevices()
    {
        var devicesToRemove = _discoveryService.Devices
            .Where(d => d.MissedScans >= MissedScansThreshold)
            .ToList();

        foreach (var device in devicesToRemove)
        {
            _logger.LogInfo($"Removing stale device: {device.MacAddressString} ({device.IPAddressString}) - {device.MissedScans} missed scans");
            _discoveryService.RemoveDevice(device);
        }
    }

    /// <summary>
    /// Trigger immediate scan (for manual "Scan Now" button)
    /// </summary>
    public async Task TriggerImmediateScanAsync()
    {
        _logger.LogInfo("Manual scan triggered");
        await PerformScanAsync();
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for scan completed event
/// </summary>
public class ScanCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Number of devices found in scan
    /// </summary>
    public int DevicesFound { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ScanCompletedEventArgs(int devicesFound)
    {
        DevicesFound = devicesFound;
    }
}
