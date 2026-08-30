namespace TotwLootMembershipValidator
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
                    : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf1931_loot\totw-loot-membership-audit.json");
                string itemsPath = Path.Combine(root, @"AORebirth\Datafiles\items.dat");
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                    File.ReadAllText(artifactPath));
                object[] rows = (object[])artifact["items"];
                int[] sourceIds = rows
                    .Cast<Dictionary<string, object>>()
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

                Console.WriteLine("TOTW_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("TOTW_LOCAL_ITEM_TEMPLATES_RESOLVED=" + sourceIds.Length);
                Console.WriteLine("TOTW_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TOTW_LOOT_MEMBERSHIP_VALIDATION=FAIL");
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }
    }
}
