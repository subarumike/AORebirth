namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Commands;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class EquipmentLootTests
{
    // Synthetic families exercise all referenced hashes without supplying retail pool membership.
    const string CatalogJson = """
        {
            "AABC": {"Children":["AAB1"]},
            "AAB1": {"Children":["AAB2","AABC","FIXD"]},
            "AAB2": {"Templates":[1,2]},
            "ARMO": {"Templates":[3,4]},
            "CLOH": {"Templates":[5,6]},
            "COOH": {"Templates":[7,8]},
            "DIST": {"Templates":[9,10]},
            "INDI": {"Templates":[11]},
            "ITDC": {"Templates":[12]},
            "MELE": {"Templates":[13,14]},
            "OUDY": {"Children":["GONE","FIXD"]},
            "FIXD": {"Templates":[15]}
        }
        """;

    [TestMethod]
    [DataRow("AABC", 1, 2, 100)]
    [DataRow("ARMO", 3, 4, 100)]
    [DataRow("CLOH", 5, 6, 90)]
    [DataRow("COOH", 7, 8, 100)]
    [DataRow("DIST", 9, 10, 100)]
    [DataRow("INDI", 11, 11, 75)]
    [DataRow("ITDC", 12, 12, 80)]
    [DataRow("MELE", 13, 14, 100)]
    [DataRow("OUDY", 15, 15, 60)]
    public void HighestUpToResolvesNestedBranchesAndPreservesEligibleQualities(
        string hash, int lowId, int highId, int quality)
    {
        var fixture = new LootFixture();

        Assert.IsTrue(fixture.Minter.TryRollHighestUpTo(hash, 100, out int low, out int high, out int rolled));
        Assert.AreEqual((lowId, highId, quality), (low, high, rolled));
        Assert.IsTrue(fixture.Catalog.TryGet(low, out _));
        Assert.IsTrue(fixture.Catalog.TryGet(high, out _));
    }

    [TestMethod]
    public void NestedFixedBranchIsEligibleBeforeScalingMinimumAndNeverExceedsRequest()
    {
        var minter = new LootFixture().Minter;

        Assert.IsFalse(minter.TryRollHighestUpTo("AABC", 59, out _, out _, out _));
        Assert.IsTrue(minter.TryRollHighestUpTo("AABC", 65, out int low, out int high, out int quality));
        Assert.AreEqual((15, 15, 60), (low, high, quality));
        Assert.IsFalse(minter.TryRollHighestUpTo("OUDY", 59, out _, out _, out _));
        Assert.IsFalse(minter.TryRollHighestUpTo("GONE", 100, out _, out _, out _));
    }

    [TestMethod]
    public void ScalingPoolsShareAcrossEquipmentAtTheSameCentreAndStillClampPerPool()
    {
        var fixture = new LootFixture();
        Item[] equipment =
        [
            Wear("Modifier", ("AABC", 100), ("INDI", 100), ("MELE", 120)),
            Wear("Lootgiver", ("ARMO", 100), ("CLOH", 100), ("COOH", 120))
        ];
        var random = new QualityDraws(104, 75, 118);
        var drops = new List<(int, int, int)>();

        EquipmentLoot.Roll(equipment, fixture.Minter, 20, 25, drops, true, random);

        CollectionAssert.AreEqual(new[] { (1, 2, 104), (11, 11, 75), (13, 14, 118),
            (3, 4, 104), (5, 6, 90), (7, 8, 118) }, drops);
        Assert.AreEqual(0, random.Remaining);
    }

    [TestMethod]
    public void OrdinaryEquipmentStillRollsIndependentlyAndRespectsRemainingCapacity()
    {
        var fixture = new LootFixture();
        Item[] equipment = [Wear("Equipment", ("AABC", 100), ("ARMO", 100), ("COOH", 100))];
        var random = new QualityDraws(84, 115);
        var drops = new List<(int, int, int)> { (99, 99, 1) };

        EquipmentLoot.Roll(equipment, fixture.Minter, 2, 25, drops, false, random);

        CollectionAssert.AreEqual(new[] { (99, 99, 1), (1, 2, 84), (3, 4, 115) }, drops);
        Assert.AreEqual(0, random.Remaining);
    }

    [TestMethod]
    public void SharedRollsAreLocalToOneCorpseAndLeaveOrdinaryItemTableIndependent()
    {
        var fixture = new LootFixture();
        Item[] equipment = [Wear("Modifier", ("ARMO", 100), ("COOH", 100))];
        var random = new QualityDraws(84, 115);
        var drops = new List<(int, int, int)>();
        var table = new[] { new MobItemTableEntry { Hash = "DIST", Chance = 100, Repeats = 1 } };

        EquipmentLoot.Roll(equipment, fixture.Minter, 3, 25, drops, true, random);
        LootableDynel.RollItemTable(table, 100, fixture.Minter, 1, 0, drops);
        CollectionAssert.AreEqual(new[] { (3, 4, 84), (7, 8, 84), (9, 10, 100) }, drops);

        drops.Clear();
        EquipmentLoot.Roll(equipment, fixture.Minter, 3, 25, drops, true, random);
        CollectionAssert.AreEqual(new[] { (3, 4, 115), (7, 8, 115) }, drops);
        Assert.AreEqual(0, random.Remaining);
    }

    [TestMethod]
    [DataRow(SpawnSource.HashSpawn, true, false, false, true, true)]
    [DataRow(SpawnSource.HashSpawn, true, true, false, true, false)]
    [DataRow(SpawnSource.HashSpawn, true, false, true, true, false)]
    [DataRow(SpawnSource.Command, true, false, false, true, false)]
    [DataRow(SpawnSource.HashSpawn, false, false, false, true, false)]
    [DataRow(SpawnSource.HashSpawn, true, false, false, false, false)]
    public void OnlyOutdoorRubiKaHashBossesWithModifierEquipmentShare(
        SpawnSource source, bool boss, bool indoor, bool shadowlands, bool modifier, bool expected)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var npc = CreateNpc(services, new PlayfieldMetaData
        {
            IsIndoor = indoor,
            UnknownV9A = shadowlands ? PlayfieldEntryFlags.ShadowlandsMetaDataBit : 0
        });
        npc.SpawnSource = source;
        if (!boss)
            npc.MobTemplate!.Features.Clear();
        if (!modifier)
            npc.Equipment.Remove(0);

        Assert.AreEqual(expected, npc.SharesEquipmentLootQuality);
    }

    [TestMethod]
    public void CorpseAndGmSimulationUseTheSameEquipmentAndOrdinaryTable()
    {
        var fixture = new LootFixture();
        var settings = new GameplaySettings { Loot = new GameplayLoot { QualityVariancePercent = 0 } };
        using var services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
        var npc = CreateNpc(services, new PlayfieldMetaData());
        npc.MobTemplate!.ItemTable.Add(new MobItemTableEntry { Hash = "DIST", Chance = 100, Repeats = 1 });
        var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 2 }, npc, fixture.Data);

        corpse.ResolveLoot(fixture.Minter, 0);
        CollectionAssert.AreEqual(new[] { (3, 4, 100), (7, 8, 100), (9, 10, 100) },
            corpse.Loot.EnumerateSlots().Select(x => (x.Value.LowId, x.Value.HighId, x.Value.Quality)).ToArray());

        var command = new NpcCommand(fixture.Data, fixture.Catalog, fixture.Minter);
        MethodInfo simulate = typeof(NpcCommand).GetMethod("SimulateLoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var report = (IReadOnlyList<string>)simulate.Invoke(command, [npc, 1])!;
        StringAssert.Contains(string.Join("\n", report), "Items: 3 (3 per corpse)");
    }

    static NpcCharacter CreateNpc(IServiceProvider services, PlayfieldMetaData metadata)
    {
        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<MetaData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(playfield, metadata);
        typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(playfield, services);
        var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 }, new StubItemBuilder())
        {
            Playfield = playfield,
            SpawnSource = SpawnSource.HashSpawn,
            MobTemplate = new MobTemplate { Features = [NpcFeature.Boss] }
        };
        npc.Stats.Set(CharacterStat.Level, 100);
        npc.Equipment.Add(0, Wear("Boss Modifier Item (Fixture)", ("ARMO", 100), ("COOH", 100)));
        return npc;
    }

    static Item Wear(string name, params (string Hash, int Centre)[] spawns)
    {
        Item item = TestWorld.CreateItem(name: name);
        item.Definition.SpellList[EventType.OnWear] = spawns.Select(spawn => new ItemSpell
        {
            FunctionType = (int)FunctionType.SpawnItem,
            Arguments = [spawn.Hash, spawn.Centre]
        }).ToList();
        return item;
    }

    sealed class QualityDraws(params int[] draws) : Random
    {
        readonly Queue<int> _draws = new(draws);

        public int Remaining => _draws.Count;

        public override int Next(int minValue, int maxValue)
        {
            int result = _draws.Dequeue();
            Assert.IsTrue(result >= minValue && result < maxValue);
            return result;
        }
    }

    sealed class LootFixture
    {
        public StubCatalog Catalog { get; } = new StubCatalog()
            .Add(1, 80).Add(2, 200).Add(3, 1).Add(4, 200).Add(5, 1).Add(6, 90)
            .Add(7, 1).Add(8, 200).Add(9, 1).Add(10, 200).Add(11, 75, flags: (int)ItemFlags.Special)
            .Add(12, 80).Add(13, 1).Add(14, 200).Add(15, 60);

        public StubGameData Data { get; } = new(HashItemCatalog.Parse(CatalogJson));

        public HashItemMinter Minter { get; }

        public LootFixture() => Minter = new HashItemMinter(Data, Catalog, new StubItemBuilder());
    }
}
