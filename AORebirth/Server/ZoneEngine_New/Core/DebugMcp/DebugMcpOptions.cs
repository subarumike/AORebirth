namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;

    using Utility.Config;

    public sealed class DebugMcpEndpoint
    {
        public DebugMcpEndpoint(IPAddress address, int port)
        {
            ArgumentNullException.ThrowIfNull(address);
            Address = address;
            Port = port;
            Url = "http://" + FormatHost(address) + ":" + port.ToString(CultureInfo.InvariantCulture) + DebugMcpOptions.Route;
        }

        public IPAddress Address { get; }

        public int Port { get; }

        public string ListenIP => Address.ToString();

        public string Url { get; }

        static string FormatHost(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return "[" + address.ToString() + "]";
            return address.ToString();
        }
    }

    public static class DebugMcpOptions
    {
        public const string DefaultListenIP = "127.0.0.1";
        public const int DefaultPort = 7590;
        public const string Route = "/mcp";

        /// <summary>
        /// Null when the server stays off. Throws when Enabled is true and the bind address is not loopback.
        /// </summary>
        public static DebugMcpEndpoint? Resolve(DebugMcpSettings? settings)
        {
            if (settings == null || !settings.Enabled)
                return null;

            string listen = string.IsNullOrWhiteSpace(settings.ListenIP)
                ? DefaultListenIP
                : settings.ListenIP.Trim();
            if (!IPAddress.TryParse(listen, out IPAddress? address) || address == null || !IPAddress.IsLoopback(address))
                throw new StartupValidationException("DebugMcp ListenIP must be a loopback address (127.0.0.1).");

            int port = settings.Port <= 0 ? DefaultPort : settings.Port;
            if (port > 65535)
                throw new StartupValidationException("DebugMcp Port must be between 1 and 65535.");

            return new DebugMcpEndpoint(address, port);
        }
    }
}
