namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Globalization;
    using System.Collections.Generic;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Logging;

    public sealed class CharacterHydrationService : ICharacterHydrationService
    {
        private readonly ICharacterRepository _characters;
        private readonly IStatRepository _stats;
        private readonly IInventoryRepository _inventory;
        private readonly IUploadedNanoRepository _uploadedNanos;
        private readonly IZoneLogger _logger;

        public CharacterHydrationService(
            ICharacterRepository characters,
            IStatRepository stats,
            IInventoryRepository inventory,
            IUploadedNanoRepository uploadedNanos,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(inventory);
            ArgumentNullException.ThrowIfNull(uploadedNanos);
            ArgumentNullException.ThrowIfNull(logger);

            _characters = characters;
            _stats = stats;
            _inventory = inventory;
            _uploadedNanos = uploadedNanos;
            _logger = logger;
        }

        public CharacterHydrationResult? LoadForLogin(int characterId)
        {
            if (characterId <= 0)
                return null;

            CharacterRecord? character = _characters.GetById(characterId);
            if (character == null || character.Playfield <= 0)
                return null;

            var result = new CharacterHydrationResult
            {
                Character = character,
                Stats = RestoreLegacyDefaults(_stats.GetForCharacter(characterId)),
                Items = _inventory.GetCarriedItems(characterId),
                UploadedNanoIds = _uploadedNanos.GetForCharacter(characterId)
            };

            CharacterHydrationValidationResult validation = CharacterHydrationValidator.Validate(result);
            if (!validation.IsValid)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character hydration incomplete for {0}: stats={1} errors={2}",
                        characterId,
                        result.Stats.Count,
                        string.Join(",", validation.Errors)));
                return null;
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Character hydrated id={0} stats={1} items={2} nanos={3}",
                    characterId,
                    result.Stats.Count,
                    result.Items.Count,
                    result.UploadedNanoIds.Count));

            return result;
        }

        internal static IReadOnlyList<StatRecord> RestoreLegacyDefaults(IReadOnlyList<StatRecord> persisted)
        {
            // Legacy Stats.Write omits default-valued rows. Restore only the four
            // evidenced sparse fields; explicit values, duplicates and sentinels
            // still reach the validator unchanged. No database writes occur here.
            var result = new List<StatRecord>(persisted);
            // Exact Stats/StatNamesDefaults values; no general fallback for
            // missing identity, appearance, primary abilities or current vitals.
            foreach ((CharacterStat stat, int value) in new[]
            {
                (CharacterStat.Race, 1), (CharacterStat.VisualFlags, 31),
                (CharacterStat.Side, 0), (CharacterStat.RunSpeed, 5)
            })
                if (!persisted.Any(row => row.StatId == (int)stat))
                    result.Add(new StatRecord { StatId = (int)stat, StatValue = value });
            return result;
        }
    }
}
