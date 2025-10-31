# EtherNet/IP Commissioning Tool - Troubleshooting Guide

## No Devices Found During Scan

### What Types of Devices Should Be Discovered?

The tool discovers devices that support **EtherNet/IP (CIP over Ethernet)** protocol:

**✅ Will be discovered:**
- Allen-Bradley PLCs (ControlLogix, CompactLogix, MicroLogix with Ethernet)
- Allen-Bradley Stratix managed switches
- Allen-Bradley PowerFlex drives with EtherNet/IP
- Allen-Bradley POINT I/O adapters (1734-AENT, etc.)
- Turck IO-Link Masters (TBIP series) and field devices
- SICK industrial sensors with EtherNet/IP
- Banner Engineering sensors/indicators with EtherNet/IP
- Pepperl+Fuchs field devices with EtherNet/IP
- Any device implementing ODVA EtherNet/IP standard

**❌ Will NOT be discovered:**
- Standard IT equipment (computers, laptops, printers)
- Non-industrial network switches/routers
- Devices with only Modbus TCP, PROFINET, or OPC UA
- Devices with EtherNet/IP disabled or not configured
- Web cameras, IoT devices, general network equipment

### Common Issues and Solutions

#### 1. Windows Firewall Blocking UDP Port 44818 ⚠️ MOST COMMON ISSUE

**Symptom:** No responses received, log shows "Sent List Identity broadcast to 255.255.255.255:44818" but 0 responses.

**Solution - Automatic (Recommended):**

Run the included PowerShell script as Administrator:

```powershell
# Navigate to the scripts folder
cd scripts

# Run the firewall configuration script
.\Configure-FirewallForEtherNetIP.ps1
```

This script will:
- Create inbound rule for UDP port 44818 (receive device responses)
- Create outbound rule for UDP port 44818 (send broadcasts)
- Verify the rules were created successfully

**Solution - Manual:**
```powershell
# Run PowerShell as Administrator
# Allow inbound UDP port 44818
New-NetFirewallRule -DisplayName "EtherNet/IP Discovery - Inbound" -Direction Inbound -Protocol UDP -LocalPort 44818 -Action Allow

# Allow outbound UDP broadcasts
New-NetFirewallRule -DisplayName "EtherNet/IP Discovery - Outbound" -Direction Outbound -Protocol UDP -RemotePort 44818 -Action Allow
```

**Alternative for Testing:** Temporarily disable Windows Firewall to test:
- Settings → Windows Security → Firewall & network protection → Turn off
- **Important:** Re-enable after testing!

#### 2. Wrong Network Adapter Selected

**Symptom:** Scanning on adapter that's not connected to industrial devices.

**Solution:**
- Verify you selected the correct adapter in the dropdown
- Check adapter IP is on same subnet as your devices
- Example: If device is 192.168.1.100, adapter should be 192.168.1.x

**Check in log file:**
```
Starting device scan on Ethernet - 192.168.1.50
```

#### 3. Device Doesn't Support EtherNet/IP

**Symptom:** Device responds to ping but not to EtherNet/IP discovery.

**Verification:**
```powershell
# Test if device responds on port 44818
Test-NetConnection -ComputerName 192.168.1.100 -Port 44818
```

**Check device documentation:**
- Does it list EtherNet/IP as a supported protocol?
- Is EtherNet/IP enabled in device configuration?
- Does it require session-based connection first?

#### 4. Subnet Mismatch

**Symptom:** Device is on different subnet than selected adapter.

**Solution:**
- EtherNet/IP List Identity uses broadcast (255.255.255.255)
- Broadcast doesn't cross subnet boundaries
- Ensure device and PC are on same Layer 2 network segment
- Check subnet mask matches (e.g., both /24 or 255.255.255.0)

**Example Problem:**
```
PC:     192.168.1.50/24
Device: 192.168.2.100/24  ← Different subnet!
```

**Example Solution:**
```
PC:     192.168.1.50/24
Device: 192.168.1.100/24  ← Same subnet ✓
```

#### 5. Device Not Responding to Broadcasts

**Possible causes:**
- Device powered off or disconnected
- Device in firmware update mode
- Device EtherNet/IP interface disabled
- Network switch blocking broadcasts (rare)
- Device has IP 0.0.0.0 (not configured)

**Solution:**
- Verify device has valid static IP or DHCP lease
- Check device status LEDs (should show network activity)
- Try BootP/DHCP mode if device is factory default (Phase 6)

### Diagnostic Steps

#### Step 1: Check the Log File

Log location: `%LocalAppData%\EtherNetIPTool\Logs\app-[date].log`

