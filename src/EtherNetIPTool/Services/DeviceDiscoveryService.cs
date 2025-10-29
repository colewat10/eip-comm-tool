using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using EtherNetIPTool.Core.CIP;
using EtherNetIPTool.Core.Network;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// Device discovery service for EtherNet/IP devices
/// Coordinates CIP List Identity broadcasts, response parsing, and device list management
/// REQ-3.3: Device Discovery (EtherNet/IP Mode)
/// </summary>
public class DeviceDiscoveryService : IDisposable
{
    private readonly ActivityLogger _logger;
    private readonly NetworkAdapterInfo _networkAdapter;
    private EtherNetIPSocket? _socket;
    private bool _disposed;

    /// <summary>
    /// Collection of discovered devices (thread-safe observable)
    /// </summary>
    public ObservableCollection<Device> Devices { get; }

    /// <summary>
    /// Indicates if a scan is currently in progress
    /// </summary>
    public bool IsScanning { get; private set; }

    /// <summary>
    /// Create discovery service for specific network adapter
    /// </summary>
    /// <param name="logger">Activity logger</param>
    /// <param name="adapter">Network adapter to use for discovery</param>
    public DeviceDiscoveryService(ActivityLogger logger, NetworkAdapterInfo adapter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _networkAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        Devices = new ObservableCollection<Device>();
    }

    /// <summary>
    /// Perform single device discovery scan (REQ-3.3.3-001, REQ-3.3.3-002)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of devices discovered</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning)
        {
            _logger.LogWarning("Scan already in progress, ignoring request");
            return 0;
        }

        IsScanning = true;
        var devicesFound = 0;

