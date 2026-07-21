namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class TempleOfThreeWindsOrdinaryContentTests
    {
        [TestMethod]
        public void TempleContentIsDedicatedAndCatalogPreservesDungeonBoundaries()
        {
            var temple = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemyProfile[] templeProfiles = temple.GetProfiles();
            OrdinaryEnemySpawnDefinition[] templeSpawns = temple.GetSpawns();

            Assert.AreEqual(7, templeProfiles.Length);
            Assert.AreEqual(122, templeSpawns.Length);
            Assert.IsTrue(templeProfiles.All(value => value.ProfileKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(templeProfiles.All(value => value.DisplayName == "Cultist" && !value.BossOrScripted));
            Assert.IsTrue(templeSpawns.All(value => value.PlayfieldInstance == 647));
            Assert.IsTrue(templeSpawns.All(value => value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsFalse(templeSpawns.Any(value => value.SpawnKey.Contains("subway")));

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                temple);
            Assert.AreEqual(322, catalog.GetRuntimeSpawns(127).Length);
            Assert.AreEqual(122, catalog.GetRuntimeSpawns(647).Length);
            Assert.AreEqual(444, catalog.GetSpawns().Length);
            Assert.IsTrue(catalog.GetRuntimeSpawns(127).All(value => !value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(catalog.GetRuntimeSpawns(647).All(value => value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void TempleCultistAppearanceMovementAndLifecycleMatchTheCaptureSlice()
        {
            var temple = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemyProfile[] profiles = temple.GetProfiles();
            OrdinaryEnemySpawnDefinition[] spawns = temple.GetSpawns();
            var appearances = new Dictionary<int, uint>
            {
                { 26074, 1579u }, { 26082, 1835u }, { 26103, 1419u },
                { 26135, 1611u }, { 26137, 1867u }, { 26147, 1643u }, { 26149, 1899u }
            };
            var corpseMeshes = new Dictionary<int, int>
            {
                { 26074, 17532 }, { 26082, 17528 }, { 26103, 23365 },
                { 26135, 23378 }, { 26137, 5934 }, { 26147, 17905 }, { 26149, 5941 }
            };

            Assert.IsTrue(profiles.All(value => value.ConstructionMode == OrdinaryEnemyConstructionMode.CapturedDirect));
            Assert.IsTrue(profiles.All(value => value.Appearance.AppearanceValue == appearances[value.MonsterData]));
            Assert.IsTrue(profiles.All(value => value.Corpse.CapturedCatMesh.Value == corpseMeshes[value.MonsterData]));
            Assert.AreEqual(16, spawns.Count(value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol));
            Assert.AreEqual(106, spawns.Count(value => value.MovementMode == OrdinaryEnemyMovementMode.Static));
            Assert.IsTrue(spawns.Where(value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol).All(value => value.Waypoints.Length == 2));
            Assert.AreEqual(20, spawns.Min(value => value.Level));
            Assert.AreEqual(35, spawns.Max(value => value.Level));
            Assert.AreEqual(5, spawns.Select(value => value.SourceCapture).Distinct(StringComparer.Ordinal).Count());
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit));
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.ExplicitPolicy.DelayStartsAt == RespawnDelayStartsAt.NpcDespawn));
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds.Value == 300.0));
        }

        [TestMethod]
        public void TempleCultistCombatAggroLootAndCreditsStayCaptureBounded()
        {
            OrdinaryEnemyProfile[] profiles = new CapturedTempleOfThreeWindsContentProvider().GetProfiles();

            Assert.IsTrue(profiles.All(value => value.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto));
            Assert.IsTrue(profiles.All(value => value.Aggression.AutomaticAggroRadius.Value == 7.0));
            Assert.IsTrue(profiles.All(value => value.Aggression.Chase && value.Aggression.ReturnToSpawn));
            Assert.IsTrue(profiles.All(value => value.Aggression.EvidenceState == OrdinaryEnemyEvidenceState.Policy));
            Assert.IsTrue(profiles.All(value => value.Combat.EvidenceState == OrdinaryEnemyEvidenceState.Observed));
            Assert.IsTrue(profiles.All(value => value.Combat.Contract.MinDamage == 15));
            Assert.IsTrue(profiles.All(value => value.Combat.Contract.MaxDamage == 32));
            Assert.IsTrue(profiles.All(value => value.Combat.Contract.RechargeSeconds == 4.635295));
            Assert.AreEqual(74, profiles.Sum(value => value.Loot.ObservedCompleteInventories));
            Assert.AreEqual(57, profiles.Sum(value => value.Loot.ObservedEmptyInventories));
            Assert.AreEqual(17, profiles.Sum(value => value.Loot.Entries.Sum(entry => entry.ObservedCount)));
            Assert.AreEqual(74, profiles.Sum(value => value.Loot.LevelCreditRules.Sum(rule => rule.ObservedCorpses)));
            Assert.IsTrue(profiles.All(value => value.Loot.LevelCreditRules.Length == 16));
            Assert.IsTrue(profiles.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 20).MinimumCredits == 371));
            Assert.IsTrue(profiles.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 35).MaximumCredits == 705));
        }
    }
}
