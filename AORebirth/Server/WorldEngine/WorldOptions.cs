namespace WorldEngine
{
    using System;
    using System.Globalization;
    using System.Net;

    using AORebirth.World.Contracts;

    using Utility.Config;

    public sealed class WorldOptions
    {
        public required IPAddress ListenAddress { get; init; }

        public required int Port { get; init; }

        /// <summary>0 when the status page is off.</summary>
        public required int StatusPort { get; init; }

        public required TimeSpan LeaseTtl { get; init; }

        public required TimeSpan LeaseGrace { get; init; }

        public required TimeSpan Heartbeat { get; init; }

        /// <summary>How long an issued zone ticket stays claimable.</summary>
        public required TimeSpan HandoffLifetime { get; init; }

        public required string Token { get; init; }

        /// <summary>After a restart, only re-registrations are served for this long; then World is authoritative.</summary>
        public TimeSpan RecoveryWindow => LeaseTtl + LeaseGrace;

        /// <summary>An instance silent this long has lost everything it owned.</summary>
        public TimeSpan InstanceExpiry => LeaseTtl + LeaseGrace;

        public static WorldOptions FromConfig(Config? config)
        {
            WorldSettings? settings = config?.World;
            string listen = WorldSettings.ResolveListenIP(settings);
            if (!IPAddress.TryParse(listen, out IPAddress? address))
                throw new InvalidOperationException("World ListenIP must be an IP address.");
            int port = WorldSettings.ResolvePort(settings);
            int status = WorldSettings.ResolveStatusPort(settings);
            if (port > 65535 || status > 65535)
                throw new InvalidOperationException("World ports must be between 1 and 65535.");
            string token = Environment.GetEnvironmentVariable(WorldChannel.TokenVariable) ?? string.Empty;
            if (token.Length == 0 && !IPAddress.IsLoopback(address))
                throw new InvalidOperationException(
                    "World listens on " + listen + " but " + WorldChannel.TokenVariable + " is not set; a non-loopback World needs a token.");

            return new WorldOptions
            {
                ListenAddress = address,
                Port = port,
                StatusPort = status < 0 ? 0 : status,
                LeaseTtl = TimeSpan.FromSeconds(WorldSettings.ResolveLeaseTtlSeconds(settings)),
                LeaseGrace = TimeSpan.FromSeconds(WorldSettings.ResolveLeaseGraceSeconds(settings)),
                Heartbeat = TimeSpan.FromMilliseconds(WorldSettings.ResolveHeartbeatMilliseconds(settings)),
                HandoffLifetime = TimeSpan.FromSeconds(ResolveHandoffSeconds()),
                Token = token
            };
        }

        /// <summary>The same knob and bounds the file-based handoff store used.</summary>
        static int ResolveHandoffSeconds()
            => int.TryParse(Environment.GetEnvironmentVariable("AO_REBIRTH_LOGIN_HANDOFF_TIMEOUT_SECONDS"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds >= 5 && seconds <= 120
                ? seconds
                : 30;
    }
}
