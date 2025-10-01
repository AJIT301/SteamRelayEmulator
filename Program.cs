// See https://aka.ms/new-console-template for more information
using System;
using System.Text.Json;
using System.Net.Http;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using RelayEmulator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Check if running as administrator
        if (!IsAdministrator())
        {
            // Restart as administrator
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas"; // Run as admin
            process.StartInfo.Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));
            process.Start();
            Environment.Exit(0);
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    private static bool IsAdministrator()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}

public class MainForm : Form
{
    private DataGridView dataGridView;
    private Button refreshButton;
    private Button blockButton;
    private Label statusLabel;
    private SteamConfig? config;
    private int lastCheckedRow = -1;
    private HashSet<string> blockedIPs = new HashSet<string>();
    private const string RULE_NAME = "SteamRelayBlock";

    public MainForm()
    {
        this.Text = "Steam Relay Blocker";
        this.Size = new System.Drawing.Size(800, 620);

        dataGridView = new DataGridView();
        dataGridView.Location = new System.Drawing.Point(10, 10);
        dataGridView.Size = new System.Drawing.Size(760, 500);
        dataGridView.AutoGenerateColumns = false;
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsBlocked", HeaderText = "Is Blocked?", Width = 80 });
        dataGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Block", HeaderText = "Block?", Width = 60 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP Address", Width = 150 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ping", HeaderText = "Ping (ms)", Width = 80 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Continent", HeaderText = "Continent", Width = 100 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Region", HeaderText = "Region", Width = 200 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "RelayCount", HeaderText = "Relays", Width = 80 });

        // Make columns sortable
        foreach (DataGridViewColumn col in dataGridView.Columns)
        {
            col.SortMode = DataGridViewColumnSortMode.Automatic;
        }
        dataGridView.SortCompare += DataGridView_SortCompare;
        dataGridView.CellContentClick += DataGridView_CellContentClick;
        this.Controls.Add(dataGridView);

        refreshButton = new Button();
        refreshButton.Text = "Refresh Data";
        refreshButton.Location = new System.Drawing.Point(10, 520);
        refreshButton.Click += RefreshButton_Click;
        this.Controls.Add(refreshButton);

        blockButton = new Button();
        blockButton.Text = "Apply Block Rules";
        blockButton.Location = new System.Drawing.Point(120, 520);
        blockButton.Click += BlockButton_Click;
        this.Controls.Add(blockButton);

        statusLabel = new Label();
        statusLabel.Text = "Ready - Windows Firewall connected";
        statusLabel.Location = new System.Drawing.Point(10, 555);
        statusLabel.Size = new System.Drawing.Size(760, 30);
        statusLabel.BorderStyle = BorderStyle.FixedSingle;
        statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this.Controls.Add(statusLabel);

        LoadBlockedIPs();
        LoadDataAsync();
    }

