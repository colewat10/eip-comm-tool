# EtherNet/IP Commissioning Tool

EtherNet/IP Commissioning Tool is a Windows desktop application that consolidates industrial Ethernet device commissioning and troubleshooting capabilities into a single unified interface. The tool replaces the need for multiple applications (RSLinx, BootP-DHCP utilities, web browsers, command-line tools) during device setup and basic diagnostics.

## Overview

This is a professional industrial automation tool for commissioning EtherNet/IP devices from vendors including:
- Allen-Bradley (ControlLogix, CompactLogix, Stratix switches, PowerFlex drives, POINT I/O)
- SICK (Industrial sensors with EtherNet/IP)
- Banner (Industrial sensors and indicators)
- Pepperl+Fuchs (IO-Link masters, field devices)
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

**Phase 2 - EtherNet/IP Discovery** (Next)
- CIP List Identity packet builder
- UDP broadcast socket configuration
- Device discovery and response parsing
- Vendor ID to name mapping

**Phase 3-9**: Device Table UI, Configuration Dialog, CIP Protocol, BootP/DHCP Server, Auto-Browse, Logging/Help System, Testing & Polish

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
├── src/
│   └── EtherNetIPTool/           # Main application project
│       ├── Core/                  # Protocol implementations
│       ├── Models/                # Data models
│       ├── ViewModels/            # MVVM ViewModels
│       ├── Views/                 # WPF Views (XAML)
│       ├── Services/              # Service layer
│       └── Resources/             # Icons, help files, styles
├── tests/
│   └── EtherNetIPTool.Tests/     # Unit tests
├── docs/
│   ├── PRD.md                     # Product Requirements Document
│   └── ARCHITECTURE_PHASE1.md    # Phase 1 Architecture Documentation
├── agents/                        # AI agent profiles
└── EtherNetIPTool.sln            # Visual Studio solution
```

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
dotnet run --project src/EtherNetIPTool/EtherNetIPTool.csproj
```

### Running Tests

```bash
# Run all unit tests
dotnet test

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

## Documentation

- **[Product Requirements Document (PRD)](docs/PRD.md)**: Complete functional and technical specifications
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

### Current Status: Phase 1 Complete ✅

- [x] Project structure and build system
- [x] Main window shell with menu bar and toolbar
- [x] Network interface enumeration and selection
- [x] Privilege detection service
- [x] Structured logging system
- [x] Application settings persistence
- [x] MVVM infrastructure (base classes, commands)
- [x] Comprehensive architecture documentation

### Next: Phase 2 - EtherNet/IP Discovery

- [ ] CIP List Identity protocol implementation
- [ ] UDP broadcast socket configuration
- [ ] Device response parser
- [ ] Device data model
- [ ] Vendor ID mapping

## License

See [LICENSE](LICENSE) file for details.

---

**Version**: 1.0.0-alpha
**Status**: Phase 1 Complete - Core Infrastructure
**Last Updated**: 2025-10-29
