namespace CyborgBarracksLootMembershipValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Items;

    internal static class Program
    {
        private static readonly int[] SourceItemIds =
        {
            153975, 153976, 153977, 153979, 153980, 153981, 153982,
            154405, 154406, 154407, 154408, 154505, 165110
        };

        private static readonly IDictionary<int, int> RangeHighItemIds =
            new Dictionary<int, int>
            {
                { 165110, 165111 }
            };

        private static int Main(string[] args)
        {
            try
            {
                bool inventoryMode = args.Length > 0
                                     && string.Equals(
                                         args[0],
                                         "--inventory",
                                         StringComparison.OrdinalIgnoreCase);
                string rootArgument = inventoryMode
                    ? (args.Length > 1 ? args[1] : null)
                    : (args.Length > 0 ? args[0] : null);
                string root = string.IsNullOrWhiteSpace(rootArgument)
                    ? Path.GetFullPath(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."))
                    : Path.GetFullPath(rootArgument);
                string itemsPath = Path.Combine(root, @"AORebirth\Datafiles\items.dat");

                ItemLoader.ItemList.Clear();
                ItemLoader.CacheAllItems(itemsPath);
                int[] missing = SourceItemIds
                    .Where(value => !ItemLoader.ItemList.ContainsKey(value))
                    .OrderBy(value => value)
                    .ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidDataException(
                        "Wiki item IDs absent from local items.dat: "
                        + string.Join(",", missing));
                }

                int[] missingHighIds = RangeHighItemIds.Values
                    .Where(value => !ItemLoader.ItemList.ContainsKey(value))
                    .OrderBy(value => value)
                    .ToArray();
                if (missingHighIds.Length > 0)
                {
                    throw new InvalidDataException(
                        "Proven ranged high item IDs absent from local items.dat: "
                        + string.Join(",", missingHighIds));
                }

                if (inventoryMode)
                {
                    foreach (int itemId in SourceItemIds.OrderBy(value => value))
                    {
                        int highItemId;
                        bool hasHighItemId = RangeHighItemIds.TryGetValue(itemId, out highItemId);
                        Console.WriteLine(
                            "CYBORG_BARRACKS_LOCAL_ITEM="
                            + itemId
                            + ":QL"
                            + ItemLoader.ItemList[itemId].Quality
                            + (hasHighItemId
                                ? "-" + highItemId + ":QL" + ItemLoader.ItemList[highItemId].Quality
                                : string.Empty));
                    }

                    return 0;
                }

                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf1833_loot\cyborg-barracks-loot-membership-audit.json");
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                    File.ReadAllText(artifactPath));
                Dictionary<string, object>[] rows = ((object[])artifact["items"])
                    .Cast<Dictionary<string, object>>()
                    .ToArray();
                int[] artifactIds = rows
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray();
                if (rows.Length != Convert.ToInt32(artifact["source_item_count"])
                    || artifactIds.Length != artifactIds.Distinct().Count())
                {
                    throw new InvalidDataException(
                        "The audit source item count or item-ID uniqueness is invalid.");
                }

                CollectionAssert(SourceItemIds.OrderBy(value => value), artifactIds);
                string[] rangePairMismatches = rows
                    .Where(
                        value => Convert.ToInt32(value["high_item_id"])
                                 != ExpectedHighItemId(Convert.ToInt32(value["item_id"])))
                    .Select(
                        value => value["item_id"]
                                 + ":audit-high="
                                 + value["high_item_id"]
                                 + ":expected-high="
                                 + ExpectedHighItemId(Convert.ToInt32(value["item_id"])))
                    .ToArray();
                if (rangePairMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit range pairs differ from proven item-name pairs: "
                        + string.Join(",", rangePairMismatches));
                }

                string[] qualityMismatches = rows
                    .Where(
                        value => ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality
                                     != Convert.ToInt32(value["minimum_quality"])
                                 || ItemLoader.ItemList[Convert.ToInt32(value["high_item_id"])].Quality
                                     != Convert.ToInt32(value["maximum_quality"]))
                    .Select(value => value["item_id"].ToString())
                    .ToArray();
                if (qualityMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit qualities differ from local items.dat: "
                        + string.Join(",", qualityMismatches));
                }

                if (Convert.ToInt32(artifact["active_mapping_count"]) != 0
                    || Convert.ToInt32(artifact["documented_mapping_count"]) != 13
                    || Convert.ToInt32(artifact["inactive_mapping_count"]) != 13
                    || Convert.ToInt32(artifact["playfield_instance"]) != 1833)
                {
                    throw new InvalidDataException(
                        "The mapping totals or PF1833 scope differ from the audited source.");
                }

                Console.WriteLine("CYBORG_BARRACKS_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("CYBORG_BARRACKS_LOCAL_ITEM_TEMPLATES_RESOLVED=" + SourceItemIds.Length);
                Console.WriteLine("CYBORG_BARRACKS_ACTIVE_MAPPINGS=0");
                Console.WriteLine("CYBORG_BARRACKS_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("CYBORG_BARRACKS_LOOT_MEMBERSHIP_VALIDATION=FAIL");
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        private static void CollectionAssert(
            IEnumerable<int> expected,
            IEnumerable<int> actual)
        {
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidDataException(
                    "The audit item-ID set differs from the wiki source set.");
            }
        }

        private static int ExpectedHighItemId(int itemId)
        {
            int highItemId;
            return RangeHighItemIds.TryGetValue(itemId, out highItemId)
                ? highItemId
                : itemId;
        }
    }
}
