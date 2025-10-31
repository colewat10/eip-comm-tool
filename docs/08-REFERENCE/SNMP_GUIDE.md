1. PRODUCT CONTEXT AND USE CASE
1.1 Industrial Reference Tools
Similar existing tools this feature should emulate:

Stratix Switch Web Interface: Port statistics page showing real-time counters
Studio 5000 Logix Designer: Module diagnostics tab with I/O statistics
FactoryTalk Linx: Device diagnostics with port health indicators
PROFINET IO-Check: Network diagnostics tool showing port quality metrics
Wireshark Statistics > Endpoints: Interface traffic summary view
Cisco IOS "show interfaces" command: Text-based port statistics output

1.2 Target Industrial Devices

Allen-Bradley Stratix industrial switches (5700, 5800, 8000 series)
Allen-Bradley ControlLogix/CompactLogix processors with embedded switch
Allen-Bradley POINT I/O EtherNet/IP adapters
SICK industrial sensors with EtherNet/IP (TDC-E, CLV)
Banner wireless controllers (DXM series)
Pepperl+Fuchs IO-Link masters with EtherNet/IP

1.3 Primary User Goals

Quickly identify which switch ports have link up/down
Detect physical layer problems (errors, collisions, FCS errors)
Monitor port utilization and traffic patterns
Troubleshoot intermittent connectivity issues
Verify cable quality and duplex mismatches
Document network health for maintenance records


2. FEATURE INTEGRATION INTO EXISTING APPLICATION
2.1 Entry Points to Port Statistics Feature
From Device Table (Main Window):

Right-click context menu item: "View Port Statistics"
Only enabled when device is selected and status is "OK" (not "Link-Local" or "Conflict")
Tooltip: "View SNMP interface and media statistics for this device"

From Tools Menu:

Menu item: "Tools > Port Statistics"
Opens dialog to manually enter device IP address
Use case: Quick diagnostic of device not in discovery list

From Configuration Dialog:

Add button: "View Port Stats" next to device information section
Allows checking port health before/after configuration changes

2.2 Window Behavior

Opens as non-modal window (allows multiple port stats windows open simultaneously)
Window remembers last position and size per user session
Can have multiple windows open for different devices (side-by-side comparison)
Window title shows: "Port Statistics - [Device IP] - [Device Name if available]"


3. SNMP PROTOCOL IMPLEMENTATION REQUIREMENTS
3.1 Library Selection

Use: Lextm.SharpSnmpLib (MIT license, actively maintained, comprehensive)
Install via NuGet package manager
Version 12.5.2 or later
No other SNMP libraries needed

3.2 SNMP Protocol Configuration

Version: SNMPv2c only (SNMPv1 and SNMPv3 not required for MVP)
Port: UDP 161 (standard SNMP agent port)
Community String: Default "public" (read-only), user-configurable in preferences
Timeout: 3 seconds per query
Retries: 2 attempts on timeout
Operations: GET, GET-NEXT, GET-BULK (no SET operations)

3.3 Required OID Categories
Query only these specific MIB objects:
A. Interface Table (RFC 1213 MIB-II):

ifNumber: Total number of interfaces
ifDescr: Interface description/name
ifType: Interface type (6=Ethernet, 117=Gigabit Ethernet)
ifSpeed: Link speed in bits/second
ifPhysAddress: MAC address
ifAdminStatus: Administrative status (1=up, 2=down)
ifOperStatus: Operational status (1=up, 2=down)
ifMtu: Maximum transmission unit size
ifInOctets: Total bytes received (32-bit counter)
ifOutOctets: Total bytes transmitted (32-bit counter)
ifInUcastPkts: Unicast packets received
ifOutUcastPkts: Unicast packets transmitted
ifInErrors: Total input errors
ifOutErrors: Total output errors
ifInDiscards: Input packets discarded (buffer full)
ifOutDiscards: Output packets discarded (buffer full)

B. Extended Interface Table (RFC 2863 IF-MIB):

ifName: Interface name (e.g., "GigabitEthernet1/1")
ifAlias: User-assigned description
ifHighSpeed: Link speed in megabits/second
ifHCInOctets: 64-bit byte counter received (prevents 32-bit wrap)
ifHCOutOctets: 64-bit byte counter transmitted
ifInMulticastPkts: Multicast packets received
ifInBroadcastPkts: Broadcast packets received
ifOutMulticastPkts: Multicast packets transmitted
ifOutBroadcastPkts: Broadcast packets transmitted
ifConnectorPresent: Physical connector present (true/false)

