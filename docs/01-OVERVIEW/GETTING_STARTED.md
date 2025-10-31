# Getting Started - EtherNet/IP Commissioning Tool

**Document Version:** 1.0
**Last Updated:** 2025-10-31
**Estimated Reading Time:** 10 minutes

---

## Quick Start in 5 Minutes

### Prerequisites Checklist

Before launching the tool:

- [ ] **Windows 10 (1809+) or Windows 11** installed
- [ ] **.NET 8 Runtime** installed ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- [ ] **Ethernet connection** to industrial network
- [ ] **Windows Firewall configured** for UDP port 44818
- [ ] **Administrator rights** (only for BootP/DHCP mode)

---

## Step 1: Configure Windows Firewall

**⚠️ CRITICAL**: This step is required for device discovery to work.

### Option A: Run PowerShell Script (Recommended)

1. Open PowerShell **as Administrator**
2. Navigate to the tool's `scripts` folder
3. Run the configuration script:

```powershell
cd path\to\eip-comm-tool\scripts
.\Configure-FirewallForEtherNetIP.ps1
```

4. Verify success message appears

### Option B: Manual Configuration

1. Open **Windows Defender Firewall with Advanced Security**
2. Create **Inbound Rule**:
   - Rule Type: Port
   - Protocol: UDP
   - Port: 44818
   - Action: Allow
   - Profile: All
   - Name: "EtherNet/IP Inbound (UDP 44818)"

3. Create **Outbound Rule**:
   - Same settings as above
   - Name: "EtherNet/IP Outbound (UDP 44818)"

**Without these rules, no devices will be discovered!**

---

## Step 2: Launch the Application

### First Launch

1. Navigate to application folder
2. Double-click `EtherNetIPTool.exe`
3. Wait 2-3 seconds for startup
4. Application window appears (1280×768 pixels)

### Verify Startup

You should see:
- ✅ Menu bar (File, Edit, Tools, View, Help)
- ✅ Network adapter dropdown populated
- ✅ Mode selector showing "EtherNet/IP" (default)
- ✅ Empty device table
- ✅ Status bar showing "Ready"

---

## Step 3: Select Network Adapter

### Choosing the Correct Adapter

1. Look at **NIC dropdown** in toolbar
2. Application auto-selects first available adapter
3. **Verify** this is the adapter connected to your industrial network
4. If incorrect, **click dropdown** and select correct adapter
5. See current IP and subnet mask displayed next to dropdown

### How to Tell Which Adapter

- **Adapter Name**: Look for names like "Intel Ethernet", "Realtek PCIe", etc.
- **IP Address**: Should be on same subnet as your devices (e.g., 192.168.1.x)
- **Subnet Mask**: Typically 255.255.255.0 for /24 networks

**Tip**: If unsure, check Windows Network Connections (Control Panel → Network and Internet → Network Connections)

---

## Step 4: Discover Devices

### Quick Discovery

1. Ensure **"Auto-Browse: Enabled"** checkbox is checked (default)
2. Watch device table - devices appear within 5 seconds
3. Devices update automatically every 5 seconds

**OR**

1. Click **"Scan Now"** button for immediate one-time scan
2. Wait 3 seconds for results
3. Devices populate the table

### What You'll See

Device table shows:
- **#**: Row number
- **MAC Address**: Hardware address (00:00:BC:xx:xx:xx)
- **IP Address**: Current IP (may be 169.254.x.x if unconfigured)
- **Subnet Mask**: Current subnet
- **Vendor**: Manufacturer name (Allen-Bradley, SICK, etc.)
- **Model**: Product name/number
- **Status**: OK | Link-Local | Conflict

### Status Meanings

| Status | Color | Meaning |
|--------|-------|---------|
| **OK** | Black | Device properly configured |
| **Link-Local** | Yellow highlight | Using auto-assigned 169.254.x.x address |
| **Conflict** | Red highlight | IP address collision detected |

---

## Step 5A: Configure a Device (EtherNet/IP Mode)

### For Devices with Existing IP Address

