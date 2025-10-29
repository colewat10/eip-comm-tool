EtherNet/IP Commissioning Tool
Product Requirements Document (PRD)
Version 1.0 - MVP

1. EXECUTIVE SUMMARY
1.1 Product Overview
EtherNet/IP Commissioning Tool is a Windows desktop application that consolidates industrial Ethernet device commissioning and troubleshooting capabilities into a single unified interface. The tool replaces the need for multiple applications (RSLinx, BootP-DHCP utilities, web browsers, command-line tools) during device setup and basic diagnostics.
1.2 Purpose and Goals

Reduce tool-switching time during commissioning by 70%
Provide single-window workflow for common commissioning tasks
Support both existing devices (EtherNet/IP mode) and factory-default devices (BootP/DHCP mode)
Deliver professional, dense UI matching industrial engineering tool standards

1.3 Target Users

Industrial controls engineers
System integrators
Maintenance technicians
Primary environment: Allen-Bradley, SICK, Banner, Pepperl+Fuchs devices

1.4 Success Criteria

Installation and first use within 5 minutes
90% of commissioning tasks completable without switching applications
Zero crashes during device configuration operations
Operates without administrator rights for EtherNet/IP mode

1.5 Development Approach
Entirely AI-generated codebase using .NET 8/WPF with modular architecture for maintainability and clear separation of concerns.

2. SCOPE DEFINITION
2.1 In Scope (MVP)
Core Functionality:

Network interface selection with auto-detection
Two operating modes: EtherNet/IP and BootP/DHCP (mutually exclusive)
Device discovery via CIP List Identity broadcast
IP address configuration via CIP Set_Attribute_Single
BootP/DHCP server for factory-default device commissioning
Device inventory table with sorting capability
Activity logging with export capability
Comprehensive help system with embedded documentation

Supported Protocols:

EtherNet/IP (CIP over Ethernet)
BootP/DHCP
ICMP (for validation)

Target Devices:

Allen-Bradley: ControlLogix, CompactLogix, Stratix switches, PowerFlex drives, POINT I/O
SICK: Industrial sensors with EtherNet/IP
Banner: Industrial sensors and indicators with EtherNet/IP
Pepperl+Fuchs: IO-Link masters, field devices with EtherNet/IP
Any device supporting EtherNet/IP and CIP TCP/IP Interface Object (Class 0xF5)

2.2 Out of Scope (MVP)

PLC programming or logic editing
Firmware management/updates
Advanced packet capture (Wireshark replacement)
PROFINET, Modbus TCP, OPC UA protocols
Bulk/batch device configuration
Device configuration templates or presets
Remote access or cloud connectivity
Mobile applications
Configuration backup/restore
Multi-adapter simultaneous operation

2.3 Key Constraints
MVP Limitations:

Single device configuration workflow (one device at a time)
No session persistence (device list clears on application exit)
Fixed window size (1280x768)
Windows 10/11 only
Same-subnet operation only (no routing to remote subnets)
Manual IP assignment (no automatic IP pool management)


3. FUNCTIONAL REQUIREMENTS
3.1 Network Interface Selection
REQ-3.1-001: Application shall enumerate all active network adapters on startup and populate NIC dropdown selector with adapter name and current IP address.
REQ-3.1-002: User shall be able to refresh NIC list via toolbar button to detect hot-plugged adapters.
REQ-3.1-003: Application shall display selected NIC's IP address and subnet mask in the toolbar.
REQ-3.1-004: Application shall automatically select the first non-loopback adapter with valid IP configuration on startup.
REQ-3.1-005: Changing NIC selection shall clear device list and restart discovery if auto-browse is enabled.

3.2 Operating Modes
REQ-3.2-001: Application shall support two mutually exclusive operating modes: EtherNet/IP and BootP/DHCP, selected via radio buttons.
REQ-3.2-002: EtherNet/IP mode shall be the default mode on application startup.
REQ-3.2-003: Switching to BootP/DHCP mode shall disable auto-browse controls and clear device table.
REQ-3.2-004: Switching to EtherNet/IP mode from BootP/DHCP shall re-enable auto-browse and trigger immediate scan if auto-browse is enabled.
REQ-3.2-005: BootP/DHCP mode shall be disabled (grayed out) if application is not running with Administrator privileges, with tooltip explaining privilege requirement.

3.3 Device Discovery (EtherNet/IP Mode)
3.3.1 Discovery Protocol
REQ-3.3.1-001: Application shall discover devices by broadcasting CIP List Identity (command 0x0063) to calculated subnet broadcast address on port 44818 via selected NIC. See REQ-4.1.1-002 for broadcast address calculation.
REQ-3.3.1-002: Discovery broadcast shall use EtherNet/IP encapsulation protocol with unconnected message format.
REQ-3.3.1-003: Application shall wait 3 seconds for List Identity responses after each broadcast.
REQ-3.3.1-004: Application shall parse responses to extract: IP address, MAC address (via ARP lookup), vendor ID, device type, product code, product name, serial number.
REQ-3.3.1-005: Vendor ID shall be mapped to vendor name using standard CIP vendor ID table (0x0001=Allen-Bradley, 0x0103=SICK, 0x017F=Banner, 0x0083=Pepperl+Fuchs).
3.3.2 Auto-Browse Functionality
REQ-3.3.2-001: Auto-browse checkbox shall enable/disable automatic periodic device discovery.
REQ-3.3.2-002: Auto-browse shall be enabled by default on application startup.
REQ-3.3.2-003: Scan interval shall be user-configurable from 1-60 seconds, default 5 seconds.
REQ-3.3.2-004: Auto-browse shall perform discovery scans in background thread without blocking UI.
REQ-3.3.2-005: Device table shall update in real-time as responses are received.
REQ-3.3.2-006: Devices not responding for 3 consecutive scans (15 seconds default) shall be removed from table.
3.3.3 Manual Scan
REQ-3.3.3-001: "Scan Now" button shall trigger immediate discovery broadcast regardless of auto-browse state.
REQ-3.3.3-002: Manual scan shall clear existing device list before populating new results.
REQ-3.3.3-003: Manual scan shall display "Scanning..." status during 3-second response wait period.
3.3.4 Device List Management
REQ-3.3.4-001: "Clear List" button shall remove all devices from table and reset discovery state.
REQ-3.3.4-002: Device list shall not persist between application sessions (cleared on exit).
REQ-3.3.4-003: Duplicate devices (same MAC address) shall be updated in place, not added as new entries.

