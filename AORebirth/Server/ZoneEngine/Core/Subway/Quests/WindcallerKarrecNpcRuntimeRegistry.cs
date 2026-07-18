namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal static class WindcallerKarrecNpcRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, WindcallerKarrecNpcRuntimeDefinition> Entries =
            new Dictionary<int, WindcallerKarrecNpcRuntimeDefinition>();

        internal static void Register(WindcallerKarrecNpcRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                Entries[runtime.NpcIdentity.Instance] = runtime;
            }
        }

        internal static bool TryGet(int npcInstance, out WindcallerKarrecNpcRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                return Entries.TryGetValue(npcInstance, out runtime);
            }
        }

        internal static bool TryGet(
            Identity playfieldIdentity,
            Identity npcIdentity,
            out WindcallerKarrecNpcRuntimeDefinition runtime)
        {
            lock (Sync)
            {
                WindcallerKarrecNpcRuntimeDefinition candidate;
                if (!Entries.TryGetValue(npcIdentity.Instance, out candidate)
                    || !Same(candidate.PlayfieldIdentity, playfieldIdentity)
                    || !Same(candidate.NpcIdentity, npcIdentity))
                {
                    runtime = null;
                    return false;
                }

                runtime = candidate;
                return true;
            }
        }

        internal static bool ContainsPlayfield(Identity playfieldIdentity)
        {
            return CountForPlayfield(playfieldIdentity) > 0;
        }

        internal static int CountForPlayfield(Identity playfieldIdentity)
        {
            lock (Sync)
            {
                return Entries.Values.Count(runtime => Same(runtime.PlayfieldIdentity, playfieldIdentity));
            }
        }

        internal static void RemoveForPlayfield(Identity playfieldIdentity)
        {
            lock (Sync)
            {
                int[] keys = Entries
                    .Where(pair => Same(pair.Value.PlayfieldIdentity, playfieldIdentity))
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (int key in keys)
                {
                    Entries.Remove(key);
                }
            }
        }

        private static bool Same(Identity left, Identity right)
        {
            return left.Type == right.Type && left.Instance == right.Instance;
        }
    }

    internal sealed class WindcallerKarrecNpcRuntimeDefinition
    {
        internal WindcallerKarrecNpcRuntimeDefinition(
            Identity playfieldIdentity,
            Identity npcIdentity,
            WindcallerKarrecNpcDefinition content)
        {
            this.PlayfieldIdentity = playfieldIdentity;
            this.NpcIdentity = npcIdentity;
            this.Content = content;
        }

        internal Identity PlayfieldIdentity { get; private set; }

        internal Identity NpcIdentity { get; private set; }

        internal WindcallerKarrecNpcDefinition Content { get; private set; }
    }
}
