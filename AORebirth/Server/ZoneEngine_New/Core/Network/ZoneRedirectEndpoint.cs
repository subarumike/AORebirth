namespace ZoneEngine_New.Core.Network;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Utility.Config;

internal sealed record ZoneRedirectEndpoint(IPAddress Address, ushort Port)
{
    internal static ZoneRedirectEndpoint Configured()
    {
        Config? config = ConfigReadWrite.Instance.CurrentConfig;
        string host = config == null || string.IsNullOrWhiteSpace(config.ZoneIP) ? "127.0.0.1" : config.ZoneIP;
        int port = config == null || config.ZonePort <= 0 ? 7501 : config.ZonePort;
        if (port > ushort.MaxValue) throw new InvalidOperationException("ZonePort is outside the retail redirect range.");
        if (IPAddress.TryParse(host, out IPAddress? parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            return new ZoneRedirectEndpoint(parsed, (ushort)port);
        IPAddress? resolved = Dns.GetHostEntry(host).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        if (resolved == null) throw new InvalidOperationException("ZoneIP did not resolve to an IPv4 address.");
        return new ZoneRedirectEndpoint(resolved, (ushort)port);
    }
}
