namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal static class CapturedAreteSarahGreeneVendorRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, CapturedAreteSarahGreeneVendorRuntimeDefinition> Entries =
            new Dictionary<int, CapturedAreteSarahGreeneVendorRuntimeDefinition>();

        internal static void Register(CapturedAreteSarahGreeneVendorRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.NpcIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int npcInstance, out CapturedAreteSarahGreeneVendorRuntimeDefinition runtime)
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

    internal sealed class CapturedAreteSarahGreeneVendorRuntimeDefinition
    {
        internal CapturedAreteSarahGreeneVendorRuntimeDefinition(
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
