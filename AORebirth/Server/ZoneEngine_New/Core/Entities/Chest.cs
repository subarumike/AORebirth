namespace ZoneEngine_New.Core.Entities
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// Treasure chest dynel. Shares open/loot flow with <see cref="Corpse"/> via <see cref="LootableDynel"/>.
    /// A locked chest cannot be opened until a Break and Enter item unlocks it.
    /// </summary>
    public class Chest : LootableDynel, IBreakAndEnterTarget
    {
        public const int LootCapacity = 21;

        public Chest(Identity identity, int lootCapacity = LootCapacity, int lockDifficulty = 0)
            : base(identity, IdentityType.Container, lootCapacity)
        {
            LockDifficulty = Math.Max(0, lockDifficulty);
            IsLocked = LockDifficulty > 0;
        }

        public bool IsLocked { get; private set; }

        /// <summary>Stat 299 a lockable chest must carry for the client to allow a lock pick on it.</summary>
        public int LockDifficulty { get; }

        public override MessageBody BuildSpawnMessage()
        {
            throw new NotSupportedException("Chest spawn packet is not implemented yet.");
        }

        protected override bool CanOpenLoot(Player player) => !IsLocked && base.CanOpenLoot(player);

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
