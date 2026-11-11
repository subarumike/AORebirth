namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using AORebirth.Database.Dao;
    using AORebirth.Modules.Network;

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

    public sealed class ZoneSession : IZoneSession, IGameTimeSession, IAsyncDisposable
    {
        public DateTime? GameTimeSynchronizedAtUtc { get; private set; }
        public int GameTimeServerSeconds { get; private set; }
        public void RecordGameTimeSynchronization(DateTime utcNow, int serverSeconds)
        {
            GameTimeSynchronizedAtUtc = utcNow;
            GameTimeServerSeconds = serverSeconds;
        }
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
        // Module byte filter between the socket and the AO protocol; null keeps the stock wire path.
        private readonly IConnectionFilter? _filter;
        private readonly ArrayBufferWriter<byte>? _filteredInbound;
        private readonly ArrayBufferWriter<byte>? _filteredOutbound;
        // Module message observers; null when no module observes messages. Each loop reuses its own field reader.
        private readonly Modules.ModuleMessageObservers? _observers;
        private readonly Modules.MessageFieldReader? _inboundFields;
        private readonly Modules.MessageFieldReader? _outboundFields;
        private readonly Channel<byte[]> _sendQueue = Channel.CreateUnbounded<byte[]>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        private readonly List<byte> _receiveBuffer = new(ReceiveChunkSize);
        private readonly object _compressSync = new();
        private const int CompressedSendTimeoutMilliseconds = 60_000;
        private SocketSendStream? _sendStream;
        private ZLibStream? _zStream;
        private short _packetNumber;
        private bool _outboundCompressed;
        private volatile bool _closed;
        private PlayfieldTransfer? _transfer;
        private ZoneEngine_New.Core.World.ZoneRedirects? _handoffAuthority;
        private int _handoffCharacter;
        private uint _handoffCookie1;
        private uint _handoffCookie2;
        private string _account = string.Empty;
        private int _state;

        public ZoneSession(
            Guid id,
            Socket socket,
            ZoneMessageCodec codec,
            ZoneMessageDispatcher dispatcher,
            IZoneLogger logger,
            IConnectionFilter? filter = null,
            Modules.ModuleMessageObservers? observers = null)
        {
            ArgumentNullException.ThrowIfNull(socket);
            ArgumentNullException.ThrowIfNull(codec);
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentNullException.ThrowIfNull(logger);

            Id = id;
            _socket = socket;
            // Only the compressed path sends synchronously. A client that takes nothing for this long is gone: fail the
            // send so the session closes and the player goes LinkDead, instead of pinning a pool thread and reporting
            // the player Online until the kernel drops the connection.
            _socket.SendTimeout = CompressedSendTimeoutMilliseconds;
            _codec = codec;
            _dispatcher = dispatcher;
            _logger = logger;
            _filter = filter;
            if (filter != null)
            {
                _filteredInbound = new ArrayBufferWriter<byte>(ReceiveChunkSize);
                _filteredOutbound = new ArrayBufferWriter<byte>(MaxPacketSize);
            }

            if (observers != null)
            {
                _observers = observers;
                _inboundFields = new Modules.MessageFieldReader();
                _outboundFields = new Modules.MessageFieldReader();
            }

            ConnectedAtUtc = DateTime.UtcNow;
            Live[id] = this;
        }

        /// <summary>Open connections by id, for <see cref="TryFind"/> (module API 1.6 message counts).</summary>
        static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ZoneSession> Live = new();

        /// <summary>The open connection with this id on this instance.</summary>
        internal static bool TryFind(Guid id, out ZoneSession session) => Live.TryGetValue(id, out session!);

        /// <summary>Messages of each N3 type this connection received and sent since it opened (module API 1.6).</summary>
        readonly System.Collections.Concurrent.ConcurrentDictionary<int, MessageTally> _tallies = new();

        sealed class MessageTally
        {
            public long Inbound;
            public long Outbound;
        }

        void CountMessage(int messageType, bool inbound)
        {
            MessageTally tally = _tallies.GetOrAdd(messageType, static _ => new MessageTally());
            if (inbound)
                Interlocked.Increment(ref tally.Inbound);
            else
                Interlocked.Increment(ref tally.Outbound);
        }

        /// <summary>A copy of the per-type message counts.</summary>
        internal Dictionary<int, AORebirth.Modules.World.MessageCount> MessageCounts()
        {
            var counts = new Dictionary<int, AORebirth.Modules.World.MessageCount>(_tallies.Count);
            foreach ((int type, MessageTally tally) in _tallies)
                counts[type] = new AORebirth.Modules.World.MessageCount(Interlocked.Read(ref tally.Inbound), Interlocked.Read(ref tally.Outbound));
            return counts;
        }

        public Guid Id { get; }

        /// <summary>When the connection was accepted.</summary>
        public DateTime ConnectedAtUtc { get; }

        /// <summary>The account that owns the handed-off character, or empty before the handoff (or when World did not say).</summary>
        public string AccountName => Volatile.Read(ref _account);

        public SessionState State
        {
            get => (SessionState)Volatile.Read(ref _state);
            set
            {
                lock (this)
                {
                    if (_closed && value != SessionState.Closed)
                        throw new InvalidOperationException("A closed session cannot resume gameplay.");
                    Volatile.Write(ref _state, (int)value);
                }
            }
        }

        public Player? Player { get; private set; }

        internal void BindZoneHandoff(int character, uint cookie1, uint cookie2, ZoneEngine_New.Core.World.ZoneRedirects authority,
            string account = "")
        {
            ArgumentNullException.ThrowIfNull(authority);
            if (character <= 0 || (cookie1 == 0 && cookie2 == 0))
                throw new InvalidOperationException("A valid claimed handoff is required.");
            lock (this)
            {
                if (_closed) throw new InvalidOperationException("A closed session cannot accept a handoff.");
                _handoffCharacter = character;
                _handoffCookie1 = cookie1;
                _handoffCookie2 = cookie2;
                _handoffAuthority = authority;
                Volatile.Write(ref _account, account ?? string.Empty);
            }
        }
        public bool IsClosed => _closed;

        public void BindPlayer(Player player)
        {
            lock (this)
            {
                if (_closed) throw new InvalidOperationException("A closed session cannot own a player.");
                if (Player != null && !ReferenceEquals(Player, player))
                    throw new InvalidOperationException("A session cannot own multiple characters.");
                Player = player;
            }
        }

        public void UnbindPlayer()
        {
            lock (this)
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
            int sender = Player?.Playfield?.ClientPlayfieldId ?? 0;
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

            try
            {
                byte[] packet = _codec.Serialize(body, sender, receiver);
                LogNetworkMessage("Sent", body);
                if (body is InfoPacketMessage)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "InfoPacket bytes={0} hex={1}",
                            packet.Length,
                            Convert.ToHexString(packet)));
                }

                Send(packet);
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to serialize {0}.",
                        body.GetType().Name));
            }
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

        /// <summary>
        /// A move is on its way (scheduled here, or to another zone instance), or the session is not in play. Every
        /// mover checks this before starting a move. The session leaves InPlay the moment a move is scheduled, so the
        /// client's input stops at the same moment (<see cref="ZoneMessageDispatcher"/> drops it).
        /// </summary>
        public bool IsTransferring => _transfer != null || _remoteTransfer || State != SessionState.InPlay;

        public bool TransferToPlayfield(Playfield destination, Vector3 landing)
            => TransferToPlayfield(destination, landing, Player?.Rotation
                ?? throw new InvalidOperationException("Session has no bound player."));

        public bool TransferToPlayfield(Playfield destination, Vector3 landing, AORebirth.Core.Vector.Quaternion heading)
            => TransferToPlayfield(destination, landing, heading, null);

        /// <summary>
        /// Schedules the move on the source playfield's transfer tick and takes the session out of play at once, so
        /// neither a second move (a door used and walked into in the same tick) nor further client input can start
        /// before it departs. False when a move is already on its way: the first move stands. Throws only for a
        /// session that cannot move at all (no player, closed, not in play, quarantined, same playfield).
        /// </summary>
        internal bool TransferToPlayfield(Playfield destination, Vector3 landing,
            AORebirth.Core.Vector.Quaternion heading, Func<bool>? stillAuthorized, bool clearReturnDoor = false)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(landing);
            ArgumentNullException.ThrowIfNull(heading);
            // Nothing to stand on there: the character would hang in the air, unable to cast. Stay put instead.
            if (Player is { } mover && destination.RefuseArrivalWithoutCollision(mover))
                return false;

            PlayfieldAdmission? destinationAdmission = destination.TryAcquireAdmission()
                ?? throw new ObjectDisposedException(nameof(destination));
            try
            {
                lock (this)
                {
                    Player player = Player ?? throw new InvalidOperationException("Session has no bound player.");
                    Playfield source = player.Playfield ?? throw new InvalidOperationException("Player is not on a playfield.");
                    if (_transfer != null || _remoteTransfer)
                    {
                        _logger.Info(string.Format(CultureInfo.InvariantCulture,
                            "Transfer to playfield {0} not started for character {1}: a transfer is already on its way",
                            destination.Identity.Instance, player.Identity.Instance));
                        return false;
                    }

                    if (_closed || State != SessionState.InPlay || !ReferenceEquals(player.Session, this)
                        || player.IsPersistenceQuarantined)
                        throw new InvalidOperationException("Session is not the current playable transfer owner.");
                    if (ReferenceEquals(source, destination))
                        throw new InvalidOperationException("Destination playfield matches current playfield.");
                    var transfer = new PlayfieldTransfer(this, player, source, destination, landing, heading,
                        destinationAdmission, stillAuthorized, clearReturnDoor);
                    _transfer = transfer;
                    if (!source.ScheduleTransfer(transfer))
                    {
                        _transfer = null;
                        throw new ObjectDisposedException(nameof(source));
                    }

                    // Out of play from here: the same step a move to another zone instance takes in TryBeginRemoteTransfer.
                    // FinishTransfer puts the session back in play if the move never departs or returns the character.
                    State = SessionState.Loading;
                    destinationAdmission = null;
                    return true;
                }
            }
            finally
            {
                destinationAdmission?.Dispose();
            }
        }

        /// <param name="destinationId">The destination as the client knows it (<see cref="Playfield.ClientPlayfieldId"/>).</param>
        internal byte[][] PrepareTransferPackets(Player player, Vector3 landing,
            AORebirth.Core.Vector.Quaternion heading, int destinationId)
        {
            ZoneRedirectEndpoint target = ZoneRedirectEndpoint.Configured();
            AuthorizeZoneRedirect(player.Identity.Instance, target);
            return
            [
                _codec.Serialize(BuildNormalTeleport(player, landing, destinationId, heading),
                    destinationId, player.Identity.Instance),
                _codec.Serialize(new ZoneRedirectionMessage { ServerIpAddress = target.Address, ServerPort = target.Port },
                    destinationId, player.Identity.Instance)
            ];
        }

        /// <summary>
        /// The transfer is over. <paramref name="playerStays"/>: it never departed, or brought the character back, so
        /// the session plays on here and goes back into play (undoing the Loading set when it was scheduled).
        /// </summary>
        internal void FinishTransfer(PlayfieldTransfer transfer, bool playerStays)
        {
            if (!ReferenceEquals(_transfer, transfer))
                return;

            _transfer = null;
            if (playerStays && !_closed && State == SessionState.Loading && Player is { } player && ReferenceEquals(player.Session, this))
                State = SessionState.InPlay;
        }

        private bool _remoteTransfer;

        /// <summary>A move to another zone instance starts: no other transfer may start, and input stops.</summary>
        internal bool TryBeginRemoteTransfer(Player player)
        {
            lock (this)
            {
                if (_closed || State != SessionState.InPlay || !ReferenceEquals(Player, player)
                    || !ReferenceEquals(player.Session, this) || _transfer != null || _remoteTransfer
                    || player.IsPersistenceQuarantined)
                    return false;
                _remoteTransfer = true;
                State = SessionState.Loading;
                return true;
            }
        }

        /// <summary>The move did not happen: the player plays on here.</summary>
        internal void AbortRemoteTransfer()
        {
            lock (this)
            {
                _remoteTransfer = false;
                if (!_closed && Player != null) State = SessionState.InPlay;
            }
        }

        /// <summary>The N3Teleport and the redirect to the instance that now owns the character.</summary>
        internal byte[][] PrepareRemoteTransferPackets(Player player, Vector3 landing,
            AORebirth.Core.Vector.Quaternion heading, int destinationId, IPAddress address, ushort port)
            =>
            [
                _codec.Serialize(BuildNormalTeleport(player, landing, destinationId, heading),
                    destinationId, player.Identity.Instance),
                _codec.Serialize(new ZoneRedirectionMessage { ServerIpAddress = address, ServerPort = port },
                    destinationId, player.Identity.Instance)
            ];

        public void SendSamePlayfieldRespawnTeleport(Vector3 landing)
        {
            ArgumentNullException.ThrowIfNull(landing);

            Player? player = Player;
            if (player == null)
                throw new InvalidOperationException("Session has no bound player.");

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                throw new InvalidOperationException("Player is not on a playfield.");

            int playfieldId = playfield.ClientPlayfieldId;
            Send(
                BuildNormalTeleport(player, landing, playfieldId, player.Rotation),
                playfieldId,
                player.Identity.Instance);
        }

        public void SendIntrazoneTeleport(Vector3 landing, AORebirth.Core.Vector.Quaternion heading, int destinationKey)
        {
            ArgumentNullException.ThrowIfNull(landing);
            ArgumentNullException.ThrowIfNull(heading);

            Player? player = Player;
            if (player == null)
                throw new InvalidOperationException("Session has no bound player.");

            Playfield? playfield = player.Playfield;
            if (playfield == null)
                throw new InvalidOperationException("Player is not on a playfield.");

            Send(
                BuildIntrazoneTeleport(player, landing, heading, destinationKey),
                playfield.ClientPlayfieldId,
                player.Identity.Instance);
        }

        /// <summary>
        /// N3.dll n3TeleportIIR_t::Activate branches on ChangePlayfield.Instance: 0 applies
        /// UpdateLastAllowedPosition + SetRelPosRot in place; anything else runs StartTeleport and
        /// unloads the zone. ReadSubClass rejects a playfield-proxy version other than 'a' and caps
        /// the trailing blob at 1000 bytes; the soft branch never reads the blob.
        /// </summary>
        internal static N3TeleportMessage BuildIntrazoneTeleport(Player player, Vector3 landing,
            AORebirth.Core.Vector.Quaternion heading, int destinationKey)
        {
            const IdentityType intrazonePlayfieldProxyType = (IdentityType)51104;

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
                    X = heading.xf,
                    Y = heading.yf,
                    Z = heading.zf,
                    W = heading.wf
                },
                Unknown1 = 0x61,
                Playfield = new Identity { Type = intrazonePlayfieldProxyType, Instance = 0 },
                GameServerId = 0,
                SgId = 0,
                ChangePlayfield = Identity.None,
                Unknown4 = 0,
                Unknown5 = 0,
                Playfield2 = new Identity { Type = IdentityType.Playfield3, Instance = destinationKey },
                Payload = new byte[8]
            };
        }

        /// <summary>
        /// <paramref name="destPlayfieldId"/> is the playfield as the client knows it: a door's proxy instance is sent as
        /// the interior it copies (<see cref="Playfield.ClientPlayfieldId"/>), so the client zones as into that interior.
        /// </summary>
        private static N3TeleportMessage BuildNormalTeleport(Player player, Vector3 landing, int destPlayfieldId,
            AORebirth.Core.Vector.Quaternion heading)
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
                    X = heading.xf,
                    Y = heading.yf,
                    Z = heading.zf,
                    W = heading.wf
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

        private void AuthorizeZoneRedirect(int character, ZoneRedirectEndpoint target)
        {
            if (_handoffAuthority == null || _handoffCharacter != character)
                throw new InvalidOperationException("Session has no claimed redirect authority.");
            _ = target;
            _handoffAuthority.Authorize(character, _handoffCookie1, _handoffCookie2, _account);
        }

        /// <summary>The cookies this client logged in with; it presents them again to whichever instance it moves to.</summary>
        internal bool TryGetHandoffCookies(int character, out uint cookie1, out uint cookie2)
        {
            lock (this)
            {
                cookie1 = _handoffCookie1;
                cookie2 = _handoffCookie2;
                return _handoffCharacter == character && (cookie1 != 0 || cookie2 != 0);
            }
        }

        public void Close()
        {
            lock (this)
            {
                CloseCore();
            }
        }

        private void CloseCore()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            State = SessionState.Closed;
            Live.TryRemove(new KeyValuePair<Guid, ZoneSession>(Id, this));
            _handoffAuthority = null;
            _handoffCharacter = 0;
            _handoffCookie1 = 0;
            _handoffCookie2 = 0;
            Playfield? transferSource = _transfer?.Source;
            _transfer?.RequestReturn();
            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneSession closed remote={0} character={1} state={2}",
                    RemoteEndPoint(),
                    Player?.Identity.Instance ?? 0,
                    State));
            _sendQueue.Writer.TryComplete();
            // The receive loop owns this buffer; do not mutate its List from a closing thread.

            // The socket goes first. A send loop stuck in a compressed Send to a dead client holds _compressSync until
            // the kernel gives up on the connection (~15 min); closing the socket fails that Send at once. Disposing
            // the streams first waited on that lock, and zlib's final block wrote to the same full socket: a reconnect
            // stealing such a session froze its playfield tick (Lost Eden 2026-11-04, pf 2132411023, 7+ minutes).
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

            DisposeCompressionStreams();

            if (Player != null)
            {
                Player owned = Player;
                if (ReferenceEquals(owned.Session, this))
                {
                    // Close can race the final PF shutdown/snapshot. The manager-owned team
                    // authority outlives its disposed child provider and fences exact owners.
                    (owned.Playfield ?? transferSource)?.NotifyTransportDisconnected(owned, this);
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

                // Both loops have ended, so nothing is inside the filter any more.
                if (_filter is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch (Exception exception) { _logger.Error(exception, "Connection filter failed to dispose."); }
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

                    // Arrival time for every message in this read: what observers see, not when the tick runs it.
                    long received = Stopwatch.GetTimestamp();
                    if (_filter == null)
                    {
                        for (int i = 0; i < read; i++)
                            _receiveBuffer.Add(chunk[i]);
                    }
                    else if (!AppendFiltered(chunk.AsSpan(0, read)))
                    {
                        Close();
                        return;
                    }

                    while (!_closed && TryPopPacket(out byte[] packet, out bool invalidPacket))
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
                                        "Unknown or empty zone message id={0} ({0:X8}) from {1} length={2} bytes={3}",
                                        TryReadN3MessageId(packet),
                                        RemoteEndPoint(),
                                        packet.Length,
                                        // Whole packet (capped) so an unimplemented client request can be decoded from the log.
                                        Convert.ToHexString(packet, 0, Math.Min(packet.Length, 256))));
                                continue;
                            }

                            LogNetworkMessage("Received", message.Body);
                            if (message.Body is N3Message counted)
                                CountMessage((int)counted.N3MessageType, inbound: true);
                            if (_observers != null)
                                ObserveInbound(message.Body, packet, received);
                            _dispatcher.Dispatch(message, this, received);
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

        /// <summary>Runs raw socket bytes through the module filter and appends the AO bytes it yields.</summary>
        private bool AppendFiltered(ReadOnlySpan<byte> raw)
        {
            ArrayBufferWriter<byte> plaintext = _filteredInbound!;
            plaintext.ResetWrittenCount();
            try
            {
                if (!_filter!.Inbound(raw, plaintext))
                {
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Connection filter dropped connection from {0}", RemoteEndPoint()));
                    return false;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Connection filter failed on inbound bytes from " + RemoteEndPoint() + ".");
                return false;
            }

            ReadOnlySpan<byte> written = plaintext.WrittenSpan;
            for (int i = 0; i < written.Length; i++)
                _receiveBuffer.Add(written[i]);
            return true;
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
                    if (packet.Length >= HeaderLength + 4
                        && BinaryPrimitives.ReadInt16BigEndian(packet.AsSpan(2)) == (short)PacketType.N3Message)
                        CountMessage((int)TryReadN3MessageId(packet), inbound: false);
                    if (_observers != null)
                        ObserveOutbound(packet);

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
            // Filtered into the session's own buffer, never in place: queued packets can be shared arrays.
            ReadOnlyMemory<byte> wire = packet;
            if (_filter != null)
            {
                _filteredOutbound!.ResetWrittenCount();
                _filter.Outbound(packet, _filteredOutbound);
                wire = _filteredOutbound.WrittenMemory;
            }

            int offset = 0;
            while (offset < wire.Length)
            {
                int sent = await _socket.SendAsync(
                        wire[offset..],
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
            _sendStream = new SocketSendStream(_socket, _filter);
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
            private readonly IConnectionFilter? _filter;
            private readonly ArrayBufferWriter<byte>? _filtered;

            public SocketSendStream(Socket socket, IConnectionFilter? filter)
            {
                _socket = socket;
                _filter = filter;
                if (filter != null)
                    _filtered = new ArrayBufferWriter<byte>(MaxPacketSize);
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
                ReadOnlySpan<byte> wire = buffer.AsSpan(offset, count);
                if (_filter != null)
                {
                    // zlib output goes through the module filter after compression, in wire order.
                    _filtered!.ResetWrittenCount();
                    _filter.Outbound(wire, _filtered);
                    wire = _filtered.WrittenSpan;
                }

                int sent = 0;
                while (sent < wire.Length)
                {
                    int n = _socket.Send(wire[sent..], SocketFlags.None);
                    if (n <= 0)
                        throw new IOException("Socket closed during compressed send.");
                    sent += n;
                }
            }
        }

        /// <summary>The client's address, for per-address limits; null once the socket is gone.</summary>
        internal IPAddress? RemoteAddress()
        {
            try
            {
                return (_socket.RemoteEndPoint as IPEndPoint)?.Address;
            }
            catch (ObjectDisposedException)
            {
                return null;
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

        /// <summary>
        /// Shows an inbound N3 message to the module observers subscribed to its type, once the session is in play. Runs
        /// on the receive loop, before the message is dispatched; observers only watch.
        /// </summary>
        private void ObserveInbound(MessageBody body, byte[] packet, long received)
        {
            if (body is not N3Message n3 || State != SessionState.InPlay)
                return;

            int type = (int)n3.N3MessageType;
            if (!_observers!.Wants(MessageDirection.Inbound, type))
                return;

            _inboundFields!.Reset(body);
            try
            {
                _observers.Publish(new AORebirth.Modules.Network.MessageSample(Id, Player?.Identity.Instance ?? 0,
                    MessageDirection.Inbound, type, received, packet, _inboundFields));
            }
            finally
            {
                _inboundFields.Reset(null);
            }
        }

        /// <summary>
        /// Shows an outbound N3 message to the module observers subscribed to its type, on the send loop just before it is
        /// written. Its fields are parsed back from the bytes only if an observer reads one.
        /// </summary>
        private void ObserveOutbound(byte[] packet)
        {
            if (!_observers!.HasOutbound || packet.Length < HeaderLength + 4
                || BinaryPrimitives.ReadInt16BigEndian(packet.AsSpan(2)) != (short)PacketType.N3Message)
                return;

            int type = (int)TryReadN3MessageId(packet);
            if (!_observers.Wants(MessageDirection.Outbound, type))
                return;

            _outboundFields!.ResetDeferred(packet, _codec);
            try
            {
                _observers.Publish(new AORebirth.Modules.Network.MessageSample(Id, Player?.Identity.Instance ?? 0,
                    MessageDirection.Outbound, type, Stopwatch.GetTimestamp(), packet, _outboundFields));
            }
            finally
            {
                _outboundFields.Reset(null);
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
