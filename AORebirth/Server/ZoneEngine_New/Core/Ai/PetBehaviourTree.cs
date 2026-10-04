namespace ZoneEngine_New.Core.Ai
{
    using System;
    using System.Collections.Generic;

    using GroveGames.BehaviourTree;
    using GroveGames.BehaviourTree.Nodes;
    using GroveGames.BehaviourTree.Nodes.Composites;
    using GroveGames.BehaviourTree.Nodes.Decorators;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A summoned pet's behaviour, most urgent first: leave when its owner is gone or its time is up; rejoin a
    /// player owner it is too far from (or cannot walk to); fight the enemy it has picked; hold still while
    /// told to wait; otherwise follow its owner in formation.
    /// </summary>
    sealed class PetBehaviourTree : BehaviourTree
    {
        readonly NpcBrain _brain;

        public PetBehaviourTree(IRoot root, NpcBrain brain)
            : base(root)
        {
            _brain = brain;
        }

        public override void SetupTree()
        {
            IParent root = Root.Selector("pet-root");
            root.Conditional(() => PetAi.ShouldLeave(_brain), "pet-should-leave").Attach(new PetLeaveNode(_brain));
            root.Conditional(() => PetAi.ShouldRejoinOwner(_brain), "pet-should-rejoin").Attach(new PetRejoinOwnerNode(_brain));
            root.Attach(new PetTendNode(_brain));
            root.Conditional(() => PetAi.PickEnemy(_brain), "pet-has-enemy").Attach(new PetFightNode(_brain));
            root.Conditional(() => _brain.Pet!.Mode == PetMode.Wait, "pet-waiting").Attach(new PetWaitNode(_brain));
            root.Attach(new PetFollowNode(_brain));
        }
    }

    /// <summary>The decisions behind <see cref="PetBehaviourTree"/>.</summary>
    static class PetAi
    {
        /// <summary>A guarding pet does not go after enemies farther than this from its owner.</summary>
        public const double GuardRange = 30.0;

        /// <summary>A following pet sets off once it is this far from its formation spot.</summary>
        public const double FollowStartMeters = 2.5;

        /// <summary>…and settles once it is this close.</summary>
        public const double FollowStopMeters = 1.0;

        /// <summary>With no route to its owner, a pet this close (straight line) stays put instead of warping.</summary>
        public const double NoRouteWarpMeters = 5.0;

        /// <summary>The walking distance to the owner is measured again after this long…</summary>
        public const double OwnerDistanceSeconds = 1.0;

        /// <summary>…or once the pet or its owner has moved this far since.</summary>
        public const double OwnerDistanceMoveMeters = 3.0;

        /// <summary>A melee pet closes to this distance from its enemy, at its own spot around it.</summary>
        public const double AttackReach = 1.6;

        [ThreadStatic]
        static List<System.Numerics.Vector3>? _routeScratch;

        /// <summary>Straight-line distance between two dynels' positions.</summary>
        public static double Distance(Dynel a, Dynel b) => Vector3.Abs(a.Position - b.Position);

        public static bool ShouldLeave(NpcBrain brain)
        {
            PetController pet = brain.Pet!;
            Character owner = pet.Owner;
            if (pet.HasExpired(DateTime.UtcNow))
                return true;
            return owner.IsDead || owner.Playfield == null || !ReferenceEquals(owner.Playfield, brain.Npc.Playfield);
        }

        /// <summary>
        /// A player's pet rejoins its owner when it is more than <see cref="PetService.MaxPlayerPetDistance"/> away,
        /// when its walking got stuck, or when it is far behind and no route leads back. A waiting pet stays put
        /// unless it is too far away. An NPC's pet stays on the navmesh like any NPC.
        /// </summary>
        public static bool ShouldRejoinOwner(NpcBrain brain)
        {
            PetController pet = brain.Pet!;
            if (!pet.IsPlayerPet)
            {
                pet.WarpRequested = false;
                return false;
            }

            if (pet.WarpRequested)
                return true;

            // Too far to walk back, or no way to walk back at all (a waiting pet only on distance: it stays put).
            // A pet already at its owner's side is not warped over a route that just fails to reach an owner
            // standing on a ledge or an unmeshed spot.
            double distance = OwnerDistance(brain);
            return distance > PetService.MaxPlayerPetDistance
                   || (double.IsPositiveInfinity(distance) && pet.Mode != PetMode.Wait
                       && Distance(brain.Npc, pet.Owner) > NoRouteWarpMeters);
        }

        /// <summary>
        /// How far the pet has to walk to reach its owner: the navmesh route length, so a pet behind a wall or on
        /// another floor counts as far even when it is close in a straight line; +infinity when no route leads
        /// there. The straight line is a lower bound, so a pet already past
        /// <see cref="PetService.MaxPlayerPetDistance"/> needs no route; otherwise the route is measured at most
        /// every <see cref="OwnerDistanceSeconds"/> (or once either has moved <see cref="OwnerDistanceMoveMeters"/>)
        /// and reused. Without a navmesh, or with either end off it, the straight line is all there is.
        /// </summary>
        public static double OwnerDistance(NpcBrain brain)
        {
            PetController pet = brain.Pet!;
            NpcCharacter npc = brain.Npc;
            Character owner = pet.Owner;
            double straight = Distance(npc, owner);
            if (straight > PetService.MaxPlayerPetDistance)
                return straight;

            Playfield? playfield = npc.Playfield;
            AORebirth.World.Pathfinding.NavMeshPathfinder? finder = playfield?.Pathfinder;
            if (finder == null)
                return straight;

            DateTime now = DateTime.UtcNow;
            if (!double.IsNaN(pet.CachedOwnerDistance)
                && (now - pet.OwnerDistanceUtc).TotalSeconds < OwnerDistanceSeconds
                && pet.OwnerDistancePetAt != null && Vector3.Abs(npc.Position - pet.OwnerDistancePetAt) < OwnerDistanceMoveMeters
                && pet.OwnerDistanceOwnerAt != null && Vector3.Abs(owner.Position - pet.OwnerDistanceOwnerAt) < OwnerDistanceMoveMeters)
                return Math.Max(straight, pet.CachedOwnerDistance);

            double measured = MeasureWalk(playfield!, finder, npc.Position, owner.Position, straight);
            pet.CachedOwnerDistance = measured;
            pet.OwnerDistanceUtc = now;
            pet.OwnerDistancePetAt = new Vector3(npc.Position.x, npc.Position.y, npc.Position.z);
            pet.OwnerDistanceOwnerAt = new Vector3(owner.Position.x, owner.Position.y, owner.Position.z);
            return Math.Max(straight, measured);
        }

        /// <summary>Navmesh route length between two points; the straight line when either is off the navmesh.</summary>
        static double MeasureWalk(Playfield playfield, AORebirth.World.Pathfinding.NavMeshPathfinder finder,
            Vector3 from, Vector3 to, double straight)
        {
            System.Numerics.Vector3 start = OnFloor(playfield, from);
            System.Numerics.Vector3 end = OnFloor(playfield, to);
            if (!finder.TrySnap(start, out _) || !finder.TrySnap(end, out _))
                return straight;

            List<System.Numerics.Vector3> route = _routeScratch ??= new List<System.Numerics.Vector3>(16);
            if (!PathDiag.TryFindPath(finder, start, end, route, PathDiag.Pet) || route.Count == 0)
                return double.PositiveInfinity;

            // A route that stops short of the owner does not reach it.
            System.Numerics.Vector3 last = route[route.Count - 1];
            if (MathF.Abs(last.X - end.X) > 3f || MathF.Abs(last.Z - end.Z) > 3f)
                return double.PositiveInfinity;

            double length = System.Numerics.Vector3.Distance(start, route[0]);
            for (int i = 1; i < route.Count; i++)
                length += System.Numerics.Vector3.Distance(route[i - 1], route[i]);
            return length;
        }

        static System.Numerics.Vector3 OnFloor(Playfield playfield, Vector3 position)
        {
            Vector3 at = playfield.TrySnapFeetToFloor(position, out Vector3 floor) ? floor : position;
            return new System.Numerics.Vector3(at.xf, at.yf, at.zf);
        }

        /// <summary>
        /// Picks the pet's enemy for this tick and hands it to the brain. An attack order's target comes first
        /// while it lives. A guarding pet (and an NPC's pet) stays on an enemy it already fights, then takes its
        /// owner's opponent, then whoever is fighting its owner, then whoever is fighting it, all within
        /// <see cref="GuardRange"/> of the owner. A following pet, a waiting pet and a heal pet that was not
        /// ordered to attack stay out of it: follow means come along now, fight or no fight.
        /// </summary>
        public static bool PickEnemy(NpcBrain brain)
        {
            PetController pet = brain.Pet!;
            NpcCharacter npc = brain.Npc;
            Character? enemy = null;

            // Leashed to the owner: with the owner too far away, any fight (an attack order too) is dropped and the
            // pet heads back; past MaxPlayerPetDistance it is warped back instead.
            if (OwnerDistance(brain) > GuardRange)
            {
                pet.EndAttack();
                brain.SetCurrentTarget(Identity.None);
                return false;
            }

            if (pet.Mode == PetMode.Attack)
            {
                enemy = Resolve(npc, pet.AttackTarget, requireNearOwner: false);
                if (enemy == null)
                    pet.EndAttack();
            }

            if (enemy == null)
            {
                switch (pet.Mode)
                {
                    case PetMode.Guard:
                    case PetMode.Assist:
                        if (PetTypes.Slot(pet.Type) == PetTypes.Slot(PetTypes.Heal))
                            break;
                        enemy = Resolve(npc, npc.FightingTarget, requireNearOwner: true) is Character current
                            && IsEngaged(current, pet.Owner, npc)
                                ? current
                                : Resolve(npc, pet.Owner.FightingTarget, requireNearOwner: true)
                                  ?? Nearest(npc, pet.Owner.Attackers)
                                  ?? Nearest(npc, npc.Attackers);
                        break;
                    case PetMode.Follow:
                        // Told to follow: it breaks off any fight and comes along, even while something hits it.
                        break;
                }
            }

            if (enemy == null)
            {
                brain.SetCurrentTarget(Identity.None);
                return false;
            }

            brain.SetCurrentTarget(enemy.Identity);
            return true;
        }

        /// <summary>A pet heals its charge whenever it is below this percent: greedy, all the way to full.</summary>
        public const int TendBelowPercent = 100;

        /// <summary>
        /// Who the pet looks after: the character a heal order put it on while that one is still here, otherwise
        /// its owner if it is a heal pet. Other pets look after no one unless ordered to. An over-equipped pet heals
        /// no one: it only follows.
        /// </summary>
        public static Character? Charge(NpcBrain brain)
        {
            PetController pet = brain.Pet!;
            if (pet.IsOverEquipped)
                return null;

            if (pet.HealTarget.Instance != 0)
            {
                if (ResolveFriend(brain.Npc, pet.HealTarget) is Character ordered)
                    return ordered;
                pet.EndHeal();
            }

            return PetTypes.Slot(pet.Type) == PetTypes.Slot(PetTypes.Heal) ? pet.Owner : null;
        }

        public static int HealthPercent(Character character)
        {
            int max = character.Stats.GetOrZero(CharacterStat.MaxHealth);
            return max <= 0 ? 100 : (int)(100L * Math.Max(0, character.Stats.GetOrZero(CharacterStat.Health)) / max);
        }

        public static Character? ResolveFriend(NpcCharacter pet, Identity identity)
        {
            if (identity.Instance == 0 || pet.Playfield == null)
                return null;
            return pet.Playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel)
                   && dynel is Character character && !character.IsDead && ReferenceEquals(character.Playfield, pet.Playfield)
                ? character
                : null;
        }

        /// <summary>A formation spot this far off the navmesh is given up for the owner's own spot.</summary>
        public const float FollowSnapMeters = 1.5f;

        /// <summary>
        /// The pet's formation spot next to its owner, on the navmesh. A spot inside a wall or off a ledge would
        /// leave the pet unable to arrive, so it follows to its owner's own spot instead.
        /// </summary>
        public static Vector3 FollowPoint(NpcBrain brain) => FollowPoint(brain, 0, 0);

        /// <summary>The formation spot moved by (<paramref name="leadX"/>, <paramref name="leadZ"/>) before it is put on the navmesh.</summary>
        public static Vector3 FollowPoint(NpcBrain brain, double leadX, double leadZ)
        {
            PetController pet = brain.Pet!;
            Vector3 formation = PetFormation.PointFor(pet.Owner, pet.Owner.OwnedPets.FormationIndex(brain.Npc));
            var spot = new Vector3(formation.x + leadX, formation.y, formation.z + leadZ);
            AORebirth.World.Pathfinding.NavMeshPathfinder? finder = brain.Npc.Playfield?.Pathfinder;
            if (finder == null)
                return spot;

            var wanted = new System.Numerics.Vector3(spot.xf, spot.yf, spot.zf);
            if (finder.TrySnap(wanted, out System.Numerics.Vector3 onMesh)
                && MathF.Abs(onMesh.X - wanted.X) <= FollowSnapMeters && MathF.Abs(onMesh.Z - wanted.Z) <= FollowSnapMeters)
                return new Vector3(onMesh.X, onMesh.Y, onMesh.Z);

            Vector3 owner = pet.Owner.Position;
            return finder.TrySnap(new System.Numerics.Vector3(owner.xf, owner.yf, owner.zf), out System.Numerics.Vector3 ownerOnMesh)
                ? new Vector3(ownerOnMesh.X, ownerOnMesh.Y, ownerOnMesh.Z)
                : new Vector3(owner.x, owner.y, owner.z);
        }

        /// <summary>Fighting the owner or the pet, or being fought by the owner.</summary>
        static bool IsEngaged(Character enemy, Character owner, NpcCharacter pet)
            => enemy.FightingTarget == owner.Identity || enemy.FightingTarget == pet.Identity
               || owner.FightingTarget == enemy.Identity;

        static Character? Nearest(NpcCharacter pet, IReadOnlyCollection<Character> candidates)
        {
            Character? best = null;
            double bestDistance = double.MaxValue;
            foreach (Character candidate in candidates)
            {
                if (!IsValidEnemy(pet, candidate, requireNearOwner: true))
                    continue;
                double distance = Distance(pet, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        static Character? Resolve(NpcCharacter pet, Identity identity, bool requireNearOwner)
        {
            if (identity.Instance == 0 || pet.Playfield == null)
                return null;
            if (!pet.Playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel)
                || dynel is not Character character)
                return null;
            return IsValidEnemy(pet, character, requireNearOwner) ? character : null;
        }

        /// <summary>Alive, here, fair game for the pet, not running home, and (for a guard) near the owner.</summary>
        static bool IsValidEnemy(NpcCharacter pet, Character candidate, bool requireNearOwner)
        {
            if (candidate.IsDead || candidate.IsEvading || !ReferenceEquals(candidate.Playfield, pet.Playfield))
                return false;
            if (!Helpers.CombatRules.CanAttack(pet, candidate))
                return false;
            return !requireNearOwner || pet.Pet == null || Distance(candidate, pet.Pet.Owner) <= GuardRange;
        }

        public static void Forget(NpcCharacter pet)
        {
        }
    }

    sealed class PetLeaveNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PetLeaveNode(NpcBrain brain)
            : base("pet-leave")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-leave", _brain.Npc.Identity.Instance);
            NpcCharacter pet = _brain.Npc;
            PetAi.Forget(pet);
            string reason = _brain.Pet!.HasExpired(DateTime.UtcNow) ? "expired" : "owner gone";
            pet.Playfield?.GetService<PetService>()?.Dismiss(pet, reason);
            return _nodeState = NodeState.Success;
        }
    }

    sealed class PetRejoinOwnerNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PetRejoinOwnerNode(NpcBrain brain)
            : base("pet-rejoin-owner")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-rejoin", _brain.Npc.Identity.Instance);
            PetController pet = _brain.Pet!;
            pet.WarpRequested = false;

            // A waiting pet pulled back to its owner has left its post: it follows from here on.
            if (pet.Mode == PetMode.Wait)
                pet.Order(PetMode.Follow);

            // Pulled back mid-attack: the order is over; the pet drops its target and goes back to what it did before.
            if (pet.Mode == PetMode.Attack)
                pet.EndAttack();
            _brain.SetCurrentTarget(SmokeLounge.AOtomation.Messaging.GameData.Identity.None);

            _brain.StopFighting();
            _brain.WarpTo(PetAi.FollowPoint(_brain));
            return _nodeState = NodeState.Success;
        }
    }

    sealed class PetFightNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PetFightNode(NpcBrain brain)
            : base("pet-fight")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-fight", _brain.Npc.Identity.Instance);
            NpcCharacter npc = _brain.Npc;
            Character? enemy = _brain.ResolveCurrentTarget();
            if (enemy == null)
                return _nodeState = NodeState.Failure;

            if (npc.FightingTarget != enemy.Identity)
                npc.StartFighting(enemy.Identity, 0);

            if (_brain.CanAttackNow(enemy))
            {
                _brain.StopPathing();
                return _nodeState = NodeState.Running;
            }

            // Each pet closes in at its own spot around the enemy, so several pets do not stack on one point.
            int index = _brain.Pet!.Owner.OwnedPets.FormationIndex(npc);
            _brain.PathTo(PetFormation.AttackPoint(npc, enemy, index, PetAi.AttackReach));
            return _nodeState = NodeState.Running;
        }
    }

    /// <summary>
    /// Keeps the pet's charge (<see cref="PetAi.Charge"/>) healed: when the charge is hurt and a heal can land,
    /// the pet casts it and that is its action this tick. Otherwise it carries on with everything else. A heal
    /// order whose target is out of heal range ends, and the pet goes back to its owner and its default duty; the
    /// pet never runs off after a heal target.
    /// </summary>
    sealed class PetTendNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PetTendNode(NpcBrain brain)
            : base("pet-tend")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-tend", _brain.Npc.Identity.Instance);
            PetController pet = _brain.Pet!;
            Character? charge = PetAi.Charge(_brain);
            if (charge == null || PetAi.HealthPercent(charge) >= PetAi.TendBelowPercent)
                return _nodeState = NodeState.Failure;

            switch (_brain.TryHeal(charge))
            {
                case HealOrderResult.Cast:
                    _brain.StopPathing();
                    return _nodeState = NodeState.Success;
                case HealOrderResult.OutOfRange:
                case HealOrderResult.NoHeal:
                    // Out of range of an ordered target (or no heal to give): back to the owner and default duty.
                    // Out of range of the owner: following brings the pet back in range.
                    if (!ReferenceEquals(charge, pet.Owner))
                        pet.EndHeal();
                    return _nodeState = NodeState.Failure;
                default:
                    return _nodeState = NodeState.Failure;
            }
        }
    }

    sealed class PetWaitNode : BehaviourNode
    {
        readonly NpcBrain _brain;

        public PetWaitNode(NpcBrain brain)
            : base("pet-wait")
        {
            _brain = brain;
        }

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-wait", _brain.Npc.Identity.Instance);
            _brain.StopFighting();
            Vector3? spot = _brain.Pet!.WaitPosition;
            if (spot != null && Vector3.Abs(_brain.Npc.Position - spot) > PetAi.FollowStartMeters)
                _brain.PathTo(spot);
            else
                _brain.StopPathing();
            return _nodeState = NodeState.Success;
        }
    }

    sealed class PetFollowNode : BehaviourNode
    {
        readonly NpcBrain _brain;
        bool _moving;

        public PetFollowNode(NpcBrain brain)
            : base("pet-follow")
        {
            _brain = brain;
        }

        /// <summary>The owner counts as moving above this ground speed (m/s).</summary>
        const double OwnerMovingSpeed = 0.5;

        /// <summary>While the owner moves, the pet aims this far ahead of its spot (seconds of owner travel).</summary>
        const double LeadSeconds = 0.8;

        /// <summary>…and this much further when it has fallen behind, so it cuts in smoothly instead of trailing.</summary>
        const double CatchUpLeadSeconds = 0.5;

        /// <summary>Behind by more than this counts as having fallen behind.</summary>
        const double CatchUpMeters = 6.0;

        /// <summary>While leading, a path is only replaced once its destination drifts this far.</summary>
        const float DriftReplanMeters = 3.0f;

        public override NodeState Evaluate(float deltaTime)
        {
            TickStallWatch.Stage("node.pet-follow", _brain.Npc.Identity.Instance);
            _brain.StopFighting();
            PetController pet = _brain.Pet!;
            Vector3 spot = PetAi.FollowPoint(_brain);
            double distance = Vector3.Abs(_brain.Npc.Position - spot);
            (double vx, double vz) = pet.Owner.OwnedPets.OwnerVelocity(pet.Owner);
            bool ownerMoving = Math.Sqrt((vx * vx) + (vz * vz)) >= OwnerMovingSpeed;

            // Set off once clearly out of place; settle only once the owner has stopped and the pet is at its spot.
            // Stopping while the owner is still moving would only mean setting off again a moment later.
            if (!_moving && (distance > PetAi.FollowStartMeters || (ownerMoving && distance > PetAi.FollowStopMeters)))
                _moving = true;
            else if (_moving && !ownerMoving && distance <= PetAi.FollowStopMeters)
                _moving = false;

            if (_moving)
            {
                if (ownerMoving)
                {
                    // Aim where the spot will be, so the path end stays ahead and the pet never brakes into a
                    // point the owner has already left; re-plan only when that aim drifts.
                    double lead = LeadSeconds + (distance > CatchUpMeters ? CatchUpLeadSeconds : 0);
                    _brain.PathTo(PetAi.FollowPoint(_brain, vx * lead, vz * lead), DriftReplanMeters);
                }
                else
                {
                    _brain.PathTo(spot);
                }

                // No route to the spot itself: head for the owner instead of standing still.
                if (!_brain.Npc.Motor.HasPath && distance > PetAi.FollowStartMeters)
                    _brain.PathTo(pet.Owner.Position);
            }
            else
            {
                _brain.StopPathing();
            }
            return _nodeState = NodeState.Success;
        }

        public override void Reset() => _moving = false;
    }
}
