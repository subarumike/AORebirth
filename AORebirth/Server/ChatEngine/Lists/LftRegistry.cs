namespace ChatEngine.Lists
{
    using System.Collections.Generic;

    /// <summary>
    /// In-memory Looking for Team registrations (chat-server only).
    /// </summary>
    public static class LftRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<uint, string> CommentsByCharacterId = new Dictionary<uint, string>();

        public static void Upsert(uint characterId, string comment)
        {
            if (characterId == 0)
            {
                return;
            }

            lock (Sync)
            {
                if (string.IsNullOrEmpty(comment))
                {
                    CommentsByCharacterId.Remove(characterId);
                    return;
                }

                CommentsByCharacterId[characterId] = comment;
            }
        }

        public static void Remove(uint characterId)
        {
            if (characterId == 0)
            {
                return;
            }

            lock (Sync)
            {
                CommentsByCharacterId.Remove(characterId);
            }
        }

        public static bool TryGetComment(uint characterId, out string comment)
        {
            lock (Sync)
            {
                return CommentsByCharacterId.TryGetValue(characterId, out comment);
            }
        }

        public static List<KeyValuePair<uint, string>> Snapshot()
        {
            lock (Sync)
            {
                return new List<KeyValuePair<uint, string>>(CommentsByCharacterId);
            }
        }
    }
}
