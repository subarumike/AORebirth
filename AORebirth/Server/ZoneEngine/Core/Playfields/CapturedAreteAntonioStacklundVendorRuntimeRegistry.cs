namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal static class CapturedAreteAntonioStacklundVendorRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedAreteAntonioStacklundVendorRuntimeDefinition> Entries =
            new Dictionary<int, CapturedAreteAntonioStacklundVendorRuntimeDefinition>();

        internal static void Register(CapturedAreteAntonioStacklundVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.NpcIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int npcInstance, out CapturedAreteAntonioStacklundVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                return Entries.TryGetValue(npcInstance, out runtime);
            }
        }

        internal static bool ContainsPlayfield(Identity playfieldIdentity)
        {
            lock (Sync)
            {
                return Entries.Values.Any(runtime => Same(runtime.PlayfieldIdentity, playfieldIdentity));
            }
        }

        internal static void RemoveForPlayfield(Identity playfieldIdentity)
        {
            lock (Sync)
            {
                int[] instances = Entries
                    .Where(pair => Same(pair.Value.PlayfieldIdentity, playfieldIdentity))
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (int instance in instances)
                {
                    Entries.Remove(instance);
                }
            }
        }

        internal static bool Same(Identity left, Identity right)
        {
            return left.Type == right.Type && left.Instance == right.Instance;
        }
    }

    internal sealed class CapturedAreteAntonioStacklundVendorRuntimeDefinition
    {
        internal CapturedAreteAntonioStacklundVendorRuntimeDefinition(
            Identity playfieldIdentity,
            Identity npcIdentity,
            Identity vendorIdentity)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.NpcIdentity = npcIdentity;
            this.VendorIdentity = vendorIdentity;
        }

        internal Identity PlayfieldIdentity { get; private set; }

        internal Identity NpcIdentity { get; private set; }

        internal Identity VendorIdentity { get; private set; }
    }
}
