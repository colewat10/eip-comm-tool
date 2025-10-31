using System.IO;
using System.Net;

namespace EtherNetIPTool.Core.CIP;

/// <summary>
/// CIP Get_Attribute_Single message builder (Service 0x0E)
/// Builds Unconnected Send CIP payloads for reading attributes from CIP objects
///
/// IMPORTANT: This class returns CIP payload data (Unconnected Send wrapper) WITHOUT
/// EtherNet/IP encapsulation. The caller (ConfigurationWriteService) is responsible for
/// wrapping the CIP payload in SendRRData encapsulation with proper session handle.
///
/// Per ODVA Volume 2 Section 2-4: Encapsulation layer must use session handle from
/// RegisterSession, which is only available in ConfigurationWriteService context.
///
/// Used for reading port statistics from:
/// - TCP/IP Interface Object (Class 0xF5) - IP-layer statistics
/// - Ethernet Link Object (Class 0xF6) - Port statistics (packets, errors, collisions)
/// </summary>
public static class GetAttributeSingleMessage
{
    // CIP Service codes
    private const byte ServiceGetAttributeSingle = 0x0E;
    private const byte ServiceGetAttributeAll = 0x01;
    private const byte ServiceUnconnectedSend = 0x52;

    // CIP Class/Instance constants
    private const byte ClassMessageRouter = 0x06;
    private const byte InstanceMessageRouter = 0x01;
    private const byte ClassTCPIPInterface = 0xF5;
    private const byte ClassEthernetLink = 0xF6;

    // Unconnected Send parameters (same as SetAttributeSingle)
    private const byte PriorityTickTime = 0x05;       // Priority and tick time
    private const byte TimeoutTicks = 0xF9;           // Approximately 2 seconds

    /// <summary>
    /// Build Get_Attribute_Single request for TCP/IP Interface Object (Class 0xF5)
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="attributeId">Attribute ID to read</param>
    /// <param name="targetDeviceIP">Target device IP address (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildGetTcpIpAttributeRequest(byte attributeId, IPAddress targetDeviceIP)
    {
        return BuildGetAttributeRequest(ClassTCPIPInterface, 0x01, attributeId, targetDeviceIP);
    }

    /// <summary>
    /// Build Get_Attribute_Single request for Ethernet Link Object (Class 0xF6)
    /// Used to read port statistics including Media Counters (Attr 6) and Interface Counters (Attr 12)
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="instanceId">Instance ID (typically 1 for port 1, 2 for port 2)</param>
    /// <param name="attributeId">Attribute ID to read</param>
    /// <param name="targetDeviceIP">Target device IP address (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildGetEthernetLinkAttributeRequest(byte instanceId, byte attributeId, IPAddress targetDeviceIP)
    {
        return BuildGetAttributeRequest(ClassEthernetLink, instanceId, attributeId, targetDeviceIP);
    }

    /// <summary>
    /// Build Get_Attribute_Single request for any CIP object
    /// Returns Unconnected Send CIP payload WITHOUT encapsulation.
    /// ConfigurationWriteService will wrap this in SendRRData encapsulation
    /// with proper session handle from RegisterSession.
    /// </summary>
    /// <param name="classId">CIP Class ID (e.g., 0xF5 for TCP/IP Interface, 0xF6 for Ethernet Link)</param>
    /// <param name="instanceId">Instance ID (typically 1)</param>
    /// <param name="attributeId">Attribute ID to read</param>
    /// <param name="targetDeviceIP">Target device IP (used for routing path)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    private static byte[] BuildGetAttributeRequest(byte classId, byte instanceId, byte attributeId, IPAddress targetDeviceIP)
    {
        // Build embedded Get_Attribute_Single message
        byte[] embeddedMessage = BuildEmbeddedGetAttributeMessage(classId, instanceId, attributeId);

        // Build Unconnected Send wrapper and return it directly
        // ConfigurationWriteService.BuildSendRRDataPacket() will:
        // 1. Add encapsulation header with proper session handle
        // 2. Add CPF structure (Interface Handle + Timeout + Item Count)
        // 3. Add CPF items (Null Address + Unconnected Data)
        // 4. Place this Unconnected Send data inside Unconnected Data item
        return BuildUnconnectedSendData(embeddedMessage, targetDeviceIP);
    }

