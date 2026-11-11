namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;
using AoVector3 = AORebirth.Core.Vector.Vector3;
using Fixture = QuestDungeonDestinationTests.Fixture;

[TestClass]
[DoNotParallelize]
public sealed class MissionChestObjectiveIntegrationTests
{
    const int RolledOwner = 111;
    const int RollLevel = 60;
    const int RollOrigin = 800;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MissionChestObjectiveVisibilityDiagnostic")]
    public void CapturedArbitraryTeleportsRetainRoomContextWhileFreshChestUsesItsContainingRoom()
    {
        var (_, dungeon) = MissionChestPlacementTests.Build(324, 1, 818302858, 12);
        var grid = new ZoneEngine_New.Core.Playfield.Locality.CellGrid(null, 1);
        grid.ApplyDungeonRooms(dungeon);
        AoVector3[] positions = [new(94.21085f, 5, 140.8883f), new(43.091675f, 5, 80.905716f), new(53.915504f, 5, 142.87596f)];
        int[] freshCells = positions.Select(position => grid.ResolveCell(position).Id).ToArray();
        var contextualCells = new List<int>();
        int current = ZoneEngine_New.Core.Playfield.Locality.CellGrid.NonLocalCellId;
        foreach (AoVector3 position in positions)
        {
            current = grid.ResolveCell(position, current).Id;
            contextualCells.Add(current);
        }
        TestContext.WriteLine("MISSION_CHEST_CAPTURED_TELEPORT_CELLS " + JsonSerializer.Serialize(new
        {
            Style = 324, Floors = 1, LayoutSeed = 818302858, FloorSize = 12,
            Positions = positions.Select(position => new[] { position.xf, position.yf, position.zf }).ToArray(),
            FreshCells = freshCells, ContextualCells = contextualCells
        }));
        CollectionAssert.AreEqual(new[] { 15, 3, 20 }, freshCells);
        CollectionAssert.AreEqual(new[] { 15, 3, 1 }, contextualCells.ToArray());
        Assert.AreNotEqual(freshCells[2], contextualCells[2]);
    }

