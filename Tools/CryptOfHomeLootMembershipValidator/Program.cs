namespace CryptOfHomeLootMembershipValidator
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
            31837,
            245092, 245115, 245135, 245139, 245169, 245170, 245171,
            245215, 245222, 245223, 245273, 245276, 245278, 245323,
            246123, 246125, 246206, 246216, 246217, 246218, 246219,
            246222, 246223, 246226, 246705, 246710, 246715, 246720,
            246829, 246830, 246831, 246832, 246833, 246834,
            287133
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
                            "CRYPT_OF_HOME_LOCAL_ITEM="
                            + itemId
                            + ":QL"
                            + ItemLoader.ItemList[itemId].Quality);
                    }

                    return 0;
                }

                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf4805_loot\crypt-of-home-loot-membership-audit.json");
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

                if (Convert.ToInt32(artifact["active_mapping_count"]) != 0
                    || Convert.ToInt32(artifact["documented_mapping_count"]) != 64
                    || Convert.ToInt32(artifact["inactive_mapping_count"]) != 64
                    || Convert.ToInt32(artifact["playfield_instance"]) != 4805)
                {
                    throw new InvalidDataException(
                        "The mapping totals or PF4805 scope differ from the audited source.");
                }

                Console.WriteLine("CRYPT_OF_HOME_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("CRYPT_OF_HOME_LOCAL_ITEM_TEMPLATES_RESOLVED=" + SourceItemIds.Length);
                Console.WriteLine("CRYPT_OF_HOME_ACTIVE_MAPPINGS=0");
                Console.WriteLine("CRYPT_OF_HOME_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("CRYPT_OF_HOME_LOOT_MEMBERSHIP_VALIDATION=FAIL");
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
