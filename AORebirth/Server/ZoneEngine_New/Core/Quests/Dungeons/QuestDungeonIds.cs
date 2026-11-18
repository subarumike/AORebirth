namespace ZoneEngine_New.Core.Quests.Dungeons
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A dungeon quest takes one number from the durable item-instance allocator (unique forever). Its quest id and
    /// its dungeon's Playfield2 id are both derived from that number, so login can find the quest from the stored
    /// playfield without any lookup table, and two quests can never share a dungeon id.
    /// </summary>
    public static class QuestDungeonIds
    {
        /// <summary>First dungeon Playfield2 id: above every RDB playfield and the legacy mission lease range.</summary>
        public const int FirstPlayfield = 0x00200000;

        // Discriminator already stored in generatedquests. Its value is part of the persisted identifier format.
        const char PersistedDungeonDiscriminator = 'D';

        /// <summary>Dungeon ids run from <see cref="FirstPlayfield"/> up to the proxy playfield block (<see cref="ZoneEngine_New.Core.Playfield.ProxyPlayfieldIds.First"/>).</summary>
        public static bool IsDungeonPlayfield(int playfieldId)
            => playfieldId >= FirstPlayfield && playfieldId < ZoneEngine_New.Core.Playfield.ProxyPlayfieldIds.First;

        public static bool TryCreate(int number, out string questId, out int playfieldId)
        {
            questId = string.Empty;
            playfieldId = 0;
            if (number <= 0 || number > int.MaxValue - FirstPlayfield)
                return false;

            questId = FormatQuestId(number);
            playfieldId = FirstPlayfield + number;
            return true;
        }

        public static bool TryGetQuestId(int playfieldId, out string questId)
        {
            questId = string.Empty;
            if (!IsDungeonPlayfield(playfieldId))
                return false;

            questId = FormatQuestId(playfieldId - FirstPlayfield);
            return true;
        }

        public static bool TryGetPlayfield(string questId, out int playfieldId)
        {
            playfieldId = 0;
            if (string.IsNullOrEmpty(questId) || questId[0] != PersistedDungeonDiscriminator
                || !int.TryParse(questId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return false;

            return TryCreate(number, out _, out playfieldId);
        }

        static string FormatQuestId(int number)
            => PersistedDungeonDiscriminator + number.ToString(CultureInfo.InvariantCulture);
    }
}