    [TestMethod]
    [TestCategory("MissionChestObjectiveIntegration")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void EveryAcceptedObjectiveReconstructsWithTheSameLayoutBindingsAndChestPopulation(int missionType)
    {
        using var run = new ObjectiveRun(missionType);
        var f = run.Fixture;
        QuestDungeonParameters accepted = f.Parameters();
        Assert.AreEqual(missionType, accepted.MissionType);
        Assert.AreEqual(324, accepted.LayoutPlan!.StyleId);
        Assert.AreEqual(1, accepted.LayoutPlan.FloorCount);
        Assert.AreEqual(12, accepted.LayoutPlan.FloorSize);
        Assert.AreEqual(missionType == 4 ? 2 : 1, run.Quest.RequiredCount);
        Assert.AreEqual(missionType is 0 or 1, accepted.ObjectiveNpc != null);
        Assert.AreEqual(missionType is 2 or 3 or 4, accepted.ObjectiveItem != null);
        Assert.AreEqual(missionType == 3, accepted.ObjectiveTool != null);
        Assert.AreEqual(missionType == 3 ? 1 : 0, f.Store.SourceItems.Count);
        AssertObjectiveBinding(run.World, accepted);
        Assert.IsNotNull(run.World.ChestPopulation);
        Assert.IsNull(run.World.ChestPopulation.DeferredReason);
        var key = new MissionKeyData(f.Player.Identity.Instance, run.Quest.QuestId);
        string sealKey = MissionChestPopulationFlags.Seal(run.Quest.AssignedAtUtc.Ticks);
        Assert.IsNotNull(run.Dao.GetFlag(key, sealKey), "Enabled chest generation must finish its durable seal, including a safe zero result.");
        byte[][] originalChests = ChestBodies(run.World);
        Assert.IsTrue(originalChests.Length > 0, "This representative coexistence case requires a nonempty chest population.");
        int persisted = int.Parse(run.Dao.GetFlag(key, sealKey).Value, CultureInfo.InvariantCulture);
        int published = PublishEveryChest(run, run.World);
        string planned = f.Information.Single(message => message.StartsWith("Mission chest population planned ", StringComparison.Ordinal));
        int requested = PlannedMetric(planned, "requested");
        int generated = PlannedMetric(planned, "generated");
        TestContext.WriteLine("MISSION_CHEST_OBJECTIVE_POPULATION " + JsonSerializer.Serialize(new
        {
            MissionType = missionType, Seed = accepted.Seed, Style = accepted.LayoutPlan!.StyleId,
            Floors = accepted.LayoutPlan.FloorCount, FloorSize = accepted.LayoutPlan.FloorSize,
            Requested = requested, Generated = generated, Persisted = persisted,
            Instantiated = originalChests.Length, SessionPublished = published,
            PlanDiagnostic = planned,
            Publication = "REAL_LOCALITY_TO_TEST_SESSION", Persistence = "EXISTING_IN_MEMORY_TRANSACTION"
        }));
        Assert.AreEqual(generated, persisted);
        Assert.AreEqual(persisted, originalChests.Length);
        Assert.AreEqual(originalChests.Length, published);
        Assert.AreEqual(originalChests.Length.ToString(CultureInfo.InvariantCulture), run.Dao.GetFlag(key, sealKey).Value);
        byte[] originalLayout = run.World.Layout.GeneratorPayload;
        int itemCount = run.Dao.Items.Count;
        long assigned = run.Quest.AssignedAtUtc.Ticks;
        string frozen = f.Store.Generated!.AcgBuildingGeneratorJson;

        Assert.IsTrue(f.Manager.TryReleaseQuestDungeon(accepted.DungeonPlayfield, run.Quest.QuestId));
        f.RestartService();
        var restored = f.Service.ResolveLogin(accepted.DungeonPlayfield);
        var replay = (QuestDungeonPlayfield)restored.Playfield;

        Assert.AreNotSame(run.World, replay);
        CollectionAssert.AreEqual(originalLayout, replay.Layout.GeneratorPayload);
        Assert.AreEqual(frozen, f.Store.Generated.AcgBuildingGeneratorJson);
        Assert.AreEqual(assigned, f.Store.Character!.AssignedAtUtcTicks);
        AssertObjectiveBinding(replay, f.Parameters());
        Assert.IsNotNull(replay.ChestPopulation);
        Assert.IsNull(replay.ChestPopulation.DeferredReason);
        byte[][] replayChests = ChestBodies(replay);
        Assert.IsTrue(replayChests.Length > 0);
        Assert.AreEqual(replayChests.Length, PublishEveryChest(run, replay));
        Assert.AreEqual(originalChests.Length, replayChests.Length);
        for (int index = 0; index < originalChests.Length; index++)
        {
            CollectionAssert.AreEqual(originalChests[index], replayChests[index]);
        }
        Assert.AreEqual(itemCount, run.Dao.Items.Count, "Reconstruction must not mint another objective, source tool or chest item.");
    }

    [TestMethod]
    [TestCategory("MissionChestObjectiveIntegration")]
    public void FindPersonInspectionRequiresTheAcceptedOwnerAndRegisteredActor()
    {
        using var run = new ObjectiveRun(1);
        var f = run.Fixture;
        var binding = f.Parameters().ObjectiveNpc!;
        var registry = run.World.GetRequiredService<DynelRegistry>();
        Assert.IsTrue(registry.TryGet(binding.Identity, out var entity));
        var npc = (NpcCharacter)entity;
        f.Player.Position = npc.Position;
        Player other = run.OtherPlayer();
        other.Position = npc.Position;
        int calls = run.Dao.Calls;

        f.Quests.OnNpcInspected(other, npc);
        Assert.AreEqual(0, run.Dao.CharacterQuest!.Progress);
        Assert.AreEqual(calls, run.Dao.Calls, "Another owner must be rejected before objective persistence.");
        Assert.IsTrue(registry.UnregisterExact(npc));
        f.Quests.OnNpcInspected(f.Player, npc);
        Assert.AreEqual(0, run.Dao.CharacterQuest.Progress);
        registry.Register(npc);

        f.Quests.OnNpcInspected(f.Player, npc);

        Assert.AreEqual(run.Quest.RequiredCount, run.Dao.CharacterQuest.Progress);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.AreEqual(MissionRewardStatus.Pending, run.Dao.Rewards.Values.Single().Status);
    }

    [TestMethod]
    [TestCategory("MissionChestObjectiveIntegration")]
    public void RepairRequiresTheAcceptedOwnerToolAndExactMachineBeforeConsumingTheTool()
    {
        using var run = new ObjectiveRun(3);
        var f = run.Fixture;
        var parameters = f.Parameters();
        var tool = parameters.ObjectiveTool!;
        var target = parameters.ObjectiveItem!;
        var source = f.Player.Inventory.Inventory.Content.Single(pair => pair.Value.Identity == tool.Identity);
        Identity slot = new() { Type = IdentityType.Inventory, Instance = source.Key };
        f.Player.Position = new AoVector3(target.X, target.Y, target.Z);
        Player other = run.OtherPlayer();
        other.Position = f.Player.Position;
        Assert.IsTrue(other.Inventory.Inventory.Add(other.Inventory.Inventory.Offset, source.Value));
        Identity otherSlot = new() { Type = IdentityType.Inventory, Instance = other.Inventory.Inventory.Offset };
        int calls = run.Dao.Calls;

        Assert.IsFalse(f.Quests.TryUseGeneratedMissionItem(other, otherSlot, target.Identity, out bool otherHandled));
        Assert.IsTrue(otherHandled);
        Assert.IsFalse(f.Quests.TryUseGeneratedMissionItem(f.Player, slot, Identity.None, out bool wrongTargetHandled));
        Assert.IsTrue(wrongTargetHandled);
        Assert.AreEqual(calls, run.Dao.Calls);
        Assert.AreEqual(0, run.Dao.CharacterQuest!.Progress);
        Assert.AreEqual((int)IdentityType.Inventory, run.Dao.Items[tool.Instance].ContainerType);

        Assert.IsTrue(f.Quests.TryUseGeneratedMissionItem(f.Player, slot, target.Identity, out bool handled));

        Assert.IsTrue(handled);
        Assert.AreEqual(run.Quest.RequiredCount, run.Dao.CharacterQuest.Progress);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.IsFalse(f.Player.Inventory.Inventory.Content.ContainsKey(source.Key));
        Assert.AreEqual(0, run.Dao.Items[tool.Instance].ContainerType);
    }

    [TestMethod]
    [TestCategory("MissionChestObjectiveIntegration")]
    public void ReturnRequiresTheAcceptedOwnerAndIssuingTerminalAfterExactItemCollection()
    {
        using var run = new ObjectiveRun(4);
        var f = run.Fixture;
        var parameters = f.Parameters();
        var binding = parameters.ObjectiveItem!;
        Assert.IsTrue(run.World.GetRequiredService<DynelRegistry>().TryGet(binding.Identity, out var entity));
        var objective = (MissionObjectiveItem)entity;
        f.Player.Position = objective.Position;
        Player other = run.OtherPlayer();
        other.Position = objective.Position;

        Assert.IsFalse(objective.TryPickup(other));
        Assert.AreEqual(0, run.Dao.CharacterQuest!.Progress);
        Assert.IsTrue(objective.TryPickup(f.Player));
        Assert.AreEqual(1, run.Dao.CharacterQuest.Progress);
        Assert.AreEqual(1, run.Quest.Progress);
        Assert.IsFalse(run.Quest.IsRewardPending);
        var source = f.Player.Inventory.Inventory.Content.Single(pair => pair.Value.Identity == binding.Identity);
        Identity slot = new() { Type = IdentityType.Inventory, Instance = source.Key };
        f.Player.Playfield = run.Exterior;
        f.Player.Position = run.Terminal.Position;
        other.Playfield = run.Exterior;
        other.Position = run.Terminal.Position;
        Assert.IsTrue(other.Inventory.Inventory.Add(other.Inventory.Inventory.Offset, source.Value));
        Identity otherSlot = new() { Type = IdentityType.Inventory, Instance = other.Inventory.Inventory.Offset };
        int calls = run.Dao.Calls;

        Assert.IsFalse(f.Quests.TryUseGeneratedMissionItem(other, otherSlot, parameters.OriginTerminal, out bool otherHandled));
        Assert.IsTrue(otherHandled);
        Assert.IsFalse(f.Quests.TryUseGeneratedMissionItem(f.Player, slot, Identity.None, out bool wrongTerminalHandled));
        Assert.IsTrue(wrongTerminalHandled);
        Assert.AreEqual(calls, run.Dao.Calls);
        Assert.AreEqual(1, run.Dao.CharacterQuest.Progress);

        Assert.IsTrue(f.Quests.TryUseGeneratedMissionItem(f.Player, slot, parameters.OriginTerminal, out bool handled));

        Assert.IsTrue(handled);
        Assert.AreEqual(2, run.Dao.CharacterQuest.Progress);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.AreEqual(0, run.Dao.Items[binding.Instance].ContainerType);
    }

    [TestMethod]
    [TestCategory("MissionObjectiveFullLifecycle")]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void RolledObjectiveCompletesFromActualContentAndPaysOnceBeforeCleanup(int missionType)
    {
        using var run = new ObjectiveRun(missionType, fromRoll: true);
        var f = run.Fixture;
        var quest = run.Quest;
        var accepted = f.Parameters();
        Assert.AreEqual(DungeonLayoutGenerator.CurrentVersion, accepted.GeneratorVersion);
        Assert.AreEqual(MissionLevelRuntime.GetMissionQuality(RollLevel, 1), accepted.Quality);
        Assert.AreEqual(GeneratedMissionState.Active, f.Offer.State);
        AssertObjectiveBinding(run.World, accepted);
        AssertObjectivePolicy(f, quest, accepted);

        int key = f.Store.Links.Single().Key;
        var player = f.Player;
        var world = run.World;
        var registry = world.GetRequiredService<DynelRegistry>();
        if (!quest.Template.IsReturnItem)
        {
            f.Manager.RegisterPlayer(player);
        }
        Assert.IsTrue(OnWorld(world, () =>
        {
            if (accepted.ObjectiveNpc is { } npcBinding)
            {
                Assert.IsTrue(registry.TryGet(npcBinding.Identity, out var actor));
                var npc = (NpcCharacter)actor!;
                player.Position = npc.Position;
                if (quest.Template.IsKill)
                {
                    registry.Register(player);
                    try
                    {
                        Assert.IsTrue(npc.ApplyDamage(player, npc.Stats.GetOrZero(CharacterStat.Health), HitType.Normal));
                    }
                    finally
                    {
                        Assert.IsTrue(registry.UnregisterExact(player));
                    }
                    Assert.IsTrue(npc.IsDead);
                    Assert.IsTrue(MissionTokenProgress.IsObjectiveKilled(run.Dao, quest.QuestId, quest.AssignedAtUtc.Ticks));
                }
                else
                {
                    f.Quests.OnNpcInspected(player, npc);
                }
            }
            else
            {
                var itemBinding = accepted.ObjectiveItem!;
                Assert.IsTrue(registry.TryGet(itemBinding.Identity, out var target));
                var item = (MissionObjectiveItem)target!;
                player.Position = item.Position;
                if (accepted.ObjectiveTool is { } tool)
                {
                    Assert.IsTrue(f.Quests.TryUseGeneratedMissionItem(player, HeldSlot(player, tool.Identity), item.Identity, out bool handled));
                    Assert.IsTrue(handled);
                    Assert.AreEqual(0, run.Dao.Items[tool.Instance].ContainerType);
                }
                else
                {
                    Assert.IsTrue(item.TryPickup(player));
                }
            }
            return true;
        }));

        if (quest.Template.IsReturnItem)
        {
            Assert.AreEqual(1, quest.Progress);
            Assert.IsFalse(quest.IsRewardPending);
            f.SetExteriorIdentity(accepted.OriginPlayfield);
            player.Playfield = run.Exterior;
            player.Position = run.Terminal.Position;
            Assert.IsTrue(f.Quests.TryUseGeneratedMissionItem(player, HeldSlot(player, accepted.ObjectiveItem!.Identity),
                accepted.OriginTerminal, out bool handled));
            Assert.IsTrue(handled);
            Assert.AreEqual(0, run.Dao.Items[accepted.ObjectiveItem.Instance].ContainerType);
        }

        Assert.AreEqual(quest.RequiredCount, run.Dao.CharacterQuest!.Progress);
        Assert.IsTrue(quest.IsRewardPending);
        Assert.AreEqual(MissionRewardStatus.Pending, run.Dao.Rewards.Values.Single().Status);
        int beforeCash = player.Stats.GetOrZero(CharacterStat.Cash);
        int beforeXp = player.Stats.GetOrZero(CharacterStat.XP);
        if (quest.Template.IsReturnItem)
        {
            f.Quests.RetryPendingRewards(player, force: true);
        }
        else
        {
            Assert.IsTrue(OnWorld(world, () =>
            {
                f.Quests.RetryPendingRewards(player, force: true);
                return true;
            }));
            player.Playfield = run.Exterior;
        }

        f.AssertNoErrors();
        Assert.AreEqual(QuestState.Completed, quest.State);
        Assert.AreEqual((int)QuestState.Completed, run.Dao.CharacterQuest.State);
        Assert.AreEqual(MissionRewardStatus.Applied, run.Dao.Rewards.Values.Single().Status);
        int expectedCash = beforeCash + f.Offer.CashReward;
        Assert.AreEqual(expectedCash, player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual((long)expectedCash, run.Dao.Stats[(int)CharacterStat.Cash]);
        Assert.IsTrue(f.Session.Sent.OfType<StatMessage>().Any(message => message.Stats.Any(stat =>
            stat.Value1 == CharacterStat.Cash && stat.Value2 == (uint)expectedCash)));
        Assert.IsTrue(player.Stats.GetOrZero(CharacterStat.XP) > beforeXp);
        Assert.AreEqual(1, run.Dao.Items.Values.Count(row => row.LowId == f.Offer.RewardLowId
            && row.HighId == f.Offer.RewardHighId && row.Quality == f.Offer.RewardQuality && row.ContainerType == (int)IdentityType.Inventory));
        Assert.AreEqual(0, f.Store.Links.Count);
        CollectionAssert.Contains(f.Store.RetiredKeys, key);
        f.Service.RestoreKeys(player);
        Assert.IsFalse(player.Inventory.EnumerateHeldItems().Any(item => item.InstanceId == key));
        Assert.AreEqual((int)IdentityType.None, run.Dao.Items[key].ContainerType);
        Assert.IsTrue(f.Manager.TryReleaseQuestDungeon(accepted.DungeonPlayfield, quest.QuestId));
        Assert.IsFalse(f.Manager.TryGetQuestDungeon(accepted.DungeonPlayfield, out _));
        int durableItems = run.Dao.Items.Count;
        int completedCash = player.Stats.GetOrZero(CharacterStat.Cash);
        f.Quests.RetryPendingRewards(player, force: true);
        Assert.AreEqual(durableItems, run.Dao.Items.Count);
        Assert.AreEqual(completedCash, player.Stats.GetOrZero(CharacterStat.Cash));
        f.AssertNoErrors();
        TestContext.WriteLine($"MISSION_OBJECTIVE_FULL_LIFECYCLE type={missionType} destination={accepted.EntrancePlayfield} quality={accepted.Quality} objective={quest.Template.Objective.Npc ?? quest.Template.Objective.WorldItem} reward={f.Offer.RewardLowId}/{f.Offer.RewardHighId}QL{f.Offer.RewardQuality} completion=APPLIED keyCleanup=APPLIED persistence=IN_MEMORY");
    }

    static Identity HeldSlot(Player player, Identity item)
        => new() { Type = IdentityType.Inventory, Instance = player.Inventory.Inventory.Content.Single(pair => pair.Value.Identity == item).Key };

    static void AssertObjectivePolicy(Fixture fixture, PlayerQuest quest, QuestDungeonParameters accepted)
    {
        if (accepted.ObjectiveNpc is { } npc)
        {
            var policy = fixture.Layouts.NpcObjectivePolicy!;
            CollectionAssert.Contains(quest.Template.IsKill ? policy.NpcHashes : policy.FindPersonNpcHashes, npc.NpcHash);
            Assert.AreEqual(npc.NpcHash, quest.Template.Objective.Npc);
            Assert.IsTrue(fixture.GameData.TryResolveMobTemplate(npc.NpcHash, npc.Quality, out var template));
            Assert.IsFalse(template.UnresolvedPlaceholder);
            return;
        }

        var selected = quest.Template.IsFindItem ? fixture.Layouts.FindItemPolicy
            : quest.Template.IsReturnItem ? fixture.Layouts.ReturnItemPolicy : fixture.Layouts.RepairMachinePolicy!.Target;
        AssertItemPolicy(accepted.ObjectiveItem!, quest.Template.Objective.WorldItem, selected!);
        if (accepted.ObjectiveTool is { } tool)
        {
            AssertItemPolicy(tool, quest.Template.Objective.SpecItem, fixture.Layouts.RepairMachinePolicy!.Source!);
        }

        void AssertItemPolicy(MissionItemObjectiveBinding item, string? hash, DungeonFindItemPolicy policy)
        {
            Assert.IsNotNull(hash);
            CollectionAssert.Contains(policy.ItemHashes, hash);
            Assert.AreEqual(policy.Quality, item.Quality);
            var families = new List<HashInstance>();
            fixture.GameData.CollectHashLeafInstances(hash, families);
            Assert.IsTrue(families.Any(family => family.TemplateIds.Contains(item.LowId) && family.TemplateIds.Contains(item.HighId)));
            Assert.IsTrue(MissionChestPlacementTests.Templates.Value.TryGet(item.LowId, out _));
            Assert.IsTrue(MissionChestPlacementTests.Templates.Value.TryGet(item.HighId, out _));
        }
    }

    static int PlannedMetric(string message, string key)
        => int.Parse(message.Split(' ').Single(part => part.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..],
            CultureInfo.InvariantCulture);

    static T OnWorld<T>(QuestDungeonPlayfield world, Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(world.Dispatch(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception exception) { completion.SetException(exception); }
        }), "The active playfield must accept the test projection.");
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }

