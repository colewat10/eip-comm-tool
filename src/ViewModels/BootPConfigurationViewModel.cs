using System.Net;
using System.Windows;
using System.Windows.Input;
using EtherNetIPTool.Core.BootP;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// ViewModel for BootP/DHCP configuration dialog
/// REQ-3.6.3: BootP Configuration Dialog
/// Allows user to assign IP configuration to factory-default device via BootP/DHCP
/// </summary>
public class BootPConfigurationViewModel : ViewModelBase
{
    private readonly BootPPacket _request;
    private readonly BootPRequestEventArgs _requestArgs;
    private string _ipAddress1 = string.Empty;
    private string _ipAddress2 = string.Empty;
    private string _ipAddress3 = string.Empty;
    private string _ipAddress4 = string.Empty;
    private string _subnetMask1 = "255";
    private string _subnetMask2 = "255";
    private string _subnetMask3 = "255";
    private string _subnetMask4 = "0";
    private string _gateway1 = string.Empty;
    private string _gateway2 = string.Empty;
    private string _gateway3 = string.Empty;
    private string _gateway4 = string.Empty;
    private bool _disableDhcpAfterAssignment = true;
    private string? _ipValidationError;
    private string? _subnetValidationError;
    private string? _gatewayValidationError;

    /// <summary>
    /// Result of dialog interaction
    /// </summary>
    public BootPConfigurationResult? Result { get; private set; }

