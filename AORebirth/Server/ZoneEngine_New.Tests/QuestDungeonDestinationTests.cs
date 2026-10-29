namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Core.GameData;
using AORebirth.Database.Domain.Quests;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using AORebirth.Interfaces.Persistence.Shops;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Metrics;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.Teams;
using ZoneEngine_New.Core.Trade;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
[DoNotParallelize]
public sealed class QuestDungeonDestinationTests
{
    public TestContext TestContext { get; set; } = null!;

    static readonly Lazy<MissionDestinationCatalog> Catalog = new(() =>
        MissionDestinationCatalog.Load(Path.Combine(RepositoryRoot(), "AORebirth", "GameData")));
    static readonly Lazy<GameDataStore> RuntimeData = new(() =>
    {
        string? root = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(root), "Set AO_REBIRTH_GAMEDATA_PATH to existing runtime GameData for the real procedural dungeon tests.");
        return new GameDataStore(new StubLogger(), null, root!);
    });

    [TestMethod]
    public void AcceptancePersistsSelectedIdentityAndPublishesLinkedKeyOnlyAfterCommit()
    {
        using var f = new Fixture();
        Assert.IsTrue(f.Offer.EntranceInstance < 0, "Exercise the catalog identity's high bit through the signed protocol representation.");
        f.Store.BeforeAccept = () =>
        {
            Assert.IsTrue(Monitor.IsEntered(f.Player.PersistenceGate));
            Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(0, f.Session.Sent.Count);
        };
        Assert.IsNull(f.Accept());
        QuestDungeonParameters parameters = f.Parameters();
        Assert.AreEqual(DungeonLayoutGenerator.CurrentVersion, parameters.GeneratorVersion);
        Assert.IsFalse(parameters.IsTeam);
        Assert.IsNotNull(parameters.LayoutPlan);
        Assert.AreEqual(1, parameters.LayoutPlan.FloorCount);
        Assert.IsFalse(parameters.LayoutPlan.BossFloor);
        Assert.IsTrue(f.Layouts.TryGenerate(parameters, out var acceptedLayout));
        Assert.IsNull(acceptedLayout.BossRoomIndex);
        Assert.AreEqual(0, acceptedLayout.FloorTransitions.Count);
        Assert.AreEqual(f.Offer.EntranceType, parameters.EntranceType);
        Assert.AreEqual(f.Offer.EntranceInstance, parameters.EntranceInstance);
        Assert.AreEqual(f.Entrance.PlayfieldId, parameters.EntrancePlayfield);
        Assert.AreEqual(f.Entrance.LocalXBits, BitConverter.SingleToUInt32Bits(parameters.EntranceX));
        Assert.AreEqual(f.Entrance.LocalYBits, BitConverter.SingleToUInt32Bits(parameters.EntranceY));
        Assert.AreEqual(f.Entrance.LocalZBits, BitConverter.SingleToUInt32Bits(parameters.EntranceZ));
        Assert.AreEqual(f.Offer.EntranceLow, parameters.BuildingLowId);
        Assert.AreEqual(f.Offer.EntranceHigh, parameters.BuildingHighId);
        Assert.AreEqual(f.Offer.DestinationType, parameters.DestinationType);
        Item key = f.Player.Inventory.Inventory.Content.Values.Single();
        Assert.IsTrue(key.IsPersisted);
        Assert.AreEqual(QuestDungeonService.KeyLowId, key.LowId);
        Assert.AreEqual(QuestDungeonService.KeyItemType, (int)key.Identity.Type);
        Assert.AreEqual(f.Store.Generated!.QuestId, f.Store.Links[key.InstanceId]);
        Assert.AreEqual(f.Player.Identity.Instance, f.Store.Keys.Single().ContainerInstance);
        Assert.IsInstanceOfType<SimpleItemFullUpdateMessage>(f.Session.Sent[0]);
        Assert.IsInstanceOfType<ContainerAddItemMessage>(f.Session.Sent[1]);
        Assert.IsInstanceOfType<TemplateActionMessage>(f.Session.Sent[2]);
        Assert.IsInstanceOfType<TemplateActionMessage>(f.Session.Sent[3]);
        Assert.IsTrue(f.Session.Sent.OfType<QuestFullUpdateMessage>().Any());
        Assert.AreEqual("That mission is no longer offered.", f.Accept());
        Assert.AreEqual(1, f.Store.AcceptCalls);
        Assert.AreEqual(1, f.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    [DataRow("type")]
    [DataRow("identity")]
    [DataRow("playfield")]
    [DataRow("playfieldIdentity")]
    [DataRow("worldType")]
    [DataRow("x")]
    [DataRow("y")]
    [DataRow("z")]
    [DataRow("offsetX")]
    [DataRow("offsetZ")]
    public void AcceptanceRejectsAnyMismatchWithoutReconstructingIdentityFromCoordinates(string field)
    {
        using var f = new Fixture();
        switch (field)
        {
            case "type": f.Offer.EntranceType++; break;
            case "identity": f.Offer.EntranceInstance = 0; break;
            case "playfield": f.Offer.DestinationPlayfield++; break;
            case "playfieldIdentity": f.Offer.DestinationInstance++; break;
            case "worldType": f.Offer.DestinationType++; break;
            case "x": f.Offer.DestinationX = MathF.BitIncrement(f.Offer.DestinationX); break;
            case "y": f.Offer.DestinationY = MathF.BitIncrement(f.Offer.DestinationY); break;
            case "z": f.Offer.DestinationZ = MathF.BitIncrement(f.Offer.DestinationZ); break;
            case "offsetX": f.Offer.EntranceLow++; break;
            case "offsetZ": f.Offer.EntranceHigh++; break;
        }
        Assert.AreEqual("That mission's location is unavailable.", f.Accept());
        Assert.AreEqual(0, f.Store.AcceptCalls);
        Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, f.Session.Sent.Count);
    }

    [TestMethod]
    public void ParameterRoundTripKeepsSignedInstanceAndHistoricalImplicitTypeButRejectsWrongType()
    {
        var parameters = new QuestDungeonParameters
        {
            Seed = 42, GeneratorVersion = 2,
            DungeonPlayfield = QuestDungeonIds.FirstPlayfield + 100, EntranceInstance = unchecked((int)0xF1234567)
        };
        Assert.IsTrue(QuestDungeonParameters.TryParse(parameters.ToJson(), out var restored));
        Assert.AreEqual(parameters.EntranceInstance, restored.EntranceInstance);
        Assert.AreEqual((int)IdentityType.MissionEntrance, restored.EntranceType);
        string historical = parameters.ToJson().Replace("\"EntranceType\":56006,", string.Empty, StringComparison.Ordinal);
        Assert.IsFalse(historical.Contains("\"EntranceType\"", StringComparison.Ordinal));
        Assert.IsTrue(QuestDungeonParameters.TryParse(historical, out restored));
        Assert.AreEqual((int)IdentityType.MissionEntrance, restored.EntranceType);
        parameters.EntranceType = (int)IdentityType.Terminal;
        Assert.IsFalse(QuestDungeonParameters.TryParse(parameters.ToJson(), out _));
    }

    [TestMethod]
    public void RealProceduralLayoutPreservesHighBitIdentityAndSameSeedPayload()
    {
        using var f = new Fixture();
        Assert.IsTrue(f.Layouts.TryGenerate(42, 2, f.Offer.EntranceInstance, out var first));
        var independent = new DungeonLayoutGenerator(RuntimeData.Value, new StubLogger());
        Assert.IsTrue(independent.TryGenerate(42, 2, f.Offer.EntranceInstance, out var second));
        Assert.AreEqual(f.Offer.EntranceInstance, first.Generator.Identity.Instance);
        Assert.AreEqual(DungeonLayoutGenerator.BuildingIdentityType, (int)first.Generator.Identity.Type);
        CollectionAssert.AreEqual(first.GeneratorPayload, second.GeneratorPayload);
        Assert.IsTrue(first.GeneratorPayload.Length > 0);
        Assert.AreEqual(first.Spawn.x, second.Spawn.x);
        Assert.AreEqual(first.Spawn.y, second.Spawn.y);
        Assert.AreEqual(first.Spawn.z, second.Spawn.z);
    }

    [TestMethod]
    public void RawWxyzRotationMatchesCurrentDynelsHeadingAtEveryExactPlacementIdentity()
    {
        int matched = 0, informative = 0;
        var differences = new List<string>();
        foreach (var group in Catalog.Value.Placements.GroupBy(row => row.PlayfieldId))
        {
            var dynels = RuntimeData.Value.GetPlayfieldGeometry(group.Key).Dynels?.Dynels;
            if (dynels == null) continue;
            foreach (var entrance in group)
            {
                var records = dynels.Where(row => unchecked((uint)row.IdentityType) == entrance.IdentityType
                    && unchecked((uint)row.IdentityInstance) == entrance.IdentityInstance).ToArray();
                if (records.Length == 0) continue;
                Assert.AreEqual(1, records.Length, "Current Dynels data repeats a complete placement identity.");
                var record = records[0];
                matched++;
                uint[] raw = [BitConverter.SingleToUInt32Bits(entrance.RotationComponent0), BitConverter.SingleToUInt32Bits(entrance.RotationComponent1),
                    BitConverter.SingleToUInt32Bits(entrance.RotationComponent2), BitConverter.SingleToUInt32Bits(entrance.RotationComponent3)];
                uint[] heading = [BitConverter.SingleToUInt32Bits(record.Heading.X), BitConverter.SingleToUInt32Bits(record.Heading.Y),
                    BitConverter.SingleToUInt32Bits(record.Heading.Z), BitConverter.SingleToUInt32Bits(record.Heading.W)];
                if (raw.Distinct().Count() > 1) informative++;
                uint[] xyzw = [raw[1], raw[2], raw[3], raw[0]];
                if (!xyzw.SequenceEqual(heading)) differences.Add(entrance.IdentityType.ToString("X8") + ":" + entrance.IdentityInstance.ToString("X8")
                    + " raw=" + string.Join(",", raw.Select(value => value.ToString("X8"))) + " heading=" + string.Join(",", heading.Select(value => value.ToString("X8"))));
            }
        }
        TestContext.WriteLine("Runtime data={0}; exact identity matches={1}; informative rotations={2}; slot differences={3}", RuntimeData.Value.RootPath, matched, informative, differences.Count);
        Assert.AreEqual(Catalog.Value.Count, matched, "Every placement must match current Dynels by its complete identity. Root=" + RuntimeData.Value.RootPath);
        Assert.IsTrue(informative > 0, "The mapping check requires informative rotations.");
        Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences.Take(5)));
    }

    [TestMethod]
    public void ExactCarriedKeyCreatesActualDungeonThenTransfersToItsSpawnAndReusesWorld()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        QuestDungeonParameters parameters = f.Parameters();
        Assert.IsFalse(f.Manager.TryGetQuestDungeon(parameters.DungeonPlayfield, out _));
        Assert.IsTrue(f.Service.TryEnter(f.Player, f.Target));
        Assert.IsTrue(f.Manager.TryGetQuestDungeon(parameters.DungeonPlayfield, out var world));
        Assert.AreSame(f.Entrance, world.Entrance);
        Assert.AreEqual(f.Store.Generated!.QuestId, world.QuestId);
        Assert.AreEqual(f.Offer.EntranceInstance, world.Layout.Generator.Identity.Instance);
        Assert.AreSame(world, f.Session.Transfers.Single().World);
        Assert.AreEqual(world.Layout.Spawn.x, f.Session.Transfers[0].Position.x);
        Assert.AreEqual(world.Layout.Spawn.y, f.Session.Transfers[0].Position.y);
        Assert.AreEqual(world.Layout.Spawn.z, f.Session.Transfers[0].Position.z);
        Assert.AreSame(world, f.Manager.GetOrCreateQuestDungeon(parameters.DungeonPlayfield, world.QuestId, world.Layout, f.Entrance));
        Assert.IsTrue(world.GetRequiredService<DynelRegistry>().Dynels().OfType<Door>().Any(), "Actual dungeon Build must materialize doors.");
    }

    [TestMethod]
    [DataRow("wrongType")]
    [DataRow("missingIdentity")]
    [DataRow("missingKey")]
    [DataRow("wrongQuestEntrance")]
    [DataRow("wrongPlayfield")]
    [DataRow("horizontalRange")]
    [DataRow("verticalRange")]
    [DataRow("expired")]
    public void EntryRequiresCurrentExactIdentityCarriedKeyPlayfieldRangeAndExpiry(string denial)
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        Identity target = f.Target;
        switch (denial)
        {
            case "wrongType": target = new Identity { Type = IdentityType.Terminal, Instance = target.Instance }; break;
            case "missingIdentity": target = new Identity { Type = target.Type, Instance = 0 }; break;
            case "missingKey": f.Player.Inventory.Inventory.Content.Clear(); break;
            case "wrongQuestEntrance":
                var parameters = f.Parameters(); parameters.EntranceInstance = 0;
                f.Store.Generated!.AcgBuildingGeneratorJson = parameters.ToJson(); f.RestartService(); break;
            case "wrongPlayfield": f.SetExteriorIdentity(f.Entrance.PlayfieldId + 1); break;
            case "horizontalRange": f.Player.Position = new Vector3(f.Entrance.LocalX + 10.01, f.Entrance.LocalY, f.Entrance.LocalZ); break;
            case "verticalRange": f.Player.Position = new Vector3(f.Entrance.LocalX, f.Entrance.LocalY + 14.01, f.Entrance.LocalZ); break;
            case "expired": f.Store.Generated!.ExpiresAtUtcTicks = DateTime.UtcNow.AddMinutes(-1).Ticks; f.RestartService(); break;
        }
        Assert.IsFalse(f.Service.TryEnter(f.Player, target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        Assert.IsFalse(f.Manager.TryGetQuestDungeon(f.Parameters().DungeonPlayfield, out _));
    }

    [TestMethod]
    public void KeyCopyAndRestoreKeepTheAcceptedEntranceNameAndDurableLink()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        var key = f.Player.Inventory.Inventory.Content.Single();
        int duplicatorSlot = f.Player.Inventory.Inventory.FindFreeSlot();
        Item duplicator = TestWorld.CreateItem(lowId: QuestDungeonService.DuplicatorLowId, instanceId: 700000);
        f.Player.Inventory.Inventory.Add(duplicatorSlot, duplicator);
        f.Session.Sent.Clear();
        Assert.IsTrue(f.Service.TryDuplicate(f.Player,
            new Identity { Type = IdentityType.Inventory, Instance = key.Key },
            new Identity { Type = IdentityType.Inventory, Instance = duplicatorSlot }));
        Assert.AreEqual(2, f.Player.Inventory.Inventory.Content.Values.Count(item => item.LowId == QuestDungeonService.KeyLowId));
        Assert.AreSame(duplicator, f.Player.Inventory.Inventory.Content[duplicatorSlot]);
        Assert.AreEqual(2, f.Store.Links.Count);
        Assert.IsTrue(f.Store.Links.Values.All(id => id == f.Store.Generated!.QuestId));
        f.Session.Sent.Clear();
        f.Service.RestoreKeys(f.Player);
        var updates = f.Session.Sent.OfType<SimpleItemFullUpdateMessage>().ToArray();
        Assert.AreEqual(2, updates.Length);
        Assert.IsTrue(updates.All(update => update.Name == "Mission key to " + f.Entrance.DisplayName + '\0'));
    }

    [TestMethod]
    public void StoredAcceptedIdentityRecreatesDungeonForLoginWithoutFuzzyLookup()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        f.RestartService();
        QuestDungeonParameters parameters = f.Parameters();
        Assert.IsTrue(f.Service.TryGetEntrance(f.Store.Generated!.QuestId, out var resolved));
        Assert.AreSame(f.Entrance, resolved);
        var login = f.Service.ResolveLogin(parameters.DungeonPlayfield);
        Assert.IsInstanceOfType<QuestDungeonPlayfield>(login.Playfield);
        Assert.AreSame(f.Entrance, ((QuestDungeonPlayfield)login.Playfield).Entrance);
        Assert.IsNotNull(login.Position);
    }

    [TestMethod]
    public void JournalDeletionSavesAbandonmentBeforeEndingKeysAndPreventsEntry()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        Identity journal = f.Session.Sent.OfType<QuestFullUpdateMessage>().Single().Quests.Single().QuestId;
        string frozenDestination = f.Store.Generated!.AcgBuildingGeneratorJson;
        int keyInstance = f.Player.Inventory.Inventory.Content.Values.Single().InstanceId;
        f.Store.BeforeEndKeys = questId =>
        {
            Assert.AreEqual(f.Store.Generated.QuestId, questId);
            Assert.AreEqual((int)QuestState.Abandoned, f.Store.Character!.State);
            Assert.IsTrue(f.Session.Sent.OfType<QuestMessage>().Any(message => message.Action == QuestAction.Delete
                && message.Mission.Equals(journal)), "Persist abandonment and delete the journal entry before ending its keys.");
        };
        Assert.IsTrue(f.Abandon(journal.Instance));
        Assert.AreEqual(1, f.Store.EndKeysCalls);
        Assert.AreEqual(0, f.Store.Links.Count);
        CollectionAssert.AreEqual(new[] { keyInstance }, f.Store.RetiredKeys.ToArray());
        Assert.AreEqual(frozenDestination, f.Store.Generated.AcgBuildingGeneratorJson, "Ending a quest must not change its frozen entrance.");
        Assert.IsFalse(f.Abandon(journal.Instance), "An already ended journal entry must not end keys twice.");
        Assert.AreEqual(1, f.Store.EndKeysCalls);
        f.Persistence.BeforeMutation = batch =>
        {
            Assert.AreEqual(0, f.Store.Links.Count);
            Assert.AreEqual(keyInstance, batch.Locations.Single().InstanceId);
        };
        Assert.IsFalse(f.Service.TryEnter(f.Player, f.Target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
        Assert.IsTrue(f.Session.Sent.OfType<CharacterActionMessage>().Any(message => message.Action == CharacterActionType.DeleteItem));
        f.AssertNoErrors();
    }

    [TestMethod]
    public void UnlinkedKeyRemovalCommitsBeforeDeletingOnlyTheRetiredKeyFromInventory()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        var key = f.Player.Inventory.Inventory.Content.Single();
        int unrelatedSlot = f.Player.Inventory.Inventory.FindFreeSlot();
        Item unrelated = TestWorld.CreateItem(lowId: QuestDungeonService.DuplicatorLowId, instanceId: 700001);
        f.Player.Inventory.Inventory.Add(unrelatedSlot, unrelated);
        f.Store.Links.Remove(key.Value.InstanceId);
        f.Session.Sent.Clear();
        f.Persistence.BeforeMutation = batch =>
        {
            Assert.IsTrue(Monitor.IsEntered(f.Player.PersistenceGate));
            Assert.AreSame(key.Value, f.Player.Inventory.Inventory.Content[key.Key]);
            Assert.IsFalse(f.Session.Sent.OfType<CharacterActionMessage>().Any());
            CollectionAssert.AreEqual(new[] { key.Value.InstanceId }, f.Store.RetiredKeys.ToArray());
            ItemLocationUpdate retired = batch.Locations.Single();
            Assert.AreEqual(key.Value.InstanceId, retired.InstanceId);
            Assert.AreEqual((int)IdentityType.None, retired.ContainerType);
            Assert.AreEqual(f.Player.Identity.Instance, retired.ContainerInstance);
        };
        f.Service.RestoreKeys(f.Player);
        Assert.AreEqual(1, f.Persistence.MutationCalls);
        Assert.IsFalse(f.Player.Inventory.Inventory.Content.ContainsKey(key.Key));
        Assert.AreSame(unrelated, f.Player.Inventory.Inventory.Content.Single().Value);
        CharacterActionMessage deletion = f.Session.Sent.OfType<CharacterActionMessage>().Single();
        Assert.AreEqual(CharacterActionType.DeleteItem, deletion.Action);
        Assert.AreEqual(IdentityType.Inventory, deletion.Target.Type);
        Assert.AreEqual(key.Key, deletion.Target.Instance);
        Assert.AreEqual(0, f.Session.Sent.OfType<SimpleItemFullUpdateMessage>().Count());
        f.Service.RestoreKeys(f.Player);
        Assert.AreEqual(1, f.Persistence.MutationCalls, "A removed key must not be retired or deleted again on restore.");
        Assert.IsFalse(f.Service.TryEnter(f.Player, f.Target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        f.AssertNoErrors();
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(25)]
    [DataRow(220)]
    public void OrdinaryMissionPopulationUsesAuthoredCatalogTemplatesAtRequestedQuality(int quality)
    {
        using var f = new Fixture();
        QuestDungeonPlayfield world = f.CreateWorld(quality);
        Assert.IsNotNull(world.Layout.Population);
        var hashes = world.Layout.Population.RegularNpcHashes;
        Assert.IsTrue(hashes.Length > 1, "The ordinary mission pool must not collapse to one placeholder NPC.");
        Assert.IsFalse(hashes.Contains("ZK3X"), "The previous unconditional alien override is not ordinary mission population.");
        foreach (string hash in hashes)
        {
            Assert.IsTrue(RuntimeData.Value.HasMobTemplate(hash), "Missing authored NPC hash " + hash);
            Assert.IsTrue(RuntimeData.Value.TryResolveMobTemplate(hash, quality, out MobTemplate template));
            Assert.IsFalse(template.UnresolvedPlaceholder, hash);
            Assert.AreNotEqual(MobTemplate.FallbackHash, template.Hash, hash);
            Assert.IsFalse(template.Has(NpcFeature.NoCombat), hash);
            Assert.AreNotEqual(220, template.Stats.GetValueOrDefault((int)CharacterStat.NPCFamily), hash + " must not resolve to the alien NPC family.");
            Assert.IsTrue(template.Stats.TryGetValue((int)CharacterStat.MonsterData, out int monsterData) && monsterData > 0, hash);
            int expectedLevel = Math.Clamp(quality, template.MinLevel, template.MaxLevel);
            Assert.AreEqual(expectedLevel, template.Stats[(int)CharacterStat.Level], hash);
            TestContext.WriteLine("Mission QL={0} hash={1} resolved={2} name={3} level={4} supported={5}-{6} MonsterData={7}",
                quality, hash, template.Hash, template.Name, expectedLevel, template.MinLevel, template.MaxLevel, monsterData);
        }
        f.AssertNoErrors();
    }

    [TestMethod]
    public void MissionPopulationRejectsUnresolvedHashesInsteadOfAcceptingNpcPlaceholderFallback()
    {
        const string missingHash = "MISSING_MISSION_POPULATION_FIXTURE";
        Assert.IsTrue(RuntimeData.Value.CanResolveMobHash(missingHash), "The general NPC lookup can use its historical fallback.");
        Assert.IsFalse(RuntimeData.Value.HasMobTemplate(missingHash));
        Assert.IsFalse(new DungeonPopulationPolicy
        {
            SpawnPointChancePercent = 35, RegularNpcHashes = [missingHash]
        }.IsValid(RuntimeData.Value), "Mission population requires authored catalog coverage, not the fallback.");
    }

    [TestMethod]
    public void OrdinaryPopulationDoesNotReplaceAnExistingMissionKillTarget()
    {
        using var f = new Fixture();
        const string acceptedTarget = "LE01";
        QuestDungeonPlayfield world = f.CreateWorld(25, targetHash: acceptedTarget);
        var spawns = world.GetRequiredService<HashSpawnSystem>();
        var points = (List<HashSpawnPoint>)typeof(HashSpawnSystem)
            .GetField("_allPoints", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spawns)!;
        Assert.AreEqual(acceptedTarget, world.TargetHash);
        Assert.AreEqual(1, points.Count(point => point.HashText == acceptedTarget));
        Assert.IsTrue(points.Count > 1);
        foreach (HashSpawnPoint point in points.Where(point => point.HashText != acceptedTarget))
        {
            Assert.IsTrue(world.Layout.Population.RegularNpcHashes.Contains(point.HashText));
            Assert.AreEqual(25, point.MinLevel);
            Assert.AreEqual(25, point.MaxLevel);
        }
        f.AssertNoErrors();
    }

    [TestMethod]
    [DataRow(2, 321)]
    [DataRow(3, 324)]
    [DataRow(25, 341)]
    [DataRow(220, 351)]
    public void ActualMissionWorldRegistersConfiguredOneShotPopulationAndSpawnsCatalogNpcStats(int quality, int style)
    {
        using var f = new Fixture();
        QuestDungeonPlayfield world = f.CreateWorld(quality, style: style);
        Assert.IsNotNull(world.Layout.Population);
        byte[] unchangedLayout = world.Layout.Generator.ToByteArray();
        var spawns = world.GetRequiredService<HashSpawnSystem>();
        var points = (List<HashSpawnPoint>)typeof(HashSpawnSystem)
            .GetField("_allPoints", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spawns)!;
        Assert.IsTrue(points.Count > 0, "The generated room districts must register ordinary mission NPCs.");
        foreach (HashSpawnPoint point in points)
        {
            Assert.IsTrue(world.Layout.Population.RegularNpcHashes.Contains(point.HashText), point.HashText);
            Assert.IsFalse(point.IsStatic);
            Assert.IsFalse(point.RespawnsAfterDeath);
            Assert.AreEqual(quality, point.MinLevel);
            Assert.AreEqual(quality, point.MaxLevel);
            Assert.AreEqual(quality, point.Source.MinLevel);
            Assert.AreEqual(quality, point.Source.MaxLevel);
            Assert.AreEqual(point.CellId, world.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>()
                .Grid.ResolveCell(point.Sites[0].Centre).Id);
        }
        Assert.IsTrue(points.Select(point => point.HashText).Distinct().Count() > 1,
            "A generated mission should use the editable pool instead of repeating the old alien override.");

        // Exercise the real hash-spawn registration path while skipping only the district's random chance timer.
        MethodInfo trySpawn = typeof(HashSpawnSystem).GetMethod("TrySpawn", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (HashSpawnPoint point in points.GroupBy(point => point.HashText).Select(group => group.First()))
        {
            point.State = HashSpawnState.Alive;
            Assert.IsTrue((bool)trySpawn.Invoke(spawns, [point, true])!, point.HashText);
            NpcCharacter[] npcs = point.MembersSnapshot().OfType<NpcCharacter>().ToArray();
            Assert.IsTrue(npcs.Length > 0, point.HashText);
            foreach (NpcCharacter npc in npcs)
            {
                Assert.AreSame(world, npc.Playfield);
                Assert.AreEqual(point.HashText, npc.SpawnHash);
                Assert.IsNotNull(npc.MobTemplate);
                Assert.IsFalse(npc.MobTemplate.UnresolvedPlaceholder);
                Assert.IsTrue(npc.Attackable);
                Assert.IsTrue(spawns.TryGetSpawnPoint(npc, out HashSpawnPoint owner));
                Assert.AreSame(point, owner);
                Assert.AreEqual(Math.Clamp(quality, npc.MobTemplate.MinLevel, npc.MobTemplate.MaxLevel), npc.Stats.GetOrZero(CharacterStat.Level));
                Assert.AreEqual(npc.MobTemplate.Stats[(int)CharacterStat.MonsterData], npc.Stats.GetOrZero(CharacterStat.MonsterData));
                Assert.AreNotEqual(220, npc.Stats.GetOrZero(CharacterStat.NPCFamily));
                TestContext.WriteLine("Spawn QL={0} style={1} hash={2} name={3} actualLevel={4}",
                    quality, style, npc.SpawnHash, npc.Name, npc.Stats.GetOrZero(CharacterStat.Level));
            }
        }
        CollectionAssert.AreEqual(unchangedLayout, world.Layout.Generator.ToByteArray());
        Assert.AreEqual(unchecked((int)f.Entrance.IdentityInstance), world.Layout.Generator.Identity.Instance);
        f.AssertNoErrors();
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(220)]
    public void ActualTeamWorldBuildsEveryPlatformAndButtonAndUsesBidirectionalIntrazoneTeleports(int quality)
    {
        using var f = new Fixture();
        QuestDungeonPlayfield world = f.CreateTeamWorld(quality);
        var dynels = world.GetRequiredService<DynelRegistry>().Dynels().OfType<DungeonFloorTransitionDynel>().ToArray();
        Assert.AreEqual(world.Layout.FloorTransitions.Count * 4, dynels.Length);
        Assert.IsTrue(dynels.Length > 0);
        foreach (var transition in world.Layout.FloorTransitions)
        {
            VerifyEndpoint(transition.From, transition.To);
            VerifyEndpoint(transition.To, transition.From);
        }
        Assert.AreEqual(0, f.Session.Transfers.Count, "A floor transition must retain the same playfield instance.");
        f.AssertNoErrors();

        void VerifyEndpoint(AORebirth.DungeonGenerator.DungeonTransitionEndpoint source,
            AORebirth.DungeonGenerator.DungeonTransitionEndpoint target)
        {
            foreach (int template in new[] { source.PlatformTemplateId, source.ButtonTemplateId })
            {
                DungeonFloorTransitionDynel dynel = dynels.Single(row => row.Template.Id == template
                    && row.Position.xf == source.Position.X && row.Position.yf == source.Position.Y && row.Position.zf == source.Position.Z);
                Assert.AreEqual(source.RoomIndex, dynel.Cell?.Id);
                SimpleItemFullUpdateMessage spawn = (SimpleItemFullUpdateMessage)dynel.BuildSpawnMessage();
                Assert.AreEqual(world.Identity.Instance, spawn.Playfield);
                Assert.IsTrue(spawn.Stats.Any(stat => stat.Value1 == CharacterStat.StaticInstance && stat.Value2 == (uint)template));
                Assert.IsTrue(spawn.Stats.Any(stat => stat.Value1 == CharacterStat.ACGItemLevel && stat.Value2 == (uint)dynel.Template.Quality));
                Assert.IsTrue(spawn.Stats.Any(stat => stat.Value1 == CharacterStat.MultipleCount && stat.Value2 == 1));
                f.Player.Playfield = world;
                f.Player.Position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
                f.Session.Sent.Clear();
                Assert.IsTrue(((IUsableDynel)dynel).TryUse(f.Player));
                Assert.AreSame(world, f.Player.Playfield);
                N3TeleportMessage teleport = f.Session.Sent.OfType<N3TeleportMessage>().Single();
                Assert.AreEqual(0, teleport.ChangePlayfield.Instance);
                Assert.AreEqual(world.Identity.Instance, teleport.Playfield2.Instance);
                Assert.AreEqual(target.Position.X, teleport.Destination.X);
                Assert.AreEqual(target.Position.Y, teleport.Destination.Y);
                Assert.AreEqual(target.Position.Z, teleport.Destination.Z);
                Assert.AreEqual(target.Position.X, f.Player.Position.xf);
                Assert.AreEqual(target.Position.Y, f.Player.Position.yf);
                Assert.AreEqual(target.Position.Z, f.Player.Position.zf);
            }
        }
    }

    [TestMethod]
    [DataRow("wrongInstance")]
    [DataRow("distant")]
    [DataRow("dead")]
    [DataRow("quarantined")]
    public void ActualFloorTransitionRejectsInvalidUsersWithoutTeleport(string denial)
    {
        using var f = new Fixture();
        QuestDungeonPlayfield world = f.CreateTeamWorld(2);
        DungeonFloorTransitionDynel dynel = world.GetRequiredService<DynelRegistry>().Dynels().OfType<DungeonFloorTransitionDynel>().First();
        f.Player.Playfield = world;
        f.Player.Position = new Vector3(dynel.Position.x, dynel.Position.y, dynel.Position.z);
        switch (denial)
        {
            case "wrongInstance":
                f.Player.Playfield = Blank<Playfield>();
                typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(f.Player.Playfield, world.Identity);
                break;
            case "distant": f.Player.Position = new Vector3(dynel.Position.x + 100, dynel.Position.y, dynel.Position.z); break;
            case "dead": typeof(Character).GetField("_deathNotified", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Player, true); break;
            case "quarantined": f.Player.QuarantinePersistence(); break;
        }
        Vector3 before = f.Player.Position;
        f.Session.Sent.Clear();
        Assert.IsFalse(((IUsableDynel)dynel).TryUse(f.Player));
        Assert.AreEqual(before.x, f.Player.Position.x);
        Assert.AreEqual(before.y, f.Player.Position.y);
        Assert.AreEqual(before.z, f.Player.Position.z);
        Assert.AreEqual(0, f.Session.Sent.OfType<N3TeleportMessage>().Count());
        Assert.AreEqual(0, f.Session.Transfers.Count);
    }

    [TestMethod]
    [DataRow(321)]
    [DataRow(324)]
    [DataRow(341)]
    [DataRow(351)]
    public void ObjectivePlacementFreezesReachablePointWithoutChangingLayout(int style)
    {
        using var f = new Fixture();
        QuestDungeonPlayfield world = f.CreateWorld(25, style: style);
        byte[] original = (byte[])world.Layout.GeneratorPayload.Clone();
        Assert.IsTrue(DungeonObjectivePlacement.TrySelect(RuntimeData.Value.RootPath, world.Layout, new StubLogger(), out int room, out var point));
        Assert.IsTrue(DungeonObjectivePlacement.TrySelect(RuntimeData.Value.RootPath, world.Layout, new StubLogger(), out int again, out var repeated));
        Assert.IsTrue(room > 0);
        Assert.AreEqual(room, again);
        Assert.AreEqual(BitConverter.SingleToUInt32Bits(point.xf), BitConverter.SingleToUInt32Bits(repeated.xf));
        Assert.AreEqual(BitConverter.SingleToUInt32Bits(point.yf), BitConverter.SingleToUInt32Bits(repeated.yf));
        Assert.AreEqual(BitConverter.SingleToUInt32Bits(point.zf), BitConverter.SingleToUInt32Bits(repeated.zf));
        var dungeon = AORebirth.World.Collision.DungeonCollisionBuilder.BuildLayout(RuntimeData.Value.RootPath, world.Layout.Generator);
        Assert.IsTrue(DungeonObjectivePlacement.RoomDistances(dungeon, world.Layout)[room] > 0);
        CollectionAssert.AreEqual(original, world.Layout.Generator.ToByteArray());
    }

    [TestMethod]
    public void ObjectiveWorldItemUsesExactBindingAndCollectsOnceOnlyAfterSuccessfulCommit()
    {
        using var f = new Fixture();
        int attempts = 0;
        bool committed = false;
        QuestDungeonPlayfield world = f.CreateObjectiveWorld((player, item) =>
        {
            attempts++;
            Assert.IsTrue(worldRegistry(item).TryGet(item.Identity, out var registered));
            Assert.AreSame(item, registered);
            Assert.AreEqual(0, f.Session.Sent.OfType<ContainerAddItemMessage>().Count());
            if (committed)
                f.PublishObjective(item);
            return committed;
        }, out var binding);
        var objective = world.GetRequiredService<DynelRegistry>().Dynels().OfType<MissionObjectiveItem>().Single();
        var update = (SimpleItemFullUpdateMessage)objective.BuildSpawnMessage();
        Assert.AreEqual(binding.Identity, update.Identity);
        Assert.AreEqual(0, update.Identitytype);
        Assert.AreEqual(0, update.Instance);
        Assert.AreEqual(binding.X, update.Coordinate.X);
        Assert.AreEqual(binding.Y, update.Coordinate.Y);
        Assert.AreEqual(binding.Z, update.Coordinate.Z);
        Assert.AreEqual((uint)binding.LowId, update.Stats.Single(row => row.Value1 == CharacterStat.ACGItemTemplateID).Value2);
        Assert.AreEqual((uint)binding.HighId, update.Stats.Single(row => row.Value1 == CharacterStat.ACGItemTemplateID2).Value2);
        Assert.AreEqual((uint)binding.Quality, update.Stats.Single(row => row.Value1 == CharacterStat.ACGItemLevel).Value2);
        Assert.AreEqual(0x80000003U, update.Stats.Single(row => row.Value1 == CharacterStat.Flags).Value2);
        Assert.AreEqual(1U, update.Stats.Single(row => row.Value1 == CharacterStat.QuestInstance).Value2);
        f.Player.Playfield = world;
        f.Player.Position = new Vector3(binding.X, binding.Y, binding.Z);
        var locality = world.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>();
        world.GetRequiredService<DynelRegistry>().Register(f.Player);
        locality.RegisterDynel(f.Player);
        locality.ActivatePlayerVisibility(f.Player);
        Assert.IsTrue(locality.IsVisibleTo(objective, f.Player));
        var observer = TestWorld.CreatePlayer(88);
        var observerSession = new Session();
        observer.Session = observerSession;
        observerSession.BindPlayer(observer);
        observer.Playfield = world;
        observer.Position = new Vector3(binding.X, binding.Y, binding.Z);
        world.GetRequiredService<DynelRegistry>().Register(observer);
        locality.RegisterDynel(observer);
        locality.ActivatePlayerVisibility(observer);
        Assert.IsTrue(locality.IsVisibleTo(objective, observer));
        f.Session.Sent.Clear();
        observerSession.Sent.Clear();
        Assert.IsFalse(objective.TryPickup(f.Player));
        Assert.AreEqual(1, attempts);
        Assert.IsTrue(world.GetRequiredService<DynelRegistry>().TryGet(binding.Identity, out _));
        committed = true;
        Assert.IsTrue(objective.TryPickup(f.Player));
        Assert.AreEqual(2, attempts);
        Assert.IsFalse(objective.TryPickup(f.Player));
        Assert.AreEqual(2, attempts);
        Assert.IsFalse(world.GetRequiredService<DynelRegistry>().TryGet(binding.Identity, out _));
        ContainerAddItemMessage ack = f.Session.Sent.OfType<ContainerAddItemMessage>().Single();
        Assert.AreEqual(binding.Identity, ack.SourceContainer);
        Assert.AreEqual(Identity.None, ack.Target);
        Assert.AreEqual(0, ack.TargetPlacement);
        Assert.AreEqual(0, f.Session.Sent.OfType<DespawnMessage>().Count(message => message.Identity == binding.Identity));
        Assert.AreEqual(1, observerSession.Sent.OfType<DespawnMessage>().Count(message => message.Identity == binding.Identity));
        SimpleItemFullUpdateMessage owned = f.Session.Sent.OfType<SimpleItemFullUpdateMessage>().Single(message => message.Identity == binding.Identity);
        int slot = f.Player.Inventory.Inventory.Content.Single(pair => pair.Value.Identity == binding.Identity).Key;
        Assert.AreEqual(f.Player.Identity, owned.Owner);
        Assert.AreEqual(1, (int)owned.Unknown2);
        Assert.AreEqual(slot, (int)owned.Unknown3);
        Assert.AreEqual(0x80000003U, owned.Stats.Single(row => row.Value1 == CharacterStat.Flags).Value2);
        Assert.IsTrue(f.Session.Sent.IndexOf(ack) < f.Session.Sent.IndexOf(owned));
        Assert.AreEqual(0, f.Session.Sent.OfType<TemplateActionMessage>().Count());

        static DynelRegistry worldRegistry(MissionObjectiveItem item) => item.Playfield!.GetRequiredService<DynelRegistry>();
    }

    [TestMethod]
    public void ObjectiveWithoutAcceptedFlagOverrideRetainsExistingWorldItemFlags()
    {
        using var f = new Fixture();
        var world = f.CreateObjectiveWorld((_, _) => false, out var binding, worldFlags: null);
        var objective = world.GetRequiredService<DynelRegistry>().Dynels().OfType<MissionObjectiveItem>().Single();
        var update = (SimpleItemFullUpdateMessage)objective.BuildSpawnMessage();
        Assert.IsNull(binding.WorldFlags);
        Assert.AreEqual(unchecked((uint)(ItemFlags.Visible | ItemFlags.TellCollision | ItemFlags.DisableStatelCollision)),
            update.Stats.Single(row => row.Value1 == CharacterStat.Flags).Value2);
        var restored = System.Text.Json.JsonSerializer.Deserialize<MissionItemObjectiveBinding>(
            System.Text.Json.JsonSerializer.Serialize(binding))!;
        Assert.IsTrue(binding.SameAs(restored));
        restored.WorldFlags = 0x80000003U;
        Assert.IsFalse(binding.SameAs(restored));
    }

    [TestMethod]
    [DataRow("wrongOwner")]
    [DataRow("wrongInstance")]
    [DataRow("distant")]
    [DataRow("dead")]
    [DataRow("quarantined")]
    [DataRow("unregistered")]
    public void ObjectivePickupRejectsInvalidActorBeforeCollection(string denial)
    {
        using var f = new Fixture();
        int attempts = 0;
        QuestDungeonPlayfield world = f.CreateObjectiveWorld((_, item) => { attempts++; f.PublishObjective(item); return true; }, out var binding);
        var objective = world.GetRequiredService<DynelRegistry>().Dynels().OfType<MissionObjectiveItem>().Single();
        f.Player.Playfield = world;
        f.Player.Position = new Vector3(binding.X, binding.Y, binding.Z);
        switch (denial)
        {
            case "wrongOwner": binding.OwnerId++; break;
            case "wrongInstance": f.Player.Playfield = Blank<Playfield>(); break;
            case "distant": f.Player.Position = new Vector3(binding.X + 100, binding.Y, binding.Z); break;
            case "dead": typeof(Character).GetField("_deathNotified", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Player, true); break;
            case "quarantined": f.Player.QuarantinePersistence(); break;
            case "unregistered": world.GetRequiredService<DynelRegistry>().UnregisterExact(objective); break;
        }
        Assert.IsFalse(objective.TryPickup(f.Player));
        Assert.AreEqual(0, attempts);
        Assert.AreEqual(0, f.Session.Sent.OfType<ContainerAddItemMessage>().Count());
    }

    [TestMethod]
    public void ClientGetItemDispatchRequiresExactPlayerAndWorldItemIdentities()
    {
        using var f = new Fixture();
        int attempts = 0;
        QuestDungeonPlayfield world = f.CreateObjectiveWorld((_, item) => { attempts++; f.PublishObjective(item); return true; }, out var binding);
        f.Player.Playfield = world;
        f.Player.Position = new Vector3(binding.X, binding.Y, binding.Z);
        var handler = new ZoneEngine_New.Core.MessageHandlers.ClientGetItemMessageHandler(f.Actions);
        handler.Handle(new ClientGetItemMessage { Identity = Identity.None, Identity1 = binding.Identity }, f.Session);
        handler.Handle(new ClientGetItemMessage { Identity = f.Player.Identity, Identity1 = Identity.None }, f.Session);
        Assert.AreEqual(0, attempts);
        handler.Handle(new ClientGetItemMessage { Identity = f.Player.Identity, Identity1 = binding.Identity }, f.Session);
        Assert.AreEqual(1, attempts);
        handler.Handle(new ClientGetItemMessage { Identity = f.Player.Identity, Identity1 = binding.Identity }, f.Session);
        Assert.AreEqual(1, attempts);
    }

    static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate this test build's repository root.");
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly MissionEntrancePlacement Entrance;
        internal readonly Player Player;
        internal readonly Session Session = new();
        internal readonly QuestStore Store = new();
        internal readonly OfferDao Offers = new();
        internal readonly DungeonLayoutGenerator Layouts;
        internal readonly PlayfieldManager Manager;
        internal readonly GeneratedMissionOffer Offer;
        internal readonly Persistence Persistence = new();
        internal QuestDungeonService Service = null!;
        readonly DiagnosticLogger _logger = new();
        readonly IItemBuilder _items;
        readonly IItemTemplateCatalog _templates;
        readonly IItemInstanceIdAllocator _ids;
        readonly InventoryFlushService _flush;
        readonly InventoryActionService _actions;
        readonly QuestService _quests;
        internal QuestService Quests => _quests;
        internal IItemBuilder Items => _items;
        internal IReadOnlyList<string> Information => _logger.Information;
        internal InventoryActionService Actions => _actions;
        internal IGameData GameData => RuntimeData.Value;
        readonly Playfield _exterior = Blank<Playfield>();

        internal Fixture(IMissionDao? completionDao = null, IItemBuilder? items = null, Func<DateTime>? now = null,
            CharacterSnapshotService? snapshots = null, IItemTemplateCatalog? templates = null,
            IInventoryRepository? inventory = null, IItemInstanceIdAllocator? ids = null, int playerId = 77,
            bool enableMissionPopulationPersistence = false)
        {
            Player = TestWorld.CreatePlayer(playerId);
            _templates = templates ?? new StubCatalog();
            _ids = ids ?? new StubInstanceIdAllocator();
            _items = items ?? new StubItemBuilder();
            Entrance = Catalog.Value.Placements.First(row => row.IdentityInstance > int.MaxValue && Catalog.Value.TryGetWorldPosition(row.Identity, out _));
            Assert.IsTrue(Catalog.Value.TryGetWorldPosition(Entrance.Identity, out var worldPos));
            Offer = new GeneratedMissionOffer
            {
                OwnerId = Player.Identity.Instance, OfferType = 0xDAC3, OfferInstance = 100,
                State = GeneratedMissionState.Offered, ExpiresAtUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
                EntranceType = unchecked((int)Entrance.IdentityType), EntranceInstance = unchecked((int)Entrance.IdentityInstance),
                DestinationType = unchecked((int)worldPos.PlayfieldIdentityType), DestinationInstance = Entrance.PlayfieldId,
                DestinationPlayfield = Entrance.PlayfieldId, DestinationX = Entrance.LocalX, DestinationY = Entrance.LocalY, DestinationZ = Entrance.LocalZ,
                EntranceLow = worldPos.WorldOffsetX, EntranceHigh = worldPos.WorldOffsetZ,
                MissionType = 1, Quality = 10, Title = "Destination fixture", Description = "Destination integration test"
            };
            Offers.Offer = Offer;
            Store.Offer = Offer;
            var lazyManager = new Lazy<PlayfieldManager>(() => Manager ?? throw new InvalidOperationException("Fixture manager is not initialized."));
            _flush = new InventoryFlushService(lazyManager, Persistence, _logger);
            _actions = new InventoryActionService(Persistence, _flush, _ids, _logger);
            _quests = new QuestService(new QuestCatalog(RuntimeData.Value, _logger), Store, _items, _flush, _logger,
                now ?? (() => DateTime.UtcNow), completionDao, snapshots);
            Layouts = new DungeonLayoutGenerator(RuntimeData.Value, _logger);
            Assert.IsTrue(Layouts.IsAvailable, "Existing runtime GameData must contain the configured procedural styles. Root=" + RuntimeData.Value.RootPath + "; " + _logger.Errors);
            if (!enableMissionPopulationPersistence)
            {
                // Destination and independent reward fixtures do not own persisted NPC/chest populations.
                // Integrated objective fixtures opt into the configured population owners explicitly.
                object policy = typeof(DungeonLayoutGenerator).GetField("_policy", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(Layouts)!;
                policy.GetType().GetProperty("TokenReward")!.SetValue(policy, null);
                policy.GetType().GetProperty("Chests")!.SetValue(policy, null);
            }
            var minter = new HashItemMinter(RuntimeData.Value, _templates, _items);
            Manager = new PlayfieldManager(_logger, new MessageRouter([], _logger), new PlayerHydrator(_items, RuntimeData.Value),
                RuntimeData.Value, _items, minter, inventory ?? new StubInventoryRepository(), _ids,
                new InventoryMoveService(_logger, _flush, _actions), _flush,
                new TradeService(_logger, RuntimeData.Value, _templates, minter, _ids, _flush, Persistence),
                Blank<CharacterSnapshotService>(), new PlayfieldMetricsRegistry(), new TeamService(), _templates, new Shops(), _quests);
            SetExteriorIdentity(Entrance.PlayfieldId);
            Player.Playfield = _exterior;
            Player.Position = new Vector3(Entrance.LocalX, Entrance.LocalY, Entrance.LocalZ);
            Player.Stats.Set(CharacterStat.Health, 100);
            Player.Stats.Set(CharacterStat.MaxHealth, 100);
            Player.QuestLog = new QuestLog();
            Player.Session = Session;
            Session.BindPlayer(Player);
            RestartService();
        }

        internal Identity Target => new() { Type = (IdentityType)Entrance.IdentityType, Instance = unchecked((int)Entrance.IdentityInstance) };
        internal bool Abandon(int journalInstance) => _quests.TryAbandon(Player, journalInstance);
        internal void AssertNoErrors() => Assert.AreEqual(string.Empty, _logger.Errors);
        internal string? Accept()
        {
            string? result = Service.AcceptOffer(Player, new Identity { Type = (IdentityType)Offer.OfferType, Instance = Offer.OfferInstance });
            if (result == null)
            {
                QuestDungeonParameters parameters = Parameters();
                Assert.IsTrue(Layouts.TryGenerate(parameters, out var layout));
                Assert.IsNotNull(layout.Doors, "Existing style data must provide its door templates.");
                AddFixtureTemplates(layout.Doors.DoorTemplate, layout.Doors.ExitTemplate);
            }
            return result;
        }
        internal QuestDungeonParameters Parameters()
        {
            Assert.IsNotNull(Store.Generated);
            Assert.IsTrue(QuestDungeonParameters.TryParse(Store.Generated.AcgBuildingGeneratorJson, out var parameters));
            return parameters;
        }
        internal QuestDungeonPlayfield CreateTeamWorld(int quality)
            => CreateWorld(quality, team: true);

        internal void PublishObjective(MissionObjectiveItem objective)
        {
            var binding = objective.Binding;
            int slot = Player.Inventory.Inventory.FindFreeSlot();
            Assert.IsTrue(slot >= 0);
            Assert.IsTrue(Player.Inventory.Inventory.Add(slot, _items.Create(binding.LowId, binding.HighId, binding.Quality,
                ItemSource.Quest, instanceId: binding.Instance, identity: binding.Identity)));
        }

        internal QuestDungeonPlayfield CreateObjectiveWorld(Func<Player, MissionObjectiveItem, bool> collect,
            out MissionItemObjectiveBinding binding, uint? worldFlags = 0x80000003U)
        {
            var parameters = new QuestDungeonParameters
            {
                Seed = 42, Quality = 25, EntranceInstance = Offer.EntranceInstance,
                DungeonPlayfield = QuestDungeonIds.FirstPlayfield + 100
            };
            Assert.IsTrue(Layouts.ConfigureNewDungeon(parameters, false));
            Assert.IsTrue(Layouts.TryGenerate(parameters, out var layout));
            Assert.IsTrue(DungeonObjectivePlacement.TrySelect(RuntimeData.Value.RootPath, layout, _logger, out int room, out var point));
            binding = new MissionItemObjectiveBinding
            {
                QuestId = "GOBJECTIVE", OwnerId = Player.Identity.Instance,
                IdentityType = (int)IdentityType.Terminal, Instance = 1234567,
                LowId = 100341, HighId = 100341, Quality = 1, RoomIndex = room,
                X = point.xf, Y = point.yf, Z = point.zf, QuestInstanceStat = 1, WorldFlags = worldFlags
            };
            Assert.IsNotNull(layout.Doors);
            AddFixtureTemplates(layout.Doors.DoorTemplate, layout.Doors.ExitTemplate);
            return Manager.GetOrCreateQuestDungeon(parameters.DungeonPlayfield, binding.QuestId, layout, Entrance,
                quality: parameters.Quality, objectiveItem: binding, collectObjective: collect);
        }

        internal QuestDungeonPlayfield CreateWorld(int quality, bool team = false, int? style = null, string? targetHash = null)
        {
            var parameters = new QuestDungeonParameters
            {
                Seed = 42, Quality = quality, EntranceInstance = Offer.EntranceInstance,
                DungeonPlayfield = QuestDungeonIds.FirstPlayfield + 100
            };
            Assert.IsTrue(Layouts.ConfigureNewDungeon(parameters, team));
            if (style.HasValue)
                parameters.LayoutPlan!.StyleId = style.Value;
            Assert.IsTrue(Layouts.TryGenerate(parameters, out DungeonLayout layout));
            Assert.IsNotNull(layout.Doors);
            AddFixtureTemplates(layout.Doors.DoorTemplate, layout.Doors.ExitTemplate);
            foreach (var transition in layout.FloorTransitions)
                foreach (var endpoint in new[] { transition.From, transition.To })
                    AddFixtureTemplates(endpoint.PlatformTemplateId, endpoint.ButtonTemplateId);
            return Manager.GetOrCreateQuestDungeon(parameters.DungeonPlayfield, "GFOUNDATION", layout, Entrance, targetHash, quality);
        }
        internal void SetExteriorIdentity(int playfield) => typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_exterior, new Identity { Type = IdentityType.Playfield, Instance = playfield });
        void AddFixtureTemplates(params int[] ids)
        {
            if (_templates is StubCatalog templates)
            {
                foreach (int id in ids)
                {
                    templates.Add(id, 1);
                }
            }
        }

        internal void RestartService()
        {
            Service?.Dispose();
            Service = new QuestDungeonService(Store, _quests, Catalog.Value, Layouts, Offers, _items, _ids,
                _flush, _actions, new Lazy<PlayfieldManager>(() => Manager), RuntimeData.Value, _logger,
                new HashItemMinter(RuntimeData.Value, _templates, _items));
        }
        public void Dispose() { Service?.Dispose(); Manager.Dispose(); _flush.Dispose(); }
    }

    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    sealed class DiagnosticLogger : ZoneEngine_New.Core.Logging.IZoneLogger
    {
        internal string Errors = string.Empty;
        internal readonly List<string> Information = [];
        public void Debug(string message) { }
        public void Info(string message) => Information.Add(message);
        public void Warn(string message) { }
        public void Error(string message) => Errors += message + Environment.NewLine;
        public void Error(Exception exception, string message) => Errors += message + " " + exception + Environment.NewLine;
        public ZoneEngine_New.Core.Logging.IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }

    internal sealed class Session : IZoneSession
    {
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        internal readonly List<MessageBody> Sent = [];
        internal Action<MessageBody>? BeforeSend;
        internal readonly List<(Playfield World, Vector3 Position)> Transfers = [];
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, Vector3 landing) => Transfers.Add((destination, landing));
        public void SendIntrazoneTeleport(Vector3 landing, AORebirth.Core.Vector.Quaternion heading, int destinationKey)
            => Send(ZoneSession.BuildIntrazoneTeleport(Player!, landing, heading, destinationKey));
        public void Send(byte[] packet) => throw new AssertFailedException("Unexpected raw packet.");
        public void Send(Message message) => Sent.Add(message.Body);
        public void Send(MessageBody body) { BeforeSend?.Invoke(body); Sent.Add(body); }
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new AssertFailedException();
        public void Close() => State = SessionState.Closed;
    }

    internal sealed class Persistence : ICharacterCoalesceCommit, IInventoryMutationPersistence, ITradePersistence
    {
        internal Action<InventoryMutationBatch>? BeforeMutation;
        internal int MutationCalls;
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates,
            int characterId, IReadOnlyList<int> nanos, IReadOnlyList<ActiveNanoRecord>? activeNanos, IReadOnlyList<SkillLockRecord>? skillLocks)
            => throw new AssertFailedException("Clean quest grants must not use an independent inventory commit.");
        public void Persist(InventoryMutationBatch batch)
        {
            Assert.IsNotNull(BeforeMutation, "Unexpected inventory mutation.");
            MutationCalls++;
            BeforeMutation(batch);
        }
        public void Persist(TradePersistenceBatch batch) => throw new AssertFailedException("Unexpected trade commit.");
    }

    sealed class Shops : IShopDao
    {
        public IList<ShopVendorData> ListForPlayfield(int playfieldId) => [];
    }

    internal sealed class QuestStore : ICharacterQuestStore
    {
        internal GeneratedMissionOffer Offer = null!;
        internal GeneratedQuestRow? Generated;
        internal CharacterQuestRow? Character;
        internal readonly Dictionary<int, string> Links = [];
        internal readonly List<QuestDungeonKeyItemRow> Keys = [];
        internal Action? BeforeAccept;
        internal Action<string>? BeforeEndKeys;
        internal int AcceptCalls;
        internal int EndKeysCalls;
        internal readonly List<int> RetiredKeys = [];
        public bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, long nowUtcTicks)
        {
            AcceptCalls++;
            BeforeAccept?.Invoke();
            Assert.AreEqual(Offer.OwnerId, ownerId);
            Assert.AreEqual(Offer.OfferType, offerType);
            Assert.AreEqual(Offer.OfferInstance, offerInstance);
            if (Offer.State != GeneratedMissionState.Offered) return false;
            Offer.State = GeneratedMissionState.Active;
            Generated = quest; Character = characterRow; Keys.Add(key); Links.Add(key.InstanceId, quest.QuestId);
            return true;
        }
        internal readonly List<QuestDungeonKeyItemRow> SourceItems = [];
        public bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, QuestDungeonKeyItemRow? sourceItem, long nowUtcTicks)
        {
            if (!TryAcceptOfferQuest(ownerId, offerType, offerInstance, quest, characterRow, key, nowUtcTicks))
                return false;
            if (sourceItem != null)
            {
                SourceItems.Add(sourceItem);
            }
            return true;
        }

        public string? TryAddDuplicateKey(int sourceKeyInstanceId, QuestDungeonKeyItemRow copy, int maxKeys, long nowUtcTicks)
        {
            if (!Links.TryGetValue(sourceKeyInstanceId, out string? quest) || Links.Count >= maxKeys) return null;
            Links.Add(copy.InstanceId, quest); Keys.Add(copy); return quest;
        }
        public IList<CharacterQuestRow> Load(int characterId) => Character != null ? [Character] : [];
        public IList<GeneratedQuestRow> LoadGeneratedFor(int characterId) => Generated != null ? [Generated] : [];
        public GeneratedQuestRow? LoadGenerated(string questId) => Generated?.QuestId == questId ? Generated : null;
        public IDictionary<int, string> LoadKeyQuests(IReadOnlyCollection<int> keyInstanceIds)
            => Links.Where(pair => keyInstanceIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        public IList<int> RetireUnlinkedKeys(IReadOnlyCollection<int> keyInstanceIds, int keyLowId)
        {
            Assert.AreEqual(QuestDungeonService.KeyLowId, keyLowId);
            int[] retired = keyInstanceIds.Where(id => !Links.ContainsKey(id)).ToArray();
            foreach (int id in retired) if (!RetiredKeys.Contains(id)) RetiredKeys.Add(id);
            return retired;
        }
        public IList<string> LoadExpiredQuestsWithKeys(long nowUtcTicks, int limit) => [];
        public int PurgeEndedGeneratedQuests(long cutoffUtcTicks, int limit) => 0;
        public int RetireDeadKeysForCharacter(int characterId, int keyLowId, IReadOnlyCollection<int> ownedContainerTypes, int bagContainerType) => 0;
        public void Save(CharacterQuestRow row) => Character = row;
        public void SaveGenerated(GeneratedQuestRow row) => Generated = row;
        public IList<RetiredDungeonKey> EndQuestKeys(string questId)
        {
            EndKeysCalls++;
            BeforeEndKeys?.Invoke(questId);
            var retired = Keys.Where(key => Links.TryGetValue(key.InstanceId, out string? linked) && linked == questId)
                .Select(key => new RetiredDungeonKey { KeyInstanceId = key.InstanceId,
                    ContainerType = key.ContainerType, ContainerInstance = key.ContainerInstance }).ToArray();
            foreach (var key in retired) { Links.Remove(key.KeyInstanceId); RetiredKeys.Add(key.KeyInstanceId); }
            return retired;
        }
        public void CloseCharacterQuests(string questId, int state, long nowUtcTicks) => throw new AssertFailedException();
    }

    internal sealed class OfferDao : IGeneratedMissionDao
    {
        internal GeneratedMissionOffer Offer = null!;
        public IList<GeneratedMissionOffer> ReadOffers(int owner) => owner == Offer.OwnerId ? [Offer] : [];
        public int ReserveIdentities(string sequence, int count) => throw new AssertFailedException();
        public GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch) => throw new AssertFailedException();
        public GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance) => throw new AssertFailedException();
        public IList<GeneratedMissionBinding> ReadAccepted(int owner) => throw new AssertFailedException();
        public GeneratedMissionBinding ReadAccepted(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult Observe(GeneratedMissionObservation observation) => throw new AssertFailedException();
        public GeneratedMissionResult Complete(GeneratedMissionCompletion completion) => throw new AssertFailedException();
        public GeneratedMissionResult End(int owner, int type, int instance, GeneratedMissionState state, long now) => throw new AssertFailedException();
        public GeneratedMissionResult AdvanceCleanup(int owner, int type, int instance, long version, long mask, long now) => throw new AssertFailedException();
        public IList<GeneratedMissionObject> ReadObjects(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult UpdateObjects(int owner, int type, int instance, IList<GeneratedMissionObject> objects, long now) => throw new AssertFailedException();
        public GeneratedMissionResult SavePosition(int owner, int type, int instance, int pf, float x, float y, float z, long now) => throw new AssertFailedException();
        public IList<MissionItemInstanceData> ReadArtifacts(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult CleanupArtifacts(int owner, int type, int instance, long now) => throw new AssertFailedException();
        public GeneratedMissionResult ClaimCorpseCredits(int owner, int type, int instance, int npc, int cash, long now) => throw new AssertFailedException();
    }
}
