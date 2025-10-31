# EtherNet/IP Commissioning Tool - Documentation Library

**Version:** 1.0.0
**Status:** Phase 8 Complete (MVP Feature Complete)
**Last Updated:** 2025-10-31

---

## Welcome

This documentation library provides comprehensive technical information about the EtherNet/IP Commissioning Tool. The documentation is organized by audience and purpose, allowing you to quickly find the information you need.

## 📖 Reading Paths by Audience

### For New Users
1. Start with [Product Overview](01-OVERVIEW/PRODUCT_OVERVIEW.md)
2. Follow [Getting Started Guide](01-OVERVIEW/GETTING_STARTED.md)
3. Reference [User Manual](../src/Resources/Help/UserManual.html) (accessible via F1 in application)

### For Developers/Contributors
1. Read [Developer Guide](07-DEVELOPMENT/DEVELOPER_GUIDE.md) first
2. Study [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md)
3. Review [Implementation Guides](04-IMPLEMENTATION/) for specific phases
4. Consult [API Reference](08-REFERENCE/API_REFERENCE.md) as needed

### For System Architects
1. Review [Product Requirements](02-REQUIREMENTS/PRD.md)
2. Study [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md)
3. Examine [Design Decisions](03-ARCHITECTURE/DESIGN_DECISIONS.md)
4. Reference [Protocol Specifications](03-ARCHITECTURE/PROTOCOL_REFERENCE.md)

### For Operations/DevOps
1. Follow [Deployment Guide](06-OPERATIONS/DEPLOYMENT_GUIDE.md)
2. Use [Troubleshooting Guide](06-OPERATIONS/TROUBLESHOOTING.md)
3. Reference [Quick Reference](08-REFERENCE/QUICK_REFERENCE.md)

---

## 📂 Documentation Structure

### [01-OVERVIEW](01-OVERVIEW/)
Executive summaries and quick-start information for all audiences.

| Document | Description | Audience |
|----------|-------------|----------|
| [Product Overview](01-OVERVIEW/PRODUCT_OVERVIEW.md) | Executive summary of the tool's purpose and capabilities | All |
| [Getting Started](01-OVERVIEW/GETTING_STARTED.md) | Quick start guide for first-time users | Users, Developers |

### [02-REQUIREMENTS](02-REQUIREMENTS/)
Detailed functional and non-functional requirements.

| Document | Description | Audience |
|----------|-------------|----------|
| [Product Requirements (PRD)](02-REQUIREMENTS/PRD.md) | Complete product specification with all requirements | Architects, Developers |

### [03-ARCHITECTURE](03-ARCHITECTURE/)
System architecture, design decisions, and technical specifications.

| Document | Description | Audience |
|----------|-------------|----------|
| [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md) | Comprehensive system architecture documentation | Architects, Developers |
| [Design Decisions](03-ARCHITECTURE/DESIGN_DECISIONS.md) | Rationale behind key architectural choices | Architects, Developers |
| [Protocol Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md) | CIP, EtherNet/IP, and BootP/DHCP protocol details | Developers, Architects |

### [04-IMPLEMENTATION](04-IMPLEMENTATION/)
Phase-by-phase implementation guides with code examples.

| Document | Description | Phase |
|----------|-------------|-------|
| [Phase 1: Core Infrastructure](04-IMPLEMENTATION/PHASE1_CORE_INFRASTRUCTURE.md) | Project setup, logging, MVVM foundation | 1 |
| [Phase 2: Device Discovery](04-IMPLEMENTATION/PHASE2_DEVICE_DISCOVERY.md) | CIP List Identity and device discovery | 2 |
| [Phase 3: Device Table UI](04-IMPLEMENTATION/PHASE3_DEVICE_TABLE_UI.md) | DataGrid implementation and device list | 3 |
| [Phase 4: Configuration Dialog](04-IMPLEMENTATION/PHASE4_CONFIGURATION_DIALOG.md) | IP configuration dialog and validation | 4 |
| [Phase 5: CIP Configuration](04-IMPLEMENTATION/PHASE5_CIP_CONFIGURATION.md) | Set_Attribute_Single and ODVA session management | 5 |
| [Phase 6: BootP/DHCP Server](04-IMPLEMENTATION/PHASE6_BOOTP_DHCP_SERVER.md) | BootP server for factory-default devices | 6 |
| [Phase 7: Auto-Browse & Scanning](04-IMPLEMENTATION/PHASE7_AUTO_BROWSE_SCANNING.md) | Automatic periodic device scanning | 7 |
| [Phase 8: Logging & Help System](04-IMPLEMENTATION/PHASE8_LOGGING_HELP_SYSTEM.md) | Activity log viewer and embedded help | 8 |

