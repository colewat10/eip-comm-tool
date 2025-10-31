# Protocol Reference
## EtherNet/IP Commissioning Tool

**Version:** 1.0
**Last Updated:** 2025-10-31

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [CIP - Common Industrial Protocol](#cip---common-industrial-protocol)
3. [EtherNet/IP Encapsulation](#ethernetip-encapsulation)
4. [BootP/DHCP Protocol](#bootpdhcp-protocol)
5. [Implementation Examples](#implementation-examples)
6. [Cross-References](#cross-references)

---

## Executive Summary

This document provides comprehensive technical reference for the three industrial networking protocols implemented in the EtherNet/IP Commissioning Tool:

### Protocol Overview

| Protocol | Purpose | Transport | Port | Use Case |
|----------|---------|-----------|------|----------|
| **CIP** | Common Industrial Protocol | TCP/UDP | 44818 | Device configuration and messaging |
| **EtherNet/IP** | CIP over Ethernet | TCP/UDP | 44818 | Encapsulation and session management |
| **BootP/DHCP** | Network configuration | UDP | 67/68 | Factory-default device commissioning |

### Key Capabilities

- **Device Discovery**: Broadcast List Identity (0x0063) to discover EtherNet/IP devices on local subnet
- **Configuration**: Write TCP/IP settings via CIP Set_Attribute_Single (0x10) service
- **Factory Reset**: Assign IP addresses to DHCP-mode devices via BootP/DHCP server
- **Validation**: ICMP ping for network connectivity verification

### Standards Compliance

- ODVA CIP Volume 1: Common Industrial Protocol Specification
- ODVA CIP Volume 2: EtherNet/IP Adaptation of CIP
- RFC 951: Bootstrap Protocol (BootP)
- RFC 2131: Dynamic Host Configuration Protocol (DHCP)
- RFC 2132: DHCP Options and BOOTP Vendor Extensions

---

## CIP - Common Industrial Protocol

### Overview

CIP (Common Industrial Protocol) is an application-layer industrial automation protocol that provides a unified framework for device communication, configuration, and control. This tool uses CIP over EtherNet/IP for discovery and configuration of industrial devices.

**Key Features:**
- Object-oriented architecture (Classes, Instances, Attributes)
- Service-based messaging (Get/Set attributes, Invoke methods)
- Standardized device profiles
- Vendor-neutral protocol

### CIP Services Used

#### 1. List Identity (Discovery)

**Service Code:** None (Encapsulation-level command)
**Command Code:** 0x0063
**Purpose:** Device discovery via UDP broadcast
**Transport:** UDP port 44818

**Use Case:** Broadcast to 255.255.255.255:44818 to discover all EtherNet/IP devices on subnet

#### 2. Set_Attribute_Single (Configuration)

**Service Code:** 0x10
**Reply Code:** 0x90
**Purpose:** Write individual object attributes
**Transport:** TCP or UDP with Unconnected Send

**Use Case:** Configure TCP/IP settings (IP address, subnet mask, gateway, hostname, DNS)

#### 3. Unconnected Send (Message Routing)

**Service Code:** 0x52
**Reply Code:** 0xD2
**Purpose:** Route explicit messages without established connection
**Transport:** Via Message Router (Class 0x06)

**Use Case:** Wrapper for Set_Attribute_Single to enable unconnected (UCMM) messaging

### Message Structure

#### Set_Attribute_Single Request

```
Offset | Size   | Field                 | Description
-------|--------|-----------------------|----------------------------------
0      | 1 byte | Service Code          | 0x10 (Set_Attribute_Single)
1      | 1 byte | Request Path Size     | Path length in 16-bit words
2+     | N      | Request Path (EPATH)  | Class, Instance, Attribute IDs
N+     | M      | Attribute Data        | New value for attribute
```

**Request Path Format (EPATH):**
```
Class Segment:    0x20 [Class ID]         (e.g., 0x20 0xF5 for TCP/IP Interface)
Instance Segment: 0x24 [Instance ID]      (e.g., 0x24 0x01 for Instance 1)
Attribute Segment: 0x30 [Attribute ID]    (e.g., 0x30 0x05 for IP Address)
```

#### Set_Attribute_Single Response

```
Offset | Size   | Field                  | Description
-------|--------|------------------------|----------------------------------
0      | 1 byte | Reply Service Code     | 0x90 (Set_Attribute_Single reply)
1      | 1 byte | Reserved               | 0x00
2      | 1 byte | General Status         | 0x00 = Success, other = Error
3      | 1 byte | Additional Status Size | Number of 16-bit status words
4+     | N      | Additional Status      | Extended error information
```

#### Unconnected Send Request

```
Offset | Size    | Field                | Description
-------|---------|----------------------|----------------------------------
0      | 1 byte  | Service Code         | 0x52 (Unconnected Send)
1      | 1 byte  | Request Path Size    | 2 words (4 bytes)
2      | 4 bytes | Request Path         | Message Router (0x20 0x06 0x24 0x01)
6      | 1 byte  | Priority/Tick Time   | 0x05 (typical)
7      | 1 byte  | Timeout Ticks        | 0xF9 (~2 seconds)
8      | 2 bytes | Message Length       | Length of embedded message
10     | N bytes | Embedded Message     | Set_Attribute_Single request
N+10   | M bytes | Route Path           | Routing information
```

### TCP/IP Interface Object (Class 0xF5)

The TCP/IP Interface Object provides network configuration for EtherNet/IP devices.

**Class Code:** 0xF5
**Instance:** 1 (typically single instance per device)

#### Attributes

| ID | Name | Data Type | Access | Description |
|----|------|-----------|--------|-------------|
| 1 | Status | DWORD | Get | Interface status flags |
| 2 | Configuration Capability | DWORD | Get | Supported configuration methods |
| 3 | Configuration Control | DWORD | Get/Set | Active configuration method |
| 4 | Physical Link Object | EPATH | Get | Path to physical layer object |
| 5 | Interface Configuration | Struct | Get/Set | IP, subnet, gateway as structure |
| 5 | IP Address | UDINT | Get/Set | IPv4 address (4 bytes, network order) |
| 6 | Subnet Mask | UDINT | Get/Set | Subnet mask (4 bytes, network order) |
| 7 | Gateway Address | UDINT | Get/Set | Default gateway (4 bytes, network order) |
| 8 | Primary Name Server | UDINT | Get/Set | DNS server (4 bytes, network order) |
| 9 | Secondary Name Server | UDINT | Get/Set | Secondary DNS (4 bytes, network order) |
| 10 | Domain Name | STRING | Get/Set | DNS domain name |
| 11 | Host Name | STRING | Get/Set | Device hostname (max 64 chars) |

**Note:** Attribute IDs 5-7 can be accessed individually or as a structure (Attribute 5). This tool accesses them individually.

#### Configuration Control Values (Attribute 3)

| Value | Hex | Description |
|-------|-----|-------------|
| 0 | 0x00000000 | DHCP/BootP - Device obtains IP via DHCP |
| 1 | 0x00000001 | Static IP - Device uses configured IP address |
| 2 | 0x00000002 | BOOTP - Device uses BootP only (no DHCP) |

**Tool Implementation:** Set to 0x00000001 (Static IP) after BootP configuration to disable DHCP mode.

### CIP Status Codes

| Code | Hex | Name | Description | Tool Interpretation |
|------|-----|------|-------------|---------------------|
| 0 | 0x00 | Success | Service succeeded | Configuration write successful |
| 4 | 0x04 | Path Destination Unknown | Object/attribute not found | Device may not support this attribute |
| 5 | 0x05 | Path Segment Error | Invalid EPATH format | Internal error - invalid CIP path |
| 8 | 0x08 | Service Not Supported | Service code unknown | Device does not support Set_Attribute_Single |
| 15 | 0x0F | Attribute Not Supported | Attribute doesn't exist | Device lacks this configuration parameter |
| 19 | 0x13 | Not Enough Data | Insufficient data in request | Configuration value incomplete |
| 20 | 0x14 | Attribute Not Settable | Read-only attribute | Configuration parameter is read-only |
| 28 | 0x1C | Privilege Violation | Authorization required | Device requires login/authentication |
| 38 | 0x26 | Invalid Parameter | Data format error | IP address or value format incorrect |

### Data Type Encoding

#### UDINT (Unsigned Double Integer) - 32-bit

**Size:** 4 bytes
**Byte Order:** Network byte order (big-endian) for IP addresses
**Example:** IP 192.168.1.100
```
Byte 0: 192 (0xC0)
Byte 1: 168 (0xA8)
Byte 2: 1   (0x01)
Byte 3: 100 (0x64)
```

#### STRING - Variable Length

**Format:** 2-byte length prefix + ASCII characters
**Byte Order:** Little-endian length
**Example:** Hostname "PLC-001"
```
Byte 0-1: 7, 0      (length = 7, little-endian)
Byte 2-8: P L C - 0 0 1
```

#### DWORD - 32-bit Word

**Size:** 4 bytes
**Byte Order:** Little-endian
**Example:** Configuration Control = 1 (Static IP)
```
Byte 0: 0x01
Byte 1: 0x00
Byte 2: 0x00
Byte 3: 0x00
```

---

## EtherNet/IP Encapsulation

### Overview

EtherNet/IP (Ethernet Industrial Protocol) adapts CIP for transmission over standard Ethernet networks using TCP/IP. It provides encapsulation, session management, and addressing for CIP messages.

**Transport Layers:**
- **TCP Port 44818:** Explicit messaging (connected, session-based)
- **UDP Port 44818:** Implicit messaging (broadcast, unconnected)
- **UDP Port 2222:** I/O messaging (real-time data)

### Encapsulation Header (24 bytes)

All EtherNet/IP messages begin with this header:

```
Offset | Size    | Field          | Byte Order    | Description
-------|---------|----------------|---------------|--------------------------------
0-1    | 2 bytes | Command        | Little-endian | Encapsulation command code
2-3    | 2 bytes | Length         | Little-endian | Length of encapsulated data
4-7    | 4 bytes | Session Handle | Little-endian | Session identifier (0 for UDP)
8-11   | 4 bytes | Status         | Little-endian | Encapsulation status code
12-19  | 8 bytes | Sender Context | -             | Request/response matching
20-23  | 4 bytes | Options        | Little-endian | Protocol options (typically 0)
```

**Total Header Size:** 24 bytes

### Command Codes

| Command | Code | Hex | Transport | Purpose |
|---------|------|-----|-----------|---------|
| NOP | 0 | 0x0000 | TCP/UDP | No operation (keepalive) |
| ListServices | 4 | 0x0004 | TCP/UDP | Query available services |
| ListIdentity | 99 | 0x0063 | UDP | Device discovery broadcast |
| ListInterfaces | 100 | 0x0064 | TCP/UDP | List communication interfaces |
| RegisterSession | 101 | 0x0065 | TCP | Establish session connection |
| UnregisterSession | 102 | 0x0066 | TCP | Close session connection |
| SendRRData | 111 | 0x006F | TCP | Send request/reply data |
| SendUnitData | 112 | 0x0070 | TCP | Send connected data |

#### Command Details

**List Identity (0x0063):**
- **Purpose:** Discover EtherNet/IP devices
- **Transport:** UDP broadcast to 255.255.255.255:44818
- **Session Handle:** 0x00000000
- **Request Length:** 0 (no encapsulated data)
- **Response:** Contains device identity (IP, vendor, model, serial)

**Register Session (0x0065):**
- **Purpose:** Establish TCP session for explicit messaging
- **Transport:** TCP to device IP:44818
- **Session Handle:** 0x00000000 in request
- **Response:** Assigns unique session handle for subsequent messages
- **Timeout:** Session expires after inactivity period

**Send RR Data (0x006F):**
- **Purpose:** Explicit request/reply messaging
- **Transport:** TCP with established session
- **Session Handle:** From RegisterSession response
- **Contains:** Common Packet Format (CPF) items with CIP data

**Unregister Session (0x0066):**
- **Purpose:** Cleanly close TCP session
- **Transport:** TCP with established session
- **Session Handle:** From RegisterSession
- **Response:** Confirmation of session closure

### Encapsulation Status Codes

| Code | Hex | Name | Description |
|------|-----|------|-------------|
| 0 | 0x0000 | Success | Command succeeded |
| 1 | 0x0001 | Invalid Command | Unsupported command code |
| 2 | 0x0002 | Insufficient Memory | Device out of memory |
| 3 | 0x0003 | Incorrect Data | Malformed encapsulation |
| 100 | 0x0064 | Invalid Session Handle | Session not registered |
| 101 | 0x0065 | Invalid Length | Length field mismatch |
| 105 | 0x0069 | Unsupported Protocol | Protocol version not supported |

### Sender Context

**Size:** 8 bytes
**Purpose:** Match requests with responses
**Format:** Arbitrary data (typically random or sequential)

**Usage:**
1. Client generates unique 8-byte context
2. Client sends request with context in header
3. Device echoes same context in response
4. Client matches response to original request

**Example:**
```
Request:  Sender Context = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]
Response: Sender Context = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]
```

### Common Packet Format (CPF)

CPF provides additional addressing and encapsulation within SendRRData and SendUnitData commands.

#### CPF Header

```
Offset | Size    | Field          | Description
-------|---------|----------------|--------------------------------
0-3    | 4 bytes | Interface Handle | 0x00000000 for CIP
4-5    | 2 bytes | Timeout        | 0x0000 (not used for unconnected)
6-7    | 2 bytes | Item Count     | Number of CPF items (typically 2)
```

#### CPF Item Structure

```
Offset | Size    | Field       | Description
-------|---------|-------------|--------------------------------
0-1    | 2 bytes | Type Code   | Item type identifier
2-3    | 2 bytes | Length      | Length of item data
4+     | N bytes | Data        | Item-specific data
```

#### CPF Item Types

| Type Code | Hex | Name | Purpose |
|-----------|-----|------|---------|
| 0 | 0x0000 | Null Address Item | No addressing (unconnected messaging) |
| 12 | 0x000C | Identity Response Item | List Identity response data |
| 161 | 0x00A1 | Connected Address Item | Connection identifier |
| 178 | 0x00B2 | Unconnected Data Item | Unconnected message data |
| 177 | 0x00B1 | Connected Data Item | Connected message data |

#### Typical CPF Structure for Set_Attribute_Single

```
[Encapsulation Header - 24 bytes]
  Command: 0x006F (SendRRData)
  Length: X (length of CPF data)
  Session Handle: [from RegisterSession]

[CPF Header - 8 bytes]
  Interface Handle: 0x00000000
  Timeout: 0x0000
  Item Count: 0x0002

[CPF Item 1 - Null Address - 4 bytes]
  Type Code: 0x0000
  Length: 0x0000

[CPF Item 2 - Unconnected Data - 4+N bytes]
  Type Code: 0x00B2
  Length: N
  Data: [Unconnected Send CIP message]
```

### Session Management

#### Establishing a Session (TCP)

**Step 1: TCP Connect**
```
Connect to device_ip:44818
```

**Step 2: Register Session**
```
Send RegisterSession (0x0065):
  Command: 0x0065
  Length: 0x0004
  Session Handle: 0x00000000
  Status: 0x00000000
  Sender Context: [random 8 bytes]
  Options: 0x00000000
  Data: 0x0001 0x0000 (Protocol Version 1)
```

**Step 3: Receive Session Handle**
```
Response:
  Command: 0x0065
  Session Handle: 0x12345678 (assigned by device)
  Status: 0x00000000 (success)
```

**Step 4: Use Session for Messaging**
```
All subsequent SendRRData messages use Session Handle: 0x12345678
```

**Step 5: Unregister Session**
```
Send UnregisterSession (0x0066):
  Session Handle: 0x12345678
```

**Step 6: Close TCP Connection**

#### Unconnected Messaging (UDP)

For discovery and simple queries, no session required:

```
Send List Identity (0x0063) via UDP:
  Broadcast to 255.255.255.255:44818
  Session Handle: 0x00000000
  No encapsulated data (Length: 0)
```

### List Identity Response Structure

```
[Encapsulation Header - 24 bytes]
  Command: 0x0063
  Status: 0x00000000

[CPF Header]
  Item Count: 1

[Identity Response Item]
  Type Code: 0x000C
  Length: N

  [Identity Item Data]
    Protocol Version: 2 bytes
    Socket Address: 16 bytes
      sin_family: 0x0002 (AF_INET)
      sin_port: 2 bytes (big-endian, typically 0xAF12 = 44818)
      sin_addr: 4 bytes (IP address)
      sin_zero: 8 bytes (padding)
    Vendor ID: 2 bytes (little-endian)
    Device Type: 2 bytes (little-endian)
    Product Code: 2 bytes (little-endian)
    Revision: 2 bytes (Major.Minor)
    Status: 2 bytes
    Serial Number: 4 bytes (little-endian)
    Product Name Length: 1 byte
    Product Name: N bytes (ASCII)
    State: 1 byte
```

### Vendor ID Mapping

| Vendor ID | Hex | Vendor Name |
|-----------|-----|-------------|
| 1 | 0x0001 | Rockwell Automation / Allen-Bradley |
| 2 | 0x0002 | Omron Corporation |
| 16 | 0x0010 | Phoenix Contact |
| 37 | 0x0025 | Turck |
| 131 | 0x0083 | Pepperl+Fuchs |
| 259 | 0x0103 | SICK AG |
| 383 | 0x017F | Banner Engineering |
| 463 | 0x01CF | Schneider Electric |
| 520 | 0x0208 | Siemens AG |
| 650 | 0x028A | Beckhoff Automation |

---

## BootP/DHCP Protocol

### Overview

Bootstrap Protocol (BootP) and Dynamic Host Configuration Protocol (DHCP) enable automatic network configuration for devices in factory-default state. Industrial devices often use BootP/DHCP for initial commissioning before switching to static IP.

**Key Standards:**
- RFC 951: Bootstrap Protocol
- RFC 2131: Dynamic Host Configuration Protocol
- RFC 2132: DHCP Options and BOOTP Vendor Extensions

**Port Requirements:**
- **UDP Port 67:** Server (DHCP/BootP server sends from this port)
- **UDP Port 68:** Client (devices send requests to this port)

**Privilege Requirement:** Administrator rights required to bind to port 68 on Windows

### BootP Packet Structure

Minimum packet size: 300 bytes (236 bytes fixed + 64 bytes minimum for options)

```
Offset  | Size     | Field   | Description
--------|----------|---------|----------------------------------------
0       | 1 byte   | OP      | Operation: 1=BOOTREQUEST, 2=BOOTREPLY
1       | 1 byte   | HTYPE   | Hardware type: 1=Ethernet (10Mbps)
2       | 1 byte   | HLEN    | Hardware address length: 6 for MAC
3       | 1 byte   | HOPS    | Hops: 0 for direct, incremented by relays
4-7     | 4 bytes  | XID     | Transaction ID (random, must match reply)
8-9     | 2 bytes  | SECS    | Seconds elapsed since client started
10-11   | 2 bytes  | FLAGS   | Bit 15=Broadcast flag (1=broadcast reply)
12-15   | 4 bytes  | CIADDR  | Client IP address (0.0.0.0 if unknown)
16-19   | 4 bytes  | YIADDR  | Your (client) IP address (set by server)
20-23   | 4 bytes  | SIADDR  | Server IP address
24-27   | 4 bytes  | GIADDR  | Gateway/relay IP address
28-43   | 16 bytes | CHADDR  | Client hardware (MAC) address + padding
44-107  | 64 bytes | SNAME   | Optional server host name (null-terminated)
108-235 | 128 bytes| FILE    | Boot file name (null-terminated)
236+    | Variable | OPTIONS | DHCP options (RFC 2132)
```

**Note:** All multi-byte fields use network byte order (big-endian)

### Field Descriptions

#### OP (Operation Code)
- **1 (BOOTREQUEST):** Client to server request
- **2 (BOOTREPLY):** Server to client response

#### HTYPE (Hardware Type)
- **1:** Ethernet (10Mbps)
- **6:** IEEE 802 Networks
- **Other values:** See ARP hardware types (RFC 1700)

#### XID (Transaction ID)
- Random 32-bit value generated by client
- Server must echo same XID in reply
- Used to match requests with responses

#### FLAGS Field
```
Bit 15 (0x8000): Broadcast Flag
  0 = Unicast reply to YIADDR
  1 = Broadcast reply to 255.255.255.255
Bits 0-14: Reserved (must be 0)
```

#### CHADDR (Client Hardware Address)
- First 6 bytes: Ethernet MAC address
- Remaining 10 bytes: Padding (zeros)
- Example: [00:1A:2B:3C:4D:5E, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]

### DHCP Options

#### Magic Cookie

All DHCP options must begin with this 4-byte signature:
```
0x63 0x82 0x53 0x63
```

This identifies the options field as DHCP (not vendor-specific BootP).

#### Option Format

Each option uses Type-Length-Value encoding:
```
Offset | Size   | Field
-------|--------|------------------
0      | 1 byte | Option Code
1      | 1 byte | Option Length (N)
2      | N bytes| Option Data
```

**Exception:** Option 255 (End) has no length or data fields.

#### Common DHCP Options

| Code | Name | Length | Description | Format |
|------|------|--------|-------------|--------|
| 1 | Subnet Mask | 4 | Subnet mask for client | IPv4 address |
| 3 | Router | 4+ | Default gateway address(es) | List of IPv4 addresses |
| 6 | Domain Name Server | 4+ | DNS server address(es) | List of IPv4 addresses |
| 12 | Host Name | Variable | Client hostname | ASCII string |
| 15 | Domain Name | Variable | DNS domain suffix | ASCII string |
| 51 | IP Address Lease Time | 4 | Lease duration in seconds | 32-bit integer |
| 53 | DHCP Message Type | 1 | Message type identifier | See table below |
| 54 | Server Identifier | 4 | DHCP server IP address | IPv4 address |
| 255 | End | 0 | End of options list | None |

#### DHCP Message Types (Option 53)

| Value | Type | Direction | Purpose |
|-------|------|-----------|---------|
| 1 | DHCPDISCOVER | Client → Server | Client requests configuration |
| 2 | DHCPOFFER | Server → Client | Server offers configuration |
| 3 | DHCPREQUEST | Client → Server | Client accepts offered config |
| 4 | DHCPDECLINE | Client → Server | Client rejects offered config |
| 5 | DHCPACK | Server → Client | Server confirms configuration |
| 6 | DHCPNAK | Server → Client | Server denies configuration |
| 7 | DHCPRELEASE | Client → Server | Client releases IP address |
| 8 | DHCPINFORM | Client → Server | Client requests local config only |

### Tool Implementation

#### Server Port Binding

The tool binds to **UDP port 68** (client port) to intercept BootP requests before they reach a real DHCP server. This requires Administrator privileges on Windows.

```csharp
// Bind to UDP port 68 (requires admin)
var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.Bind(new IPEndPoint(IPAddress.Any, 68));
```

#### Request Processing Workflow

1. **Receive BOOTREQUEST** on UDP port 68
2. **Parse request** to extract:
   - Transaction ID (XID)
   - Client MAC address (CHADDR)
   - Broadcast flag (FLAGS bit 15)
3. **Display to user** with device MAC and timestamp
4. **User enters** IP address, subnet mask, gateway
5. **Build BOOTREPLY** with assigned configuration
6. **Send reply** to client
7. **Wait 2 seconds** for device to apply settings
8. **Send CIP command** to disable DHCP mode (optional)

#### Reply Construction

```
OP: 2 (BOOTREPLY)
HTYPE: 1 (Ethernet)
HLEN: 6
HOPS: 0
XID: [copy from request]
SECS: 0
FLAGS: [copy from request]
CIADDR: 0.0.0.0
YIADDR: [assigned IP address]
SIADDR: [tool's IP address]
GIADDR: [gateway if provided, else 0.0.0.0]
CHADDR: [copy from request]
SNAME: "EtherNetIPTool" (optional)
FILE: [empty]
OPTIONS:
  Magic Cookie: 0x63 0x82 0x53 0x63
  Option 1 (Subnet Mask): [4 bytes]
  Option 3 (Router): [4 bytes if gateway provided]
  Option 255 (End)
```

#### Reply Transmission

Destination is determined by FLAGS field:

**Broadcast (FLAGS & 0x8000 = 1):**
- Destination IP: 255.255.255.255
- Destination Port: 68
- Destination MAC: FF:FF:FF:FF:FF:FF or client MAC

**Unicast (FLAGS & 0x8000 = 0):**
- Destination IP: YIADDR (assigned IP)
- Destination Port: 68
- Destination MAC: Client CHADDR

Source:
- Source IP: Tool's selected NIC IP
- Source Port: 67

### BootP/DHCP Configuration Workflow

```
1. Device Powers On (Factory Default)
   └─> Sends BOOTREQUEST every few seconds

2. Tool Receives BOOTREQUEST
   └─> Displays in UI: MAC, timestamp

3. User Configures Device
   ├─> Enters IP: 192.168.1.100
   ├─> Enters Subnet: 255.255.255.0
   └─> Enters Gateway: 192.168.1.1 (optional)

4. Tool Sends BOOTREPLY
   └─> YIADDR = 192.168.1.100
   └─> Options: Subnet Mask, Router

5. Device Applies Configuration
   └─> Waits 2 seconds for device restart/reconfigure

6. Tool Disables DHCP Mode (Optional)
   ├─> Connects to 192.168.1.100:44818 (TCP)
   ├─> Registers session
   ├─> Sends Set_Attribute_Single:
   │   └─> Class 0xF5, Instance 1, Attribute 3
   │   └─> Value: 0x00000001 (Static IP)
   ├─> Unregisters session
   └─> Closes connection

7. Device Switches to Static IP
   └─> No longer sends DHCP requests
```

### Troubleshooting

#### No Requests Received

- Verify application running as Administrator
- Check Windows Firewall allows UDP port 68
- Confirm device is in factory-default state (DHCP enabled)
- Verify device and tool on same network segment (no routers)
- Check device is powered and operational
- Try power cycling device to force new DHCP request

#### Device Doesn't Apply Configuration

- Check FLAGS field - may require broadcast reply
- Verify subnet mask is correct for assigned IP
- Some devices require specific DHCP options (check manual)
- Device may need power cycle to apply settings
- Check for IP conflicts on network

#### DHCP Disable Fails

- Device may not support CIP Configuration Control attribute
- Check device documentation for alternative configuration method
- May need to configure static IP via device web interface
- Try manual telnet/web configuration

---

## Implementation Examples

### Example 1: List Identity Request (Device Discovery)

**C# Code:**
```csharp
public static byte[] BuildRequest(byte[]? senderContext = null)
{
    var header = new CIPEncapsulationHeader
    {
        Command = (ushort)CIPEncapsulationCommand.ListIdentity,  // 0x0063
        Length = 0,  // No encapsulated data for List Identity
        SessionHandle = 0x00000000,
        Status = 0x00000000,
        SenderContext = senderContext ?? GenerateSenderContext(),
        Options = 0x00000000
    };

    return header.ToBytes();
}
```

**Packet Bytes (Hex):**
```
63 00 00 00 00 00 00 00   // Command (0x0063), Length (0x0000), Session (0x00000000)
00 00 00 00 01 02 03 04   // Status (0x00000000), Sender Context (8 bytes)
05 06 07 08 00 00 00 00   // Sender Context cont., Options (0x00000000)
```

**Total Size:** 24 bytes

### Example 2: Set IP Address via CIP

**C# Code:**
```csharp
public static byte[] BuildSetIPAddressRequest(IPAddress ipAddress, IPAddress targetDeviceIP)
{
    byte[] ipBytes = ipAddress.GetAddressBytes();  // Network byte order
    return BuildSetAttributeRequest(targetDeviceIP, 5, ipBytes);
}

private static byte[] BuildEmbeddedSetAttributeMessage(byte attributeId, byte[] attributeData)
{
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);

    // Service: Set_Attribute_Single (0x10)
    writer.Write((byte)0x10);

    // Request Path Size: 3 words = 6 bytes
    writer.Write((byte)3);

    // Path: Class 0xF5, Instance 1, Attribute ID
    writer.Write((byte)0x20);  // 8-bit class
    writer.Write((byte)0xF5);  // TCP/IP Interface Object
    writer.Write((byte)0x24);  // 8-bit instance
    writer.Write((byte)0x01);  // Instance 1
    writer.Write((byte)0x30);  // 8-bit attribute
    writer.Write(attributeId); // Attribute 5 (IP Address)

    // Attribute Data (4 bytes for IP address)
    writer.Write(attributeData);

    return ms.ToArray();
}
```

**Embedded Message Bytes (Set IP to 192.168.1.100):**
```
10                        // Service: Set_Attribute_Single (0x10)
03                        // Path size: 3 words
20 F5                     // Class: TCP/IP Interface (0xF5)
24 01                     // Instance: 1
30 05                     // Attribute: IP Address (5)
C0 A8 01 64               // Data: 192.168.1.100 (network byte order)
```

**Total Embedded Message Size:** 11 bytes

### Example 3: Parse List Identity Response

**C# Code:**
```csharp
public static Device? ParseResponse(byte[] buffer, int length)
{
    // Parse encapsulation header (24 bytes)
    var header = CIPEncapsulationHeader.FromBytes(buffer, 0);

    // Verify command and status
    if (header.Command != 0x0063 || header.Status != 0x0000)
        return null;

    int offset = 24;  // Skip header

    // Item Count (2 bytes, little-endian)
    ushort itemCount = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

    // Type Code (2 bytes) - should be 0x000C for Identity Response
    ushort typeCode = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

    // Length (2 bytes)
    ushort itemLength = (ushort)(buffer[offset++] | (buffer[offset++] << 8));

    // Parse Identity Item structure
    return ParseIdentityItem(buffer, offset, itemLength);
}

private static Device? ParseIdentityItem(byte[] buffer, int offset, int length)
{
    var device = new Device();
    int pos = offset;

    // Skip Protocol Version (2 bytes)
    pos += 2;

    // Socket Address (16 bytes)
    ushort sinFamily = (ushort)(buffer[pos++] | (buffer[pos++] << 8));
    ushort sinPort = (ushort)((buffer[pos++] << 8) | buffer[pos++]);  // Big-endian

    // IP Address (4 bytes)
    var ipBytes = new byte[4];
    Array.Copy(buffer, pos, ipBytes, 0, 4);
    device.IPAddress = new IPAddress(ipBytes);
    pos += 4;

    // Skip sin_zero (8 bytes)
    pos += 8;

    // Vendor ID (2 bytes, little-endian)
    device.VendorId = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

    // Device Type (2 bytes, little-endian)
    device.DeviceType = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

    // Product Code (2 bytes, little-endian)
    device.ProductCode = (ushort)(buffer[pos++] | (buffer[pos++] << 8));

    // Revision (2 bytes) - Minor.Major
    byte revisionMinor = buffer[pos++];
    byte revisionMajor = buffer[pos++];
    device.FirmwareRevision = new Version(revisionMajor, revisionMinor);

    // Skip Status (2 bytes)
    pos += 2;

    // Serial Number (4 bytes, little-endian)
    device.SerialNumber = (uint)(buffer[pos++] | (buffer[pos++] << 8) |
                                 (buffer[pos++] << 16) | (buffer[pos++] << 24));

    // Product Name (1 byte length + ASCII string)
    byte productNameLength = buffer[pos++];
    device.ProductName = Encoding.ASCII.GetString(buffer, pos, productNameLength);

    return device;
}
```

### Example 4: Build BootP Reply

**C# Code:**
```csharp
public static byte[] BuildReply(BootPPacket request, IPAddress assignedIP,
                                 IPAddress serverIP, IPAddress subnetMask,
                                 IPAddress? gateway = null)
{
    var packet = new byte[MINIMUM_PACKET_SIZE + 256];
    int offset = 0;

    // Op = BOOTREPLY (0x02)
    packet[offset++] = 0x02;

    // Htype, Hlen, Hops
    packet[offset++] = request.Htype;
    packet[offset++] = request.Hlen;
    packet[offset++] = 0;

    // Xid (4 bytes, network byte order - copy from request)
    packet[offset++] = (byte)((request.Xid >> 24) & 0xFF);
    packet[offset++] = (byte)((request.Xid >> 16) & 0xFF);
    packet[offset++] = (byte)((request.Xid >> 8) & 0xFF);
    packet[offset++] = (byte)(request.Xid & 0xFF);

    // Secs, Flags (copy from request)
    packet[offset++] = (byte)((request.Secs >> 8) & 0xFF);
    packet[offset++] = (byte)(request.Secs & 0xFF);
    packet[offset++] = (byte)((request.Flags >> 8) & 0xFF);
    packet[offset++] = (byte)(request.Flags & 0xFF);

    // CIADDR (4 bytes) = 0.0.0.0
    offset += 4;

    // YIADDR (4 bytes) = assigned IP
    var yiaddrBytes = assignedIP.GetAddressBytes();
    Array.Copy(yiaddrBytes, 0, packet, offset, 4);
    offset += 4;

    // SIADDR (4 bytes) = server IP
    var siaddrBytes = serverIP.GetAddressBytes();
    Array.Copy(siaddrBytes, 0, packet, offset, 4);
    offset += 4;

    // GIADDR (4 bytes) = gateway or 0.0.0.0
    if (gateway != null)
    {
        var giaddrBytes = gateway.GetAddressBytes();
        Array.Copy(giaddrBytes, 0, packet, offset, 4);
    }
    offset += 4;

    // CHADDR (16 bytes) - copy from request
    Array.Copy(request.Chaddr, 0, packet, offset, 16);
    offset += 16;

    // SNAME (64 bytes)
    var sname = Encoding.ASCII.GetBytes("EtherNetIPTool");
    Array.Copy(sname, 0, packet, offset, Math.Min(sname.Length, 64));
    offset += 64;

    // FILE (128 bytes) - empty
    offset += 128;

    // OPTIONS - Magic Cookie
    packet[offset++] = 0x63;
    packet[offset++] = 0x82;
    packet[offset++] = 0x53;
    packet[offset++] = 0x63;

    // Option 1: Subnet Mask
    packet[offset++] = 1;    // Code
    packet[offset++] = 4;    // Length
    var maskBytes = subnetMask.GetAddressBytes();
    Array.Copy(maskBytes, 0, packet, offset, 4);
    offset += 4;

    // Option 3: Router (if provided)
    if (gateway != null)
    {
        packet[offset++] = 3;    // Code
        packet[offset++] = 4;    // Length
        var gwBytes = gateway.GetAddressBytes();
        Array.Copy(gwBytes, 0, packet, offset, 4);
        offset += 4;
    }

    // Option 255: End
    packet[offset++] = 255;

    // Return actual size
    var result = new byte[offset];
    Array.Copy(packet, 0, result, 0, offset);
    return result;
}
```

### Example 5: Parse CIP Status Code

**C# Code:**
```csharp
public static string GetStatusMessage(byte statusCode)
{
    return statusCode switch
    {
        0x00 => "Success",
        0x04 => "Path destination unknown - Device may not support this attribute",
        0x05 => "Path segment error - Invalid CIP path format",
        0x08 => "Service not supported - Device does not support Set_Attribute_Single",
        0x0F => "Attribute not supported - Device does not have this configuration attribute",
        0x13 => "Not enough data - Configuration value is incomplete",
        0x14 => "Attribute not settable - Configuration value is read-only",
        0x1C => "Privilege violation - Device requires authorization",
        0x26 => "Invalid parameter - Configuration value format is incorrect",
        _ => $"Unknown error code: 0x{statusCode:X2}"
    };
}
```

### Example 6: Encapsulation Header Serialization

**C# Code:**
```csharp
public byte[] ToBytes()
{
    var buffer = new byte[24];
    int offset = 0;

    // Command (2 bytes, little-endian)
    buffer[offset++] = (byte)(Command & 0xFF);
    buffer[offset++] = (byte)((Command >> 8) & 0xFF);

    // Length (2 bytes, little-endian)
    buffer[offset++] = (byte)(Length & 0xFF);
    buffer[offset++] = (byte)((Length >> 8) & 0xFF);

    // Session Handle (4 bytes, little-endian)
    buffer[offset++] = (byte)(SessionHandle & 0xFF);
    buffer[offset++] = (byte)((SessionHandle >> 8) & 0xFF);
    buffer[offset++] = (byte)((SessionHandle >> 16) & 0xFF);
    buffer[offset++] = (byte)((SessionHandle >> 24) & 0xFF);

    // Status (4 bytes, little-endian)
    buffer[offset++] = (byte)(Status & 0xFF);
    buffer[offset++] = (byte)((Status >> 8) & 0xFF);
    buffer[offset++] = (byte)((Status >> 16) & 0xFF);
    buffer[offset++] = (byte)((Status >> 24) & 0xFF);

    // Sender Context (8 bytes)
    Array.Copy(SenderContext, 0, buffer, offset, 8);
    offset += 8;

    // Options (4 bytes, little-endian)
    buffer[offset++] = (byte)(Options & 0xFF);
    buffer[offset++] = (byte)((Options >> 8) & 0xFF);
    buffer[offset++] = (byte)((Options >> 16) & 0xFF);
    buffer[offset++] = (byte)((Options >> 24) & 0xFF);

    return buffer;
}
```

---

## Cross-References

### Related Documentation

- **[PRD.md](../02-REQUIREMENTS/PRD.md)** - Product Requirements Document
  - Section 4.1: EtherNet/IP Protocol specifications
  - Section 4.2: BootP/DHCP Protocol specifications
  - Section 4.3: Network Operations details

- **[IMPLEMENTATION_GUIDE.md](./IMPLEMENTATION_GUIDE.md)** - Implementation Guide
  - Device discovery workflow
  - Configuration write sequence
  - BootP server implementation
  - Error handling strategies

- **[ODVA_COMPLIANCE_PLAN.md](../02-REQUIREMENTS/ODVA_COMPLIANCE_PLAN.md)** - ODVA Compliance Plan
  - EtherNet/IP conformance requirements
  - CIP object model compliance
  - Testing and validation procedures

### Source Code References

**CIP Protocol Implementation:**
- `E:\Github\eip-comm-tool\src\Core\CIP\CIPEncapsulation.cs` - Encapsulation header structures
- `E:\Github\eip-comm-tool\src\Core\CIP\ListIdentityMessage.cs` - Device discovery
- `E:\Github\eip-comm-tool\src\Core\CIP\SetAttributeSingleMessage.cs` - Configuration messages
- `E:\Github\eip-comm-tool\src\Core\CIP\CIPStatusCodes.cs` - Status code translation

**BootP/DHCP Implementation:**
- `E:\Github\eip-comm-tool\src\Core\BootP\BootPPacket.cs` - Packet structure and parsing
- `E:\Github\eip-comm-tool\src\Core\BootP\BootPServer.cs` - Server implementation

**Network Layer:**
- `E:\Github\eip-comm-tool\src\Core\Network\EtherNetIPSocket.cs` - Socket management

### Help System References

**Embedded HTML Documentation:**
- `E:\Github\eip-comm-tool\src\Resources\Help\CIPProtocolReference.html` - User-facing CIP reference
- `E:\Github\eip-comm-tool\src\Resources\Help\BootPReference.html` - User-facing BootP reference

### External Standards

**ODVA Specifications:**
- CIP Networks Library Volume 1: Common Industrial Protocol Specification
- CIP Networks Library Volume 2: EtherNet/IP Adaptation of CIP
- TCP/IP Interface Object Specification (Class 0xF5)
- Available at: https://www.odva.org/

**IETF RFCs:**
- RFC 951: Bootstrap Protocol (BootP)
- RFC 2131: Dynamic Host Configuration Protocol (DHCP)
- RFC 2132: DHCP Options and BOOTP Vendor Extensions
- Available at: https://www.ietf.org/rfc/

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-31 | Claude Code | Initial comprehensive protocol reference |

---

**Document End**
