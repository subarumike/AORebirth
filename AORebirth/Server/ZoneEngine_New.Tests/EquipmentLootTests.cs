namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
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
    const string ReconstructedJson = """
        {
            "GEAR": {"Templates":[1,2]},
            "PRIVATE_FIXED": {"Templates":[3],"EquipmentLootOnly":true},
            "PRIVATE_BAD": {"Templates":[4,999],"EquipmentLootOnly":true},
            "FIRST": {"Children":["GEAR","PRIVATE_FIXED","PRIVATE_BAD"],"EquipmentLoot":{
                "GEAR":{"Weight":5,"MinCenter":10,"MaxCenter":200},
                "PRIVATE_FIXED":{"Weight":2,"MinCenter":40,"MaxCenter":80},
                "PRIVATE_BAD":{"Weight":1,"MinCenter":1,"MaxCenter":300}
            }},
            "SECOND": {"Children":["GEAR"],"EquipmentLoot":{"GEAR":{"Weight":1,"MinCenter":1,"MaxCenter":300}}}
        }
        """;

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

    [TestMethod]
    public void ReconstructedPoolsKeepOrdinaryLookupsAndFamiliesIsolated()
    {
        HashItemCatalog data = HashItemCatalog.Parse(ReconstructedJson);
        Assert.AreEqual(1, data.InstanceCount);
        Assert.AreEqual(0, data.CategoryCount);
        Assert.IsTrue(data.TryGetAssignedHash(1, 2, out string assigned));
        Assert.AreEqual("GEAR", assigned);
        Assert.IsFalse(data.TryResolveInstance("FIRST", out _));
        Assert.IsFalse(data.TryGetAssignedHash(3, 3, out _));
        Assert.IsFalse(data.TryGetInstance("PRIVATE_FIXED", out _));
        Assert.AreEqual(3, data.GetEquipmentLootMembers("FIRST")!.Count);
    }

    [TestMethod]
    [DataRow(0, 1, 2, 63)]
    [DataRow(4, 1, 2, 63)]
    [DataRow(5, 3, 3, 100)]
    [DataRow(6, 3, 3, 100)]
    public void WeightedPickerAdmitsFixedAboveDesiredQlAndRejectsMissingTemplates(
        int ticket, int lowId, int highId, int expectedQl)
    {
        HashItemMinter minter = ReconstructedMinter();
        Item[] equipment = [Wear("Modifier", ("FIRST", 60), ("SECOND", 60))];
        var drops = new List<(int, int, int)>();
        var random = new WeightedDraws(63, (ticket, 7), (0, 1));

        EquipmentLoot.Roll(equipment, minter, 20, 25, drops, true, random);

        CollectionAssert.AreEqual(new[] { (lowId, highId, expectedQl), (1, 2, 63) }, drops);
        Assert.AreEqual(0, random.Remaining);
        drops.Clear();
        EquipmentLoot.Roll(equipment, minter, 20, 0, drops, false, new Random(1));
        Assert.AreEqual(0, drops.Count);
    }

    [TestMethod]
    public void ReconstructedPickerRespectsCenterLimitsAndScalingCap()
    {
        var minter = ReconstructedMinter();
        var members = minter.GetEquipmentLootMembers("FIRST")!;
        var random = new WeightedDraws(0, (0, 5));

        Assert.IsFalse(minter.TryRollEquipmentLoot(members, 30, 4, random, out _, out _, out _));
        Assert.IsFalse(minter.TryRollEquipmentLoot(members, 201, 220, random, out _, out _, out _));
        Assert.IsTrue(minter.TryRollEquipmentLoot(members, 100, 115, random, out int low, out int high, out int quality));
        Assert.AreEqual((1, 2, 100), (low, high, quality));
    }

    [TestMethod]
    public void CorpseAndGmSimulationBothUseReconstructedEquipmentAndCapacity()
    {
        var data = new StubGameData(HashItemCatalog.Parse(ReconstructedJson));
        var catalog = new StubCatalog().Add(1, 5).Add(2, 100).Add(3, 100).Add(4, 1);
        var minter = new HashItemMinter(data, catalog, new StubItemBuilder());
        var settings = new GameplaySettings { Loot = new GameplayLoot { QualityVariancePercent = 0 } };
        using var services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
        var npc = CreateNpc(services, new PlayfieldMetaData());
        npc.Equipment.Remove(0);
        npc.Equipment.Add(0, Wear("Boss Modifier Item (Fixture)", ("FIRST", 100), ("SECOND", 100)));
        var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 2 }, npc, data);

        corpse.ResolveLoot(minter, 0);
        CollectionAssert.AreEqual(new[] { (1, 2, 100), (1, 2, 100) },
            corpse.Loot.EnumerateSlots().Select(x => (x.Value.LowId, x.Value.HighId, x.Value.Quality)).ToArray());

        var command = new NpcCommand(data, catalog, minter);
        MethodInfo simulate = typeof(NpcCommand).GetMethod("SimulateLoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var report = (IReadOnlyList<string>)simulate.Invoke(command, [npc, 1])!;
        StringAssert.Contains(string.Join("\n", report), "Items: 2 (2 per corpse)");
    }

    [TestMethod]
    public void GmSimulationReservesCarriedSlotsWithoutConsumingThem()
    {
        var data = new StubGameData(HashItemCatalog.Parse(ReconstructedJson));
        var catalog = new StubCatalog().Add(1, 5).Add(2, 100).Add(3, 100).Add(4, 1);
        var minter = new HashItemMinter(data, catalog, new StubItemBuilder());
        var settings = new GameplaySettings { Loot = new GameplayLoot { QualityVariancePercent = 0 } };
        using var services = new ServiceCollection().AddSingleton(settings).BuildServiceProvider();
        var npc = CreateNpc(services, new PlayfieldMetaData());
        npc.Equipment.Remove(0);
        npc.Equipment.Add(0, Wear("Boss Modifier Item (Fixture)", ("FIRST", 100), ("SECOND", 100)));
        for (int i = 0; i < Corpse.LootCapacity - 1; i++)
        {
            Assert.IsTrue(npc.TryCarryLoot(TestWorld.CreateItem(lowId: 3, highId: 3, quality: 100, instanceId: 50 + i)));
        }

        var command = new NpcCommand(data, catalog, minter);
        MethodInfo simulate = typeof(NpcCommand).GetMethod("SimulateLoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var report = (IReadOnlyList<string>)simulate.Invoke(command, [npc, 2])!;
        StringAssert.Contains(string.Join("\n", report), $"Items: {Corpse.LootCapacity * 2} ({Corpse.LootCapacity} per corpse)");
        Assert.AreEqual(Corpse.LootCapacity - 1, npc.CarriedLoot.Count);

        var corpse = new Corpse(new Identity { Type = IdentityType.Corpse, Instance = 2 }, npc, data);
        corpse.ResolveLoot(minter, 0);
        Assert.AreEqual(Corpse.LootCapacity, corpse.Loot.Content.Count);
        Assert.AreEqual(3, corpse.Loot.Content[Corpse.LootCapacity - 2].LowId);
        Assert.AreEqual(1, corpse.Loot.Content[Corpse.LootCapacity - 1].LowId);
        Assert.AreEqual(0, npc.CarriedLoot.Count);
    }

    [TestMethod]
    public void MalformedEquipmentPolicyFailsClosed()
    {
        Assert.ThrowsExactly<FormatException>(() => HashItemCatalog.Parse(
            """{"BAD":{"Children":["MISSING"],"EquipmentLoot":{"MISSING":{"Weight":1,"MinCenter":1,"MaxCenter":99}}}}"""));
        Assert.ThrowsExactly<FormatException>(() => HashItemCatalog.Parse(
            """{"BAD":{"Children":["LEAF"],"EquipmentLoot":{"LEAF":{"Weight":0,"MinCenter":1,"MaxCenter":99}}},"LEAF":{"Templates":[1]}}"""));
    }

    [TestMethod]
    public void CanonicalReconstructedPoolsLoadAndMintThroughTheExistingItemBuilder()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_RK_DYNA_TEST_GAMEDATA");
        if (string.IsNullOrEmpty(root))
        {
            Assert.Inconclusive("Set AO_REBIRTH_RK_DYNA_TEST_GAMEDATA for the private catalog check.");
            return;
        }

        HashItemCatalog hashes = HashItemCatalog.Parse(File.ReadAllText(Path.Combine(root, "ItemTemplates.json")));
        Assert.IsTrue(hashes.TryGetCategory("CLOH", out _));
        Assert.IsTrue(hashes.TryGetCategory("COOH", out _));
        var data = new StubGameData(hashes, rootPath: root);
        var catalog = new ItemTemplateCatalog(data, new StubLogger());
        var minter = new HashItemMinter(data, catalog, new ItemBuilder(catalog, new StubInstanceIdAllocator(), new StubLogger()));
        foreach (string hash in new[] { "AABC", "ARMO", "DIST", "INDI", "ITDC", "MELE", "OUDY" })
        {
            var members = hashes.GetEquipmentLootMembers(hash);
            Assert.IsNotNull(members, hash);
            Assert.IsFalse(hashes.CanResolveItem(hash), hash);
            foreach (EquipmentLootMember member in members)
            {
                foreach (int id in member.Family.TemplateIds)
                {
                    Assert.IsTrue(catalog.TryGet(id, out _), $"{hash}/{member.Family.Hash}/{id}");
                }
            }

            int center = members[0].MinCenter;
            var drops = new List<(int, int, int)>();
            EquipmentLoot.Roll([Wear("Modifier", (hash, center))], minter, 1, 25, drops, true, new QualityDraws(int.MaxValue));
            Assert.AreEqual(1, drops.Count, hash);
            var drop = drops[0];
            Item item = minter.CreateWithNewInstance(drop.Item1, drop.Item2, drop.Item3, ItemSource.Loot);
            Assert.AreEqual(drop, (item.LowId, item.HighId, item.Quality), hash);
        }
    }

    static HashItemMinter ReconstructedMinter()
    {
        var data = new StubGameData(HashItemCatalog.Parse(ReconstructedJson));
        var catalog = new StubCatalog().Add(1, 5).Add(2, 100).Add(3, 100).Add(4, 1);
        return new HashItemMinter(data, catalog, new StubItemBuilder());
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
            if (result == int.MaxValue)
                return maxValue - 1;
            Assert.IsTrue(result >= minValue && result < maxValue);
            return result;
        }
    }

    sealed class WeightedDraws(int requestedQuality, params (long Ticket, long Total)[] draws) : Random
    {
        readonly Queue<(long Ticket, long Total)> _draws = new(draws);

        public int Remaining => _draws.Count;

        public override int Next(int minValue, int maxValue) => requestedQuality;

        public override long NextInt64(long maxValue)
        {
            (long ticket, long total) = _draws.Dequeue();
            Assert.AreEqual(total, maxValue);
            return ticket;
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
