namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Inventory;

/// <summary>Queryable reward rows from packaged item content, with roll eligibility supplied by policy.</summary>
internal static class MissionRollRewardItems
{
    sealed record Item(int LowId, int HighId, int LowQl, int HighQl, bool Fixed);
    sealed record RewardCatalog(FrozenDictionary<int, Item[]> ChoicesByQuality, int[] TemplateIds);
    static readonly Lazy<RewardCatalog> Catalog = new(Load);

    internal static void ValidateCatalog(IItemTemplateCatalog templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        foreach (int id in Catalog.Value.TemplateIds)
        {
            if (!templates.TryGet(id, out _))
                throw new InvalidDataException("Configured mission reward template is unavailable: " + id + ".");
        }
    }

    internal static QuestItemShort Select(int quality, Random random)
    {
        if (quality <= 0) throw new ArgumentOutOfRangeException(nameof(quality));
        ArgumentNullException.ThrowIfNull(random);
        if (!Catalog.Value.ChoicesByQuality.TryGetValue(quality, out var eligible))
            throw new InvalidOperationException("No configured mission reward covers the requested quality.");
        var selected = eligible[random.Next(eligible.Length)];
        return new() { LowId = selected.LowId, HighId = selected.HighId, Quality = selected.Fixed ? selected.LowQl : quality };
    }
    static RewardCatalog Load()
    {
        var policy = MissionRollPolicy.Current.Rewards;
        var rows = new List<Item>();
        var fixedFamilies = new HashSet<(int, int)>();
        string[] roots = [Path.Combine(AppContext.BaseDirectory, "XML Data", "MissionRewards"),
            Path.Combine(AppContext.BaseDirectory, "MissionRewards"), Path.Combine(Environment.CurrentDirectory, "XML Data", "MissionRewards"),
            Path.Combine(Environment.CurrentDirectory, "MissionRewards")];
        string root = roots.FirstOrDefault(Directory.Exists) ?? throw new InvalidDataException("Mission reward content directory is missing.");
        foreach (var file in policy.Catalogs)
        {
            string path = Path.Combine(root, file.File);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var key = row.GetProperty("Key");
                int Number(string name) => key.GetProperty(name).GetInt32();
                int low = Number("LowId"), high = Number("HighId"), min = Number("LowQl"), max = Number("HighQl");
                string? name = key.GetProperty("Name").GetString();
                if (low <= 0 || high <= 0 || min <= 0 || max < min || string.IsNullOrWhiteSpace(name))
                    throw new InvalidDataException("Malformed reward item range in " + file.File + ".");
                if (MissionRareLootCatalog.IsRareLootTemplate(low)
                    || policy.ExcludedNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    || file.FixedQuality && (min != max || !fixedFamilies.Add((low, high)))) continue;
                rows.Add(new(low, high, min, max, file.FixedQuality));
            }
        }

        var index = new Dictionary<int, Item[]>();
        foreach (int quality in MissionLevelRuntime.SupportedQualities)
        {
            // Preserve the authored catalog order and fixed-quality fallback policy.
            var eligible = rows.Where(row => !row.Fixed && row.LowQl <= quality && quality <= row.HighQl).ToArray();
            if (eligible.Length == 0)
            {
                int tolerance = policy.NanoQualityTolerance;
                eligible = rows.Where(row => row.Fixed && row.LowQl <= quality + (long)tolerance
                    && row.HighQl >= quality - (long)tolerance).ToArray();
            }
            if (eligible.Length == 0)
                throw new InvalidDataException("No configured mission reward covers mission quality " + quality + ".");
            index.Add(quality, eligible);
        }
        var templateIds = rows.Select(row => row.LowId).Concat(rows.Select(row => row.HighId)).Distinct().ToArray();
        return new(index.ToFrozenDictionary(), templateIds);
    }
}
