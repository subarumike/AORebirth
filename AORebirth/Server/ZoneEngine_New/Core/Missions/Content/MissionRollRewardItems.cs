namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;

/// <summary>Queryable reward rows from packaged item content, with roll eligibility supplied by policy.</summary>
internal static class MissionRollRewardItems
{
    sealed record Item(int LowId, int HighId, int LowQl, int HighQl, string Name, bool Fixed);
    static readonly Lazy<Item[]> Catalog = new(Load);
    internal static QuestItemShort Select(int quality, Random random)
    {
        if (quality <= 0) throw new ArgumentOutOfRangeException(nameof(quality));
        ArgumentNullException.ThrowIfNull(random);
        var rows = Catalog.Value;
        var eligible = rows.Where(r => !r.Fixed && r.LowQl <= quality && quality <= r.HighQl).ToArray();
        if (eligible.Length == 0)
        {
            int tolerance = MissionRollPolicy.Current.Rewards.NanoQualityTolerance;
            eligible = rows.Where(r => r.Fixed && r.LowQl <= quality + tolerance && r.HighQl >= quality - tolerance).ToArray();
        }
        if (eligible.Length == 0) throw new InvalidOperationException("No configured mission reward covers the requested quality.");
        var selected = eligible[random.Next(eligible.Length)];
        return new() { LowId = selected.LowId, HighId = selected.HighId, Quality = selected.Fixed ? selected.LowQl : quality };
    }
    static Item[] Load()
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
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("Key", out var key) || key.ValueKind != JsonValueKind.Object) continue;
                int Number(string name) => key.TryGetProperty(name, out var field) ? field.GetInt32() : 0;
                int low = Number("LowId"), high = Number("HighId"), min = Number("LowQl"), max = Number("HighQl");
                string name = key.TryGetProperty("Name", out var label) ? label.GetString() ?? string.Empty : string.Empty;
                if (low <= 0 || MissionRareLootCatalog.IsRareLootTemplate(low)
                    || policy.ExcludedNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    || file.FixedQuality && (min != max || !fixedFamilies.Add((low, high)))) continue;
                rows.Add(new(low, high > 0 ? high : low, min, max > 0 ? max : min, name, file.FixedQuality));
            }
        }
        if (rows.Count == 0) throw new InvalidDataException("Mission reward content has no eligible rows.");
        return rows.ToArray();
    }
}