### [05-COMPLIANCE](05-COMPLIANCE/)
ODVA standards compliance documentation.

| Document | Description | Audience |
|----------|-------------|----------|
| [ODVA Compliance](05-COMPLIANCE/ODVA_COMPLIANCE.md) | Complete ODVA CIP and EtherNet/IP compliance implementation | Architects, Developers |

### [06-OPERATIONS](06-OPERATIONS/)
Deployment, operation, and troubleshooting guides.

| Document | Description | Audience |
|----------|-------------|----------|
| [Deployment Guide](06-OPERATIONS/DEPLOYMENT_GUIDE.md) | Installation, configuration, and deployment | Operations, Users |
| [Troubleshooting Guide](06-OPERATIONS/TROUBLESHOOTING.md) | Common issues and solutions | Operations, Users |

### [07-DEVELOPMENT](07-DEVELOPMENT/)
Developer resources for contributing to the project.

| Document | Description | Audience |
|----------|-------------|----------|
| [Developer Guide](07-DEVELOPMENT/DEVELOPER_GUIDE.md) | Development setup, coding standards, contribution guidelines | Developers |
| [Testing Guide](07-DEVELOPMENT/TESTING_GUIDE.md) | Testing strategy and test writing guidelines | Developers |
| [Build System](07-DEVELOPMENT/BUILD_SYSTEM.md) | Build configuration and tooling | Developers |

### [08-REFERENCE](08-REFERENCE/)
Quick reference materials and API documentation.

| Document | Description | Audience |
|----------|-------------|----------|
| [API Reference](08-REFERENCE/API_REFERENCE.md) | Public API and service interfaces | Developers |
| [Glossary](08-REFERENCE/GLOSSARY.md) | Technical terms and definitions | All |
| [Quick Reference](08-REFERENCE/QUICK_REFERENCE.md) | Cheat sheet for common tasks | Users, Developers |

---

## 🚀 Quick Links

### Most Frequently Accessed

- **[Getting Started](01-OVERVIEW/GETTING_STARTED.md)** - Start here if you're new
- **[Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md)** - Understand the system design
- **[Troubleshooting](06-OPERATIONS/TROUBLESHOOTING.md)** - Solve common problems
- **[Developer Guide](07-DEVELOPMENT/DEVELOPER_GUIDE.md)** - Contribute to the project

### Protocol Specifications

