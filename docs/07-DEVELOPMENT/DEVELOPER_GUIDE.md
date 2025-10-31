# Developer Guide - EtherNet/IP Commissioning Tool

**Document Version:** 1.0
**Last Updated:** 2025-10-31
**Audience:** Developers, Contributors

---

## Quick Start for Developers

### Prerequisites

- Windows 10 (1809+) or Windows 11
- Visual Studio 2022 (Community or better) OR VS Code with C# extension
- .NET 8 SDK
- Git

### Clone and Build

```bash
# Clone repository
git clone <repository-url>
cd eip-comm-tool

# Restore packages
dotnet restore

# Build solution
dotnet build

# Run application
dotnet run --project src/EtherNetIPTool.csproj

# Run tests
dotnet test
```

**Build time**: ~30 seconds
**First run**: Application starts in ~2 seconds

---

## Development Environment Setup

### Visual Studio 2022

1. Open `EtherNetIPTool.sln`
2. Set `EtherNetIPTool` as startup project
3. Build → Build Solution (Ctrl+Shift+B)
4. Debug → Start Debugging (F5)

**Recommended Extensions**:
- ReSharper or CodeMaid (code cleanup)
- XAML Styler (XAML formatting)
- Git Extensions

### VS Code

1. Open root folder
2. Install recommended extensions:
   - C# for Visual Studio Code
   - .NET Core Test Explorer
   - XAML (by Microsoft)

3. Press F5 to start debugging

**Configuration** (`.vscode/launch.json`):
```json
{
    "name": ".NET Core Launch (WPF)",
    "type": "coreclr",
    "request": "launch",
    "program": "${workspaceFolder}/src/bin/Debug/net8.0-windows/EtherNetIPTool.exe"
}
```

---

## Project Structure

```
eip-comm-tool/
├── src/                           # Application source
│   ├── Core/                      # Protocol implementations
│   │   ├── BootP/                 # BootP/DHCP protocol
│   │   ├── CIP/                   # CIP protocol
│   │   └── Network/               # Network utilities
│   ├── Models/                    # Data models
│   ├── ViewModels/                # MVVM ViewModels
│   ├── Views/                     # WPF Views (XAML)
│   ├── Services/                  # Business logic services
│   ├── Resources/                 # Images, help files
│   └── Converters/                # WPF value converters
├── tests/                         # Unit tests
├── docs/                          # Documentation
├── scripts/                       # Utility scripts
└── agents/                        # AI agent profiles
```

**File Locations**:
- Main window: `src/Views/MainWindow.xaml`
- Primary ViewModel: `src/ViewModels/MainWindowViewModel.cs`
- Discovery service: `src/Services/DeviceDiscoveryService.cs`
- Configuration service: `src/Services/ConfigurationWriteService.cs`

---

## Coding Standards

### C# Style

**Follow Microsoft C# Coding Conventions**:
- PascalCase for public members
- camelCase for private fields (with `_` prefix)
- Explicit access modifiers
- `var` for obvious types only

**Example**:
```csharp
public class DeviceDiscoveryService
{
    private readonly ActivityLogger _logger;
    private UdpClient? _udpClient;

    public async Task<List<Device>> ScanAsync(
        IPAddress adapterIP,
        IPAddress subnetMask,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### XAML Style

- 4-space indentation
- Attributes on separate lines for complex elements
- Data binding over code-behind
- Use StaticResource for reusable styles

**Example**:
```xml
<Button Content="Scan Now"
        Command="{Binding ScanNowCommand}"
        Width="100"
        Height="24"
        Margin="0,0,5,0"
        ToolTip="Scan for devices on selected network adapter"/>
```

### Documentation

**XML Comments**: All public APIs
```csharp
/// <summary>
/// Discovers EtherNet/IP devices on the specified network adapter
/// </summary>
/// <param name="adapterIP">IP address of network adapter</param>
/// <param name="subnetMask">Subnet mask for broadcast calculation</param>
/// <returns>List of discovered devices</returns>
public async Task<List<Device>> ScanAsync(...)
```

**Inline Comments**: Complex logic only
```csharp
// REQ-3.3.1-002: Single UDP socket with OS-assigned ephemeral port
_udpClient = new UdpClient(new IPEndPoint(adapterIP, 0));
```

---

## MVVM Architecture

### Pattern Rules

✅ **DO**:
- Keep business logic in ViewModels and Services
- Use data binding for all UI updates
- Implement `INotifyPropertyChanged` for property changes
- Use `ICommand` for user actions
- Inject services via constructor

❌ **DON'T**:
- Put business logic in Views (code-behind)
- Access UI controls from ViewModels
- Use event handlers in code-behind (use commands)
- Create tight coupling between layers

### Creating a New ViewModel

```csharp
public class MyViewModel : ViewModelBase
{
    private readonly MyService _service;
    private string _statusText = "Ready";

