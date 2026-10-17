namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using GroveGames.BehaviourTree.Collections;
    using GroveGames.BehaviourTree.Nodes;

    using AORebirth.World.Pathfinding;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    public sealed class NpcBrain
    {
        readonly GroveGames.BehaviourTree.BehaviourTree _tree;
        readonly NpcNanoCaster _nanos;
        readonly List<Vector3> _pathScratch = new(2);
        readonly List<Vector3> _routeScratch = new(8);
        readonly List<System.Numerics.Vector3> _reachPathScratch = new(8);
        readonly List<System.Numerics.Vector3> _planScratch = new(8);
        Identity _currentTarget = Identity.None;
        bool _leashing;
        bool _treeResetPending;
        bool _followAnnounced;
        Vector3 _segmentDestination = new(0, 0, 0);
        Vector3 _segmentEnd = new(0, 0, 0);
        DateTime _segmentPlannedUtc;
        bool _segmentReachesDestination;
        Vector3 _progressAnchor = new(0, 0, 0);
        Identity _reachCacheId = Identity.None;
        Vector3 _reachCacheStart = new(0, 0, 0);
        Vector3 _reachCachePos = new(0, 0, 0);
        DateTime _reachCacheUtc;
        bool _reachCacheResult;

        // Each cached reach answer lives for its own jittered time and target movement (mean unchanged), so a pack
        // that aggroed on the same tick spreads its re-checks over many ticks instead of searching in one burst.
        double _reachCacheSeconds = NpcFollowTarget.PathReplanSeconds;
        float _reachCacheMeters = NpcFollowTarget.MinAnnounceDeltaMeters;
        const double ReachCacheJitter = 0.25;
        DateTime _lastChanceUtc;
        int _stuckWarps;
        bool _evading;

        /// <summary>Set when a hate target's reach map was too large; the next leash check resets the NPC.</summary>
        bool _resetForUnreachable;
        bool _returningHome;
        bool _returnFailed;

        NpcBrain(NpcCharacter npc, Vector3? home, NpcAiProfile profile)
        {
            Npc = npc;
            Home = home;
            Hate = new HateList();
            PatrolEnabled = profile.PatrolEnabled;
            PatrolWaypoints = Array.Empty<Vector3>();

            var blackboard = new Blackboard();
            _tree = new NpcBehaviourTree(new BehaviourRoot(blackboard), this, profile);
            _tree.SetupTree();
            _tree.Enable();
            _nanos = new NpcNanoCaster(this);
            _isEngageable = IsEngageable;
            _thinkElapsed = ThinkPhase(npc);
            if (npc.Playfield?.GetService<GameData.IGameData>()?.RootPath is string root && root.Length > 0)
                _petSummons = NpcPetSummons.TryCreate(npc, root);
        }

        /// <summary>Combat pets from GameData MonsterCombatRules.json; null for NPCs without a pet rule.</summary>
        readonly NpcPetSummons? _petSummons;

        /// <summary>A summoned pet's brain: the pet tree instead of the roaming-NPC one, and no home to leash to.</summary>
        NpcBrain(NpcCharacter npc, Pets.PetController pet)
        {
            Npc = npc;
            Home = null;
            Hate = new HateList();
            PatrolWaypoints = Array.Empty<Vector3>();
            Pet = pet;

            var blackboard = new Blackboard();
            _tree = new PetBehaviourTree(new BehaviourRoot(blackboard), this);
            _tree.SetupTree();
            _tree.Enable();
            _nanos = new NpcNanoCaster(this);
            _isEngageable = IsEngageable;
            _thinkElapsed = ThinkPhase(npc);
        }

        /// <summary>The engage test, created once: passing the method group allocated a delegate on every call.</summary>
        readonly Func<Identity, bool> _isEngageable;

        /// <summary>Time gathered since the last think; the brain thinks once it reaches the think interval.</summary>
        double _thinkElapsed;

        /// <summary>Counts thinks; engage answers are kept for the think that computed them.</summary>
        long _thinkSerial;

        long _engageSerial = -1;
        readonly Dictionary<ulong, bool> _engageByTarget = new();
        readonly List<Player> _nearbyPlayers = new();

        /// <summary>A start offset spread over the interval, so brains that spawn together do not think in the same tick.</summary>
        static double ThinkPhase(NpcCharacter npc)
            => (npc.Identity.Instance % 997) / 997.0 * NpcAiRules.BrainThinkSeconds;

        /// <summary>The pet state when this is a summoned pet's brain.</summary>
        public Pets.PetController? Pet { get; }

        public static NpcBrain CreatePet(NpcCharacter npc, Pets.PetController pet)
        {
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(pet);
            var brain = new NpcBrain(npc, pet);
            npc.AttachBrain(brain);
            return brain;
        }

        public NpcCharacter Npc { get; }

        public Vector3? Home { get; private set; }

        public bool HasHome => Home is not null;

        // Wandering (district spawns): strolls at walking pace to random navmesh points within the radius of Home,
        // pausing between them. Combat, leash and evade always run.
        float _wanderRadius;
        double _wanderMinPauseSeconds;
        double _wanderMaxPauseSeconds;
        Vector3? _wanderDestination;
        DateTime _wanderPauseUntilUtc;
        bool _wanderedThisThink;

        public bool WanderEnabled => _wanderRadius > 0f && HasHome && Pet == null && !Npc.IsImmobile;

        /// <summary>
        /// Makes this NPC wander within <paramref name="radius"/> of <paramref name="anchor"/>, which also becomes its
        /// home for leashing. The first stroll starts after a random pause so a freshly spawned group does not set off
        /// together.
        /// </summary>
        public void EnableWander(Vector3 anchor, float radius, double minPauseSeconds, double maxPauseSeconds)
        {
            if (Pet != null || radius <= 0f || Npc.IsImmobile)
                return;

            Home = new Vector3(anchor.x, anchor.y, anchor.z);
            _wanderRadius = radius;
            _wanderMinPauseSeconds = Math.Max(0, minPauseSeconds);
            _wanderMaxPauseSeconds = Math.Max(_wanderMinPauseSeconds, maxPauseSeconds);
            _wanderDestination = null;
            _wanderPauseUntilUtc = DateTime.UtcNow.AddSeconds(NextWanderPause());
        }

        double NextWanderPause()
            => _wanderMinPauseSeconds + Random.Shared.NextDouble() * (_wanderMaxPauseSeconds - _wanderMinPauseSeconds);

        /// <summary>
        /// One wander think: stand out the pause, then walk to a random reachable point near home, then pause again.
        /// Always running: wandering is the NPC's idle activity.
        /// </summary>
        internal void TickWander()
        {
            _wanderedThisThink = true;
            DateTime now = DateTime.UtcNow;
            if (_wanderDestination is not Vector3 destination)
            {
                if (now < _wanderPauseUntilUtc)
                    return;
                if (!TryPickWanderPoint(out destination))
                {
                    _wanderPauseUntilUtc = now.AddSeconds(NextWanderPause());
                    return;
                }

                _wanderDestination = destination;
            }

            if (NpcAiRules.IsNearby(Npc.Position, destination, NpcAiRules.ArriveHomeMeters)
                || (!Npc.Motor.HasPath && _followAnnounced && _segmentReachesDestination && HasReachedSegmentEnd()))
            {
                EndStroll(now);
                return;
            }

            if (_patrolRun)
            {
                if (Npc.Motor.State == Movement.MovementState.Walk)
                    Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToRun);
            }
            else if (Npc.Motor.State != Movement.MovementState.Walk)
            {
                Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToWalk);
            }
            PathTo(destination);
        }

        // Patrol (a captured waypoint graph): wandering's walk and pause, but each stroll goes to the next waypoint,
        // picked by how often live NPCs walked each edge from the current one.
        Mobs.NpcPatrol? _patrol;
        HashSet<int>? _patrolNodes;
        int _patrolAt = -1;
        bool _patrolRun;

        /// <summary>Waypoints this far from home are left out, so a patrol stays inside the 70 m leash.</summary>
        const float PatrolRadius = 60f;

        public bool HasPatrol => _patrol != null;

        /// <summary>
        /// Walks the part of <paramref name="patrol"/> within <see cref="PatrolRadius"/> of home, starting from the
        /// waypoint nearest the NPC. Needs a home and at least two waypoints there; otherwise nothing changes.
        /// </summary>
        public void EnablePatrol(Mobs.NpcPatrol patrol)
        {
            ArgumentNullException.ThrowIfNull(patrol);
            if (Pet != null || Npc.IsImmobile || Home is not Vector3 home)
                return;

            var near = new HashSet<int>();
            int nearest = -1;
            double nearestDistance = double.MaxValue;
            for (int i = 0; i < patrol.Nodes.Length; i++)
            {
                float[] node = patrol.Nodes[i];
                if (node is not { Length: 3 })
                    continue;
                var point = new Vector3(node[0], node[1], node[2]);
                if (Vector3.Abs(point - home) > PatrolRadius)
                    continue;
                near.Add(i);
                double distance = Vector3.Abs(point - Npc.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }
            if (near.Count < 2)
                return;

            _patrol = patrol;
            _patrolNodes = near;
            _patrolAt = nearest;
            _patrolRun = patrol.Run;
            _wanderRadius = PatrolRadius;
            _wanderMinPauseSeconds = Math.Max(0, patrol.MinPauseSeconds);
            _wanderMaxPauseSeconds = Math.Max(_wanderMinPauseSeconds, patrol.MaxPauseSeconds);
            _wanderDestination = null;
            _wanderPauseUntilUtc = DateTime.UtcNow.AddSeconds(NextWanderPause());
        }

        bool TryPickPatrolPoint(out Vector3 destination)
        {
            destination = default!;
            if (_patrol == null || _patrolNodes == null)
                return false;

            int total = 0;
            foreach (int[] edge in _patrol.Edges)
                if (edge is { Length: 3 } && edge[0] == _patrolAt && edge[1] != _patrolAt && _patrolNodes.Contains(edge[1]))
                    total += Math.Max(1, edge[2]);

            int next = -1;
            if (total > 0)
            {
                int roll = Random.Shared.Next(total);
                foreach (int[] edge in _patrol.Edges)
                {
                    if (edge is not { Length: 3 } || edge[0] != _patrolAt || edge[1] == _patrolAt || !_patrolNodes.Contains(edge[1]))
                        continue;
                    roll -= Math.Max(1, edge[2]);
                    if (roll < 0)
                    {
                        next = edge[1];
                        break;
                    }
                }
            }
            else
            {
                // A dead end (live left the area there): go on from any other waypoint nearby.
                int[] others = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(_patrolNodes, i => i != _patrolAt));
                next = others[Random.Shared.Next(others.Length)];
            }

            float[] node = _patrol.Nodes[next];
            _patrolAt = next;
            destination = new Vector3(node[0], node[1], node[2]);
            return true;
        }

        void EndStroll(DateTime now)
        {
            _wanderDestination = null;
            _wanderPauseUntilUtc = now.AddSeconds(NextWanderPause());
            StopPathing();
        }

        bool TryPickWanderPoint(out Vector3 destination)
        {
            if (_patrol != null)
                return TryPickPatrolPoint(out destination);

            destination = default!;
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder == null || Home is not Vector3 home)
                return false;

            var centre = new System.Numerics.Vector3((float)home.x, (float)home.y, (float)home.z);
            if (!finder.TryFindRandomPointWithin(centre, _wanderRadius, Random.Shared, out System.Numerics.Vector3 point))
                return false;

            destination = new Vector3(point.X, point.Y, point.Z);
            return true;
        }

        /// <summary>
        /// Anything but a stroll moves at running pace: drops the stroll in progress and leaves walk. Called before the
        /// tree when the NPC is busy (hate, leash, evade) and after it when another branch took over.
        /// </summary>
        void StopWandering()
        {
            if (!WanderEnabled)
                return;

            if (_wanderDestination is not null)
            {
                _wanderDestination = null;
                _wanderPauseUntilUtc = DateTime.UtcNow.AddSeconds(NextWanderPause());
            }

            if (Npc.Motor.State == Movement.MovementState.Walk)
                Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToRun);
        }

        public HateList Hate { get; }

        public bool PatrolEnabled { get; set; }

        public IReadOnlyList<Vector3> PatrolWaypoints { get; set; }

        /// <summary>When the pathing NPC last made <see cref="NpcFollowTarget.PathStuckProgressMeters"/> of progress.</summary>
        internal DateTime ProgressSinceUtc { get; set; }

        public bool IsBusy => !Hate.IsEmpty || _leashing || Npc.FightingTarget.Instance != 0;

        /// <summary>
        /// Leashing home. Latched from the moment the leash starts until the reset at home: hate is
        /// cleared and HP restored up front, attacks miss, hostile nanos do not land and no threat is taken.
        /// </summary>
        public bool IsEvading => _evading;

        /// <summary>Diagnostics (debug MCP get_npc_brain): the brain committed to leashing home on its last think.</summary>
        public bool IsLeashing => _leashing;

        /// <summary>Diagnostics: the target the combat branch last selected.</summary>
        public Identity CurrentTarget => _currentTarget;

        /// <summary>Diagnostics: when the brain last thought; stale means its cell is not ticking.</summary>
        public DateTime LastThinkUtc { get; private set; }

        public static NpcBrain Create(NpcCharacter npc, Vector3? home = null, NpcAiProfile? profile = null)
        {
            ArgumentNullException.ThrowIfNull(npc);
            var brain = new NpcBrain(npc, home, profile ?? NpcAiProfiles.Default);
            npc.AttachBrain(brain);
            return brain;
        }

        public void Tick(double deltaTime)
        {
            if (Npc.IsDead)
                return;

            // Think on the brain's own cadence with the time gathered since its last think; movement and combat
            // swings keep running every tick in the character.
            _thinkElapsed += deltaTime;
            if (_thinkElapsed < NpcAiRules.BrainThinkSeconds)
                return;
            deltaTime = _thinkElapsed;
            _thinkElapsed = 0;
            _thinkSerial++;
            LastThinkUtc = DateTime.UtcNow;

            if (_treeResetPending)
            {
                _treeResetPending = false;
                _tree.Reset();
            }

            // Pacified: no aggression. Drop whatever hate or fight got in and stand down.
            if (Npc.IsPacified && (!Hate.IsEmpty || Npc.FightingTarget.Instance != 0))
            {
                ClearHate();
                StopFighting();
                StopPathing();
            }

            TickStallWatch.Stage("brain.scan", Npc.Identity.Instance);
            // A pet picks its fights from its owner, never from who happens to walk by.
            if (Pet == null)
                ScanProximity();

            // Before the casting check: a cast roots the NPC but does not hold back its pets.
            _petSummons?.Tick(Npc.FightingTarget.Instance != 0 && !_evading, DateTime.UtcNow);

            // Channelling a cast roots the caster (pets too): no chasing or following until the cast ends.
            if (Npc.IsCastingNano)
            {
                StopPathing();
                return;
            }

            // A wanderer drops its stroll and runs as soon as it has anything to fight or flee.
            _wanderedThisThink = false;
            if (!Hate.IsEmpty || _leashing || _evading)
                StopWandering();

            TickStallWatch.Stage("brain.tree", Npc.Identity.Instance);
            _tree.Tick((float)deltaTime);
            if (!_wanderedThisThink)
                StopWandering();
            TickStallWatch.Stage("brain.nanos", Npc.Identity.Instance);
            _nanos.Tick(DateTime.UtcNow);

            // A cast begun this think (a pet heal in the tree, or the nano caster) stops it where it stands.
            if (Npc.IsCastingNano)
                StopPathing();
        }

        public void AddThreat(Identity identity, float amount)
        {
            // Pacified NPCs keep no hate.
            if (_evading || Npc.IsPacified)
                return;
            bool newAttacker = !Hate.Contains(identity);
            Hate.Add(identity, amount);
            if (newAttacker)
                CallForHelp(identity);
        }

        /// <summary>
        /// Threat from <paramref name="source"/>'s own act. A player's pet also puts its owner on the list (Gameplay.json
        /// pets): the owner earns the configured share of the pet's threat, and at least the proximity minimum the first
        /// time, so the owner is the NPC's next target once the pet is dead, dismissed or out of reach.
        /// </summary>
        public void AddThreatFrom(Character source, float amount)
        {
            ArgumentNullException.ThrowIfNull(source);
            AddThreat(source.Identity, amount);

            if (source is not NpcCharacter { PetOwner: Player owner } || owner.IsDead
                || Npc.Playfield?.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.Pets is not { } rules)
                return;

            float share = amount * rules.OwnerThreatSharePercent / 100f;
            if (rules.OwnerAlwaysOnHateList && !Hate.Contains(owner.Identity))
                share = Math.Max(share, NpcAiRules.ProximityHate);
            if (share > 0f)
                AddThreat(owner.Identity, share);
        }

        /// <summary>
        /// Social aggro (Gameplay.json socialAggro): NPCs near this one that are of its kind join the fight against
        /// <paramref name="attacker"/>, nearest first. Runs once per new attacker.
        /// </summary>
        void CallForHelp(Identity attacker)
        {
            if (Pet != null || !Npc.Attackable || Npc.Playfield is not ZoneEngine_New.Core.Playfield.Playfield playfield)
                return;
            if (playfield.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.SocialAggro is not { Enabled: true, SearchRadius: > 0 } rules)
                return;
            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(attacker, out Dynel? found)
                || found is not Character enemy || enemy.IsDead)
                return;

            int side = Npc.Stats.GetOrZero(CharacterStat.Side);
            int family = Npc.Stats.GetOrZero(CharacterStat.NPCFamily);
            playfield.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>().CollectNearby(Npc, rules.SearchRadius, _nearbyNpcs);
            _helpers.Clear();
            for (int i = 0; i < _nearbyNpcs.Count; i++)
            {
                NpcCharacter other = _nearbyNpcs[i];
                if (ReferenceEquals(other, Npc) || other.IsDead || !other.Attackable || other.Brain is not NpcBrain brain
                    || brain.Pet != null || brain._evading || other.IsPacified || brain.Hate.Contains(attacker))
                    continue;
                if (other.Stats.GetOrZero(CharacterStat.Side) != side
                    || (rules.SameFamilyOnly && other.Stats.GetOrZero(CharacterStat.NPCFamily) != family))
                    continue;

                double distance = Npc.GetEdgeDistanceTo(other);
                if (distance > rules.RadiusFor(brain.SpawnAssistanceRadius()) || !ZoneEngine_New.Core.Helpers.CombatRules.CanAttack(other, enemy))
                    continue;
                if (rules.RequireLineOfSight && !other.HasLineOfSightTo(Npc))
                    continue;

                _helpers.Add((distance, brain));
            }

            _nearbyNpcs.Clear();
            _helpers.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            int count = rules.MaxHelpers > 0 ? Math.Min(rules.MaxHelpers, _helpers.Count) : _helpers.Count;
            for (int i = 0; i < count; i++)
                _helpers[i].Brain.JoinFight(attacker, rules.Chain);
            _helpers.Clear();
        }

        /// <summary>
        /// This NPC's spawn point AssistanceRadius (Playfields/{id}/Spawns.json), 0 without one. Looked up on first use,
        /// once the spawn system has bound the NPC to its point; the point never changes for this NPC.
        /// </summary>
        int SpawnAssistanceRadius()
        {
            if (_spawnAssistanceRadius is int known)
                return known;

            int radius = Npc.Playfield?.GetService<HashSpawnSystem>() is HashSpawnSystem spawns
                && spawns.TryGetSpawnPoint(Npc, out HashSpawnPoint point)
                    ? System.Math.Max(0, point.Source.AssistanceRadius)
                    : 0;
            _spawnAssistanceRadius = radius;
            return radius;
        }

        int? _spawnAssistanceRadius;

        /// <summary>Answers a call for help: <paramref name="attacker"/> goes on the hate list with the proximity threat.</summary>
        void JoinFight(Identity attacker, bool chain)
        {
            if (_evading || Npc.IsPacified || Hate.Contains(attacker))
                return;

            Hate.Add(attacker, NpcAiRules.ProximityHate);
            if (chain)
                CallForHelp(attacker);
        }

        readonly List<NpcCharacter> _nearbyNpcs = new();

        readonly List<(double Distance, NpcBrain Brain)> _helpers = new();

        public void ClearHate()
        {
            Hate.Clear();
            _currentTarget = Identity.None;
            _lastChanceUtc = default;
        }

        public void OnOwnerDied()
        {
            _tree.Disable();
            _leashing = false;
            StopPathing();
            ClearHate();
        }

        public void ScanProximity()
        {
            if (!Npc.Attackable || !Npc.AttacksOnSight)
                return;
            if (!NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat)))
                return;
            if (Npc.Playfield == null)
                return;

            // Only this NPC's cell and the cells touching it (wider only for an aggro range longer than a cell).
            var locality = Npc.Playfield.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>();
            float range = NpcAiRules.ProximityAggroRangeFor(Npc.Stats, locality.Grid.IsOutdoor);
            locality.CollectNearby(Npc, range, _nearbyPlayers);
            for (int i = 0; i < _nearbyPlayers.Count; i++)
                TryProximityAggro(_nearbyPlayers[i]);
            _nearbyPlayers.Clear();
        }

        /// <summary>
        /// Adds 1 hate when BreedHostility &gt; 0 and the player is nearby, in line of sight and not already listed.
        /// </summary>
        public void TryProximityAggro(Character player)
        {
            if (Npc.AttacksOnSight)
                TryNotice(player);
        }

        /// <summary>
        /// Something drew this NPC's attention to <paramref name="player"/> (a guard's strongbox being picked): it goes
        /// for them by the same rules as proximity aggro, even when it never attacks on sight.
        /// </summary>
        public void TryAlertAggro(Character player) => TryNotice(player);

        void TryNotice(Character player)
        {
            if (player == null || !player.IsPlayer || player.IsDead)
                return;
            if (!Npc.Attackable || _evading || Npc.IsPacified)
                return;
            if (!NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat)))
                return;
            if (Hate.Contains(player.Identity))
                return;
            if (!NpcAiRules.SideAllowsProximityAggro(
                    Npc.Stats.GetOrZero(CharacterStat.Side),
                    player.Stats.GetOrZero(CharacterStat.Side)))
                return;
            // A "gray" NPC (below the player's LevelEligibility.json range, the same rule that makes its kill worth
            // 1 XP) never starts a fight. It still fights back when attacked.
            if (Teams.TeamLevelEligibility.Current.IsTooLowForMember(
                    player.Stats.GetOrOne(CharacterStat.Level),
                    Npc.Stats.GetOrOne(CharacterStat.Level)))
                return;
            bool outdoor = Npc.Playfield?.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>().Grid.IsOutdoor ?? true;
            float range = NpcAiRules.ProximityAggroRangeFor(Npc.Stats, outdoor);
            if (Npc.GetEdgeDistanceTo(player) > range)
                return;
            // Unprovoked aggro needs sight. Once the player is on the hate list, chasing around walls is fine.
            if (!Npc.HasLineOfSightTo(player))
                return;
            if (!HasChance(player))
                return;

            Hate.Add(player.Identity, NpcAiRules.ProximityHate);
            CallForHelp(player.Identity);
        }

        public bool ShouldLeash()
        {
            TickStallWatch.Stage("brain.leash", Npc.Identity.Instance);
            // A target whose reach map was too costly to build means this fight cannot be judged: reset now rather
            // than search again (and stop players standing out of reach from farming a stuck mob).
            bool leash = _evading || _resetForUnreachable || NpcAiRules.ShouldLeash(Hate, Home, Npc.Position, _isEngageable);
            _resetForUnreachable = false;
            if (leash)
                BeginEvade();
            _leashing = leash;
            return leash;
        }

        /// <summary>
        /// Commits to the leash. Clearing hate here, not on arrival, stops the leash condition flipping back
        /// to combat on the way home; healing and stripping what others cast on it means nothing done to the
        /// NPC before or during the return sticks, so running it home cannot be used to whittle it down.
        /// Buffs the NPC or its own items put on itself stay.
        /// </summary>
        void BeginEvade()
        {
            if (_evading)
                return;

            _evading = true;
            ClearHate();
            StopFighting();
            StopPathing();
            Nanos.NanoRuntime.StripForeignBuffs(Npc);
            Npc.OnReset();
        }

        public bool HasNearbyHate()
        {
            TickStallWatch.Stage("brain.hate", Npc.Identity.Instance);
            return NpcAiRules.TryHighestNearby(Hate, _isEngageable, out _, out _);
        }

        public bool TrySelectHighestThreat()
        {
            TickStallWatch.Stage("brain.select", Npc.Identity.Instance);
            if (!NpcAiRules.TryHighestNearby(Hate, _isEngageable, out Identity identity, out _))
            {
                _currentTarget = Identity.None;
                return false;
            }

            _currentTarget = identity;
            return true;
        }

        public Character? ResolveCurrentTarget() => Resolve(_currentTarget);

        public bool HasArrivedHome()
            => !HasHome || NpcAiRules.IsNearby(Npc.Position, Home, NpcAiRules.ArriveHomeMeters);

        public bool HasChance(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (CanAttackNow(target))
                return true;
            // No LOS is fine while the chase path still has travel left.
            // Give up only with no path, or already on the last point.
            if (HasUnfinishedPath())
                return true;
            return CanPathTo(target);
        }

        public bool HasChanceWithGrace(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (HasChance(target))
            {
                _lastChanceUtc = DateTime.UtcNow;
                return true;
            }

            return NpcAiRules.IsWithinNoChanceGrace(_lastChanceUtc, DateTime.UtcNow);
        }

        public bool HasUnfinishedPath()
        {
            if (!Npc.Motor.HasPath)
                return false;

            MsgVector3[] remaining = Npc.Motor.CopyRemainingWaypoints();
            if (remaining.Length == 0)
                return false;

            MsgVector3 last = remaining[remaining.Length - 1];
            return !PathEndsUnderNpc(
                new System.Numerics.Vector3((float)Npc.Position.x, (float)Npc.Position.y, (float)Npc.Position.z),
                new System.Numerics.Vector3(last.X, last.Y, last.Z));
        }

        /// <summary>A factor in [1 - jitter, 1 + jitter], mean 1.</summary>
        static double Jitter() => 1.0 + ReachCacheJitter * (2.0 * Random.Shared.NextDouble() - 1.0);

        public bool CanPathTo(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            // An immobile NPC only ever has the targets it can hit from where it stands (HasChance: CanAttackNow).
            if (Npc.IsImmobile)
                return false;
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder == null)
                return true;

            DateTime now = DateTime.UtcNow;
            Vector3 cachedEnd = HeightfieldOrSelf(target.Position);
            if (_reachCacheId == target.Identity
                && (now - _reachCacheUtc).TotalSeconds < _reachCacheSeconds
                && Vector3.Abs(cachedEnd - _reachCachePos) < _reachCacheMeters)
            {
                PathDiag.CacheHit(PathDiag.Reach);
                return _reachCacheResult;
            }

            Vector3 startPos = HeightfieldOrSelf(Npc.Position);
            Vector3 endPos = cachedEnd;
            var start = new System.Numerics.Vector3((float)startPos.x, (float)startPos.y, (float)startPos.z);
            var end = new System.Numerics.Vector3((float)endPos.x, (float)endPos.y, (float)endPos.z);

            // One shared map per target answers every NPC asking about it (no per-NPC search). Off-mesh starts stay
            // movable so holes and future off-mesh links are not a wall. A route that ends under the NPC is not a
            // chase chance once the target is also out of attack range: they cannot get closer. In-range fight-back
            // is decided by HasChance before this runs.
            ReachMap map = Npc.Playfield!.GetRequiredService<ReachMaps>().Get(target, finder, endPos);
            ReachAnswer answer = finder.Reaches(map, start);
            if (answer == ReachAnswer.Reachable && map.Kind == ReachMapKind.Flooded && PathEndsUnderNpc(start, map.EndPoint))
                answer = ReachAnswer.Unreachable;
            if (answer == ReachAnswer.TooLarge && (Hate.Contains(target.Identity) || Npc.FightingTarget == target.Identity))
                _resetForUnreachable = true;
            bool reachable = answer == ReachAnswer.Reachable;
            _reachCacheId = target.Identity;
            _reachCacheStart = new Vector3(startPos.x, startPos.y, startPos.z);
            _reachCachePos = new Vector3(endPos.x, endPos.y, endPos.z);
            _reachCacheUtc = now;
            _reachCacheResult = reachable;
            _reachCacheSeconds = NpcFollowTarget.PathReplanSeconds * Jitter();
            _reachCacheMeters = (float)(NpcFollowTarget.MinAnnounceDeltaMeters * Jitter());
            return reachable;
        }

        public bool IsInAttackRange(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            // Match the swing check: a weapon only swings inside its own range, so stopping the
            // chase inside a longer range leaves a short-reach NPC standing out of reach.
            double edge = Npc.GetEdgeDistanceTo(target);
            bool armed = false;
            foreach (CharacterWeapon? weapon in Npc.Weapons.Values)
            {
                if (weapon == null)
                    continue;
                armed = true;
                if (edge <= weapon.GetAttackRange())
                    return true;
            }

            return !armed && edge <= CharacterWeapon.DefaultMeleeAttackRange;
        }

        public bool CanAttackNow(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return IsInAttackRange(target) && Npc.HasLineOfSightTo(target);
        }

        /// <summary>
        /// Keeps a segment of <see cref="NpcFollowTarget.PathLookaheadSeconds"/> of travel ahead of the NPC
        /// along the route to <paramref name="destination"/>. A running segment is replaced once
        /// <see cref="NpcFollowTarget.PathReplanSeconds"/> of it has been travelled, so its end is never reached
        /// mid-route. The last segment, which ends at the destination, is only replaced when the destination
        /// moves. An NPC that stops making progress for
        /// <see cref="NpcFollowTarget.PathStuckWarpSeconds"/> is warped one replan interval along its path.
        /// </summary>
        public void PathTo(Vector3 destination) => PathTo(destination, null);

        /// <summary>
        /// <see cref="PathTo(Vector3)"/> for a destination that keeps moving (a pet following its owner): a path
        /// that already heads there is only replaced once the destination drifts <paramref name="replanDeltaMeters"/>
        /// from it, or its end is nearly reached, so the NPC neither re-announces its path constantly nor brakes
        /// at an end that is already stale.
        /// </summary>
        public void PathTo(Vector3 destination, float? replanDeltaMeters)
        {
            _replanDeltaMeters = replanDeltaMeters ?? NpcFollowTarget.MinAnnounceDeltaMeters;
            // Rooted, or a body that cannot move at all: settle where it stands; the stuck clock restarts once a root
            // is gone.
            if (Npc.IsRooted || Npc.IsImmobile)
            {
                StopPathing();
                return;
            }

            destination = HeightfieldOrSelf(destination);
            DateTime now = DateTime.UtcNow;
            bool active = Npc.Motor.HasPath;
            // The motor drops a segment the body stopped on (wall, arrive-halt) short of its end. Replanning
            // that from scratch every tick re-announces it every tick and never lets the stuck clock run.
            bool continuing = active || (_followAnnounced && !HasReachedSegmentEnd());
            if (continuing)
            {
                if (IsStuck(now))
                {
                    // Warping ahead did not help: the body cannot follow this route (e.g. navmesh over
                    // collision it cannot climb). Evade rather than stand there taking hits; if it is
                    // the walk home that is stuck, give up on walking and let the caller snap home.
                    if (_stuckWarps >= NpcAiRules.MaxStuckWarps)
                    {
                        // A stroll that cannot go on is just given up; nothing is wrong enough to evade.
                        if (_wanderDestination is not null)
                        {
                            _stuckWarps = 0;
                            EndStroll(now);
                            return;
                        }

                        // A pet that cannot walk on rejoins its owner (a player's pet) rather than evading.
                        if (Pet != null)
                        {
                            Pet.WarpRequested = true;
                            StopPathing();
                            _stuckWarps = 0;
                            return;
                        }

                        if (_returningHome)
                        {
                            _returnFailed = true;
                            StopPathing();
                        }
                        else
                        {
                            BeginEvade();
                        }

                        return;
                    }

                    WarpAlongPath(now);
                    _stuckWarps++;
                    active = false;
                }
                else if (active && !ShouldReplan(destination))
                    return;
                else if (!active && (now - _segmentPlannedUtc).TotalSeconds < NpcFollowTarget.PathReplanSeconds)
                    return;
            }

            PlanRoute(destination);
            Vector3 start = Npc.Position;
            Vector3 routeEnd = _routeScratch[_routeScratch.Count - 1];
            if (PathEndsUnderNpc(
                new System.Numerics.Vector3((float)start.x, (float)start.y, (float)start.z),
                new System.Numerics.Vector3((float)routeEnd.x, (float)routeEnd.y, (float)routeEnd.z)))
                return;

            bool truncated = TruncateByDistance(_routeScratch, SegmentLookaheadMeters(), _pathScratch);
            if (active)
                Npc.Motor.ReplacePath(_pathScratch);
            else
                Npc.Motor.SetPath(_pathScratch);
            if (!Npc.Motor.HasPath)
                return;

            if (!continuing)
                ResetProgress(now);
            Vector3 segmentEnd = _pathScratch[_pathScratch.Count - 1];
            _segmentEnd = new Vector3(segmentEnd.x, segmentEnd.y, segmentEnd.z);
            _segmentPlannedUtc = now;
            _segmentDestination = new Vector3(destination.x, destination.y, destination.z);
            _segmentReachesDestination = !truncated;
            NpcFollowTarget.AnnounceCoordinatePath(Npc, start, _pathScratch);
            _followAnnounced = true;
        }

        /// <summary>Clears motor path and settles observers with a FollowTarget stop.</summary>
        public void StopPathing()
        {
            bool had = Npc.Motor.HasPath || _followAnnounced;
            Npc.Motor.ClearPath();
            if (!had)
                return;

            NpcFollowTarget.AnnounceStop(Npc, Npc.Position);
            _followAnnounced = false;
            ProgressSinceUtc = default;
        }

        /// <summary>
        /// Copies the leading <paramref name="meters"/> of <paramref name="path"/> (flat XZ distance) into
        /// <paramref name="into"/>, cutting the crossing leg with an interpolated point. A path shorter
        /// than that, or a non-positive limit, is copied whole. Returns true when the path was cut short.
        /// </summary>
        internal static bool TruncateByDistance(IReadOnlyList<Vector3> path, float meters, List<Vector3> into)
        {
            into.Clear();
            if (path == null || path.Count == 0)
                return false;

            into.Add(new Vector3(path[0].x, path[0].y, path[0].z));
            float along = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 from = path[i - 1];
                Vector3 to = path[i];
                double dx = to.x - from.x;
                double dz = to.z - from.z;
                float leg = (float)Math.Sqrt((dx * dx) + (dz * dz));
                if (meters <= 0f || along + leg <= meters)
                {
                    into.Add(new Vector3(to.x, to.y, to.z));
                    along += leg;
                    continue;
                }

                double t = leg > 0f ? (meters - along) / leg : 0.0;
                into.Add(new Vector3(
                    from.x + ((to.x - from.x) * t),
                    from.y + ((to.y - from.y) * t),
                    from.z + ((to.z - from.z) * t)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Flat XZ distance from <paramref name="path"/>[0] to the first vertex where the route
        /// turns more than <paramref name="maxTurnDegrees"/> past <paramref name="afterDistance"/>.
        /// Positive infinity when no such turn remains.
        /// </summary>
        internal static float DistanceUntilTurn(IReadOnlyList<Vector3> path, float maxTurnDegrees, float afterDistance = 0f)
        {
            if (path == null || path.Count < 3)
                return float.PositiveInfinity;

            double cosLimit = Math.Cos(maxTurnDegrees * (Math.PI / 180.0));
            float along = 0f;
            for (int i = 1; i < path.Count - 1; i++)
            {
                double ax = path[i].x - path[i - 1].x;
                double az = path[i].z - path[i - 1].z;
                double aLen = Math.Sqrt((ax * ax) + (az * az));
                along += (float)aLen;
                double bx = path[i + 1].x - path[i].x;
                double bz = path[i + 1].z - path[i].z;
                double bLen = Math.Sqrt((bx * bx) + (bz * bz));
                if (aLen < 1e-4 || bLen < 1e-4)
                    continue;

                double dot = ((ax * bx) + (az * bz)) / (aLen * bLen);
                if (dot < cosLimit && along > afterDistance)
                    return along;
            }

            return float.PositiveInfinity;
        }

        /// <summary>
        /// Flat XZ distance from <paramref name="path"/>[0] to the point nearest (<paramref name="x"/>, <paramref name="z"/>)
        /// within the first <paramref name="maxAlong"/> meters of the route. The body is behind the guide, so a later
        /// leg that folds back closer, on the far side of a wall, is never used. Nor is a short leg behind the body
        /// that happens to lie within a couple of meters of it.
        /// </summary>
        internal static float DistanceAlongPath(IReadOnlyList<Vector3> path, double x, double z, float maxAlong)
        {
            if (path == null || path.Count < 2)
                return 0f;

            float along = 0f;
            float best = 0f;
            double bestMiss = double.PositiveInfinity;
            for (int i = 1; i < path.Count && along <= maxAlong; i++)
            {
                double ax = path[i - 1].x;
                double az = path[i - 1].z;
                double dx = path[i].x - ax;
                double dz = path[i].z - az;
                double len2 = (dx * dx) + (dz * dz);
                float leg = (float)Math.Sqrt(len2);
                double t = 0.0;
                if (len2 > 1e-8)
                {
                    t = (((x - ax) * dx) + ((z - az) * dz)) / len2;
                    double tMax = leg > 0f ? Math.Min(1.0, (maxAlong - along) / leg) : 0.0;
                    t = Math.Clamp(t, 0.0, Math.Max(0.0, tMax));
                }

                double ex = x - (ax + (dx * t));
                double ez = z - (az + (dz * t));
                double miss = (ex * ex) + (ez * ez);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    best = along + (float)(leg * t);
                }

                along += leg;
            }

            return best;
        }

        /// <summary>
        /// Holds <paramref name="guideDistance"/> at a sharp turn until the body is within
        /// <see cref="NpcFollowTarget.PathCornerReleaseMeters"/> of it.
        /// Releasing early lets the steer point run around the wall and pull the body back.
        /// Waiting for the body to pass it never ends: arrive steering slows the body as it nears the parked guide.
        /// </summary>
        internal static float ClampGuideDistance(float guideDistance, float bodyDistance, float cornerDistance)
        {
            if (float.IsInfinity(cornerDistance))
                return guideDistance;
            if (bodyDistance >= cornerDistance - NpcFollowTarget.PathCornerReleaseMeters)
                return guideDistance;
            return guideDistance > cornerDistance ? cornerDistance : guideDistance;
        }

        bool HasReachedSegmentEnd()
            => PathEndsUnderNpc(
                new System.Numerics.Vector3((float)Npc.Position.x, (float)Npc.Position.y, (float)Npc.Position.z),
                new System.Numerics.Vector3((float)_segmentEnd.x, (float)_segmentEnd.y, (float)_segmentEnd.z));

        /// <summary>The end is this close: a moving destination is re-planned before the NPC brakes into it.</summary>
        const double NearSegmentEndMeters = 2.5;

        /// <summary>…as long as the destination has moved at least this much.</summary>
        const double NearEndReplanMeters = 1.0;

        float _replanDeltaMeters = NpcFollowTarget.MinAnnounceDeltaMeters;

        bool ShouldReplan(Vector3 destination)
        {
            if (_segmentReachesDestination)
            {
                double drift = Vector3.Abs(destination - _segmentDestination);
                if (drift >= _replanDeltaMeters)
                    return true;
                return _replanDeltaMeters > NpcFollowTarget.MinAnnounceDeltaMeters
                       && drift >= NearEndReplanMeters
                       && Npc.Motor.RemainingPathMeters() <= NearSegmentEndMeters;
            }

            return Npc.Motor.RemainingPathMeters() <= ReplanRemainingMeters();
        }

        /// <summary>Clearance kept between the guide's brake zone and the segment end when the next segment is planned.</summary>
        const float SegmentEndMarginMeters = 1f;

        /// <summary>
        /// Remaining path at which the next segment replaces the current one: the travel time left after a replan
        /// interval, but never inside the guide's brake zone. Running (≈6 m/s) is time-bound (3 m); walking (≈1.5 m/s)
        /// would replan 0.75 m from the end, after the guide had already started braking the body each segment.
        /// </summary>
        float ReplanRemainingMeters()
        {
            float velocity = Npc.Motor.Vehicle.MaxVel;
            float timed = (float)(velocity * (NpcFollowTarget.PathLookaheadSeconds - NpcFollowTarget.PathReplanSeconds));
            return MathF.Max(timed, Npc.Motor.SlowingDistance + SegmentEndMarginMeters);
        }

        /// <summary>
        /// Length of each planned segment: the lookahead's travel time, and at least a replan interval of travel past the
        /// replan point, so a slow NPC still covers a full interval before the next update.
        /// </summary>
        float SegmentLookaheadMeters()
        {
            float velocity = Npc.Motor.Vehicle.MaxVel;
            float timed = (float)(velocity * NpcFollowTarget.PathLookaheadSeconds);
            return MathF.Max(timed, ReplanRemainingMeters() + (float)(velocity * NpcFollowTarget.PathReplanSeconds));
        }

        bool IsStuck(DateTime now)
        {
            if (Vector3.Abs(Npc.Position - _progressAnchor) >= NpcFollowTarget.PathStuckProgressMeters)
            {
                ResetProgress(now);
                _stuckWarps = 0;
                return false;
            }

            return (now - ProgressSinceUtc).TotalSeconds >= NpcFollowTarget.PathStuckWarpSeconds;
        }

        void ResetProgress(DateTime now)
        {
            Vector3 position = Npc.Position;
            _progressAnchor = new Vector3(position.x, position.y, position.z);
            ProgressSinceUtc = now;
        }

        /// <summary>
        /// Stuck: place the NPC one replan interval along its current path and settle observers there.
        /// </summary>
        void WarpAlongPath(DateTime now)
        {
            Vector3 position = Npc.Position;
            _routeScratch.Clear();
            _routeScratch.Add(new Vector3(position.x, position.y, position.z));
            MsgVector3[] remaining = Npc.Motor.CopyRemainingWaypoints();
            for (int i = 0; i < remaining.Length; i++)
                _routeScratch.Add(new Vector3(remaining[i].X, remaining[i].Y, remaining[i].Z));

            float meters = (float)(Npc.Motor.Vehicle.MaxVel * NpcFollowTarget.PathReplanSeconds);
            TruncateByDistance(_routeScratch, meters, _pathScratch);
            Vector3 last = _pathScratch[_pathScratch.Count - 1];
            var target = new Vector3(last.x, last.y, last.z);

            Npc.Motor.ClearPath();
            Npc.Motor.Warp(target);
            NpcFollowTarget.AnnounceStop(Npc, target);
            ResetProgress(now);
        }

        /// <summary>
        /// Full route from the NPC's position to <paramref name="destination"/> into <see cref="_routeScratch"/>,
        /// starting with the current position.
        /// Reuses the <see cref="CanPathTo"/> path when it was planned from here to the same place; with no
        /// navmesh, or no path found, the route is a straight line.
        /// </summary>
        void PlanRoute(Vector3 destination)
        {
            _routeScratch.Clear();
            Vector3 feet = Npc.Position;
            _routeScratch.Add(new Vector3(feet.x, feet.y, feet.z));
            Vector3 startPos = HeightfieldOrSelf(feet);
            if (_reachPathScratch.Count > 0
                && Vector3.Abs(destination - _reachCachePos) < NpcFollowTarget.MinAnnounceDeltaMeters
                && Vector3.Abs(startPos - _reachCacheStart) < MovementConfig.PathArrivalRadius)
            {
                PathDiag.CacheHit(PathDiag.Route);
                AppendRoute(_reachPathScratch);
                return;
            }

            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder != null)
            {
                var start = new System.Numerics.Vector3((float)startPos.x, (float)startPos.y, (float)startPos.z);
                var end = new System.Numerics.Vector3((float)destination.x, (float)destination.y, (float)destination.z);
                if (PathDiag.TryFindPath(finder, start, end, _planScratch, PathDiag.Route) && _planScratch.Count > 0)
                {
                    AppendRoute(_planScratch);
                    return;
                }
            }

            _routeScratch.Add(new Vector3(destination.x, destination.y, destination.z));
        }

        void AppendRoute(List<System.Numerics.Vector3> points)
        {
            for (int i = 0; i < points.Count; i++)
                _routeScratch.Add(new Vector3(points[i].X, points[i].Y, points[i].Z));
        }

        public void StopFighting() => NpcAiCombat.AnnounceStopFight(Npc);

        /// <summary>
        /// Runs home along the navmesh. False when the NPC should snap home with <see cref="WarpHome"/>
        /// instead: there is no route home from here, or the walk made no progress through
        /// <see cref="NpcAiRules.MaxStuckWarps"/> stuck-warps.
        /// </summary>
        public bool ReturnHome()
        {
            if (!HasHome || _returnFailed)
                return false;

            // Rooted on the way home: wait it out instead of snapping home.
            if (Npc.IsRooted)
            {
                StopPathing();
                return true;
            }

            if (!_returningHome)
            {
                if (!CanWalkTo(Home!))
                    return false;
                _returningHome = true;
            }

            PathTo(Home!);
            return !_returnFailed && Npc.Motor.HasPath;
        }

        /// <summary>Places the NPC on its home spot. The following reset heals and clears hate.</summary>
        public void WarpHome()
        {
            if (!HasHome)
                return;

            Vector3 home = Home!;
            Npc.Motor.ClearPath();
            Npc.Motor.Warp(home);
            NpcFollowTarget.AnnounceStop(Npc, home);
            _followAnnounced = false;
            ProgressSinceUtc = default;
        }

        /// <summary>A heal order: casts a heal on <paramref name="target"/> now if one can land.</summary>
        internal HealOrderResult TryHeal(Character target) => _nanos.TryHeal(target, DateTime.UtcNow);

        /// <summary>Makes <paramref name="target"/> the one the chase and attack work on (a pet's chosen enemy).</summary>
        internal void SetCurrentTarget(Identity target) => _currentTarget = target;

        /// <summary>Places the NPC at <paramref name="position"/> on the floor and settles observers there.</summary>
        internal void WarpTo(Vector3 position)
        {
            Vector3 at = HeightfieldOrSelf(position);
            Npc.Motor.ClearPath();
            Npc.Motor.Warp(at);
            NpcFollowTarget.AnnounceStop(Npc, at);
            _followAnnounced = false;
            ProgressSinceUtc = default;
            _stuckWarps = 0;
        }

        /// <summary>A complete navmesh route from here ends at <paramref name="destination"/>. No navmesh: true.</summary>
        internal bool CanWalkTo(Vector3 destination)
        {
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder == null)
                return true;

            Vector3 from = HeightfieldOrSelf(Npc.Position);
            Vector3 to = HeightfieldOrSelf(destination);
            var start = new System.Numerics.Vector3((float)from.x, (float)from.y, (float)from.z);
            var end = new System.Numerics.Vector3((float)to.x, (float)to.y, (float)to.z);
            if (!finder.TrySnap(start, out _) || !PathDiag.TryFindPath(finder, start, end, _planScratch, PathDiag.Walk) || _planScratch.Count == 0)
                return false;

            return System.Numerics.Vector3.Distance(_planScratch[_planScratch.Count - 1], end) <= NpcAiRules.ArriveHomeMeters;
        }

        public void ResetOutOfCombat()
        {
            TickStallWatch.Stage("brain.reset", Npc.Identity.Instance);
            _leashing = false;
            _evading = false;
            _resetForUnreachable = false;
            _stuckWarps = 0;
            _returningHome = false;
            _returnFailed = false;
            _reachCacheId = Identity.None;
            _reachCacheResult = false;
            ClearHate();
            StopPathing();
            Npc.OnReset();

            // Resetting the tree from inside Evaluate rewinds the running Sequence child index, so
            // the sequence re-enters this node for the rest of the tick. Clear state before the
            // next tick instead.
            _treeResetPending = true;
        }

        Vector3 HeightfieldOrSelf(Vector3 position)
        {
            Playfield? playfield = Npc.Playfield;
            if (playfield != null && playfield.TrySnapFeetToFloor(position, out Vector3 floor))
                return floor;
            return position;
        }

        Character? Resolve(Identity identity)
        {
            if (identity.Instance == 0 || Npc.Playfield == null)
                return null;

            DynelRegistry registry = Npc.Playfield.GetRequiredService<DynelRegistry>();
            if (registry.TryGet(identity, out Dynel? dynel) && dynel is Character target && !target.IsDead)
                return target;

            Hate.Remove(identity);
            if (_currentTarget == identity)
                _currentTarget = Identity.None;
            return null;
        }

        /// <summary>
        /// Leash, hate and target choice all ask this of every hate entry in the same think; the answer cannot change
        /// within it, so it is worked out once per entry per think (line of sight and reach included).
        /// </summary>
        bool IsEngageable(Identity identity)
        {
            if (_engageSerial != _thinkSerial)
            {
                _engageByTarget.Clear();
                _engageSerial = _thinkSerial;
            }

            // The current target gets the longer, sticky engage range, so its answer is kept apart.
            ulong key = identity.Long() ^ (identity == _currentTarget ? 1UL << 63 : 0UL);
            if (_engageByTarget.TryGetValue(key, out bool known))
                return known;

            bool engageable = ComputeEngageable(identity);
            _engageByTarget[key] = engageable;
            return engageable;
        }

        bool ComputeEngageable(Identity identity)
        {
            Character? target = Resolve(identity);
            return target != null
                && Npc.GetEdgeDistanceTo(target) <= NpcAiRules.EngageRange(identity == _currentTarget || OwnsCurrentPetTarget(identity))
                && HasChanceWithGrace(target);
        }

        /// <summary>
        /// <paramref name="identity"/> is the owner of the pet this NPC is fighting (Gameplay.json pets
        /// ownerSharesPetEngageRange): the owner is in the fight and gets the pet's engage range.
        /// </summary>
        bool OwnsCurrentPetTarget(Identity identity)
        {
            if (_currentTarget.Instance == 0 || identity == _currentTarget)
                return false;
            if (Npc.Playfield?.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.Pets is not { OwnerSharesPetEngageRange: true })
                return false;

            return Resolve(_currentTarget) is NpcCharacter { PetOwner: Player owner } && owner.Identity == identity;
        }

        internal static bool PathEndsUnderNpc(
            System.Numerics.Vector3 npc,
            IReadOnlyList<System.Numerics.Vector3> path)
        {
            if (path == null || path.Count == 0)
                return true;

            return PathEndsUnderNpc(npc, path[path.Count - 1]);
        }

        /// <summary>
        /// The route's last point is at the NPC's feet: close in XZ and on the same level. A target on the
        /// floor directly above or below is still reachable by the ramp or stairs the route takes.
        /// </summary>
        static bool PathEndsUnderNpc(System.Numerics.Vector3 npc, System.Numerics.Vector3 end)
        {
            float dx = end.X - npc.X;
            float dz = end.Z - npc.Z;
            float limit = NpcAiRules.PathEndGiveUpMeters;
            return (dx * dx) + (dz * dz) <= limit * limit
                && MathF.Abs(end.Y - npc.Y) <= NpcAiRules.PathEndGiveUpHeightMeters;
        }
    }
}
