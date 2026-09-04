namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;

    public sealed class ZoneNetworkHost : IAsyncDisposable
    {
        private readonly ZoneMessageCodec _codec;
        private readonly ZoneMessageDispatcher _dispatcher;
        private readonly IZoneLogger _logger;
        private readonly ConcurrentDictionary<Guid, ZoneSession> _sessions = new();
        private readonly CancellationTokenSource _cts = new();
        private Socket? _listener;
        private Task? _acceptLoop;
        private volatile bool _started;
        private bool _disposed;

        public ZoneNetworkHost(
            ZoneMessageCodec codec,
            ZoneMessageDispatcher dispatcher,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(codec);
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentNullException.ThrowIfNull(logger);

            _codec = codec;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            Config config = ConfigReadWrite.Instance.CurrentConfig;
            string host = config == null || string.IsNullOrWhiteSpace(config.ZoneIP)
                ? "127.0.0.1"
                : config.ZoneIP;
            int port = config == null || config.ZonePort <= 0 ? 7501 : config.ZonePort;

            if (!IPAddress.TryParse(host, out IPAddress? address))
            {
                address = IPAddress.Any;
            }

            _listener = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(address, port));
            _listener.Listen(backlog: 256);
            _started = true;

            _acceptLoop = AcceptLoopAsync(_cts.Token);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneNetworkHost listening on {0}:{1}",
                    address,
                    port));
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _cts.CancelAsync().ConfigureAwait(false);

            if (_listener != null)
            {
                try
                {
                    _listener.Close();
                }
                catch
                {
                }
            }

            if (_acceptLoop != null)
            {
                try
                {
                    await _acceptLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            foreach (ZoneSession session in _sessions.Values)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            _sessions.Clear();
            _cts.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            Socket listener = _listener ?? throw new InvalidOperationException("Listener not started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Socket socket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    Guid sessionId = Guid.NewGuid();
                    ZoneSession session = new ZoneSession(
                        sessionId,
                        socket,
                        _codec,
                        _dispatcher,
                        _logger);

                    _sessions[sessionId] = session;

                    _logger.Info(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Accepted zone connection from {0}",
                            session.RemoteEndPoint()));

                    _ = RunSessionAsync(session, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested || _disposed)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "AcceptLoop error.");
                }
            }
        }

        private async Task RunSessionAsync(ZoneSession session, CancellationToken cancellationToken)
        {
            try
            {
                await session.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sessions.TryRemove(session.Id, out _);
            }
        }
    }
}
