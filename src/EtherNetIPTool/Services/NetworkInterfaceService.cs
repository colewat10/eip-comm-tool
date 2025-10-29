using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EtherNetIPTool.Services;

/// <summary>
/// Represents a network interface adapter with relevant configuration
/// </summary>
public class NetworkAdapterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IPAddress? IPAddress { get; set; }
    public IPAddress? SubnetMask { get; set; }
    public PhysicalAddress? MacAddress { get; set; }
    public NetworkInterfaceType InterfaceType { get; set; }
    public OperationalStatus Status { get; set; }

    /// <summary>
    /// Display format for dropdown: "[Adapter Name] - [IP Address]"
    /// </summary>
    public string DisplayName => $"{Name} - {IPAddress?.ToString() ?? "No IP"}";

    /// <summary>
    /// Detailed information string for tooltip
    /// </summary>
    public string DetailedInfo =>
        $"Name: {Name}\n" +
        $"Description: {Description}\n" +
        $"IP: {IPAddress?.ToString() ?? "None"}\n" +
        $"Subnet: {SubnetMask?.ToString() ?? "None"}\n" +
        $"MAC: {MacAddress?.ToString() ?? "None"}\n" +
        $"Type: {InterfaceType}\n" +
        $"Status: {Status}";
}

/// <summary>
/// Service for enumerating and managing network interfaces
/// Provides NIC selection functionality with auto-detection
/// </summary>
public class NetworkInterfaceService
{
    private readonly ActivityLogger _logger;

    public NetworkInterfaceService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enumerate all active network adapters that meet criteria for industrial networking
    /// Filters: Operational status Up, has IPv4 address, Ethernet or Wireless, not loopback
    /// </summary>
    /// <returns>List of suitable network adapters</returns>
    public List<NetworkAdapterInfo> EnumerateAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();

        try
        {
            _logger.LogInfo("Enumerating network adapters...");

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var nic in interfaces)
            {
                // Filter criteria from PRD REQ-4.3.1
                if (!IsValidAdapter(nic))
                    continue;

                var ipProperties = nic.GetIPProperties();
                var unicastAddresses = ipProperties.UnicastAddresses;

                // Get first IPv4 address
                var ipv4Address = unicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4Address == null)
                    continue;

                // Filter out loopback addresses
                if (IPAddress.IsLoopback(ipv4Address.Address))
                    continue;

                var adapter = new NetworkAdapterInfo
                {
                    Id = nic.Id,
                    Name = nic.Name,
                    Description = nic.Description,
                    IPAddress = ipv4Address.Address,
                    SubnetMask = ipv4Address.IPv4Mask,
                    MacAddress = nic.GetPhysicalAddress(),
                    InterfaceType = nic.NetworkInterfaceType,
                    Status = nic.OperationalStatus
                };

                adapters.Add(adapter);
                _logger.LogInfo($"Found adapter: {adapter.Name} ({adapter.IPAddress})");
            }

            _logger.LogInfo($"Total suitable adapters found: {adapters.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to enumerate network adapters", ex);
            throw;
        }

        return adapters;
    }

    /// <summary>
    /// Validate if adapter meets criteria for industrial networking
    /// </summary>
    private bool IsValidAdapter(NetworkInterface nic)
    {
        // Must be operational
        if (nic.OperationalStatus != OperationalStatus.Up)
            return false;

        // Must be Ethernet or Wireless
        if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
            nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
            nic.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet)
            return false;

        // Must have IP properties
        try
        {
            var ipProperties = nic.GetIPProperties();
            return ipProperties != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Auto-select the first suitable adapter (non-loopback with valid IP)
    /// </summary>
    /// <returns>Selected adapter or null if none found</returns>
    public NetworkAdapterInfo? AutoSelectAdapter()
    {
        var adapters = EnumerateAdapters();
        var selected = adapters.FirstOrDefault();

        if (selected != null)
        {
            _logger.LogInfo($"Auto-selected adapter: {selected.Name} ({selected.IPAddress})");
        }
        else
        {
            _logger.LogWarning("No suitable network adapter found for auto-selection");
        }

        return selected;
    }

    /// <summary>
    /// Get adapter by ID
    /// </summary>
    public NetworkAdapterInfo? GetAdapterById(string id)
    {
        var adapters = EnumerateAdapters();
        return adapters.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>
    /// Check if adapter supports broadcast (required for device discovery)
    /// </summary>
    public bool SupportsUdpBroadcast(NetworkAdapterInfo adapter)
    {
        // All Ethernet and Wireless adapters support broadcast
        return adapter.InterfaceType == NetworkInterfaceType.Ethernet ||
               adapter.InterfaceType == NetworkInterfaceType.Wireless80211 ||
               adapter.InterfaceType == NetworkInterfaceType.GigabitEthernet;
    }

    /// <summary>
    /// Get network statistics for an adapter
    /// </summary>
    public IPv4InterfaceStatistics? GetAdapterStatistics(NetworkAdapterInfo adapter)
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Id == adapter.Id);

            return nic?.GetIPv4Statistics();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get statistics for adapter {adapter.Name}", ex);
            return null;
        }
    }
}
