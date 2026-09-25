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
        bool _segmentReachesDestination;
        Vector3 _progressAnchor = new(0, 0, 0);
        Identity _reachCacheId = Identity.None;
        Vector3 _reachCacheStart = new(0, 0, 0);
        Vector3 _reachCachePos = new(0, 0, 0);
        DateTime _reachCacheUtc;
        bool _reachCacheResult;
        DateTime _lastChanceUtc;

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

        public void AddThreat(Identity identity, float amount) => Hate.Add(identity, amount);

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
            if (!Npc.Attackable)
                return;
            if (!NpcAiRules.IsProximityHostile(Npc.Stats.GetOrZero(NpcAiRules.BreedHostilityStat)))
                return;
            if (Hate.Contains(player.Identity))
                return;
            if (Npc.GetEdgeDistanceTo(player) > NpcAiRules.NearbyRange)
                return;
            if (!HasChance(player))
                return;

            Hate.Add(player.Identity, NpcAiRules.ProximityHate);
        }

        public bool ShouldLeash()
        {
            TickStallWatch.Stage("brain.leash", Npc.Identity.Instance);
            bool leash = NpcAiRules.ShouldLeash(Hate, Home, Npc.Position, IsEngageable);
            _leashing = leash;
            return leash;
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
                last.X,
                last.Z);
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
            return Npc.GetEdgeDistanceTo(target) <= GetAttackRange();
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
        /// moves. An NPC that stops making progress for <see cref="NpcFollowTarget.PathStuckWarpSeconds"/> is
        /// warped one replan interval along its path.
        /// </summary>
        public void PathTo(Vector3 destination)
        {
            destination = HeightfieldOrSelf(destination);
            DateTime now = DateTime.UtcNow;
            bool active = Npc.Motor.HasPath;
            if (active)
            {
                if (IsStuck(now))
                {
                    WarpAlongPath(now);
                    active = false;
                }
                else if (!ShouldReplan(destination))
                    return;
            }

            PlanRoute(destination);
            Vector3 start = Npc.Position;
            if (PathEndsUnderNpc(
                new System.Numerics.Vector3((float)start.x, (float)start.y, (float)start.z),
                (float)_routeScratch[_routeScratch.Count - 1].x,
                (float)_routeScratch[_routeScratch.Count - 1].z))
                return;

            float lookaheadMeters = (float)(Npc.Motor.Vehicle.MaxVel * NpcFollowTarget.PathLookaheadSeconds);
            bool truncated = TruncateByDistance(_routeScratch, lookaheadMeters, _pathScratch);
            if (active)
                Npc.Motor.ReplacePath(_pathScratch);
            else
                Npc.Motor.SetPath(_pathScratch);
            if (!Npc.Motor.HasPath)
                return;

            if (!active)
                ResetProgress(now);
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

        public void ResetOutOfCombat()
        {
            TickStallWatch.Stage("brain.reset", Npc.Identity.Instance);
            _leashing = false;
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

        double GetAttackRange()
        {
            double range = CharacterWeapon.DefaultMeleeAttackRange;
            foreach (CharacterWeapon? weapon in Npc.Weapons.Values)
            {
                if (weapon == null)
                    continue;
                range = Math.Max(range, weapon.GetAttackRange());
            }

            return range;
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
                && Npc.GetEdgeDistanceTo(target) <= NpcAiRules.NearbyRange
                && HasChanceWithGrace(target);
        }

        internal static bool PathEndsUnderNpc(
            System.Numerics.Vector3 npc,
            IReadOnlyList<System.Numerics.Vector3> path)
        {
            if (path == null || path.Count == 0)
                return true;

            System.Numerics.Vector3 end = path[path.Count - 1];
            return PathEndsUnderNpc(npc, end.X, end.Z);
        }

        static bool PathEndsUnderNpc(System.Numerics.Vector3 npc, float endX, float endZ)
        {
            float dx = endX - npc.X;
            float dz = endZ - npc.Z;
            float limit = NpcAiRules.PathEndGiveUpMeters;
            return (dx * dx) + (dz * dz) <= limit * limit;
        }
    }
}
