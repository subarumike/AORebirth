namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class CapturedBucketheadTechnodealerRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedBucketheadTechnodealerRuntimeDefinition> ByNpc =
            new Dictionary<int, CapturedBucketheadTechnodealerRuntimeDefinition>();

        private static readonly Dictionary<int, int> NpcByOwner = new Dictionary<int, int>();

        internal static void Register(CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                ByNpc[runtime.NpcIdentity.Instance] = runtime;
                NpcByOwner[runtime.OwnerIdentity.Instance] = runtime.NpcIdentity.Instance;
            }
        }

        internal static bool TryGet(int npcInstance, out CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                return ByNpc.TryGetValue(npcInstance, out runtime);
            }
        }

        internal static bool TryGetByOwner(int ownerInstance, out CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                int npcInstance;
                if (!NpcByOwner.TryGetValue(ownerInstance, out npcInstance))
                {
                    runtime = null;
                    return false;
                }

                return ByNpc.TryGetValue(npcInstance, out runtime);
            }
        }

        internal static bool Remove(int npcInstance, out CapturedBucketheadTechnodealerRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                if (!ByNpc.TryGetValue(npcInstance, out runtime))
                {
                    return false;
                }

                ByNpc.Remove(npcInstance);
                int ownerNpc;
                if (NpcByOwner.TryGetValue(runtime.OwnerIdentity.Instance, out ownerNpc)
                    && ownerNpc == npcInstance)
                {
                    NpcByOwner.Remove(runtime.OwnerIdentity.Instance);
                }

                return true;
            }
        }

        internal static bool Same(Identity left, Identity right)
        {
            return left.Type == right.Type && left.Instance == right.Instance;
        }
    }

    internal sealed class CapturedBucketheadTechnodealerRuntimeDefinition
    {
        internal CapturedBucketheadTechnodealerRuntimeDefinition(
            Identity playfieldIdentity,
            Identity ownerIdentity,
            Identity npcIdentity,
            Identity vendorIdentity,
            int lifetimeSeconds,
            Vendor vendor)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.OwnerIdentity = ownerIdentity;
            this.NpcIdentity = npcIdentity;
            this.VendorIdentity = vendorIdentity;
            this.LifetimeSeconds = lifetimeSeconds;
            this.Vendor = vendor;
        }

        internal Identity PlayfieldIdentity { get; private set; }

        internal Identity OwnerIdentity { get; private set; }

        internal Identity NpcIdentity { get; private set; }

        internal Identity VendorIdentity { get; private set; }

        internal int LifetimeSeconds { get; private set; }

        /// <summary>
        /// Strong reference so Use does not depend on Pool lookup alone.
        /// </summary>
        internal Vendor Vendor { get; private set; }
    }
}
