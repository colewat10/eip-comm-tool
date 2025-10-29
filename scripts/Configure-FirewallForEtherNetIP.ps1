#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Configure Windows Firewall for EtherNet/IP device discovery

.DESCRIPTION
    Creates firewall rules to allow UDP traffic on port 44818 (EtherNet/IP).
    This script must be run as Administrator.

    Creates two rules:
    1. Inbound UDP 44818 - Allow devices to send List Identity responses
    2. Outbound UDP 44818 - Allow application to send broadcasts

.NOTES
    Requirements: Windows PowerShell 5.1 or later, Administrator privileges
    REQ-3.3.1-001: EtherNet/IP discovery requires UDP port 44818
#>

# Script configuration
$RuleName = "EtherNet/IP Discovery"
$Port = 44818
$Protocol = "UDP"

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "EtherNet/IP Firewall Configuration" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "Checking for existing firewall rules..." -ForegroundColor Yellow

# Remove existing rules if they exist
$existingInbound = Get-NetFirewallRule -DisplayName "$RuleName - Inbound" -ErrorAction SilentlyContinue
$existingOutbound = Get-NetFirewallRule -DisplayName "$RuleName - Outbound" -ErrorAction SilentlyContinue

if ($existingInbound) {
    Write-Host "  Removing existing inbound rule..." -ForegroundColor Yellow
    Remove-NetFirewallRule -DisplayName "$RuleName - Inbound"
}

if ($existingOutbound) {
    Write-Host "  Removing existing outbound rule..." -ForegroundColor Yellow
    Remove-NetFirewallRule -DisplayName "$RuleName - Outbound"
}

Write-Host ""
Write-Host "Creating new firewall rules..." -ForegroundColor Green

# Create inbound rule (allow devices to respond)
try {
    New-NetFirewallRule `
        -DisplayName "$RuleName - Inbound" `
        -Description "Allow EtherNet/IP List Identity responses from industrial devices" `
        -Direction Inbound `
        -Protocol UDP `
        -LocalPort $Port `
        -Action Allow `
        -Profile Any `
        -Enabled True | Out-Null

    Write-Host "  [OK] Inbound rule created (UDP $Port)" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Failed to create inbound rule: $_" -ForegroundColor Red
    exit 1
}

# Create outbound rule (allow application to send broadcasts)
try {
    New-NetFirewallRule `
        -DisplayName "$RuleName - Outbound" `
        -Description "Allow EtherNet/IP List Identity broadcast requests" `
        -Direction Outbound `
        -Protocol UDP `
        -RemotePort $Port `
        -Action Allow `
        -Profile Any `
        -Enabled True | Out-Null

    Write-Host "  [OK] Outbound rule created (UDP $Port)" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Failed to create outbound rule: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Configuration Complete!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Firewall rules created successfully:" -ForegroundColor Green
Write-Host "  - Inbound UDP $Port (receive device responses)" -ForegroundColor White
Write-Host "  - Outbound UDP $Port (send broadcasts)" -ForegroundColor White
Write-Host ""
Write-Host "You can now run the EtherNet/IP Commissioning Tool and scan for devices." -ForegroundColor Yellow
Write-Host ""

# Display the created rules
Write-Host "Verifying firewall rules..." -ForegroundColor Yellow
Get-NetFirewallRule -DisplayName "$RuleName*" | Format-Table -Property DisplayName, Enabled, Direction, Action -AutoSize

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
