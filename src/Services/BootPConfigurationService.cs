using System.Net;
using System.Net.Sockets;
using EtherNetIPTool.Core.BootP;
using EtherNetIPTool.Core.CIP;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for executing BootP/DHCP device configuration workflow
/// REQ-3.6.4: BootP Reply and DHCP Disable
/// Coordinates sending BootP reply and optionally disabling DHCP mode via CIP
/// </summary>
public class BootPConfigurationService
{
    private readonly ActivityLogger _logger;
    private readonly BootPServer _bootpServer;

    /// <summary>
    /// Create new BootP configuration service
    /// </summary>
    /// <param name="logger">Activity logger</param>
    /// <param name="bootpServer">BootP server instance</param>
    public BootPConfigurationService(ActivityLogger logger, BootPServer bootpServer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bootpServer = bootpServer ?? throw new ArgumentNullException(nameof(bootpServer));
    }

    /// <summary>
    /// Execute complete BootP configuration workflow
    /// REQ-3.6.4-001: Send BootP BOOTREPLY
    /// REQ-3.6.4-005: Wait 2 seconds for device to configure
    /// REQ-3.6.4-006: Send DHCP disable command if requested
    /// </summary>
    /// <param name="request">Original BootP request</param>
    /// <param name="assignedIP">IP address to assign</param>
    /// <param name="subnetMask">Subnet mask to assign</param>
    /// <param name="gateway">Optional gateway</param>
    /// <param name="disableDhcp">Whether to disable DHCP mode after assignment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of configuration operation</returns>
    public async Task<BootPConfigurationServiceResult> ConfigureDeviceAsync(
        BootPPacket request,
        IPAddress assignedIP,
        IPAddress subnetMask,
        IPAddress? gateway,
        bool disableDhcp,
        CancellationToken cancellationToken = default)
    {
        var result = new BootPConfigurationServiceResult();

        try
        {
            _logger.LogBootP($"Starting BootP configuration for MAC {request.GetClientMacAddressString()}");
            _logger.LogBootP($"Assigned IP: {assignedIP}, Subnet: {subnetMask}" +
                           (gateway != null ? $", Gateway: {gateway}" : ""));

            // Step 1: Send BootP BOOTREPLY (REQ-3.6.4-001, REQ-3.6.4-002, REQ-3.6.4-003)
            await _bootpServer.SendReplyAsync(request, assignedIP, subnetMask, gateway);

            result.ReplyS

ent = true;
            _logger.LogBootP("BootP BOOTREPLY sent successfully");

            // Step 2: Wait for device to configure itself (REQ-3.6.4-005)
            _logger.LogBootP("Waiting 2 seconds for device to configure...");
            await Task.Delay(2000, cancellationToken);

            // Step 3: Disable DHCP mode if requested (REQ-3.6.4-006)
            if (disableDhcp)
            {
                _logger.LogBootP("Sending CIP command to disable DHCP mode (set static IP)...");

                try
                {
                    await DisableDhcpModeAsync(assignedIP, cancellationToken);
                    result.DhcpDisabled = true;
                    _logger.LogBootP("DHCP mode disabled successfully - device set to static IP");
                }
                catch (Exception ex)
                {
                    result.DhcpDisableError = ex.Message;
                    _logger.LogError($"Failed to disable DHCP mode: {ex.Message}");
                    // Don't throw - BootP reply was sent, this is just post-configuration
                }
            }
            else
            {
                _logger.LogBootP("DHCP disable skipped (user option not selected)");
            }

            result.Success = true;
            _logger.LogBootP("BootP configuration completed successfully");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"BootP configuration failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Disable DHCP mode by sending CIP Set_Attribute_Single to Attribute 3
    /// REQ-3.6.4-006: Set Configuration Control to 0x00000001 (static IP mode)
    /// REQ-3.6.4-007: 3-second timeout
    /// </summary>
    private async Task DisableDhcpModeAsync(IPAddress deviceIP, CancellationToken cancellationToken)
    {
        // Build CIP message for Configuration Control attribute
        byte[] request = SetAttributeSingleMessage.BuildSetConfigurationControlRequest(
            setToStaticIP: true,
            targetDeviceIP: deviceIP);

        _logger.LogCIP($"Sending Set_Attribute_Single for Configuration Control (Attr 3) to {deviceIP}");
        _logger.LogCIP($"Value: 0x00000001 (Static IP mode)");

        // Send via TCP to EtherNet/IP port 44818
        using var client = new TcpClient();

        // REQ-3.6.4-007: 3-second timeout
        using var timeoutCts = new CancellationTokenSource(3000);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Connect to device
            await client.ConnectAsync(deviceIP, 44818, linkedCts.Token);
            _logger.LogCIP($"Connected to {deviceIP}:44818");

            // Get stream
            var stream = client.GetStream();
            stream.ReadTimeout = 3000;
            stream.WriteTimeout = 3000;

            // Send request
            await stream.WriteAsync(request, linkedCts.Token);
            _logger.LogCIP($"Sent {request.Length} bytes");

            // Read response
            byte[] responseHeader = new byte[24]; // CIP encapsulation header
            int bytesRead = await stream.ReadAsync(responseHeader, linkedCts.Token);

            if (bytesRead < 24)
            {
                throw new InvalidOperationException($"Incomplete response: only {bytesRead} bytes received");
            }

            // Parse response status
            // Status is at bytes 8-11 (little-endian DWORD)
            uint status = (uint)(responseHeader[8] |
                                (responseHeader[9] << 8) |
                                (responseHeader[10] << 16) |
                                (responseHeader[11] << 24));

            if (status != 0)
            {
                string statusMessage = CIPStatusCodes.GetStatusDescription((byte)(status & 0xFF));
                throw new InvalidOperationException(
                    $"CIP error response: Status 0x{status:X8} - {statusMessage}");
            }

            _logger.LogCIP("DHCP disable command successful (Status 0x00000000 Success)");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("DHCP disable command timed out after 3 seconds");
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Network error: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Result of BootP configuration operation
/// </summary>
public class BootPConfigurationServiceResult
{
    /// <summary>
    /// Overall success of operation
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether BootP reply was sent successfully
    /// </summary>
    public bool ReplySent { get; set; }

    /// <summary>
    /// Whether DHCP mode was disabled successfully
    /// </summary>
    public bool DhcpDisabled { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error message specific to DHCP disable step
    /// </summary>
    public string? DhcpDisableError { get; set; }

    /// <summary>
    /// Get user-friendly status message
    /// </summary>
    public string GetStatusMessage()
    {
        if (Success)
        {
            if (DhcpDisabled)
                return "Configuration successful. Device configured with static IP.";
            else if (!string.IsNullOrEmpty(DhcpDisableError))
                return $"IP assigned successfully. Warning: Could not disable DHCP mode - {DhcpDisableError}";
            else
                return "IP assigned successfully. Device remains in DHCP mode.";
        }
        else
        {
            return $"Configuration failed: {ErrorMessage}";
        }
    }
}
