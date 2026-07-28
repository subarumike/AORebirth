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
    /// reward item for a rolled mission QL. Nano crystals may land at missionQl ± 10; every other category
    /// must match the mission QL exactly (and the item's LowQl..HighQl band must cover that QL).
    /// </summary>
    internal static class MissionRewardCatalog
    {
        private const int NanoQlTolerance = 10;

        private static readonly object InitLock = new object();

        private static List<RewardItem> items;

        private static List<RewardItem> nanoItems;

        private static List<RewardItem> otherItems;

        private static HashSet<string> missionRewardNanoKeys;

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

            int rewardQl = ResolveRewardQuality(picked, missionQuality, isNano || picked.IsNano, rng);
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

        private static int ResolveRewardQuality(RewardItem item, int missionQuality, bool allowNanoBand, Random rng)
        {
            if (!allowNanoBand)
            {
                return Clamp(missionQuality, item.LowQl, item.HighQl);
            }

            int min = Math.Max(item.LowQl, missionQuality - NanoQlTolerance);
            int max = Math.Min(item.HighQl, missionQuality + NanoQlTolerance);
            if (min > max)
            {
                return Clamp(missionQuality, item.LowQl, item.HighQl);
            }

            return rng.Next(min, max + 1);
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

                missionRewardNanoKeys = LoadMissionRewardNanoKeys(dir);

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
                int nanosSkipped = 0;
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

                            bool isNano = fileIsNano || HasNanoTag(row.Key.Tags);
                            if (IsExcludedFromRollRewards(row.Key.LowId, row.Key.Name))
                            {
                                continue;
                            }

                            if (isNano && !IsMissionRewardNano(row.Key.Name))
                            {
                                nanosSkipped++;
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
                        "REWARD-CATALOG loaded files={0} items={1} nanos={2} other={3} nanosSkipped={4} allowlist={5} dir={6}",
                        loadedFiles,
                        items.Count,
                        nanoItems.Count,
                        otherItems.Count,
                        nanosSkipped,
                        missionRewardNanoKeys == null ? 0 : missionRewardNanoKeys.Count,
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

        /// <summary>
        /// AO Galaxy "Mission Reward" nano names (crystal formula titles). Empty allowlist = keep all Mali nanos.
        /// </summary>
        private static bool IsMissionRewardNano(string itemName)
        {
            if (missionRewardNanoKeys == null || missionRewardNanoKeys.Count == 0)
            {
                return true;
            }

            string key = NormalizeNanoName(itemName);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (missionRewardNanoKeys.Contains(key))
            {
                return true;
            }

            // Allow partial containment either direction (display suffixes / crystal wrappers).
            foreach (string allowed in missionRewardNanoKeys)
            {
                if (key.IndexOf(allowed, StringComparison.Ordinal) >= 0
                    || allowed.IndexOf(key, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> LoadMissionRewardNanoKeys(string rewardsDir)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rewardsDir))
            {
                return keys;
            }

            string path = Path.Combine(rewardsDir, "MissionRewardNanoNames.txt");
            if (!File.Exists(path))
            {
                return keys;
            }

            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string key = NormalizeNanoName(line);
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }
            catch (Exception ex)
            {
                MissionDiagnostics.Log("REWARD-NANO-ALLOWLIST-FAIL {0}", ex.Message);
            }

            return keys;
        }

        private static string NormalizeNanoName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            string s = name.Trim();
            const string prefix = "Nano Crystal (";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && s.EndsWith(")", StringComparison.Ordinal))
            {
                s = s.Substring(prefix.Length, s.Length - prefix.Length - 1).Trim();
            }

            // AO Galaxy display suffixes.
            int spec = s.LastIndexOf(" Spec:", StringComparison.OrdinalIgnoreCase);
            if (spec > 0)
            {
                s = s.Substring(0, spec).Trim();
            }

            if (s.EndsWith(" FP", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 3).Trim();
            }

            return s.ToLowerInvariant();
        }

        private static bool HasNanoTag(string[] tags)
        {
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], "nano", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tags[i], "crystal", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