1. **Select device** in table (single click)
2. **Double-click** device row (or click "Configure Selected Device")
3. Configuration dialog opens showing current settings
4. **Enter new configuration**:
   - **IP Address**: ✅ Required (e.g., 192.168.1.10)
   - **Subnet Mask**: ✅ Required (e.g., 255.255.255.0)
   - **Gateway**: Optional (e.g., 192.168.1.1)
   - **Hostname**: Optional (up to 64 characters)
   - **DNS Server**: Optional (e.g., 8.8.8.8)

5. **Validation**:
   - Real-time feedback on each field
   - Red error messages for invalid entries
   - "Apply" button enabled only when valid

6. Click **"Apply"** button
7. **Review confirmation dialog**:
   - Shows current vs. new configuration side-by-side
   - Verify changes are correct
8. Click **"Apply Changes"**
9. Progress dialog shows each attribute being written
10. Result dialog shows success/failure for each parameter
11. Click **"OK"** to close

### Expected Timing

- **Configuration entry**: 30 seconds
- **Write operation**: 5-8 seconds
- **Total time**: ~1 minute per device

---

## Step 5B: Configure a Factory-Default Device (BootP Mode)

### For Brand New Devices (DHCP Mode)

**⚠️ Requires Administrator Privileges**

1. **Right-click** `EtherNetIPTool.exe` → **"Run as administrator"**
2. Application launches with elevated privileges
3. Select **"BootP/DHCP"** radio button (operating mode)
4. **Connect factory-default device** to network
5. Device powers on and sends BootP request
6. Device **appears in table automatically** (within seconds)
7. **Double-click** device row
8. BootP configuration dialog opens showing:
   - MAC address from request
   - Hostname from request (if provided)

9. **Enter IP configuration**:
   - **IP Address**: ✅ Required
   - **Subnet Mask**: ✅ Required
   - **Gateway**: Optional
   - **Disable DHCP**: ☑ Checked (recommended)

10. Click **"Assign & Configure"**
11. Tool sends BootP OFFER to device
12. Waits 2 seconds for device to apply
13. (If "Disable DHCP" checked) Sends CIP message to disable DHCP mode
14. Result dialog shows outcome
15. Device switches to static IP mode

### Expected Timing

- **Wait for BootP request**: 5-30 seconds (varies by device)
- **Configuration**: 30 seconds
- **Assignment**: 2-5 seconds
- **Total time**: ~1-2 minutes

---

## Common First-Time Issues

### 🔴 "No devices found"

**Solutions**:
1. ✅ Verify Windows Firewall rules (Step 1)
2. ✅ Check network adapter selection
3. ✅ Confirm devices are powered and connected
4. ✅ Verify subnet mask matches devices
5. ✅ Try manual "Scan Now" click

### 🔴 "BootP/DHCP mode is disabled"

**Solution**: Application not running as Administrator
- Close application
- Right-click `EtherNetIPTool.exe`
- Select "Run as administrator"

### 🔴 "Configuration failed: Timeout"

**Possible Causes**:
1. Device not responding (powered off, disconnected)
2. Windows Firewall blocking TCP port 44818
3. Device doesn't support CIP configuration
4. Network congestion

**Solutions**:
1. Verify device powered and connected
2. Check firewall rules for TCP (in addition to UDP)
3. Consult device documentation
4. Try again during low network activity

### 🔴 "Attribute Not Supported"

**Meaning**: Device doesn't support that configuration parameter

**Solution**: Some attributes (Hostname, DNS) may not be available on all devices. Configure only supported parameters.

---

## Understanding the User Interface

### Main Window Layout

```
┌─────────────────────────────────────────┐
│ [Menu Bar]                               │  20px
├─────────────────────────────────────────┤
│ [Toolbar with NIC selector]              │  40px
├─────────────────────────────────────────┤
│ [Mode & Controls]                        │  40px
├─────────────────────────────────────────┤
│ [Device Table - scrollable]              │  520px
├─────────────────────────────────────────┤
│ [Action Buttons]                         │  30px
├─────────────────────────────────────────┤
│ [Status Bar]                             │  20px
└─────────────────────────────────────────┘
Total: 1280×768 (fixed size)
```

### Context Menu (Right-Click Device)

- **Configure Device**: Opens configuration dialog
- **Copy MAC Address**: Copies to clipboard
- **Copy IP Address**: Copies to clipboard
- **Ping Device**: Sends ICMP ping
- **Refresh Device Info**: Re-scans single device

