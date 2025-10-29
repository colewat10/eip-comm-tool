using System.Net;
using System.Windows.Input;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// ViewModel for device configuration dialog (REQ-3.5)
/// </summary>
public class ConfigurationViewModel : ViewModelBase
{
    private readonly Device _device;
    private IPAddress? _newIPAddress;
    private IPAddress? _newSubnetMask;
    private IPAddress? _newGateway;
    private string _newHostname = string.Empty;
    private IPAddress? _newDnsServer;

    private string _ipAddressError = string.Empty;
    private string _subnetMaskError = string.Empty;
    private string _gatewayError = string.Empty;
    private string _hostnameError = string.Empty;
    private string _dnsServerError = string.Empty;

    /// <summary>
    /// Constructor
    /// </summary>
    public ConfigurationViewModel(Device device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        // Initialize new configuration with current values
        _newIPAddress = device.IPAddress;
        _newSubnetMask = device.SubnetMask;
        _newGateway = device.Gateway;
        _newHostname = string.Empty;
        _newDnsServer = null;

        // Commands
        ApplyConfigurationCommand = new RelayCommand(_ => ApplyConfiguration(), _ => CanApplyConfiguration());
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    #region Current Device Properties (Read-Only) - REQ-3.5.2-001

    /// <summary>
    /// Device MAC address
    /// </summary>
    public string MacAddress => _device.MacAddressString;

    /// <summary>
    /// Vendor name and ID
    /// </summary>
    public string VendorInfo => $"{_device.VendorName} (0x{_device.VendorId:X4})";

    /// <summary>
    /// Device type and product code
    /// </summary>
    public string DeviceTypeInfo => $"Type: 0x{_device.DeviceType:X4}, Product: 0x{_device.ProductCode:X4}";

    /// <summary>
    /// Product name
    /// </summary>
    public string ProductName => _device.ProductName;

    /// <summary>
    /// Serial number
    /// </summary>
    public string SerialNumber => _device.SerialNumber.ToString();

    /// <summary>
    /// Current IP address
    /// </summary>
    public string CurrentIPAddress => _device.IPAddressString;

    /// <summary>
    /// Current subnet mask
    /// </summary>
    public string CurrentSubnetMask => _device.SubnetMaskString;

    /// <summary>
    /// Current gateway (if available)
    /// </summary>
    public string CurrentGateway => _device.Gateway?.ToString() ?? "Not available";

    #endregion

    #region New Configuration Properties (Editable) - REQ-3.5.3

    /// <summary>
    /// New IP Address (required) - REQ-3.5.3-001
    /// </summary>
    public IPAddress? NewIPAddress
    {
        get => _newIPAddress;
        set
        {
            if (SetProperty(ref _newIPAddress, value))
            {
                ValidateIPAddress();
                ValidateGateway();
                ValidateDnsServer();
                OnPropertyChanged(nameof(CanApplyConfiguration));
            }
        }
    }

    /// <summary>
    /// New Subnet Mask (required) - REQ-3.5.3-001
    /// </summary>
    public IPAddress? NewSubnetMask
    {
        get => _newSubnetMask;
        set
        {
            if (SetProperty(ref _newSubnetMask, value))
            {
                ValidateSubnetMask();
                ValidateGateway();
                ValidateDnsServer();
                OnPropertyChanged(nameof(CanApplyConfiguration));
            }
        }
    }

    /// <summary>
    /// New Gateway (optional) - REQ-3.5.3-002
    /// </summary>
    public IPAddress? NewGateway
    {
        get => _newGateway;
        set
        {
            if (SetProperty(ref _newGateway, value))
            {
                ValidateGateway();
                OnPropertyChanged(nameof(CanApplyConfiguration));
            }
        }
    }

    /// <summary>
    /// New Hostname (optional, max 64 chars) - REQ-3.5.3-002, REQ-3.5.3-004
    /// </summary>
    public string NewHostname
    {
        get => _newHostname;
        set
        {
            if (SetProperty(ref _newHostname, value ?? string.Empty))
            {
                ValidateHostname();
                OnPropertyChanged(nameof(CanApplyConfiguration));
            }
        }
    }

    /// <summary>
    /// New DNS Server (optional) - REQ-3.5.3-002
    /// </summary>
    public IPAddress? NewDnsServer
    {
        get => _newDnsServer;
        set
        {
            if (SetProperty(ref _newDnsServer, value))
            {
                ValidateDnsServer();
                OnPropertyChanged(nameof(CanApplyConfiguration));
            }
        }
    }

    #endregion

    #region Validation Error Properties - REQ-3.5.3-007

    /// <summary>
    /// IP Address validation error message
    /// </summary>
    public string IPAddressError
    {
        get => _ipAddressError;
        private set => SetProperty(ref _ipAddressError, value);
    }

    /// <summary>
    /// Subnet Mask validation error message
    /// </summary>
    public string SubnetMaskError
    {
        get => _subnetMaskError;
        private set => SetProperty(ref _subnetMaskError, value);
    }

    /// <summary>
    /// Gateway validation error message
    /// </summary>
    public string GatewayError
    {
        get => _gatewayError;
        private set => SetProperty(ref _gatewayError, value);
    }

    /// <summary>
    /// Hostname validation error message
    /// </summary>
    public string HostnameError
    {
        get => _hostnameError;
        private set => SetProperty(ref _hostnameError, value);
    }

    /// <summary>
    /// DNS Server validation error message
    /// </summary>
    public string DnsServerError
    {
        get => _dnsServerError;
        private set => SetProperty(ref _dnsServerError, value);
    }

    #endregion

    #region Validation Methods - REQ-3.5.3-005, REQ-3.5.3-006, REQ-3.5.3-007

    /// <summary>
    /// Validate IP Address field (required, proper IPv4)
    /// </summary>
    private void ValidateIPAddress()
    {
        if (NewIPAddress == null)
        {
            IPAddressError = "IP Address is required";
            return;
        }

        if (NewIPAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            IPAddressError = "Must be a valid IPv4 address";
            return;
        }

        IPAddressError = string.Empty;
    }

    /// <summary>
    /// Validate Subnet Mask field (required, proper IPv4)
    /// </summary>
    private void ValidateSubnetMask()
    {
        if (NewSubnetMask == null)
        {
            SubnetMaskError = "Subnet Mask is required";
            return;
        }

        if (NewSubnetMask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            SubnetMaskError = "Must be a valid IPv4 subnet mask";
            return;
        }

        // Validate it's a proper subnet mask (contiguous 1 bits from left)
        if (!IsValidSubnetMask(NewSubnetMask))
        {
            SubnetMaskError = "Invalid subnet mask format";
            return;
        }

        SubnetMaskError = string.Empty;
    }

    /// <summary>
    /// Validate Gateway field (optional, must be on same subnet) - REQ-3.5.3-006
    /// </summary>
    private void ValidateGateway()
    {
        // Optional field - no error if empty
        if (NewGateway == null)
        {
            GatewayError = string.Empty;
            return;
        }

        if (NewGateway.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            GatewayError = "Must be a valid IPv4 address";
            return;
        }

        // REQ-3.5.3-006: Must be on same subnet
        if (NewIPAddress != null && NewSubnetMask != null)
        {
            if (!IsOnSameSubnet(NewIPAddress, NewGateway, NewSubnetMask))
            {
                GatewayError = "Gateway must be on the same subnet as IP/Mask";
                return;
            }
        }

        GatewayError = string.Empty;
    }

    /// <summary>
    /// Validate Hostname field (optional, alphanumeric/hyphen/underscore, max 64) - REQ-3.5.3-004
    /// </summary>
    private void ValidateHostname()
    {
        // Optional field - no error if empty
        if (string.IsNullOrWhiteSpace(NewHostname))
        {
            HostnameError = string.Empty;
            return;
        }

        // Max 64 characters
        if (NewHostname.Length > 64)
        {
            HostnameError = "Hostname must be 64 characters or less";
            return;
        }

        // Alphanumeric, hyphens, underscores only
        foreach (char c in NewHostname)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                HostnameError = "Hostname must contain only letters, digits, hyphens, and underscores";
                return;
            }
        }

