namespace ZoneEngine_New.Tests
{
    using System;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;

    [TestClass]
    public sealed class CombatRulesTests
    {
        [TestMethod]
        public void PlayerCannotAttackAnotherPlayerUntilPvpIsEnabled()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);

            Assert.IsFalse(CombatRules.CanAttack(attacker, target));
            Assert.IsFalse(CombatRules.IsInRestrictedGas(target));

            Grant(target, ActionRestrictionFlags.PvPEnabled);
            Assert.IsTrue(CombatRules.CanAttack(attacker, target));
            Assert.IsFalse(CombatRules.IsPvpAttackBlocked(attacker, target));
        }

        [TestMethod]
        public void UnflaggedPlayerTargetIsAPvpBlock()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 3 }, new StubItemBuilder())
            {
                Attackable = false
            };

            Assert.IsTrue(CombatRules.IsPvpAttackBlocked(attacker, target));
            Assert.IsFalse(CombatRules.IsPvpAttackBlocked(attacker, npc));
            Assert.IsFalse(CombatRules.IsPvpAttackBlocked(npc, target));
        }

        [TestMethod]
        public void TowerPvpFlagAlsoMakesAPlayerAttackable()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            Player target = TestWorld.CreatePlayer(2);
            Grant(target, ActionRestrictionFlags.PvPEnabled_Tower);

            Assert.IsTrue(CombatRules.CanAttack(attacker, target));
        }

        [TestMethod]
        public void NpcCanAttackAPlayerWhoIsNotPvpEnabled()
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 3 }, new StubItemBuilder());
            Player target = TestWorld.CreatePlayer(2);

            Assert.IsTrue(CombatRules.CanAttack(npc, target));
        }

        [TestMethod]
        public void UnattackableNpcCannotBeAttacked()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 4 }, new StubItemBuilder())
            {
                Attackable = false
            };
            var attackable = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 5 }, new StubItemBuilder());

            Assert.IsFalse(CombatRules.CanAttack(attacker, npc));
            Assert.IsTrue(CombatRules.CanAttack(attacker, attackable));
        }

        [TestMethod]
        public void RebaseCollectsPvpFlagsFromChangeActionRestrictionBuffs()
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 60);

            Assert.AreEqual(BuffApplyDecision.Apply, Apply(player, 214879, (int)ActionRestrictionFlags.PvPEnabled));
            Assert.AreEqual(ActionRestrictionFlags.PvPEnabled, player.ActionRestrictionFlags);

            Assert.AreEqual(BuffApplyDecision.Apply, Apply(player, 202732, (int)ActionRestrictionFlags.PvPEnabled_Tower));
            Assert.AreEqual(
                ActionRestrictionFlags.PvPEnabled | ActionRestrictionFlags.PvPEnabled_Tower,
                player.ActionRestrictionFlags);

            Assert.AreEqual(BuffRemovalOutcome.Removed, player.TryRemoveBuff(214879, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(ActionRestrictionFlags.PvPEnabled_Tower, player.ActionRestrictionFlags);

            Assert.AreEqual(BuffRemovalOutcome.Removed, player.TryRemoveBuff(202732, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(ActionRestrictionFlags.None, player.ActionRestrictionFlags);
        }

        [TestMethod]
        public void CrossCharacterDamageUsesTheAttackCheck()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            Player peaceful = TestWorld.CreatePlayer(2);
            peaceful.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);
            var safe = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 8 }, new StubItemBuilder())
            {
                Attackable = false
            };
            safe.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);
            var mob = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 9 }, new StubItemBuilder());
            mob.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);

            Assert.IsFalse(peaceful.ApplyDamage(attacker, 25, HitType.Normal));
            Assert.AreEqual(100, peaceful.Stats.GetOrZero(CharacterStat.Health));
            Assert.IsFalse(safe.ApplyDamage(attacker, 25, HitType.Normal));
            Assert.AreEqual(100, safe.Stats.GetOrZero(CharacterStat.Health));

            Assert.IsFalse(mob.ApplyDamage(attacker, 25, HitType.Normal));
            Assert.AreEqual(75, mob.Stats.GetOrZero(CharacterStat.Health));

            var wolf = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 10 }, new StubItemBuilder());
            Assert.IsFalse(peaceful.ApplyDamage(wolf, 10, HitType.Normal));
            Assert.AreEqual(90, peaceful.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void HostileNanoDoesNotLandOnAnIllegalTarget()
        {
            Player attacker = TestWorld.CreatePlayer(1);
            Player peaceful = TestWorld.CreatePlayer(2);
            peaceful.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);
            peaceful.Stats.Set(CharacterStat.MaxNCU, 60);
            var mob = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 11 }, new StubItemBuilder());
            mob.Stats.Set(CharacterStat.Health, 100, StatDetail.Base);

            NanoSpell nuke = TestNanos.Create(
                158508,
                durationCentiseconds: 0,
                can: 0,
                flags: NanoFlags.IsHostile,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, -40, -40, 0]
                    }
                ]);
            NanoSpell debuff = TestNanos.Create(158509, can: 0, flags: NanoFlags.IsHostile);
            var items = new StubItemBuilder().Add(nuke).Add(debuff);
            var inventory = new StubInventoryRepository();

            Assert.IsFalse(NanoRuntime.TryApplyImmediate(attacker, peaceful, nuke.Id, items, inventory, DateTime.UtcNow));
            Assert.AreEqual(100, peaceful.Stats.GetOrZero(CharacterStat.Health));
            Assert.IsFalse(NanoRuntime.TryApplyImmediate(attacker, peaceful, debuff.Id, items, inventory, DateTime.UtcNow));
            Assert.AreEqual(0, peaceful.Buffs.Count);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(attacker, mob, nuke.Id, items, inventory, DateTime.UtcNow));
            Assert.AreEqual(60, mob.Stats.GetOrZero(CharacterStat.Health));
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(attacker, mob, debuff.Id, items, inventory, DateTime.UtcNow));
            Assert.AreEqual(1, mob.Buffs.Count);
        }

        [TestMethod]
        public void HostileNanoLandsOnTheCaster()
        {
            Player caster = TestWorld.CreatePlayer(1);
            NanoSpell debuff = TestNanos.Create(158509, can: 0, flags: NanoFlags.IsHostile);
            var items = new StubItemBuilder().Add(debuff);
            var inventory = new StubInventoryRepository();

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(caster, caster, debuff.Id, items, inventory, DateTime.UtcNow));
            Assert.AreEqual(1, caster.Buffs.Count);
        }

        [TestMethod]
        public void RebaseIgnoresChangeActionRestrictionThatDoesNotEnableTheBit()
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 60);

            Assert.AreEqual(BuffApplyDecision.Apply, Apply(player, 214879, (int)ActionRestrictionFlags.PvPEnabled, mode: -1));
            Assert.AreEqual(ActionRestrictionFlags.None, player.ActionRestrictionFlags);
        }

        static void Grant(Player player, ActionRestrictionFlags flags)
        {
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            Assert.AreEqual(BuffApplyDecision.Apply, Apply(player, 1, (int)flags));
        }

        static BuffApplyDecision Apply(Player player, int nanoId, int bits, int mode = 0)
        {
            NanoSpell spell = TestNanos.Create(
                nanoId,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.ChangeActionRestriction,
                        Arguments = [bits, mode]
                    }
                ]);
            return player.TryApplyBuff(spell, player.Identity, DateTime.UtcNow, out _, out _);
        }
    }
}
