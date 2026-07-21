namespace ZoneEngine.Core.Playfields
{
    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class CapturedHoloDeckVendorRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedHoloDeckVendorRuntimeDefinition> Entries =
            new Dictionary<int, CapturedHoloDeckVendorRuntimeDefinition>();

        internal static void Register(CapturedHoloDeckVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.VendorIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int vendorInstance, out CapturedHoloDeckVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                return Entries.TryGetValue(vendorInstance, out runtime);
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

    internal sealed class CapturedHoloDeckVendorRuntimeDefinition
    {
        internal CapturedHoloDeckVendorRuntimeDefinition(
            Identity playfieldIdentity,
            Identity vendorIdentity)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.VendorIdentity = vendorIdentity;
        }

        internal Identity PlayfieldIdentity { get; private set; }

        internal Identity VendorIdentity { get; private set; }
    }
}
