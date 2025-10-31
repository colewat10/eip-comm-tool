using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for reading port statistics via SNMP (Simple Network Management Protocol)
/// Implements RFC 1213 (MIB-II) and RFC 2665 (EtherLike-MIB) for comprehensive port statistics
/// Used as fallback when CIP Ethernet Link Object (Class 0xF6) is not supported
/// </summary>
public class SnmpPortStatisticsService
{
    private readonly ActivityLogger _logger;
    private const int SnmpTimeout = 3000; // 3 second timeout
    private const int SnmpRetries = 1;
    private const string SnmpCommunity = "public"; // Standard read-only community

    // RFC 1213 (MIB-II) - Interface Table OIDs
    // Base: 1.3.6.1.2.1.2.2.1 (ifTable)
    private const string OID_ifInOctets = "1.3.6.1.2.1.2.2.1.10";          // Bytes in
    private const string OID_ifInUcastPkts = "1.3.6.1.2.1.2.2.1.11";       // Unicast packets in
    private const string OID_ifInNUcastPkts = "1.3.6.1.2.1.2.2.1.12";      // Non-unicast packets in (deprecated)
    private const string OID_ifInDiscards = "1.3.6.1.2.1.2.2.1.13";        // Inbound discards
    private const string OID_ifInErrors = "1.3.6.1.2.1.2.2.1.14";          // Inbound errors
    private const string OID_ifOutOctets = "1.3.6.1.2.1.2.2.1.16";         // Bytes out
    private const string OID_ifOutUcastPkts = "1.3.6.1.2.1.2.2.1.17";      // Unicast packets out
    private const string OID_ifOutNUcastPkts = "1.3.6.1.2.1.2.2.1.18";     // Non-unicast packets out (deprecated)
    private const string OID_ifOutDiscards = "1.3.6.1.2.1.2.2.1.19";       // Outbound discards
    private const string OID_ifOutErrors = "1.3.6.1.2.1.2.2.1.20";         // Outbound errors
    private const string OID_ifSpeed = "1.3.6.1.2.1.2.2.1.5";              // Interface speed (bps)
    private const string OID_ifOperStatus = "1.3.6.1.2.1.2.2.1.8";         // Operational status

    // RFC 2863 (IF-MIB) - Enhanced counters
    private const string OID_ifHCInOctets = "1.3.6.1.2.1.31.1.1.1.6";      // 64-bit counter for bytes in
    private const string OID_ifHCOutOctets = "1.3.6.1.2.1.31.1.1.1.10";    // 64-bit counter for bytes out
    private const string OID_ifInMulticastPkts = "1.3.6.1.2.1.31.1.1.1.2"; // Multicast packets in
    private const string OID_ifInBroadcastPkts = "1.3.6.1.2.1.31.1.1.1.3"; // Broadcast packets in
    private const string OID_ifOutMulticastPkts = "1.3.6.1.2.1.31.1.1.1.4";// Multicast packets out
    private const string OID_ifOutBroadcastPkts = "1.3.6.1.2.1.31.1.1.1.5";// Broadcast packets out

    // RFC 2665 (EtherLike-MIB) - Detailed Ethernet errors
    // Base: 1.3.6.1.2.1.10.7.2.1 (dot3StatsTable)
    private const string OID_dot3StatsAlignmentErrors = "1.3.6.1.2.1.10.7.2.1.2";
    private const string OID_dot3StatsFCSErrors = "1.3.6.1.2.1.10.7.2.1.3";
    private const string OID_dot3StatsSingleCollisionFrames = "1.3.6.1.2.1.10.7.2.1.4";
    private const string OID_dot3StatsMultipleCollisionFrames = "1.3.6.1.2.1.10.7.2.1.5";
    private const string OID_dot3StatsSQETestErrors = "1.3.6.1.2.1.10.7.2.1.6";
    private const string OID_dot3StatsDeferredTransmissions = "1.3.6.1.2.1.10.7.2.1.7";
    private const string OID_dot3StatsLateCollisions = "1.3.6.1.2.1.10.7.2.1.8";
    private const string OID_dot3StatsExcessiveCollisions = "1.3.6.1.2.1.10.7.2.1.9";
    private const string OID_dot3StatsInternalMacTransmitErrors = "1.3.6.1.2.1.10.7.2.1.10";
    private const string OID_dot3StatsCarrierSenseErrors = "1.3.6.1.2.1.10.7.2.1.11";
    private const string OID_dot3StatsFrameTooLongs = "1.3.6.1.2.1.10.7.2.1.13";
    private const string OID_dot3StatsInternalMacReceiveErrors = "1.3.6.1.2.1.10.7.2.1.16";
    private const string OID_dot3StatsDuplexStatus = "1.3.6.1.2.1.10.7.2.1.19"; // 1=unknown, 2=half, 3=full

