using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using EtherNetIPTool.Services;
using Microsoft.Win32;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// ViewModel for Activity Log Viewer window (REQ-3.7-004, REQ-3.7-005)
/// Provides log display with category filtering and export functionality
/// </summary>
public class ActivityLogViewModel : ViewModelBase
{
    private readonly ActivityLogger _logger;
    private readonly ObservableCollection<LogEntry> _filteredEntries;
    private readonly ICollectionView _entriesView;

    // Category filter flags (REQ-3.7-005)
    private bool _showInfo = true;
    private bool _showScan = true;
    private bool _showDisc = true;
    private bool _showConfig = true;
    private bool _showCip = true;
    private bool _showBootp = true;
    private bool _showError = true;
    private bool _showWarn = true;

    public ActivityLogViewModel(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create filtered collection view
        _filteredEntries = new ObservableCollection<LogEntry>();
        _entriesView = CollectionViewSource.GetDefaultView(_filteredEntries);

        // Subscribe to logger entries changes
        _logger.Entries.CollectionChanged += (s, e) => RefreshFilteredEntries();

        // Initialize commands
        ExportLogCommand = new RelayCommand(ExecuteExportLog, CanExecuteExportLog);
        ClearLogCommand = new RelayCommand(ExecuteClearLog, CanExecuteClearLog);
        CloseCommand = new RelayCommand(ExecuteClose);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll);
        DeselectAllCommand = new RelayCommand(ExecuteDeselectAll);

        // Initial load
        RefreshFilteredEntries();

        _logger.LogInfo("Activity Log Viewer opened");
    }

    #region Properties

    /// <summary>
    /// Filtered log entries for display
    /// </summary>
    public ICollectionView Entries => _entriesView;

    /// <summary>
    /// Total number of log entries (all categories)
    /// </summary>
    public int TotalEntryCount => _logger.Entries.Count;

    /// <summary>
    /// Number of visible entries after filtering
    /// </summary>
    public int FilteredEntryCount => _filteredEntries.Count;

    // Category filter properties with OnPropertyChanged notifications
    public bool ShowInfo
    {
        get => _showInfo;
        set
        {
            if (SetProperty(ref _showInfo, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowScan
    {
        get => _showScan;
        set
        {
            if (SetProperty(ref _showScan, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowDisc
    {
        get => _showDisc;
        set
        {
            if (SetProperty(ref _showDisc, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowConfig
    {
        get => _showConfig;
        set
        {
            if (SetProperty(ref _showConfig, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowCip
    {
        get => _showCip;
        set
        {
            if (SetProperty(ref _showCip, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowBootp
    {
        get => _showBootp;
        set
        {
            if (SetProperty(ref _showBootp, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowError
    {
        get => _showError;
        set
        {
            if (SetProperty(ref _showError, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public bool ShowWarn
    {
        get => _showWarn;
        set
        {
            if (SetProperty(ref _showWarn, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand ExportLogCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }

    #endregion

    #region Command Implementations

    /// <summary>
    /// Export log to text file (REQ-3.7-006, REQ-3.7-007)
    /// </summary>
    private void ExecuteExportLog(object? parameter)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Activity Log",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"EtherNetIP_ActivityLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                // Export only filtered entries
                var entriesToExport = _filteredEntries.Select(e => e.FormattedEntry);
                File.WriteAllLines(saveDialog.FileName, entriesToExport, System.Text.Encoding.UTF8);

                _logger.LogInfo($"Activity log exported to: {saveDialog.FileName} ({_filteredEntries.Count} entries)");

                MessageBox.Show(
                    $"Activity log exported successfully.\n\nFile: {saveDialog.FileName}\nEntries: {_filteredEntries.Count}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to export activity log: {ex.Message}", ex);

            MessageBox.Show(
                $"Failed to export activity log:\n\n{ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanExecuteExportLog(object? parameter)
    {
        return _filteredEntries.Count > 0;
    }

    /// <summary>
    /// Clear all log entries (REQ-3.7-009)
    /// </summary>
    private void ExecuteClearLog(object? parameter)
    {
        var result = MessageBox.Show(
            $"Clear all {_logger.Entries.Count} log entries?\n\nThis action cannot be undone.",
            "Clear Activity Log",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _logger.Clear();
            RefreshFilteredEntries();
            _logger.LogInfo("Activity log cleared by user");
        }
    }

    private bool CanExecuteClearLog(object? parameter)
    {
        return _logger.Entries.Count > 0;
    }

    /// <summary>
    /// Close the log viewer window
    /// </summary>
    private void ExecuteClose(object? parameter)
    {
        if (parameter is Window window)
        {
            window.Close();
        }
    }

    /// <summary>
    /// Select all category filters
    /// </summary>
    private void ExecuteSelectAll(object? parameter)
    {
        ShowInfo = true;
        ShowScan = true;
        ShowDisc = true;
        ShowConfig = true;
        ShowCip = true;
        ShowBootp = true;
        ShowError = true;
        ShowWarn = true;
    }

    /// <summary>
    /// Deselect all category filters
    /// </summary>
    private void ExecuteDeselectAll(object? parameter)
    {
        ShowInfo = false;
        ShowScan = false;
        ShowDisc = false;
        ShowConfig = false;
        ShowCip = false;
        ShowBootp = false;
        ShowError = false;
        ShowWarn = false;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Refresh filtered entries based on category selections
    /// </summary>
    private void RefreshFilteredEntries()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _filteredEntries.Clear();

            foreach (var entry in _logger.Entries)
            {
                if (ShouldShowEntry(entry))
                {
                    _filteredEntries.Add(entry);
                }
            }

            OnPropertyChanged(nameof(TotalEntryCount));
            OnPropertyChanged(nameof(FilteredEntryCount));

            // Notify command state changes
            (ExportLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
        });
    }

    /// <summary>
    /// Determine if entry should be shown based on category filters
    /// </summary>
    private bool ShouldShowEntry(LogEntry entry)
    {
        return entry.Category switch
        {
            LogCategory.INFO => ShowInfo,
            LogCategory.SCAN => ShowScan,
            LogCategory.DISC => ShowDisc,
            LogCategory.CONFIG => ShowConfig,
            LogCategory.CIP => ShowCip,
            LogCategory.BOOTP => ShowBootp,
            LogCategory.ERROR => ShowError,
            LogCategory.WARN => ShowWarn,
            _ => true
        };
    }

    #endregion
}
