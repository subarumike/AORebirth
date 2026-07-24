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

            Assert.AreEqual(10, templeProfiles.Length);
            Assert.AreEqual(167, templeSpawns.Length);
            Assert.IsTrue(templeProfiles.All(value => value.ProfileKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(templeProfiles.All(value => !value.BossOrScripted));
            Assert.AreEqual(7, templeProfiles.Count(value => value.DisplayName == "Cultist"));
            Assert.AreEqual(1, templeProfiles.Count(value => value.DisplayName == "Eternal Sentinel"));
            Assert.AreEqual(1, templeProfiles.Count(value => value.DisplayName == "Deathless Legionnaire"));
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
            Assert.AreEqual(167, catalog.GetRuntimeSpawns(1931).Length);
            Assert.AreEqual(489, catalog.GetSpawns().Length);
            Assert.IsTrue(catalog.GetRuntimeSpawns(127).All(value => !value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
            Assert.IsTrue(catalog.GetRuntimeSpawns(1931).All(value => value.SpawnKey.StartsWith("totw.", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void DeathlessLegionnairesUseExactFrontDoorAnchorsAndGeneratedCombat()
        {
            var provider = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemyProfile profile = provider.GetProfiles().Single(
                value => value.ProfileKey
                         == CapturedTempleOfThreeWindsContentProvider
                             .DeathlessLegionnaireProfileKey);
            OrdinaryEnemySpawnDefinition[] spawns = provider.GetSpawns().Where(
                value => value.ProfileKey == profile.ProfileKey).ToArray();

            Assert.AreEqual("Deathless Legionnaire", profile.DisplayName);
            Assert.AreEqual(42981, profile.MonsterData);
            Assert.AreEqual(1611u, profile.Appearance.AppearanceValue);
            Assert.AreEqual(204735u, profile.Appearance.Meshes.Single().Id);
            Assert.AreEqual(42952, profile.Corpse.CapturedCatMesh.Value);
            Assert.AreEqual(14, spawns.Length);
            Assert.AreEqual(4, spawns.Count(value => value.Level == 48));
            Assert.AreEqual(3, spawns.Count(value => value.Level == 49));
            Assert.AreEqual(7, spawns.Count(value => value.Level == 50));
            Assert.AreEqual(5, spawns.Count(
                value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol));
            Assert.AreEqual(9, spawns.Count(
                value => value.MovementMode == OrdinaryEnemyMovementMode.Static));
            Assert.IsTrue(spawns.All(value => value.SourceCapture == "20260722-042930"));
            Assert.AreEqual(19, profile.Loot.ObservedCompleteInventories);
            Assert.AreEqual(15, profile.Loot.ObservedEmptyInventories);
            Assert.AreEqual(204746, profile.Loot.Entries.Single().LowId);
            Assert.AreEqual(
                1012,
                profile.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 48)
                    .MinimumCredits);
            Assert.AreEqual(
                1036,
                profile.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 49)
                    .MinimumCredits);
            Assert.AreEqual(
                1059,
                profile.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 50)
                    .MinimumCredits);

            Assert.AreEqual(
                OrdinaryEnemyDamageSource.WeaponRoll,
                profile.Combat.DamageSource);
            CapturedEnemyCombatProfileDefinition[] capturedProfiles =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Where(
                    value => value.MatchesArchetypeKey(
                        1931,
                        profile.DisplayName,
                        profile.MonsterData)).ToArray();
            Assert.AreEqual(2, capturedProfiles.Length);
            Assert.IsTrue(
                capturedProfiles.All(
                    value => value.SupportsCaptureProvenEquippedWeaponArchetype));
            Assert.IsTrue(
                capturedProfiles[0].MatchesCaptureProvenEquippedWeaponArchetype(
                    capturedProfiles[1]));
            string deathlessArchetypeId = null;
            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                CapturedEnemyCombatContract current =
                    profile.Combat.ResolveContract(spawn.SourceIdentity, spawn.Level);
                Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, current.AttackModel);
                CapturedEnemyCombatContract resolved;
                string failure;
                Assert.IsTrue(
                    CapturedEnemyCombatProfileCatalog.TryResolve(
                        1931,
                        profile.DisplayName,
                        profile.MonsterData,
                        spawn.Level,
                        spawn.SourceIdentity,
                        current,
                        out resolved,
                        out failure),
                    failure);
                Assert.IsTrue(resolved.IsCombatReady);
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, resolved.AttackModel);
                Assert.IsTrue(resolved.UsesCaptureProvenArchetype);
                Assert.IsTrue(resolved.UsesEquippedWeaponDamage);
                Assert.IsTrue(resolved.UsesEquippedWeaponTiming);
                Assert.AreEqual(0, resolved.MinDamage);
                Assert.AreEqual(0, resolved.MaxDamage);
                Assert.AreEqual(6, resolved.AttackInfoWeaponSlot);
                Assert.AreEqual(0, resolved.AttackInfoWeaponInstance);
                Assert.AreEqual(0, resolved.AttackInfoUnknown);
                Assert.AreEqual(3, resolved.AttackInfoHitType);
                Assert.IsNotNull(resolved.WeaponDefinition);
                Assert.AreEqual(11, resolved.WeaponDefinition.Unknown1);
                Assert.AreEqual(6, resolved.WeaponDefinition.InventorySlot);
                Assert.AreEqual(1000015, resolved.WeaponDefinition.StateMachineType);
                Assert.AreEqual(204747, resolved.WeaponDefinition.LowId);
                Assert.AreEqual(204747, resolved.WeaponDefinition.HighId);
                Assert.AreEqual(1, resolved.WeaponDefinition.Quality);
                if (deathlessArchetypeId == null)
                {
                    deathlessArchetypeId = resolved.CaptureProvenArchetypeId;
                }

                Assert.AreEqual(
                    deathlessArchetypeId,
                    resolved.CaptureProvenArchetypeId);
            }

            Assert.AreEqual(
                4,
                spawns.Count(
                    spawn =>
                    {
                        CapturedEnemyCombatContract resolved;
                        string failure;
                        return spawn.Level == 48
                               && CapturedEnemyCombatProfileCatalog.TryResolve(
                                   1931,
                                   profile.DisplayName,
                                   profile.MonsterData,
                                   spawn.Level,
                                   spawn.SourceIdentity,
                                   profile.Combat.ResolveContract(
                                       spawn.SourceIdentity,
                                       spawn.Level),
                                   out resolved,
                                   out failure)
                               && resolved.IsCombatReady;
                    }));
        }

        [TestMethod]
        public void AzturRoomBossCombatUsesTheCompleteCapture()
        {
            CapturedEnemyCombatContract ukleshCombat =
                CapturedTempleOfThreeWindsCombatCatalog.UkleshTheFrozen();
            Assert.IsTrue(ukleshCombat.IsCombatReady);
            Assert.AreEqual(
                unchecked((int)0x7987F730u),
                ukleshCombat.EvidenceSourceIdentity);
            Assert.AreEqual(2, ukleshCombat.ParallelAttackSequence.Streams.Length);
            Assert.AreEqual(
                127,
                ukleshCombat.ParallelAttackSequence.Streams[0].Attack.MinDamage);
            Assert.AreEqual(
                58,
                ukleshCombat.ParallelAttackSequence.Streams[1].Attack.MinDamage);
            CollectionAssert.AreEqual(
                new[] { 0, 20 },
                ukleshCombat.CapturedSpecialAttackWeaponUnknown5Observations);

            CapturedEnemyCombatContract khalumCombat;
            CapturedEnemyCombatContract khalumBaseline =
                CapturedTempleOfThreeWindsCombatCatalog.Khalum();
            Assert.IsTrue(khalumBaseline.Retaliates);
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    1931,
                    "Khalum",
                    95352,
                    73,
                    unchecked((int)0x7988C14Du),
                    khalumBaseline,
                    out khalumCombat,
                    out failure),
                failure);
            Assert.IsTrue(khalumCombat.IsCombatReady);
            Assert.AreEqual(2, khalumCombat.ParallelAttackSequence.Streams.Length);
            Assert.IsTrue(khalumCombat.ParallelAttackSequence.Streams.All(
                value => value.Attack.MinDamage == 58
                         && value.Attack.MaxDamage == 58));

            CapturedEnemyCombatContract azturCombat;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    1931,
                    "Aztur the Immortal",
                    159966,
                    74,
                    unchecked((int)0x7988C153u),
                    CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal(),
                    out azturCombat,
                    out failure),
                failure);
            Assert.IsTrue(azturCombat.IsCombatReady);
            Assert.AreEqual(3, azturCombat.ParallelAttackSequence.Streams.Length);
            Assert.AreEqual(
                83,
                azturCombat.ParallelAttackSequence.Streams[0].Attack.MinDamage);
            Assert.AreEqual(
                350,
                azturCombat.ParallelAttackSequence.Streams[1].Attack.MinDamage);
            Assert.AreEqual(
                157,
                azturCombat.ParallelAttackSequence.Streams[2].Attack.MinDamage);
            CollectionAssert.AreEqual(
                new[] { 0, 62, 62, 62, 62, 62, 62 },
                azturCombat.CapturedSpecialAttackWeaponUnknown5Observations);
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
            var provider = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemyProfile[] profiles = provider.GetProfiles();
            OrdinaryEnemySpawnDefinition[] spawns = provider.GetSpawns();
            OrdinaryEnemyProfile[] cultists = profiles.Where(value => value.DisplayName == "Cultist").ToArray();
            Dictionary<string, OrdinaryEnemyProfile> profilesByKey = profiles.ToDictionary(
                value => value.ProfileKey,
                StringComparer.Ordinal);
            Assert.IsTrue(profiles.All(value => value.Aggression.Mode == OrdinaryEnemyAggressionMode.Auto));
            Assert.IsTrue(profiles.All(value => value.Aggression.AutomaticAggroRadius.Value == 7.0));
            Assert.IsTrue(profiles.All(value => value.Aggression.Chase && value.Aggression.ReturnToSpawn));
            Assert.IsTrue(profiles.All(value => value.Aggression.EvidenceState == OrdinaryEnemyEvidenceState.Policy));
            Assert.IsTrue(profiles.All(value => value.Combat.EvidenceState == OrdinaryEnemyEvidenceState.Observed));
            OrdinaryEnemySpawnDefinition[] cultistSpawns = spawns.Where(
                value => value.ProfileKey.StartsWith("totw.cultist.", StringComparison.Ordinal)).ToArray();
            CapturedEnemyCombatContract[] cultistContracts = cultistSpawns.Select(
                spawn => profilesByKey[spawn.ProfileKey].Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level)).ToArray();
            Assert.AreEqual(14, cultistContracts.Count(value => value.IsCombatReady));
            Assert.AreEqual(135, cultistContracts.Count(value => value.IsQuarantined));
            foreach (CapturedEnemyCombatContract contract in cultistContracts.Where(
                value => value.IsCombatReady))
            {
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                Assert.IsNull(contract.SpecialAttackSequence);
                Assert.AreEqual(
                    CapturedTempleOfThreeWindsCombatCatalog.CultistFirstSuccessfulHitDelaySeconds,
                    contract.FirstHitDelaySeconds);
                Assert.AreEqual(15, contract.MinDamage);
                Assert.AreEqual(32, contract.MaxDamage);
                Assert.AreEqual(
                    CapturedTempleOfThreeWindsCombatCatalog.CultistRechargeSeconds,
                    contract.RechargeSeconds);
                Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
                Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
                Assert.AreEqual(3, contract.AttackInfoHitType);
                Assert.AreEqual(0, contract.AttackInfoN3Unknown);
                Assert.IsNotNull(contract.WeaponDefinition);
                Assert.AreEqual(contract.EvidenceSourceIdentity, contract.WeaponDefinition.EvidenceSourceIdentity);
                Assert.IsTrue(
                    contract.SpecialAttackWeaponUnknown5 == 0
                    || contract.SpecialAttackWeaponUnknown5 == 5);
            }
            Assert.AreEqual(74, cultists.Sum(value => value.Loot.ObservedCompleteInventories));
            Assert.AreEqual(57, cultists.Sum(value => value.Loot.ObservedEmptyInventories));
            Assert.AreEqual(17, cultists.Sum(value => value.Loot.Entries.Sum(entry => entry.ObservedCount)));
            Assert.AreEqual(74, cultists.Sum(value => value.Loot.LevelCreditRules.Sum(rule => rule.ObservedCorpses)));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Length == 16));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 20).MinimumCredits == 371));
            Assert.IsTrue(cultists.All(value => value.Loot.LevelCreditRules.Single(rule => rule.EnemyLevel == 35).MaximumCredits == 705));

            OrdinaryEnemyProfile sentinel = profiles.Single(value => value.DisplayName == "Eternal Sentinel");
            foreach (OrdinaryEnemySpawnDefinition sentinelSpawn in spawns.Where(
                value => value.ProfileKey == sentinel.ProfileKey))
            {
                Assert.IsFalse(
                    sentinel.Combat.ResolveContract(
                        sentinelSpawn.SourceIdentity,
                        sentinelSpawn.Level).IsCombatReady);
            }
            Assert.AreEqual(5, sentinel.Loot.ObservedCompleteInventories);
            Assert.AreEqual(5, sentinel.Loot.ObservedEmptyInventories);
            Assert.AreEqual(111, sentinel.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 18).MinimumCredits);
            Assert.AreEqual(124, sentinel.Loot.LevelCreditRules.Single(value => value.EnemyLevel == 20).MaximumCredits);

            OrdinaryEnemyProfile murial = profiles.Single(value => value.DisplayName == "Murial the Faithful");
            OrdinaryEnemySpawnDefinition murialSpawn = spawns.Single(
                value => value.ProfileKey == murial.ProfileKey);
            Assert.IsFalse(
                murial.Combat.ResolveContract(
                    murialSpawn.SourceIdentity,
                    murialSpawn.Level).IsCombatReady);
        }

        [TestMethod]
        public void DefenderCombatAndLootRemainDedicatedToTheTempleEncounter()
        {
            CapturedEnemyCombatContract combat =
                CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree();
            Assert.AreEqual(CapturedEnemyAttackModel.Specialized, combat.AttackModel);
            Assert.IsFalse(combat.IsCombatReady);
            Assert.AreEqual(10.915985, combat.SpecialAttackSequence.InitialAttackDelaySeconds);
            Assert.AreEqual(43, combat.SpecialAttackSequence.RepeatingAttack.MinDamage);
            Assert.AreEqual(43, combat.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(-1, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoAmmoCount);
            Assert.AreEqual(0, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(1465538645, combat.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance);
            Assert.AreEqual(
                NpcCombatAttackRules.NormalAttackInfoHitType,
                combat.SpecialAttackSequence.RepeatingAttack.AttackInfoHitType);
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
            Assert.IsTrue(yatila.ParallelAttackSequence.Streams.All(
                value => value.Attack.AttackInfoHitType
                         == NpcCombatAttackRules.NormalAttackInfoHitType));
            Assert.AreEqual(3, yatila.ParallelAttackSequence.SpecialAttacks.Length);
            Assert.AreEqual(413, yatila.ParallelAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(33, yatila.ParallelAttackSequence.SpecialAttackWeaponUnknown4);

            CapturedEnemyCombatContract[] simpleNamedContracts =
            {
                CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard(),
                CapturedTempleOfThreeWindsCombatCatalog.ReAnimator(),
                CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany()
            };
            Assert.AreEqual(37, simpleNamedContracts[0].SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(72, simpleNamedContracts[1].SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(30, simpleNamedContracts[2].SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.IsTrue(simpleNamedContracts.All(
                value => value.SpecialAttackSequence.RepeatingAttack.AttackInfoHitType
                         == NpcCombatAttackRules.NormalAttackInfoHitType));

            CapturedEnemyCombatContract curator =
                CapturedTempleOfThreeWindsCombatCatalog.TheCurator();
            Assert.AreEqual(33, curator.SpecialAttackSequence.OpeningAttack.MinDamage);
            Assert.AreEqual(57, curator.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(0, curator.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(1465538645, curator.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance);
            Assert.AreEqual(381, curator.SpecialAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(31, curator.SpecialAttackSequence.SpecialAttackWeaponUnknown4);
            Assert.AreEqual(
                NpcCombatAttackRules.NormalAttackInfoHitType,
                curator.SpecialAttackSequence.RepeatingAttack.AttackInfoHitType);

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
            Assert.IsTrue(nematet.ParallelAttackSequence.Streams.All(
                value => value.Attack.AttackInfoHitType
                         == NpcCombatAttackRules.NormalAttackInfoHitType));

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
            Assert.IsTrue(guardian.ParallelAttackSequence.Streams.All(
                value => value.Attack.AttackInfoHitType
                         == NpcCombatAttackRules.NormalAttackInfoHitType));

            CapturedEnemyCombatContract gartua =
                CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper();
            Assert.AreEqual(76, gartua.SpecialAttackSequence.RepeatingAttack.MinDamage);
            Assert.AreEqual(114, gartua.SpecialAttackSequence.RepeatingAttack.MaxDamage);
            Assert.AreEqual(6, gartua.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot);
            Assert.AreEqual(382, gartua.SpecialAttackSequence.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(37, gartua.SpecialAttackSequence.SpecialAttackWeaponUnknown4);
            Assert.AreEqual(
                NpcCombatAttackRules.NormalAttackInfoHitType,
                gartua.SpecialAttackSequence.RepeatingAttack.AttackInfoHitType);

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
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.UkleshProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.UkleshEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.KhalumProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.KhalumEncounterKey));
            Assert.IsTrue(CapturedTempleOfThreeWindsLootDefinitions.TryRegister(
                registry,
                CapturedTempleOfThreeWindsLootDefinitions.AzturProfileKey,
                CapturedTempleOfThreeWindsLootDefinitions.AzturEncounterKey));
            Assert.AreEqual(12, registry.Assignments().Length);
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
            Assert.AreEqual(625, CapturedTempleOfThreeWindsLootDefinitions.BuildUkleshLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(2, CapturedTempleOfThreeWindsLootDefinitions.BuildUkleshLootTable().ObservedCorpseSnapshots[0].Entries[0].MinimumQuantity);
            Assert.AreEqual(625, CapturedTempleOfThreeWindsLootDefinitions.BuildKhalumLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(2, CapturedTempleOfThreeWindsLootDefinitions.BuildKhalumLootTable().ObservedCorpseSnapshots[0].Entries[0].MinimumQuantity);
            Assert.AreEqual(3184, CapturedTempleOfThreeWindsLootDefinitions.BuildAzturLootTable().ObservedCorpseSnapshots[0].Credits);
            Assert.AreEqual(4, CapturedTempleOfThreeWindsLootDefinitions.BuildAzturLootTable().ObservedCorpseSnapshots[0].Entries.Length);
            Assert.AreEqual(
                200,
                CapturedTempleOfThreeWindsLootDefinitions.BuildNematetLootTable().ObservedCorpseSnapshots[0].Entries[0].FixedQuality);
        }
    }
}
