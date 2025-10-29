namespace EtherNetIPTool.Core.CIP;

/// <summary>
/// CIP status codes and error translation (REQ-3.5.5-010)
/// Based on CIP Common Packet Format specification
/// </summary>
public static class CIPStatusCodes
{
    // Status codes from PRD Section 4.1.3
    public const byte Success = 0x00;
    public const byte PathDestinationUnknown = 0x04;
    public const byte PathSegmentError = 0x05;
    public const byte ServiceNotSupported = 0x08;
    public const byte AttributeNotSupported = 0x0F;
    public const byte NotEnoughData = 0x13;
    public const byte AttributeNotSettable = 0x14;
    public const byte PrivilegeViolation = 0x1C;
    public const byte InvalidParameter = 0x26;

    /// <summary>
    /// Translate CIP status code to human-readable message (REQ-3.5.5-010)
    /// </summary>
    /// <param name="statusCode">CIP status code byte</param>
    /// <returns>Human-readable error message</returns>
    public static string GetStatusMessage(byte statusCode)
    {
        return statusCode switch
        {
            Success => "Success",
            PathDestinationUnknown => "Path destination unknown - Device may not support this attribute",
            PathSegmentError => "Path segment error - Invalid CIP path format",
            ServiceNotSupported => "Service not supported - Device does not support Set_Attribute_Single",
            AttributeNotSupported => "Attribute not supported - This device does not have this configuration attribute",
            NotEnoughData => "Not enough data - Configuration value is incomplete",
            AttributeNotSettable => "Attribute not settable - This configuration value is read-only",
            PrivilegeViolation => "Privilege violation - Device requires authorization",
            InvalidParameter => "Invalid parameter - Configuration value format is incorrect",
            _ => $"Unknown error code: 0x{statusCode:X2}"
        };
    }

    /// <summary>
    /// Check if status code indicates success
    /// </summary>
    public static bool IsSuccess(byte statusCode)
    {
        return statusCode == Success;
    }

    /// <summary>
    /// Get attribute name for logging
    /// </summary>
    public static string GetAttributeName(byte attributeId)
    {
        return attributeId switch
        {
            3 => "Configuration Control",
            5 => "IP Address",
            6 => "Subnet Mask",
            7 => "Gateway",
            8 => "Hostname",
            10 => "DNS Server",
            _ => $"Attribute {attributeId}"
        };
    }
}
