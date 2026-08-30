namespace InnerSanctumLootMembershipValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Items;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string root = args.Length == 1
                    ? Path.GetFullPath(args[0])
                    : Path.GetFullPath(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf1943_loot\inner-sanctum-boss-loot-audit.json");
                string itemsPath = Path.Combine(root, @"AORebirth\Datafiles\items.dat");
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                    File.ReadAllText(artifactPath));
                object[] rows = (object[])artifact["items"];
                Dictionary<string, object>[] items = rows
                    .Cast<Dictionary<string, object>>()
                    .ToArray();
                int[] sourceIds = items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .ToArray();
                if (rows.Length != Convert.ToInt32(artifact["source_item_count"])
                    || sourceIds.Length != sourceIds.Distinct().Count())
                {
                    throw new InvalidDataException(
                        "The audit source item count or item-ID uniqueness is invalid.");
                }

                ItemLoader.ItemList.Clear();
                ItemLoader.CacheAllItems(itemsPath);
                int[] missing = sourceIds
                    .Where(value => !ItemLoader.ItemList.ContainsKey(value))
                    .OrderBy(value => value)
                    .ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit item IDs absent from local items.dat: "
                        + string.Join(",", missing));
                }

                string[] qualityMismatches = items
                    .Where(
                        value => ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality
                                 != Convert.ToInt32(value["quality"]))
                    .Select(
                        value => string.Format(
                            "{0}:audit={1}:local={2}",
                            value["item_id"],
                            value["quality"],
                            ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality))
                    .ToArray();
                if (qualityMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit fixed qualities differ from local items.dat: "
                        + string.Join(",", qualityMismatches));
                }

                Console.WriteLine("INNER_SANCTUM_BOSS_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine(
                    "INNER_SANCTUM_LOCAL_ITEM_TEMPLATES_RESOLVED=" + sourceIds.Length);
                Console.WriteLine("INNER_SANCTUM_BOSS_LOOT_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("INNER_SANCTUM_LOOT_MEMBERSHIP_VALIDATION=FAIL");
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }
    }
}
