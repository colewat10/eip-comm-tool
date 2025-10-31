namespace EtherNetIPTool.Models;

/// <summary>
/// Network port statistics for an EtherNet/IP device
/// Combines data from Ethernet Link Object (Class 0xF6):
/// - Attribute 4: Interface Speed
/// - Attribute 5: Interface Flags (link status, duplex)
/// - Attribute 6: Media Counters (general traffic stats - 11 DINTs)
/// - Attribute 12: Interface Counters (detailed error breakdown per RFC 2665 - 12 DINTs)
///
/// Per ODVA CIP specification and RFC 2665 (Ethernet-like MIB)
/// </summary>
public class PortStatistics
{
    // === IDENTIFICATION ===
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIP { get; set; } = string.Empty;
    public int PortNumber { get; set; } = 1;
    public DateTime LastUpdated { get; set; }

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 4: INTERFACE SPEED ===
    /// <summary>Interface Speed (Attr 4) - Mbps (10, 100, 1000, 10000)</summary>
    public uint InterfaceSpeed { get; set; }

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 5: INTERFACE FLAGS ===
    /// <summary>Interface Flags (Attr 5) - Link status, duplex mode, negotiation status</summary>
    public uint InterfaceFlags { get; set; }

    /// <summary>Link Up/Down (derived from InterfaceFlags bit 0)</summary>
    public bool LinkStatus => (InterfaceFlags & 0x01) != 0;

    /// <summary>Full/Half Duplex (derived from InterfaceFlags bit 1: 0=half, 1=full)</summary>
    public bool IsFullDuplex => (InterfaceFlags & 0x02) != 0;

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 6: MEDIA COUNTERS ===
    /// <summary>
    /// Media Counters (Attr 6) - Array of 11 DINTs (44 bytes total)
    /// Index 0: In Octets (Bytes Received)
    /// Index 1: In Ucast Packets (Unicast packets received)
    /// Index 2: In NUcast Packets (Multicast + Broadcast received)
    /// Index 3: In Discards (Inbound packets discarded)
    /// Index 4: In Errors (Inbound packets with errors)
    /// Index 5: In Unknown Protocols (Packets with unsupported protocol)
    /// Index 6: Out Octets (Bytes Transmitted)
    /// Index 7: Out Ucast Packets (Unicast packets transmitted)
    /// Index 8: Out NUcast Packets (Multicast + Broadcast transmitted)
    /// Index 9: Out Discards (Outbound packets discarded)
    /// Index 10: Out Errors (Outbound packets with errors)
    /// </summary>
    public uint[] MediaCounters { get; set; } = new uint[11];

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 8: INTERFACE TYPE ===
    /// <summary>Interface Type (Attr 8) - Internal, twisted pair, fiber, etc.</summary>
    public byte InterfaceType { get; set; }

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 9: INTERFACE STATE ===
    /// <summary>Interface State (Attr 9) - Unknown, enabled, disabled, testing</summary>
    public byte InterfaceState { get; set; }

    // === ETHERNET LINK OBJECT (Class 0xF6) - ATTRIBUTE 12: INTERFACE COUNTERS ===
    // RFC 2665 Ethernet-like MIB - Detailed error breakdown (12 DINTs = 48 bytes)

    /// <summary>Alignment Errors - Frames not ending on octet boundary with bad FCS</summary>
    public uint AlignmentErrors { get; set; }

    /// <summary>FCS Errors - Frames with correct length but bad Frame Check Sequence (CRC errors)</summary>
    public uint FCSErrors { get; set; }

    /// <summary>Single Collision Frames - Successfully transmitted after exactly 1 collision</summary>
    public uint SingleCollisionFrames { get; set; }

    /// <summary>Multiple Collision Frames - Successfully transmitted after 2-15 collisions</summary>
    public uint MultipleCollisionFrames { get; set; }

    /// <summary>SQE Test Errors - Signal Quality Error test counter (10Base5 heartbeat)</summary>
    public uint SQETestErrors { get; set; }

    /// <summary>Deferred Transmissions - First transmission delayed due to medium busy</summary>
    public uint DeferredTransmissions { get; set; }

    /// <summary>Late Collisions - Collisions detected after 512 bit times (cable too long or duplex mismatch)</summary>
    public uint LateCollisions { get; set; }

