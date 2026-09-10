namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using GroveGames.BehaviourTree.Collections;
    using GroveGames.BehaviourTree.Nodes;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    public sealed class NpcBrain
    {
        readonly NpcBehaviourTree _tree;
        readonly List<Vector3> _pathScratch = new(2);
        Identity _currentTarget = Identity.None;
        bool _leashing;
        bool _treeResetPending;
        bool _followAnnounced;
        Vector3 _lastAnnouncedEnd = new(0, 0, 0);
        DateTime _lastRepathUtc;

        NpcBrain(NpcCharacter npc, Vector3 home, NpcAiProfile profile)
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

        public Vector3 Home { get; }

        public HateList Hate { get; }

        public bool PatrolEnabled { get; set; }

        public IReadOnlyList<Vector3> PatrolWaypoints { get; set; }

        public bool IsBusy => !Hate.IsEmpty || _leashing || Npc.FightingTarget.Instance != 0;

        public static NpcBrain Create(NpcCharacter npc, Vector3 home, NpcAiProfile? profile = null)
        {
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(home);
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
            if (!NpcAiRules.IsNearby(Npc.Position, player.Position, NpcAiRules.NearbyRange))
                return;

            Hate.Add(player.Identity, NpcAiRules.ProximityHate);
        }

        public bool ShouldLeash()
        {
            TickStallWatch.Stage("brain.leash", Npc.Identity.Instance);
            bool leash = NpcAiRules.ShouldLeash(Hate, Home, Npc.Position, IsValidNearby);
            _leashing = leash;
            return leash;
        }

        public bool HasNearbyHate()
        {
            TickStallWatch.Stage("brain.hate", Npc.Identity.Instance);
            return NpcAiRules.TryHighestNearby(Hate, IsValidNearby, out _, out _);
        }

        public bool TrySelectHighestThreat()
        {
            TickStallWatch.Stage("brain.select", Npc.Identity.Instance);
            if (!NpcAiRules.TryHighestNearby(Hate, IsValidNearby, out Identity identity, out _))
            {
                _currentTarget = Identity.None;
                return false;
            }

            _currentTarget = identity;
            return true;
        }

        public Character? ResolveCurrentTarget() => Resolve(_currentTarget);

        public bool HasArrivedHome()
            => NpcAiRules.IsNearby(Npc.Position, Home, NpcAiRules.ArriveHomeMeters);

        public bool IsInAttackRange(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return Npc.Distance3D(target) <= GetAttackRange();
        }

        public void PathTo(Vector3 destination)
        {
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

            // Retarget in place while chasing so SetPath does not ClearPath every repath.
            if (!Npc.Motor.TryRetargetFinalWaypoint(destination, 0.25f))
            {
                _pathScratch.Clear();
                _pathScratch.Add(new Vector3(destination.x, destination.y, destination.z));
                Npc.Motor.SetPath(_pathScratch);
            }
            else
            {
                _pathScratch.Clear();
                _pathScratch.Add(new Vector3(destination.x, destination.y, destination.z));
            }

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

        bool IsValidNearby(Identity identity)
        {
            Character? target = Resolve(identity);
            return target != null && NpcAiRules.IsNearby(Npc.Position, target.Position, NpcAiRules.NearbyRange);
        }
    }
}
