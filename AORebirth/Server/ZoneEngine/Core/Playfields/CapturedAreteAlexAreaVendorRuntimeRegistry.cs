namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class CapturedAreteAlexAreaVendorRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedAreteAlexAreaVendorRuntimeDefinition> Entries =
            new Dictionary<int, CapturedAreteAlexAreaVendorRuntimeDefinition>();

        internal static void Register(CapturedAreteAlexAreaVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.VendorIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int vendorInstance, out CapturedAreteAlexAreaVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                return Entries.TryGetValue(vendorInstance, out runtime);
            }
        }

        internal static void Remove(int vendorInstance)
        {
            lock (Sync)
            {
                Entries.Remove(vendorInstance);
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

    internal sealed class CapturedAreteAlexAreaVendorRuntimeDefinition
    {
        internal CapturedAreteAlexAreaVendorRuntimeDefinition(
            Identity playfieldIdentity,
            Identity vendorIdentity,
            string displayName)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.VendorIdentity = vendorIdentity;
            this.DisplayName = displayName;
        }

        internal Identity PlayfieldIdentity { get; private set; }

        internal Identity VendorIdentity { get; private set; }

        internal string DisplayName { get; private set; }
    }
}
