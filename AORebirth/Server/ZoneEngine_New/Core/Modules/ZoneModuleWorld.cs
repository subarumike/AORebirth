namespace ZoneEngine_New.Core.Modules
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Modules.World;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// The module API's world view. Each playfield publishes an immutable snapshot at the end of its tick, only when a
    /// reader needs one: with no reader a tick costs one dictionary lookup.
    /// Readers never touch live entities off their tick. A read on the owning tick builds the snapshot in place. A read
    /// from a module thread is served a snapshot at most one tick old; when the last one is older it asks the playfield
    /// for one and waits a bounded time for that tick. A read from another playfield's tick never waits, so while such
    /// reads arrive (within <see cref="DemandLifetime"/>) the playfield publishes on every tick to keep them current.
    /// </summary>
    internal sealed class ZoneModuleWorld : IZoneWorld
    {
        /// <summary>
        /// How long one read keeps a playfield's snapshot (and a watched stat) alive; for a read from another playfield's
        /// tick, also how long the playfield keeps publishing on every tick.
        /// </summary>
        internal static readonly TimeSpan DemandLifetime = TimeSpan.FromSeconds(30);

        /// <summary>The freshness limit for a playfield whose tick rate is not known (no heartbeat).</summary>
        static readonly long MaxAgeTicks = Stopwatch.Frequency / 2;

        static readonly long DemandTicks = (long)(DemandLifetime.TotalSeconds * Stopwatch.Frequency);

        /// <summary>The longest a read from a module thread waits for snapshots.</summary>
        internal static TimeSpan WaitLimit { get; set; } = TimeSpan.FromMilliseconds(500);

        readonly Lazy<PlayfieldManager> _manager;
        readonly ConcurrentDictionary<int, Slot> _slots = new();

        public ZoneModuleWorld(Func<PlayfieldManager> manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            _manager = new Lazy<PlayfieldManager>(manager);
        }

        public IReadOnlyList<PlayerInfo> Players()
        {
            var players = new List<PlayerInfo>();
            foreach (Snapshot snapshot in ReadAll())
                players.AddRange(snapshot.Players);
            return players.ToArray();
        }

        public PlayerInfo? Player(int characterId)
        {
            if (!_manager.Value.FindPlayer(characterId, out Player player) || player.Playfield is not Playfield playfield)
                return null;
            Snapshot? snapshot = Read(playfield);
            return snapshot != null && snapshot.PlayersById.TryGetValue(characterId, out PlayerInfo? info) ? info : null;
        }

        public IReadOnlyList<int> Playfields()
        {
            IReadOnlyList<Playfield> playfields = _manager.Value.SnapshotPlayfields();
            var ids = new int[playfields.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = playfields[i].Identity.Instance;
            return ids;
        }

        public IReadOnlyList<NpcInfo> Npcs(int playfieldId)
            => Find(playfieldId) is Playfield playfield && Read(playfield) is Snapshot snapshot ? snapshot.Npcs : Array.Empty<NpcInfo>();

        public NpcInfo? Npc(int playfieldId, int instance)
            => Find(playfieldId) is Playfield playfield && Read(playfield) is Snapshot snapshot
                && snapshot.NpcsById.TryGetValue(instance, out NpcInfo? info) ? info : null;

        /// <summary>Read from the player's live connection, not a snapshot: the fields it reads never change after login.</summary>
        public SessionInfo? Session(int characterId)
        {
            if (!_manager.Value.FindPlayer(characterId, out Player player)
                || player.Session is not ZoneEngine_New.Core.Network.ZoneSession session || session.IsClosed)
                return null;

            return new SessionInfo
            {
                SessionId = session.Id,
                CharacterId = characterId,
                AccountName = session.AccountName,
                RemoteAddress = session.RemoteAddress()?.ToString() ?? string.Empty,
                ConnectedAtUtc = session.ConnectedAtUtc
            };
        }

        /// <summary>
        /// The player's running locks (module API 1.6), from the snapshot their playfield publishes; read on the player's own
        /// tick they come straight from the character.
        /// </summary>
        public IReadOnlyList<CooldownInfo> Cooldowns(int characterId)
        {
            if (!_manager.Value.FindPlayer(characterId, out Player player) || player.Playfield is not Playfield playfield)
                return Array.Empty<CooldownInfo>();
            if (playfield.IsTickHeldByCurrentThread)
                return ModuleCooldowns.Read(player);

            Slot slot = SlotFor(playfield);
            long now = Stopwatch.GetTimestamp();
            bool known = slot.CooldownWatch.ContainsKey(characterId);
            slot.CooldownWatch[characterId] = now + DemandTicks;
            slot.Demand(now + DemandTicks);
            Snapshot? current = slot.Current;
            if (Playfield.IsOnTickThread)
            {
                slot.DemandEveryTick(now + DemandTicks);
                return current != null && current.Cooldowns.TryGetValue(characterId, out CooldownInfo[]? stale) ? stale : Array.Empty<CooldownInfo>();
            }

            if (known && IsFresh(current, playfield, now) && current!.Cooldowns.TryGetValue(characterId, out CooldownInfo[]? cached))
                return cached;

            // A newly watched player is in the snapshot after the next publish.
            slot.RequestPublish().Wait(WaitLimit);
            return slot.Current is Snapshot published && published.Cooldowns.TryGetValue(characterId, out CooldownInfo[]? value)
                ? value
                : Array.Empty<CooldownInfo>();
        }

        /// <summary>Live counters on the connection (module API 1.6), never a snapshot.</summary>
        public IReadOnlyDictionary<int, MessageCount>? MessageCounts(Guid sessionId)
            => ZoneEngine_New.Core.Network.ZoneSession.TryFind(sessionId, out ZoneEngine_New.Core.Network.ZoneSession session)
                ? session.MessageCounts()
                : null;

        public int? Stat(EntityRef entity, int statId)
        {
            if (entity.Type != EntityRef.CharacterType)
                return null;

            Playfield? playfield = null;
            if (_manager.Value.FindPlayer(entity.Instance, out Player player))
                playfield = player.Playfield;
            else
            {
                foreach (Playfield candidate in _manager.Value.SnapshotPlayfields())
                {
                    if (Read(candidate) is Snapshot snapshot && snapshot.NpcsById.ContainsKey(entity.Instance))
                    {
                        playfield = candidate;
                        break;
                    }
                }
            }

            if (playfield == null)
                return null;

            var key = new StatKey(entity.Instance, statId);
            Slot slot = SlotFor(playfield);
            long now = Stopwatch.GetTimestamp();
            bool known = slot.Watch.ContainsKey(key);
            slot.Watch[key] = now + DemandTicks;
            slot.Demand(now + DemandTicks);
            if (playfield.IsTickHeldByCurrentThread)
                return LiveStat(playfield, key);

            Snapshot? current = slot.Current;
            if (Playfield.IsOnTickThread)
            {
                slot.DemandEveryTick(now + DemandTicks);
                return current != null && current.Stats.TryGetValue(key, out int? stale) ? stale : null;
            }

            if (known && IsFresh(current, playfield, now) && current!.Stats.TryGetValue(key, out int? cached))
                return cached;

            // A newly watched stat is in the snapshot after the next publish.
            slot.RequestPublish().Wait(WaitLimit);
            return slot.Current is Snapshot published && published.Stats.TryGetValue(key, out int? value) ? value : null;
        }

        /// <summary>
        /// End of a playfield tick (its tick lock held): publish when a module thread asked for a snapshot, or on every
        /// tick while another playfield's tick is reading this one.
        /// </summary>
        internal void OnTick(Playfield playfield)
        {
            if (!_slots.TryGetValue(playfield.Identity.Instance, out Slot? slot))
                return;

            long now = Stopwatch.GetTimestamp();
            if (now > Volatile.Read(ref slot.DemandUntil))
            {
                if (slot.Current != null)
                {
                    slot.Current = null;
                    slot.Watch.Clear();
                    slot.CooldownWatch.Clear();
                }

                return;
            }

            bool requested = slot.TakeRequest();
            if (!requested && now > Volatile.Read(ref slot.EveryTickUntil))
                return;

            slot.Publish(Build(playfield, slot, now));
        }

        /// <summary>Built no more than one tick of <paramref name="playfield"/> ago.</summary>
        static bool IsFresh(Snapshot? snapshot, Playfield playfield, long now)
        {
            if (snapshot == null)
                return false;
            int tickRate = playfield.TickRate;
            long freshTicks = tickRate > 0 ? Stopwatch.Frequency / tickRate : MaxAgeTicks;
            return now - snapshot.BuiltAt <= freshTicks;
        }

        Playfield? Find(int playfieldId)
        {
            foreach (Playfield playfield in _manager.Value.SnapshotPlayfields())
            {
                if (playfield.Identity.Instance == playfieldId)
                    return playfield;
            }

            return null;
        }

        Slot SlotFor(Playfield playfield) => _slots.GetOrAdd(playfield.Identity.Instance, static _ => new Slot());

        Snapshot? Read(Playfield playfield)
        {
            Slot slot = SlotFor(playfield);
            long now = Stopwatch.GetTimestamp();
            slot.Demand(now + DemandTicks);
            Snapshot? current = slot.Current;
            if (playfield.IsTickHeldByCurrentThread)
            {
                if (IsFresh(current, playfield, now))
                    return current;
                current = Build(playfield, slot, now);
                slot.Publish(current);
                return current;
            }

            if (Playfield.IsOnTickThread)
            {
                slot.DemandEveryTick(now + DemandTicks);
                return current;
            }

            if (IsFresh(current, playfield, now))
                return current;

            slot.RequestPublish().Wait(WaitLimit);
            return slot.Current;
        }

        List<Snapshot> ReadAll()
        {
            IReadOnlyList<Playfield> playfields = _manager.Value.SnapshotPlayfields();
            long now = Stopwatch.GetTimestamp();
            bool onTick = Playfield.IsOnTickThread;
            var waits = new List<Task>();
            foreach (Playfield playfield in playfields)
            {
                Slot slot = SlotFor(playfield);
                slot.Demand(now + DemandTicks);
                if (playfield.IsTickHeldByCurrentThread)
                    continue;
                if (onTick)
                    slot.DemandEveryTick(now + DemandTicks);
                else if (!IsFresh(slot.Current, playfield, now))
                    waits.Add(slot.RequestPublish());
            }

            // Every playfield publishes on its own tick: wait for all of them at once, not one after another.
            if (waits.Count != 0)
                Task.WaitAll(waits.ToArray(), WaitLimit);

            var snapshots = new List<Snapshot>(playfields.Count);
            foreach (Playfield playfield in playfields)
            {
                if (Read(playfield, wait: false) is Snapshot snapshot)
                    snapshots.Add(snapshot);
            }

            return snapshots;
        }

        Snapshot? Read(Playfield playfield, bool wait)
        {
            if (wait)
                return Read(playfield);
            Slot slot = SlotFor(playfield);
            if (playfield.IsTickHeldByCurrentThread)
            {
                long now = Stopwatch.GetTimestamp();
                Snapshot? current = slot.Current;
                if (IsFresh(current, playfield, now))
                    return current;
                current = Build(playfield, slot, now);
                slot.Publish(current);
                return current;
            }

            return slot.Current;
        }

        /// <summary>Copies everything a reader can ask for. Runs on <paramref name="playfield"/>'s tick only.</summary>
        static Snapshot Build(Playfield playfield, Slot slot, long now)
        {
            int playfieldId = playfield.Identity.Instance;
            Knubot.KnubotCatalog? knubot = playfield.GetService<Knubot.KnubotCatalog>();
            Teams.TeamService? teams = playfield.GetService<Teams.TeamService>();
            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            var players = new List<PlayerInfo>();
            var npcs = new List<NpcInfo>();
            foreach (Dynel dynel in registry.Dynels())
            {
                if (!ReferenceEquals(dynel.Playfield, playfield))
                    continue;
                if (dynel is Player player)
                    players.Add(Project(player, playfieldId, teams));
                else if (dynel is NpcCharacter npc)
                    npcs.Add(Project(npc, playfieldId, knubot));
            }

            var stats = new Dictionary<StatKey, int?>();
            foreach (KeyValuePair<StatKey, long> watched in slot.Watch)
            {
                if (now > watched.Value)
                    slot.Watch.TryRemove(watched);
                else
                    stats[watched.Key] = LiveStat(playfield, watched.Key);
            }

            var cooldowns = new Dictionary<int, CooldownInfo[]>();
            foreach (KeyValuePair<int, long> watched in slot.CooldownWatch)
            {
                if (now > watched.Value)
                    slot.CooldownWatch.TryRemove(watched);
                else if (registry.TryGet(new Identity { Type = IdentityType.CanbeAffected, Instance = watched.Key }, out Dynel? dynel)
                         && dynel is Player watchedPlayer && ReferenceEquals(watchedPlayer.Playfield, playfield))
                    cooldowns[watched.Key] = ModuleCooldowns.Read(watchedPlayer);
            }

            return new Snapshot(now, players.ToArray(), npcs.ToArray(), stats, cooldowns);
        }

        static int? LiveStat(Playfield playfield, StatKey key)
        {
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = key.Instance };
            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel)
                || dynel is not Character character
                || !ReferenceEquals(character.Playfield, playfield))
                return null;
            if (!character.Stats.TryGetValue((CharacterStat)key.StatId, out int value) || StatCollection.IsUnset(value))
                return null;
            return value;
        }

        internal static PlayerInfo Project(Player player, int playfieldId, Teams.TeamService? teams)
        {
            AORebirth.Core.Vector.Vector3 at = player.Position;
            Identity target = player.Target;
            int teamId = 0;
            bool teamLeader = false;
            teams?.TryGetTeamId(player, out teamId, out teamLeader);
            return new PlayerInfo
            {
                CharacterId = player.Identity.Instance,
                Name = DebugMcp.ZoneDebugSnapshots.DisplayName(player),
                FirstName = player.FirstName ?? string.Empty,
                LastName = player.LastName ?? string.Empty,
                PlayfieldId = playfieldId,
                Position = new WorldPosition(at.xf, at.yf, at.zf),
                HeadingDegrees = (float)(player.Rotation.yaw * (180.0 / Math.PI)),
                Level = player.Stats.GetOrZero(CharacterStat.Level),
                Health = player.Stats.GetOrZero(CharacterStat.Health),
                MaxHealth = player.Stats.GetOrZero(CharacterStat.MaxHealth),
                Nano = player.Stats.GetOrZero(CharacterStat.CurrentNano),
                MaxNano = player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy),
                ProfessionId = player.Stats.GetOrZero(CharacterStat.Profession),
                BreedId = player.Stats.GetOrZero(CharacterStat.Breed),
                GmLevel = player.Stats.GetOrZero(CharacterStat.GmLevel),
                IsDead = player.IsDead,
                Target = target.Instance == 0 ? default : new EntityRef((int)target.Type, target.Instance),
                IsConnected = player.Session != null,
                InCombat = player.IsInCombat,
                IsSitting = player.Motor.State == ZoneEngine_New.Core.Movement.MovementState.Sit,
                IsMoving = player.Motor.IsMoving,
                TeamId = teamId,
                TeamLeader = teamLeader,
                OrgId = player.Stats.GetOrZero(CharacterStat.Clan)
            };
        }

        internal static NpcInfo Project(NpcCharacter npc, int playfieldId, Knubot.KnubotCatalog? knubot)
        {
            AORebirth.Core.Vector.Vector3 at = npc.Position;
            return new NpcInfo
            {
                Instance = npc.Identity.Instance,
                TemplateHash = npc.MobTemplate?.Hash ?? string.Empty,
                Name = npc.Name ?? npc.MobTemplate?.Name ?? string.Empty,
                Level = npc.Stats.GetOrZero(CharacterStat.Level),
                PlayfieldId = playfieldId,
                Position = new WorldPosition(at.xf, at.yf, at.zf),
                IsDead = npc.IsDead,
                HasKnubotScript = knubot != null && !npc.IsDead && knubot.TryResolve(npc, out _),
                SpawnedAt = npc.SpawnedAt,
                Hostile = npc.Attackable
                          && Ai.NpcAiRules.IsProximityHostile(npc.Stats.GetOrZero(CharacterStat.BreedHostility))
            };
        }

        readonly record struct StatKey(int Instance, int StatId);

        sealed record Snapshot(
            long BuiltAt,
            PlayerInfo[] Players,
            NpcInfo[] Npcs,
            Dictionary<StatKey, int?> Stats,
            Dictionary<int, CooldownInfo[]> Cooldowns)
        {
            public Dictionary<int, PlayerInfo> PlayersById { get; } = Index(Players, x => x.CharacterId);

            public Dictionary<int, NpcInfo> NpcsById { get; } = Index(Npcs, x => x.Instance);

            static Dictionary<int, T> Index<T>(T[] items, Func<T, int> key)
            {
                var index = new Dictionary<int, T>(items.Length);
                foreach (T item in items)
                    index[key(item)] = item;
                return index;
            }
        }

        sealed class Slot
        {
            public long DemandUntil;
            public readonly ConcurrentDictionary<StatKey, long> Watch = new();

            /// <summary>Players whose locks a module read lately (module API 1.6), until when.</summary>
            public readonly ConcurrentDictionary<int, long> CooldownWatch = new();
            volatile Snapshot? _current;
            TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Snapshot? Current
            {
                get => _current;
                set => _current = value;
            }

            /// <summary>Until when another playfield's tick reads this one, so it publishes on every tick.</summary>
            public long EveryTickUntil;

            int _requested;

            /// <summary>Completes at the next publish.</summary>
            public Task NextPublish => Volatile.Read(ref _next).Task;

            /// <summary>
            /// Asks the next tick to publish and returns that publish. The task is taken before the request is raised, so
            /// the publish that consumes the request always completes it.
            /// </summary>
            public Task RequestPublish()
            {
                Task next = NextPublish;
                Volatile.Write(ref _requested, 1);
                return next;
            }

            /// <summary>On the owning tick: whether a module thread asked for a publish since the last one.</summary>
            public bool TakeRequest() => Interlocked.Exchange(ref _requested, 0) != 0;

            public void DemandEveryTick(long until) => Raise(ref EveryTickUntil, until);

            public void Demand(long until) => Raise(ref DemandUntil, until);

            static void Raise(ref long field, long until)
            {
                long current = Volatile.Read(ref field);
                while (until > current)
                {
                    long seen = Interlocked.CompareExchange(ref field, until, current);
                    if (seen == current)
                        return;
                    current = seen;
                }
            }

            public void Publish(Snapshot snapshot)
            {
                _current = snapshot;
                Interlocked.Exchange(ref _next, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
                    .TrySetResult();
            }
        }
    }
}
