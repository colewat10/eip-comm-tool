using System.IO;
using System.Net;
using System.Text;

namespace EtherNetIPTool.Core.CIP;

/// <summary>
/// CIP Set_Attribute_Single message builder (REQ-3.5.5-001, REQ-3.5.5-003)
/// Builds Unconnected Send CIP payloads for setting TCP/IP Interface Object attributes
///
/// IMPORTANT: This class returns CIP payload data (Unconnected Send wrapper) WITHOUT
/// EtherNet/IP encapsulation. The caller (ConfigurationWriteService) is responsible for
/// wrapping the CIP payload in SendRRData encapsulation with proper session handle.
///
/// Per ODVA Volume 2 Section 2-4: Encapsulation layer must use session handle from
/// RegisterSession, which is only available in ConfigurationWriteService context.
///
/// Based on PRD Section 4.1.3
/// </summary>
public static class SetAttributeSingleMessage
{
    // Legacy encapsulation constants - no longer used, kept for reference
    [Obsolete("Encapsulation is now handled by ConfigurationWriteService with proper session handle")]
    private const ushort CommandSendRRData = 0x006F;  // SendRRData command
    [Obsolete("Session handle must come from RegisterSession, not hardcoded to 0x00000000")]
    private const uint SessionHandle = 0x00000000;    // WRONG: Should use RegisterSession handle
    [Obsolete("Encapsulation is now handled by ConfigurationWriteService")]
    private const uint Status = 0x00000000;
    [Obsolete("Encapsulation is now handled by ConfigurationWriteService")]
    private const uint Options = 0x00000000;

    // Legacy CPF constants - no longer used, kept for reference
    [Obsolete("CPF structure is now handled by ConfigurationWriteService")]
    private const ushort CPFTypeNullAddress = 0x0000;
    [Obsolete("CPF structure is now handled by ConfigurationWriteService")]
    private const ushort CPFTypeUnconnectedData = 0x00B2;

    // CIP Service codes
    private const byte ServiceUnconnectedSend = 0x52;
    private const byte ServiceSetAttributeSingle = 0x10;

    // CIP Class/Instance constants
    private const byte ClassMessageRouter = 0x06;
    private const byte InstanceMessageRouter = 0x01;
    private const byte ClassTCPIPInterface = 0xF5;
    private const byte InstanceTCPIPInterface = 0x01;

    // Unconnected Send parameters (PRD Section 4.1.3)
    private const byte PriorityTickTime = 0x05;       // Priority and tick time
    private const byte TimeoutTicks = 0xF9;           // Approximately 2 seconds