    private void LoadBlockedIPs()
    {
        blockedIPs.Clear();

        string[] ruleNames = new string[]
        {
            $"{RULE_NAME}-Outbound-UDP",
            $"{RULE_NAME}-Inbound-UDP",
            $"{RULE_NAME}-Outbound-TCP",
            $"{RULE_NAME}-Inbound-TCP",
            $"{RULE_NAME}-Outbound-Any",
            $"{RULE_NAME}-Inbound-Any"
        };

        foreach (string ruleName in ruleNames)
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse the output to extract blocked IPs from RemoteIP field
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("RemoteIP:", StringComparison.OrdinalIgnoreCase))
                {
                    string ipList = line.Substring(line.IndexOf(':') + 1).Trim();
                    // Split by comma in case there are multiple IPs
                    var ips = ipList.Split(',');
                    foreach (var ip in ips)
                    {
                        blockedIPs.Add(ip.Trim());
                    }
                    break; // Found the IPs for this rule
                }
            }
        }

        // Update status label with debug info
        string firstThreeIPs = string.Join(", ", blockedIPs.Take(3));
        statusLabel.Text = $"Firewall rules loaded. IPs blocked: {blockedIPs.Count}\r\nFirst 3 IPs: {firstThreeIPs}";

        // DEBUG: Show message box if no IPs found
        if (blockedIPs.Count == 0)
        {
            MessageBox.Show("No firewall rules found or no IPs blocked", "Debug", MessageBoxButtons.OK);
        }
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        LoadBlockedIPs();
        await LoadDataAsync();
    }

    private void BlockButton_Click(object? sender, EventArgs e)
    {
        var selectedIPs = dataGridView.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Cells["Block"].Value is bool b && b)
            .Select(r => r.Cells["IP"].Value?.ToString())
            .Where(ip => !string.IsNullOrEmpty(ip))
            .Distinct()
            .ToList();

        if (selectedIPs.Count == 0)
        {
            statusLabel.Text = "No IPs selected to block";
            return;
        }

        try
        {
            UpdateFirewallRule(selectedIPs);
            LoadBlockedIPs();
            statusLabel.Text = $"Updated firewall rule with {selectedIPs.Count} relay IPs";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Firewall error: {ex.Message}";
            MessageBox.Show($"Firewall Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateFirewallRule(List<string> selectedIPs)
    {
        // First, delete existing rules if they exist (multiple rules now)
        var deleteProcess = new System.Diagnostics.Process();
        deleteProcess.StartInfo.FileName = "netsh";
        deleteProcess.StartInfo.Arguments = $"advfirewall firewall delete rule name=\"{RULE_NAME}-*\"";
        deleteProcess.StartInfo.UseShellExecute = false;
        deleteProcess.StartInfo.RedirectStandardOutput = true;
        deleteProcess.StartInfo.RedirectStandardError = true;
        deleteProcess.StartInfo.CreateNoWindow = true;
        deleteProcess.Start();
        deleteProcess.WaitForExit();

        // Create comma-separated list of IPs
        string ipList = string.Join(",", selectedIPs);

        string[] rules = new string[]
        {
            $"{RULE_NAME}-Outbound-UDP",
            $"{RULE_NAME}-Inbound-UDP",
            $"{RULE_NAME}-Outbound-TCP",
            $"{RULE_NAME}-Inbound-TCP",
            $"{RULE_NAME}-Outbound-Any",
            $"{RULE_NAME}-Inbound-Any"
        };

        string[][] ruleArgs = new string[][]
        {
            new string[] { "out", "udp", "27015-27068" },
            new string[] { "in", "udp", "27015-27068" },
            new string[] { "out", "tcp", "27015-27068" },
            new string[] { "in", "tcp", "27015-27068" },
            new string[] { "out", "any", "" },
            new string[] { "in", "any", "" }
        };

        for (int i = 0; i < rules.Length; i++)
        {
            string ruleName = rules[i];
            string direction = ruleArgs[i][0];
            string protocol = ruleArgs[i][1];
            string ports = ruleArgs[i][2];

            var process = new System.Diagnostics.Process();
            string portsArg = !string.IsNullOrEmpty(ports) ? $" remoteport={ports}" : "";
            process.StartInfo.Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir={direction} action=block remoteip={ipList} protocol={protocol}{portsArg}";
            process.StartInfo.FileName = "netsh";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Failed to create firewall rule '{ruleName}': {error}\r\nOutput: {output}");
            }
        }
    }

    private async Task LoadDataAsync()
    {
        refreshButton.Enabled = false;
        refreshButton.Text = "Fetching...";
        dataGridView.Rows.Clear();

        try
        {
            using var client = new HttpClient();
            // var response = await client.GetStringAsync("http://localhost:3001/steamconfig");
            var response = await client.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            config = JsonSerializer.Deserialize<SteamConfig>(response, options);

            if (config?.Pops != null)
            {
                var rows = new List<DataGridViewRow>();

                // DEBUG: Log what we're comparing
                int blockedCount = 0;

                // DEBUG: Check first relay IP
                string firstRelayIP = "";

                foreach (KeyValuePair<string, PopInfo> kvp in config.Pops)
                {
                    string key = kvp.Key;
                    PopInfo pop = kvp.Value;
                    string continent = GetContinent(pop.Geo);
                    foreach (var relay in pop.Relays)
                    {
                        if (string.IsNullOrEmpty(firstRelayIP)) firstRelayIP = relay.Ipv4;

                        bool isBlocked = blockedIPs.Contains(relay.Ipv4);
                        if (isBlocked) blockedCount++;

                        string blockedStatus = isBlocked ? "Yes" : "No";
                        var row = new DataGridViewRow();
                        row.CreateCells(dataGridView, blockedStatus, false, relay.Ipv4, "-", continent, $"{key}: {pop.Desc}", pop.Relays.Count);
                        rows.Add(row);
                    }
                }

                // DEBUG: Compare first IPs
                string firstBlockedIP = blockedIPs.FirstOrDefault() ?? "none";
                MessageBox.Show($"First blocked IP: [{firstBlockedIP}] (length: {firstBlockedIP.Length})\r\n" +
                               $"First relay IP: [{firstRelayIP}] (length: {firstRelayIP.Length})\r\n" +
                               $"Are equal: {firstBlockedIP == firstRelayIP}\r\n" +
                               $"HashSet contains relay IP: {blockedIPs.Contains(firstRelayIP)}",
                               "Debug IP Comparison");

                dataGridView.Rows.AddRange(rows.ToArray());

                // DEBUG: Update status to show matching info
                string firstThreeIPs = string.Join(", ", blockedIPs.Take(3));
                statusLabel.Text = $"Firewall: {blockedIPs.Count} IPs blocked | Grid: {blockedCount} matched\r\nFirst 3: {firstThreeIPs}";

                // Ping IPs asynchronously after loading
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    string? ip = row.Cells["IP"].Value?.ToString();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        _ = PingIpAsync(row, ip);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        refreshButton.Enabled = true;
        refreshButton.Text = "Refresh Data";
    }

    private void DataGridView_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex == 1) // Checkbox column (now index 1)
        {
            bool isChecked = !(dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as bool? ?? false);
            dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = isChecked;

            if ((ModifierKeys & Keys.Shift) == Keys.Shift && lastCheckedRow >= 0 && lastCheckedRow != e.RowIndex)
            {
                int start = Math.Min(lastCheckedRow, e.RowIndex);
                int end = Math.Max(lastCheckedRow, e.RowIndex);

                for (int i = start; i <= end; i++)
                {
                    dataGridView.Rows[i].Cells[e.ColumnIndex].Value = isChecked;
                }
            }

            lastCheckedRow = e.RowIndex;
        }
    }

    private void DataGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
    {
        if (e.Column.Index == 2) // IP column (now index 2)
        {
            string ip1 = e.CellValue1?.ToString() ?? "";
            string ip2 = e.CellValue2?.ToString() ?? "";
            e.SortResult = CompareIPAddresses(ip1, ip2);
            e.Handled = true;
        }
        else if (e.Column.Index == 3) // Ping column (now index 3)
        {
            string ping1 = e.CellValue1?.ToString() ?? "";
            string ping2 = e.CellValue2?.ToString() ?? "";
            e.SortResult = ComparePingValues(ping1, ping2);
            e.Handled = true;
        }
    }

    private int CompareIPAddresses(string ip1, string ip2)
    {
        string[] parts1 = ip1.Split('.');
        string[] parts2 = ip2.Split('.');

        for (int i = 0; i < 4 && i < parts1.Length && i < parts2.Length; i++)
        {
            if (int.TryParse(parts1[i], out int n1) && int.TryParse(parts2[i], out int n2))
            {
                int cmp = n1.CompareTo(n2);
                if (cmp != 0) return cmp;
            }
        }
        return string.Compare(ip1, ip2); // fallback
    }

    private int ComparePingValues(string ping1, string ping2)
    {
        // Handle special cases: "-", then numbers, then "Timeout", "N/A"
        if (ping1 == ping2) return 0;

        // "-" (not started) should be last
        if (ping1 == "-") return 1;
        if (ping2 == "-") return -1;

        // Try to parse as numbers
        if (int.TryParse(ping1, out int n1) && int.TryParse(ping2, out int n2))
        {
            return n1.CompareTo(n2);
        }

        // One is number, one is text - numbers first
        if (int.TryParse(ping1, out _)) return -1;
        if (int.TryParse(ping2, out _)) return 1;

        // Both text, alphabetical
        return string.Compare(ping1, ping2);
    }

    private string GetContinent(double[] geo)
    {
        if (geo.Length < 2) return "Unknown";
        double lat = geo[1], lon = geo[0]; // Assuming [lon, lat]

        if (lat >= 35 && lat <= 72 && lon >= -25 && lon <= 45) return "Europe";
        if (lat >= 24 && lat <= 50 && lon >= -125 && lon <= -65) return "USA";
        if (lat >= -10 && lat <= 55 && lon >= 60 && lon <= 150) return "Asia";
        return "Other";
    }

    private async Task PingIpAsync(DataGridViewRow row, string ip)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 1000);
            if (reply.Status == IPStatus.Success)
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        row.Cells["Ping"].Value = reply.RoundtripTime.ToString();
                    }));
                }
                else
                {
                    row.Cells["Ping"].Value = reply.RoundtripTime.ToString();
                }
            }
            else
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        row.Cells["Ping"].Value = "Timeout";
                    }));
                }
                else
                {
                    row.Cells["Ping"].Value = "Timeout";
                }
            }
        }
        catch
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    row.Cells["Ping"].Value = "N/A";
                }));
            }
            else
            {
                row.Cells["Ping"].Value = "N/A";
            }
        }
    }
}
