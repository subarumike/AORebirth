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

            CharacterHydrationValidator.RequireValid(hydration);

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
                // Opponent count is runtime fight bookkeeping and Pets is which pet slots are taken right now: neither
                // is a saved character row (a stale stored Pets would block every summon).
                if (stat.StatId is (int)CharacterStat.NumberOfFightingOpponents or (int)CharacterStat.Pets)
                    continue;

                player.Stats.Set((CharacterStat)stat.StatId, stat.StatValue, StatDetail.Base);
            }

            ApplyXpThresholds(player);

            player.Inventory.Apply(hydration, character.Id, _items);

            player.UploadedNanoIds.Clear();
            foreach (int nanoId in hydration.UploadedNanoIds)
                player.TryAddUploadedNano(nanoId);

            RestoreActiveNanos(player, hydration);

            DateTime nowUtc = DateTime.UtcNow;
            foreach (SkillLockRecord record in hydration.SkillLocks)
                player.SkillLocks.Restore(record.StatId, new DateTime(record.ExpiresAtUtcTicks, DateTimeKind.Utc), nowUtc);
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

            // AlienXP is progress inside the alien level; AlienNextXP is the AlienXp.json step out of it.
            // A character that never earned alien XP has neither stored, and Unset would reach the client.
            if (StatCollection.IsUnset(player.Stats.Get(CharacterStat.AlienLevel)))
                player.Stats.Set(CharacterStat.AlienLevel, 0, StatDetail.Base);
            if (StatCollection.IsUnset(player.Stats.Get(CharacterStat.AlienXP)))
                player.Stats.Set(CharacterStat.AlienXP, 0, StatDetail.Base);

            int alienLevel = player.Stats.GetOrZero(CharacterStat.AlienLevel);
            int alienNext = _gameData.TryGetAlienXpLevel(alienLevel + 1, out AlienXpLevelEntry step) && step.NextLevelXp > 0
                ? step.NextLevelXp
                : 0;
            player.Stats.Set(CharacterStat.AlienNextXP, alienNext, StatDetail.Base);
        }

        int GetNextXp(int level)
        {
            if (!_gameData.TryGetXpLevel(level, out XpLevelEntry entry) || entry.NextLevelXp <= 0)
                return 0;

            return entry.FloorXp + entry.NextLevelXp;
        }
    }
}
