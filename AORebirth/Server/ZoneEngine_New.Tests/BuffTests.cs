namespace ZoneEngine_New.Tests
{
    using System;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
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
            Player player = TestWorld.CreatePlayer(500);
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
            Player player = TestWorld.CreatePlayer(501);
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
            Player player = TestWorld.CreatePlayer(502);
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
        public void DeathEmptiesNcuIncludingUncancellableBuffs()
        {
            Player player = TestWorld.CreatePlayer(503);
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.TryApplyBuff(TestNanos.Create(1008, canCancel: false), Caster, DateTime.UtcNow, out _, out _);

            player.OnDeath();

            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(0, player.UsedNcu);
        }
    }
}
