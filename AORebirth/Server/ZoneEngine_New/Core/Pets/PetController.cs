namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A summoned pet's owner-facing state: who owns it, what it is, how long it lasts, and what it has been told
    /// to do. The pet's brain reads it every tick; commands and lifecycle hooks write it. Everything here runs on
    /// the owner's (and so the pet's) playfield thread.
    /// </summary>
    public sealed class PetController
    {
        public PetController(Character owner, int type, string hash, int level, DateTime? expiresUtc,
            IReadOnlyList<ItemRequirement>? summonRequirements = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Type = type;
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            Level = level;
            ExpiresUtc = expiresUtc;
            SummonRequirements = summonRequirements ?? [];
            Mode = owner is Player ? PetMode.Guard : PetMode.Assist;
        }

        /// <summary>ToUse rows of the nano or item that summoned the pet: its owner must keep within 80% of them.</summary>
        public IReadOnlyList<ItemRequirement> SummonRequirements { get; }

        /// <summary>
        /// The owner's skills fell under 80% of <see cref="SummonRequirements"/>: the pet follows and ignores every
        /// command until they recover.
        /// </summary>
        public bool IsOverEquipped { get; internal set; }

        public Character Owner { get; }

        /// <summary>Pet type (see <see cref="PetTypes"/>).</summary>
        public int Type { get; }

        public string Hash { get; }

        public int Level { get; }

        /// <summary>When a timed pet leaves; null for a pet that stays until dismissed.</summary>
        public DateTime? ExpiresUtc { get; }

        public PetMode Mode { get; private set; }

        /// <summary>What an attack order returns to once its target is gone.</summary>
        public PetMode ModeBeforeAttack { get; private set; } = PetMode.Guard;

        /// <summary>The target of an attack order.</summary>
        public Identity AttackTarget { get; private set; } = Identity.None;

        /// <summary>
        /// Who a heal order put the pet on, until that character is out of heal range or gone. Without one a heal
        /// pet looks after its owner.
        /// </summary>
        public Identity HealTarget { get; private set; } = Identity.None;

        /// <summary>Where a waiting pet stays.</summary>
        public Vector3? WaitPosition { get; private set; }

        public bool IsPlayerPet => Owner is Player;

        /// <summary>Set when the pet cannot follow its owner on foot; it rejoins the owner on the next tick.</summary>
        internal bool WarpRequested { get; set; }

        /// <summary>
        /// Last walking distance from the pet to its owner (navmesh route length; +infinity when no route), and
        /// where both stood when it was measured. See PetAi.OwnerDistance.
        /// </summary>
        internal double CachedOwnerDistance { get; set; } = double.NaN;

        internal DateTime OwnerDistanceUtc { get; set; }

        internal Vector3? OwnerDistancePetAt { get; set; }

        internal Vector3? OwnerDistanceOwnerAt { get; set; }

        internal void Order(PetMode mode, Vector3? at = null)
        {
            if (mode == PetMode.Attack)
                throw new ArgumentException("Use Attack() for attack orders.", nameof(mode));

            Mode = mode;
            AttackTarget = Identity.None;
            WaitPosition = mode == PetMode.Wait && at != null ? new Vector3(at.x, at.y, at.z) : null;
        }

        internal void Attack(Identity target)
        {
            if (Mode != PetMode.Attack)
                ModeBeforeAttack = Mode == PetMode.Wait ? PetMode.Guard : Mode;
            Mode = PetMode.Attack;
            AttackTarget = target;
            WaitPosition = null;
        }

        /// <summary>The attack order's target is gone: back to what the pet was doing before.</summary>
        internal void EndAttack()
        {
            if (Mode != PetMode.Attack)
                return;
            Mode = ModeBeforeAttack;
            AttackTarget = Identity.None;
        }

        internal void Heal(Identity target) => HealTarget = target == Owner.Identity ? Identity.None : target;

        internal void EndHeal() => HealTarget = Identity.None;

        public bool HasExpired(DateTime nowUtc) => ExpiresUtc is DateTime expires && nowUtc >= expires;
    }

    /// <summary>A pet carried through a zone change: it is despawned on the way out and summoned again on arrival.</summary>
    public sealed record PetStash(string Hash, int Type, int Level, DateTime? ExpiresUtc, PetMode Mode, int HealthPercent,
        int NanoPercent, IReadOnlyList<ItemRequirement> SummonRequirements);

    /// <summary>The pets a character owns, one per pet slot, in summon order (which is also their formation order).</summary>
    public sealed class OwnedPets
    {
        readonly List<NpcCharacter> _pets = new();
        readonly List<PetStash> _stash = new();

        public IReadOnlyList<NpcCharacter> All => _pets;

        public int Count => _pets.Count;

        /// <summary>Pets waiting to be summoned again once their owner arrives on the next playfield.</summary>
        internal List<PetStash> Stash => _stash;

        internal void Add(NpcCharacter pet)
        {
            if (!_pets.Contains(pet))
                _pets.Add(pet);
        }

        internal bool Remove(NpcCharacter pet) => _pets.Remove(pet);

        /// <summary>The owned pet in <paramref name="petType"/>'s slot, if any.</summary>
        public NpcCharacter? InSlotOf(int petType)
        {
            int slot = PetTypes.Slot(petType);
            foreach (NpcCharacter pet in _pets)
            {
                if (pet.Pet != null && PetTypes.Slot(pet.Pet.Type) == slot)
                    return pet;
            }

            return null;
        }

        /// <summary>The Pets stat value for these pets: one bit per occupied slot.</summary>
        public int Flags()
        {
            int flags = 0;
            foreach (NpcCharacter pet in _pets)
            {
                if (pet.Pet != null)
                    flags |= PetTypes.Flag(pet.Pet.Type);
            }

            return flags;
        }

        /// <summary>This pet's place in the formation (its index among the owner's living pets).</summary>
        public int FormationIndex(NpcCharacter pet) => Math.Max(0, _pets.IndexOf(pet));

        /// <summary>The owner must move this far before the formation turns with it.</summary>
        public const double FormationTurnAfterMeters = 0.5;

        Vector3? _formationAnchor;
        double _formationYaw;

        /// <summary>Owner velocity samples closer together than this are not used (too noisy).</summary>
        const double VelocitySampleSeconds = 0.1;

        /// <summary>Weight of the newest sample in the smoothed owner velocity.</summary>
        const double VelocitySmoothing = 0.5;

        Vector3? _velocitySamplePos;
        DateTime _velocitySampleUtc;
        double _velocityX;
        double _velocityZ;

        /// <summary>
        /// The owner's ground velocity (meters per second), from how far it moved between samples, smoothed. Works
        /// for any owner (player movement is client-driven, so there is no motor velocity to read).
        /// </summary>
        internal (double X, double Z) OwnerVelocity(Character owner)
        {
            DateTime now = DateTime.UtcNow;
            Vector3 at = owner.Position;
            if (_velocitySamplePos == null)
            {
                _velocitySamplePos = new Vector3(at.x, at.y, at.z);
                _velocitySampleUtc = now;
                return (0, 0);
            }

            double dt = (now - _velocitySampleUtc).TotalSeconds;
            if (dt >= VelocitySampleSeconds)
            {
                double vx = (at.x - _velocitySamplePos.x) / dt;
                double vz = (at.z - _velocitySamplePos.z) / dt;
                _velocityX += (vx - _velocityX) * VelocitySmoothing;
                _velocityZ += (vz - _velocityZ) * VelocitySmoothing;
                _velocitySamplePos = new Vector3(at.x, at.y, at.z);
                _velocitySampleUtc = now;
            }

            return (_velocityX, _velocityZ);
        }

        /// <summary>
        /// The heading the formation is laid out on. It follows the owner's heading only once the owner has moved
        /// <see cref="FormationTurnAfterMeters"/>; turning on the spot leaves the pets where they are.
        /// </summary>
        internal double FormationYaw(Character owner)
        {
            Vector3 at = owner.Position;
            if (_formationAnchor == null || Vector3.Abs(at - _formationAnchor) >= FormationTurnAfterMeters)
            {
                _formationAnchor = new Vector3(at.x, at.y, at.z);
                AORebirth.Core.Vector.Quaternion q = owner.Rotation;
                _formationYaw = 2.0 * Math.Atan2(q.y, q.w);
            }

            return _formationYaw;
        }
    }

    /// <summary>
    /// Where each pet walks when it follows its owner: fixed slots behind and to either side, turned with the
    /// owner's heading, so several pets spread out instead of piling onto one spot.
    /// </summary>
    public static class PetFormation
    {
        /// <summary>(sideways, behind) offsets in meters, in fill order.</summary>
        static readonly (double Side, double Back)[] Slots =
        [
            (-1.8, 1.6),
            (1.8, 1.6),
            (0.0, 3.0),
            (-3.0, 3.2),
            (3.0, 3.2),
            (-1.6, 4.6),
            (1.6, 4.6)
        ];

        /// <summary>The follow point of formation slot <paramref name="index"/> around <paramref name="owner"/>.</summary>
        public static Vector3 PointFor(Character owner, int index)
        {
            ArgumentNullException.ThrowIfNull(owner);
            (double side, double back) = Slots[Math.Clamp(index, 0, Slots.Length - 1)];
            if (index >= Slots.Length)
                back += 1.5 * (index - Slots.Length + 1);

            double yaw = owner.OwnedPets.FormationYaw(owner);
            double forwardX = Math.Sin(yaw);
            double forwardZ = Math.Cos(yaw);
            double rightX = Math.Cos(yaw);
            double rightZ = -Math.Sin(yaw);
            Vector3 at = owner.Position;
            return new Vector3(
                at.x - (forwardX * back) + (rightX * side),
                at.y,
                at.z - (forwardZ * back) + (rightZ * side));
        }

        /// <summary>
        /// A point <paramref name="reach"/> meters from <paramref name="target"/>, spread around it by the pet's
        /// formation index so several pets do not stack on one spot while they fight.
        /// </summary>
        public static Vector3 AttackPoint(Character pet, Character target, int index, double reach)
        {
            double dx = pet.Position.x - target.Position.x;
            double dz = pet.Position.z - target.Position.z;
            double baseAngle = Math.Atan2(dx, dz);
            double spread = (index % 2 == 0 ? 1 : -1) * ((index + 1) / 2) * (Math.PI / 5);
            double angle = baseAngle + spread;
            return new Vector3(
                target.Position.x + (Math.Sin(angle) * reach),
                target.Position.y,
                target.Position.z + (Math.Cos(angle) * reach));
        }

    }
}
