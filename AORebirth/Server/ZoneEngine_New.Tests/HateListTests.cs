namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class HateListTests
    {
        static readonly Identity PlayerA = new() { Type = IdentityType.CanbeAffected, Instance = 10 };
        static readonly Identity PlayerB = new() { Type = IdentityType.CanbeAffected, Instance = 20 };
        static readonly Vector3 Origin = new(0, 0, 0);

        [TestMethod]
        public void AddIncrementsThreatAndHighestWins()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 5f);
            hate.Add(PlayerB, 3f);
            hate.Add(PlayerA, 1f);

            Assert.AreEqual(6f, hate.ThreatOf(PlayerA));
            Assert.IsTrue(NpcAiRules.TryHighestNearby(hate, _ => true, out Identity top, out float threat));
            Assert.AreEqual(PlayerA, top);
            Assert.AreEqual(6f, threat);
        }

        [TestMethod]
        public void InvalidAndFarEntriesAreIgnoredWhenSelecting()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 100f);
            hate.Add(PlayerB, 1f);

            Assert.IsTrue(NpcAiRules.TryHighestNearby(hate, id => id == PlayerB, out Identity top, out _));
            Assert.AreEqual(PlayerB, top);
        }

        [TestMethod]
        public void ClearRemovesAllEntries()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 2f);
            hate.Clear();
            Assert.IsTrue(hate.IsEmpty);
            Assert.IsFalse(hate.Contains(PlayerA));
        }

        [TestMethod]
        public void ShouldLeashWhenHateExistsButNobodyIsNearby()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 1f);
            Vector3 npc = Origin;
            Assert.IsTrue(NpcAiRules.ShouldLeash(hate, Origin, npc, _ => false));
        }

        [TestMethod]
        public void ShouldNotLeashWhenIdleAtHome()
        {
            var hate = new HateList();
            Assert.IsFalse(NpcAiRules.ShouldLeash(hate, Origin, Origin, _ => false));
        }

        [TestMethod]
        public void ShouldLeashWhenPastMaxHomeRange()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 1f);
            Vector3 far = new(NpcAiRules.MaxLeashRange + 1f, 0, 0);
            Assert.IsTrue(NpcAiRules.ShouldLeash(hate, Origin, far, _ => true));
        }

        [TestMethod]
        public void ShouldNotLeashOnHomeRangeWhenHomeIsMissing()
        {
            var hate = new HateList();
            Vector3 far = new(NpcAiRules.MaxLeashRange + 1f, 0, 0);
            Assert.IsFalse(NpcAiRules.ShouldLeash(hate, null, far, _ => false));
        }

        [TestMethod]
        public void ShouldLeashWhenHateExistsButNobodyIsEngageable()
        {
            var hate = new HateList();
            hate.Add(PlayerA, 1f);
            Assert.IsTrue(NpcAiRules.ShouldLeash(hate, Origin, Origin, _ => false));
            Assert.IsFalse(NpcAiRules.ShouldLeash(hate, Origin, Origin, _ => true));
        }

        [TestMethod]
        public void ProximityHostileOnlyWhenBreedHostilityIsPositive()
        {
            Assert.IsFalse(NpcAiRules.IsProximityHostile(0));
            Assert.IsTrue(NpcAiRules.IsProximityHostile(1));
        }

        [TestMethod]
        public void NoChanceGraceHoldsUntilTwoSeconds()
        {
            DateTime last = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(NpcAiRules.IsWithinNoChanceGrace(default, last));
            Assert.IsTrue(NpcAiRules.IsWithinNoChanceGrace(last, last.AddSeconds(1.9)));
            Assert.IsFalse(NpcAiRules.IsWithinNoChanceGrace(last, last.AddSeconds(2)));
        }
    }
}