    int PublishEveryChest(ObjectiveRun run, QuestDungeonPlayfield world)
        => OnWorld(world, () => PublishEveryChestOnTick(run, world));

    int PublishEveryChestOnTick(ObjectiveRun run, QuestDungeonPlayfield world)
    {
        world.RequireTick();
        var player = run.Fixture.Player;
        var registry = world.GetRequiredService<DynelRegistry>();
        var locality = world.GetRequiredService<ZoneEngine_New.Core.Playfield.Locality.PlayfieldLocality>();
        Chest[] chests = registry.Dynels().OfType<Chest>().ToArray();
        var identities = chests.Select(chest => chest.Identity.Instance).ToHashSet();
        Playfield originalWorld = player.Playfield!;
        AoVector3 originalPosition = player.Position;
        int[] preClearSent = SentChestIds();
        int[] preClearVisible = chests.Where(chest => locality.IsVisibleTo(chest, player))
            .Select(chest => chest.Identity.Instance).ToArray();
        var visits = new List<object>();
        run.Fixture.Session.Sent.Clear();
        registry.Register(player);
        try
        {
            player.Playfield = world;
            foreach (Chest chest in chests)
            {
                int? beforeCell = player.Cell?.Id;
                int sentBefore = run.Fixture.Session.Sent.Count;
                int[] idsBefore = SentChestIds();
                bool visibleBefore = locality.IsVisibleTo(chest, player);
                locality.UnregisterDynel(player);
                player.Position = chest.Position;
                locality.RegisterDynel(player);
                int? registeredCell = player.Cell?.Id;
                Assert.IsNotNull(chest.Cell, "A registered chest must have a locality cell.");
                Assert.AreEqual(chest.Cell?.Id, registeredCell,
                    "An independent test teleport must use the same fresh containing room as its chest.");
                int sentAfterRegister = run.Fixture.Session.Sent.Count;
                locality.ActivatePlayerVisibility(player);
                visits.Add(new
                {
                    Target = chest.Identity.Instance, Position = new[] { chest.Position.xf, chest.Position.yf, chest.Position.zf },
                    ChestCell = chest.Cell?.Id, PlayerCellBefore = beforeCell, PlayerCellAfterRegister = registeredCell,
                    SentBefore = sentBefore, SentAfterRegister = sentAfterRegister, SentAfterActivate = run.Fixture.Session.Sent.Count,
                    ChestIdsBefore = idsBefore, ChestIdsAfterActivate = SentChestIds(),
                    VisibleBefore = visibleBefore, VisibleAfterActivate = locality.IsVisibleTo(chest, player)
                });
            }
            return run.Fixture.Session.Sent.OfType<SmokeLounge.AOtomation.Messaging.Messages.N3Messages.ChestItemFullUpdateMessage>()
                .Where(message => identities.Contains(message.Identity.Instance)).Select(message => message.Identity.Instance).Distinct().Count();
        }
        finally
        {
            TestContext.WriteLine("MISSION_CHEST_OBJECTIVE_VISIBILITY " + JsonSerializer.Serialize(new
            {
                PreClearSentChestIds = preClearSent, PreClearVisibleChestIds = preClearVisible, Visits = visits
            }));
            locality.UnregisterDynel(player);
            registry.UnregisterExact(player);
            player.Playfield = originalWorld;
            player.Position = originalPosition;
        }

        int[] SentChestIds() => run.Fixture.Session.Sent
            .OfType<SmokeLounge.AOtomation.Messaging.Messages.N3Messages.ChestItemFullUpdateMessage>()
            .Where(message => identities.Contains(message.Identity.Instance))
            .Select(message => message.Identity.Instance).Distinct().ToArray();
    }

