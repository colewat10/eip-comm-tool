using System.Net;
using System.Net.Sockets;
using EtherNetIPTool.Core.CIP;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// Service for writing device configuration via CIP Set_Attribute_Single (REQ-3.5.5)
/// Handles sequential attribute writes with progress tracking
/// </summary>
public class ConfigurationWriteService
{
    private readonly ActivityLogger _logger;
    private const int EtherNetIPPort = 44818;
    private const int MessageTimeout = 3000;  // REQ-3.5.5-004: 3-second timeout
    private const int InterMessageDelay = 100; // REQ-3.5.5-005: 100ms between writes

    /// <summary>
    /// Progress callback for updating UI (REQ-3.5.5-006)
    /// Parameters: (current step, total steps, operation name)
    /// </summary>
    public event Action<int, int, string>? ProgressUpdated;

    public ConfigurationWriteService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Write complete device configuration (REQ-3.5.5-002)
    /// Attributes written in sequence: IP → Subnet → Gateway → Hostname → DNS
    /// REQ-3.5.5-007: If any write fails, remaining writes are skipped
    /// </summary>
    public async Task<ConfigurationWriteResult> WriteConfigurationAsync(
        Device device,
        DeviceConfiguration config,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _logger.LogConfig($"Starting configuration write to device: {device.MacAddressString} ({device.IPAddressString})");

        var result = new ConfigurationWriteResult();
        int currentStep = 0;
        int totalSteps = CountRequiredWrites(config);

        try
        {
            // REQ-3.5.5-002: Write IP Address (Attribute 5) - REQUIRED
            if (config.IPAddress != null)
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "IP Address");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing IP Address: {config.IPAddress}");

                var writeResult = await WriteAttributeAsync(
                    device.IPAddress,
                    SetAttributeSingleMessage.BuildSetIPAddressRequest(config.IPAddress, device.IPAddress),
                    "IP Address",
                    cancellationToken);

                result.AddResult(writeResult);

                // REQ-3.5.5-007: Stop on failure
                if (!writeResult.Success)
                {
                    _logger.LogError($"IP Address write failed: {writeResult.ErrorMessage}");
                    return result;
                }

                // REQ-3.5.5-005: 100ms delay between writes
                await Task.Delay(InterMessageDelay, cancellationToken);
            }

