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

            Assert.AreEqual(9, templeProfiles.Length);
            Assert.AreEqual(153, templeSpawns.Length);
            Assert.IsTrue(templeProfiles.All(value => value.ProfileKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(templeProfiles.All(value => !value.BossOrScripted));
            Assert.AreEqual(7, templeProfiles.Count(value => value.DisplayName == "Cultist"));
            Assert.AreEqual(1, templeProfiles.Count(value => value.DisplayName == "Eternal Sentinel"));
            Assert.AreEqual(1, templeProfiles.Count(value => value.DisplayName == "Murial the Faithful"));
            Assert.IsTrue(templeSpawns.All(value => value.PlayfieldInstance == 1931));
            Assert.IsTrue(templeSpawns.All(value => value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsFalse(templeSpawns.Any(value => value.SpawnKey.Contains("subway")));

            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                temple);
            Assert.AreEqual(322, catalog.GetRuntimeSpawns(127).Length);
            Assert.AreEqual(0, catalog.GetRuntimeSpawns(647).Length);
            Assert.AreEqual(153, catalog.GetRuntimeSpawns(1931).Length);
            Assert.AreEqual(475, catalog.GetSpawns().Length);
            Assert.IsTrue(catalog.GetRuntimeSpawns(127).All(value => !value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(catalog.GetRuntimeSpawns(1931).All(value => value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void TempleCultistAppearanceMovementAndLifecycleMatchTheCaptureSlice()
        {
            var temple = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemyProfile[] profiles = temple.GetProfiles();
            OrdinaryEnemySpawnDefinition[] spawns = temple.GetSpawns();
            OrdinaryEnemyProfile[] cultists = profiles.Where(value => value.DisplayName == "Cultist").ToArray();
            OrdinaryEnemySpawnDefinition[] cultistSpawns = spawns
                .Where(value => value.ProfileKey.StartsWith("totw.cultist.", StringComparison.Ordinal))
                .ToArray();
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
            Assert.IsTrue(cultists.All(value => value.Appearance.AppearanceValue == appearances[value.MonsterData]));
            Assert.IsTrue(cultists.All(value => value.Corpse.CapturedCatMesh.Value == corpseMeshes[value.MonsterData]));
            Assert.AreEqual(16, cultistSpawns.Count(value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol));
            Assert.AreEqual(133, cultistSpawns.Count(value => value.MovementMode == OrdinaryEnemyMovementMode.Static));
            Assert.IsTrue(cultistSpawns.Where(value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol).All(value => value.Waypoints.Length == 2));
            Assert.AreEqual(20, cultistSpawns.Min(value => value.Level));
            Assert.AreEqual(35, cultistSpawns.Max(value => value.Level));
            Assert.AreEqual(7, cultistSpawns.Select(value => value.SourceCapture).Distinct(StringComparer.Ordinal).Count());
            Assert.AreEqual(22, cultistSpawns.Count(value => value.SourceCapture == "20260721-230426"));
            Assert.IsTrue(cultistSpawns.Where(value => value.SourceCapture == "20260721-230426")
                .All(value => value.Z >= 419.0f && value.Z <= 463.0f));
            Assert.AreEqual(5, cultistSpawns.Count(value => value.SourceCapture == "20260721-232051"));
            Assert.IsTrue(cultistSpawns.Where(value => value.SourceCapture == "20260721-232051")
                .All(value => value.Z >= 404.0f && value.Z <= 410.0f));
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit));
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.ExplicitPolicy.DelayStartsAt == RespawnDelayStartsAt.NpcDespawn));
            Assert.IsTrue(spawns.All(value => value.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds.Value == 300.0));

            OrdinaryEnemyProfile sentinel = profiles.Single(value => value.DisplayName == "Eternal Sentinel");
            OrdinaryEnemySpawnDefinition[] sentinelSpawns = spawns
                .Where(value => value.ProfileKey == sentinel.ProfileKey)
                .ToArray();
            Assert.AreEqual(1227u, sentinel.Appearance.AppearanceValue);
            Assert.AreEqual(41664, sentinel.Corpse.CapturedCatMesh.Value);
            Assert.AreEqual(3, sentinelSpawns.Length);
            Assert.AreEqual(18, sentinelSpawns.Min(value => value.Level));
            Assert.AreEqual(20, sentinelSpawns.Max(value => value.Level));

            OrdinaryEnemyProfile murial = profiles.Single(value => value.DisplayName == "Murial the Faithful");
            OrdinaryEnemySpawnDefinition murialSpawn = spawns.Single(
                value => value.ProfileKey == CapturedTempleOfThreeWindsContentProvider.MurialProfileKey);
            Assert.AreEqual(26090, murial.MonsterData);
            Assert.AreEqual(1835u, murial.Appearance.AppearanceValue);
            Assert.AreEqual(40629, murial.Appearance.HeadMesh);
            Assert.AreEqual(5927, murial.Corpse.CapturedCatMesh.Value);
            Assert.AreEqual(OrdinaryEnemyLootEvidence.NoneProven, murial.Loot.Evidence);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Unresolved, murial.Loot.CreditEvidence);
            Assert.AreEqual(OrdinaryEnemyMovementMode.Patrol, murialSpawn.MovementMode);
            Assert.AreEqual(20, murialSpawn.Waypoints.Length);
            Assert.IsFalse(murialSpawn.UseSpawnAsPatrolStart);
            Assert.AreEqual(34, murialSpawn.Level);
            Assert.AreEqual(1535, murialSpawn.Health);
            Assert.AreEqual(118, murialSpawn.RunSpeed);
            Assert.AreEqual(266.339355f, murialSpawn.Waypoints[0].X);
            Assert.AreEqual(513.76355f, murialSpawn.Waypoints[0].Z);
            Assert.AreEqual(267.127625f, murialSpawn.Waypoints[19].X);
            Assert.AreEqual(508.234467f, murialSpawn.Waypoints[19].Z);
        }

        [TestMethod]
        public void TempleCultistCombatAggroLootAndCreditsStayCaptureBounded()
        {
            OrdinaryEnemyProfile[] profiles = new CapturedTempleOfThreeWindsContentProvider().GetProfiles();
            OrdinaryEnemyProfile[] cultists = profiles.Where(value => value.DisplayName == "Cultist").ToArray();

            Assert.IsTrue(profiles.All(value => value.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto));
            Assert.IsTrue(profiles.All(value => value.Aggression.AutomaticAggroRadius.Value == 7.0));
            Assert.IsTrue(profiles.All(value => value.Aggression.Chase && value.Aggression.ReturnToSpawn));
            Assert.IsTrue(profiles.All(value => value.Aggression.EvidenceState == OrdinaryEnemyEvidenceState.Policy));
            Assert.IsTrue(profiles.All(value => value.Combat.EvidenceState == OrdinaryEnemyEvidenceState.Observed));
            Assert.IsTrue(cultists.All(value => value.Combat.Contract.MinDamage == 15));
            Assert.IsTrue(cultists.All(value => value.Combat.Contract.MaxDamage == 32));
            Assert.IsTrue(cultists.All(value => value.Combat.Contract.RechargeSeconds == 4.635295));
            Assert.AreEqual(74, cultists.Sum(value => value.Loot.ObservedCompleteInventories));
            Assert.AreEqual(57, cultists.Sum(value => value.Loot.ObservedEmptyInventories));
            Assert.AreEqual(17, cultists.Sum(value => value.Loot.Entries.Sum(entry => entry.ObservedCount)));
            Assert.AreEqual(74, cultists.Sum(value => value.Loot.LevelCreditRules.Sum(rule => rule.ObservedCorpses)));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Length == 16));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 20).MinimumCredits == 371));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 35).MaximumCredits == 705));

            OrdinaryEnemyProfile sentinel = profiles.Single(value => value.DisplayName == "Eternal Sentinel");
            Assert.AreEqual(17, sentinel.Combat.Contract.MinDamage);
            Assert.AreEqual(18, sentinel.Combat.Contract.MaxDamage);
            Assert.AreEqual(5.67, sentinel.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(5, sentinel.Loot.ObservedCompleteInventories);
            Assert.AreEqual(5, sentinel.Loot.ObservedEmptyInventories);
            Assert.AreEqual(111, sentinel.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 18).MinimumCredits);
            Assert.AreEqual(124, sentinel.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 20).MaximumCredits);

            OrdinaryEnemyProfile murial = profiles.Single(value => value.DisplayName == "Murial the Faithful");
            CapturedEnemySpecialAttackSequenceDefinition murialSequence =
                murial.Combat.Contract.SpecialAttackSequence;
            Assert.AreEqual(26, murialSequence.RepeatingAttack.MinDamage);
            Assert.AreEqual(26, murialSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(6, murialSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(1.5397, murialSequence.InitialAttackDelaySeconds);
            Assert.AreEqual(3.7885, murialSequence.RepeatingAttack.RechargeSeconds);
            Assert.AreEqual(258, murialSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(21, murialSequence.SpecialAttackWeaponUnknown4);
        }

        [TestMethod]
        public void DefenderCombatAndLootRemainDedicatedToTheTempleEncounter()
        {
            CapturedEnemyCombatContract combat =
                CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree();
            Assert.AreEqual(CapturedEnemyAttackModel.Specialized, combat.AttackModel);
            Assert.IsTrue(combat.IsCombatReady);
            Assert.AreEqual(10.915985, combat.SpecialAttackSequence.InitialAttackDelaySeconds);
            Assert.AreEqual(43, combat.SpecialAttackSequence.RepeatingAttack.MinDamage);
            Assert.AreEqual(43, combat.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(-1, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoAmmoCount);
            Assert.AreEqual(0, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(1465538645, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance);
            Assert.AreEqual(1, combat.SpecialAttackSequence.SpecialAttacks.Length);
            Assert.AreEqual(205877, combat.SpecialAttackSequence.SpecialAttacks[0].LowTemplate);
            Assert.AreEqual(205878, combat.SpecialAttackSequence.SpecialAttacks[0].HighTemplate);
            Assert.AreEqual("WZXU", combat.SpecialAttackSequence.SpecialAttacks[0].Name);
            Assert.AreEqual(239, combat.SpecialAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(25, combat.SpecialAttackSequence.SpecialAttackWeaponUnknown4);

            CapturedEnemyCombatContract yatila =
                CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila();
            Assert.AreEqual(4, yatila.ParallelAttackSequence.Streams.Length);
            Assert.AreEqual(31, yatila.ParallelAttackSequence.Streams[0].Attack.MinDamage);
            Assert.AreEqual(56, yatila.ParallelAttackSequence.Streams[0].Attack.MaxDamage);
            Assert.AreEqual(269, yatila.ParallelAttackSequence.Streams[1].Attack.MinDamage);
            Assert.AreEqual(65, yatila.ParallelAttackSequence.Streams[2].Attack.MinDamage);
            Assert.AreEqual(120, yatila.ParallelAttackSequence.Streams[3].Attack.MinDamage);
            Assert.AreEqual(3, yatila.ParallelAttackSequence.SpecialAttacks.Length);
            Assert.AreEqual(413, yatila.ParallelAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(33, yatila.ParallelAttackSequence.SpecialAttackWeaponUnknown4);

            Assert.AreEqual(37, CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard().SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(72, CapturedTempleOfThreeWindsCombatCatalog.ReAnimator().SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(30, CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany().SpecialAttackSequence.RepeatingAttack.MaxDamage);

            CapturedEnemyCombatContract curator =
                CapturedTempleOfThreeWindsCombatCatalog.TheCurator();
            Assert.AreEqual(33, curator.SpecialAttackSequence.OpeningAttack.MinDamage);
            Assert.AreEqual(57, curator.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(0, curator.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(1465538645, curator.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance);
            Assert.AreEqual(381, curator.SpecialAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(31, curator.SpecialAttackSequence.SpecialAttackWeaponUnknown4);

            CapturedEnemyCombatContract nematet =
                CapturedTempleOfThreeWindsCombatCatalog.NematetTheCustodianOfTime();
            Assert.AreEqual(3, nematet.ParallelAttackSequence.Streams.Length);
            Assert.AreEqual(82, nematet.ParallelAttackSequence.Streams[0].Attack.MinDamage);
            Assert.AreEqual(2, nematet.ParallelAttackSequence.Streams[0].Attack.AttackInfoWeaponSlot);
            Assert.AreEqual(70, nematet.ParallelAttackSequence.Streams[1].Attack.MinDamage);
            Assert.AreEqual(0, nematet.ParallelAttackSequence.Streams[1].Attack.AttackInfoWeaponSlot);
            Assert.AreEqual(152, nematet.ParallelAttackSequence.Streams[2].Attack.MinDamage);
            Assert.AreEqual(1, nematet.ParallelAttackSequence.Streams[2].Attack.AttackInfoWeaponSlot);
            Assert.AreEqual(4, nematet.ParallelAttackSequence.SpecialAttacks.Length);
            Assert.AreEqual("USW1", nematet.ParallelAttackSequence.SpecialAttacks[3].Name);
            Assert.AreEqual(494, nematet.ParallelAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(38, nematet.ParallelAttackSequence.SpecialAttackWeaponUnknown4);

            CapturedEnemyCombatContract guardian =
                CapturedTempleOfThreeWindsCombatCatalog.GuardianOfTomorrow();
            Assert.AreEqual(2, guardian.ParallelAttackSequence.Streams.Length);
            Assert.AreEqual(36, guardian.ParallelAttackSequence.Streams[0].Attack.MinDamage);
            Assert.AreEqual(75, guardian.ParallelAttackSequence.Streams[0].Attack.MaxDamage);
            Assert.AreEqual(1, guardian.ParallelAttackSequence.Streams[0].Attack.AttackInfoWeaponSlot);
            Assert.AreEqual(0, guardian.ParallelAttackSequence.Streams[1].Attack.AttackInfoWeaponSlot);
            Assert.AreEqual(2, guardian.ParallelAttackSequence.SpecialAttacks.Length);
            Assert.AreEqual("MPKS", guardian.ParallelAttackSequence.SpecialAttacks[0].Name);
            Assert.AreEqual("SFTN", guardian.ParallelAttackSequence.SpecialAttacks[1].Name);
            Assert.AreEqual(511, guardian.ParallelAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(39, guardian.ParallelAttackSequence.SpecialAttackWeaponUnknown4);

            CapturedEnemyCombatContract gartua =
                CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper();
            Assert.AreEqual(76, gartua.SpecialAttackSequence.RepeatingAttack.MinDamage);
            Assert.AreEqual(114, gartua.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(6, gartua.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(382, gartua.SpecialAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(37, gartua.SpecialAttackSequence.SpecialAttackWeaponUnknown4);

            LootTableDefinition table =
                CapturedTempleOfThreeWindsLootDefinitions.BuildDefenderLootTable();
            Assert.AreEqual(2, table.ObservedCorpseSnapshots.Length);
            Assert.IsTrue(table.ObservedCorpseSnapshots.All(value => value.Credits == 1450));
            Assert.AreEqual(
                1,
                table.ObservedCorpseSnapshots[0].Entries.Single(value => value.ItemTemplateId == 204750).MinimumQuantity);
            Assert.AreEqual(
                2,
                table.ObservedCorpseSnapshots[1].Entries.Single(value => value.ItemTemplateId == 204750).MinimumQuantity);
            Assert.IsTrue(
                table.ObservedCorpseSnapshots.All(
                    value => value.Entries.Single(entry => entry.ItemTemplateId == 204649).MinimumQuantity == 1));

            var registry = new LootTableRegistry(value => true);
            Assert.IsTrue(
                CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                    registry,
                    CapturedTempleOfThreeWindsLootDefinitions.DefenderProfileKey,
                    CapturedTempleOfThreeWindsLootDefinitions.DefenderEncounterKey));
            Assert.IsFalse(
                CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                    registry,
                    "subway.127.boss.abmouth-supremus",
                    "subway.127.encounter.abmouth"));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.YatilaEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.GulardProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.GulardEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.ReAnimatorEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.BetanyProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.BetanyEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.CuratorEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.NematetEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.GuardianEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.GartuaProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.GartuaEncounterKey));
            Assert.AreEqual(9, registry.Assignments().Length);
            Assert.IsTrue(registry.Assignments().All(value => value.PlayfieldId.Value == 1931));

            Assert.AreEqual(5, CapturedTempleOfThreeWindsLootDefinitions.BuildYatilaLootTable().ObservedCorpseSnapshots[0].Entries.Length);
            Assert.AreEqual(2, CapturedTempleOfThreeWindsLootDefinitions.BuildGulardLootTable().ObservedCorpseSnapshots.Length);
            Assert.AreEqual(2357, CapturedTempleOfThreeWindsLootDefinitions.BuildReAnimatorLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(50, CapturedTempleOfThreeWindsLootDefinitions.BuildBetanyLootTable().ObservedCorpseSnapshots[0].Entries[0].MinimumQuantity);
            Assert.AreEqual(377, CapturedTempleOfThreeWindsLootDefinitions.BuildCuratorLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(2711, CapturedTempleOfThreeWindsLootDefinitions.BuildNematetLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(2830, CapturedTempleOfThreeWindsLootDefinitions.BuildGuardianLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(4, CapturedTempleOfThreeWindsLootDefinitions.BuildGuardianLootTable().ObservedCorpseSnapshots[0].Entries.Length);
            Assert.AreEqual(1592, CapturedTempleOfThreeWindsLootDefinitions.BuildGartuaLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(2, CapturedTempleOfThreeWindsLootDefinitions.BuildGartuaLootTable().ObservedCorpseSnapshots[0].Entries.Length);
            Assert.AreEqual(
                200,
                CapturedTempleOfThreeWindsLootDefinitions.BuildNematetLootTable().ObservedCorpseSnapshots[0].Entries[0].FixedQuality);
        }
    }
}
