using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using Vector3 = AORebirth.Core.Vector.Vector3;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class FriendlyItemUseTests
{
    const int HealthRestored = 30;
    const int NanoRestored = 20;
    const CanFlags FriendlyAndSelf = CanFlags.ApplyOnFriendly | CanFlags.ApplyOnSelf;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FriendlyStimAllowsSelfUseWhenTemplateAllowsSelf(bool selectSelf)
    {
        using var h = new Harness();
        if (selectSelf)
            h.User.SetTarget(h.User.Identity);
        Item stim = h.Stim();

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50 + HealthRestored, 40 + NanoRestored);
        Assert.IsTrue(h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.AreEqual(2, stim.StackCount);
        AssertUseAnnouncement(h, stim, h.User);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FriendlyStimRestoresSelectedPlayerEvenWhenUserIsFull(bool userFull)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        if (userFull)
            SetVitals(h.User, 100, 100);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, userFull ? 100 : 50, userFull ? 100 : 40);
        AssertVitals(patient, 50 + HealthRestored, 40 + NanoRestored);
        Assert.IsTrue(h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.IsFalse(patient.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.AreEqual(2, stim.StackCount);
        AssertUseAnnouncement(h, stim, patient);
        HealthDamageMessage heal = h.Session(patient).Messages.OfType<HealthDamageMessage>().Single();
        Assert.AreEqual(patient.Identity, heal.Identity);
        Assert.AreEqual(h.User.Identity, heal.Target);
        Assert.AreEqual(HealthRestored, heal.Unknown2);
        patient.FlushDirtyStats();
        Assert.IsTrue(h.Session(patient).Messages.OfType<StatMessage>()
            .Any(message => message.Identity == patient.Identity && message.Stats.Any(stat =>
                stat.Value1 == CharacterStat.CurrentNano && stat.Value2 == 40 + NanoRestored)));
    }

    [TestMethod]
    public void DelayedUseKeepsAcceptedPatientWhenSelectionChangesAndSpendsOneCharge()
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        Player nextSelection = h.OtherPlayer(333);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(delay: 100);

        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.User, Slot(64), stim));
        Assert.IsTrue(stim.Locked);
        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));
        h.User.SetTarget(nextSelection.Identity);
        h.Uses.Tick(0.5);
        AssertVitals(patient, 50, 40);
        Assert.IsFalse(h.UserSession.Messages.OfType<TemplateActionMessage>()
            .Any(message => message.Action == TemplateActionType.Use));
        h.Uses.Tick(0.6);
        h.Uses.Tick(2);

        AssertVitals(patient, 50 + HealthRestored, 40 + NanoRestored);
        AssertVitals(h.User, 50, 40);
        AssertVitals(nextSelection, 50, 40);
        Assert.AreEqual(2, stim.StackCount);
        Assert.IsFalse(stim.Locked);
        Assert.IsFalse(h.Uses.HasPending(h.User.Identity.Instance));
        AssertUseAnnouncement(h, stim, patient);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("dead")]
    [DataRow("other-playfield")]
    [DataRow("disconnected")]
    [DataRow("out-of-range")]
    public void InvalidSelectedPatientDoesNotFallBackToSelfOrConsume(string invalid)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        h.MakeInvalid(patient, invalid);
        Item stim = h.Stim();

        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
        Assert.IsFalse(stim.Locked);
        Assert.IsFalse(h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.IsFalse(h.UserSession.Messages.OfType<TemplateActionMessage>()
            .Any(message => message.Action == TemplateActionType.Use));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("dead")]
    [DataRow("other-playfield")]
    [DataRow("disconnected")]
    [DataRow("replacement")]
    public void PatientLostDuringDelayAbortsWithoutEffectsLocksOrConsumption(string invalid)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(delay: 100);
        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.User, Slot(64), stim));

        h.MakeInvalid(patient, invalid);
        h.Uses.Tick(2);

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
        Assert.IsFalse(stim.Locked);
        Assert.IsFalse(h.Uses.HasPending(h.User.Identity.Instance));
        Assert.IsFalse(h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.IsFalse(patient.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.IsFalse(h.UserSession.Messages.OfType<TemplateActionMessage>()
            .Any(message => message.Action == TemplateActionType.Use));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToUseOnTargetRequirementReadsPatientInsteadOfUser(bool patientQualifies)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.Stats.Set(CharacterStat.Stamina, patientQualifies ? 5 : 50);
        patient.Stats.Set(CharacterStat.Stamina, patientQualifies ? 50 : 5);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { Operator = (int)Operator.OnTarget },
                new() { StatNumber = (int)CharacterStat.Stamina, Operator = (int)Operator.GreaterThan, Value = 10 }
            ]
        });

        Assert.AreEqual(patientQualifies ? ItemUseStart.Executed : ItemUseStart.Rejected,
            h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, patientQualifies ? 50 + HealthRestored : 50, patientQualifies ? 40 + NanoRestored : 40);
        Assert.AreEqual(patientQualifies ? 2 : 3, stim.StackCount);
    }

    [TestMethod]
    public void UserRequirementDoesNotBorrowPatientsSkill()
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.Stats.Set(CharacterStat.FirstAid, 5);
        patient.Stats.Set(CharacterStat.FirstAid, 50);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { StatNumber = (int)CharacterStat.FirstAid, Operator = (int)Operator.GreaterThan, Value = 10 }
            ]
        });

        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));
        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
    }

    [TestMethod]
    public void AcceptedRangeAndUseRequirementsRemainStartOnlyDuringDelay()
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        patient.Stats.Set(CharacterStat.Stamina, 50);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(delay: 100);
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { Operator = (int)Operator.OnTarget },
                new() { StatNumber = (int)CharacterStat.Stamina, Operator = (int)Operator.GreaterThan, Value = 10 }
            ]
        });
        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.User, Slot(64), stim));
        patient.Stats.Set(CharacterStat.Stamina, 5);
        patient.Position = new Vector3(100, 0, 0);

        h.Uses.Tick(2);

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, 50 + HealthRestored, 40 + NanoRestored);
        Assert.AreEqual(2, stim.StackCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnTargetStateRequirementUsesPatientsNanoState(bool patientHasNano)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        Player buffOwner = patientHasNano ? patient : h.User;
        Assert.IsNotNull(buffOwner.TryRestoreBuff(TestNanos.Create(900001, ncuCost: 0), buffOwner.Identity, 1,
            DateTime.UtcNow.AddMinutes(1)));
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { Operator = (int)Operator.OnTarget },
                new() { Operator = (int)Operator.HasRunningNano, Value = 900001 }
            ]
        });

        Assert.AreEqual(patientHasNano ? ItemUseStart.Executed : ItemUseStart.Rejected,
            h.Uses.TryBegin(h.User, Slot(64), stim));
        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, patientHasNano ? 50 + HealthRestored : 50, patientHasNano ? 40 + NanoRestored : 40);
        Assert.AreEqual(patientHasNano ? 2 : 3, stim.StackCount);
    }

    [TestMethod]
    [DataRow(ItemTarget.User)]
    [DataRow(ItemTarget.Wearer)]
    [DataRow(ItemTarget.Target)]
    public void LockSkillKeepsItsExplicitOwnerWhenHealingAnotherPlayer(ItemTarget lockTarget)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(lockTarget: lockTarget);

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(patient, 50 + HealthRestored, 40 + NanoRestored);
        bool targetLocked = lockTarget == ItemTarget.Target;
        Assert.AreEqual(!targetLocked, h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.AreEqual(targetLocked, patient.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        Assert.AreEqual(2, stim.StackCount);
    }

    [TestMethod]
    [DataRow(ItemTarget.User)]
    [DataRow(ItemTarget.Wearer)]
    [DataRow(ItemTarget.Target)]
    public void ExistingLockOnItsExplicitOwnerPreventsEffectsAndConsumption(ItemTarget lockTarget)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Player locked = lockTarget == ItemTarget.Target ? patient : h.User;
        locked.SkillLocks.Lock((int)CharacterStat.FirstAid, 40, DateTime.UtcNow);
        Item stim = h.Stim(lockTarget: lockTarget);

        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
        Assert.IsFalse(stim.Locked);
    }

    [TestMethod]
    public void FriendlyOnlyTemplateDoesNotInventSelfPermission()
    {
        using var h = new Harness();
        Item stim = h.Stim(can: CanFlags.ApplyOnFriendly);

        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));
        AssertVitals(h.User, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
    }

    [TestMethod]
    public void SelectedWorldObjectDoesNotRedirectFriendlyStimToUser()
    {
        using var h = new Harness();
        StaticDynel selected = h.WorldObject();
        h.User.SetTarget(selected.Identity);
        Item stim = h.Stim();

        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50, 40);
        Assert.AreEqual(3, stim.StackCount);
        Assert.IsFalse(h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
    }

    [TestMethod]
    public void SelfOnlyTemplateStillUsesUserWithAnotherPlayerSelected()
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(can: CanFlags.ApplyOnSelf);

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50 + HealthRestored, 40 + NanoRestored);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(2, stim.StackCount);
    }

    [TestMethod]
    public void UserDirectedFunctionsStillApplyToUserWithAnotherPlayerSelected()
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim(effectTarget: ItemTarget.User);

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), stim));
        AssertVitals(h.User, 50 + HealthRestored, 40 + NanoRestored);
        AssertVitals(patient, 50, 40);
        Assert.AreEqual(2, stim.StackCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AuthoredTargetTypeRequirementControlsNpcEligibility(bool selectNpc)
    {
        using var h = new Harness();
        Character patient = selectNpc ? h.HostileNpc(444) : h.OtherPlayer(222);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { StatNumber = (int)CharacterStat.SelectedTargetType,
                    Operator = (int)Operator.NotBitAnd, Value = 16 }
            ]
        });

        Assert.AreEqual(selectNpc ? ItemUseStart.Rejected : ItemUseStart.Executed,
            h.Uses.TryBegin(h.User, Slot(64), stim));

        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, selectNpc ? 50 : 50 + HealthRestored, selectNpc ? 40 : 40 + NanoRestored);
        Assert.AreEqual(selectNpc ? 3 : 2, stim.StackCount);
        Assert.AreEqual(!selectNpc, h.User.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnTargetCharStateRequirementRefreshesPatientsRuntimeState(bool patientFighting)
    {
        using var h = new Harness();
        Player patient = h.OtherPlayer(222);
        if (patientFighting)
            patient.SetFightingTarget(h.HostileNpc(444).Identity);
        patient.Stats.Set(CharacterStat.CharState, patientFighting ? 0 : 10);
        h.User.SetTarget(patient.Identity);
        Item stim = h.Stim();
        stim.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements =
            [
                new() { Operator = (int)Operator.OnTarget },
                new() { StatNumber = (int)CharacterStat.CharState, Operator = (int)Operator.EqualTo, Value = 10 }
            ]
        });

        Assert.AreEqual(patientFighting ? ItemUseStart.Executed : ItemUseStart.Rejected,
            h.Uses.TryBegin(h.User, Slot(64), stim));

        Assert.AreEqual(patientFighting ? 10 : 0, patient.Stats.GetOrZero(CharacterStat.CharState));
        AssertVitals(h.User, 50, 40);
        AssertVitals(patient, patientFighting ? 50 + HealthRestored : 50, patientFighting ? 40 + NanoRestored : 40);
        Assert.AreEqual(patientFighting ? 2 : 3, stim.StackCount);
    }

    [TestMethod]
    public void HostileTargetUseRetainsExistingDamageRouting()
    {
        using var h = new Harness();
        NpcCharacter target = h.HostileNpc(444);
        h.User.SetTarget(target.Identity);
        Item item = h.Stim(can: CanFlags.ApplyOnHostile);
        item.SpellList[EventType.OnUse] =
        [
            new() { FunctionType = (int)FunctionType.Hit, Target = (int)ItemTarget.Target,
                Arguments = [(int)CharacterStat.Health, -10, -10, 0] }
        ];

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), item));
        AssertVitals(h.User, 50, 40);
        Assert.AreEqual(40, target.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(2, item.StackCount);
    }

    [TestMethod]
    public void FightingTargetTakesPrecedenceOverFriendlySelection()
    {
        using var h = new Harness();
        Player selected = h.OtherPlayer(222);
        NpcCharacter opponent = h.HostileNpc(444);
        h.User.SetTarget(selected.Identity);
        h.User.SetFightingTarget(opponent.Identity);
        Item item = h.Stim(can: CanFlags.ApplyOnFightingTarget | CanFlags.ApplyOnHostile | FriendlyAndSelf);
        item.SpellList[EventType.OnUse] =
        [
            new() { FunctionType = (int)FunctionType.Hit, Target = (int)ItemTarget.Target,
                Arguments = [(int)CharacterStat.Health, -10, -10, 0] }
        ];

        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.User, Slot(64), item));

        AssertVitals(h.User, 50, 40);
        AssertVitals(selected, 50, 40);
        AssertVitals(opponent, 40, 40);
        Assert.AreEqual(2, item.StackCount);
    }

    static Identity Slot(int placement) => new() { Type = IdentityType.Inventory, Instance = placement };

    static void AssertUseAnnouncement(Harness h, Item item, Character recipient)
    {
        TemplateActionMessage used = h.UserSession.Messages.OfType<TemplateActionMessage>()
            .Single(message => message.Action == TemplateActionType.Use);
        Assert.AreEqual(h.User.Identity, used.Identity);
        Assert.AreEqual(Slot(64), used.Placement);
        Assert.AreEqual(item.LowId, used.ItemLowId);
        Assert.AreEqual(item.HighId, used.ItemHighId);
        Assert.AreEqual(item.Quality, used.Quality);
        Assert.AreEqual(1, used.Count);
        Assert.AreEqual((int)recipient.Identity.Type, used.Unknown3);
        Assert.AreEqual(recipient.Identity.Instance, used.Unknown4);
    }

    static void AssertVitals(Character character, int health, int nano)
    {
        Assert.AreEqual(health, character.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(nano, character.Stats.GetOrZero(CharacterStat.CurrentNano));
    }

    static void SetVitals(Character character, int health = 50, int nano = 40)
    {
        character.Stats.Set(CharacterStat.MaxHealth, 100);
        character.Stats.Set(CharacterStat.MaxNanoEnergy, 100);
        character.Stats.Set(CharacterStat.Health, health);
        character.Stats.Set(CharacterStat.CurrentNano, nano);
    }

    sealed class Harness : IDisposable
    {
        readonly InventoryActionTests.World _world = new();
        readonly ServiceProvider _services;
        readonly DynelRegistry _registry = new();

        public Player User => _world.Player;
        public InventoryActionTests.Session UserSession => _world.Session;
        public ItemUseService Uses { get; }

        public Harness()
        {
            var moves = new InventoryMoveService(new StubLogger(), _world.Flush, _world.Actions);
            Uses = new ItemUseService(User.Playfield!, new StubLogger(), moves, new StubInventoryRepository(), new StubItemBuilder());
            _services = new ServiceCollection().AddSingleton(_registry).AddSingleton<WorldSimulationAccess>()
                .AddSingleton<IGameData>(new StubGameData(HashItemCatalog.Parse("{}")))
                .AddSingleton(new PlayfieldLocality(4582, null)).AddSingleton(_world.Flush)
                .AddSingleton(moves).AddSingleton(Uses).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(User.Playfield, _services);
            typeof(Playfield).GetField("_lifecycleSync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(User.Playfield, new System.Threading.Lock());
            typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(User.Playfield, new ConcurrentDictionary<Character, byte>());
            _registry.Register(User);
            User.Position = new Vector3(0, 0, 0);
            SetVitals(User);
        }

        public Player OtherPlayer(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Playfield = User.Playfield;
            player.Position = new Vector3(2, 0, 0);
            var session = new InventoryActionTests.Session();
            session.BindPlayer(player);
            player.Session = session;
            _registry.Register(player);
            SetVitals(player);
            return player;
        }

        public InventoryActionTests.Session Session(Player player) => (InventoryActionTests.Session)player.Session!;

        public StaticDynel WorldObject()
        {
            var target = new TestObject(new Identity { Type = IdentityType.Terminal, Instance = 555 })
            {
                Playfield = User.Playfield,
                Position = new Vector3(2, 0, 0)
            };
            _registry.Register(target);
            return target;
        }

        public NpcCharacter HostileNpc(int id)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = id }, new StubItemBuilder())
            {
                Playfield = User.Playfield,
                Position = new Vector3(2, 0, 0),
                Attackable = true
            };
            _registry.Register(npc);
            SetVitals(npc);
            return npc;
        }

        public Item Stim(int delay = 0, CanFlags can = FriendlyAndSelf,
            ItemTarget lockTarget = ItemTarget.Wearer, ItemTarget effectTarget = ItemTarget.Target)
        {
            Item item = _world.Add(11, 3);
            item.Definition.Stats[CharacterStat.Can] = unchecked((int)(can | CanFlags.Use | CanFlags.Consume | CanFlags.Stackable));
            item.Definition.Stats[CharacterStat.AttackDelay] = delay;
            item.Definition.Stats[CharacterStat.AttackRange] = 5;
            item.SpellList[EventType.OnUse] =
            [
                new() { FunctionType = (int)FunctionType.Hit, Target = (int)effectTarget,
                    Arguments = [(int)CharacterStat.Health, HealthRestored, HealthRestored, 0] },
                new() { FunctionType = (int)FunctionType.Hit, Target = (int)effectTarget,
                    Arguments = [(int)CharacterStat.CurrentNano, NanoRestored, NanoRestored, 0] },
                new() { FunctionType = (int)FunctionType.LockSkill, Target = (int)lockTarget,
                    Arguments = [(int)CharacterStat.FirstAid, 40] }
            ];
            return item;
        }

        public void MakeInvalid(Player patient, string reason)
        {
            switch (reason)
            {
                case "missing":
                    _registry.UnregisterExact(patient);
                    break;
                case "dead":
                    typeof(Character).GetField("_deathNotified", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(patient, true);
                    break;
                case "other-playfield":
                    patient.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                    break;
                case "disconnected":
                    Session(patient).Close();
                    break;
                case "out-of-range":
                    patient.Position = new Vector3(100, 0, 0);
                    break;
                case "replacement":
                    OtherPlayer(patient.Identity.Instance);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        public void Dispose()
        {
            _services.Dispose();
            _world.Dispose();
        }
    }

    sealed class TestObject(Identity identity) : StaticDynel(identity, new ItemTemplate { Id = 900002 });
}
