namespace ChatEngine.Lists
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Live playfield information pushed from ZoneEngine over ISCom.
    ///
    /// Expansion:
    /// 1 = Rubi-Ka
    /// 2 = Shadowlands
    /// </summary>
    public static class LftPlayfieldRegistry
    {
        public const string PlayfieldCommandPrefix = "#aorebirth-pf";

        private static readonly ConcurrentDictionary<uint, int> Playfields =
            new ConcurrentDictionary<uint, int>();

        private static readonly ConcurrentDictionary<uint, int> Expansions =
            new ConcurrentDictionary<uint, int>();

        public static void Set(
            uint characterId,
            int playfieldId,
            int expansion)
        {
            if (characterId == 0 || playfieldId <= 0)
            {
                return;
            }

            Playfields[characterId] = playfieldId;

            /*
             * Playfield expansion:
             *
             * 1 = Rubi-Ka
             * 2 = Shadowlands
             */
            if (expansion == 1 || expansion == 2)
            {
                Expansions[characterId] = expansion;
            }
        }

        public static bool TryGet(
            uint characterId,
            out int playfieldId)
        {
            return Playfields.TryGetValue(
                       characterId,
                       out playfieldId)
                   && playfieldId > 0;
        }

        public static bool TryGetExpansion(
            uint characterId,
            out int expansion)
        {
            return Expansions.TryGetValue(
                       characterId,
                       out expansion)
                   && (expansion == 1 || expansion == 2);
        }

        public static void Remove(uint characterId)
        {
            int ignored;

            Playfields.TryRemove(
                characterId,
                out ignored);

            Expansions.TryRemove(
                characterId,
                out ignored);
        }
    }
}
