namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Remembers which mission-key item instances were granted to each character on mission accept so a
    /// following mission-delete (Quest Action=Delete from the journal window) can find and destroy the
    /// matching key. Kept in memory only: a mission key is short lived and only meaningful for the session
    /// that granted it.
    ///
    /// Limitation: the accepted-mission window currently replays a single fixed captured QuestId, so all
    /// accepts share one mission identity. We therefore track keys as a per-character stack and remove the
    /// most recently granted key on delete. This is sufficient for the current single-mission testing flow;
    /// a full implementation should key by the real (unique) accepted mission identity.
    /// </summary>
    internal static class MissionKeyStore
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, List<int>> KeysByCharacter = new Dictionary<int, List<int>>();

        /// <summary>
        /// Records a mission-key item instance just granted to a character.
        /// </summary>
        public static void Register(int characterInstance, int keyInstance)
        {
            lock (Sync)
            {
                List<int> keys;
                if (!KeysByCharacter.TryGetValue(characterInstance, out keys) || keys == null)
                {
                    keys = new List<int>();
                    KeysByCharacter[characterInstance] = keys;
                }

                keys.Add(keyInstance);
            }
        }

        /// <summary>
        /// Removes and returns the most recently granted mission-key instance for a character, if any.
        /// </summary>
        public static bool TryTakeLatest(int characterInstance, out int keyInstance)
        {
            keyInstance = 0;

            lock (Sync)
            {
                List<int> keys;
                if (!KeysByCharacter.TryGetValue(characterInstance, out keys) || keys == null || keys.Count == 0)
                {
                    return false;
                }

                int lastIndex = keys.Count - 1;
                keyInstance = keys[lastIndex];
                keys.RemoveAt(lastIndex);
                return true;
            }
        }
    }
}
