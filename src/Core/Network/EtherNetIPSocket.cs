using System.Net;
using System.Net.Sockets;

namespace EtherNetIPTool.Core.Network;

/// <summary>
/// UDP socket wrapper for EtherNet/IP broadcast communication
/// Implements single-socket architecture with ephemeral port per REQ-4.1.1-001
/// Handles sending List Identity broadcasts and receiving responses
/// REQ-3.3.1-001, REQ-3.3.1-003, REQ-4.3.3, REQ-4.3.4
/// </summary>
public class EtherNetIPSocket : IDisposable
{
    private UdpClient? _udpClient;
    private readonly IPAddress _localIP;
    private bool _disposed;

    /// <summary>
    /// Standard EtherNet/IP port for explicit messaging (UDP/TCP)
    /// </summary>
    public const int EtherNetIPPort = 44818;

    /// <summary>
    /// Default discovery timeout in milliseconds (REQ-3.3.1-003)
    /// </summary>
    public const int DefaultDiscoveryTimeout = 3000;

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
    /// Per REQ-4.1.1-001: Uses single socket with OS-assigned ephemeral port.
    /// This ensures compatibility with RSLinx and other EtherNet/IP tools
    /// that may already be using port 44818.
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
            // REQ-4.1.1-001: Use ephemeral port (port 0 = OS assigns free port)
            var ephemeralEndPoint = new IPEndPoint(_localIP, 0);
            _udpClient = new UdpClient(ephemeralEndPoint)
            {
                EnableBroadcast = true,
                Client =
                {
                    ReceiveTimeout = DefaultDiscoveryTimeout,
                    SendTimeout = 5000
                }
            };
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to create UDP socket on {_localIP}: {ex.Message}");
        }
    }

    /// <summary>
    /// Send List Identity to subnet broadcast address
    /// Per REQ-4.1.1-002: Sends ONLY to subnet broadcast, not global broadcast
    /// (REQ-3.3.1-001)
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <param name="subnetBroadcast">Calculated subnet broadcast address (e.g., 192.168.21.255)</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendSubnetBroadcast(byte[] packet, IPAddress subnetBroadcast)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        if (subnetBroadcast == null)
            throw new ArgumentNullException(nameof(subnetBroadcast));

        try
        {
            // REQ-4.1.1-002: Send to subnet broadcast address only
            var broadcastEndPoint = new IPEndPoint(subnetBroadcast, EtherNetIPPort);
            _udpClient.Send(packet, packet.Length, broadcastEndPoint);
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to send subnet broadcast to {subnetBroadcast}: {ex.Message}");
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
