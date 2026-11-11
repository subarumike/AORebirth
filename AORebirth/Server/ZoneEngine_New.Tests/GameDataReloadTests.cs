namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;
using AORebirth.Modules.Content;
using AORebirth.Modules.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Modules;
using ZoneEngine_New.Core.NpcScripts;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;

/// <summary>Module API 1.3: the GameData override folder, reloadable catalogs and respawning an NPC now.</summary>
[TestClass]
public sealed class GameDataReloadTests
{
    const string Hash = "RLDT";

    [TestMethod]
    public void OverrideFileReplacesShippedFileAndRemovingItFallsBack()
    {
        using var content = new Content();
        using var overrides = new OverrideFolder();
        string shipped = Path.Combine(content.Root, MonsterCombatRulesCatalog.FileName);
        File.WriteAllText(shipped, "{}");
        Assert.AreEqual(Path.GetFullPath(shipped), GameDataPaths.ResolveFile(content.Root, MonsterCombatRulesCatalog.FileName));

        string overridden = overrides.Write(MonsterCombatRulesCatalog.FileName, "{}");
        Assert.AreEqual(Path.GetFullPath(overridden), GameDataPaths.ResolveFile(content.Root, MonsterCombatRulesCatalog.FileName));
        Assert.AreEqual(overrides.Root, GameDataPaths.ResolveOverrideRoot());

        File.Delete(overridden);
        Assert.AreEqual(Path.GetFullPath(shipped), GameDataPaths.ResolveFile(content.Root, MonsterCombatRulesCatalog.FileName));
    }

    [TestMethod]
    public void MonsterCombatRulesReloadSwapsAndAMalformedFileKeepsTheOldRules()
    {
        using var content = new Content();
        content.Rules("{}");
        Assert.IsFalse(MonsterCombatRulesCatalog.For(content.Root).TryGet(Hash, out _));

        content.Rules("{ \"" + Hash + "\": { \"Features\": [ \"NoCombat\" ] } }");
        GameDataReloadResult reloaded = MonsterCombatRulesCatalog.Reload(content.Root);
        Assert.IsTrue(reloaded.Ok);
        Assert.AreEqual(1, reloaded.Entries);
        Assert.IsTrue(MonsterCombatRulesCatalog.For(content.Root).TryGet(Hash, out MonsterCombatRules rules));
        CollectionAssert.Contains(rules.Features!, NpcFeature.NoCombat);

        content.Rules("{ \"" + Hash + "\": { \"Features\": [ ");
        GameDataReloadResult broken = MonsterCombatRulesCatalog.Reload(content.Root);
        Assert.IsFalse(broken.Ok);
        Assert.AreEqual(1, broken.Problems.Count);
        StringAssert.StartsWith(broken.Problems[0], MonsterCombatRulesCatalog.FileName + ": ");
        Assert.IsTrue(MonsterCombatRulesCatalog.For(content.Root).TryGet(Hash, out _), "A failed reload keeps the old rules.");
    }

    [TestMethod]
    [DataRow("""{ "Hash": "BAD", "Action": "Assemble" }""", "requires an objective SpecItem")]
    [DataRow("""{ "Hash": "BAD", "Action": "Equip", "Objective": { "SpecItem": "MISSING" } }""",
        "objective SpecItem 'MISSING' does not resolve")]
    [DataRow("""{ "Hash": "BAD", "StartItems": [ { "Hash": "MISSING", "Quality": 1 } ] }""",
        "start item hash 'MISSING' must resolve")]
    public void QuestReloadRejectsMissingRequiredItemsAndKeepsPublishedTemplates(string invalidQuest, string problem)
    {
        using var content = new Content();
        using var overrides = new OverrideFolder();
        const string validQuest = """{ "Hash": "GOOD", "Action": "TalkTo" }""";
        string file = overrides.Write(QuestCatalog.FileName, "[" + validQuest + "]");
        var logger = new StubLogger();
        var quests = new QuestCatalog(new GameDataStore(logger, null, content.Root), logger);
        Assert.IsTrue(quests.TryGet("GOOD", out QuestTemplate before));

        File.WriteAllText(file, "[" + validQuest + "," + invalidQuest + "]");
        GameDataReloadResult result = quests.Reload();

        Assert.IsFalse(result.Ok);
        StringAssert.Contains(result.Problems.Single(), problem);
        Assert.IsTrue(quests.TryGet("GOOD", out QuestTemplate after));
        Assert.AreSame(before, after);
        Assert.IsFalse(quests.TryGet("BAD", out _));
    }

