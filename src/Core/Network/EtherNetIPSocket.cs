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
    private UdpClient? _mainClient;        // Primary socket (ephemeral port, always works)
    private UdpClient? _port44818Client;   // Secondary socket (port 44818, if available)
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
    /// Get the local port the main socket is bound to (0 if not open)
    /// </summary>
    public int LocalPort => (_mainClient?.Client?.LocalEndPoint as IPEndPoint)?.Port ?? 0;

    /// <summary>
    /// Check if secondary 44818 listener is active
    /// </summary>
    public bool Has44818Listener => _port44818Client != null;

    /// <summary>
    /// Create a new EtherNet/IP socket bound to specific network adapter
    /// </summary>
    /// <param name="localIP">Local IP address to bind to</param>
    public EtherNetIPSocket(IPAddress localIP)
    {
        _localIP = localIP ?? throw new ArgumentNullException(nameof(localIP));
    }

    /// <summary>
    /// Open UDP sockets for broadcast communication using dual-socket architecture
    ///
    /// Primary socket: Always binds to ephemeral port (guaranteed to work)
    /// - Used for sending broadcasts
    /// - Receives responses from Rockwell-style devices (reply to source port)
    ///
    /// Secondary socket: Attempts to bind to port 44818 for listening
    /// - Optional - only if port 44818 is available
    /// - Receives responses from Turck-style devices (reply to port 44818)
    ///
    /// This dual-socket approach ensures compatibility with both device types
    /// regardless of whether RSLinx or other tools are using port 44818.
    /// </summary>
    /// <exception cref="SocketException">If primary socket cannot be created</exception>
    public void Open()
    {
        if (_mainClient != null)
            return; // Already open

        try
        {
            // 1. ALWAYS create primary socket on ephemeral port (guaranteed to work)
            var ephemeralEndPoint = new IPEndPoint(_localIP, 0);
            _mainClient = new UdpClient(ephemeralEndPoint)
            {
                EnableBroadcast = true,
                Client =
                {
                    ReceiveTimeout = DefaultDiscoveryTimeout,
                    SendTimeout = 5000
                }
            };

            // 2. ALSO TRY to create secondary listener on port 44818
            // This is optional - gracefully degrades if 44818 is busy (e.g., RSLinx)
            try
            {
                var port44818EndPoint = new IPEndPoint(_localIP, EtherNetIPPort);
                _port44818Client = new UdpClient(port44818EndPoint)
                {
                    EnableBroadcast = true,
                    Client =
                    {
                        ReceiveTimeout = DefaultDiscoveryTimeout,
                        SendTimeout = 5000
                    }
                };
            }
            catch (SocketException)
            {
                // Port 44818 is in use (likely RSLinx or another tool)
                // This is not an error - we still have the ephemeral socket
                _port44818Client = null;
            }
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to create primary UDP socket on {_localIP}: {ex.Message}");
        }
    }

    /// <summary>
    /// Send List Identity broadcast to discover devices
    /// Sends from primary (ephemeral) socket
    /// (REQ-3.3.1-001)
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendBroadcast(byte[] packet)
    {
        if (_mainClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            // Broadcast to 255.255.255.255:44818 (REQ-3.3.1-001)
            // Send from primary socket (ephemeral port)
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, EtherNetIPPort);
            _mainClient.Send(packet, packet.Length, broadcastEndPoint);
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
    /// Sends from primary (ephemeral) socket
    /// </summary>
    /// <param name="packet">List Identity request packet</param>
    /// <param name="targetIP">Target device IP address</param>
    /// <exception cref="InvalidOperationException">If socket not open</exception>
    /// <exception cref="SocketException">If send fails</exception>
    public void SendUnicast(byte[] packet, IPAddress targetIP)
    {
        if (_mainClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        try
        {
            var targetEndPoint = new IPEndPoint(targetIP, EtherNetIPPort);
            _mainClient.Send(packet, packet.Length, targetEndPoint);
        }
        catch (SocketException ex)
        {
            throw new SocketException((int)ex.SocketErrorCode,
                $"Failed to send unicast to {targetIP}: {ex.Message}");
        }
    }

    /// <summary>
    /// Try to receive pending data from a specific socket without blocking
    /// </summary>
    /// <param name="client">UDP client to check</param>
    /// <param name="responses">List to add received responses to</param>
    private void TryReceiveFrom(UdpClient? client, List<(byte[] Data, IPEndPoint Source)> responses, string socketName)
    {
        if (client == null)
            return;

        try
        {
            // Check if data is available without blocking
            while (client.Available > 0)
            {
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = client.Receive(ref remoteEndPoint);

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
            // Continue with other socket
        }
    }

    /// <summary>
    /// Receive all responses within timeout period from BOTH sockets
    /// Merges responses from primary (ephemeral) and secondary (44818) sockets
    /// (REQ-3.3.1-003: wait 3 seconds for responses)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of received responses with source endpoints (duplicates removed)</returns>
    public async Task<List<(byte[] Data, IPEndPoint Source)>> ReceiveAllResponsesAsync(
        CancellationToken cancellationToken = default)
    {
        var responses = new List<(byte[] Data, IPEndPoint Source)>();

        if (_mainClient == null)
            throw new InvalidOperationException("Socket not open. Call Open() first.");

        // Calculate end time for timeout
        var endTime = DateTime.Now.AddMilliseconds(DefaultDiscoveryTimeout);

        // Poll both sockets for responses
        // Use 50ms polling interval for good responsiveness
        const int pollingIntervalMs = 50;

        while (DateTime.Now < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check both sockets for available data
                TryReceiveFrom(_mainClient, responses, "primary");
                TryReceiveFrom(_port44818Client, responses, "port44818");

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

        // Remove duplicate responses (same source IP/port and identical data)
        // This can happen if device responds to both sockets
        var uniqueResponses = RemoveDuplicateResponses(responses);

        return uniqueResponses;
    }

    /// <summary>
    /// Remove duplicate responses based on source endpoint and data content
    /// Keeps first occurrence of each unique response
    /// </summary>
    private List<(byte[] Data, IPEndPoint Source)> RemoveDuplicateResponses(
        List<(byte[] Data, IPEndPoint Source)> responses)
    {
        var seen = new HashSet<string>();
        var unique = new List<(byte[] Data, IPEndPoint Source)>();

        foreach (var (data, source) in responses)
        {
            // Create unique key from source and data content
            var key = $"{source.Address}:{source.Port}:{BitConverter.ToString(data)}";

            if (!seen.Contains(key))
            {
                seen.Add(key);
                unique.Add((data, source));
            }
        }

        return unique;
    }

    /// <summary>
    /// Close both UDP sockets
    /// </summary>
    public void Close()
    {
        _mainClient?.Close();
        _mainClient = null;

        _port44818Client?.Close();
        _port44818Client = null;
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
