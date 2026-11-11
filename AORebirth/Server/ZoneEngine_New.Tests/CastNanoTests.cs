namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Pets;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class CastNanoTests
    {
        const int BuffId = 21001;
        const int InstantId = 21002;
        const int ChildId = 21003;
        const int HostileId = 21004;

        [TestMethod]
        public void OnUseCastNanoOccupiesNcuWithoutTouchingCastState()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            Player player = ReadyPlayer(1);
            player.BeginNanoCast(new PendingNanoCast(TestNanos.Create(9999, attackDelay: 200), player.Identity, 0, 200, DateTime.UtcNow));

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreEqual(1, player.Buffs.Count);
            Assert.AreEqual(BuffId, player.Buffs[0].Id);
            Assert.AreEqual(10, player.UsedNcu);
            Assert.IsNotNull(player.PendingCast);
            Assert.IsFalse(player.IsInNanoRecharge(DateTime.UtcNow));
        }

        [TestMethod]
        public void InstantCastNanoRunsOnUseAndDoesNotEnterNcu()
        {
            var items = new StubItemBuilder().Add(InstantHit(InstantId, heal: 12));
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 40);

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, InstantId));
            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(52, player.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        [DataRow(3.0, true)]
        [DataRow(60.0, false)]
        public void HealPetKeepsInRangeOrderWhileNanoRecovers(double distance, bool inRange)
        {
            NanoSpell heal = TestNanos.Create(InstantId, durationCentiseconds: 0, nanoPointCost: 5,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, 12, 12]
                    }
                ]);
            heal.Stats[CharacterStat.AttackRange] = 20;
            using var world = new FanoutWorld(new StubItemBuilder().Add(heal));
            Player owner = world.Player(1, 0, 0, 0);
            Player teammate = world.Player(2, distance, 0, 0);
            world.Join(owner, teammate);
            teammate.Stats.Set(CharacterStat.MaxHealth, 100);
            teammate.Stats.Set(CharacterStat.Health, 40);
            NpcCharacter pet = world.Npc(3, 0, 0, 0, attackable: true);
            pet.Stats.Set(CharacterStat.CurrentNano, 0);
            pet.TryAddUploadedNano(heal.Id);
            var controller = new PetController(owner, PetTypes.Heal, "TEST", 1, null);
            pet.BindPet(controller);
            controller.Heal(teammate.Identity);
            var tend = new PetTendNode(NpcBrain.CreatePet(pet, controller));

            tend.Evaluate(0.1f);

            Assert.AreEqual(40, teammate.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(inRange ? teammate.Identity : Identity.None, controller.HealTarget,
                "Low nano may postpone an in-range heal; an out-of-range order still ends.");
            if (inRange)
            {
                pet.Stats.Set(CharacterStat.CurrentNano, 5);
                tend.Evaluate(0.1f);
                Assert.AreEqual(52, teammate.Stats.GetOrZero(CharacterStat.Health));
                Assert.AreEqual(0, pet.Stats.GetOrZero(CharacterStat.CurrentNano));
            }
        }

        [TestMethod]
        public void CastNanoLeavesAnExistingPendingCastUntouched()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            Player player = ReadyPlayer(1);
            var pending = new PendingNanoCast(TestNanos.Create(8888, attackDelay: 400), player.Identity, 15, 400, DateTime.UtcNow);
            player.BeginNanoCast(pending);

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreSame(pending, player.PendingCast);
        }

        [TestMethod]
        public void NcuRefusalDoesNotApply()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(BuffId, ncuCost: 40));
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 10);

            Assert.IsFalse(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(0, player.UsedNcu);
        }

        [TestMethod]
        public void NestedCastNanoFromParentOnUseLandsTheChild()
        {
            NanoSpell parent = TestNanos.Create(
                BuffId,
                modifiers: [Cast(FunctionType.CastNano, ChildId)]);
            var items = new StubItemBuilder().Add(parent).Add(Timed(ChildId));
            Player player = ReadyPlayer(1);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(
                player,
                player,
                BuffId,
                items,
                new SilentInventory(),
                DateTime.UtcNow));
            Assert.AreEqual(2, player.Buffs.Count);
            Assert.IsTrue(player.TryGetBuff(BuffId, out _));
            Assert.IsTrue(player.TryGetBuff(ChildId, out _));
        }

        [TestMethod]
        public void AreaCastNanoUsesRadiusAndAttackableNotVendorSkip()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(HostileId, can: CanFlags.ApplyOnHostile));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter nearVendor = world.Npc(2, 4, 0, 0, attackable: true);
            NpcCharacter far = world.Npc(3, 40, 0, 0, attackable: true);
            NpcCharacter nearSafe = world.Npc(4, 3, 0, 0, attackable: false);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 10));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(1, nearVendor.Buffs.Count);
            Assert.AreEqual(0, far.Buffs.Count);
            Assert.AreEqual(0, nearSafe.Buffs.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NanoWithoutDefenseSkillsLandsDespiteNanoResistAndAddAllDef(bool explicitZeroDefense)
        {
            NanoSpell nano = TestNanos.Create(HostileId, flags: NanoFlags.IsHostile);
            if (explicitZeroDefense)
            {
                nano.Defend.Add(CharacterStat.NanoAC, 0);
            }
            Player caster = ReadyPlayer(1);
            Player target = ReadyPlayer(2);
            caster.Stats.Set(CharacterStat.AMSModifier, 0);
            target.Stats.Set(CharacterStat.NanoAC, 100000);
            target.Stats.Set(CharacterStat.DMSModifier, 100000);

            Assert.IsTrue(DamageCalculator.ResolveNanoLand(caster, target, nano, out _, out int defense, out int chance));
            Assert.AreEqual(0, defense);
            Assert.AreEqual(100, chance);
        }

        [TestMethod]
        public void NanoWithAuthoredDefenseKeepsItsResistanceContest()
        {
            NanoSpell nano = TestNanos.Create(HostileId, flags: NanoFlags.IsHostile);
            nano.Attack.Add(CharacterStat.PsychologicalModification, 100);
            nano.Defend.Add(CharacterStat.NanoAC, 25);
            Player caster = ReadyPlayer(1);
            Player target = ReadyPlayer(2);
            caster.Stats.Set(CharacterStat.PsychologicalModification, 0);
            caster.Stats.Set(CharacterStat.AMSModifier, 0);
            target.Stats.Set(CharacterStat.NanoAC, 1000);
            target.Stats.Set(CharacterStat.DMSModifier, 100);

            DamageCalculator.ResolveNanoLand(caster, target, nano, out int attack, out int defense, out int chance);
            Assert.AreEqual(0, attack);
            Assert.AreEqual(350, defense);
            Assert.AreEqual(0, chance);
        }

        [TestMethod]
        public void AreaTauntWithoutDefenseReachesAnNpcWithHighNanoResist()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId, durationCentiseconds: 0, can: 0, flags: NanoFlags.IsHostile,
                modifiers: [Taunt(5000)]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            caster.Stats.Set(CharacterStat.AMSModifier, 0);
            NpcCharacter target = world.Npc(2, 5, 0, 0, attackable: true);
            target.Stats.Set(CharacterStat.NanoAC, 100000);
            target.Stats.Set(CharacterStat.DMSModifier, 100000);
            NpcBrain.Create(target);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual(5000f, target.Brain!.Hate.ThreatOf(caster.Identity));
        }

        [TestMethod]
        public void AreaTauntAddsTheRowPickedByTheCastersSkillToNpcsInRange()
        {
            // Mongo Slam!'s shape (100198 area-casting 100194): a hostile child whose TauntNpc rows are picked by the
            // caster's skill.
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId,
                durationCentiseconds: 0,
                can: 0,
                flags: NanoFlags.IsHostile,
                modifiers:
                [
                    Taunt(2000, CasterSkill(Operator.LessThan, 50)),
                    Taunt(4000, CasterSkill(Operator.GreaterThan, 149))
                ]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            caster.Stats.Set(CharacterStat.PsychologicalModification, 10);
            NpcCharacter near = world.Npc(2, 5, 0, 0, attackable: true);
            NpcCharacter far = world.Npc(3, 40, 0, 0, attackable: true);
            NpcBrain.Create(near);
            NpcBrain.Create(far);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual(2000f, near.Brain!.Hate.ThreatOf(caster.Identity));
            Assert.AreEqual(0f, far.Brain!.Hate.ThreatOf(caster.Identity));

            caster.Stats.Set(CharacterStat.PsychologicalModification, 200);
            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual(6000f, near.Brain.Hate.ThreatOf(caster.Identity));
        }

        [TestMethod]
        public void AreaTauntMiddleRowReadsBothSkillChecksOnTheCaster()
        {
            // Mongo Slam!'s middle row: one OnCaster selector, then two stat-129 checks joined by And. Both checks
            // read the caster, not the NPC (whose stat 129 is 0).
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId,
                durationCentiseconds: 0,
                can: 0,
                flags: NanoFlags.IsHostile,
                modifiers:
                [
                    Taunt(3000,
                        CasterSkill(Operator.LessThan, 150),
                        CasterSkill(Operator.GreaterThan, 49),
                        new ItemRequirement { Operator = (int)Operator.And })
                ]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            caster.Stats.Set(CharacterStat.PsychologicalModification, 100);
            NpcCharacter near = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain.Create(near);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual(3000f, near.Brain!.Hate.ThreatOf(caster.Identity));

            caster.Stats.Set(CharacterStat.PsychologicalModification, 200);
            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual(3000f, near.Brain.Hate.ThreatOf(caster.Identity));
        }

        [TestMethod]
        public void TauntAddsNoHateFromAnNpcOrWithoutAnAmount()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcCharacter otherNpc = world.Npc(3, 6, 0, 0, attackable: true);
            NpcBrain.Create(victim);

            Assert.IsFalse(UseTaunt(otherNpc, victim, items, amount: 500));
            Assert.IsFalse(UseTaunt(caster, victim, items, amount: 0));
            Assert.IsTrue(victim.Brain!.Hate.IsEmpty);

            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.AreEqual(500f, victim.Brain.Hate.ThreatOf(caster.Identity));
        }

        [DataTestMethod]
        [DataRow(0, 200, 2000)]
        [DataRow(49, 100, 2000)]
        [DataRow(50, 0, 3000)]
        [DataRow(100, 200, 3000)]
        [DataRow(149, 0, 3000)]
        [DataRow(150, 100, 4000)]
        [DataRow(200, 0, 4000)]
        public void AreaTauntSelectsExactlyOneMongoBandAtSkillBoundaries(
            int casterSkill, int targetSkill, int expectedThreat)
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId,
                durationCentiseconds: 0,
                can: 0,
                flags: NanoFlags.IsHostile,
                modifiers:
                [
                    Taunt(2000, CasterSkill(Operator.LessThan, 50)),
                    Taunt(3000,
                        CasterSkill(Operator.LessThan, 150),
                        CasterSkill(Operator.GreaterThan, 49),
                        new ItemRequirement { Operator = (int)Operator.And }),
                    Taunt(4000, CasterSkill(Operator.GreaterThan, 149))
                ]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            caster.Stats.Set(CharacterStat.PsychologicalModification, casterSkill);
            NpcCharacter near = world.Npc(2, 5, 0, 0, attackable: true);
            near.Stats.Set(CharacterStat.PsychologicalModification, targetSkill);
            NpcBrain nearBrain = NpcBrain.Create(near);
            NpcCharacter far = world.Npc(3, 40, 0, 0, attackable: true);
            NpcBrain farBrain = NpcBrain.Create(far);
            NpcCharacter safe = world.Npc(4, 3, 0, 0, attackable: false);
            NpcBrain safeBrain = NpcBrain.Create(safe);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 20));
            Assert.AreEqual((float)expectedThreat, nearBrain.Hate.ThreatOf(caster.Identity));
            Assert.AreEqual(1, nearBrain.Hate.Count);
            Assert.IsTrue(farBrain.Hate.IsEmpty);
            Assert.IsTrue(safeBrain.Hate.IsEmpty);
        }

        [TestMethod]
        public void PositiveTauntsAccumulateWithoutReplacingExistingHate()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player other = world.Player(3, 1, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);
            brain.AddThreat(caster.Identity, 10);
            brain.AddThreat(other.Identity, 40);

            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 250));
            Assert.AreEqual(760f, brain.Hate.ThreatOf(caster.Identity));
            Assert.AreEqual(40f, brain.Hate.ThreatOf(other.Identity));
            Assert.AreEqual(2, brain.Hate.Count);
        }

        [TestMethod]
        public void PlayerOwnedPetTauntCreditsThePetAndNotItsOwner()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player owner = world.Player(1, 0, 0, 0);
            NpcCharacter pet = world.Npc(3, 1, 0, 0, attackable: true);
            pet.BindPet(new PetController(owner, type: 1, hash: "synthetic-pet", level: 60, expiresUtc: null));
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);

            Assert.IsTrue(UseTaunt(pet, victim, items, amount: 500));
            Assert.AreEqual(500f, brain.Hate.ThreatOf(pet.Identity));
            Assert.AreEqual(0f, brain.Hate.ThreatOf(owner.Identity));
            Assert.AreEqual(1, brain.Hate.Count);
        }

        [TestMethod]
        public void PacifiedNpcAcceptsTauntWithoutKeepingHate()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);
            NanoSpell pacify = TestNanos.Create(BuffId,
                modifiers: [new ItemSpell { FunctionType = (int)FunctionType.Pacify }]);
            Assert.AreEqual(BuffApplyDecision.Apply,
                victim.TryApplyBuff(pacify, victim.Identity, DateTime.UtcNow, out Buff? buff, out _));
            Assert.IsTrue(victim.IsPacified);

            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.IsTrue(brain.Hate.IsEmpty);
            Assert.IsTrue(victim.IsPacified);
            Assert.AreSame(buff, victim.Buffs[0]);
            Assert.AreEqual(Identity.None, victim.FightingTarget);
        }

        [TestMethod]
        public void EvadingNpcAcceptsTauntWithoutKeepingHateOrLeavingEvade()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, NpcAiRules.MaxLeashRange + 10, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim, new Vector3(0, 0, 0));
            brain.AddThreat(caster.Identity, 10);
            Assert.IsTrue(brain.ShouldLeash());
            Assert.IsTrue(brain.IsEvading);

            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.IsTrue(brain.Hate.IsEmpty);
            Assert.IsTrue(brain.IsEvading);
            Assert.AreEqual(Identity.None, victim.FightingTarget);
        }

        [TestMethod]
        public void TauntAddsThreatWithoutStartingOrRetargetingAFight()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player other = world.Player(3, 1, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);

            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.AreEqual(Identity.None, victim.FightingTarget);
            Assert.IsNull(brain.ResolveCurrentTarget());

            brain.AddThreat(other.Identity, 10);
            brain.SetCurrentTarget(other.Identity);
            victim.SetFightingTarget(other.Identity);
            Assert.IsTrue(UseTaunt(caster, victim, items, amount: 500));
            Assert.AreEqual(1000f, brain.Hate.ThreatOf(caster.Identity));
            Assert.AreEqual(other.Identity, victim.FightingTarget);
            Assert.AreSame(other, brain.ResolveCurrentTarget());
        }

        [TestMethod]
        public void TauntRejectsMissingSelfAndNonPlayerSourcesAndTargetsWithoutBrains()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);
            NpcCharacter otherNpc = world.Npc(3, 6, 0, 0, attackable: true);
            NpcCharacter npcOwnedPet = world.Npc(4, 7, 0, 0, attackable: true);
            npcOwnedPet.BindPet(new PetController(otherNpc, type: 1, hash: "synthetic-pet", level: 60, expiresUtc: null));

            Assert.IsFalse(UseTaunt(null, victim, items, amount: 500));
            Assert.IsFalse(UseTaunt(victim, victim, items, amount: 500));
            Assert.IsFalse(UseTaunt(otherNpc, victim, items, amount: 500));
            Assert.IsFalse(UseTaunt(npcOwnedPet, victim, items, amount: 500));
            Assert.IsFalse(UseTaunt(caster, caster, items, amount: 500));
            Assert.IsFalse(UseTaunt(caster, otherNpc, items, amount: 500));
            Assert.IsTrue(brain.Hate.IsEmpty);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(-500)]
        public void TauntRejectsZeroAndNegativeAmountsWithoutChangingExistingHate(int amount)
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);
            brain.AddThreat(caster.Identity, 75);

            Assert.IsFalse(UseTaunt(caster, victim, items, amount));
            Assert.AreEqual(75f, brain.Hate.ThreatOf(caster.Identity));
            Assert.AreEqual(1, brain.Hate.Count);
        }

        [TestMethod]
        public void TauntRejectsMissingAndNonNumericAmounts()
        {
            var items = new StubItemBuilder();
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter victim = world.Npc(2, 5, 0, 0, attackable: true);
            NpcBrain brain = NpcBrain.Create(victim);

            Assert.IsFalse(UseTauntSpell(caster, victim, items,
                new ItemSpell { FunctionType = (int)FunctionType.TauntNpc, Target = (int)ItemTarget.Target }));
            Assert.IsFalse(UseTauntSpell(caster, victim, items,
                new ItemSpell
                {
                    FunctionType = (int)FunctionType.TauntNpc,
                    Target = (int)ItemTarget.Target,
                    Arguments = ["invalid-amount"]
                }));
            Assert.IsTrue(brain.Hate.IsEmpty);
        }

        [TestMethod]
        public void HostileAreaCastWithNoCanFlagsHitsAroundTheTargetNotTheCaster()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId,
                can: 0,
                flags: NanoFlags.IsHostile));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 6, 0, 0);
            NpcCharacter center = world.Npc(2, 0, 0, 0, attackable: true);
            NpcCharacter nearCenter = world.Npc(3, 3, 0, 0, attackable: true);
            NpcCharacter nearSafe = world.Npc(4, 2, 0, 0, attackable: false);
            NpcCharacter outside = world.Npc(5, 16, 0, 0, attackable: true);
            Player bystander = world.Player(6, 4, 0, 0);

            Assert.IsTrue(UseAreaOn(caster, center, items, HostileId, radius: 10));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(1, center.Buffs.Count);
            Assert.AreEqual(1, nearCenter.Buffs.Count);
            Assert.AreEqual(0, nearSafe.Buffs.Count);
            Assert.AreEqual(0, outside.Buffs.Count);
            Assert.AreEqual(0, bystander.Buffs.Count);
        }

        [TestMethod]
        public void HostileAreaCastOnSelfHitsNearbyAttackableNotTheCaster()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId,
                can: 0,
                flags: NanoFlags.IsHostile));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter near = world.Npc(2, 4, 0, 0, attackable: true);

            Assert.IsTrue(UseAreaOn(caster, caster, items, HostileId, radius: 10));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(1, near.Buffs.Count);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void HostileAreaCastSparesOwnTeamAndRaidPetsButStillCatchesUnrelatedPets(bool raid)
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId, can: 0, flags: NanoFlags.IsHostile, modifiers: [Taunt(500)]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player teammate = world.Player(2, 1, 0, 0);
            Player thirdMember = world.Player(3, 2, 0, 0);
            Player unrelatedOwner = world.Player(4, 3, 0, 0);
            world.Join(caster, teammate);
            world.Join(caster, thirdMember);
            if (raid)
            {
                TeamService teams = world.Playfield.GetRequiredService<TeamService>();
                Assert.IsTrue(teams.ConvertToRaid(caster));
                Assert.IsTrue(teams.MoveRaidMember(caster, thirdMember.Identity.Instance, 1));
            }

            NpcCharacter ownPet = world.Npc(10, 4, 0, 0, attackable: true);
            NpcCharacter teamPet = world.Npc(11, 5, 0, 0, attackable: true);
            NpcCharacter thirdPet = world.Npc(12, 6, 0, 0, attackable: true);
            NpcCharacter unrelatedPet = world.Npc(13, 7, 0, 0, attackable: true);
            NpcCharacter ordinaryNpc = world.Npc(14, 8, 0, 0, attackable: true);
            ownPet.BindPet(new PetController(caster, 1, "synthetic-pet", 60, null));
            teamPet.BindPet(new PetController(teammate, 1, "synthetic-pet", 60, null));
            thirdPet.BindPet(new PetController(thirdMember, 1, "synthetic-pet", 60, null));
            unrelatedPet.BindPet(new PetController(unrelatedOwner, 1, "synthetic-pet", 60, null));
            foreach (NpcCharacter npc in new[] { ownPet, teamPet, thirdPet, unrelatedPet, ordinaryNpc })
                NpcBrain.Create(npc);

            Assert.IsTrue(UseAreaOn(caster, caster, items, HostileId, radius: 20));

            foreach (NpcCharacter pet in new[] { ownPet, teamPet, thirdPet })
            {
                Assert.IsFalse(pet.TryGetBuff(HostileId, out _));
                Assert.IsTrue(pet.Brain!.Hate.IsEmpty);
            }
            foreach (NpcCharacter npc in new[] { unrelatedPet, ordinaryNpc })
            {
                Assert.IsTrue(npc.TryGetBuff(HostileId, out _));
                Assert.IsTrue(npc.Brain!.Hate.ThreatOf(caster.Identity) >= 500f);
            }
        }

        [TestMethod]
        public void HostileAreaFromPetUsesItsOwnersPetAndTeamAffiliation()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId, can: 0, flags: NanoFlags.IsHostile, modifiers: [Taunt(500)]));
            using var world = new FanoutWorld(items);
            Player owner = world.Player(1, 0, 0, 0);
            Player teammate = world.Player(2, 1, 0, 0);
            Player unrelatedOwner = world.Player(3, 2, 0, 0);
            world.Join(owner, teammate);
            NpcCharacter caster = world.Npc(10, 3, 0, 0, attackable: true);
            NpcCharacter siblingPet = world.Npc(11, 4, 0, 0, attackable: true);
            NpcCharacter teamPet = world.Npc(12, 5, 0, 0, attackable: true);
            NpcCharacter unrelatedPet = world.Npc(13, 6, 0, 0, attackable: true);
            caster.BindPet(new PetController(owner, 1, "synthetic-pet", 60, null));
            siblingPet.BindPet(new PetController(owner, 1, "synthetic-pet", 60, null));
            teamPet.BindPet(new PetController(teammate, 1, "synthetic-pet", 60, null));
            unrelatedPet.BindPet(new PetController(unrelatedOwner, 1, "synthetic-pet", 60, null));
            caster.Stats.Set(CharacterStat.PetMaster, owner.Identity.Instance);
            siblingPet.Stats.Set(CharacterStat.PetMaster, owner.Identity.Instance);
            teamPet.Stats.Set(CharacterStat.PetMaster, teammate.Identity.Instance);
            unrelatedPet.Stats.Set(CharacterStat.PetMaster, unrelatedOwner.Identity.Instance);
            foreach (NpcCharacter pet in new[] { siblingPet, teamPet, unrelatedPet })
                NpcBrain.Create(pet);

            Assert.IsTrue(UseAreaOn(caster, caster, items, HostileId, radius: 20));

            Assert.IsFalse(siblingPet.TryGetBuff(HostileId, out _));
            Assert.IsFalse(teamPet.TryGetBuff(HostileId, out _));
            Assert.IsTrue(siblingPet.Brain!.Hate.IsEmpty);
            Assert.IsTrue(teamPet.Brain!.Hate.IsEmpty);
            Assert.IsTrue(unrelatedPet.TryGetBuff(HostileId, out _));
            Assert.IsTrue(unrelatedPet.Brain!.Hate.ThreatOf(caster.Identity) >= 500f);
            Assert.AreEqual(0f, unrelatedPet.Brain.Hate.ThreatOf(owner.Identity));
        }

        [TestMethod]
        public void FriendlyAreaBuffStillLandsOnOwnAndTeamPets()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player teammate = world.Player(2, 1, 0, 0);
            world.Join(caster, teammate);
            NpcCharacter ownPet = world.Npc(10, 2, 0, 0, attackable: true);
            NpcCharacter teamPet = world.Npc(11, 3, 0, 0, attackable: true);
            ownPet.BindPet(new PetController(caster, 1, "synthetic-pet", 60, null));
            teamPet.BindPet(new PetController(teammate, 1, "synthetic-pet", 60, null));

            Assert.IsTrue(UseAreaOn(caster, caster, items, BuffId, radius: 20));

            Assert.IsTrue(caster.TryGetBuff(BuffId, out _));
            Assert.IsTrue(ownPet.TryGetBuff(BuffId, out _));
            Assert.IsTrue(teamPet.TryGetBuff(BuffId, out _));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(6000)]
        public void DirectHostileAreaRejectsFriendlyPetBeforePacketsBuffOrTauntButAllowsGmOverride(int duration)
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(
                HostileId, durationCentiseconds: duration, can: 0, flags: NanoFlags.IsHostile,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.TauntNpc,
                        Target = (int)ItemTarget.User,
                        Arguments = [500]
                    }
                ]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter ownPet = world.Npc(10, 2, 0, 0, attackable: true);
            NpcCharacter ordinaryNpc = world.Npc(11, 3, 0, 0, attackable: true);
            ownPet.BindPet(new PetController(caster, 1, "synthetic-pet", 60, null));
            NpcBrain.Create(ownPet);
            NpcBrain.Create(ordinaryNpc);
            Cell cell = new CellGrid(null, visibilityNeighborLevel: 0).ResolveCell(default);
            foreach (Character character in new Character[] { caster, ownPet, ordinaryNpc })
            {
                character.Cell = cell;
                cell.Add(character);
            }
            var session = (InventoryActionTests.Session)caster.Session!;

            // The same cell and session observe both packets for an eligible target.
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(
                caster, ordinaryNpc, HostileId, items, new SilentInventory(), DateTime.UtcNow, areaSpread: true));
            Assert.IsTrue(session.Messages.OfType<CastNanoSpellMessage>().Any(m => m.Target == ordinaryNpc.Identity));
            Assert.IsTrue(session.Messages.OfType<CharacterActionMessage>().Any(m =>
                m.Action == CharacterActionType.FinishNanoCasting && m.Parameter2 == HostileId));
            Assert.IsTrue(ordinaryNpc.Brain!.Hate.ThreatOf(caster.Identity) >= 500f);
            session.Messages.Clear();

            Assert.IsFalse(NanoRuntime.TryApplyImmediate(
                caster, ownPet, HostileId, items, new SilentInventory(), DateTime.UtcNow, areaSpread: true));

            Assert.AreEqual(0, session.Messages.Count);
            Assert.IsFalse(ownPet.TryGetBuff(HostileId, out _));
            Assert.IsTrue(ownPet.Brain!.Hate.IsEmpty);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(
                caster, ownPet, HostileId, items, new SilentInventory(), DateTime.UtcNow,
                gmApply: true, areaSpread: true));
            Assert.IsTrue(session.Messages.OfType<CastNanoSpellMessage>().Any(m => m.Target == ownPet.Identity));
            Assert.IsTrue(session.Messages.OfType<CharacterActionMessage>().Any(m =>
                m.Action == CharacterActionType.FinishNanoCasting && m.Parameter2 == HostileId));
            Assert.AreEqual(duration > 0, ownPet.TryGetBuff(HostileId, out _));
        }

        [TestMethod]
        public void TeamCastNanoLandsOnTeammatesAndSoloSelf()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            using var world = new FanoutWorld(items);
            Player a = world.Player(1, 0, 0, 0);
            Player b = world.Player(2, 10, 0, 0);
            world.Join(a, b);

            Assert.IsTrue(UseCast(a, items, FunctionType.TeamCastNano, BuffId));
            Assert.AreEqual(1, a.Buffs.Count);
            Assert.AreEqual(1, b.Buffs.Count);

            Player solo = ReadyPlayer(9);
            Assert.IsTrue(UseCast(solo, items, FunctionType.TeamCastNano, BuffId));
            Assert.AreEqual(1, solo.Buffs.Count);
        }

        [TestMethod]
        public void TeamCastNanoAppliesHostileFlaggedExperienceBonusToTeammates()
        {
            const int leadership = 225573;
            NanoFlags leadershipFlags = NanoFlags.IsBuff | NanoFlags.IsHostile | NanoFlags.NotRemovable
                | NanoFlags.NoResistCannotFumble | NanoFlags.NoTimerNotify;
            var bonus = new ItemSpell
            {
                FunctionType = (int)FunctionType.Modify,
                Target = (int)ItemTarget.Wearer,
                Arguments = [(int)CharacterStat.XPModifier, 50]
            };
            var debuff = new ItemSpell
            {
                FunctionType = (int)FunctionType.Modify,
                Target = (int)ItemTarget.Wearer,
                Arguments = [(int)CharacterStat.XPModifier, -50]
            };
            var items = new StubItemBuilder()
                .Add(TestNanos.Create(leadership, durationCentiseconds: 4500, ncuCost: 999, strain: 611,
                    can: 0, flags: leadershipFlags, modifiers: [bonus]))
                .Add(TestNanos.Create(HostileId, can: 0, flags: leadershipFlags, modifiers: [debuff]));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player mate = world.Player(2, 10, 0, 0);
            Player bystander = world.Player(3, 5, 0, 0);
            world.Join(caster, mate);
            world.MakeVisible(caster, mate, bystander);

            TeamSnapshot? team = world.Playfield.GetRequiredService<TeamService>().GetImmediateTeam(caster);
            Assert.IsNotNull(team);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, team.MemberIds.ToArray());
            Assert.IsTrue(world.Playfield.GetRequiredService<PlayfieldLocality>().IsVisibleTo(mate, caster));
            mate.Stats.Set(CharacterStat.XPModifier, 0);
            Assert.IsFalse(NanoRuntime.TryApplyImmediate(
                caster, mate, leadership, items, new SilentInventory(), DateTime.UtcNow));
            Assert.IsTrue(UseCast(caster, items, FunctionType.TeamCastNano, HostileId));
            Assert.IsFalse(mate.TryGetBuff(HostileId, out _), "An actual debuff must still be rejected on a teammate.");
            Assert.IsTrue(UseCast(caster, items, FunctionType.TeamCastNano, leadership));
            Assert.IsTrue(caster.TryGetBuff(leadership, out _));
            Assert.IsFalse(bystander.TryGetBuff(leadership, out _));
            Assert.IsTrue(mate.TryGetBuff(leadership, out Buff? applied), "Leadership must reach a visible teammate.");
            Assert.IsNotNull(applied);
            Assert.AreEqual(4500, applied.DurationCentiseconds);
            Assert.AreEqual(0, mate.UsedNcu);
            Assert.IsFalse(applied.CanCancel);
            mate.RebaseStats();
            Assert.AreEqual(50, mate.Stats.GetOrZero(CharacterStat.XPModifier));
        }

        [TestMethod]
        public void TeamCastNanoChecksChildLevelPerRecipient()
        {
            NanoSpell restricted = Timed(BuffId);
            restricted.Actions.Add(new ItemAction
            {
                ActionType = (int)ActionType.ToUse,
                Requirements =
                [
                    new ItemRequirement
                    {
                        Target = (int)ItemTarget.Self,
                        Operator = (int)Operator.GreaterThan,
                        StatNumber = (int)CharacterStat.Level,
                        Value = 24
                    }
                ]
            });
            var items = new StubItemBuilder().Add(restricted).Add(Timed(ChildId));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player ineligible = world.Player(2, 10, 0, 0);
            Player eligible = world.Player(3, 5, 0, 0);
            caster.Stats.Set(CharacterStat.Level, 25);
            ineligible.Stats.Set(CharacterStat.Level, 24);
            eligible.Stats.Set(CharacterStat.Level, 25);
            world.Join(caster, ineligible);
            world.Join(caster, eligible);
            world.MakeVisible(caster, ineligible, eligible);

            TeamSnapshot? team = world.Playfield.GetRequiredService<TeamService>().GetImmediateTeam(caster);
            Assert.IsNotNull(team);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, team.MemberIds.ToArray());
            Assert.IsTrue(world.Playfield.GetRequiredService<PlayfieldLocality>().IsVisibleTo(ineligible, caster));
            Assert.IsTrue(UseCast(caster, items, FunctionType.TeamCastNano, ChildId));
            Assert.IsTrue(ineligible.TryGetBuff(ChildId, out _), "An unrestricted child must reach the lower-level teammate.");
            Assert.IsTrue(UseCast(caster, items, FunctionType.TeamCastNano, BuffId));
            Assert.IsTrue(caster.TryGetBuff(BuffId, out _));
            Assert.IsTrue(eligible.TryGetBuff(BuffId, out _));
            Assert.IsFalse(ineligible.TryGetBuff(BuffId, out _), "Level 24 must not receive a child that requires level 25.");
        }

        [TestMethod]
        public void PeriodicTeamCastNanoRefreshesTheCasterAndTeamAtTheConfiguredIntervalAndCount()
        {
            var refresh = new ItemSpell
            {
                FunctionType = (int)FunctionType.TeamCastNano,
                Target = (int)ItemTarget.Wearer,
                Arguments = [ChildId],
                TickCount = 3,
                TickInterval = 100
            };
            var items = new StubItemBuilder()
                .Add(TestNanos.Create(BuffId, modifiers: [refresh]))
                .Add(TestNanos.Create(ChildId, durationCentiseconds: 1000));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player mate = world.Player(2, 10, 0, 0);
            Player bystander = world.Player(3, 5, 0, 0);
            world.Join(caster, mate);
            DateTime start = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, caster, BuffId, items, new SilentInventory(), start));
            Buff firstCaster = RequireBuff(caster, ChildId);
            Buff firstMate = RequireBuff(mate, ChildId);
            NanoRuntime.Tick(caster, start.AddMilliseconds(999));
            Assert.AreSame(firstCaster, RequireBuff(caster, ChildId));
            Assert.AreSame(firstMate, RequireBuff(mate, ChildId));

            NanoRuntime.Tick(caster, start.AddSeconds(1));
            Buff secondCaster = RequireBuff(caster, ChildId);
            Buff secondMate = RequireBuff(mate, ChildId);
            Assert.AreNotSame(firstCaster, secondCaster);
            Assert.AreNotSame(firstMate, secondMate);
            Assert.AreEqual(caster.Identity, secondCaster.Source);
            Assert.AreEqual(caster.Identity, secondMate.Source);

            NanoRuntime.Tick(caster, start.AddSeconds(2));
            Buff thirdCaster = RequireBuff(caster, ChildId);
            Buff thirdMate = RequireBuff(mate, ChildId);
            Assert.AreNotSame(secondCaster, thirdCaster);
            Assert.AreNotSame(secondMate, thirdMate);
            NanoRuntime.Tick(caster, start.AddSeconds(3));
            NanoRuntime.Tick(mate, start.AddSeconds(3));

            Assert.AreSame(thirdCaster, RequireBuff(caster, ChildId));
            Assert.AreSame(thirdMate, RequireBuff(mate, ChildId));
            Assert.AreEqual(2, caster.Buffs.Count);
            Assert.AreEqual(1, mate.Buffs.Count);
            Assert.AreEqual(20, caster.UsedNcu);
            Assert.AreEqual(10, mate.UsedNcu);
            Assert.AreEqual(0, bystander.Buffs.Count);
        }

        [TestMethod]
        public void CancellingPeriodicTeamAuraStopsRefreshAndLetsTheExistingTeamChildrenExpire()
        {
            var refresh = new ItemSpell
            {
                FunctionType = (int)FunctionType.TeamCastNano,
                Target = (int)ItemTarget.Wearer,
                Arguments = [ChildId],
                TickCount = 10,
                TickInterval = 100
            };
            var items = new StubItemBuilder()
                .Add(TestNanos.Create(BuffId, modifiers: [refresh]))
                .Add(TestNanos.Create(ChildId, durationCentiseconds: 1000));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            Player mate = world.Player(2, 10, 0, 0);
            world.Join(caster, mate);
            DateTime start = DateTime.UtcNow;
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, caster, BuffId, items, new SilentInventory(), start));
            NanoRuntime.Tick(caster, start.AddSeconds(1));
            Buff casterChild = RequireBuff(caster, ChildId);
            Buff mateChild = RequireBuff(mate, ChildId);

            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(caster, BuffId));
            NanoRuntime.Tick(caster, start.AddSeconds(2));
            NanoRuntime.Tick(mate, start.AddSeconds(2));
            Assert.AreSame(casterChild, RequireBuff(caster, ChildId));
            Assert.AreSame(mateChild, RequireBuff(mate, ChildId));
            Assert.AreEqual(1, caster.Buffs.Count);
            Assert.AreEqual(1, mate.Buffs.Count);
            Assert.AreEqual(10, caster.UsedNcu);
            Assert.AreEqual(10, mate.UsedNcu);

            DateTime bothExpired = casterChild.ExpiresAtUtc > mateChild.ExpiresAtUtc
                ? casterChild.ExpiresAtUtc : mateChild.ExpiresAtUtc;
            NanoRuntime.Tick(caster, bothExpired);
            NanoRuntime.Tick(mate, bothExpired);
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(0, mate.Buffs.Count);
            Assert.AreEqual(0, caster.UsedNcu);
            Assert.AreEqual(0, mate.UsedNcu);
            NanoRuntime.Tick(caster, bothExpired.AddMinutes(1));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(0, mate.Buffs.Count);
        }

        [TestMethod]
        public void PeriodicAreaCastNanoReevaluatesRangeAndKeepsCasterAttribution()
        {
            var refresh = new ItemSpell
            {
                FunctionType = (int)FunctionType.AreaCastNano,
                Target = (int)ItemTarget.Wearer,
                Arguments = [HostileId, 10],
                TickCount = 3,
                TickInterval = 100
            };
            var items = new StubItemBuilder()
                .Add(TestNanos.Create(BuffId, modifiers: [refresh]))
                .Add(TestNanos.Create(HostileId, durationCentiseconds: 1000, can: 0, flags: NanoFlags.IsHostile));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter near = world.Npc(2, 5, 0, 0, attackable: true);
            NpcCharacter edge = world.Npc(3, 10, 0, 0, attackable: true);
            NpcCharacter outside = world.Npc(4, 11, 0, 0, attackable: true);
            NpcCharacter safe = world.Npc(5, 3, 0, 0, attackable: false);
            DateTime start = DateTime.UtcNow;
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, caster, BuffId, items, new SilentInventory(), start));
            Buff firstNear = RequireBuff(near, HostileId);
            Buff firstEdge = RequireBuff(edge, HostileId);
            Assert.AreEqual(caster.Identity, firstNear.Source);
            Assert.AreEqual(caster.Identity, firstEdge.Source);
            Assert.IsFalse(outside.TryGetBuff(HostileId, out _));

            near.Position = new Vector3(25, 0, 0);
            outside.Position = new Vector3(4, 0, 0);
            NanoRuntime.Tick(caster, start.AddMilliseconds(999));
            Assert.IsFalse(outside.TryGetBuff(HostileId, out _));
            NanoRuntime.Tick(caster, start.AddSeconds(1));
            Buff secondOutside = RequireBuff(outside, HostileId);
            Assert.AreEqual(caster.Identity, secondOutside.Source);
            Assert.AreSame(firstNear, RequireBuff(near, HostileId));
            Assert.AreNotSame(firstEdge, RequireBuff(edge, HostileId));

            near.Position = new Vector3(5, 0, 0);
            NanoRuntime.Tick(caster, start.AddSeconds(2));
            Buff thirdNear = RequireBuff(near, HostileId);
            Buff thirdOutside = RequireBuff(outside, HostileId);
            Assert.AreNotSame(firstNear, thirdNear);
            Assert.AreNotSame(secondOutside, thirdOutside);
            Assert.AreEqual(caster.Identity, thirdNear.Source);
            NanoRuntime.Tick(caster, start.AddSeconds(3));

            Assert.AreSame(thirdNear, RequireBuff(near, HostileId));
            Assert.AreSame(thirdOutside, RequireBuff(outside, HostileId));
            Assert.IsFalse(caster.TryGetBuff(HostileId, out _));
            Assert.IsFalse(safe.TryGetBuff(HostileId, out _));
            Assert.AreEqual(1, near.Buffs.Count);
            Assert.AreEqual(1, edge.Buffs.Count);
            Assert.AreEqual(1, outside.Buffs.Count);
            Assert.AreEqual(0, safe.Buffs.Count);
        }

        [TestMethod]
        public void PlayfieldNanoStaysOnTheCurrentPlayfield()
        {
            CanFlags can = CanFlags.ApplyOnSelf | CanFlags.ApplyOnFriendly | CanFlags.ApplyOnHostile;
            var items = new StubItemBuilder().Add(TestNanos.Create(BuffId, can: can));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter local = world.Npc(2, 8, 0, 0, attackable: true);
            using var other = new FanoutWorld(items);
            Player stranger = other.Player(3, 0, 0, 0);

            Assert.IsTrue(UseCast(caster, items, FunctionType.PlayfieldNano, BuffId));
            Assert.AreEqual(1, caster.Buffs.Count);
            Assert.AreEqual(1, local.Buffs.Count);
            Assert.AreEqual(0, stranger.Buffs.Count);
        }

        [TestMethod]
        public void WearCastNanoLandsNpcControllerBuffThatExceedsMaxNcu()
        {
            NanoSpell controller = TestNanos.Create(205606, ncuCost: 999, durationCentiseconds: 72000000);
            var items = new StubItemBuilder().Add(controller);
            Item gear = WearItem(Cast(FunctionType.CastNano, 205606));
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 81 }, items);
            npc.Stats.Set(CharacterStat.MaxNCU, 81);
            npc.Equipment.Add(0, gear);

            WearCastNano.ApplyContainer(npc, npc.Equipment, includeWield: true, items, new SilentInventory());
            Assert.AreEqual(1, npc.Buffs.Count);
            Assert.AreEqual(205606, npc.Buffs[0].Id);
        }

        [TestMethod]
        public void PlayerWearCastNanoStillRefusesWhenNcuDoesNotFit()
        {
            NanoSpell controller = TestNanos.Create(205606, ncuCost: 999, durationCentiseconds: 72000000);
            var items = new StubItemBuilder().Add(controller);
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 81);
            Item gear = WearItem(Cast(FunctionType.CastNano, 205606));

            WearCastNano.ApplyItem(player, gear, includeWield: false, items, new SilentInventory());
            Assert.AreEqual(0, player.Buffs.Count);
        }

        [TestMethod]
        public void WearCastNanoAppliesOnceAndRebaseDoesNotRecast()
        {
            NanoSpell nano = Timed(BuffId);
            var items = new StubItemBuilder().Add(nano);
            Item gear = WearItem(Cast(FunctionType.CastNano, BuffId));
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 80 }, items);
            npc.Stats.Set(CharacterStat.MaxNCU, 60);
            npc.Equipment.Add(0, gear);

            WearCastNano.ApplyContainer(npc, npc.Equipment, includeWield: true, items, new SilentInventory());
            npc.Rebase();
            Assert.AreEqual(1, npc.Buffs.Count);
            npc.RebaseStats();
            Assert.AreEqual(1, npc.Buffs.Count);
        }

        [TestMethod]
        public void PlayerHydrationRebaseDoesNotCastWearNanos()
        {
            NanoSpell nano = Timed(BuffId);
            var items = new StubItemBuilder().Add(nano);
            Player player = ReadyPlayer(1);
            Item gear = WearItem(Cast(FunctionType.CastNano, BuffId));
            Assert.IsTrue(player.Inventory.Armor.Add(0x11, gear));

            player.Rebase();
            Assert.AreEqual(0, player.Buffs.Count);

            WearCastNano.ApplyItem(player, gear, includeWield: false, items, new SilentInventory());
            Assert.AreEqual(1, player.Buffs.Count);
            player.RebaseStats();
            Assert.AreEqual(1, player.Buffs.Count);
        }

        static Buff RequireBuff(Character character, int nanoId)
        {
            Assert.IsTrue(character.TryGetBuff(nanoId, out Buff? buff));
            Assert.IsNotNull(buff);
            return buff;
        }

        static ItemSpell Taunt(int amount, params ItemRequirement[] requirements)
            => new()
            {
                FunctionType = (int)FunctionType.TauntNpc,
                Target = (int)ItemTarget.Target,
                Arguments = [amount],
                Requirements = [new ItemRequirement { Operator = (int)Operator.OnCaster }, .. requirements]
            };

        static ItemRequirement CasterSkill(Operator comparison, int value)
            => new()
            {
                Operator = (int)comparison,
                StatNumber = (int)CharacterStat.PsychologicalModification,
                Value = value
            };

        static bool UseTaunt(Character? source, Character target, StubItemBuilder items, int amount)
            => UseTauntSpell(source, target, items, Taunt(amount));

        static bool UseTauntSpell(Character? source, Character target, StubItemBuilder items, ItemSpell spell)
        {
            var template = new ItemTemplate
            {
                Id = 9002,
                SpellList = new Dictionary<EventType, List<ItemSpell>> { [EventType.OnUse] = [spell] }
            };
            return template.ExecuteOnUseSpells(target, new SilentInventory(), items, source: source);
        }

        static bool UseCast(Player player, StubItemBuilder items, FunctionType function, int nanoId, int radius = 0)
        {
            var arguments = new List<object> { nanoId };
            if (function == FunctionType.AreaCastNano)
                arguments.Add(radius);

            var template = new ItemTemplate
            {
                Id = 9000,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] = [new ItemSpell { FunctionType = (int)function, Arguments = arguments }]
                }
            };
            return template.ExecuteOnUseSpells(player, new SilentInventory(), items);
        }

        static bool UseAreaOn(Character caster, Character center, StubItemBuilder items, int nanoId, int radius)
        {
            var template = new ItemTemplate
            {
                Id = 9001,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.AreaCastNano,
                            Target = (int)ItemTarget.Target,
                            Arguments = [nanoId, radius]
                        }
                    ]
                }
            };
            return template.ExecuteOnUseSpells(center, new SilentInventory(), items, source: caster);
        }

        static Player ReadyPlayer(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            return player;
        }

        static NanoSpell Timed(int id)
            => TestNanos.Create(id);

        static NanoSpell InstantHit(int id, int heal)
            => TestNanos.Create(
                id,
                durationCentiseconds: 0,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, heal, heal]
                    }
                ]);

        static ItemSpell Cast(FunctionType function, int nanoId)
            => new()
            {
                FunctionType = (int)function,
                Arguments = [nanoId]
            };

        static Item WearItem(ItemSpell spell)
        {
            Item item = TestWorld.CreateItem(lowId: 3000, highId: 3000, name: "WearCast");
            item.Definition.SpellList[EventType.OnWear] = [spell];
            return item;
        }

        sealed class FanoutWorld : IDisposable
        {
            readonly ServiceProvider _services;
            readonly DynelRegistry _registry;
            readonly TeamService _teams = new(dispatchOnOwner: (_, action) => action());
            readonly PlayfieldManager _manager;

            public Playfield Playfield { get; }

            public FanoutWorld(StubItemBuilder items)
            {
                _registry = new DynelRegistry();
                _manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_manager, new Dictionary<int, Player>());

                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                typeof(Playfield).GetField("<FightModes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, DistrictFightModes.None);
                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton(_teams)
                    .AddSingleton(_manager)
                    .AddSingleton(new PlayfieldLocality(1, metaData: null))
                    .AddSingleton<WorldSimulationAccess>()
                    .AddSingleton<IItemBuilder>(items)
                    .AddSingleton<IInventoryRepository>(new SilentInventory())
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);
                typeof(Playfield).GetField("_lifecycleSync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new Lock());
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new ConcurrentDictionary<Character, byte>());
            }

            public Player Player(int id, double x, double y, double z)
            {
                Player player = ReadyPlayer(id);
                player.Stats.Set(CharacterStat.Level, 60);
                player.Stats.Set(CharacterStat.Profession, 3);
                var session = new InventoryActionTests.Session();
                session.BindPlayer(player);
                player.Session = session;
                player.Playfield = Playfield;
                player.Position = new Vector3(x, y, z);
                _registry.Register(player);
                _manager.RegisterPlayer(player);
                _teams.AttachPlayer(player);
                return player;
            }

            public NpcCharacter Npc(int id, double x, double y, double z, bool attackable)
            {
                var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = id }, new StubItemBuilder())
                {
                    Playfield = Playfield,
                    Position = new Vector3(x, y, z),
                    Attackable = attackable
                };
                npc.Stats.Set(CharacterStat.MaxNCU, 60);
                _registry.Register(npc);
                return npc;
            }

            public void Join(Player inviter, Player invitee)
            {
                _teams.TryHandle(inviter, new CharacterActionMessage
                {
                    Identity = inviter.Identity,
                    Action = CharacterActionType.TeamRequestInvite,
                    Target = invitee.Identity
                });
                _teams.TryHandle(invitee, new CharacterActionMessage
                {
                    Identity = invitee.Identity,
                    Action = CharacterActionType.TeamRequestReply,
                    Target = inviter.Identity,
                    Parameter2 = 1
                });
            }

            public void MakeVisible(params Player[] players)
            {
                PlayfieldLocality locality = _services.GetRequiredService<PlayfieldLocality>();
                foreach (Player player in players)
                    locality.RegisterDynel(player);
                foreach (Player player in players)
                    locality.ActivatePlayerVisibility(player);
            }

            public void Dispose() => _services.Dispose();
        }

        sealed class SilentInventory : IInventoryRepository
        {
            public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId) => [];
            public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId) => [];
            public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId) => [];
            public int LeaseInstanceIdBlock(int count) => 1;
            public ItemInstanceRecord Insert(ItemInstanceRecord item) => item;
            public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement) { }
            public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations) { }
            public void PersistNewAndUpdateLocations(
                IReadOnlyList<ItemInstanceRecord> inserts,
                IReadOnlyList<ItemLocationUpdate> updates)
            {
            }
        }
    }
}
