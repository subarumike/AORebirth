namespace ZoneEngine_New.Core.Quests
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Mission bits 0-639: character flags packed 32 to a stat. Completing a quest sets its
    /// <see cref="QuestTemplate.MissionBit"/>; items set, clear and test the same stats with SetFlag, ClearFlag and
    /// BitAnd requirements. Bits 576 and up are in server-only stats the client never sees.
    /// </summary>
    public static class MissionBits
    {
        public const int Count = 640;

        /// <summary>The stat holding each run of 32 bits, in bit order.</summary>
        static readonly CharacterStat[] Stats =
        [
            CharacterStat.MissionBits1,          // 256
            CharacterStat.MissionBits2,          // 257
            CharacterStat.MissionBits3,          // 303
            CharacterStat.MissionBits4,          // 432
            CharacterStat.MissionBits5,          // 65
            CharacterStat.MissionBits6,          // 66
            CharacterStat.MissionBits7,          // 67
            CharacterStat.MissionBits8,          // 544
            CharacterStat.MissionBits9,          // 545
            CharacterStat.MissionBits10,         // 617
            CharacterStat.MissionBits11,         // 618
            CharacterStat.MissionBits12,         // 619
            CharacterStat.MissionBits13,         // 198
            CharacterStat.MissionBits14,         // 685
            CharacterStat.MissionBits15,         // 686
            CharacterStat.MissionBits16,         // 692
            CharacterStat.MissionBits17,         // 693
            CharacterStat.MissionBits18,         // 694
            CharacterStat.MissionBits19,         // server-only
            CharacterStat.MissionBits20          // server-only
        ];

        public static bool IsValid(int bit) => bit >= 0 && bit < Count;

        /// <summary>The stat and mask holding <paramref name="bit"/>.</summary>
        public static (CharacterStat Stat, int Mask) Locate(int bit)
        {
            if (!IsValid(bit))
                throw new ArgumentOutOfRangeException(nameof(bit), bit, "Mission bits are 0-" + (Count - 1));

            return (Stats[bit >> 5], 1 << (bit & 31));
        }

        /// <summary>Full value, so a flag held by worn gear counts as it does for item requirements.</summary>
        public static bool Has(StatCollection stats, int bit)
        {
            (CharacterStat stat, int mask) = Locate(bit);
            return (stats.GetOrZero(stat) & mask) != 0;
        }

        /// <summary>Sets the bit in the stored value and queues the stat for the client.</summary>
        public static void Set(StatCollection stats, int bit)
        {
            (CharacterStat stat, int mask) = Locate(bit);
            stats.Set(stat, stats.GetOrZero(stat, StatDetail.Base) | mask, StatDetail.Base, dirty: true);
        }
    }
}
