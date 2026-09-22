namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;

    [TestClass]
    public sealed class BuffTests
    {
        static readonly Identity Caster = new() { Type = IdentityType.CanbeAffected, Instance = 42 };

        [TestMethod]
        public void NanoWithoutDurationIsNotABuffAndCannotEnterNcu()
        {
            NanoSpell instant = TestNanos.Create(1000, durationCentiseconds: 0);

            Assert.IsFalse(instant.IsBuff);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => Buff.Create(instant, Caster, nanoInstance: 1, DateTime.UtcNow));
        }

        [TestMethod]
        public void NanoWithIsBuffFlagIsABuffEvenWithoutDuration()
        {
            NanoSpell buff = TestNanos.Create(1020, durationCentiseconds: 0, flags: NanoFlags.IsBuff);

            Assert.IsTrue(buff.IsBuff);
        }

        [TestMethod]
        public void ExpiryIsADeadlineSoASkippedTickCannotExtendABuff()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Buff buff = Buff.Create(TestNanos.Create(1001, durationCentiseconds: 6000), Caster, 1, start);

            Assert.AreEqual(start.AddSeconds(60), buff.ExpiresAtUtc);
            Assert.IsFalse(buff.IsExpired(start.AddSeconds(59)));
            Assert.IsTrue(buff.IsExpired(start.AddSeconds(60)));
            Assert.AreEqual(3000, buff.RemainingCentiseconds(start.AddSeconds(30)));
            Assert.AreEqual(0, buff.RemainingCentiseconds(start.AddMinutes(5)));
        }

        [TestMethod]
        public void RestoredBuffKeepsTheStoredDeadlineInsteadOfRefreshing()
        {
            var expiry = new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc);
            Buff buff = Buff.Restore(TestNanos.Create(1002, durationCentiseconds: 6000), Caster, 7, expiry);

            Assert.AreEqual(expiry, buff.ExpiresAtUtc);
            Assert.AreEqual(7, buff.NanoInstance);
        }

        [TestMethod]
        public void OnlyOwnerCancelsHonorCanCancel()
        {
            Buff locked = Buff.Create(
                TestNanos.Create(1003, canCancel: false),
                Caster,
                1,
                DateTime.UtcNow);

            Assert.IsFalse(locked.TryCancel(BuffRemovalReason.Cancelled));
            Assert.IsTrue(locked.TryCancel(BuffRemovalReason.Expired));
            Assert.IsTrue(locked.TryCancel(BuffRemovalReason.Death));
        }

        [TestMethod]
        public void HostileNanosAreNeverCancellableByTheirTarget()
        {
            NanoSpell debuff = TestNanos.Create(1004, can: CanFlags.ApplyOnHostile);

            Assert.IsTrue(debuff.IsHostile);
            Assert.IsFalse(debuff.CanCancel);
        }

        [TestMethod]
        public void CancellingABuffFreesNcuAndDropsItsBonus()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 500 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            NanoSpell spell = TestNanos.Create(
                1005,
                ncuCost: 25,
                modifiers: [TestNanos.Modify(CharacterStat.Strength, 40)]);

            BuffApplyDecision decision = player.TryApplyBuff(
                spell,
                Caster,
                DateTime.UtcNow,
                out Buff? applied,
                out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.Apply, decision);
            Assert.IsNotNull(applied);
            Assert.IsNull(replaced);
            Assert.AreEqual(25, player.UsedNcu);
            Assert.AreEqual(25, player.Stats.GetOrZero(CharacterStat.CurrentNCU));
            Assert.AreEqual(40, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));

            BuffRemovalOutcome outcome = player.TryRemoveBuff(
                1005,
                BuffRemovalReason.Cancelled,
                out Buff? removed);

            Assert.AreEqual(BuffRemovalOutcome.Removed, outcome);
            Assert.AreEqual(1005, removed?.Id);
            Assert.AreEqual(0, player.UsedNcu);
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Bonus));
        }

        [TestMethod]
        public void CancelIsRefusedForMissingAndUncancellableBuffs()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 501 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.TryApplyBuff(
                TestNanos.Create(1006, canCancel: false),
                Caster,
                DateTime.UtcNow,
                out _,
                out _);

            Assert.AreEqual(
                BuffRemovalOutcome.NotFound,
                player.TryRemoveBuff(9999, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(
                BuffRemovalOutcome.NotCancellable,
                player.TryRemoveBuff(1006, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(1, player.Buffs.Count);
        }

        [TestMethod]
        public void ExpiredBuffsDrainOnceAndFreeTheirNcu()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 502 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.TryApplyBuff(
                TestNanos.Create(1007, durationCentiseconds: 1000, ncuCost: 15),
                Caster,
                start,
                out _,
                out _);

            Assert.AreEqual(0, player.DrainExpiredBuffs(start.AddSeconds(9)).Count);
            Assert.AreEqual(1, player.DrainExpiredBuffs(start.AddSeconds(10)).Count);
            Assert.AreEqual(0, player.DrainExpiredBuffs(start.AddSeconds(20)).Count);
            Assert.AreEqual(0, player.UsedNcu);
        }

        [TestMethod]
        public void ConditionalShapeRebasesAndRestoresWithoutPersistingTransformedBase()
        {
            Player CreatePlayer()
            {
                var player = new Player(Caster, new StubLogger(), new StubItemBuilder());
                player.Stats.Set(CharacterStat.MaxNCU, 60);
                player.Stats.Set(CharacterStat.MonsterData, 17);
                player.Stats.Set((CharacterStat)12, 2);
                return player;
            }

            var shape = new ItemSpell
            {
                FunctionType = (int)FunctionType.MonsterShape,
                Arguments = [900],
                Requirements =
                [
                    new() { StatNumber = 12, Operator = (int)Operator.EqualTo, Value = 1 },
                    new() { StatNumber = 12, Operator = (int)Operator.EqualTo, Value = 2 },
                    new() { Operator = (int)Operator.Or },
                ],
            };
            NanoSpell spell = TestNanos.Create(1010, modifiers: [shape]);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Player player = CreatePlayer();
            Assert.AreEqual(BuffApplyDecision.Apply,
                player.TryApplyBuff(spell, Caster, start, out Buff? applied, out _));
            Assert.AreEqual(900, player.Stats.Get(CharacterStat.MonsterData));
            player.RebaseStats();
            player.RebaseStats();
            Assert.AreEqual(900, player.Stats.Get(CharacterStat.MonsterData));
            Assert.AreEqual(17, player.Stats.Get(CharacterStat.MonsterData, StatDetail.Base));
            Assert.IsFalse(Array.Exists(player.Stats.DrainDirty(), s => s.Value1 == CharacterStat.MonsterData));

            Player restored = CreatePlayer();
            restored.TryRestoreBuff(spell, Caster, applied!.NanoInstance, applied.ExpiresAtUtc);
            Assert.AreEqual(900, restored.Stats.Get(CharacterStat.MonsterData));
            Assert.AreEqual(applied.ExpiresAtUtc, restored.Buffs[0].ExpiresAtUtc);
            Assert.AreEqual(BuffRemovalOutcome.Removed,
                player.TryRemoveBuff(spell.Id, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(17, player.Stats.Get(CharacterStat.MonsterData));
            Assert.AreEqual(1, restored.DrainExpiredBuffs(applied.ExpiresAtUtc).Count);
            Assert.AreEqual(17, restored.Stats.Get(CharacterStat.MonsterData));
            Assert.AreEqual(17, restored.Stats.Get(CharacterStat.MonsterData, StatDetail.Base));
        }

        [TestMethod]
        public void DeathEmptiesNcuIncludingUncancellableBuffs()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 503 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.TryApplyBuff(TestNanos.Create(1008, canCancel: false), Caster, DateTime.UtcNow, out _, out _);

            player.OnDeath();

            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(0, player.UsedNcu);
        }

        [TestMethod]
        public void BuffSetFlagOrsTheBitOnRebaseAndClearsItWithoutTouchingBase()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 504 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            NanoSpell spell = TestNanos.Create(
                1011,
                modifiers: [TestNanos.SetFlag(CharacterStat.Flags, 3)]);

            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(spell, Caster, DateTime.UtcNow, out _, out _));
            Assert.AreEqual(8, player.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Flags, StatDetail.Base));

            Assert.AreEqual(
                BuffRemovalOutcome.Removed,
                player.TryRemoveBuff(1011, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Flags, StatDetail.Base));
        }

        [TestMethod]
        public void TwoBuffsSettingTheSameBitStayThatBit()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 505 }, new StubItemBuilder());
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            ItemSpell flag = TestNanos.SetFlag(CharacterStat.Flags, 3);

            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(
                    TestNanos.Create(1012, strain: 1, modifiers: [flag]),
                    Caster,
                    DateTime.UtcNow,
                    out _,
                    out _));
            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(
                    TestNanos.Create(1013, strain: 2, modifiers: [flag]),
                    Caster,
                    DateTime.UtcNow,
                    out _,
                    out _));
            Assert.AreEqual(8, player.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Flags, StatDetail.Base));

            Assert.AreEqual(
                BuffRemovalOutcome.Removed,
                player.TryRemoveBuff(1012, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(8, player.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(
                BuffRemovalOutcome.Removed,
                player.TryRemoveBuff(1013, BuffRemovalReason.Cancelled, out _));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void InstantSetFlagWritesBase()
        {
            NpcCharacter player = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 506 }, new StubItemBuilder());
            var template = new ItemTemplate
            {
                Id = 1014,
                Name = "Flag item",
                Quality = 1,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] = [TestNanos.SetFlag(CharacterStat.Flags, 3)]
                }
            };

            Assert.IsTrue(template.ExecuteOnUseSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder(),
                skipPassiveModifiers: false));
            Assert.AreEqual(8, player.Stats.GetOrZero(CharacterStat.Flags, StatDetail.Base));
            Assert.AreEqual(8, player.Stats.GetOrZero(CharacterStat.Flags));
        }
    }
}
