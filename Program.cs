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
    private Button selectRegionButton;
    private Label statusLabel;
    private Label selectedRegionLabel;
    private HashSet<string> availableContinents = new HashSet<string>();
    private SteamConfig? config;
    private int lastCheckedRow = -1;
    private HashSet<string> blockedIPs = new HashSet<string>();
    private string? selectedContinent = null;
    private const string RULE_NAME = "SteamRelayBlock";

    public MainForm()
    {
        this.Text = "Steam Relay Blocker";
        this.Size = new System.Drawing.Size(1000, 700);
        this.MinimumSize = new System.Drawing.Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);

        dataGridView = new DataGridView();
        dataGridView.Location = new System.Drawing.Point(12, 12);
        dataGridView.Size = new System.Drawing.Size(this.ClientSize.Width - 24, this.ClientSize.Height - 100);
        dataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dataGridView.AutoGenerateColumns = false;
        dataGridView.BorderStyle = BorderStyle.Fixed3D;
        dataGridView.BackgroundColor = System.Drawing.Color.White;
        dataGridView.GridColor = System.Drawing.Color.LightGray;
        dataGridView.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(200, 200, 200);
        dataGridView.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
        dataGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dataGridView.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9);
        dataGridView.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(51, 153, 255);
        dataGridView.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.White;
        dataGridView.RowHeadersWidth = 20;

        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsBlocked", HeaderText = "Blocked?", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dataGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Block", HeaderText = "Block?", Width = 60 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP Address", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ping", HeaderText = "Ping (ms)", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Continent", HeaderText = "Continent", Width = 100 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Region", HeaderText = "Region", Width = 200 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "RelayCount", HeaderText = "Relays", Width = 80, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        // Make columns sortable
        foreach (DataGridViewColumn col in dataGridView.Columns)
        {
            col.SortMode = DataGridViewColumnSortMode.Automatic;
        }
        dataGridView.SortCompare += DataGridView_SortCompare;
        dataGridView.CellContentClick += DataGridView_CellContentClick;
        this.Controls.Add(dataGridView);

        var buttonPanel = new Panel();
        buttonPanel.Location = new System.Drawing.Point(12, this.ClientSize.Height - 70);
        buttonPanel.Size = new System.Drawing.Size(this.ClientSize.Width - 24, 35);
        buttonPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.Controls.Add(buttonPanel);

        refreshButton = new Button();
        refreshButton.Text = "Refresh Data";
        refreshButton.Size = new System.Drawing.Size(120, 30);
        refreshButton.Location = new System.Drawing.Point(0, 0);
        refreshButton.Font = new System.Drawing.Font("Segoe UI", 9);
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        refreshButton.Click += RefreshButton_Click;
        buttonPanel.Controls.Add(refreshButton);

        blockButton = new Button();
        blockButton.Text = "Apply Block Rules";
        blockButton.Size = new System.Drawing.Size(130, 30);
        blockButton.Location = new System.Drawing.Point(130, 0);
        blockButton.Font = new System.Drawing.Font("Segoe UI", 9);
        blockButton.FlatStyle = FlatStyle.Flat;
        blockButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        blockButton.BackColor = System.Drawing.Color.FromArgb(255, 87, 87);
        blockButton.ForeColor = System.Drawing.Color.White;
        blockButton.Click += BlockButton_Click;
        buttonPanel.Controls.Add(blockButton);

        selectRegionButton = new Button();
        selectRegionButton.Text = "Select Region";
        selectRegionButton.Size = new System.Drawing.Size(120, 30);
        selectRegionButton.Location = new System.Drawing.Point(270, 0);
        selectRegionButton.Font = new System.Drawing.Font("Segoe UI", 9);
        selectRegionButton.FlatStyle = FlatStyle.Flat;
        selectRegionButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
        selectRegionButton.Click += SelectRegionButton_Click;
        buttonPanel.Controls.Add(selectRegionButton);

        selectedRegionLabel = new Label();
        selectedRegionLabel.Text = "Selected Region: None";
        selectedRegionLabel.Location = new System.Drawing.Point(12, this.ClientSize.Height - 55);
        selectedRegionLabel.Size = new System.Drawing.Size(250, 20);
        selectedRegionLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        selectedRegionLabel.Font = new System.Drawing.Font("Segoe UI", 9);
        selectedRegionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        this.Controls.Add(selectedRegionLabel);

        statusLabel = new Label();
        statusLabel.Text = "Ready - Windows Firewall connected";
        statusLabel.Location = new System.Drawing.Point(12, this.ClientSize.Height - 30);
        statusLabel.Size = new System.Drawing.Size(this.ClientSize.Width - 24, 25);
        statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        statusLabel.BorderStyle = BorderStyle.None;
        statusLabel.BackColor = System.Drawing.Color.FromArgb(220, 220, 220);
        statusLabel.Font = new System.Drawing.Font("Segoe UI", 9);
        statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        statusLabel.Padding = new Padding(5, 0, 0, 0);
        this.Controls.Add(statusLabel);

        this.Resize += (s, e) => UpdateLayout();

        LoadBlockedIPs();
        LoadDataAsync();
    }

    private void UpdateLayout()
    {
        // This method can be expanded if needed for dynamic adjustments
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
                        string cleanIP = ip.Trim();
                        // Remove /32 CIDR notation if present
                        if (cleanIP.Contains("/"))
                        {
                            cleanIP = cleanIP.Substring(0, cleanIP.IndexOf("/"));
                        }
                        blockedIPs.Add(cleanIP);
                    }
                    break; // Found the IPs for this rule
                }
            }
        }

        // Update status label with debug info
        string firstThreeIPs = string.Join(", ", blockedIPs.Take(3));
        statusLabel.Text = $"Firewall rules loaded. IPs blocked: {blockedIPs.Count}\r\nFirst 3 IPs: {firstThreeIPs}";
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        LoadBlockedIPs();
        await LoadDataAsync();
    }

    private void BlockButton_Click(object? sender, EventArgs e)
    {
        List<string> selectedIPs;

        if (!string.IsNullOrEmpty(selectedContinent))
        {
            if (selectedContinent == "World")
            {
                // "World" means unblock all - no IPs to block
                selectedIPs = new List<string>();
            }
            else
            {
                // Block all IPs NOT in the selected continent
                selectedIPs = dataGridView.Rows.Cast<DataGridViewRow>()
                    .Where(r =>
                    {
                        string? rowContinent = r.Cells["Continent"].Value?.ToString();
                        return !string.IsNullOrEmpty(rowContinent) && rowContinent != selectedContinent;
                    })
                    .Select(r => r.Cells["IP"].Value?.ToString())
                    .Where(ip => !string.IsNullOrEmpty(ip))
                    .Distinct()
                    .ToList();

                if (selectedIPs.Count == 0)
                {
                    statusLabel.Text = $"No IPs to block (all relays are in {selectedContinent})";
                    return;
                }
            }
        }
        else
        {
            // Manual selection mode
            selectedIPs = dataGridView.Rows.Cast<DataGridViewRow>()
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
        }

        try
        {
            UpdateFirewallRule(selectedIPs);
            LoadBlockedIPs();
            RefreshButton_Click(null, EventArgs.Empty);
            string modeMessage = !string.IsNullOrEmpty(selectedContinent) ? $" (filtered: all except {selectedContinent})" : " (manual selection)";
            statusLabel.Text = $"Updated firewall rules with {selectedIPs.Count} relay IPs{modeMessage}";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Firewall error: {ex.Message}";
            MessageBox.Show($"Firewall Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateFirewallRule(List<string> selectedIPs)
    {
        // Delete all existing rules
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
            var deleteProcess = new System.Diagnostics.Process();
            deleteProcess.StartInfo.FileName = "netsh";
            deleteProcess.StartInfo.Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"";
            deleteProcess.StartInfo.UseShellExecute = false;
            deleteProcess.StartInfo.CreateNoWindow = true;
            deleteProcess.Start();
            deleteProcess.WaitForExit();
        }

        // If no IPs to block, we're done (all rules deleted, effectively unblocked)
        if (selectedIPs.Count == 0)
        {
            return;
        }

        // Create comma-separated list of IPs
        string ipList = string.Join(",", selectedIPs);

        string[][] ruleArgs = new string[][]
        {
            new string[] { $"{RULE_NAME}-Outbound-UDP", "out", "udp", "27015-27068" },
            new string[] { $"{RULE_NAME}-Inbound-UDP", "in", "udp", "27015-27068" },
            new string[] { $"{RULE_NAME}-Outbound-TCP", "out", "tcp", "27015-27068" },
            new string[] { $"{RULE_NAME}-Inbound-TCP", "in", "tcp", "27015-27068" },
            new string[] { $"{RULE_NAME}-Outbound-Any", "out", "any", "" },
            new string[] { $"{RULE_NAME}-Inbound-Any", "in", "any", "" }
        };

        foreach (var ruleArg in ruleArgs)
        {
            string ruleName = ruleArg[0];
            string direction = ruleArg[1];
            string protocol = ruleArg[2];
            string ports = ruleArg[3];

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
                throw new Exception($"Failed to create firewall rule '{ruleName}': {error}");
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
            var response = await client.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            config = JsonSerializer.Deserialize<SteamConfig>(response, options);

            if (config?.Pops != null)
            {
                var rows = new List<DataGridViewRow>();
                int blockedCount = 0;
                availableContinents.Clear();

                foreach (KeyValuePair<string, PopInfo> kvp in config.Pops)
                {
                    string key = kvp.Key;
                    PopInfo pop = kvp.Value;
                    string continent = GetContinent(pop.Geo);
                    availableContinents.Add(continent);
                    foreach (var relay in pop.Relays)
                    {
                        bool isBlocked = blockedIPs.Contains(relay.Ipv4);
                        if (isBlocked) blockedCount++;

                        string blockedStatus = isBlocked ? "Yes" : "No";
                        var row = new DataGridViewRow();
                        row.CreateCells(dataGridView, blockedStatus, false, relay.Ipv4, "-", continent, $"{key}: {pop.Desc}", pop.Relays.Count);
                        rows.Add(row);
                    }
                }

                dataGridView.Rows.AddRange(rows.ToArray());



                // Update status to show matching info
                statusLabel.Text = $"Firewall: {blockedIPs.Count} IPs blocked | Grid: {blockedCount} matched";

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
        return string.Compare(ip1, ip2);
    }

    private int ComparePingValues(string ping1, string ping2)
    {
        if (ping1 == ping2) return 0;
        if (ping1 == "-") return 1;
        if (ping2 == "-") return -1;

        if (int.TryParse(ping1, out int n1) && int.TryParse(ping2, out int n2))
        {
            return n1.CompareTo(n2);
        }

        if (int.TryParse(ping1, out _)) return -1;
        if (int.TryParse(ping2, out _)) return 1;

        return string.Compare(ping1, ping2);
    }

    private string GetContinent(double[] geo)
    {
        if (geo.Length < 2) return "Unknown";
        double lat = geo[1], lon = geo[0];

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

    private void SelectRegionButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new RegionSelectForm(availableContinents);
        var result = dialog.ShowDialog(this);
        if (result == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedContinent))
        {
            selectedContinent = dialog.SelectedContinent;
            selectedRegionLabel.Text = $"Selected Region: {selectedContinent}";
            statusLabel.Text = $"Region selected: {selectedContinent}. Refreshing data...";
            RefreshButton_Click(null, EventArgs.Empty);
        }
    }
}

public class RegionSelectForm : Form
{
    public string? SelectedContinent { get; private set; }

    private readonly HashSet<string> continents;

    public RegionSelectForm(HashSet<string> availableContinents)
    {
        continents = availableContinents.ToHashSet();
        continents.Add("World"); // Add "World" option
        this.Text = "Select Preferred Region";
        this.Size = new System.Drawing.Size(350, 280); // Increased height for info label
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);

        var infoLabel = new Label();
        infoLabel.Text = "Select your preferred region. 'World' removes all blocks.\nAfter confirming, click 'Apply Block Rules' to update firewall.";
        infoLabel.Size = new System.Drawing.Size(310, 40);
        infoLabel.Location = new System.Drawing.Point(20, 10);
        infoLabel.Font = new System.Drawing.Font("Segoe UI", 8);
        infoLabel.ForeColor = System.Drawing.Color.DarkBlue;
        this.Controls.Add(infoLabel);

        var listBox = new ListBox();
        listBox.Location = new System.Drawing.Point(20, 50);
        listBox.Size = new System.Drawing.Size(310, 120);
        var sortedContinents = continents.OrderBy(c => c == "World" ? 1 : 0).ThenBy(c => c).ToArray(); // World last
        listBox.Items.AddRange(sortedContinents);
        listBox.SelectionMode = SelectionMode.One;
        this.Controls.Add(listBox);

        var okButton = new Button();
        okButton.Text = "Confirm";
        okButton.Location = new System.Drawing.Point(80, 180);
        okButton.Size = new System.Drawing.Size(80, 30);
        okButton.BackColor = System.Drawing.Color.FromArgb(51, 153, 255);
        okButton.ForeColor = System.Drawing.Color.White;
        okButton.Click += (s, e) =>
        {
            if (listBox.SelectedItem != null)
            {
                SelectedContinent = listBox.SelectedItem.ToString();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        };
        this.Controls.Add(okButton);

        var cancelButton = new Button();
        cancelButton.Text = "Cancel";
        cancelButton.Location = new System.Drawing.Point(190, 180);
        cancelButton.Size = new System.Drawing.Size(80, 30);
        cancelButton.BackColor = System.Drawing.Color.FromArgb(200, 200, 200);
        cancelButton.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };
        this.Controls.Add(cancelButton);

        this.AcceptButton = okButton;
        this.CancelButton = cancelButton;
    }
}
