namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
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
using Vector3 = AORebirth.Core.Vector.Vector3;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Commands;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
[DoNotParallelize]
public sealed class RkDynaPrivateLootIntegrationTests
{
    const int VariancePercent = 25;
    static readonly string[] ReconstructedHashes = ["AABC", "ARMO", "DIST", "INDI", "ITDC", "MELE", "OUDY"];
    static readonly Lazy<PrivateCatalogs> Canonical = new(() => new PrivateCatalogs(PrivateRoot()));

    [TestMethod]
    [DataRow(605, 153, "The Collector", false)]
    [DataRow(570, 250, "Critical Hit Boss", false)]
    [DataRow(605, 153, "The Collector", true)]
    public void CanonicalBossEquipmentMintsThroughSpawnServiceAndSharesACompatibleRequestedQl(
        int playfieldId, int level, string modifier, bool fillCapacity)
    {
        PrivateCatalogs catalogs = Canonical.Value;
        using var world = new LootWorld(catalogs, playfieldId);
        NpcCharacter npc = world.Spawn(level, modifier, militaryBoss: playfieldId == 570);
        Assert.IsTrue(npc.SharesEquipmentLootQuality);
        if (playfieldId == 570)
        {
            Assert.AreNotEqual("Boss Monster", npc.MobTemplate!.Name);
            Assert.IsTrue(npc.MobTemplate.ItemTable.Count > 0, "The named boss keeps its original item table.");
        }

        var functions = new List<(string Hash, int Center)>();
        foreach (Item item in npc.Equipment.Content.Values)
        {
            foreach (ItemSpell spell in EquipmentLoot.SpawnFunctions(item))
            {
                Assert.IsTrue(EquipmentLoot.TryReadSpawn(spell, out string hash, out int center));
                Assert.AreEqual(level, center, $"The real equipment interpolates its SpawnItem center for {hash}.");
                functions.Add((hash, center));
            }
        }

        string[] roots = functions.Select(source => source.Hash).Distinct().ToArray();
        if (modifier == "Critical Hit Boss")
            CollectionAssert.AreEquivalent(ReconstructedHashes, roots);
        else
        {
            CollectionAssert.Contains(roots, "CLOH");
            CollectionAssert.Contains(roots, "COOH");
        }

        Item[] carried = fillCapacity
            ? Enumerable.Range(0, Corpse.LootCapacity - 1)
                .Select(_ => world.Items.CreateWithNewInstance(202328, 202328, 1, ItemSource.Loot)).ToArray()
            : [];
        foreach (Item item in carried)
            Assert.IsTrue(npc.TryCarryLoot(item));

        int allocatedBeforeCorpse = world.Ids.Count;
        Corpse corpse = world.Spawns.SpawnCorpse(npc);
        Assert.IsTrue(world.Registry.TryGet(corpse.Identity, out Dynel? registered));
        Assert.AreSame(corpse, registered);
        Item[] drops = corpse.Loot.EnumerateSlots().Select(slot => slot.Value).ToArray();
        Assert.IsTrue(drops.All(item => item.InstanceId > 0 && item.Source == ItemSource.Loot));
        Assert.AreEqual(drops.Length, drops.Select(item => item.InstanceId).Distinct().Count());
        Assert.AreEqual(drops.Length - carried.Length, world.Ids.Count - allocatedBeforeCorpse);
        CollectionAssert.AreEqual(carried, drops.Take(carried.Length).ToArray());
        if (fillCapacity)
            Assert.AreEqual(Corpse.LootCapacity, drops.Length);
        else
            Assert.IsTrue(drops.Length >= functions.Count, "Every worn SpawnItem function has an eligible item here.");

        (int minimum, int maximum) = LootableDynel.QualityWindow(level, VariancePercent);
        var commonRequestedQualities = Enumerable.Range(minimum, maximum - minimum + 1).ToHashSet();
        int equipmentSlots = Math.Min(functions.Count, drops.Length - carried.Length);
        for (int index = 0; index < equipmentSlots; index++)
        {
            (string hash, int center) = functions[index];
            Item drop = drops[carried.Length + index];
            Assert.IsTrue(catalogs.Items.TryGet(drop.LowId, out _));
            Assert.IsTrue(catalogs.Items.TryGet(drop.HighId, out _));
            commonRequestedQualities.RemoveWhere(quality => !catalogs.CanProduce(hash, center, quality, drop));
            Assert.IsTrue(commonRequestedQualities.Count > 0,
                $"{hash} minted {drop.LowId}:{drop.HighId} QL {drop.Quality} outside the common equipment roll.");
        }

        if (!fillCapacity)
        {
            int allocatedBeforeGm = world.Ids.Count;
            var command = new NpcCommand(catalogs.Data, catalogs.Items, world.Minter);
            MethodInfo simulate = typeof(NpcCommand).GetMethod("SimulateLoot", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var report = (IReadOnlyList<string>)simulate.Invoke(command, [npc, 1])!;
            StringAssert.Contains(string.Join("\n", report), $"Items: {drops.Length} ({drops.Length} per corpse)");
            Assert.AreEqual(allocatedBeforeGm, world.Ids.Count, "The GM preview does not mint items.");
        }
    }

    [TestMethod]
    public void ProductionItemTemplatesReloadInvalidatesWarmHashCachesAndReconstructedMembers()
    {
        string root = Directory.CreateTempSubdirectory("ao-rk-loot-reload-").FullName;
        string path = Path.Combine(root, GameDataPaths.ItemTemplatesFileName);
        try
        {
            Assert.AreEqual(path, GameDataPaths.ResolveFile(root, GameDataPaths.ItemTemplatesFileName));
            File.WriteAllText(path, """
                { "NEST": {"Children":["ORDN"]}, "ORDN":{"Templates":[1,2]},
                  "ROUT":{"Children":["WEAR"],"EquipmentLoot":{"WEAR":{"Weight":1,"MinCenter":1,"MaxCenter":200}}},
                  "WEAR":{"Templates":[3],"EquipmentLootOnly":true} }
                """);
            var logger = new StubLogger();
            var data = new GameDataStore(logger, null, root);
            var items = new StubCatalog().Add(1, 1).Add(2, 200).Add(3, 40).Add(4, 70).Add(5, 90);
            var minter = new HashItemMinter(data, items, new StubItemBuilder());
            var reload = new GameDataReloadRegistry([
                new ItemTemplatesReload(data, new KnubotCatalog(data, new QuestCatalog(data, logger), items, logger), minter)
            ]);
            Assert.IsTrue(minter.HasScalingFamily("NEST"));
            Assert.IsTrue(minter.TryRollHighestUpTo("NEST", 100, out int low, out int high, out int quality));
            Assert.AreEqual((1, 2, 100), (low, high, quality));
            Assert.IsTrue(minter.TryRollEquipmentLoot(minter.GetEquipmentLootMembers("ROUT")!, 100, 100,
                new Random(1), out low, out high, out quality));
            Assert.AreEqual((3, 3, 40), (low, high, quality));

            File.WriteAllText(path, """
                { "NEST":{"Children":["ORDN"]}, "ORDN":{"Templates":[4]},
                  "ROUT":{"Children":["WEAR"],"EquipmentLoot":{"WEAR":{"Weight":1,"MinCenter":1,"MaxCenter":200}}},
                  "WEAR":{"Templates":[5],"EquipmentLootOnly":true} }
                """);
            var updated = reload.Reload("ItemTemplates");
            Assert.IsTrue(updated.Ok, string.Join("; ", updated.Problems));
            Assert.IsFalse(minter.HasScalingFamily("NEST"));
            Assert.IsTrue(minter.TryRollHighestUpTo("NEST", 100, out low, out high, out quality));
            Assert.AreEqual((4, 4, 70), (low, high, quality));
            Assert.IsTrue(minter.TryRollEquipmentLoot(minter.GetEquipmentLootMembers("ROUT")!, 100, 100,
                new Random(1), out low, out high, out quality));
            Assert.AreEqual((5, 5, 90), (low, high, quality));
            Assert.IsFalse(data.CanResolveItemHash("WEAR"));

            File.WriteAllText(path, "{ invalid json");
            Assert.IsFalse(reload.Reload("ItemTemplates").Ok);
            Assert.IsTrue(minter.TryRollHighestUpTo("NEST", 100, out low, out high, out quality));
            Assert.AreEqual((4, 4, 70), (low, high, quality));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(root);
        }
    }

    static string PrivateRoot()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_RK_DYNA_TEST_GAMEDATA");
        if (string.IsNullOrWhiteSpace(root))
            Assert.Inconclusive("Set AO_REBIRTH_RK_DYNA_TEST_GAMEDATA for the private catalog integration check.");
        return Path.GetFullPath(root!);
    }

