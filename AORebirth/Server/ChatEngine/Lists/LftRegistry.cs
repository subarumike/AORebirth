namespace ChatEngine.Lists
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// In-memory Looking for Team registrations (chat-server only).
    /// 05DC + comment = listed; 05DD or empty 05DC = unlisted.
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

            Unlisted,

            IgnoredLoginEcho
        }

        /// <summary>
        /// Empty comment = unlist. Non-empty = list / update comment.
        /// </summary>
        public static ApplyResult Apply(uint characterId, string comment, DateTime chatAuthUtc)
        {
            if (characterId == 0)
            {
                return ApplyResult.Unlisted;
            }

            bool blank = string.IsNullOrWhiteSpace(comment);
            string trimmed = blank ? string.Empty : comment.Trim();

            lock (Sync)
            {
                if (blank)
                {
                    ListedByCharacterId.Remove(characterId);
                    return ApplyResult.Unlisted;
                }

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
