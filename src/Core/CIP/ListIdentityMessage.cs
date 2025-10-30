using System.Net;
using System.Text;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;

namespace EtherNetIPTool.Core.CIP;

/// <summary>
/// CIP List Identity message builder and parser
/// Used for device discovery via UDP broadcast
/// Reference: CIP Networks Library Vol 2, Section 2-4.2
/// PRD Section 4.1.1
/// </summary>
public static class ListIdentityMessage
{
    /// <summary>
    /// Build a List Identity request packet (REQ-3.3.1-001, REQ-3.3.1-002)
    /// </summary>
    /// <param name="senderContext">8-byte context for matching request/response (optional)</param>
    /// <returns>Complete List Identity request packet (24 bytes)</returns>
    public static byte[] BuildRequest(byte[]? senderContext = null)
    {
        var header = new CIPEncapsulationHeader
        {
            Command = (ushort)CIPEncapsulationCommand.ListIdentity,
            Length = 0,  // No encapsulated data for List Identity request
            SessionHandle = 0x00000000,
            Status = 0x00000000,
            SenderContext = senderContext ?? GenerateSenderContext(),
            Options = 0x00000000
        };

        return header.ToBytes();
    }

    /// <summary>
    /// Generate a random 8-byte sender context
    /// </summary>
    private static byte[] GenerateSenderContext()
    {
        var context = new byte[8];
        Random.Shared.NextBytes(context);
        return context;
    }

    /// <summary>
    /// Parse List Identity response and extract device information
    /// (REQ-3.3.1-004)
    /// </summary>
    /// <param name="buffer">Response packet buffer</param>
    /// <param name="length">Length of response data</param>
    /// <returns>Parsed device information or null if parsing fails</returns>
    public static Device? ParseResponse(byte[] buffer, int length)
    {
        try
        {
            // Minimum response size: 24 (header) + 2 (item count) + CPF items
            if (length < 26)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Buffer too small ({length} bytes, need at least 26)");
                return null;
            }

            // Parse encapsulation header
            var header = CIPEncapsulationHeader.FromBytes(buffer, 0);

            // Verify it's a List Identity response
            if (header.Command != (ushort)CIPEncapsulationCommand.ListIdentity)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Invalid command 0x{header.Command:X4}, expected 0x{(ushort)CIPEncapsulationCommand.ListIdentity:X4}");
                return null;
            }

