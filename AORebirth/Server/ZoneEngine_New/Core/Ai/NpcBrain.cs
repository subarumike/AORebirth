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

    /// <summary>How a scripted walk ended, read once by the program's runner.</summary>
    public enum ScriptMoveResult
    {
        None = 0,
        Arrived = 1,
        Stuck = 2,
        Interrupted = 3
    }

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

        /// <summary>Whose chance <see cref="_lastChanceUtc"/> records: the grace belongs to that target alone.</summary>
        Identity _lastChanceTarget = Identity.None;
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

        /// <summary>
        /// Where the running program last sent this NPC. While set, the distance leash measures from here rather than
        /// from <see cref="Home"/>: a scripted march may legitimately go farther from the spawn than the leash range.
        /// Cleared when the program gives movement back or re-posts the NPC with <see cref="SetHome"/>.
        /// </summary>
        public Vector3? ScriptedAnchor => _scriptedAnchor;

        /// <summary>The patrol waypoint the NPC is walking to (PatrolNode), null when it is not patrolling.</summary>
        internal Vector3? PatrolAnchor { get; set; }

        /// <summary>
        /// Where the distance leash measures from: the point a program or patrol is walking the NPC to while that
        /// walk is under way, else <see cref="Home"/>. A route longer than the leash range is then not a leash.
        /// </summary>
        public Vector3? LeashAnchor => _scriptedAnchor ?? (PatrolEnabled ? PatrolAnchor : null) ?? Home;

        /// <summary>A program re-posts the NPC: leash, return and wander anchor at <paramref name="home"/> from now on.</summary>
        internal void SetHome(Vector3 home)
        {
            ArgumentNullException.ThrowIfNull(home);
            Home = new Vector3(home.x, home.y, home.z);
            _scriptedAnchor = null;
            _returningHome = false;
            _returnFailed = false;
        }

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

            if (Npc.Motor.State != Movement.MovementState.Walk)
                Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToWalk);
            PathTo(destination);
        }

        void EndStroll(DateTime now)
        {
            _wanderDestination = null;
            _wanderPauseUntilUtc = now.AddSeconds(NextWanderPause());
            StopPathing();
        }

        bool TryPickWanderPoint(out Vector3 destination)
        {
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

        // Scripted movement (NpcScripts): a program holds the movement lease while it walks the NPC somewhere. The
        // tree's scripted-move branch sits after leash and combat, so a fight or a leash takes the lease away; the
        // runner sees Interrupted and asks again once the NPC is free. The brain stays the only writer of the motor.
        Vector3? _scriptedDestination;
        bool _scriptedRun;
        bool _scriptedDirect;
        Vector3? _scriptedAnchor;
        ScriptMoveResult _scriptedResult;

        public bool ScriptHoldsMovement => _scriptedDestination is not null;

        /// <summary>The held scripted walk goes straight to its point instead of along the navmesh.</summary>
        public bool ScriptMovesDirect => _scriptedDirect && _scriptedDestination is not null;

        /// <summary>
        /// Walk (or run) to <paramref name="destination"/>; the result is read with <see cref="ConsumeScriptMoveResult"/>.
        /// <paramref name="direct"/> skips the navmesh and walks the straight line: a script's bridge across a gap
        /// the mesh does not cover (a ramp too steep to bake, between two islands).
        /// </summary>
        public void ScriptMoveTo(Vector3 destination, bool run, bool direct = false)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (Npc.IsImmobile || Pet != null)
            {
                _scriptedResult = ScriptMoveResult.Arrived;
                return;
            }

            // A fresh leg: whatever path the body last finished (an earlier leg, a chase) must not count as this one's
            // arrival, so the segment flags and any leftover motor path go first.
            StopPathing();
            // A shared script destination is spread per NPC so a group does not pile onto one point. A direct leg is
            // an exact off-mesh bridge (a ramp) and is walked as given.
            _scriptedDestination = direct
                ? new Vector3(destination.x, destination.y, destination.z)
                : NpcSpread.Destination(Npc, destination);
            _scriptedRun = run;
            _scriptedDirect = direct;
            _scriptedAnchor = _scriptedDestination;
            _scriptedResult = ScriptMoveResult.None;
        }

        /// <summary>The program gives movement back to patrol / wander / idle.</summary>
        public void ScriptReleaseMovement()
        {
            _scriptedAnchor = null;
            if (_scriptedDestination is null)
                return;

            _scriptedDestination = null;
            StopPathing();
            if (Npc.Motor.State == Movement.MovementState.Walk)
                Npc.Motor.ApplyAction(Movement.MovementAction.SwitchToRun);
        }

        public ScriptMoveResult ConsumeScriptMoveResult()
        {
            ScriptMoveResult result = _scriptedResult;
            _scriptedResult = ScriptMoveResult.None;
            return result;
        }

        /// <summary>One scripted-move think: arrive, or keep pathing at the requested pace.</summary>
        internal void TickScriptedMove()
        {
            if (_scriptedDestination is not Vector3 destination)
                return;

            if (NpcAiRules.IsNearby(Npc.Position, destination, NpcAiRules.ArriveHomeMeters)
                || (!Npc.Motor.HasPath && _followAnnounced && _segmentReachesDestination && HasReachedSegmentEnd()))
            {
                _scriptedDestination = null;
                _scriptedResult = ScriptMoveResult.Arrived;
                StopPathing();
                return;
            }

            if (_scriptedRun)
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

        /// <summary>Hate, leash or evade took over: the lease is gone; the runner re-requests the walk when free.</summary>
        void InterruptScriptedMovement()
        {
            if (_scriptedDestination is null)
                return;

            _scriptedDestination = null;
            _scriptedResult = ScriptMoveResult.Interrupted;
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

            // Charmed (CharmNpc): it only follows its leader until the charming nano ends.
            if (TickCharm())
                return;

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
            {
                StopWandering();
                InterruptScriptedMovement();
            }

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

        // CharmNpc: who this NPC follows, while the nano that charmed it runs on it.
        Identity _charmLeader;
        int _charmNanoId;
        Nanos.Buff? _charmBuff;

        /// <summary>
        /// CharmNpc: follows <paramref name="leader"/> like a pet for as long as nano <paramref name="nanoId"/> runs on
        /// this NPC (Call to Arms 297247 on an Omni-AF Private), then goes back to what it was doing.
        /// </summary>
        public void Charm(Character leader, int nanoId)
        {
            ArgumentNullException.ThrowIfNull(leader);
            _charmLeader = leader.Identity;
            _charmNanoId = nanoId;
            _charmBuff = null;
            foreach (Nanos.Buff buff in Npc.Buffs)
            {
                if (buff.Id == nanoId)
                    _charmBuff = buff;
            }
            ClearHate();
            StopFighting();
            StopWandering();
        }

        public bool IsCharmed => _charmNanoId != 0;

        bool TickCharm()
        {
            if (_charmNanoId == 0)
                return false;

            Character? leader = null;
            if (Npc.Playfield?.GetService<ZoneEngine_New.Core.Playfield.DynelRegistry>() is { } registry
                && registry.TryGet(_charmLeader, out Dynel? found) && found is Character character
                && !character.IsDead && ReferenceEquals(character.Playfield, Npc.Playfield))
                leader = character;

            // The copy that charmed it: a recast (CastNano2 at Henri Zure) replaces it and ends the charm.
            bool charmRuns = false;
            foreach (Nanos.Buff buff in Npc.Buffs)
            {
                if (buff.Id == _charmNanoId && (_charmBuff == null || ReferenceEquals(buff, _charmBuff)))
                {
                    charmRuns = true;
                    break;
                }
            }

            if (leader == null || !charmRuns)
            {
                // As a pet charm's end: what the charmer and their friends ran on it goes, except a recast copy.
                Nanos.NanoRuntime.ClearBuffsOnCharmEnd(Npc, charmRuns ? 0 : _charmNanoId);
                _charmNanoId = 0;
                _charmLeader = default;
                _charmBuff = null;
                StopPathing();
                _treeResetPending = true;
                return false;
            }

            double distance = Npc.Distance3D(leader);
            if (distance > PetAi.FollowStartMeters)
                PathTo(leader.Position, CharmReplanMeters);
            else if (distance <= PetAi.FollowStopMeters)
                StopPathing();
            return true;
        }

        const float CharmReplanMeters = 3.0f;

        public void AddThreat(Identity identity, float amount)
        {
            // Pacified NPCs keep no hate.
            if (_evading || Npc.IsPacified)
                return;
            bool newAttacker = !Hate.Contains(identity);
            AddHate(identity, amount);
            if (newAttacker)
                CallForHelp(identity);
        }

        /// <summary>
        /// Puts <paramref name="identity"/> on the hate list. The first hate starts the NPC's fight, whoever started it
        /// (an attack on it, its own proximity aggro, a call for help), so its scripts' CombatStart runs.
        /// </summary>
        void AddHate(Identity identity, float amount)
        {
            bool firstHate = Hate.IsEmpty;
            Hate.Add(identity, amount);
            if (firstHate)
                Npc.Scripts?.Raise(NpcScripts.NpcScriptTrigger.CombatStart);
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
            int clan = Npc.Stats.GetOrZero(CharacterStat.Clan);
            bool npcAttacker = enemy is NpcCharacter { PetOwner: null };
            playfield.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>().CollectNearby(Npc, rules.SearchRadius, _nearbyNpcs);
            _helpers.Clear();
            for (int i = 0; i < _nearbyNpcs.Count; i++)
            {
                NpcCharacter other = _nearbyNpcs[i];
                if (ReferenceEquals(other, Npc) || other.IsDead || !other.Attackable || other.Brain is not NpcBrain brain
                    || brain.Pet != null || brain._evading || other.IsPacified || brain.Hate.Contains(attacker))
                    continue;
                if (other.Stats.GetOrZero(CharacterStat.Side) != side
                    || (rules.SameClanOnly && other.Stats.GetOrZero(CharacterStat.Clan) != clan))
                    continue;

                double distance = Npc.GetEdgeDistanceTo(other);
                if (distance > rules.RadiusFor(brain.SpawnAssistanceRadius(), npcAttacker) || !ZoneEngine_New.Core.Helpers.CombatRules.CanAttack(other, enemy))
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

            AddHate(attacker, NpcAiRules.ProximityHate);
            if (chain)
                CallForHelp(attacker);
        }

        readonly List<NpcCharacter> _nearbyNpcs = new();

        readonly List<(double Distance, NpcBrain Brain)> _helpers = new();

        public void ClearHate()
        {
            Hate.Clear();
            _currentTarget = Identity.None;
            _approachTarget = Identity.None;
            _engageTarget = Identity.None;
            _lastChanceUtc = default;
            _lastChanceTarget = Identity.None;
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
            if (!Npc.Attackable || Npc.Playfield == null || _evading || Npc.IsPacified)
                return;

            bool hostile = NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(CharacterStat.BreedHostility));
            bool guard = Npc.MobTemplate?.Has(Mobs.NpcFeature.Guard) == true;
            IReadOnlySet<int>? hated = HatedFamilies();
            if (!hostile && !guard && hated == null)
                return;

            // Only this NPC's cell and the cells touching it (wider only for an aggro range longer than a cell). The same
            // range is how far it notices players, defends them as a guard, and spots NPCs it hates.
            var locality = Npc.Playfield.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>();
            float range = NpcAiRules.ProximityAggroRangeFor(Npc.Stats, locality.Grid.IsOutdoor);
            if (hostile || guard)
            {
                locality.CollectNearby(Npc, range, _nearbyPlayers);
                for (int i = 0; i < _nearbyPlayers.Count; i++)
                    TryProximityAggro(_nearbyPlayers[i]);
                _nearbyPlayers.Clear();
            }

            if (guard || hated != null)
            {
                // Its own list: answering a call for help below collects into _nearbyNpcs.
                locality.CollectNearby(Npc, range, _scanNpcs);
                for (int i = 0; i < _scanNpcs.Count; i++)
                    TryNpcAggro(_scanNpcs[i], range, guard, hated);
                _scanNpcs.Clear();
            }
        }

        /// <summary>
        /// The NPC families this NPC attacks on sight (GameData MonsterCombatRules.json "Hates", by its spawn hash or
        /// its template's); null when none. Looked up once.
        /// </summary>
        IReadOnlySet<int>? HatedFamilies()
        {
            if (_hatedLookedUp)
                return _hated;
            _hatedLookedUp = true;
            if (Pet == null && Npc.Playfield?.GetService<GameData.IGameData>()?.RootPath is string root && root.Length > 0)
            {
                var catalog = Mobs.MonsterCombatRulesCatalog.For(root);
                if (catalog.TryGet(Npc.SpawnHash, out Mobs.MonsterCombatRules rules) && rules.HatedFamilies is { } bySpawn)
                    _hated = bySpawn;
                else if (catalog.TryGet(Npc.MobTemplate?.Hash, out rules))
                    _hated = rules.HatedFamilies;
            }

            return _hated;
        }

        IReadOnlySet<int>? _hated;
        bool _hatedLookedUp;

        /// <summary>The NPC this one was fighting started resetting and nobody else is left: reset as well.</summary>
        bool _resetForTargetReset;
        readonly List<NpcCharacter> _scanNpcs = new();

        /// <summary>
        /// Unprovoked aggro on another NPC: a guard on one fighting a player or a player's pet (never on another guard),
        /// and any NPC on one of a family it hates. Same terms as a player: in range, in sight, reachable. Players' pets are never targets.
        /// </summary>
        void TryNpcAggro(NpcCharacter other, float range, bool guard, IReadOnlySet<int>? hated)
        {
            if (ReferenceEquals(other, Npc) || other.IsDead || other.IsEvading || other.PetOwner != null
                || Hate.Contains(other.Identity))
                return;

            // A guard never defends a player against another guard (one of the other faction on sight of them).
            bool defend = guard && other.MobTemplate?.Has(Mobs.NpcFeature.Guard) != true && IsFightingAPlayer(other);
            bool hates = hated != null && hated.Contains(other.Stats.GetOrZero(CharacterStat.NPCFamily));
            if (!defend && !hates)
                return;
            if (!ZoneEngine_New.Core.Helpers.CombatRules.CanAttack(Npc, other) || Npc.GetEdgeDistanceTo(other) > range)
                return;
            if (!Npc.HasLineOfSightTo(other) || !HasChance(other))
                return;

            AddHate(other.Identity, NpcAiRules.ProximityHate);
            CallForHelp(other.Identity);
        }

        /// <summary>The NPC is attacking a player or a player's pet.</summary>
        bool IsFightingAPlayer(NpcCharacter other)
        {
            Identity target = other.FightingTarget;
            if (target.Instance == 0 || Npc.Playfield == null
                || !Npc.Playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel))
                return false;
            return dynel is Player { IsDead: false } || dynel is NpcCharacter { PetOwner: Player, IsDead: false };
        }

        /// <summary>
        /// Adds 1 hate when the player is nearby, in line of sight and not already listed, and either BreedHostility &gt; 0
        /// or this NPC is a guard and the player is of the opposing faction (a Clan guard on an Omni, an Omni guard on a
        /// Clan). A guard leaves a player who is pvping alone.
        /// </summary>
        public void TryProximityAggro(Character player)
        {
            if (player == null || !player.IsPlayer || player.IsDead || player is Player { HiddenFromPlayers: true })
                return;
            if (player is Player zoning && zoning.InZoneInGrace(DateTime.UtcNow))
                return;
            if (!Npc.Attackable || _evading || Npc.IsPacified)
                return;
            bool hostile = NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(CharacterStat.BreedHostility));
            bool guard = Npc.MobTemplate?.Has(Mobs.NpcFeature.Guard) == true;
            if (!hostile && !guard)
                return;
            if (Hate.Contains(player.Identity))
                return;
            int npcSide = Npc.Stats.GetOrZero(CharacterStat.Side);
            int playerSide = player.Stats.GetOrZero(CharacterStat.Side);
            if (!NpcAiRules.SideAllowsProximityAggro(npcSide, playerSide))
                return;
            // A guard that is not hostile on its own only goes for the opposing faction.
            if (!hostile && !NpcAiRules.IsOpposingFaction(npcSide, playerSide))
                return;
            if (guard && IsPvping(player))
                return;
            // A sneaker whose Concealment beats this NPC's Perception goes unnoticed.
            if (player.Motor.State == Movement.MovementState.Sneak && !ZoneEngine_New.Core.Helpers.SpecialAttacks.CanSee(Npc, player))
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
            if (!WithinProximityWalk(player, range))
                return;

            AddHate(player.Identity, NpcAiRules.ProximityHate);
            CallForHelp(player.Identity);
        }

        /// <summary>
        /// The player is pvping: PvP flagged (the attacker flag or a tower flag), or fighting or being fought by another
        /// player or a player's pet.
        /// </summary>
        bool IsPvping(Character player)
        {
            if (player is Player flagged && ZoneEngine_New.Core.Helpers.CombatRules.IsPvpEnabled(flagged))
                return true;
            Identity target = player.FightingTarget;
            if (target.Instance != 0 && Npc.Playfield != null
                && Npc.Playfield.GetRequiredService<DynelRegistry>().TryGet(target, out Dynel? dynel)
                && dynel is Character { IsDead: false } opponent && IsPlayerSide(opponent))
                return true;
            foreach (Character attacker in player.Attackers)
            {
                if (!attacker.IsDead && IsPlayerSide(attacker))
                    return true;
            }

            return false;
        }

        static bool IsPlayerSide(Character character)
            => character is Player || character is NpcCharacter { PetOwner: Player };

        /// <summary>
        /// The walk to <paramref name="player"/> is short enough for unprovoked aggro: at most proximityAggro.maxWalkFactor
        /// times the aggro <paramref name="range"/> (Gameplay.json), measured along the navmesh by the target's shared reach
        /// map. Seeing a player on a bridge overhead is not enough when the way up is far round. True when the walk is not
        /// known (no navmesh, off the mesh) or the check is off.
        /// </summary>
        bool WithinProximityWalk(Character player, float range)
        {
            float factor = Npc.Playfield?.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.ProximityAggro.MaxWalkFactor ?? 0f;
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (factor <= 0f || finder == null)
                return true;

            Vector3 feet = HeightfieldOrSelf(Npc.Position);
            ReachMap map = Npc.Playfield!.GetRequiredService<ReachMaps>().Get(player, finder, HeightfieldOrSelf(player.Position));
            var start = new System.Numerics.Vector3((float)feet.x, (float)feet.y, (float)feet.z);
            return !finder.TryGetWalkDistance(map, start, out float walk)
                || walk <= (range + Npc.GetCollisionRadius() + player.GetCollisionRadius()) * factor;
        }

        public bool ShouldLeash()
        {
            TickStallWatch.Stage("brain.leash", Npc.Identity.Instance);
            // A target whose reach map was too costly to build means this fight cannot be judged: reset now rather
            // than search again (and stop players standing out of reach from farming a stuck mob).
            bool leash = _evading || _resetForUnreachable || (_resetForTargetReset && Hate.IsEmpty)
                // A scripted march or a patrol leashes by distance from the point it is walking to, not from the spawn.
                || NpcAiRules.ShouldLeash(Hate, LeashAnchor, Npc.Position, _isEngageable);
            _resetForUnreachable = false;
            _resetForTargetReset = false;
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
        /// <summary>
        /// Resets this NPC as a leash does, without waiting for the leash to decide it: it heals, sheds what others
        /// cast on it and walks home (a charm that ended with nobody left to fight).
        /// </summary>
        internal void Evade()
        {
            BeginEvade();
            _leashing = true;
        }

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
            if (!NpcAiRules.TryHighestNearby(Hate, _isEngageable, out Identity identity, out float top))
            {
                _currentTarget = Identity.None;
                return false;
            }

            _currentTarget = BreakThreatTie(identity, top);
            return true;
        }

        /// <summary>
        /// Several enemies on the same top threat (a pack that only has proximity hate on everyone) are not an even
        /// choice: one the NPC can hit now beats one it has to walk to, the one it is already fighting beats a
        /// switch, and otherwise the nearest wins. Picking by hate-list order sent a scout running back and forth at a
        /// guard on a raised deck it could never reach while others stood inside it.
        /// </summary>
        Identity BreakThreatTie(Identity first, float top)
        {
            Identity best = first;
            int bestRank = int.MaxValue;
            double bestDistance = double.MaxValue;
            foreach (HateEntry entry in Hate.Entries)
            {
                if (entry.Threat < top || !_isEngageable(entry.Identity) || Resolve(entry.Identity) is not Character enemy)
                    continue;

                int rank = CanAttackNow(enemy) ? 0 : entry.Identity == _currentTarget ? 1 : 2;
                double distance = Npc.GetEdgeDistanceTo(enemy);
                if (rank < bestRank || (rank == bestRank && distance < bestDistance))
                {
                    best = entry.Identity;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public Character? ResolveCurrentTarget() => Resolve(_currentTarget);

        // The bearing this NPC approaches its target from, fixed when the target is taken so the approach point does
        // not swing round as the NPC closes in (NpcSpread).
        Identity _approachTarget = Identity.None;
        double _approachAngle;

        // Set once the NPC is close enough to make the final step straight at the target; kept for that target, so
        // the destination does not flip between the spread point and the target as the distance wobbles.
        bool _approachDirect;

        /// <summary>
        /// Where to walk to fight <paramref name="target"/>: inside the shortest weapon's reach, fanned across an arc
        /// by this NPC's identity so a pack does not stack on the target (best effort; see <see cref="NpcSpread"/>).
        /// Pets keep their own formation.
        /// </summary>
        public Vector3 ApproachPoint(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (Pet != null)
                return target.Position;

            if (_approachTarget != target.Identity)
            {
                _approachTarget = target.Identity;
                _approachAngle = NpcSpread.Bearing(target.Position, Npc.Position);
                _approachDirect = false;
            }

            if (_approachDirect)
                return target.Position;

            double shortest = double.MaxValue;
            foreach (CharacterWeapon? weapon in Npc.Weapons.Values)
            {
                if (weapon != null)
                    shortest = Math.Min(shortest, weapon.GetAttackRange());
            }

            if (shortest == double.MaxValue)
                shortest = CharacterWeapon.DefaultMeleeAttackRange;
            double reach = Npc.GetCollisionRadius() + target.GetCollisionRadius()
                + (Math.Min(shortest, NpcSpread.ApproachReachCap) * NpcSpread.ApproachReachFactor);
            Vector3 point = NpcSpread.ApproachPoint(Npc, target, _approachAngle, reach);

            // The spread point beside a target in a corner or at a ledge can fall in the wall or off the edge. No route
            // ends there (EndOffMesh), and the route's straight-line fallback ran the NPC into the wall and off the
            // drop; close in on the target itself instead, which the reach check already found a path to.
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder != null
                && !finder.IsDestinationOnMesh(new System.Numerics.Vector3((float)point.x, (float)point.y, (float)point.z)))
            {
                _approachDirect = true;
                return target.Position;
            }

            // Only called while out of reach. A spread point this close to the NPC is a step the planner treats as
            // "already there" (PathEndsUnderNpc) and never walks, leaving the NPC a few decimetres short of its swing
            // for ever (a 2 m scout weapon at 2.3 m edge distance). Close in on the target itself instead.
            double dx = point.x - Npc.Position.x;
            double dz = point.z - Npc.Position.z;
            double minStep = NpcAiRules.PathEndGiveUpMeters * 2.0;
            // Close to the spread point, or already inside the ring it sits on: walking to it would only step
            // sideways or back out. Close in on the target itself from here on.
            double tx = target.Position.x - Npc.Position.x;
            double tz = target.Position.z - Npc.Position.z;
            if ((dx * dx) + (dz * dz) < minStep * minStep || (tx * tx) + (tz * tz) <= reach * reach)
            {
                _approachDirect = true;
                return target.Position;
            }

            return point;
        }

        public bool HasArrivedHome()
            => !HasHome || NpcAiRules.IsNearby(Npc.Position, Home, NpcAiRules.ArriveHomeMeters);

        public bool HasChance(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (CanAttackNow(target))
                return true;
            // No LOS is fine while the chase path still has travel left. That path is the chase of the current
            // target, so it says nothing about anyone else on the hate list.
            // Give up only with no path, or already on the last point.
            if (target.Identity == _currentTarget && HasUnfinishedPath())
                return true;
            return CanPathTo(target);
        }

        public bool HasChanceWithGrace(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (HasChance(target))
            {
                _lastChanceUtc = DateTime.UtcNow;
                _lastChanceTarget = target.Identity;
                return true;
            }

            // The grace covers a brief loss of the target that last had a chance (a corner, a replan). It used to be
            // shared by the whole hate list, so an adjacent enemy kept refreshing it and an unreachable top-threat
            // target stayed "engageable" for ever: the NPC stood still out of reach while being beaten on.
            return target.Identity == _lastChanceTarget
                && NpcAiRules.IsWithinNoChanceGrace(_lastChanceUtc, DateTime.UtcNow);
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
            // Walking there is not enough: from the closest point the route gets to, some weapon must reach the
            // target. A guard posted on a deck 3 m above the plaza is "reachable" to the foot of the deck, where a
            // 2 m weapon never connects, and the NPC would run about beneath it for ever.
            if (answer == ReachAnswer.Reachable && map.Kind == ReachMapKind.Flooded && !CanHitFrom(map.EndPoint, target))
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

        /// <summary>Standing at <paramref name="at"/>, some weapon of this NPC would reach <paramref name="target"/> (edge to edge, 3D).</summary>
        bool CanHitFrom(System.Numerics.Vector3 at, Character target)
        {
            double longest = 0;
            foreach (CharacterWeapon? weapon in Npc.Weapons.Values)
            {
                if (weapon != null)
                    longest = Math.Max(longest, weapon.GetAttackRange());
            }

            if (longest <= 0)
                longest = CharacterWeapon.DefaultMeleeAttackRange;

            double dx = target.Position.x - at.X;
            double dy = target.Position.y - at.Y;
            double dz = target.Position.z - at.Z;
            double edge = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) - Npc.GetCollisionRadius() - target.GetCollisionRadius();
            if (edge > longest)
                return false;

            // In range is not enough either: a swing needs sight (CanAttackNow). Below the rim of a raised deck the
            // NPC is close enough but never sees the guard on it, and stood there for ever, neither hitting nor
            // giving up. Same eye height as Dynel.HasLineOfSightTo.
            WorldSimulation.PlayfieldWorldSimulation? world = Npc.Playfield?.WorldAccess.Instance;
            if (world == null)
                return true;
            const float eye = Movement.MovementConfig.LineOfSightEyeHeight;
            return world.HasLineOfSight(at.X, at.Y + eye, at.Z,
                (float)target.Position.x, (float)target.Position.y + eye, (float)target.Position.z);
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

        /// <summary>Base time an NPC holds and fires before each step closer.</summary>
        public const double AdvanceDelaySeconds = 4.0;

        /// <summary>Per-NPC spread of <see cref="AdvanceDelaySeconds"/> (from its identity), so a pack does not step in together.</summary>
        public const double AdvanceDelayJitterSeconds = 3.0;

        /// <summary>Longest a step closer lasts before the NPC holds and fires again while something still reaches.</summary>
        public const double AdvanceStepSeconds = 3.0;

        /// <summary>
        /// The farthest an NPC settles from its target (edge to edge), whatever its weapons' reach: even a rifle mob
        /// closes to this. Its shortest weapon's reach when that is less. NPCs want to fight up close, within 5 m.
        /// </summary>
        public const double MaxFightDistanceMeters = 5.0;

        // When the current target came into reach of a weapon this time (the hold/step cycle runs from there).
        Identity _engageTarget = Identity.None;
        DateTime _engageSinceUtc;

        /// <summary>The target left every weapon's reach: the next time it is in reach starts with a hold.</summary>
        public void ResetFightDistance() => _engageTarget = Identity.None;

        /// <summary>
        /// The chase may stop here. The NPC settles within its shortest weapon's reach, and never farther than
        /// <see cref="MaxFightDistanceMeters"/>. Getting there alternates: hold and fire for
        /// <see cref="AdvanceDelaySeconds"/> (plus its own jitter), then step closer for at most
        /// <see cref="AdvanceStepSeconds"/>, and repeat. A target that backs off to stay in reach of a long weapon
        /// therefore keeps getting shot instead of keeping the NPC running (ranged weapons do not fire on the move);
        /// only a target out of every weapon's reach gets an uninterrupted chase. An NPC that cannot walk, or has no
        /// route closer, holds where it is.
        /// </summary>
        public bool HoldsFightDistance(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (Pet != null || Npc.IsImmobile || Npc.IsRooted)
                return true;

            double shortest = double.MaxValue;
            foreach (CharacterWeapon? weapon in Npc.Weapons.Values)
            {
                if (weapon != null)
                    shortest = Math.Min(shortest, weapon.GetAttackRange());
            }

            double settle = Math.Min(shortest == double.MaxValue ? CharacterWeapon.DefaultMeleeAttackRange : shortest, MaxFightDistanceMeters);
            if (Npc.GetEdgeDistanceTo(target) <= settle)
                return true;

            DateTime now = DateTime.UtcNow;
            if (_engageTarget != target.Identity)
            {
                _engageTarget = target.Identity;
                _engageSinceUtc = now;
            }

            double hold = AdvanceDelaySeconds + ((NpcSpread.Unit(Npc) - 0.5) * AdvanceDelayJitterSeconds);
            double phase = (now - _engageSinceUtc).TotalSeconds % (hold + AdvanceStepSeconds);
            if (phase < hold)
                return true;

            return !CanPathTo(target);
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
            // A segment that ends within give-up reach of the NPC counts as reached even if the body never moved
            // (a step it cannot climb drops the path at once). Planning again from the same spot is the same attempt:
            // it waits the replan interval and keeps the stuck clock, rather than re-announcing the identical path
            // every think forever.
            bool continuing = active
                || (_followAnnounced && (!HasReachedSegmentEnd() || IsAtSegmentStart()));
            if (continuing)
            {
                if (IsStuck(now))
                {
                    // Warping ahead did not help: the body cannot follow this route (e.g. navmesh over
                    // collision it cannot climb). Evade rather than stand there taking hits; if it is
                    // the walk home that is stuck, give up on walking and let the caller snap home.
                    if (_stuckWarps >= NpcAiRules.MaxStuckWarps)
                    {
                        // A scripted walk that cannot go on reports Stuck to its program; nothing to evade from.
                        if (_scriptedDestination is not null)
                        {
                            _stuckWarps = 0;
                            _scriptedDestination = null;
                            _scriptedResult = ScriptMoveResult.Stuck;
                            StopPathing();
                            return;
                        }

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

            // A direct scripted leg is one straight line with nothing to re-plan around: the whole leg goes out as one
            // segment (observers get the destination itself), not a lookahead's worth re-sent as the NPC walks.
            bool truncated;
            if (ScriptMovesDirect)
            {
                _pathScratch.Clear();
                _pathScratch.AddRange(_routeScratch);
                truncated = false;
            }
            else
                truncated = TruncateByDistance(_routeScratch, SegmentLookaheadMeters(), _pathScratch);
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
            _segmentStart = new Vector3(start.x, start.y, start.z);
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

        /// <summary>Where the current segment was planned from.</summary>
        Vector3 _segmentStart = new(0, 0, 0);

        /// <summary>The NPC still stands where its current segment started: it has not moved along it at all.</summary>
        bool IsAtSegmentStart()
        {
            double dx = Npc.Position.x - _segmentStart.x;
            double dz = Npc.Position.z - _segmentStart.z;
            return (dx * dx) + (dz * dz) <= SegmentStartToleranceMeters * SegmentStartToleranceMeters;
        }

        /// <summary>Movement smaller than this since a segment was planned is no movement at all.</summary>
        const double SegmentStartToleranceMeters = 0.25;

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

        /// <summary>The NPC has a path but has not made progress along it for the stuck time.</summary>
        public bool IsPathStalled() => Npc.Motor.HasPath && IsStuck(DateTime.UtcNow);

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
        /// Reuses the <see cref="CanPathTo"/> path when it was planned from here to the same place. With no navmesh the
        /// route is a straight line; with one and no path found, the route is only the current position (no walk).
        /// </summary>
        void PlanRoute(Vector3 destination)
        {
            _routeScratch.Clear();
            Vector3 feet = Npc.Position;
            _routeScratch.Add(new Vector3(feet.x, feet.y, feet.z));
            if (ScriptMovesDirect)
            {
                _routeScratch.Add(new Vector3(destination.x, destination.y, destination.z));
                return;
            }

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
                // With a navmesh, no route means no walk: a straight line ran NPCs into walls and off ledges.
                var start = new System.Numerics.Vector3((float)startPos.x, (float)startPos.y, (float)startPos.z);
                var end = new System.Numerics.Vector3((float)destination.x, (float)destination.y, (float)destination.z);
                if (PathDiag.TryFindPath(finder, start, end, _planScratch, PathDiag.Route) && _planScratch.Count > 0)
                    AppendRoute(_planScratch);
                return;
            }

            // A playfield without a navmesh has nothing to route over: the straight line is all there is.
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

        /// <summary>
        /// The NPC stops doing what this brain had it doing, without a reset: its fight, route, hate and what it had
        /// judged about targets go, while its health, nano and buffs stay as they are (unlike
        /// <see cref="ResetOutOfCombat"/>). A pet zoning with its owner drops what referred to the world it leaves; a
        /// charm hands the NPC to another brain, and its own takes it back clean when the charm ends.
        /// </summary>
        internal void DropEngagement()
        {
            StopFighting();
            StopPathing();
            ClearHate();
            _leashing = false;
            _evading = false;
            _resetForUnreachable = false;
            _stuckWarps = 0;
            _returningHome = false;
            _returnFailed = false;
            _reachCacheId = Identity.None;
            _reachCacheResult = false;
            _engageByTarget.Clear();
            _nearbyPlayers.Clear();
            _nearbyNpcs.Clear();
            _helpers.Clear();
            _followAnnounced = false;
            ProgressSinceUtc = default;
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
            {
                if (target is not NpcCharacter { IsEvading: true } && target is not Player { HiddenFromPlayers: true })
                    return target;

                // An NPC that is resetting (leashing home) is out of the fight: let it go rather than chase it home.
                // A GM hidden with .visibility off is let go the same way.
                // With nobody else to fight, this NPC gives up too and resets on its next think.
                Hate.Remove(identity);
                if (_currentTarget == identity)
                    _currentTarget = Identity.None;
                if (Hate.IsEmpty)
                    _resetForTargetReset = true;
                return null;
            }

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
