# Steam Relay Blocker

Steam Relay Blocker is a Windows desktop application designed to help you optimize your Steam gaming experience by controlling Steam relay servers through Windows Firewall rules. This can reduce latency by forcing Steam to use specific geographic routes or eliminate suboptimal relay assignments.

## Features

- **Live Data Fetching**: Automatically fetches current Steam relay server configuration from Steam APIs
- **Region-Based Blocking**: Select preferred regions (Europe, USA, Asia, or unblock all with "World")
- **Real-Time Ping Analysis**: Measures ping times for each relay server (displays asynchronously)
- **Manual Override**: Still supports individual IP selection for fine-tuned control
- **Smart Blocking**: When a region is selected, blocks ALL relay IPs except your chosen region
- **Bulk Selection**: Use Shift+click for range selection of IPs
- **Firewall Integration**: Seamlessly manages Windows Firewall rules
- **Administrator Required**: Automatically requests elevation for firewall management

## Requirements

- Windows 10/11
- .NET Framework 4.7.2 or later (included in Windows 10 version 1709+)
- Administrator privileges (application requests elevation automatically)
- Active internet connection for Steam API data

## Building and Publishing

### Development Build
1. Clone or download the project
2. Open `RelayEmulator.csproj` in Visual Studio or use the dotnet CLI
3. Build the project:
   ```bash
   dotnet build
   ```
4. Find the executable in `bin/Debug/net472/RelayEmulator.exe`

### Standalone Executable (Recommended for Distribution)
To create a self-contained executable that runs without installing .NET Framework:

```bash
dotnet publish -c Release -r win-x64 --self-contained=true
```

This creates `RelayEmulator.exe` in `bin\Release\net472\win-x64\publish\` along with all required .NET runtime files.

For 32-bit systems:
```bash
dotnet publish -c Release -r win-x86 --self-contained=true
```

The published executable will be in `bin\Release\net472\win-x86\publish\`.

## Usage

### Getting Started
1. Launch the application (requires administrator privileges)
2. Application fetches and displays current Steam relay configuration
3. Ping measurements appear asynchronously (may take several seconds)

### Basic Blocking
1. Use checkbox in "Block?" column to select individual IPs
2. Hold Shift and click for range selection
3. Click "Apply Block Rules" to update firewall

### Region-Based Blocking (Recommended)
1. Click "Select Region" button
2. Choose from:
   - **Europe** - Allow only European relays
   - **USA** - Force US server routings
   - **Asia** - Connect through Asian servers
   - **Other** - Miscellaneous regions
   - **World** - Unblock all relays (disable blocking)
3. Confirm selection in modal popup
4. Click "Apply Block Rules" to enforce

### Tips
- Region selection provides the most reliable routing control
- Use "IS Blocked?" column to verify current firewall state
- Combining region filtering with manual selection works seamlessly
- Firewall rules are named with "SteamRelayBlock-" prefix

## Advanced Notes

### Firewall Management
- Rules are bidirectional (inbound/outbound, TCP/UDP)
- Port range: 27015-27068 (Valve's Steam relay ports)
- Apply same selection multiple times to refresh blocked list

### Troubleshooting
- Application requires constant admin privileges for firewall changes
- Ping timeouts are normal for distant servers
- Clear selections and re-apply to modify rules
- Manual firewall rule cleanup may be needed for accumulated rules

### Known Behavior
- Firewall rule accumulation: Each blocking operation adds new rule sets
- Clean up old rules manually in Windows Firewall with Advanced Security
- Application is designed for legitimate gaming optimization only
