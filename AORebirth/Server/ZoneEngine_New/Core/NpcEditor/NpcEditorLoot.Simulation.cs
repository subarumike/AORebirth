namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Commands;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

public sealed record NpcEditorLootSimulationRequest([property: JsonRequired] int Rolls);

public sealed record NpcEditorLootSimulationQuality(int Quality, int Count);

public sealed record NpcEditorLootSimulationItem(int LowId, int HighId, string Name, int TotalDrops, int CorpsesWithItem,
    double CorpseFrequencyPercent, NpcEditorLootSimulationQuality[] Qualities);

public sealed record NpcEditorLootSimulationResult(int Rolls, int EmptyCorpses, double EmptyPercent,
    double AverageItemsPerCorpse, NpcEditorLootSimulationItem[] Items);

internal sealed record NpcEditorLootSimulationSnapshot(int Level, int Variance, MobItemTableEntry[] Table,
    (string Hash, int Quality)[] Equipment, (int LowId, int HighId, int Quality)[] Carried);

public sealed partial class NpcEditorLoot
{
    // Capture only values. The requested rolls execute outside the playfield and never build items.
    internal NpcEditorLootSimulationSnapshot CaptureSimulation(NpcCharacter npc)
    {
        var equipment = new List<(string Hash, int Quality)>();
        foreach (Item worn in npc.Equipment.Content.Values)
        {
            foreach (ItemSpell spell in EquipmentLoot.SpawnFunctions(worn))
            {
                if (EquipmentLoot.TryReadSpawn(spell, out string hash, out int quality))
                    equipment.Add((hash, quality));
            }
        }

        int variance = npc.Playfield?.GetService<GameplaySettings>()?.Loot.QualityVariancePercent
            ?? new GameplayLoot().QualityVariancePercent;
        MobItemTableEntry[] table = (npc.ActiveLootTable ?? [])
            .Where(entry => entry != null).Select(entry => NpcEditorLootEntry.FromNative(entry).ToNative()).ToArray();
        return new(npc.Stats.GetOrOne(CharacterStat.Level), variance, table, [.. equipment],
            npc.InspectCarriedLoot().Take(Corpse.LootCapacity).Select(item => (item.LowId, item.HighId, item.Quality)).ToArray());
    }

    internal NpcEditorLootSimulationResult Simulate(NpcEditorLootSimulationSnapshot snapshot, int rolls, CancellationToken cancellationToken)
    {
        if (rolls is < 1 or > NpcCommand.MaxLootRolls)
            throw new ArgumentException($"Loot simulations allow 1 to {NpcCommand.MaxLootRolls} rolls.");
        HashItemMinter roller = minter ?? throw new InvalidOperationException("The runtime item minter is unavailable.");
        var random = new Random();
        var outcomes = new Dictionary<(int LowId, int HighId), SimulatedItem>();
        var corpse = new List<(int LowId, int HighId, int Quality)>(Corpse.LootCapacity);
        int empty = 0;
        int total = 0;
        for (int roll = 1; roll <= rolls; roll++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            corpse.Clear();
            corpse.AddRange(snapshot.Carried);
            EquipmentLoot.Roll(snapshot.Equipment, roller, Corpse.LootCapacity - corpse.Count, snapshot.Variance, corpse, random);
            LootableDynel.RollItemTable(snapshot.Table, snapshot.Level, roller, Corpse.LootCapacity - corpse.Count,
                snapshot.Variance, corpse, random);
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
                        .Select(row => new NpcEditorLootSimulationQuality(row.Key, row.Value)).ToArray());
            }).ToArray();
        return new(rolls, empty, 100.0 * empty / rolls, (double)total / rolls, results);
    }

    sealed class SimulatedItem
    {
        internal int TotalDrops { get; set; }
        internal int LastCorpse { get; set; }
        internal int Corpses { get; set; }
        internal Dictionary<int, int> Qualities { get; } = new();
    }
}
