using System.IO;
using System.Net;
using System.Net.Sockets;
using EtherNetIPTool.Core.CIP;
using EtherNetIPTool.Models;

namespace EtherNetIPTool.Services;

/// <summary>
/// ODVA-compliant service for writing device configuration via CIP Set_Attribute_Single (REQ-3.5.5)
/// Implements proper EtherNet/IP session management per ODVA Volume 2 specification
/// </summary>
public class ConfigurationWriteService
{
    private readonly ActivityLogger _logger;
    private const int EtherNetIPPort = 44818;
    private const int MessageTimeout = 3000;  // REQ-3.5.5-004: 3-second timeout
    private const int InterMessageDelay = 100; // REQ-3.5.5-005: 100ms between writes

    // ODVA EtherNet/IP Command Codes
    private const ushort CMD_RegisterSession = 0x0065;
    private const ushort CMD_UnregisterSession = 0x0066;
    private const ushort CMD_SendRRData = 0x006F;

    // CPF Item Type Codes
    private const ushort CPF_NullAddressItem = 0x0000;
    private const ushort CPF_UnconnectedDataItem = 0x00B2;

    // Static counter for Sender Context uniqueness
    private static long _contextCounter = 0;

    // Store last Sender Context for response validation (ODVA compliance)
    // Per ODVA Volume 2 Section 2-3.2: Sender Context must be echoed in responses
    private byte[] _lastSenderContext = new byte[8];

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
    ///
    /// ODVA Compliance: Uses single TCP connection with proper session management
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

        _logger.LogConfig($"Starting ODVA-compliant configuration write to device: {device.MacAddressString} ({device.IPAddressString})");

        var result = new ConfigurationWriteResult();
        int currentStep = 0;
        int totalSteps = CountRequiredWrites(config);

        TcpClient? tcpClient = null;
        NetworkStream? stream = null;
        uint sessionHandle = 0;