C. Ethernet Statistics Table (RFC 2665 EtherLike-MIB):

dot3StatsAlignmentErrors: Frame alignment errors
dot3StatsFCSErrors: Frame check sequence (CRC) errors
dot3StatsSingleCollisionFrames: Single collision count
dot3StatsMultipleCollisionFrames: Multiple collision count
dot3StatsLateCollisions: Late collision count
dot3StatsExcessiveCollisions: Excessive collision count
dot3StatsDeferredTransmissions: Deferred transmission count
dot3StatsCarrierSenseErrors: Carrier sense error count
dot3StatsFrameTooLongs: Oversized frame count
dot3StatsInternalMacTransmitErrors: Internal MAC transmit errors
dot3StatsInternalMacReceiveErrors: Internal MAC receive errors
dot3StatsSymbolErrors: Symbol error count
dot3StatsDuplexStatus: Duplex mode (1=unknown, 2=half, 3=full)

Do NOT query:

System information (sysDescr, sysUpTime, etc.)
IP routing tables
TCP/UDP connection tables
SNMP trap configuration
Vendor-specific enterprise MIBs (for MVP)


4. DATA RETRIEVAL STRATEGY
4.1 Discovery and Query Sequence
Step 1: Determine Interface Count

Query OID: ifNumber (1.3.6.1.2.1.2.1.0)
Returns: Integer count of network interfaces
If query fails or returns 0: Display error "Device does not support SNMP or SNMP is disabled"

Step 2: Retrieve Per-Interface Statistics

For each interface index (1 through ifNumber):

Build list of all OIDs with interface index appended (e.g., ifDescr.1, ifDescr.2, etc.)
Use GET-BULK operation to retrieve multiple OIDs in single request (efficient)
Fallback to individual GET operations if GET-BULK not supported



Step 3: Parse and Store Results

