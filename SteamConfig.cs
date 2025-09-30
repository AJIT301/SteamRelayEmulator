using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RelayEmulator
{
    public class SteamConfig
    {
        public Dictionary<string, PopInfo> Pops { get; set; } = new();
    }

    public class PopInfo
    {
        public string Desc { get; set; } = string.Empty;
        public double[] Geo { get; set; } = Array.Empty<double>();
        public List<RelayInfo> Relays { get; set; } = new();
    }

    public class RelayInfo
    {
        public string Ipv4 { get; set; } = string.Empty;
        public int[] PortRange { get; set; } = new int[0];
    }
}
