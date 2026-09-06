namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Logout/despawn durable write of the in-memory character aggregate (location + base stats).
    /// Inventory is flushed separately by <see cref="ZoneEngine_New.Core.Inventory.InventoryFlushService"/>.
    /// </summary>
    public sealed class CharacterSnapshotService
    {
        private readonly ICharacterRepository _characters;
        private readonly IStatRepository _stats;
        private readonly IZoneLogger _logger;

        public CharacterSnapshotService(
            ICharacterRepository characters,
            IStatRepository stats,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(characters);
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(logger);

            _characters = characters;
            _stats = stats;
            _logger = logger;
        }

        public void Commit(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            int characterId = player.Identity.Instance;
            if (characterId <= 0)
                return;

            Playfield? playfield = player.Playfield;
            if (playfield == null)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character snapshot skipped character={0}: no playfield",
                        characterId));
                return;
            }

            int playfieldId = playfield.Identity.Instance;
            if (playfieldId <= 0)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character snapshot skipped character={0}: playfield id {1}",
                        characterId,
                        playfieldId));
                return;
            }

            Vector3 position = player.Position;
            Quaternion heading = player.Rotation;
            var record = new CharacterRecord
            {
                Id = characterId,
                Playfield = playfieldId,
                X = position.xf,
                Y = position.yf,
                Z = position.zf,
                HeadingW = heading.wf,
                HeadingX = heading.xf,
                HeadingY = heading.yf,
                HeadingZ = heading.zf
            };

            List<StatRecord> stats = [];
            foreach (var entry in player.Stats.GetEntries())
            {
                if (StatCollection.IsUnset(entry.Base))
                    continue;

                stats.Add(
                    new StatRecord
                    {
                        StatId = (int)entry.Stat,
                        StatValue = entry.Base
                    });
            }

            _characters.SaveLocation(record, online: 0);
            _stats.UpsertForCharacter(characterId, stats);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Character snapshot character={0} playfield={1} pos=({2},{3},{4}) stats={5}",
                    characterId,
                    playfieldId,
                    position.xf,
                    position.yf,
                    position.zf,
                    stats.Count));
        }
    }
}