Convert raw OID values to appropriate data types (integers, strings, MAC addresses)
Handle missing OIDs gracefully (some devices don't support all counters)
Store in structured data model for UI binding

Step 4: Calculate Derived Values

Total packets = unicast + multicast + broadcast
Total errors = input errors + output errors + FCS errors + alignment errors
Error rate percentage = (total errors / total packets) × 100

4.2 Handling Device Variations
High-Capacity Counters:

Try 64-bit counters (ifHCInOctets) first
If not available, fall back to 32-bit counters (ifInOctets)
Flag when using 32-bit counters (risk of wrap-around on high-traffic ports)

Missing/Unsupported OIDs:

If media statistics (dot3Stats) not available: Display "N/A" in error columns
If extended interface table not available: Use basic interface table only
If MAC address not returned: Display "Unknown"

Interface Type Filtering:

Only display Ethernet-type interfaces (type codes 6, 62, 117)
Ignore loopback, management, and virtual interfaces
Allow user preference to "Show All Interfaces" vs. "Show Ethernet Only"


5. USER INTERFACE DESIGN
5.1 Window Layout (1200×700 pixels, resizable)
Top Toolbar (30px height):

Device IP address label (read-only)
"Refresh" button with circular arrow icon
Auto-refresh checkbox with interval spinner (1-60 seconds, default 5)
"Export CSV" button
"Clear Errors" button (explain: resets baseline for error rate calculations, doesn't clear device counters)

Device Summary Bar (50px height, gray background):
Display horizontally:

"Device: [IP or hostname]"
"Total Ports: [X]"
"Ports Up: [X]" (green text)
"Ports Down: [X]" (red text if >0)
"Last Updated: [timestamp]"

Main Port Table (scrollable, remaining space):
Dense DataGrid with columns:
ColumnWidthDescription#40pxPort number (interface index)Port Name150pxifName or ifDescr, truncate with ellipsisStatus60px"Up" (green) / "Down" (gray) / "Testing" (yellow)Link Speed80px"1 Gbps", "100 Mbps", "10 Mbps"Duplex60px"Full" / "Half" / "Auto"RX Bytes100pxFormatted with units (MB, GB, TB)TX Bytes100pxFormatted with unitsRX Packets100pxFormatted with thousands separatorTX Packets100pxFormatted with thousands separatorRX Errors80pxRed text if >0, "-" if 0TX Errors80pxRed text if >0, "-" if 0FCS Errors80pxRed text if >0, "-" if 0Collisions80pxYellow text if >0, "-" if 0Total Errors90pxSum of all error types, bold red if >100
Row Formatting Rules:

Operational status "Down": Gray out entire row, dim text color
Any error count >0: Highlight error cell(s) with light red background
Collision count >100: Highlight with light yellow background (indicates half-duplex or network congestion)
FCS errors >10: Highlight row with light red background (indicates cable/hardware problem)

Bottom Status Bar (20px height):

Left section: Status text ("Ready", "Refreshing...", "Error: [message]")
Right section: "Last refresh: [HH:MM:SS]"

5.2 Port Detail Dialog (Double-Click Behavior)
When user double-clicks a port row, open modal detail dialog showing:
Basic Information Section:

Port Number
Port Name
Description/Alias
MAC Address
Interface Type
MTU Size
Connector Present (Yes/No)

Link Status Section:

Administrative Status
Operational Status
Link Speed (Mbps)
Duplex Mode
Last Status Change (if available)

Traffic Counters Section (two columns):

Inbound: Bytes, Unicast, Multicast, Broadcast, Total Packets
Outbound: Bytes, Unicast, Multicast, Broadcast, Total Packets

Error Counters Section (two columns):

Input Errors: Total Errors, Discards, Unknown Protocols
Output Errors: Total Errors, Discards, Queue Length

Media/Ethernet Errors Section:

Alignment Errors
FCS/CRC Errors
Single Collisions
Multiple Collisions
Late Collisions
Excessive Collisions
Deferred Transmissions
Carrier Sense Errors
Frame Too Long
Symbol Errors
Internal MAC TX/RX Errors

Dialog Buttons:

"Refresh" - Update counters for this port only
"Export Details" - Save to text file
"Close"

5.3 Right-Click Context Menu on Port Row

"View Details" - Opens detail dialog
"Copy Port Name"
"Copy MAC Address"
"Reset Error Baseline" - Marks current counters as baseline for rate calculations
Separator
"Ping Port IP" - Only enabled if port has associated IP address


6. DATA DISPLAY FORMATTING
6.1 Number Formatting Rules
Byte Counters:

Less than 1 KB: Display as "X bytes"
1 KB - 1 MB: Display as "X.XX KB"
1 MB - 1 GB: Display as "X.XX MB"
1 GB - 1 TB: Display as "X.XX GB"
1 TB+: Display as "X.XX TB"

Packet Counters:

Use thousands separator: "1,234,567"
Millions: Display as "X.XX M" (e.g., "12.34 M")
Billions: Display as "X.XX B"

Link Speed:

Display as "10 Mbps", "100 Mbps", "1 Gbps", "10 Gbps"
If speed unknown or 0: Display as "Unknown"

Error Counters:

Display exact count with thousands separator
If zero: Display as "-" (cleaner than "0")
If count exceeds display width: Use "999K+", "1M+" notation

Timestamps:

Display as "HH:MM:SS" (24-hour format)
Include seconds for precision

6.2 Status Indicators
Operational Status Colors:

Up: Green text, green dot icon
Down: Gray text, gray dot icon
Testing: Yellow text, yellow dot icon

Error Level Indicators:

0 errors: Display "-" in gray
1-10 errors: Display count in black
11-100 errors: Display count in orange with warning icon
100+ errors: Display count in red with error icon, bold text

Port Health Summary (Optional Enhancement):

Calculate overall port health score (0-100%) based on:

Operational status (up=100%, down=0%)
Error rate (<0.01%=100%, >1%=0%)
Traffic balance (RX/TX ratio close to 1:1 = healthy)


Display as colored bar or percentage in dedicated column


7. AUTO-REFRESH AND REAL-TIME UPDATES
7.1 Auto-Refresh Implementation
Behavior:

Checkbox enabled by default on window open
Interval spinner: 1-60 seconds, default 5 seconds
Timer starts automatically if checkbox enabled
Changing interval restarts timer with new value
Unchecking stops timer immediately

Refresh Process:

Disable refresh button and show "Refreshing..." in status bar
Query all ports in background thread (non-blocking)
Update table rows as responses received (incremental update, not clear-and-rebuild)
Re-enable refresh button when complete
Display "Last refresh: HH:MM:SS" in status bar

Error Handling During Auto-Refresh:

If single query times out: Mark that port as "Unknown" but continue with others
If device becomes unreachable: Display error in status bar, pause auto-refresh, prompt user
If multiple consecutive failures: Show dialog asking to disable auto-refresh or close window

7.2 Rate Calculation (Optional Enhancement)
Track deltas between refreshes to calculate:

Bytes per second inbound/outbound
Packets per second inbound/outbound
Error rate per second
Link utilization percentage (actual throughput / link capacity)

Display in additional columns:

"RX Rate" - MB/s or Mbps
"TX Rate" - MB/s or Mbps
"Utilization" - Percentage with colored bar (green <50%, yellow 50-80%, red >80%)

Requirements:

Store previous counters and timestamp
Calculate delta: (current_value - previous_value) / time_elapsed
Handle counter wrap-around for 32-bit counters
Display "N/A" until second refresh completes


8. EXPORT AND REPORTING
8.1 CSV Export Format
File naming: PortStats_[DeviceIP]_[YYYYMMDD_HHMMSS].csv
CSV Structure:

Header row: Column names matching table display
Data rows: One per port, all columns
Include metadata rows at top:

Device IP
Device Name (if available)
Timestamp of export
Total ports, ports up, ports down
Blank line separator before column headers



Export Button Behavior:

Opens standard Windows SaveFileDialog
Default location: User's Documents folder
Filter: CSV Files (*.csv)
On save: Display brief success message in status bar ("Exported to [filename]")

8.2 Port Detail Export (from detail dialog)
File naming: PortDetail_[DeviceIP]_Port[X]_[YYYYMMDD_HHMMSS].txt
Text Format:
PORT STATISTICS DETAIL REPORT
Device: [IP]
Port: [X] - [Name]
Generated: [Timestamp]
================================

BASIC INFORMATION
  Port Number: X
  Port Name: [name]
  Description: [description]
  MAC Address: [MAC]
  ...

LINK STATUS
  Operational Status: Up
  Link Speed: 1 Gbps
  ...

TRAFFIC COUNTERS
  Inbound:
    Bytes: 1,234,567,890 (1.15 GB)
    Unicast Packets: 9,876,543
    ...
  Outbound:
    ...

ERROR COUNTERS
  ...

MEDIA STATISTICS
  ...

9. ERROR HANDLING AND USER MESSAGING
9.1 Connection Errors
Scenario: Device not responding to SNMP

Message: "Device [IP] did not respond to SNMP queries. Possible causes: SNMP disabled on device, incorrect community string, firewall blocking UDP port 161, device unreachable."
Action buttons: "Retry", "Change Community String", "Close"
Log to activity log: "SNMP timeout connecting to [IP]"

Scenario: Wrong community string

Message: "SNMP authentication failed. The community string may be incorrect."
Prompt for new community string
Action buttons: "Try Again", "Cancel"

Scenario: SNMP not supported

Message: "Device [IP] does not appear to support SNMP or interface statistics are not available via SNMP."
Suggestion: "Verify device supports SNMP and it is enabled in device configuration."
Action button: "Close"

9.2 Data Quality Warnings
Scenario: Using 32-bit counters on high-speed interface

Display warning icon in status bar
Tooltip: "This device uses 32-bit counters which may wrap around quickly on Gigabit interfaces. Statistics may be inaccurate for high-traffic ports."
Suggest: "Consider querying more frequently (1-2 second intervals) to detect wraps."

Scenario: Many missing OIDs

If >30% of expected OIDs return no data:
Display warning: "Device returned incomplete statistics. Some counters are not available or not supported."
Show which specific counters are missing in detail dialog

Scenario: Stale/unchanging counters

If counters don't change across multiple refreshes on "Up" ports:
Display info icon: "Warning: Counters are not changing. Device may not be updating statistics or port has no traffic."

9.3 Validation and Constraints
Before opening port statistics window:

Verify device IP is valid and reachable (ping test)
If ping fails: Warn user but allow proceeding (device may block ICMP)

Community string validation:

Allow alphanumeric characters, hyphens, underscores only
Max length: 32 characters
No spaces allowed
Warn if using default "public" (security concern)

Refresh interval constraints:

Minimum: 1 second (prevent device overload)
Maximum: 60 seconds
Display warning if <3 seconds: "Frequent polling may impact device performance"


10. PREFERENCES AND CONFIGURATION
10.1 Add SNMP Section to Preferences Dialog
Under "Tools > Preferences", add "SNMP" tab with settings:
Connection Settings:

Default Community String: Text box, default "public"
Query Timeout: Spinner, 1-10 seconds, default 3
Retry Count: Spinner, 0-5, default 2
SNMP Port: Text box, default 161 (allow custom for non-standard configs)

Display Settings:

Show Only Ethernet Interfaces: Checkbox, default checked
Use High-Capacity Counters When Available: Checkbox, default checked
Highlight Errors: Checkbox, default checked
Error Highlight Threshold: Spinner, 1-1000, default 10

Auto-Refresh Settings:

Enable Auto-Refresh by Default: Checkbox, default checked
Default Refresh Interval: Spinner, 1-60 seconds, default 5
Pause Auto-Refresh on Focus Loss: Checkbox, default unchecked

CSV Export Settings:

Default Export Location: Folder picker, default Documents
Include Metadata in CSV: Checkbox, default checked

Advanced:

Log All SNMP Queries: Checkbox, default unchecked (for debugging)
Max Concurrent SNMP Queries: Spinner, 1-10, default 5 (for batch operations)

10.2 Per-Device Settings (Optional)
Store in application settings:

Last used community string per device IP
Last used refresh interval per device
Window position/size per device
Custom port descriptions/aliases entered by user


11. ACTIVITY LOGGING
11.1 Log Events to Capture
Add new log categories:

SNMP: All SNMP operations (query sent, response received, timeout, error)

Log entry examples:

12:34:56.789 | SNMP | Query sent to 192.168.1.100: ifNumber
12:34:56.891 | SNMP | Response from 192.168.1.100: ifNumber = 24
12:34:59.892 | SNMP | Timeout querying 192.168.1.100 for ifDescr.1
12:35:01.234 | SNMP | Retrieved statistics for 24 ports from 192.168.1.100
12:35:01.235 | SNMP | Port GigabitEthernet1/5 has 1,234 FCS errors (exceeds threshold)

11.2 Activity Log Viewer Updates
Filter category dropdown: Add "SNMP" option
Log detail view: For SNMP entries, show:

Device IP
OID queried
Response value
Response time (milliseconds)
Error code (if failed)


12. HELP SYSTEM ADDITIONS
12.1 User Manual New Section
Add section: "Port Statistics and SNMP Monitoring"
Topics to cover:

What are port statistics and why they matter
How to open port statistics window
Understanding the statistics table columns
Interpreting error counters and what they mean
Auto-refresh feature explanation
Exporting statistics for documentation
Troubleshooting common SNMP connection issues
Configuring SNMP on Allen-Bradley Stratix switches
Understanding duplex mismatches and collision errors
When to suspect cable problems vs. device problems

12.2 Context-Sensitive Help (F1 Key)
When port statistics window has focus:

F1 opens help to "Port Statistics" section
F1 on detail dialog opens "Understanding Error Counters" subsection

12.3 Tooltips on All UI Elements
Examples:

Refresh button: "Manually refresh all port statistics from device"
Auto-refresh checkbox: "Automatically query device at specified interval"
FCS Errors column header: "Frame Check Sequence errors indicate bad frames received, often caused by cable problems or electromagnetic interference"
Collisions column: "High collision counts indicate network congestion or duplex mismatch"
Status "Down": "Port is administratively or operationally down - check cable connection and device configuration"


13. TESTING REQUIREMENTS
13.1 Device Compatibility Testing Matrix
Test with these specific devices (minimum):

Allen-Bradley Stratix 5700 (8-port model)
Allen-Bradley Stratix 5800 (24-port model)
Allen-Bradley 1756-EN2T ControlLogix module
SICK TDC-E sensor with EtherNet/IP
Any switch/device that responds to standard IF-MIB queries

For each device, verify:

Correct interface count detected
All "Up" ports displayed with accurate statistics
"Down" ports displayed correctly
Error counters populated (if device supports)
Export functions work
Auto-refresh works without errors for 5 minutes minimum

13.2 Functional Test Scenarios

Device with no SNMP support: Display appropriate error message
Device with wrong community string: Prompt for correct string
Device disconnected during refresh: Handle gracefully, don't crash
Mix of Gigabit and Fast Ethernet ports: Display speeds correctly
Port with high error count: Verify highlighting and warnings display
32-bit counter wrap-around: Detect and handle (if calculating rates)
Missing/unsupported OIDs: Display "N/A" appropriately
Multiple windows open simultaneously: No conflicts or shared state issues

13.3 Performance Requirements

Initial query of 24-port switch: Complete within 5 seconds
Auto-refresh cycle: Complete within interval time (don't stack up requests)
UI remains responsive during query: No freezing or lag
Memory usage: No leaks during extended auto-refresh sessions
Window open/close: No memory leaks, properly cleanup SNMP connections


14. TROUBLESHOOTING GUIDE (FOR HELP SYSTEM)
14.1 "Port Statistics Window Shows No Ports"
Possible Causes:

Device does not support SNMP
SNMP is disabled on device
Firewall blocking UDP port 161
Wrong community string

Solutions:

Verify device supports SNMP (check device manual)
Enable SNMP in device web interface or configuration tool
Check Windows Firewall settings, allow outbound UDP 161
Try community string "private" or consult device documentation
Use device's web interface to verify SNMP is enabled and working

14.2 "High Error Counts on Port"
FCS/CRC Errors:

Indicates: Bad cable, damaged connector, EMI interference
Solutions: Replace cable, check for sharp bends, test with known-good cable, move away from power lines/motors

Alignment Errors:

Indicates: Duplex mismatch (one side full, other half)
Solutions: Check duplex settings on both ends, set to auto-negotiate or manually match

Collisions (Half-Duplex):

Indicates: Network congestion or hub/repeater in path
Solutions: Upgrade to full-duplex, replace hubs with switches, reduce traffic

Late Collisions:

Indicates: Cable too long (>100m) or duplex mismatch
Solutions: Check cable length, verify duplex settings, replace cable

Symbol Errors:

Indicates: Signal quality issues, transceiver problem
Solutions: Check SFP/transceiver seating, replace transceiver, check fiber cleanliness (if fiber)

14.3 "Counters Not Changing"
Possible Causes:

Port has no active traffic
Device not updating counters (firmware bug)
Counter wrap-around not detected (32-bit counters)

Solutions:

Generate traffic on port (ping test)
Check device firmware version, update if available
Reduce refresh interval to detect 32-bit wrap-arounds


15. IMPLEMENTATION PRIORITY AND PHASING
15.1 Phase 1: Core Functionality (MVP)
Must-have for initial release:

SNMP query engine (GET, GET-NEXT, GET-BULK operations)
Query interface count and basic interface statistics
Display port table with: name, status, speed, duplex, RX/TX bytes, RX/TX packets, total errors
Manual refresh button
Export to CSV
Error handling for unreachable devices

15.2 Phase 2: Enhanced Diagnostics
Add after MVP proven:

Media/Ethernet error counters (FCS, collisions, alignment, etc.)
Port detail dialog with all counters
Auto-refresh with configurable interval
Error highlighting and health indicators
Preferences for SNMP settings

15.3 Phase 3: Advanced Features
Future enhancements:

Rate calculations (bytes/sec, packets/sec, utilization %)
Historical trending with charts
Baseline comparison (store initial counters, show deltas)
Batch query multiple devices
Alert thresholds with notifications
Integration with activity log for automatic error event capture


16. KEY DESIGN PRINCIPLES
16.1 Industrial Tool Standards

Dense information display: Maximize data visible without scrolling
No unnecessary whitespace: Professional, technical aesthetic
Monospace fonts for numbers: Easier to compare values vertically
Muted color palette: Gray backgrounds, subtle highlights, reserve bright colors for errors
Instant responsiveness: No delays clicking buttons or selecting rows

16.2 User Experience Priorities

Speed: Show critical information (port up/down, errors) immediately
Clarity: Use plain language, avoid acronyms without explanation
Actionability: Every error should have suggested solution
Non-disruptive: Auto-refresh should not jump scroll position or lose selection
Familiarity: Match conventions from Stratix web interface and Studio 5000

16.3 Reliability Requirements

Never crash: Handle all SNMP errors gracefully
Fail informatively: Tell user exactly what went wrong and how to fix
Degrade gracefully: If some OIDs unavailable, show what's available
Predictable: Same device should always return same results
Logged: All failures logged to activity log for post-analysis


SUMMARY CHECKLIST
Before marking this feature complete, verify:

 []Port statistics window accessible from device table context menu
 []SNMP queries working against Stratix switches and ControlLogix modules
 []All 15+ interface statistics displayed correctly
 []All 13+ media error counters displayed correctly
 []Status indicators (up/down, error highlighting) working
 []Manual refresh button functional
 []Auto-refresh with configurable interval working
 []Export to CSV functional with proper formatting
 []Port detail dialog showing all counters
 []Error handling for all timeout/unreachable scenarios
 []SNMP preferences in settings dialog
 []Help documentation section complete
 []Activity log captures SNMP events
 []Tested with minimum 3 different industrial device models
 []No memory leaks during extended auto-refresh
 []Window position/size remembered between sessions