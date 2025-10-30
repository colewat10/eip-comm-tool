namespace EtherNetIPTool.Services;

/// <summary>
/// Maps CIP Vendor IDs to vendor names
/// Vendor IDs are assigned by ODVA (Open DeviceNet Vendors Association)
/// Reference: ODVA Vendor ID list (REQ-3.3.1-005, PRD Section 4.1.1)
/// </summary>
public static class VendorIdMapper
{
    /// <summary>
    /// Standard CIP vendor ID to name mapping
    /// Source: ODVA Vendor ID registry and PRD Appendix A
    /// </summary>
    private static readonly Dictionary<ushort, string> VendorMap = new()
    {
        // Major industrial automation vendors (from PRD Section 4.1.1)
        { 0x0001, "Rockwell Automation / Allen-Bradley" },
        { 0x0083, "Pepperl+Fuchs" },
        { 0x0103, "SICK AG" },
        { 0x017F, "Banner Engineering" },

        // Additional common vendors (from PRD Appendix A)
        { 0x0002, "Omron Corporation" },
        { 0x0010, "Phoenix Contact" },
        { 0x0030, "Turck" },
        { 0x01CF, "Schneider Electric" },
        { 0x0208, "Siemens" },
        { 0x028A, "Beckhoff Automation" },

        // Other industrial vendors
        { 0x0003, "Honeywell" },
        { 0x0005, "Keyence" },
        { 0x0006, "Parker Hannifin" },
        { 0x0007, "Yaskawa" },
        { 0x0008, "Mitsubishi Electric" },
        { 0x0009, "Schneider Automation" },
        { 0x000B, "Bosch Rexroth" },
        { 0x000C, "Eaton Corporation" },
        { 0x0011, "Wago" },
        { 0x0012, "SMC Corporation" },
        { 0x0013, "Festo" },
        { 0x0014, "Emerson Process Management" },
        { 0x0015, "ABB" },
        { 0x0016, "Yokogawa" },
        { 0x0017, "Endress+Hauser" },
        { 0x0018, "Krohne" },
        { 0x0019, "Vega" },
        { 0x001C, "Molex" },
        { 0x001F, "Telemecanique" },
        { 0x0020, "Square D" },
        { 0x0021, "Modicon" },
        { 0x0024, "TE Connectivity" },
        { 0x002B, "Hirschmann" },
        { 0x002D, "ifm electronic" },
        { 0x00EE, "Belden" },
        { 0x0034, "Balluff" },
        { 0x0036, "Panduit" },
        { 0x003D, "Murrelektronik" },
        { 0x0042, "Cognex" },
        { 0x0046, "Lenze" },
        { 0x004A, "Baumer" },
        { 0x0051, "Pilz" },
        { 0x0053, "SEW-Eurodrive" },
        { 0x0058, "Datalogic" },
        { 0x005A, "Harting" },
        { 0x0064, "National Instruments" },
        { 0x0067, "Hilscher" },
        { 0x006F, "Cisco Systems" },
        { 0x0073, "B&R Industrial Automation" },
        { 0x007C, "Red Lion Controls" },
        { 0x0084, "Wieland Electric" },
        { 0x008B, "HMS Industrial Networks" },
        { 0x0094, "Moxa" },
        { 0x009E, "Advantech" },
        { 0x00A5, "Kunbus" },
        { 0x00B3, "ASCO" },
        { 0x00C8, "Horner APG" },
        { 0x00D4, "Bihl+Wiedemann" },
        { 0x00E4, "Turck Banner" },
        { 0x0100, "ControlTechniques" },
        { 0x0111, "Weidmuller" },
        { 0x0125, "IDEC" },
        { 0x0157, "AutomationDirect" },
        { 0x0174, "VIPA" },
        { 0x0192, "Carlo Gavazzi" },
        { 0x01A6, "Schunk" },
        { 0x01B9, "Gefran" },
        { 0x01E0, "Unitronics" },
        { 0x01F4, "Sick Stegmann" },
        { 0x0212, "Watlow" },
        { 0x022D, "Opto 22" },
        { 0x0258, "Acromag" },
        { 0x029A, "HMS Networks" }
    };

    /// <summary>
    /// Get vendor name from vendor ID
    /// Returns formatted hex string if vendor ID not found in map
    /// </summary>
    /// <param name="vendorId">CIP Vendor ID (2 bytes)</param>
    /// <returns>Vendor name or "Unknown (0xXXXX)" if not found</returns>
    public static string GetVendorName(ushort vendorId)
    {
        if (VendorMap.TryGetValue(vendorId, out var vendorName))
        {
            return vendorName;
        }

        // Return hex representation for unknown vendors (REQ-3.3.1-005)
        return $"Unknown (0x{vendorId:X4})";
    }

    /// <summary>
    /// Check if vendor ID is known (exists in mapping table)
    /// </summary>
    /// <param name="vendorId">CIP Vendor ID</param>
    /// <returns>True if vendor is known, false otherwise</returns>
    public static bool IsKnownVendor(ushort vendorId)
    {
        return VendorMap.ContainsKey(vendorId);
    }

    /// <summary>
    /// Get all known vendor IDs
    /// </summary>
    /// <returns>Collection of all registered vendor IDs</returns>
    public static IEnumerable<ushort> GetAllVendorIds()
    {
        return VendorMap.Keys;
    }

    /// <summary>
    /// Get all known vendor names
    /// </summary>
    /// <returns>Collection of all registered vendor names</returns>
    public static IEnumerable<string> GetAllVendorNames()
    {
        return VendorMap.Values;
    }

    /// <summary>
    /// Get count of registered vendors
    /// </summary>
    /// <returns>Number of vendors in the mapping table</returns>
    public static int GetVendorCount()
    {
        return VendorMap.Count;
    }
}