    static void ConfigureRepresentativeSoloProfile(DungeonLayoutGenerator generator)
    {
        object policy = typeof(DungeonLayoutGenerator).GetField("_policy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(generator)!;
        var profiles = (System.Collections.IEnumerable)policy.GetType().GetProperty("Profiles")!.GetValue(policy)!;
        foreach (object profile in profiles)
        {
            Type type = profile.GetType();
            if ((bool)type.GetProperty("Team")!.GetValue(profile)!)
                continue;
            // Keep this coexistence test on one supported room style/size. Cave/zero coverage remains separate.
            type.GetProperty("Styles")!.SetValue(profile, new[] { 324 });
            type.GetProperty("MinFloorSize")!.SetValue(profile, 12);
            type.GetProperty("MaxFloorSize")!.SetValue(profile, 12);
        }
    }

    static byte[][] ChestBodies(QuestDungeonPlayfield world)
        => world.GetRequiredService<DynelRegistry>().Dynels().OfType<Chest>().OrderBy(chest => chest.Identity.Instance)
            .Select(chest => MissionChestProtocolTests.Serialize(chest.BuildSpawnMessage())).ToArray();

    static void AssertObjectiveBinding(QuestDungeonPlayfield world, QuestDungeonParameters parameters)
    {
        var registry = world.GetRequiredService<DynelRegistry>();
        if (parameters.ObjectiveItem is { } item)
        {
            Assert.IsTrue(registry.TryGet(item.Identity, out var entity));
            Assert.IsInstanceOfType<MissionObjectiveItem>(entity);
            Assert.IsTrue(item.SameAs(((MissionObjectiveItem)entity).Binding));
            Assert.AreEqual(item.X, entity.Position.xf);
            Assert.AreEqual(item.Y, entity.Position.yf);
            Assert.AreEqual(item.Z, entity.Position.zf);
        }
        if (parameters.ObjectiveNpc is { } npc)
        {
            Assert.IsTrue(registry.TryGet(npc.Identity, out var entity));
            Assert.IsInstanceOfType<NpcCharacter>(entity);
            Assert.AreEqual(npc.NpcHash, ((NpcCharacter)entity).SpawnHash);
            AoVector3 expected = world.SnapNpcSpawn(new AoVector3(npc.X, npc.Y, npc.Z));
            Assert.AreEqual(expected.xf, entity.Position.xf);
            Assert.AreEqual(expected.yf, entity.Position.yf);
            Assert.AreEqual(expected.zf, entity.Position.zf);
        }
    }

    sealed class ObjectiveRun : IDisposable
    {
        internal readonly AuthoredMissionTestDao Dao = new();
        internal readonly Fixture Fixture;
        internal readonly QuestDungeonPlayfield World;
        internal readonly Playfield Exterior;
        internal readonly MissionTerminal Terminal;
        readonly ServiceProvider _exteriorServices;
        readonly CharacterSnapshotService? _snapshots;
        internal PlayerQuest Quest => Fixture.Quests.GetLog(Fixture.Player).Quests[Fixture.Store.Generated!.QuestId];

        internal ObjectiveRun(int missionType, bool fromRoll = false)
        {
            var ids = new StubInstanceIdAllocator();
            IItemTemplateCatalog templates = MissionChestPlacementTests.Templates.Value;
            (GeneratedMissionOffer Offer, MissionTerminal Terminal)? roll = fromRoll ? RollOffer(missionType, templates) : null;
            if (fromRoll)
            {
                var store = new ObjectiveSnapshotStore(Dao);
                _snapshots = new CharacterSnapshotService(store, store, new StubLogger());
            }
            Fixture = new Fixture(Dao, new ItemBuilder(templates, ids, new StubLogger()),
                snapshots: _snapshots, templates: templates, inventory: new ObjectiveInventory(Dao), ids: ids, playerId: RolledOwner,
                enableMissionPopulationPersistence: true, rolledOffer: roll?.Offer);
            Assert.IsTrue(Fixture.Layouts.ChestPolicy?.RoomDensity > 0);
            ConfigureRepresentativeSoloProfile(Fixture.Layouts);
            Exterior = Fixture.Player.Playfield!;
            if (roll is { } fromCatalog)
            {
                Terminal = fromCatalog.Terminal;
                Terminal.Playfield = Exterior;
                Fixture.Player.Stats.Set(CharacterStat.Level, RollLevel);
                Assert.IsTrue(Fixture.GameData.TryGetXpLevel(RollLevel, out var level));
                Fixture.Player.Stats.Set(CharacterStat.XP, level.FloorXp);
                Fixture.Player.Stats.Set(CharacterStat.Side, (int)Side.Omni);
                Fixture.Player.Stats.Set(CharacterStat.Cash, 100_000);
            }
            else
            {
                Fixture.Offer.MissionType = missionType;
                Fixture.Offer.Quality = 125;
                Fixture.Offer.IssuingTerminalType = MissionTerminal.LiveIdentityType;
                Fixture.Offer.IssuingTerminalInstance = 123456;
                Fixture.Offer.IssuingTerminalPlayfield = Fixture.Entrance.PlayfieldId;
                Fixture.Player.Stats.Set(CharacterStat.Level, 125);
                Terminal = new MissionTerminal(new Identity { Type = (IdentityType)MissionTerminal.LiveIdentityType, Instance = 123456 },
                    new ItemTemplate { Id = 1, Quality = 1 })
                {
                    Playfield = Exterior, Position = new AoVector3(0, 0, 0)
                };
            }
            var registry = new DynelRegistry();
            registry.Register(Terminal);
            int origin = Fixture.Offer.IssuingTerminalPlayfield;
            var locality = new PlayfieldLocality(origin, Fixture.GameData.GetPlayfieldMetaData(origin));
            _exteriorServices = new ServiceCollection().AddSingleton(registry).AddSingleton(locality)
                .AddSingleton<IGameData>(Fixture.GameData).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Exterior, _exteriorServices);
            if (fromRoll)
            {
                Fixture.SetExteriorIdentity(Fixture.Offer.IssuingTerminalPlayfield);
                Fixture.Player.Position = Terminal.Position;
                Fixture.Persistence.BeforeMutation = batch =>
                {
                    var retired = batch.Locations.Single();
                    Assert.AreEqual(Fixture.Store.Keys.Single().InstanceId, retired.InstanceId);
                    Assert.AreEqual((int)IdentityType.None, retired.ContainerType);
                    Assert.AreEqual(Fixture.Player.Identity.Instance, retired.ContainerInstance);
                    var item = Dao.Items[retired.InstanceId];
                    item.ContainerType = retired.ContainerType;
                    item.ContainerInstance = retired.ContainerInstance;
                    item.ContainerPlacement = retired.ContainerPlacement;
                };
            }
            Assert.IsNull(Fixture.Accept());
            var row = Fixture.Store.Character!;
            var generated = Fixture.Store.Generated!;
            Dao.CharacterQuest = new()
            {
                CharacterId = row.CharacterId, QuestId = row.QuestId, Source = row.Source, State = row.State,
                Progress = row.Progress, RequiredCount = row.RequiredCount, AssignedAtUtcTicks = row.AssignedAtUtcTicks,
                UpdatedAtUtcTicks = row.UpdatedAtUtcTicks, OwnerType = generated.OwnerType, OwnerId = generated.OwnerId,
                ExpiresAtUtcTicks = generated.ExpiresAtUtcTicks, DefinitionJson = generated.DefinitionJson,
                AcgBuildingGeneratorJson = generated.AcgBuildingGeneratorJson
            };
            foreach (var held in Fixture.Player.Inventory.Inventory.Content)
            {
                ItemInstanceRecord record = held.Value.ToRecord(Fixture.Player.Inventory.Inventory.Identity, held.Key, held.Value.StackCount);
                Dao.Items.Add(record.InstanceId, new MissionItemInstanceData
                {
                    InstanceId = record.InstanceId, ContainerType = record.ContainerType, ContainerInstance = record.ContainerInstance,
                    ContainerPlacement = record.ContainerPlacement, ItemType = record.ItemType, LowId = record.LowId, HighId = record.HighId,
                    Quality = record.Quality, StackCount = record.StackCount, Source = (byte)record.Source
                });
            }
            if (fromRoll)
            {
                Fixture.SetExteriorIdentity(Fixture.Entrance.PlayfieldId);
                Fixture.Player.Position = new AoVector3(Fixture.Entrance.LocalX, Fixture.Entrance.LocalY, Fixture.Entrance.LocalZ);
            }
            Assert.IsTrue(Fixture.Service.TryEnter(Fixture.Player, Fixture.Target));
            World = (QuestDungeonPlayfield)Fixture.Session.Transfers.Single().World;
            Fixture.Player.Playfield = World;
        }

        static (GeneratedMissionOffer Offer, MissionTerminal Terminal) RollOffer(int missionType, IItemTemplateCatalog templates)
        {
            var data = MissionChestPlacementTests.Data.Value;
            var destinations = MissionDestinationCatalog.Load(data.RootPath);
            var placed = data.GetPlayfieldGeometry(RollOrigin).Dynels!.Dynels.First(dynel => dynel.IdentityType == MissionTerminal.LiveIdentityType);
            var owner = new Identity { Type = IdentityType.CanbeAffected, Instance = RolledOwner };
            var terminalIdentity = new Identity { Type = (IdentityType)placed.IdentityType, Instance = placed.IdentityInstance };
            var request = new QuestAlternativeMessage { Identity = owner, MissionTerminalIdentity = terminalIdentity, VersionId = 4, LevelSlider = 1 };
            DateTime now = DateTime.UtcNow;
            for (int seed = 0; seed < 64; seed++)
            {
                int offerId = 100 + seed * MissionOfferCatalog.OfferCount;
                var response = GeneratedMissionRollService.Shared.Roll(request, owner, RollLevel, RollOrigin, 1_201_445_827,
                    seed, seed, () => offerId++, destinations, out var entrances);
                Assert.AreEqual(MissionOfferCatalog.OfferCount, response.QuestInfos.Length);
                var batch = GeneratedMissionRollProjection.Create(request, response, RollOrigin, MissionRollPolicy.Current.Fee(RollLevel),
                    seed, seed, now, entrances);
                var selected = batch.Offers.FirstOrDefault(offer => offer.MissionType == missionType
                    && templates.TryGet(offer.RewardLowId, out var item) && (item.Flags & (int)ItemFlags.Null) == 0);
                if (selected == null)
                    continue;

                // Match the DAO's ReadOffers join, which projects the issuing terminal from the batch.
                selected.IssuingTerminalType = batch.TerminalType;
                selected.IssuingTerminalInstance = batch.TerminalInstance;
                selected.IssuingTerminalPlayfield = batch.TerminalPlayfield;
                var terminal = new MissionTerminal(terminalIdentity, templates.Require(placed.TemplateId))
                {
                    Position = new AoVector3(placed.Position.X, placed.Position.Y, placed.Position.Z)
                };
                return (selected, terminal);
            }
            throw new AssertFailedException($"No supported offer with a material reward was produced for mission type {missionType} in 64 rolls.");
        }

        internal Player OtherPlayer()
        {
            var player = TestWorld.CreatePlayer(112);
            player.Playfield = World;
            player.QuestLog = new QuestLog();
            player.QuestLog.Quests.Add(Quest.QuestId, Quest);
            var session = new QuestDungeonDestinationTests.Session();
            session.BindPlayer(player);
            player.Session = session;
            player.Stats.Set(CharacterStat.Health, 100);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            return player;
        }

        public void Dispose()
        {
            Fixture.Dispose();
            _snapshots?.Dispose();
            _exteriorServices.Dispose();
        }
    }

