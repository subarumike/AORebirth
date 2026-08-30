namespace ForemansLootMembershipValidator
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
            136622, 136624, 136636,
            156328, 156330, 156332, 156575, 156576, 156693, 156695, 156697,
            156769, 156770, 156771, 156773, 156774,
            200818,
            208253, 208255, 208257, 208259, 208261, 208263
        };

        private static readonly IDictionary<int, int> RangeHighItemIds =
            new Dictionary<int, int>
            {
                { 136622, 136623 },
                { 136624, 136625 },
                { 136636, 136637 },
                { 208253, 208254 },
                { 208255, 208256 },
                { 208257, 208258 },
                { 208259, 208260 },
                { 208261, 208262 },
                { 208263, 208264 }
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
                            "FOREMANS_LOCAL_ITEM="
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
                    @"docs\generated\pf1941_loot\foremans-loot-membership-audit.json");
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
                                     != AuditTemplateQuality(value, "template_minimum_quality", "minimum_quality")
                                 || ItemLoader.ItemList[Convert.ToInt32(value["high_item_id"])].Quality
                                     != AuditTemplateQuality(value, "template_maximum_quality", "maximum_quality"))
                    .Select(
                        value => string.Format(
                            "{0}:audit={1}-{2}:local={3}-{4}",
                            value["item_id"],
                            AuditTemplateQuality(value, "template_minimum_quality", "minimum_quality"),
                            AuditTemplateQuality(value, "template_maximum_quality", "maximum_quality"),
                            ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality,
                            ItemLoader.ItemList[Convert.ToInt32(value["high_item_id"])].Quality))
                    .ToArray();
                if (qualityMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit qualities differ from local items.dat: "
                        + string.Join(",", qualityMismatches));
                }

                Console.WriteLine("FOREMANS_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("FOREMANS_LOCAL_ITEM_TEMPLATES_RESOLVED=" + SourceItemIds.Length);
                Console.WriteLine("FOREMANS_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FOREMANS_LOOT_MEMBERSHIP_VALIDATION=FAIL");
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

        private static int AuditTemplateQuality(
            IDictionary<string, object> row,
            string templateKey,
            string fallbackKey)
        {
            return Convert.ToInt32(
                row.ContainsKey(templateKey) ? row[templateKey] : row[fallbackKey]);
        }
    }
}
