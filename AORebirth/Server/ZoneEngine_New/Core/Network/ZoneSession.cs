namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.IO.Pipelines;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    public sealed class ZoneSession : IZoneSession, IAsyncDisposable
    {
        private const int HeaderLength = 16;
        private const int ReceiveChunkSize = 4096;
        private const int MaxPacketSize = 8192;

        // Hardcoded InitiateCompression negotiate packet from legacy ZoneClient — do not alter.
        // RecvCompression=Yes (Server→Client zlib), SendCompression=No (Client→Server plaintext).
        private static readonly byte[] InitiateCompressionPacket =
        [
            0xdf, 0xdf,
            0x7f, 0x00,
            0x00, 0x01,
            0x00, 0x10,
            0x01, 0x00,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        private readonly Socket _socket;
        private readonly ZoneMessageCodec _codec;
        private readonly ZoneMessageDispatcher _dispatcher;
        private readonly IZoneLogger _logger;
        private readonly Channel<byte[]> _sendQueue = Channel.CreateUnbounded<byte[]>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        private readonly Pipe _receivePipe = new();
        private readonly object _compressSync = new();
        private SocketSendStream? _sendStream;
        private ZLibStream? _zStream;
        private short _packetNumber;
        private bool _outboundCompressed;
        private volatile bool _closed;

        public ZoneSession(
            Guid id,
            Socket socket,
            ZoneMessageCodec codec,
            ZoneMessageDispatcher dispatcher,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(socket);
            ArgumentNullException.ThrowIfNull(codec);
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentNullException.ThrowIfNull(logger);

            Id = id;
            _socket = socket;
            _codec = codec;
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public Guid Id { get; }

        public SessionState State { get; set; } = SessionState.Connected;

        public Player? Player { get; private set; }

        public void BindPlayer(Player player)
        {
            Player = player;
        }

        public void UnbindPlayer()
        {
            Player = null;
        }

        public void Send(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
            {
                return;
            }

            if (_closed)
            {
                LogSendDropped("raw packet");
                return;
            }

            _sendQueue.Writer.TryWrite(packet);
        }

        public void Send(Message message)
        {
            if (message == null)
            {
                return;
            }

            if (_closed)
            {
                LogSendDropped(message.Body?.GetType().Name ?? "Message");
                return;
            }

            LogNetworkMessage("Sent", message.Body);
            Send(_codec.Serialize(message));
        }

        public void Send(MessageBody body)
        {
            if (body == null)
            {
                return;
            }

            if (_closed)
            {
                LogSendDropped(body.GetType().Name);
                return;
            }

            int receiver = Player?.Identity.Instance ?? 0;
            int sender = Player?.Playfield?.Identity.Instance ?? 0;
            Send(body, sender, receiver);
        }

        public void Send(MessageBody body, int sender, int receiver)
        {
            if (body == null)
            {
                return;
            }

            if (_closed)
            {
                LogSendDropped(body.GetType().Name);
                return;
            }

            LogNetworkMessage("Sent", body);
            Send(_codec.Serialize(body, sender, receiver));
        }

        public void SendInitiateCompression()
        {
            if (_closed)
            {
                LogSendDropped("InitiateCompression");
                return;
            }

            LogUtil.Debug(DebugInfoDetail.Network, "Sent InitiateCompression");
            Send(InitiateCompressionPacket);
        }

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneSession closed remote={0} character={1} state={2}",
                    RemoteEndPoint(),
                    Player?.Identity.Instance ?? 0,
                    State));
            _sendQueue.Writer.TryComplete();

            DisposeCompressionStreams();

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                _socket.Close();
            }
            catch
            {
            }

            if (Player != null)
            {
                Player owned = Player;
                if (ReferenceEquals(owned.Session, this))
                {
                    owned.EnterLinkDead(PlayfieldManager.ResolveLinkDeadTimeout());
                    _logger.Info(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Player {0} entered LinkDead until {1:o}",
                            owned.Identity.Instance,
                            owned.LinkDeadUntilUtc));
                }

                Player = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Close();
            await _receivePipe.Reader.CompleteAsync().ConfigureAwait(false);
            await _receivePipe.Writer.CompleteAsync().ConfigureAwait(false);
        }

        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            Task fillPipe = FillReceivePipeAsync(cancellationToken);
            Task sendLoop = SendLoopAsync(cancellationToken);

            try
            {
                await ConsumePacketsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_closed)
                {
                    _logger.Error(exception, "ZoneSession receive failed.");
                }
            }
            finally
            {
                Close();
                await _receivePipe.Writer.CompleteAsync().ConfigureAwait(false);

                try
                {
                    await fillPipe.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }

                try
                {
                    await sendLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }
        }

        private async Task FillReceivePipeAsync(CancellationToken cancellationToken)
        {
            // Client→Server is always plaintext; do not inflate inbound bytes.
            byte[] chunk = new byte[ReceiveChunkSize];
            try
            {
                while (!_closed && !cancellationToken.IsCancellationRequested)
                {
                    int read = await _socket.ReceiveAsync(chunk, SocketFlags.None, cancellationToken)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        _logger.Info(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "ZoneSession client disconnected remote={0} character={1} state={2}",
                                RemoteEndPoint(),
                                Player?.Identity.Instance ?? 0,
                                State));
                        break;
                    }

                    await _receivePipe.Writer.WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_closed)
                {
                    _logger.Error(exception, "ZoneSession socket read failed.");
                }
            }
            finally
            {
                await _receivePipe.Writer.CompleteAsync().ConfigureAwait(false);
            }
        }

        private async Task ConsumePacketsAsync(CancellationToken cancellationToken)
        {
            while (!_closed && !cancellationToken.IsCancellationRequested)
            {
                ReadResult readResult = await _receivePipe.Reader.ReadAsync(cancellationToken)
                    .ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                while (TryExtractPacket(ref buffer, out byte[] packet, out bool invalidPacket))
                {
                    if (invalidPacket)
                    {
                        _logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Invalid packet from {0}",
                                RemoteEndPoint()));
                        Close();
                        _receivePipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                        return;
                    }

                    try
                    {
                        Message? message = _codec.Deserialize(packet);
                        if (message?.Body == null)
                        {
                            _logger.Warn(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Unknown or empty zone message id={0} ({0:X8}) from {1} header={2}",
                                    TryReadN3MessageId(packet),
                                    RemoteEndPoint(),
                                    Convert.ToHexString(packet, 0, Math.Min(packet.Length, 24))));
                            continue;
                        }

                        LogNetworkMessage("Received", message.Body);
                        _dispatcher.Dispatch(message, this);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(exception, "Failed to dispatch zone packet.");
                        Close();
                        _receivePipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                        return;
                    }
                }

                _receivePipe.Reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted)
                {
                    break;
                }
            }
        }

        private static bool TryExtractPacket(
            ref ReadOnlySequence<byte> buffer,
            out byte[] packet,
            out bool invalidPacket)
        {
            packet = Array.Empty<byte>();
            invalidPacket = false;

            while (buffer.Length >= HeaderLength)
            {
                Span<byte> header = stackalloc byte[HeaderLength];
                buffer.Slice(0, HeaderLength).CopyTo(header);

                short packetType = BinaryPrimitives.ReadInt16BigEndian(header[2..]);
                short size = BinaryPrimitives.ReadInt16BigEndian(header[6..]);

                if (!IsPlausibleFrame(packetType, size))
                {
                    // Resync: Client→Server is plaintext AO frames; skip junk (e.g. leading NULs).
                    buffer = buffer.Slice(1);
                    continue;
                }

                if (buffer.Length < size)
                    return false;

                packet = buffer.Slice(0, size).ToArray();
                buffer = buffer.Slice(size);
                return true;
            }

            return false;
        }

        private static bool IsPlausibleFrame(short packetType, short size)
        {
            if (size < HeaderLength || size > MaxPacketSize)
                return false;

            return packetType is (short)PacketType.SystemMessage
                or (short)PacketType.TextMessage
                or (short)PacketType.N3Message
                or (short)PacketType.PingMessage
                or (short)PacketType.OperatorMessage
                or (short)PacketType.InitiateCompressionMessage;
        }

        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (byte[] packet in _sendQueue.Reader.ReadAllAsync(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    if (_outboundCompressed)
                    {
                        WriteCompressed(packet);
                        continue;
                    }

                    await SendExactAsync(packet, cancellationToken).ConfigureAwait(false);

                    // InitiateCompression is plaintext; afterward only Server→Client is zlib.
                    // Client→Server stays plaintext for the life of the session.
                    if (IsInitiateCompressionPacket(packet))
                    {
                        EnableOutboundCompression();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_closed)
                {
                    _logger.Error(exception, "ZoneSession send failed.");
                    Close();
                }
            }
        }

        private async Task SendExactAsync(byte[] packet, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < packet.Length)
            {
                int sent = await _socket.SendAsync(
                        packet.AsMemory(offset, packet.Length - offset),
                        SocketFlags.None,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (sent <= 0)
                {
                    throw new IOException("Socket closed during send.");
                }

                offset += sent;
            }
        }

        private void EnableOutboundCompression()
        {
            if (_outboundCompressed)
                return;

            // Server→Client zlib only. Do NOT wrap the live socket in NetworkStream — that races
            // with Socket.ReceiveAsync on Client→Server plaintext and corrupts the receive buffer.
            _sendStream = new SocketSendStream(_socket);
            _zStream = new ZLibStream(_sendStream, CompressionLevel.Fastest, leaveOpen: true);
            _packetNumber = 1;
            _outboundCompressed = true;
        }

        private void WriteCompressed(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (buffer.Length < 2)
                return;

            ZLibStream? zStream = _zStream;
            if (zStream == null)
                throw new InvalidOperationException("Outbound compression stream is not ready.");

            lock (_compressSync)
            {
                byte[] packetNumberBytes = BitConverter.GetBytes(_packetNumber++);
                buffer[0] = packetNumberBytes[1];
                buffer[1] = packetNumberBytes[0];
                zStream.Write(buffer, 0, buffer.Length);
                zStream.Flush();
            }
        }

        private static bool IsInitiateCompressionPacket(byte[] packet)
        {
            if (packet.Length != InitiateCompressionPacket.Length)
                return false;

            for (int i = 0; i < InitiateCompressionPacket.Length; i++)
            {
                if (packet[i] != InitiateCompressionPacket[i])
                    return false;
            }

            return true;
        }

        private void DisposeCompressionStreams()
        {
            lock (_compressSync)
            {
                try
                {
                    _zStream?.Dispose();
                }
                catch
                {
                }

                _zStream = null;

                try
                {
                    _sendStream?.Dispose();
                }
                catch
                {
                }

                _sendStream = null;
                _outboundCompressed = false;
            }
        }

        /// <summary>
        /// Write-only stream over <see cref="Socket.Send"/> so zlib outbound never shares a
        /// <see cref="NetworkStream"/> with the plaintext receive path.
        /// </summary>
        private sealed class SocketSendStream : Stream
        {
            private readonly Socket _socket;

            public SocketSendStream(Socket socket)
            {
                _socket = socket;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                int sent = 0;
                while (sent < count)
                {
                    int n = _socket.Send(buffer, offset + sent, count - sent, SocketFlags.None);
                    if (n <= 0)
                        throw new IOException("Socket closed during compressed send.");
                    sent += n;
                }
            }
        }

        internal string RemoteEndPoint()
        {
            try
            {
                return _socket.RemoteEndPoint?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static uint TryReadN3MessageId(byte[] packet)
        {
            if (packet.Length < HeaderLength + 4)
                return 0;

            return BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(HeaderLength, 4));
        }

        private static void LogNetworkMessage(string direction, MessageBody? body)
        {
            if (body == null)
                return;

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    direction,
                    body.GetType().Name));
        }

        private void LogSendDropped(string what)
        {
            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Send dropped (disconnected) {0} remote={1} character={2} state={3}",
                    what,
                    RemoteEndPoint(),
                    Player?.Identity.Instance ?? 0,
                    State));
        }
    }
}