    sealed class ObjectiveSnapshotStore(AuthoredMissionTestDao dao) : ICharacterRepository, IStatRepository
    {
        public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats)
        {
            Assert.AreEqual(RolledOwner, characterId);
            foreach (var stat in stats)
            {
                dao.Stats[stat.StatId] = stat.StatValue;
            }
            return true;
        }

        public CharacterRecord? GetById(int characterId) => throw new NotSupportedException();
        public void SetOnline(int characterId) => throw new NotSupportedException();
        public void SetOffline(int characterId) => throw new NotSupportedException();
        public void SaveLocation(CharacterRecord character, int online) => throw new NotSupportedException();
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();
        public IReadOnlyList<StatRecord> GetForCharacter(int characterId) => throw new NotSupportedException();
        public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();
    }

    sealed class ObjectiveInventory(AuthoredMissionTestDao dao) : IInventoryRepository
    {
        public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int id) => dao.Items.Values
            .Where(row => row.ContainerType == (int)IdentityType.Container && row.ContainerInstance == id)
            .Select(row => new ItemInstanceRecord
            {
                InstanceId = row.InstanceId, ContainerType = row.ContainerType, ContainerInstance = row.ContainerInstance,
                ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
                Quality = row.Quality, StackCount = row.StackCount, Source = (ItemSource)row.Source
            }).ToArray();
        public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int id) => [];
        public IReadOnlyList<ItemInstanceRecord> GetBankItems(int id) => [];
        public int LeaseInstanceIdBlock(int count) => throw new NotSupportedException();
        public ItemInstanceRecord Insert(ItemInstanceRecord item) => throw new NotSupportedException();
        public void UpdateLocation(int id, int type, int owner, int slot) => throw new NotSupportedException();
        public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations) => throw new NotSupportedException();
        public void PersistNewAndUpdateLocations(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates)
            => throw new NotSupportedException();
    }
}

