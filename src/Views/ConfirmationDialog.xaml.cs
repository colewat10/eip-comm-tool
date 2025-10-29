using System.Windows;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// Confirmation dialog for device configuration (REQ-3.5.4, REQ-5.9)
/// Size: 400x300 pixels, centered on parent
/// Shows current vs. new configuration side-by-side
/// </summary>
public partial class ConfirmationDialog : Window
{
    private readonly ActivityLogger _logger;
    private readonly Device _device;

    public ConfirmationDialog(Device device, DeviceConfiguration newConfig, ActivityLogger logger)
    {
        InitializeComponent();

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _device = device;

        // Log confirmation dialog display (REQ-3.7-003)
        _logger.LogConfig($"Showing confirmation dialog for {device.MacAddressString}");
        _logger.LogConfig($"Current -> New: IP {device.IPAddressString} -> {newConfig.IPAddress}");
        _logger.LogConfig($"Current -> New: Subnet {device.SubnetMaskString} -> {newConfig.SubnetMask}");

        // Create view model with current and new values
        var viewModel = new ConfirmationViewModel(device, newConfig);
        DataContext = viewModel;
    }

    /// <summary>
    /// Apply button clicked - REQ-3.5.4-003
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogConfig($"User confirmed configuration changes for {_device.MacAddressString}");
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Cancel button clicked
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogConfig($"User cancelled configuration confirmation for {_device.MacAddressString}");
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// ViewModel for confirmation dialog
/// </summary>
public class ConfirmationViewModel
{
    private readonly Device _device;
    private readonly DeviceConfiguration _newConfig;

    public ConfirmationViewModel(Device device, DeviceConfiguration newConfig)
    {
        _device = device;
        _newConfig = newConfig;
    }

    // Current values
    public string CurrentIPAddress => _device.IPAddressString;
    public string CurrentSubnetMask => _device.SubnetMaskString;
    public string CurrentGateway => _device.Gateway?.ToString() ?? "Not set";
    public string CurrentHostname => "Not set";
    public string CurrentDnsServer => "Not set";

    // New values
    public string NewIPAddress => _newConfig.IPAddress?.ToString() ?? "Not set";
    public string NewSubnetMask => _newConfig.SubnetMask?.ToString() ?? "Not set";
    public string NewGateway => _newConfig.Gateway?.ToString() ?? "Not set";
    public string NewHostname => string.IsNullOrWhiteSpace(_newConfig.Hostname) ? "Not set" : _newConfig.Hostname;
    public string NewDnsServer => _newConfig.DnsServer?.ToString() ?? "Not set";
}
