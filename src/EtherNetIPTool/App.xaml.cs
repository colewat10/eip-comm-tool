using System.Windows;
using EtherNetIPTool.Services;
using Serilog;

namespace EtherNetIPTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ILogger? _logger;

    /// <summary>
    /// Application startup handler - initializes logging and services
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize logging service
        InitializeLogging();

        _logger?.Information("========================================");
        _logger?.Information("EtherNet/IP Commissioning Tool Starting");
        _logger?.Information("Version: {Version}", GetType().Assembly.GetName().Version);
        _logger?.Information("========================================");

        // Check privilege level
        var privilegeService = new PrivilegeDetectionService();
        var isAdmin = privilegeService.IsRunningAsAdministrator();
        _logger?.Information("Running as Administrator: {IsAdmin}", isAdmin);

        // Log system information
        LogSystemInformation();
    }

    /// <summary>
    /// Application exit handler - cleanup and final logging
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("========================================");
        _logger?.Information("EtherNet/IP Commissioning Tool Exiting");
        _logger?.Information("========================================");

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Unhandled exception handler - log and display error
    /// </summary>
    private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error(e.Exception, "Unhandled exception occurred");

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nPlease check the application log for details.",
            "EtherNet/IP Commissioning Tool - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    /// <summary>
    /// Initialize Serilog logging with file and debug sinks
    /// </summary>
    private void InitializeLogging()
    {
        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EtherNetIPTool",
            "Logs",
            $"app-{DateTime.Now:yyyyMMdd}.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _logger = Log.Logger;
        _logger.Information("Logging initialized to: {LogPath}", logPath);
    }

    /// <summary>
    /// Log system and environment information
    /// </summary>
    private void LogSystemInformation()
    {
        _logger?.Information("Operating System: {OS}", Environment.OSVersion);
        _logger?.Information("64-bit OS: {Is64Bit}", Environment.Is64BitOperatingSystem);
        _logger?.Information("64-bit Process: {Is64Bit}", Environment.Is64BitProcess);
        _logger?.Information("Processor Count: {Count}", Environment.ProcessorCount);
        _logger?.Information("CLR Version: {Version}", Environment.Version);
        _logger?.Information("Machine Name: {Name}", Environment.MachineName);
        _logger?.Information("User Name: {Name}", Environment.UserName);
    }
}
