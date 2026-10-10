namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class PerkRequirementIntegrationTests
    {
        const int ActionHash = 0x54455354;
        const int GrantTemplateId = 900001;
        const int ActionTemplateId = 900002;
        const int TargetNanoId = 900003;

        [DataTestMethod]
        [DataRow(96)]
        [DataRow(97)]
        public void KenFiReadsSelectedTargetsFamilyInsteadOfCasterOrFightingTarget(int family)
        {
            using var world = new PerkWorld(KenFiRequirements());
            NpcCharacter selected = world.OtherNpc(2);
            NpcCharacter fighting = world.OtherNpc(3);
            world.Caster.Stats.Set(CharacterStat.NPCFamily, 0);
            selected.Stats.Set(CharacterStat.NPCFamily, family);
            fighting.Stats.Set(CharacterStat.NPCFamily, 0);
            world.Caster.SetFightingTarget(fighting.Identity);
            world.Caster.SetTarget(selected.Identity);

            Assert.IsTrue(world.Caster.TryPreparePerkAction(ActionHash, out Player.PerkActionUse? use));
            Assert.IsNotNull(use);
            Assert.AreSame(selected, use.Target);
        }

        [TestMethod]
        public void KenFiReportsTargetsUnmetFamilyEvenWhenCasterMatches()
        {
            using var world = new PerkWorld(KenFiRequirements());
            NpcCharacter selected = world.OtherNpc(2);
            world.Caster.Stats.Set(CharacterStat.NPCFamily, 97);
            selected.Stats.Set(CharacterStat.NPCFamily, 0);
            world.Caster.SetTarget(selected.Identity);

            Assert.IsFalse(world.Caster.TryPreparePerkAction(ActionHash, out _));

            string feedback = world.Session.Sent.OfType<FormatFeedbackMessage>().Last().FormattedMessage;
            StringAssert.Contains(feedback, "target NPCFamily 0 (needs = 97)");
            StringAssert.Contains(feedback, "target NPCFamily 0 (needs = 96)");
        }

        [TestMethod]
        public void PerkRequirementUsesTargetsRunningNanoResolverAndReportsTargetFailure()
        {
            using var world = new PerkWorld(
                [Selector(Operator.OnTarget), State(Operator.HasRunningNano, TargetNanoId)]);
            Player selected = world.OtherPlayer(2);
            RestoreNano(selected, TargetNanoId);
            world.Caster.SetTarget(selected.Identity);

            Assert.IsTrue(world.Caster.TryPreparePerkAction(ActionHash, out Player.PerkActionUse? use));
            Assert.IsNotNull(use);
            Assert.AreSame(selected, use.Target);

            selected.TryRemoveBuff(TargetNanoId, BuffRemovalReason.Stripped, out _);
            RestoreNano(world.Caster, TargetNanoId);
            world.Session.Sent.Clear();

            Assert.IsFalse(world.Caster.TryPreparePerkAction(ActionHash, out _));
            string feedback = world.Session.Sent.OfType<FormatFeedbackMessage>().Last().FormattedMessage;
            StringAssert.Contains(feedback, "target: needs " + TargetNanoId + " running");
        }

        [TestMethod]
        public void FriendlyPerkWithoutSelectionDoesNotFallBackToFightingTarget()
        {
            using var world = new PerkWorld([], CanFlags.ApplyOnFriendly);
            world.Caster.SetFightingTarget(world.OtherPlayer(2).Identity);
            world.Caster.SetTarget(Identity.None);

            Assert.IsFalse(world.Caster.TryPreparePerkAction(ActionHash, out _));
            StringAssert.Contains(
                world.Session.Sent.OfType<FormatFeedbackMessage>().Last().FormattedMessage,
                "You need a target to use this.");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DazzleRunsOnlyTheCastersMatchingRankBranch(bool higherRank)
        {
            Player caster = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);
            caster.TrainedPerks.Restore(higherRank ? [760, 761] : [760]);
            target.TrainedPerks.Restore(higherRank ? [760] : [760, 761]);
            var action = Event(
                Set(CharacterStat.Strength, 71,
                    Selector(Operator.OnCaster), State(Operator.HasPerk, 760),
                    State(Operator.HasPerk, 761), Link(Operator.Not), Link(Operator.And)),
                Set(CharacterStat.Agility, 72,
                    Selector(Operator.OnCaster), State(Operator.HasPerk, 761),
                    State(Operator.HasPerk, 762), Link(Operator.Not), Link(Operator.And)));

            Assert.IsTrue(Execute(action, target, caster));
            Assert.AreEqual(!higherRank, target.Stats.Get(CharacterStat.Strength) == 71);
            Assert.AreEqual(higherRank, target.Stats.Get(CharacterStat.Agility) == 72);
        }

        [TestMethod]
        public void RelatedRunningNanoChecksRemainOnCaster()
        {
            Player caster = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);
            RestoreNano(caster, 900010);
            RestoreNano(target, 900011);
            var action = Event(Set(CharacterStat.Strength, 73,
                Selector(Operator.OnCaster), State(Operator.HasRunningNano, 900010),
                State(Operator.HasRunningNano, 900011), Link(Operator.Not), Link(Operator.And)));

            Assert.IsTrue(Execute(action, target, caster));
            Assert.AreEqual(73, target.Stats.Get(CharacterStat.Strength));

            RestoreNano(caster, 900011);
            target.Stats.Set(CharacterStat.Strength, 0);
            Assert.IsFalse(Execute(action, target, caster));
            Assert.AreEqual(0, target.Stats.Get(CharacterStat.Strength));
        }

        [DataTestMethod]
        [DataRow(25)]
        [DataRow(26)]
        public void OpportunityKnocksDamageUsesEventRollAfterCasterRankChecks(int roll)
        {
            Player caster = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);
            caster.TrainedPerks.Restore([1243]);
            target.TrainedPerks.Restore([1244]);
            caster.Stats.Set(CharacterStat.LastRnd, roll > 25 ? 0 : 100);
            target.Stats.Set(CharacterStat.Health, 100);
            target.Stats.Set(CharacterStat.MaxHealth, 100);
            target.ActionRestrictionFlags = ActionRestrictionFlags.PvPEnabled;
            var action = Event(
                Set(CharacterStat.CurrentNano, 1, Leaf(CharacterStat.Rnd, Operator.LessThan, 26)),
                new ItemSpell
                {
                    FunctionType = (int)FunctionType.SpecialHit,
                    Target = (int)ItemTarget.Target,
                    Arguments = [(int)CharacterStat.Health, -10, -10],
                    Requirements =
                    [
                        Selector(Operator.OnCaster), State(Operator.HasPerk, 1243),
                        State(Operator.HasNotPerk, 1244), Link(Operator.And),
                        Leaf(CharacterStat.LastRnd, Operator.GreaterThan, 25), Link(Operator.And)
                    ]
                });
            int rolls = 0;

            Assert.IsTrue(action.ExecuteOnUseSpells(target, new StubInventoryRepository(), new StubItemBuilder(),
                source: caster, criteria: new SpellCriteria { NextRoll = () => { rolls++; return roll; } }));

            Assert.AreEqual(1, rolls);
            Assert.AreEqual(roll > 25 ? 90 : 100, target.Stats.Get(CharacterStat.Health));
        }

        static List<ItemRequirement> KenFiRequirements()
            =>
            [
                State(Operator.HasPerk, 1120), State(Operator.IsPerkUnlocked, 1120), Link(Operator.And),
                Selector(Operator.OnTarget), Leaf(CharacterStat.NPCFamily, Operator.EqualTo, 97),
                Link(Operator.And), Leaf(CharacterStat.NPCFamily, Operator.EqualTo, 96), Link(Operator.Or)
            ];

        static bool Execute(ItemTemplate action, Character target, Character caster)
            => action.ExecuteOnUseSpells(target, new StubInventoryRepository(), new StubItemBuilder(), source: caster);

        static ItemTemplate Event(params ItemSpell[] spells)
            => new()
            {
                Id = ActionTemplateId,
                SpellList = new Dictionary<EventType, List<ItemSpell>> { [EventType.OnUse] = new(spells) }
            };

        static ItemSpell Set(CharacterStat stat, int value, params ItemRequirement[] requirements)
            => new()
            {
                FunctionType = (int)FunctionType.Set,
                Target = (int)ItemTarget.Target,
                Arguments = [(int)stat, value],
                Requirements = new(requirements)
            };

        static ItemRequirement Leaf(CharacterStat stat, Operator op, int value)
            => new() { StatNumber = (int)stat, Operator = (int)op, Value = value };

        static ItemRequirement State(Operator op, int id)
            => new() { Operator = (int)op, Value = id };

        static ItemRequirement Selector(Operator op) => new() { Operator = (int)op };

        static ItemRequirement Link(Operator op) => new() { Operator = (int)op };

        static void RestoreNano(Character character, int id)
        {
            Assert.IsNotNull(character.TryRestoreBuff(TestNanos.Create(id, ncuCost: 0), character.Identity, id,
                DateTime.UtcNow.AddMinutes(1)));
        }

        sealed class PerkWorld : IDisposable
        {
            readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "AORebirth-perk-test-" + Guid.NewGuid().ToString("N"));
            readonly ServiceProvider _services;
            readonly DynelRegistry _registry = new();

            public Playfield Playfield { get; }
            public Player Caster { get; }
            public RecordingZoneSession Session { get; } = new();

            public PerkWorld(List<ItemRequirement> requirements, CanFlags can = 0)
            {
                Directory.CreateDirectory(_dataRoot);
                File.WriteAllText(Path.Combine(_dataRoot, "Perks.json"),
                    "{\"PerkLines\":[{\"Perks\":[{\"Id\":1120,\"ItemId\":900001,\"Tier\":1}]}]}");
                var action = Event(Set(CharacterStat.Strength, 1));
                action.Stats[CharacterStat.Can] = unchecked((int)can);
                action.Actions.Add(new ItemAction { ActionType = (int)ActionType.ToUse, Requirements = requirements });
                var items = new StubItemBuilder().Add(action).Add(new ItemTemplate
                {
                    Id = GrantTemplateId,
                    SpellList = new Dictionary<EventType, List<ItemSpell>>
                    {
                        [EventType.OnWear] =
                        [
                            new ItemSpell
                            {
                                FunctionType = (int)FunctionType.AddAction,
                                Arguments = [0, ActionHash, 0, ActionTemplateId]
                            }
                        ]
                    }
                });
                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection().AddSingleton(_registry).AddSingleton<WorldSimulationAccess>()
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new ConcurrentDictionary<Character, byte>());
                Caster = new Player(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 }, new StubLogger(), items)
                {
                    Playfield = Playfield,
                    PerkCatalog = PerkCatalog.Load(_dataRoot),
                    PerkActionCatalog = PerkActionCatalog.Empty,
                    Session = Session
                };
                Caster.Inventory.Apply(new CharacterHydrationResult(), 1, items);
                Session.BindPlayer(Caster);
                _registry.Register(Caster);
                Caster.LoadTrainedPerks([1120]);
            }

            public Player OtherPlayer(int id)
            {
                Player player = TestWorld.CreatePlayer(id);
                player.Playfield = Playfield;
                _registry.Register(player);
                return player;
            }

            public NpcCharacter OtherNpc(int id)
            {
                var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = id },
                    new StubItemBuilder())
                {
                    Playfield = Playfield,
                    Attackable = true
                };
                _registry.Register(npc);
                return npc;
            }

            public void Dispose()
            {
                _services.Dispose();
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
    }
}
