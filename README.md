# Steam Relay Blocker

Steam Relay Blocker is a Windows desktop application designed to help you block Steam relay servers by managing Windows Firewall rules. This can improve your gaming experience by reducing latency caused by suboptimal relay routing.
Also can be used if you want to connect for example from USA to European servers by blocking your local relay servers, which will force Steam to assign you to European/NA or Asian servers. Blocking it futher you can achieve your desired destination.

## Features

- Displays a list of Steam relay servers with their locations, IPs, and ping times
- Allows you to select specific relays to block via Windows Firewall
- Automatically applies firewall rules for UDP and TCP traffic on Steam ports (27015-27068)
- Provides real-time ping testing for each relay IP
- Supports bulk selection with Shift+click
- Requires administrator privileges for firewall management

## Requirements

- Windows 10/11
- .NET Framework 4.7.2 or later (included in Windows 10 version 1709+)
- Administrator privileges (application will request elevation automatically)
- Internet connection to fetch Steam configuration data

## Building and Publishing

### Development Build
1. Clone or download the project
2. Open `RelayEmulator.csproj` in Visual Studio or use the dotnet CLI
3. Build the project:
   ```
   dotnet build
   ```
4. Find the executable in `bin/Debug/net472/RelayEmulator.exe`

### Standalone Executable (Recommended for Distribution)
To create a self-contained executable that doesn't require the .NET Framework to be installed on the target machine:

```
dotnet publish -c Release -r win-x64 --self-contained=true
```

This will create a `RelayEmulator.exe` in the `bin\Release\net472\win-x64\publish\` directory along with all required .NET runtime files.

Alternatively, for 32-bit systems:
```
dotnet publish -c Release -r win-x86 --self-contained=true
```

The published executable will be in `bin\Release\net472\win-x86\publish\`.

### Running the Application
The application will automatically request administrator privileges on startup.

## Usage

1. Launch the application (requires admin rights)
2. The main window will display a grid of Steam relay servers
3. Click "Refresh Data" to fetch the latest Steam configuration
4. Check the boxes next to the relays you want to block
5. Click "Apply Block Rules" to create/update Windows Firewall rules
6. Blocked relays will show "Yes" in the "Is Blocked?" column

### Additional Notes
- Firewall rules are named with prefix "SteamRelayBlock-"
- To unblock IPs, simply uncheck them and apply rules again
- Ping times are measured asynchronously and may take a few seconds to appear

### Currently NOT working
- Checking for Is blocked? state. It shows No by default. 
- Each time you create ruleset - block range of IPs it will result in another 3 inbound and 3 outbound rulesets.
- You must delete them manually in advanced firewall settings.