        try
        {
            // ODVA REQUIREMENT 1: Establish TCP connection
            tcpClient = new TcpClient();

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(MessageTimeout);

            _logger.LogCIP($"Connecting to {device.IPAddress}:{EtherNetIPPort} (timeout: {MessageTimeout}ms)");

            try
            {
                await tcpClient.ConnectAsync(device.IPAddress, EtherNetIPPort, connectCts.Token);
                _logger.LogCIP("TCP connection established successfully");
            }
            catch (OperationCanceledException) when (connectCts.IsCancellationRequested)
            {
                _logger.LogError($"TCP connection timeout after {MessageTimeout}ms");
                result.SetError($"Connection timeout: Device not responding after {MessageTimeout / 1000} seconds");
                return result;
            }
            catch (SocketException ex)
            {
                _logger.LogError($"TCP connection failed: {ex.Message} (SocketErrorCode: {ex.SocketErrorCode})");
                result.SetError($"Connection failed: {ex.Message}");
                return result;
            }

            stream = tcpClient.GetStream();
            stream.ReadTimeout = MessageTimeout;
            stream.WriteTimeout = MessageTimeout;

            // ODVA REQUIREMENT 2: RegisterSession (MANDATORY before any CIP messages)
            _logger.LogCIP("Sending RegisterSession (Command 0x0065)");

            try
            {
                sessionHandle = await RegisterSessionAsync(stream, cancellationToken);
                _logger.LogConfig($"Session registered successfully. Session Handle: 0x{sessionHandle:X8}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"RegisterSession failed: {ex.Message}");
                result.SetError($"Session registration failed: {ex.Message}");
                return result;
            }
            catch (IOException ex)
            {
                _logger.LogError($"RegisterSession I/O error: {ex.Message}");
                result.SetError($"Communication error during session registration: {ex.Message}");
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("RegisterSession cancelled");
                result.SetError("Session registration cancelled");
                return result;
            }

            // REQ-3.5.5-002: Write IP Address (Attribute 5) - REQUIRED
            if (config.IPAddress != null)
            {
                currentStep++;
                ProgressUpdated?.Invoke(currentStep, totalSteps, "IP Address");
                _logger.LogConfig($"[{currentStep}/{totalSteps}] Writing IP Address: {config.IPAddress}");

                var cipMessage = SetAttributeSingleMessage.BuildSetIPAddressRequest(config.IPAddress, device.IPAddress);
                var writeResult = await WriteAttributeAsync(
                    stream,
                    sessionHandle,
                    cipMessage,
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

                var cipMessage = SetAttributeSingleMessage.BuildSetSubnetMaskRequest(config.SubnetMask, device.IPAddress);
                var writeResult = await WriteAttributeAsync(
                    stream,
                    sessionHandle,
                    cipMessage,
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

                var cipMessage = SetAttributeSingleMessage.BuildSetGatewayRequest(config.Gateway, device.IPAddress);
                var writeResult = await WriteAttributeAsync(
                    stream,
                    sessionHandle,
                    cipMessage,
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

                var cipMessage = SetAttributeSingleMessage.BuildSetHostnameRequest(config.Hostname, device.IPAddress);
                var writeResult = await WriteAttributeAsync(
                    stream,
                    sessionHandle,
                    cipMessage,
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

                var cipMessage = SetAttributeSingleMessage.BuildSetDNSServerRequest(config.DnsServer, device.IPAddress);
                var writeResult = await WriteAttributeAsync(
                    stream,
                    sessionHandle,
                    cipMessage,
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
        finally
        {
            // ODVA REQUIREMENT 3: UnregisterSession before disconnect (if session was registered)
            if (stream != null && sessionHandle != 0)
            {
                try
                {
                    _logger.LogCIP("Sending UnregisterSession (Command 0x0066)");
                    await UnregisterSessionAsync(stream, sessionHandle, cancellationToken);
                    _logger.LogCIP("Session unregistered successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"UnregisterSession failed (non-critical): {ex.Message}");
                }
            }

            stream?.Close();
            tcpClient?.Close();
            _logger.LogCIP("TCP connection closed");
        }
    }

    /// <summary>
    /// Read a single CIP attribute via Get_Attribute_Single (Service 0x0E)
    /// Used for reading port statistics from Ethernet Link Object (Class 0xF6)
    /// and other diagnostic data from CIP objects
    /// </summary>
    /// <param name="device">Target device</param>
    /// <param name="classId">CIP Class ID (e.g., 0xF5 for TCP/IP Interface, 0xF6 for Ethernet Link)</param>
    /// <param name="instanceId">Instance ID (typically 1 for port 1)</param>
    /// <param name="attributeId">Attribute ID to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AttributeReadResult containing raw attribute data or error information</returns>
    public async Task<AttributeReadResult> ReadAttributeAsync(
        Device device,
        byte classId,
        byte instanceId,
        byte attributeId,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        _logger.LogCIP($"Reading attribute: Class 0x{classId:X2}, Instance {instanceId}, Attribute {attributeId} from {device.IPAddressString}");

        TcpClient? tcpClient = null;
        NetworkStream? stream = null;
        uint sessionHandle = 0;

        try
        {
            // 1. Establish TCP connection
            tcpClient = new TcpClient();

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(MessageTimeout);

            _logger.LogCIP($"Connecting to {device.IPAddress}:{EtherNetIPPort}");

            try
            {
                await tcpClient.ConnectAsync(device.IPAddress, EtherNetIPPort, connectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Connection timeout");
            }
            catch (SocketException ex)
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, $"Connection failed: {ex.Message}");
            }

            stream = tcpClient.GetStream();
            stream.ReadTimeout = MessageTimeout;
            stream.WriteTimeout = MessageTimeout;

            // 2. RegisterSession
            sessionHandle = await RegisterSessionAsync(stream, cancellationToken);
            _logger.LogCIP($"Session registered: Handle = 0x{sessionHandle:X8}");

            // 3. Build Get_Attribute_Single request
            byte[] cipMessage;
            if (classId == 0xF5)
            {
                cipMessage = GetAttributeSingleMessage.BuildGetTcpIpAttributeRequest(attributeId, device.IPAddress);
            }
            else if (classId == 0xF6)
            {
                cipMessage = GetAttributeSingleMessage.BuildGetEthernetLinkAttributeRequest(instanceId, attributeId, device.IPAddress);
            }
            else
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, $"Unsupported class ID: 0x{classId:X2}");
            }

            // 4. Wrap in SendRRData and send
            var sendRRDataPacket = BuildSendRRDataPacket(sessionHandle, cipMessage);
            _logger.LogCIP($"Sending Get_Attribute_Single request ({sendRRDataPacket.Length} bytes)");

            await stream.WriteAsync(sendRRDataPacket, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // 5. Read response
            var response = await ReadCompleteResponseAsync(stream, cancellationToken);
            _logger.LogCIP($"Received response ({response.Length} bytes)");

            // 6. Parse response
            var result = ParseAttributeReadResponse(response, sessionHandle, classId, instanceId, attributeId);

            // 7. UnregisterSession
            await UnregisterSessionAsync(stream, sessionHandle, cancellationToken);

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogError($"Attribute read timeout after {MessageTimeout}ms");
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Attribute read failed: {ex.Message}", ex);
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, ex.Message);
        }
        finally
        {
            // Cleanup (UnregisterSession already called if successful)
            if (stream != null && sessionHandle != 0)
            {
                try
                {
                    await UnregisterSessionAsync(stream, sessionHandle, cancellationToken);
                }
                catch { /* Already logged or non-critical */ }
            }
            stream?.Close();
            tcpClient?.Close();
        }
    }

    /// <summary>
    /// Read all attributes of a CIP object instance via Get_Attribute_All (Service 0x01)
    /// Used as a fallback when Get_Attribute_Single (0x0E) is not supported
    /// </summary>
    /// <param name="device">Target device</param>
    /// <param name="classId">CIP Class ID (e.g., 0xF6 for Ethernet Link)</param>
    /// <param name="instanceId">Instance ID (typically 1 for port 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AttributeReadResult containing all attributes data</returns>
    public async Task<AttributeReadResult> ReadAllAttributesAsync(
        Device device,
        byte classId,
        byte instanceId,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        _logger.LogCIP($"Reading ALL attributes: Class 0x{classId:X2}, Instance {instanceId} from {device.IPAddressString}");

        TcpClient? tcpClient = null;
        NetworkStream? stream = null;
        uint sessionHandle = 0;

        try
        {
            // 1. Establish TCP connection
            tcpClient = new TcpClient();

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(MessageTimeout);

            _logger.LogCIP($"Connecting to {device.IPAddress}:{EtherNetIPPort}");

            try
            {
                await tcpClient.ConnectAsync(device.IPAddress, EtherNetIPPort, connectCts.Token);
            }
            catch (OperationCanceledException)
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, 0xFF, 0xFF, "Connection timeout");
            }
            catch (SocketException ex)
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, 0xFF, 0xFF, $"Connection failed: {ex.Message}");
            }

            stream = tcpClient.GetStream();
            stream.ReadTimeout = MessageTimeout;
            stream.WriteTimeout = MessageTimeout;

            // 2. RegisterSession
            sessionHandle = await RegisterSessionAsync(stream, cancellationToken);
            _logger.LogCIP($"Session registered: Handle = 0x{sessionHandle:X8}");

            // 3. Build Get_Attribute_All request
            byte[] cipMessage;
            if (classId == 0xF6)
            {
                cipMessage = GetAttributeSingleMessage.BuildGetEthernetLinkAllAttributesRequest(instanceId, device.IPAddress);
            }
            else
            {
                return AttributeReadResult.CreateFailure(classId, instanceId, 0xFF, 0xFF, $"Get_Attribute_All not implemented for class 0x{classId:X2}");
            }

            // 4. Wrap in SendRRData and send
            var sendRRDataPacket = BuildSendRRDataPacket(sessionHandle, cipMessage);
            _logger.LogCIP($"Sending Get_Attribute_All request ({sendRRDataPacket.Length} bytes)");

            await stream.WriteAsync(sendRRDataPacket, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // 5. Read response
            var response = await ReadCompleteResponseAsync(stream, cancellationToken);
            _logger.LogCIP($"Received response ({response.Length} bytes)");

            // 6. Parse response (use same parser, reply code is 0x81 instead of 0x8E)
            var result = ParseAttributeReadResponse(response, sessionHandle, classId, instanceId, 0xFF); // 0xFF = All attributes

            // 7. UnregisterSession
            await UnregisterSessionAsync(stream, sessionHandle, cancellationToken);

            return result;
        }
        catch (TimeoutException)
        {
            _logger.LogError($"Get_Attribute_All timeout after {MessageTimeout}ms");
            return AttributeReadResult.CreateFailure(classId, instanceId, 0xFF, 0xFF, "Timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get_Attribute_All failed: {ex.Message}", ex);
            return AttributeReadResult.CreateFailure(classId, instanceId, 0xFF, 0xFF, ex.Message);
        }
        finally
        {
            // Cleanup
            if (stream != null && sessionHandle != 0)
            {
                try
                {
                    await UnregisterSessionAsync(stream, sessionHandle, cancellationToken);
                }
                catch { /* Already logged or non-critical */ }
            }
            stream?.Close();
            tcpClient?.Close();
        }
    }

    /// <summary>
    /// Parse Get_Attribute_Single response and extract attribute data
    /// Similar to ParseAttributeResponse but for read operations (Service 0x8E reply)
    /// </summary>
    private AttributeReadResult ParseAttributeReadResponse(
        byte[] response,
        uint expectedSessionHandle,
        byte classId,
        byte instanceId,
        byte attributeId)
    {
        if (response.Length < 24)
        {
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF,
                $"Response too short: {response.Length} bytes");
        }

        // Validate encapsulation header
        ushort responseCommand = BitConverter.ToUInt16(response, 0);
        uint responseSessionHandle = BitConverter.ToUInt32(response, 4);
        uint encapsulationStatus = BitConverter.ToUInt32(response, 8);

        if (responseCommand != CMD_SendRRData)
        {
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF,
                $"Invalid response command: 0x{responseCommand:X4}");
        }

        if (encapsulationStatus != 0)
        {
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, (byte)(encapsulationStatus & 0xFF),
                $"Encapsulation error: 0x{encapsulationStatus:X8}");
        }

