namespace ZoneEngine_New.Tests;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

using AORebirth.Core.GameData;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;

[TestClass]
public sealed class MissionDungeonPopulationModuleTests
{
    const int Owner = 111;
    const string QuestId = "GPOPULATION";
    const string Point = "r1p2";
    const string Hash = "TEST";

    [TestMethod]
    public void RebuildRejectsAKillThatCommitsAfterTheInitialCensusRead()
    {
        AuthoredMissionTestDao dao = SeedSealedPopulation();
        MissionTokenProgress stale = OpenPopulation(dao);
        Register(stale, Data());
        Assert.IsFalse(stale.IsKilled(Point, 0));

        RecordDurableKill(dao);
        InvalidDataException rejected = Assert.ThrowsExactly<InvalidDataException>(stale.Seal);
        StringAssert.Contains(rejected.Message, "recorded a kill during population reconstruction");

        MissionTokenProgress rebuilt = OpenPopulation(dao);
        Register(rebuilt, Data());
        Assert.IsTrue(rebuilt.IsKilled(Point, 0));
        rebuilt.Seal();
        Assert.AreEqual((1, 1), dao.Execute(Owner, tx =>
            MissionTokenProgress.ReadCounts(tx, QuestId, dao.CharacterQuest!.AssignedAtUtcTicks)));
    }

    [TestMethod]
    public void RebuildFailsWhenTheDurableKillCountHasNoMatchingMarker()
    {
        AuthoredMissionTestDao dao = SeedSealedPopulation();
        RecordDurableKill(dao);
        dao.Flags.Remove(dao.Flags.Single(pair => pair.Value.FlagKey.EndsWith("dead:" + Point + ":0", StringComparison.Ordinal)).Key);

        InvalidDataException rejected = Assert.ThrowsExactly<InvalidDataException>(() => OpenPopulation(dao));
        StringAssert.Contains(rejected.Message, "kill counters do not match");
    }

    [TestMethod]
    public void SameMissionTransformSeedKeepsSurvivingBranchInPlaceWhenAnotherBranchWasKilled()
    {
        using var fixture = new QuestDungeonDestinationTests.Fixture();
        QuestDungeonPlayfield world = fixture.CreateWorld(25, style: 324);
        string family = world.Layout.Population.RegularNpcHashes.First();
        Assert.IsTrue(fixture.GameData.TryResolveMobTemplate(family, 25, out MobTemplate template));

        var spawnSystem = world.GetRequiredService<HashSpawnSystem>();
        var district = new PlayfieldDistrictEntry { RespawnChance = 100 };
        var points = (System.Collections.Generic.List<HashSpawnPoint>)typeof(HashSpawnSystem)
            .GetField("_allPoints", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spawnSystem)!;
        MethodInfo spawn = typeof(HashSpawnSystem).GetMethod("TrySpawn", BindingFlags.Instance | BindingFlags.NonPublic)!;

        NpcCharacter[] baseline = Spawn(_ => false);
        NpcCharacter[] restored = Spawn(branch => branch == 0);
        Assert.HasCount(2, baseline);
        Assert.HasCount(1, restored);
        Assert.AreNotEqual(baseline[0].Position.xf, baseline[1].Position.xf);
        Assert.AreEqual(baseline[1].Position.xf, restored[0].Position.xf);
        Assert.AreEqual(baseline[1].Position.yf, restored[0].Position.yf);
        Assert.AreEqual(baseline[1].Position.zf, restored[0].Position.zf);
        Assert.AreNotEqual(baseline[1].Identity, restored[0].Identity);

        NpcCharacter[] Spawn(Func<int, bool> isKilled)
        {
            Assert.IsTrue(spawnSystem.AddPlacedDistrictPoint(family, district, world.Layout.Spawn, radius: 1,
                useDistrictLevels: false, respawns: false, requestedLevel: 25, frozenTemplates: [template, template],
                isBranchKilled: isKilled, initialTransformSeed: 42));
            HashSpawnPoint point = points[^1];
            Assert.IsFalse(point.RespawnsAfterDeath);
            Assert.IsTrue((bool)spawn.Invoke(spawnSystem, [point, true])!);
            return point.MembersSnapshot().OfType<NpcCharacter>().ToArray();
        }
    }