    public MyViewModel(MyService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        // Initialize commands
        DoSomethingCommand = new AsyncRelayCommand(_ => DoSomethingAsync());
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand DoSomethingCommand { get; }

    private async Task DoSomethingAsync()
    {
        StatusText = "Working...";
        await _service.DoWorkAsync();
        StatusText = "Done";
    }
}
```

---

## Adding New Features

### Workflow

1. **Review PRD**: Check requirements in [PRD.md](../02-REQUIREMENTS/PRD.md)
2. **Design**: Update architecture docs if needed
3. **Implement**:
   - Create/modify Models
   - Implement Service logic
   - Create/update ViewModel
   - Design View (XAML)
4. **Test**: Write unit tests
5. **Document**: Update implementation guide
6. **Commit**: Clear commit message

### Example: Adding New Device Property

**1. Model** (`src/Models/Device.cs`):
```csharp
private string _serialNumber = string.Empty;
public string SerialNumber
{
    get => _serialNumber;
    set => SetProperty(ref _serialNumber, value);
}
```

**2. Discovery Service** (`src/Services/DeviceDiscoveryService.cs`):
```csharp
// Parse serial number from CIP response
device.SerialNumber = ParseSerialNumber(response);
```

**3. View** (`src/Views/MainWindow.xaml`):
```xml
<DataGridTextColumn Header="Serial Number"
                    Binding="{Binding SerialNumber}"
                    Width="120"/>
```

**4. Test** (`tests/DeviceTests.cs`):
```csharp
[Fact]
public void Device_SerialNumber_RaisesPropertyChanged()
{
    var device = new Device();
    var eventRaised = false;
    device.PropertyChanged += (s, e) =>
    {
        if (e.PropertyName == nameof(Device.SerialNumber))
            eventRaised = true;
    };

    device.SerialNumber = "ABC123";

    Assert.True(eventRaised);
}
```

---

## Testing

### Unit Tests

**Framework**: xUnit, Moq, FluentAssertions

**Test Structure**:
```csharp
public class DeviceDiscoveryServiceTests
{
    [Fact]
    public async Task ScanAsync_ValidAdapter_ReturnsDevices()
    {
        // Arrange
        var mockLogger = new Mock<ActivityLogger>();
        var service = new DeviceDiscoveryService(mockLogger.Object);
        var adapterIP = IPAddress.Parse("192.168.1.100");
        var subnetMask = IPAddress.Parse("255.255.255.0");

        // Act
        var devices = await service.ScanAsync(adapterIP, subnetMask, false, CancellationToken.None);

        // Assert
        devices.Should().NotBeNull();
    }
}
```

**Running Tests**:
```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter DeviceDiscoveryServiceTests

# With coverage
dotnet test /p:CollectCoverage=true
```

### Manual Testing

1. **Discovery**: Click "Scan Now", verify devices appear
2. **Configuration**: Double-click device, enter config, verify writes
3. **BootP Mode**: Run as admin, test factory-default device
4. **Auto-Browse**: Enable, verify continuous scanning
5. **Error Handling**: Disconnect network, verify graceful errors

---

## Debugging

### Breakpoints

**Key Points**:
- `MainWindowViewModel.ScanNowAsync()` - Discovery start
- `DeviceDiscoveryService.ScanAsync()` - Discovery logic
- `ConfigurationWriteService.WriteConfigurationAsync()` - Configuration
- `ActivityLogger.Log()` - All logging calls

### Logging

**View Logs**:
- Application: Tools → Activity Log Viewer
- File: `logs/ethernetip-tool-{date}.log`
- Debug: Visual Studio Output window

**Log Levels**:
```csharp
_logger.LogInfo("Informational message");
_logger.LogScan("Scan operation message");
_logger.LogDiscovery("Device discovery message");
_logger.LogConfig("Configuration message");
_logger.LogCIP("CIP protocol message");
_logger.LogBootP("BootP/DHCP message");
_logger.LogWarning("Warning message");
_logger.LogError("Error message", exception);
```

### Network Debugging

**Wireshark Filters**:
```
# EtherNet/IP traffic
udp.port == 44818 || tcp.port == 44818

# BootP/DHCP
udp.port == 67 || udp.port == 68

# Specific device
ip.addr == 192.168.1.10
```

---

## Common Development Tasks

### Adding a New Service

1. Create `src/Services/MyService.cs`:
```csharp
public class MyService : IDisposable
{
    private readonly ActivityLogger _logger;

