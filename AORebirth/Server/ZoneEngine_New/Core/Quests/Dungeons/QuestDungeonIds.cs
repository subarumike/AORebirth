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

        public const string QuestIdPrefix = "D";

        public static bool IsDungeonPlayfield(int playfieldId) => playfieldId >= FirstPlayfield;

        public static bool TryCreate(int number, out string questId, out int playfieldId)
        {
            questId = string.Empty;
            playfieldId = 0;
            if (number <= 0 || number > int.MaxValue - FirstPlayfield)
                return false;

            questId = QuestIdPrefix + number.ToString(CultureInfo.InvariantCulture);
            playfieldId = FirstPlayfield + number;
            return true;
        }

        public static bool TryGetQuestId(int playfieldId, out string questId)
        {
            questId = string.Empty;
            if (!IsDungeonPlayfield(playfieldId))
                return false;

            questId = QuestIdPrefix + (playfieldId - FirstPlayfield).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public static bool TryGetPlayfield(string questId, out int playfieldId)
        {
            playfieldId = 0;
            if (string.IsNullOrEmpty(questId) || !questId.StartsWith(QuestIdPrefix, System.StringComparison.Ordinal)
                || !int.TryParse(questId.AsSpan(QuestIdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return false;

            return TryCreate(number, out _, out playfieldId);
        }
    }
}
