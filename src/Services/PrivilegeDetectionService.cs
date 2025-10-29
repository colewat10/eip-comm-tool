using System.Security.Principal;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for detecting application privilege level
/// Required for BootP/DHCP server functionality which needs Administrator rights
/// </summary>
public class PrivilegeDetectionService
{
    /// <summary>
    /// Determines if the current process is running with Administrator privileges
    /// </summary>
    /// <returns>True if running as Administrator, false otherwise</returns>
    public bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // If we can't determine privilege level, assume non-admin for safety
            return false;
        }
    }

    /// <summary>
    /// Gets a user-friendly description of the current privilege level
    /// </summary>
    /// <returns>Description string</returns>
    public string GetPrivilegeLevelDescription()
    {
        return IsRunningAsAdministrator()
            ? "Running with Administrator privileges (BootP/DHCP mode available)"
            : "Running with Standard user privileges (BootP/DHCP mode disabled)";
    }

    /// <summary>
    /// Gets instructions for elevating privileges
    /// </summary>
    /// <returns>Instructions string</returns>
    public string GetElevationInstructions()
    {
        return "To enable BootP/DHCP server mode, right-click the application and select 'Run as administrator'.";
    }
}
