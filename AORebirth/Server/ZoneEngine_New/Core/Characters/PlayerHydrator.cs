namespace ZoneEngine_New.Core.Characters
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    public sealed class PlayerHydrator
    {
        private readonly IItemBuilder _items;
        private readonly IGameData _gameData;

        public PlayerHydrator(IItemBuilder items, IGameData gameData)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(gameData);
            _items = items;
            _gameData = gameData;
        }

        public void Apply(Player player, CharacterHydrationResult hydration)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(hydration);

            CharacterRecord character = hydration.Character;
            player.Name = character.Name;
            player.FirstName = character.FirstName ?? string.Empty;
            player.LastName = character.LastName ?? string.Empty;
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

            ApplyXpThresholds(player);

            player.Inventory.Apply(hydration, character.Id, _items);

            player.UploadedNanoIds.Clear();
            foreach (int nanoId in hydration.UploadedNanoIds)
                player.TryAddUploadedNano(nanoId);

            RestoreActiveNanos(player, hydration);
        }

        /// <summary>
        /// Puts stored NCU back. Buffs whose deadline passed while the character was offline are
        /// simply not restored, and the next flush drops their rows.
        /// </summary>
        void RestoreActiveNanos(Player player, CharacterHydrationResult hydration)
        {
            DateTime nowUtc = DateTime.UtcNow;
            foreach (ActiveNanoRecord record in hydration.ActiveNanos)
            {
                if (record.NanoId <= 0)
                    continue;

                var expiresAtUtc = new DateTime(record.ExpiresAtUtcTicks, DateTimeKind.Utc);
                if (expiresAtUtc <= nowUtc)
                    continue;

                NanoSpell spell = NanoSpell.From(
                    _items.CreateTemplate(record.NanoId, record.NanoId, quality: 1));
                player.TryRestoreBuff(spell, player.Identity, record.NanoInstance, expiresAtUtc);
            }
        }

        void ApplyXpThresholds(Player player)
        {
            int level = player.Stats.GetOrOne(CharacterStat.Level);
            player.Stats.Set(CharacterStat.NextXP, GetNextXp(level), StatDetail.Base);
            player.Stats.Set(CharacterStat.LastXP, level > 1 ? GetNextXp(level - 1) : 0, StatDetail.Base);
        }

        int GetNextXp(int level)
        {
            if (!_gameData.TryGetXpLevel(level, out XpLevelEntry entry) || entry.NextLevelXp <= 0)
                return 0;

            return entry.FloorXp + entry.NextLevelXp;
        }
    }
}
