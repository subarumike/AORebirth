namespace ZoneEngine_New.Core.Chat
{
    using System;
    using System.Globalization;
    using System.Net;

    using AORebirth.Communication.ISComV2Client;
    using AORebirth.Communication.Messages;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Teams;

    using ConfigReadWrite = Utility.Config.ConfigReadWrite;

    public interface IChatEngineLink : IDisposable
    {
        void Start();

        bool TrySend(MessageBase message);
    }

    /// <summary>
    /// Zone↔ChatEngine ISCom link: Zone→Chat sends; Chat→Zone delivers LFT seed commands.
    /// </summary>
    public sealed class IsComChatEngineLink : IChatEngineLink
    {
        private readonly IZoneLogger _logger;
        private readonly Lazy<TeamService> _teams;
        private readonly ISComV2Client _client = new ISComV2Client();
        private bool _started;
        private bool _disposed;

        public IsComChatEngineLink(IZoneLogger logger, Lazy<TeamService> teams)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(teams);
            _logger = logger;
            _teams = teams;
            _client.OnReceiveData += OnReceiveData;
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
                return;

            Config config = ConfigReadWrite.Instance.CurrentConfig
                ?? throw new InvalidOperationException("Config is not loaded.");

            string chatIp = string.IsNullOrWhiteSpace(config.ChatIP) ? "127.0.0.1" : config.ChatIP;
            int port = config.CommPort > 0 ? config.CommPort : 6996;

            IPAddress address = IPAddress.Parse(chatIp);
            _client.Configure(address, port);
            _client.TryLinkIfChatEngineListening();
            _started = true;

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ISCom configured ChatEngine {0}:{1} linked={2}",
                    chatIp,
                    port,
                    _client.IsConnected));
        }

        public bool TrySend(MessageBase message)
        {
            ArgumentNullException.ThrowIfNull(message);
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_started)
                return false;

            try
            {
                return _client.TrySend(message);
            }
            catch (Exception exception)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ISCom TrySend failed type={0}: {1}",
                        message.GetType().FullName,
                        exception.Message));
                return false;
            }
        }

        void OnReceiveData(object sender, DynamicMessage message)
        {
            if (message?.DataObject is not ChatCommand command
                || string.IsNullOrWhiteSpace(command.ChatCommandString))
                return;

            try
            {
                _teams.Value.TryHandleInboundChatCommand(
                    command.CharacterId,
                    command.ChatCommandString);
            }
            catch (Exception exception)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ISCom inbound ChatCommand failed: {0}",
                        exception.Message));
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _client.OnReceiveData -= OnReceiveData;
            _client.ShutDown();
            _client.Dispose();
        }
    }
}
