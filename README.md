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

## Installation

1. Clone or download the project
2. Open `RelayEmulator.csproj` in Visual Studio or use the dotnet CLI
3. Build the project:
   ```
   dotnet build
   ```
4. Run the application:
   ```
   dotnet run
   ```

The application will automatically request administrator privileges on startup.

## Usage

1. Launch the application (requires admin rights)
2. The main window will display a grid of Steam relay servers
3. Click "Refresh Data" to fetch the latest Steam configuration
4. Check the boxes next to the relays you want to block
5. Click "Apply Block Rules" to create/update Windows Firewall rules
6. Blocked relays will show "Yes" in the "Is Blocked?" column

### Additional Notes

- The application fetches configuration data from a local service at `http://localhost:3001/steamconfig`
- Firewall rules are named with prefix "SteamRelayBlock-"
- To unblock IPs, simply uncheck them and apply rules again
- Ping times are measured asynchronously and may take a few seconds to appear
