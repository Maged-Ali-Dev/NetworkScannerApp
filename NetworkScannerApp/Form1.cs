using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;

namespace NetworkScannerApp
{
    public partial class Form1 : Form
    {
        private System.Windows.Forms.Timer updateTimer;
        private Dictionary<string, string> previousLatencies = new Dictionary<string, string>();

        public Form1()
        {
            InitializeComponent();
            InitializeDataGridView();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 6000;
            updateTimer.Tick += async (sender, e) => await UpdateDeviceData();
            updateTimer.Start();
        }

        private async Task UpdateDeviceData()
        {
            string routerIP = GetDefaultGateway();
            string localIP = GetLocalIPAddress();

            if (routerIP != null)
            {
                string subnet = GetSubnet(routerIP);
                if (subnet != null)
                {
                    List<Device> devices = await ScanNetwork(subnet, routerIP, localIP);
                    BindDevicesToGrid(devices);
                }
                else
                {
                    MessageBox.Show("Could not determine subnet.");
                }
            }
            else
            {
                MessageBox.Show("Router IP Address could not be found.");
            }
        }

        private async void btnScan_Click_1(object sender, EventArgs e)
        {
            await UpdateDeviceData();
        }

        static string GetDefaultGateway()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork) // Only IPv4
                        {
                            return gateway.Address.ToString();
                        }
                    }
                }
            }
            return null;
        }

        static string GetLocalIPAddress()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return unicastAddress.Address.ToString();
                        }
                    }
                }
            }
            return null;
        }

        static string GetSubnet(string routerIP)
        {
            string[] ipParts = routerIP.Split('.');
            if (ipParts.Length == 4)
            {
                return $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.";
            }
            return null;
        }

        async Task<List<Device>> ScanNetwork(string subnet, string routerIP, string localIP)
        {
            List<Device> devices = new List<Device>();
            List<Task> tasks = new List<Task>();

            // Add Router first
            devices.Add(new Device
            {
                IPAddress = routerIP,
                HostName = "Router",
                Latency = await MeasureLatency(routerIP),
                MacAddress = GetMacAddress(routerIP)
            });

            // Add Local Device second
            devices.Add(new Device
            {
                IPAddress = localIP,
                HostName = "This Device",
                Latency = await MeasureLatency(localIP),
                MacAddress = GetMacAddress(localIP)
            });

            // Scan remaining devices
            for (int i = 1; i < 255; i++)
            {
                string ip = $"{subnet}{i}";
                if (ip == routerIP || ip == localIP)
                    continue;

                tasks.Add(Task.Run(async () =>
                {
                    if (await IsDeviceOnline(ip))
                    {
                        var device = new Device
                        {
                            IPAddress = ip,
                            HostName = GetHostName(ip),
                            Latency = await MeasureLatency(ip),
                            MacAddress = GetMacAddress(ip)
                        };

                        if (device.HostName == "Unknown")
                        {
                            device.HostName = GetNetBiosName(ip);
                        }

                        lock (devices)
                        {
                            devices.Add(device);
                        }

                        if (previousLatencies.ContainsKey(ip) &&
                            int.TryParse(previousLatencies[ip].Replace(" ms", ""), out int previousLatency) &&
                            int.TryParse(device.Latency.Replace(" ms", ""), out int currentLatency) &&
                            currentLatency > previousLatency)
                        {
                            Console.WriteLine($"Latency increased for {ip}: {previousLatency} ms -> {currentLatency} ms");
                        }

                        previousLatencies[ip] = device.Latency;
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return devices;
        }

        async Task<bool> IsDeviceOnline(string ip)
        {
            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(ip, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        async Task<string> MeasureLatency(string ip)
        {
            const int measurements = 5;
            List<long> latencies = new List<long>();

            try
            {
                using (var ping = new Ping())
                {
                    for (int i = 0; i < measurements; i++)
                    {
                        PingReply reply = await ping.SendPingAsync(ip, 1000);
                        if (reply.Status == IPStatus.Success)
                        {
                            latencies.Add(reply.RoundtripTime);
                        }
                        await Task.Delay(100);
                    }
                }

                if (latencies.Count > 0)
                {
                    long averageLatency = (long)latencies.Average();
                    return $"{averageLatency} ms";
                }
                else
                {
                    return "Request timed out";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        string GetHostName(string ip)
        {
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(ip);
                return hostEntry.HostName;
            }
            catch (SocketException)
            {
                return "Unknown";
            }
        }

        string GetNetBiosName(string ip)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "nbtstat";
                process.StartInfo.Arguments = $"-A {ip}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    if (line.Contains("<20>") || (line.Contains("<00>") && line.Contains("UNIQUE")))
                    {
                        return line.Substring(0, line.IndexOf("<")).Trim();
                    }
                }
            }
            catch (Exception)
            {
                // Handle any exceptions related to the process
            }
            return "Unknown";
        }


        string GetMacAddress(string ipAddress)
        {
            if (ipAddress == GetLocalIPAddress())
            {
                return GetLocalMacAddress();
            }
            else
            {
                try
                {
                    var arpProcess = new Process();
                    arpProcess.StartInfo.FileName = "arp";
                    arpProcess.StartInfo.Arguments = "-a";
                    arpProcess.StartInfo.RedirectStandardOutput = true;
                    arpProcess.StartInfo.UseShellExecute = false;
                    arpProcess.StartInfo.CreateNoWindow = true;
                    arpProcess.Start();

                    string output = arpProcess.StandardOutput.ReadToEnd();
                    arpProcess.WaitForExit();

                    string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.Contains(ipAddress))
                        {
                            string[] segments = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (segments.Length >= 3)
                            {
                                string macAddress = segments[1];
                                if (IsValidMacAddress(macAddress))
                                {
                                    return FormatMacAddress(macAddress);
                                }
                            }
                        }
                    }
                    return "MAC Address not found";
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
        }

        string GetLocalMacAddress()
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    var unicastAddresses = nic.GetIPProperties().UnicastAddresses;
                    foreach (UnicastIPAddressInformation ip in unicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && ip.Address.ToString() == GetLocalIPAddress())
                        {
                            byte[] macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                            string macAddress = BitConverter.ToString(macBytes).Replace("-", "");
                            return macAddress;
                        }
                    }
                }
            }
            return "MAC Address not found";
        }

        bool IsValidMacAddress(string macAddress)
        {
            return macAddress.Length == 17 && macAddress.Count(c => c == '-') == 5;
        }

        string FormatMacAddress(string macAddress)
        {
            return macAddress.Replace("-", "").ToUpper();
        }


        


         


        void InitializeDataGridView()
        {
            this.BackColor = Color.WhiteSmoke;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.Padding = new Padding(10);

            // Customize buttons
            btnScan.FlatStyle = FlatStyle.Flat;
            btnScan.FlatAppearance.BorderSize = 0;
            btnScan.BackColor = Color.FromArgb(0, 123, 255);
            btnScan.ForeColor = Color.White;


            dataGridViewDevices.BorderStyle = BorderStyle.None;
            dataGridViewDevices.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(238, 239, 249);
            dataGridViewDevices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewDevices.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 123, 255);
            dataGridViewDevices.DefaultCellStyle.SelectionForeColor = Color.WhiteSmoke;
            dataGridViewDevices.BackgroundColor = Color.White;

            dataGridViewDevices.EnableHeadersVisualStyles = false;
            dataGridViewDevices.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewDevices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 25, 72);
            dataGridViewDevices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;


            dataGridViewDevices.ColumnCount = 4;
            dataGridViewDevices.Columns[0].Name = "IP Address";
            dataGridViewDevices.Columns[1].Name = "Host Name";
            dataGridViewDevices.Columns[2].Name = "Latency";
            dataGridViewDevices.Columns[3].Name = "MAC Address";
            dataGridViewDevices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        void BindDevicesToGrid(List<Device> devices)
        {
            dataGridViewDevices.Rows.Clear();

            foreach (var device in devices)
            {
                dataGridViewDevices.Rows.Add(device.IPAddress, device.HostName, device.Latency, device.MacAddress);
            }
        }
    }

    public class Device
    {
        public string IPAddress { get; set; }
        public string HostName { get; set; }
        public string Latency { get; set; }
        public string MacAddress { get; set; }
    }
}
