using System.Net;
using System.Net.Sockets;
using EtherNetIPTool.Services;

namespace EtherNetIPTool.Core.BootP;

/// <summary>
/// BootP/DHCP server for commissioning factory-default devices
/// Listens on UDP port 68 for BOOTREQUEST packets and provides configuration interface
/// REQ-3.6.1, REQ-3.6.2, REQ-3.6.4
/// Requires Administrator privileges to bind to port 68 (privileged port on Windows)
/// </summary>
public class BootPServer : IDisposable
{
    private readonly ActivityLogger _logger;
    private UdpClient? _udpServer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// BootP client port (standard port 68)
    /// </summary>
    public const int BOOTP_CLIENT_PORT = 68;

    /// <summary>
    /// BootP server port (standard port 67)
    /// </summary>
    public const int BOOTP_SERVER_PORT = 67;

    /// <summary>
    /// Event raised when a BootP request is received
    /// Consumer should display configuration dialog and call SendReply when ready
    /// </summary>
    public event EventHandler<BootPRequestEventArgs>? RequestReceived;

    /// <summary>
    /// Gets whether the server is currently listening
    /// </summary>
    public bool IsListening { get; private set; }

    /// <summary>
    /// Local IP address the server is bound to
    /// </summary>
    public IPAddress? LocalIP { get; private set; }

