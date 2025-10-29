using System.Net;
using System.Net.Sockets;

namespace EtherNetIPTool.Core.Network;

/// <summary>
/// UDP socket wrapper for EtherNet/IP broadcast communication
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
    /// Open the UDP socket for broadcast communication
    /// Attempts to bind to port 44818 first (some devices send responses there),
    /// falls back to ephemeral port if 44818 is unavailable
    /// </summary>
    /// <exception cref="SocketException">If socket cannot be created or bound</exception>
    public void Open()
    {
        if (_udpClient != null)
            return; // Already open

        // Try to bind to port 44818 first - some EtherNet/IP devices (Turck, etc.)
        // send responses specifically to port 44818 rather than the source port
        try
        {
            var localEndPoint = new IPEndPoint(_localIP, EtherNetIPPort);
            _udpClient = new UdpClient(localEndPoint)
            {
                EnableBroadcast = true,
                Client =
                {
                    ReceiveTimeout = DefaultDiscoveryTimeout,
                    SendTimeout = 5000
                }
            };

            // Successfully bound to port 44818
            return;
        }
        catch (SocketException)
        {
            // Port 44818 in use, try ephemeral port
            _udpClient = null;
        }

        // Fallback: Use ephemeral port (standard approach)
        try
        {
            var localEndPoint = new IPEndPoint(_localIP, 0); // Use ephemeral port
            _udpClient = new UdpClient(localEndPoint)
            {
                // Enable broadcast (REQ-4.3.3)
                EnableBroadcast = true,

                // Set receive timeout (REQ-4.3.4)
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
    /// Send List Identity broadcast to discover devices
    /// (REQ-3.3.1-001)
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendBroadcast(byte[] packet)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            // Broadcast to 255.255.255.255:44818 (REQ-3.3.1-001)
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, EtherNetIPPort);
            _udpClient.Send(packet, packet.Length, broadcastEndPoint);
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to send broadcast: {ex.Message}");
        }
    }

    /// <summary>
    /// Send List Identity as unicast to specific device (diagnostic/troubleshooting)
    /// Some devices may respond to unicast but not broadcast
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <param name="targetIP">Target device IP address</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendUnicast(byte[] packet, IPAddress targetIP)
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            var targetEndPoint = new IPEndPoint(targetIP, EtherNetIPPort);
            _udpClient.Send(packet, packet.Length, targetEndPoint);
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to send unicast to {targetIP}: {ex.Message}");
        }
    }

    /// <summary>
    /// Receive response from a device
    /// Blocks until data received or timeout occurs
    /// </summary>
    /// <param name="remoteEndPoint">Remote endpoint that sent the response</param>
    /// <returns>Received data or null if timeout</returns>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    public byte[]? ReceiveResponse(out IPEndPoint? remoteEndPoint)
    {
        remoteEndPoint = null;

        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            var data = _udpClient.Receive(ref remoteEndPoint);
            return data;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            // Timeout is expected when no more responses
            return null;
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to receive response: {ex.Message}");
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
        var responses = new List<(byte[], IPEndPoint)>();

        if (_udpClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        // Calculate end time for timeout
        var endTime = DateTime.Now.AddMilliseconds(DefaultDiscoveryTimeout);

        while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Calculate remaining timeout
                var remainingTimeout = (int)(endTime - DateTime.Now).TotalMilliseconds;
                if (remainingTimeout <= 0)
                    break;

                _udpClient.Client.ReceiveTimeout = remainingTimeout;

                // Non-blocking receive with timeout
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = await Task.Run(() => _udpClient.Receive(ref remoteEndPoint), cancellationToken);

                if (data != null && data.Length > 0)
                {
                    responses.Add((data, remoteEndPoint));
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                // Timeout - no more responses
                break;
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
