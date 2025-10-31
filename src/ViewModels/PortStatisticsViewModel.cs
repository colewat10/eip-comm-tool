using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// ViewModel for Port Statistics Window
/// Displays real-time network port metrics for a selected device
/// Includes auto-refresh capability with configurable interval
/// </summary>
public class PortStatisticsViewModel : ViewModelBase, IDisposable
{
    private readonly Device _device;
    private readonly ActivityLogger _logger;
    private readonly PortStatisticsService _statsService;
    private readonly DispatcherTimer _refreshTimer;

    private PortStatistics? _currentStats;
    private bool _isRefreshing;
    private int _refreshIntervalSeconds = 2;  // Default 2-second refresh
    private bool _autoRefreshEnabled = true;
    private bool _disposed;

    public PortStatisticsViewModel(Device device, ActivityLogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statsService = new PortStatisticsService(logger);

        // Initialize commands
        RefreshNowCommand = new AsyncRelayCommand(_ => RefreshStatsAsync(), _ => !IsRefreshing);
        CloseCommand = new RelayCommand(ExecuteClose);

        // Setup auto-refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_refreshIntervalSeconds)
        };
        _refreshTimer.Tick += async (s, e) => await RefreshStatsAsync();

        // Initial load - fire and forget (intentional)
#pragma warning disable CS4014
        RefreshStatsAsync();
#pragma warning restore CS4014

        // Start auto-refresh if enabled
        if (_autoRefreshEnabled)
            _refreshTimer.Start();