3.4 Device Table
REQ-3.4-001: Device table shall display columns: Row #, MAC Address (full format XX:XX:XX:XX:XX:XX), IP Address, Subnet Mask, Vendor, Model, Status.
REQ-3.4-002: Model column shall truncate long device names with ellipsis, full name shown in tooltip on hover.
REQ-3.4-003: All columns shall be sortable by clicking column header (ascending/descending toggle).
REQ-3.4-004: Default sort order shall be discovery order (chronological).
REQ-3.4-005: Table rows shall be 20px height for optimal density.
REQ-3.4-006: Single row selection only (one device highlighted at a time).
REQ-3.4-007: Status column shall display: "OK" (normal), "Link-Local" (169.254.x.x IP), "Conflict" (duplicate IP detected on subnet).
REQ-3.4-008: Link-Local rows shall be highlighted in light yellow background.
REQ-3.4-009: Conflict rows shall be highlighted in light red background.
REQ-3.4-010: Double-clicking a row shall open configuration dialog for that device.
REQ-3.4-011: Right-click context menu shall provide: Configure, Copy MAC, Copy IP, Ping Device, Refresh Info.
REQ-3.4-012: Device table shall show device count in section header (e.g., "12 device(s)").
REQ-3.4-013: When BootP/DHCP mode active, table shall display centered message: "BootP/DHCP Server Active - Listening for device requests on UDP 68".

3.5 Device Configuration (EtherNet/IP Mode)
3.5.1 Configuration Dialog Trigger
REQ-3.5.1-001: "Configure Selected Device" button shall be enabled only when a device is selected in table.
REQ-3.5.1-002: Clicking button or double-clicking device row shall open modal configuration dialog.
REQ-3.5.1-003: Configuration dialog shall be fixed size 500x400 pixels, centered on parent window.
3.5.2 Dialog Display - Current Configuration
REQ-3.5.2-001: Dialog shall display read-only device information section showing: MAC address, vendor name and ID, device type and code, product name, serial number, current IP, current subnet, current gateway (if readable).
REQ-3.5.2-002: Current configuration shall be retrieved via CIP Get_Attribute_Single if not available from List Identity response.
3.5.3 Dialog Input - New Configuration
REQ-3.5.3-001: IP Address and Subnet Mask fields shall be required (marked with asterisk).
REQ-3.5.3-002: Gateway, Hostname, and DNS Server fields shall be optional.
REQ-3.5.3-003: IP and subnet fields shall use 4-octet input boxes, each octet accepting 0-255 numeric values only.
REQ-3.5.3-004: Hostname field shall accept alphanumeric characters, hyphens, and underscores only, max 64 characters.
REQ-3.5.3-005: All IP address fields shall validate as proper IPv4 format.
REQ-3.5.3-006: Gateway and DNS IPs shall be validated to be on same subnet as IP/Subnet combination.
REQ-3.5.3-007: Validation errors shall display next to invalid field in red text.
REQ-3.5.3-008: "Apply Configuration" button shall be disabled until all required fields are valid.
3.5.4 Confirmation Flow
REQ-3.5.4-001: Clicking "Apply Configuration" shall display confirmation dialog showing current and new configuration side-by-side.
REQ-3.5.4-002: Confirmation dialog shall have "Apply" and "Cancel" buttons.
REQ-3.5.4-003: User must explicitly click "Apply" to proceed with configuration write.
3.5.5 CIP Write Operations
REQ-3.5.5-001: Configuration shall be written via CIP Set_Attribute_Single service (0x10) to TCP/IP Interface Object (Class 0xF5, Instance 1).
REQ-3.5.5-002: Attributes shall be written in sequence: IP Address (Attr 5), Subnet Mask (Attr 6), Gateway (Attr 7, if provided), Hostname (Attr 8, if provided), DNS Server (Attr 10, if provided).
REQ-3.5.5-003: Each write operation shall use Unconnected Send (Service 0x52) via UCMM, no session required.
REQ-3.5.5-004: Each CIP message shall have 3-second timeout.
REQ-3.5.5-005: Application shall wait 100ms between sequential attribute writes.
REQ-3.5.5-006: Progress indicator shall display "Sending configuration... (X/Y)" during write operations.
REQ-3.5.5-007: If any write fails, remaining writes shall be skipped and error reported.
REQ-3.5.5-008: Success/failure result shall be displayed in modal dialog after completion.
REQ-3.5.5-009: On success, device shall be removed from list (will reappear at new IP on next scan).
REQ-3.5.5-010: Common CIP error codes shall be translated to human-readable messages (0x04=Path unknown, 0x08=Service not supported, 0x0F=Attribute not supported, 0x14=Attribute not settable).

