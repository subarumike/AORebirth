namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Nanos;

    [TestClass]
    public sealed class BuffApplyRulesTests
    {
        static readonly Identity Caster = new() { Type = IdentityType.CanbeAffected, Instance = 1 };

        [TestMethod]
        public void EmptyNcuAcceptsAnyBuff()
        {
            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2000, ncuCost: 20),
                [],
                maxNcu: 60,
                out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.Apply, decision);
            Assert.IsNull(replaced);
        }

        [TestMethod]
        public void InstantNanoNeverEntersNcu()
        {
            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2001, durationCentiseconds: 0),
                [],
                maxNcu: 60,
                out _);

            Assert.AreEqual(BuffApplyDecision.RefusedNotABuff, decision);
        }

        [TestMethod]
        public void StrongerSameStrainNanoBlocksTheWeakerOne()
        {
            Buff strong = Running(TestNanos.Create(2002, strain: 55, stackingOrder: 20));

            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2003, strain: 55, stackingOrder: 10),
                [strong],
                maxNcu: 60,
                out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.RefusedStrainStronger, decision);
            Assert.IsNull(replaced);
        }

        [TestMethod]
        public void StrongerNanoReplacesTheWeakerSameStrainEntry()
        {
            Buff weak = Running(TestNanos.Create(2004, strain: 55, stackingOrder: 10));

            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2005, strain: 55, stackingOrder: 20),
                [weak],
                maxNcu: 60,
                out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.Replace, decision);
            Assert.AreSame(weak, replaced);
        }

        [TestMethod]
        public void RecastReplacesItselfEvenAtTheSameStackingOrder()
        {
            NanoSpell spell = TestNanos.Create(2006, strain: 55, stackingOrder: 10);
            Buff running = Running(spell);

            BuffApplyDecision decision = BuffApplyRules.Evaluate(spell, [running], maxNcu: 60, out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.Replace, decision);
            Assert.AreSame(running, replaced);
        }

        [TestMethod]
        public void StrainlessNanosStackWithEachOther()
        {
            Buff first = Running(TestNanos.Create(2007, ncuCost: 5));

            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2008, ncuCost: 5),
                [first],
                maxNcu: 60,
                out Buff? replaced);

            Assert.AreEqual(BuffApplyDecision.Apply, decision);
            Assert.IsNull(replaced);
        }

        [TestMethod]
        public void NcuIsRefusedOnlyWhenTheFreedSlotStillDoesNotFit()
        {
            Buff running = Running(TestNanos.Create(2009, ncuCost: 40, strain: 60, stackingOrder: 5));
            var active = new List<Buff> { running };

            Assert.AreEqual(
                BuffApplyDecision.RefusedNotEnoughNcu,
                BuffApplyRules.Evaluate(TestNanos.Create(2010, ncuCost: 30), active, maxNcu: 60, out _));

            // Replacing the running nano frees its 40 NCU first, so the same cost now fits.
            Assert.AreEqual(
                BuffApplyDecision.Replace,
                BuffApplyRules.Evaluate(
                    TestNanos.Create(2011, ncuCost: 30, strain: 60, stackingOrder: 10),
                    active,
                    maxNcu: 60,
                    out _));
        }

        [TestMethod]
        public void HostileNanosIgnoreTheTargetsNcu()
        {
            Buff running = Running(TestNanos.Create(2012, ncuCost: 55));

            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2013, ncuCost: 40, can: CanFlags.ApplyOnHostile),
                [running],
                maxNcu: 60,
                out _);

            Assert.AreEqual(BuffApplyDecision.Apply, decision);
        }

        [TestMethod]
        public void CharactersWithoutAnNcuStatAreUnlimited()
        {
            BuffApplyDecision decision = BuffApplyRules.Evaluate(
                TestNanos.Create(2014, ncuCost: 500),
                [],
                maxNcu: 0,
                out _);

            Assert.AreEqual(BuffApplyDecision.Apply, decision);
        }

        static Buff Running(NanoSpell spell)
            => Buff.Create(spell, Caster, nanoInstance: 1, DateTime.UtcNow);
    }
}
