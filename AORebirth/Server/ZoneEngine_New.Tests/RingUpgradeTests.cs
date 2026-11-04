namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
[DoNotParallelize]
public sealed class RingUpgradeTests
{
    const string RingHash = "RFOT";
    const int LowId = 202717;
    const int HighId = 202718;
    const int OriginalInstance = 41001;
    static readonly Lazy<NativeAssets> Assets = new(NativeAssets.Load);

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void RecordSelectedInputs()
    {
        NativeAssets assets = Assets.Value;
        TestContext.WriteLine("RING_UPGRADE_INPUTS items={0} itemsSha256={1} catalog={2} catalogSha256={3}",
            assets.ItemsPath, assets.ItemsSha256, assets.HashPath, assets.HashSha256);
    }

    [TestMethod]
    public void SelectedFamilyIncludesBothNativeQualityEndpoints()
    {
        NativeAssets assets = Assets.Value;
        Assert.IsTrue(assets.Hashes.TryGetInstance(RingHash, out HashInstance family));
        CollectionAssert.AreEqual(new[] { LowId, HighId }, family.TemplateIds,
            "The selected runtime family must retain both native ring endpoints.");
        Assert.AreEqual(1, assets.Templates.Require(LowId).Quality);
        Assert.AreEqual(200, assets.Templates.Require(HighId).Quality);
    }

    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(50, 51)]
    [DataRow(199, 200)]
    [DataRow(200, 200)]
    public void NativeUseReplacesOneRingAtNextQualityWithinItsNativeCap(int quality, int expectedQuality)
    {
        using var world = new Harness(Assets.Value, quality);
        world.Player.Stats.Set(CharacterStat.Treatment, RequiredTreatment(world.Original) + 1);
        int originalNcu = NcuOf(world.Original);

        Assert.AreEqual(ItemUseStart.Executed, world.Use());

        Item replacement = AssertSingleReplacement(world, expectedQuality);
        Assert.IsTrue(NcuOf(replacement) >= originalNcu, "Upgrading must not lower the native NCU bonus.");
        Assert.IsTrue(NcuOf(replacement) is >= 1 and <= 50);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(100)]
    [DataRow(199)]
    public void NativeTreatmentBoundaryDoesNotDestroyOrGrantUntilTheCriterionPasses(int quality)
    {
        using var world = new Harness(Assets.Value, quality);
        int threshold = RequiredTreatment(world.Original);
        world.Player.Stats.Set(CharacterStat.Treatment, threshold);

        Assert.AreEqual(ItemUseStart.Rejected, world.Use());

        Assert.AreSame(world.Original, world.Player.Inventory.Inventory.Content[64]);
        Assert.AreSame(world.Unrelated, world.Player.Inventory.Inventory.Content[65]);
        Assert.AreEqual(1, world.Original.StackCount);
        Assert.AreEqual(2, world.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, world.Player.Inventory.Overflow.Content.Count);
        Assert.AreEqual(0, world.Session.Messages.OfType<AddTemplateMessage>().Count());

        world.Player.Stats.Set(CharacterStat.Treatment, threshold + 1);
        Assert.AreEqual(ItemUseStart.Executed, world.Use());
        AssertSingleReplacement(world, quality + 1);
    }

    [TestMethod]
    public void NativeNcuCurveIsMonotonicAndRetainsItsEndpointBounds()
    {
        var builder = new ItemBuilder(Assets.Value.Templates, new StubLogger());
        int previous = 0;
        for (int quality = 1; quality <= 200; quality++)
        {
            Item ring = builder.Create(LowId, HighId, quality, ItemSource.Other);
            int ncu = NcuOf(ring);
            Assert.IsTrue(ncu is >= 1 and <= 50, "Native NCU bound at QL " + quality);
            Assert.IsTrue(ncu >= previous, "Native NCU must not decrease at QL " + quality);
            if (quality == 1)
                Assert.AreEqual(1, ncu);
            if (quality == 200)
                Assert.AreEqual(50, ncu);
            previous = ncu;
        }
    }

    static Item AssertSingleReplacement(Harness world, int expectedQuality)
    {
        Assert.AreEqual(0, world.Original.StackCount);
        Assert.IsFalse(world.Player.Inventory.Inventory.Content.Values.Contains(world.Original));
        Assert.AreSame(world.Unrelated, world.Player.Inventory.Inventory.Content[65]);
        Assert.AreEqual(2, world.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, world.Player.Inventory.Overflow.Content.Count);
        Item replacement = world.Player.Inventory.Inventory.Content.Values.Single(item => !ReferenceEquals(item, world.Unrelated));
        Assert.AreEqual(expectedQuality, replacement.Quality, "The actual selected family must produce the requested upgrade.");
        Assert.AreEqual(LowId, replacement.LowId);
        Assert.AreEqual(HighId, replacement.HighId);
        Assert.AreEqual(1, replacement.StackCount);
        Assert.AreNotEqual(OriginalInstance, replacement.InstanceId);
        AddTemplateMessage grant = world.Session.Messages.OfType<AddTemplateMessage>().Single();
        Assert.AreEqual(expectedQuality, grant.Quality);
        Assert.AreEqual(LowId, grant.LowId);
        Assert.AreEqual(HighId, grant.HighId);
        Assert.AreEqual(1, grant.Count);
        return replacement;
    }

    static int RequiredTreatment(Item ring)
    {
        ItemSpell spawn = ring.SpellList[EventType.OnUse].Single(spell => spell.Is(FunctionType.SpawnItem));
        Assert.IsTrue(spawn.TryReadString(0, out string hash));
        Assert.AreEqual(RingHash, hash);
        ItemRequirement criterion = spawn.Requirements.Single(requirement => requirement.StatNumber == (int)CharacterStat.Treatment);
        Assert.AreEqual((int)Operator.GreaterThan, criterion.Operator);
        return criterion.Value;
    }

    static int NcuOf(Item ring)
    {
        var stats = new StatCollection();
        stats.Set(CharacterStat.MaxNCU, 0);
        StatModifierSpells.Apply(ring.SpellList[EventType.OnWear], stats);
        return stats.GetOrZero(CharacterStat.MaxNCU);
    }

    sealed record NativeAssets(HashItemCatalog Hashes, ItemTemplateCatalog Templates, string ItemsPath,
        string ItemsSha256, string HashPath, string HashSha256)
    {
        public static NativeAssets Load()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName)))
                Assert.Inconclusive("Select the actual private GameData with " + GameDataPaths.EnvironmentVariableName + "; private records are not copied into fixtures.");
            string root = GameDataPaths.ResolveRuntimeRoot();
            string itemsPath = Path.Combine(root, GameDataPaths.ItemsFileName);
            string hashPath = GameDataPaths.ResolveFile(root, "ItemTemplates.json");
            Assert.IsTrue(File.Exists(itemsPath), "The selected native items.dat must exist.");
            Assert.IsTrue(File.Exists(hashPath), "The selected runtime hash catalog must exist.");
            HashItemCatalog hashes = HashItemCatalog.Parse(File.ReadAllText(hashPath));
            var templates = new ItemTemplateCatalog(new StubGameData(hashes, rootPath: root), new StubLogger());
            Assert.AreEqual("Ring of the Nucleus Basalis", templates.Require(LowId).Name);
            Assert.AreEqual(templates.Require(LowId).Name, templates.Require(HighId).Name);
            return new NativeAssets(hashes, templates, itemsPath, Digest(itemsPath), hashPath, Digest(hashPath));
        }

        static string Digest(string path)
        {
            using Stream input = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        }
    }

    sealed class Harness : IDisposable
    {
        readonly InventoryActionTests.World _world = new();
        readonly ServiceProvider _services;
        readonly ItemUseService _uses;

        public Player Player => _world.Player;
        public InventoryActionTests.Session Session => _world.Session;
        public Item Original { get; }
        public Item Unrelated { get; }

        public Harness(NativeAssets assets, int quality)
        {
            var builder = new ItemBuilder(assets.Templates, new StubInstanceIdAllocator(), new StubLogger());
            var gameData = new StubGameData(assets.Hashes);
            var moves = new InventoryMoveService(new StubLogger(), _world.Flush, _world.Actions);
            _uses = new ItemUseService(Player.Playfield!, new StubLogger(), moves, new StubInventoryRepository(), builder);
            var registry = new DynelRegistry();
            _services = new ServiceCollection().AddSingleton(registry).AddSingleton<WorldSimulationAccess>()
                .AddSingleton<IGameData>(gameData).AddSingleton<IItemTemplateCatalog>(assets.Templates)
                .AddSingleton<IItemBuilder>(builder)
                .AddSingleton(new HashItemMinter(gameData, assets.Templates, builder))
                .AddSingleton(new PlayfieldLocality(4582, null)).AddSingleton(_world.Flush)
                .AddSingleton(moves).AddSingleton(_uses).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, _services);
            typeof(Playfield).GetField("_lifecycleSync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, new System.Threading.Lock());
            typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, new ConcurrentDictionary<Character, byte>());
            registry.Register(Player);
            Player.Stats.Set(CharacterStat.MaxHealth, 100);
            Player.Stats.Set(CharacterStat.Health, 100);
            Original = builder.Create(LowId, HighId, quality, ItemSource.Other, instanceId: OriginalInstance);
            Player.Inventory.Inventory.Add(64, Original);
            Unrelated = _world.Add(42001, 1, slot: 65);
        }

        public ItemUseStart Use()
        {
            ItemUseStart start = _uses.TryBegin(Player, new Identity { Type = IdentityType.Inventory, Instance = 64 }, Original);
            if (start != ItemUseStart.Started)
                return start;
            _uses.Tick(5);
            Assert.IsFalse(_uses.HasPending(Player.Identity.Instance));
            Assert.IsFalse(Original.Locked);
            return Session.Messages.OfType<TemplateActionMessage>().Any(message => message.Action == TemplateActionType.Use)
                ? ItemUseStart.Executed : ItemUseStart.Rejected;
        }

        public void Dispose()
        {
            _world.Dispose();
            _services.Dispose();
        }
    }
}
