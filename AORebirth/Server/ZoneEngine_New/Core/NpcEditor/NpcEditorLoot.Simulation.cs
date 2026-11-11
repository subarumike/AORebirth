namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Commands;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

public sealed record NpcEditorLootSimulationRequest([property: JsonRequired] int Rolls, int? Seed = null);

public sealed record NpcEditorLootSimulationQuality(int Quality, int Count);

public sealed record NpcEditorLootSimulationItem(int LowId, int HighId, string Name, int TotalDrops, int CorpsesWithItem,
    double CorpseFrequencyPercent, NpcEditorLootSimulationQuality[] Qualities, double AverageQuantity);

public sealed record NpcEditorLootSimulationEntry(string EntryId, int Selections, int ItemsGenerated,
    bool InitiallyEligible, double? InitialConditionalPercent);

public sealed record NpcEditorLootSimulationGroup(string QualifiedId, string TableId, string AssignmentId, string GroupId,
    int EntriesSelected, int EmptySelections, int ItemsGenerated, int Ineligible, int ChanceMisses, int CapacitySkips,
    NpcEditorLootSimulationEntry[] Entries);

public sealed record NpcEditorLootSimulationResult(int Rolls, int EmptyCorpses, double EmptyPercent,
    double AverageItemsPerCorpse, NpcEditorLootSimulationItem[] Items, int Seed, NpcEditorLootSimulationGroup[] Groups);

internal sealed record NpcEditorLootSimulationSnapshot(int Level, int Variance, MobItemTableEntry[] Table,
    (string Hash, int Quality)[] Equipment, (int LowId, int HighId, int Quality)[] Carried, NamedLootResolution? Named);

public sealed partial class NpcEditorLoot
{
    // Capture only values. The requested rolls execute outside the playfield and never build items.
    internal NpcEditorLootSimulationSnapshot CaptureSimulation(NpcCharacter npc)
    {
        int variance = npc.Playfield?.GetService<GameplaySettings>()?.Loot.QualityVariancePercent
            ?? new GameplayLoot().QualityVariancePercent;
        MobItemTableEntry[] table = (npc.ActiveLootTable ?? [])
            .Where(entry => entry != null).Select(entry => NpcEditorLootEntry.FromNative(entry).ToNative()).ToArray();
        return new(npc.Stats.GetOrOne(CharacterStat.Level), variance, table,
            EquipmentLoot.ReadInputs(npc.Equipment.Content.Values).ToArray(),
            npc.InspectCarriedLoot().Take(Corpse.LootCapacity).Select(item => (item.LowId, item.HighId, item.Quality)).ToArray(),
            gameData.ResolveNamedLoot(npc));
    }

    internal NpcEditorLootSimulationResult Simulate(NpcEditorLootSimulationSnapshot snapshot, int rolls,
        CancellationToken cancellationToken, int? seed = null)
    {
        if (rolls is < 1 or > NpcCommand.MaxLootRolls)
            throw new ArgumentException($"Loot simulations allow 1 to {NpcCommand.MaxLootRolls} rolls.");
        HashItemMinter roller = minter ?? throw new InvalidOperationException("The runtime item minter is unavailable.");
        int usedSeed = seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        var random = new Random(usedSeed);
        var groups = new Dictionary<(string QualifiedId, string AssignmentId), SimulatedGroup>();
        if (snapshot.Named is { } named)
        {
            foreach (NamedLootResolvedGroup group in named.Groups)
                groups.Add((group.QualifiedId, group.AssignmentId), new(group, named.Context, roller, snapshot.Carried));
        }
        NamedLootRollObserver? observer = groups.Count == 0 ? null : Observe;
        var outcomes = new Dictionary<(int LowId, int HighId), SimulatedItem>();
        var corpse = new List<(int LowId, int HighId, int Quality)>(Corpse.LootCapacity);
        int empty = 0;
        int total = 0;
        for (int roll = 1; roll <= rolls; roll++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            corpse.Clear();
            corpse.AddRange(snapshot.Carried);
            LootableDynel.RollLoot(snapshot.Table, snapshot.Level, snapshot.Equipment, snapshot.Named, snapshot.Carried,
                roller, Corpse.LootCapacity - corpse.Count, snapshot.Variance, corpse, random, observer);
            if (corpse.Count == 0)
                empty++;
            total += corpse.Count;

            foreach ((int lowId, int highId, int quality) in corpse)
            {
                if (!outcomes.TryGetValue((lowId, highId), out SimulatedItem? result))
                {
                    result = new();
                    outcomes.Add((lowId, highId), result);
                }

                result.TotalDrops++;
                result.Qualities[quality] = result.Qualities.GetValueOrDefault(quality) + 1;
                if (result.LastCorpse != roll)
                {
                    result.LastCorpse = roll;
                    result.Corpses++;
                }
            }
        }

        NpcEditorLootSimulationItem[] results = outcomes.OrderByDescending(pair => pair.Value.TotalDrops)
            .ThenBy(pair => pair.Key.LowId).ThenBy(pair => pair.Key.HighId).Select(pair =>
            {
                (int lowId, int highId) = pair.Key;
                string name = items.TryGet(lowId, out ItemTemplate template) || items.TryGet(highId, out template)
                    ? template.Name : "Unknown catalog item";
                SimulatedItem result = pair.Value;
                return new NpcEditorLootSimulationItem(lowId, highId, name, result.TotalDrops, result.Corpses,
                    100.0 * result.Corpses / rolls, result.Qualities.OrderBy(row => row.Key)
                        .Select(row => new NpcEditorLootSimulationQuality(row.Key, row.Value)).ToArray(),
                    (double)result.TotalDrops / rolls);
            }).ToArray();
        return new(rolls, empty, 100.0 * empty / rolls, (double)total / rolls, results, usedSeed,
            groups.Values.Select(group => group.Result()).ToArray());

        void Observe(NamedLootResolvedGroup group, string? entryId, NamedLootRollOutcome outcome, int count)
            => groups[(group.QualifiedId, group.AssignmentId)].Record(entryId, outcome, count);
    }

