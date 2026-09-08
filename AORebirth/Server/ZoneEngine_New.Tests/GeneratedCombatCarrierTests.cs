namespace ZoneEngine_New.Tests
{
    using System;
    using System.Linq;
    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;

    [TestClass]
    public sealed class GeneratedCombatCarrierTests
    {
        [TestMethod]
        public void NewLoadsUnchangedGeneratedCatalogThroughSharedCarriers()
        {
            var profiles = CapturedEnemyCombatGeneratedProfiles.Create();
            Assert.IsTrue(profiles.Length > 0);
            Assert.AreEqual(profiles.Length, profiles.Select(p => p.ProfileId).Distinct(StringComparer.Ordinal).Count());
            var cultist = profiles.Single(p => p.ProfileId == "002f6bffabdaa21f-744247ca24ac9229");
            Assert.AreEqual(1931, cultist.ResourceId); Assert.AreEqual("Cultist", cultist.Name);
            Assert.AreEqual(26149, cultist.MonsterData); Assert.AreEqual(34, cultist.Level);
            Assert.IsTrue(cultist.CaptureRuntimeEvidenceSafe);
            StringAssert.Contains(cultist.Evidence, "20260721-052115");
            Assert.IsTrue(cultist.WeaponDefinition.IsValid);
        }

        [TestMethod]
        public void SharedWeaponValidationAndProductionCopyNeverMutateCapturedStats()
        {
            var captured = CapturedEnemyCombatGeneratedProfiles.Create().First(p => p.WeaponDefinition != null).WeaponDefinition;
            int quality = captured.Quality;
            var production = captured.WithProductionWeaponLoadout(123, 456, 7);
            Assert.AreEqual(quality, captured.Quality);
            Assert.AreEqual(123, production.LowId); Assert.AreEqual(456, production.HighId); Assert.AreEqual(7, production.Quality);
            Assert.IsTrue(production.IsValid);
            var malformed = new CapturedEnemyWeaponDefinition(captured.Evidence, captured.EvidenceSourceIdentity,
                captured.N3Unknown, captured.Unknown1, captured.InventorySlot, captured.StateMachineType,
                captured.StateMachineInstance, captured.Unknown2, captured.Stats.Reverse().ToArray(), captured.Unknown3);
            Assert.IsFalse(malformed.IsValid);
        }

        [TestMethod]
        public void SharedStreamRetainsEvidenceCopiesAndCompletenessGate()
        {
            int[] damage = [3, 7]; double[] start = [0.5]; double[] first = [1.0]; double[] landed = [2.0];
            var stream = Stream(damage, start, first, landed);
            Assert.IsTrue(stream.HasCompleteFixedRuntimeEvidence);
            damage[0] = 999; start[0] = double.NaN; first[0] = -1; landed[0] = -1;
            CollectionAssert.AreEqual(new[] { 3, 7 }, stream.CapturedDamageObservations);
            CollectionAssert.AreEqual(new[] { 0.5 }, stream.CapturedAttackStartDelayObservationsSeconds);
            CollectionAssert.AreEqual(new[] { 1.0 }, stream.CapturedFirstHitDelayObservationsSeconds);
            CollectionAssert.AreEqual(new[] { 2.0 }, stream.CapturedLandedIntervalObservationsSeconds);
            Assert.IsTrue(stream.HasCompleteFixedRuntimeEvidence);
            Assert.IsFalse(Stream([3, 7], [double.NaN], [1.0], [2.0]).HasCompleteFixedRuntimeEvidence);
            Assert.IsFalse(Stream([3, 8], [0.5], [1.0], [2.0]).HasCompleteFixedRuntimeEvidence);
            Assert.IsFalse(Stream([3, 7], [0.5], [], [2.0]).HasCompleteFixedRuntimeEvidence);
        }

        static CapturedEnemyCombatProfileStreamDefinition Stream(int[] damage, double[] start, double[] first, double[] landed)
            => new(3, 7, 10, 6, 0, 0, 0, 0, 2.0, damage, start, first, landed, 0, true, null, true);

        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(false, true, true, false)]
        [DataRow(true, false, false, false)]
        [DataRow(true, true, false, true)]
        [DataRow(true, false, true, true)]
        public void CaptureRuntimeSafetyStillRequiresSafeEvidenceAndProvenInitialization(
            bool safe, bool deterministic, bool ordered, bool expected)
        {
            var profile = new CapturedEnemyCombatProfileDefinition("id", "evidence", 1, "Name", 2, 3,
                false, safe, deterministic, [10], 10, null!, [], 0, 0, 0, 0, 0, 4, 0, 0, [],
                ordered ? [4, 5] : [4]);
            Assert.AreEqual(expected, profile.CaptureRuntimeEvidenceSafe);
            Assert.AreEqual(ordered, profile.HasCapturedOrderedSpecialAttackWeaponState);
        }

        [TestMethod]
        public void SharedWifuBuilderKeepsTemplateOwnerInstanceSlotAndStatOrderDistinct()
        {
            var captured = CapturedEnemyCombatGeneratedProfiles.Create().First(p => p.WeaponDefinition != null).WeaponDefinition;
            var owner = new Identity { Type = IdentityType.CanbeAffected, Instance = 71 };
            var instance = new Identity { Type = IdentityType.WeaponInstance, Instance = 72 };
            var packet = CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(owner, 4582, instance, captured, 17, 3);
            Assert.AreEqual(owner, packet.Owner); Assert.AreEqual(instance, packet.Identity); Assert.AreEqual(4582, packet.PlayfieldId);
            Assert.AreEqual(captured.Unknown2, packet.Unknown2);
            CollectionAssert.AreEqual(captured.Stats.Select(s => s.Stat).ToArray(), packet.Stats.Select(s => s.Value1).ToArray());
            Assert.AreEqual(17u, packet.Stats.Single(s => s.Value1 == CharacterStat.Energy).Value2);
            Assert.AreEqual(3u, packet.Stats.Single(s => s.Value1 == CharacterStat.MultipleCount).Value2);
            Assert.AreEqual((uint)captured.LowId, packet.Stats.Single(s => s.Value1 == CharacterStat.ACGItemTemplateID).Value2);
            Assert.ThrowsExactly<InvalidOperationException>(() => CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(owner, 4582, instance, null!));
        }

        [TestMethod]
        public void SharedAttackBuildersPreserveEveryExplicitWireField()
        {
            var attacker = new Identity { Type = IdentityType.CanbeAffected, Instance = 11 };
            var target = new Identity { Type = IdentityType.CanbeAffected, Instance = 22 };
            var start = CapturedEnemyCombatPacketFactory.CreateAttack(attacker, target, 9, 7);
            Assert.AreEqual(attacker, start.Identity); Assert.AreEqual(target, start.Target);
            Assert.AreEqual((byte)9, start.Unknown); Assert.AreEqual((byte)7, start.Action);
            var hit = CapturedEnemyCombatPacketFactory.CreateAttackInfo(attacker, target, 101, 102, 103, 104, 105, 106, 8);
            Assert.AreEqual(attacker, hit.Identity); Assert.AreEqual(target, hit.Target); Assert.AreEqual((byte)8, hit.Unknown);
            Assert.AreEqual(101, hit.Unknown1); Assert.AreEqual(102, hit.Unknown2); Assert.AreEqual(103, hit.Unknown3);
            Assert.AreEqual(104, hit.Unknown4); Assert.AreEqual(105, hit.Unknown5); Assert.AreEqual(106, hit.Unknown6);
            var specials = CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(attacker,
                [new CapturedEnemySpecialAttackDefinition(10, 20, 30, "captured")], 9, 1, 2, 3, 4, 5);
            Assert.AreEqual((byte)9, specials.Unknown);
            Assert.AreEqual(1, specials.CloseCombatInitiative); Assert.AreEqual(2, specials.DistanceWeaponInitiative);
            Assert.AreEqual(3, specials.PhysicalProwessInitiative); Assert.AreEqual(4, specials.NanoProwessInitiative);
            Assert.AreEqual(5, specials.AggDef);
            var special = specials.Specials.Single();
            Assert.AreEqual(10, special.Unknown1); Assert.AreEqual(20, special.Unknown2);
            Assert.AreEqual(30, special.Unknown3); Assert.AreEqual("captured", special.Unknown4);
        }

        [TestMethod]
        public void SharedSequenceValidationRetainsOneShotAndRepeatingTiming()
        {
            var repeating = new CapturedEnemyCombatAttackDefinition(3, 7, 0, 10, 2, false, 0, 0, 0, 0, 0, 0, true);
            var oneShot = new CapturedEnemyCombatAttackDefinition(3, 7, 0, 10, 0, false, 0, 0, 0, 0, 0, 0, true);
            Assert.IsTrue(repeating.IsValid); Assert.IsFalse(repeating.IsValidOneShot);
            Assert.IsFalse(oneShot.IsValid); Assert.IsTrue(oneShot.IsValidOneShot);
            var stream = new CapturedEnemyParallelAttackStreamDefinition(0.5, repeating);
            var terminal = new CapturedEnemyParallelAttackStreamDefinition(0.5, oneShot, false);
            Assert.IsTrue(stream.IsValid); Assert.IsTrue(terminal.IsValid);
            DateTime now = DateTime.UnixEpoch;
            Assert.AreEqual(now.AddSeconds(2), stream.ResolveNextTickAfterHit(now));
            Assert.AreEqual(DateTime.MaxValue, terminal.ResolveNextTickAfterHit(now));
            var invalid = new CapturedEnemyParallelAttackSequenceDefinition([stream], [], 0, 0, 0, 0, 0, 0, 0, 0, double.NaN);
            Assert.IsFalse(invalid.IsValid);
        }
    }
}
