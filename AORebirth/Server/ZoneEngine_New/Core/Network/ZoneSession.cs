namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility;
    using Utility.Config;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Vector3 = AORebirth.Core.Vector.Vector3;

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
        private readonly List<byte> _receiveBuffer = new(ReceiveChunkSize);
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
            byte[] packet = _codec.Serialize(body, sender, receiver);
            Send(packet);
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

        public void TransferToPlayfield(Playfield destination, Vector3 landing)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(landing);

            Player? player = Player;
            if (player == null)
                throw new InvalidOperationException("Session has no bound player.");

            Playfield? source = player.Playfield;
            if (source == null)
                throw new InvalidOperationException("Player is not on a playfield.");

            if (ReferenceEquals(source, destination))
                throw new InvalidOperationException("Destination playfield matches current playfield.");

            int characterId = player.Identity.Instance;
            int destId = destination.Identity.Instance;

            source.LeaveTransferredPlayer(player);
            destination.ArriveTransferredPlayer(player, landing);

            Send(
                BuildNormalTeleport(player, landing, destId),
                destId,
                characterId);
            Send(
                BuildZoneRedirection(),
                destId,
                characterId);

            player.Session = null;
            UnbindPlayer();

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield transfer character={0} from={1} to={2} landing=({3},{4},{5})",
                    characterId,
                    source.Identity.Instance,
                    destId,
                    landing.xf,
                    landing.yf,
                    landing.zf));
        }

        private static N3TeleportMessage BuildNormalTeleport(Player player, Vector3 landing, int destPlayfieldId)
        {
            const IdentityType livePlayfieldProxyType = (IdentityType)0x0000C79E;

            byte[] payload = new byte[12];
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(0, 4), landing.xf);
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(4, 4), landing.yf);
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(8, 4), landing.zf);

            return new N3TeleportMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Destination = new MsgVector3
                {
                    X = landing.xf,
                    Y = landing.yf,
                    Z = landing.zf
                },
                Heading = new MsgQuaternion
                {
                    X = player.Rotation.xf,
                    Y = player.Rotation.yf,
                    Z = player.Rotation.zf,
                    W = player.Rotation.wf
                },
                Unknown1 = 0x61,
                Playfield = new Identity
                {
                    Type = livePlayfieldProxyType,
                    Instance = destPlayfieldId
                },
                GameServerId = 1,
                SgId = 0,
                ChangePlayfield = new Identity
                {
                    Type = IdentityType.Playfield2,
                    Instance = destPlayfieldId
                },
                Unknown4 = 0,
                Unknown5 = 0,
                Playfield2 = Identity.None,
                Payload = payload
            };
        }

        private static ZoneRedirectionMessage BuildZoneRedirection()
        {
            Config? config = ConfigReadWrite.Instance.CurrentConfig;
            string host = config == null || string.IsNullOrWhiteSpace(config.ZoneIP)
                ? "127.0.0.1"
                : config.ZoneIP;
            int port = config == null || config.ZonePort <= 0 ? 7501 : config.ZonePort;

            return new ZoneRedirectionMessage
            {
                ServerIpAddress = ResolveZoneRedirectAddress(host),
                ServerPort = (ushort)port
            };
        }

        private static IPAddress ResolveZoneRedirectAddress(string host)
        {
            if (IPAddress.TryParse(host, out IPAddress? parsed))
                return parsed;

            foreach (IPAddress ip in Dns.GetHostEntry(host).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            return IPAddress.Loopback;
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
            _receiveBuffer.Clear();

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

        public ValueTask DisposeAsync()
        {
            Close();
            return ValueTask.CompletedTask;
        }

        internal async Task RunAsync(CancellationToken cancellationToken)
        {
            Task sendLoop = SendLoopAsync(cancellationToken);

            try
            {
                await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
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

                try
                {
                    await sendLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            // Client→Server is always plaintext; do not inflate inbound bytes.
            // Append socket reads into _receiveBuffer; only process once a full frame is present.
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

                    for (int i = 0; i < read; i++)
                        _receiveBuffer.Add(chunk[i]);

                    while (TryPopPacket(out byte[] packet, out bool invalidPacket))
                    {
                        if (invalidPacket)
                        {
                            _logger.Warn(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Invalid packet from {0}",
                                    RemoteEndPoint()));
                            Close();
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
                            return;
                        }
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
                    _logger.Error(exception, "ZoneSession socket read failed.");
                }
            }
        }

        /// <summary>
        /// Client→Server plaintext frames are <c>Size</c> bytes (full frame from byte 0), then 0–3
        /// padding bytes so the on-wire length is 4-byte aligned. Padding is not part of <c>Size</c>
        /// and is not present on zlib streams.
        /// </summary>
        private bool TryPopPacket(out byte[] packet, out bool invalidPacket)
        {
            packet = Array.Empty<byte>();
            invalidPacket = false;

            if (_receiveBuffer.Count < HeaderLength)
                return false;

            Span<byte> header = stackalloc byte[HeaderLength];
            for (int i = 0; i < HeaderLength; i++)
                header[i] = _receiveBuffer[i];

            short packetType = BinaryPrimitives.ReadInt16BigEndian(header[2..]);
            short size = BinaryPrimitives.ReadInt16BigEndian(header[6..]);

            if (!IsPlausibleFrame(packetType, size))
            {
                invalidPacket = true;
                return true;
            }

            int padding = PlaintextFramePadding(size);
            int onWireLength = size + padding;
            if (_receiveBuffer.Count < onWireLength)
                return false;

            packet = new byte[size];
            _receiveBuffer.CopyTo(0, packet, 0, size);
            _receiveBuffer.RemoveRange(0, onWireLength);
            return true;
        }

        /// <summary>Bytes to skip after a plaintext frame so the next frame starts on a 4-byte boundary.</summary>
        private static int PlaintextFramePadding(int size)
        {
            int rem = size % 4;
            return rem == 0 ? 0 : 4 - rem;
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

                    await SendPlaintextFrameAsync(packet, cancellationToken).ConfigureAwait(false);

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

        private async Task SendPlaintextFrameAsync(byte[] packet, CancellationToken cancellationToken)
        {
            await SendExactAsync(packet, cancellationToken).ConfigureAwait(false);

            int padding = PlaintextFramePadding(packet.Length);
            if (padding == 0)
                return;

            // Match client plaintext framing: pad with NULs to 4-byte alignment (not included in Size).
            byte[] pad = new byte[padding];
            await SendExactAsync(pad, cancellationToken).ConfigureAwait(false);
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