internal sealed partial class AuthoredMissionTestDao : ICharacterQuestCompletionTransaction, ICharacterQuestObjectiveCollectionTransaction
{
    public CharacterQuestCompletionSnapshotData ReadCharacterQuest(string questId)
        => CharacterQuest?.QuestId == questId ? CloneCharacterQuest(CharacterQuest)!
            : throw new InvalidOperationException("No exact generated quest was seeded for this test.");

    public CharacterQuestCompletionResult TryEarnCharacterQuest(CharacterQuestEarnRequest request)
    {
        var row = RequireObjectiveRequest(request);
        if (row.Progress >= row.RequiredCount)
            return ObjectiveResult(CharacterQuestCompletionStatus.AlreadyEarned);
        ApplyInventoryMutationCore(request.Grants, request.Removals);
        row.Progress = row.RequiredCount;
        row.UpdatedAtUtcTicks = request.NowUtcTicks;
        var key = new MissionRewardKeyData(new MissionKeyData(CharacterId, row.QuestId),
            "completion:" + row.AssignedAtUtcTicks.ToString(CultureInfo.InvariantCulture));
        Rewards.Add(key, new MissionRewardStageData
        {
            CharacterId = CharacterId, QuestId = row.QuestId, RewardKey = key.RewardKey,
            RewardType = "CharacterQuestCompletion", Status = MissionRewardStatus.Pending, Version = 1
        });
        return ObjectiveResult(CharacterQuestCompletionStatus.Earned);
    }

