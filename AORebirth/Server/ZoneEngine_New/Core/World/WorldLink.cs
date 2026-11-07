namespace ZoneEngine_New.Core.World
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using AORebirth.World.Contracts;
    using AORebirth.World.Contracts.V1;

    using Grpc.Core;

    using Utility.Config;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// This zone's connection to World and the leases World granted it. The registry stream is the instance's own
    /// lease: while heartbeats are acknowledged the zone may write its characters; past the deadline it must not.
    /// Nothing here blocks a playfield tick: lease checks are local, releases are queued.
    /// </summary>
    public sealed class WorldLink : IDisposable
    {
        static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Log everyone out this long before the lease deadline, while their saves are still owned.</summary>
        static readonly TimeSpan FenceMargin = TimeSpan.FromSeconds(3);

        readonly IZoneLogger _logger;
        readonly WorldChannel _channel;
        readonly CancellationTokenSource _stop = new();
        readonly ConcurrentDictionary<int, LocalLease> _leases = new();
        readonly ConcurrentDictionary<int, ulong> _playfields = new();
        // Shutdown drains this queue using Reader.Count, which the single-reader channel does not support.
        readonly Channel<ReleaseRequest> _releases = System.Threading.Channels.Channel.CreateUnbounded<ReleaseRequest>();
        readonly ConcurrentDictionary<ulong, long> _heartbeatsSent = new();
        readonly TaskCompletionSource _firstRegistration = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly object _registrationSync = new();
        IWorldZoneHandlers? _handlers;
        Modules.ZoneModuleHost? _modules;
        Task? _connectLoop;
        Task? _releaseLoop;
        Task? _fenceLoop;
        long _deadlineMs;
        long _epoch;
        int _slot = -1;
        string _session = string.Empty;
        volatile bool _connected;
        int _fenceLogoutStarted;
        int _disposed;
        long _gameClockOffsetSeconds;

        public WorldLink(IZoneLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Instance = ZoneInstanceOptions.Current;
            Url = WorldSettings.ResolveUrl(ConfigReadWrite.Instance.CurrentConfig?.World);
            _channel = new WorldChannel(Url, "zone");
        }

        public ZoneInstanceOptions Instance { get; }

        public string Url { get; }

        public WorldChannel Channel => _channel;

        /// <summary>World's epoch at the last registration.</summary>
        public long Epoch => Interlocked.Read(ref _epoch);

        /// <summary>First NPC instance id this instance may hand out; set at registration.</summary>
        public int NpcIdBase { get; private set; }

        public int NpcIdCount { get; private set; }

        public bool IsDutyHolder { get; private set; }

        public long GameClockOffsetSeconds => Interlocked.Read(ref _gameClockOffsetSeconds);

        static long Now => Environment.TickCount64;

        /// <summary>Registered and inside the lease deadline: admissions, transfers and saves are allowed.</summary>
        public bool IsServing => _connected && Now < Interlocked.Read(ref _deadlineMs);

        /// <summary>Saves are allowed until the deadline even while reconnecting.</summary>
        public bool WithinDeadline => Now < Interlocked.Read(ref _deadlineMs);

        public void Attach(IWorldZoneHandlers handlers)
            => _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));

        /// <summary>
        /// The loaded modules, for the handoff hook (module API 1.4). Attach before <see cref="Start"/> so the first
        /// registration already tells World whether this zone decides on handoffs.
        /// </summary>
        public void AttachModules(Modules.ZoneModuleHost modules)
            => _modules = modules ?? throw new ArgumentNullException(nameof(modules));

        public void Start()
        {
            if (_connectLoop != null) return;
            _connectLoop = Task.Run(() => ConnectLoopAsync(_stop.Token));
            _releaseLoop = Task.Run(() => ReleaseLoopAsync(_stop.Token));
            _fenceLoop = Task.Run(() => FenceLoopAsync(_stop.Token));
        }

        /// <summary>Set when World will never accept this instance as configured; startup stops.</summary>
        public string? FatalRegistrationError { get; private set; }

        /// <summary>Blocks startup until World accepts this instance (the game port must not open before).</summary>
        public bool WaitUntilRegistered(TimeSpan timeout) => _firstRegistration.Task.Wait(timeout);

        // ------------------------------------------------------------------------------------------ leases

        /// <summary>A client presented cookies here. On success the lease is held locally until released.</summary>
        public async Task<ClaimReply> ClaimAsync(int characterId, uint cookie1, uint cookie2)
        {
            if (!IsServing) return new ClaimReply { Accepted = false, Reason = "world_unavailable" };
            ClaimReply reply = await _channel.Leases.ClaimAsync(
                new ClaimRequest { CharacterId = characterId, Cookie1 = cookie1, Cookie2 = cookie2 },
                _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);
            if (reply.Accepted) Grant(characterId, reply.Token, reply.Account);
            return reply;
        }

        void Grant(int characterId, LeaseToken token, string account)
        {
            _leases.AddOrUpdate(characterId,
                _ => new LocalLease(token) { Account = account ?? string.Empty },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        existing.Token = token;
                        if (!string.IsNullOrEmpty(account)) existing.Account = account;
                        // A new token can mean a new lease at World (a relog creates one with no name card or level), and
                        // presence sent under the old token was refused as stale: send it all again on the next
                        // heartbeat. Otherwise World keeps level 0 and team invites to this character fail with
                        // "Team invite target level is unavailable." until something in the presence changes.
                        existing.SentPresence = null;
                    }

                    return existing;
                });
        }

        /// <summary>Gives back a lease nothing here uses yet (an admission that failed after the claim).</summary>
        public void Abandon(int characterId)
        {
            if (_leases.TryGetValue(characterId, out LocalLease? lease))
            {
                lock (lease)
                {
                    if (lease.References > 0) return;
                }

                if (_leases.TryRemove(new KeyValuePair<int, LocalLease>(characterId, lease)))
                    QueueRelease(characterId, lease.Token);
            }
        }

        /// <summary>
        /// Online ownership for a spawned player. The lease must already be held (claimed at admission); the reference
        /// releases it to World when the last holder lets go.
        /// </summary>
        public IDisposable AcquireOwnership(int characterId)
        {
            if (!_leases.TryGetValue(characterId, out LocalLease? lease))
                throw new InvalidOperationException("Character " + characterId.ToString(CultureInfo.InvariantCulture)
                    + " has no World lease on this instance.");
            lock (lease)
            {
                if (lease.Released) throw new InvalidOperationException("The World lease was released.");
                lease.References++;
            }

            return new Ownership(this, characterId, lease);
        }

        void ReleaseReference(int characterId, LocalLease lease)
        {
            lock (lease)
            {
                if (--lease.References > 0) return;
                lease.Released = true;
            }

            if (_leases.TryRemove(new KeyValuePair<int, LocalLease>(characterId, lease)))
                QueueRelease(characterId, lease.Token);
        }

        void QueueRelease(int characterId, LeaseToken token)
            => _releases.Writer.TryWrite(new ReleaseRequest { CharacterId = characterId, Token = token });

        /// <summary>
        /// True while this instance may write the character: it holds the lease and the deadline has not passed.
        /// Checked right before each database commit.
        /// </summary>
        public bool OwnsLease(int characterId) => WithinDeadline && _leases.ContainsKey(characterId);

        public LeaseToken? TokenOf(int characterId)
        {
            if (!_leases.TryGetValue(characterId, out LocalLease? lease)) return null;
            lock (lease) return lease.Token;
        }

        /// <summary>Drops a lease World says belongs to someone else; the player must already be quarantined.</summary>
        internal void Forget(int characterId) => _leases.TryRemove(characterId, out _);

        // ------------------------------------------------------------------------------------------ playfields

        public async Task<ResolveReply> ResolveAsync(int playfieldId, CancellationToken cancellation = default)
        {
            if (!IsServing) return new ResolveReply { Accepted = false, Reason = "world_unavailable" };
            ResolveReply reply = await _channel.Playfields.ResolveAsync(
                new ResolveRequest { PlayfieldId = playfieldId, Affinity = Instance.Name },
                _channel.Call(CallTimeout, cancellation)).ResponseAsync.ConfigureAwait(false);
            if (reply.Accepted && string.Equals(reply.Instance, Instance.Name, StringComparison.OrdinalIgnoreCase))
                _playfields[playfieldId] = reply.Assignment;
            return reply;
        }

        /// <summary>True when World assigned <paramref name="playfieldId"/> here (known locally, no call).</summary>
        public bool HostsPlayfield(int playfieldId) => _playfields.ContainsKey(playfieldId);

        /// <summary>An empty playfield was unloaded; World may run it elsewhere next time.</summary>
        public void PlayfieldUnloaded(int playfieldId)
        {
            if (!_playfields.TryRemove(playfieldId, out ulong assignment) || !IsServing) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    Ack ack = await _channel.Playfields.ReleaseAsync(
                        new PlayfieldReleaseRequest { PlayfieldId = playfieldId, Assignment = assignment },
                        _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);
                    if (!ack.Ok) _playfields.TryAdd(playfieldId, assignment);
                }
                catch (RpcException)
                {
                    _playfields.TryAdd(playfieldId, assignment);
                }
            });
        }

        public void PlayfieldLoaded(int playfieldId) => _playfields.TryAdd(playfieldId, 0);

        // ------------------------------------------------------------------------------------------ transfers

        public async Task<TransferReply> BeginTransferAsync(int characterId, uint cookie1, uint cookie2, TransferEnvelope envelope)
        {
            LeaseToken? token = TokenOf(characterId);
            if (token == null) return new TransferReply { Accepted = false, Reason = "no_lease" };
            if (!IsServing) return new TransferReply { Accepted = false, Reason = "world_unavailable" };
            return await _channel.Leases.BeginTransferAsync(
                new TransferRequest { CharacterId = characterId, Token = token, Cookie1 = cookie1, Cookie2 = cookie2, Envelope = envelope },
                _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);
        }

        /// <summary>World moved the lease to the destination: this instance no longer owns the character.</summary>
        public void TransferredAway(int characterId)
        {
            if (_leases.TryRemove(characterId, out LocalLease? lease))
                lock (lease) lease.Released = true;
        }

        // ------------------------------------------------------------------------------------------ directory

        public async Task<LocateReply> LocateAsync(int characterId, string? name)
        {
            if (!IsServing) return new LocateReply { Found = false };
            return await _channel.Directory.LocateAsync(
                new LocateRequest { CharacterId = characterId, Name = name ?? string.Empty },
                _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);
        }

        public async Task<ListOnlineReply> ListOnlineAsync()
            => await _channel.Directory.ListOnlineAsync(new ListOnlineRequest(), _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);

        public async Task<long> ShiftGameClockAsync(long deltaGameSeconds, bool reset)
        {
            Ack ack = await _channel.Directory.ShiftGameClockAsync(
                new ShiftGameClockRequest { DeltaGameSeconds = deltaGameSeconds, Reset = reset },
                _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);
            return long.Parse(ack.Reason, CultureInfo.InvariantCulture);
        }

        public async Task AnnounceAsync(string text)
            => await _channel.Directory.AnnounceAsync(new Announcement { Text = text }, _channel.Call(CallTimeout)).ResponseAsync.ConfigureAwait(false);

        // ------------------------------------------------------------------------------------------ connection

        async Task ConnectLoopAsync(CancellationToken stop)
        {
            TimeSpan backoff = TimeSpan.FromMilliseconds(250);
            bool loggedWaiting = false;
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await RunSessionAsync(stop).ConfigureAwait(false);
                    backoff = TimeSpan.FromMilliseconds(250);
                }
                catch (RpcException exception) when (!stop.IsCancellationRequested && exception.StatusCode == StatusCode.AlreadyExists
                    && !_firstRegistration.Task.IsCompleted)
                {
                    // Another live process already runs under this instance name; waiting will not change that.
                    FatalRegistrationError = "World refused instance " + Instance.Name + ": " + exception.Status.Detail
                        + " (another zone engine is already running under this name or port)";
                    _logger.Error(FatalRegistrationError);
                    return;
                }
                catch (Exception exception) when (!stop.IsCancellationRequested)
                {
                    if (_connected || !loggedWaiting)
                    {
                        _logger.Warn("World link to " + Url + " unavailable: " + Describe(exception));
                        loggedWaiting = true;
                    }
                }
                finally
                {
                    _connected = false;
                }

                if (stop.IsCancellationRequested) break;
                try { await Task.Delay(backoff, stop).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                backoff = TimeSpan.FromMilliseconds(Math.Min(2000, backoff.TotalMilliseconds * 2));
            }
        }

        async Task RunSessionAsync(CancellationToken stop)
        {
            using var session = CancellationTokenSource.CreateLinkedTokenSource(stop);
            // The registry call itself must not carry the old session header.
            _channel.RegistrySession = string.Empty;
            using AsyncDuplexStreamingCall<ServiceToWorld, WorldToService> call =
                _channel.Registry.Connect(_channel.Stream(session.Token));
            var outbound = System.Threading.Channels.Channel.CreateUnbounded<ServiceToWorld>(new UnboundedChannelOptions { SingleReader = true });
            Task writer = Task.Run(async () =>
            {
                await foreach (ServiceToWorld message in outbound.Reader.ReadAllAsync(session.Token).ConfigureAwait(false))
                    await call.RequestStream.WriteAsync(message, session.Token).ConfigureAwait(false);
            }, session.Token);

            long registerSentMs = Now;
            outbound.Writer.TryWrite(new ServiceToWorld { Register = BuildRegistration() });
            if (!await call.ResponseStream.MoveNext(session.Token).ConfigureAwait(false)
                || call.ResponseStream.Current.BodyCase != WorldToService.BodyOneofCase.Accepted)
                throw new InvalidOperationException("World did not accept the registration.");
            Accept(call.ResponseStream.Current.Accepted, registerSentMs);
            Volatile.Write(ref _outbound, outbound.Writer);

            Task heartbeat = HeartbeatLoopAsync(outbound.Writer, TimeSpan.FromMilliseconds(Math.Max(250, _heartbeatMs)), session.Token);
            try
            {
                while (await call.ResponseStream.MoveNext(session.Token).ConfigureAwait(false))
                    Handle(call.ResponseStream.Current, outbound.Writer);
                throw new InvalidOperationException("World closed the registry stream.");
            }
            finally
            {
                _connected = false;
                Interlocked.CompareExchange(ref _outbound, null, outbound.Writer);
                session.Cancel();
                outbound.Writer.TryComplete();
                try { await Task.WhenAll(writer, heartbeat).ConfigureAwait(false); }
                catch (Exception) { }
            }
        }

        ChannelWriter<ServiceToWorld>? _outbound;

        /// <summary>Chat for ChatEngine, in order, on the registry stream. False while World is unreachable.</summary>
        public bool PublishChat(ChatEvent chat)
            => _connected && Volatile.Read(ref _outbound)?.TryWrite(new ServiceToWorld { Chat = chat }) == true;

        int _heartbeatMs = WorldSettings.DefaultHeartbeatMilliseconds;
        int _leaseTtlMs = WorldSettings.DefaultLeaseTtlSeconds * 1000;

        Register BuildRegistration()
        {
            var register = new Register
            {
                Role = ServiceRole.RoleZone,
                Instance = Instance.Name,
                Game = new Endpoint { Address = Instance.AdvertisedAddress.ToString(), Port = (uint)Instance.Port },
                LastEpoch = Epoch,
                Build = typeof(WorldLink).Assembly.GetName().Version?.ToString() ?? string.Empty,
                PreferredSlot = _slot,
                PreviousSession = _session,
                HandoffHook = _modules?.HasHandoffHook ?? false
            };
            Dictionary<int, PlayerPresence> presence = SafePresence().ToDictionary(p => p.CharacterId);
            foreach (KeyValuePair<int, LocalLease> lease in _leases)
            {
                LeaseToken token;
                string account;
                lock (lease.Value)
                {
                    token = lease.Value.Token;
                    account = lease.Value.Account;
                }

                presence.TryGetValue(lease.Key, out PlayerPresence player);
                register.Leases.Add(new LeaseAssertion
                {
                    CharacterId = lease.Key,
                    Token = token,
                    Account = account,
                    PlayfieldId = player.PlayfieldId,
                    LinkDead = player.LinkDead,
                    Name = player.Name ?? string.Empty
                });
            }

            foreach (KeyValuePair<int, ulong> playfield in _playfields)
                register.Playfields.Add(new PlayfieldAssertion { PlayfieldId = playfield.Key, Assignment = playfield.Value });
            // The teams this instance mirrors: World takes them back after a restart, and resends them otherwise.
            try { if (_handlers != null) register.Teams.AddRange(_handlers.Teams()); }
            catch (Exception exception) { _logger.Warn("Team snapshot for World registration failed: " + exception.GetType().Name); }
            return register;
        }

        void Accept(RegisterAccepted accepted, long sentMs)
        {
            lock (_registrationSync)
            {
                long previousEpoch = Epoch;
                Interlocked.Exchange(ref _epoch, accepted.Epoch);
                _session = accepted.Session;
                _slot = accepted.Slot;
                _channel.RegistrySession = accepted.Session;
                _heartbeatMs = accepted.HeartbeatMs > 0 ? accepted.HeartbeatMs : _heartbeatMs;
                _leaseTtlMs = accepted.LeaseTtlMs > 0 ? accepted.LeaseTtlMs : _leaseTtlMs;
                NpcIdBase = accepted.NpcIdBase;
                NpcIdCount = accepted.NpcIdCount;
                Interlocked.Exchange(ref _gameClockOffsetSeconds, accepted.GameClockOffsetSeconds);
                Interlocked.Exchange(ref _deadlineMs, sentMs + _leaseTtlMs);
                Volatile.Write(ref _fenceLogoutStarted, 0);

                // A new stream: World may have missed presence sent on the old one, so everything is sent again.
                foreach (LocalLease held in _leases.Values)
                    lock (held) held.SentPresence = null;

                // What World reissued is authoritative: adopt its tokens. A lease released since the registration was
                // built is released again under the reissued token.
                foreach (LeaseAssertion lease in accepted.Leases)
                {
                    if (_leases.TryGetValue(lease.CharacterId, out LocalLease? held))
                        lock (held) held.Token = lease.Token;
                    else
                        QueueRelease(lease.CharacterId, lease.Token);
                }

                foreach (PlayfieldAssertion playfield in accepted.Playfields) _playfields[playfield.PlayfieldId] = playfield.Assignment;
                foreach (int lost in accepted.LostPlayfields)
                {
                    _playfields.TryRemove(lost, out _);
                    _logger.Error("World assigned playfield " + lost.ToString(CultureInfo.InvariantCulture)
                        + " to another instance while this one ran it");
                }

                _connected = true;
                if (accepted.Recovering || previousEpoch != accepted.Epoch)
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "World registered instance={0} slot={1} epoch={2} leases={3} playfields={4} recovering={5}",
                        Instance.Name, accepted.Slot, accepted.Epoch, accepted.Leases.Count, accepted.Playfields.Count, accepted.Recovering));

                foreach (LeaseAssertion lost in accepted.LostLeases)
                {
                    _logger.Error("World gave character " + lost.CharacterId.ToString(CultureInfo.InvariantCulture)
                        + " to another owner; dropping it here without saving");
                    _handlers?.Revoke(lost.CharacterId, "lease_lost_on_register");
                    Forget(lost.CharacterId);
                }
            }

            _firstRegistration.TrySetResult();
        }

        async Task HeartbeatLoopAsync(ChannelWriter<ServiceToWorld> outbound, TimeSpan interval, CancellationToken token)
        {
            ulong sequence = 0;
            using var timer = new PeriodicTimer(interval);
            do
            {
                sequence++;
                var heartbeat = new Heartbeat { Sequence = sequence };
                IReadOnlyList<PlayfieldLoad> loads = SafeLoad();
                heartbeat.Playfields.AddRange(loads);
                heartbeat.Players = loads.Sum(load => load.Players);
                foreach (PlayfieldLoad load in loads) _playfields.TryAdd(load.PlayfieldId, 0);
                _heartbeatsSent[sequence] = Now;
                outbound.TryWrite(new ServiceToWorld { Heartbeat = heartbeat });
                SyncPresence();
                AbandonUnusedClaims();
            }
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
        }

        void Handle(WorldToService message, ChannelWriter<ServiceToWorld> outbound)
        {
            switch (message.BodyCase)
            {
                case WorldToService.BodyOneofCase.HeartbeatAck:
                    if (_heartbeatsSent.TryRemove(message.HeartbeatAck.Sequence, out long sentMs))
                    {
                        long deadline = sentMs + _leaseTtlMs;
                        if (deadline > Interlocked.Read(ref _deadlineMs)) Interlocked.Exchange(ref _deadlineMs, deadline);
                        Volatile.Write(ref _fenceLogoutStarted, 0);
                    }

                    foreach (ulong old in _heartbeatsSent.Keys.Where(s => s < message.HeartbeatAck.Sequence).ToList())
                        _heartbeatsSent.TryRemove(old, out _);
                    break;

                case WorldToService.BodyOneofCase.LeaseRevoked:
                    LeaseToken? held = TokenOf(message.LeaseRevoked.CharacterId);
                    if (held != null && LeaseTokens.Compare(held, message.LeaseRevoked.Token) <= 0)
                    {
                        _handlers?.Revoke(message.LeaseRevoked.CharacterId, message.LeaseRevoked.Reason);
                        Forget(message.LeaseRevoked.CharacterId);
                    }

                    break;

                case WorldToService.BodyOneofCase.Request:
                    _ = AnswerAsync(message.Request, outbound);
                    break;

                case WorldToService.BodyOneofCase.ClearOnline:
                    var ids = message.ClearOnline.CharacterIds.Where(id => !_leases.ContainsKey(id)).ToList();
                    if (ids.Count > 0) _ = Task.Run(() => _handlers?.ClearOnline(ids));
                    break;

                case WorldToService.BodyOneofCase.GameClock:
                    Interlocked.Exchange(ref _gameClockOffsetSeconds, message.GameClock.OffsetGameSeconds);
                    _handlers?.GameClockChanged(message.GameClock.OffsetGameSeconds);
                    break;

                case WorldToService.BodyOneofCase.Duty:
                    IsDutyHolder = message.Duty.Holder;
                    _logger.Info("World duty holder=" + message.Duty.Holder);
                    _handlers?.DutyChanged(message.Duty.Holder);
                    break;

                case WorldToService.BodyOneofCase.Announcement:
                    _handlers?.Announce(message.Announcement.Text);
                    break;

                default:
                    WorldMessageReceived?.Invoke(message);
                    break;
            }
        }

        /// <summary>Pushes this link does not handle itself (teams, chat).</summary>
        public event Action<WorldToService>? WorldMessageReceived;

        async Task AnswerAsync(ServiceRequest request, ChannelWriter<ServiceToWorld> outbound)
        {
            var reply = new ServiceReply { RequestId = request.RequestId };
            try
            {
                IWorldZoneHandlers? handlers = _handlers;
                switch (request.BodyCase)
                {
                    case ServiceRequest.BodyOneofCase.ReleaseCharacter:
                        ReleaseOutcome outcome = handlers == null
                            ? ReleaseOutcome.ReleaseFailed
                            : await handlers.ReleaseCharacterAsync(request.ReleaseCharacter.CharacterId).ConfigureAwait(false);
                        reply.ReleaseCharacter = new ReleaseCharacterReply { Outcome = outcome };
                        break;
                    case ServiceRequest.BodyOneofCase.LocateCharacter:
                        reply.LocateCharacter = handlers?.Locate(request.LocateCharacter.CharacterId) ?? new LocateCharacterReply { Found = false };
                        break;
                    case ServiceRequest.BodyOneofCase.AdmitClient:
                        reply.AdmitClient = AdmitClient(request.AdmitClient);
                        break;
                    default:
                        return;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "World request " + request.BodyCase + " failed");
                if (request.BodyCase == ServiceRequest.BodyOneofCase.ReleaseCharacter)
                    reply.ReleaseCharacter = new ReleaseCharacterReply { Outcome = ReleaseOutcome.ReleaseFailed };
                else if (request.BodyCase == ServiceRequest.BodyOneofCase.AdmitClient)
                    reply.AdmitClient = new AdmitClientReply { Admitted = false, Reason = "hook failed" };
                else
                    reply.LocateCharacter = new LocateCharacterReply { Found = false };
            }

            outbound.TryWrite(new ServiceToWorld { Reply = reply });
        }

        /// <summary>The module handoff hook's answer for a login handoff to this zone; admit when there is no hook.</summary>
        AdmitClientReply AdmitClient(AdmitClient request)
        {
            AORebirth.Modules.Network.HandoffDecision decision = _modules?.DecideHandoff(
                    new AORebirth.Modules.Network.HandoffRequest(request.Account, request.CharacterId, request.ClientVersion))
                ?? AORebirth.Modules.Network.HandoffDecision.Admit();
            return new AdmitClientReply
            {
                Admitted = decision.Admitted,
                EventServerType = decision.EventServerType,
                Reason = decision.Reason ?? string.Empty
            };
        }

        int _presenceQueued;

        /// <summary>Sends changed presence right away (a player just arrived) instead of at the next heartbeat.</summary>
        public void SyncPresenceSoon()
        {
            if (Interlocked.Exchange(ref _presenceQueued, 1) != 0) return;
            _ = Task.Run(async () =>
            {
                // Let the spawn finish its tick so the card has the hydrated stats.
                await Task.Delay(250).ConfigureAwait(false);
                Volatile.Write(ref _presenceQueued, 0);
                if (_connected) SyncPresence();
            });
        }

        /// <summary>A claim whose login never spawned (hydration failed, client vanished) gives its lease back.</summary>
        void AbandonUnusedClaims()
        {
            long now = Now;
            foreach (KeyValuePair<int, LocalLease> lease in _leases)
            {
                bool unused;
                lock (lease.Value) unused = lease.Value.References == 0 && now - lease.Value.GrantedMs > UnusedClaimMs;
                if (unused) Abandon(lease.Key);
            }
        }

        const long UnusedClaimMs = 120_000;

        /// <summary>Presence for the directory: only what changed since the last heartbeat, as one message.</summary>
        void SyncPresence()
        {
            ChannelWriter<ServiceToWorld>? outbound = Volatile.Read(ref _outbound);
            if (outbound == null) return;
            var batch = new PresenceBatch();
            var sent = new List<LocalLease>();
            foreach (PlayerPresence player in SafePresence())
            {
                if (!_leases.TryGetValue(player.CharacterId, out LocalLease? lease)) continue;
                LeaseToken token;
                lock (lease)
                {
                    if (lease.SentPresence == player) continue;
                    lease.SentPresence = player;
                    token = lease.Token;
                }

                var update = new PresenceUpdate
                {
                    CharacterId = player.CharacterId,
                    Token = token,
                    Name = player.Name ?? string.Empty,
                    PlayfieldId = player.PlayfieldId,
                    LinkDead = player.LinkDead,
                    Level = player.Level,
                    Profession = player.Profession,
                    Card = player.Card
                };
                batch.Updates.Add(update);
                sent.Add(lease);
            }

            if (batch.Updates.Count == 0) return;
            if (!outbound.TryWrite(new ServiceToWorld { Presence = batch }))
                foreach (LocalLease lease in sent)
                    lock (lease) lease.SentPresence = null;
        }

        async Task ReleaseLoopAsync(CancellationToken stop)
        {
            await foreach (ReleaseRequest release in _releases.Reader.ReadAllAsync(stop).ConfigureAwait(false))
            {
                while (!stop.IsCancellationRequested)
                {
                    // A registration in the meantime already told World what is still held.
                    if (_leases.ContainsKey(release.CharacterId) && LeaseTokens.SameAs(TokenOf(release.CharacterId), release.Token)) break;
                    if (!_connected)
                    {
                        await Task.Delay(500, stop).ConfigureAwait(false);
                        if (!_connected) continue;
                    }

                    try
                    {
                        await _channel.Leases.ReleaseAsync(release, _channel.Call(CallTimeout, stop)).ResponseAsync.ConfigureAwait(false);
                        break;
                    }
                    catch (RpcException exception) when (exception.StatusCode == StatusCode.FailedPrecondition)
                    {
                        // Our session was superseded; the next registration reconciles.
                        break;
                    }
                    catch (RpcException)
                    {
                        await Task.Delay(500, stop).ConfigureAwait(false);
                    }
                }
            }
        }

        async Task FenceLoopAsync(CancellationToken stop)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
            {
                if (!_firstRegistration.Task.IsCompleted || _leases.IsEmpty) continue;
                long remaining = Interlocked.Read(ref _deadlineMs) - Now;
                if (remaining > (long)FenceMargin.TotalMilliseconds) continue;
                if (Interlocked.Exchange(ref _fenceLogoutStarted, 1) != 0) continue;
                _logger.Error(string.Format(CultureInfo.InvariantCulture,
                    "World unreachable; lease deadline in {0}ms. Saving and logging out {1} characters.", remaining, _leases.Count));
                try { _handlers?.LogoutAll("World service unreachable"); }
                catch (Exception exception) { _logger.Error(exception, "Fence logout failed"); }
            }
        }

        IReadOnlyList<PlayfieldLoad> SafeLoad()
        {
            try { return _handlers?.Load() ?? Array.Empty<PlayfieldLoad>(); }
            catch (Exception) { return Array.Empty<PlayfieldLoad>(); }
        }

        IReadOnlyList<PlayerPresence> SafePresence()
        {
            try { return _handlers?.Presence() ?? Array.Empty<PlayerPresence>(); }
            catch (Exception) { return Array.Empty<PlayerPresence>(); }
        }

        static string Describe(Exception exception)
            => exception is RpcException rpc ? rpc.StatusCode + " " + rpc.Status.Detail : exception.GetType().Name + " " + exception.Message;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _stop.Cancel();
            _releases.Writer.TryComplete();
            try { Task.WhenAll(new[] { _connectLoop, _releaseLoop, _fenceLoop }.Where(t => t != null)!).Wait(TimeSpan.FromSeconds(2)); }
            catch (Exception) { }
            _channel.Dispose();
            _stop.Dispose();
        }

        /// <summary>Releases queued for World before shutdown; give them a moment to go out.</summary>
        public void FlushReleases(TimeSpan timeout)
        {
            DateTime until = DateTime.UtcNow + timeout;
            while (_releases.Reader.Count > 0 && DateTime.UtcNow < until) Thread.Sleep(25);
        }

        sealed class LocalLease(LeaseToken token)
        {
            public LeaseToken Token { get; set; } = token;

            public string Account { get; set; } = string.Empty;

            public long GrantedMs { get; } = Now;

            public int References { get; set; }

            public bool Released { get; set; }

            public PlayerPresence? SentPresence { get; set; }
        }

        sealed class Ownership(WorldLink link, int characterId, LocalLease lease) : IDisposable
        {
            int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) link.ReleaseReference(characterId, lease);
            }
        }
    }
}
