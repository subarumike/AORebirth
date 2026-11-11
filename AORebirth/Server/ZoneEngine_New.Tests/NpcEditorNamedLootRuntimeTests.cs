namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorNamedLootRuntimeTests
{
    const string Enemy = "MOB1";
    const string Actor = "FixtureGM";
    const int CarriedItem = 901;
    const int NativeItem = 902;
    const int EquipmentItem = 903;
    const int NamedLow = 911;
    const int NamedHigh = 912;

    [TestMethod]
    public void NativeCorpseAppendsSavedNamedLootOnceWithinCapacityAndRespectsForfeit()
    {
        using var fixture = new Fixture();
        Corpse ordinary = fixture.Corpse(fixture.Spawn(withPriorLoot: true));
        ordinary.ResolveLoot(fixture.Minter, 0);
        CollectionAssert.AreEqual(new[] { CarriedItem, EquipmentItem, NativeItem, NativeItem },
            ordinary.Loot.EnumerateSlots().Select(row => row.Value.LowId).ToArray());

        var group = new NamedLootGroup("all", "GUARANTEED_ALL", 1, 10_000, 0, null,
            [new("duplicate", CarriedItem, CarriedItem, 20, 20, false, 1, 1, 10_000, 0, true),
             new("named", NamedLow, NamedHigh, 17, 23, true, 20, 20, 10_000, 0, false)]);
        fixture.SaveGroups(group);
        var reopened = new GameDataStore(new StubLogger(), null, fixture.DataRoot, fixture.Overlay);
        reopened.InitializeNamedLoot(fixture.Catalog);
        NpcCharacter fresh = fixture.Spawn(withPriorLoot: true, reopened);
        Corpse corpse = fixture.Corpse(fresh, reopened);
        corpse.ResolveLoot(fixture.Minter, 0);
        Assert.AreEqual(Corpse.LootCapacity, corpse.Loot.Content.Count);
        CollectionAssert.AreEqual(new[] { CarriedItem, EquipmentItem, NativeItem, NativeItem },
            corpse.Loot.EnumerateSlots().Take(4).Select(row => row.Value.LowId).ToArray());
        Assert.IsTrue(corpse.Loot.EnumerateSlots().Skip(4).All(row =>
            row.Value.LowId == NamedLow && row.Value.HighId == NamedHigh && row.Value.Quality == 20 && !row.Value.IsPersisted));
        int allocations = fixture.Allocator.Count;
        corpse.Loot.Content.Clear();
        corpse.ResolveLoot(fixture.Minter, 0);
        Assert.AreEqual(0, corpse.Loot.Content.Count, "Emptying a corpse cannot trigger another loot generation.");
        Assert.AreEqual(allocations, fixture.Allocator.Count);

        Corpse forfeited = fixture.Corpse(fixture.Spawn(withPriorLoot: true, reopened), reopened);
        forfeited.ForfeitSpoils();
        forfeited.ResolveLoot(fixture.Minter, 0);
        Assert.AreEqual(CarriedItem, forfeited.Loot.Content.Values.Single().LowId);
        Assert.AreEqual(allocations, fixture.Allocator.Count);
    }

    [TestMethod]
    public void SeededExclusiveSimulationReportsEligibilityEmptyWeightAndQualityWithoutMinting()
    {
        using var fixture = new Fixture();
        var group = new NamedLootGroup("exclusive", "WEIGHTED_ONE", 1, 10_000, 1, null,
            [new("eligible", NamedLow, NamedHigh, 17, 23, false, 1, 1, 10_000, 3, false),
             new("conditional", NativeItem, NativeItem, 20, 20, false, 1, 1, 10_000, 100, false,
                 new(MinimumLevel: 40))]);
        fixture.SaveGroups(group);
        NpcCharacter npc = fixture.Spawn(withPriorLoot: false);
        npc.LiveEditorLootTable = [];
        var editor = new NpcEditorLoot(fixture.Data, fixture.Catalog, fixture.Minter);
        NpcEditorLootSimulationSnapshot snapshot = editor.CaptureSimulation(npc);
        const int seed = 56789;
        NpcEditorLootSimulationResult result = editor.Simulate(snapshot, 256, CancellationToken.None, seed);
        NpcEditorLootSimulationResult repeated = editor.Simulate(snapshot, 256, CancellationToken.None, seed);
        Assert.AreEqual(seed, result.Seed);
        Assert.AreEqual(JsonSerializer.Serialize(result), JsonSerializer.Serialize(repeated));
        Assert.AreEqual(0, fixture.Allocator.Count);
        Assert.IsTrue(result.EmptyCorpses is > 0 and < 256);
        NpcEditorLootSimulationItem item = result.Items.Single();
        Assert.AreEqual(256 - result.EmptyCorpses, item.TotalDrops);
        Assert.AreEqual(item.TotalDrops, item.CorpsesWithItem);
        Assert.IsTrue(item.Qualities.All(quality => quality.Quality is >= 17 and <= 23));
        NpcEditorLootSimulationGroup report = result.Groups.Single();
        Assert.AreEqual(result.EmptyCorpses, report.EmptySelections);
        Assert.AreEqual(item.TotalDrops, report.EntriesSelected);
        Assert.AreEqual(75.0, report.Entries.Single(entry => entry.EntryId == "eligible").InitialConditionalPercent);
        Assert.IsFalse(report.Entries.Single(entry => entry.EntryId == "conditional").InitiallyEligible);
        Assert.AreEqual(256, report.Ineligible);
    }

    [TestMethod]
    public void WeightedManyAndIndependentGroupsRespectUniqueEntriesAndExplicitZeroChance()
    {
        using var fixture = new Fixture();
        var unique = new NamedLootEntry("unique", NamedLow, NamedHigh, 17, 23, true, 1, 1, 10_000, 10, true);
        var group = new NamedLootGroup("multiple", "WEIGHTED_MANY", 4, 10_000, 0, null, [unique]);
        var independent = new NamedLootGroup("individual", "INDEPENDENT", 1, 10_000, 0, null,
            [unique with { Id = "duplicate", Weight = 0, UniquePerCorpse = false },
             new("never", NativeItem, NativeItem, 20, 20, false, 1, 1, 0, 0, false),
             new("twice", EquipmentItem, EquipmentItem, 20, 20, false, 2, 2, 10_000, 0, false)]);
        fixture.SaveGroups(group, independent);
        NpcCharacter npc = fixture.Spawn(withPriorLoot: false);
        npc.LiveEditorLootTable = [];
        var editor = new NpcEditorLoot(fixture.Data, fixture.Catalog, fixture.Minter);
        var result = editor.Simulate(editor.CaptureSimulation(npc), 12, CancellationToken.None, 8);
        Assert.AreEqual(3.0, result.AverageItemsPerCorpse);
        Assert.AreEqual(12, result.Items.Single(item => item.LowId == NamedLow).TotalDrops);
        Assert.AreEqual(24, result.Items.Single(item => item.LowId == EquipmentItem).TotalDrops);
        Assert.AreEqual(12, result.Groups.Single(group => group.GroupId == "individual").ChanceMisses);
        Assert.AreEqual(0, fixture.Allocator.Count);
    }

    sealed class Fixture : IDisposable
    {
        readonly string _root = Directory.CreateTempSubdirectory("aorebirth-named-loot-runtime-").FullName;
        readonly IItemBuilder _items;
        internal string DataRoot => Path.Combine(_root, "GameData");
        internal string Overlay => Path.Combine(_root, "Overlay");
        internal StubCatalog Catalog { get; } = new StubCatalog().Add(CarriedItem, 20).Add(NativeItem, 20)
            .Add(EquipmentItem, 20).Add(NamedLow, 10).Add(NamedHigh, 30);
        internal CountingAllocator Allocator { get; } = new();
        internal GameDataStore Data { get; }
        internal HashItemMinter Minter { get; }

        internal Fixture()
        {
            Directory.CreateDirectory(Path.Combine(DataRoot, GameDataPaths.PlayfieldsFolderName));
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(Path.Combine(DataRoot, GameDataPaths.NpcTemplatesFileName), """
                {"MOB1":{"Templates":[{"Name":"Named loot fixture","Level":20,
                  "LootTable":[{"Hash":"BASE","Repeats":2,"Chance":100,"LevelMod":0}]}]}}
                """);
            File.WriteAllText(Path.Combine(DataRoot, GameDataPaths.ItemTemplatesFileName), """
                {"BASE":{"Templates":[902]},"WORN":{"Templates":[903]},"PAIR":{"Templates":[911,912]}}
                """);
            var logger = new StubLogger();
            Data = new(logger, null, DataRoot, Overlay);
            Data.InitializeNamedLoot(Catalog);
            _items = new ItemBuilder(Catalog, Allocator, logger);
            Minter = new(Data, Catalog, _items);
        }

        internal void SaveGroups(params NamedLootGroup[] groups)
        {
            var table = new NamedLootTable("fixture", "Fixture table", "", "ordinaryNpc", groups);
            var assignment = new NamedLootAssignment("assign", "template", 0, "append", table.Id, TemplateHash: Enemy);
            Data.ChangeNamedLoot(0, Actor, "saveTable", table.Id, _ => ([table], []));
            Data.ChangeNamedLoot(1, Actor, "saveAssignment", assignment.Id, _ => ([table], [assignment]));
        }

        internal NpcCharacter Spawn(bool withPriorLoot, GameDataStore? data = null)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 77001 }, _items)
            {
                MobTemplate = (data ?? Data).RequireMobTemplate(Enemy)
            };
            npc.Stats.Set(CharacterStat.Level, 20);
            if (withPriorLoot)
            {
                Assert.IsTrue(npc.TryCarryLoot(new Item
                {
                    LowId = CarriedItem, HighId = CarriedItem, Quality = 20, Definition = Catalog.Require(CarriedItem)
                }));
                Assert.IsTrue(npc.Equipment.Add(0, new Item
                {
                    Definition = new ItemTemplate
                    {
                        SpellList = new()
                        {
                            [EventType.OnWear] = [new ItemSpell { FunctionType = (int)FunctionType.SpawnItem, Arguments = ["WORN", 20] }]
                        }
                    }
                }));
            }
            return npc;
        }

        internal Corpse Corpse(NpcCharacter npc, GameDataStore? data = null)
            => new(new Identity { Type = IdentityType.Corpse, Instance = 77501 }, npc, data ?? Data);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    sealed class CountingAllocator : IItemInstanceIdAllocator
    {
        internal int Count { get; private set; }
        public int Allocate() => ++Count;
    }
}