    public CharacterQuestCompletionResult TryCollectCharacterQuestItem(CharacterQuestEarnRequest request)
    {
        var row = RequireObjectiveRequest(request);
        if (row.RequiredCount != 2 || request.Grants.Count != 1 || request.Removals.Count != 0)
            throw new InvalidOperationException("Collection requires the exact return-item plan.");
        if (row.Progress == 1)
            return ObjectiveResult(CharacterQuestCompletionStatus.AlreadyCollected);
        if (row.Progress != 0)
            throw new InvalidOperationException("Collection progress is not current.");
        ApplyInventoryMutationCore(request.Grants, request.Removals);
        row.Progress = 1;
        row.UpdatedAtUtcTicks = request.NowUtcTicks;
        return ObjectiveResult(CharacterQuestCompletionStatus.Collected);
    }

    CharacterQuestCompletionSnapshotData RequireObjectiveRequest(CharacterQuestEarnRequest request)
    {
        var row = CharacterQuest ?? throw new InvalidOperationException("No generated quest was seeded.");
        if (row.CharacterId != CharacterId || row.OwnerId != CharacterId || row.OwnerType != (int)QuestOwnerType.Character
            || row.Source != (int)QuestSource.Generated || row.State != (int)QuestState.Active
            || row.QuestId != request.QuestId || row.AssignedAtUtcTicks != request.AssignedAtUtcTicks
            || row.DefinitionJson != request.ExpectedDefinitionJson || row.AcgBuildingGeneratorJson != request.ExpectedAcgBuildingGeneratorJson
            || row.ExpiresAtUtcTicks <= request.NowUtcTicks)
            throw new InvalidOperationException("Objective transaction did not retain the accepted owner/run/definition.");
        return row;
    }