        _logger.LogInfo($"Port Statistics window opened for {device.ProductName} ({device.IPAddressString})");
    }

    #region Properties

    public string WindowTitle => $"Port Statistics - {_device.ProductName} ({_device.IPAddressString})";

    public PortStatistics? CurrentStats
    {
        get => _currentStats;
        set => SetProperty(ref _currentStats, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                (RefreshNowCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set
        {
            if (SetProperty(ref _autoRefreshEnabled, value))
            {
                if (value)
                    _refreshTimer.Start();
                else
                    _refreshTimer.Stop();
            }
        }
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            if (value < 1) value = 1;
            if (value > 60) value = 60;

            if (SetProperty(ref _refreshIntervalSeconds, value))
            {
                _refreshTimer.Interval = TimeSpan.FromSeconds(value);
            }
        }
    }

    // === Basic Statistics Properties ===

    public string LinkStatus => CurrentStats?.LinkStatusText ?? "Unknown";
    public string LinkSpeed => CurrentStats?.LinkSpeedText ?? "Unknown";
    public string Duplex => CurrentStats?.DuplexText ?? "Unknown";
    public string PacketsIn => CurrentStats?.PacketsIn.ToString("N0") ?? "0";
    public string PacketsOut => CurrentStats?.PacketsOut.ToString("N0") ?? "0";
    public string BytesIn => FormatBytes(CurrentStats?.BytesIn ?? 0);
    public string BytesOut => FormatBytes(CurrentStats?.BytesOut ?? 0);
    public string ErrorsIn => CurrentStats?.ErrorsIn.ToString("N0") ?? "0";
    public string ErrorsOut => CurrentStats?.ErrorsOut.ToString("N0") ?? "0";
    public string DiscardsIn => CurrentStats?.DiscardsIn.ToString("N0") ?? "0";
    public string DiscardsOut => CurrentStats?.DiscardsOut.ToString("N0") ?? "0";
    public string MulticastIn => CurrentStats?.MulticastIn.ToString("N0") ?? "0";
    public string MulticastOut => CurrentStats?.MulticastOut.ToString("N0") ?? "0";
    public string LastUpdated => CurrentStats?.LastUpdated.ToString("HH:mm:ss") ?? "Never";

    // === Detailed Error Properties (Attribute 12) ===

    public bool SupportsDetailedErrors => CurrentStats?.SupportsDetailedErrors ?? false;

    // Physical Layer Errors
    public string FCSErrors => CurrentStats?.FCSErrors.ToString("N0") ?? "0";
    public string AlignmentErrors => CurrentStats?.AlignmentErrors.ToString("N0") ?? "0";
    public string FrameTooLongErrors => CurrentStats?.FrameTooLongErrors.ToString("N0") ?? "0";
    public string TotalPhysicalErrors => CurrentStats?.TotalPhysicalErrors.ToString("N0") ?? "0";

    // Collision Counters
    public string SingleCollisions => CurrentStats?.SingleCollisionFrames.ToString("N0") ?? "0";
    public string MultipleCollisions => CurrentStats?.MultipleCollisionFrames.ToString("N0") ?? "0";
    public string LateCollisions => CurrentStats?.LateCollisions.ToString("N0") ?? "0";
    public string ExcessiveCollisions => CurrentStats?.ExcessiveCollisions.ToString("N0") ?? "0";
    public string TotalCollisions => CurrentStats?.TotalCollisions.ToString("N0") ?? "0";

    // MAC Layer Errors
    public string MACTransmitErrors => CurrentStats?.MACTransmitErrors.ToString("N0") ?? "0";
    public string MACReceiveErrors => CurrentStats?.MACReceiveErrors.ToString("N0") ?? "0";
    public string CarrierSenseErrors => CurrentStats?.CarrierSenseErrors.ToString("N0") ?? "0";
    public string TotalMACErrors => CurrentStats?.TotalMACErrors.ToString("N0") ?? "0";

    // Other Counters
    public string DeferredTransmissions => CurrentStats?.DeferredTransmissions.ToString("N0") ?? "0";
    public string SQETestErrors => CurrentStats?.SQETestErrors.ToString("N0") ?? "0";

    // Summary
    public string CriticalErrors => CurrentStats?.CriticalErrors.ToString("N0") ?? "0";

    #endregion

    #region Commands

    public ICommand RefreshNowCommand { get; }
    public ICommand CloseCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Refresh port statistics from device
    /// </summary>
    private async Task RefreshStatsAsync()
    {
        if (IsRefreshing)
            return;

        try
        {
            IsRefreshing = true;

            var stats = await _statsService.ReadPortStatisticsAsync(_device, portInstance: 1);

            if (stats != null)
            {
                CurrentStats = stats;

                // Notify all property changes
                OnPropertyChanged(nameof(LinkStatus));
                OnPropertyChanged(nameof(LinkSpeed));
                OnPropertyChanged(nameof(Duplex));
                OnPropertyChanged(nameof(PacketsIn));
                OnPropertyChanged(nameof(PacketsOut));
                OnPropertyChanged(nameof(BytesIn));
                OnPropertyChanged(nameof(BytesOut));
                OnPropertyChanged(nameof(ErrorsIn));
                OnPropertyChanged(nameof(ErrorsOut));
                OnPropertyChanged(nameof(DiscardsIn));
                OnPropertyChanged(nameof(DiscardsOut));
                OnPropertyChanged(nameof(MulticastIn));
                OnPropertyChanged(nameof(MulticastOut));
                OnPropertyChanged(nameof(LastUpdated));

                // Notify detailed error properties
                OnPropertyChanged(nameof(SupportsDetailedErrors));
                OnPropertyChanged(nameof(FCSErrors));
                OnPropertyChanged(nameof(AlignmentErrors));
                OnPropertyChanged(nameof(FrameTooLongErrors));
                OnPropertyChanged(nameof(TotalPhysicalErrors));
                OnPropertyChanged(nameof(SingleCollisions));
                OnPropertyChanged(nameof(MultipleCollisions));
                OnPropertyChanged(nameof(LateCollisions));
                OnPropertyChanged(nameof(ExcessiveCollisions));
                OnPropertyChanged(nameof(TotalCollisions));
                OnPropertyChanged(nameof(MACTransmitErrors));
                OnPropertyChanged(nameof(MACReceiveErrors));
                OnPropertyChanged(nameof(CarrierSenseErrors));
                OnPropertyChanged(nameof(TotalMACErrors));
                OnPropertyChanged(nameof(DeferredTransmissions));
                OnPropertyChanged(nameof(SQETestErrors));
                OnPropertyChanged(nameof(CriticalErrors));
            }
            else
            {
                _logger.LogWarning("Failed to read port statistics from device");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to refresh port statistics: {ex.Message}", ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ExecuteClose(object? parameter)
    {
        if (parameter is Window window)
        {
            window.Close();
        }
    }

    private string FormatBytes(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double displayValue = bytes;

        while (displayValue >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            displayValue /= 1024;
            suffixIndex++;
        }

        return $"{displayValue:N2} {suffixes[suffixIndex]}";
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _refreshTimer?.Stop();
        _disposed = true;

        _logger.LogInfo($"Port Statistics window closed for {_device.ProductName}");
    }

    #endregion
}