3.6 BootP/DHCP Server Functionality
3.6.1 Server Activation
REQ-3.6.1-001: Selecting BootP/DHCP mode radio button shall start UDP server listening on port 68.
REQ-3.6.1-002: If port binding fails due to privilege error (SocketException with AccessDenied), application shall display error message explaining Administrator requirement and revert to EtherNet/IP mode.
REQ-3.6.1-003: Status bar shall display "BootP Server: Listening on UDP 68" when mode active.
REQ-3.6.1-004: Switching back to EtherNet/IP mode shall stop UDP server and close socket.
3.6.2 Request Detection
REQ-3.6.2-001: Application shall listen for BootP BOOTREQUEST packets (Op=0x01) on UDP port 68.
REQ-3.6.2-002: Requests shall be filtered to selected NIC only (ignore requests on other interfaces).
REQ-3.6.2-003: Upon receiving valid request, application shall immediately display BootP configuration dialog as modal popup.
REQ-3.6.2-004: Multiple simultaneous requests shall be queued and presented one at a time.
3.6.3 BootP Configuration Dialog
REQ-3.6.3-001: Dialog shall display device MAC address from request CHADDR field.
REQ-3.6.3-002: Dialog shall display request timestamp and transaction ID (XID).
REQ-3.6.3-003: User shall enter IP Address (required), Subnet Mask (required), and Gateway (optional).
REQ-3.6.3-004: Validation rules shall match EtherNet/IP configuration dialog.
REQ-3.6.3-005: Checkbox "Disable DHCP mode after assignment" shall be checked by default.
REQ-3.6.3-006: "Assign & Configure" button shall be enabled only when required fields are valid.
REQ-3.6.3-007: "Ignore Request" button shall close dialog without taking action.
3.6.4 BootP Reply and DHCP Disable
REQ-3.6.4-001: Clicking "Assign & Configure" shall send BootP BOOTREPLY packet (Op=0x02) with assigned IP in YIADDR field.
REQ-3.6.4-002: Reply shall include DHCP options: Magic Cookie (0x63825363), Option 1 (Subnet Mask), Option 3 (Router/Gateway if provided), Option 255 (End).
REQ-3.6.4-003: Reply destination shall be broadcast (255.255.255.255:68) or unicast to assigned IP depending on request FLAGS field.
REQ-3.6.4-004: Reply source port shall be UDP 67, destination MAC shall match request CHADDR.
REQ-3.6.4-005: After sending reply, application shall wait 2 seconds for device to configure itself.
REQ-3.6.4-006: If "Disable DHCP mode" checkbox is checked, application shall send CIP Set_Attribute_Single to Configuration Control attribute (Class 0xF5, Instance 1, Attribute 3) with value 0x00000001 to set static IP mode.
REQ-3.6.4-007: DHCP disable command shall have 3-second timeout.
REQ-3.6.4-008: Success/failure result shall be displayed in result dialog.
REQ-3.6.4-009: After completion, application shall suggest user switch to EtherNet/IP mode to verify device at new IP.

3.7 Logging and Reporting
REQ-3.7-001: Application shall maintain detailed activity log of all operations.
REQ-3.7-002: Log entries shall include timestamp (HH:MM:SS.mmm), category (INFO, SCAN, DISC, CONFIG, CIP, BOOTP, ERROR, WARN), and message text.
REQ-3.7-003: Log shall capture: NIC selection, mode changes, scan operations, device discoveries, configuration attempts, CIP message results, BootP transactions, errors and warnings.
REQ-3.7-004: Log viewer shall be accessible via Tools menu > Activity Log.
REQ-3.7-005: Log viewer window shall display log entries in scrollable list with filter by category.
REQ-3.7-006: Log shall be exportable to text file via "Export Log" button in viewer.
REQ-3.7-007: Log file shall use UTF-8 encoding with .txt extension.
REQ-3.7-008: Log shall be cleared on application exit (not persisted between sessions).
REQ-3.7-009: "Clear Log" button in viewer shall empty log without closing viewer window.

3.8 Help and Documentation
3.8.1 Contextual Help
REQ-3.8.1-001: All input fields and buttons shall have tooltips appearing on mouse hover after 500ms delay.
REQ-3.8.1-002: Status bar shall display extended help text for focused UI element.
REQ-3.8.1-003: Help text shall be concise, technical, and relevant to controls engineers.
3.8.2 Embedded Manual
REQ-3.8.2-001: Help menu > User Manual shall open embedded HTML help viewer.
REQ-3.8.2-002: Manual shall include sections: Getting Started, EtherNet/IP Mode, BootP/DHCP Mode, Troubleshooting, Technical Reference.
REQ-3.8.2-003: F1 key shall open help viewer to context-sensitive section based on current UI focus.
REQ-3.8.2-004: Manual shall be navigable with table of contents and search functionality.
3.8.3 Technical Reference
REQ-3.8.3-001: Help menu > CIP Protocol Reference shall provide technical details on: CIP object classes, TCP/IP Interface Object attributes, packet structures, status codes.
REQ-3.8.3-002: Help menu > BootP/DHCP Reference shall document BootP packet structure, DHCP options used, port requirements.
REQ-3.8.3-003: Help menu > Troubleshooting Guide shall provide flowcharts and solutions for: device not discovered, configuration fails, IP conflicts, BootP request not received, privilege errors.

4. TECHNICAL SPECIFICATIONS
4.1 EtherNet/IP Protocol
4.1.1 Discovery Protocol Implementation

REQ-4.1.1-001: Single Socket Architecture

Application shall use single UDP socket with OS-assigned ephemeral
port for device discovery, ensuring compatibility with RSLinx and
other EtherNet/IP tools.

REQ-4.1.1-002: Subnet Broadcast Only

Application shall calculate subnet broadcast address from selected
NIC's IP and subnet mask, and send ListIdentity broadcasts to subnet
broadcast address only (e.g., 192.168.21.255), not global broadcast
(255.255.255.255).

Rationale: Subnet broadcast ensures traffic routes through correct
network interface and respects network isolation, preventing
discovery packets from leaking to VPN or other unintended interfaces.