    [TestMethod]
    public void ReloadedNoCombatReachesAnNpcOnlyWhenItRespawns()
    {
        using var content = new Content();
        content.Rules("{}");
        using var world = new HalloweenActivationTests.World(content.Root, false);
        HashSpawnPoint point = world.Points.Single();
        world.Tick(point);
        NpcCharacter before = point.MembersSnapshot().OfType<NpcCharacter>().Single();
        Assert.IsTrue(before.Attackable);
        Assert.IsNotNull(before.Brain);

        content.Rules("{ \"" + Hash + "\": { \"Features\": [ \"NoCombat\" ] } }");
        Assert.IsTrue(new MonsterCombatRulesReload(world.Data).Reload().Ok);
        Assert.IsTrue(before.Attackable, "A spawned NPC keeps its state until it respawns.");
        Assert.IsNotNull(before.Brain);

        Assert.IsTrue(OnTick(world, () => world.System.TryRespawnNow(before)));
        Assert.IsNull(before.Playfield);
        NpcCharacter after = point.MembersSnapshot().OfType<NpcCharacter>().Single();
        Assert.AreNotEqual(before.Identity, after.Identity);
        Assert.IsFalse(after.Attackable);
        Assert.IsNull(after.Brain);
    }

    [TestMethod]
    public void AReloadedHateReachesTheRespawnedNpc()
    {
        using var content = new Content();
        content.Rules("{}");
        using var world = new HalloweenActivationTests.World(content.Root, false);
        HashSpawnPoint point = world.Points.Single();
        world.Tick(point);
        NpcCharacter before = point.MembersSnapshot().OfType<NpcCharacter>().Single();
        Assert.IsNull(Hated(before.Brain!));

        content.Rules("{ \"" + Hash + "\": { \"Hates\": [ 42 ] } }");
        Assert.IsTrue(new MonsterCombatRulesReload(world.Data).Reload().Ok);
        Assert.IsTrue(OnTick(world, () => world.System.TryRespawnNow(before)));

        NpcCharacter after = point.MembersSnapshot().OfType<NpcCharacter>().Single();
        IReadOnlySet<int>? hated = Hated(after.Brain!);
        Assert.IsNotNull(hated);
        Assert.IsTrue(hated.Contains(42), "The respawned NPC attacks the family its new rules hate.");
    }