    /// <summary>
    /// Create new BootP server instance
    /// </summary>
    /// <param name="logger">Activity logger for diagnostics</param>
    public BootPServer(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start BootP server listening on specified network interface
    /// REQ-3.6.1-001: Start UDP server listening on port 68
    /// REQ-3.6.1-002: Handle privilege errors gracefully
    /// </summary>
    /// <param name="localIP">Local IP address of selected NIC</param>
    /// <exception cref="UnauthorizedAccessException">If not running as Administrator</exception>
    /// <exception cref="SocketException">If port binding fails</exception>
    public void Start(IPAddress localIP)
    {
        if (IsListening)
        {
            _logger.LogWarning("BootP server already listening");
            return;
        }

        LocalIP = localIP ?? throw new ArgumentNullException(nameof(localIP));

        try
        {
            // REQ-3.6.1-001: Bind to UDP port 68 (client port)
            // Note: BootP server listens on port 68, not 67, for device commissioning scenarios
            var listenEndPoint = new IPEndPoint(localIP, BOOTP_CLIENT_PORT);

            _udpServer = new UdpClient();
            _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpServer.Client.Bind(listenEndPoint);

            _logger.LogBootP($"BootP server started on {localIP}:{BOOTP_CLIENT_PORT}");
            _logger.LogInfo("BootP server configuration: Listening for BOOTREQUEST packets");
            _logger.LogInfo("Status: Waiting for factory-default devices to send DHCP requests");

            // Start listening task
            _cancellationTokenSource = new CancellationTokenSource();
            _listenTask = ListenForRequestsAsync(_cancellationTokenSource.Token);
            IsListening = true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
        {
            // REQ-3.6.1-002: Handle privilege error
            _logger.LogError("BootP server failed to start: Access denied (Administrator privileges required)");
            throw new UnauthorizedAccessException(
                "Failed to bind to UDP port 68. BootP/DHCP mode requires Administrator privileges.", ex);
        }
        catch (SocketException ex)
        {
            _logger.LogError($"BootP server failed to start: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stop BootP server
    /// REQ-3.6.1-004: Stop server when switching back to EtherNet/IP mode
    /// </summary>
    public void Stop()
    {
        if (!IsListening)
            return;

        try
        {
            _logger.LogBootP("BootP server stopping...");

            // Cancel listening task
            _cancellationTokenSource?.Cancel();

            // Wait for listen task to complete (with timeout)
            if (_listenTask != null)
            {
                Task.WaitAny(_listenTask, Task.Delay(1000));
            }

            // Close UDP client
            _udpServer?.Close();
            _udpServer = null;

            IsListening = false;
            _logger.LogBootP("BootP server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error stopping BootP server: {ex.Message}");
        }
    }

    /// <summary>
    /// Background task to listen for BootP requests
    /// REQ-3.6.2-001: Listen for BOOTREQUEST packets
    /// REQ-3.6.2-002: Filter requests to selected NIC only
    /// </summary>
    private async Task ListenForRequestsAsync(CancellationToken cancellationToken)
    {
        _logger.LogBootP("BootP listener task started");

        try
        {
            while (!cancellationToken.IsCancellationRequested && _udpServer != null)
            {
                try
                {
                    // Wait for incoming packet
                    var result = await _udpServer.ReceiveAsync(cancellationToken);

                    _logger.LogBootP($"BootP packet received from {result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port} ({result.Buffer.Length} bytes)");

                    // Parse packet
                    var packet = BootPPacket.Parse(result.Buffer);

                    if (packet == null)
                    {
                        _logger.LogWarning("Failed to parse BootP packet - malformed data");
                        continue;
                    }

                    // REQ-3.6.2-001: Verify it's a BOOTREQUEST
                    if (packet.Op != BootPPacket.BootPOpCode.BOOTREQUEST)
                    {
                        _logger.LogWarning($"Ignoring BootP packet: Not a BOOTREQUEST (Op={packet.Op})");
                        continue;
                    }

                    _logger.LogBootP($"BOOTREQUEST received: XID=0x{packet.Xid:X8}, MAC={packet.GetClientMacAddressString()}, Flags=0x{packet.Flags:X4}");

                    // REQ-3.6.2-003: Immediately display configuration dialog
                    // Raise event for UI to handle (must be on UI thread)
                    var eventArgs = new BootPRequestEventArgs(packet, result.RemoteEndPoint);
                    OnRequestReceived(eventArgs);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping server
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing BootP request: {ex.Message}");
                    // Continue listening despite error
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"BootP listener task error: {ex.Message}");
        }
        finally
        {
            _logger.LogBootP("BootP listener task ended");
        }
    }

    /// <summary>
    /// Send BOOTREPLY packet to client
    /// REQ-3.6.4-001: Send BootP BOOTREPLY with assigned IP
    /// REQ-3.6.4-002: Include DHCP options (subnet mask, router)
    /// REQ-3.6.4-003: Broadcast or unicast based on FLAGS field
    /// </summary>
    /// <param name="request">Original BOOTREQUEST packet</param>
    /// <param name="assignedIP">IP address to assign to client</param>
    /// <param name="subnetMask">Subnet mask to assign</param>
    /// <param name="gateway">Optional gateway/router IP</param>
    /// <exception cref="InvalidOperationException">If server not listening</exception>
    public async Task SendReplyAsync(BootPPacket request, IPAddress assignedIP, IPAddress subnetMask, IPAddress? gateway = null)
    {
        if (!IsListening || _udpServer == null || LocalIP == null)
            throw new InvalidOperationException("BootP server not listening. Call Start() first.");

        try
        {
            // Build BOOTREPLY packet
            var replyData = BootPPacket.BuildReply(request, assignedIP, LocalIP, subnetMask, gateway);

            // Determine destination address based on FLAGS field
            // REQ-3.6.4-003: Broadcast (255.255.255.255) or unicast to assigned IP
            IPAddress destAddress;
            if (request.IsBroadcastFlagSet())
            {
                destAddress = IPAddress.Broadcast;
                _logger.LogBootP("Using broadcast destination (FLAGS broadcast bit set)");
            }
            else
            {
                destAddress = assignedIP;
                _logger.LogBootP($"Using unicast destination: {assignedIP}");
            }

            // REQ-3.6.4-004: Send from port 67 to port 68
            var destEndPoint = new IPEndPoint(destAddress, BOOTP_CLIENT_PORT);

            // Send reply
            int bytesSent = await _udpServer.SendAsync(replyData, destEndPoint);

            _logger.LogBootP($"BOOTREPLY sent: {bytesSent} bytes to {destAddress}:{BOOTP_CLIENT_PORT}");
            _logger.LogBootP($"Assigned IP: {assignedIP}, Subnet: {subnetMask}" +
                           (gateway != null ? $", Gateway: {gateway}" : ""));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to send BOOTREPLY: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Raise RequestReceived event
    /// </summary>
    protected virtual void OnRequestReceived(BootPRequestEventArgs e)
    {
        RequestReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();

        _cancellationTokenSource?.Dispose();
        _udpServer?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for BootP request received event
/// </summary>
public class BootPRequestEventArgs : EventArgs
{
    /// <summary>
    /// Parsed BootP request packet
    /// </summary>
    public BootPPacket Request { get; }

    /// <summary>
    /// Remote endpoint that sent the request
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Timestamp when request was received
    /// </summary>
    public DateTime ReceivedAt { get; }

    public BootPRequestEventArgs(BootPPacket request, IPEndPoint remoteEndPoint)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        ReceivedAt = DateTime.Now;
    }
}
