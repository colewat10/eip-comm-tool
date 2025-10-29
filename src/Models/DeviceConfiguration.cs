using System.Net;

namespace EtherNetIPTool.Models;

/// <summary>
/// Device configuration data model (REQ-3.5.3)
/// Contains new IP configuration to be written to device
/// </summary>
public class DeviceConfiguration
{
    /// <summary>
    /// IP Address (required) - REQ-3.5.3-001
    /// </summary>
    public IPAddress? IPAddress { get; set; }

    /// <summary>
    /// Subnet Mask (required) - REQ-3.5.3-001
    /// </summary>
    public IPAddress? SubnetMask { get; set; }

    /// <summary>
    /// Gateway Address (optional) - REQ-3.5.3-002
    /// </summary>
    public IPAddress? Gateway { get; set; }

    /// <summary>
    /// Hostname (optional, max 64 chars) - REQ-3.5.3-002, REQ-3.5.3-004
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// DNS Server (optional) - REQ-3.5.3-002
    /// </summary>
    public IPAddress? DnsServer { get; set; }

    /// <summary>
    /// Validates if all required fields are present and valid
    /// </summary>
    public bool IsValid()
    {
        // REQ-3.5.3-001: IP Address and Subnet Mask are required
        if (IPAddress == null || SubnetMask == null)
            return false;

        // REQ-3.5.3-005: Validate as proper IPv4
        if (IPAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            SubnetMask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        // REQ-3.5.3-006: Gateway and DNS must be on same subnet if provided
        if (Gateway != null && !IsOnSameSubnet(IPAddress, Gateway, SubnetMask))
            return false;

        if (DnsServer != null && !IsOnSameSubnet(IPAddress, DnsServer, SubnetMask))
            return false;

        // REQ-3.5.3-004: Hostname validation (alphanumeric, hyphens, underscores, max 64)
        if (!string.IsNullOrEmpty(Hostname))
        {
            if (Hostname.Length > 64)
                return false;

            foreach (char c in Hostname)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if two IP addresses are on the same subnet (REQ-3.5.3-006)
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

    /// <summary>
    /// Creates a copy of this configuration
    /// </summary>
    public DeviceConfiguration Clone()
    {
        return new DeviceConfiguration
        {
            IPAddress = IPAddress,
            SubnetMask = SubnetMask,
            Gateway = Gateway,
            Hostname = Hostname,
            DnsServer = DnsServer
        };
    }
}
