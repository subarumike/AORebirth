namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class GeneratedMissionServiceTests
{
    static Identity Quest => new() { Type = (IdentityType)0xDAC3, Instance = 12345 };

    [TestMethod]
    public void RewardPreservesUnsnapshottedBaseStatsAndPublishesOnlyAfterCommit()
    {
        using var w = new World();
        w.Player.Stats.Set(CharacterStat.Cash, 123);
        w.Player.Stats.Set((CharacterStat)52, 321);
        w.Dao.BeforeComplete = completion =>
        {
            Assert.IsTrue(Monitor.IsEntered(w.Player.PersistenceGate));
            Assert.AreEqual(123, completion.CurrentCash);
            Assert.AreEqual(321, completion.CurrentExperience);
            Assert.AreEqual(421, completion.FinalExperience);
            Assert.AreEqual(100, completion.RequestedExperienceReward);
            Assert.AreEqual(421L, completion.ProgressionStats.Single(stat => stat.StatId == 52).Value);
            Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(123, w.Player.Stats.GetOrZero(CharacterStat.Cash));
            var row = completion.Items.Single();
            Assert.AreEqual(w.Player.Identity.Instance, row.ContainerInstance);
            Assert.AreEqual(64, row.ContainerPlacement);
            Assert.AreEqual(901, row.LowId); Assert.AreEqual(902, row.HighId);
            Assert.AreEqual(25, row.Quality); Assert.AreEqual(1, row.StackCount);
        };
        var result = w.Service.Complete(w.Player, Quest);
        Assert.AreEqual(GeneratedMissionResultStatus.Applied, result.Status);
        Assert.AreEqual(173, w.Player.Stats.GetOrZero(CharacterStat.Cash));
        Assert.AreEqual(421, w.Player.Stats.GetOrZero((CharacterStat)52));
        Assert.IsTrue(w.Player.Inventory.Inventory.Content[64].IsPersisted);
        Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(GeneratedMissionResultStatus.AlreadyApplied, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(1, w.Dao.CompleteCalls);
        Assert.AreEqual(1, w.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    public void TokenGrantUsesSealedLevelSideAndCountInTheSameRewardTransaction()
    {
        using var w = new World();
        w.Dao.Binding.TokenDisposition = 2; w.Dao.Binding.TokenClaimSide = 1;
        w.Dao.Binding.TokenClaimLevel = 25; w.Dao.Binding.TokenCount = 2;
        w.Player.Stats.Set(CharacterStat.Level, 1);
        w.Player.Stats.Set((CharacterStat)33, 0);
        w.Dao.BeforeComplete = completion =>
        {
            Assert.IsTrue(Monitor.IsEntered(w.Player.PersistenceGate));
            Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
            Assert.IsNotNull(completion.TokenItem);
            Assert.AreEqual(103910, completion.TokenItem.LowId); Assert.AreEqual(103911, completion.TokenItem.HighId);
            Assert.AreEqual(1, completion.TokenItem.Quality); Assert.AreEqual(2, completion.TokenItem.StackCount);
            Assert.AreEqual(1, completion.Items.Count);
        };
        Assert.AreEqual(GeneratedMissionResultStatus.Applied, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(2, w.Player.Inventory.Inventory.Content.Count);
        Assert.IsTrue(w.Player.Inventory.Inventory.Content.Values.All(item => item.IsPersisted));
        Assert.AreEqual(GeneratedMissionResultStatus.AlreadyApplied, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(1, w.Dao.CompleteCalls);
    }

    [TestMethod]
    public void CompletionWithoutSealedTokenPolicyFailsClosedBeforeAnyRewardMutation()
    {
        using var w = new World();
        w.Dao.Binding.CompletionFrozenAtUtcTicks = 0;
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(0, w.Dao.CompleteCalls); Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    public void FullInventoryLeavesMissionAndRewardUntouched()
    {
        using var w = new World(); TestWorld.FillInventory(w.Player);
        int count = w.Player.Inventory.Inventory.Content.Count;
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(0, w.Dao.CompleteCalls);
        Assert.AreEqual(GeneratedMissionState.Active, w.Dao.Binding.State);
        Assert.AreEqual(count, w.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    public void KnownRollbackDoesNotPublishOrQuarantineAndAllowsExplicitRetry()
    {
        using var w = new World();
        w.Dao.Failure = new InvalidOperationException("injected transaction rollback");
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
        Assert.IsFalse(w.Player.IsPersistenceQuarantined);
        w.Dao.Failure = null;
        Assert.AreEqual(GeneratedMissionResultStatus.Applied, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(2, w.Dao.CompleteCalls);
    }

    [TestMethod]
    public void UnknownCommitQuarantinesWithoutPublishingOrRetrying()
    {
        using var w = new World();
        w.Dao.Failure = new MissionCommitOutcomeUnknownException(new Exception("lost commit reply"));
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.IsTrue(w.Player.IsPersistenceQuarantined);
        Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(1, w.Dao.CompleteCalls);
    }

    [TestMethod]
    public void ReplayUsesOriginalBindingAndNeverPublishesAnotherKey()
    {
        using var w = new World();
        var result = w.Service.Accept(w.Player, new()
        {
            OwnerId = w.Player.Identity.Instance, OfferType = 0xDAC3, OfferInstance = 400
        }, [TestWorld.CreateItem(instanceId: 200, persisted: false)]);
        Assert.AreEqual(GeneratedMissionResultStatus.AlreadyApplied, result.Status);
        Assert.AreSame(w.Dao.Binding, result.Binding);
        Assert.AreEqual(0, w.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, w.Dao.AcceptCalls);
    }

    [TestMethod]
    public void InactiveExpiredIncompleteAndUnownedMissionsCannotGrant()
    {
        using var w = new World();
        w.Dao.Binding.Progress = 0;
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        w.Dao.Binding.Progress = 1; w.Dao.Binding.ExpiresAtUtcTicks = 10;
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        w.Dao.Binding.ExpiresAtUtcTicks = 100; w.Dao.Binding.State = GeneratedMissionState.Abandoned;
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, Quest).Status);
        Assert.AreEqual(GeneratedMissionResultStatus.Rejected, w.Service.Complete(w.Player, new Identity { Type = Quest.Type, Instance = 999 }).Status);
        Assert.AreEqual(0, w.Dao.CompleteCalls);
        Assert.AreEqual(0, w.Ids.Calls);
    }

    sealed class World : IDisposable
    {
        public Player Player = TestWorld.CreatePlayer(77);
        public Dao Dao = new(); public Ids Ids = new();
        public InventoryFlushService Flush;
        public GeneratedMissionService Service;
        public World()
        {
            // Same minimal writer-lifetime fixture used by existing inventory tests; no world data.
            var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
            typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Dictionary<int, Player>());
            Flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => manager), new Coalesce(), new StubLogger());
            Service = new GeneratedMissionService(Dao, Flush, new StubItemBuilder(), Ids, new StubLogger(), () => 50);
        }
        public void Dispose() => Flush.Dispose();
    }
    sealed class Ids : IItemInstanceIdAllocator
    {
        public int Calls;
        public int Allocate() => 1000 + ++Calls;
    }
    sealed class Coalesce : ICharacterCoalesceCommit
    {
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int characterId, IReadOnlyList<int> nanos)
            => throw new AssertFailedException("A clean mission grant unexpectedly used an independent inventory commit.");
    }
    sealed class Dao : IGeneratedMissionDao
    {
        public int CompleteCalls, AcceptCalls;
        public Exception? Failure;
        public Action<GeneratedMissionCompletion>? BeforeComplete;
        public GeneratedMissionBinding Binding = new()
        {
            OwnerId = 77, QuestType = 0xDAC3, QuestInstance = 12345, OfferType = 0xDAC3, OfferInstance = 400,
            State = GeneratedMissionState.Active, ExpiresAtUtcTicks = 100, Progress = 1, RequiredCount = 1,
            CompletionFrozenAtUtcTicks = 1, TokenProgressPercent = 100, TokenClaimLevel = 1, TokenClaimSide = 0, TokenDisposition = 1, TokenCount = 0,
            Offer = new GeneratedMissionOffer { RewardLowId = 901, RewardHighId = 902, RewardQuality = 25, RewardCount = 1, CashReward = 50, ExperienceReward = 100 }
        };
        public GeneratedMissionResult Complete(GeneratedMissionCompletion completion)
        {
            CompleteCalls++; BeforeComplete?.Invoke(completion);
            if (Failure != null) throw Failure;
            Binding.State = GeneratedMissionState.Completed;
            return new() { Status = GeneratedMissionResultStatus.Applied, Binding = Binding, Cash = completion.CurrentCash + 50, Experience = completion.FinalExperience };
        }
        public IList<GeneratedMissionBinding> ReadAccepted(int owner) => owner == 77 ? [Binding] : [];
        public GeneratedMissionBinding ReadAccepted(int owner, int type, int instance) => owner == 77 && type == Binding.QuestType && instance == Binding.QuestInstance ? Binding : null!;
        public GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance) { AcceptCalls++; throw new AssertFailedException("Unexpected accept transaction."); }
        public int ReserveIdentities(string sequence, int count) => throw new NotSupportedException();
        public GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch) => throw new NotSupportedException();
        public IList<GeneratedMissionOffer> ReadOffers(int owner) => throw new NotSupportedException();
        public GeneratedMissionResult Observe(GeneratedMissionObservation observation) => throw new NotSupportedException();
        public GeneratedMissionResult End(int owner, int type, int instance, GeneratedMissionState state, long now) => throw new NotSupportedException();
        public GeneratedMissionResult AdvanceCleanup(int owner, int type, int instance, long version, long mask, long now) => throw new NotSupportedException();
        public IList<GeneratedMissionObject> ReadObjects(int owner, int type, int instance) => throw new NotSupportedException();
        public GeneratedMissionResult UpdateObjects(int owner, int type, int instance, IList<GeneratedMissionObject> objects, long now) => throw new NotSupportedException();
        public GeneratedMissionResult SavePosition(int owner, int type, int instance, int pf, float x, float y, float z, long now) => throw new NotSupportedException();
        public IList<MissionItemInstanceData> ReadArtifacts(int owner, int type, int instance) => throw new NotSupportedException();
        public GeneratedMissionResult CleanupArtifacts(int owner, int type, int instance, long now) => throw new NotSupportedException();
        public GeneratedMissionResult ClaimCorpseCredits(int owner, int type, int instance, int npc, int cash, long now) => throw new NotSupportedException();
    }
}
