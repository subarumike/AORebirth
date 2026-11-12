namespace WorldEngine.State
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;

    using AORebirth.World.Contracts;
    using AORebirth.World.Contracts.V1;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Everything World knows, behind one lock. Every operation is a short in-memory step; nothing here waits on I/O,
    /// so a single lock keeps the rules simple without becoming a bottleneck.
    /// </summary>
    public sealed class WorldState
    {
        readonly object _sync = new();
        readonly WorldOptions _options;
        readonly ILogger _log;
        readonly Func<long> _clock;
        readonly long _startedMs;

        readonly Dictionary<string, ServiceInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, ServiceInstance> _sessions = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> _slots = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<int, CharacterLease> _leases = new();
        readonly Dictionary<int, HandoffTicket> _tickets = new();
        readonly Dictionary<string, string> _logins = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<int, PlayfieldAssignment> _playfields = new();
        readonly Dictionary<int, long> _deleting = new();
        readonly HashSet<int> _pendingClearOnline = new();
        readonly Dictionary<int, long> _conflictLoggedMs = new();
        readonly System.Collections.Concurrent.ConcurrentQueue<int> _gone = new();

        ulong _generation;
        ulong _assignment;
        long _requestId;
        long _gameClockOffsetSeconds;
        bool _recoveryLogged;
        string? _dutyHolder;

        public WorldState(WorldOptions options, ILogger<WorldState> log, Func<long>? clock = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? (() => Environment.TickCount64);
            _startedMs = _clock();
            // Wall time, so a restarted World always outranks tokens from the one before it.
            Epoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public long Epoch { get; }

        public WorldOptions Options => _options;

        long Now => _clock();

        bool Recovering(long now) => now - _startedMs < (long)_options.RecoveryWindow.TotalMilliseconds;

        public bool IsRecovering
        {
            get { lock (_sync) return Recovering(Now); }
        }

        public ulong NextRequestId() => (ulong)Interlocked.Increment(ref _requestId);

        LeaseToken NewToken() => new() { Epoch = Epoch, Generation = ++_generation };

        // ------------------------------------------------------------------------------------------ registry

        public RegisterAccepted Register(ServiceInstance candidate, Register message)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(message);
            long now = Now;
            lock (_sync)
            {
                if (_instances.TryGetValue(candidate.Name, out ServiceInstance? existing) && !existing.Removed)
                {
                    bool sameProcess = message.PreviousSession.Length > 0
                        && string.Equals(message.PreviousSession, existing.Session, StringComparison.Ordinal);
                    bool alive = existing.Connected && now - existing.LastHeartbeatMs < (long)_options.LeaseTtl.TotalMilliseconds;
                    if (alive && !sameProcess)
                        throw new InvalidOperationException("instance_in_use");
                    Retire(existing);
                }

                candidate.Game = message.Game;
                candidate.Build = message.Build;
                candidate.HasHandoffHook = message.HandoffHook;
                candidate.Slot = AssignSlot(candidate.Name, message.PreferredSlot, candidate.Role);
                _instances[candidate.Name] = candidate;
                _sessions[candidate.Session] = candidate;

                bool recovering = Recovering(now);
                NpcInstanceRange npcRange = NpcInstanceRange.ForSlot(candidate.Slot);
                var accepted = new RegisterAccepted
                {
                    Epoch = Epoch,
                    Session = candidate.Session,
                    Slot = candidate.Slot,
                    HeartbeatMs = (int)_options.Heartbeat.TotalMilliseconds,
                    LeaseTtlMs = (int)_options.LeaseTtl.TotalMilliseconds,
                    Recovering = recovering,
                    NpcIdBase = npcRange.FirstInstance,
                    NpcIdCount = npcRange.Count,
                    GameClockOffsetSeconds = _gameClockOffsetSeconds
                };

                ReconcileLeases(candidate, message, recovering, accepted);
                ReconcilePlayfields(candidate, message, now, accepted);

                _log.LogInformation(
                    "Registered {Role} {Instance} slot={Slot} endpoint={Endpoint} leases={Leases} playfields={Playfields} lost={Lost} recovering={Recovering} handoffHook={HandoffHook}",
                    candidate.Role, candidate.Name, candidate.Slot, Describe(candidate.Game), accepted.Leases.Count,
                    accepted.Playfields.Count, accepted.LostLeases.Count + accepted.LostPlayfields.Count, recovering,
                    candidate.HasHandoffHook);
                return accepted;
            }
        }

        int AssignSlot(string name, int preferred, ServiceRole role)
        {
            if (role != ServiceRole.RoleZone) return -1;
            var taken = new HashSet<int>(_instances.Values.Where(i => !i.Removed && i.Role == ServiceRole.RoleZone
                && !string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)).Select(i => i.Slot));
            if (_slots.TryGetValue(name, out int remembered) && !taken.Contains(remembered)) return remembered;
            int slot = preferred >= 0 && preferred < 64 && !taken.Contains(preferred) ? preferred : -1;
            for (int candidate = 0; slot < 0; candidate++)
                if (!taken.Contains(candidate)) slot = candidate;
            _slots[name] = slot;
            return slot;
        }

        void ReconcileLeases(ServiceInstance instance, Register message, bool recovering, RegisterAccepted accepted)
        {
            var asserted = new HashSet<int>();
            foreach (LeaseAssertion assertion in message.Leases)
            {
                if (assertion.CharacterId <= 0 || !asserted.Add(assertion.CharacterId)) continue;
                _leases.TryGetValue(assertion.CharacterId, out CharacterLease? current);
                if (current != null && string.Equals(current.Owner, instance.Name, StringComparison.OrdinalIgnoreCase)
                    && current.State == LeaseState.InPlay)
                {
                    ApplyPresence(current, assertion);
                    if (current.Account.Length == 0 && !string.IsNullOrEmpty(assertion.Account)) current.Account = assertion.Account;
                    accepted.Leases.Add(ToAssertion(current));
                    continue;
                }

                // Unknown to this World: a restart forgot it. Only a re-registration inside the recovery window, or
                // one from this same epoch, may take it back; anything older has fenced itself and is stale.
                bool reclaimable = current == null && (recovering || assertion.Token?.Epoch == Epoch);
                if (reclaimable)
                {
                    var lease = new CharacterLease(assertion.CharacterId, instance.Name, NewToken(), assertion.Account ?? string.Empty);
                    ApplyPresence(lease, assertion);
                    _leases[lease.CharacterId] = lease;
                    accepted.Leases.Add(ToAssertion(lease));
                }
                else
                {
                    accepted.LostLeases.Add(assertion);
                    _log.LogWarning("Lease for character {Character} asserted by {Instance} was not reissued (owner={Owner})",
                        assertion.CharacterId, instance.Name, current?.Owner ?? "none");
                }
            }

            // Leases this name held that its process no longer has: that process is gone.
            foreach (CharacterLease lease in _leases.Values.Where(l => string.Equals(l.Owner, instance.Name,
                StringComparison.OrdinalIgnoreCase) && !asserted.Contains(l.CharacterId)).ToList())
            {
                _leases.Remove(lease.CharacterId);
                _pendingClearOnline.Add(lease.CharacterId);
                        _gone.Enqueue(lease.CharacterId);
            }
        }

        void ReconcilePlayfields(ServiceInstance instance, Register message, long now, RegisterAccepted accepted)
        {
            var asserted = new HashSet<int>();
            foreach (PlayfieldAssertion assertion in message.Playfields)
            {
                if (!asserted.Add(assertion.PlayfieldId)) continue;
                if (_playfields.TryGetValue(assertion.PlayfieldId, out PlayfieldAssignment? current)
                    && !string.Equals(current.Instance, instance.Name, StringComparison.OrdinalIgnoreCase)
                    && IsLive(current.Instance, now))
                {
                    accepted.LostPlayfields.Add(assertion.PlayfieldId);
                    continue;
                }

                if (current == null || !string.Equals(current.Instance, instance.Name, StringComparison.OrdinalIgnoreCase))
                {
                    current = new PlayfieldAssignment(assertion.PlayfieldId, instance.Name, ++_assignment, now);
                    _playfields[assertion.PlayfieldId] = current;
                }

                accepted.Playfields.Add(new PlayfieldAssertion { PlayfieldId = current.PlayfieldId, Assignment = current.Assignment });
            }

            foreach (PlayfieldAssignment stale in _playfields.Values.Where(p => string.Equals(p.Instance, instance.Name,
                StringComparison.OrdinalIgnoreCase) && !asserted.Contains(p.PlayfieldId)).ToList())
                _playfields.Remove(stale.PlayfieldId);
        }

        static void ApplyPresence(CharacterLease lease, LeaseAssertion assertion)
        {
            lease.PlayfieldId = assertion.PlayfieldId;
            lease.LinkDead = assertion.LinkDead;
            if (!string.IsNullOrEmpty(assertion.Name)) lease.Name = assertion.Name;
        }

        static LeaseAssertion ToAssertion(CharacterLease lease) => new()
        {
            CharacterId = lease.CharacterId,
            Token = lease.Token,
            PlayfieldId = lease.PlayfieldId,
            LinkDead = lease.LinkDead,
            Name = lease.Name,
            Account = lease.Account
        };

        public void Heartbeat(ServiceInstance instance, Heartbeat heartbeat)
        {
            long now = Now;
            lock (_sync)
            {
                if (instance.Removed) return;
                instance.LastHeartbeatMs = now;
                instance.Players = heartbeat.Players;
                instance.PlayfieldLoads.Clear();
                foreach (PlayfieldLoad load in heartbeat.Playfields)
                {
                    instance.PlayfieldLoads[load.PlayfieldId] = load.Players;
                    if (!_playfields.TryGetValue(load.PlayfieldId, out PlayfieldAssignment? assignment))
                    {
                        // A playfield the instance built without asking (startup, or a path not routed through World).
                        _playfields[load.PlayfieldId] = new PlayfieldAssignment(load.PlayfieldId, instance.Name, ++_assignment, now);
                    }
                    else if (!string.Equals(assignment.Instance, instance.Name, StringComparison.OrdinalIgnoreCase)
                        && (!_conflictLoggedMs.TryGetValue(load.PlayfieldId, out long logged) || now - logged > 60_000))
                    {
                        _conflictLoggedMs[load.PlayfieldId] = now;
                        _log.LogWarning("Playfield {Playfield} runs on {Instance} but is assigned to {Owner}",
                            load.PlayfieldId, instance.Name, assignment.Instance);
                    }
                }
            }
        }

        public void Disconnected(ServiceInstance instance)
        {
            lock (_sync)
            {
                instance.Connected = false;
                _log.LogWarning("{Role} {Instance} disconnected; it keeps what it owns for {Seconds}s",
                    instance.Role, instance.Name, (int)_options.InstanceExpiry.TotalSeconds);
            }
        }

        /// <summary>The live instance behind a call's session header; null for an unknown or superseded session.</summary>
        public ServiceInstance? FindSession(string? session)
        {
            if (string.IsNullOrEmpty(session)) return null;
            lock (_sync)
                return _sessions.TryGetValue(session, out ServiceInstance? instance) && !instance.Removed ? instance : null;
        }

        void Retire(ServiceInstance instance)
        {
            instance.Removed = true;
            instance.Connected = false;
            _sessions.Remove(instance.Session);
            instance.Close();
        }

        bool IsLive(string name, long now)
            => _instances.TryGetValue(name, out ServiceInstance? instance) && !instance.Removed
                && now - instance.LastHeartbeatMs < (long)_options.InstanceExpiry.TotalMilliseconds;

        bool IsRoutable(ServiceInstance instance, long now)
            => !instance.Removed && instance.Connected && instance.Role == ServiceRole.RoleZone && instance.Game != null
                && now - instance.LastHeartbeatMs < (long)_options.LeaseTtl.TotalMilliseconds;

        ServiceInstance? Instance(string name)
            => _instances.TryGetValue(name, out ServiceInstance? instance) && !instance.Removed ? instance : null;

        /// <summary>Expires silent instances, stale tickets and abandoned deletes. Called about twice a second.</summary>
        public void Sweep()
        {
            long now = Now;
            lock (_sync)
            {
                if (!_recoveryLogged && !Recovering(now))
                {
                    _recoveryLogged = true;
                    _log.LogInformation("Recovery window closed; World is authoritative (leases={Leases} instances={Instances})",
                        _leases.Count, _instances.Count);
                }

                foreach (ServiceInstance instance in _instances.Values.Where(i => !i.Removed
                    && now - i.LastHeartbeatMs >= (long)_options.InstanceExpiry.TotalMilliseconds).ToList())
                {
                    int leases = 0;
                    foreach (CharacterLease lease in _leases.Values.Where(l => string.Equals(l.Owner, instance.Name,
                        StringComparison.OrdinalIgnoreCase)).ToList())
                    {
                        _leases.Remove(lease.CharacterId);
                        _pendingClearOnline.Add(lease.CharacterId);
                        _gone.Enqueue(lease.CharacterId);
                        leases++;
                    }

                    foreach (PlayfieldAssignment assignment in _playfields.Values.Where(p => string.Equals(p.Instance,
                        instance.Name, StringComparison.OrdinalIgnoreCase)).ToList())
                        _playfields.Remove(assignment.PlayfieldId);
                    foreach (HandoffTicket ticket in _tickets.Values.Where(t => string.Equals(t.Target, instance.Name,
                        StringComparison.OrdinalIgnoreCase)).ToList())
                        _tickets.Remove(ticket.CharacterId);
                    _instances.Remove(instance.Name);
                    Retire(instance);
                    _log.LogWarning("{Role} {Instance} expired; released {Leases} leases", instance.Role, instance.Name, leases);
                    if (string.Equals(_dutyHolder, instance.Name, StringComparison.OrdinalIgnoreCase)) _dutyHolder = null;
                }

                foreach (HandoffTicket ticket in _tickets.Values.Where(t => now >= t.ExpiresMs).ToList())
                {
                    _tickets.Remove(ticket.CharacterId);
                    if (ticket.Kind == TicketKind.Transfer && _leases.TryGetValue(ticket.CharacterId, out CharacterLease? lease)
                        && lease.State == LeaseState.Transferring)
                    {
                        // The client never reached the destination. The source saved before letting go, so the
                        // character is simply offline at its last save.
                        _leases.Remove(ticket.CharacterId);
                        _pendingClearOnline.Add(ticket.CharacterId);
                        _gone.Enqueue(ticket.CharacterId);
                        _log.LogWarning("Transfer of character {Character} to {Target} was never claimed; lease released",
                            ticket.CharacterId, ticket.Target);
                    }
                }

                foreach (KeyValuePair<int, long> delete in _deleting.Where(d => now >= d.Value).ToList())
                    _deleting.Remove(delete.Key);
                foreach (KeyValuePair<int, long> logged in _conflictLoggedMs.Where(c => now - c.Value > 300_000).ToList())
                    _conflictLoggedMs.Remove(logged.Key);

                AssignDuty();
                FlushClearOnline();
            }
        }

        /// <summary>Clears Online for orphaned characters through any live zone (World has no database).</summary>
        void FlushClearOnline()
        {
            if (_pendingClearOnline.Count == 0) return;
            ServiceInstance? zone = _instances.Values.Where(i => IsRoutable(i, Now)).OrderBy(i => i.Slot).FirstOrDefault();
            if (zone == null) return;
            var message = new ClearOnline();
            // Never clear a character someone owns again by now.
            message.CharacterIds.AddRange(_pendingClearOnline.Where(id => !_leases.ContainsKey(id)));
            _pendingClearOnline.Clear();
            if (message.CharacterIds.Count > 0) zone.Send(new WorldToService { ClearOnline = message });
        }

        /// <summary>The lowest-slot live zone runs the database-wide sweeps.</summary>
        /// <summary>
        /// Work a registration causes once its acceptance is on the stream: the acceptance must be the first message
        /// the instance gets.
        /// </summary>
        public void AfterRegister()
        {
            lock (_sync)
            {
                AssignDuty();
                FlushClearOnline();
            }
        }

        void AssignDuty()
        {
            long now = Now;
            if (_dutyHolder != null && Instance(_dutyHolder) is ServiceInstance holder && IsRoutable(holder, now)) return;
            ServiceInstance? next = _instances.Values.Where(i => IsRoutable(i, now)).OrderBy(i => i.Slot).FirstOrDefault();
            if (next == null || string.Equals(next.Name, _dutyHolder, StringComparison.OrdinalIgnoreCase)) return;
            if (_dutyHolder != null && Instance(_dutyHolder) is ServiceInstance previous)
                previous.Send(new WorldToService { Duty = new DutyAssignment { Holder = false } });
            _dutyHolder = next.Name;
            next.Send(new WorldToService { Duty = new DutyAssignment { Holder = true } });
            _log.LogInformation("Duty holder is {Instance}", next.Name);
        }

        // ------------------------------------------------------------------------------------------ sessions

        public string BeginLogin(string account)
        {
            ValidateAccount(account);
            lock (_sync)
            {
                string generation = Guid.NewGuid().ToString("N");
                _logins[account] = generation;
                return generation;
            }
        }

        public SelectCharacterReply SelectCharacter(SelectCharacterRequest request) => SelectCharacter(request, out _);

        /// <summary>
        /// Issues the login ticket and names the instance it routes to (<paramref name="target"/>, null when refused),
        /// so the caller can ask that zone's handoff hook and <see cref="WithdrawHandoff"/> on refusal.
        /// </summary>
        public SelectCharacterReply SelectCharacter(SelectCharacterRequest request, out ServiceInstance? target)
        {
            target = null;
            ValidateAccount(request.Account);
            if (request.CharacterId <= 0) return SelectRefused("invalid_character");
            long now = Now;
            lock (_sync)
            {
                if (Recovering(now)) return SelectRefused("world_recovering");
                if (!_logins.TryGetValue(request.Account, out string? generation)
                    || !string.Equals(generation, request.Generation, StringComparison.Ordinal))
                    return SelectRefused("stale_login");
                if (_deleting.ContainsKey(request.CharacterId)) return SelectRefused("character_busy");

                ServiceInstance? routed;
                if (_leases.TryGetValue(request.CharacterId, out CharacterLease? lease))
                {
                    // Already owned (in play elsewhere or link-dead): the client goes back to that instance, which
                    // hands it the live character.
                    if (lease.State != LeaseState.InPlay) return SelectRefused("character_busy");
                    routed = Instance(lease.Owner);
                    if (routed == null || routed.Role != ServiceRole.RoleZone) return SelectRefused("character_busy");
                    if (!IsRoutable(routed, now)) return SelectRefused("zone_unavailable");
                }
                else
                {
                    routed = ResolveCore(request.StoredPlayfield, null, now);
                    if (routed == null) return SelectRefused("no_zone_available");
                }

                (uint cookie1, uint cookie2) = NewCookies();
                _tickets[request.CharacterId] = new HandoffTicket
                {
                    CharacterId = request.CharacterId,
                    CookieHash = Hash(cookie1, cookie2),
                    Account = request.Account,
                    Generation = request.Generation,
                    Target = routed.Name,
                    Kind = TicketKind.Login,
                    ExpiresMs = now + (long)_options.HandoffLifetime.TotalMilliseconds
                };
                target = routed;
                return new SelectCharacterReply
                {
                    Accepted = true,
                    Reason = "issued",
                    Cookie1 = cookie1,
                    Cookie2 = cookie2,
                    Zone = routed.Game!.Clone()
                };
            }
        }

        /// <summary>
        /// Takes back a login ticket the target zone's handoff hook refused, only while it is still the one just issued
        /// (same cookies), so the refused client's cookies are dead at once rather than left to expire.
        /// </summary>
        public void WithdrawHandoff(int characterId, uint cookie1, uint cookie2)
        {
            lock (_sync)
            {
                if (_tickets.TryGetValue(characterId, out HandoffTicket? ticket)
                    && ticket.Kind == TicketKind.Login
                    && CryptographicOperations.FixedTimeEquals(ticket.CookieHash, Hash(cookie1, cookie2)))
                    _tickets.Remove(characterId);
            }
        }

        static SelectCharacterReply SelectRefused(string reason) => new() { Accepted = false, Reason = reason };

        public enum DeleteStep
        {
            Released,
            AskOwner,
            InPlay,
            Busy
        }

        /// <summary>
        /// First half of a delete. Released: nobody owns it and selects are now blocked. AskOwner: a zone holds it and
        /// must save and despawn it first (<paramref name="owner"/>), or refuse because a client is in play.
        /// </summary>
        public DeleteStep BeginDelete(int characterId, out ServiceInstance? owner)
        {
            owner = null;
            long now = Now;
            lock (_sync)
            {
                if (Recovering(now) || _deleting.ContainsKey(characterId)) return DeleteStep.Busy;
                // An unclaimed ticket must not let the character in while it is deleted.
                _tickets.Remove(characterId);
                if (!_leases.TryGetValue(characterId, out CharacterLease? lease))
                {
                    _deleting[characterId] = now + 60_000;
                    return DeleteStep.Released;
                }

                if (lease.State != LeaseState.InPlay) return DeleteStep.Busy;
                owner = Instance(lease.Owner);
                // The owner knows whether a client is attached; it answers in play or saves and lets go.
                if (owner == null || owner.Role != ServiceRole.RoleZone) return DeleteStep.Busy;
                return DeleteStep.AskOwner;
            }
        }

        /// <summary>The owner answered a release request. Done or not-here frees the lease and blocks selects.</summary>
        public DeleteStep FinishDeleteRelease(int characterId, ServiceInstance owner, ReleaseOutcome? outcome)
        {
            long now = Now;
            lock (_sync)
            {
                if (outcome == ReleaseOutcome.ReleaseInPlay) return DeleteStep.InPlay;
                if (outcome is not (ReleaseOutcome.ReleaseDone or ReleaseOutcome.ReleaseNotHere)) return DeleteStep.Busy;
                if (_leases.TryGetValue(characterId, out CharacterLease? lease))
                {
                    if (!string.Equals(lease.Owner, owner.Name, StringComparison.OrdinalIgnoreCase)) return DeleteStep.Busy;
                    _leases.Remove(characterId);
                    _gone.Enqueue(characterId);
                }

                _tickets.Remove(characterId);
                _deleting[characterId] = now + 60_000;
                return DeleteStep.Released;
            }
        }

        public void CompleteDelete(int characterId)
        {
            lock (_sync) _deleting.Remove(characterId);
        }

        // ------------------------------------------------------------------------------------------ leases

        public ClaimReply Claim(ServiceInstance instance, ClaimRequest request)
        {
            if (request.CharacterId <= 0 || (request.Cookie1 == 0 && request.Cookie2 == 0)) return ClaimRefused("missing_handoff");
            long now = Now;
            lock (_sync)
            {
                if (instance.Removed) return ClaimRefused("stale_session");
                if (!_tickets.TryGetValue(request.CharacterId, out HandoffTicket? ticket)
                    || !CryptographicOperations.FixedTimeEquals(ticket.CookieHash, Hash(request.Cookie1, request.Cookie2)))
                    return ClaimRefused("unknown_handoff");
                if (!string.Equals(ticket.Target, instance.Name, StringComparison.OrdinalIgnoreCase))
                    return ClaimRefused("redirect_target_mismatch");
                if (now >= ticket.ExpiresMs) return ClaimRefused("expired_handoff");
                if (ticket.Kind == TicketKind.Login && (!_logins.TryGetValue(ticket.Account, out string? generation)
                    || !string.Equals(generation, ticket.Generation, StringComparison.Ordinal)))
                    return ClaimRefused("stale_login");
                if (_deleting.ContainsKey(request.CharacterId)) return ClaimRefused("character_busy");

                _leases.TryGetValue(request.CharacterId, out CharacterLease? lease);
                ClaimKind kind;
                if (ticket.Kind == TicketKind.Transfer)
                {
                    if (lease == null || lease.State != LeaseState.Transferring
                        || !string.Equals(lease.TransferTarget, instance.Name, StringComparison.OrdinalIgnoreCase))
                        return ClaimRefused("transfer_abandoned");
                    lease.Owner = instance.Name;
                    lease.State = LeaseState.InPlay;
                    lease.TransferTarget = null;
                    lease.Token = NewToken();
                    kind = ClaimKind.ClaimTransfer;
                }
                else if (lease != null)
                {
                    if (!string.Equals(lease.Owner, instance.Name, StringComparison.OrdinalIgnoreCase)
                        || lease.State != LeaseState.InPlay)
                        return ClaimRefused("character_busy");
                    lease.Token = NewToken();
                    lease.Account = ticket.Account;
                    kind = ClaimKind.ClaimReconnect;
                }
                else
                {
                    lease = new CharacterLease(request.CharacterId, instance.Name, NewToken(), ticket.Account);
                    _leases[request.CharacterId] = lease;
                    kind = ClaimKind.ClaimLogin;
                }

                _tickets.Remove(request.CharacterId);
                var reply = new ClaimReply
                {
                    Accepted = true,
                    Reason = kind == ClaimKind.ClaimTransfer ? "claimed_transfer" : "claimed_initial",
                    Account = ticket.Account,
                    Token = lease.Token,
                    Kind = kind
                };
                if (ticket.Envelope != null) reply.Envelope = ticket.Envelope;
                return reply;
            }
        }

        static ClaimReply ClaimRefused(string reason) => new() { Accepted = false, Reason = reason };

        public Ack Release(ServiceInstance instance, ReleaseRequest request)
        {
            lock (_sync)
            {
                if (!_leases.TryGetValue(request.CharacterId, out CharacterLease? lease))
                    return new Ack { Ok = true, Reason = "not_leased" };
                if (!string.Equals(lease.Owner, instance.Name, StringComparison.OrdinalIgnoreCase)
                    || !LeaseTokens.SameAs(lease.Token, request.Token)
                    || lease.State != LeaseState.InPlay)
                    return new Ack { Ok = false, Reason = "stale_lease" };
                _leases.Remove(request.CharacterId);
                _gone.Enqueue(request.CharacterId);
                return new Ack { Ok = true, Reason = "released" };
            }
        }

        public ServiceLeaseReply AcquireService(ServiceInstance instance, ServiceLeaseRequest request)
        {
            if (request.CharacterId <= 0) return new ServiceLeaseReply { Accepted = false, Reason = "invalid_character" };
            lock (_sync)
            {
                if (instance.Removed) return new ServiceLeaseReply { Accepted = false, Reason = "stale_session" };
                if (Recovering(Now)) return new ServiceLeaseReply { Accepted = false, Reason = "world_recovering" };
                if (_deleting.ContainsKey(request.CharacterId)) return new ServiceLeaseReply { Accepted = false, Reason = "character_busy" };
                if (_leases.TryGetValue(request.CharacterId, out CharacterLease? lease))
                {
                    if (!string.Equals(lease.Owner, instance.Name, StringComparison.OrdinalIgnoreCase))
                        return new ServiceLeaseReply { Accepted = false, Reason = "character_busy" };
                    return new ServiceLeaseReply { Accepted = true, Reason = "already_owned", Token = lease.Token };
                }

                lease = new CharacterLease(request.CharacterId, instance.Name, NewToken(), string.Empty);
                _leases[request.CharacterId] = lease;
                return new ServiceLeaseReply { Accepted = true, Reason = "granted", Token = lease.Token };
            }
        }

        public LeaseQueryReply Query(LeaseQuery query)
        {
            var reply = new LeaseQueryReply();
            lock (_sync)
            {
                foreach (int id in query.CharacterIds)
                    if (_leases.ContainsKey(id) || _deleting.ContainsKey(id)) reply.Owned.Add(id);
            }

            return reply;
        }

        public TransferReply BeginTransfer(ServiceInstance instance, TransferRequest request)
        {
            if (request.Envelope == null) return TransferRefused("missing_envelope");
            if (request.Cookie1 == 0 && request.Cookie2 == 0) return TransferRefused("missing_handoff");
            long now = Now;
            lock (_sync)
            {
                if (!_leases.TryGetValue(request.CharacterId, out CharacterLease? lease)
                    || !string.Equals(lease.Owner, instance.Name, StringComparison.OrdinalIgnoreCase)
                    || !LeaseTokens.SameAs(lease.Token, request.Token) || lease.State != LeaseState.InPlay)
                    return TransferRefused("stale_lease");
                ServiceInstance? target = ResolveCore(request.Envelope.DestinationPlayfield, null, now);
                if (target == null) return TransferRefused("no_zone_available");
                if (ReferenceEquals(target, instance)) return TransferRefused("same_instance");

                lease.State = LeaseState.Transferring;
                lease.TransferTarget = target.Name;
                // The source is fenced from here on: its token no longer matches.
                lease.Token = NewToken();
                lease.LinkDead = false;
                lease.PlayfieldId = request.Envelope.DestinationPlayfield;
                _tickets[request.CharacterId] = new HandoffTicket
                {
                    CharacterId = request.CharacterId,
                    CookieHash = Hash(request.Cookie1, request.Cookie2),
                    Account = lease.Account,
                    Generation = string.Empty,
                    Target = target.Name,
                    Kind = TicketKind.Transfer,
                    ExpiresMs = now + (long)_options.HandoffLifetime.TotalMilliseconds,
                    Envelope = request.Envelope
                };
                _log.LogInformation("Transfer character {Character} {Source} -> {Target} playfield={Playfield}",
                    request.CharacterId, instance.Name, target.Name, request.Envelope.DestinationPlayfield);
                return new TransferReply { Accepted = true, Reason = "transfer_started", Target = target.Game!.Clone(), TargetInstance = target.Name };
            }
        }

        static TransferReply TransferRefused(string reason) => new() { Accepted = false, Reason = reason };

        public Ack UpdatePresence(ServiceInstance instance, PresenceUpdate update)
        {
            lock (_sync)
            {
                if (!_leases.TryGetValue(update.CharacterId, out CharacterLease? lease)
                    || !string.Equals(lease.Owner, instance.Name, StringComparison.OrdinalIgnoreCase)
                    || !LeaseTokens.SameAs(lease.Token, update.Token))
                    return new Ack { Ok = false, Reason = "stale_lease" };
                if (!string.IsNullOrEmpty(update.Name)) lease.Name = update.Name;
                lease.PlayfieldId = update.PlayfieldId;
                lease.LinkDead = update.LinkDead;
                if (update.Level > 0) lease.Level = update.Level;
                if (update.Profession > 0) lease.Profession = update.Profession;
                if (update.Card != null) lease.Card = update.Card;
                return new Ack { Ok = true };
            }
        }

        // ------------------------------------------------------------------------------------------ playfields

        public ResolveReply Resolve(ResolveRequest request)
        {
            long now = Now;
            lock (_sync)
            {
                if (Recovering(now)) return new ResolveReply { Accepted = false, Reason = "world_recovering" };
                ServiceInstance? target = ResolveCore(request.PlayfieldId, request.Affinity, now);
                if (target == null) return new ResolveReply { Accepted = false, Reason = "no_zone_available" };
                return new ResolveReply
                {
                    Accepted = true,
                    Instance = target.Name,
                    Endpoint = target.Game!.Clone(),
                    Assignment = _playfields[request.PlayfieldId].Assignment
                };
            }
        }

        /// <summary>The instance running <paramref name="playfieldId"/>, assigning the least-loaded live zone if none does.</summary>
        ServiceInstance? ResolveCore(int playfieldId, string? affinity, long now)
        {
            if (_playfields.TryGetValue(playfieldId, out PlayfieldAssignment? assignment))
            {
                ServiceInstance? owner = Instance(assignment.Instance);
                if (owner != null && IsRoutable(owner, now))
                {
                    assignment.LastRoutedMs = now;
                    return owner;
                }

                // An owner that is only briefly unreachable still runs the playfield; never start a second copy.
                if (IsLive(assignment.Instance, now)) return null;
                _playfields.Remove(playfieldId);
            }

            List<ServiceInstance> candidates = _instances.Values.Where(i => IsRoutable(i, now)).ToList();
            if (candidates.Count == 0) return null;
            var playfieldCounts = _playfields.Values.GroupBy(p => p.Instance, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            double Score(ServiceInstance instance)
                => instance.Players + 2 * (playfieldCounts.TryGetValue(instance.Name, out int count) ? count : 0);

            ServiceInstance best = candidates.OrderBy(Score).ThenBy(i => i.Slot).First();
            if (!string.IsNullOrEmpty(affinity) && candidates.FirstOrDefault(i => string.Equals(i.Name, affinity,
                StringComparison.OrdinalIgnoreCase)) is ServiceInstance preferred && Score(preferred) <= Score(best) * 1.25 + 2)
                best = preferred;

            _playfields[playfieldId] = new PlayfieldAssignment(playfieldId, best.Name, ++_assignment, now);
            _log.LogInformation("Assigned playfield {Playfield} to {Instance}", playfieldId, best.Name);
            return best;
        }

        public Ack ReleasePlayfield(ServiceInstance instance, PlayfieldReleaseRequest request)
        {
            long now = Now;
            lock (_sync)
            {
                if (!_playfields.TryGetValue(request.PlayfieldId, out PlayfieldAssignment? assignment))
                    return new Ack { Ok = true, Reason = "not_assigned" };
                if (!string.Equals(assignment.Instance, instance.Name, StringComparison.OrdinalIgnoreCase)
                    || (request.Assignment != 0 && request.Assignment != assignment.Assignment))
                    return new Ack { Ok = false, Reason = "stale_assignment" };
                if (now - assignment.LastRoutedMs < (long)_options.HandoffLifetime.TotalMilliseconds)
                    return new Ack { Ok = false, Reason = "busy" };
                _playfields.Remove(request.PlayfieldId);
                return new Ack { Ok = true, Reason = "released" };
            }
        }

        // ------------------------------------------------------------------------------------------ directory

        public LocateReply Locate(LocateRequest request)
        {
            lock (_sync)
            {
                CharacterLease? lease = null;
                if (request.CharacterId > 0) _leases.TryGetValue(request.CharacterId, out lease);
                else if (!string.IsNullOrWhiteSpace(request.Name))
                    lease = _leases.Values.FirstOrDefault(l => string.Equals(l.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (lease == null || lease.State != LeaseState.InPlay) return new LocateReply { Found = false };
                return new LocateReply
                {
                    Found = true,
                    CharacterId = lease.CharacterId,
                    Instance = lease.Owner,
                    PlayfieldId = lease.PlayfieldId
                };
            }
        }

        public ListOnlineReply ListOnline()
        {
            var reply = new ListOnlineReply();
            lock (_sync)
            {
                foreach (CharacterLease lease in _leases.Values.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (Instance(lease.Owner)?.Role != ServiceRole.RoleZone) continue;
                    reply.Characters.Add(new OnlineCharacter
                    {
                        CharacterId = lease.CharacterId,
                        Name = lease.Name,
                        Instance = lease.Owner,
                        PlayfieldId = lease.PlayfieldId,
                        LinkDead = lease.LinkDead,
                        Level = lease.Level,
                        Profession = lease.Profession
                    });
                }
            }

            return reply;
        }

        /// <summary>Characters whose lease ended (logout, delete, an instance that died) since the last call.</summary>
        public IReadOnlyList<int> DrainGone()
        {
            var gone = new List<int>();
            while (_gone.TryDequeue(out int id)) gone.Add(id);
            return gone;
        }

        /// <summary>A character as teams see it: where it is, whether it can act, and its card.</summary>
        public readonly record struct MemberView(int CharacterId, ServiceInstance Owner, LeaseToken Token, int PlayfieldId,
            bool LinkDead, PlayerCard Card);

        public MemberView? View(int characterId)
        {
            lock (_sync)
            {
                if (!_leases.TryGetValue(characterId, out CharacterLease? lease) || lease.State != LeaseState.InPlay)
                    return null;
                ServiceInstance? owner = Instance(lease.Owner);
                if (owner == null || owner.Role != ServiceRole.RoleZone) return null;
                return new MemberView(characterId, owner, lease.Token, lease.PlayfieldId, lease.LinkDead, CardOf(lease));
            }
        }

        /// <summary>The one character online under this name, or null when none or several match.</summary>
        public int FindByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            lock (_sync)
            {
                var matches = _leases.Values.Where(l => l.State == LeaseState.InPlay
                    && string.Equals(l.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
                return matches.Count == 1 ? matches[0].CharacterId : 0;
            }
        }

        static PlayerCard CardOf(CharacterLease lease)
        {
            PlayerCard card = lease.Card?.Clone() ?? new PlayerCard();
            card.CharacterId = lease.CharacterId;
            if (string.IsNullOrEmpty(card.Name)) card.Name = lease.Name;
            if (card.Level <= 0) card.Level = lease.Level;
            if (card.Profession <= 0) card.Profession = lease.Profession;
            return card;
        }

        public ServiceInstance? OwnerOf(int characterId)
        {
            lock (_sync)
                return _leases.TryGetValue(characterId, out CharacterLease? lease) && lease.State == LeaseState.InPlay
                    ? Instance(lease.Owner)
                    : null;
        }

        /// <summary>
        /// Chat traffic. From a zone: to every ChatEngine. From ChatEngine: a command for a character goes to the zone
        /// instance that owns it.
        /// </summary>
        public bool PublishChat(ServiceInstance sender, ChatEvent chat)
        {
            var message = new WorldToService { Chat = chat };
            lock (_sync)
            {
                if (sender.Role == ServiceRole.RoleZone)
                {
                    bool sent = false;
                    foreach (ServiceInstance instance in _instances.Values)
                        if (!instance.Removed && instance.Role == ServiceRole.RoleChat) sent |= instance.Send(message);
                    return sent;
                }

                if (chat.BodyCase != ChatEvent.BodyOneofCase.ToCharacter) return false;
                ServiceInstance? owner = _leases.TryGetValue(chat.ToCharacter.CharacterId, out CharacterLease? lease)
                    && lease.State == LeaseState.InPlay ? Instance(lease.Owner) : null;
                return owner != null && owner.Role == ServiceRole.RoleZone && owner.Send(message);
            }
        }

        /// <summary>Delivers a command to its target instance(s). False when nobody owns the target.</summary>
        public bool Route(ServiceInstance sender, InstanceCommand command)
        {
            command.Origin = sender.Name;
            var message = new WorldToService { Command = command };
            lock (_sync)
            {
                if (command.Broadcast)
                {
                    foreach (ServiceInstance instance in _instances.Values)
                        if (!instance.Removed && instance.Role == ServiceRole.RoleZone
                            && !string.Equals(instance.Name, sender.Name, StringComparison.OrdinalIgnoreCase))
                            instance.Send(message);
                    return true;
                }

                ServiceInstance? target = null;
                if (command.CharacterId > 0 && _leases.TryGetValue(command.CharacterId, out CharacterLease? lease)
                    && lease.State == LeaseState.InPlay)
                    target = Instance(lease.Owner);
                else if (command.PlayfieldId > 0 && _playfields.TryGetValue(command.PlayfieldId, out PlayfieldAssignment? assignment))
                    target = Instance(assignment.Instance);
                return target != null && target.Role == ServiceRole.RoleZone && target.Send(message);
            }
        }

        public long ShiftGameClock(long deltaGameSeconds, bool reset)
        {
            lock (_sync)
            {
                _gameClockOffsetSeconds = reset ? 0 : checked(_gameClockOffsetSeconds + deltaGameSeconds);
                Broadcast(new WorldToService { GameClock = new GameClockChanged { OffsetGameSeconds = _gameClockOffsetSeconds } }, ServiceRole.RoleZone);
                return _gameClockOffsetSeconds;
            }
        }

        public void Broadcast(WorldToService message, ServiceRole? role = null)
        {
            lock (_sync)
            {
                foreach (ServiceInstance instance in _instances.Values)
                    if (!instance.Removed && (role == null || instance.Role == role)) instance.Send(message);
            }
        }

        public IReadOnlyList<ServiceInstance> LiveInstances(ServiceRole role)
        {
            lock (_sync)
                return _instances.Values.Where(i => !i.Removed && i.Connected && i.Role == role).ToList();
        }

        // ------------------------------------------------------------------------------------------ status

        public object Snapshot()
        {
            long now = Now;
            lock (_sync)
            {
                return new
                {
                    epoch = Epoch,
                    recovering = Recovering(now),
                    uptimeSeconds = (now - _startedMs) / 1000,
                    dutyHolder = _dutyHolder,
                    gameClockOffsetSeconds = _gameClockOffsetSeconds,
                    instances = _instances.Values.OrderBy(i => i.Slot).Select(i => new
                    {
                        name = i.Name,
                        role = i.Role.ToString(),
                        slot = i.Slot,
                        endpoint = Describe(i.Game),
                        connected = i.Connected,
                        players = i.Players,
                        playfields = i.PlayfieldLoads.Count,
                        heartbeatAgeMs = now - i.LastHeartbeatMs,
                        build = i.Build
                    }).ToList(),
                    leaseCount = _leases.Count,
                    leases = _leases.Values.OrderBy(l => l.CharacterId).Take(500).Select(l => new
                    {
                        character = l.CharacterId,
                        name = l.Name,
                        owner = l.Owner,
                        state = l.State.ToString(),
                        target = l.TransferTarget,
                        token = LeaseTokens.Describe(l.Token),
                        playfield = l.PlayfieldId,
                        linkDead = l.LinkDead
                    }).ToList(),
                    tickets = _tickets.Values.Select(t => new
                    {
                        character = t.CharacterId,
                        target = t.Target,
                        kind = t.Kind.ToString(),
                        expiresInMs = t.ExpiresMs - now
                    }).ToList(),
                    deleting = _deleting.Keys.ToList(),
                    playfields = _playfields.Values.OrderBy(p => p.PlayfieldId).Select(p => new
                    {
                        playfield = p.PlayfieldId,
                        instance = p.Instance,
                        assignment = p.Assignment,
                        routedAgoMs = now - p.LastRoutedMs
                    }).ToList()
                };
            }
        }

        // ------------------------------------------------------------------------------------------ helpers

        static (uint, uint) NewCookies()
        {
            Span<byte> bytes = stackalloc byte[8];
            do RandomNumberGenerator.Fill(bytes);
            while (BitConverter.ToUInt64(bytes) == 0);
            return (BitConverter.ToUInt32(bytes[..4]), BitConverter.ToUInt32(bytes[4..]));
        }

        static byte[] Hash(uint first, uint second)
        {
            Span<byte> bytes = stackalloc byte[8];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = (byte)(first >> (24 - i * 8));
                bytes[i + 4] = (byte)(second >> (24 - i * 8));
            }

            return SHA256.HashData(bytes);
        }

        static void ValidateAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account) || account.Length > 32)
                throw new ArgumentException("Authenticated account required.");
        }

        static string Describe(Endpoint? endpoint)
            => endpoint == null || string.IsNullOrEmpty(endpoint.Address)
                ? "-"
                : endpoint.Address + ":" + endpoint.Port.ToString(CultureInfo.InvariantCulture);
    }
}
