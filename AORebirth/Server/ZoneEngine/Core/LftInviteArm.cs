namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// After LFT Search, remember listed remotes + display names for InfoRequest seed.
    /// Invite only on client 0x1A — never auto-invite on InfoRequest.
    /// </summary>
    public static class LftInviteArm
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, ArmedSearcher> Armed =
            new Dictionary<int, ArmedSearcher>();

        private static readonly TimeSpan ArmLifetime = TimeSpan.FromMinutes(2);

        private sealed class ArmedSearcher
        {
            public HashSet<int> CandidateInstances = new HashSet<int>();

            public Dictionary<int, string> Names = new Dictionary<int, string>();

            public DateTime ExpiresUtc;
        }

        public static void Arm(ICharacter searcher, IEnumerable<int> candidateInstances)
        {
            Arm(searcher, candidateInstances, null);
        }

        public static void Arm(
            ICharacter searcher,
            IEnumerable<int> candidateInstances,
            IDictionary<int, string> names)
        {
            if (searcher == null || candidateInstances == null)
            {
                return;
            }

            int searcherId = searcher.Identity.Instance;
            var set = new HashSet<int>();
            var nameMap = new Dictionary<int, string>();
            foreach (int id in candidateInstances)
            {
                if (id == 0 || id == searcherId)
                {
                    continue;
                }

                set.Add(id);
                string n;
                if (names != null && names.TryGetValue(id, out n) && !string.IsNullOrWhiteSpace(n))
                {
                    nameMap[id] = n;
                }
            }

            if (set.Count == 0)
            {
                return;
            }

            lock (Sync)
            {
                Armed[searcherId] = new ArmedSearcher
                {
                    CandidateInstances = set,
                    Names = nameMap,
                    ExpiresUtc = DateTime.UtcNow.Add(ArmLifetime)
                };
            }
        }

        public static bool IsArmedTarget(ICharacter searcher, Identity targetIdentity)
        {
            string ignored;
            return TryGetArmedName(searcher, targetIdentity, out ignored);
        }

        public static bool TryGetArmedName(ICharacter searcher, Identity targetIdentity, out string name)
        {
            name = null;
            if (searcher == null || targetIdentity.Instance == 0)
            {
                return false;
            }

            lock (Sync)
            {
                ArmedSearcher entry;
                if (!Armed.TryGetValue(searcher.Identity.Instance, out entry))
                {
                    return false;
                }

                if (DateTime.UtcNow > entry.ExpiresUtc)
                {
                    Armed.Remove(searcher.Identity.Instance);
                    return false;
                }

                if (!entry.CandidateInstances.Contains(targetIdentity.Instance))
                {
                    return false;
                }

                entry.Names.TryGetValue(targetIdentity.Instance, out name);
                return true;
            }
        }
    }
}
