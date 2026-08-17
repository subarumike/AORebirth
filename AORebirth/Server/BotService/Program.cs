namespace AORebirth.BotService.Host
{
    using System;
    using System.Data;
    using System.Net;
    using System.Reflection;
    using System.Threading;

    using AORebirth.BotService;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                HostConfiguration configuration = HostConfiguration.Load();
                configuration.Validate();
                if (args.Length == 1 && string.Equals(args[0], "--validate-startup", StringComparison.Ordinal))
                {
                    return 0;
                }

                if (!configuration.Enabled)
                {
                    Console.WriteLine("AORebirth BotService host is disabled.");
                    return 0;
                }

                IPersistentBotRepository repository = new AdoNetBotRepository(
                    () => CreateConnection(configuration.ProviderType, configuration.ConnectionString));
                ((IPersistentBotSchemaValidator)repository).ValidateSchema();
                IHostedBotChatGateway gateway = new PrivateTcpHostedBotChatGateway(
                    new IPEndPoint(configuration.ChatAddress, configuration.ChatPort),
                    configuration.ServiceKey);
                BotServiceHostLoop host = new BotServiceHostLoop(
                    repository,
                    gateway,
                    new MetadataOnlyEventSink(),
                    configuration.PollInterval,
                    configuration.InitialReconnectDelay,
                    configuration.MaximumReconnectDelay);
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        cancellation.Cancel();
                    };
                    AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => cancellation.Cancel();
                    host.Run(cancellation.Token);
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("AORebirth BotService host failed: " + exception.Message);
                return 1;
            }
        }

        private static IDbConnection CreateConnection(string providerType, string connectionString)
        {
            Type type = Type.GetType(providerType, true);
            object instance = Activator.CreateInstance(type, new object[] { connectionString });
            IDbConnection connection = instance as IDbConnection;
            if (connection == null)
            {
                throw new InvalidOperationException("The configured bot database provider does not implement IDbConnection.");
            }

            return connection;
        }

        private sealed class MetadataOnlyEventSink : IBotInboundEventSink
        {
            public void Receive(BotPrincipal principal, BotInboundEvent inboundEvent)
            {
                Console.WriteLine(
                    "Bot inbound event bot={0} event={1} kind={2}",
                    principal.BotId.ToString("N"),
                    inboundEvent.EventId.ToString("N"),
                    inboundEvent.Kind);
            }
        }
    }

    internal sealed class HostConfiguration
    {
        public bool Enabled { get; private set; }

        public IPAddress ChatAddress { get; private set; }

        public int ChatPort { get; private set; }

        public byte[] ServiceKey { get; private set; }

        public string ConnectionString { get; private set; }

        public string ProviderType { get; private set; }

        public TimeSpan PollInterval { get; private set; }

        public TimeSpan InitialReconnectDelay { get; private set; }

        public TimeSpan MaximumReconnectDelay { get; private set; }

        public static HostConfiguration Load()
        {
            string addressText = Environment.GetEnvironmentVariable("AO_REBIRTH_BOT_CHAT_HOST") ?? "127.0.0.1";
            IPAddress address;
            if (!IPAddress.TryParse(addressText, out address))
            {
                throw new InvalidOperationException("AO_REBIRTH_BOT_CHAT_HOST must be an IP address.");
            }

            return new HostConfiguration
            {
                Enabled = ReadBoolean("AO_REBIRTH_BOT_SERVICE_ENABLED", false),
                ChatAddress = address,
                ChatPort = ReadInteger("AO_REBIRTH_BOT_CHAT_PORT", 7411),
                ServiceKey = ReadKey(Environment.GetEnvironmentVariable("AO_REBIRTH_BOT_CHAT_KEY")),
                ConnectionString = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION"),
                ProviderType = Environment.GetEnvironmentVariable("AO_REBIRTH_BOT_DB_PROVIDER")
                    ?? "MySqlConnector.MySqlConnection, MySqlConnector",
                PollInterval = TimeSpan.FromMilliseconds(ReadInteger("AO_REBIRTH_BOT_POLL_MS", 250)),
                InitialReconnectDelay = TimeSpan.FromMilliseconds(ReadInteger("AO_REBIRTH_BOT_RECONNECT_INITIAL_MS", 500)),
                MaximumReconnectDelay = TimeSpan.FromMilliseconds(ReadInteger("AO_REBIRTH_BOT_RECONNECT_MAX_MS", 30000))
            };
        }

        public void Validate()
        {
            if (!IPAddress.IsLoopback(this.ChatAddress))
            {
                throw new InvalidOperationException("BotService may connect only to a loopback ChatEngine endpoint.");
            }

            if (this.ChatPort < 1 || this.ChatPort > 65535)
            {
                throw new InvalidOperationException("AO_REBIRTH_BOT_CHAT_PORT is invalid.");
            }

            if (!this.Enabled)
            {
                return;
            }

            if (this.ServiceKey == null || this.ServiceKey.Length < 32)
            {
                throw new InvalidOperationException("AO_REBIRTH_BOT_CHAT_KEY must decode to at least 32 bytes.");
            }

            if (string.IsNullOrWhiteSpace(this.ConnectionString) || string.IsNullOrWhiteSpace(this.ProviderType))
            {
                throw new InvalidOperationException("Enabled BotService requires its private MySQL connection and provider.");
            }
        }

        private static bool ReadBoolean(string name, bool defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            bool parsed;
            return string.IsNullOrEmpty(value) ? defaultValue : bool.TryParse(value, out parsed) && parsed;
        }

        private static int ReadInteger(string name, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            int parsed;
            return string.IsNullOrEmpty(value) ? defaultValue : int.TryParse(value, out parsed) ? parsed : -1;
        }

        private static byte[] ReadKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : Convert.FromBase64String(value);
        }
    }
}
