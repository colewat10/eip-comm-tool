# Deployment Guide - EtherNet/IP Commissioning Tool

**Document Version:** 1.0
**Last Updated:** 2025-10-31
**Audience:** IT Operations, System Administrators

---

## System Requirements

### Minimum Requirements

| Component | Specification |
|-----------|--------------|
| **Operating System** | Windows 10 (version 1809 or later) OR Windows 11 |
| **.NET Runtime** | .NET 8.0 Runtime (Desktop) |
| **RAM** | 4 GB |
| **Disk Space** | 100 MB free space |
| **Network** | Ethernet adapter (1Gbps or 100Mbps) |
| **Display** | 1280×768 resolution minimum |

### Recommended Requirements

| Component | Specification |
|-----------|--------------|
| **Operating System** | Windows 11 Pro |
| **RAM** | 8 GB |
| **Processor** | Intel i5 or AMD equivalent |
| **Network** | Intel Gigabit Ethernet adapter |
| **Display** | 1920×1080 or higher |
| **Privileges** | Administrator rights (for BootP/DHCP mode) |

---

## Installation

### Step 1: Install .NET 8 Runtime

**Check if Already Installed**:
```powershell
dotnet --list-runtimes
```

Look for `Microsoft.WindowsDesktop.App 8.0.x`

**If Not Installed**:
1. Download from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Select **".NET Desktop Runtime 8.0.x"** (x64)
3. Run installer (requires ~200MB download)
4. Verify installation:
```powershell
dotnet --list-runtimes
```

### Step 2: Deploy Application Files

**Option A: Binary Distribution** (Recommended)
```
EtherNetIPTool/
├── EtherNetIPTool.exe          # Main executable
├── EtherNetIPTool.dll          # Application library
├── EtherNetIPTool.deps.json    # Dependencies
├── EtherNetIPTool.runtimeconfig.json
├── *.dll                        # Framework and package DLLs
├── Resources/
│   └── Help/                    # Help documentation files
└── scripts/
    └── Configure-FirewallForEtherNetIP.ps1
```