    public SnmpPortStatisticsService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Read port statistics via SNMP
    /// </summary>
    /// <param name="device">Target device</param>
    /// <param name="portIndex">SNMP ifIndex (typically 1 for port 1, 2 for port 2)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PortStatistics object or null if read failed</returns>
    public async Task<PortStatistics?> ReadPortStatisticsAsync(
        Device device,
        int portIndex = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo($"📡 Reading port statistics via SNMP from {device.IPAddressString}, ifIndex {portIndex}");

            var stats = new PortStatistics
            {
                DeviceName = device.ProductName,
                DeviceIP = device.IPAddressString,
                PortNumber = portIndex,
                LastUpdated = DateTime.Now
            };

            var endpoint = new IPEndPoint(device.IPAddress, 161); // SNMP port 161
            var community = new OctetString(SnmpCommunity);

            // Build list of OIDs to query
            var oids = new List<Variable>();

            // === Basic Interface Statistics (RFC 1213) ===
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInOctets}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInUcastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInNUcastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInDiscards}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutOctets}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutUcastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutNUcastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutDiscards}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifSpeed}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOperStatus}.{portIndex}")));

            // === Enhanced counters (RFC 2863) - Try but don't fail if not supported ===
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInMulticastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifInBroadcastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutMulticastPkts}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_ifOutBroadcastPkts}.{portIndex}")));

            // === Detailed Ethernet Errors (RFC 2665) ===
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsAlignmentErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsFCSErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsSingleCollisionFrames}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsMultipleCollisionFrames}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsSQETestErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsDeferredTransmissions}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsLateCollisions}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsExcessiveCollisions}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsInternalMacTransmitErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsCarrierSenseErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsFrameTooLongs}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsInternalMacReceiveErrors}.{portIndex}")));
            oids.Add(new Variable(new ObjectIdentifier($"{OID_dot3StatsDuplexStatus}.{portIndex}")));

            _logger.LogCIP($"SNMP GET request for {oids.Count} OIDs to {endpoint}");
            _logger.LogCIP($"   Using SNMPv2c, community='{SnmpCommunity}', timeout={SnmpTimeout}ms");

            // Perform SNMP GET request
            var response = await Task.Run(() =>
                Messenger.Get(
                    VersionCode.V2, // Try SNMPv2c first (supports 64-bit counters)
                    endpoint,
                    community,
                    oids,
                    SnmpTimeout),
                cancellationToken);

            _logger.LogCIP($"✅ SNMP response received: {response.Count} variables");

            // Parse response
            foreach (var variable in response)
            {
                ParseSnmpVariable(variable, stats, portIndex);
            }

            // Check if we got basic data
            if (stats.BytesIn == 0 && stats.BytesOut == 0)
            {
                _logger.LogWarning("SNMP returned all zeros - device may not support SNMP or ifIndex is wrong");
                return null;
            }

            // Calculate MediaCounters array for compatibility with CIP-based display
            PopulateMediaCounters(stats);

            _logger.LogInfo($"✅ SNMP port statistics read successfully");
            _logger.LogInfo($"   RX: {stats.PacketsIn:N0} pkts, {FormatBytes(stats.BytesIn)}");
            _logger.LogInfo($"   TX: {stats.PacketsOut:N0} pkts, {FormatBytes(stats.BytesOut)}");
            _logger.LogInfo($"   Errors: {stats.ErrorsIn + stats.ErrorsOut} total");

            if (stats.SupportsDetailedErrors)
            {
                _logger.LogInfo($"   Detailed errors: FCS={stats.FCSErrors}, Alignment={stats.AlignmentErrors}, Collisions={stats.TotalCollisions}");
            }

            return stats;
        }
        catch (Lextm.SharpSnmpLib.Messaging.TimeoutException ex)
        {
            _logger.LogError($"❌ SNMP timeout after {SnmpTimeout}ms - device not responding on UDP port 161");
            _logger.LogError($"   Possible causes:");
            _logger.LogError($"   1. SNMP disabled on device (check device web interface)");
            _logger.LogError($"   2. Firewall blocking UDP port 161 (Windows Firewall or device)");
            _logger.LogError($"   3. Wrong community string (tried: '{SnmpCommunity}')");
            _logger.LogError($"   4. Device only supports SNMPv1 or requires SNMPv3 authentication");
            _logger.LogError($"   Exception: {ex.Message}");
            return null;
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _logger.LogError($"❌ SNMP socket error: {ex.Message}");
            _logger.LogError($"   Error code: {ex.ErrorCode}");
            _logger.LogError($"   This may indicate a network connectivity issue or firewall blocking");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ SNMP read failed: {ex.GetType().Name}");
            _logger.LogError($"   Message: {ex.Message}");
            _logger.LogError($"   This may indicate incompatible SNMP implementation or wrong OIDs");
            if (ex.InnerException != null)
            {
                _logger.LogError($"   Inner exception: {ex.InnerException.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// Parse individual SNMP variable and populate stats
    /// </summary>
    private void ParseSnmpVariable(Variable variable, PortStatistics stats, int portIndex)
    {
        try
        {
            string oid = variable.Id.ToString();
            var data = variable.Data;

            // Basic counters
            if (oid.StartsWith($"{OID_ifInOctets}.")) stats.MediaCounters[0] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifInUcastPkts}.")) stats.MediaCounters[1] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifInNUcastPkts}.")) stats.MediaCounters[2] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifInDiscards}.")) stats.MediaCounters[3] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifInErrors}.")) stats.MediaCounters[4] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifOutOctets}.")) stats.MediaCounters[6] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifOutUcastPkts}.")) stats.MediaCounters[7] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifOutNUcastPkts}.")) stats.MediaCounters[8] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifOutDiscards}.")) stats.MediaCounters[9] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifOutErrors}.")) stats.MediaCounters[10] = GetCounter32(data);
            else if (oid.StartsWith($"{OID_ifSpeed}."))
            {
                uint speedBps = GetCounter32(data);
                stats.InterfaceSpeed = speedBps / 1_000_000; // Convert bps to Mbps
            }
            else if (oid.StartsWith($"{OID_ifOperStatus}."))
            {
                int status = GetInteger(data);
                stats.InterfaceFlags = (status == 1) ? 0x01u : 0x00u; // 1=up, set bit 0
            }

            // Detailed Ethernet errors (RFC 2665)
            else if (oid.StartsWith($"{OID_dot3StatsAlignmentErrors}."))
            {
                stats.AlignmentErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsFCSErrors}."))
            {
                stats.FCSErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsSingleCollisionFrames}."))
            {
                stats.SingleCollisionFrames = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsMultipleCollisionFrames}."))
            {
                stats.MultipleCollisionFrames = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsSQETestErrors}."))
            {
                stats.SQETestErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsDeferredTransmissions}."))
            {
                stats.DeferredTransmissions = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsLateCollisions}."))
            {
                stats.LateCollisions = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsExcessiveCollisions}."))
            {
                stats.ExcessiveCollisions = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsInternalMacTransmitErrors}."))
            {
                stats.MACTransmitErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsCarrierSenseErrors}."))
            {
                stats.CarrierSenseErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsFrameTooLongs}."))
            {
                stats.FrameTooLongErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsInternalMacReceiveErrors}."))
            {
                stats.MACReceiveErrors = GetCounter32(data);
                stats.SupportsDetailedErrors = true;
            }
            else if (oid.StartsWith($"{OID_dot3StatsDuplexStatus}."))
            {
                int duplex = GetInteger(data);
                // 1=unknown, 2=half, 3=full
                if (duplex == 3)
                    stats.InterfaceFlags |= 0x02; // Set bit 1 for full duplex
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to parse SNMP variable {variable.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Populate MediaCounters array for compatibility
    /// </summary>
    private void PopulateMediaCounters(PortStatistics stats)
    {
        // MediaCounters already populated in ParseSnmpVariable
        // Index 0: In Octets - done
        // Index 1: In Ucast Packets - done
        // Index 2: In NUcast Packets - done
        // Index 3: In Discards - done
        // Index 4: In Errors - done
        // Index 5: In Unknown Protocols - not available via SNMP, leave at 0
        // Index 6: Out Octets - done
        // Index 7: Out Ucast Packets - done
        // Index 8: Out NUcast Packets - done
        // Index 9: Out Discards - done
        // Index 10: Out Errors - done
    }

    private uint GetCounter32(ISnmpData data)
    {
        if (data is Counter32 counter32)
            return counter32.ToUInt32();
        if (data is Gauge32 gauge32)
            return gauge32.ToUInt32();
        if (data is Integer32 int32)
            return (uint)int32.ToInt32();
        return 0;
    }

    private int GetInteger(ISnmpData data)
    {
        if (data is Integer32 int32)
            return int32.ToInt32();
        return 0;
    }

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
