namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Tracks the designated Kill-target NPC spawned inside a mission instance.
    /// </summary>
    internal static class MissionTargetTracker
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Targets = new HashSet<long>();

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void Register(Identity npcIdentity)
        {
            if ((int)npcIdentity.Type == 0 || npcIdentity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                Targets.Add(Key(npcIdentity));
            }
        }

        public static bool IsMissionTarget(Identity npcIdentity)
        {
            lock (Sync)
            {
                return Targets.Contains(Key(npcIdentity));
            }
        }

        public static void Unregister(Identity npcIdentity)
        {
            lock (Sync)
            {
                Targets.Remove(Key(npcIdentity));
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Targets.Clear();
            }
        }
    }
}
