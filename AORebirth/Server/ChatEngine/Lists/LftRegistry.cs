namespace ChatEngine.Lists
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// In-memory Looking for Team registrations (chat-server only).
    /// 05DC = listed (the comment is optional); 05DD = unlisted.
    /// Ticking the box sends 05DD then 05DC, so an empty 05DC is a tick without a comment.
    /// </summary>
    public static class LftRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<uint, string> ListedByCharacterId = new Dictionary<uint, string>();

        /// <summary>
        /// Ignore Personal Description echo right after chat login (checkbox still off).
        /// </summary>
        private static readonly TimeSpan LoginEchoGrace = TimeSpan.FromSeconds(20);

        public enum ApplyResult
        {
            ListedComment,

            UpdatedComment,

            IgnoredLoginEcho
        }

        /// <summary>
        /// List the character or update its comment; an empty comment still lists.
        /// </summary>
        public static ApplyResult Apply(uint characterId, string comment, DateTime chatAuthUtc)
        {
            if (characterId == 0)
            {
                return ApplyResult.IgnoredLoginEcho;
            }

            string trimmed = string.IsNullOrWhiteSpace(comment) ? string.Empty : comment.Trim();

            lock (Sync)
            {
                if (ListedByCharacterId.ContainsKey(characterId))
                {
                    ListedByCharacterId[characterId] = trimmed;
                    return ApplyResult.UpdatedComment;
                }

                // Not listed yet: ignore description echo in the first seconds after chat login.
                if (chatAuthUtc != default(DateTime)
                    && DateTime.UtcNow - chatAuthUtc < LoginEchoGrace)
                {
                    return ApplyResult.IgnoredLoginEcho;
                }

                ListedByCharacterId[characterId] = trimmed;
                return ApplyResult.ListedComment;
            }
        }

        public static void Upsert(uint characterId, string comment)
        {
            Apply(characterId, comment, default(DateTime));
        }

        public static void Remove(uint characterId)
        {
            if (characterId == 0)
            {
                return;
            }

            lock (Sync)
            {
                ListedByCharacterId.Remove(characterId);
            }
        }

        public static bool IsListed(uint characterId)
        {
            lock (Sync)
            {
                return ListedByCharacterId.ContainsKey(characterId);
            }
        }

        public static bool TryGetComment(uint characterId, out string comment)
        {
            lock (Sync)
            {
                return ListedByCharacterId.TryGetValue(characterId, out comment);
            }
        }

        public static List<KeyValuePair<uint, string>> Snapshot()
        {
            lock (Sync)
            {
                return new List<KeyValuePair<uint, string>>(ListedByCharacterId);
            }
        }
    }
}
