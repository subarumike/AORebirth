namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Globalization;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Logging;

    public sealed class CharacterHydrationService : ICharacterHydrationService
    {
        private readonly ICharacterRepository _characters;
        private readonly IStatRepository _stats;
        private readonly IInventoryRepository _inventory;
        private readonly IZoneLogger _logger;

        public CharacterHydrationService(
            ICharacterRepository characters,
            IStatRepository stats,
            IInventoryRepository inventory,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(inventory);
            ArgumentNullException.ThrowIfNull(logger);

            _characters = characters;
            _stats = stats;
            _inventory = inventory;
            _logger = logger;
        }

        public CharacterHydrationResult? LoadForLogin(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            CharacterRecord? character = _characters.GetById(characterId);
            if (character == null || character.Playfield <= 0)
            {
                return null;
            }

            var result = new CharacterHydrationResult
            {
                Character = character,
                Stats = _stats.GetForCharacter(characterId),
                Items = _inventory.GetItemsForCharacter(characterId),
                InstancedItems = _inventory.GetInstancedItemsForCharacter(characterId)
            };

            if (!result.IsSpawnReady)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character hydration incomplete for {0}: stats={1}",
                        characterId,
                        result.Stats.Count));
                return null;
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Character hydrated id={0} stats={1} items={2} instanced={3}",
                    characterId,
                    result.Stats.Count,
                    result.Items.Count,
                    result.InstancedItems.Count));

            return result;
        }
    }
}