    /// <summary>
    /// Build embedded Get_Attribute_Single message (Service 0x0E)
    /// Target: Any CIP Object (Class, Instance, Attribute)
    /// </summary>
    private static byte[] BuildEmbeddedGetAttributeMessage(byte classId, byte instanceId, byte attributeId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Service: Get_Attribute_Single (0x0E)
        writer.Write(ServiceGetAttributeSingle);

        // Request Path Size (in words): 3 words = 6 bytes
        // Path: Class, Instance, Attribute
        writer.Write((byte)3);

        // Path Segment 1: Class
        writer.Write((byte)0x20);  // 8-bit class
        writer.Write(classId);

        // Path Segment 2: Instance
        writer.Write((byte)0x24);  // 8-bit instance
        writer.Write(instanceId);

        // Path Segment 3: Attribute
        writer.Write((byte)0x30);  // 8-bit attribute
        writer.Write(attributeId);

        return ms.ToArray();
    }

    /// <summary>
    /// Build Unconnected Send wrapper (Service 0x52)
    /// Same structure as SetAttributeSingleMessage
    /// Request Path is Message Router (Class 0x06, Instance 1)
    /// </summary>
    private static byte[] BuildUnconnectedSendData(byte[] embeddedMessage, IPAddress targetDeviceIP)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Service: Unconnected Send (0x52)
        writer.Write(ServiceUnconnectedSend);

        // Request Path Size: 2 words (4 bytes)
        writer.Write((byte)2);

        // Request Path: Message Router (Class 0x06, Instance 1)
        writer.Write((byte)0x20);  // 8-bit class
        writer.Write(ClassMessageRouter);
        writer.Write((byte)0x24);  // 8-bit instance
        writer.Write(InstanceMessageRouter);

        // Priority/Tick Time
        writer.Write(PriorityTickTime);

        // Timeout Ticks (0xF9 ≈ 2 seconds)
        writer.Write(TimeoutTicks);

        // Embedded message length (little-endian)
        writer.Write((ushort)embeddedMessage.Length);

        // Embedded message
        writer.Write(embeddedMessage);

        // Route Path: Send to device IP
        // For unconnected, we use port 1, backplane 0 (typical for EIP devices)
        writer.Write((byte)1);     // Path size in words
        writer.Write((byte)0x00);  // Padding
        writer.Write((byte)0x01);  // Port 1 (backplane)
        writer.Write((byte)0x00);  // Address 0

        return ms.ToArray();
    }

    /// <summary>
    /// Build Get_Attribute_All request for Ethernet Link Object (Class 0xF6)
    /// Some devices (like Turck) don't support Get_Attribute_Single but do support Get_Attribute_All
    /// This reads ALL attributes of the object instance at once
    /// </summary>
    /// <param name="instanceId">Instance ID (typically 1 for port 1)</param>
    /// <param name="targetDeviceIP">Target device IP address</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildGetEthernetLinkAllAttributesRequest(byte instanceId, IPAddress targetDeviceIP)
    {
        // Build embedded Get_Attribute_All message (Service 0x01)
        byte[] embeddedMessage = BuildEmbeddedGetAllAttributesMessage(ClassEthernetLink, instanceId);

        // Wrap in Unconnected Send
        return BuildUnconnectedSendData(embeddedMessage, targetDeviceIP);
    }

    /// <summary>
    /// Build embedded Get_Attribute_All message (Service 0x01)
    /// Simpler than Get_Attribute_Single - no attribute ID needed
    /// </summary>
    private static byte[] BuildEmbeddedGetAllAttributesMessage(byte classId, byte instanceId)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Service: Get_Attribute_All (0x01)
        writer.Write(ServiceGetAttributeAll);

        // Request Path Size (in words): 2 words = 4 bytes
        // Path: Class, Instance (NO Attribute needed for Get_Attribute_All)
        writer.Write((byte)2);

        // Path Segment 1: Class
        writer.Write((byte)0x20);  // 8-bit class
        writer.Write(classId);

        // Path Segment 2: Instance
        writer.Write((byte)0x24);  // 8-bit instance
        writer.Write(instanceId);

        return ms.ToArray();
    }
}