        try
        {
            _logger.LogScan($"Starting device scan on {_networkAdapter.Name} ({_networkAdapter.IPAddress})");

            // Ensure socket is open
            if (_socket == null)
            {
                _socket = new EtherNetIPSocket(_networkAdapter.IPAddress!);
                _socket.Open();

                // Log dual-socket binding details for diagnostics
                var mainPort = _socket.LocalPort;
                var has44818 = _socket.Has44818Listener;

                _logger.LogInfo($"Opened primary UDP socket on {_networkAdapter.IPAddress}:{mainPort}");

                if (has44818)
                {
                    _logger.LogInfo("✓ Secondary listener on port 44818 active (Turck-style devices supported)");
                    _logger.LogInfo("✓ Dual-socket mode: Compatible with both Rockwell and Turck devices");
                }
                else
                {
                    _logger.LogWarning("⚠ Port 44818 is in use (likely RSLinx or another tool)");
                    _logger.LogInfo("  Single-socket mode: Rockwell-style devices supported");
                    _logger.LogInfo("  Turck-style devices may not respond (they send to port 44818)");
                    _logger.LogInfo("  TIP: Close RSLinx/other tools before scanning to enable dual-socket mode");
                }
            }

            // Build List Identity request packet (REQ-3.3.1-001, REQ-3.3.1-002)
            var requestPacket = ListIdentityMessage.BuildRequest();
            _logger.LogCIP($"Built List Identity request packet ({requestPacket.Length} bytes)");
            _logger.LogCIP($"Packet hex: {BitConverter.ToString(requestPacket)}");

            // Send broadcast (REQ-3.3.1-001)
            // Try both 255.255.255.255 and subnet-directed broadcast
            // Some switches block 255.255.255.255 but allow subnet broadcasts
            _socket.SendBroadcast(requestPacket);
            _logger.LogScan($"Sent List Identity broadcast to 255.255.255.255:44818");

            // Also send subnet-directed broadcast if we have subnet mask
            if (_networkAdapter.SubnetMask != null)
            {
                var subnetBroadcast = CalculateSubnetBroadcast(_networkAdapter.IPAddress!, _networkAdapter.SubnetMask);
                if (subnetBroadcast != null && !subnetBroadcast.Equals(IPAddress.Broadcast))
                {
                    _socket.SendUnicast(requestPacket, subnetBroadcast);
                    _logger.LogScan($"Sent List Identity subnet broadcast to {subnetBroadcast}:44818");
                }
            }

            _logger.LogScan($"Listening for responses for 3 seconds...");

            // Receive all responses within timeout (REQ-3.3.1-003: 3 seconds)
            var responses = await _socket.ReceiveAllResponsesAsync(cancellationToken);
            _logger.LogScan($"Received {responses.Count} total response(s)");

            // Log all response sources for diagnostics
            foreach (var (data, source) in responses)
            {
                _logger.LogInfo($"  Response source: {source.Address}:{source.Port} ({data.Length} bytes)");
            }

            // Filter out our own broadcast echo (we receive our own packet when bound to 44818)
            var validResponses = responses.Where(r => !r.Source.Address.Equals(_networkAdapter.IPAddress)).ToList();
            var selfResponses = responses.Count - validResponses.Count;

            if (selfResponses > 0)
            {
                _logger.LogInfo($"Filtered out {selfResponses} response(s) from our own IP ({_networkAdapter.IPAddress})");
            }

            _logger.LogScan($"Processing {validResponses.Count} valid device response(s)");

            if (validResponses.Count == 0)
            {
                _logger.LogWarning("No devices responded to List Identity broadcast");
                _logger.LogInfo("Possible causes:");
                _logger.LogInfo("  1. No EtherNet/IP devices on network");
                _logger.LogInfo("  2. Windows Firewall blocking UDP port 44818");
                _logger.LogInfo("  3. Devices have EtherNet/IP disabled");
                _logger.LogInfo("  4. Wrong network adapter selected");
                _logger.LogInfo("  5. Devices on different subnet (broadcast doesn't cross subnets)");
            }

            // Parse each valid response (REQ-3.3.1-004)
            foreach (var (data, source) in validResponses)
            {
                _logger.LogCIP($"Response from {source.Address}: {data.Length} bytes");
                _logger.LogCIP($"Response hex (first 64 bytes): {BitConverter.ToString(data, 0, Math.Min(64, data.Length))}");

                var device = ListIdentityMessage.ParseResponse(data, data.Length);

                if (device != null)
                {
                    // Perform ARP lookup for MAC address (REQ-3.3.1-004)
                    device.MacAddress = await GetMacAddressAsync(device.IPAddress);

                    // Add or update device in collection (REQ-3.3.4-003)
                    AddOrUpdateDevice(device);

                    devicesFound++;

                    _logger.LogDiscovery(
                        $"Discovered device: {device.ProductName} " +
                        $"(Vendor: {device.VendorName}, IP: {device.IPAddress}, " +
                        $"MAC: {device.MacAddress}, Serial: {device.SerialNumber})");
                }
                else
                {
                    _logger.LogWarning($"Failed to parse response from {source.Address}");
                }
            }

            _logger.LogScan($"Scan complete. Found {devicesFound} device(s). Total devices: {Devices.Count}");
            return devicesFound;
        }
        catch (SocketException ex)
        {
            _logger.LogError($"Socket error during scan: {ex.Message}", ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error during scan: {ex.Message}", ex);
            throw;
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Add new device or update existing device in collection
    /// (REQ-3.3.4-003: Duplicate devices based on MAC shall be updated in place)
    /// </summary>
    private void AddOrUpdateDevice(Device device)
    {
        // Find existing device by MAC address (unique identifier)
        var existingDevice = Devices.FirstOrDefault(d =>
            d.MacAddress.Equals(device.MacAddress) &&
            !d.MacAddress.Equals(PhysicalAddress.None));

        if (existingDevice != null)
        {
            // Update existing device
            existingDevice.IPAddress = device.IPAddress;
            existingDevice.SubnetMask = device.SubnetMask;
            existingDevice.Gateway = device.Gateway;
            existingDevice.VendorId = device.VendorId;
            existingDevice.VendorName = device.VendorName;
            existingDevice.DeviceType = device.DeviceType;
            existingDevice.ProductCode = device.ProductCode;
            existingDevice.ProductName = device.ProductName;
            existingDevice.SerialNumber = device.SerialNumber;
            existingDevice.FirmwareRevision = device.FirmwareRevision;
            existingDevice.UpdateStatus();
            existingDevice.ResetMissedScans();

            _logger.LogDiscovery($"Updated existing device: {device.MacAddress} at {device.IPAddress}");
        }
        else
        {
            // Add new device to collection (on UI thread)
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Devices.Add(device);
            });

            _logger.LogDiscovery($"Added new device: {device.MacAddress} at {device.IPAddress}");
        }
    }