- **[CIP Protocol Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md#cip-protocol)** - Common Industrial Protocol
- **[EtherNet/IP Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md#ethernetip)** - EtherNet/IP encapsulation
- **[BootP/DHCP Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md#bootpdhcp)** - Bootstrap Protocol

### Implementation Guides

- **[Complete Phase List](04-IMPLEMENTATION/)** - All 8 phases
- **[ODVA Compliance](05-COMPLIANCE/ODVA_COMPLIANCE.md)** - Standards compliance

---

## 📊 Project Status

| Phase | Name | Status | Documentation |
|-------|------|--------|---------------|
| 1 | Core Infrastructure | ✅ Complete | [Phase 1 Guide](04-IMPLEMENTATION/PHASE1_CORE_INFRASTRUCTURE.md) |
| 2 | Device Discovery | ✅ Complete | [Phase 2 Guide](04-IMPLEMENTATION/PHASE2_DEVICE_DISCOVERY.md) |
| 3 | Device Table UI | ✅ Complete | [Phase 3 Guide](04-IMPLEMENTATION/PHASE3_DEVICE_TABLE_UI.md) |
| 4 | Configuration Dialog | ✅ Complete | [Phase 4 Guide](04-IMPLEMENTATION/PHASE4_CONFIGURATION_DIALOG.md) |
| 5 | CIP Configuration | ✅ Complete | [Phase 5 Guide](04-IMPLEMENTATION/PHASE5_CIP_CONFIGURATION.md) |
| 6 | BootP/DHCP Server | ✅ Complete | [Phase 6 Guide](04-IMPLEMENTATION/PHASE6_BOOTP_DHCP_SERVER.md) |
| 7 | Auto-Browse & Scanning | ✅ Complete | [Phase 7 Guide](04-IMPLEMENTATION/PHASE7_AUTO_BROWSE_SCANNING.md) |
| 8 | Logging & Help System | ✅ Complete | [Phase 8 Guide](04-IMPLEMENTATION/PHASE8_LOGGING_HELP_SYSTEM.md) |
| 9 | Testing & Polish | 🔄 In Progress | Coming soon |

**MVP Status:** Feature Complete (Phases 1-8) ✅

---

## 🔍 Finding Information

### By Topic

- **Device Discovery**: [Phase 2](04-IMPLEMENTATION/PHASE2_DEVICE_DISCOVERY.md), [Protocol Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md#cip-list-identity)
- **Device Configuration**: [Phase 4](04-IMPLEMENTATION/PHASE4_CONFIGURATION_DIALOG.md), [Phase 5](04-IMPLEMENTATION/PHASE5_CIP_CONFIGURATION.md)
- **BootP/DHCP**: [Phase 6](04-IMPLEMENTATION/PHASE6_BOOTP_DHCP_SERVER.md), [Protocol Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md#bootpdhcp)
- **Logging**: [Phase 8](04-IMPLEMENTATION/PHASE8_LOGGING_HELP_SYSTEM.md#activity-log-viewer)
- **Help System**: [Phase 8](04-IMPLEMENTATION/PHASE8_LOGGING_HELP_SYSTEM.md#help-system)

### By Component

- **Services**: [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md#service-layer)
- **ViewModels**: [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md#viewmodel-layer)
- **Views**: [Architecture Guide](03-ARCHITECTURE/ARCHITECTURE_GUIDE.md#view-layer)
- **Protocol Handlers**: [Protocol Reference](03-ARCHITECTURE/PROTOCOL_REFERENCE.md)

### By Error/Issue

- **No Devices Found**: [Troubleshooting](06-OPERATIONS/TROUBLESHOOTING.md#no-devices-found)
- **Configuration Failed**: [Troubleshooting](06-OPERATIONS/TROUBLESHOOTING.md#configuration-failed)
- **BootP Issues**: [Troubleshooting](06-OPERATIONS/TROUBLESHOOTING.md#bootpdhcp-issues)

---

## 📝 Documentation Standards

All documentation in this library follows these standards:

1. **Markdown Format**: All docs use GitHub Flavored Markdown
2. **Heading Hierarchy**: Clear H1-H6 structure for navigation
3. **Code Examples**: Syntax-highlighted code blocks with explanations
4. **Cross-References**: Links between related documents
5. **Version Control**: Document version and last updated date
6. **Audience Tags**: Clear indication of target audience

### Documentation Versioning

- Documentation version matches the application version (1.0.0)
- Breaking changes are documented in each guide
- Historical decisions are preserved for context

---

## 🤝 Contributing to Documentation

Found an error or want to improve the documentation?

1. See [Developer Guide](07-DEVELOPMENT/DEVELOPER_GUIDE.md#contributing)
2. Follow documentation standards above
3. Submit pull request with clear description

---

## 📧 Support and Resources

### Internal Resources

- **Source Code**: Root `/src` directory
- **Tests**: Root `/tests` directory
- **Agent Profiles**: Root `/agents` directory
- **Scripts**: Root `/scripts` directory

### External Resources

- **ODVA Specifications**: [ODVA.org](https://www.odva.org)
- **.NET 8 Documentation**: [Microsoft Docs](https://docs.microsoft.com/dotnet/)
- **WPF Documentation**: [Microsoft WPF Docs](https://docs.microsoft.com/dotnet/desktop/wpf/)

---

## 📄 License

See [LICENSE](../LICENSE) file in repository root.

---

**Document Maintained By**: EtherNet/IP Commissioning Tool Documentation Team
**Questions**: Refer to specific document or [Troubleshooting Guide](06-OPERATIONS/TROUBLESHOOTING.md)