    /// <summary>
    /// Create ViewModel for BootP configuration dialog
    /// </summary>
    /// <param name="requestArgs">BootP request event arguments</param>
    public BootPConfigurationViewModel(BootPRequestEventArgs requestArgs)
    {
        _requestArgs = requestArgs ?? throw new ArgumentNullException(nameof(requestArgs));
        _request = requestArgs.Request;

        // Initialize commands
        AssignCommand = new RelayCommand(_ => Assign(), _ => CanAssign());
        IgnoreCommand = new RelayCommand(_ => Ignore());

        // Hook up validation
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName?.Contains("Address") == true || e.PropertyName?.Contains("Mask") == true || e.PropertyName?.Contains("Gateway") == true)
            {
                ValidateConfiguration();
                ((RelayCommand)AssignCommand).RaiseCanExecuteChanged();
            }
        };
    }

    #region Properties

    /// <summary>
    /// Client MAC address from BootP request (REQ-3.6.3-001)
    /// </summary>
    public string MacAddress => _request.GetClientMacAddressString();

    /// <summary>
    /// Request timestamp (REQ-3.6.3-002)
    /// </summary>
    public string Timestamp => _requestArgs.ReceivedAt.ToString("HH:mm:ss.fff");

    /// <summary>
    /// Transaction ID from request (REQ-3.6.3-002)
    /// </summary>
    public string TransactionId => $"0x{_request.Xid:X8}";

    // IP Address octets (REQ-3.6.3-003: Required)
    public string IpAddress1
    {
        get => _ipAddress1;
        set => SetProperty(ref _ipAddress1, value);
    }

    public string IpAddress2
    {
        get => _ipAddress2;
        set => SetProperty(ref _ipAddress2, value);
    }

    public string IpAddress3
    {
        get => _ipAddress3;
        set => SetProperty(ref _ipAddress3, value);
    }

    public string IpAddress4
    {
        get => _ipAddress4;
        set => SetProperty(ref _ipAddress4, value);
    }

    // Subnet Mask octets (REQ-3.6.3-003: Required)
    public string SubnetMask1
    {
        get => _subnetMask1;
        set => SetProperty(ref _subnetMask1, value);
    }

    public string SubnetMask2
    {
        get => _subnetMask2;
        set => SetProperty(ref _subnetMask2, value);
    }

    public string SubnetMask3
    {
        get => _subnetMask3;
        set => SetProperty(ref _subnetMask3, value);
    }

    public string SubnetMask4
    {
        get => _subnetMask4;
        set => SetProperty(ref _subnetMask4, value);
    }

    // Gateway octets (REQ-3.6.3-003: Optional)
    public string Gateway1
    {
        get => _gateway1;
        set => SetProperty(ref _gateway1, value);
    }

    public string Gateway2
    {
        get => _gateway2;
        set => SetProperty(ref _gateway2, value);
    }

    public string Gateway3
    {
        get => _gateway3;
        set => SetProperty(ref _gateway3, value);
    }

    public string Gateway4
    {
        get => _gateway4;
        set => SetProperty(ref _gateway4, value);
    }

    /// <summary>
    /// Disable DHCP mode after assignment (REQ-3.6.3-005: Checked by default)
    /// </summary>
    public bool DisableDhcpAfterAssignment
    {
        get => _disableDhcpAfterAssignment;
        set => SetProperty(ref _disableDhcpAfterAssignment, value);
    }

    /// <summary>
    /// IP address validation error message
    /// </summary>
    public string? IpValidationError
    {
        get => _ipValidationError;
        private set => SetProperty(ref _ipValidationError, value);
    }

    /// <summary>
    /// Subnet mask validation error message
    /// </summary>
    public string? SubnetValidationError
    {
        get => _subnetValidationError;
        private set => SetProperty(ref _subnetValidationError, value);
    }

    /// <summary>
    /// Gateway validation error message
    /// </summary>
    public string? GatewayValidationError
    {
        get => _gatewayValidationError;
        private set => SetProperty(ref _gatewayValidationError, value);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to assign configuration (REQ-3.6.3-006)
    /// </summary>
    public ICommand AssignCommand { get; }

    /// <summary>
    /// Command to ignore request (REQ-3.6.3-007)
    /// </summary>
    public ICommand IgnoreCommand { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Validate configuration and update error messages
    /// REQ-3.6.3-004: Validation rules match EtherNet/IP configuration dialog
    /// </summary>
    private void ValidateConfiguration()
    {
        // Validate IP address
        if (TryParseIPAddress(IpAddress1, IpAddress2, IpAddress3, IpAddress4, out var ipAddress))
        {
            // Check if valid unicast IP (not 0.0.0.0 or 255.255.255.255)
            if (ipAddress!.Equals(IPAddress.Any) || ipAddress.Equals(IPAddress.Broadcast))
            {
                IpValidationError = "Invalid IP address: Cannot be 0.0.0.0 or 255.255.255.255";
            }
            else
            {
                IpValidationError = null;
            }
        }
        else
        {
            IpValidationError = "Invalid IP address format";
        }

        // Validate subnet mask
        if (TryParseIPAddress(SubnetMask1, SubnetMask2, SubnetMask3, SubnetMask4, out var subnetMask))
        {
            if (!IsValidSubnetMask(subnetMask!))
            {
                SubnetValidationError = "Invalid subnet mask: Must be a valid subnet mask (e.g., 255.255.255.0)";
            }
            else
            {
                SubnetValidationError = null;
            }
        }
        else
        {
            SubnetValidationError = "Invalid subnet mask format";
        }

        // Validate gateway (optional)
        if (IsGatewayProvided())
        {
            if (TryParseIPAddress(Gateway1, Gateway2, Gateway3, Gateway4, out var gateway))
            {
                // Validate gateway is on same subnet as IP/Subnet
                if (ipAddress != null && subnetMask != null && !IsOnSameSubnet(ipAddress, gateway!, subnetMask))
                {
                    GatewayValidationError = "Gateway must be on same subnet as IP address";
                }
                else
                {
                    GatewayValidationError = null;
                }
            }
            else
            {
                GatewayValidationError = "Invalid gateway format";
            }
        }
        else
        {
            GatewayValidationError = null;
        }
    }

    /// <summary>
    /// Check if user has provided gateway (any non-empty octet)
    /// </summary>
    private bool IsGatewayProvided()
    {
        return !string.IsNullOrWhiteSpace(Gateway1) ||
               !string.IsNullOrWhiteSpace(Gateway2) ||
               !string.IsNullOrWhiteSpace(Gateway3) ||
               !string.IsNullOrWhiteSpace(Gateway4);
    }

    /// <summary>
    /// Try to parse IP address from octets
    /// </summary>
    private bool TryParseIPAddress(string o1, string o2, string o3, string o4, out IPAddress? address)
    {
        address = null;

        if (!byte.TryParse(o1, out byte b1) ||
            !byte.TryParse(o2, out byte b2) ||
            !byte.TryParse(o3, out byte b3) ||
            !byte.TryParse(o4, out byte b4))
        {
            return false;
        }

        address = new IPAddress(new[] { b1, b2, b3, b4 });
        return true;
    }

    /// <summary>
    /// Validate subnet mask is valid
    /// </summary>
    private bool IsValidSubnetMask(IPAddress mask)
    {
        var bytes = mask.GetAddressBytes();
        uint maskValue = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

        // Valid subnet mask has contiguous 1s followed by contiguous 0s
        // Invert and add 1, should be power of 2
        uint inverted = ~maskValue + 1;
        return (inverted & (inverted - 1)) == 0;
    }

    /// <summary>
    /// Check if two IPs are on same subnet
    /// </summary>
    private bool IsOnSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
    {
        var ip1Bytes = ip1.GetAddressBytes();
        var ip2Bytes = ip2.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();

        for (int i = 0; i < 4; i++)
        {
            if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if configuration can be assigned
    /// REQ-3.6.3-006: Button enabled only when required fields valid
    /// </summary>
    private bool CanAssign()
    {
        return IpValidationError == null &&
               SubnetValidationError == null &&
               GatewayValidationError == null &&
               !string.IsNullOrWhiteSpace(IpAddress1) &&
               !string.IsNullOrWhiteSpace(IpAddress2) &&
               !string.IsNullOrWhiteSpace(IpAddress3) &&
               !string.IsNullOrWhiteSpace(IpAddress4);
    }

    /// <summary>
    /// Assign configuration and close dialog
    /// </summary>
    private void Assign()
    {
        ValidateConfiguration();

        if (!CanAssign())
            return;

        // Parse configuration
        TryParseIPAddress(IpAddress1, IpAddress2, IpAddress3, IpAddress4, out var ipAddress);
        TryParseIPAddress(SubnetMask1, SubnetMask2, SubnetMask3, SubnetMask4, out var subnetMask);

        IPAddress? gateway = null;
        if (IsGatewayProvided())
        {
            TryParseIPAddress(Gateway1, Gateway2, Gateway3, Gateway4, out gateway);
        }

        // Create result
        Result = new BootPConfigurationResult
        {
            Accepted = true,
            AssignedIP = ipAddress!,
            SubnetMask = subnetMask!,
            Gateway = gateway,
            DisableDhcp = DisableDhcpAfterAssignment,
            Request = _request,
            RequestArgs = _requestArgs
        };

        // Close dialog
        CloseDialog(true);
    }

    /// <summary>
    /// Ignore request and close dialog
    /// REQ-3.6.3-007: Ignore Request button closes without action
    /// </summary>
    private void Ignore()
    {
        Result = new BootPConfigurationResult
        {
            Accepted = false
        };

        CloseDialog(false);
    }

    /// <summary>
    /// Close dialog helper
    /// </summary>
    private void CloseDialog(bool dialogResult)
    {
        // This would be set by the view when dialog is shown
        // For now, this is a placeholder
        DialogResult = dialogResult;
    }

    /// <summary>
    /// Dialog result (set by view)
    /// </summary>
    public bool? DialogResult { get; set; }

    #endregion
}

/// <summary>
/// Result of BootP configuration dialog
/// </summary>
public class BootPConfigurationResult
{
    /// <summary>
    /// Whether user accepted and configured the device
    /// </summary>
    public bool Accepted { get; init; }

    /// <summary>
    /// IP address assigned to device
    /// </summary>
    public IPAddress? AssignedIP { get; init; }

    /// <summary>
    /// Subnet mask assigned to device
    /// </summary>
    public IPAddress? SubnetMask { get; init; }

    /// <summary>
    /// Gateway/router IP (optional)
    /// </summary>
    public IPAddress? Gateway { get; init; }

    /// <summary>
    /// Whether to disable DHCP mode after assignment
    /// </summary>
    public bool DisableDhcp { get; init; }

    /// <summary>
    /// Original BootP request
    /// </summary>
    public BootPPacket? Request { get; init; }

    /// <summary>
    /// Request event arguments
    /// </summary>
    public BootPRequestEventArgs? RequestArgs { get; init; }
}
