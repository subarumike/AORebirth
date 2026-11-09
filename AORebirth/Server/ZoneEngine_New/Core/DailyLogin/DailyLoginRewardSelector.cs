namespace ZoneEngine_New.Core.DailyLogin;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.GameData;

/// <summary>Recovered selection rules over current item templates, without allocating or granting inventory.</summary>
public sealed class DailyLoginRewardSelector(IItemTemplateCatalog templates, DailyLoginRewardCatalog rewards, IGameData? gameData = null)
{
    readonly object _familyLock = new();
    FamilyCache? _familyCache;

    sealed record FamilyCache(IReadOnlyCollection<HashInstance> Source, Dictionary<int, List<HashInstance>> Index)
    {
        internal ConcurrentDictionary<int, HashInstance[]> Components { get; } = new();
    }

    FamilyCache CurrentFamilies()
    {
        lock (_familyLock)
        {
            IReadOnlyCollection<HashInstance> source = gameData?.ItemFamilies ?? Array.Empty<HashInstance>();
            if (_familyCache is null || !ReferenceEquals(_familyCache.Source, source))
                _familyCache = new FamilyCache(source, IndexFamilies(rewards.QualityFamilies.Concat(source)));
            return _familyCache;
        }
    }

    public bool TrySelect(int day, int level, int profession, Random random,
        out DailyLoginRewardSelection? selection, out string error)
    {
        ArgumentNullException.ThrowIfNull(random);
        selection = null;
        error = string.Empty;
        if (!rewards.TryGet(day, out DailyLoginDayReward reward))
            return Fail("Day " + day + " has no reward configured.", out error);

        level = Math.Max(1, level);
        int itemId;
        int quality;
        bool professionNano = string.Equals(reward.QualityMode, "professionVendorNano", StringComparison.OrdinalIgnoreCase);
        bool scaled = string.Equals(reward.QualityMode, "characterLevel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reward.QualityMode, "characterLevelPlusMinus", StringComparison.OrdinalIgnoreCase);
        FamilyCache? families = scaled || (!professionNano && reward.RandomItemIds is { Length: > 0 }) ? CurrentFamilies() : null;
        if (professionNano)
        {
            if (!TryPickProfessionNano(reward, level, profession, random, out itemId, out quality))
                return Fail("No usable daily reward nano is configured for this profession.", out error);
        }
        else
        {
            itemId = families is null ? 0 : PickRandomExistingItem(reward.RandomItemIds, random, families);
            if (itemId <= 0)
                itemId = reward.ItemId;
            quality = ResolveQuality(reward, level, random);
        }

        if (itemId <= 0)
            return Fail("Day " + day + " has no reward configured.", out error);
        if (!templates.TryGet(itemId, out ItemTemplate seed))
            return Fail("The daily reward item is missing from the item database.", out error);

        int lowId = itemId;
        int highId = itemId;
        if (scaled)
        {
            if (!TryResolveDataFamily(itemId, quality, families!, out lowId, out highId, out int dataQuality))
                ResolveScalePair(itemId, quality, out lowId, out highId);
            else
                quality = dataQuality;
        }

        ItemTemplate low = templates.Require(lowId);
        ItemTemplate high = templates.Require(highId);
        quality = Math.Clamp(quality, Math.Max(1, Math.Min(low.Quality, high.Quality)),
            Math.Max(1, Math.Max(low.Quality, high.Quality)));

        bool stackable = seed.Stats.TryGetValue(CharacterStat.Can, out int can)
            && ((CanFlags)(uint)can & CanFlags.Stackable) != 0;
        // Ammo boxes carry their rounds in MaxEnergy even when they cannot be split as an item stack.
        bool countedItem = stackable || seed.MintStackCount > 1;
        selection = new DailyLoginRewardSelection(lowId, highId, quality, reward.Amount,
            countedItem ? 1 : reward.Amount, countedItem ? reward.Amount : 1,
            string.IsNullOrWhiteSpace(seed.Name) ? reward.ItemName : seed.Name);
        return true;
    }

    int PickRandomExistingItem(int[]? pool, Random random, FamilyCache families)
    {
        if (pool is null || pool.Length == 0)
            return 0;
        var valid = new List<int>();
        var lowEndpoints = new List<int>();
        foreach (int id in pool)
        {
            if (id <= 0 || !templates.TryGet(id, out _))
                continue;
            valid.Add(id);
            if (TryGetRelationPair(id, out int low, out _) && id == low
                || IsDataFamilyLowEndpoint(id, families))
                lowEndpoints.Add(id);
        }
        List<int> candidates = lowEndpoints.Count > 0 ? lowEndpoints : valid;
        return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : 0;
    }

    static int ResolveQuality(DailyLoginDayReward reward, int level, Random random)
    {
        if (string.Equals(reward.QualityMode, "characterLevel", StringComparison.OrdinalIgnoreCase))
            return level;
        if (string.Equals(reward.QualityMode, "characterLevelPlusMinus", StringComparison.OrdinalIgnoreCase))
        {
            int delta = reward.QualityDelta > 0 ? reward.QualityDelta : 10;
            long maximum = Math.Min(int.MaxValue - 1L, (long)level + delta);
            int minimum = (int)Math.Max(1L, (long)level - delta);
            return random.Next(minimum, (int)maximum + 1);
        }
        return Math.Max(1, reward.Quality);
    }

    bool TryPickProfessionNano(DailyLoginDayReward reward, int level, int profession, Random random,
        out int itemId, out int quality)
    {
        itemId = 0;
        quality = 1;
        if (!rewards.TryGetProfessionNanos(profession, out IReadOnlyList<DailyLoginNanoReward> pool))
            return false;
        int delta = reward.QualityDelta > 0 ? reward.QualityDelta : 10;
        long minimum = Math.Max(1L, (long)level - delta);
        long maximum = (long)level + delta;
        long bestDistance = long.MaxValue;
        var candidates = new List<(int Id, int Quality)>();
        foreach (DailyLoginNanoReward entry in pool)
        {
            if (entry is null || entry.ItemId <= 0 || !templates.TryGet(entry.ItemId, out ItemTemplate template))
                continue;
            int candidateQuality = Math.Max(1, entry.Quality > 0 ? entry.Quality : template.Quality);
            long distance = candidateQuality < minimum ? minimum - candidateQuality
                : candidateQuality > maximum ? candidateQuality - maximum : 0;
            if (distance > bestDistance)
                continue;
            if (distance < bestDistance)
            {
                candidates.Clear();
                bestDistance = distance;
            }
            candidates.Add((entry.ItemId, candidateQuality));
        }
        if (candidates.Count == 0)
            return false;
        (itemId, quality) = candidates[random.Next(candidates.Count)];
        return true;
    }

    void ResolveScalePair(int itemId, int quality, out int lowId, out int highId)
    {
        if (TryGetRelationPair(itemId, out lowId, out highId))
            return;
        lowId = highId = itemId;
        ItemTemplate seed = templates.Require(itemId);
        ItemTemplate? resolvedLow = null;
        foreach (int id in seed.Relations)
        {
            if (templates.TryGet(id, out ItemTemplate related) && related.Quality <= quality
                && (resolvedLow is null || related.Quality > resolvedLow.Quality))
                resolvedLow = related;
        }
        if (resolvedLow is not null)
        {
            if (TryGetRelationPair(resolvedLow.Id, out int pairLow, out int pairHigh))
            {
                lowId = pairLow;
                highId = pairHigh;
                return;
            }
            ItemTemplate? resolvedHigh = null;
            foreach (int id in resolvedLow.Relations)
            {
                if (templates.TryGet(id, out ItemTemplate related) && related.Quality >= quality
                    && (resolvedHigh is null || related.Quality < resolvedHigh.Quality))
                    resolvedHigh = related;
            }
            if (resolvedHigh is not null)
            {
                lowId = resolvedLow.Id;
                highId = resolvedHigh.Id;
                return;
            }
        }
    }

    bool IsDataFamilyLowEndpoint(int itemId, FamilyCache families)
    {
        if (!templates.TryGet(itemId, out ItemTemplate seed))
            return false;
        int[] qualities = FamilyComponent(itemId, families).SelectMany(family => family.TemplateIds)
            .Distinct().Where(id => templates.TryGet(id, out _)).Select(id => templates.Require(id).Quality).ToArray();
        return qualities.Length > 0 && qualities.Min() < qualities.Max() && seed.Quality == qualities.Min();
    }

    bool TryResolveDataFamily(int itemId, int wanted, FamilyCache cache, out int lowId, out int highId, out int quality)
    {
        lowId = highId = itemId;
        quality = wanted;
        HashInstance[] families = FamilyComponent(itemId, cache)
            .Where(family => family.TemplateIds.All(id => templates.TryGet(id, out _))).ToArray();
        if (families.Length == 0)
            return false;
        int[] qualities = families.SelectMany(family => family.TemplateIds).Select(id => templates.Require(id).Quality).ToArray();
        if (qualities.Min() == qualities.Max() && wanted != qualities[0])
            return false;
        quality = Math.Clamp(wanted, Math.Max(1, qualities.Min()), Math.Max(1, qualities.Max()));
        int bestWidth = int.MaxValue;
        bool found = false;
        foreach (HashInstance family in families)
        {
            int min = family.TemplateIds.Min(id => templates.Require(id).Quality);
            int max = family.TemplateIds.Max(id => templates.Require(id).Quality);
            if (quality < min || quality > max || max - min > bestWidth
                || !HashItemCatalog.TrySelectIds(family, quality, id => templates.Require(id).Quality,
                    out int selectedLow, out int selectedHigh))
                continue;
            lowId = selectedLow;
            highId = selectedHigh;
            bestWidth = max - min;
            found = true;
        }
        return found;
    }

    static HashInstance[] FamilyComponent(int seed, FamilyCache cache)
        => cache.Components.GetOrAdd(seed, id =>
        {
            var seen = new HashSet<int> { id };
            var families = new HashSet<HashInstance>();
            var pending = new Queue<int>();
            pending.Enqueue(id);
            while (pending.TryDequeue(out int current))
            {
                if (!cache.Index.TryGetValue(current, out List<HashInstance>? related))
                    continue;
                foreach (HashInstance family in related)
                {
                    if (!families.Add(family))
                        continue;
                    foreach (int member in family.TemplateIds)
                        if (seen.Add(member)) pending.Enqueue(member);
                }
            }
            return families.OrderBy(family => family.Hash, StringComparer.Ordinal).ToArray();
        });

    static Dictionary<int, List<HashInstance>> IndexFamilies(IEnumerable<HashInstance> families)
    {
        var index = new Dictionary<int, List<HashInstance>>();
        foreach (HashInstance family in families)
        {
            foreach (int id in family.TemplateIds.Distinct())
            {
                if (!index.TryGetValue(id, out List<HashInstance>? entries))
                    index.Add(id, entries = []);
                entries.Add(family);
            }
        }
        return index;
    }

    bool TryGetRelationPair(int itemId, out int lowId, out int highId)
    {
        lowId = highId = itemId;
        if (!templates.TryGet(itemId, out ItemTemplate seed) || seed.Relations.Count < 2)
            return false;
        int lowQuality = int.MaxValue;
        int highQuality = int.MinValue;
        foreach (int relatedId in seed.Relations)
        {
            if (!templates.TryGet(relatedId, out ItemTemplate related))
                continue;
            if (related.Quality < lowQuality)
            {
                lowId = relatedId;
                lowQuality = related.Quality;
            }
            if (related.Quality > highQuality)
            {
                highId = relatedId;
                highQuality = related.Quality;
            }
        }
        return lowId > 0 && highId > 0 && lowId != highId && lowQuality < highQuality;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
