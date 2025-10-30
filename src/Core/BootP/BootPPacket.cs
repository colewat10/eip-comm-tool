using System.Net;
using System.Text;

namespace EtherNetIPTool.Core.BootP;

/// <summary>
/// BootP/DHCP packet structure and parsing
/// Implements BootP protocol per RFC 951 and DHCP options per RFC 2132
/// REQ-3.6.2, REQ-3.6.4
/// </summary>
public class BootPPacket
{
    /// <summary>
    /// BootP operation codes
    /// </summary>
    public enum BootPOpCode : byte
    {
        BOOTREQUEST = 0x01,  // Client request
        BOOTREPLY = 0x02     // Server reply
    }

    /// <summary>
    /// Hardware type constants
    /// </summary>
    public const byte HTYPE_ETHERNET = 0x01;

    /// <summary>
    /// Hardware address length for Ethernet
    /// </summary>
    public const byte HLEN_ETHERNET = 0x06;

    /// <summary>
    /// BootP packet minimum size (without options)
    /// </summary>
    public const int MINIMUM_PACKET_SIZE = 300;

    /// <summary>
    /// DHCP magic cookie value (0x63825363)
    /// </summary>
    public static readonly byte[] DHCP_MAGIC_COOKIE = { 0x63, 0x82, 0x53, 0x63 };

    // BootP packet fields
    public BootPOpCode Op { get; set; }
    public byte Htype { get; set; } = HTYPE_ETHERNET;
    public byte Hlen { get; set; } = HLEN_ETHERNET;
    public byte Hops { get; set; } = 0;
    public uint Xid { get; set; }  // Transaction ID
    public ushort Secs { get; set; } = 0;
    public ushort Flags { get; set; } = 0;
    public IPAddress Ciaddr { get; set; } = IPAddress.Any;  // Client IP
    public IPAddress Yiaddr { get; set; } = IPAddress.Any;  // Your (client) IP
    public IPAddress Siaddr { get; set; } = IPAddress.Any;  // Server IP
    public IPAddress Giaddr { get; set; } = IPAddress.Any;  // Gateway IP
    public byte[] Chaddr { get; set; } = new byte[16];      // Client hardware address
    public byte[] Sname { get; set; } = new byte[64];       // Server host name
    public byte[] File { get; set; } = new byte[128];       // Boot file name
    public byte[] Options { get; set; } = Array.Empty<byte>(); // DHCP options