    [TestMethod]
    public void SharedRetainedCatalogMaterializesTheSameHashFromItsAcceptedRevision()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("mission-npc-catalog");
        try
        {
            byte[] original = Stage(root.FullName, "Original NPC", """{"TEST":{"Features":["NoCombat"]}}""");
            byte[] current = Stage(root.FullName, "Updated NPC", "{}");
            Assert.IsFalse(original.SequenceEqual(current));

            var catalogs = new RetainedMissionNpcContent(root.FullName);
            NpcTemplateCatalog originalCatalog = catalogs.ResolveStyleDependency(original);
            NpcTemplateCatalog currentCatalog = catalogs.ResolveStyleDependency(current);
            Assert.AreSame(originalCatalog, catalogs.ResolveStyleDependency(original),
                "Styles sharing one content revision must reuse its parsed NPC catalog.");
            Assert.IsTrue(originalCatalog.TryResolve(Hash, 1, out MobTemplate originalNpc));
            Assert.IsTrue(currentCatalog.TryResolve(Hash, 1, out MobTemplate updatedNpc));
            Assert.AreEqual("Original NPC", originalNpc.Name);
            Assert.AreEqual("Updated NPC", updatedNpc.Name);
            Assert.IsTrue(originalNpc.Has(NpcFeature.NoCombat));
            Assert.IsFalse(updatedNpc.Has(NpcFeature.NoCombat));

            using JsonDocument reference = JsonDocument.Parse(original);
            string digest = reference.RootElement.GetProperty("Sha256").GetString()!;
            string originalDirectory = Path.Combine(root.FullName, RetainedMissionNpcContent.Folder, digest);
            File.WriteAllText(Path.Combine(originalDirectory, GameDataPaths.NpcTemplatesFileName), "{}");
            Assert.ThrowsExactly<InvalidDataException>(() => new RetainedMissionNpcContent(root.FullName).ResolveStyleDependency(original));
        }
        finally
        {
            root.Delete(recursive: true);
        }

        static byte[] Stage(string root, string displayName, string rulesJson)
        {
            byte[] templates = JsonSerializer.SerializeToUtf8Bytes(new
            {
                TEST = new { Templates = new[] { new { Name = displayName, Level = 1, Stats = new { } } } }
            });
            byte[] rules = Encoding.UTF8.GetBytes(rulesJson);
            string manifest = RetainedMissionNpcContent.IdentityHeader
                + MonsterCombatRulesCatalog.FileName + " " + RetainedDungeonContent.Hash(rules) + "\n"
                + GameDataPaths.NpcTemplatesFileName + " " + RetainedDungeonContent.Hash(templates) + "\n";
            string hash = RetainedDungeonContent.Hash(Encoding.UTF8.GetBytes(manifest));
            string directory = Directory.CreateDirectory(Path.Combine(root, RetainedMissionNpcContent.Folder, hash)).FullName;
            File.WriteAllBytes(Path.Combine(directory, GameDataPaths.NpcTemplatesFileName), templates);
            File.WriteAllBytes(Path.Combine(directory, MonsterCombatRulesCatalog.FileName), rules);
            return JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Sha256 = hash });
        }
    }

    static AuthoredMissionTestDao SeedSealedPopulation()
    {
        DateTime now = DateTime.UtcNow;
        var dao = new AuthoredMissionTestDao
        {
            CharacterQuest = new CharacterQuestCompletionSnapshotData
            {
                CharacterId = Owner, QuestId = QuestId, OwnerType = (int)QuestOwnerType.Character, OwnerId = Owner,
                Source = (int)QuestSource.Generated, State = (int)QuestState.Active, RequiredCount = 1,
                AssignedAtUtcTicks = now.Ticks, ExpiresAtUtcTicks = now.AddHours(1).Ticks
            }
        };
        MissionTokenProgress initial = OpenPopulation(dao);
        Register(initial, Data());
        initial.Seal();
        return dao;
    }

    static MissionTokenProgress OpenPopulation(AuthoredMissionTestDao dao) => new(dao, Owner, QuestId,
        dao.CharacterQuest!.AssignedAtUtcTicks, new MissionTokenRewardPolicy
        {
            SidedItems = new() { [(int)Side.Clan] = 1, [(int)Side.Omni] = 2 },
            ItemQuality = 1, FeedbackStartingKill = 1, FeedbackMessageId = 1
        });

    static StubGameData Data() => new(HashItemCatalog.Parse("{}"),
        mobs: new() { [Hash] = new MobTemplate { Hash = Hash, Name = "Population fixture" } });

    static void Register(MissionTokenProgress population, IGameData data)
    {
        MissionTokenProgress.SpawnPoint? point = population.ResolvePoint(Point, Hash, 1, data);
        Assert.IsNotNull(point);
        population.RegisterPoint(point);
    }

    static void RecordDurableKill(AuthoredMissionTestDao dao)
    {
        string prefix = "mission-token:" + dao.CharacterQuest!.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture) + ":";
        var mission = new MissionKeyData(Owner, QuestId);
        var progressKey = new MissionObjectiveKeyData(mission, prefix + "population");
        dao.Execute(Owner, tx =>
        {
            MissionObjectiveProgressData progress = tx.GetObjective(progressKey);
            Assert.IsTrue(tx.TryAddObservation(new()
            {
                CharacterId = Owner, QuestId = QuestId, ObjectiveId = prefix + "population",
                ObservationKey = Point + ":0", EventType = "MissionNpcKilled"
            }));
            progress.Progress = 1;
            progress.LastObservationKey = Point + ":0";
            tx.SaveObjective(progressKey, progress);
            tx.SaveFlag(mission, new() { CharacterId = Owner, QuestId = QuestId, FlagKey = prefix + "dead:" + Point + ":0", Value = "1" });
            return true;
        });
    }
}
