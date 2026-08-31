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
    using ZoneEngine.Core.Navigation;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class AreteRegularMobCombatAcceptanceTests
    {
        private static readonly DateTime SpatialEpoch =
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

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
            internal double ExpectedAttackRangeMeters;
        }

        private sealed class ClearSurfaceSpatialContractProvider : IPlayfieldChaseNavigationProvider
        {
            public int PlayfieldResource
            {
                get { return 6553; }
            }

            public ChaseNavigationCapability Capability
            {
                get { return ChaseNavigationCapability.Supported; }
            }

            public string GeometryVersion
            {
                get { return "clear-surface-spatial-contract-v1"; }
            }

            public bool TryProjectToSurface(
                ChaseNavigationPoint reference,
                double x,
                double z,
                out ChaseNavigationPoint projected)
            {
                projected = new ChaseNavigationPoint(x, reference.Y, z);
                return true;
            }

            public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
            {
                return start.IsFinite && end.IsFinite;
            }

            public bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
            {
                return this.IsSegmentTraversable(start, end);
            }

            public ChaseRoutePlan RequestRoute(
                ChaseNavigationPoint start,
                ChaseNavigationPoint goal,
                ChaseRouteSearchLimits limits)
            {
                return ChaseRoutePlan.Failed(
                    ChaseRoutePlanStatus.Unreachable,
                    this.GeometryVersion,
                    0,
                    0);
            }

            public bool IsRouteCurrent(ChaseRoutePlan route)
            {
                return route != null
                       && string.Equals(
                           route.GeometryVersion,
                           this.GeometryVersion,
                           StringComparison.Ordinal);
            }
        }

        private static readonly ExpectedMob[] SupportedMobs =
        {
            Mob("Waste Collector", 17714, 2, 0x7988CAFFu, "fa6fc8256e910451-c850f3966b62b38e", 0, 0, NpcAiProfile.Aggressive, true, 2.0),
            Mob("Garbage Flea", 17657, 2, 0x7988C914u, "46a87c17eefbd77b-c850f3966b62b38e", 0, 117, NpcAiProfile.Aggressive, true, 2.0),
            Mob("Cleanmeister Intelligence Robot", 297023, 2, 0x798915E0u, "028210688643a4d8-6ff86979de5c6526", 0, 82, NpcAiProfile.Aggressive, true, 2.0),
            Mob("Cleaning Robot", 297023, 1, 0x7988C8C3u, "1c42797c1a980105-998f3f8fca4b8167", 0, 0, NpcAiProfile.Passive, false, 2.0),
            Mob("Desert Reet", 30365, 6, 0x798828F0u, "45c6e511d794c6bf-8c8a88032be6ca97", 0, 0, NpcAiProfile.Passive, false, 2.0),
            Mob("Rollerrat", 17687, 5, 0x79882AECu, "f05cd862c6056037-c2f9cf4727f71a13", 0, 0, NpcAiProfile.Aggressive, true, 2.0),
            Mob("Rollerrat", 17687, 6, 0x798912B8u, "3d2df0c70c1adc8a-42554a1c70a69759", 0, 0, NpcAiProfile.Aggressive, true, 2.0),
            Mob("ICC Peacekeeper", 26092, 40, 0x78D0F291u, "2a00185997e49d3a-0a5f6153239ce087", 0, 0, NpcAiProfile.Passive, false, 0.0)
        };

        [TestMethod]
        public void SupportedRegularMobContractsResolveCapturedPacketsAndCorpseOrdering()
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

                Assert.IsTrue(NpcCorpseLifecycleRules.CorpseSpawnDelay > TimeSpan.Zero, expected.Name);
                Assert.IsTrue(
                    NpcCorpseLifecycleRules.DeadNpcDespawnDelay > NpcCorpseLifecycleRules.CorpseSpawnDelay,
                    expected.Name + " must create a corpse before the dead actor is removed.");
            }
        }

        [TestMethod]
        public void ExactTwoMetreSpatialPolicyPursuesOutsideHoldsInsideAndRepursuesAfterRetreat()
        {
            // AOtomation links the production spatial policy and navigation
            // runtime, but not the server Playfield/NPCController object graph.
            // Live FollowTarget command emission remains a server/client smoke.
            const double attackRange = 2.0;
            const double epsilon = 0.001;
            const int attackerInstance = 919999;
            const int targetInstance = 819999;

            Assert.IsTrue(
                NpcCombatSpatialPolicy.IsWithinAttackEnvelope(
                    attackRange - epsilon,
                    attackRange));
            Assert.IsFalse(
                NpcCombatSpatialPolicy.IsWithinAttackEnvelope(
                    attackRange + epsilon,
                    attackRange));
            Assert.IsTrue(
                NpcCombatSpatialPolicy.ShouldHoldMeleeFollow(
                    attackRange - epsilon,
                    attackRange));
            Assert.IsFalse(
                NpcCombatSpatialPolicy.ShouldHoldMeleeFollow(
                    attackRange + epsilon,
                    attackRange));

            double pursuitStop = NpcCombatSpatialPolicy.BuildPursuitStopDistance(attackRange);
            Assert.AreEqual(
                attackRange - NpcCombatSpatialPolicy.NavigationArrivalTolerance,
                pursuitStop,
                0.000001);

            var navigation = new NpcChaseNavigationRuntimeService(
                new ClearSurfaceSpatialContractProvider());
            var attacker = new ChaseNavigationPoint(0.0, 0.0, 0.0);
            var target = new ChaseNavigationPoint(attackRange + epsilon, 0.0, 0.0);
            NpcChaseUpdateResult approach = navigation.UpdatePursuit(
                attackerInstance,
                targetInstance,
                attacker,
                target,
                pursuitStop,
                SpatialEpoch);

            Assert.AreEqual(NpcChaseMovementKind.Direct, approach.Kind);
            Assert.IsTrue(approach.HasDestination);
            Assert.IsTrue(approach.ShouldIssueMovement);
            Assert.IsTrue(approach.Destination.Distance2D(target) <= attackRange);

            attacker = approach.Destination;
            NpcChaseUpdateResult hold = navigation.UpdatePursuit(
                attackerInstance,
                targetInstance,
                attacker,
                target,
                pursuitStop,
                SpatialEpoch + TimeSpan.FromMilliseconds(150));
            Assert.AreEqual(NpcChaseMovementKind.Hold, hold.Kind);

            target = new ChaseNavigationPoint(attackRange + 4.0, 0.0, 0.0);
            NpcChaseUpdateResult resumed = navigation.UpdatePursuit(
                attackerInstance,
                targetInstance,
                attacker,
                target,
                pursuitStop,
                SpatialEpoch + TimeSpan.FromMilliseconds(300));
            Assert.AreEqual(NpcChaseMovementKind.Direct, resumed.Kind);
            Assert.IsTrue(resumed.HasDestination);
            Assert.IsTrue(resumed.ShouldIssueMovement);
        }

        [TestMethod]
        public void ResolvedAreteMeleeContractsRequirePursuitBeforePacketEligibilityAndResumeAfterRetreat()
        {
            for (int index = 0; index < SupportedMobs.Length; index++)
            {
                ExpectedMob expected = SupportedMobs[index];
                if (expected.ExpectedAttackRangeMeters <= 0.0)
                {
                    continue;
                }

                CapturedEnemyCombatContract resolved = Resolve(expected);
                double attackRange = ResolveAttackRange(resolved);
                Assert.AreEqual(
                    expected.ExpectedAttackRangeMeters,
                    attackRange,
                    0.000001,
                    expected.Name + " must use the exact captured two-metre melee reach.");

                var navigation = new NpcChaseNavigationRuntimeService(
                    new ClearSurfaceSpatialContractProvider());
                int attackerInstance = 910000 + index;
                int targetInstance = 810000 + index;
                var attacker = new ChaseNavigationPoint(0.0, 0.0, 0.0);
                var target = new ChaseNavigationPoint(attackRange + 0.05, 0.0, 0.0);
                double outsideDistance = attacker.Distance2D(target);
                MessageBody packet;

                Assert.IsFalse(
                    TryCreateAttackAtDistance(
                        attackerInstance,
                        targetInstance,
                        resolved,
                        outsideDistance,
                        attackRange,
                        out packet),
                    expected.Name + " must not attack before closing its captured reach.");
                Assert.IsNull(packet, expected.Name);

                double pursuitStop = NpcCombatSpatialPolicy.BuildPursuitStopDistance(attackRange);
                NpcChaseUpdateResult approach = navigation.UpdatePursuit(
                    attackerInstance,
                    targetInstance,
                    attacker,
                    target,
                    pursuitStop,
                    SpatialEpoch);

                Assert.AreEqual(NpcChaseMovementKind.Direct, approach.Kind, expected.Name);
                Assert.IsTrue(approach.HasDestination, expected.Name);
                Assert.IsTrue(approach.ShouldIssueMovement, expected.Name);
                Assert.IsTrue(
                    approach.Destination.Distance2D(target) <= attackRange,
                    expected.Name + " pursuit must finish inside the attack envelope.");

                attacker = approach.Destination;
                double closedDistance = attacker.Distance2D(target);
                Assert.IsTrue(
                    TryCreateAttackAtDistance(
                        attackerInstance,
                        targetInstance,
                        resolved,
                        closedDistance,
                        attackRange,
                        out packet),
                    expected.Name + " must attack after pursuit closes the certified distance.");
                Assert.IsInstanceOfType(packet, typeof(AttackMessage), expected.Name);

                NpcChaseUpdateResult inRange = navigation.UpdatePursuit(
                    attackerInstance,
                    targetInstance,
                    attacker,
                    target,
                    pursuitStop,
                    SpatialEpoch + TimeSpan.FromMilliseconds(150));
                Assert.AreEqual(NpcChaseMovementKind.Hold, inRange.Kind, expected.Name);

                target = new ChaseNavigationPoint(attackRange + 4.0, 0.0, 0.0);
                double retreatedDistance = attacker.Distance2D(target);
                Assert.IsFalse(
                    TryCreateAttackAtDistance(
                        attackerInstance,
                        targetInstance,
                        resolved,
                        retreatedDistance,
                        attackRange,
                        out packet),
                    expected.Name + " must stop attacking after the target leaves reach.");
                Assert.IsNull(packet, expected.Name);

                NpcChaseUpdateResult resumed = navigation.UpdatePursuit(
                    attackerInstance,
                    targetInstance,
                    attacker,
                    target,
                    pursuitStop,
                    SpatialEpoch + TimeSpan.FromMilliseconds(300));
                Assert.AreEqual(NpcChaseMovementKind.Direct, resumed.Kind, expected.Name);
                Assert.IsTrue(resumed.HasDestination, expected.Name);
                Assert.IsTrue(resumed.ShouldIssueMovement, expected.Name);
                Assert.IsTrue(
                    resumed.Destination.Distance2D(target) <= attackRange,
                    expected.Name + " must resume pursuit to the certified attack envelope.");
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
                0,
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

        [TestMethod]
        public void RexPlatformCleaningRobotsDoNotDieFromAnUnconditionalLifetimeTimer()
        {
            string root = FindRepositoryRoot();
            string source = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs"));

            Assert.IsTrue(source.Contains("private const double RespawnSeconds = 60.0;"));
            Assert.IsFalse(source.Contains("LifeUntilBurnSeconds"));
            Assert.IsFalse(source.Contains("BurnBeforeExplodeSeconds"));
            Assert.IsFalse(source.Contains("TickBurnAndExplodeLifecycle"));
            Assert.IsFalse(source.Contains("Captured Arete robot explode"));
            Assert.IsFalse(source.Contains("candidate.Stats[StatIds.health].Value = 0;"));
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
            bool automaticAggro,
            double expectedAttackRangeMeters)
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
                AutomaticAggro = automaticAggro,
                ExpectedAttackRangeMeters = expectedAttackRangeMeters
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

        private static double ResolveAttackRange(CapturedEnemyCombatContract contract)
        {
            if (contract.CapturedAttackRange.HasValue)
            {
                return contract.CapturedAttackRange.Value;
            }

            CapturedEnemyCombatAttackDefinition attack = FirstAttack(contract);
            return attack == null ? 0.0 : attack.Range;
        }

        private static bool TryCreateAttackAtDistance(
            int attackerInstance,
            int targetInstance,
            CapturedEnemyCombatContract contract,
            double distance,
            double attackRange,
            out MessageBody packet)
        {
            packet = null;
            if (!NpcCombatSpatialPolicy.IsWithinAttackEnvelope(distance, attackRange))
            {
                return false;
            }

            packet = CapturedEnemyCombatPacketFactory.CreateAttack(
                SimpleChar(attackerInstance),
                SimpleChar(targetInstance),
                contract);
            return true;
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
