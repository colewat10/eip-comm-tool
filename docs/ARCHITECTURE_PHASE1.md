# EtherNet/IP Commissioning Tool
## Phase 1 Architecture Documentation: Core Infrastructure

### Table of Contents
1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Project Structure](#project-structure)
4. [Core Components](#core-components)
5. [Service Layer](#service-layer)
6. [MVVM Infrastructure](#mvvm-infrastructure)
7. [User Interface](#user-interface)
8. [Configuration Management](#configuration-management)
9. [Testing Strategy](#testing-strategy)
10. [Development Guidelines](#development-guidelines)

---

## 1. Executive Summary

Phase 1 establishes the foundational infrastructure for the EtherNet/IP Commissioning Tool, a Windows desktop application built with .NET 8 and WPF. This phase implements the core application shell, service layer, MVVM pattern infrastructure, and basic UI framework that will support all subsequent development phases.

**Key Deliverables:**
- Complete .NET 8 WPF solution structure
- Main application window with menu bar, toolbar, and status bar
- Network interface enumeration and selection
- Privilege detection for administrator-only features
- Structured logging system with activity tracking
- Application settings persistence
- MVVM pattern implementation with base classes

**Technology Stack:**
- Framework: .NET 8 (C# 12)
- UI Framework: WPF (Windows Presentation Foundation)
- Architecture Pattern: MVVM (Model-View-ViewModel)
- Logging: Serilog with file and debug sinks
- Testing: xUnit, Moq, FluentAssertions

---

## 2. Architecture Overview

### 2.1 High-Level Architecture

The application follows a layered architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  (Views - XAML, ViewModels, UI Controllers)             │
└─────────────────────────────────────────────────────────┘
                          │
┌─────────────────────────────────────────────────────────┐
│                     Service Layer                        │
│  (Business Logic, Protocol Implementation, Network)     │
└─────────────────────────────────────────────────────────┘
                          │
┌─────────────────────────────────────────────────────────┐
│                      Data Layer                          │
│  (Models, Configuration, Settings Persistence)          │
└─────────────────────────────────────────────────────────┘
                          │
┌─────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                    │
│  (Logging, I/O, Network Stack, Windows APIs)            │
└─────────────────────────────────────────────────────────┘
```

### 2.2 Design Principles

1. **MVVM Pattern**: Strict separation between UI (Views), presentation logic (ViewModels), and data (Models)
2. **Dependency Injection**: Services injected via constructor for testability
3. **Single Responsibility**: Each class has one clearly defined purpose
4. **Observable Pattern**: Property change notification for reactive UI updates
5. **Command Pattern**: UI actions implemented as commands for loose coupling
6. **Fail-Safe Design**: Graceful degradation when features unavailable (e.g., no admin rights)

### 2.3 Key Architectural Decisions

| Decision | Rationale | Alternatives Considered |
|----------|-----------|------------------------|
| WPF over WinForms | Modern XAML-based UI, better MVVM support, data binding | WinForms (rejected: legacy), Avalonia (rejected: less mature) |
| Serilog for logging | Structured logging, multiple sinks, rich ecosystem | NLog (similar), Log4Net (older) |
| JSON for settings | Human-readable, .NET native support, extensible | XML (verbose), Binary (not readable) |
| Fixed window size (MVP) | Simplifies layout, matches industrial tool standards | Resizable (deferred to post-MVP) |

---

## 3. Project Structure

### 3.1 Solution Organization

```
eip-comm-tool/
├── src/
│   └── EtherNetIPTool/              # Main application project
│       ├── Core/                     # Protocol implementations (Phase 2+)
│       │   ├── CIP/                  # CIP protocol
│       │   ├── BootP/                # BootP/DHCP server
│       │   └── Network/              # Network discovery
│       ├── Models/                   # Data models
│       ├── ViewModels/               # MVVM ViewModels
│       │   ├── ViewModelBase.cs      # Base ViewModel class
│       │   ├── RelayCommand.cs       # Command implementations
│       │   └── MainWindowViewModel.cs # Main window ViewModel
│       ├── Views/                    # WPF Views (XAML)
│       │   ├── MainWindow.xaml       # Main application window
│       │   └── MainWindow.xaml.cs    # Code-behind
│       ├── Services/                 # Service layer
│       │   ├── ActivityLogger.cs     # Activity logging service
│       │   ├── NetworkInterfaceService.cs # NIC enumeration
│       │   ├── PrivilegeDetectionService.cs # Admin rights check
│       │   └── ApplicationSettingsService.cs # Settings persistence
│       ├── Resources/                # Application resources
│       │   ├── Icons/                # Application icons
│       │   ├── Help/                 # Help documentation (Phase 8)
│       │   └── Styles/               # WPF resource dictionaries
│       │       ├── Colors.xaml       # Color scheme
│       │       ├── Buttons.xaml      # Button styles
│       │       └── TextBoxes.xaml    # TextBox styles
│       ├── App.xaml                  # Application definition
│       ├── App.xaml.cs               # Application startup
│       └── EtherNetIPTool.csproj     # Project file
├── tests/
│   └── EtherNetIPTool.Tests/         # Unit test project
│       └── EtherNetIPTool.Tests.csproj
├── docs/
│   ├── PRD.md                        # Product Requirements Document
│   ├── ARCHITECTURE_PHASE1.md        # This document
│   └── ...
├── agents/                           # AI agent profiles
└── EtherNetIPTool.sln               # Solution file
```

### 3.2 File Organization Rationale

- **Core/**: Protocol-specific implementations isolated for maintainability
- **Services/**: Business logic layer, testable without UI dependencies
- **ViewModels/**: Presentation logic, no direct UI dependencies
- **Views/**: Pure XAML UI definitions with minimal code-behind
- **Models/**: Data structures, Plain Old CLR Objects (POCOs)
- **Resources/**: Non-code assets organized by type

---

## 4. Core Components

### 4.1 Application Entry Point

**File**: `src/EtherNetIPTool/App.xaml.cs`

The `App` class manages application lifecycle and initializes core services.

**Key Responsibilities:**
- Initialize Serilog logging on startup
- Log system information (OS, CLR version, privilege level)
- Handle unhandled exceptions globally
- Cleanup resources on exit

**Logging Configuration:**
```csharp
// Logs to: %LocalAppData%\EtherNetIPTool\Logs\app-yyyyMMdd.log
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Debug()                  // Visual Studio Output window
    .WriteTo.File(                    // Rolling daily log files
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)    // Keep 7 days
    .CreateLogger();
```

**Exception Handling:**
- All unhandled exceptions logged to Serilog
- User-friendly error dialog displayed
- Application continues running (exception marked as handled)

### 4.2 Main Window

**Files**:
- `src/EtherNetIPTool/Views/MainWindow.xaml` (UI definition)
- `src/EtherNetIPTool/Views/MainWindow.xaml.cs` (code-behind)
- `src/EtherNetIPTool/ViewModels/MainWindowViewModel.cs` (logic)

**UI Specifications (from PRD REQ-5.1):**
- Fixed size: 1280 x 768 pixels
- Not resizable (MVP constraint)
- Centered on screen at startup
- Segoe UI font, 9pt

**Layout Structure:**
```
┌─────────────────────────────────────────────┐
│ Menu Bar (20px)                             │
├─────────────────────────────────────────────┤
│ Toolbar (40px) - NIC selection, clock       │
├─────────────────────────────────────────────┤
│ Mode Panel (40px) - EIP/BootP, Auto-Browse  │
├─────────────────────────────────────────────┤
│ Device Table Header (20px)                  │
├─────────────────────────────────────────────┤
│                                             │
│ Device Table (480px scrollable)             │
│                                             │
├─────────────────────────────────────────────┤
│ Action Buttons (30px)                       │
├─────────────────────────────────────────────┤
│ Status Bar (20px)                           │
└─────────────────────────────────────────────┘
```

**Menu Structure:**
- **File**: Exit
- **Edit**: Preferences (disabled in Phase 1)
- **Tools**: Activity Log Viewer, Export Device List, Clear Device List
- **View**: Refresh NIC List, Refresh Device Table
- **Help**: User Manual (F1), CIP/BootP References, Troubleshooting, About

---

## 5. Service Layer

### 5.1 ActivityLogger

**File**: `src/EtherNetIPTool/Services/ActivityLogger.cs`

Provides structured logging with categorization for UI display and export.

**Categories (from PRD REQ-3.7-002):**
- `INFO`: General information messages
- `SCAN`: Scan operations
- `DISC`: Device discovery events
- `CONFIG`: Configuration operations
- `CIP`: CIP protocol messages
- `BOOTP`: BootP/DHCP operations
- `ERROR`: Error conditions
- `WARN`: Warning messages

**Key Features:**
```csharp
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogCategory Category { get; set; }
    public string Message { get; set; }
    public string FormattedEntry =>
        $"{Timestamp:HH:mm:ss.fff} [{Category,-6}] {Message}";
}
```

**Thread Safety:**
- All operations protected by lock
- UI updates dispatched to UI thread via Dispatcher

**Performance:**
- In-memory collection limited to 10,000 entries (configurable)
- Oldest entries removed when limit exceeded (FIFO)
- Async log export to prevent UI blocking

**Export Format (UTF-8 text file):**
```
HH:mm:ss.fff [CATEGORY] Message text
HH:mm:ss.fff [CATEGORY] Message text
...
```

### 5.2 NetworkInterfaceService

**File**: `src/EtherNetIPTool/Services/NetworkInterfaceService.cs`

Enumerates and manages network interface adapters.

**Adapter Filtering Criteria (from PRD REQ-4.3.1):**
1. OperationalStatus == Up
2. NetworkInterfaceType == Ethernet, Wireless80211, or GigabitEthernet
3. Has IPv4 unicast address assigned
4. Not loopback (127.0.0.1)

**Data Model:**
```csharp
public class NetworkAdapterInfo
{
    public string Id { get; set; }              // System identifier
    public string Name { get; set; }            // Display name
    public string Description { get; set; }     // Full description
    public IPAddress? IPAddress { get; set; }   // IPv4 address
    public IPAddress? SubnetMask { get; set; }  // Subnet mask
    public PhysicalAddress? MacAddress { get; set; } // MAC address
    public NetworkInterfaceType InterfaceType { get; set; }
    public OperationalStatus Status { get; set; }

    public string DisplayName =>                // For UI dropdown
        $"{Name} - {IPAddress?.ToString() ?? "No IP"}";
}
```

**Auto-Selection Logic:**
1. Check settings for last selected adapter ID
2. If found in current list, restore selection
3. Otherwise, select first available adapter
4. Log selection to activity log

**API Usage:**
```csharp
var service = new NetworkInterfaceService(logger);
var adapters = service.EnumerateAdapters();  // Returns List<NetworkAdapterInfo>
var selected = service.AutoSelectAdapter();  // Returns first suitable adapter
```

### 5.3 PrivilegeDetectionService

**File**: `src/EtherNetIPTool/Services/PrivilegeDetectionService.cs`

Detects if application is running with Administrator privileges.

**Purpose:**
- BootP/DHCP server mode requires Administrator rights (UDP port 68 binding)
- Service provides safe privilege check with fallback to non-admin

**Implementation:**
```csharp
public bool IsRunningAsAdministrator()
{
    try
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;  // Assume non-admin on error (fail-safe)
    }
}
```

**UI Integration:**
- BootP/DHCP mode radio button disabled if not admin
- Tooltip explains privilege requirement
- Status bar shows current privilege level
- Window title shows "[Administrator]" when elevated

### 5.4 ApplicationSettingsService

**File**: `src/EtherNetIPTool/Services/ApplicationSettingsService.cs`

Manages application configuration persistence.

**Storage Location:**
```
%LocalAppData%\EtherNetIPTool\settings.json
```

**Settings Model:**
```csharp
public class ApplicationSettings
{
    // Discovery settings
    public int ScanIntervalSeconds { get; set; } = 5;
    public bool AutoBrowseEnabled { get; set; } = true;
    public int DiscoveryTimeoutMilliseconds { get; set; } = 3000;

    // CIP protocol settings
    public int CipMessageTimeoutMilliseconds { get; set; } = 3000;
    public int InterMessageDelayMilliseconds { get; set; } = 100;
    public int SocketTimeoutMilliseconds { get; set; } = 5000;

    // BootP/DHCP settings
    public int BootPTransactionTimeoutMilliseconds { get; set; } = 10000;
    public bool DisableDhcpAfterAssignment { get; set; } = true;

    // Device management
    public int DeviceRemovalScans { get; set; } = 3;
    public int MaxDeviceListSize { get; set; } = 256;

    // UI settings
    public string? LastSelectedAdapterId { get; set; }
    public bool WindowMaximized { get; set; } = false;
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;

    // Logging
    public bool VerboseLogging { get; set; } = false;
    public int MaxLogEntries { get; set; } = 10000;
}
```

**Validation:**
- Scan interval constrained to 1-60 seconds (REQ-3.3.2-003)
- Timeouts constrained to 1-30 seconds (1-60 for BootP)
- Invalid values clamped to acceptable ranges
- Validation applied on load and before save

**API Usage:**
```csharp
var service = new ApplicationSettingsService(logger);
service.LoadSettings();                    // Load from disk
var settings = service.Settings;           // Access current settings
service.UpdateSetting(s => s.ScanIntervalSeconds = 10); // Modify
service.SaveSettings();                    // Persist to disk
```

---

## 6. MVVM Infrastructure

### 6.1 ViewModelBase

**File**: `src/EtherNetIPTool/ViewModels/ViewModelBase.cs`

Base class for all ViewModels implementing `INotifyPropertyChanged`.

**Key Features:**
```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    // Standard property change notification
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null);

    // Set property with automatic change notification
    protected bool SetProperty<T>(ref T field, T value,
        [CallerMemberName] string? propertyName = null);

    // Set property with callback on change
    protected bool SetProperty<T>(ref T field, T value, Action onChanged,
        [CallerMemberName] string? propertyName = null);
}
```

**Usage Example:**
```csharp
private string _statusText = "Ready";
public string StatusText
{
    get => _statusText;
    set => SetProperty(ref _statusText, value);
}
```

### 6.2 RelayCommand

**File**: `src/EtherNetIPTool/ViewModels/RelayCommand.cs`

Implements `ICommand` for MVVM command binding.

**Variants:**
1. **RelayCommand**: Non-generic, object parameter
2. **RelayCommand\<T\>**: Generic with typed parameter
3. **AsyncRelayCommand**: Supports async/await operations

**Key Features:**
- Automatic CanExecute reevaluation via `CommandManager.RequerySuggested`
- Async version prevents concurrent execution
- Strongly-typed generic version for type safety

**Usage Example:**
```csharp
// In ViewModel constructor
RefreshNicListCommand = new RelayCommand(
    _ => RefreshNetworkAdapters(),           // Execute action
    _ => !_isRefreshing);                    // CanExecute predicate

// Async version
ScanDevicesCommand = new AsyncRelayCommand(
    async _ => await ScanForDevicesAsync());

// In XAML
<Button Content="Refresh" Command="{Binding RefreshNicListCommand}"/>
```

### 6.3 MainWindowViewModel

**File**: `src/EtherNetIPTool/ViewModels/MainWindowViewModel.cs`

Primary ViewModel for main application window.

**Key Responsibilities:**
- Manage network adapter collection and selection
- Update status bar and clock display
- Handle menu and toolbar commands
- Coordinate service interactions
- Save/restore UI state

**Properties:**
```csharp
public ObservableCollection<NetworkAdapterInfo> NetworkAdapters { get; }
public NetworkAdapterInfo? SelectedAdapter { get; set; }
public string StatusText { get; set; }
public string CurrentTime { get; set; }
public bool IsAdministrator { get; set; }
public string WindowTitle { get; }
public string PrivilegeStatus { get; }
```

**Commands:**
```csharp
public ICommand RefreshNicListCommand { get; }
public ICommand ExitApplicationCommand { get; }
public ICommand ShowActivityLogCommand { get; }
public ICommand ShowAboutCommand { get; }
```

**Initialization Sequence:**
1. Construct and inject services
2. Check privilege level
3. Load application settings
4. Enumerate network adapters
5. Auto-select adapter (restore last or first available)
6. Start clock update timer (1-second interval)

---

## 7. User Interface

### 7.1 Color Scheme

**File**: `src/EtherNetIPTool/Resources/Styles/Colors.xaml`

Industrial-grade color palette (from PRD REQ-5.1):

| Element | Color | Hex | Usage |
|---------|-------|-----|-------|
| Background | Light Gray | #F0F0F0 | Main window background |
| Section Header | Light Gray | #E8E8E8 | Panel headers |
| Table Header | Medium Gray | #D0D0D0 | DataGrid column headers |
| Alternate Row | Off-White | #F8F8F8 | DataGrid alternating rows |
| Link-Local | Light Yellow | #FFFACD | 169.254.x.x IP warning |
| Conflict | Light Red | #FFE6E6 | Duplicate IP warning |
| Text | Black | #000000 | Primary text |
| Disabled Text | Gray | #808080 | Disabled controls |
| Error Text | Red | #FF0000 | Error messages |
| Border | Gray | #D0D0D0 | Control borders |

**Design Philosophy:**
- Prioritize information density over aesthetics
- High contrast for readability in industrial settings
- Consistent with RSLinx, Studio 5000 tool appearance
- Color coding for status (yellow = warning, red = error)

### 7.2 Typography

**Font Specifications (from PRD REQ-5.1):**
- Primary: Segoe UI, 9pt (body text, controls)
- Headers: Segoe UI, 9pt Bold
- Status Bar: Segoe UI, 8pt
- Table: Segoe UI, 9pt (fixed-width for alignment)

**Rationale:**
- Segoe UI: Standard Windows system font, high readability
- 9pt size: Dense display, suitable for data-heavy tables
- Consistent sizing: Predictable layout, easy scanning

### 7.3 Control Styles

**Button Style** (`Resources/Styles/Buttons.xaml`):
- Light gray background (#E1E1E1)
- 1px solid border (#ADADAD)
- Hover: Light blue highlight (#BEE6FD)
- Pressed: Blue background (#0078D7), white text
- Disabled: Gray-on-gray low contrast

**TextBox Style** (`Resources/Styles/TextBoxes.xaml`):
- 1px border, 3px padding
- Focus: 2px blue border (#0078D7)
- Numeric variant: Right-aligned, max 3 characters (for IP octets)

### 7.4 Layout Guidelines

**Fixed Dimensions:**
- Window: 1280 x 768 px (not resizable in MVP)
- Menu bar: 20px height
- Toolbar: 40px height
- Status bar: 20px height
- DataGrid row: 20px height (high density)

**Spacing:**
- Section margin: 10px
- Control spacing: 5-10px
- Panel padding: 10px

**Alignment:**
- Labels: Left-aligned
- Numeric inputs: Right-aligned
- Status text: Left-aligned
- Clock: Right-aligned

---

## 8. Configuration Management

### 8.1 Settings File Format

**Location**: `%LocalAppData%\EtherNetIPTool\settings.json`

**Example:**
```json
{
  "ScanIntervalSeconds": 5,
  "AutoBrowseEnabled": true,
  "DiscoveryTimeoutMilliseconds": 3000,
  "CipMessageTimeoutMilliseconds": 3000,
  "InterMessageDelayMilliseconds": 100,
  "SocketTimeoutMilliseconds": 5000,
  "BootPTransactionTimeoutMilliseconds": 10000,
  "DisableDhcpAfterAssignment": true,
  "DeviceRemovalScans": 3,
  "MaxDeviceListSize": 256,
  "LastSelectedAdapterId": "{GUID}",
  "WindowMaximized": false,
  "WindowLeft": 0,
  "WindowTop": 0,
  "VerboseLogging": false,
  "MaxLogEntries": 10000
}
```

### 8.2 Validation Rules

| Setting | Min | Max | Default | Validation |
|---------|-----|-----|---------|------------|
| ScanIntervalSeconds | 1 | 60 | 5 | Clamp to range |
| DiscoveryTimeoutMilliseconds | 1000 | 30000 | 3000 | Clamp to range |
| DeviceRemovalScans | 1 | 10 | 3 | Clamp to range |
| MaxDeviceListSize | 10 | 1000 | 256 | Clamp to range |
| MaxLogEntries | 100 | 50000 | 10000 | Clamp to range |

### 8.3 Settings Lifecycle

1. **Startup**:
   - `ApplicationSettingsService.LoadSettings()` called in ViewModel constructor
   - If file missing, create with defaults
   - Validate and clamp all values

2. **Runtime**:
   - Settings updated via `UpdateSetting()` fluent method
   - Changes saved immediately to disk

3. **Shutdown**:
   - Final save not required (already persisted)

---

## 9. Testing Strategy

### 9.1 Unit Testing Setup

**Framework**: xUnit with Moq and FluentAssertions

**Test Project**: `tests/EtherNetIPTool.Tests/EtherNetIPTool.Tests.csproj`

**Dependencies:**
```xml
<PackageReference Include="xUnit" Version="2.6.6" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

### 9.2 Test Coverage Goals

**Phase 1 Coverage Targets:**
- Services: 80% line coverage
- ViewModels: 70% line coverage
- MVVM Infrastructure: 90% line coverage

**Priority Test Areas:**
1. NetworkInterfaceService adapter filtering logic
2. ApplicationSettingsService validation and persistence
3. ActivityLogger thread safety and export
4. PrivilegeDetectionService privilege detection
5. RelayCommand CanExecute logic

### 9.3 Example Test Structure

```csharp
public class NetworkInterfaceServiceTests
{
    private readonly Mock<ActivityLogger> _mockLogger;
    private readonly NetworkInterfaceService _service;

    public NetworkInterfaceServiceTests()
    {
        _mockLogger = new Mock<ActivityLogger>(Mock.Of<ILogger>());
        _service = new NetworkInterfaceService(_mockLogger.Object);
    }

    [Fact]
    public void EnumerateAdapters_ShouldFilterLoopbackAdapters()
    {
        // Arrange
        // Act
        var adapters = _service.EnumerateAdapters();

        // Assert
        adapters.Should().NotContain(a =>
            IPAddress.IsLoopback(a.IPAddress));
    }

    [Fact]
    public void EnumerateAdapters_ShouldOnlyIncludeOperationalAdapters()
    {
        // Arrange
        // Act
        var adapters = _service.EnumerateAdapters();

        // Assert
        adapters.Should().OnlyContain(a =>
            a.Status == OperationalStatus.Up);
    }
}
```

---

## 10. Development Guidelines

### 10.1 Coding Standards

**C# Conventions:**
- Follow .NET naming conventions (PascalCase, camelCase)
- Use C# 12 features (primary constructors, file-scoped namespaces, etc.)
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- XML documentation comments on all public APIs

**MVVM Guidelines:**
1. **Views** (XAML):
   - Pure declarative UI, no logic in code-behind
   - Data binding for all dynamic content
   - Commands for all user actions

2. **ViewModels**:
   - No UI dependencies (no `MessageBox`, no `Window` references)
   - All properties implement `INotifyPropertyChanged`
   - Commands for user actions, not event handlers
   - Services injected via constructor

3. **Models**:
   - Plain Old CLR Objects (POCOs)
   - No UI or business logic
   - Immutable where possible

**Service Layer:**
- Constructor dependency injection
- Async/await for I/O operations
- Thread-safe for multi-threaded operations
- Comprehensive error handling

### 10.2 Error Handling

**Exception Strategy:**
1. **Catch Specific Exceptions**: Don't catch generic `Exception`
2. **Log All Errors**: Use ActivityLogger for user-facing errors
3. **Graceful Degradation**: Continue operation when possible
4. **User-Friendly Messages**: Translate technical errors to actionable messages

**Example:**
```csharp
try
{
    var adapters = _networkService.EnumerateAdapters();
}
catch (NetworkInformationException ex)
{
    _activityLogger.LogError("Failed to enumerate network adapters", ex);
    StatusText = "Error: Unable to access network interfaces";
    // Application continues, user can retry
}
```

### 10.3 Logging Guidelines

**Log Levels:**
- `INFO`: Normal operations (adapter selection, settings loaded)
- `WARN`: Unusual but non-critical conditions (no adapters found)
- `ERROR`: Failures that prevent operation (file I/O errors)

**Log Message Format:**
```csharp
// Good: Structured, actionable
_logger.LogInfo($"Selected adapter: {adapter.Name} ({adapter.IPAddress})");

// Bad: Vague, not actionable
_logger.LogInfo("Something happened");
```

### 10.4 Performance Considerations

**Phase 1 Performance Targets:**
- Application startup: < 3 seconds
- NIC enumeration: < 500ms
- Settings load/save: < 100ms
- UI responsiveness: No blocking operations on UI thread

**Optimization Techniques:**
- Use async/await for I/O operations
- Limit log entry collection size (prevent memory issues)
- Debounce rapid property changes
- Cache expensive computations

### 10.5 Documentation Requirements

**Code Documentation:**
- XML comments on all public classes and methods
- Inline comments for complex logic
- Reference PRD requirements in comments (e.g., `// REQ-3.1-001`)

**Commit Messages:**
```
[Phase 1] Add network interface enumeration service

- Implement NetworkInterfaceService with adapter filtering
- Filter criteria: Up, Ethernet/Wireless, IPv4, not loopback
- Auto-selection logic: restore last or select first
- Satisfies REQ-3.1-001 through REQ-3.1-005
```

---

## Appendix A: File Manifest

### Core Application Files
- `EtherNetIPTool.sln` - Solution file
- `src/EtherNetIPTool/EtherNetIPTool.csproj` - Project file
- `src/EtherNetIPTool/App.xaml` - Application definition
- `src/EtherNetIPTool/App.xaml.cs` - Application entry point

### Services (Phase 1 Complete)
- `src/EtherNetIPTool/Services/ActivityLogger.cs`
- `src/EtherNetIPTool/Services/NetworkInterfaceService.cs`
- `src/EtherNetIPTool/Services/PrivilegeDetectionService.cs`
- `src/EtherNetIPTool/Services/ApplicationSettingsService.cs`

### MVVM Infrastructure
- `src/EtherNetIPTool/ViewModels/ViewModelBase.cs`
- `src/EtherNetIPTool/ViewModels/RelayCommand.cs`
- `src/EtherNetIPTool/ViewModels/MainWindowViewModel.cs`

### Views
- `src/EtherNetIPTool/Views/MainWindow.xaml`
- `src/EtherNetIPTool/Views/MainWindow.xaml.cs`

### Resources
- `src/EtherNetIPTool/Resources/Styles/Colors.xaml`
- `src/EtherNetIPTool/Resources/Styles/Buttons.xaml`
- `src/EtherNetIPTool/Resources/Styles/TextBoxes.xaml`

### Tests
- `tests/EtherNetIPTool.Tests/EtherNetIPTool.Tests.csproj`

### Documentation
- `docs/PRD.md` - Product Requirements Document
- `docs/ARCHITECTURE_PHASE1.md` - This document
- `agents/*.md` - AI agent profiles

---

## Appendix B: Dependencies

### NuGet Packages (Main Project)
```xml
<PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.Debug" Version="2.0.0" />
```

### NuGet Packages (Test Project)
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
<PackageReference Include="xUnit" Version="2.6.6" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

---

## Appendix C: Next Steps (Phase 2 Preview)

Phase 2 will build upon this infrastructure to implement:

1. **CIP Protocol Implementation**
   - List Identity packet builder
   - UDP broadcast socket configuration
   - Response parser for device information
   - Vendor ID to name mapping

2. **Device Discovery**
   - Background discovery service
   - Device list management
   - ARP table lookup for MAC addresses

3. **Data Models**
   - Device model with EtherNet/IP attributes
   - Configuration model for IP settings

Phase 1 provides the foundation with:
- ✅ Service layer architecture
- ✅ MVVM infrastructure
- ✅ Logging system
- ✅ Network interface management
- ✅ Settings persistence
- ✅ Main UI shell

---

**Document Version**: 1.0
**Last Updated**: 2025-10-29
**Phase**: 1 - Core Infrastructure
**Status**: Complete
