namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Script.Serialization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Mali Mission Roller item databases (clusters / implants / nanos / refined / rest). Used to pick a
    /// reward item for a rolled mission QL. ItemDb_Nanos.json is authoritative for rollable nano crystals:
    /// each nano remains locked to its catalog QL while mission QL may match it within ±10. Every other
    /// category must match the mission QL exactly (and the item's LowQl..HighQl band must cover that QL).
    /// </summary>
    internal static class MissionRewardCatalog
    {
        private const int NanoQlTolerance = 10;

        private static readonly object InitLock = new object();

        private static List<RewardItem> items;

        private static List<RewardItem> nanoItems;

        private static List<RewardItem> otherItems;

        private static bool loadAttempted;

        private static string lastLoadError;

        internal static string LastLoadError
        {
            get
            {
                EnsureLoaded();
                return lastLoadError;
            }
        }

        internal static int ItemCount
        {
            get
            {
                EnsureLoaded();
                return items == null ? 0 : items.Count;
            }
        }

        /// <summary>
        /// Picks one reward for <paramref name="missionQuality"/>. Returns false when no catalog entry
        /// covers that QL; generated rolls treat that as a fail-closed generation error.
        /// </summary>
        public static bool TryPickReward(int missionQuality, Random rng, out QuestItemShort reward, out string itemName, out bool isNano)
        {
            reward = null;
            itemName = null;
            isNano = false;
            EnsureLoaded();

            if (rng == null || missionQuality <= 0 || items == null || items.Count == 0)
            {
                return false;
            }

            // Prefer a non-nano exact-QL hit; fall back to nano ±10; then any exact-QL.
            RewardItem picked = PickFrom(otherItems, missionQuality, 0, rng);
            if (picked == null)
            {
                picked = PickFrom(nanoItems, missionQuality, NanoQlTolerance, rng);
                if (picked != null)
                {
                    isNano = true;
                }
            }

            if (picked == null)
            {
                picked = PickFrom(items, missionQuality, 0, rng);
            }

            if (picked == null)
            {
                return false;
            }

            int rewardQl = ResolveRewardQuality(picked, missionQuality);
            reward = new QuestItemShort
                     {
                         LowId = picked.LowId,
                         HighId = picked.HighId,
                         Quality = rewardQl,
                         Unknown1 = 0
                     };
            itemName = picked.Name;
            isNano = picked.IsNano;
            return true;
        }

        private static int ResolveRewardQuality(RewardItem item, int missionQuality)
        {
            if (item.IsNano)
            {
                return item.LowQl;
            }

            return Clamp(missionQuality, item.LowQl, item.HighQl);
        }

        private static RewardItem PickFrom(List<RewardItem> pool, int missionQuality, int tolerance, Random rng)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            int minQl = missionQuality - tolerance;
            int maxQl = missionQuality + tolerance;
            var matches = new List<RewardItem>();
            for (int i = 0; i < pool.Count; i++)
            {
                RewardItem item = pool[i];
                if (item.LowQl <= maxQl && item.HighQl >= minQl)
                {
                    // Exact-QL requirement for non-tolerant pools: band must cover missionQuality.
                    if (tolerance == 0 && (missionQuality < item.LowQl || missionQuality > item.HighQl))
                    {
                        continue;
                    }

                    matches.Add(item);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            return matches[rng.Next(matches.Count)];
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static void EnsureLoaded()
        {
            if (loadAttempted)
            {
                return;
            }

            lock (InitLock)
            {
                if (loadAttempted)
                {
                    return;
                }

                loadAttempted = true;
                items = new List<RewardItem>();
                nanoItems = new List<RewardItem>();
                otherItems = new List<RewardItem>();

                string dir = FindRewardsDirectory();
                if (dir == null)
                {
                    lastLoadError = "MissionRewards directory not found";
                    return;
                }

                string[] files =
                    {
                        "ItemDb_Clusters.json",
                        "ItemDB_Implants.json",
                        "ItemDb_Nanos.json",
                        "ItemDb_Refined.json",
                        "ItemDb_Rest.json"
                    };

                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                int loadedFiles = 0;
                int duplicateNanosSkipped = 0;
                int invalidNanoQlSkipped = 0;
                var seenNanoFamilies = new HashSet<string>(StringComparer.Ordinal);
                foreach (string fileName in files)
                {
                    string path = Path.Combine(dir, fileName);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        string json = File.ReadAllText(path);
                        var rows = serializer.Deserialize<List<MaliEntry>>(json);
                        if (rows == null)
                        {
                            continue;
                        }

                        bool fileIsNano = fileName.IndexOf("Nano", StringComparison.OrdinalIgnoreCase) >= 0;
                        foreach (MaliEntry row in rows)
                        {
                            if (row == null || row.Key == null || row.Key.LowId <= 0)
                            {
                                continue;
                            }

                            bool isNano = fileIsNano;
                            if (IsExcludedFromRollRewards(row.Key.LowId, row.Key.Name))
                            {
                                continue;
                            }

                            if (isNano && row.Key.LowQl != row.Key.HighQl)
                            {
                                invalidNanoQlSkipped++;
                                continue;
                            }

                            if (isNano
                                && !seenNanoFamilies.Add(
                                    row.Key.LowId.ToString()
                                    + ":"
                                    + row.Key.HighId.ToString()))
                            {
                                duplicateNanosSkipped++;
                                continue;
                            }

                            var item = new RewardItem
                                       {
                                           LowId = row.Key.LowId,
                                           HighId = row.Key.HighId > 0 ? row.Key.HighId : row.Key.LowId,
                                           LowQl = row.Key.LowQl,
                                           HighQl = row.Key.HighQl > 0 ? row.Key.HighQl : row.Key.LowQl,
                                           Name = row.Key.Name ?? string.Empty,
                                           IsNano = isNano
                                       };
                            items.Add(item);
                            if (item.IsNano)
                            {
                                nanoItems.Add(item);
                            }
                            else
                            {
                                otherItems.Add(item);
                            }
                        }

                        loadedFiles++;
                    }
                    catch (Exception ex)
                    {
                        lastLoadError = fileName + ": " + ex.Message;
                    }
                }

                if (items.Count == 0 && string.IsNullOrEmpty(lastLoadError))
                {
                    lastLoadError = "No reward items loaded from " + dir;
                }
                else if (items.Count > 0)
                {
                    lastLoadError = null;
                    MissionDiagnostics.Log(
                        "REWARD-CATALOG loaded files={0} items={1} nanos={2} other={3} duplicateNanosSkipped={4} invalidNanoQlSkipped={5} dir={6}",
                        loadedFiles,
                        items.Count,
                        nanoItems.Count,
                        otherItems.Count,
                        duplicateNanosSkipped,
                        invalidNanoQlSkipped,
                        dir);
                }
            }
        }

        /// <summary>
        /// Ultra-rare Instruction Disc / similar items are mob/chest loot only (~1%), never terminal rolls.
        /// </summary>
        private static bool IsExcludedFromRollRewards(int lowId, string name)
        {
            if (MissionRareLootCatalog.IsRareLootTemplate(lowId))
            {
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.IndexOf("Instruction Disc (Summon Grid Armor", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Instruction Disk (Summon Grid Armor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindRewardsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
                {
                    Path.Combine(baseDir, "XML Data", "MissionRewards"),
                    Path.Combine(baseDir, "MissionRewards"),
                    Path.Combine(Directory.GetCurrentDirectory(), "XML Data", "MissionRewards"),
                    Path.Combine(Directory.GetCurrentDirectory(), "MissionRewards")
                };

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private sealed class RewardItem
        {
            public int LowId;

            public int HighId;

            public int LowQl;

            public int HighQl;

            public string Name;

            public bool IsNano;
        }

        private sealed class MaliEntry
        {
            public MaliKey Key { get; set; }

            public int[] Value { get; set; }
        }

        private sealed class MaliKey
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int LowQl { get; set; }

            public int HighQl { get; set; }

            public string[] Tags { get; set; }

            public string Name { get; set; }
        }
    }
}
