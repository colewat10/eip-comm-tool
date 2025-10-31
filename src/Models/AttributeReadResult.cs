namespace EtherNetIPTool.Models;

/// <summary>
/// Result of reading a single CIP attribute via Get_Attribute_Single
/// Used for retrieving port statistics and other diagnostic data
/// </summary>
public class AttributeReadResult
{
    /// <summary>CIP Class ID that was read</summary>
    public byte ClassId { get; set; }

    /// <summary>CIP Instance ID that was read</summary>
    public byte InstanceId { get; set; }

    /// <summary>CIP Attribute ID that was read</summary>
    public byte AttributeId { get; set; }

    /// <summary>Whether the attribute read succeeded</summary>
    public bool Success { get; set; }

    /// <summary>Raw attribute data bytes (null if failed)</summary>
    public byte[]? Data { get; set; }

    /// <summary>CIP status code (0x00 = success)</summary>
    public byte? StatusCode { get; set; }

    /// <summary>Human-readable error message (null if success)</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Create a successful read result
    /// </summary>
    public static AttributeReadResult CreateSuccess(byte classId, byte instanceId, byte attributeId, byte[] data)
    {
        return new AttributeReadResult
        {
            ClassId = classId,
            InstanceId = instanceId,
            AttributeId = attributeId,
            Success = true,
            Data = data,
            StatusCode = 0x00,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Create a failed read result
    /// </summary>
    public static AttributeReadResult CreateFailure(byte classId, byte instanceId, byte attributeId, byte statusCode, string errorMessage)
    {
        return new AttributeReadResult
        {
            ClassId = classId,
            InstanceId = instanceId,
            AttributeId = attributeId,
            Success = false,
            Data = null,
            StatusCode = statusCode,
            ErrorMessage = errorMessage
        };
    }
}