    /// <summary>Excessive Collisions - Frames that failed due to 16 collision attempts</summary>
    public uint ExcessiveCollisions { get; set; }

    /// <summary>MAC Transmit Errors - Internal MAC sublayer transmit errors</summary>
    public uint MACTransmitErrors { get; set; }

    /// <summary>Carrier Sense Errors - Times carrier sense lost or never asserted during transmission</summary>
    public uint CarrierSenseErrors { get; set; }

    /// <summary>Frame Too Long - Frames exceeding maximum permitted frame size (1518 bytes for Ethernet)</summary>
    public uint FrameTooLongErrors { get; set; }

    /// <summary>MAC Receive Errors - Internal MAC sublayer receive errors</summary>
    public uint MACReceiveErrors { get; set; }

    /// <summary>Indicates if Attribute 12 (Interface Counters) is supported by this device</summary>
    public bool SupportsDetailedErrors { get; set; }

    // === DERIVED STATISTICS FROM MEDIA COUNTERS (ATTRIBUTE 6) ===

    public ulong PacketsIn => MediaCounters[1] + MediaCounters[2];
    public ulong PacketsOut => MediaCounters[7] + MediaCounters[8];
    public ulong BytesIn => MediaCounters[0];
    public ulong BytesOut => MediaCounters[6];
    public ulong ErrorsIn => MediaCounters[4];  // General errors (total)
    public ulong ErrorsOut => MediaCounters[10]; // General errors (total)
    public ulong DiscardsIn => MediaCounters[3];
    public ulong DiscardsOut => MediaCounters[9];
    public ulong MulticastIn => MediaCounters[2];  // NUcast = Multicast + Broadcast
    public ulong MulticastOut => MediaCounters[8];

    // === COMPUTED ERROR TOTALS FROM INTERFACE COUNTERS (ATTRIBUTE 12) ===

    /// <summary>Total collisions (all types: single, multiple, late, excessive)</summary>
    public ulong TotalCollisions =>
        (ulong)SingleCollisionFrames +
        MultipleCollisionFrames +
        LateCollisions +
        ExcessiveCollisions;

    /// <summary>Total physical layer errors (FCS + Alignment + Frame Too Long)</summary>
    public ulong TotalPhysicalErrors =>
        (ulong)FCSErrors + AlignmentErrors + FrameTooLongErrors;

    /// <summary>Total MAC layer errors (TX + RX + Carrier Sense)</summary>
    public ulong TotalMACErrors =>
        (ulong)MACTransmitErrors + MACReceiveErrors + CarrierSenseErrors;

    /// <summary>
    /// Critical errors that indicate serious physical problems
    /// (FCS, Alignment, Excessive Collisions, Late Collisions)
    /// These require immediate attention as they indicate:
    /// - Bad cabling (FCS, Alignment)
    /// - Cable too long or duplex mismatch (Late Collisions)
    /// - Network congestion or hardware failure (Excessive Collisions)
    /// </summary>
    public ulong CriticalErrors =>
        (ulong)FCSErrors + AlignmentErrors + ExcessiveCollisions + LateCollisions;

    // === DISPLAY FORMATTERS ===

    /// <summary>Link speed formatted as text (10 Mbps, 100 Mbps, 1 Gbps, etc.)</summary>
    public string LinkSpeedText => InterfaceSpeed switch
    {
        10 => "10 Mbps",
        100 => "100 Mbps",
        1000 => "1 Gbps",
        10000 => "10 Gbps",
        _ => $"{InterfaceSpeed} Mbps"
    };

    /// <summary>Duplex mode formatted as text</summary>
    public string DuplexText => IsFullDuplex ? "Full Duplex" : "Half Duplex";

    /// <summary>Link status formatted as text</summary>
    public string LinkStatusText => LinkStatus ? "Up" : "Down";

    /// <summary>Interface state formatted as text</summary>
    public string InterfaceStateText => InterfaceState switch
    {
        1 => "Unknown",
        2 => "Enabled",
        3 => "Disabled",
        4 => "Testing",
        _ => $"Unknown ({InterfaceState})"
    };

    /// <summary>Interface type formatted as text</summary>
    public string InterfaceTypeText => InterfaceType switch
    {
        1 => "Internal",
        2 => "Twisted Pair",
        3 => "Fiber",
        _ => $"Type {InterfaceType}"
    };
}
