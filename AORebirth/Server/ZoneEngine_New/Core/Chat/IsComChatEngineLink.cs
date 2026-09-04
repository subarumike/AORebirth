namespace ZoneEngine_New.Core.Chat
{
    using System;
    using System.Globalization;
    using System.Net;

    using AORebirth.Communication.ISComV2Client;
    using AORebirth.Communication.Messages;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;

    using ConfigReadWrite = Utility.Config.ConfigReadWrite;

    public interface IChatEngineLink : IDisposable
    {
        void Start();

        bool TrySend(MessageBase message);
    }

    /// <summary>
    /// Zone→ChatEngine ISCom link for vicinity (and later system chat) traffic.
    /// </summary>
    public sealed class IsComChatEngineLink : IChatEngineLink
    {
        private readonly IZoneLogger _logger;
        private readonly ISComV2Client _client = new ISComV2Client();
        private bool _started;
        private bool _disposed;

        public IsComChatEngineLink(IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _client.OnReceiveData += (_, _) => { };
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _client.ShutDown();
            _client.Dispose();
        }
    }
}
