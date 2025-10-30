# EtherNet/IP Commissioning Tool

EtherNet/IP Commissioning Tool is a Windows desktop application that consolidates industrial Ethernet device commissioning and troubleshooting capabilities into a single unified interface. The tool replaces the need for multiple applications (RSLinx, BootP-DHCP utilities, web browsers, command-line tools) during device setup and basic diagnostics.

## Overview

This is a professional industrial automation tool for commissioning EtherNet/IP devices from vendors including:
- Allen-Bradley (ControlLogix, CompactLogix, Stratix switches, PowerFlex drives, POINT I/O)
- SICK (Industrial sensors with EtherNet/IP)
- Banner (Industrial sensors and indicators)
- Pepperl+Fuchs (IO-Link masters, field devices)
- Turck (TBIP series IO-Link Masters, field devices)
- Any device supporting EtherNet/IP and CIP TCP/IP Interface Object (Class 0xF5)

## Features

### MVP (Minimum Viable Product)

**Phase 1 - Core Infrastructure** ✅ Complete
- Main window shell with menu bar and toolbar
- Network interface enumeration and selection
- Application settings management
- Privilege detection (Administrator rights check)
- Structured logging service with activity tracking
- MVVM architecture foundation

**Phase 2 - EtherNet/IP Discovery** ✅ Complete
- CIP List Identity packet builder with byte-level protocol implementation
- Single UDP socket with OS-assigned ephemeral port (RSLinx compatible)
- Subnet-directed broadcast discovery (calculated per adapter, no global broadcast)
- Device discovery with 3-second response collection
- Response parser extracting device identity
- Vendor ID to name mapping (70+ vendors)
- Device list management with duplicate detection
- UI integration with Scan Now and Clear List commands