            // Verify success status
            if (header.Status != (uint)CIPEncapsulationStatus.Success)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Non-success status 0x{header.Status:X8}");
                return null;
            }

            // Start parsing encapsulated data
            int offset = CIPEncapsulationHeader.HeaderSize;

            // Item Count (2 bytes, little-endian)
            if (offset + 2 > length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Buffer too small for item count at offset {offset}");
                return null;
            }

            ushort itemCount = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

            if (itemCount == 0)
            {
                ActivityLogger.GlobalLogger?.LogWarning("Parse failed: Item count is zero");
                return null;
            }

            // Parse first CPF item (should be Identity Response item)
            // Type Code (2 bytes) - should be 0x000C for Identity Response
            if (offset + 2 > length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Buffer too small for type code at offset {offset}");
                return null;
            }

            ushort typeCode = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

            // Length (2 bytes)
            if (offset + 2 > length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Buffer too small for item length at offset {offset}");
                return null;
            }

            ushort itemLength = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

            if (offset + itemLength > length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Item length {itemLength} extends beyond buffer (offset={offset}, buffer length={length})");
                return null;
            }

            ActivityLogger.GlobalLogger?.LogCIP($"Parsing identity item: offset={offset}, itemLength={itemLength}, typeCode=0x{typeCode:X4}");

            // Now parse the Identity Item structure
            return ParseIdentityItem(buffer, offset, itemLength);
        }
        catch (Exception ex)
        {
            // Parsing failed - malformed response
            ActivityLogger.GlobalLogger?.LogError($"Parse exception: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Parse Identity Item structure from List Identity response
    /// Reference: CIP Vol 2, Table 2-4.4
    /// </summary>
    private static Device? ParseIdentityItem(byte[] buffer, int offset, int length)
    {
        try
        {
            var device = new Device();
            int pos = offset;

            ActivityLogger.GlobalLogger?.LogCIP($"ParseIdentityItem: offset={offset}, length={length}, buffer total={buffer.Length}");

            // Protocol Version (2 bytes) - skip
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for protocol version at pos={pos}");
                return null;
            }
            pos += 2;

            // Socket Address Structure (16 bytes)
            if (pos + 16 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for socket address at pos={pos}");
                return null;
            }

            // sin_family (2 bytes) - should be 0x0002 for AF_INET
            ushort sinFamily = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

            // sin_port (2 bytes, big-endian) - TCP port
            ushort sinPort = (ushort)((buffer[pos++] << 8) | buffer[pos++]);

            // sin_addr (4 bytes, big-endian) - IPv4 address
            var ipBytes = new byte[4];
            Array.Copy(buffer, pos, ipBytes, 0, 4);
            device.IPAddress = new IPAddress(ipBytes);
            pos += 4;

            // sin_zero (8 bytes) - skip
            pos += 8;

            ActivityLogger.GlobalLogger?.LogCIP($"Parsed socket address: IP={device.IPAddress}, port={sinPort}, family=0x{sinFamily:X4}");

            // Vendor ID (2 bytes, little-endian) (REQ-3.3.1-005)
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for vendor ID at pos={pos}");
                return null;
            }

            device.VendorId = (ushort)(buffer[pos++] | (buffer[pos++] << 8));
            device.VendorName = VendorIdMapper.GetVendorName(device.VendorId);

            // Device Type (2 bytes, little-endian)
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for device type at pos={pos}");
                return null;
            }

            device.DeviceType = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

            // Product Code (2 bytes, little-endian)
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for product code at pos={pos}");
                return null;
            }

            device.ProductCode = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

            // Revision (2 bytes) - Major.Minor
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for revision at pos={pos}");
                return null;
            }

            byte revisionMinor = buffer[pos++];
            byte revisionMajor = buffer[pos++];
            device.FirmwareRevision = new Version(revisionMajor, revisionMinor);

            // Status (2 bytes) - skip
            if (pos + 2 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for status at pos={pos}");
                return null;
            }
            pos += 2;

            // Serial Number (4 bytes, little-endian)
            if (pos + 4 > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for serial number at pos={pos}");
                return null;
            }

            device.SerialNumber = (uint)(buffer[pos++] | (buffer[pos++] << 8) |
                                         (buffer[pos++] << 16) | (buffer[pos++] << 24));

            ActivityLogger.GlobalLogger?.LogCIP($"Parsed device IDs: Vendor={device.VendorId} ({device.VendorName}), Type={device.DeviceType}, Product={device.ProductCode}, Serial={device.SerialNumber}");

            // Product Name (1 byte length + string)
            if (pos >= offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for product name length at pos={pos}");
                return null;
            }

            byte productNameLength = buffer[pos++];
            ActivityLogger.GlobalLogger?.LogCIP($"Product name length: {productNameLength} bytes at pos={pos-1}");

            if (pos + productNameLength > offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Product name extends beyond buffer (pos={pos}, nameLen={productNameLength}, end={offset + length})");
                return null;
            }

            device.ProductName = Encoding.ASCII.GetString(buffer, pos, productNameLength);
            pos += productNameLength;

            ActivityLogger.GlobalLogger?.LogCIP($"Parsed product name: '{device.ProductName}' at pos={pos - productNameLength}");

            // State (1 byte) - skip for now
            if (pos >= offset + length)
            {
                ActivityLogger.GlobalLogger?.LogWarning($"Parse failed: Not enough bytes for state at pos={pos}");
                return null;
            }
            pos++;

            ActivityLogger.GlobalLogger?.LogCIP($"Successfully parsed device: {device.ProductName} at {device.IPAddress}");

            // Update device status based on IP
            device.UpdateStatus();
            device.ResetMissedScans();

            return device;
        }
        catch (Exception ex)
        {
            ActivityLogger.GlobalLogger?.LogError($"ParseIdentityItem exception at offset {offset}: {ex.Message}", ex);
            return null;
        }
    }
}