            // REQ-3.5.5-002: Write Subnet Mask (Attribute 6) - REQUIRED
            if (config.SubnetMask != null)
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "Subnet Mask");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing Subnet Mask: {config.SubnetMask}");

                var writeResult = await WriteAttributeAsync(
                    device.IPAddress,
                    SetAttributeSingleMessage.BuildSetSubnetMaskRequest(config.SubnetMask, device.IPAddress),
                    "Subnet Mask",
                    cancellationToken);

                result.AddResult(writeResult);

                if (!writeResult.Success)
                {
                    _logger.LogError($"Subnet Mask write failed: {writeResult.ErrorMessage}");
                    return result;
                }

                await Task.Delay(InterMessageDelay, cancellationToken);
            }

            // REQ-3.5.5-002: Write Gateway (Attribute 7) - OPTIONAL
            if (config.Gateway != null)
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "Gateway");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing Gateway: {config.Gateway}");

                var writeResult = await WriteAttributeAsync(
                    device.IPAddress,
                    SetAttributeSingleMessage.BuildSetGatewayRequest(config.Gateway, device.IPAddress),
                    "Gateway",
                    cancellationToken);

                result.AddResult(writeResult);

                if (!writeResult.Success)
                {
                    _logger.LogError($"Gateway write failed: {writeResult.ErrorMessage}");
                    return result;
                }

                await Task.Delay(InterMessageDelay, cancellationToken);
            }

            // REQ-3.5.5-002: Write Hostname (Attribute 8) - OPTIONAL
            if (!string.IsNullOrWhiteSpace(config.Hostname))
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "Hostname");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing Hostname: {config.Hostname}");

                var writeResult = await WriteAttributeAsync(
                    device.IPAddress,
                    SetAttributeSingleMessage.BuildSetHostnameRequest(config.Hostname, device.IPAddress),
                    "Hostname",
                    cancellationToken);

                result.AddResult(writeResult);

                if (!writeResult.Success)
                {
                    _logger.LogError($"Hostname write failed: {writeResult.ErrorMessage}");
                    return result;
                }

                await Task.Delay(InterMessageDelay, cancellationToken);
            }

            // REQ-3.5.5-002: Write DNS Server (Attribute 10) - OPTIONAL
            if (config.DnsServer != null)
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "DNS Server");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing DNS Server: {config.DnsServer}");

                var writeResult = await WriteAttributeAsync(
                    device.IPAddress,
                    SetAttributeSingleMessage.BuildSetDNSServerRequest(config.DnsServer, device.IPAddress),
                    "DNS Server",
                    cancellationToken);

                result.AddResult(writeResult);

                if (!writeResult.Success)
                {
                    _logger.LogError($"DNS Server write failed: {writeResult.ErrorMessage}");
                    return result;
                }
            }

            _logger.LogConfig($"Configuration write completed successfully. {result.SuccessCount}/{result.TotalWrites} attributes written.");
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Configuration write cancelled by user");
            result.SetError("Operation cancelled by user");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error during configuration write: {ex.Message}", ex);
            result.SetError($"Unexpected error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Write a single attribute via TCP (REQ-3.5.5-004: 3-second timeout)
    /// </summary>
    private async Task<AttributeWriteResult> WriteAttributeAsync(
        IPAddress deviceIP,
        byte[] requestPacket,
        string attributeName,
        CancellationToken cancellationToken)
    {
        TcpClient? tcpClient = null;
        NetworkStream? stream = null;

        try
        {
            // Create TCP connection to device
            tcpClient = new TcpClient();

            // REQ-3.5.5-004: 3-second timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(MessageTimeout);

            await tcpClient.ConnectAsync(deviceIP, EtherNetIPPort, connectCts.Token);
            _logger.LogCIP($"TCP connection established to {deviceIP}:{EtherNetIPPort}");

            stream = tcpClient.GetStream();
            stream.ReadTimeout = MessageTimeout;
            stream.WriteTimeout = MessageTimeout;

            // Send request
            _logger.LogCIP($"Sending {attributeName} write request ({requestPacket.Length} bytes)");
            _logger.LogCIP($"Request hex (first 64 bytes): {BitConverter.ToString(requestPacket, 0, Math.Min(64, requestPacket.Length))}");

            await stream.WriteAsync(requestPacket, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // Receive response
            byte[] responseBuffer = new byte[1024];

            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readCts.CancelAfter(MessageTimeout);

            int bytesRead = await stream.ReadAsync(responseBuffer, readCts.Token);

            if (bytesRead == 0)
            {
                _logger.LogError($"{attributeName} write: No response from device");
                return new AttributeWriteResult
                {
                    AttributeName = attributeName,
                    Success = false,
                    ErrorMessage = "No response from device"
                };
            }

            byte[] response = new byte[bytesRead];
            Array.Copy(responseBuffer, response, bytesRead);

            _logger.LogCIP($"Received {attributeName} response ({bytesRead} bytes)");
            _logger.LogCIP($"Response hex (first 64 bytes): {BitConverter.ToString(response, 0, Math.Min(64, response.Length))}");

            // Parse CIP status from response
            byte statusCode = SetAttributeSingleMessage.ParseResponseStatus(response);
            string statusMessage = CIPStatusCodes.GetStatusMessage(statusCode);

            if (CIPStatusCodes.IsSuccess(statusCode))
            {
                _logger.LogConfig($"{attributeName} write successful");
                return new AttributeWriteResult
                {
                    AttributeName = attributeName,
                    Success = true
                };
            }
            else
            {
                // REQ-3.5.5-010: Translate error code to human-readable message
                _logger.LogError($"{attributeName} write failed: {statusMessage} (0x{statusCode:X2})");
                return new AttributeWriteResult
                {
                    AttributeName = attributeName,
                    Success = false,
                    StatusCode = statusCode,
                    ErrorMessage = statusMessage
                };
            }
        }
        catch (TimeoutException)
        {
            _logger.LogError($"{attributeName} write timeout after {MessageTimeout}ms");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Timeout after {MessageTimeout / 1000} seconds"
            };
        }
        catch (SocketException ex)
        {
            _logger.LogError($"{attributeName} write socket error: {ex.Message}", ex);
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"{attributeName} write error: {ex.Message}", ex);
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            stream?.Close();
            tcpClient?.Close();
        }
    }

    /// <summary>
    /// Count how many attributes will be written (for progress tracking)
    /// </summary>
    private int CountRequiredWrites(DeviceConfiguration config)
    {
        int count = 0;

        if (config.IPAddress != null) count++;
        if (config.SubnetMask != null) count++;
        if (config.Gateway != null) count++;
        if (!string.IsNullOrWhiteSpace(config.Hostname)) count++;
        if (config.DnsServer != null) count++;

        return count;
    }
}

/// <summary>
/// Result of writing a single attribute
/// </summary>
public class AttributeWriteResult
{
    public string AttributeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public byte? StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of writing complete device configuration
/// </summary>
public class ConfigurationWriteResult
{
    private readonly List<AttributeWriteResult> _results = new();

    public IReadOnlyList<AttributeWriteResult> Results => _results;

    public int TotalWrites => _results.Count;
    public int SuccessCount => _results.Count(r => r.Success);
    public int FailureCount => _results.Count(r => !r.Success);
    public bool Success => FailureCount == 0 && TotalWrites > 0;

    public string? GeneralError { get; private set; }

    public void AddResult(AttributeWriteResult result)
    {
        _results.Add(result);
    }

    public void SetError(string error)
    {
        GeneralError = error;
    }

    /// <summary>
    /// Get first error message (for display)
    /// </summary>
    public string? GetFirstErrorMessage()
    {
        if (!string.IsNullOrEmpty(GeneralError))
            return GeneralError;

        var firstError = _results.FirstOrDefault(r => !r.Success);
        return firstError?.ErrorMessage;
    }

    /// <summary>
    /// Get detailed summary of all writes
    /// </summary>
    public string GetDetailedSummary()
    {
        var summary = $"Configuration Write Result:\n";
        summary += $"Total Writes: {TotalWrites}\n";
        summary += $"Successful: {SuccessCount}\n";
        summary += $"Failed: {FailureCount}\n\n";

        if (!string.IsNullOrEmpty(GeneralError))
        {
            summary += $"Error: {GeneralError}\n";
        }

        if (_results.Any())
        {
            summary += "Details:\n";
            foreach (var result in _results)
            {
                string status = result.Success ? "✓" : "✗";
                summary += $"  {status} {result.AttributeName}";

                if (!result.Success && result.ErrorMessage != null)
                {
                    summary += $": {result.ErrorMessage}";
                }

                summary += "\n";
            }
        }

        return summary;
    }
}
