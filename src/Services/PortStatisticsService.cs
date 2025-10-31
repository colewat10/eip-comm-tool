using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for reading port statistics from EtherNet/IP devices
/// Reads from Ethernet Link Object (Class 0xF6):
/// - Attribute 4: Interface Speed
/// - Attribute 5: Interface Flags (link status, duplex)
/// - Attribute 6: Media Counters (general traffic stats - 11 DINTs)
/// - Attribute 12: Interface Counters (detailed error breakdown per RFC 2665 - 12 DINTs)
/// </summary>
public class PortStatisticsService
{
    private readonly ActivityLogger _logger;
    private readonly ConfigurationWriteService _cipService;

    // Ethernet Link Object (Class 0xF6) Attribute IDs
    private const byte ATTR_INTERFACE_SPEED = 4;
    private const byte ATTR_INTERFACE_FLAGS = 5;
    private const byte ATTR_MEDIA_COUNTERS = 6;
    private const byte ATTR_INTERFACE_TYPE = 8;
    private const byte ATTR_INTERFACE_STATE = 9;
    private const byte ATTR_INTERFACE_COUNTERS = 12;  // RFC 2665 detailed errors (optional)

    public PortStatisticsService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cipService = new ConfigurationWriteService(logger);
    }

    /// <summary>
    /// Read comprehensive port statistics from device
    /// Reads both Media Counters (Attr 6) and Interface Counters (Attr 12)
    /// </summary>
    /// <param name="device">Target device</param>
    /// <param name="portInstance">Port instance ID (typically 1 for port 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PortStatistics object or null if read failed</returns>
    public async Task<PortStatistics?> ReadPortStatisticsAsync(
        Device device,
        int portInstance = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo($"Reading port statistics for {device.ProductName} ({device.IPAddressString}), Port {portInstance}");

            var stats = new PortStatistics
            {
                DeviceName = device.ProductName,
                DeviceIP = device.IPAddressString,
                PortNumber = portInstance,
                LastUpdated = DateTime.Now
            };

            // === Read Attribute 4: Interface Speed ===
            var speedResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_INTERFACE_SPEED, cancellationToken);

            if (speedResult.Success && speedResult.Data != null && speedResult.Data.Length >= 4)
            {
                stats.InterfaceSpeed = BitConverter.ToUInt32(speedResult.Data, 0);
                _logger.LogCIP($"Interface Speed: {stats.InterfaceSpeed} Mbps");
            }
            else
            {
                _logger.LogWarning($"Failed to read Interface Speed (Attr 4): {speedResult.ErrorMessage}");
            }

            // === Read Attribute 5: Interface Flags ===
            var flagsResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_INTERFACE_FLAGS, cancellationToken);

            if (flagsResult.Success && flagsResult.Data != null && flagsResult.Data.Length >= 4)
            {
                stats.InterfaceFlags = BitConverter.ToUInt32(flagsResult.Data, 0);
                _logger.LogCIP($"Link Status: {stats.LinkStatusText}, Duplex: {stats.DuplexText}");
            }
            else
            {
                _logger.LogWarning($"Failed to read Interface Flags (Attr 5): {flagsResult.ErrorMessage}");
            }

            // === Read Attribute 6: Media Counters (CRITICAL - Main traffic stats) ===
            // This is an array of 11 DINTs (44 bytes total)
            var countersResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_MEDIA_COUNTERS, cancellationToken);

            if (countersResult.Success && countersResult.Data != null && countersResult.Data.Length >= 44)
            {
                // Parse all 11 DINT counters
                for (int i = 0; i < 11 && i * 4 < countersResult.Data.Length; i++)
                {
                    stats.MediaCounters[i] = BitConverter.ToUInt32(countersResult.Data, i * 4);
                }

                _logger.LogCIP($"Media Counters: RX={stats.PacketsIn:N0} pkts ({FormatBytes(stats.BytesIn)}), " +
                              $"TX={stats.PacketsOut:N0} pkts ({FormatBytes(stats.BytesOut)}), " +
                              $"Errors={stats.ErrorsIn + stats.ErrorsOut}");
            }
            else
            {
                _logger.LogError($"Failed to read Media Counters (Attr 6): {countersResult.ErrorMessage}");
                // This is critical - without Media Counters, port stats are not useful
                return null;
            }

            // === Read Attribute 8: Interface Type (optional) ===
            var typeResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_INTERFACE_TYPE, cancellationToken);

            if (typeResult.Success && typeResult.Data != null && typeResult.Data.Length >= 1)
            {
                stats.InterfaceType = typeResult.Data[0];
                _logger.LogCIP($"Interface Type: {stats.InterfaceTypeText}");
            }

            // === Read Attribute 9: Interface State (optional) ===
            var stateResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_INTERFACE_STATE, cancellationToken);

            if (stateResult.Success && stateResult.Data != null && stateResult.Data.Length >= 1)
            {
                stats.InterfaceState = stateResult.Data[0];
                _logger.LogCIP($"Interface State: {stats.InterfaceStateText}");
            }

            // === Read Attribute 12: Interface Counters (DETAILED ERRORS - RFC 2665) ===
            // This is OPTIONAL - not all devices support it
            // Structure: 12 DINTs (48 bytes total) with detailed error breakdown
            var interfaceCountersResult = await _cipService.ReadAttributeAsync(
                device, 0xF6, (byte)portInstance, ATTR_INTERFACE_COUNTERS, cancellationToken);

            if (interfaceCountersResult.Success && interfaceCountersResult.Data != null)
            {
                // Attribute 12 structure (per CIP spec / RFC 2665):
                // Offset 0: Alignment Errors (DINT)
                // Offset 4: FCS Errors (DINT)
                // Offset 8: Single Collision Frames (DINT)
                // Offset 12: Multiple Collision Frames (DINT)
                // Offset 16: SQE Test Errors (DINT)
                // Offset 20: Deferred Transmissions (DINT)
                // Offset 24: Late Collisions (DINT)
                // Offset 28: Excessive Collisions (DINT)
                // Offset 32: MAC Transmit Errors (DINT)
                // Offset 36: Carrier Sense Errors (DINT)
                // Offset 40: Frame Too Long (DINT)
                // Offset 44: MAC Receive Errors (DINT)
                // Total: 12 DINTs = 48 bytes

                if (interfaceCountersResult.Data.Length >= 48)
                {
                    stats.AlignmentErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 0);
                    stats.FCSErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 4);
                    stats.SingleCollisionFrames = BitConverter.ToUInt32(interfaceCountersResult.Data, 8);
                    stats.MultipleCollisionFrames = BitConverter.ToUInt32(interfaceCountersResult.Data, 12);
                    stats.SQETestErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 16);
                    stats.DeferredTransmissions = BitConverter.ToUInt32(interfaceCountersResult.Data, 20);
                    stats.LateCollisions = BitConverter.ToUInt32(interfaceCountersResult.Data, 24);
                    stats.ExcessiveCollisions = BitConverter.ToUInt32(interfaceCountersResult.Data, 28);
                    stats.MACTransmitErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 32);
                    stats.CarrierSenseErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 36);
                    stats.FrameTooLongErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 40);
                    stats.MACReceiveErrors = BitConverter.ToUInt32(interfaceCountersResult.Data, 44);

                    stats.SupportsDetailedErrors = true;

                    _logger.LogCIP($"Interface Counters (Attr 12): " +
                                  $"FCS={stats.FCSErrors}, Alignment={stats.AlignmentErrors}, " +
                                  $"Collisions={stats.TotalCollisions}, MAC Errors={stats.TotalMACErrors}, " +
                                  $"CRITICAL={stats.CriticalErrors}");

                    if (stats.CriticalErrors > 0)
                    {
                        _logger.LogWarning($"⚠️ CRITICAL ERRORS DETECTED: {stats.CriticalErrors} errors indicate physical layer problems!");
                    }
                }
                else
                {
                    _logger.LogWarning($"Interface Counters data length insufficient: {interfaceCountersResult.Data.Length} bytes (expected 48)");
                    stats.SupportsDetailedErrors = false;
                }
            }
            else
            {
                // Attribute 12 not supported - this is NORMAL for many devices
                _logger.LogInfo($"Device does not support Interface Counters (Attr 12) - detailed error breakdown unavailable");
                _logger.LogInfo($"Only general error totals from Media Counters (Attr 6) will be displayed");
                stats.SupportsDetailedErrors = false;
            }

            _logger.LogInfo($"✓ Port statistics read successfully for port {portInstance}");
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to read port statistics: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Format bytes as human-readable size (B, KB, MB, GB)
    /// </summary>
    private string FormatBytes(ulong bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double displayValue = bytes;

        while (displayValue >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            displayValue /= 1024;
            suffixIndex++;
        }

        return $"{displayValue:F2} {suffixes[suffixIndex]}";
    }
}
