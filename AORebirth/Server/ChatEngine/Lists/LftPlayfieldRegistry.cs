namespace ChatEngine.Lists
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Live playfield ids pushed from ZoneEngine over ISCom.
    /// LFT Location "Not found" happens when DB playfield is 0/stale.
    /// </summary>
    public static class LftPlayfieldRegistry
    {
        public const string PlayfieldCommandPrefix = "#aorebirth-pf";

        private static readonly ConcurrentDictionary<uint, int> Playfields =
            new ConcurrentDictionary<uint, int>();

        public static void Set(uint characterId, int playfieldId)
        {
            if (characterId == 0 || playfieldId <= 0)
            {
                return;
            }

            Playfields[characterId] = playfieldId;
        }

        public static bool TryGet(uint characterId, out int playfieldId)
        {
            return Playfields.TryGetValue(characterId, out playfieldId) && playfieldId > 0;
        }

        public static void Remove(uint characterId)
        {
            int ignored;
            Playfields.TryRemove(characterId, out ignored);
        }
    }
}