**Phase 3 - Device Table UI** ✅ Complete
- WPF DataGrid with 7 columns (Row #, MAC, IP, Subnet, Vendor, Model, Status)
- Column sorting with ascending/descending toggle
- Single row selection with MVVM binding
- Status-based row highlighting (Link-Local yellow, Conflict red)
- Context menu (Configure, Copy MAC/IP, Ping, Refresh)
- Double-click to configure device
- Model column ellipsis truncation with tooltip
- Device count display in section header
- Row number display (1-based indexing)

**Phase 4 - Configuration Dialog (EtherNet/IP)** ✅ Complete
- Modal configuration dialog (500x400px) with device information display
- Custom IP octet input control (4-box, 0-255, auto-advance)
- Required fields: IP Address, Subnet Mask (marked with asterisk)
- Optional fields: Gateway, Hostname (64 char max), DNS Server
- Real-time validation with error messages in red text
- Hostname validation (alphanumeric, hyphens, underscores only)
- Subnet validation (Gateway/DNS must be on same subnet)
- Apply button enabled only when all fields valid
- Confirmation dialog (400x300px) showing current vs. new configuration
- Complete dialog flow with logging and status updates

**Phase 5 - CIP Configuration Protocol** ✅ Complete
- CIP Set_Attribute_Single message builder (Service 0x10)
- Unconnected Send wrapper (Service 0x52 via UCMM)
- TCP/IP Interface Object attribute writes (Class 0xF5, Instance 1)
- Sequential attribute writes (IP → Subnet → Gateway → Hostname → DNS)
- 3-second timeout per write operation
- 100ms inter-message delay between writes
- Progress dialog with "Sending configuration... (X/Y)" indicator
- Stop on first failure behavior
- Result dialog with success/failure details for each attribute
- CIP error code translation to human-readable messages
- Device removal from list after successful configuration

**Phase 6 - BootP/DHCP Server** ✅ Complete
- Operating mode switching (EtherNet/IP Discovery vs. BootP/DHCP Server)
- BootP packet parser (RFC 951 format with DHCP options RFC 2132)
- BootP reply builder with magic cookie and DHCP option 53 (DHCPOFFER)
- UDP server listening on port 68 (requires Administrator privileges)
- Privilege detection with graceful degradation and user notification
- BootP configuration dialog (450x350px) with request information display
- IP/Subnet/Gateway configuration with IpOctetInput controls
- "Disable DHCP after assignment" option (default enabled)
- Complete configuration workflow (send reply → wait 2s → disable DHCP)
- CIP Set_Attribute_Single for Configuration Control (Attribute 3)
- Broadcast/unicast reply handling based on BootP FLAGS field
- Automatic server lifecycle management on mode and adapter changes

**Phase 7-9**: Auto-Browse, Logging/Help System, Testing & Polish

## Technology Stack

- **Platform**: Windows 10/11 Desktop Application
- **Framework**: .NET 8 (C# 12)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Logging**: Serilog with structured logging
- **Testing**: xUnit, Moq, FluentAssertions

## Project Structure

```
eip-comm-tool/
├── src/                           # Main application source
│   ├── Core/                      # Protocol implementations
│   │   ├── BootP/                 # BootP/DHCP protocol (RFC 951/2132)
│   │   ├── CIP/                   # CIP protocol (encapsulation, messages)
│   │   └── Network/               # Network layer (sockets)
│   ├── Models/                    # Data models (Device, etc.)
│   ├── ViewModels/                # MVVM ViewModels
│   ├── Views/                     # WPF Views (XAML)
│   ├── Services/                  # Service layer (discovery, logging, settings)
│   ├── Resources/                 # Icons, help files, styles
│   ├── App.xaml                   # Application definition
│   ├── App.xaml.cs                # Application entry point
│   └── EtherNetIPTool.csproj     # Project file
├── tests/                         # Unit tests
│   └── EtherNetIPTool.Tests.csproj
├── scripts/                       # Utility scripts
│   └── Configure-FirewallForEtherNetIP.ps1  # Windows Firewall setup
├── docs/                          # Documentation
│   ├── PRD.md                     # Product Requirements Document
│   ├── TROUBLESHOOTING.md         # Troubleshooting guide
│   └── ARCHITECTURE_PHASE1.md    # Phase 1 Architecture Documentation
├── agents/                        # AI agent profiles
└── EtherNetIPTool.sln            # Visual Studio solution
```

**Note:** This is a single-project solution with a flattened structure for simplicity.
The `src/` directory contains all application code directly, without an intermediate
project folder. This reduces path nesting and improves navigation.

**Discovery Architecture:** Uses single-socket with subnet-only broadcast per PRD
REQ-4.1.1-001/002. This ensures RSLinx compatibility and network isolation while
requiring proper subnet mask configuration.

## Getting Started

### Prerequisites

- Windows 10 (version 1809+) or Windows 11
- .NET 8 SDK or Runtime
- Visual Studio 2022 (recommended) or Visual Studio Code

### Building the Project

```bash
# Clone the repository
git clone <repository-url>
cd eip-comm-tool

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project src/EtherNetIPTool.csproj
```

### Running Tests

```bash
# Run all unit tests
dotnet test

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

### First-Time Setup: Configure Windows Firewall

**IMPORTANT:** EtherNet/IP discovery requires UDP port 44818 to be open in Windows Firewall.

Run the included PowerShell script as Administrator:

```powershell
# Navigate to scripts folder
cd scripts

# Run firewall configuration script
.\Configure-FirewallForEtherNetIP.ps1
```

This creates firewall rules to allow:
- Inbound UDP port 44818 (receive device responses)
- Outbound UDP port 44818 (send broadcasts)

Without these rules, **no devices will be discovered**.

See [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for detailed troubleshooting steps.

## Documentation

- **[Product Requirements Document (PRD)](docs/PRD.md)**: Complete functional and technical specifications
- **[Troubleshooting Guide](docs/TROUBLESHOOTING.md)**: Device discovery troubleshooting and diagnostics
- **[Phase 1 Architecture Documentation](docs/ARCHITECTURE_PHASE1.md)**: Detailed architecture and implementation guide
- **[Phase 3 Implementation Guide](docs/PHASE3_IMPLEMENTATION.md)**: Device Table UI implementation details
- **[Phase 4 Implementation Guide](docs/PHASE4_IMPLEMENTATION.md)**: Configuration Dialog implementation details
- **[Phase 5 Implementation Guide](docs/PHASE5_IMPLEMENTATION.md)**: CIP Configuration Protocol implementation details
- **[Phase 6 Implementation Guide](docs/PHASE6_IMPLEMENTATION.md)**: BootP/DHCP Server implementation details
- **[Agent Profiles](agents/)**: AI development agent specifications

## Development Approach

This project is entirely AI-generated using specialized agent profiles:

- **industrial-software-pro**: Industrial automation and protocol expertise
- **csharp-pro**: Modern C# and .NET best practices
- **backend-architect**: System architecture and design patterns
- **docs-architect**: Comprehensive technical documentation

All development strictly follows the PRD and agent profiles to ensure professional, production-ready code.

## Key Design Principles

1. **Industrial-Grade Reliability**: Rock-solid core functionality, comprehensive error handling
2. **Dense Information Display**: Maximize information density, minimal whitespace
3. **MVVM Architecture**: Clear separation of concerns, testable code
4. **Fail-Safe Design**: Graceful degradation when features unavailable
5. **Comprehensive Logging**: Every operation logged for troubleshooting
6. **Standards Compliance**: Follow ODVA CIP and EtherNet/IP specifications

## Roadmap

### Current Status: Phase 6 Complete ✅

**Phase 1 - Core Infrastructure:**
- [x] Project structure and build system
- [x] Main window shell with menu bar and toolbar
- [x] Network interface enumeration and selection
- [x] Privilege detection service
- [x] Structured logging system
- [x] Application settings persistence
- [x] MVVM infrastructure (base classes, commands)
- [x] Comprehensive architecture documentation

**Phase 2 - EtherNet/IP Discovery:**
- [x] CIP Encapsulation header (24-byte structure)
- [x] CIP List Identity packet builder (command 0x0063)
- [x] CIP List Identity response parser
- [x] UDP broadcast socket service (EtherNet/IP port 44818)
- [x] Device data model with status tracking
- [x] Vendor ID mapping service (70+ vendors)
- [x] Device discovery service with ARP lookup
- [x] ViewModel integration and UI bindings
- [x] Functional device scanning and display

**Phase 3 - Device Table UI:**
- [x] WPF DataGrid with 7 columns
- [x] Row selection handling
- [x] Status-based row coloring (Link-Local yellow, Conflict red)
- [x] Context menu (Configure, Copy MAC/IP, Ping, Refresh)
- [x] Column sorting functionality
- [x] Double-click to configure

**Phase 4 - Configuration Dialog:**
- [x] Modal configuration dialog with validation
- [x] Custom IP octet input control
- [x] Required/optional field handling
- [x] Real-time validation with error messages
- [x] Hostname and subnet validation
- [x] Confirmation dialog with current vs. new comparison

**Phase 5 - CIP Configuration Protocol:**
- [x] CIP Set_Attribute_Single message builder
- [x] Unconnected Send wrapper
- [x] TCP/IP Interface Object attribute writes
- [x] Sequential write service with progress tracking
- [x] Progress dialog with percentage indicator
- [x] Result dialog with success/failure details
- [x] CIP error code translation
- [x] Device removal on successful configuration

**Phase 6 - BootP/DHCP Server:**
- [x] Operating mode enumeration (EtherNet/IP vs. BootP/DHCP)
- [x] BootP packet structure (RFC 951 format)
- [x] BootP packet parser with DHCP options (RFC 2132)
- [x] BootP reply builder with magic cookie and option 53
- [x] UDP server on port 68 with broadcast/unicast support
- [x] Administrator privilege detection and validation
- [x] BootP server lifecycle management (start/stop)
- [x] BootP configuration dialog with request information
- [x] BootP configuration ViewModel with IP validation
- [x] Configuration workflow service (reply → wait → disable DHCP)
- [x] CIP Configuration Control attribute (Attribute 3)
- [x] MainWindowViewModel mode switching integration
- [x] Automatic adapter change handling

### Next: Phase 7 - Auto-Browse

- [ ] Embedded web browser control
- [ ] HTTP server detection
- [ ] Device web interface integration

## License

See [LICENSE](LICENSE) file for details.

---

**Version**: 1.0.0-alpha
**Status**: Phase 6 Complete - BootP/DHCP Server
**Last Updated**: 2025-10-30