    [TestMethod]
    public void RespawnWithoutASpawnPointReturnsFalseAndLeavesTheNpc()
    {
        using var content = new Content();
        using var world = new HalloweenActivationTests.World(content.Root, false);
        var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 77001 }, new StubItemBuilder())
        {
            Name = "GM spawned", Playfield = world.Playfield, SpawnHash = Hash
        };
        world.Registry.Register(npc);

        Assert.IsFalse(OnTick(world, () => world.System.TryRespawnNow(npc)));
        Assert.AreSame(world.Playfield, npc.Playfield);
        Assert.IsTrue(world.Registry.TryGet(npc.Identity, out _));
        Assert.IsFalse(npc.IsDead);
    }

    [TestMethod]
    public void ItemTemplatesReloadResolvesANewHashAndReloadsKnubotWithIt()
    {
        using var content = new Content();
        File.WriteAllText(Path.Combine(content.Root, GameDataPaths.ItemTemplatesFileName), "{}");
        Directory.CreateDirectory(Path.Combine(content.Root, KnubotCatalog.DirectoryName));
        File.WriteAllText(Path.Combine(content.Root, KnubotCatalog.DirectoryName, "test.json"), """
            { "Npcs": ["TSTD"], "Openers": [ { "If": ["has NEWI"], "Say": "hi" } ], "Lines": { "hi": { "Text": "Hello." } } }
            """);
        var logger = new StubLogger();
        var data = new GameDataStore(logger, null, content.Root);
        var items = new StubCatalog();
        var minter = new HashItemMinter(data, items, new StubItemBuilder());
        KnubotCatalog knubot = WithoutKnubotOverride(() => new KnubotCatalog(data, new QuestCatalog(data, logger, minter), items, logger));
        Assert.IsFalse(data.CanResolveItemHash("NEWI"));
        Assert.AreEqual(0, knubot.Reload().Loaded, "The script names a hash ItemTemplates.json does not have yet.");

        File.WriteAllText(Path.Combine(content.Root, GameDataPaths.ItemTemplatesFileName),
            "{ \"NEWI\": { \"Templates\": [ 246817 ] } }");
        int knubotReloads = 0;
        knubot.Reloaded += _ => knubotReloads++;
        GameDataReloadResult result = WithoutKnubotOverride(() => new ItemTemplatesReload(data, knubot, minter).Reload());

        Assert.IsTrue(result.Ok, string.Join("; ", result.Problems));
        Assert.AreEqual(1, result.Entries);
        Assert.IsTrue(data.CanResolveItemHash("NEWI"));
        Assert.AreEqual(1, knubotReloads, "Knubot reloads with ItemTemplates.");
        Assert.AreEqual(0, knubot.Check("test", File.ReadAllText(Path.Combine(content.Root, KnubotCatalog.DirectoryName, "test.json"))).Count);
    }

    [TestMethod]
    public void NpcScriptsReloadPicksUpAnOverrideScript()
    {
        using var content = new Content();
        using var overrides = new OverrideFolder();
        var data = new GameDataStore(new StubLogger(), null, content.Root);
        var scripts = new NpcScriptCatalog(data, new StubLogger());
        Assert.AreEqual(0, scripts.Result.Programs);

        overrides.Write(Path.Combine(NpcScriptCatalog.DirectoryName, "greet.json"), """
            { "Id": "greet", "Handlers": [ { "On": "Spawned", "Do": [ { "Wait": 1000 } ] } ], "Bindings": [ { "Hash": "RLDT" } ] }
            """);
        GameDataReloadResult result = new NpcScriptsReload(scripts).Reload();
        Assert.IsTrue(result.Ok);
        Assert.AreEqual(0, result.Problems.Count, string.Join("; ", result.Problems));
        Assert.AreEqual(1, result.Entries);
        Assert.AreEqual(1, scripts.Result.Programs);
        Assert.IsNotNull(scripts.Resolve(Hash, 665, null, null));
    }

    [TestMethod]
    public void UnregisteredAndLoadOnceCatalogsAreNotReloadable()
    {
        using var content = new Content();
        var data = new GameDataStore(new StubLogger(), null, content.Root);
        var registry = new GameDataReloadRegistry([new MonsterCombatRulesReload(data), new NpcTemplatesReload(data)]);
        CollectionAssert.AreEqual(new[] { "MonsterCombatRules", "NpcTemplates" }, registry.Names.ToArray());

        foreach (string name in new[] { "WaterExits", "PetTypes", "Nope", "" })
        {
            GameDataReloadResult result = registry.Reload(name);
            Assert.IsFalse(result.Ok, name);
            Assert.AreEqual(1, result.Problems.Count, name);
        }

        Assert.IsTrue(registry.Reload("npctemplates").Ok, "Names match case-insensitively.");
    }

    [TestMethod]
    public void UngrantedGameDataReloadAndRespawnNpcThrow()
    {
        using var content = new Content();
        var data = new GameDataStore(new StubLogger(), null, content.Root);
        var gate = new CapabilityGate("Example", [ModuleCapabilities.ReadWorld], new StubLogger());
        var gameData = new GatedGameData(new ModuleGameData(() => new GameDataReloadRegistry([new NpcTemplatesReload(data)])), gate);
        var actions = new GatedActions(new ZoneModuleActions(() => null!), gate);

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => gameData.Reload("NpcTemplates"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => actions.RespawnNpc(1, 1));
        CollectionAssert.AreEqual(new[] { "NpcTemplates" }, gameData.Reloadable.ToArray(), "Listing needs no capability.");
        _ = gameData.OverrideDirectory;

        var granted = new GatedGameData(new ModuleGameData(() => new GameDataReloadRegistry([new NpcTemplatesReload(data)])),
            new CapabilityGate("Example", [ModuleCapabilities.GameDataReload], new StubLogger()));
        Assert.IsTrue(granted.Reload("NpcTemplates").Result.Ok);
    }

    static bool OnTick(HalloweenActivationTests.World world, Func<bool> work)
    {
        FieldInfo field = typeof(Playfield).GetField("_tickSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (field.GetValue(world.Playfield) is not Lock tick)
        {
            tick = new Lock();
            field.SetValue(world.Playfield, tick);
        }

        lock (tick)
            return work();
    }

    static IReadOnlySet<int>? Hated(NpcBrain brain)
        => (IReadOnlySet<int>?)typeof(NpcBrain).GetMethod("HatedFamilies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(brain, null);

    static T WithoutKnubotOverride<T>(Func<T> work)
    {
        string? saved = Environment.GetEnvironmentVariable(KnubotCatalog.OverrideVariable);
        Environment.SetEnvironmentVariable(KnubotCatalog.OverrideVariable, null);
        try
        {
            return work();
        }
        finally
        {
            Environment.SetEnvironmentVariable(KnubotCatalog.OverrideVariable, saved);
        }
    }

    /// <summary>A GameData root with one RLDT mob template and one spawn point for it on playfield 665.</summary>
    sealed class Content : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "aorebirth-reload-test-" + Guid.NewGuid().ToString("N"));

        internal Content()
        {
            string spawns = Path.Combine(Root, "Playfields", "665", "Spawns.json");
            Directory.CreateDirectory(Path.GetDirectoryName(spawns)!);
            File.WriteAllText(Path.Combine(Root, GameDataPaths.NpcTemplatesFileName),
                "{\"" + Hash + "\":{\"Templates\":[{\"Name\":\"Reload fixture\",\"Level\":1,\"Stats\":{\"54\":1,\"1\":100}}]}}");
            File.WriteAllText(spawns, JsonSerializer.Serialize(new PlayfieldSpawnsData
            {
                SchemaVersion = 1,
                PlayfieldId = 665,
                Spawns =
                [
                    new PlayfieldSpawnEntry
                    {
                        HashText = Hash, Position = [4, 5, 6], Radius = 0, MinLevel = 1, MaxLevel = 1,
                        RespawnChance = 100, RespawnTime = 30
                    }
                ]
            }));
        }

        internal void Rules(string json) => File.WriteAllText(Path.Combine(Root, MonsterCombatRulesCatalog.FileName), json);

        public void Dispose() => Directory.Delete(Root, true);
    }

    /// <summary>Sets AO_REBIRTH_GAMEDATA_OVERRIDE_PATH to a fresh folder for the test, then restores it.</summary>
    sealed class OverrideFolder : IDisposable
    {
        readonly string? _saved = Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName);

        internal OverrideFolder()
        {
            Directory.CreateDirectory(Root);
            Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, Root);
        }

        internal string Root { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aorebirth-override-test-" + Guid.NewGuid().ToString("N")));

        internal string Write(string relativePath, string json)
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            return path;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName, _saved);
            Directory.Delete(Root, true);
        }
    }
}