**Deployment**:
1. Copy entire folder to: `C:\Program Files\EtherNetIPTool\`
2. Create desktop shortcut to `EtherNetIPTool.exe`
3. Do NOT separate files (dependencies required)

**Option B: Build from Source**
```bash
git clone <repository-url>
cd eip-comm-tool
dotnet publish -c Release -r win-x64 --self-contained false
```

Output: `src/bin/Release/net8.0-windows/publish/`

### Step 3: Configure Windows Firewall

**⚠️ CRITICAL STEP** - Required for device discovery

**Run as Administrator**:
```powershell
cd "C:\Program Files\EtherNetIPTool\scripts"
.\Configure-FirewallForEtherNetIP.ps1
```

**What This Does**:
- Creates inbound rule for UDP port 44818
- Creates outbound rule for UDP port 44818
- Creates inbound rule for TCP port 44818
- Creates outbound rule for TCP port 44818

**Manual Configuration** (if script fails):
1. Open: Windows Defender Firewall with Advanced Security
2. Create Inbound Rule:
   - Type: Port
   - Protocol: UDP
   - Port: 44818
   - Action: Allow the connection
   - Profile: Domain, Private, Public
   - Name: "EtherNet/IP Inbound (UDP 44818)"

3. Repeat for:
   - Outbound UDP 44818
   - Inbound TCP 44818
   - Outbound TCP 44818

### Step 4: Verify Installation

1. **Launch Application**:
   - Double-click `EtherNetIPTool.exe`
   - Should start within 2-3 seconds

2. **Verify UI**:
   - Window size: 1280×768
   - Menu bar visible
   - Network adapter dropdown populated
   - Status bar shows "Ready"

3. **Test Discovery** (with device connected):
   - Click "Scan Now"
   - Devices should appear within 3 seconds
   - If no devices: Check firewall rules

---

## Network Configuration

### Network Adapter Selection

The application uses **standard network adapters** - no special drivers required.

**Supported Adapters**:
- Intel Ethernet (recommended)
- Realtek PCIe Ethernet
- Broadcom NetXtreme
- Any Windows-compatible Ethernet adapter

**Configuration**:
- Adapter must have valid IPv4 address
- Static IP recommended (DHCP works but may change)
- Should be on same subnet as target devices

### Firewall Rules

**Required Rules**:

| Direction | Protocol | Port | Purpose |
|-----------|----------|------|---------|
| Inbound | UDP | 44818 | Receive CIP List Identity responses |
| Outbound | UDP | 44818 | Send CIP List Identity requests |
| Inbound | TCP | 44818 | Receive CIP configuration responses |
| Outbound | TCP | 44818 | Send CIP configuration requests |

**For BootP/DHCP Mode (Administrator only)**:

| Direction | Protocol | Port | Purpose |
|-----------|----------|------|---------|
| Inbound | UDP | 68 | Receive BootP requests from devices |
| Outbound | UDP | 68 | Send BootP replies to devices |

### Network Isolation

**Best Practices**:
1. Use dedicated network adapter for industrial network
2. Isolate industrial network from corporate LAN
3. Use VLAN segmentation if shared physical network
4. Document IP address assignments

---

## Security Considerations

### Privilege Requirements

| Mode | Required Privilege | Reason |
|------|-------------------|--------|
| **EtherNet/IP** | Standard User | Uses non-privileged ports (ephemeral → 44818) |
| **BootP/DHCP** | Administrator | Binds to UDP port 68 (< 1024 requires elevation) |

### Running as Administrator

**When Needed**: Only for BootP/DHCP mode

**How to Run**:
1. Right-click `EtherNetIPTool.exe`
2. Select "Run as administrator"
3. Confirm UAC prompt

**Create Elevated Shortcut** (optional):
1. Right-click desktop shortcut → Properties
2. Advanced button
3. Check "Run as administrator"
4. OK, Apply

### Security Features

✅ **No Internet Connectivity**: Application is fully offline
✅ **No Telemetry**: No data leaves local machine
✅ **Local Subnet Only**: No routing, broadcasts contained
✅ **No System Modifications**: Doesn't alter Windows configuration
✅ **No Registry Changes**: Settings stored in JSON file
✅ **Read-Only Discovery**: EtherNet/IP mode only reads, doesn't write

---

## Configuration Management

### Application Settings

**Location**: `%LOCALAPPDATA%\EtherNetIPTool\appsettings.json`

**Settings**:
```json
{
  "ScanIntervalSeconds": 5,
  "AutoBrowseEnabled": true,
  "LastSelectedAdapter": "Intel Ethernet Connection"
}
```

**Modification**: Edit JSON file or use application UI

### Logging

**Log Location**: `%LOCALAPPDATA%\EtherNetIPTool\logs\`

**Log Files**:
- `ethernetip-tool-{date}.log` - Daily rotation
- Structured logging (Serilog format)
- Retention: Managed by Serilog (default: 31 days)

**Log Levels**:
- Information: Normal operations
- Warning: Non-critical issues
- Error: Failures requiring attention

**View Logs**:
- In application: Tools → Activity Log Viewer
- On disk: Navigate to log folder

---

## Deployment Scenarios

### Scenario 1: Single User Workstation

**Target**: Individual engineer's laptop/desktop

**Deployment**:
1. User installs .NET 8 Runtime
2. Copy application to `C:\Users\{username}\AppData\Local\EtherNetIPTool\`
3. User runs firewall script (requires admin once)
4. Create start menu shortcut

**Privileges**: Standard user for EtherNet/IP, request admin for BootP

### Scenario 2: Shared Industrial PC

**Target**: Shop floor commissioning station

**Deployment**:
1. IT installs .NET 8 Runtime (system-wide)
2. IT copies application to `C:\Program Files\EtherNetIPTool\`
3. IT configures firewall rules
4. IT creates elevated shortcut for all users
5. Set folder permissions: Read/Execute for Users group

**Privileges**: All users can run as administrator (via shortcut)

### Scenario 3: Multiple Users (Network Share)

**Target**: Engineering team with shared tool

**Deployment**:
1. IT installs on file server: `\\fileserver\tools\EtherNetIPTool\`
2. Create network share with Read access
3. Each user:
   - Maps network drive
   - Installs .NET 8 Runtime locally
   - Runs firewall script on their machine
4. Launch from network share

**Considerations**:
- Settings stored locally per user
- Logs stored locally per user
- Firewall rules per machine

### Scenario 4: Portable (USB Drive)

**Target**: Mobile engineers visiting multiple sites

**Limitations**:
- .NET 8 Runtime must be installed on each PC
- Firewall rules must be configured on each PC
- Cannot run BootP mode without admin rights

**Deployment**:
1. Copy application to USB drive
2. Include .NET 8 Runtime installer on USB
3. Include firewall script on USB
4. Create setup instructions document

**First-Time Setup on New PC**:
1. Install .NET 8 Runtime (requires admin)
2. Run firewall script (requires admin)
3. Launch application from USB

---

## Troubleshooting Deployment

### Application Won't Start

**Error**: "Framework not found"
- **Solution**: Install .NET 8 Desktop Runtime

**Error**: "Missing DLL"
- **Solution**: Ensure all files from publish folder are present

**Error**: Crashes on startup
- **Solution**: Check Event Viewer → Windows Logs → Application

### No Devices Found

**Check**:
1. Firewall rules configured (UDP 44818)
2. Correct network adapter selected
3. Device powered and connected
4. Same subnet as device

**Test Firewall**:
```powershell
# Check if rules exist
Get-NetFirewallRule -DisplayName "*EtherNet/IP*"

