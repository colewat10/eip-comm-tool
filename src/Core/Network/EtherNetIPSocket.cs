using System.Net;
using System.Net.Sockets;

namespace EtherNetIPTool.Core.Network;

/// <summary>
/// UDP socket wrapper for EtherNet/IP broadcast communication
/// Implements standard EtherNet/IP discovery using port 2222 source binding per REQ-4.1.1-001
/// Handles sending List Identity broadcasts and receiving responses
/// REQ-3.3.1-001, REQ-3.3.1-003, REQ-4.3.3, REQ-4.3.4
/// </summary>
public class EtherNetIPSocket : IDisposable
{
    private UdpClient? _udpClient;
    private readonly IPAddress _localIP;
    private bool _disposed;

    /// <summary>
    /// Standard EtherNet/IP source port for List Identity broadcasts (0x08AE)
    /// Following industrial Ethernet best practices for reliable device discovery
    /// </summary>
    public const int EtherNetIPSourcePort = 2222;

    /// <summary>
    /// Standard EtherNet/IP destination port for explicit messaging (0xAF12)
    /// </summary>
    public const int EtherNetIPPort = 44818;

    /// <summary>
    /// Default discovery timeout in milliseconds (REQ-3.3.1-003)
    /// </summary>
    public const int DefaultDiscoveryTimeout = 3000;

    /// <summary>
    /// Minimum receive buffer size for complete encapsulation packets
    /// </summary>
    public const int MinimumReceiveBufferSize = 4096;

    /// <summary>
    /// Get the local port the socket is bound to (0 if not open)
    /// </summary>
    public int LocalPort => (_udpClient?.Client?.LocalEndPoint as IPEndPoint)?.Port ?? 0;

    /// <summary>
    /// Create a new EtherNet/IP socket bound to specific network adapter
    /// </summary>
    /// <param name="localIP">Local IP address to bind to</param>
    public EtherNetIPSocket(IPAddress localIP)
    {
        _localIP = localIP ?? throw new ArgumentNullException(nameof(localIP));
    }

    /// <summary>
    /// Open UDP socket for broadcast communication
    ///
    /// Per REQ-4.1.1-001: Binds to standard EtherNet/IP source port 2222 (0x08AE).
    /// This follows industrial Ethernet best practices for reliable device discovery,
    /// matching the proven pycomm3 implementation approach.
    ///
    /// Socket options:
    /// - SO_BROADCAST: Enables broadcast packet transmission
    /// - SO_REUSEADDR: Allows multiple applications to bind to same port
    /// - ReceiveBuffer: Minimum 4096 bytes for complete encapsulation packets
    ///
    /// Socket is bound to specific network adapter to ensure broadcasts
    /// go out the correct interface per REQ-4.1.1-002.
    /// </summary>
    /// <exception cref="SocketException">If socket cannot be created</exception>
    public void Open()
    {
        if (_udpClient != null)
            return; // Already open

        try
        {
            // REQ-4.1.1-001: Bind to standard EtherNet/IP source port 2222
            var sourceEndPoint = new IPEndPoint(_localIP, EtherNetIPSourcePort);

            // Create socket with explicit options for industrial Ethernet compatibility
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Set SO_REUSEADDR to allow multiple applications to use port 2222
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Set SO_BROADCAST to enable broadcast packet transmission
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

            // Set receive buffer size to ensure complete encapsulation packets can be received
            socket.ReceiveBufferSize = Math.Max(socket.ReceiveBufferSize, MinimumReceiveBufferSize);

            // Bind to specific network adapter and port
            socket.Bind(sourceEndPoint);

            // Set timeouts
            socket.ReceiveTimeout = DefaultDiscoveryTimeout;
            socket.SendTimeout = 5000;

            // Wrap in UdpClient for convenience methods
            _udpClient = new UdpClient();
            _udpClient.Client = socket;

            // Verify socket was bound successfully
            var boundEndPoint = socket.LocalEndPoint as IPEndPoint;
            if (boundEndPoint == null || boundEndPoint.Port != EtherNetIPSourcePort)
            {
                throw new SocketException((int)SocketError.AddressNotAvailable,
                    $"Failed to bind socket to port {EtherNetIPSourcePort}");
            }
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to create UDP socket on {_localIP}:{EtherNetIPSourcePort}: {ex.Message}");
        }
    }

    /// <summary>
    /// Send List Identity broadcast to discover EtherNet/IP devices
    /// Per REQ-4.1.1-002: Sends to global broadcast (255.255.255.255) by default
    /// following industrial Ethernet best practices for maximum device compatibility
    /// (REQ-3.3.1-001)
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <param name="broadcastAddress">Broadcast address (defaults to 255.255.255.255 if null)</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendBroadcast(byte[] packet, IPAddress? broadcastAddress = null)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            // Default to global broadcast (255.255.255.255) for maximum compatibility
            var targetAddress = broadcastAddress ?? IPAddress.Broadcast;
            var broadcastEndPoint = new IPEndPoint(targetAddress, EtherNetIPPort);
            _udpClient.Send(packet, packet.Length, broadcastEndPoint);
        }
        catch (SocketException ex)
        {
            var target = broadcastAddress ?? IPAddress.Broadcast;
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to send broadcast to {target}: {ex.Message}");
        }
    }

    /// <summary>
    /// Try to receive pending data from socket without blocking
    /// </summary>
    /// <param name="responses">List to add received responses to</param>
    private void TryReceive(List<(byte[] Data, IPEndPoint Source)> responses)
    {
        if (_udpClient == null)
            return;

        try
        {
            // Check if data is available without blocking
            while (_udpClient.Available > 0)
            {
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = _udpClient.Receive(ref remoteEndPoint);

                if (data != null && data.Length > 0)
                {
                    responses.Add((data, remoteEndPoint));
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            // Expected when no data available
        }
        catch (SocketException)
        {
            // Socket may have been closed or other recoverable error
            // Continue processing
        }
    }

    /// <summary>
    /// Receive all responses within timeout period
    /// (REQ-3.3.1-003: wait 3 seconds for responses)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of received responses with source endpoints</returns>
    public async Task<List<(byte[] Data, IPEndPoint Source)>> ReceiveAllResponsesAsync(
        CancellationToken cancellationToken = default)
    {
        var responses = new List<(byte[] Data, IPEndPoint Source)>();

        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        // Calculate end time for timeout
        var endTime = DateTime.Now.AddMilliseconds(DefaultDiscoveryTimeout);

        // Poll socket for responses
        // Use 50ms polling interval for good responsiveness
        const int pollingIntervalMs = 50;

        while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check socket for available data
                TryReceive(responses);

                // Calculate remaining time
                var remainingTime = (int)(endTime - DateTime.Now).TotalMilliseconds;
                if (remainingTime <= 0)
                    break;

                // Wait briefly before next poll (or until timeout)
                var delayMs = Math.Min(pollingIntervalMs, remainingTime);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by user
                break;
            }
        }

        return responses;
    }

    /// <summary>
    /// Close the UDP socket
    /// </summary>
    public void Close()
    {
        _udpClient?.Close();
        _udpClient = null;
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
