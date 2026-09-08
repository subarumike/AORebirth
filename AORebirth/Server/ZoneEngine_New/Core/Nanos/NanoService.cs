namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Owner-tick casting and active-nano lifecycle. All state changes are planned before a scoped
    /// DAO transaction; no packet handler SQL, sleeping casts, transport timers or legacy assembly.
    /// Each state belongs to one exact Player. Same-actor zoning keeps active state but cancels casts.
    /// </summary>
    public sealed class NanoService
    {
        // Existing ActiveNanoRuntimeService.ClampDurationCentiseconds timer boundary.
        private const int MaximumDurationCentiseconds = 36000000;
        private sealed record Active(ActiveNanoRecord Record, Dictionary<CharacterStat, int> Modifiers,
            INanoSpecialization? Specialty, int CasterId);
        private sealed record Pending(NanoDefinition Nano, Player Target, IZoneSession Session,
            IZoneSession TargetSession, ZoneEngine_New.Core.Playfield.Playfield? Playfield, Identity WireTarget, long CompletesAt);
        private sealed class State(Player player)
        {
            public Player Player { get; } = player;
            public readonly Dictionary<int, Active> Active = new();
            public readonly Dictionary<CharacterStat, int> Derived = new();
            public Pending? Pending;
            public long ReadyAt;
            public int NextInstance;
            public Action<Character, TimedActionInterrupt>? Interrupt;
        }

        private readonly ConcurrentDictionary<int, State> _states = new();
        private readonly INanoCatalog _catalog;
        private readonly IActiveNanoRepository _repository;
        private readonly INanoSpecialization[] _specialties;
        private readonly IActiveNanoProjection[] _projections;
        private readonly HashSet<CharacterStat> _projectionKeys;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<long> _milliseconds;
        private readonly Func<int, int, int> _next;

        public NanoService(INanoCatalog catalog, IActiveNanoRepository repository,
            IEnumerable<INanoSpecialization>? specialties = null, Func<DateTime>? utcNow = null,
            Func<long>? monotonicMilliseconds = null, Func<int, int, int>? next = null)
        {
            _catalog = catalog; _repository = repository;
            _specialties = specialties?.ToArray() ?? [];
            _projections = _specialties.OfType<IActiveNanoProjection>().ToArray();
            _projectionKeys = _projections.SelectMany(p => p.Project([]).Keys).ToHashSet();
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _milliseconds = monotonicMilliseconds ?? (() => Environment.TickCount64);
            _next = next ?? Random.Shared.Next;
        }

        /// <summary>Call once on the accepted owner tick before the initial self full update.</summary>
        public bool AttachPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (_states.TryGetValue(player.Identity.Instance, out State? existing))
                return ReferenceEquals(existing.Player, player);
            if (player.Identity.Instance <= 0 || player.IsPersistenceQuarantined) return false;
            var state = new State(player);
            lock (player.PersistenceGate)
            {
                IReadOnlyList<ActiveNanoRecord> rows;
                try { rows = _repository.Load(player.Identity.Instance); }
                catch (Exception exception) { player.Logger.Error(exception, "Active nano hydration failed."); return false; }
                long now = _utcNow().Ticks;
                bool normalize = false;
                var strains = new HashSet<int>();
                var instances = new HashSet<int>();
                state.NextInstance = rows.Select(r => r.NanoInstance).DefaultIfEmpty(0).Max();
                foreach (ActiveNanoRecord raw in rows)
                {
                    if (!_catalog.TryGet(raw.NanoId, out NanoDefinition nano) || raw.Strain != nano.Strain
                        || !nano.TryCalculateAttackTime(0, 0, out _)
                        || raw.DurationCentiseconds < 0 || raw.ExpiresAtUtcTicks < 0
                        || raw.ExpiresAtUtcTicks > DateTime.MaxValue.Ticks || raw.NanoInstance < 0
                        || !strains.Add(raw.Strain) || (raw.NanoInstance > 0 && !instances.Add(raw.NanoInstance))) return false;
                    bool durationOnly = raw.ExpiresAtUtcTicks == 0 || raw.ExpiresAtUtcTicks == DateTime.MaxValue.Ticks;
                    long remaining = durationOnly ? raw.DurationCentiseconds
                        : Math.Max(0, (raw.ExpiresAtUtcTicks - now + TimeSpan.TicksPerMillisecond * 10 - 1)
                            / (TimeSpan.TicksPerMillisecond * 10));
                    if (raw.DurationCentiseconds > 0) remaining = Math.Min(remaining, raw.DurationCentiseconds);
                    remaining = Math.Min(remaining, MaximumDurationCentiseconds);
                    if (remaining <= 0)
                    {
                        if (durationOnly) return false; // permanent pets require their missing ownership lifecycle
                        normalize = true; continue;
                    }
                    INanoSpecialization? specialty = Specialty(raw.NanoId);
                    Dictionary<CharacterStat, int> modifiers;
                    if (specialty != null)
                    {
                        if (!specialty.TryPrepare(player, player, nano, out var plan) || !plan.UsesActiveNano) return false;
                        modifiers = new(plan.Modifiers);
                    }
                    else
                    {
                        if (!NanoEffectPlan.TryBuild(player, player, nano, (_, _) => throw new InvalidOperationException(),
                            restoring: true, out var plan)) return false;
                        modifiers = plan.Modifiers;
                    }
                    state.NextInstance = Math.Max(state.NextInstance, raw.NanoInstance);
                    if (state.NextInstance == int.MaxValue && raw.NanoInstance == 0) return false;
                    ActiveNanoRecord record = raw.NanoInstance == 0
                        ? raw with { NanoInstance = ++state.NextInstance } : raw;
                    long restoredDeadline = checked(now + remaining * TimeSpan.TicksPerMillisecond * 10);
                    if (!durationOnly) restoredDeadline = Math.Min(restoredDeadline, raw.ExpiresAtUtcTicks);
                    if (restoredDeadline != raw.ExpiresAtUtcTicks || raw.DurationCentiseconds == 0)
                        record = record with { DurationCentiseconds = raw.DurationCentiseconds > 0 ? raw.DurationCentiseconds : (int)remaining,
                            ExpiresAtUtcTicks = restoredDeadline };
                    normalize |= raw.NanoInstance == 0;
                    normalize |= restoredDeadline != raw.ExpiresAtUtcTicks || raw.DurationCentiseconds == 0;
                    if (state.Active.Values.Any(a => a.Record.NanoInstance == record.NanoInstance)) return false;
                    state.Active.Add(raw.Strain, new Active(record, modifiers, specialty, player.Identity.Instance));
                }
                if (!CanProject(player, state.Active.Values.SelectMany(a => a.Modifiers), subtract: null)
                    || !CanProjectDerived(state, state.Active.Values.ToArray(), currentApplied: false)
                    || !ValidActiveSet(state, state.Active.Values.ToArray())
                    || state.Active.Values.Sum(a => (long)NcuCost(a.Record.NanoId)) > int.MaxValue) return false;
                var writes = new Dictionary<Player, Dictionary<CharacterStat, int>>();
                MergeProjections(state, state.Active.Values.ToArray(), writes);
                if ((normalize || writes.Any(p => p.Value.Any(s => p.Key.Stats.GetOrZero(s.Key, StatDetail.Base) != s.Value)))
                    && !Commit([state], writes, new Dictionary<State, Active[]> { [state] = state.Active.Values.ToArray() }))
                    return false;
                if (!_states.TryAdd(player.Identity.Instance, state)) return false;
                ApplyWrites(writes);
                state.Interrupt = (_, _) => Cancel(player);
                player.TimedActionsInterrupted += state.Interrupt;
                foreach (Active active in state.Active.Values)
                {
                    ApplyModifiers(player, active.Modifiers, 1);
                    if (active.Specialty != null && _catalog.TryGet(active.Record.NanoId, out NanoDefinition nano))
                        active.Specialty.Restored(player, nano, active.Record);
                }
                UpdateDerived(state);
                SyncNcu(state);
                return true;
            }
        }

        public void RefreshPlayer(Player player)
        {
            if (!TryState(player, out State state) || !Online(player)) return;
            Expire(state);
            foreach (Active active in state.Active.Values.OrderBy(a => a.Record.NanoInstance)) SendDuration(player, active);
            SendProjections(player);
            foreach (INanoOwnerProjection projection in _specialties.OfType<INanoOwnerProjection>()) projection.Refresh(player);
        }

        /// <summary>Transport-safe cancellation; does not read/mutate Stats or active state.</summary>
        public void Cancel(Player player, IZoneSession? session = null)
        {
            if (!TryState(player, out State state)) return;
            Pending? pending = Volatile.Read(ref state.Pending);
            if (pending != null && (session == null || ReferenceEquals(pending.Session, session)))
                Interlocked.CompareExchange(ref state.Pending, null, pending);
        }

        /// <summary>Actual removal only, not a same-authority zone transfer. Durable rows already own state.</summary>
        public void DetachPlayer(Player player)
        {
            if (!TryState(player, out State state) || !_states.TryRemove(new KeyValuePair<int, State>(player.Identity.Instance, state))) return;
            Interlocked.Exchange(ref state.Pending, null);
            player.TimedActionsInterrupted -= state.Interrupt;
            foreach (Active active in state.Active.Values) ApplyModifiers(player, active.Modifiers, -1);
            ApplyModifiers(player, state.Derived, -1);
            foreach (INanoSpecialization specialty in _specialties) specialty.Detached(player);
            player.Stats.Set(CharacterStat.CurrentNCU, 0, StatDetail.Base, dirty: true);
        }

        /// <summary>Call immediately after equipment rebasing has cleared and rebuilt Bonus values.</summary>
        public void ReapplyBonusesAfterRebase(Player player)
        {
            if (!TryState(player, out State state)) return;
            // The caller just cleared all Bonus values; the old derived contribution is gone too.
            state.Derived.Clear();
            foreach (Active active in state.Active.Values) ApplyModifiers(player, active.Modifiers, 1);
            UpdateDerived(state);
            foreach (INanoOwnerProjection projection in _specialties.OfType<INanoOwnerProjection>()) projection.ReapplyAfterRebase(player);
        }

        public bool IsFightingRestricted(Player player) => TryState(player, out _)
            && _specialties.OfType<INanoOwnerProjection>().Any(p => p.IsFightingRestricted(player));

        /// <summary>
        /// Pure prospective progression helper: the caller first builds detached new-level/title,
        /// equipment and base resource maxima. Apply current literal nano contributions and derive
        /// their resource bonuses against that detached result. No appearance projection, owner
        /// mutation, packets, expiry transition or DAO write occurs here. Call once per fresh plan.
        /// </summary>
        public void ProjectBonusesAfterRebase(Player player, StatCollection detachedStats)
        {
            ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(detachedStats);
            if (ReferenceEquals(player.Stats, detachedStats)) throw new ArgumentException("Projection requires detached stats.", nameof(detachedStats));
            lock (player.PersistenceGate)
            {
                if (player.IsPersistenceQuarantined) throw new InvalidOperationException("Quarantined nano owner cannot project a reward.");
                if (!_states.TryGetValue(player.Identity.Instance, out State? state)) return;
                if (!ReferenceEquals(state.Player, player)) throw new InvalidOperationException("Nano projection owner changed.");
                // Work on another detached value first: checked failures cannot partially alter
                // even the caller's proposed reward projection.
                var projected = new StatCollection();
                foreach (var entry in detachedStats.GetEntries())
                { projected.Set(entry.Stat, entry.Base); projected.Set(entry.Stat, entry.Bonus, StatDetail.Bonus); }
                foreach (Active active in state.Active.Values)
                    foreach (var modifier in active.Modifiers) AddChecked(modifier.Key, modifier.Value);
                int body = checked(state.Active.Values.Sum(a => a.Modifiers.GetValueOrDefault(CharacterStat.BodyDevelopment)));
                int pool = checked(state.Active.Values.Sum(a => a.Modifiers.GetValueOrDefault(CharacterStat.NanoPool)));
                AddChecked(CharacterStat.MaxHealth, checked(-NanoDerivedStats.HealthDelta(projected, checked(-body))));
                AddChecked(CharacterStat.MaxNanoEnergy, checked(-NanoDerivedStats.NanoDelta(projected, checked(-pool))));
                foreach (var entry in projected.GetEntries()) detachedStats.Set(entry.Stat, entry.Bonus, StatDetail.Bonus);
                void AddChecked(CharacterStat stat, int amount)
                {
                    int bonus = checked(projected.GetOrZero(stat, StatDetail.Bonus) + amount);
                    _ = checked(projected.GetOrZero(stat, StatDetail.Base) + bonus);
                    projected.Set(stat, bonus, StatDetail.Bonus);
                }
            }
        }

        public IReadOnlyList<ActiveNanoRecord> GetActive(Player player)
            => TryState(player, out State state) ? state.Active.Values.Select(a => a.Record).ToArray() : [];

        public bool TryCast(Player caster, int nanoId, Identity targetIdentity)
        {
            if (!TryState(caster, out State state) || !Online(caster) || state.Pending != null
                || _milliseconds() < state.ReadyAt || !_catalog.TryGet(nanoId, out NanoDefinition nano)
                || !caster.UploadedNanoIds.Contains(nanoId)) return false;
            Player? target = targetIdentity == Identity.None || targetIdentity == caster.Identity ? caster
                : _states.TryGetValue(targetIdentity.Instance, out State? targetState)
                    && targetState.Player.Identity == targetIdentity ? targetState.Player : null;
            if (target == null || !Validate(caster, target, nano, out _, out _, out _)
                || !nano.TryCalculateAttackTime(caster.Stats.GetOrZero(CharacterStat.AggDef),
                    caster.Stats.GetOrZero(CharacterStat.NanoCInit), out int attack)) return false;
            var pending = new Pending(nano, target, caster.Session!, target.Session!, caster.Playfield,
                targetIdentity, checked(_milliseconds() + (long)attack * 10));
            if (Interlocked.CompareExchange(ref state.Pending, pending, null) != null) return false;
            Announce(caster, new CastNanoSpellMessage { Identity = caster.Identity, Unknown = 0,
                Caster = caster.Identity, Target = targetIdentity, NanoId = nanoId, Unknown1 = 0 });
            if (attack == 0) Tick(caster);
            return true;
        }

        /// <summary>Call from each player's playfield tick; deadlines never block the tick thread.</summary>
        public void Tick(Player player)
        {
            if (!TryState(player, out State state) || player.IsPersistenceQuarantined) return;
            Expire(state);
            Pending? pending = Volatile.Read(ref state.Pending);
            if (pending != null && _milliseconds() >= pending.CompletesAt
                && ReferenceEquals(Interlocked.CompareExchange(ref state.Pending, null, pending), pending))
                Complete(state, pending);
            foreach (INanoSpecialization specialty in _specialties) specialty.Tick(player);
        }

        public bool Remove(Player player, int nanoId)
        {
            if (!TryState(player, out State state) || !Online(player)) return false;
            Active? active = state.Active.Values.FirstOrDefault(a => a.Record.NanoId == nanoId);
            return active != null && Remove(state, [active]);
        }

        public bool TryRemove(Player player, CharacterActionMessage message)
        {
            if (message.Action != CharacterActionType.RemoveFriendlyNano || !TryState(player, out State state)) return false;
            int nanoId;
            if (message.Target.Type == IdentityType.NanoProgram && message.Target.Instance != 0) nanoId = message.Target.Instance;
            else if (message.Parameter2 > 0) nanoId = message.Parameter2;
            else if (message.Target.Instance > 0 && _catalog.TryGet(message.Target.Instance, out _)) nanoId = message.Target.Instance;
            else nanoId = message.Parameter1 > 0 ? state.Active.Values
                .FirstOrDefault(a => a.Record.NanoInstance == message.Parameter1)?.Record.NanoId ?? 0 : 0;
            // No arbitrary single-active fallback or untracked vehicle clear against a different owner.
            return nanoId > 0 && Remove(player, nanoId);
        }

        private bool Validate(Player caster, Player target, NanoDefinition nano, out INanoSpecialization? specialty,
            out int duration, out bool usesActive)
        {
            specialty = Specialty(nano.Id); duration = Math.Min(nano.DurationCentiseconds, MaximumDurationCentiseconds); usesActive = duration > 0;
            if (!Online(caster) || !Online(target) || !TryState(target, out State state)
                || !ReferenceEquals(caster.Playfield, target.Playfield)
                || caster.Stats.GetOrZero(CharacterStat.CurrentNano) < nano.NanoCost
                || !nano.TryCalculateAttackTime(0, 0, out _) || !NanoEffectPlan.ActionRequirements(caster, target, nano)) return false;
            if (!ReferenceEquals(caster, target) && (nano.RangeMeters <= 0
                || caster.Distance3D(target) > nano.RangeMeters || !caster.HasLineOfSightTo(target))) return false;
            if (specialty != null)
            {
                if (!specialty.TryPrepare(caster, target, nano, out var plan)) return false;
                duration = Math.Min(plan.DurationCentiseconds, MaximumDurationCentiseconds); usesActive = plan.UsesActiveNano;
                if (duration < 0 || (usesActive && duration == 0)) return false;
            }
            else
            {
                Active? previous = state.Active.GetValueOrDefault(nano.Strain);
                if (!NanoEffectPlan.TryBuild(caster, target, nano, (minimum, _) => minimum, false, out _,
                    previous?.Record.NanoId == nano.Id ? previous.Modifiers : null)) return false;
            }
            if (usesActive)
            {
                long used = state.Active.Values.Where(a => a.Record.Strain != nano.Strain)
                    .Sum(a => (long)NcuCost(a.Record.NanoId));
                if (used + nano.NcuCost > Math.Max(0, target.Stats.GetOrZero(CharacterStat.MaxNCU))) return false;
            }
            return true;
        }

        private void Complete(State casterState, Pending pending)
        {
            if (!TryState(pending.Target, out State targetState)) return;
            State[] participants = new[] { casterState, targetState }.Distinct().OrderBy(s => s.Player.Identity.Instance).ToArray();
            int locked = 0;
            try
            {
                // Keep durable commit AND its in-memory projection behind the same snapshot gate.
                foreach (State state in participants) { Monitor.Enter(state.Player.PersistenceGate); locked++; }
                CompleteLocked(casterState, pending);
            }
            finally { for (int i = locked - 1; i >= 0; i--) Monitor.Exit(participants[i].Player.PersistenceGate); }
        }

        private void CompleteLocked(State casterState, Pending pending)
        {
            Player caster = casterState.Player; Player target = pending.Target;
            if (!ReferenceEquals(caster.Session, pending.Session) || !ReferenceEquals(target.Session, pending.TargetSession)
                || !ReferenceEquals(caster.Playfield, pending.Playfield) || !caster.UploadedNanoIds.Contains(pending.Nano.Id)
                || !Validate(caster, target, pending.Nano, out var specialty, out int duration, out bool usesActive)
                || !TryState(target, out State targetState)) return;
            var effects = new NanoEffectPlan();
            Active? previous = usesActive ? targetState.Active.GetValueOrDefault(pending.Nano.Strain) : null;
            if (specialty == null)
            {
                if (!NanoEffectPlan.TryBuild(caster, target, pending.Nano, _next, false, out effects,
                    previous?.Record.NanoId == pending.Nano.Id ? previous.Modifiers : null)) return;
            }
            else
            {
                if (!specialty.TryPrepare(caster, target, pending.Nano, out var plan)) return;
                foreach (var modifier in plan.Modifiers) effects.Modifiers.Add(modifier.Key, modifier.Value);
                effects.BaseWrites[caster] = new() { [CharacterStat.CurrentNano] =
                    caster.Stats.GetOrZero(CharacterStat.CurrentNano, StatDetail.Base) - pending.Nano.NanoCost };
            }
            Active? active = null;
            if (usesActive)
            {
                if (targetState.NextInstance == int.MaxValue || !CanProject(target, effects.Modifiers, previous?.Modifiers)) return;
                long expiry;
                try { expiry = _utcNow().AddMilliseconds((long)duration * 10).Ticks; }
                catch (ArgumentOutOfRangeException) { return; }
                int instance = previous?.Record.NanoId == pending.Nano.Id ? previous.Record.NanoInstance : targetState.NextInstance + 1;
                active = new Active(new ActiveNanoRecord(pending.Nano.Id, pending.Nano.Strain, instance, duration, expiry),
                    effects.Modifiers, specialty, caster.Identity.Instance);
            }
            var replacements = new Dictionary<State, Active[]>();
            if (active != null)
            {
                Active[] next = targetState.Active.Values.Where(a => a.Record.Strain != active.Record.Strain).Append(active).ToArray();
                if (!CanProjectDerived(targetState, next)) return;
                replacements[targetState] = next;
            }
            State[] participants = new[] { casterState, targetState }.Distinct().OrderBy(s => s.Player.Identity.Instance).ToArray();
            if (!Commit(participants, effects.BaseWrites, replacements)) return;
            casterState.ReadyAt = checked(_milliseconds() + (long)pending.Nano.RechargeCentiseconds * 10);
            if (previous != null)
            {
                ApplyModifiers(target, previous.Modifiers, -1);
                if (previous.Record.NanoId != pending.Nano.Id)
                {
                    previous.Specialty?.Removed(target, previous.Record.NanoId);
                    SendRemoval(target, previous.Record.NanoId);
                }
            }
            if (active != null)
            {
                targetState.NextInstance = Math.Max(targetState.NextInstance, active.Record.NanoInstance);
                targetState.Active[active.Record.Strain] = active;
                ApplyModifiers(target, active.Modifiers, 1); UpdateDerived(targetState); SyncNcu(targetState);
            }
            ApplyWrites(effects.BaseWrites);
            caster.Session?.Send(new CharacterActionMessage { Identity = caster.Identity, Unknown = 0,
                Action = CharacterActionType.FinishNanoCasting, Target = Identity.None, Parameter1 = 1, Parameter2 = pending.Nano.Id });
            foreach (State participant in participants) Flush(participant.Player);
            specialty?.Applied(caster, target, pending.Nano, active?.Record);
            if (active != null) SendDuration(target, active);
            foreach (State participant in participants) SendProjections(participant.Player);
        }

        private void Expire(State state)
        {
            Active[] expired = state.Active.Values.Where(a => a.Record.ExpiresAtUtcTicks <= _utcNow().Ticks).ToArray();
            if (expired.Length > 0) Remove(state, expired);
        }

        private bool Remove(State state, Active[] removed)
        {
            lock (state.Player.PersistenceGate) return RemoveLocked(state, removed);
        }

        private bool RemoveLocked(State state, Active[] removed)
        {
            var removeSet = removed.ToHashSet();
            Active[] next = state.Active.Values.Where(a => !removeSet.Contains(a)).ToArray();
            if (!CanProjectDerived(state, next)) return false;
            var writes = new Dictionary<Player, Dictionary<CharacterStat, int>>();
            if (!Commit([state], writes, new Dictionary<State, Active[]> { [state] = next }))
                return false;
            ApplyWrites(writes);
            foreach (Active active in removed)
            {
                state.Active.Remove(active.Record.Strain); ApplyModifiers(state.Player, active.Modifiers, -1);
                active.Specialty?.Removed(state.Player, active.Record.NanoId); SendRemoval(state.Player, active.Record.NanoId);
            }
            UpdateDerived(state);
            SyncNcu(state); Flush(state.Player); SendProjections(state.Player); return true;
        }

        private bool Commit(State[] participants, Dictionary<Player, Dictionary<CharacterStat, int>> writes,
            Dictionary<State, Active[]> replacements)
        {
            int locked = 0;
            try
            {
                foreach (State state in participants) { Monitor.Enter(state.Player.PersistenceGate); locked++; }
                if (participants.Any(s => s.Player.IsPersistenceQuarantined)) return false;
                if (participants.Any(s => !ValidActiveSet(s,
                    replacements.TryGetValue(s, out var proposed) ? proposed : s.Active.Values.ToArray()))) return false;
                foreach (State state in participants)
                    MergeProjections(state, replacements.TryGetValue(state, out var next) ? next : state.Active.Values.ToArray(), writes);
                _repository.Commit(participants.Select(s => new NanoCharacterWrite(s.Player.Identity.Instance,
                    (replacements.TryGetValue(s, out var active) ? active : s.Active.Values.ToArray()).Select(a => a.Record).ToArray(),
                    writes.TryGetValue(s.Player, out var stats) ? stats.Select(p => new StatRecord { StatId = (int)p.Key, StatValue = p.Value }).ToArray() : [])).ToArray());
                return true;
            }
            catch (DatabaseCommitOutcomeUnknownException exception)
            {
                foreach (State state in participants)
                {
                    state.Player.QuarantinePersistence(); Cancel(state.Player);
                    state.Player.Logger.Error(exception, "Nano commit outcome unknown; reconciliation required.");
                    state.Player.Session?.Close();
                }
                return false;
            }
            catch (Exception exception)
            {
                participants[0].Player.Logger.Error(exception, "Nano transaction failed; memory and completion packets unchanged.");
                return false;
            }
            finally { for (int i = locked - 1; i >= 0; i--) Monitor.Exit(participants[i].Player.PersistenceGate); }
        }

        private bool TryState(Player player, out State state)
            => _states.TryGetValue(player.Identity.Instance, out state!) && ReferenceEquals(state.Player, player);
        private bool ValidActiveSet(State state, Active[] active) => _specialties.OfType<INanoActiveSetValidator>()
            .All(v => v.IsValidActiveSet(state.Player, active.Select(a => a.Record).ToArray()));
        private void MergeProjections(State state, Active[] active, Dictionary<Player, Dictionary<CharacterStat, int>> writes)
        {
            ActiveNanoRecord[] records = active.Select(a => a.Record).ToArray();
            var seen = new Dictionary<CharacterStat, int>();
            foreach (IActiveNanoProjection projection in _projections)
                foreach (var stat in projection.Project(records))
                {
                    if (!_projectionKeys.Contains(stat.Key) || (seen.TryGetValue(stat.Key, out int previous) && previous != stat.Value))
                        throw new InvalidOperationException("Conflicting or dynamic nano base projection keys.");
                    seen[stat.Key] = stat.Value;
                    if (!writes.TryGetValue(state.Player, out var playerWrites)) writes[state.Player] = playerWrites = new();
                    playerWrites[stat.Key] = stat.Value;
                }
        }
        private void ApplyWrites(Dictionary<Player, Dictionary<CharacterStat, int>> writes)
        {
            foreach (var player in writes)
                foreach (var stat in player.Value) player.Key.Stats.Set(stat.Key, stat.Value, StatDetail.Base, dirty: !_projectionKeys.Contains(stat.Key));
        }
        private void SendProjections(Player player)
        { foreach (IActiveNanoProjection projection in _projections) projection.SendProjection(player); }
        private static void UpdateDerived(State state)
        {
            ApplyModifiers(state.Player, state.Derived, -1); state.Derived.Clear();
            int body = checked(state.Active.Values.Sum(a => a.Modifiers.GetValueOrDefault(CharacterStat.BodyDevelopment)));
            int pool = checked(state.Active.Values.Sum(a => a.Modifiers.GetValueOrDefault(CharacterStat.NanoPool)));
            int health = checked(-NanoDerivedStats.HealthDelta(state.Player, checked(-body)));
            int nano = checked(-NanoDerivedStats.NanoDelta(state.Player, checked(-pool)));
            if (health != 0) state.Derived[CharacterStat.MaxHealth] = health;
            if (nano != 0) state.Derived[CharacterStat.MaxNanoEnergy] = nano;
            ApplyModifiers(state.Player, state.Derived, 1);
        }
        private static bool CanProjectDerived(State state, Active[] next, bool currentApplied = true)
        {
            try
            {
                Active[] prior = currentApplied ? state.Active.Values.ToArray() : [];
                foreach (var pair in new[] { (CharacterStat.BodyDevelopment, CharacterStat.MaxHealth),
                    (CharacterStat.NanoPool, CharacterStat.MaxNanoEnergy) })
                {
                    int oldSource = checked(prior.Sum(a => a.Modifiers.GetValueOrDefault(pair.Item1)));
                    int newSource = checked(next.Sum(a => a.Modifiers.GetValueOrDefault(pair.Item1)));
                    if (oldSource == int.MinValue || newSource == int.MinValue) return false;
                    int changed = checked(newSource - oldSource);
                    int derived = pair.Item1 == CharacterStat.BodyDevelopment
                        ? checked(NanoDerivedStats.HealthDelta(state.Player, changed) - NanoDerivedStats.HealthDelta(state.Player, -oldSource))
                        : checked(NanoDerivedStats.NanoDelta(state.Player, changed) - NanoDerivedStats.NanoDelta(state.Player, -oldSource));
                    long oldLiteral = prior.Sum(a => (long)a.Modifiers.GetValueOrDefault(pair.Item2));
                    long newLiteral = next.Sum(a => (long)a.Modifiers.GetValueOrDefault(pair.Item2));
                    long delta = newLiteral - oldLiteral + derived - state.Derived.GetValueOrDefault(pair.Item2);
                    if ((long)state.Player.Stats.GetOrZero(pair.Item2) + delta is < int.MinValue or > int.MaxValue
                        || (long)state.Player.Stats.GetOrZero(pair.Item2, StatDetail.Bonus) + delta is < int.MinValue or > int.MaxValue) return false;
                }
                return true;
            }
            catch (OverflowException) { return false; }
        }
        private bool Online(Player player) => TryState(player, out _) && !player.IsDead && !player.IsPersistenceQuarantined
            && player.Stats.GetOrZero(CharacterStat.Health) > 0
            && player.ConnectionPhase == PlayerConnectionPhase.Online && player.Session is { State: SessionState.InPlay } session
            && ReferenceEquals(session.Player, player);
        private INanoSpecialization? Specialty(int nanoId) => _specialties.SingleOrDefault(s => s.Handles(nanoId));
        private int NcuCost(int nanoId) => _catalog.TryGet(nanoId, out var nano) ? nano.NcuCost : 0;
        private void SyncNcu(State state) => state.Player.Stats.Set(CharacterStat.CurrentNCU,
            checked(state.Active.Values.Sum(a => NcuCost(a.Record.NanoId))), StatDetail.Base, dirty: true);
        private static bool CanProject(Player player, IEnumerable<KeyValuePair<CharacterStat, int>> add,
            Dictionary<CharacterStat, int>? subtract)
        {
            var deltas = new Dictionary<CharacterStat, long>();
            foreach (var pair in add) deltas[pair.Key] = deltas.GetValueOrDefault(pair.Key) + pair.Value;
            if (subtract != null) foreach (var pair in subtract) deltas[pair.Key] = deltas.GetValueOrDefault(pair.Key) - pair.Value;
            return deltas.All(p => (long)player.Stats.GetOrZero(p.Key, StatDetail.Bonus) + p.Value is >= int.MinValue and <= int.MaxValue
                && (long)player.Stats.GetOrZero(p.Key) + p.Value is >= int.MinValue and <= int.MaxValue);
        }
        private static void ApplyModifiers(Player player, Dictionary<CharacterStat, int> modifiers, int sign)
        { foreach (var modifier in modifiers) player.Stats.AddBonus(modifier.Key, checked(sign * modifier.Value), dirty: true); }
        private void SendDuration(Player player, Active active)
        {
            if (!Online(player)) return;
            long remaining = Math.Max(0, (active.Record.ExpiresAtUtcTicks - _utcNow().Ticks + TimeSpan.TicksPerMillisecond * 10 - 1)
                / (TimeSpan.TicksPerMillisecond * 10));
            remaining = Math.Min(remaining, active.Record.DurationCentiseconds);
            player.Session!.Send(new CharacterActionMessage { Identity = player.Identity, Unknown = 0,
                Action = CharacterActionType.SetNanoDuration, Target = new Identity { Type = IdentityType.NanoProgram, Instance = active.Record.NanoId },
                Parameter1 = active.CasterId, Parameter2 = (int)Math.Min(int.MaxValue, remaining) });
        }
        private static void SendRemoval(Player player, int nanoId)
        {
            if (player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player))
                session.Send(new BuffMessage { Identity = player.Identity, Action = 0,
                    NanoProgram = new Identity { Type = IdentityType.NanoProgram, Instance = nanoId } });
        }
        private static void Announce(Player player, MessageBody body)
        {
            if (player.Playfield != null) player.Playfield.GetRequiredService<PlayfieldLocality>().Announce(player, body, includeSelf: true);
            else player.Session?.Send(body);
        }
        private static void Flush(Player player)
        {
            if (player.Playfield != null) player.FlushDirtyStats();
            else
            {
                var dirty = player.Stats.DrainDirty();
                if (dirty.Length > 0) player.Session?.Send(new StatMessage { Identity = player.Identity, Stats = dirty });
            }
        }
    }
}