REQ-4.1.1-003: Known Limitation - Subnet Configuration

Discovery requires selected NIC to have correct subnet mask
configured. Devices outside the calculated subnet broadcast range
will not be discovered. User must ensure proper network configuration.

4.1.2 List Identity Packet Structure
Packet Structure:

Encapsulation Command: 0x0063 (List Identity)
Length: 0x0000
Session Handle: 0x00000000
Status: 0x00000000
Sender Context: 8-byte random/sequential identifier
Options: 0x00000000
No CPF items in request

Response Parsing:

Extract Socket Address structure for IP address
Extract Vendor ID (2 bytes) and map to vendor name
Extract Device Type (2 bytes) and Product Code (2 bytes)
Extract Product Name (length-prefixed ASCII string)
Extract Serial Number (4 bytes)
Perform ARP table lookup for MAC address using returned IP

Vendor ID Mapping:

0x0001 = Allen-Bradley / Rockwell Automation
0x0083 = Pepperl+Fuchs
0x0103 = SICK
0x017F = Banner Engineering
Unknown IDs display as hex value

4.1.3 Set_Attribute_Single (Device Configuration)
Message Structure:

Encapsulation Command: 0x006F (SendRRData)
CPF Items: Null Address Item (0x0000) + Unconnected Data Item (0x00B2)
Unconnected Send Service: 0x52
Request Path: Message Router (Class 0x06, Instance 1)
Priority/Tick Time: 0x05
Timeout Ticks: 0xF9 (approximately 2 seconds)
Embedded Message: Set_Attribute_Single (Service 0x10)
Target Path: TCP/IP Interface Object (Class 0xF5, Instance 1, Attribute ID)

Attribute IDs:

Attribute 3: Configuration Control (DWORD, 0x00000001 = Static IP)
Attribute 5: IP Address (4 bytes, network byte order)
Attribute 6: Subnet Mask (4 bytes, network byte order)
Attribute 7: Gateway Address (4 bytes, network byte order)
Attribute 8: Hostname (String, length-prefixed ASCII)
Attribute 10: DNS Server (4 bytes, network byte order)

Error Codes:

0x00 = Success
0x04 = Path destination unknown
0x05 = Path segment error
0x08 = Service not supported
0x0F = Attribute not supported
0x13 = Not enough data
0x14 = Attribute not settable
0x1C = Privilege violation
0x26 = Invalid parameter


4.2 BootP/DHCP Protocol
4.2.1 BootP Request Structure
Fields to Parse:

Op: 0x01 (BOOTREQUEST)
Htype: 0x01 (Ethernet)
Hlen: 0x06 (MAC address length)
XID: Transaction ID (4 bytes, copy to reply)
FLAGS: Broadcast flag (0x0000 or 0x8000)
CHADDR: Client MAC address (first 6 bytes of 16-byte field)

4.2.2 BootP Reply Structure
Fields to Send:

Op: 0x02 (BOOTREPLY)
Htype: 0x01
Hlen: 0x06
XID: Copy from request
FLAGS: Copy from request
YIADDR: Assigned IP address (4 bytes)
SIADDR: Server IP (selected NIC IP)
GIADDR: 0.0.0.0 or gateway if specified
CHADDR: Copy from request
SNAME: "EtherNetIPTool" or empty
FILE: Empty
OPTIONS:

Magic Cookie: 0x63 0x82 0x53 0x63
Option 1 (Subnet Mask): 0x01 0x04 [4 bytes]
Option 3 (Router): 0x03 0x04 [4 bytes] (if gateway provided)
Option 255 (End): 0xFF



Transmission:

Source Port: UDP 67
Destination Port: UDP 68
Destination IP: 255.255.255.255 (broadcast) or assigned IP based on FLAGS
Destination MAC: Client MAC from CHADDR
Send on selected NIC only


4.3 Network Operations
4.3.1 NIC Enumeration
Use System.Net.NetworkInformation.NetworkInterface to enumerate adapters.
Filter Criteria:

OperationalStatus == Up
NetworkInterfaceType == Ethernet or Wireless80211
Has IPv4 unicast address assigned
Not loopback (127.0.0.1)

Display Format:
"[Adapter Name] - [IP Address]"
4.3.2 ARP Table Lookup
Use System.Net.NetworkInformation.IPGlobalProperties.GetIPNeighborInformation() to resolve IP to MAC.
Fallback:
If ARP entry not found, send ICMP ping to populate ARP cache, then retry lookup.
4.3.3 Broadcast Handling
Use UdpClient with socket options:

EnableBroadcast = true
Bind to specific NIC IP (not 0.0.0.0)
Set broadcast destination 255.255.255.255

4.3.4 Timeout Values

Discovery scan wait: 3 seconds
CIP message timeout: 3 seconds
BootP transaction timeout: 10 seconds
Inter-message delay: 100ms
Socket send/receive: 5 seconds
Auto-browse interval: 5 seconds default (1-60 configurable)
Device removal: 15 seconds (3 missed scans)


5. USER INTERFACE SPECIFICATIONS
5.1 Main Window
Dimensions:

Fixed size: 1280 x 768 pixels
Not resizable
Centered on screen on startup

Layout Sections:

Menu bar (20px height)
Toolbar (24px height)
Mode/control panel (40px height)
Device table section header (20px height)
Device table (480px height, scrollable)
Action buttons (30px height)
Status bar (20px height)

Color Scheme:

Background: Standard Windows control gray (#F0F0F0)
Section headers: Light gray background (#E8E8E8)
Table header: Medium gray (#D0D0D0)
Selected row: Blue highlight (system selection color)
Link-Local row: Light yellow (#FFFACD)
Conflict row: Light red (#FFE6E6)
Text: Black (#000000)
Disabled text: Gray (#808080)

Typography:

Primary font: Segoe UI, 9pt
Headers: Segoe UI, 9pt Bold
Status bar: Segoe UI, 8pt
Table: Segoe UI, 9pt (fixed-width for alignment)

5.2 Menu Structure
File:

Exit

Edit:

Preferences (scan interval, timeouts, log verbosity)

Tools:

Activity Log Viewer
Export Device List to CSV
Clear Device List

View:

Refresh NIC List
Refresh Device Table

Help:

User Manual (F1)
CIP Protocol Reference
BootP/DHCP Reference
Troubleshooting Guide
About

5.3 Toolbar
Left side:

Label "NIC:"
Dropdown selector showing "[Adapter Name] - [IP]"
Refresh button (circular arrow icon)

Right side:

Clock display (HH:MM:SS AM/PM)

5.4 Mode and Control Panel
Left section (150px width):

Label "Mode:"
Radio button "EtherNet/IP"
Radio button "BootP/DHCP"
BootP disabled with tooltip if not admin

Right section:

Label "Auto-Browse:"
Checkbox "Enabled"
Label "Interval:"
Numeric input (1-60) with "s" suffix
Button "Scan Now"
Button "Clear List"

5.5 Device Table
Column Configuration:


(30px): Row number, right-aligned

MAC Address (140px): Full format XX:XX:XX:XX:XX:XX
IP Address (120px): XXX.XXX.XXX.XXX
Subnet Mask (120px): XXX.XXX.XXX.XXX
Vendor (80px): Text name or hex ID
Model (200px): Truncated with ellipsis, tooltip shows full
Status (80px): OK/Link-Local/Conflict

Table Behavior:

Fixed header row
Scrollable body
Single selection
Row height 20px
Alternating row colors (white/#F8F8F8)
Click column header to sort (toggle asc/desc)
Double-click row to configure
Right-click for context menu

Context Menu:

Configure Device
Copy MAC Address
Copy IP Address
Ping Device
Refresh Device Info

5.6 Action Buttons
Horizontal layout below table:

Button "Configure Selected Device" (enabled when row selected)
Button "Refresh Device Info" (enabled when row selected)
Buttons 120px width, 24px height
10px spacing between buttons

5.7 Status Bar
Three sections (separated by vertical dividers):

Left: General status text (40% width)
Center: Timing info (30% width)
Right: Selection info (30% width)

Status Text Examples:

"Ready"
"Scanning... (X devices found)"
"Configuring device..."
"BootP Server: Listening on UDP 68"
"Error: [error message]"

5.8 Configuration Dialog (EtherNet/IP)
Modal Dialog:

Size: 500 x 400 pixels
Centered on parent
Title: "Configure EtherNet/IP Device"

Layout:

Device Information box (read-only, 8 fields, 120px height)
Spacing (10px)
New Configuration box (input fields, 180px height)
Required field note (15px)
Button panel (35px): "Apply Configuration" / "Cancel"

Input Fields:

IP Address: 4 octets, 40px each
Subnet Mask: 4 octets, 40px each
Gateway: 4 octets, 40px each, "(optional)" label
Hostname: Single text box, 200px width, "(optional, max 64 chars)" label
DNS Server: 4 octets, 40px each, "(optional)" label

Validation:

Invalid fields show red text error below field
Apply button disabled until all required fields valid

5.9 Confirmation Dialog
Modal Dialog:

Size: 400 x 300 pixels
Title: "Confirm Configuration"
Two-column layout showing current vs. new values
Buttons: "Apply" / "Cancel"

5.10 BootP Configuration Dialog
Modal Dialog:

Size: 450 x 350 pixels
Auto-popup on BootP request detection
Title: "BootP Request Detected"

Layout:

Request information box (MAC, timestamp, XID, 80px height)
Spacing (10px)
Configuration input box (IP, Subnet, Gateway, 120px height)
Checkbox "Disable DHCP mode after assignment" (checked by default)
Button panel: "Assign & Configure" / "Ignore Request"

5.11 Log Viewer Window
Window:

Size: 800 x 600 pixels
Resizable
Title: "Activity Log"

Layout:

Filter toolbar (category checkboxes: INFO, SCAN, DISC, CONFIG, CIP, BOOTP, ERROR, WARN)
Log text area (scrollable, fixed-width font)
Button panel: "Export Log" / "Clear Log" / "Close"

Log Display:

Fixed-width font (Consolas, 9pt)
Color-coded by category (INFO=black, ERROR=red, WARN=orange)
Timestamp | Category | Message format


6. NON-FUNCTIONAL REQUIREMENTS
6.1 Performance
REQ-6.1-001: Discovery scan shall complete within 3 seconds for /24 subnet (256 addresses).
REQ-6.1-002: UI shall remain responsive during all background network operations (use async/await).
REQ-6.1-003: Application startup time shall be less than 3 seconds on standard industrial PC (Intel i5, 8GB RAM, SSD).
REQ-6.1-004: Configuration dialog shall open within 500ms of button click.
REQ-6.1-005: Table sorting shall complete within 100ms for up to 256 devices.
6.2 Usability
REQ-6.2-001: Application shall auto-detect and select appropriate NIC on startup (first non-loopback with valid IP).
REQ-6.2-002: All interactive elements shall provide visual feedback on hover and click.
REQ-6.2-003: Keyboard navigation shall be supported (Tab, Enter, Escape, F1, arrow keys in table).
REQ-6.2-004: Tooltips shall appear within 500ms of hover and contain relevant technical information.
REQ-6.2-005: Error messages shall be specific, actionable, and avoid technical jargon where possible.
6.3 Reliability
REQ-6.3-001: Application shall handle NIC cable disconnect/reconnect gracefully without crashing.
REQ-6.3-002: Malformed or unexpected device responses shall be logged but not cause exceptions.
REQ-6.3-003: Socket timeouts shall be enforced on all network operations.
REQ-6.3-004: Application shall not hang or freeze under any network condition.
REQ-6.3-005: Memory usage shall not exceed 200MB during normal operation.
6.4 Security and Privileges
REQ-6.4-001: Application shall operate in EtherNet/IP mode without Administrator privileges.
REQ-6.4-002: Application shall detect privilege level on startup using WindowsIdentity/WindowsPrincipal.
REQ-6.4-003: BootP/DHCP mode shall be disabled (grayed out) if not running as Administrator, with explanatory tooltip.
REQ-6.4-004: If BootP port binding fails due to privilege error, application shall display user-friendly error message and revert to EtherNet/IP mode.
REQ-6.4-005: Application shall provide menu option to restart as Administrator.
REQ-6.4-006: No data shall be transmitted outside local network (no telemetry, cloud connectivity, or external services).
REQ-6.4-007: Application shall not modify Windows firewall rules or system network configuration.
6.5 Compatibility
REQ-6.5-001: Application shall run on Windows 10 (version 1809+) and Windows 11.
REQ-6.5-002: Application shall function on industrial PCs with minimum specifications: Intel i5 equivalent, 8GB RAM, 100MB free disk space.
REQ-6.5-003: Application shall support standard Ethernet adapters (Intel, Realtek, Broadcom chipsets).
REQ-6.5-004: Application shall work on networks with 1Gbps and 100Mbps Ethernet speeds.
REQ-6.5-005: Application shall handle screen resolutions from 1280x768 to 4K (window remains fixed size).
6.6 Maintainability
REQ-6.6-001: Code shall be organized in modular structure with clear separation of concerns.
REQ-6.6-002: MVVM pattern shall be used for UI code (WPF Views, ViewModels, Models).
REQ-6.6-003: Network protocol code shall be isolated in separate classes (CIPClient, BootPServer).
REQ-6.6-004: Configuration values (timeouts, defaults) shall be defined as constants at top of classes.
REQ-6.6-005: All public methods shall have XML documentation comments.

7. TECHNICAL ARCHITECTURE
7.1 Technology Stack
Platform: Windows Desktop Application
Framework: .NET 8 (C# 12)
UI Framework: WPF (Windows Presentation Foundation)
Architecture Pattern: MVVM (Model-View-ViewModel)
7.2 Key Libraries
Required NuGet Packages:

PacketDotNet (v1.4+) - Packet construction and parsing
SharpPcap (v6.2+) - Raw socket access for BootP (optional, may use raw sockets directly)
Microsoft.Xaml.Behaviors.Wpf - MVVM commanding and behaviors
Serilog (v3.1+) - Structured logging

Built-in Namespaces:

System.Net.Sockets - UDP/TCP networking
System.Net.NetworkInformation - NIC enumeration, ARP, ping
System.Security.Principal - Privilege detection

7.3 Project Structure
/EtherNetIPTool
  /Core
    /CIP - CIP protocol implementation
    /BootP - BootP/DHCP server
    /Network - NIC management, discovery
  /Models - Data structures (Device, Configuration)
  /ViewModels - MVVM view models
  /Views - WPF XAML views
  /Services - Logging, validation, helpers
  /Resources - Icons, help files, styles
7.4 Data Models
Device Model:

MAC Address (string)
IP Address (IPAddress)
Subnet Mask (IPAddress)
Gateway (IPAddress, nullable)
Vendor ID (ushort)
Vendor Name (string)
Device Type (ushort)
Product Code (ushort)
Product Name (string)
Serial Number (uint)
Status (enum: OK, LinkLocal, Conflict)
Last Seen (DateTime)

Configuration Model:

IP Address (IPAddress, required)
Subnet Mask (IPAddress, required)
Gateway (IPAddress, optional)
Hostname (string, optional, max 64)
DNS Server (IPAddress, optional)

BootP Request Model:

MAC Address (byte[6])
Transaction ID (uint)
Request Time (DateTime)
Flags (ushort)


8. AI DEVELOPMENT PHASES
Phase 1: Core Infrastructure

Main window shell with menu bar and toolbar
NIC enumeration and selection dropdown
Application settings management
Privilege detection on startup
Basic logging service

Phase 2: EtherNet/IP Discovery

CIP List Identity packet builder
UDP broadcast socket configuration
Response parser for device information
Device list data model and collection
Vendor ID to name mapping

Phase 3: Device Table UI

WPF DataGrid with custom columns
Device collection binding (MVVM)
Row selection handling
Sort functionality
Context menu implementation
Status column with color coding

Phase 4: Configuration Dialog (EtherNet/IP)

Dialog window layout and fields
IP octet input validation
Configuration data model
Confirmation dialog flow
Apply button enable/disable logic

Phase 5: CIP Configuration Protocol

Set_Attribute_Single message builder
Unconnected Send wrapper
Sequential attribute write logic
Error code handling and translation
Progress indication during writes

Phase 6: BootP/DHCP Server

BootP request listener (UDP 68)
Packet parser for BOOTREQUEST
BootP configuration dialog
BOOTREPLY packet builder
DHCP disable CIP command

Phase 7: Auto-Browse and Scanning

Background timer for periodic scans
Auto-browse checkbox and interval control
Device list update/removal logic
Manual scan button
ARP table lookup for MAC addresses

Phase 8: Logging and Help System

Log viewer window
Category filtering
Log export to text file
Embedded HTML help viewer
Tooltip and status bar help text
Documentation content authoring

Phase 9: Testing and Polish

Error handling robustness
Timeout enforcement
Memory leak testing
UI responsiveness tuning
Edge case handling
Installer package creation


9. TESTING REQUIREMENTS
9.1 Unit Testing
Coverage Areas:

CIP packet construction and parsing
BootP packet construction and parsing
IP address validation logic
Vendor ID mapping
Configuration data model validation
Privilege detection logic

Target: 70% code coverage for Core namespace classes.
9.2 Integration Testing
Test Scenarios:

NIC selection triggers discovery scan
Discovery scan populates device table correctly
Device configuration writes all attributes in sequence
BootP request triggers dialog and sends reply
Mode switching enables/disables appropriate controls
Log entries created for all operations

9.3 Device Compatibility Testing
Required Test Devices:

Allen-Bradley 1756-EN2T or EN3TR (ControlLogix)
Allen-Bradley 1734-AENT (POINT I/O)
Allen-Bradley Stratix switch (any model)
SICK sensor with EtherNet/IP (TDC-E or similar)
Banner DXM controller or sensor with EtherNet/IP
Pepperl+Fuchs device with EtherNet/IP

Test Cases per Device:

Discovery via List Identity
Read current configuration
Write new IP/subnet/gateway
Write hostname (if supported)
BootP commissioning from factory default
DHCP disable command

9.4 Network Condition Testing
Test Scenarios:

Multiple NICs installed (select correct one)
NIC cable disconnect during scan
Device powered off during configuration
IP conflict (duplicate IP on subnet)
Link-local IP device (169.254.x.x)
Subnet mismatch (device on different subnet)
Broadcast storm (many simultaneous responses)

9.5 Privilege Testing
Test Cases:

Run as standard user - EtherNet/IP mode functional, BootP disabled
Run as Administrator - Both modes functional
Attempt BootP binding without admin - graceful error and revert
Toggle modes as admin - smooth transitions

9.6 User Acceptance Testing
Workflow Tests:

Commissioning 10 devices from factory default via BootP
Reconfiguring 10 existing devices via EtherNet/IP mode
Troubleshooting device not appearing in scan
Resolving IP conflict scenario
Using help system to understand operation
Exporting device list and activity log

Success Criteria:

Complete commissioning workflow in single application window
70% reduction in tool-switching vs. traditional workflow
Controls engineer can operate tool effectively within 10 minutes of first use


10. DOCUMENTATION DELIVERABLES
10.1 User Manual (Embedded HTML)
Section 1: Getting Started

System requirements
Installation instructions
First launch and NIC selection
Understanding the interface

Section 2: EtherNet/IP Mode

How device discovery works
Auto-browse vs. manual scan
Reading device information
Configuring device IP settings
Understanding device status indicators
Troubleshooting discovery issues

Section 3: BootP/DHCP Mode

When to use BootP mode
Administrator privilege requirement
Configuring factory-default devices
Disabling DHCP mode permanently
Troubleshooting BootP issues

Section 4: Troubleshooting Guide

Device not appearing in scan
Configuration write fails
IP address conflict errors
BootP request not detected
Permission denied errors
Network adapter issues

Section 5: Technical Reference

CIP protocol overview
TCP/IP Interface Object attributes
BootP/DHCP packet structure
Vendor ID reference table
CIP status code reference
Timeout and retry behavior

Section 6: Glossary

CIP, EtherNet/IP, UCMM definitions
BootP, DHCP terminology
Industrial networking terms

10.2 Installation Guide

Windows version requirements
.NET 8 runtime installation (if needed)
MSI installer walkthrough
Administrator rights considerations
Firewall configuration (if needed)
Uninstallation procedure

10.3 Release Notes

Version number and date
Feature list
Known limitations
Supported devices
Bug fixes (future versions)

10.4 Code Documentation

XML comments on all public methods
Architecture overview document
Module dependency diagram
Protocol implementation notes


11. DEPLOYMENT
11.1 Installer Package
Format: MSI (Windows Installer)
Contents:

Application executable
Required DLLs
Help files (HTML documentation)
README.txt
LICENSE.txt

Installation Options:

Default install path: C:\Program Files\EtherNetIPTool
Optional desktop shortcut
Optional start menu entry
Check for .NET 8 runtime, prompt to install if missing

11.2 Portable Version
Format: ZIP archive
Contents:

Self-contained .NET 8 deployment (all dependencies included)
Application executable
Help files
README.txt

Usage:

Extract to any folder (USB drive compatible)
Run executable directly
No installation or admin rights required
Settings stored in local folder

11.3 System Requirements
Minimum:

Windows 10 version 1809 or Windows 11
.NET 8 Runtime (included in self-contained deployment)
100MB free disk space
4GB RAM
Ethernet network adapter

Recommended:

Windows 10 version 21H2 or Windows 11
Intel i5 or equivalent processor
8GB RAM
Gigabit Ethernet adapter


12. RISKS AND MITIGATIONS
12.1 Technical Risks
Risk: Device-specific CIP implementation variations cause configuration failures
Impact: High
Mitigation: Extensive device compatibility testing, implement retry logic, provide detailed error messages for troubleshooting
Risk: AI-generated code contains subtle networking bugs
Impact: High
Mitigation: Comprehensive unit and integration testing, packet capture validation, manual code review of critical protocol sections
Risk: Raw socket operations fail on some Windows configurations
Impact: Medium
Mitigation: Use standard .NET socket APIs where possible, detect and gracefully handle permission errors, provide clear error messages
12.2 Compatibility Risks
Risk: Vendor-specific deviations from EtherNet/IP standard
Impact: Medium
Mitigation: Test with real devices from target vendors, document known device-specific issues, provide workarounds in help system
Risk: Windows firewall blocks UDP broadcasts
Impact: Low
Mitigation: Detect blocked traffic, provide troubleshooting steps in help, suggest firewall exception configuration
12.3 Security Risks
Risk: Misconfiguration causes production network disruption
Impact: High
Mitigation: Require explicit confirmation before writes, provide clear preview of changes, recommend isolated commissioning network in documentation
Risk: BootP server responds to unintended devices
Impact: Medium
Mitigation: Operate on isolated network (documented best practice), show device MAC before configuration, require user confirmation
12.4 Usability Risks
Risk: Controls engineers unfamiliar with new tool workflow
Impact: Medium
Mitigation: Design UI to match familiar tools (RSLinx style), provide comprehensive help system, include quick-start guide

13. FUTURE ENHANCEMENTS (POST-MVP)
Phase 2 Potential Features
Device Management:

Session persistence (save/load device list)
Configuration templates for common device types
Bulk operations (configure multiple devices with pattern)
Device comparison tool (show config differences)
Import device list from CSV

Protocol Extensions:

Modbus TCP support
PROFINET DCP (Discovery and Configuration Protocol)
LLDP (Link Layer Discovery Protocol)
SNMP for switch statistics

Advanced Diagnostics:

Continuous ping monitor with graphing
Packet capture integration (Wireshark export)
Cable diagnostic tests
Port mirroring configuration

Configuration Management:

Backup/restore device configurations
Configuration diff/compare
Change history tracking
Configuration validation/audit

UI Enhancements:

Dark mode
Resizable window with adaptive layout
Multi-language support
Custom column visibility
Network topology visualization

Automation:

Scripting interface for batch operations
Command-line mode for integration
REST API for external tools
Automated commissioning workflows


APPENDICES
Appendix A: Vendor ID Reference
Vendor IDVendor Name0x0001Rockwell Automation / Allen-Bradley0x0002Omron0x0010Phoenix Contact0x0025Turck0x0083Pepperl+Fuchs0x0103SICK AG0x017FBanner Engineering0x01CFSchneider Electric0x0208Siemens0x028ABeckhoff
Appendix B: CIP Status Codes
CodeDescription0x00Success0x01Connection failure0x02Resource unavailable0x03Invalid parameter value0x04Path segment error0x05Path destination unknown0x06Partial transfer0x07Connection lost0x08Service not supported0x09Invalid attribute value0x0AAttribute list error0x0BAlready in requested mode/state0x0CObject state conflict0x0DObject already exists0x0EAttribute not settable0x0FPrivilege violation0x10Device state conflict0x11Reply data too large0x12Fragmentation of a primitive value0x13Not enough data0x14Attribute not supported0x15Too much data0x16Object does not exist0x17Service fragmentation sequence not in progress0x18No stored attribute data0x19Store operation failure0x1ARouting failure, request packet too large0x1BRouting failure, response packet too large0x1CMissing attribute list entry data0x1DInvalid attribute value list0x1EEmbedded service error0x1FVendor specific error0x20Invalid parameter0x21Write-once value or medium already written0x22Invalid Reply Received0x23Buffer Overflow0x24Invalid QoS Received0x25Invalid Safety Connection Size0x26Invalid Safety Connection Parameter0xFFGeneral Error
Appendix C: Troubleshooting Flowcharts
Device Not Discovered:

Verify NIC selected is on same subnet as device
Check device is powered on and Ethernet cable connected
Try manual "Scan Now" if auto-browse disabled
Check device has valid IP (not 0.0.0.0)
Verify firewall not blocking UDP 44818
Try ping device IP from Windows command line
Check device supports EtherNet/IP (not all industrial Ethernet is EIP)

Configuration Write Fails:

Verify device is still reachable (ping IP)
Check for IP conflict on subnet
Verify device is not locked or write-protected
Check timeout settings in preferences
Examine activity log for specific CIP error code
Verify gateway/DNS are on same subnet if provided
Try reading current config first to verify communication

BootP Request Not Received:

Verify application running as Administrator
Check BootP mode is selected (radio button)
Verify device is in DHCP mode (factory default or configured for DHCP)
Power cycle device to force new DHCP request
Check Windows Firewall not blocking UDP 67/68
Verify NIC selected is correct interface
Try disabling antivirus temporarily

Appendix D: Glossary
CIP (Common Industrial Protocol): Application-layer protocol used by EtherNet/IP, DeviceNet, and ControlNet for industrial device communication.
EtherNet/IP: Industrial Ethernet protocol using CIP over standard TCP/IP and Ethernet networks, developed by Rockwell Automation (ODVA standard).
UCMM (Unconnected Message Manager): CIP service allowing one-shot messages without establishing explicit connection, used for configuration and diagnostics.
BootP (Bootstrap Protocol): Network protocol used to automatically assign IP addresses to network devices, predecessor to DHCP.
DHCP (Dynamic Host Configuration Protocol): Network protocol that automatically assigns IP addresses and configuration to devices, commonly used for factory-default industrial devices.
List Identity: CIP service (command 0x0063) that broadcasts request for all EtherNet/IP devices to identify themselves, used for device discovery.
Set_Attribute_Single: CIP service (0x10) that writes a single attribute value to an object instance, used for device configuration.
TCP/IP Interface Object: CIP object class (0xF5) that contains network configuration attributes (IP, subnet, gateway, hostname, DNS).
Link-Local Address: Self-assigned IP address in 169.254.0.0/16 range used when DHCP fails and no static IP configured, indicates device needs configuration.
Vendor ID: 16-bit identifier assigned by ODVA to each manufacturer, used to identify device manufacturer in CIP communications.
MAC Address: Media Access Control address, unique 48-bit identifier assigned to network interface hardware.
Subnet Mask: 32-bit value defining network and host portions of an IP address, typically 255.255.255.0 for /24 networks.
Gateway: Router IP address for sending traffic outside local subnet, optional for isolated commissioning networks.

END OF PRODUCT REQUIREMENTS DOCUMENT