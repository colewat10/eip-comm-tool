using System.Collections.ObjectModel;
using System.IO;
using Serilog;

namespace EtherNetIPTool.Services;

/// <summary>
/// Categories for activity log entries
/// </summary>
public enum LogCategory
{
    INFO,
    SCAN,
    DISC,    // Discovery
    CONFIG,
    CIP,
    BOOTP,
    ERROR,
    WARN
}

/// <summary>
/// Represents a single activity log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Format the log entry for display
    /// </summary>
    public string FormattedEntry =>
        $"{Timestamp:HH:mm:ss.fff} [{Category,-6}] {Message}";
}

/// <summary>
/// Application activity logger with categorization and export capabilities
/// Maintains in-memory log for UI display and writes to Serilog for persistence
/// </summary>
public class ActivityLogger
{
    private readonly ILogger _logger;
    private readonly ObservableCollection<LogEntry> _entries;
    private readonly object _lock = new();

    // Constants for log management
    private const int MaxLogEntries = 10000;

    /// <summary>
    /// Global logger instance for access from static contexts (e.g., message parsers)
    /// </summary>
    public static ActivityLogger? GlobalLogger { get; set; }

    public ActivityLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entries = new ObservableCollection<LogEntry>();
    }

    /// <summary>
    /// Gets the observable collection of log entries for UI binding
    /// </summary>
    public ObservableCollection<LogEntry> Entries => _entries;

    /// <summary>
    /// Log an informational message
    /// </summary>
    public void LogInfo(string message)
    {
        Log(LogCategory.INFO, message);
        _logger.Information(message);
    }

    /// <summary>
    /// Log a scan operation message
    /// </summary>
    public void LogScan(string message)
    {
        Log(LogCategory.SCAN, message);
        _logger.Information("[SCAN] {Message}", message);
    }

    /// <summary>
    /// Log a device discovery message
    /// </summary>
    public void LogDiscovery(string message)
    {
        Log(LogCategory.DISC, message);
        _logger.Information("[DISC] {Message}", message);
    }

    /// <summary>
    /// Log a configuration operation message
    /// </summary>
    public void LogConfig(string message)
    {
        Log(LogCategory.CONFIG, message);
        _logger.Information("[CONFIG] {Message}", message);
    }

    /// <summary>
    /// Log a CIP protocol message
    /// </summary>
    public void LogCIP(string message)
    {
        Log(LogCategory.CIP, message);
        _logger.Information("[CIP] {Message}", message);
    }

    /// <summary>
    /// Log a BootP/DHCP operation message
    /// </summary>
    public void LogBootP(string message)
    {
        Log(LogCategory.BOOTP, message);
        _logger.Information("[BOOTP] {Message}", message);
    }

    /// <summary>
    /// Log an error message
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        Log(LogCategory.ERROR, message);
        if (exception != null)
        {
            _logger.Error(exception, message);
        }
        else
        {
            _logger.Error(message);
        }
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public void LogWarning(string message)
    {
        Log(LogCategory.WARN, message);
        _logger.Warning(message);
    }

    /// <summary>
    /// Internal log method that adds entry to collection
    /// </summary>
    private void Log(LogCategory category, string message)
    {
        lock (_lock)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Category = category,
                Message = message
            };

            // Add to UI collection on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _entries.Add(entry);

                // Prevent memory issues by limiting log size
                if (_entries.Count > MaxLogEntries)
                {
                    _entries.RemoveAt(0);
                }
            });
        }
    }

    /// <summary>
    /// Clear all log entries
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _entries.Clear();
            });
        }
        _logger.Information("Activity log cleared by user");
    }

    /// <summary>
    /// Export log entries to a text file
    /// </summary>
    /// <param name="filePath">Path to export file</param>
    public void ExportToFile(string filePath)
    {
        lock (_lock)
        {
            try
            {
                var lines = _entries.Select(e => e.FormattedEntry);
                File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
                _logger.Information("Activity log exported to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export activity log to: {FilePath}", filePath);
                throw;
            }
        }
    }

    /// <summary>
    /// Get filtered log entries by category
    /// </summary>
    public IEnumerable<LogEntry> GetEntriesByCategory(LogCategory category)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.Category == category).ToList();
        }
    }

    /// <summary>
    /// Get log entries within a time range
    /// </summary>
    public IEnumerable<LogEntry> GetEntriesByTimeRange(DateTime start, DateTime end)
    {
        lock (_lock)
        {
            return _entries.Where(e => e.Timestamp >= start && e.Timestamp <= end).ToList();
        }
    }
}