    sealed class SimulatedGroup
    {
        readonly NamedLootResolvedGroup _group;
        readonly Dictionary<string, SimulatedEntry> _entries = new(StringComparer.Ordinal);
        int _selected;
        int _empty;
        int _generated;
        int _ineligible;
        int _missed;
        int _capacity;

        internal SimulatedGroup(NamedLootResolvedGroup group, NamedLootContext context, HashItemMinter minter,
            IReadOnlyList<(int LowId, int HighId, int Quality)> carried)
        {
            _group = group;
            bool weighted = group.Group.Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
            long weight = group.Group.EmptyWeight;
            foreach (NamedLootEntry entry in group.Group.Entries)
            {
                bool eligible = group.Group.Condition?.Matches(context) != false
                    && NamedLootRoller.TryQualityWindow(entry, context, minter, out _, out _)
                    && (!entry.UniquePerCorpse || !carried.Any(item => item.LowId == entry.LowId && item.HighId == entry.HighId));
                _entries.Add(entry.Id, new(eligible));
                if (eligible && weighted)
                    weight += entry.Weight;
            }
            foreach (NamedLootEntry entry in group.Group.Entries)
            {
                SimulatedEntry state = _entries[entry.Id];
                if (!state.Eligible || group.Group.ChanceBasisPoints == 0)
                    continue;
                state.InitialConditionalPercent = weighted ? (weight > 0 ? 100.0 * entry.Weight / weight : null)
                    : group.Group.Mode == "INDEPENDENT" ? entry.ChanceBasisPoints / 100.0 : 100.0;
            }
        }

        internal void Record(string? entryId, NamedLootRollOutcome outcome, int count)
        {
            switch (outcome)
            {
                case NamedLootRollOutcome.Selected:
                    _selected += count;
                    _entries[entryId!].Selections += count;
                    break;
                case NamedLootRollOutcome.Generated:
                    _generated += count;
                    _entries[entryId!].Generated += count;
                    break;
                case NamedLootRollOutcome.Empty: _empty += count; break;
                case NamedLootRollOutcome.Ineligible: _ineligible += count; break;
                case NamedLootRollOutcome.ChanceMiss: _missed += count; break;
                case NamedLootRollOutcome.Capacity: _capacity += count; break;
            }
        }

        internal NpcEditorLootSimulationGroup Result() => new(_group.QualifiedId, _group.TableId, _group.AssignmentId,
            _group.Group.Id, _selected, _empty, _generated, _ineligible, _missed, _capacity,
            _entries.Select(row => new NpcEditorLootSimulationEntry(row.Key, row.Value.Selections, row.Value.Generated,
                row.Value.Eligible, row.Value.InitialConditionalPercent)).ToArray());
    }

    sealed class SimulatedEntry(bool eligible)
    {
        internal bool Eligible { get; } = eligible;
        internal int Selections { get; set; }
        internal int Generated { get; set; }
        internal double? InitialConditionalPercent { get; set; }
    }

    sealed class SimulatedItem
    {
        internal int TotalDrops { get; set; }
        internal int LastCorpse { get; set; }
        internal int Corpses { get; set; }
        internal Dictionary<int, int> Qualities { get; } = new();
    }
}
