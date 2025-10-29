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

**Phase 3-9**: Device Table UI enhancements, Configuration Dialog, CIP Configuration Protocol, BootP/DHCP Server, Auto-Browse, Logging/Help System, Testing & Polish

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

### Current Status: Phase 2 Complete ✅

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

### Next: Phase 3 - Device Table UI Enhancements

- [ ] Row selection handling
- [ ] Status-based row coloring (Link-Local yellow, Conflict red)
- [ ] Context menu (Configure, Copy MAC/IP, Ping, Refresh)
- [ ] Column sorting functionality
- [ ] Double-click to configure

## License

See [LICENSE](LICENSE) file for details.

---

**Version**: 1.0.0-alpha
**Status**: Phase 2 Complete - EtherNet/IP Discovery
**Last Updated**: 2025-10-29