    /// <summary>
    /// Parse BootP packet from received bytes
    /// REQ-3.6.2-001: Parse BOOTREQUEST packets
    /// </summary>
    /// <param name="data">Raw packet data</param>
    /// <returns>Parsed BootPPacket or null if invalid</returns>
    public static BootPPacket? Parse(byte[] data)
    {
        if (data == null || data.Length < MINIMUM_PACKET_SIZE)
            return null;

        try
        {
            var packet = new BootPPacket();
            int offset = 0;

            // Op (1 byte)
            packet.Op = (BootPOpCode)data[offset++];

            // Htype (1 byte)
            packet.Htype = data[offset++];

            // Hlen (1 byte)
            packet.Hlen = data[offset++];

            // Hops (1 byte)
            packet.Hops = data[offset++];

            // Xid (4 bytes, network byte order - big-endian)
            packet.Xid = (uint)((data[offset++] << 24) |
                                (data[offset++] << 16) |
                                (data[offset++] << 8) |
                                 data[offset++]);

            // Secs (2 bytes, network byte order)
            packet.Secs = (ushort)((data[offset++] << 8) | data[offset++]);

            // Flags (2 bytes, network byte order)
            packet.Flags = (ushort)((data[offset++] << 8) | data[offset++]);

            // CIADDR (4 bytes)
            packet.Ciaddr = new IPAddress(new[] { data[offset++], data[offset++], data[offset++], data[offset++] });

            // YIADDR (4 bytes)
            packet.Yiaddr = new IPAddress(new[] { data[offset++], data[offset++], data[offset++], data[offset++] });

            // SIADDR (4 bytes)
            packet.Siaddr = new IPAddress(new[] { data[offset++], data[offset++], data[offset++], data[offset++] });

            // GIADDR (4 bytes)
            packet.Giaddr = new IPAddress(new[] { data[offset++], data[offset++], data[offset++], data[offset++] });

            // CHADDR (16 bytes)
            Array.Copy(data, offset, packet.Chaddr, 0, 16);
            offset += 16;

            // SNAME (64 bytes)
            Array.Copy(data, offset, packet.Sname, 0, 64);
            offset += 64;

            // FILE (128 bytes)
            Array.Copy(data, offset, packet.File, 0, 128);
            offset += 128;

            // OPTIONS (remaining bytes)
            if (offset < data.Length)
            {
                int optionsLength = data.Length - offset;
                packet.Options = new byte[optionsLength];
                Array.Copy(data, offset, packet.Options, 0, optionsLength);
            }

            return packet;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Build BOOTREPLY packet bytes
    /// REQ-3.6.4-001: Send BootP BOOTREPLY with assigned IP
    /// </summary>
    /// <param name="request">Original request packet</param>
    /// <param name="assignedIP">IP address to assign to client</param>
    /// <param name="serverIP">Server IP address (selected NIC)</param>
    /// <param name="subnetMask">Subnet mask to assign</param>
    /// <param name="gateway">Optional gateway/router IP</param>
    /// <returns>BOOTREPLY packet bytes ready to send</returns>
    public static byte[] BuildReply(BootPPacket request, IPAddress assignedIP, IPAddress serverIP,
                                     IPAddress subnetMask, IPAddress? gateway = null)
    {
        var packet = new byte[MINIMUM_PACKET_SIZE + 256]; // Extra space for options
        int offset = 0;

        // Op = BOOTREPLY (0x02)
        packet[offset++] = (byte)BootPOpCode.BOOTREPLY;

        // Htype, Hlen, Hops (copy from request)
        packet[offset++] = request.Htype;
        packet[offset++] = request.Hlen;
        packet[offset++] = 0; // Hops = 0

        // Xid (4 bytes, network byte order - copy from request)
        packet[offset++] = (byte)((request.Xid >> 24) & 0xFF);
        packet[offset++] = (byte)((request.Xid >> 16) & 0xFF);
        packet[offset++] = (byte)((request.Xid >> 8) & 0xFF);
        packet[offset++] = (byte)(request.Xid & 0xFF);

        // Secs (2 bytes) = 0
        packet[offset++] = 0;
        packet[offset++] = 0;

        // Flags (2 bytes, network byte order - copy from request)
        packet[offset++] = (byte)((request.Flags >> 8) & 0xFF);
        packet[offset++] = (byte)(request.Flags & 0xFF);

        // CIADDR (4 bytes) = 0.0.0.0
        offset += 4;

        // YIADDR (4 bytes) = assigned IP address
        var yiaddrBytes = assignedIP.GetAddressBytes();
        Array.Copy(yiaddrBytes, 0, packet, offset, 4);
        offset += 4;

        // SIADDR (4 bytes) = server IP
        var siaddrBytes = serverIP.GetAddressBytes();
        Array.Copy(siaddrBytes, 0, packet, offset, 4);
        offset += 4;

        // GIADDR (4 bytes) = 0.0.0.0 or gateway if specified
        if (gateway != null)
        {
            var giaddrBytes = gateway.GetAddressBytes();
            Array.Copy(giaddrBytes, 0, packet, offset, 4);
        }
        offset += 4;

        // CHADDR (16 bytes) - copy from request
        Array.Copy(request.Chaddr, 0, packet, offset, 16);
        offset += 16;

        // SNAME (64 bytes) - "EtherNetIPTool"
        var sname = Encoding.ASCII.GetBytes("EtherNetIPTool");
        Array.Copy(sname, 0, packet, offset, Math.Min(sname.Length, 64));
        offset += 64;

        // FILE (128 bytes) - empty
        offset += 128;

        // OPTIONS - DHCP magic cookie and options
        // Magic Cookie: 0x63 0x82 0x53 0x63
        Array.Copy(DHCP_MAGIC_COOKIE, 0, packet, offset, 4);
        offset += 4;

        // Option 1: Subnet Mask (REQ-3.6.4-002)
        packet[offset++] = 1;    // Option code: Subnet Mask
        packet[offset++] = 4;    // Length: 4 bytes
        var maskBytes = subnetMask.GetAddressBytes();
        Array.Copy(maskBytes, 0, packet, offset, 4);
        offset += 4;

        // Option 3: Router/Gateway (if provided)
        if (gateway != null)
        {
            packet[offset++] = 3;    // Option code: Router
            packet[offset++] = 4;    // Length: 4 bytes
            var gwBytes = gateway.GetAddressBytes();
            Array.Copy(gwBytes, 0, packet, offset, 4);
            offset += 4;
        }

        // Option 255: End (REQ-3.6.4-002)
        packet[offset++] = 255;

        // Return packet with actual size
        var result = new byte[offset];
        Array.Copy(packet, 0, result, 0, offset);
        return result;
    }

    /// <summary>
    /// Get client MAC address from CHADDR field
    /// Returns first 6 bytes (Ethernet MAC)
    /// </summary>
    public byte[] GetClientMacAddress()
    {
        var mac = new byte[6];
        Array.Copy(Chaddr, 0, mac, 0, 6);
        return mac;
    }

    /// <summary>
    /// Get client MAC address as formatted string (XX:XX:XX:XX:XX:XX)
    /// </summary>
    public string GetClientMacAddressString()
    {
        var mac = GetClientMacAddress();
        return string.Join(":", mac.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Check if broadcast flag is set in FLAGS field
    /// </summary>
    public bool IsBroadcastFlagSet()
    {
        return (Flags & 0x8000) != 0;
    }
}
