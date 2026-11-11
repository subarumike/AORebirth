namespace ZoneEngine_New.Core.Entities;

using System;
using System.Collections.Generic;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

internal enum NamedLootRollOutcome
{
    Selected,
    Generated,
    Empty,
    Ineligible,
    ChanceMiss,
    Capacity
}

internal delegate void NamedLootRollObserver(NamedLootResolvedGroup group, string? entryId, NamedLootRollOutcome outcome, int count);

/// <summary>Executes the named groups in the native corpse roll, appending only item IDs and quality.</summary>
internal static class NamedLootRoller
{
    internal static void Roll(NamedLootResolution resolution, HashItemMinter minter, int capacity,
        IReadOnlyList<(int LowId, int HighId, int Quality)> carried, List<(int LowId, int HighId, int Quality)> into,
        Random random, NamedLootRollObserver? observe)
    {
        int end = into.Count + capacity;
        HashSet<(int LowId, int HighId)>? reserved = null;
        foreach (NamedLootResolvedGroup resolved in resolution.Groups)
        {
            NamedLootGroup group = resolved.Group;
            if (into.Count >= end)
            {
                if (observe == null)
                    return;
                observe(resolved, null, NamedLootRollOutcome.Capacity, 1);
                continue;
            }
            if (group.Condition?.Matches(resolution.Context) == false)
            {
                observe?.Invoke(resolved, null, NamedLootRollOutcome.Ineligible, 1);
                continue;
            }
            if (!PassesChance(group.ChanceBasisPoints, random))
            {
                observe?.Invoke(resolved, null, NamedLootRollOutcome.ChanceMiss, 1);
                continue;
            }

            var candidates = new Candidate[group.Entries.Length];
            for (int index = 0; index < candidates.Length; index++)
            {
                NamedLootEntry entry = group.Entries[index];
                bool eligible = TryQualityWindow(entry, resolution.Context, minter, out int min, out int max);
                candidates[index] = new(entry, eligible, min, max);
            }

            bool weighted = group.Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
            bool[]? eligibleDraw = weighted ? new bool[candidates.Length] : null;
            for (int roll = 0; roll < group.RollCount; roll++)
            {
                if (into.Count >= end)
                {
                    observe?.Invoke(resolved, null, NamedLootRollOutcome.Capacity, 1);
                    break;
                }

                if (eligibleDraw != null)
                {
                    int picked = PickWeighted();
                    if (picked >= 0)
                        Emit(candidates[picked]);
                    continue;
                }

                foreach (Candidate candidate in candidates)
                {
                    if (into.Count >= end)
                    {
                        observe?.Invoke(resolved, null, NamedLootRollOutcome.Capacity, 1);
                        break;
                    }
                    if (!CanSelect(candidate))
                        continue;
                    if (group.Mode == "INDEPENDENT" && !PassesChance(candidate.Entry.ChanceBasisPoints, random))
                    {
                        observe?.Invoke(resolved, candidate.Entry.Id, NamedLootRollOutcome.ChanceMiss, 1);
                        continue;
                    }

                    Emit(candidate);
                }
            }

            bool CanSelect(Candidate candidate)
            {
                NamedLootEntry entry = candidate.Entry;
                var itemKey = (entry.LowId, entry.HighId);
                if (candidate.Eligible && reserved?.Contains(itemKey) != true
                    && (!entry.UniquePerCorpse || (!Contains(carried, itemKey) && !Contains(into, itemKey))))
                    return true;
                observe?.Invoke(resolved, entry.Id, NamedLootRollOutcome.Ineligible, 1);
                return false;
            }

            int PickWeighted()
            {
                long weight = group.EmptyWeight;
                for (int index = 0; index < candidates.Length; index++)
                {
                    eligibleDraw![index] = CanSelect(candidates[index]);
                    if (eligibleDraw[index])
                        weight += candidates[index].Entry.Weight;
                }
                if (weight <= 0)
                    return -1;

                long selected = random.NextInt64(weight);
                if (selected < group.EmptyWeight)
                {
                    observe?.Invoke(resolved, null, NamedLootRollOutcome.Empty, 1);
                    return -1;
                }
                selected -= group.EmptyWeight;
                for (int index = 0; index < candidates.Length; index++)
                {
                    if (!eligibleDraw![index])
                        continue;
                    selected -= candidates[index].Entry.Weight;
                    if (selected < 0)
                        return index;
                }
                return -1;
            }

            void Emit(Candidate candidate)
            {
                NamedLootEntry entry = candidate.Entry;
                observe?.Invoke(resolved, entry.Id, NamedLootRollOutcome.Selected, 1);
                int quality = entry.LevelQuality
                    ? Math.Clamp(resolution.Context.Level, candidate.Min, candidate.Max)
                    : NextInclusive(candidate.Min, candidate.Max, random);
                int quantity = entry.UniquePerCorpse ? 1 : NextInclusive(entry.MinQuantity, entry.MaxQuantity, random);
                int count = Math.Min(quantity, end - into.Count);
                for (int index = 0; index < count; index++)
                    into.Add((entry.LowId, entry.HighId, quality));
                if (entry.UniquePerCorpse)
                    (reserved ??= []).Add((entry.LowId, entry.HighId));
                observe?.Invoke(resolved, entry.Id, NamedLootRollOutcome.Generated, count);
                if (count < quantity)
                    observe?.Invoke(resolved, entry.Id, NamedLootRollOutcome.Capacity, quantity - count);
            }
        }
    }

    internal static bool TryQualityWindow(NamedLootEntry entry, NamedLootContext context, HashItemMinter minter,
        out int min, out int max)
    {
        if (entry.Condition?.Matches(context) == false)
        {
            min = 0;
            max = 0;
            return false;
        }
        return minter.TryGetNamedLootQualityWindow(entry.LowId, entry.HighId, entry.MinQuality, entry.MaxQuality, out min, out max);
    }

    static bool PassesChance(int basisPoints, Random random)
        => basisPoints >= 10000 || (basisPoints > 0 && random.Next(10000) < basisPoints);

    static int NextInclusive(int min, int max, Random random)
        => min == max ? min : (int)random.NextInt64(min, (long)max + 1);

    static bool Contains(IReadOnlyList<(int LowId, int HighId, int Quality)> items, (int LowId, int HighId) key)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].LowId == key.LowId && items[index].HighId == key.HighId)
                return true;
        }
        return false;
    }

    readonly record struct Candidate(NamedLootEntry Entry, bool Eligible, int Min, int Max);
}
