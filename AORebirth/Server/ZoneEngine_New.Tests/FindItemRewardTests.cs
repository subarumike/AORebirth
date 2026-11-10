namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using AORebirth.Database.Domain.Quests;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;
using Fixture = QuestDungeonDestinationTests.Fixture;

/// <summary>
/// Runtime publication and recovery checks with the real acceptance, journal, dungeon and pickup paths.
/// The completion-capability fake controls durable outcomes; SQL atomicity is covered separately by
/// MissionDaoValidation against disposable MySQL, not claimed from this in-memory fixture.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class FindItemRewardTests
{
    const int RewardTemplate = 987654;

    [TestMethod]
    public void AcceptedObjectiveJournalPickupAndIndependentRewardKeepExactIdentity()
    {
        using var run = new Run();
        var f = run.Fixture;
        var binding = f.Parameters().ObjectiveItem!;
        var journal = f.Session.Sent.OfType<QuestFullUpdateMessage>().Single().Quests.Single();
        Assert.AreEqual(15, journal.QuestActions.Single().Version);
        Assert.AreEqual(binding.Identity, journal.QuestActions.Single().Action);
        Assert.AreNotEqual(RewardTemplate, binding.LowId, "The terminal reward must not become the objective item.");
        Assert.IsFalse(f.Quests.TryComplete(f.Player, run.Quest.QuestId, out _), "NPC-style completion cannot bypass collection.");
        run.Dao.BeforeEarn = () =>
        {
            Assert.IsTrue(Monitor.IsEntered(f.Player.PersistenceGate));
            Assert.IsFalse(f.Player.Inventory.Inventory.Content.Values.Any(i => i.Identity == binding.Identity));
        };
        Assert.IsTrue(run.Objective.TryPickup(f.Player));
        Assert.AreEqual(binding.Identity, f.Player.Inventory.Inventory.Content.Values.Single(i => i.InstanceId == binding.Instance).Identity);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(0, run.Dao.Completed);
        Assert.IsTrue(f.Session.Sent.OfType<ContainerAddItemMessage>().Any(p => p.SourceContainer == binding.Identity && p.Target == Identity.None));
        Assert.IsFalse(run.Objective.TryPickup(f.Player));
        f.Quests.RetryPendingRewards(f.Player, force: true);
        Assert.AreEqual(QuestState.Completed, run.Quest.State);
        Assert.AreEqual(137, f.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(1, run.Dao.Completed);
        Assert.AreEqual(1, run.Dao.Grants.Count(i => i.InstanceId == binding.Instance));
        Assert.AreEqual(1, run.Dao.Grants.Count(i => i.LowId == RewardTemplate));
        Assert.AreEqual(0, f.Store.Links.Count, "Completion must remove mission key bindings through the existing lifecycle.");
        // This fixture does not run the world projection queue; use the existing world-entry cleanup.
        f.Service.RestoreKeys(f.Player);
        Assert.IsFalse(f.Player.Inventory.EnumerateHeldItems().Any(i => i.LowId == f.Keys.KeyTemplateId));
        f.Quests.RetryPendingRewards(f.Player, force: true);
        Assert.AreEqual(1, run.Dao.Completed);
        f.AssertNoErrors();
    }

    [TestMethod]
    public void FullInventoryBeforeCollectionLeavesObjectiveAndQuestUnchanged()
    {
        using var run = new Run();
        run.FillInventory(0);
        Assert.IsFalse(run.Objective.TryPickup(run.Fixture.Player));
        Assert.AreEqual(0, run.Dao.Grants.Count);
        Assert.AreEqual(0, run.Quest.Progress);
        Assert.IsTrue(run.World.GetRequiredService<DynelRegistry>().TryGet(run.Objective.Identity, out _));
        Assert.AreEqual(100, run.Fixture.Player.Stats.GetOrZero(CharacterStat.Cash));
    }

    [TestMethod]
    public void FullInventoryAfterPickupKeepsEarnedRewardAcrossExpiryAbandonAndReload()
    {
        using var run = new Run();
        run.FillInventory(1);
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        // A reward that does not fit in the inventory goes to overflow; with that full too it stays pending.
        run.FillOverflow();
        var f = run.Fixture;
        f.Quests.RetryPendingRewards(f.Player, force: true);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.AreEqual(0, run.Dao.Completed);
        Assert.AreEqual(100, f.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsTrue(f.Abandon(QuestJournal.JournalInstance(run.Quest.QuestId)));
        Assert.IsTrue(run.Quest.IsRewardPending);
        run.Now = run.Quest.ExpiresAtUtc!.Value.AddDays(1);
        f.Player.QuestLog = null;
        f.RestartService();
        f.Quests.Restore(f.Player);
        Assert.IsTrue(run.Quest.IsRewardPending, "An earned reward must survive reloading after the mission deadline.");
        run.RemoveFiller();
        f.Quests.Restore(f.Player);
        Assert.AreEqual(QuestState.Completed, run.Quest.State);
        Assert.AreEqual(137, f.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(1, run.Dao.Completed);
        f.AssertNoErrors();
    }

    [TestMethod]
    public void FullInventoryAfterPickupDeliversTheRewardToOverflow()
    {
        using var run = new Run();
        run.FillInventory(1);
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        var f = run.Fixture;
        f.Quests.RetryPendingRewards(f.Player, force: true);
        Assert.AreEqual(QuestState.Completed, run.Quest.State);
        Assert.AreEqual(1, run.Dao.Completed);
        Assert.AreEqual(137, f.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsTrue(f.Player.Inventory.Overflow.Content.Values.Any(item => item.LowId == RewardTemplate && item.IsPersisted));
        f.AssertNoErrors();
    }

    [TestMethod]
    public void MissingRewardTemplateKeepsPendingAndCanRecoverWithoutSecondCollection()
    {
        using var run = new Run();
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        run.Items.MissingReward = true;
        run.Fixture.Quests.RetryPendingRewards(run.Fixture.Player, force: true);
        Assert.IsTrue(run.Quest.IsRewardPending);
        Assert.AreEqual(0, run.Dao.Completed);
        Assert.AreEqual(100, run.Fixture.Player.Stats.GetOrZero(CharacterStat.Cash));
        run.Items.MissingReward = false;
        run.Fixture.Player.QuestLog = null;
        run.Fixture.Quests.Restore(run.Fixture.Player);
        Assert.AreEqual(QuestState.Completed, run.Quest.State);
        Assert.AreEqual(1, run.Dao.Completed);
        Assert.AreEqual(2, run.Dao.Grants.Count);
    }

    [TestMethod]
    public void DeadWrongOwnerAndUnrelatedItemCannotEarnAcceptedObjective()
    {
        using var run = new Run();
        var f = run.Fixture;
        typeof(Character).GetField("_deathNotified", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Player, true);
        Assert.IsFalse(run.Objective.TryPickup(f.Player));
        typeof(Character).GetField("_deathNotified", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Player, false);
        var intruder = TestWorld.CreatePlayer(78);
        var session = new QuestDungeonDestinationTests.Session();
        session.BindPlayer(intruder); intruder.Session = session; intruder.Playfield = run.World;
        intruder.Position = run.Objective.Position; intruder.Stats.Set(CharacterStat.Health, 100);
        Assert.IsFalse(run.Objective.TryPickup(intruder));
        var binding = f.Parameters().ObjectiveItem!;
        var unrelated = new MissionObjectiveItem(new MissionItemObjectiveBinding
        {
            QuestId = binding.QuestId, OwnerId = binding.OwnerId, IdentityType = binding.IdentityType,
            Instance = binding.Instance + 100, LowId = binding.LowId, HighId = binding.HighId,
            Quality = binding.Quality, RoomIndex = binding.RoomIndex, X = binding.X, Y = binding.Y, Z = binding.Z,
            QuestInstanceStat = binding.QuestInstanceStat
        }, f.Items.CreateTemplate(binding.LowId, binding.HighId, binding.Quality), f.Quests.TryCollectFindItem)
        { Playfield = run.World, Position = run.Objective.Position };
        Assert.IsTrue(run.World.GetRequiredService<DynelRegistry>().TryRegister(unrelated));
        Assert.IsFalse(unrelated.TryPickup(f.Player));
        Assert.AreEqual(0, run.Dao.Grants.Count);
        Assert.AreEqual(0, run.Quest.Progress);
    }

    [TestMethod]
    public void ExpiredUnearnedObjectiveCannotBeCollectedOrPaid()
    {
        using var run = new Run();
        run.Now = run.Quest.ExpiresAtUtc!.Value.AddSeconds(1);
        Assert.IsFalse(run.Objective.TryPickup(run.Fixture.Player));
        Assert.AreEqual(QuestState.Expired, run.Quest.State);
        Assert.AreEqual(0, run.Dao.Grants.Count);
        Assert.AreEqual(100, run.Fixture.Player.Stats.GetOrZero(CharacterStat.Cash));
    }

    [TestMethod]
    public void MissingObjectiveTemplateRejectsAcceptanceWithoutClaimingOfferOrGrantingKey()
    {
        var items = new SelectiveItemBuilder();
        using var f = new Fixture(items: items);
        f.Offer.MissionType = 2;
        items.MissingObjective = true;
        Assert.IsNotNull(f.Accept());
        Assert.AreEqual(GeneratedMissionState.Offered, f.Offer.State);
        Assert.AreEqual(0, f.Store.AcceptCalls);
        Assert.IsNull(f.Store.Generated);
        Assert.IsNull(f.Store.Character);
        Assert.AreEqual(0, f.Store.Keys.Count);
        Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, f.Session.Sent.OfType<QuestFullUpdateMessage>().Count());
    }

    [TestMethod]
    public void AbandonedUnearnedMissionRejectsItsFormerObjective()
    {
        using var run = new Run();
        Assert.IsTrue(run.Fixture.Abandon(QuestJournal.JournalInstance(run.Quest.QuestId)));
        Assert.AreEqual(QuestState.Abandoned, run.Quest.State);
        Assert.IsFalse(run.Objective.TryPickup(run.Fixture.Player));
        Assert.AreEqual(0, run.Dao.Grants.Count);
        Assert.AreEqual(0, run.Dao.Completed);
    }

    [TestMethod]
    public void ReconstructedDungeonDoesNotRespawnDurablyCollectedObjective()
    {
        using var run = new Run();
        run.FillInventory(1);
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        run.FillOverflow();
        var f = run.Fixture;
        int playfield = f.Parameters().DungeonPlayfield;
        string acceptedLayout = f.Store.Generated!.AcgBuildingGeneratorJson;
        f.Manager.UnregisterPlayer(f.Player);
        Assert.IsTrue(f.Manager.TryReleaseQuestDungeon(playfield, run.Quest.QuestId));
        f.RestartService();
        var restored = f.Service.ResolveLogin(playfield);
        Assert.IsInstanceOfType<QuestDungeonPlayfield>(restored.Playfield);
        Assert.AreNotSame(run.World, restored.Playfield);
        Assert.IsFalse(restored.Playfield.GetRequiredService<DynelRegistry>().Dynels().OfType<MissionObjectiveItem>().Any());
        Assert.AreEqual(acceptedLayout, f.Store.Generated.AcgBuildingGeneratorJson);
        Assert.AreEqual(1, run.Dao.Grants.Count);
        Assert.IsTrue(run.Quest.IsRewardPending);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UncertainCompletionQuarantinesActorWithoutPublishingReward(bool committed)
    {
        using var run = new Run();
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        run.Dao.UnknownCompletionCommit = committed;
        run.Fixture.Quests.RetryPendingRewards(run.Fixture.Player, force: true);
        Assert.IsTrue(run.Fixture.Player.IsPersistenceQuarantined);
        Assert.AreEqual(SessionState.Closed, run.Fixture.Session.State);
        Assert.AreEqual(100, run.Fixture.Player.Stats.GetOrZero(CharacterStat.Cash), "An uncertain outcome must not publish speculative cash.");
        Assert.IsFalse(run.Fixture.Player.Inventory.Inventory.Content.Values.Any(i => i.LowId == RewardTemplate));
        Assert.AreEqual(committed ? 1 : 0, run.Dao.Completed);
        run.Fixture.Quests.RetryPendingRewards(run.Fixture.Player, force: true);
        Assert.AreEqual(committed ? 1 : 0, run.Dao.Completed);
        if (committed)
        {
            // A new actor/session loads durable state rather than reusing the quarantined aggregate.
            var fresh = TestWorld.CreatePlayer(run.Fixture.Player.Identity.Instance);
            var session = new QuestDungeonDestinationTests.Session();
            fresh.Session = session; session.BindPlayer(fresh);
            fresh.Playfield = run.Exterior;
            fresh.Position = new(run.Fixture.Entrance.LocalX, run.Fixture.Entrance.LocalY, run.Fixture.Entrance.LocalZ);
            fresh.Stats.Set(CharacterStat.Health, 100);
            Item key = run.Fixture.Player.Inventory.Inventory.Content.Values.Single(i => i.LowId == run.Fixture.Keys.KeyTemplateId);
            Assert.IsTrue(fresh.Inventory.Inventory.Add(fresh.Inventory.Inventory.FindFreeSlot(), key));
            Thread.Sleep(800); // The existing request throttle is 750 ms; exercise durable closure beyond it.
            Assert.IsFalse(run.Fixture.Service.TryEnter(fresh, run.Fixture.Target), "A cached active layout must not admit a durably completed quest.");
            Assert.AreEqual(0, session.Transfers.Count);
            Assert.IsTrue(run.Fixture.Store.Links.Count > 0, "Simulate the lost postcommit cleanup before reconnect.");
            run.Fixture.Quests.Restore(fresh);
            Assert.AreEqual(0, run.Fixture.Store.Links.Count);
            Assert.AreEqual(QuestState.Completed, fresh.QuestLog!.Quests[run.Quest.QuestId].State);
            Assert.AreEqual(1, run.Dao.Completed);
        }
    }

    [TestMethod]
    public void RewardPacketFailureAfterCommitQuarantinesAndKeepsDurableCompletion()
    {
        using var run = new Run();
        Assert.IsTrue(run.Objective.TryPickup(run.Fixture.Player));
        run.Fixture.Session.BeforeSend = message =>
        {
            if (message is AddTemplateMessage) throw new InvalidOperationException("Injected disconnected reward socket.");
        };
        run.Fixture.Quests.RetryPendingRewards(run.Fixture.Player, force: true);
        Assert.AreEqual(1, run.Dao.Completed);
        Assert.AreEqual((int)QuestState.Completed, run.Fixture.Store.Character!.State);
        Assert.AreEqual(0, run.Fixture.Store.Links.Count, "Durable key cleanup precedes fallible reward packets.");
        Assert.IsTrue(run.Fixture.Player.IsPersistenceQuarantined);
        Assert.AreEqual(SessionState.Closed, run.Fixture.Session.State);
    }

    [TestMethod]
    public void ObjectivePickupPacketFailureAfterCommitCannotGrantTheItemAgain()
    {
        using var run = new Run();
        run.Fixture.Session.BeforeSend = message =>
        {
            if (message is ContainerAddItemMessage) throw new InvalidOperationException("Injected disconnected pickup socket.");
        };
        Assert.IsFalse(run.Objective.TryPickup(run.Fixture.Player));
        Assert.IsTrue(run.Fixture.Player.IsPersistenceQuarantined);
        Assert.AreEqual(SessionState.Closed, run.Fixture.Session.State);
        Assert.AreEqual(1, run.Dao.Grants.Count);
        Assert.AreEqual(run.Fixture.Store.Character!.RequiredCount, run.Fixture.Store.Character.Progress);
        Assert.IsFalse(run.Objective.TryPickup(run.Fixture.Player));
        Assert.AreEqual(1, run.Dao.Grants.Count);
    }

    [TestMethod]
    public void CompletionCheckpointsLiveProgressAndFencesQueuedOlderStats()
    {
        using var run = new Run();
        var player = run.Fixture.Player;
        Assert.IsTrue(run.Objective.TryPickup(player));
        run.Dao.Stats[(int)CharacterStat.Cash] = 1;
        run.Dao.Stats[(int)CharacterStat.XP] = 2;
        int xp = player.Stats.GetOrZero(CharacterStat.XP) + 13;
        player.Stats.Set(CharacterStat.Cash, 212, StatDetail.Base, dirty: true);
        player.Stats.Set(CharacterStat.XP, xp, StatDetail.Base, dirty: true);
        long queued;
        lock (player.PersistenceGate)
        {
            queued = player.SaveState.NextSequence();
            run.Snapshots.CheckpointIfDue(player, Environment.TickCount64 + CharacterSnapshotService.CheckpointIntervalMilliseconds);
            run.Fixture.Quests.RetryPendingRewards(player, force: true);
            Assert.AreEqual(QuestState.Completed, run.Quest.State);
            Assert.IsFalse(player.SaveState.IsNewerThanCommitted(queued));
        }
        run.Snapshots.Dispose(); // joins the writer; the old queued snapshot must be discarded
        Assert.AreEqual(249, run.Dao.Stats[(int)CharacterStat.Cash]);
        Assert.AreEqual(xp + 11, run.Dao.Stats[(int)CharacterStat.XP]);
        Assert.AreEqual(249, player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.IsTrue(run.Dao.Checkpoints > 0);
    }

    [TestMethod]
    public void AuthoredFindItemTemplateStillCompletesThroughExistingNpcTurnIn()
    {
        var dao = new CompletionDao();
        using var snapshots = new CharacterSnapshotService(dao, dao, new StubLogger());
        using var f = new Fixture(dao, snapshots: snapshots);
        dao.Fixture = f;
        f.Player.Playfield = f.CreateWorld(25);
        f.Player.Stats.Set(CharacterStat.Level, 25);
        Assert.IsTrue(f.GameData.TryGetXpLevel(25, out var level));
        f.Player.Stats.Set(CharacterStat.XP, level.FloorXp);
        var templates = JsonSerializer.Deserialize<List<QuestTemplate>>(
            File.ReadAllText(Path.Combine(f.GameData.RootPath, QuestCatalog.FileName)), QuestTemplate.JsonOptions)!;
        var template = templates.FirstOrDefault(t => t.IsFindItem && string.IsNullOrEmpty(t.NextStep)
            && (t.Reward?.Records == null || t.Reward.Records.All(record => record.Count < 5)));
        Assert.IsNotNull(template, "Existing GameData must include an authored FindItem template for this regression.");
        Assert.IsTrue(f.Quests.TryAssignTemplate(f.Player, template.Hash, out _));
        Assert.IsNull(f.Store.Generated);
        Assert.IsTrue(f.Quests.TryComplete(f.Player, template.Hash, out _));
        Assert.AreEqual(QuestState.Completed, f.Quests.GetLog(f.Player).Quests[template.Hash].State);
        Assert.AreEqual(1, dao.Completed);
        Assert.AreEqual((int)QuestSource.Template, f.Store.Character!.Source);
        f.AssertNoErrors();
    }

    [TestMethod]
    [DataRow(0, false, true, false, true)]
    [DataRow(0, false, false, true, true)]
    [DataRow(1, true, false, false, false)]
    [DataRow(5, false, false, false, false)]
    [DataRow(5, true, false, false, true)]
    public void AuthoredChainCommitsEligibleSuccessorAndStartItemTogether(int existingState, bool olderRun,
        bool loseCommitAcknowledgment, bool fillInventory, bool assignSuccessor)
    {
        const string first = "T001", next = "T002";
        const int startItem = 987653;
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ao-quest-successor-");
        string catalogPath = Path.Combine(directory.FullName, QuestCatalog.FileName);
        try
        {
            File.WriteAllText(catalogPath, """
                [{"Hash":"T001","Name":"First","Action":"TalkTo","NextStep":"T002"},
                 {"Hash":"T002","Name":"Next","Action":"TalkTo","StartItems":[{"Id":987653,"Quality":1}]}]
                """);
            var data = new StubGameData(new HashItemCatalog([], []), rootPath: directory.FullName);
            var catalog = new QuestCatalog(data, new StubLogger());
            var dao = new CompletionDao();
            using var snapshots = new CharacterSnapshotService(dao, dao, new StubLogger());
            DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            using var f = new Fixture(dao, now: () => now, snapshots: snapshots, questCatalog: catalog);
            dao.Fixture = f;
            f.Player.Playfield = f.CreateWorld(25);
            Assert.IsTrue(f.Quests.TryAssignTemplate(f.Player, first, out _));
            long firstAssigned = now.Ticks;
            long existingAssigned = now.AddMinutes(olderRun ? -1 : 1).Ticks;
            if (existingState != 0)
            {
                f.Store.Save(new CharacterQuestRow
                {
                    CharacterId = f.Player.Identity.Instance, QuestId = next, Source = (int)QuestSource.Template,
                    State = existingState, RequiredCount = 1, AssignedAtUtcTicks = existingAssigned
                });
                f.Player.QuestLog = null;
            }
            now = now.AddMinutes(2);
            dao.UnknownCompletionCommit = loseCommitAcknowledgment ? true : null;

            if (fillInventory)
            {
                int instance = 700000;
                foreach (Container page in new[] { f.Player.Inventory.Inventory, f.Player.Inventory.Overflow })
                    for (int slot = page.FindFreeSlot(); slot >= 0; slot = page.FindFreeSlot())
                        Assert.IsTrue(page.Add(slot, TestWorld.CreateItem(765432, 765432, instanceId: ++instance, persisted: true)));
                Assert.IsFalse(f.Quests.TryComplete(f.Player, first, out _));
                Assert.IsTrue(f.Quests.GetLog(f.Player).Quests[first].IsRewardPending);
                Assert.AreEqual(0, dao.Completed);
                Assert.IsFalse(f.Store.AdditionalQuests.ContainsKey(next));
                Assert.AreEqual(0, dao.Grants.Count);
                Assert.IsNotNull(f.Player.Inventory.Overflow.Remove(f.Player.Inventory.Overflow.Offset));
            }

            Assert.AreEqual(!loseCommitAcknowledgment, f.Quests.TryComplete(f.Player, first, out _));
            Assert.AreEqual((int)QuestState.Completed, f.Store.Character!.State);
            Assert.AreEqual(firstAssigned, f.Store.Character.AssignedAtUtcTicks);
            Assert.IsTrue(f.Store.AdditionalQuests.TryGetValue(next, out CharacterQuestRow? successor),
                "The predecessor completion transaction must leave a durable successor row even if acknowledgment was lost.");
            Assert.AreEqual(assignSuccessor ? (int)QuestState.Active : existingState, successor.State);
            Assert.AreEqual(assignSuccessor ? now.Ticks : existingAssigned, successor.AssignedAtUtcTicks);
            Assert.AreEqual(assignSuccessor ? 1 : 0, dao.Grants.Count(item => item.LowId == startItem));

            if (loseCommitAcknowledgment)
            {
                Assert.IsTrue(f.Player.IsPersistenceQuarantined);
                Assert.IsFalse(f.Player.Inventory.EnumerateHeldItems().Any(item => item.LowId == startItem));
                var fresh = TestWorld.CreatePlayer(f.Player.Identity.Instance);
                var session = new QuestDungeonDestinationTests.Session();
                session.BindPlayer(fresh);
                fresh.Session = session;
                fresh.Playfield = f.Player.Playfield;
                f.Quests.Restore(fresh);
                Assert.AreEqual(QuestState.Active, fresh.QuestLog!.Quests[next].State);
            }
            else if (assignSuccessor)
            {
                Assert.IsTrue(f.Player.Inventory.EnumerateHeldItems().Single(item => item.LowId == startItem).IsPersisted);
                if (fillInventory)
                    Assert.AreEqual((int)IdentityType.OverflowWindow, dao.Grants.Single().ContainerType);
                Assert.IsTrue(f.Quests.TryAbandon(f.Player, QuestJournal.JournalInstance(next)));
                f.Player.QuestLog = null;
                f.Quests.Restore(f.Player);
                Assert.AreEqual(QuestState.Abandoned, f.Player.QuestLog!.Quests[next].State);
            }
            Assert.AreEqual(1, dao.Completed);
            Assert.AreEqual(assignSuccessor ? 1 : 0, dao.Grants.Count(item => item.LowId == startItem));
        }
        finally
        {
            File.Delete(catalogPath);
            directory.Delete();
        }
    }

    [TestMethod]
    [DataRow(2, false)]
    [DataRow(25, false)]
    [DataRow(25, true)]
    [DataRow(199, false)]
    public void PlannedQuestXpMatchesExistingQuestAwardSemantics(int level, bool crossesLevel)
    {
        using var f = new Fixture();
        var world = f.CreateWorld(25);
        f.Player.Playfield = world;
        Assert.IsTrue(f.GameData.TryGetXpLevel(level, out var data));
        int initialXp = crossesLevel ? checked(data.FloorXp + data.NextLevelXp - 1) : data.FloorXp;
        f.Player.Stats.Set(CharacterStat.Level, level);
        f.Player.Stats.Set(CharacterStat.XP, initialXp);
        f.Player.Stats.Set(CharacterStat.Breed, (int)Breed.Solitus);
        f.Player.Stats.Set(CharacterStat.Profession, (int)Profession.Soldier);
        var expected = TestWorld.CreatePlayer(78);
        expected.Playfield = world;
        foreach (var entry in f.Player.Stats.GetEntries()) expected.Stats.Set(entry.Stat, entry.Base);
        var expectedSession = new QuestDungeonDestinationTests.Session();
        expectedSession.BindPlayer(expected); expected.Session = expectedSession;
        if (crossesLevel)
        {
            foreach (var player in new[] { f.Player, expected })
            {
                player.Stats.Set(CharacterStat.MaxNCU, 60);
                var buff = TestNanos.Create(1020, modifiers:
                [
                    new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.MaxHealth, 50] },
                    new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.Strength, 8] },
                    new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.MaxNanoEnergy, 40] }
                ]);
                Assert.AreEqual(BuffApplyDecision.Apply, player.TryApplyBuff(buff, player.Identity, DateTime.UtcNow, out _, out _));
                player.AddTimedEffect(123, CharacterStat.Stamina, 12, DateTime.UtcNow.AddMinutes(5));
                typeof(Player).GetField("_perkTemplates", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, new ItemTemplate[]
                {
                    new()
                    {
                    Id = 700100,
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.BodyDevelopment, 7] },
                            new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.MaxNanoEnergy, 15] }
                        ]
                    }
                    }
                });
                player.RebaseStats();
            }
        }
        f.Session.Sent.Clear(); expectedSession.Sent.Clear();
        var before = f.Player.Stats.GetEntries().Select(e => (e.Stat, e.Base, e.Bonus)).ToArray();
        int reward = data.NextLevelXp;
        var plan = DirectXpRewardPlan.CreateQuestReward(f.Player, reward);
        Assert.AreEqual(initialXp, f.Player.Stats.GetOrZero(CharacterStat.XP), "Planning cannot mutate the actor.");
        CollectionAssert.AreEquivalent(before, f.Player.Stats.GetEntries().Select(e => (e.Stat, e.Base, e.Bonus)).ToArray());
        expected.AwardXp(reward, XpSource.Quest);
        foreach (var pair in plan.Stats)
            Assert.AreEqual(expected.Stats.GetOrZero(pair.Key, StatDetail.Base), pair.Value,
                "Durable projection must match the existing award before publication: " + pair.Key);
        plan.PublishAfterCommit(f.Player);
        foreach (var stat in new[] { CharacterStat.XP, CharacterStat.Level, CharacterStat.LastXP, CharacterStat.NextXP,
            CharacterStat.TitleLevel, CharacterStat.Health, CharacterStat.MaxHealth, CharacterStat.CurrentNano, CharacterStat.MaxNanoEnergy })
        {
            Assert.AreEqual(expected.Stats.GetOrZero(stat, StatDetail.Base), f.Player.Stats.GetOrZero(stat, StatDetail.Base), stat.ToString());
            Assert.AreEqual(expected.Stats.GetOrZero(stat, StatDetail.Bonus), f.Player.Stats.GetOrZero(stat, StatDetail.Bonus), stat + " bonus");
        }
        if (crossesLevel)
        {
            var expectedIp = expectedSession.Sent.OfType<StatMessage>().SelectMany(m => m.Stats).Where(s => s.Value1 == CharacterStat.IP).Select(s => s.Value2).ToArray();
            Assert.IsTrue(expectedIp.Length > 0, "The existing award must publish available IP on level-up.");
            CollectionAssert.AreEqual(expectedIp, f.Session.Sent.OfType<StatMessage>().SelectMany(m => m.Stats)
                .Where(s => s.Value1 == CharacterStat.IP).Select(s => s.Value2).ToArray());
        }
    }

    sealed class Run : IDisposable
    {
        internal readonly CompletionDao Dao = new();
        internal readonly SelectiveItemBuilder Items = new();
        internal readonly Fixture Fixture;
        internal readonly CharacterSnapshotService Snapshots;
        internal readonly QuestDungeonPlayfield World;
        internal readonly Playfield Exterior;
        internal readonly MissionObjectiveItem Objective;
        internal DateTime Now = DateTime.UtcNow;
        internal PlayerQuest Quest => Fixture.Quests.GetLog(Fixture.Player).Quests[Fixture.Store.Generated!.QuestId];

        internal Run()
        {
            Snapshots = new CharacterSnapshotService(Dao, Dao, new StubLogger());
            Fixture = new Fixture(Dao, Items, () => Now, Snapshots);
            Dao.Fixture = Fixture;
            Fixture.Persistence.BeforeMutation = _ => { };
            Fixture.Offer.MissionType = 2;
            Fixture.Offer.CashReward = 37;
            Fixture.Offer.ExperienceReward = 11;
            Fixture.Offer.RewardCount = 1;
            Fixture.Offer.RewardLowId = Fixture.Offer.RewardHighId = RewardTemplate;
            Fixture.Offer.RewardQuality = 1;
            Fixture.Player.Stats.Set(CharacterStat.Level, 25);
            Assert.IsTrue(Fixture.GameData.TryGetXpLevel(25, out var level));
            Fixture.Player.Stats.Set(CharacterStat.XP, level.FloorXp);
            Fixture.Player.Stats.Set(CharacterStat.Cash, 100);
            Assert.IsNull(Fixture.Accept());
            Exterior = Fixture.Player.Playfield!;
            Assert.IsTrue(Fixture.Service.TryEnter(Fixture.Player, Fixture.Target));
            World = (QuestDungeonPlayfield)Fixture.Session.Transfers.Single().World;
            Fixture.Player.Playfield = World;
            Fixture.Manager.RegisterPlayer(Fixture.Player);
            var binding = Fixture.Parameters().ObjectiveItem;
            Assert.IsNotNull(binding);
            Assert.IsTrue(World.GetRequiredService<DynelRegistry>().TryGet(binding.Identity, out var objective));
            Assert.IsNotNull(objective);
            Objective = (MissionObjectiveItem)objective;
            Fixture.Player.Position = Objective.Position;
        }

        internal void FillInventory(int leaveFree)
        {
            Container page = Fixture.Player.Inventory.Inventory;
            while (page.Capacity - page.Content.Count > leaveFree)
            {
                int slot = page.FindFreeSlot();
                Assert.IsTrue(page.Add(slot, TestWorld.CreateItem(lowId: 765432, highId: 765432,
                    instanceId: 700000 + slot, persisted: true)));
            }
        }
        internal void FillOverflow()
        {
            Container page = Fixture.Player.Inventory.Overflow;
            for (int slot = page.FindFreeSlot(); slot >= 0; slot = page.FindFreeSlot())
                Assert.IsTrue(page.Add(slot, TestWorld.CreateItem(lowId: 765433, highId: 765433,
                    instanceId: 800000 + slot, persisted: true)));
        }
        internal void RemoveFiller()
        {
            var page = Fixture.Player.Inventory.Inventory;
            int slot = page.Content.First(pair => pair.Value.LowId == 765432).Key;
            Assert.IsNotNull(page.Remove(slot));
        }
        public void Dispose() { Snapshots.Dispose(); Fixture.Dispose(); }
    }

    sealed class SelectiveItemBuilder : IItemBuilder
    {
        readonly StubItemBuilder _inner = new();
        internal bool MissingReward;
        internal bool MissingObjective;
        public Item Create(int lowId, int highId, int quality, ItemSource source, int stackCount = 1,
            int instanceId = 0, Identity? identity = null, byte[]? statsBlob = null)
            => _inner.Create(lowId, highId, quality, source, stackCount, instanceId, identity, statsBlob);
        public Item CreateWithNewInstance(int lowId, int highId, int quality, ItemSource source, int stackCount = 1)
            => MissingReward && lowId == RewardTemplate ? throw new InvalidOperationException("Injected missing reward template.")
                : _inner.CreateWithNewInstance(lowId, highId, quality, source, stackCount);
        public ItemTemplate CreateTemplate(int lowId, int highId, int quality) => MissingObjective
            ? throw new InvalidOperationException("Injected missing objective template.") : _inner.CreateTemplate(lowId, highId, quality);
        public bool TryFromInstanceRecord(ItemInstanceRecord row, out Item item) => _inner.TryFromInstanceRecord(row, out item);
    }

    sealed class CompletionDao : IMissionDao, IMissionDaoTransaction, ICharacterQuestCompletionTransaction,
        ICharacterRepository, IStatRepository
    {
        internal Fixture Fixture = null!;
        internal readonly List<MissionItemInstanceData> Grants = [];
        internal readonly Dictionary<int, int> Stats = [];
        internal int Completed;
        internal int Checkpoints;
        internal bool? UnknownCompletionCommit;
        internal Action? BeforeEarn;
        public int CharacterId => Fixture.Player.Identity.Instance;
        public string AccountKey => string.Empty;
        public T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation) => Execute(characterId, string.Empty, operation);
        public T Execute<T>(int characterId, string accountKey, Func<IMissionDaoTransaction, T> operation)
        {
            Assert.AreEqual(CharacterId, characterId);
            Assert.IsTrue(Monitor.IsEntered(Fixture.Player.PersistenceGate));
            return operation(this);
        }
        public CharacterQuestCompletionSnapshotData ReadCharacterQuest(string questId)
        {
            var row = Fixture.Store.Character?.QuestId == questId ? Fixture.Store.Character
                : Fixture.Store.AdditionalQuests.GetValueOrDefault(questId);
            if (row == null)
                return null!;
            var generated = row.Source == (int)QuestSource.Generated ? Fixture.Store.Generated : null;
            return new()
            {
                CharacterId = row.CharacterId, QuestId = row.QuestId, Source = row.Source, State = row.State,
                Progress = row.Progress, RequiredCount = row.RequiredCount, AssignedAtUtcTicks = row.AssignedAtUtcTicks,
                UpdatedAtUtcTicks = row.UpdatedAtUtcTicks, OwnerId = generated?.OwnerId ?? 0, OwnerType = generated?.OwnerType ?? 0,
                ExpiresAtUtcTicks = generated?.ExpiresAtUtcTicks ?? 0, DefinitionJson = generated?.DefinitionJson,
                AcgBuildingGeneratorJson = generated?.AcgBuildingGeneratorJson
            };
        }
        public CharacterQuestCompletionResult TryEarnCharacterQuest(CharacterQuestEarnRequest request)
        {
            var row = Fixture.Store.Character!;
            if (Completed > 0) return Result(CharacterQuestCompletionStatus.AlreadyApplied);
            if (row.State != (int)QuestState.Active || (row.Source == (int)QuestSource.Generated
                && row.Progress < row.RequiredCount && Fixture.Store.Generated!.ExpiresAtUtcTicks <= request.NowUtcTicks))
                return Result(CharacterQuestCompletionStatus.Rejected);
            if (row.Progress >= row.RequiredCount) return Result(CharacterQuestCompletionStatus.AlreadyEarned);
            BeforeEarn?.Invoke();
            Grants.AddRange(request.Grants);
            row.Progress = row.RequiredCount;
            return Result(CharacterQuestCompletionStatus.Earned);
        }
        public CharacterQuestCompletionResult TryCompleteCharacterQuest(CharacterQuestCompletionRequest request)
        {
            if (Completed > 0) return Result(CharacterQuestCompletionStatus.AlreadyApplied);
            if (UnknownCompletionCommit == false) throw new MissionCommitOutcomeUnknownException(new Exception("Injected before commit."));
            Assert.AreEqual(Fixture.Store.Character!.RequiredCount, Fixture.Store.Character.Progress);
            foreach (var expected in request.ExpectedStatValues)
                Assert.AreEqual(expected.Value, (long)Stats.GetValueOrDefault(expected.StatId), "Durable baseline must match the live reward plan.");
            foreach (var mutation in request.StatMutations)
            {
                Assert.AreEqual(MissionStatMutationKind.Set, mutation.Kind);
                Assert.IsTrue(mutation.Value >= int.MinValue && mutation.Value <= int.MaxValue);
                Assert.AreEqual(int.MinValue, mutation.MinimumValue);
                Assert.AreEqual(int.MaxValue, mutation.MaximumValue);
            }
            Grants.AddRange(request.Grants);
            foreach (var mutation in request.StatMutations) Stats[mutation.StatId] = checked((int)mutation.Value);
            Fixture.Store.Character.State = (int)QuestState.Completed;
            if (request.Successor is { } successor)
            {
                Assert.IsTrue(CharacterQuestSuccessorRequest.CanAssign(ReadCharacterQuest(successor.QuestId), request.AssignedAtUtcTicks));
                Fixture.Store.Save(new CharacterQuestRow
                {
                    CharacterId = CharacterId, QuestId = successor.QuestId, Source = (int)QuestSource.Template,
                    State = (int)QuestState.Active, RequiredCount = successor.RequiredCount,
                    AssignedAtUtcTicks = request.NowUtcTicks, UpdatedAtUtcTicks = request.NowUtcTicks
                });
            }
            Completed++;
            if (UnknownCompletionCommit == true) throw new MissionCommitOutcomeUnknownException(new Exception("Injected after commit."));
            return new() { Status = CharacterQuestCompletionStatus.Applied,
                Quest = ReadCharacterQuest(request.QuestId), StatValues = request.StatMutations.Select(m => new MissionStatValueData
                    { StatIdentityType = m.StatIdentityType, StatId = m.StatId, Value = m.Value }).ToArray() };
        }
        CharacterQuestCompletionResult Result(CharacterQuestCompletionStatus status) => new()
            { Status = status, Quest = ReadCharacterQuest(Fixture.Store.Character!.QuestId) };
        public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats)
        {
            Assert.AreEqual(CharacterId, characterId);
            Assert.IsTrue(Monitor.IsEntered(Fixture.Player.PersistenceGate));
            foreach (var stat in stats) Stats[stat.StatId] = stat.StatValue;
            Checkpoints++;
            return true;
        }
        public CharacterRecord? GetById(int characterId) => throw new NotSupportedException();
        public void SetOnline(int characterId) => throw new NotSupportedException();
        public void SetOffline(int characterId) => throw new NotSupportedException();
        public void SaveLocation(CharacterRecord character, int online) => throw new NotSupportedException();
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();
        public IReadOnlyList<StatRecord> GetForCharacter(int characterId) => throw new NotSupportedException();
        public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();
        public MissionStateData GetMission(MissionKeyData key) => throw new NotSupportedException();
        public IList<MissionStateData> GetMissions(int characterId) => throw new NotSupportedException();
        public MissionCharacterSnapshotData ReadCharacter(int characterId)
        {
            Assert.AreEqual(CharacterId, characterId);
            // This standalone reward fixture has no stored chest populations; its generation policy is disabled locally.
            return new MissionCharacterSnapshotData(characterId, [], [], [], []);
        }
        public string ResolveCharacterAccountKey(int characterId) => throw new NotSupportedException();
        public MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey) => throw new NotSupportedException();
        public IList<MissionAccountFlagData> GetAccountFlags(string accountKey) => throw new NotSupportedException();
        public MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request) => throw new NotSupportedException();
        public bool MarkStartAreaSelectionPending(int characterId) => throw new NotSupportedException();
        public string GetStartAreaSelectionState(int characterId) => throw new NotSupportedException();
        public bool TryCompleteStartAreaSelection(int characterId, string selectedState) => throw new NotSupportedException();
        public void SaveMission(MissionKeyData key, MissionStateData record) => throw new NotSupportedException();
        public MissionObjectiveProgressData GetObjective(MissionObjectiveKeyData key) => throw new NotSupportedException();
        public void SaveObjective(MissionObjectiveKeyData key, MissionObjectiveProgressData record) => throw new NotSupportedException();
        public bool TryAddObservation(MissionObjectiveObservationData observation) => throw new NotSupportedException();
        public MissionFlagData GetFlag(MissionKeyData key, string flagKey) => throw new NotSupportedException();
        public void SaveFlag(MissionKeyData key, MissionFlagData flag) => throw new NotSupportedException();
        public void SaveAccountFlag(string accountKey, MissionAccountFlagData flag) => throw new NotSupportedException();
        public MissionRewardStageData GetReward(MissionRewardKeyData key) => throw new NotSupportedException();
        public MissionRewardClaimResultData TryClaimReward(MissionRewardKeyData key, string rewardType, string token, long now, long expiry) => throw new NotSupportedException();
        public bool TryMarkRewardApplied(MissionRewardKeyData key, string token, long version, string effect, long now, out MissionRewardStageData stage) => throw new NotSupportedException();
        public bool TryMarkRewardFailed(MissionRewardKeyData key, string token, long version, string error, long now, out MissionRewardStageData stage) => throw new NotSupportedException();
        public MissionAtomicStatRewardResultData TryApplyCharacterStatReward(MissionRewardKeyData key, string type, IList<MissionStatMutationData> mutations, string effect, long now) => throw new NotSupportedException();
    }
}