    /// <summary>
    /// Calculate subnet-directed broadcast address from IP and subnet mask
    /// Example: IP=192.168.21.252, Mask=255.255.255.0 -> Broadcast=192.168.21.255
    /// </summary>
    private static IPAddress? CalculateSubnetBroadcast(IPAddress ip, IPAddress subnetMask)
    {
        try
        {
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();

            if (ipBytes.Length != 4 || maskBytes.Length != 4)
                return null; // Only IPv4 supported

            var broadcastBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                // Broadcast = IP | ~Mask
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }
        catch
        {
            return null;
        }
    }

    // P/Invoke for Windows SendARP API
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref int physAddrLen);

    /// <summary>
    /// Perform ARP table lookup to get MAC address for IP address
    /// Uses Windows SendARP API for reliable ARP resolution
    /// (REQ-3.3.1-004, REQ-4.3.2)
    /// </summary>
    private async Task<PhysicalAddress> GetMacAddressAsync(IPAddress ipAddress)
    {
        try
        {
            // First, send ping to ensure device is in ARP cache (REQ-4.3.2)
            using var ping = new Ping();
            var pingReply = await ping.SendPingAsync(ipAddress, 1000);

            // Wait briefly for ARP cache to update
            await Task.Delay(50);

            // Convert IP address to uint (network byte order)
            var ipBytes = ipAddress.GetAddressBytes();
            if (ipBytes.Length != 4)
            {
                _logger.LogWarning($"Invalid IP address format: {ipAddress}");
                return PhysicalAddress.None;
            }

            uint destIp = BitConverter.ToUInt32(ipBytes, 0);

            // Call SendARP to get MAC address
            byte[] macAddr = new byte[6];
            int macAddrLen = macAddr.Length;

            int result = SendARP(destIp, 0, macAddr, ref macAddrLen);

            if (result == 0 && macAddrLen == 6)
            {
                // Successfully retrieved MAC address
                return new PhysicalAddress(macAddr);
            }
            else
            {
                _logger.LogWarning($"SendARP failed for {ipAddress} with result code: {result}");
                return PhysicalAddress.None;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"ARP lookup failed for {ipAddress}: {ex.Message}");
            return PhysicalAddress.None;
        }
    }

    /// <summary>
    /// Clear all devices from collection (REQ-3.3.4-001)
    /// </summary>
    public void ClearDevices()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Devices.Clear();
        });
        _logger.LogInfo("Device list cleared");
    }

    /// <summary>
    /// Remove devices that haven't responded for N scans
    /// (REQ-3.3.2-006: Remove after 3 consecutive missed scans)
    /// </summary>
    /// <param name="maxMissedScans">Maximum missed scans before removal</param>
    public void RemoveStaleDevices(int maxMissedScans = 3)
    {
        var devicesToRemove = Devices.Where(d => d.MissedScans >= maxMissedScans).ToList();

        if (devicesToRemove.Any())
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var device in devicesToRemove)
                {
                    Devices.Remove(device);
                    _logger.LogDiscovery($"Removed stale device: {device.MacAddress} (missed {device.MissedScans} scans)");
                }
            });
        }
    }

    /// <summary>
    /// Increment missed scan counter for all devices
    /// Call before each scan to track which devices didn't respond
    /// </summary>
    public void IncrementMissedScans()
    {
        foreach (var device in Devices)
        {
            device.IncrementMissedScans();
        }
    }

    /// <summary>
    /// Close socket and dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _socket?.Dispose();
        _socket = null;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