# Should show 4 rules
```

### BootP Mode Unavailable

**Symptom**: Radio button grayed out

**Cause**: Not running as Administrator

**Solution**:
1. Close application
2. Right-click → Run as administrator
3. Confirm UAC prompt

### Performance Issues

**Symptom**: Slow UI, delays

**Check**:
1. Antivirus scanning application folder
2. Network congestion
3. Low system resources (RAM < 4GB)

**Solutions**:
- Exclude application folder from antivirus
- Run on dedicated network segment
- Close other applications

---

## Uninstallation

### Standard Removal

1. Delete application folder:
   ```
   C:\Program Files\EtherNetIPTool\
   ```

2. Delete user settings (optional):
   ```
   %LOCALAPPDATA%\EtherNetIPTool\
   ```

3. Remove firewall rules:
   ```powershell
   Remove-NetFirewallRule -DisplayName "*EtherNet/IP*"
   ```

4. Remove desktop/start menu shortcuts

5. Uninstall .NET 8 Runtime (if not used by other apps):
   - Settings → Apps → Microsoft .NET Desktop Runtime 8.0.x → Uninstall

### Clean Uninstall Script

```powershell
# Run as Administrator
$appPath = "C:\Program Files\EtherNetIPTool"
$userPath = "$env:LOCALAPPDATA\EtherNetIPTool"

# Remove application
if (Test-Path $appPath) {
    Remove-Item -Path $appPath -Recurse -Force
}

# Remove user data
if (Test-Path $userPath) {
    Remove-Item -Path $userPath -Recurse -Force
}

# Remove firewall rules
Get-NetFirewallRule -DisplayName "*EtherNet/IP*" | Remove-NetFirewallRule

Write-Host "EtherNet/IP Commissioning Tool removed successfully"
```

---

## Backup and Restore

### Settings Backup

**Backup**:
```powershell
Copy-Item "$env:LOCALAPPDATA\EtherNetIPTool\appsettings.json" `
          "C:\Backup\EtherNetIPTool-settings.json"
```

**Restore**:
```powershell
Copy-Item "C:\Backup\EtherNetIPTool-settings.json" `
          "$env:LOCALAPPDATA\EtherNetIPTool\appsettings.json"
```

### Log Archive

**Archive Logs**:
```powershell
Compress-Archive -Path "$env:LOCALAPPDATA\EtherNetIPTool\logs\*" `
                 -DestinationPath "C:\Backup\EtherNetIPTool-logs-$(Get-Date -Format 'yyyyMMdd').zip"
```

---

## Monitoring

### Health Checks

**Application Health**:
- Launch time: < 3 seconds
- Memory usage: < 200MB
- CPU usage: < 5% idle, < 20% during scan
- Network traffic: Bursts during scans, minimal idle

**Log Monitoring**:
- Check for ERROR entries
- Monitor WARNING frequency
- Review failed configuration attempts

### Event Log Integration

Application logs to Windows Event Log:
- Source: "EtherNetIPTool"
- Log: Application
- Levels: Information, Warning, Error

**View Events**:
```powershell
Get-EventLog -LogName Application -Source "EtherNetIPTool" -Newest 50
```

---

## Updates

### Updating Application

1. **Stop application** if running
2. **Backup settings** (optional)
3. **Delete old files** from installation folder
4. **Copy new files** to installation folder
5. **Restart application**
6. **Verify version**: Help → About

**Settings Preserved**: User settings survive updates (separate folder)

### Update Checklist

- [ ] Backup current version
- [ ] Backup user settings
- [ ] Read release notes for breaking changes
- [ ] Deploy new version
- [ ] Test discovery
- [ ] Test configuration
- [ ] Verify logs

---

## Support Information

### Diagnostic Information to Collect

When reporting issues:
1. Windows version: `winver`
2. .NET version: `dotnet --list-runtimes`
3. Application version: Help → About
4. Exported activity log (Tools → Activity Log → Export)
5. Firewall rules: `Get-NetFirewallRule -DisplayName "*EtherNet/IP*"`
6. Network adapters: `Get-NetAdapter`

### Documentation

- **User Manual**: Press F1 in application
- **Troubleshooting**: [Troubleshooting Guide](TROUBLESHOOTING.md)
- **Technical Docs**: [docs/README.md](../README.md)

---

**Document Maintained By**: Operations Team
**Review Cycle**: Before each major release
**Questions**: See [Troubleshooting Guide](TROUBLESHOOTING.md)
