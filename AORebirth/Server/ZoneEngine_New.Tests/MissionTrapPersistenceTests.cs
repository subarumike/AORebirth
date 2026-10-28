namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.IO;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class MissionTrapPersistenceTests
{
    [TestMethod]
    public void ReconstructionKeepsSealedIdentityParentProfileWithoutAllocation()
    {
        var f = new Fixture();
        Chest[] parents = [Parent(41), Parent(42), Parent(43)];
        var first = f.Create().LoadOrInitialize(parents, f.Ids);
        var restored = f.Create().LoadOrInitialize(parents.Reverse().ToArray(), f.Ids);
        Assert.AreEqual(3, first.Count);
        Assert.AreEqual(3, first.Select(trap => trap.ParentInstance).Distinct().Count());
        CollectionAssert.AreEqual(first.Select(trap => trap.Instance).ToArray(), restored.Select(trap => trap.Instance).ToArray());
        CollectionAssert.AreEqual(first.Select(trap => trap.ParentInstance).ToArray(), restored.Select(trap => trap.ParentInstance).ToArray());
        Assert.AreEqual(3, f.Ids.Calls);
    }

    [TestMethod]
    public void EmptySealDoesNotRerollWhenAdditionalParentsAppear()
    {
        var f = new Fixture();
        Assert.AreEqual(0, f.Create().LoadOrInitialize([], f.Ids).Count);
        Assert.AreEqual(0, f.Create().LoadOrInitialize([Parent(42)], f.Ids).Count);
        Assert.AreEqual(0, f.Ids.Calls);
        Assert.AreEqual(1, f.Dao.Flags.Count);
    }

    [DataTestMethod]
    [DataRow(MissionTrapState.Triggered | MissionTrapState.Disabled)]
    [DataRow(MissionTrapState.Disarmed | MissionTrapState.Disabled | MissionTrapState.Revealed)]
    public void StatePersistsAndASecondStaleProjectionCannotWin(MissionTrapState state)
    {
        var f = new Fixture();
        Chest parent = Parent(42);
        var population = f.Create();
        MissionTrapSnapshot stored = population.LoadOrInitialize([parent], f.Ids).Single();
        MissionTrap trap = f.Entity(stored, parent);
        Player actor = TestWorld.CreatePlayer(222);
        Assert.IsTrue(population.TryCommitState(trap, actor, state));
        Assert.IsFalse(population.TryCommitState(trap, actor, MissionTrapState.Destroyed));
        MissionTrapSnapshot restored = f.Create().LoadOrInitialize([parent], f.Ids).Single();
        Assert.AreEqual(state, restored.State);
        if ((state & MissionTrapState.Revealed) != 0)
        {
            Assert.AreEqual(actor.Identity.Instance, restored.RevealedByInstance);
            Assert.AreEqual(actor.Identity, f.Entity(restored, parent).RevealedBy);
        }
        Assert.AreEqual(1, f.Ids.Calls);
    }

    [TestMethod]
    public void FailedTransactionLeavesNoPartialTrapOrSeal()
    {
        var f = new Fixture();
        f.Dao.BeforeCommit = working =>
        {
            if (working.Flags.Count > 0)
                f.Dao.Failure = new InvalidOperationException("Injected failure");
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => f.Create().LoadOrInitialize([Parent(42)], f.Ids));
        Assert.AreEqual(0, f.Dao.Flags.Count);
    }

    [TestMethod]
    public void LostCommitAcknowledgementRecoversSealWithoutNewIds()
    {
        var f = new Fixture();
        f.Dao.BeforeCommit = working => f.Dao.UnknownCommit = working.Flags.Count > 0;
        Assert.ThrowsExactly<MissionCommitOutcomeUnknownException>(() => f.Create().LoadOrInitialize([Parent(42)], f.Ids));
        Assert.AreEqual(2, f.Dao.Flags.Count);
        f.Dao.BeforeCommit = null;
        f.Dao.UnknownCommit = false;
        Assert.AreEqual(1, f.Create().LoadOrInitialize([Parent(42)], f.Ids).Count);
        Assert.AreEqual(1, f.Ids.Calls);
    }

    [TestMethod]
    public void ExpiredOrReassignedRunRejectsBeforeAllocation()
    {
        var f = new Fixture();
        f.Dao.CharacterQuest!.ExpiresAtUtcTicks = DateTime.UtcNow.AddMinutes(-1).Ticks;
        Assert.ThrowsExactly<InvalidOperationException>(() => f.Create().LoadOrInitialize([Parent(42)], f.Ids));
        f.Dao.CharacterQuest.ExpiresAtUtcTicks = DateTime.UtcNow.AddHours(1).Ticks;
        f.Dao.CharacterQuest.AssignedAtUtcTicks++;
        Assert.ThrowsExactly<InvalidOperationException>(() => f.Create().LoadOrInitialize([Parent(42)], f.Ids));
        Assert.AreEqual(0, f.Ids.Calls);
        Assert.AreEqual(0, f.Dao.Flags.Count);
    }

    [TestMethod]
    public void OldMissionIsNotRetrofittedAndMutablePolicyDoesNotReplaceFrozenPolicy()
    {
        var f = new Fixture();
        f.Parameters.Traps!.ChestChance = 0;
        f.Parameters.Traps.Profiles[0].Id = "not-accepted";
        Assert.AreEqual("fixture", f.Create().LoadOrInitialize([Parent(42)], f.Ids).Single().ProfileId);
        f = new Fixture();
        f.Parameters.Traps = null;
        f.Freeze();
        Assert.AreEqual(0, f.Create().LoadOrInitialize([Parent(42)], f.Ids).Count);
        Assert.AreEqual(0, f.Dao.Flags.Count);
    }

    [TestMethod]
    public void RetiredPopulationCannotCommitAStateTransition()
    {
        var f = new Fixture();
        Chest parent = Parent(42);
        var population = f.Create();
        MissionTrapSnapshot stored = population.LoadOrInitialize([parent], f.Ids).Single();
        population.Retire();
        Assert.IsFalse(population.TryCommitState(f.Entity(stored, parent), TestWorld.CreatePlayer(111), MissionTrapState.Disabled));
        Assert.AreEqual(MissionTrapState.Armed, f.Create().LoadOrInitialize([parent], f.Ids).Single().State);
    }

    [TestMethod]
    public void PartialMaterializationRemovesOnlyItsOwnTrapsAndKeepsTheDurableSeal()
    {
        var f = new Fixture();
        using var world = new InventoryActionTests.World();
        var registry = new DynelRegistry();
        using var services = MissionChestProtocolTests.AttachChestServices(world, registry);
        Chest[] parents = [Parent(41), Parent(42)];
        foreach (Chest parent in parents)
        {
            parent.Playfield = world.Player.Playfield;
            parent.Position = world.Player.Position;
            registry.Register(parent);
        }
        var population = f.Create();
        var stored = population.LoadOrInitialize(parents, f.Ids);
        var collision = new Dynel(new Identity { Type = IdentityType.Trap, Instance = stored[1].Instance });
        registry.Register(collision);
        Assert.ThrowsExactly<InvalidDataException>(() => population.Materialize(world.Player.Playfield!, stored, parents));
        Assert.AreEqual(0, registry.Dynels().OfType<MissionTrap>().Count());
        Assert.IsTrue(registry.TryGet(collision.Identity, out Dynel? remaining));
        Assert.AreSame(collision, remaining);
        Assert.AreEqual(2, f.Create().LoadOrInitialize(parents, f.Ids).Count);
        Assert.AreEqual(2, f.Ids.Calls);
    }

    static Chest Parent(int instance) => new(new Identity { Type = IdentityType.Container, Instance = instance });
    sealed class Ids : IItemInstanceIdAllocator
    {
        public int Calls;
        public int Allocate() => 90000 + ++Calls;
    }
    sealed class Fixture
    {
        public readonly AuthoredMissionTestDao Dao = new();
        public readonly Ids Ids = new();
        public QuestDungeonParameters Parameters = new()
        {
            Quality = 25,
            Traps = new MissionTrapPolicy
            {
                Enabled = true, ChestChance = 1, MaximumCount = 8,
                Interaction = new MissionTrapInteractionPolicy { DetectionRange = 8, InteractionRange = 8 },
                Profiles = [new MissionTrapProfile { Id = "fixture", MinMissionQuality = 1, MaxMissionQuality = 250,
                    Weight = 1, NanoId = 83957, Spawn = new MissionTrapSpawnDefinition { TrapFlags = 2,
                    Stats = [new MissionTrapStat { StatId = (int)CharacterStat.StaticInstance, Value = 152936 }] } }]
            }
        };
        public string Frozen = string.Empty;
        public Fixture()
        {
            Dao.CharacterQuest = new CharacterQuestCompletionSnapshotData
            {
                CharacterId = 111, OwnerId = 111, OwnerType = (int)QuestOwnerType.Character,
                QuestId = "generated-trap-fixture", Source = (int)QuestSource.Generated,
                State = (int)QuestState.Active, AssignedAtUtcTicks = 71, ExpiresAtUtcTicks = DateTime.UtcNow.AddHours(1).Ticks
            };
            Freeze();
        }
        public void Freeze() => Dao.CharacterQuest!.AcgBuildingGeneratorJson = Frozen = Parameters.ToJson();
        public MissionTrapPopulation Create() => new(Dao, 111, "generated-trap-fixture", 71, Frozen, Parameters, new StubLogger());
        public MissionTrap Entity(MissionTrapSnapshot snapshot, Chest parent)
        {
            MissionTrapPolicy policy = System.Text.Json.JsonSerializer.Deserialize<QuestDungeonParameters>(Frozen)!.Traps!;
            MissionTrapProfile profile = policy.Profiles.Single();
            var identity = new Identity { Type = IdentityType.Trap, Instance = snapshot.Instance };
            return new MissionTrap(identity, parent, profile.Spawn.Create(identity, parent.Identity, 0),
                profile.NanoId, policy.Interaction, snapshot.State, (_, _, _) => true,
                snapshot.RevealedByInstance > 0 ? new Identity
                    { Type = (IdentityType)snapshot.RevealedByType, Instance = snapshot.RevealedByInstance } : null);
        }
    }
}