        HostnameError = string.Empty;
    }

    /// <summary>
    /// Validate DNS Server field (optional, must be on same subnet) - REQ-3.5.3-006
    /// </summary>
    private void ValidateDnsServer()
    {
        // Optional field - no error if empty
        if (NewDnsServer == null)
        {
            DnsServerError = string.Empty;
            return;
        }

        if (NewDnsServer.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            DnsServerError = "Must be a valid IPv4 address";
            return;
        }

        // REQ-3.5.3-006: Must be on same subnet
        if (NewIPAddress != null && NewSubnetMask != null)
        {
            if (!IsOnSameSubnet(NewIPAddress, NewDnsServer, NewSubnetMask))
            {
                DnsServerError = "DNS Server must be on the same subnet as IP/Mask";
                return;
            }
        }

        DnsServerError = string.Empty;
    }

    /// <summary>
    /// Check if subnet mask is valid (contiguous 1 bits from MSB)
    /// </summary>
    private static bool IsValidSubnetMask(IPAddress mask)
    {
        byte[] bytes = mask.GetAddressBytes();
        uint maskValue = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

        // Check if it's a valid subnet mask (contiguous 1s from left)
        uint inverted = ~maskValue;
        return (inverted & (inverted + 1)) == 0;
    }

    /// <summary>
    /// Check if two IP addresses are on the same subnet (REQ-3.5.3-006)
    /// </summary>
    private static bool IsOnSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
    {
        if (ip1.AddressFamily != ip2.AddressFamily)
            return false;

        byte[] ip1Bytes = ip1.GetAddressBytes();
        byte[] ip2Bytes = ip2.GetAddressBytes();
        byte[] maskBytes = subnetMask.GetAddressBytes();

        if (ip1Bytes.Length != ip2Bytes.Length || ip1Bytes.Length != maskBytes.Length)
            return false;

        for (int i = 0; i < ip1Bytes.Length; i++)
        {
            if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
                return false;
        }

        return true;
    }

    #endregion

    #region Commands and Dialog Result

    /// <summary>
    /// Apply Configuration command (REQ-3.5.4-001)
    /// </summary>
    public ICommand ApplyConfigurationCommand { get; }

    /// <summary>
    /// Cancel command
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Dialog result (true = Apply, false = Cancel)
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// New configuration to be applied (null if cancelled)
    /// </summary>
    public DeviceConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Check if configuration can be applied - REQ-3.5.3-008
    /// All required fields valid and no validation errors
    /// </summary>
    private bool CanApplyConfiguration()
    {
        // Required fields must be present
        if (NewIPAddress == null || NewSubnetMask == null)
            return false;

        // No validation errors
        if (!string.IsNullOrEmpty(IPAddressError) ||
            !string.IsNullOrEmpty(SubnetMaskError) ||
            !string.IsNullOrEmpty(GatewayError) ||
            !string.IsNullOrEmpty(HostnameError) ||
            !string.IsNullOrEmpty(DnsServerError))
            return false;

        return true;
    }

    /// <summary>
    /// Apply configuration - REQ-3.5.4-001
    /// </summary>
    private void ApplyConfiguration()
    {
        // Create configuration object
        Configuration = new DeviceConfiguration
        {
            IPAddress = NewIPAddress,
            SubnetMask = NewSubnetMask,
            Gateway = NewGateway,
            Hostname = string.IsNullOrWhiteSpace(NewHostname) ? null : NewHostname,
            DnsServer = NewDnsServer
        };

        DialogResult = true;
    }

    /// <summary>
    /// Cancel configuration
    /// </summary>
    private void Cancel()
    {
        Configuration = null;
        DialogResult = false;
    }

    #endregion
}
