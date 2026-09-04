namespace ZoneEngine_New.Core.Characters
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    public sealed class PlayerHydrator
    {
        private readonly IItemBuilder _items;

        public PlayerHydrator(IItemBuilder items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _items = items;
        }

        public void Apply(Player player, CharacterHydrationResult hydration)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(hydration);

            CharacterRecord character = hydration.Character;
            player.Name = character.Name;
            player.Position = new Vector3(character.X, character.Y, character.Z);
            player.Rotation = new Quaternion(
                character.HeadingX,
                character.HeadingY,
                character.HeadingZ,
                character.HeadingW);

            foreach (StatRecord stat in hydration.Stats)
            {
                player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base);
            }

            player.Inventory.Apply(hydration, character.Id, _items);
        }
    }
}