    CharacterQuestCompletionResult ObjectiveResult(CharacterQuestCompletionStatus status)
        => new() { Status = status, Quest = CloneCharacterQuest(CharacterQuest)! };

    public CharacterQuestCompletionResult TryCompleteCharacterQuest(CharacterQuestCompletionRequest request)
    {
        var row = ReadCharacterQuest(request.QuestId);
        Assert.AreEqual(row.AssignedAtUtcTicks, request.AssignedAtUtcTicks);
        Assert.AreEqual(row.DefinitionJson, request.ExpectedDefinitionJson);
        Assert.AreEqual(row.AcgBuildingGeneratorJson, request.ExpectedAcgBuildingGeneratorJson);
        var stage = Rewards.Values.Single(reward => reward.QuestId == row.QuestId);
        if (stage.Status == MissionRewardStatus.Applied)
            return ObjectiveResult(CharacterQuestCompletionStatus.AlreadyApplied);

        Assert.AreEqual(MissionRewardStatus.Pending, stage.Status);
        Assert.AreEqual(row.RequiredCount, row.Progress);
        foreach (var expected in request.ExpectedStatValues)
        {
            Assert.AreEqual(expected.Value, Stats.GetValueOrDefault(expected.StatId));
        }
        ApplyInventoryMutationCore(request.Grants, []);
        foreach (var mutation in request.StatMutations)
        {
            Assert.AreEqual(MissionStatMutationKind.Set, mutation.Kind);
            Stats[mutation.StatId] = mutation.Value;
        }
        CharacterQuest!.State = (int)QuestState.Completed;
        CharacterQuest.UpdatedAtUtcTicks = request.NowUtcTicks;
        stage.Status = MissionRewardStatus.Applied;
        stage.AppliedAtUtcTicks = request.NowUtcTicks;
        stage.Version++;
        return new CharacterQuestCompletionResult
        {
            Status = CharacterQuestCompletionStatus.Applied,
            Quest = CloneCharacterQuest(CharacterQuest)!,
            StatValues = request.StatMutations.Select(mutation => new MissionStatValueData
            {
                StatIdentityType = mutation.StatIdentityType, StatId = mutation.StatId, Value = mutation.Value
            }).ToArray()
        };
    }
}
