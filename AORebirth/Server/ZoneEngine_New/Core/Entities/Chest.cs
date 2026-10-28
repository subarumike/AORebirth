namespace ZoneEngine_New.Core.Entities
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Treasure chest dynel. Shares open/loot flow with <see cref="Corpse"/> via <see cref="LootableDynel"/>.
    /// A locked chest cannot be opened until a Break and Enter item unlocks it.
    /// </summary>
    public class Chest : LootableDynel, IBreakAndEnterTarget
    {
        public const int LootCapacity = 21;

        float _collisionRadius;

        public Chest(Identity identity, int lootCapacity = LootCapacity, int lockDifficulty = 0)
            : base(identity, IdentityType.Container, lootCapacity)
        {
            LockDifficulty = Math.Max(0, lockDifficulty);
            IsLocked = LockDifficulty > 0;
        }

        public bool IsLocked { get; private set; }

        public object LootGate { get; } = new();

        public float CollisionRadius
        {
            get => _collisionRadius;
            set
            {
                if (!float.IsFinite(value) || value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _collisionRadius = value;
            }
        }

        public override double GetCollisionRadius() => CollisionRadius;

        public ChestSpawnDefinition? SpawnDefinition { get; set; }

        public Func<Player, bool>? AccessValidator { get; set; }

        /// <summary>Reloads authoritative durable contents before each new open, never on a close toggle.</summary>
        public Func<Chest, bool>? RefreshLoot { get; set; }

        /// <summary>Runs after a successful closed-to-open transition and the existing contents response.</summary>
        public event Action<Player>? AfterOpen;

        /// <summary>Generated mission chests bind the current RK policy; generic containers retain their own rules.</summary>
        public Func<Item, bool>? LootItemValidator { get; set; }

        public bool CanDistribute(Item item) => LootItemValidator?.Invoke(item) != false;

        /// <summary>Stat 299 a lockable chest must carry for the client to allow a lock pick on it.</summary>
        public int LockDifficulty { get; }

        public override MessageBody BuildSpawnMessage()
        {
            ChestSpawnDefinition definition = SpawnDefinition
                ?? throw new InvalidOperationException("Chest appearance has not been configured.");
            definition.Validate();
            var stats = new GameTuple<CharacterStat, uint>[definition.Stats.Length];
            for (int i = 0; i < stats.Length; i++)
                stats[i] = new GameTuple<CharacterStat, uint>
                {
                    Value1 = (CharacterStat)definition.Stats[i].StatId,
                    Value2 = definition.Stats[i].Value
                };

            // Retail mission-20261011-004128-b8e7bf2e, ordinal 480: positioned common prefix,
            // empty optional blob, lockable marker/list and chest marker; contents are a separate packet.
            return new ChestItemFullUpdateMessage
            {
                Identity = Identity,
                Unknown = 0,
                Unknown1 = 11,
                Owner = Identity.None,
                Coordinate = Position,
                Heading = Rotation,
                PlayfieldId = Playfield?.ClientPlayfieldId ?? 0,
                StateMachine = new Identity
                {
                    Type = (IdentityType)definition.StateMachineType,
                    Instance = definition.StateMachineInstance
                },
                Unknown5 = definition.HeaderStatBytes,
                Stats = stats,
                Unknown6 = 0,
                Unknown7 = 2,
                Unknown8 = definition.LockableValue,
                UnknownArray = [],
                Unknown9 = 3
            };
        }

        public override bool TryUse(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            lock (LootGate)
            {
                if (!CanInteract(player))
                    return false;
                if (!IsOpen && (!CanOpenLoot(player) || RefreshLoot?.Invoke(this) == false))
                    return false;
                bool wasOpen = IsOpen;
                bool used = base.TryUse(player);
                if (used && !wasOpen && IsOpen)
                    AfterOpen?.Invoke(player);
                return used;
            }
        }

        protected override bool CanOpenLoot(Player player) => !IsLocked && CanInteract(player);

        public bool CanTransferLoot(Player player)
            => OpenerIdentity == player.Identity && !IsLocked && CanInteract(player);

        public override void Tick(double deltaTime)
        {
            lock (LootGate)
                base.Tick(deltaTime);
        }

        bool CanInteract(Player player)
            => SpawnDefinition != null && Playfield != null && !player.IsDead && !player.IsPersistenceQuarantined
                && player.Session?.State == SessionState.InPlay && ReferenceEquals(player.Session.Player, player)
                && ReferenceEquals(player.Playfield, Playfield) && GetEdgeDistanceTo(player) <= OpenRange
                && Playfield.GetRequiredService<DynelRegistry>().TryGet(Identity, out Dynel? registered)
                && ReferenceEquals(registered, this) && AccessValidator?.Invoke(player) != false;

        /// <summary>A Break and Enter item used on the locked chest: it unlocks (and the client opens it) for the user.</summary>
        public BreakAndEnterResult TryBreakAndEnter(Player player, int pickRating)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!IsLocked || Playfield == null || player.IsDead || !ReferenceEquals(player.Playfield, Playfield)
                || GetEdgeDistanceTo(player) > OpenRange)
                return BreakAndEnterResult.Refused;

            if (pickRating < LockDifficulty)
            {
                BreakAndEnterActions.SendFailed(this, player);
                return BreakAndEnterResult.Failed;
            }

            IsLocked = false;
            Playfield.GetRequiredService<PlayfieldLocality>().Announce(this,
                BreakAndEnterActions.Result(Identity, player.Identity, BreakAndEnterActions.Unlocked), includeSelf: true);
            return BreakAndEnterResult.Unlocked;
        }
    }
}
