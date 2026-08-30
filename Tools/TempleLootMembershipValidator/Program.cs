namespace TempleLootMembershipValidator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Items;

    internal static class Program
    {
        private const string SourceUrl =
            "https://wiki.aodb.us/wiki/Temple_of_Three_Winds";

        private static readonly ExpectedMapping[] ExpectedMappings =
        {
            Mapping("totw.647.named.windcaller-yatila", 204576, 600, 900),
            Mapping("totw.647.boss.the-curator", 204575, 100, 100),
            Mapping("totw.647.boss.the-curator", 204577, 200, 300),
            Mapping("totw.647.boss.the-curator", 204578, 600, 700),
            Mapping("totw.647.boss.nematet-the-custodian-of-time", 204613, 300, 500),
            Mapping("totw.647.boss.nematet-the-custodian-of-time", 204647, 2000, 2000),
            Mapping("totw.1931.boss.guardian-of-tomorrow", 204748, 400, 500)
        };

        private static int Main(string[] args)
        {
            try
            {
                string root = args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
                string itemsPath = Path.Combine(root, @"AORebirth\Datafiles\items.dat");

                ItemLoader.ItemList.Clear();
                ItemLoader.CacheAllItems(itemsPath);
                int[] sourceItemIds = ExpectedMappings
                    .Select(value => value.ItemId)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArray();
                int[] missing = sourceItemIds
                    .Where(value => !ItemLoader.ItemList.ContainsKey(value))
                    .ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidDataException(
                        "Wiki item IDs absent from local items.dat: "
                        + string.Join(",", missing));
                }

                string artifactPath = Path.Combine(
                    root,
                    @"docs\generated\pf1931_loot\temple-loot-membership-audit.json");
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                    File.ReadAllText(artifactPath));
                Dictionary<string, object>[] rows = ((object[])artifact["items"])
                    .Cast<Dictionary<string, object>>()
                    .ToArray();
                if (!string.Equals(
                        Convert.ToString(artifact["source_url"]),
                        SourceUrl,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The audit source URL is not the accepted AOWiki page.");
                }
                if (Convert.ToInt32(artifact["playfield_instance"]) != 1931
                    || Convert.ToInt32(artifact["source_item_count"]) != sourceItemIds.Length
                    || Convert.ToInt32(artifact["documented_mapping_count"]) != ExpectedMappings.Length
                    || rows.Length != ExpectedMappings.Length)
                {
                    throw new InvalidDataException("The audit counts or playfield scope are invalid.");
                }

                string[] expected = ExpectedMappings
                    .Select(value => value.Identity)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                string[] actual = rows
                    .Select(RowIdentity)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (!expected.SequenceEqual(actual))
                {
                    throw new InvalidDataException(
                        "The audit profile, item, or documented probability mappings differ from the accepted source set.");
                }

                string[] qualityMismatches = rows
                    .Where(
                        value => ItemLoader.ItemList[Convert.ToInt32(value["item_id"])].Quality
                                 != Convert.ToInt32(value["quality"]))
                    .Select(value => Convert.ToString(value["item_id"]))
                    .ToArray();
                if (qualityMismatches.Length > 0)
                {
                    throw new InvalidDataException(
                        "Audit qualities differ from local items.dat: "
                        + string.Join(",", qualityMismatches));
                }

                Console.WriteLine("TEMPLE_LOOT_AUDIT_ITEMS=" + rows.Length);
                Console.WriteLine("TEMPLE_LOCAL_ITEM_TEMPLATES_RESOLVED=" + sourceItemIds.Length);
                Console.WriteLine("TEMPLE_CAPTURE_LOOT_PRESERVED=YES");
                Console.WriteLine("TEMPLE_LOOT_MEMBERSHIP_SOURCE_AUDITED=YES");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TEMPLE_LOOT_MEMBERSHIP_VALIDATION=FAIL");
                Console.Error.WriteLine(error.Message);
                return 1;
            }
        }

        private static ExpectedMapping Mapping(
            string profileKey,
            int itemId,
            int minimumBasisPoints,
            int maximumBasisPoints)
        {
            return new ExpectedMapping(
                profileKey,
                itemId,
                minimumBasisPoints,
                maximumBasisPoints);
        }

        private static string RowIdentity(IDictionary<string, object> row)
        {
            return string.Join(
                "|",
                Convert.ToString(row["profile_key"]),
                Convert.ToString(row["item_id"]),
                Convert.ToString(row["minimum_basis_points"]),
                Convert.ToString(row["maximum_basis_points"]));
        }

        private sealed class ExpectedMapping
        {
            internal ExpectedMapping(
                string profileKey,
                int itemId,
                int minimumBasisPoints,
                int maximumBasisPoints)
            {
                this.Identity = string.Join(
                    "|",
                    profileKey,
                    itemId,
                    minimumBasisPoints,
                    maximumBasisPoints);
                this.ItemId = itemId;
            }

            internal string Identity { get; private set; }
            internal int ItemId { get; private set; }
        }
    }
}
