using System.Net;
using System.Net.NetworkInformation;

namespace EtherNetIPTool.Models;

/// <summary>
/// Device status enumeration (REQ-3.4-007)
/// </summary>
public enum DeviceStatus
{
    /// <summary>Normal operation - valid IP address</summary>
    OK,

    /// <summary>Link-Local IP address (169.254.x.x) - needs configuration</summary>
    LinkLocal,

    /// <summary>Duplicate IP detected on subnet</summary>
    Conflict
}

/// <summary>
/// Represents an EtherNet/IP device discovered on the network
/// Contains device identity information from CIP List Identity response
/// </summary>
public class Device
{
    /// <summary>
    /// MAC address of the device (from ARP lookup)
    /// Format: XX:XX:XX:XX:XX:XX
    /// </summary>
    public PhysicalAddress MacAddress { get; set; } = PhysicalAddress.None;

    /// <summary>
    /// IPv4 address of the device
    /// </summary>
    public IPAddress IPAddress { get; set; } = IPAddress.None;

    /// <summary>
    /// Subnet mask of the device
    /// </summary>
    public IPAddress SubnetMask { get; set; } = IPAddress.None;

    /// <summary>
    /// Gateway address (optional, may be 0.0.0.0)
    /// </summary>
    public IPAddress? Gateway { get; set; }

    /// <summary>
    /// CIP Vendor ID (2 bytes)
    /// Examples: 0x0001=Allen-Bradley, 0x0103=SICK, 0x017F=Banner, 0x0083=Pepperl+Fuchs
    /// </summary>
    public ushort VendorId { get; set; }

    /// <summary>
    /// Human-readable vendor name (mapped from VendorId)
    /// </summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>
    /// CIP Device Type code (2 bytes)
    /// </summary>
    public ushort DeviceType { get; set; }

    /// <summary>
    /// CIP Product Code (2 bytes)
    /// </summary>
    public ushort ProductCode { get; set; }

    /// <summary>
    /// Product name / model string (from List Identity response)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Device serial number (4 bytes)
    /// </summary>
    public uint SerialNumber { get; set; }

    /// <summary>
    /// Device status (OK, LinkLocal, Conflict)
    /// </summary>
    public DeviceStatus Status { get; set; } = DeviceStatus.OK;

    /// <summary>
    /// Timestamp when device was last seen (for timeout tracking)
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.Now;

    /// <summary>
    /// Number of consecutive scans where device was not found
    /// Used for removal after N missed scans (REQ-3.3.2-006)
    /// </summary>
    public int MissedScans { get; set; }

    /// <summary>
    /// Firmware revision (major.minor)
    /// </summary>
    public Version? FirmwareRevision { get; set; }

    /// <summary>
    /// MAC address formatted for display
    /// </summary>
    public string MacAddressString => MacAddress.ToString();

    /// <summary>
    /// IP address formatted for display
    /// </summary>
    public string IPAddressString => IPAddress.ToString();

    /// <summary>
    /// Subnet mask formatted for display
    /// </summary>
    public string SubnetMaskString => SubnetMask.ToString();

    /// <summary>
    /// Status text for display (REQ-3.4-007)
    /// </summary>
    public string StatusText => Status switch
    {
        DeviceStatus.OK => "OK",
        DeviceStatus.LinkLocal => "Link-Local",
        DeviceStatus.Conflict => "Conflict",
        _ => "Unknown"
    };

    /// <summary>
    /// Determines if IP address is in link-local range (169.254.x.x)
    /// </summary>
    public bool IsLinkLocal()
    {
        var bytes = IPAddress.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    /// <summary>
    /// Updates the status based on IP address analysis
    /// </summary>
    public void UpdateStatus()
    {
        if (IsLinkLocal())
        {
            Status = DeviceStatus.LinkLocal;
        }
        else if (Status != DeviceStatus.Conflict) // Don't override conflict status
        {
            Status = DeviceStatus.OK;
        }
    }

    /// <summary>
    /// Creates a unique identifier for the device (based on MAC address)
    /// Used for duplicate detection (REQ-3.3.4-003)
    /// </summary>
    public string GetUniqueId()
    {
        return MacAddress.ToString();
    }

    /// <summary>
    /// Reset missed scan counter (device responded)
    /// </summary>
    public void ResetMissedScans()
    {
        MissedScans = 0;
        LastSeen = DateTime.Now;
    }

    /// <summary>
    /// Increment missed scan counter (device didn't respond)
    /// </summary>
    public void IncrementMissedScans()
    {
        MissedScans++;
    }
}
