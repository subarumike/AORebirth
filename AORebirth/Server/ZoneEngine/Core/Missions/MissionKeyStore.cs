namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Remembers which mission-key item instances were granted to each character on mission accept so a
    /// following mission-delete (Quest Action=Delete from the journal window) can find and destroy the
    /// matching key. Kept in memory only: a mission key is short lived and only meaningful for the session
    /// that granted it.
    ///
    /// Keys are stored by accepted mission identity when available; TryTakeLatest remains as a fallback for
    /// older accepts that only recorded a per-character stack.
    /// </summary>
    internal static class MissionKeyStore
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, List<int>> KeysByCharacter = new Dictionary<int, List<int>>();

        private static readonly Dictionary<long, int> KeyByMission = new Dictionary<long, int>();

        private static readonly Dictionary<long, int> KitByMission = new Dictionary<long, int>();

        private static long MissionKey(int characterInstance, Identity mission)
        {
            return ((long)characterInstance << 32) ^ (((long)(int)mission.Type << 32) | (uint)mission.Instance);
        }

        /// <summary>
        /// Records a mission-key item instance just granted to a character (stack fallback).
        /// </summary>
        public static void Register(int characterInstance, int keyInstance)
        {
            Register(characterInstance, Identity.None, keyInstance);
        }

        /// <summary>
        /// Records a mission-key item for a specific accepted mission identity.
        /// </summary>
        public static void Register(int characterInstance, Identity mission, int keyInstance)
        {
            if (keyInstance == 0)
            {
                return;
            }

            lock (Sync)
            {
                List<int> keys;
                if (!KeysByCharacter.TryGetValue(characterInstance, out keys) || keys == null)
                {
                    keys = new List<int>();
                    KeysByCharacter[characterInstance] = keys;
                }

                keys.Add(keyInstance);

                if (mission != null && (int)mission.Type != 0 && mission.Instance != 0)
                {
                    KeyByMission[MissionKey(characterInstance, mission)] = keyInstance;
                }
            }
        }

        /// <summary>
        /// Records the Terminal repair-kit instance granted with a RepairMachine accept.
        /// </summary>
        public static void RegisterRepairKit(int characterInstance, Identity mission, int kitInstance)
        {
            if (kitInstance == 0 || mission == null || (int)mission.Type == 0 || mission.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                KitByMission[MissionKey(characterInstance, mission)] = kitInstance;
            }
        }

        /// <summary>
        /// Removes and returns the repair-kit instance for a deleted mission, if recorded.
        /// </summary>
        public static bool TryTakeRepairKit(int characterInstance, Identity mission, out int kitInstance)
        {
            kitInstance = 0;
            if (mission == null || (int)mission.Type == 0 || mission.Instance == 0)
            {
                return false;
            }

            lock (Sync)
            {
                long mk = MissionKey(characterInstance, mission);
                int mapped;
                if (!KitByMission.TryGetValue(mk, out mapped) || mapped == 0)
                {
                    return false;
                }

                KitByMission.Remove(mk);
                kitInstance = mapped;
                return true;
            }
        }

        /// <summary>
        /// Removes the key for a specific mission identity when known; otherwise pops the latest key.
        /// </summary>
        public static bool TryTake(int characterInstance, Identity mission, out int keyInstance)
        {
            if (TryTakeExact(characterInstance, mission, out keyInstance))
            {
                return true;
            }

            return TryTakeLatest(characterInstance, out keyInstance);
        }

        /// <summary>
        /// Removes only the key mapped to the exact accepted mission. It never falls back to a
        /// different mission's latest key.
        /// </summary>
        public static bool TryTakeExact(
            int characterInstance,
            Identity mission,
            out int keyInstance)
        {
            keyInstance = 0;
            if (mission == null || (int)mission.Type == 0 || mission.Instance == 0)
            {
                return false;
            }

            lock (Sync)
            {
                long mk = MissionKey(characterInstance, mission);
                int mapped;
                if (!KeyByMission.TryGetValue(mk, out mapped) || mapped == 0)
                {
                    return false;
                }

                KeyByMission.Remove(mk);
                keyInstance = mapped;
                RemoveFromStack_NoLock(characterInstance, mapped);
                return true;
            }
        }

        /// <summary>
        /// Removes only the exact accepted-mission mapping and exact inventory instance from the
        /// compatibility stack. It never falls back to another mission's latest key.
        /// </summary>
        public static void ForgetExact(
            int characterInstance,
            Identity mission,
            int keyInstance)
        {
            if (mission == null || (int)mission.Type == 0 || mission.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                long mk = MissionKey(characterInstance, mission);
                int mapped;
                if (KeyByMission.TryGetValue(mk, out mapped)
                    && mapped == keyInstance)
                {
                    KeyByMission.Remove(mk);
                }

                RemoveFromStack_NoLock(characterInstance, keyInstance);
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

        private static void RemoveFromStack_NoLock(int characterInstance, int keyInstance)
        {
            List<int> keys;
            if (!KeysByCharacter.TryGetValue(characterInstance, out keys) || keys == null)
            {
                return;
            }

            for (int i = keys.Count - 1; i >= 0; i--)
            {
                if (keys[i] == keyInstance)
                {
                    keys.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
