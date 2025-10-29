namespace EtherNetIPTool.Core.CIP;

/// <summary>
/// CIP Encapsulation command codes
/// Reference: CIP Networks Library Vol 2: EtherNet/IP Adaptation, Chapter 2
/// </summary>
public enum CIPEncapsulationCommand : ushort
{
    /// <summary>No Operation (0x0000)</summary>
    NOP = 0x0000,

    /// <summary>List Services (0x0004) - Lists available services</summary>
    ListServices = 0x0004,

    /// <summary>List Identity (0x0063) - Device discovery broadcast</summary>
    ListIdentity = 0x0063,

    /// <summary>List Interfaces (0x0064)</summary>
    ListInterfaces = 0x0064,

    /// <summary>Register Session (0x0065)</summary>
    RegisterSession = 0x0065,

    /// <summary>Unregister Session (0x0066)</summary>
    UnregisterSession = 0x0066,

    /// <summary>Send RR Data (0x006F) - Explicit messaging</summary>
    SendRRData = 0x006F,

    /// <summary>Send Unit Data (0x0070) - Implicit messaging</summary>
    SendUnitData = 0x0070
}

/// <summary>
/// CIP Encapsulation status codes
/// </summary>
public enum CIPEncapsulationStatus : uint
{
    /// <summary>Success (0x0000)</summary>
    Success = 0x0000,

    /// <summary>Invalid or unsupported command (0x0001)</summary>
    InvalidCommand = 0x0001,

    /// <summary>Insufficient memory resources (0x0002)</summary>
    InsufficientMemory = 0x0002,

    /// <summary>Incorrect data in encapsulation (0x0003)</summary>
    IncorrectData = 0x0003,

    /// <summary>Invalid session handle (0x0064)</summary>
    InvalidSessionHandle = 0x0064,

    /// <summary>Invalid length (0x0065)</summary>
    InvalidLength = 0x0065,

    /// <summary>Unsupported protocol version (0x0069)</summary>
    UnsupportedProtocolVersion = 0x0069
}

/// <summary>
/// CIP Encapsulation Header (24 bytes)
/// All multi-byte fields are little-endian
/// Reference: CIP Networks Library Vol 2, Section 2-3
/// </summary>
public struct CIPEncapsulationHeader
{
    /// <summary>Encapsulation command (2 bytes, little-endian)</summary>
    public ushort Command;

    /// <summary>Length of encapsulated data in bytes (2 bytes, little-endian)</summary>
    public ushort Length;

    /// <summary>Session handle (4 bytes, little-endian)</summary>
    public uint SessionHandle;

    /// <summary>Status code (4 bytes, little-endian)</summary>
    public uint Status;

    /// <summary>Sender context (8 bytes) - used to match request/response</summary>
    public byte[] SenderContext;

    /// <summary>Options flags (4 bytes, little-endian)</summary>
    public uint Options;

    /// <summary>Total size of encapsulation header in bytes</summary>
    public const int HeaderSize = 24;

    /// <summary>
    /// Create a new encapsulation header
    /// </summary>
    public CIPEncapsulationHeader()
    {
        Command = 0;
        Length = 0;
        SessionHandle = 0;
        Status = 0;
        SenderContext = new byte[8];
        Options = 0;
    }

    /// <summary>
    /// Serialize header to byte array (little-endian)
    /// </summary>
    /// <returns>24-byte header</returns>
    public byte[] ToBytes()
    {
        var buffer = new byte[HeaderSize];
        var offset = 0;

        // Command (2 bytes, little-endian)
        buffer[offset++] = (byte)(Command & 0xFF);
        buffer[offset++] = (byte)((Command >> 8) & 0xFF);

        // Length (2 bytes, little-endian)
        buffer[offset++] = (byte)(Length & 0xFF);
        buffer[offset++] = (byte)((Length >> 8) & 0xFF);

        // Session Handle (4 bytes, little-endian)
        buffer[offset++] = (byte)(SessionHandle & 0xFF);
        buffer[offset++] = (byte)((SessionHandle >> 8) & 0xFF);
        buffer[offset++] = (byte)((SessionHandle >> 16) & 0xFF);
        buffer[offset++] = (byte)((SessionHandle >> 24) & 0xFF);

        // Status (4 bytes, little-endian)
        buffer[offset++] = (byte)(Status & 0xFF);
        buffer[offset++] = (byte)((Status >> 8) & 0xFF);
        buffer[offset++] = (byte)((Status >> 16) & 0xFF);
        buffer[offset++] = (byte)((Status >> 24) & 0xFF);

        // Sender Context (8 bytes)
        Array.Copy(SenderContext, 0, buffer, offset, 8);
        offset += 8;

        // Options (4 bytes, little-endian)
        buffer[offset++] = (byte)(Options & 0xFF);
        buffer[offset++] = (byte)((Options >> 8) & 0xFF);
        buffer[offset++] = (byte)((Options >> 16) & 0xFF);
        buffer[offset++] = (byte)((Options >> 24) & 0xFF);

        return buffer;
    }

    /// <summary>
    /// Parse header from byte array (little-endian)
    /// </summary>
    /// <param name="buffer">Buffer containing header data</param>
    /// <param name="offset">Offset to start parsing</param>
    /// <returns>Parsed header</returns>
    /// <exception cref="ArgumentException">If buffer is too small</exception>
    public static CIPEncapsulationHeader FromBytes(byte[] buffer, int offset = 0)
    {
        if (buffer.Length - offset < HeaderSize)
            throw new ArgumentException($"Buffer too small. Need {HeaderSize} bytes, got {buffer.Length - offset}");

        var header = new CIPEncapsulationHeader();
        var pos = offset;

        // Command (2 bytes, little-endian)
        header.Command = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

        // Length (2 bytes, little-endian)
        header.Length = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

        // Session Handle (4 bytes, little-endian)
        header.SessionHandle = (uint)(buffer[pos++] | (buffer[pos++] << 8) |
                                      (buffer[pos++] << 16) | (buffer[pos++] << 24));

        // Status (4 bytes, little-endian)
        header.Status = (uint)(buffer[pos++] | (buffer[pos++] << 8) |
                               (buffer[pos++] << 16) | (buffer[pos++] << 24));

        // Sender Context (8 bytes)
        header.SenderContext = new byte[8];
        Array.Copy(buffer, pos, header.SenderContext, 0, 8);
        pos += 8;

        // Options (4 bytes, little-endian)
        header.Options = (uint)(buffer[pos++] | (buffer[pos++] << 8) |
                                (buffer[pos++] << 16) | (buffer[pos++] << 24));

        return header;
    }
}
