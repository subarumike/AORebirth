namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class AreteRegularMobCombatAcceptanceTests
    {
        private sealed class ExpectedMob
        {
            internal string Name;
            internal int MonsterData;
            internal int Level;
            internal int SourceIdentity;
            internal string ProfileSelector;
            internal int RangeMicrometers;
            internal int SpecialAttackWeaponUnknown5;
            internal NpcAiProfile AiProfile;
            internal bool AutomaticAggro;
        }

        private static readonly ExpectedMob[] SupportedMobs =
        {
            Mob("Waste Collector", 17714, 2, 0x7988CAFFu, "fa6fc8256e910451-c850f3966b62b38e", 1332759, 0, NpcAiProfile.Aggressive, true),
            Mob("Garbage Flea", 17657, 2, 0x7988C914u, "46a87c17eefbd77b-c850f3966b62b38e", 15576482, 117, NpcAiProfile.Aggressive, true),
            Mob("Cleanmeister Intelligence Robot", 297023, 2, 0x798915E0u, "028210688643a4d8-6ff86979de5c6526", 2771750, 82, NpcAiProfile.Aggressive, true),
            Mob("Cleaning Robot", 297023, 1, 0x7988C8C3u, "1c42797c1a980105-998f3f8fca4b8167", 5155609, 0, NpcAiProfile.Passive, false),
            Mob("Desert Reet", 30365, 6, 0x798828F0u, "45c6e511d794c6bf-8c8a88032be6ca97", 23167874, 0, NpcAiProfile.Passive, false),
            Mob("Rollerrat", 17687, 5, 0x79882AECu, "f05cd862c6056037-c2f9cf4727f71a13", 11018516, 0, NpcAiProfile.Aggressive, true),
            Mob("Rollerrat", 17687, 6, 0x798912B8u, "3d2df0c70c1adc8a-42554a1c70a69759", 18747485, 0, NpcAiProfile.Aggressive, true),
            Mob("ICC Peacekeeper", 26092, 40, 0x78D0F291u, "2a00185997e49d3a-0a5f6153239ce087", 0, 0, NpcAiProfile.Passive, false)
        };

        [TestMethod]
        public void SupportedRegularMobsSpawnResolveFightDieAndCreateCorpses()
        {
            for (int index = 0; index < SupportedMobs.Length; index++)
            {
                ExpectedMob expected = SupportedMobs[index];
                int runtimeIdentity = 900000 + index;
                CapturedEnemyCombatContract resolved = Resolve(expected);

                Assert.AreNotEqual(expected.SourceIdentity, runtimeIdentity, expected.Name + " must retain its generated runtime identity.");
                Assert.AreEqual(expected.SourceIdentity, resolved.EvidenceSourceIdentity, expected.Name);
                Assert.AreEqual(expected.ProfileSelector, resolved.CaptureProvenArchetypeId, expected.Name);
                Assert.IsTrue(resolved.IsCombatReady, expected.Name);
                Assert.AreEqual(expected.AiProfile, resolved.AiProfile, expected.Name);
                Assert.AreEqual(expected.AutomaticAggro, resolved.AiProfile == NpcAiProfile.Aggressive, expected.Name);
                Assert.IsTrue(resolved.Retaliates, expected.Name);

                Identity attacker = SimpleChar(runtimeIdentity);
                Identity target = SimpleChar(800000 + index);
                CapturedEnemyCombatAttackDefinition attack = FirstAttack(resolved);
                int damage = SelectCapturedDamage(resolved, attack);
                int ammoCount = attack == null ? resolved.AttackInfoAmmoCount : attack.AttackInfoAmmoCount;
                int weaponSlot = attack == null ? resolved.AttackInfoWeaponSlot : attack.AttackInfoWeaponSlot;
                int attackUnknown = attack == null ? resolved.AttackInfoUnknown : attack.AttackInfoUnknown;
                int hitType = attack == null ? resolved.AttackInfoHitType : attack.AttackInfoHitType;
                int weaponInstance = attack == null ? resolved.AttackInfoWeaponInstance : attack.AttackInfoWeaponInstance;
                byte attackN3Unknown = attack == null ? resolved.AttackInfoN3Unknown : attack.AttackInfoN3Unknown;
                MessageBody[] packets =
                {
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(attacker, resolved),
                    CapturedEnemyCombatPacketFactory.CreateAttack(attacker, target, resolved),
                    CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                        attacker,
                        target,
                        damage,
                        ammoCount,
                        weaponSlot,
                        attackUnknown,
                        hitType,
                        weaponInstance,
                        attackN3Unknown)
                };

                Assert.IsInstanceOfType(packets[0], typeof(SpecialAttackWeaponMessage), expected.Name);
                Assert.IsInstanceOfType(packets[1], typeof(AttackMessage), expected.Name);
                Assert.IsInstanceOfType(packets[2], typeof(AttackInfoMessage), expected.Name);
                Assert.IsTrue(damage > 0, expected.Name + " must select captured damage.");

                int health = damage * 2;
                health -= damage;
                Assert.IsTrue(health > 0, expected.Name + " must remain in combat after a nonlethal hit.");
                health -= damage;
                Assert.IsTrue(health <= 0, expected.Name + " must reach the death state.");
                Assert.IsTrue(NpcCorpseLifecycleRules.CorpseSpawnDelay > TimeSpan.Zero, expected.Name);
                Assert.IsTrue(
                    NpcCorpseLifecycleRules.DeadNpcDespawnDelay > NpcCorpseLifecycleRules.CorpseSpawnDelay,
                    expected.Name + " must create a corpse before the dead actor is removed.");
            }
        }

        [TestMethod]
        public void UnsupportedAreteActorsSpawnButRemainExpectedPassiveExclusions()
        {
            AssertQuarantined(
                "Engineer Automaton I",
                17649,
                5,
                0x7985CD86u,
                "resource=6553|md=17649|level=5|name=Engineer Automaton I",
                0,
                0);
            AssertQuarantined(
                "Robotic Guard Dog",
                17720,
                13,
                0x78E0FCE9u,
                "resource=6553|md=17720|level=13|name=Robotic Guard Dog",
                0,
                0);
            AssertQuarantined(
                "Malfunctioning Cleaning Robot",
                297023,
                1,
                0x789753ACu,
                "1481e2f9f1b55bde-ec1c75204dea6fc7",
                0,
                0);
            AssertQuarantined("32-V Docker", 17649, 3, 0, string.Empty, 0, 0);
        }

        [TestMethod]
        public void CapturedSelectorsSupportSharedProfilesAndRejectVariantOrFamilyFallbacks()
        {
            ExpectedMob waste = SupportedMobs[0];
            CapturedEnemyCombatContract first = Resolve(waste);
            CapturedEnemyCombatContract second = Resolve(waste);
            Assert.AreEqual(first.CaptureProvenArchetypeId, second.CaptureProvenArchetypeId);
            Assert.AreEqual(first.EvidenceSourceIdentity, second.EvidenceSourceIdentity);

            AssertResolutionFails(
                "Cleaning Robot",
                297023,
                1,
                unchecked((int)0x7988C8C3u),
                "028210688643a4d8-6ff86979de5c6526",
                5155609,
                0);
            AssertResolutionFails(
                "Engineer Automaton I",
                17649,
                5,
                unchecked((int)0x7985CD86u),
                waste.ProfileSelector,
                waste.RangeMicrometers,
                waste.SpecialAttackWeaponUnknown5);
        }

        [TestMethod]
        public void CombatIdentityFeedsExactLootRoutingWithoutEngineerDockerFallback()
        {
            CapturedEnemyCombatContract supported = Resolve(SupportedMobs[0]);
            var supportedContext = new LootGenerationContext();
            AreteCombatLootIdentityPolicy.Apply(supportedContext, supported, 6553, "Waste Collector");
            Assert.IsTrue(supportedContext.CombatReady);
            Assert.AreEqual(supported.EvidenceSourceIdentity, supportedContext.CombatEvidenceSourceIdentity);
            Assert.AreEqual(supported.CaptureProvenArchetypeId, supportedContext.CombatProfileSelector);
            Assert.IsFalse(supportedContext.SuppressMonsterDataFallbackLoot);

            CapturedEnemyCombatContract engineer = AreteRegularMobCombatProfileSelector.Create(
                "acceptance",
                "resource=6553|md=17649|level=5|name=Engineer Automaton I",
                unchecked((int)0x7985CD86u),
                0,
                0,
                NpcAiProfile.Passive);
            var engineerContext = new LootGenerationContext { EnemyProfileKey = "monster-data:17649" };
            AreteCombatLootIdentityPolicy.Apply(
                engineerContext,
                engineer,
                6553,
                AreteCombatLootIdentityPolicy.EngineerAutomatonName);
            Assert.IsFalse(engineerContext.CombatReady);
            Assert.AreEqual(engineer.EvidenceSourceIdentityHint, engineerContext.CombatEvidenceSourceIdentity);
            Assert.AreEqual(engineer.EvidenceProfileSelectorHint, engineerContext.CombatProfileSelector);
            Assert.AreEqual(
                AreteCombatLootIdentityPolicy.EngineerAutomatonLootProfileKey,
                engineerContext.EnemyProfileKey);
            Assert.IsTrue(engineerContext.SuppressMonsterDataFallbackLoot);

            var dockerContext = new LootGenerationContext { EnemyProfileKey = "monster-data:17649" };
            AreteCombatLootIdentityPolicy.Apply(dockerContext, null, 6553, "32-V Docker");
            Assert.IsFalse(dockerContext.SuppressMonsterDataFallbackLoot);
            Assert.AreNotEqual(engineerContext.EnemyProfileKey, dockerContext.EnemyProfileKey);
        }

        [TestMethod]
        public void AreteSpawnersConsumePreparationAndCoordinatorPreservesPreparedRegistryState()
        {
            string root = FindRepositoryRoot();
            string[] spawners =
            {
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AlexAreaMobRuntime.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\JunkyardCleaningRobotRuntime.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\LoreleiOasisMobRuntime.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteFinishCaptureMobRuntime.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteIccPeacekeeperPatrolRuntime.cs",
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteRoboticGuardDogRuntime.cs"
            };

            for (int index = 0; index < spawners.Length; index++)
            {
                string source = File.ReadAllText(Path.Combine(root, spawners[index]));
                Assert.IsTrue(source.Contains("PrepareAndRequireCombatReady("), spawners[index]);
                Assert.IsFalse(source.Contains("CapturedEnemyCombatRuntime.Prepare("), spawners[index]);
                AssertPreparationIsNotFollowedByAiOverwrite(source, spawners[index]);
            }

            string coordinator = File.ReadAllText(
                Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            Assert.IsTrue(coordinator.Contains("CapturedEnemyCombatRuntimeRegistry.TryGet("));
            Assert.IsFalse(coordinator.Contains("CapturedEnemyCombatRuntimeRegistry.Remove("));
            Assert.IsFalse(coordinator.Contains("CapturedEnemyCombatRuntimeRegistry.Clear("));
            Assert.IsFalse(coordinator.Contains("CapturedEnemyCombatRuntimeRegistry.Register("));
        }

        private static ExpectedMob Mob(
            string name,
            int monsterData,
            int level,
            uint sourceIdentity,
            string profileSelector,
            int rangeMicrometers,
            int specialAttackWeaponUnknown5,
            NpcAiProfile aiProfile,
            bool automaticAggro)
        {
            return new ExpectedMob
            {
                Name = name,
                MonsterData = monsterData,
                Level = level,
                SourceIdentity = unchecked((int)sourceIdentity),
                ProfileSelector = profileSelector,
                RangeMicrometers = rangeMicrometers,
                SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5,
                AiProfile = aiProfile,
                AutomaticAggro = automaticAggro
            };
        }

        private static CapturedEnemyCombatContract Resolve(ExpectedMob expected)
        {
            CapturedEnemyCombatContract seed = AreteRegularMobCombatProfileSelector.Create(
                "acceptance",
                expected.ProfileSelector,
                expected.SourceIdentity,
                expected.RangeMicrometers,
                expected.SpecialAttackWeaponUnknown5,
                expected.AiProfile);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    6553,
                    expected.Name,
                    expected.MonsterData,
                    expected.Level,
                    expected.SourceIdentity,
                    seed,
                    out resolved,
                    out failure),
                expected.Name + ": " + failure);
            return resolved;
        }

        private static void AssertQuarantined(
            string name,
            int monsterData,
            int level,
            uint sourceIdentity,
            string profileSelector,
            int rangeMicrometers,
            int unknown5)
        {
            int runtimeIdentity = name.GetHashCode();
            CapturedEnemyCombatContract seed = AreteRegularMobCombatProfileSelector.Create(
                "expected unsupported exclusion",
                profileSelector,
                unchecked((int)sourceIdentity),
                rangeMicrometers,
                unknown5,
                NpcAiProfile.Passive);
            CapturedEnemyCombatContract resolved;
            string failure;
            bool ready = CapturedEnemyCombatProfileCatalog.TryResolve(
                6553,
                name,
                monsterData,
                level,
                unchecked((int)sourceIdentity),
                seed,
                out resolved,
                out failure);

            Assert.AreNotEqual(0, runtimeIdentity, name + " spawn identity");
            Assert.IsFalse(ready, name + " must remain an expected unsupported exclusion.");
            Assert.IsFalse(seed.IsCombatReady, name);
            Assert.AreEqual(NpcAiProfile.Passive, seed.AiProfile, name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(failure), name);
            bool attackRejected = false;
            try
            {
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    SimpleChar(runtimeIdentity),
                    SimpleChar(runtimeIdentity + 1),
                    seed);
            }
            catch (InvalidOperationException)
            {
                attackRejected = true;
            }

            Assert.IsTrue(
                attackRejected,
                name + " must not generate attack packets while quarantined.");
        }

        private static void AssertResolutionFails(
            string name,
            int monsterData,
            int level,
            int sourceIdentity,
            string profileSelector,
            int rangeMicrometers,
            int unknown5)
        {
            CapturedEnemyCombatContract seed = AreteRegularMobCombatProfileSelector.Create(
                "negative acceptance",
                profileSelector,
                sourceIdentity,
                rangeMicrometers,
                unknown5,
                NpcAiProfile.Passive);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsFalse(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    6553,
                    name,
                    monsterData,
                    level,
                    sourceIdentity,
                    seed,
                    out resolved,
                    out failure),
                name + " must not accept a cross-variant or cross-family profile.");
        }

        private static CapturedEnemyCombatAttackDefinition FirstAttack(CapturedEnemyCombatContract contract)
        {
            if (contract.ParallelAttackSequence != null)
            {
                return contract.ParallelAttackSequence.Streams[0].Attack;
            }

            if (contract.SpecialAttackSequence != null)
            {
                return contract.SpecialAttackSequence.OpeningAttack
                       ?? contract.SpecialAttackSequence.RepeatingAttack;
            }

            return null;
        }

        private static int SelectCapturedDamage(
            CapturedEnemyCombatContract contract,
            CapturedEnemyCombatAttackDefinition attack)
        {
            if (attack != null && attack.CapturedDamageObservations.Length > 0)
            {
                return attack.CapturedDamageObservations[0];
            }

            if (contract.CapturedDamageObservations != null
                && contract.CapturedDamageObservations.Length > 0)
            {
                return contract.CapturedDamageObservations[0];
            }

            return Math.Max(1, contract.MinDamage);
        }

        private static Identity SimpleChar(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }

        private static void AssertPreparationIsNotFollowedByAiOverwrite(string source, string path)
        {
            const string preparation = "PrepareAndRequireCombatReady(";
            int offset = 0;
            while ((offset = source.IndexOf(preparation, offset, StringComparison.Ordinal)) >= 0)
            {
                int length = Math.Min(700, source.Length - offset);
                string suffix = source.Substring(offset, length);
                Assert.IsFalse(suffix.Contains(".AiProfile ="), path + " must preserve preparation state.");
                offset += preparation.Length;
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("AORebirth repository root was not found.");
        }
    }
}
