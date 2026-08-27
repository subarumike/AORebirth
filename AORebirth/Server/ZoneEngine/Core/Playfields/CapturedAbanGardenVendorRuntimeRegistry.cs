namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class CapturedAbanGardenVendorRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedAbanGardenVendorRuntimeDefinition> Entries =
            new Dictionary<int, CapturedAbanGardenVendorRuntimeDefinition>();

        internal static void Register(CapturedAbanGardenVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.NpcIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int npcInstance, out CapturedAbanGardenVendorRuntimeDefinition runtime)
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

    internal sealed class CapturedAbanGardenVendorRuntimeDefinition
    {
        internal CapturedAbanGardenVendorRuntimeDefinition(
            Identity playfieldIdentity,
            Identity npcIdentity,
            Identity vendorIdentity,
            CapturedAbanGardenVendorDefinition content)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.NpcIdentity = npcIdentity;
            this.VendorIdentity = vendorIdentity;
            this.Content = content;
        }

        internal Identity PlayfieldIdentity { get; private set; }
        internal Identity NpcIdentity { get; private set; }
        internal Identity VendorIdentity { get; private set; }
        internal CapturedAbanGardenVendorDefinition Content { get; private set; }
    }
}
