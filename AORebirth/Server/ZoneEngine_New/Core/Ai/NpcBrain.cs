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
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    public sealed class NpcBrain
    {
        readonly NpcBehaviourTree _tree;
        readonly List<Vector3> _pathScratch = new(2);
        readonly List<System.Numerics.Vector3> _reachPathScratch = new(8);
        Identity _currentTarget = Identity.None;
        bool _leashing;
        bool _treeResetPending;
        bool _followAnnounced;
        Vector3 _lastAnnouncedEnd = new(0, 0, 0);
        DateTime _lastRepathUtc;
        Identity _reachCacheId = Identity.None;
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
                && (now - _reachCacheUtc).TotalSeconds < NpcFollowTarget.RepathIntervalSeconds
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

        public void PathTo(Vector3 destination)
        {
            destination = HeightfieldOrSelf(destination);
            DateTime now = DateTime.UtcNow;
            if (Npc.Motor.HasPath || _followAnnounced)
            {
                double elapsed = (now - _lastRepathUtc).TotalSeconds;
                if (elapsed < NpcFollowTarget.RepathIntervalSeconds)
                    return;

                float delta = (float)Vector3.Abs(destination - _lastAnnouncedEnd);
                if (delta < NpcFollowTarget.MinAnnounceDeltaMeters)
                    return;
            }

            if (_reachPathScratch.Count > 0
                && Vector3.Abs(destination - _reachCachePos) < NpcFollowTarget.MinAnnounceDeltaMeters)
            {
                _pathScratch.Clear();
                for (int i = 0; i < _reachPathScratch.Count; i++)
                {
                    System.Numerics.Vector3 point = _reachPathScratch[i];
                    _pathScratch.Add(new Vector3(point.X, point.Y, point.Z));
                }

                Npc.Motor.SetPath(_pathScratch);
            }
            else if (!Npc.Motor.TryRetargetFinalWaypoint(destination, 0.25f))
                Npc.Motor.NavigateTo(destination);

            _pathScratch.Clear();
            var remaining = Npc.Motor.CopyRemainingWaypoints();
            for (int i = 0; i < remaining.Length; i++)
                _pathScratch.Add(new Vector3(remaining[i].X, remaining[i].Y, remaining[i].Z));
            if (_pathScratch.Count == 0)
                _pathScratch.Add(new Vector3(destination.x, destination.y, destination.z));

            NpcFollowTarget.AnnounceCoordinatePath(Npc, Npc.Position, _pathScratch);
            _followAnnounced = true;
            _lastAnnouncedEnd = new Vector3(destination.x, destination.y, destination.z);
            _lastRepathUtc = now;
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
            _lastAnnouncedEnd = new Vector3(0, 0, 0);
            _lastRepathUtc = default;
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