    /// <summary>
    /// Build Set_Attribute_Single request for Configuration Control (Attribute 3)
    /// REQ-3.6.4-006: Set to static IP mode (0x00000001) to disable DHCP
    /// Value: 0x00000001 = Static IP, 0x00000000 = DHCP/BootP mode
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="setToStaticIP">True to enable static IP mode (disable DHCP), false for DHCP mode</param>
    /// <param name="targetDeviceIP">IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetConfigurationControlRequest(bool setToStaticIP, IPAddress targetDeviceIP)
    {
        // DWORD value: 0x00000001 = Static IP, 0x00000000 = DHCP
        uint controlValue = setToStaticIP ? 0x00000001u : 0x00000000u;

        // Convert to 4-byte array (little-endian for CIP DWORD)
        byte[] controlBytes = new byte[4];
        controlBytes[0] = (byte)(controlValue & 0xFF);
        controlBytes[1] = (byte)((controlValue >> 8) & 0xFF);
        controlBytes[2] = (byte)((controlValue >> 16) & 0xFF);
        controlBytes[3] = (byte)((controlValue >> 24) & 0xFF);

        return BuildSetAttributeRequest(targetDeviceIP, 3, controlBytes);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for IP Address (Attribute 5)
    /// REQ-3.5.5-002: IP Address (4 bytes, network byte order)
    ///
    /// Per ODVA CIP Vol 1: IP addresses are transmitted in NETWORK BYTE ORDER (big-endian).
    /// This is different from CIP multi-byte values which use little-endian.
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="ipAddress">IP address to set on device</param>
    /// <param name="targetDeviceIP">Current IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetIPAddressRequest(IPAddress ipAddress, IPAddress targetDeviceIP)
    {
        if (ipAddress == null)
            throw new ArgumentNullException(nameof(ipAddress));

        byte[] ipBytes = ipAddress.GetAddressBytes();
        if (ipBytes.Length != 4)
            throw new ArgumentException("IP address must be IPv4", nameof(ipAddress));

        return BuildSetAttributeRequest(targetDeviceIP, 5, ipBytes);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for Subnet Mask (Attribute 6)
    /// REQ-3.5.5-002: Subnet Mask (4 bytes, network byte order)
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="subnetMask">Subnet mask to set on device</param>
    /// <param name="targetDeviceIP">Current IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetSubnetMaskRequest(IPAddress subnetMask, IPAddress targetDeviceIP)
    {
        if (subnetMask == null)
            throw new ArgumentNullException(nameof(subnetMask));

        byte[] maskBytes = subnetMask.GetAddressBytes();
        if (maskBytes.Length != 4)
            throw new ArgumentException("Subnet mask must be IPv4", nameof(subnetMask));

        return BuildSetAttributeRequest(targetDeviceIP, 6, maskBytes);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for Gateway (Attribute 7)
    /// REQ-3.5.5-002: Gateway Address (4 bytes, network byte order)
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="gateway">Gateway address to set on device</param>
    /// <param name="targetDeviceIP">Current IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetGatewayRequest(IPAddress gateway, IPAddress targetDeviceIP)
    {
        if (gateway == null)
            throw new ArgumentNullException(nameof(gateway));

        byte[] gatewayBytes = gateway.GetAddressBytes();
        if (gatewayBytes.Length != 4)
            throw new ArgumentException("Gateway must be IPv4", nameof(gateway));

        return BuildSetAttributeRequest(targetDeviceIP, 7, gatewayBytes);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for Hostname (Attribute 8)
    /// REQ-3.5.5-002: Hostname (String, length-prefixed ASCII)
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="hostname">Hostname to set on device (max 64 characters)</param>
    /// <param name="targetDeviceIP">Current IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetHostnameRequest(string hostname, IPAddress targetDeviceIP)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("Hostname cannot be empty", nameof(hostname));

        if (hostname.Length > 64)
            throw new ArgumentException("Hostname must be 64 characters or less", nameof(hostname));

        // CIP String format: 2-byte length + ASCII characters
        byte[] hostnameBytes = Encoding.ASCII.GetBytes(hostname);
        byte[] stringData = new byte[2 + hostnameBytes.Length];

        // Length in little-endian
        stringData[0] = (byte)hostnameBytes.Length;
        stringData[1] = (byte)(hostnameBytes.Length >> 8);
        Array.Copy(hostnameBytes, 0, stringData, 2, hostnameBytes.Length);

        return BuildSetAttributeRequest(targetDeviceIP, 8, stringData);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for DNS Server (Attribute 10)
    /// REQ-3.5.5-002: DNS Server (4 bytes, network byte order)
    ///
    /// Returns: Unconnected Send CIP payload (NOT encapsulated)
    /// Caller must wrap in SendRRData encapsulation with proper session handle
    /// </summary>
    /// <param name="dnsServer">DNS server address to set on device</param>
    /// <param name="targetDeviceIP">Current IP address of target device (used for routing)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation)</returns>
    public static byte[] BuildSetDNSServerRequest(IPAddress dnsServer, IPAddress targetDeviceIP)
    {
        if (dnsServer == null)
            throw new ArgumentNullException(nameof(dnsServer));

        byte[] dnsBytes = dnsServer.GetAddressBytes();
        if (dnsBytes.Length != 4)
            throw new ArgumentException("DNS server must be IPv4", nameof(dnsServer));

        return BuildSetAttributeRequest(targetDeviceIP, 10, dnsBytes);
    }

    /// <summary>
    /// Build Set_Attribute_Single request for any attribute
    ///
    /// Returns Unconnected Send CIP payload WITHOUT encapsulation.
    /// ConfigurationWriteService will wrap this in SendRRData encapsulation
    /// with proper session handle from RegisterSession.
    ///
    /// PRD Section 4.1.3: Unconnected Send message structure
    /// ODVA Volume 2 Section 2-4: Encapsulation layer separate from CIP layer
    /// </summary>
    /// <param name="targetDeviceIP">Target device IP (used for routing path)</param>
    /// <param name="attributeId">TCP/IP Interface Object attribute ID</param>
    /// <param name="attributeData">Attribute data bytes (format varies by attribute)</param>
    /// <returns>Unconnected Send CIP payload (without encapsulation or CPF wrapping)</returns>
    private static byte[] BuildSetAttributeRequest(IPAddress targetDeviceIP, byte attributeId, byte[] attributeData)
    {
        // Build embedded Set_Attribute_Single message
        byte[] embeddedMessage = BuildEmbeddedSetAttributeMessage(attributeId, attributeData);

        // Build Unconnected Send wrapper and return it directly
        // ConfigurationWriteService.BuildSendRRDataPacket() will:
        // 1. Add encapsulation header with proper session handle
        // 2. Add CPF structure (Interface Handle + Timeout + Item Count)
        // 3. Add CPF items (Null Address + Unconnected Data)
        // 4. Place this Unconnected Send data inside Unconnected Data item
        return BuildUnconnectedSendData(embeddedMessage, targetDeviceIP);
    }

    /// <summary>
    /// Build embedded Set_Attribute_Single message (Service 0x10)
    /// Target: TCP/IP Interface Object (Class 0xF5, Instance 1, Attribute ID)
    /// </summary>
    private static byte[] BuildEmbeddedSetAttributeMessage(byte attributeId, byte[] attributeData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Service: Set_Attribute_Single (0x10)
        writer.Write(ServiceSetAttributeSingle);

        // Request Path Size (in words): 3 words = 6 bytes
        // Path: Class 0xF5, Instance 1, Attribute ID
        writer.Write((byte)3);

        // Path Segment 1: Class 0xF5 (TCP/IP Interface Object)
        writer.Write((byte)0x20);  // 8-bit class
        writer.Write(ClassTCPIPInterface);

        // Path Segment 2: Instance 1
        writer.Write((byte)0x24);  // 8-bit instance
        writer.Write(InstanceTCPIPInterface);

        // Path Segment 3: Attribute ID
        writer.Write((byte)0x30);  // 8-bit attribute
        writer.Write(attributeId);

        // Attribute Data
        writer.Write(attributeData);

        return ms.ToArray();
    }

    /// <summary>
    /// Build Unconnected Send wrapper (Service 0x52)
    /// REQ-3.5.5-003: Use Unconnected Send via UCMM
    /// PRD Section 4.1.3: Request Path is Message Router (Class 0x06, Instance 1)
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

        // Priority/Tick Time (PRD Section 4.1.3)
        writer.Write(PriorityTickTime);

        // Timeout Ticks (PRD Section 4.1.3: 0xF9 ≈ 2 seconds)
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

    // REMOVED: BuildCPFData() and BuildEncapsulationPacket() methods
    //
    // These methods are no longer needed because:
    // 1. They created double-encapsulation (this class added encapsulation, then
    //    ConfigurationWriteService added it again)
    // 2. They hardcoded Session Handle to 0x00000000, violating ODVA spec for TCP
    //    (Session Handle must come from RegisterSession response)
    // 3. ConfigurationWriteService.BuildSendRRDataPacket() now handles all encapsulation
    //    and CPF structure with proper session handle
    //
    // Per ODVA Volume 2 Section 2-4:
    // - Encapsulation layer (SendRRData) is separate from CIP layer (Unconnected Send)
    // - Session handle is part of encapsulation, not CIP
    // - This class now returns CIP payloads only; caller handles encapsulation

    /// <summary>
    /// Parse response to extract CIP status code
    /// Returns status code byte from Set_Attribute_Single reply
    ///
    /// OBSOLETE: This method is no longer used. ConfigurationWriteService now handles
    /// all response parsing with ParseAttributeResponse() and ParseCIPResponse() methods,
    /// which provide better error handling and detailed result objects.
    ///
    /// Kept for backward compatibility only.
    /// </summary>
    [Obsolete("Use ConfigurationWriteService.ParseAttributeResponse() instead. This method uses heuristic parsing and is less reliable.")]
    public static byte ParseResponseStatus(byte[] response)
    {
        if (response == null || response.Length < 24)
            throw new ArgumentException("Response too short for encapsulation header");

        // Skip encapsulation header (24 bytes)
        int offset = 24;

        // Skip interface handle (4 bytes) and timeout (2 bytes)
        offset += 6;

        // Check if we have enough data for CPF
        if (response.Length < offset + 2)
            throw new ArgumentException("Response too short for CPF data");

        // Skip item count (2 bytes)
        offset += 2;

        // Skip first item (Null Address Item)
        // Type (2) + Length (2) + Data (0)
        offset += 4;

        // Second item (Unconnected Data Item)
        if (response.Length < offset + 4)
            throw new ArgumentException("Response too short for data item");

        // Skip type (2 bytes)
        offset += 2;

        // Read data length (2 bytes, little-endian)
        ushort dataLength = BitConverter.ToUInt16(response, offset);
        offset += 2;

        // Now we're at the Unconnected Send reply
        if (response.Length < offset + 1)
            throw new ArgumentException("Response too short for service code");

        // Service reply code (0xD2 for Unconnected Send reply)
        byte serviceReply = response[offset];
        offset += 1;

        // Reserved byte
        offset += 1;

        // General status (this is what we want for Unconnected Send)
        if (response.Length < offset + 1)
            throw new ArgumentException("Response too short for general status");

        byte unconnectedSendStatus = response[offset];
        offset += 1;

        // If Unconnected Send succeeded, look at embedded message status
        if (unconnectedSendStatus == 0x00)
        {
            // Skip additional status size
            offset += 1;

            // Skip to embedded message reply
            // This is complex - for now, look for Set_Attribute_Single reply (0x90)
            for (int i = offset; i < response.Length - 2; i++)
            {
                if (response[i] == 0x90 || response[i] == 0xD0)
                {
                    // Found service reply, status is 2 bytes later
                    if (i + 2 < response.Length)
                    {
                        return response[i + 2];
                    }
                }
            }

            // If we can't find embedded status, assume success
            return CIPStatusCodes.Success;
        }

        // Unconnected Send failed, return that status
        return unconnectedSendStatus;
    }
}