**Look for:**
```
[SCAN] Starting device scan on Ethernet - 192.168.1.50
[CIP] Built List Identity request packet (24 bytes)
[CIP] Packet hex: 63-00-00-00-00-00-00-00-00-00-00-00...
[SCAN] Sent List Identity broadcast to 255.255.255.255:44818
[SCAN] Listening for responses for 3 seconds...
[SCAN] Received 0 response(s)
[WARN] No devices responded to List Identity broadcast
```

#### Step 2: Test with Known Device

If you have an Allen-Bradley device:
1. Connect directly with Ethernet cable (no switch)
2. Set PC to static IP: 192.168.1.50 / 255.255.255.0
3. Set device to: 192.168.1.100 / 255.255.255.0
4. Verify ping: `ping 192.168.1.100`
5. Try scan again

#### Step 3: Verify Broadcast is Sent

Use Wireshark to capture:
1. Install Wireshark
2. Start capture on your network adapter
3. Filter: `udp.port == 44818`
4. Click "Scan Now" in application
5. You should see:
   - Outgoing broadcast to 255.255.255.255:44818
   - Incoming responses from device IPs

**Expected packet:**
```
Source: 192.168.1.50 (your PC)
Destination: 255.255.255.255 (broadcast)
Protocol: UDP
Port: 44818
Data: 24 bytes (CIP List Identity request)
```

#### Step 4: Check Vendor-Specific Requirements

**Allen-Bradley:**
- Most devices respond to List Identity immediately
- No special configuration needed
- Ensure device is not in "Hard Faulted" state

**SICK Sensors:**
- May need "EtherNet/IP Interface" enabled in configuration
- Check sensor display for network status

**Pepperl+Fuchs:**
- Verify EtherNet/IP mode is selected (not PROFINET)
- Check DIP switches or web interface

**Turck:**
- TBIP series IO-Link Masters should respond by default
- Ensure device has valid IP address (not 0.0.0.0)
- Some Turck devices may send responses specifically to port 44818
- Check log for "Successfully bound to port 44818" message

## Understanding the Discovery Process

### What Happens During Scan

1. **Build Packet** (24 bytes)
   ```
   Command: 0x0063 (List Identity)
   Length: 0x0000 (no data)
   Session: 0x00000000
   Status: 0x00000000
   Context: 8 random bytes
   Options: 0x00000000
   ```

2. **Send Broadcast**
   - Destination: 255.255.255.255:44818
   - Source: Selected adapter IP:44818 (or ephemeral port if 44818 in use)
   - Protocol: UDP
   - Note: Application attempts to bind to port 44818 for compatibility with devices that send responses there

3. **Wait for Responses** (3 seconds)
   - Devices respond with their identity
   - Each response contains:
     - IP address
     - Vendor ID & name
     - Device type & product code
     - Product name
     - Serial number
     - Firmware version

4. **ARP Lookup**
   - Pings each device
   - Resolves MAC address via Windows SendARP
   - MAC used for duplicate detection

5. **Display Results**
   - Devices appear in table
   - Duplicates (same MAC) updated in place

### Expected Response Format

A valid response will be ~60-100 bytes:
```
Header (24 bytes):
  Command: 0x0063
  Status: 0x0000 (success)

CPF Items:
  Item Count: 1+
  Type: 0x000C (Identity Response)
  Length: varies

Identity Data:
  Protocol Version
  Socket Address (IP:PORT)
  Vendor ID
  Device Type
  Product Code
  Revision
  Serial Number
  Product Name (length-prefixed string)
```

## Still Having Issues?

### Enable Verbose Logging

Add to log viewer (Phase 8) or check file directly:
- All [CIP] messages show packet hex dumps
- [SCAN] messages show discovery progress
- [WARN] messages highlight potential issues

### Test with Alternative Tool

Verify device responds to other tools:
- **RSLinx Classic**: Browse for EtherNet/IP devices
- **Studio 5000**: "Who Active" in controller organizer
- **KEPServerEX**: EtherNet/IP driver discovery

If device appears in these tools but not this tool, that's a bug - please report it.

### Network Isolation Test

Create isolated test network:
1. Use direct Ethernet cable (PC ↔ Device)
2. Set static IPs on same subnet
3. Disable all firewalls
4. Test discovery
5. If works: problem is network infrastructure
6. If doesn't work: check device EtherNet/IP configuration

## Additional Resources

- **ODVA EtherNet/IP Specification**: https://www.odva.org
- **CIP Networks Library Vol 2**: EtherNet/IP Adaptation document
- **Allen-Bradley EtherNet/IP Selection Guide**: Publication ENET-SG001
- **Device manuals**: Check vendor documentation for EtherNet/IP setup

---

**Remember:** The tool only discovers devices that:
1. Support EtherNet/IP (CIP over Ethernet)
2. Are on the same subnet
3. Have EtherNet/IP enabled and configured
4. Can receive UDP broadcasts on port 44818