        // Parse CPF structure
        int offset = 24; // Start after encapsulation header

        if (response.Length < offset + 10)
        {
            return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Response too short for CPF");
        }

        // Skip CPF header (Interface Handle + Timeout + Item Count)
        offset += 6;
        ushort itemCount = BitConverter.ToUInt16(response, offset);
        offset += 2;

        // Find Unconnected Data Item (Type 0x00B2)
        for (int i = 0; i < itemCount; i++)
        {
            if (response.Length < offset + 4)
                break;

            ushort itemType = BitConverter.ToUInt16(response, offset);
            ushort itemLength = BitConverter.ToUInt16(response, offset + 2);
            offset += 4;

            if (itemType == CPF_UnconnectedDataItem)
            {
                // Found Unconnected Data Item - parse CIP response inside
                if (response.Length < offset + itemLength)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Response truncated in CIP data");
                }

                // Parse Unconnected Send Reply
                if (itemLength < 4)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "CIP response too short");
                }

                byte serviceReply = response[offset];
                byte generalStatus = response[offset + 2];
                byte additionalStatusSize = response[offset + 3];

                if (serviceReply != 0xD2) // Unconnected Send Reply (0x80 + 0x52)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF,
                        $"Invalid service reply: 0x{serviceReply:X2}");
                }

                if (generalStatus != 0x00)
                {
                    string statusMessage = CIPStatusCodes.GetStatusMessage(generalStatus);
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, generalStatus,
                        $"Unconnected Send error: {statusMessage}");
                }

                // Skip to embedded Get_Attribute_Single Reply
                int embeddedOffset = offset + 4 + (additionalStatusSize * 2);

                if (embeddedOffset + 3 > offset + itemLength)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Response truncated in embedded message");
                }

                // Parse embedded Get_Attribute_Single Reply
                // Offset 0: Reply Service (0x8E = 0x80 + 0x0E)
                // Offset 1: Reserved
                // Offset 2: General Status
                // Offset 3: Additional Status Size
                // Offset 4+: Attribute Data (if status = 0x00)

                byte embeddedServiceReply = response[embeddedOffset];
                byte embeddedStatus = response[embeddedOffset + 2];
                byte embeddedAdditionalStatusSize = response[embeddedOffset + 3];

                // Valid service replies:
                // 0x81/0xC1 = Get_Attribute_All Reply (success/error)
                // 0x8E/0xCE = Get_Attribute_Single Reply (success/error)
                if (embeddedServiceReply != 0x81 && embeddedServiceReply != 0xC1 &&
                    embeddedServiceReply != 0x8E && embeddedServiceReply != 0xCE)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF,
                        $"Invalid embedded service reply: 0x{embeddedServiceReply:X2}");
                }

                if (embeddedStatus != 0x00)
                {
                    string statusMessage = CIPStatusCodes.GetStatusMessage(embeddedStatus);
                    string serviceName = (embeddedServiceReply == 0x81 || embeddedServiceReply == 0xC1) ? "Get_Attribute_All" : "Get_Attribute_Single";
                    _logger.LogWarning($"{serviceName} failed: {statusMessage} (Status: 0x{embeddedStatus:X2})");
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, embeddedStatus, statusMessage);
                }

                // Success - extract attribute data
                int dataOffset = embeddedOffset + 4 + (embeddedAdditionalStatusSize * 2);
                int dataLength = (offset + itemLength) - dataOffset;

                if (dataLength < 0)
                {
                    return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "Invalid data length");
                }

                byte[] attributeData = new byte[dataLength];
                Array.Copy(response, dataOffset, attributeData, 0, dataLength);

                _logger.LogCIP($"Attribute read successful: {dataLength} bytes of data");
                return AttributeReadResult.CreateSuccess(classId, instanceId, attributeId, attributeData);
            }

            // Skip this item's data
            offset += itemLength;
        }

        return AttributeReadResult.CreateFailure(classId, instanceId, attributeId, 0xFF, "No Unconnected Data Item found");
    }

    /// <summary>
    /// ODVA RegisterSession - Establish EtherNet/IP session (Command 0x0065)
    /// MANDATORY before any CIP communication over TCP
    /// </summary>
    /// <returns>Session Handle for use in subsequent messages</returns>
    private async Task<uint> RegisterSessionAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        // Build RegisterSession request (28 bytes total)
        var request = new byte[28];
        int offset = 0;

        // Bytes 0-1: Command = 0x0065 (RegisterSession)
        BitConverter.GetBytes(CMD_RegisterSession).CopyTo(request, offset);
        offset += 2;

        // Bytes 2-3: Length = 0x0004 (4 bytes of protocol version data)
        BitConverter.GetBytes((ushort)4).CopyTo(request, offset);
        offset += 2;

        // Bytes 4-7: Session Handle = 0x00000000 (not assigned yet)
        BitConverter.GetBytes((uint)0).CopyTo(request, offset);
        offset += 4;

        // Bytes 8-11: Status = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(request, offset);
        offset += 4;

        // Bytes 12-19: Sender Context (8 bytes, unique identifier)
        var senderContext = GetSenderContext();

        // Store for response validation (ODVA compliance)
        Array.Copy(senderContext, _lastSenderContext, 8);

        senderContext.CopyTo(request, offset);
        offset += 8;

        // Bytes 20-23: Options = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(request, offset);
        offset += 4;

        // Bytes 24-25: Protocol Version = 0x0001
        BitConverter.GetBytes((ushort)1).CopyTo(request, offset);
        offset += 2;

        // Bytes 26-27: Option Flags = 0x0000
        BitConverter.GetBytes((ushort)0).CopyTo(request, offset);

        _logger.LogCIP($"RegisterSession request: {BitConverter.ToString(request)}");

        // Send request
        _logger.LogCIP("Sending RegisterSession request to device...");
        await stream.WriteAsync(request, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        _logger.LogCIP("RegisterSession request sent, waiting for response...");

        // Read response (minimum 28 bytes)
        var response = await ReadCompleteResponseAsync(stream, cancellationToken);
        _logger.LogCIP($"Received RegisterSession response ({response.Length} bytes)");

        if (response.Length < 28)
        {
            throw new InvalidOperationException($"RegisterSession response too short: {response.Length} bytes");
        }

        _logger.LogCIP($"RegisterSession response: {BitConverter.ToString(response)}");

        // ODVA Compliance: Validate Sender Context
        if (!ValidateSenderContext(response, _lastSenderContext))
        {
            _logger.LogWarning("RegisterSession response has mismatched Sender Context");
            // Continue - some devices may not echo correctly
        }

        // Parse response
        ushort responseCommand = BitConverter.ToUInt16(response, 0);
        uint status = BitConverter.ToUInt32(response, 8);
        uint sessionHandle = BitConverter.ToUInt32(response, 4);

        // Validate response
        if (responseCommand != CMD_RegisterSession)
        {
            throw new InvalidOperationException($"Invalid RegisterSession response command: 0x{responseCommand:X4}");
        }

        if (status != 0)
        {
            throw new InvalidOperationException($"RegisterSession failed with status: 0x{status:X8}");
        }

        if (sessionHandle == 0)
        {
            throw new InvalidOperationException("RegisterSession returned invalid Session Handle (0x00000000)");
        }

        return sessionHandle;
    }

    /// <summary>
    /// ODVA UnregisterSession - Close EtherNet/IP session (Command 0x0066)
    /// Should be called before TCP disconnect
    /// </summary>
    private async Task UnregisterSessionAsync(NetworkStream stream, uint sessionHandle, CancellationToken cancellationToken)
    {
        // Build UnregisterSession request (24 bytes, no data payload)
        var request = new byte[24];
        int offset = 0;

        // Bytes 0-1: Command = 0x0066 (UnregisterSession)
        BitConverter.GetBytes(CMD_UnregisterSession).CopyTo(request, offset);
        offset += 2;

        // Bytes 2-3: Length = 0x0000 (no data)
        BitConverter.GetBytes((ushort)0).CopyTo(request, offset);
        offset += 2;

        // Bytes 4-7: Session Handle (from RegisterSession)
        BitConverter.GetBytes(sessionHandle).CopyTo(request, offset);
        offset += 4;

        // Bytes 8-11: Status = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(request, offset);
        offset += 4;

        // Bytes 12-19: Sender Context (8 bytes)
        var senderContext = GetSenderContext();

        // Store for completeness (though no response expected for UnregisterSession)
        Array.Copy(senderContext, _lastSenderContext, 8);

        senderContext.CopyTo(request, offset);
        offset += 8;

        // Bytes 20-23: Options = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(request, offset);

        _logger.LogCIP($"UnregisterSession request: {BitConverter.ToString(request)}");

        // Send request (no response expected per ODVA spec)
        await stream.WriteAsync(request, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Write a single attribute via ODVA-compliant SendRRData with CPF structure
    /// </summary>
    /// <param name="stream">Open NetworkStream with registered session</param>
    /// <param name="sessionHandle">Session Handle from RegisterSession</param>
    /// <param name="cipMessage">Raw CIP message (Set_Attribute_Single with Unconnected Send wrapper)</param>
    /// <param name="attributeName">Human-readable attribute name for logging</param>
    private async Task<AttributeWriteResult> WriteAttributeAsync(
        NetworkStream stream,
        uint sessionHandle,
        byte[] cipMessage,
        string attributeName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build complete ODVA-compliant SendRRData message with CPF structure
            var sendRRDataPacket = BuildSendRRDataPacket(sessionHandle, cipMessage);

            _logger.LogCIP($"Sending {attributeName} write request ({sendRRDataPacket.Length} bytes total)");
            _logger.LogCIP($"Request hex (first 128 bytes): {BitConverter.ToString(sendRRDataPacket, 0, Math.Min(128, sendRRDataPacket.Length))}");

            // Send request
            await stream.WriteAsync(sendRRDataPacket, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // Read complete response
            var response = await ReadCompleteResponseAsync(stream, cancellationToken);

            _logger.LogCIP($"Received {attributeName} response ({response.Length} bytes)");
            _logger.LogCIP($"Response hex (first 128 bytes): {BitConverter.ToString(response, 0, Math.Min(128, response.Length))}");

            // Parse response
            return ParseAttributeResponse(response, sessionHandle, attributeName);
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
    }

    /// <summary>
    /// Build ODVA-compliant SendRRData packet with CPF structure
    /// Wraps CIP message in proper encapsulation header + CPF items
    /// </summary>
    private byte[] BuildSendRRDataPacket(uint sessionHandle, byte[] cipMessage)
    {
        // Calculate sizes
        int cpfDataSize = 16 + cipMessage.Length; // CPF header (10) + 2 items (6) + CIP data
        int totalSize = 24 + cpfDataSize; // Encapsulation header (24) + CPF data

        var packet = new byte[totalSize];
        int offset = 0;

        // === ENCAPSULATION HEADER (24 bytes) ===

        // Bytes 0-1: Command = 0x006F (SendRRData)
        BitConverter.GetBytes(CMD_SendRRData).CopyTo(packet, offset);
        offset += 2;

        // Bytes 2-3: Length (size of CPF data)
        BitConverter.GetBytes((ushort)cpfDataSize).CopyTo(packet, offset);
        offset += 2;

        // Bytes 4-7: Session Handle
        BitConverter.GetBytes(sessionHandle).CopyTo(packet, offset);
        offset += 4;

        // Bytes 8-11: Status = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(packet, offset);
        offset += 4;

        // Bytes 12-19: Sender Context (unique per request)
        var senderContext = GetSenderContext();

        // Store for response validation (ODVA compliance)
        // Per ODVA Volume 2 Section 2-3.2: Response must echo this context
        Array.Copy(senderContext, _lastSenderContext, 8);

        senderContext.CopyTo(packet, offset);
        offset += 8;

        // Bytes 20-23: Options = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(packet, offset);
        offset += 4;

        // === CPF (COMMON PACKET FORMAT) ===

        // Bytes 0-3: Interface Handle = 0x00000000
        BitConverter.GetBytes((uint)0).CopyTo(packet, offset);
        offset += 4;

        // Bytes 4-5: Timeout = 0x0000
        BitConverter.GetBytes((ushort)0).CopyTo(packet, offset);
        offset += 2;

        // Bytes 6-7: Item Count = 0x0002 (2 items)
        BitConverter.GetBytes((ushort)2).CopyTo(packet, offset);
        offset += 2;

        // === CPF ITEM 1: NULL ADDRESS ITEM ===

        // Bytes 0-1: Type Code = 0x0000 (Null Address)
        BitConverter.GetBytes(CPF_NullAddressItem).CopyTo(packet, offset);
        offset += 2;

        // Bytes 2-3: Length = 0x0000
        BitConverter.GetBytes((ushort)0).CopyTo(packet, offset);
        offset += 2;

        // === CPF ITEM 2: UNCONNECTED DATA ITEM ===

        // Bytes 0-1: Type Code = 0x00B2 (Unconnected Data)
        BitConverter.GetBytes(CPF_UnconnectedDataItem).CopyTo(packet, offset);
        offset += 2;

        // Bytes 2-3: Length (size of CIP message)
        BitConverter.GetBytes((ushort)cipMessage.Length).CopyTo(packet, offset);
        offset += 2;

        // Bytes 4+: CIP Message (Set_Attribute_Single with Unconnected Send wrapper)
        cipMessage.CopyTo(packet, offset);

        return packet;
    }

    /// <summary>
    /// Parse ODVA-compliant SendRRData response with CPF structure
    /// Extracts CIP status code from embedded response
    /// </summary>
    private AttributeWriteResult ParseAttributeResponse(byte[] response, uint expectedSessionHandle, string attributeName)
    {
        if (response.Length < 24)
        {
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Response too short: {response.Length} bytes (minimum 24 required)"
            };
        }

        // === VALIDATE ENCAPSULATION HEADER ===

        ushort responseCommand = BitConverter.ToUInt16(response, 0);
        ushort payloadLength = BitConverter.ToUInt16(response, 2);
        uint responseSessionHandle = BitConverter.ToUInt32(response, 4);
        uint encapsulationStatus = BitConverter.ToUInt32(response, 8);

        // ODVA Compliance: Validate Sender Context matches request
        // Per ODVA Volume 2 Section 2-3.2: Response must echo Sender Context
        if (!ValidateSenderContext(response, _lastSenderContext))
        {
            _logger.LogWarning($"Sender Context validation failed for {attributeName} write");
            // Continue processing - mismatch is logged but not fatal
            // Most devices echo context correctly, but some may not
        }

        if (responseCommand != CMD_SendRRData)
        {
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Invalid response command: 0x{responseCommand:X4} (expected 0x006F)"
            };
        }

        if (responseSessionHandle != expectedSessionHandle)
        {
            _logger.LogWarning($"Session Handle mismatch: expected 0x{expectedSessionHandle:X8}, got 0x{responseSessionHandle:X8}");
        }

        if (encapsulationStatus != 0)
        {
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                StatusCode = (byte)(encapsulationStatus & 0xFF),
                ErrorMessage = $"Encapsulation error: 0x{encapsulationStatus:X8}"
            };
        }

        // === PARSE CPF STRUCTURE ===

        int offset = 24; // Start after encapsulation header

        if (response.Length < offset + 10)
        {
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = "Response too short for CPF header"
            };
        }

        // Skip Interface Handle (4 bytes) and Timeout (2 bytes)
        offset += 6;

        // Read Item Count
        ushort itemCount = BitConverter.ToUInt16(response, offset);
        offset += 2;

        // Find Unconnected Data Item (skip Null Address Item)
        for (int i = 0; i < itemCount; i++)
        {
            if (response.Length < offset + 4)
            {
                return new AttributeWriteResult
                {
                    AttributeName = attributeName,
                    Success = false,
                    ErrorMessage = "Response truncated in CPF items"
                };
            }

            ushort itemType = BitConverter.ToUInt16(response, offset);
            ushort itemLength = BitConverter.ToUInt16(response, offset + 2);
            offset += 4;

            if (itemType == CPF_UnconnectedDataItem)
            {
                // Found CIP response data
                if (response.Length < offset + itemLength)
                {
                    return new AttributeWriteResult
                    {
                        AttributeName = attributeName,
                        Success = false,
                        ErrorMessage = "Response truncated in CIP data"
                    };
                }

                // Extract CIP response (inside Unconnected Send reply)
                return ParseCIPResponse(response, offset, itemLength, attributeName);
            }

            // Skip this item's data
            offset += itemLength;
        }

        return new AttributeWriteResult
        {
            AttributeName = attributeName,
            Success = false,
            ErrorMessage = "No Unconnected Data Item found in response"
        };
    }

    /// <summary>
    /// Parse CIP response inside CPF Unconnected Data Item
    /// Uses deterministic offset calculations per ODVA CIP specification
    /// </summary>
    private AttributeWriteResult ParseCIPResponse(byte[] response, int offset, int length, string attributeName)
    {
        // Unconnected Send Reply structure:
        // Offset 0: Service Reply (0xD2 = 0x80 + 0x52)
        // Offset 1: Reserved (0x00)
        // Offset 2: General Status
        // Offset 3: Additional Status Size
        // Offset 4+: Additional Status (if size > 0)
        // Offset N: Embedded message (if General Status = 0x00)

        if (length < 4)
        {
            _logger.LogError($"CIP response too short: {length} bytes (minimum 4 required)");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = "Response too short for CIP structure"
            };
        }

        byte serviceReply = response[offset];
        _logger.LogCIP($"Service Reply: 0x{serviceReply:X2}");

        // Validate service reply code
        if (serviceReply != 0xD2) // Unconnected Send Reply (0x80 + 0x52)
        {
            _logger.LogError($"Invalid service reply: 0x{serviceReply:X2}, expected 0xD2");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Invalid service reply code: 0x{serviceReply:X2}"
            };
        }

        byte reserved = response[offset + 1];
        byte generalStatus = response[offset + 2];
        byte additionalStatusSize = response[offset + 3];

        _logger.LogCIP($"Unconnected Send General Status: 0x{generalStatus:X2}");
        _logger.LogCIP($"Additional Status Size: {additionalStatusSize}");

        // Check Unconnected Send status
        if (generalStatus != 0x00)
        {
            string statusMessage = CIPStatusCodes.GetStatusMessage(generalStatus);
            _logger.LogError($"Unconnected Send failed: {statusMessage}");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                StatusCode = generalStatus,
                ErrorMessage = $"Unconnected Send error: {statusMessage}"
            };
        }

        // Skip additional status bytes
        int embeddedOffset = offset + 4 + (additionalStatusSize * 2);

        if (embeddedOffset + 3 > offset + length)
        {
            _logger.LogError("Response truncated before embedded message");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = "Response truncated in embedded message"
            };
        }

        // Parse embedded Set_Attribute_Single Reply
        // Offset 0: Reply Service (0x90 = 0x80 + 0x10)
        // Offset 1: Reserved
        // Offset 2: General Status
        // Offset 3: Additional Status Size

        byte embeddedServiceReply = response[embeddedOffset];

        if (embeddedServiceReply != 0x90) // Set_Attribute_Single Reply (0x80 + 0x10)
        {
            _logger.LogError($"Invalid embedded service reply: 0x{embeddedServiceReply:X2}, expected 0x90");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                ErrorMessage = $"Invalid embedded reply code: 0x{embeddedServiceReply:X2}"
            };
        }

        byte embeddedStatus = response[embeddedOffset + 2];
        _logger.LogCIP($"Set_Attribute_Single General Status: 0x{embeddedStatus:X2}");

        string embeddedStatusMessage = CIPStatusCodes.GetStatusMessage(embeddedStatus);

        if (CIPStatusCodes.IsSuccess(embeddedStatus))
        {
            _logger.LogConfig($"{attributeName} write successful (CIP status: 0x00)");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = true
            };
        }
        else
        {
            _logger.LogError($"{attributeName} write failed: {embeddedStatusMessage} (CIP status: 0x{embeddedStatus:X2})");
            return new AttributeWriteResult
            {
                AttributeName = attributeName,
                Success = false,
                StatusCode = embeddedStatus,
                ErrorMessage = embeddedStatusMessage
            };
        }
    }

    /// <summary>
    /// Read complete ODVA encapsulated response with proper framing
    /// Reads exact number of bytes based on encapsulation header length field
    /// </summary>
    private async Task<byte[]> ReadCompleteResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        _logger.LogCIP("Reading 24-byte encapsulation header...");

        // Read 24-byte encapsulation header first
        var header = await ReadExactAsync(stream, 24, cancellationToken);
        _logger.LogCIP("Encapsulation header received");

        // Parse length field (bytes 2-3)
        ushort payloadLength = BitConverter.ToUInt16(header, 2);
        _logger.LogCIP($"Payload length from header: {payloadLength} bytes");

        if (payloadLength == 0)
        {
            // No payload, return just header
            _logger.LogCIP("No payload data, returning header only");
            return header;
        }

        // Read exact payload length
        _logger.LogCIP($"Reading {payloadLength} bytes of payload data...");
        var payload = await ReadExactAsync(stream, payloadLength, cancellationToken);
        _logger.LogCIP("Payload data received");

        // Combine header + payload
        var fullResponse = new byte[24 + payloadLength];
        header.CopyTo(fullResponse, 0);
        payload.CopyTo(fullResponse, 24);

        return fullResponse;
    }

    /// <summary>
    /// Read exact number of bytes from stream (handles partial reads)
    /// Critical for ODVA protocol compliance
    /// </summary>
    private async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        int readAttempts = 0;

        _logger.LogCIP($"ReadExactAsync: Need to read {count} bytes");

        while (offset < count)
        {
            readAttempts++;
            _logger.LogCIP($"ReadExactAsync: Attempt {readAttempts}, reading {count - offset} bytes (total: {offset}/{count})");

            int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);

            if (bytesRead == 0)
            {
                _logger.LogError($"Connection closed by device. Read {offset}/{count} bytes after {readAttempts} attempts.");
                throw new IOException($"Connection closed by device. Read {offset}/{count} bytes.");
            }

            offset += bytesRead;
            _logger.LogCIP($"ReadExactAsync: Read {bytesRead} bytes, total: {offset}/{count}");
        }

        _logger.LogCIP($"ReadExactAsync: Successfully read all {count} bytes in {readAttempts} attempts");
        return buffer;
    }

    /// <summary>
    /// Generate unique Sender Context for each request
    /// Uses thread-safe counter to ensure uniqueness
    /// </summary>
    private byte[] GetSenderContext()
    {
        long contextValue = Interlocked.Increment(ref _contextCounter);
        byte[] context = new byte[8];
        BitConverter.GetBytes(contextValue).CopyTo(context, 0);
        return context;
    }

    /// <summary>
    /// Validate that response Sender Context matches request
    /// Per ODVA Volume 2 Section 2-3.2: Target must echo Sender Context in response
    ///
    /// ODVA Specification Requirement:
    /// "The Sender Context is an 8-byte array that is echoed back in the response.
    ///  It is used by the originator to match responses to requests."
    ///
    /// This validation ensures the response corresponds to our request and
    /// helps detect issues like:
    /// - Response/request mismatch (e.g., slow network causing out-of-order responses)
    /// - Device firmware bugs (not echoing context correctly)
    /// - Man-in-the-middle attacks (context would differ)
    /// </summary>
    /// <param name="response">Response packet from device</param>
    /// <param name="expectedContext">Expected Sender Context (from request)</param>
    /// <returns>True if context matches, false otherwise</returns>
    private bool ValidateSenderContext(byte[] response, byte[] expectedContext)
    {
        // Sender Context is at bytes 12-19 in encapsulation header
        const int contextOffset = 12;
        const int contextLength = 8;

        // Validate response is long enough
        if (response == null || response.Length < contextOffset + contextLength)
        {
            _logger.LogWarning($"Response too short to validate Sender Context ({response?.Length ?? 0} bytes)");
            return false;
        }

        // Validate expected context
        if (expectedContext == null || expectedContext.Length != contextLength)
        {
            _logger.LogWarning($"Invalid expected Sender Context (length: {expectedContext?.Length ?? 0})");
            return false;
        }

        // Compare all 8 bytes
        bool matches = true;
        for (int i = 0; i < contextLength; i++)
        {
            if (response[contextOffset + i] != expectedContext[i])
            {
                matches = false;
                break;
            }
        }

        if (!matches)
        {
            // Log mismatch with hex dump for diagnostics
            string expectedHex = BitConverter.ToString(expectedContext);
            string receivedHex = BitConverter.ToString(response, contextOffset, contextLength);

            _logger.LogWarning($"Sender Context MISMATCH detected!");
            _logger.LogWarning($"  Expected: {expectedHex}");
            _logger.LogWarning($"  Received: {receivedHex}");
            _logger.LogWarning($"  This may indicate out-of-order responses or device firmware issue");

            return false;
        }

        // Context matches - expected behavior
        _logger.LogCIP($"Sender Context validated successfully: {BitConverter.ToString(expectedContext)}");
        return true;
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