### Auto-Browse Settings

- **Enabled**: Checkbox to enable/disable automatic scanning
- **Interval**: 1-60 seconds (default 5)
- **Behavior**: Scans continuously, removes stale devices after 3 missed scans

---

## Accessing Help

### Built-in Documentation

1. Press **F1** key (anywhere in application)
   - Opens User Manual

2. Use **Help menu**:
   - User Manual (F1)
   - CIP Protocol Reference
   - BootP/DHCP Reference
   - Troubleshooting Guide
   - About

### Activity Log

View detailed operation logs:

1. Menu: **Tools → Activity Log Viewer**
2. Window opens showing all operations
3. **Filter by category**:
   - INFO, SCAN, DISC, CONFIG, CIP, BOOTP, ERROR, WARN
4. **Export log** to file for support/documentation
5. **Clear log** when needed (with confirmation)

**Use Case**: When troubleshooting, export log and share with support

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **F1** | Open User Manual |
| **F5** | Refresh device list |
| **Tab** | Navigate between controls |
| **Enter** | Activate selected button |
| **Esc** | Cancel/close dialog |

---

## Best Practices

### For Efficient Workflow

1. **Keep Auto-Browse enabled** for continuous monitoring
2. **Use context menu** (right-click) for quick actions
3. **Check Activity Log** after any failures
4. **Export logs** before closing application (for records)
5. **Verify firewall rules** during initial setup

### For Network Safety

1. **Test on isolated network first** (if possible)
2. **Verify IP addresses** before applying changes
3. **Use confirmation dialogs** to catch mistakes
4. **Document changes** using log export feature

### For Troubleshooting

1. **Check device status** in table (color coding)
2. **Review Activity Log** for detailed errors
3. **Ping device** to verify connectivity
4. **Try manual scan** if auto-browse not finding devices
5. **Consult built-in help** (F1) for specific issues

---

## Next Steps

Now that you're up and running:

1. **Explore the UI**: Click around, try context menus, sort columns
2. **Read User Manual**: Press F1 for comprehensive documentation
3. **Review Protocol References**: Understand CIP and BootP/DHCP
4. **Configure test devices**: Practice on non-critical devices first
5. **Check Activity Log**: See what operations are logged

### Advanced Topics

Once comfortable with basics:

- [Architecture Guide](../03-ARCHITECTURE/ARCHITECTURE_GUIDE.md) - Understand how it works
- [Protocol Reference](../03-ARCHITECTURE/PROTOCOL_REFERENCE.md) - Deep dive into protocols
- [Troubleshooting Guide](../06-OPERATIONS/TROUBLESHOOTING.md) - Solve complex issues

---

## Support Resources

### Documentation

- **User Manual**: Press F1 in application
- **Troubleshooting**: [Troubleshooting Guide](../06-OPERATIONS/TROUBLESHOOTING.md)
- **Full Documentation**: [docs/README.md](../README.md)

### External Resources

- **ODVA Specifications**: https://www.odva.org
- **.NET 8 Downloads**: https://dotnet.microsoft.com/download/dotnet/8.0
- **Windows Firewall Help**: Search "Windows Defender Firewall with Advanced Security"

---

## Frequently Asked Questions

**Q: Do I need Administrator rights?**
A: Only for BootP/DHCP mode. EtherNet/IP mode runs as standard user.

**Q: Why can't I find my devices?**
A: Check firewall rules (UDP 44818) and network adapter selection. See [Common Issues](#common-first-time-issues).

**Q: Can I configure multiple devices at once?**
A: No, MVP version configures one device at a time. Bulk configuration planned for future release.

**Q: Will this work with PROFINET or Modbus devices?**
A: No, only EtherNet/IP devices are supported. PROFINET/Modbus support may come in future versions.

**Q: Can I save my device list and settings?**
A: Not in current version. Device list clears on application exit. Session persistence planned for future release.

---

**Ready to begin?** Launch the application and start with Step 1: [Configure Windows Firewall](#step-1-configure-windows-firewall)

**Need help?** Press **F1** in the application or see [Troubleshooting Guide](../06-OPERATIONS/TROUBLESHOOTING.md)
