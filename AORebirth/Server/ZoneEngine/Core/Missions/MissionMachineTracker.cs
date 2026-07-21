namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Tracks the Broken Machine NPC spawned inside a RepairMachine mission instance.
    /// </summary>
    internal static class MissionMachineTracker
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Machines = new HashSet<long>();

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void Register(Identity machineIdentity)
        {
            if ((int)machineIdentity.Type == 0 || machineIdentity.Instance == 0)
            {
                return;
            }

            lock (Sync)
            {
                Machines.Add(Key(machineIdentity));
            }
        }

        public static bool IsMissionMachine(Identity machineIdentity)
        {
            lock (Sync)
            {
                return Machines.Contains(Key(machineIdentity));
            }
        }

        public static void Unregister(Identity machineIdentity)
        {
            lock (Sync)
            {
                Machines.Remove(Key(machineIdentity));
            }
        }
    }
}