    sealed class PrivateCatalogs
    {
        public GameDataStore Data { get; }

        public ItemTemplateCatalog Items { get; }

        public PrivateCatalogs(string root)
        {
            Assert.AreEqual(Path.Combine(root, GameDataPaths.ItemTemplatesFileName),
                GameDataPaths.ResolveFile(root, GameDataPaths.ItemTemplatesFileName));
            var logger = new StubLogger();
            Data = new GameDataStore(logger, null, root);
            Items = new ItemTemplateCatalog(Data, logger);
            foreach (string hash in ReconstructedHashes)
            {
                var members = Data.GetEquipmentLootMembers(hash);
                Assert.IsNotNull(members, hash);
                Assert.IsTrue(members.Count > 0, hash);
                Assert.IsFalse(Data.CanResolveItemHash(hash), "The equipment policy stays out of general hash resolution.");
                foreach (EquipmentLootMember member in members)
                {
                    foreach (int id in member.Family.TemplateIds)
                        Assert.IsTrue(Items.TryGet(id, out _), $"{hash}/{member.Family.Hash}/{id}");
                }
            }
        }

        public bool CanProduce(string hash, int center, int wanted, Item drop)
        {
            var members = Data.GetEquipmentLootMembers(hash);
            if (members != null)
            {
                return members.Any(member => center >= member.MinCenter && center <= member.MaxCenter
                    && Reaches(member.Family, wanted, drop, fixedAboveWanted: true));
            }

            var leaves = new List<HashInstance>();
            Data.CollectHashLeafInstances(hash, leaves);
            int highest = leaves.Where(leaf => Minimum(leaf) <= wanted)
                .Select(leaf => Math.Min(wanted, Maximum(leaf))).DefaultIfEmpty(0).Max();
            return drop.Quality == highest && leaves.Any(leaf => Reaches(leaf, wanted, drop, fixedAboveWanted: false));
        }

