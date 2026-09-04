namespace ZoneEngine_New.Core.Entities
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    /// <summary>
    /// Treasure chest dynel. Shares open/loot flow with <see cref="Corpse"/> via <see cref="LootableDynel"/>.
    /// </summary>
    public class Chest : LootableDynel
    {
        public const int LootCapacity = 21;

        public Chest(Identity identity, int lootCapacity = LootCapacity)
            : base(identity, IdentityType.Container, lootCapacity)
        {
        }

        public override MessageBody BuildSpawnMessage()
        {
            throw new NotSupportedException("Chest spawn packet is not implemented yet.");
        }
    }
}