    public MyService(ActivityLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DoWorkAsync()
    {
        _logger.LogInfo("Starting work...");
        // Implementation
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}
```

2. Inject in ViewModel:
```csharp
public MainWindowViewModel()
{
    _myService = new MyService(_activityLogger);
}
```

3. Use in command:
```csharp
private async Task ExecuteMyCommand()
{
    await _myService.DoWorkAsync();
}
```

### Adding a New Dialog

1. Create View `src/Views/MyDialog.xaml`
2. Create ViewModel `src/ViewModels/MyDialogViewModel.cs`
3. Show from MainViewModel:
```csharp
private void ShowMyDialog()
{
    var dialog = new Views.MyDialog()
    {
        Owner = Application.Current.MainWindow,
        DataContext = new MyDialogViewModel()
    };

    bool? result = dialog.ShowDialog();
    if (result == true)
    {
        // Handle confirmation
    }
}
```

### Modifying Protocol Implementation

1. Study specification in [Protocol Reference](../03-ARCHITECTURE/PROTOCOL_REFERENCE.md)
2. Update message builder in `src/Core/CIP/` or `src/Core/BootP/`
3. Add unit test for packet structure
4. Test with real device
5. Update protocol documentation

---

## Contributing

### Workflow

1. **Fork** repository
2. **Create branch**: `feature/my-feature` or `fix/my-bug`
3. **Make changes**: Follow coding standards
4. **Test**: Ensure all tests pass
5. **Commit**: Use clear, descriptive messages
6. **Push** to your fork
7. **Create Pull Request**: Describe changes, link issues

### Commit Messages

**Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Maintenance

**Example**:
```
feat(discovery): Add serial number parsing to CIP List Identity

- Extract serial number from Identity Object
- Add SerialNumber property to Device model
- Display in device table

Closes #42
```

### Pull Request Guidelines

**Before Submitting**:
- [ ] All tests pass
- [ ] Code follows style guide
- [ ] XML comments on public APIs
- [ ] Updated relevant documentation
- [ ] No compiler warnings

**PR Description Should Include**:
- What changed and why
- Screenshots for UI changes
- Test coverage information
- Breaking changes (if any)

---

## Build System

### Project File

**Key Settings** (`src/EtherNetIPTool.csproj`):
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

### Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.Xaml.Behaviors.Wpf | MVVM behaviors |
| Serilog | Structured logging |
| Serilog.Sinks.File | File logging |
| Serilog.Sinks.Debug | Debug output |

**Update packages**:
```bash
dotnet list package --outdated
dotnet add package <PackageName> --version <Version>
```

### Build Configurations

- **Debug**: Development, full symbols, no optimizations
- **Release**: Production, optimized, trimmed

```bash
# Debug build
dotnet build --configuration Debug

# Release build
dotnet build --configuration Release
```

---

## Troubleshooting Development Issues

### Build Errors

**"SDK not found"**:
- Install .NET 8 SDK from https://dot net.microsoft.com/download

**"WPF not available"**:
- Ensure `<UseWPF>true</UseWPF>` in .csproj
- Using correct target framework: `net8.0-windows`

### Runtime Errors

**"Devices not found during testing"**:
- Windows Firewall may block UDP 44818
- Run firewall script: `scripts/Configure-FirewallForEtherNetIP.ps1`

**"BootP mode not available"**:
- Requires Administrator privileges
- Right-click → Run as administrator

### Git Issues

**Large files**:
- Don't commit bin/, obj/, or logs/ folders
- Check `.gitignore` is properly configured

---

## Resources

### Internal Documentation

- **[Architecture Guide](../03-ARCHITECTURE/ARCHITECTURE_GUIDE.md)** - System design
- **[Protocol Reference](../03-ARCHITECTURE/PROTOCOL_REFERENCE.md)** - Protocols
- **[Implementation Guides](../04-IMPLEMENTATION/)** - Phase-by-phase development
- **[API Reference](../08-REFERENCE/API_REFERENCE.md)** - Public APIs

### External Resources

- **ODVA Specifications**: https://www.odva.org (CIP, EtherNet/IP)
- **.NET Documentation**: https://docs.microsoft.com/dotnet/
- **WPF Tutorial**: https://docs.microsoft.com/dotnet/desktop/wpf/
- **Serilog**: https://serilog.net/

### Community

- **Issues**: GitHub Issues (bug reports, feature requests)
- **Discussions**: GitHub Discussions (questions, ideas)

---

## FAQ

**Q: How do I add a new CIP attribute?**
A: 1) Add to `SetAttributeSingleMessage`, 2) Update `ConfigurationWriteService`, 3) Add to `DeviceConfiguration` model

**Q: Can I use a different logging framework?**
A: Possible but not recommended. Serilog is integrated throughout. See `ActivityLogger.cs`

**Q: How do I test protocol changes without real devices?**
A: Create mock services or use packet capture replay. See `tests/` folder for examples.

**Q: Where is the main entry point?**
A: `src/App.xaml.cs` - `Application.OnStartup()` method

---

**Ready to contribute?** Start with an easy issue labeled "good first issue" or "help wanted"

**Questions?** Open a GitHub Discussion or contact maintainers