        bool Reaches(HashInstance family, int wanted, Item drop, bool fixedAboveWanted)
        {
            int minimum = Minimum(family);
            int maximum = Maximum(family);
            int quality = fixedAboveWanted && minimum == maximum ? minimum : Math.Min(wanted, maximum);
            return quality >= minimum && quality == drop.Quality
                && HashItemCatalog.TrySelectIds(family, quality, CatalogQuality, out int low, out int high)
                && (low, high) == (drop.LowId, drop.HighId);
        }

        int CatalogQuality(int id) => Math.Max(1, Items.Require(id).Quality);

        int Minimum(HashInstance family) => family.TemplateIds.Min(CatalogQuality);

        int Maximum(HashInstance family) => family.TemplateIds.Max(CatalogQuality);
    }

    sealed class CountingIds : IItemInstanceIdAllocator
    {
        public int Count { get; private set; }

        public int Allocate() => 500000 + ++Count;
    }

    sealed class LootWorld : IDisposable
    {
        readonly PrivateCatalogs _catalogs;
        readonly ServiceProvider _services;
        readonly Playfield _playfield = Blank<Playfield>();

        public DynelRegistry Registry { get; } = new();

        public CountingIds Ids { get; } = new();

        public ItemBuilder Items { get; }

        public HashItemMinter Minter { get; }

        public SpawnService Spawns { get; }

        public LootWorld(PrivateCatalogs catalogs, int playfieldId)
        {
            _catalogs = catalogs;
            var logger = new StubLogger();
            PlayfieldMetaData? metadata = catalogs.Data.GetPlayfieldMetaData(playfieldId);
            Assert.IsNotNull(metadata);
            SetField("<ResourceId>k__BackingField", playfieldId);
            SetField("<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = playfieldId });
            SetField("<MetaData>k__BackingField", metadata);
            SetField("_lifecycleSync", new System.Threading.Lock());
            SetField("_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            var settings = new GameplaySettings { Loot = new GameplayLoot { QualityVariancePercent = VariancePercent } };
            _services = new ServiceCollection()
                .AddSingleton(Registry)
                .AddSingleton<IGameData>(catalogs.Data)
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(new WorldSimulationAccess())
                .AddSingleton(new PlayfieldLocality(playfieldId, metadata))
                .AddSingleton(settings)
                .BuildServiceProvider();
            SetField("_serviceProvider", _services);
            Items = new ItemBuilder(catalogs.Items, Ids, logger);
            Minter = new HashItemMinter(catalogs.Data, catalogs.Items, Items);
            Spawns = new SpawnService(_services, Registry, logger, _playfield, Blank<PlayfieldManager>(), catalogs.Data,
                Items, Minter, Blank<InventoryFlushService>(), Blank<TradeService>(), Blank<CharacterSnapshotService>());
        }

        public NpcCharacter Spawn(int level, string modifier, bool militaryBoss)
        {
            string selected = $"Boss Modifier Item ({modifier})";
            foreach (PlayfieldSpawnEntry placement in _catalogs.Data.GetPlayfieldSpawns(_playfield.ResourceId).Spawns)
            {
                if (level < placement.MinLevel || level > placement.MaxLevel
                    || !_catalogs.Data.TryResolveMobTemplate(placement.HashText, level, out MobTemplate template)
                    || !template.Has(NpcFeature.Boss) || militaryBoss != (template.ItemTable.Count > 0)
                    || !template.Equipment.Any(pair => _catalogs.Items.TryGet(pair[0], out ItemTemplate item) && item.Name == selected))
                    continue;

                // Pin only the random modifier choice while retaining the actual equipment and wear functions.
                template.Equipment.RemoveAll(pair => _catalogs.Items.TryGet(pair[0], out ItemTemplate item)
                    && item.Name.StartsWith("Boss Modifier Item", StringComparison.OrdinalIgnoreCase) && item.Name != selected);
                return Spawns.SpawnMob(template, new Vector3(placement.Position[0], placement.Position[1], placement.Position[2]),
                    level: level, spawnSource: SpawnSource.HashSpawn, spawnHash: placement.HashText, attachDefaultBrain: false);
            }

            throw new AssertFailedException($"No qualifying level {level} boss for {modifier} in playfield {_playfield.ResourceId}.");
        }

        void SetField<T>(string name, T value)
            => typeof(Playfield).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_playfield, value);

        static T Blank<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        public void Dispose() => _services.Dispose();
    }
}
