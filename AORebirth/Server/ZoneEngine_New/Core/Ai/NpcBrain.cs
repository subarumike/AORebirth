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
        readonly NpcBehaviourTree _tree;
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
        DateTime _lastChanceUtc;
        int _stuckWarps;
        bool _evading;
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
        }

        public NpcCharacter Npc { get; }

        public Vector3? Home { get; }

        public bool HasHome => Home is not null;

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

            if (_treeResetPending)
            {
                _treeResetPending = false;
                _tree.Reset();
            }

            TickStallWatch.Stage("brain.scan", Npc.Identity.Instance);
            ScanProximity();
            TickStallWatch.Stage("brain.tree", Npc.Identity.Instance);
            _tree.Tick((float)deltaTime);
        }

        public void AddThreat(Identity identity, float amount)
        {
            if (_evading)
                return;
            Hate.Add(identity, amount);
        }

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
            if (!Npc.Attackable)
                return;
            if (!NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat)))
                return;
            if (Npc.Playfield == null)
                return;

            foreach (Player player in Npc.Playfield.GetRequiredService<DynelRegistry>().PlayerEntities())
                TryProximityAggro(player);
        }

        /// <summary>Adds 1 hate when BreedHostility &gt; 0 and the player is nearby and not already listed.</summary>
        public void TryProximityAggro(Character player)
        {
            if (player == null || !player.IsPlayer || player.IsDead)
                return;
            if (!Npc.Attackable || _evading)
                return;
            if (!NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat)))
                return;
            if (Hate.Contains(player.Identity))
                return;
            if (Npc.GetEdgeDistanceTo(player) > NpcAiRules.ProximityAggroRange)
                return;
            if (!HasChance(player))
                return;

            Hate.Add(player.Identity, NpcAiRules.ProximityHate);
        }

        public bool ShouldLeash()
        {
            TickStallWatch.Stage("brain.leash", Npc.Identity.Instance);
            bool leash = _evading || NpcAiRules.ShouldLeash(Hate, Home, Npc.Position, IsEngageable);
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
            return NpcAiRules.TryHighestNearby(Hate, IsEngageable, out _, out _);
        }

        public bool TrySelectHighestThreat()
        {
            TickStallWatch.Stage("brain.select", Npc.Identity.Instance);
            if (!NpcAiRules.TryHighestNearby(Hate, IsEngageable, out Identity identity, out _))
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

        public bool CanPathTo(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder == null)
                return true;

            DateTime now = DateTime.UtcNow;
            Vector3 cachedEnd = HeightfieldOrSelf(target.Position);
            if (_reachCacheId == target.Identity
                && (now - _reachCacheUtc).TotalSeconds < NpcFollowTarget.PathReplanSeconds
                && Vector3.Abs(cachedEnd - _reachCachePos) < NpcFollowTarget.MinAnnounceDeltaMeters)
                return _reachCacheResult;

            Vector3 startPos = HeightfieldOrSelf(Npc.Position);
            Vector3 endPos = cachedEnd;
            var start = new System.Numerics.Vector3((float)startPos.x, (float)startPos.y, (float)startPos.z);
            var end = new System.Numerics.Vector3((float)endPos.x, (float)endPos.y, (float)endPos.z);

            // Off-mesh starts stay movable so holes and future off-mesh links are not a wall.
            // A complete path that ends under the NPC is not a chase chance once the target
            // is also out of attack range — they cannot get closer. In-range fight-back is
            // decided by HasChance before this runs.
            bool foundPath = finder.TryFindPath(start, end, _reachPathScratch);
            bool reachable;
            if (!finder.TrySnap(start, out _))
                reachable = true;
            else if (foundPath)
                reachable = !PathEndsUnderNpc(start, _reachPathScratch);
            else
                reachable = !finder.TrySnap(end, out _);
            _reachCacheId = target.Identity;
            _reachCacheStart = new Vector3(startPos.x, startPos.y, startPos.z);
            _reachCachePos = new Vector3(endPos.x, endPos.y, endPos.z);
            _reachCacheUtc = now;
            _reachCacheResult = reachable;
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
        public void PathTo(Vector3 destination)
        {
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

            float lookaheadMeters = (float)(Npc.Motor.Vehicle.MaxVel * NpcFollowTarget.PathLookaheadSeconds);
            bool truncated = TruncateByDistance(_routeScratch, lookaheadMeters, _pathScratch);
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

        bool ShouldReplan(Vector3 destination)
        {
            if (_segmentReachesDestination)
                return Vector3.Abs(destination - _segmentDestination) >= NpcFollowTarget.MinAnnounceDeltaMeters;

            double leftSeconds = NpcFollowTarget.PathLookaheadSeconds - NpcFollowTarget.PathReplanSeconds;
            return Npc.Motor.RemainingPathMeters() <= Npc.Motor.Vehicle.MaxVel * leftSeconds;
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
                AppendRoute(_reachPathScratch);
                return;
            }

            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder != null)
            {
                var start = new System.Numerics.Vector3((float)startPos.x, (float)startPos.y, (float)startPos.z);
                var end = new System.Numerics.Vector3((float)destination.x, (float)destination.y, (float)destination.z);
                if (finder.TryFindPath(start, end, _planScratch) && _planScratch.Count > 0)
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

        /// <summary>A complete navmesh route from here ends at <paramref name="destination"/>. No navmesh: true.</summary>
        bool CanWalkTo(Vector3 destination)
        {
            NavMeshPathfinder? finder = Npc.Playfield?.Pathfinder;
            if (finder == null)
                return true;

            Vector3 from = HeightfieldOrSelf(Npc.Position);
            Vector3 to = HeightfieldOrSelf(destination);
            var start = new System.Numerics.Vector3((float)from.x, (float)from.y, (float)from.z);
            var end = new System.Numerics.Vector3((float)to.x, (float)to.y, (float)to.z);
            if (!finder.TrySnap(start, out _) || !finder.TryFindPath(start, end, _planScratch) || _planScratch.Count == 0)
                return false;

            return System.Numerics.Vector3.Distance(_planScratch[_planScratch.Count - 1], end) <= NpcAiRules.ArriveHomeMeters;
        }

        public void ResetOutOfCombat()
        {
            TickStallWatch.Stage("brain.reset", Npc.Identity.Instance);
            _leashing = false;
            _evading = false;
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

        bool IsEngageable(Identity identity)
        {
            Character? target = Resolve(identity);
            return target != null
                && Npc.GetEdgeDistanceTo(target) <= NpcAiRules.EngageRange(identity == _currentTarget)
                && HasChanceWithGrace(target);
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
