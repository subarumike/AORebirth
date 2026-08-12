namespace Utility.Network
{
    using System;
    using System.Net;

    public enum EngineBindMode
    {
        Loopback,
        Public
    }

    public sealed class EngineBindPolicy
    {
        public const string EnvironmentVariableName = "AO_REBIRTH_BIND_MODE";
        public const string LoopbackValue = "Loopback";
        public const string PublicValue = "Public";
        public const string LoopbackAddressText = "127.0.0.1";
        public const string PublicAddressText = "0.0.0.0";

        private EngineBindPolicy(EngineBindMode mode, IPAddress address)
        {
            this.Mode = mode;
            this.Address = address;
        }

        public EngineBindMode Mode { get; private set; }

        public IPAddress Address { get; private set; }

        public string AddressText
        {
            get { return this.Mode == EngineBindMode.Public ? PublicAddressText : LoopbackAddressText; }
        }

        public static EngineBindPolicy ResolveFromEnvironment()
        {
            return Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        }

        public static EngineBindPolicy Resolve(string value)
        {
            if (value == null)
            {
                return Loopback();
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidOperationException(EnvironmentVariableName + " must be Loopback or Public.");
            }

            if (string.Equals(trimmed, LoopbackValue, StringComparison.OrdinalIgnoreCase))
            {
                return Loopback();
            }

            if (string.Equals(trimmed, PublicValue, StringComparison.OrdinalIgnoreCase))
            {
                return new EngineBindPolicy(EngineBindMode.Public, IPAddress.Any);
            }

            throw new InvalidOperationException(EnvironmentVariableName + " must be Loopback or Public.");
        }

        private static EngineBindPolicy Loopback()
        {
            return new EngineBindPolicy(EngineBindMode.Loopback, IPAddress.Loopback);
        }
    }
}
