namespace MercenaryCampLootMembershipValidator
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
            156772,
            165127, 165130, 165176,
            165203, 165205, 165206, 165207, 165208, 165209,
            165213, 165214, 165215,
            165303, 165304, 165305, 165306, 165307, 165308,
            168670, 168671, 168672, 168675,
            212995
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

                if (inventoryMode)
                {
                    foreach (int itemId in SourceItemIds.OrderBy(value => value))
                    {
                        Console.WriteLine(
                            "MERCENARY_CAMP_LOCAL_ITEM="
                            + itemId
                            + ":QL"
                            + ItemLoader.ItemList[itemId].Quality);
                    }

                    return 0;
                }

                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf620_loot\mercenary-camp-loot-membership-audit.json");
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
                string[] qualityMismatches = rows
                    .Where(
                        value => ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality
                                 != Convert.ToInt32(value["quality"]))
                    .Select(value => value["item_id"].ToString())
                    .ToArray();
                if (qualityMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit qualities differ from local items.dat: "
                        + string.Join(",", qualityMismatches));
                }

                if (Convert.ToInt32(artifact["active_mapping_count"]) != 3
                    || Convert.ToInt32(artifact["documented_mapping_count"]) != 48
                    || Convert.ToInt32(artifact["inactive_mapping_count"]) != 45
                    || Convert.ToInt32(artifact["playfield_instance"]) != 620)
                {
                    throw new InvalidDataException(
                        "The mapping totals or PF620 scope differ from the audited source.");
                }

                Console.WriteLine("MERCENARY_CAMP_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("MERCENARY_CAMP_LOCAL_ITEM_TEMPLATES_RESOLVED=" + SourceItemIds.Length);
                Console.WriteLine("MERCENARY_CAMP_ACTIVE_MAPPINGS=3");
                Console.WriteLine("MERCENARY_CAMP_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("MERCENARY_CAMP_LOOT_MEMBERSHIP_VALIDATION=FAIL");
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
    }
}
