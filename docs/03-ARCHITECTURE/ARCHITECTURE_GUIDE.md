# Architecture Guide - EtherNet/IP Commissioning Tool

**Document Version:** 1.0
**Last Updated:** 2025-10-31
**Audience:** Architects, Senior Developers

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [Layer Architecture](#layer-architecture)
4. [Key Design Patterns](#key-design-patterns)
5. [Component Architecture](#component-architecture)
6. [Data Flow](#data-flow)
7. [Network Architecture](#network-architecture)
8. [Security Architecture](#security-architecture)
9. [Performance Architecture](#performance-architecture)
10. [Testing Architecture](#testing-architecture)

---

## 1. Executive Summary

The EtherNet/IP Commissioning Tool is a Windows desktop application built on .NET 8/WPF using the MVVM architectural pattern. The system implements a layered architecture with clear separation between presentation, business logic, and infrastructure concerns.

### Key Architectural Characteristics

| Aspect | Approach | Rationale |
|--------|----------|-----------|
| **UI Framework** | WPF (XAML) | Modern, data binding, MVVM support |
| **Pattern** | MVVM | Separation of concerns, testability |
| **Language** | C# 12 | Modern features, async/await |
| **Services** | Constructor injection | Testability, loose coupling |
| **Protocols** | CIP, EtherNet/IP, BootP | Industry standards (ODVA, RFC 951) |
| **Networking** | Sockets (UDP/TCP) | Direct control, no dependencies |
| **Logging** | Serilog | Structured, multiple sinks |

### Design Goals

1. **Industrial-Grade Reliability**: Rock-solid core, comprehensive error handling
2. **ODVA Compliance**: Strict adherence to specifications
3. **Professional UX**: Dense information, minimal clicks
4. **Maintainability**: Clear architecture, well-documented
5. **Testability**: Dependency injection, mocked services

---

## 2. System Architecture

### 2.1 High-Level System View

```
┌──────────────────────────────────────────────────────────┐
│                     User Interface                        │
│              (WPF Windows & Controls)                     │
├──────────────────────────────────────────────────────────┤
│                                                           │
│                    Presentation Layer                     │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │              │  │              │  │              │  │
│  │  ViewModels  │  │   Commands   │  │  Converters  │  │
│  │              │  │              │  │              │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                           │
├──────────────────────────────────────────────────────────┤
│                                                           │
│                      Service Layer                        │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │   Discovery  │  │Configuration │  │   Activity   │  │
│  │   Service    │  │   Service    │  │   Logger     │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │    BootP     │  │  Auto-Browse │  │   Network    │  │
│  │   Server     │  │   Service    │  │   Service    │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                           │
├──────────────────────────────────────────────────────────┤
│                                                           │
│                     Protocol Layer                        │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │     CIP      │  │  EtherNet/IP │  │BootP/DHCP    │  │
│  │   Messages   │  │ Encapsulation│  │   Packets    │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                           │
├──────────────────────────────────────────────────────────┤
│                                                           │
│                     Network Layer                         │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │  UDP Socket  │  │  TCP Socket  │  │  ARP Lookup  │  │
│  │  (Port 44818)│  │  (Port 44818)│  │              │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                           │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
                ┌──────────────────────┐
                │  Windows Network API │
                │   (.NET Sockets)     │
                └──────────────────────┘
```

### 2.2 Component Interaction

```
User Action (Click "Scan Now")
        │
        ▼
MainWindowViewModel.ScanNowCommand
        │
        ▼
DeviceDiscoveryService.ScanAsync()
        │
        ├──> Build CIP List Identity packet
        │
        ├──> Send UDP broadcast (port 44818)
        │
        ├──> Collect responses (3-second window)
        │
        ├──> Parse CIP Identity responses
        │
        ├──> Lookup MAC addresses (ARP)
        │
        ├──> Map Vendor IDs to names
        │
        └──> Update ObservableCollection<Device>
                │
                ▼
        DataGrid reflects changes (data binding)
```

---

## 3. Layer Architecture

### 3.1 View Layer (XAML + Code-Behind)

**Responsibility**: User interface presentation only

**Components**:
- `MainWindow.xaml` - Primary application window
- `ConfigurationDialog.xaml` - IP configuration dialog
- `BootPConfigurationDialog.xaml` - BootP assignment dialog
- `ActivityLogWindow.xaml` - Log viewer
- `HelpWindow.xaml` - Help documentation viewer
- `ProgressDialog.xaml`, `ConfirmationDialog.xaml` - Utility dialogs

**Design Rules**:
- ✅ XAML-only layouts (no procedural UI generation)
- ✅ Data binding for all dynamic content
- ✅ Minimal code-behind (initialization only)
- ❌ No business logic in code-behind
- ❌ No direct service access from views

**See**: [Phase 1 Guide](../04-IMPLEMENTATION/PHASE1_CORE_INFRASTRUCTURE.md#user-interface) for UI specifications

### 3.2 ViewModel Layer

**Responsibility**: Presentation logic, command handling, state management

**Base Classes**:
- `ViewModelBase` - `INotifyPropertyChanged` implementation: `src/ViewModels/ViewModelBase.cs`
- `RelayCommand` - `ICommand` implementation: `src/ViewModels/RelayCommand.cs`
- `AsyncRelayCommand` - Async command wrapper: `src/ViewModels/RelayCommand.cs`

**Key ViewModels**:

| ViewModel | Purpose | Key Responsibilities |
|-----------|---------|---------------------|
| `MainWindowViewModel` | Primary application state | Device list, adapter selection, mode switching, commands |
| `ConfigurationViewModel` | Device configuration | IP validation, confirmation flow |
| `BootPConfigurationViewModel` | BootP assignment | BootP request handling, DHCP disable |
| `ActivityLogViewModel` | Log viewer | Category filtering, log export |
| `HelpViewModel` | Help display | Content loading |

**Pattern Usage**:
```csharp
// Property with change notification
private string _statusText = "Ready";
public string StatusText
{
    get => _statusText;
    set => SetProperty(ref _statusText, value);
}

// Command binding
public ICommand ScanNowCommand { get; }

// Constructor injection
public MainWindowViewModel(ActivityLogger logger, ...)
{
    _logger = logger;
    ScanNowCommand = new AsyncRelayCommand(_ => ScanNowAsync());
}
```

### 3.3 Service Layer

**Responsibility**: Business logic, protocol implementation, external interactions

**Core Services**:

```
Services/
├── ActivityLogger.cs          - Logging with categorization
├── ApplicationSettingsService  - Settings persistence
├── DeviceDiscoveryService      - CIP device discovery
├── ConfigurationWriteService   - CIP configuration writes
├── BootPServer                 - BootP/DHCP server
├── AutoBrowseService           - Periodic scanning
├── NetworkInterfaceService     - NIC enumeration
├── PrivilegeDetectionService   - Admin rights check
└── VendorMappingService        - Vendor ID translation
```

**Service Lifecycle**:
- **Singleton**: `ActivityLogger` (shared across application)
- **Per-ViewModel**: Services created when ViewModel instantiated
- **Disposal**: Services implement `IDisposable`, cleaned up with ViewModel

**Example Service**:
```csharp
public class DeviceDiscoveryService : IDisposable
{
    private readonly ActivityLogger _logger;
    private UdpClient? _udpClient;

    public DeviceDiscoveryService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<Device>> ScanAsync(
        IPAddress adapterIP,
        IPAddress subnetMask,
        bool autoBrowseMode,
        CancellationToken cancellationToken)
    {
        // Discovery logic...
    }

    public void Dispose()
    {
        _udpClient?.Dispose();
    }
}
```

### 3.4 Protocol Layer

**Responsibility**: Protocol packet construction and parsing

**Protocol Implementations**:

```
Core/
├── CIP/
│   ├── CIPEncapsulation.cs        - EtherNet/IP headers
│   ├── CIPListIdentity.cs         - List Identity messages
│   ├── SetAttributeSingleMessage  - Configuration messages
│   └── CIPStatusCodes.cs          - Error translation
│
└── BootP/
    ├── BootPPacket.cs             - BootP packet structure
    ├── BootPServer.cs             - Server implementation
    └── DHCPOptions.cs             - DHCP option parsing
```

**Design Pattern**: Message Builders with static factory methods
```csharp
public static class SetAttributeSingleMessage
{
    public static byte[] BuildSetIPAddressRequest(IPAddress ipAddress, IPAddress targetDevice)
    {
        // Construct complete CIP message with:
        // - EtherNet/IP encapsulation header
        // - CPF (Common Packet Format) structure
        // - Unconnected Send wrapper
        // - Set_Attribute_Single message
        // - TCP/IP Interface Object addressing
        // - IP address attribute data
    }
}
```

**See**: [Phase 5 Guide](../04-IMPLEMENTATION/PHASE5_IMPLEMENTATION.md) for CIP protocol details

### 3.5 Network Layer

**Responsibilities**: Socket management, packet transmission/reception

**UDP Communication** (Discovery):
- Port: 44818 (EtherNet/IP)
- OS-assigned ephemeral source port
- Subnet-directed broadcast
- 3-second response window

**TCP Communication** (Configuration):
- Port: 44818 (EtherNet/IP)
- Session-oriented (RegisterSession/UnregisterSession)
- Timeout: 3 seconds per message
- Explicit message framing

**BootP Server** (Factory-Default Devices):
- Port: 68 (client port, requires admin)
- Receives BootP requests
- Sends unicast/broadcast replies

---

## 4. Key Design Patterns

### 4.1 MVVM Pattern

**Implementation**:
- Views bind to ViewModel properties via `{Binding}`
- Commands execute ViewModel methods
- ViewModels notify Views via `INotifyPropertyChanged`
- No View references in ViewModels (testable)

**Example Binding**:
```xml
<DataGrid ItemsSource="{Binding Devices}"
          SelectedItem="{Binding SelectedDevice, Mode=TwoWay}">
```

### 4.2 Observer Pattern

**Implementation**: `ObservableCollection<T>` for reactive updates

```csharp
// ViewModel
public ObservableCollection<Device> Devices { get; }

// Service adds device
Devices.Add(newDevice); // UI updates automatically
```

### 4.3 Command Pattern

**Implementation**: `ICommand` with `RelayCommand`

```csharp
public ICommand ConfigureDeviceCommand { get; }

ConfigureDeviceCommand = new RelayCommand(
    execute: _ => ConfigureDevice(),
    canExecute: _ => SelectedDevice != null
);
```

### 4.4 Dependency Injection

**Implementation**: Constructor injection (manual, no IoC container)

```csharp
public MainWindowViewModel()
{
    _activityLogger = new ActivityLogger(Log.Logger);
    _networkService = new NetworkInterfaceService(_activityLogger);
    _discoveryService = new DeviceDiscoveryService(_activityLogger);
}
```

**Rationale**: Simplicity for desktop app (no complex DI container needed)

### 4.5 Strategy Pattern

**Implementation**: Operating mode switching (EtherNet/IP vs. BootP/DHCP)

```csharp
public enum OperatingMode
{
    EtherNetIP,
    BootP_DHCP
}

public OperatingMode CurrentMode
{
    get => _currentMode;
    set
    {
        if (SetProperty(ref _currentMode, value))
        {
            OnOperatingModeChanged();
        }
    }
}
```

---

## 5. Component Architecture

### 5.1 Device Discovery Flow

```
[User clicks "Scan Now"]
        │
        ▼
[MainWindowViewModel.ScanNowAsync()]
        │
        ▼
[DeviceDiscoveryService.ScanAsync()]
        │
        ├──> [CIPListIdentity.BuildRequest()]
        │    └──> 24-byte encapsulation header
        │         + 18-byte List Identity payload
        │
        ├──> [UdpClient.SendAsync(broadcast, 44818)]
        │
        ├──> [Task.Delay(3000)] // Response collection window
        │
        ├──> [Parse responses loop]
        │    │
        │    ├──> [CIPListIdentity.ParseResponse()]
        │    │    └──> Extract: MAC, IP, Vendor, Model
        │    │
        │    ├──> [VendorMappingService.GetVendorName()]
        │    │    └──> Map vendor ID → name
        │    │
        │    └──> [Create Device object]
        │
        └──> [Update ViewModel.Devices collection]
                │
                ▼
        [DataGrid updates via binding]
```

### 5.2 Configuration Write Flow

```
[User double-clicks device]
        │
        ▼
[ConfigurationDialog shows]
        │
        ▼
[User enters IP/subnet/gateway, clicks Apply]
        │
        ▼
[ConfigurationViewModel validates]
        │
        ▼
[ConfirmationDialog shows current vs. new]
        │
        ▼
[User confirms changes]
        │
        ▼
[ConfigurationWriteService.WriteConfigurationAsync()]
        │
        ├──> [TCP connection to device:44818]
        │
        ├──> [RegisterSession (Command 0x0065)]
        │    └──> Receive Session Handle
        │
        ├──> [Write IP Address (Attribute 5)]
        │    ├──> SetAttributeSingleMessage.BuildSetIPAddressRequest()
        │    ├──> Wrap in SendRRData (0x006F) with CPF
        │    ├──> TCP send
        │    └──> Parse response, check status
        │
        ├──> [100ms delay]
        │
        ├──> [Write Subnet Mask (Attribute 6)]
        │
        ├──> [Write Gateway (Attribute 7)] // If provided
        │
        ├──> [Write Hostname (Attribute 8)] // If provided
        │
        ├──> [Write DNS (Attribute 10)] // If provided
        │
        ├──> [UnregisterSession (Command 0x0066)]
        │
        └──> [Close TCP connection]
                │
                ▼
        [Result Dialog shows success/failure]
```

**See**: [Phase 5 Guide](../04-IMPLEMENTATION/PHASE5_IMPLEMENTATION.md) for ODVA-compliant implementation details

---

## 6. Data Flow

### 6.1 Data Binding Architecture

```
Model (Device)
    ↕ (Property changes)
ViewModel (MainWindowViewModel)
    ↕ (INotifyPropertyChanged)
View (MainWindow.xaml)
    ↕ (User interaction)
```

**Example**:
```csharp
// Model
public class Device : INotifyPropertyChanged
{
    private string _status = "OK";
    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }
}

// ViewModel
public ObservableCollection<Device> Devices { get; } = new();

// View
<DataGrid ItemsSource="{Binding Devices}">
```

### 6.2 Command Flow

```
User Interaction (Button Click)
        │
        ▼
Command Binding ({Binding ScanNowCommand})
        │
        ▼
CanExecute Check (optional)
        │
        ▼
Execute Method (ViewModel)
        │
        ▼
Service Call (async)
        │
        ▼
Update ViewModel Properties
        │
        ▼
PropertyChanged Events
        │
        ▼
View Updates (Data Binding)
```

---

## 7. Network Architecture

### 7.1 Discovery (UDP Broadcast)

```
Application
    │
    ├──> Bind UDP socket to adapter IP : ephemeral port
    │
    ├──> Calculate subnet broadcast address
    │    (e.g., 192.168.1.0/24 → 192.168.1.255)
    │
    ├──> Build CIP List Identity request
    │
    ├──> SendTo(broadcast_address, 44818)
    │
    ├──> ReceiveFrom() loop (3 seconds)
    │
    └──> Close socket
```

**Key Decision**: Subnet-only broadcast (not 255.255.255.255)
- **Rationale**: RSLinx compatibility, network isolation
- **See**: [Phase 2 Guide](../04-IMPLEMENTATION/PHASE2_DEVICE_DISCOVERY.md)

### 7.2 Configuration (TCP Connection)

```
Application
    │
    ├──> TcpClient.ConnectAsync(device_ip, 44818)
    │
    ├──> RegisterSession → Session Handle
    │
    ├──> SendRRData (multiple attribute writes)
    │    └──> Each with 3-second timeout
    │
    ├──> UnregisterSession
    │
    └──> Close connection
```

**Session Management**: ODVA-compliant lifecycle
- **See**: [ODVA Compliance](../05-COMPLIANCE/ODVA_COMPLIANCE.md)

### 7.3 BootP Server (UDP Port 68)

```
Application (as Administrator)
    │
    ├──> Bind UDP socket to 0.0.0.0:68
    │
    ├──> ReceiveFrom() loop (background thread)
    │
    ├──> On BootP request:
    │    ├──> Parse request (XID, MAC, FLAGS)
    │    ├──> Display to user
    │    ├──> User assigns IP
    │    ├──> Build BootP OFFER
    │    └──> SendTo (unicast or broadcast based on FLAGS)
    │
    └──> Close socket on mode switch
```

---

## 8. Security Architecture

### 8.1 Privilege Model

| Mode | Privilege Required | Port Usage | Rationale |
|------|-------------------|------------|-----------|
| EtherNet/IP | Standard User | UDP/TCP ephemeral → 44818 | Non-privileged ports |
| BootP/DHCP | Administrator | UDP 68 (listen) | Port < 1024 requires elevation |

### 8.2 Security Features

**Input Validation**:
- IP octet validation (0-255)
- Hostname validation (alphanumeric, hyphens, underscores)
- Subnet validation (gateway on same subnet)

**Network Isolation**:
- No internet connectivity
- Local subnet only (no routing)
- No telemetry or analytics

**Error Handling**:
- CIP status code validation
- Socket timeout enforcement
- Sender Context validation (ODVA requirement)

---

## 9. Performance Architecture

### 9.1 Performance Targets

| Operation | Target | Achieved | Notes |
|-----------|--------|----------|-------|
| Startup | < 3s | ~2s | Cold start |
| Discovery Scan | < 3s | 3s | Per subnet |
| Config Write | < 10s | 5-8s | 5 attributes |
| Auto-Browse | < 3s | 3s | Non-blocking |
| Table Sort | < 100ms | < 50ms | 256 devices |
| Memory | < 200MB | ~100MB | Normal operation |

### 9.2 Async Design

**All I/O operations use async/await**:
```csharp
// Discovery
public async Task<List<Device>> ScanAsync(...)

// Configuration
public async Task<ConfigurationWriteResult> WriteConfigurationAsync(...)

// Network reads
await stream.ReadAsync(buffer, cancellationToken);
```

**Benefits**:
- UI remains responsive during network operations
- No blocking calls on UI thread
- Cancellation support via `CancellationToken`

### 9.3 Collection Management

**Observable Collections**: Auto-update UI
```csharp
public ObservableCollection<Device> Devices { get; }

// Background thread
Application.Current.Dispatcher.Invoke(() =>
{
    Devices.Add(newDevice);
});
```

**Stale Device Removal**: Automatic cleanup (Phase 7)
- Tracks `LastSeen` timestamp
- Increments `MissedScans` counter
- Removes after 3 consecutive misses

---

## 10. Testing Architecture

### 10.1 Test Strategy

**Unit Tests** (xUnit):
- Service layer logic
- Protocol message builders/parsers
- Validation logic
- ViewModels (with mocked services)

**Integration Tests**:
- End-to-end configuration flow
- BootP server lifecycle
- Auto-browse service timing

**Manual Testing**:
- UI workflows
- Real device communication
- Error scenarios

### 10.2 Testability Design

**Dependency Injection**: Services mocked for tests
```csharp
// Production
var logger = new ActivityLogger(Log.Logger);

// Test
var mockLogger = new Mock<IActivityLogger>();
```

**Isolated Business Logic**: Services testable without UI
```csharp
[Fact]
public void SetAttributeSingleMessage_BuildsCorrectPacket()
{
    var packet = SetAttributeSingleMessage.BuildSetIPAddressRequest(
        IPAddress.Parse("192.168.1.10"),
        IPAddress.Parse("192.168.1.5")
    );

    Assert.Equal(expected_length, packet.Length);
    Assert.Equal(0x006F, BitConverter.ToUInt16(packet, 0)); // SendRRData
}
```

---

## Appendices

### Appendix A: File Structure

```
src/
├── App.xaml / App.xaml.cs
├── Core/
│   ├── BootP/
│   │   ├── BootPPacket.cs
│   │   ├── BootPServer.cs
│   │   └── DHCPOptions.cs
│   ├── CIP/
│   │   ├── CIPEncapsulation.cs
│   │   ├── CIPListIdentity.cs
│   │   ├── CIPStatusCodes.cs
│   │   └── SetAttributeSingleMessage.cs
│   └── Network/
│       └── SubnetCalculator.cs
├── Converters/
│   └── RowNumberConverter.cs
├── Models/
│   ├── Device.cs
│   ├── DeviceConfiguration.cs
│   ├── NetworkAdapterInfo.cs
│   └── OperatingMode.cs
├── Resources/
│   └── Help/
│       ├── UserManual.html
│       ├── CIPProtocolReference.html
│       └── BootPReference.html
├── Services/
│   ├── ActivityLogger.cs
│   ├── ApplicationSettingsService.cs
│   ├── AutoBrowseService.cs
│   ├── BootPServer.cs (service wrapper)
│   ├── ConfigurationWriteService.cs
│   ├── DeviceDiscoveryService.cs
│   ├── NetworkInterfaceService.cs
│   ├── PrivilegeDetectionService.cs
│   └── VendorMappingService.cs
├── ViewModels/
│   ├── ActivityLogViewModel.cs
│   ├── BootPConfigurationViewModel.cs
│   ├── ConfigurationViewModel.cs
│   ├── HelpViewModel.cs
│   ├── MainWindowViewModel.cs
│   ├── RelayCommand.cs
│   └── ViewModelBase.cs
└── Views/
    ├── ActivityLogWindow.xaml[.cs]
    ├── BootPConfigurationDialog.xaml[.cs]
    ├── ConfigurationDialog.xaml[.cs]
    ├── ConfigurationResultDialog.xaml[.cs]
    ├── ConfirmationDialog.xaml[.cs]
    ├── HelpWindow.xaml[.cs]
    ├── MainWindow.xaml[.cs]
    └── ProgressDialog.xaml[.cs]
```

### Appendix B: Key Metrics

| Metric | Value |
|--------|-------|
| Total Source Lines | ~15,000 |
| Classes | ~40 |
| Services | 9 |
| ViewModels | 5 |
| Views | 8 |
| Protocol Message Types | 6 |
| Supported Vendors | 70+ |

### Appendix C: Related Documentation

- **[Phase 1: Core Infrastructure](../04-IMPLEMENTATION/PHASE1_CORE_INFRASTRUCTURE.md)** - MVVM setup, services foundation
- **[Phase 2: Device Discovery](../04-IMPLEMENTATION/PHASE2_DEVICE_DISCOVERY.md)** - CIP List Identity implementation
- **[Phase 5: CIP Configuration](../04-IMPLEMENTATION/PHASE5_IMPLEMENTATION.md)** - ODVA-compliant configuration
- **[ODVA Compliance](../05-COMPLIANCE/ODVA_COMPLIANCE.md)** - Standards adherence details
- **[Protocol Reference](PROTOCOL_REFERENCE.md)** - Complete protocol specifications

---

**Document Maintained By**: Architecture Team
**Review Cycle**: After each major phase completion
**Questions**: See [Developer Guide](../07-DEVELOPMENT/DEVELOPER_GUIDE.md)
