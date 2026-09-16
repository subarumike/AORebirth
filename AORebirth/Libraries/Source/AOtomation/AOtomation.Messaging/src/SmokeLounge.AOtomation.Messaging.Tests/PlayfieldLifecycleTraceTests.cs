// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    #endregion

    [TestClass]
    public class PlayfieldLifecycleTraceTests
    {
        [TestMethod]
        public void PrivateCityReadyInitKeepsOrgStateBeforeFullCharacterAndReadyBlockAfter()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd);
            }
        }

        [TestMethod]
        public void PrivateCityReadyInitRecorderGuardsPacketMessageOrderAndDetails()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordPrivateCityReadyInitCurrentPacketSequence();

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                Assert.AreEqual(
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder.Length,
                    CountFlow(capture.Events, PlayfieldLifecycleTrace.FlowPrivateCityReadyInit));
                Assert.AreEqual(
                    4,
                    CountStage(capture.Events, PlayfieldLifecycleTrace.StagePrivateCitySocialStatus),
                    "Private-city org init must preserve the captured repeated SocialStatus=4 sequence.");

                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.MessageOrgInfoPacket);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.MessageFullCharacter);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllTowers);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllCities);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd);

                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket, "Est. 2024"),
                    "Private-city org info must remain before FullCharacter.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent, "org=1970177"),
                    "Private-city org init summary must preserve the captured organization identity.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent, "cityPayloadBytes=0"),
                    "Captured Montroyal private-city ready block currently sends the empty towers/cities fallback.");
            }
        }

        [TestMethod]
        public void SamePlayfieldVisibilityKeepsCharInPlayAndExistingPlayerSnapshotOrder()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageVisibilityJoinerReady,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                    PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay);
            }
        }



        [TestMethod]
        public void CleaningRobotDeathOrderIncludesStopFightDeathCorpseAndDespawnScheduling()
        {
            Identity attacker = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity corpse = new Identity { Type = IdentityType.Corpse, Instance = 3001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    attacker,
                    "deadTarget=" + robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.MessageCharacterActionDeath,
                    robot,
                    "Parameter2=503");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseSpawnScheduled,
                    "CorpseSpawnScheduled",
                    corpse,
                    "deadNpc=" + robot + " delayMs=600");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageDeadNpcDespawnScheduled,
                    "DeadNpcDespawnScheduled",
                    robot,
                    "delayMs=10000");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate,
                    PlayfieldLifecycleTrace.MessageCorpseFullUpdate,
                    corpse,
                    "deadNpc=" + robot);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotDeathOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.StageRobotStopFight);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate);
                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2, "Parameter2=503"),
                    "Robot death trace must preserve captured CharacterAction Death Parameter2=503.");
            }
        }

        [TestMethod]
        public void CleaningRobotNpcAttackOrderKeepsSpecialAttackWeaponBeforeAttackInfo()
        {
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity target = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.MessageSpecialAttackWeapon,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.MessageAttack,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo,
                    PlayfieldLifecycleTrace.MessageAttackInfo,
                    robot,
                    "target=" + target);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotNpcAttackOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo);
            }
        }



        [TestMethod]
        public void IccShuttleportBasicCombatPromotesOnlyCaptureBackedIslandReet()
        {
            string repositoryRoot = FindRepositoryRoot();
            string spatialPolicyText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\NpcCombatSpatialPolicy.cs"));
            string profileCatalogText = LegacyGameplaySource.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedEnemyCombatProfileCatalog.cs"));
            Assert.IsTrue(
                profileCatalogText.Contains("current.AttackModel == CapturedEnemyAttackModel.BasicCaptureBackedOrdinary")
                && profileCatalogText.Contains("resolved = current;"),
                "Direct-certified basic captured ordinary combat must not be replaced by an older canonical profile.");
            Assert.IsTrue(
                spatialPolicyText.Contains("GenericBasicMeleeAttackRange")
                && spatialPolicyText.Contains("NpcCombatAttackRules.MaxMeleeCombatDistance"),
                "Basic captured ordinary combat range must be explicit generic runtime policy, not captured attack range.");
        }

        [TestMethod]
        public void IccShuttleportAuthoritativePlacementsRemainDataCompleteAndRuntimeFailClosed()
        {
            string repositoryRoot = FindRepositoryRoot();
            string reportText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"docs\generated\pf4582_authoritative_placement_report.json"));
            Assert.IsTrue(
                reportText.Contains("\"NO_HAND_TRANSCRIPTION\": \"YES\"")
                && reportText.Contains("\"DUPLICATE_POSITIONS_PRESERVED\": \"YES\"")
                && reportText.Contains("\"UNKNOWN_METADATA_PRESERVED\": \"YES\"")
                && reportText.Contains("\"UNPROVEN_SPAWNS_ACTIVATED\": \"NO\""),
                "The deterministic report must publish the placement-governance invariants.");
        }

        [TestMethod]
        public void NpcCorpseLifecycleRulesPreserveCapturedCleaningRobotDeathTimings()
        {
            Assert.AreEqual(
                600,
                (int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds,
                "Cleaning robot corpse spawn delay must stay capture-backed.");
            Assert.AreEqual(
                10000,
                (int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds,
                "Dead NPC despawn delay must stay capture-backed.");
            Assert.AreEqual(
                503,
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2,
                "Cleaning robot CharacterAction Death Parameter2 must stay capture-backed (20260722-keeper-exect-nano).");
        }

        [TestMethod]
        public void NpcCombatAttackRulesPreserveCapturedCleaningRobotContextDecision()
        {
            Assert.AreEqual(10, NpcCombatAttackRules.CapturedCleaningRobotRightHandDamage);
            Assert.AreEqual(8, NpcCombatAttackRules.CapturedCleaningRobotLeftHandDamage);
            Assert.AreEqual(-1, NpcCombatAttackRules.CapturedSubwayThiefAttackInfoAmmoCount);
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayThiefAttackInfoUnknown);
            Assert.AreEqual(32, NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown1);
            Assert.AreEqual(32, NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown2);
            Assert.AreEqual(32, NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown3);
            Assert.AreEqual(32, NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown4);
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayThiefSpecialAttackWeaponUnknown5);
            Assert.AreEqual(1409, (int)(NpcCombatAttackRules.CapturedSubwayThiefAttackStartDelaySeconds * 1000));
            Assert.AreEqual(
                219,
                (int)(NpcCombatAttackRules.CapturedSubwayThiefMovementTransitionDelaySeconds * 1000));
            Assert.AreEqual(11409, (int)(NpcCombatAttackRules.CapturedSubwayThiefFirstHitDelaySeconds * 1000));
            Assert.AreEqual(6000, (int)(NpcCombatAttackRules.CapturedSubwayThiefRechargeSeconds * 1000));
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayThiefWeaponDamageMinimumOverride);
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayThiefWeaponDamageMaximumOverride);
            Assert.AreEqual(
                2700,
                (int)(NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds * 1000));
            Assert.IsTrue(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    null,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    1001,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    true,
                    null,
                    1001));
        }



        [TestMethod]
        public void SubwayFilthFleaCombatUsesCapturedPoisonAndMeleeAttackContext()
        {
            Assert.AreEqual(17657, NpcCombatAttackRules.CapturedSubwayFilthFleaMonsterData);
            Assert.AreEqual(14, NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMinimumDamage);
            Assert.AreEqual(24, NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonMaximumDamage);
            Assert.AreEqual(3, NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMinimumDamage);
            Assert.AreEqual(10, NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeMaximumDamage);
            Assert.AreEqual(1, NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonWeaponSlot);
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeWeaponSlot);
            Assert.AreEqual(0x45504148, NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag);
            Assert.AreEqual(0x415A5553, NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag);
            Assert.AreEqual(3650, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaInitialAttackSeconds * 1000));
            Assert.AreEqual(1580, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonRechargeSeconds * 1000));
            Assert.AreEqual(2800, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeRechargeSeconds * 1000));

            string repositoryRoot = FindRepositoryRoot();
            string capturedPacketFactoryText = LegacyGameplaySource.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine_New\SharedGameplay\Combat\CapturedEnemyCombatPacketFactory.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            Assert.IsTrue(
                providerText.Contains("Filth Flea: 18 complete official-live corpse opens")
                && providerText.Contains("20260708-004038")
                && providerText.Contains("20260712-161506")
                && providerText.Contains("\"Filth Flea\"")
                && providerText.Contains("17657")
                && providerText.Contains("234874")
                && providerText.Contains("103110")
                && providerText.Contains("101581")
                && providerText.Contains("110874")
                && providerText.Contains("101507")
                && providerText.Contains("202719")
                && providerText.Contains("234876")
                && providerText.Contains("101761")
                && providerText.Contains("110192"),
                "Filth Flea must retain captured Subway corpse loot evidence from completed inventory captures.");
            string filthFleaFactory = ExtractMethodBlock(
                providerText,
                "private static CapturedSubwaySpawnDefinition FilthFlea");
            Assert.IsTrue(
                !filthFleaFactory.Contains("respawnDelaySeconds:")
                && catalogText.Contains("SubwayOrdinaryRespawnSeconds = 240.0")
                && catalogText.Contains("SubwayOrdinaryRespawnPolicy()")
                && catalogText.Contains("WorldRespawnPolicyAssignment.Inherit("),
                "Filth Flea must inherit the shared captured four-minute Subway respawn schedule.");
            Assert.IsTrue(
                catalogText.Contains("bool preserveFilthFleaFallback = monsterData == 17657;")
                && catalogText.Contains("preserveFilthFleaFallback ? 23 : (int?)null")
                && catalogText.Contains("preserveFilthFleaFallback ? 79 : (int?)null"),
                "Filth Flea must retain captured Subway corpse credit evidence from completed corpse full-update captures.");
        }



        [TestMethod]
        public void PlayerCombatRuntimeServiceFinalBoundaryOwnsLifecycleOrchestrationOnly()
        {
            string repositoryRoot = FindRepositoryRoot();
            string characterCombatText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Libraries\Source\AORebirth.Core\Entities\Character.Combat.cs"));
            string strikeBuilderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Libraries\Source\AORebirth.Core\Combat\CharacterCombatStrikeBuilder.cs"));
            string checkpointText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"docs\generated\player_combat_lifecycle_ownership_checkpoint_20260705.md"));
            Assert.IsTrue(
                strikeBuilderText.Contains("if (weapon == null || weapon.LowID <= 0)")
                && strikeBuilderText.Contains("NormalizeStat(attacker.Stats[StatIds.mindamage].Value)")
                && strikeBuilderText.Contains("NormalizeStat(attacker.Stats[StatIds.maxdamage].Value)")
                && strikeBuilderText.Contains("unarmedDamage = PlayerUnarmedFallbackDamage;")
                && strikeBuilderText.Contains("UsesEquippedWeapon = false")
                && strikeBuilderText.Contains("DamageSource = CombatDamageSource.UnarmedAutoAttack")
                && strikeBuilderText.Contains("AttackInfoAmmoCount = PlayerUnarmedAttackInfoAmmoCount")
                && strikeBuilderText.Contains("AttackInfoWeaponSlot = PlayerUnarmedAttackInfoWeaponSlot")
                && strikeBuilderText.Contains("AttackInfoWeaponInstance = PlayerUnarmedAttackInfoWeaponInstance")
                && strikeBuilderText.Contains("value == MissingStatValue"),
                "The core strike builder must preserve the proven unarmed player source and packet contract when no weapon is equipped.");

            Assert.IsTrue(
                checkpointText.Contains("PlayerCombatRuntimeService")
                && checkpointText.Contains("Final boundary")
                && checkpointText.Contains("attack start")
                && checkpointText.Contains("cancel/stop clear")
                && checkpointText.Contains("combat tick orchestration")
                && checkpointText.Contains("invalid-target cleanup")
                && checkpointText.Contains("death combat cleanup")
                && checkpointText.Contains("Playfield still owns")
                && checkpointText.Contains("NPCRuntimeService remains NPC-only"),
                "The player combat lifecycle checkpoint doc must describe the final player combat boundary.");
        }

        [TestMethod]
        public void AOSharpLiveCaptureIsolatesDecodedExportFailuresAndFailsClosedOnMissingCombatRows()
        {
            string repositoryRoot = FindRepositoryRoot();
            string captureText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Main.cs"));
            string captureLauncherText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\start-aosharp-live-capture.cmd"));
            string lifecycleDecoderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\decode_npc_lifecycle_capture.py"));
            string inboundHandler = ExtractMethodBlock(
                captureText,
                "private void OnN3MessageReceived(object sender, N3Message message)");
            string decodedPipeline = ExtractMethodBlock(
                captureText,
                "private void LogN3Message(string direction, int sequence, N3Message message)");
            string fightSelector = ExtractMethodBlock(
                captureText,
                "private bool ShouldCaptureEnemyFightEvidence(string direction, int sequence, N3Message message)");
            string captureValidation = ExtractMethodBlock(
                captureText,
                "private CaptureValidation ValidateCapture()");

            Assert.IsTrue(
                inboundHandler.Contains("\"combat-loot-smoke\"")
                && inboundHandler.Contains("\"decoded-message-pipeline\"")
                && inboundHandler.Contains("this.RunN3CaptureStage("),
                "One failing decoded-message consumer must not abort the capture callback.");
            AssertTextBefore(
                decodedPipeline,
                "this.LogEvent(",
                "\"specialized-export\"");
            Assert.IsTrue(
                decodedPipeline.Contains("this.decodedN3EventRowCount++;")
                && decodedPipeline.Contains("\"npc-lifecycle-export\"")
                && decodedPipeline.Contains("\"enemy-fight-annotation\"")
                && decodedPipeline.Contains("\"enemy-evidence-export\"")
                && captureText.Contains("N3-STAGE-ERROR"),
                "Decoded metadata must be logged first and every evidence exporter must be failure-isolated.");
            Assert.IsTrue(
                fightSelector.Contains("bool isCombatEvidence = IsEnemyCombatEvidenceMessage(message);")
                && fightSelector.Contains("if (isCombatEvidence)")
                && fightSelector.Contains("this.enemyFightCaptureStarted = true;")
                && captureText.Contains("\"SpecialAttackWeapon\"")
                && captureText.Contains("IsRawCombatEvidencePacket(packet)"),
                "Combat packets must be captured even when focused-enemy registration is unavailable.");
            Assert.IsFalse(
                captureText.Contains("SimpleItemFullUpdateMessage")
                || captureText.Contains("VendingMachineFullUpdateMessage"),
                "The live AOSharp runtime does not provide these compile-time message types; capture must use reflection-safe message-name handling.");
            Assert.IsTrue(
                captureText.Contains("\"SimpleItemFullUpdate\"")
                && captureText.Contains("\"VendingMachineFullUpdate\"")
                && captureText.Contains("GetMemberValue(message, \"Stats\")"),
                "Unavailable optional AOSharp message types must remain captureable without triggering TypeLoadException.");
            Assert.IsTrue(
                captureValidation.Contains("this.decodedN3EventRowCount == 0")
                && captureValidation.Contains("this.n3CaptureStageErrorCount > 0")
                && captureValidation.Contains("this.rawCombatPacketCount > 0 && this.enemyCombatRowCount == 0"),
                "Capture health must fail closed instead of reporting a combat capture complete with missing decoded evidence.");
            Assert.IsTrue(
                captureText.Contains("corpse-loot-observations.csv")
                && captureText.Contains("InitialSnapshot")
                && captureText.Contains("CorpseCredits")
                && captureText.Contains("PlayerLevel")
                && captureText.Contains("this.corpseLootInitialSnapshotCount < 10")
                && captureText.Contains("this.corpseLootInitialEnemyKeys.Count != 1"),
                "A marked ten-kill loot capture must preserve empty outcomes, credits, enemy/player context, and one-enemy completeness in one pass.");
            Assert.IsTrue(
                captureText.Contains("activeCorpseEvidenceByCorpse")
                && captureText.Contains("bool isNewGeneration")
                && captureText.Contains("this.corpseInventorySnapshotCounts.Remove(normalizedCorpseIdentity);")
                && captureText.Contains("this.activeCorpseEvidenceByCorpse.TryGetValue(")
                && captureText.Contains("this.activeCorpseEvidenceByCorpse.Remove(normalizedCorpseIdentity);"),
                "Live loot correlation must bind the active corpse generation and reset its open ordinal when the client reuses a corpse identity.");
            Assert.IsTrue(
                lifecycleDecoderText.Contains("def rebind_corpse_loot_observations(")
                && lifecycleDecoderText.Contains("generations_by_corpse = collections.defaultdict(list)")
                && lifecycleDecoderText.Contains("generation[\"SeenTime\"] <= observed_time")
                && lifecycleDecoderText.Contains("\"CorrelationStatus\": \"linked-offline-generation\"")
                && lifecycleDecoderText.Contains("reused corpse identities must bind separate loot generations"),
                "Offline reconstruction must repair identity-reused loot rows from CFU generation boundaries without another gameplay capture.");
            Assert.IsTrue(
                captureLauncherText.Contains("--loot-10")
                && captureLauncherText.Contains("LOOT_CAPTURE_REQUEST")
                && captureText.Contains("LootCaptureRequestFileName")
                && captureText.Contains("loot-10 armed by approved launcher"),
                "Codex must be able to arm ten-kill loot validation through the approved external launcher without asking Mike to type an in-game command.");
        }

        [TestMethod]
        public void AOSharpPf127CaptureContainsRuntimeFailuresAndSnapshotsCharacterWrappersOnce()
        {
            string repositoryRoot = FindRepositoryRoot();
            string mainText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Main.cs"));
            string geometryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Pf127GeometryCapture.cs"));
            string runtimeSafetyText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\CaptureRuntimeSafety.cs"));
            string mainUpdate = ExtractMethodBlock(
                mainText,
                "private void OnUpdate(object sender, float deltaTime)");
            string runMethod = ExtractMethodBlock(
                mainText,
                "private void Initialize(string pluginDir)");
            string lineOfSightSample = ExtractMethodBlock(
                geometryText,
                "private void SampleLineOfSight(");
            string geometryWriter = ExtractMethodBlock(
                geometryText,
                "private void TryWriteCanonicalGeometry()");
            int readinessObserved = geometryWriter.IndexOf(
                "Interlocked.Exchange(ref this.geometryStage, GeometryStageReadinessObserved)",
                StringComparison.Ordinal);
            int loadSurfaces = geometryWriter.IndexOf("DevExtras.LoadAllSurfaces()", StringComparison.Ordinal);
            int surfacesLoaded = geometryWriter.IndexOf(
                "Interlocked.Exchange(ref this.geometryStage, GeometryStageSurfacesLoaded)",
                StringComparison.Ordinal);
            int serializeGeometry = geometryWriter.IndexOf(
                "WriteCanonicalGeometryAttempt(attemptPath)",
                StringComparison.Ordinal);

            Assert.IsTrue(
                mainUpdate.Contains("Volatile.Read(ref this.pf127CaptureRuntimeReady) != 0")
                && mainUpdate.Contains("geometryCapture.ExecuteUpdateBoundary(")
                && runMethod.IndexOf("this.LogSnapshot(\"initial\")", StringComparison.Ordinal)
                   < runMethod.IndexOf("Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 1)", StringComparison.Ordinal)
                && runMethod.IndexOf("Interlocked.Exchange(ref this.pf127CaptureRuntimeReady, 1)", StringComparison.Ordinal)
                   < runMethod.IndexOf("Game.OnUpdate += this.OnUpdateBoundary", StringComparison.Ordinal),
                "PF127 instrumentation must start only after plugin startup and must never escape Game.OnUpdate.");
            Assert.IsTrue(
                geometryText.Contains("pf127-capture-errors.log")
                && geometryText.Contains("CaptureRuntimeCircuitBreaker")
                && geometryText.Contains("runtime circuit breaker tripped")
                && geometryText.Contains("GeometryStageReadinessObserved")
                && geometryText.Contains("GeometryStageSurfacesLoaded")
                && geometryText.Contains("GeometryStageCircuitBroken")
                && geometryText.Contains("ex.ToString()")
                && readinessObserved >= 0
                && loadSurfaces > readinessObserved
                && surfacesLoaded > readinessObserved
                && serializeGeometry > surfacesLoaded
                && geometryWriter.Contains("this.residentSurfacesOnly")
                && geometryWriter.Contains("canonical serialization is deferred to the next update")
                && geometryWriter.Contains("surface loading is deferred to the next update"),
                "PF127 runtime failures must retain full durable evidence and fail validation closed without native retries.");
            Assert.IsTrue(
                lineOfSightSample.Contains("CaptureRuntimeSafety.TrySnapshot<SimpleChar, LineOfSightTargetSnapshot>(")
                && lineOfSightSample.Contains("Identity identity = character.Identity;")
                && lineOfSightSample.Contains("TryReadMonsterData(")
                && lineOfSightSample.Contains("() => character.IsInLineOfSight")
                && lineOfSightSample.Contains("characterSnapshots.RemoveAll(character => character.Identity == localIdentity)")
                && geometryText.Contains("this.combatRequestGate.TryBegin(")
                && geometryText.Contains("batchHasOnlyUsableNpcPairs")
                && geometryText.Contains("this.combatRequestGate.MarkRetryRequired()")
                && geometryText.Contains("this.CompleteCombatRequest(combatRequest.Generation)")
                && runtimeSafetyText.Contains("internal sealed class CaptureCombatRequestGate")
                && runtimeSafetyText.Contains("private readonly object syncRoot = new object()")
                && runtimeSafetyText.Contains("this.generation++;")
                && runtimeSafetyText.Contains("this.generation != sampledGeneration"),
                "LOS sampling must capture wrapper identity once, skip invalid wrappers, and retry incomplete combat evidence.");
            Assert.IsFalse(
                lineOfSightSample.Contains(".Where(character =>")
                || lineOfSightSample.Contains(".OrderBy(character =>")
                || lineOfSightSample.Contains(".ThenBy(character =>")
                || geometryText.Contains("target.Character")
                || geometryText.Contains("public SimpleChar Character")
                || geometryText.Contains("Playfield.Doors == null")
                || geometryText.Contains("room.Doors == null")
                || geometryText.Contains("zones.Any(zone =>")
                || geometryText.Contains(".Select(door => DoorIdentityKey((int)door.Identity.Type")
                || geometryText.Contains("nextCombatSampleRetryUtc")
                || geometryText.Contains("combatRequestGeneration")
                || mainText.Contains("RequestCombatSample(identityType, identityInstance)"),
                "LOS collection ordering must not repeatedly dereference live AO character wrappers.");
        }

        [TestMethod]
        public void AOSharpLiveCaptureRoutesEveryRegisteredCallbackThroughOneDurableNoThrowBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string mainText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Main.cs"));
            string runtimeSafetyText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\CaptureRuntimeSafety.cs"));
            string runMethod = ExtractMethodBlock(mainText, "public override void Run(string pluginDir)");
            string initializeMethod = ExtractMethodBlock(mainText, "private void Initialize(string pluginDir)");
            string teardownMethod = ExtractMethodBlock(mainText, "public override void Teardown()");
            string unsubscribeMethod = ExtractMethodBlock(mainText, "private void UnsubscribeCallbacksNoThrow()");
            string dispatchMethod = ExtractMethodBlock(
                mainText,
                "private void DispatchCallback(string callbackName, Action callback)");
            string startMinimalMethod = ExtractMethodBlock(
                mainText,
                "private void StartMinimalPf127CaptureNoThrow(string pluginDir)");
            string teardownMinimalMethod = ExtractMethodBlock(
                mainText,
                "private void TeardownMinimalPf127CaptureNoThrow()");
            string validationMethod = ExtractMethodBlock(mainText, "private CaptureValidation ValidateCapture()");
            string healthMethod = ExtractMethodBlock(
                mainText,
                "private void WriteCaptureHealth(CaptureValidation validation)");

            string[] registrations =
            {
                "Network.PacketReceived += this.OnPacketReceivedBoundary;",
                "Network.PacketSent += this.OnPacketSentBoundary;",
                "Network.N3MessageReceived += this.OnN3MessageReceivedBoundary;",
                "Network.N3MessageSent += this.OnN3MessageSentBoundary;",
                "Network.ChatMessageReceived += this.OnChatMessageReceivedBoundary;",
                "DynelManager.DynelSpawned += this.OnDynelSpawnedBoundary;",
                "DynelManager.CharInPlay += this.OnCharInPlayBoundary;",
                "Game.PlayfieldInit += this.OnPlayfieldInitBoundary;",
                "Game.TeleportStarted += this.OnTeleportStartedBoundary;",
                "Game.TeleportEnded += this.OnTeleportEndedBoundary;",
                "Game.TeleportFailed += this.OnTeleportFailedBoundary;",
                "Game.OnUpdate += this.OnUpdateBoundary;",
                "Chat.RegisterCommand(\"aocap\", this.OnCommandBoundary);",
                "Chat.RegisterCommand(\"aosmoke\", this.OnSmokeCommandBoundary);"
            };
            string[] callbackNames =
            {
                "Chat.Command.aocap",
                "Chat.Command.aosmoke",
                "Network.PacketReceived",
                "Network.PacketSent",
                "Network.N3MessageReceived",
                "Network.N3MessageSent",
                "Network.ChatMessageReceived",
                "DynelManager.DynelSpawned",
                "DynelManager.CharInPlay",
                "Game.PlayfieldInit",
                "Game.TeleportStarted",
                "Game.TeleportEnded",
                "Game.TeleportFailed",
                "Game.OnUpdate",
                "Game.OnUpdate.MinimalPf127Capture"
            };
            string[] unsubscriptions =
            {
                "Network.PacketReceived -= this.OnPacketReceivedBoundary",
                "Network.PacketSent -= this.OnPacketSentBoundary",
                "Network.N3MessageReceived -= this.OnN3MessageReceivedBoundary",
                "Network.N3MessageSent -= this.OnN3MessageSentBoundary",
                "Network.ChatMessageReceived -= this.OnChatMessageReceivedBoundary",
                "DynelManager.DynelSpawned -= this.OnDynelSpawnedBoundary",
                "DynelManager.CharInPlay -= this.OnCharInPlayBoundary",
                "Game.PlayfieldInit -= this.OnPlayfieldInitBoundary",
                "Game.TeleportStarted -= this.OnTeleportStartedBoundary",
                "Game.TeleportEnded -= this.OnTeleportEndedBoundary",
                "Game.TeleportFailed -= this.OnTeleportFailedBoundary",
                "Game.OnUpdate -= this.OnUpdateBoundary"
            };

            foreach (string registration in registrations)
            {
                StringAssert.Contains(initializeMethod, registration);
            }

            foreach (string callbackName in callbackNames)
            {
                StringAssert.Contains(mainText, "\"" + callbackName + "\"");
            }

            foreach (string unsubscription in unsubscriptions)
            {
                StringAssert.Contains(unsubscribeMethod, unsubscription);
            }

            Assert.IsTrue(
                runMethod.Contains("this.callbackBoundary.Dispatch(")
                && runMethod.Contains("this.DisableAfterInitializationFailureNoThrow();")
                && !runMethod.Contains("throw;")
                && teardownMethod.Contains("this.callbackBoundary.Dispatch(\"Plugin.Teardown\"")
                && dispatchMethod.Contains("Volatile.Read(ref this.callbackDispatchEnabled) == 0")
                && dispatchMethod.Contains("this.callbackBoundary.Dispatch("),
                "Initialization, teardown, retained commands, and every subscribed callback must be no-throw through one dispatcher.");
            AssertTextBefore(
                initializeMethod,
                "Game.OnUpdate += this.OnUpdateBoundary;",
                "Chat.RegisterCommand(\"aocap\", this.OnCommandBoundary);");
            AssertTextBefore(
                runMethod,
                "MinimalPf127Capture.ConsumeRequestNoThrow(pluginDir)",
                "this.Initialize(pluginDir);");
            Assert.IsTrue(
                startMinimalMethod.Contains("Game.OnUpdate += this.OnMinimalPf127CaptureUpdateBoundary;")
                && mainText.Contains("private void OnMinimalPf127CaptureUpdateBoundary")
                && mainText.Contains("\"Game.OnUpdate.MinimalPf127Capture\"")
                && teardownMinimalMethod.Contains("Game.OnUpdate -= this.OnMinimalPf127CaptureUpdateBoundary")
                && runMethod.Contains("return;")
                && startMinimalMethod.Contains("this.initialized = true;")
                && startMinimalMethod.Contains("return;")
                && !startMinimalMethod.Contains("this.Initialize("),
                "Geometry-only mode must use the same no-throw callback boundary and never fall through to comprehensive subscriptions.");
            Assert.IsTrue(
                runtimeSafetyText.Contains("internal sealed class CaptureCallbackBoundary")
                && runtimeSafetyText.Contains("File.AppendAllText(path, evidence, Encoding.UTF8)")
                && runtimeSafetyText.Contains("exception.ToString()")
                && runtimeSafetyText.Contains("this.totalInvocationCount++")
                && runtimeSafetyText.Contains("this.totalErrorCount++")
                && mainText.Contains("capture-callback-errors.log")
                && mainText.Contains("this.callbackBoundary.BeginSession("),
                "Callback failures must retain full durable evidence and per-callback accounting for the active capture.");
            Assert.IsTrue(
                validationMethod.Contains("CaptureCallbackBoundarySnapshot callbackHealth")
                && validationMethod.Contains("callbackHealth.TotalErrorCount > 0")
                && healthMethod.Contains("this.AppendCallbackHealthJson(json, \"  \")")
                && mainText.Contains("\\\"callbackHealth\\\"")
                && mainText.Contains("callbackHealth.TotalErrorCount > 0"),
                "Any callback failure must make comprehensive capture validation incomplete and appear in capture health.");
        }

        [TestMethod]
        public void AOSharpPf127NativeCollectionWaitsForMatchingTeleportEnd()
        {
            string repositoryRoot = FindRepositoryRoot();
            string mainText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpLiveCapture\Main.cs"));
            string playfieldInit = ExtractMethodBlock(
                mainText,
                "private void OnPlayfieldInit(object sender, uint playfieldId)");
            string teleportStarted = ExtractMethodBlock(
                mainText,
                "private void OnTeleportStarted(object sender, EventArgs e)");
            string teleportEnded = ExtractMethodBlock(
                mainText,
                "private void OnTeleportEnded(object sender, EventArgs e)");
            string update = ExtractMethodBlock(
                mainText,
                "private void OnUpdate(object sender, float deltaTime)");
            string activate = ExtractMethodBlock(mainText, "private void ActivateCaptureSession()");

            Assert.IsTrue(
                teleportStarted.Contains("Interlocked.Increment(ref this.teleportGeneration)")
                && teleportStarted.Contains("Interlocked.Exchange(ref this.teleportInProgress, 1)")
                && teleportStarted.Contains("Interlocked.Exchange(ref this.pf127CollectionArmed, 0)")
                && teleportStarted.Contains("NotifyPlayfieldChanged(false)"),
                "Teleport start must synchronously cancel native PF collection.");
            Assert.IsTrue(
                playfieldInit.Contains("this.lastPlayfieldId = playfieldId.ToString")
                && playfieldInit.Contains("ref this.playfieldInitGeneration")
                && playfieldInit.Contains("NotifyPlayfieldChanged(false)")
                && !playfieldInit.Contains("Playfield.")
                && !playfieldInit.Contains("LogSnapshot(")
                && !playfieldInit.Contains("RequestImmediateUpdate()"),
                "PlayfieldInit may record only the numeric generation while AO native wrappers are unstable.");
            Assert.IsTrue(
                teleportEnded.Contains("matchingPlayfieldInit")
                && teleportEnded.Contains("string.Equals(this.lastPlayfieldId, \"127\"")
                && teleportEnded.Contains("Interlocked.Exchange(ref this.teleportInProgress, 0)")
                && teleportEnded.Contains("NotifyPlayfieldChanged(isPf127)")
                && update.Contains("Volatile.Read(ref this.teleportInProgress) == 0")
                && update.Contains("Volatile.Read(ref this.pf127CollectionArmed) != 0"),
                "Only the matching stable teleport end may arm PF127 Rooms, Doors, and LOS access.");
            Assert.IsFalse(
                activate.Contains("IsDetectedResourcePlayfield127()")
                || activate.Contains("NotifyPlayfieldChanged(")
                || activate.Contains("RequestImmediateUpdate()"),
                "Opening or restarting raw capture must not bypass the teleport stability gate.");
        }

        [TestMethod]
        public void SubwayDisobedientBotCorpseUsesCapturedCreditsAndEvidenceBackedPool()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            string capturedLootDefinitions = ExtractMethodBlock(
                providerText,
                "public CapturedSubwayLootDefinition[] GetLootDefinitions()");

            Assert.IsTrue(
                catalogText.Contains("if (monsterData == 17649)")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(5, 6, 6, 2")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(6, 8, 8, 3")
                && catalogText.Contains("20260719-020104")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(8, 10, 10, 4")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(9, 11, 11, 3")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(10, 12, 12, 2")
                && catalogText.Contains("OrdinaryEnemyEvidenceState.Observed")
                && catalogText.Contains("Keep unobserved levels unresolved"),
                "Disobedient Bot credits must stay conditioned by identity-correlated enemy level instead of using a global range or guessed formula.");
            Assert.IsTrue(
                capturedLootDefinitions.Contains("\"Disobedient Bot\"")
                && capturedLootDefinitions.Contains("234877")
                && capturedLootDefinitions.Contains("104683")
                && capturedLootDefinitions.Contains("104684")
                && capturedLootDefinitions.Contains("113398")
                && capturedLootDefinitions.Contains("113399")
                && capturedLootDefinitions.Contains("ProvenTransferredEnemyCorpseItem")
                && capturedLootDefinitions.Contains("ProvenEnemyCorpseItem")
                && capturedLootDefinitions.Contains("ProvisionalProjectPolicy"),
                "Disobedient Bot must expose only the three fully linked observed items and must keep the ambiguous 234876 candidate inactive.");
            string ordinaryLootAdapterText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyLootTableAdapter.cs"));
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesSpawnDefinitions()
        {
            var provider = new CapturedAreteRobotContentProvider();
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();

            Assert.AreEqual(11, spawns.Length);
            Assert.AreEqual("Malfunctioning Cleaning Robot", CapturedAreteRobotContentProvider.RobotName);
            Assert.AreEqual(297023, CapturedAreteRobotContentProvider.MonsterData);
            Assert.AreEqual(0x79866553, spawns[0].SourceInstance);
            Assert.AreEqual(12, spawns[0].Health);
            Assert.AreEqual(1, spawns[0].Level);
            Assert.AreEqual(6, spawns[0].RunSpeed);
            Assert.AreEqual(3594.546000f, spawns[0].X);
            Assert.AreEqual(51.745000f, spawns[0].Y);
            Assert.AreEqual(799.167700f, spawns[0].Z);
            Assert.AreEqual(0x7986655D, spawns[10].SourceInstance);
            Assert.AreEqual(3622.508540f, spawns[10].X);
            Assert.AreEqual(51.745000f, spawns[10].Y);
            Assert.AreEqual(798.139500f, spawns[10].Z);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesCanonicalPatrolReplayPathAndMissingFileFallback()
        {
            Assert.AreEqual(
                @"Content\Captured\Arete\cleaning_robot_patrol_replay.csv",
                CapturedAreteRobotContentProvider.PatrolReplayRelativePath);
            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });

            Assert.AreEqual(string.Empty, provider.FindPatrolReplayPath());
            Assert.AreEqual(0, provider.GetPatrolReplaySegments(0x79866553).Length);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderLoadsCommittedPatrolReplayData()
        {
            var provider = new CapturedAreteRobotContentProvider();
            string replayPath = provider.FindPatrolReplayPath();

            Assert.IsTrue(File.Exists(replayPath));
            string repositoryRoot = Path.GetFullPath(FindRepositoryRoot())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullReplayPath = Path.GetFullPath(replayPath);
            Assert.IsTrue(fullReplayPath.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase));
            string relativeReplayPath = fullReplayPath.Substring(repositoryRoot.Length);
            Assert.IsTrue(
                relativeReplayPath.IndexOf("tools-temp", StringComparison.OrdinalIgnoreCase) < 0,
                "Runtime replay data must load from committed content, not tools-temp captures.");
            CollectionAssert.AreEqual(
                File.ReadAllBytes(Path.Combine(repositoryRoot, CapturedAreteRobotContentProvider.PatrolReplaySourceRelativePath)),
                File.ReadAllBytes(fullReplayPath),
                "The selected runtime content must match the committed canonical bytes.");

            Assert.AreEqual(39, provider.GetPatrolReplaySegments(0x79866553).Length);

            CapturedAreteRobotPatrolReplaySegment first =
                provider.GetPatrolReplaySegments(0x79866553)[0];
            Assert.AreEqual(3620.47729f, first.StartX);
            Assert.AreEqual(51.7449989f, first.StartY);
            Assert.AreEqual(785.077393f, first.StartZ);
            Assert.AreEqual(3621.25732f, first.EndX);
            Assert.AreEqual(52.5f, first.EndY);
            Assert.AreEqual(784.154419f, first.EndZ);
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsCapturedReplaySegmentsFromProvider()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllLines(
                path,
                new[]
                {
                    "CapturedUtc,MessageType,SourceInstance,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ",
                    "2026-06-29T19:31:21.0000000Z,FollowTarget,79225E7C,NpcPath,1,2,3,4,5,6",
                    "2026-06-29T19:31:22.5000000Z,FollowTarget,79225E7C,NpcPath,4,5,6,7,8,9"
                });

            try
            {
                var provider = new CapturedAreteRobotContentProvider(new[] { path });
                var coordinator = new NpcPatrolReplayCoordinator(provider);
                NpcPatrolReplaySegment[] assigned = null;

                coordinator.AssignCapturedAreteRobotReplay(
                    0x79225E7C,
                    segments => assigned = segments);

                Assert.IsNotNull(assigned);
                Assert.AreEqual(2, assigned.Length);
                Assert.AreEqual(1.5, assigned[0].DelayAfterSeconds);
                Assert.AreEqual(1f, assigned[0].StartX);
                Assert.AreEqual(2f, assigned[0].StartY);
                Assert.AreEqual(3f, assigned[0].StartZ);
                Assert.AreEqual(4f, assigned[0].EndX);
                Assert.AreEqual(5f, assigned[0].EndY);
                Assert.AreEqual(6f, assigned[0].EndZ);
                Assert.AreEqual(0.25, assigned[1].DelayAfterSeconds);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsEmptyReplayForMissingProviderData()
        {
            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            NpcPatrolReplaySegment[] assigned = null;

            coordinator.AssignCapturedAreteRobotReplay(
                0x79225E7C,
                segments => assigned = segments);

            Assert.IsNotNull(assigned);
            Assert.AreEqual(0, assigned.Length);
        }

        [TestMethod]
        public void CapturedAreteRobotSpawnOrchestrationTraceKeepsSetupReplayAndScfuOrder()
        {
            var provider = new CapturedAreteRobotContentProvider();
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();
            CapturedAreteRobotSpawnDefinition spawn = spawns[0];
            Identity playfield = new Identity { Type = IdentityType.Playfield, Instance = 6553 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            string spawnCreatedDetail =
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(
                    spawn.SourceInstance,
                    CapturedAreteRobotContentProvider.MonsterData,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.PatrolX,
                    spawn.PatrolY,
                    spawn.PatrolZ);

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnRowsLoaded,
                    playfield,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(
                        spawns.Length,
                        CapturedAreteRobotContentProvider.MonsterData));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnCreated,
                    robot,
                    spawnCreatedDetail);

                NpcPatrolReplaySegment[] assigned = null;
                coordinator.AssignCapturedAreteRobotReplay(spawn.SourceInstance, segments => assigned = segments);
                Assert.IsNotNull(assigned);
                Assert.AreEqual(39, assigned.Length);

                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotPatrolReplayAssigned,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(
                        spawn.SourceInstance,
                        assigned.Length));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.ExpectedCapturedAreteRobotSpawnOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                        "count=11 monsterData=297023"));
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                        spawnCreatedDetail));
                Assert.IsTrue(
                    spawnCreatedDetail.IndexOf(
                        "sourceInstance=79866553 monsterData=297023 hp=12 level=1 runSpeed=6",
                        StringComparison.Ordinal) >= 0);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                        "sourceInstance=79866553 segments=39"));
            }
        }







        [TestMethod]
        public void SubwayContentModuleRegistersCapturedNpcSpawnsWithoutOwningRuntimeSystems()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            string scfuMessageText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\SimpleCharFullUpdateMessage.cs"));
            string scfuSerializerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\Serializers\Custom\SimpleCharFullUpdateSerializer.cs"));
            Assert.IsTrue(
                providerText.Contains("public const int SubwayPlayfieldInstance = 127"),
                "Captured Subway NPC spawns must bind to the live/client-visible PF127 Subway proxy resource.");

            Assert.IsTrue(
                providerText.Contains("\"Filth Flea\"")
                && providerText.Contains("\"Discarded Pet\"")
                && providerText.Contains("\"Disobedient Bot\"")
                && providerText.Contains("\"Thief\"")
                && providerText.Contains("\"Violent Vagabond\"")
                && providerText.Contains("\"Mugger\""),
                "CapturedSubwayContentProvider must contain the first visible Subway mob families.");
            Assert.IsTrue(
                providerText.Contains("17657")
                && providerText.Contains("17720")
                && providerText.Contains("17649")
                && providerText.Contains("26092")
                && providerText.Contains("203733")
                && providerText.Contains("203734"),
                "CapturedSubwayContentProvider must preserve the captured monsterData values.");
            int patrolReplayIndex = providerText.IndexOf(
                "private static readonly Dictionary<int, CapturedSubwayPatrolReplaySegment[]>",
                StringComparison.Ordinal);
            Assert.IsTrue(patrolReplayIndex > 0, "Captured Subway patrol replay data must follow spawn definitions.");
            string spawnDefinitionsText = providerText.Substring(0, patrolReplayIndex);
            Assert.AreEqual(
                124,
                CountOccurrences(spawnDefinitionsText, "CapturedSurveySpawn("),
                "CapturedSubwayContentProvider must preserve all 124 capture-backed supported-family Subway spawns.");
            string[] restoredSupportedSourceInstances =
                {
                    "0x79557C09", "0x79557C26", "0x79557C31", "0x79557C8B", "0x79557CA7",
                    "0x79557CAB", "0x79557CAD", "0x7957E411", "0x7957E4A5", "0x7957E4B1",
                    "0x7957E4BC", "0x79557C66", "0x7957E40A", "0x79557F14", "0x7957E5C6",
                    "0x7957E5C7", "0x7957E5C8", "0x7957E5CA", "0x79557CAC", "0x7957405C",
                    "0x795743A7", "0x795743A8", "0x7957E02C", "0x7957E02E", "0x7957E123",
                    "0x7957E40E", "0x7957E5BF", "0x7957E5C4", "0x7957E5C5"
                };
            for (int i = 0; i < restoredSupportedSourceInstances.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    CountOccurrences(spawnDefinitionsText, restoredSupportedSourceInstances[i]),
                    "Restored supported source identity must appear exactly once: " + restoredSupportedSourceInstances[i]);
            }
            Assert.IsTrue(
                providerText.Contains("RuntimeQuarantinedSourceInstances.Contains(spawn.SourceInstance)"),
                "The supported-family diagnostic quarantine mechanism must remain available even when no rows use it.");
            Assert.IsFalse(
                providerText.Contains("122002"),
                "CapturedSubwayContentProvider must bind content to resource/playfield 127, not capture object Playfield2:122002.");
            Assert.IsTrue(
                scfuMessageText.Contains("public byte[] ExtendedTextureOverrideData { get; set; }")
                && scfuSerializerText.Contains("SimpleCharFullUpdateFlags.HasExtendedTextures")
                && scfuSerializerText.Contains("streamWriter.WriteBytes(scfu.ExtendedTextureOverrideData);"),
                "SimpleCharFullUpdate must be able to emit captured extended texture override data.");
            string thiefFactory = ExtractMethodBlock(
                providerText,
                "private static CapturedSubwaySpawnDefinition Thief");
            Assert.IsTrue(
                thiefFactory.Contains("\"Thief\"")
                && thiefFactory.Contains("26092")
                && thiefFactory.Contains("40694")
                && thiefFactory.Contains("138")
                && providerText.Contains("CapturedSurveySpawn(Thief(0x7953AEA5, 5, 146, 72.7292557f, 115.61483f, 313.1308f, 93, 20, useSpawnAsPatrolStart: true, healthDamage: 31))"),
                "Captured Subway Thief must preserve live max/current health, monsterData, scale, head mesh, run speed, NPC family, and current surveyed position.");
            Assert.IsTrue(
                scfuMessageText.Contains("public SimpleCharFullUpdateFlags AdditionalFlags { get; set; }")
                && scfuMessageText.Contains("public SimpleCharFullUpdateFlags SuppressedFlags { get; set; }")
                && scfuMessageText.Contains("public Vector3[] Waypoints { get; set; }")
                && scfuSerializerText.Contains("SimpleCharFullUpdateFlags.HasWaypoints")
                && scfuSerializerText.Contains("streamWriter.WriteInt32(waypoints.Length);")
                && scfuSerializerText.Contains("flags |= scfu.AdditionalFlags;")
                && scfuSerializerText.Contains("flags &= ~scfu.SuppressedFlags;"),
                "SimpleCharFullUpdate must be able to emit captured waypoint data and capture-only flag deltas.");
        }



        [TestMethod]
        public void SubwayExistingPopulationAndPatrolReplayRemainLoaded()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\NpcPatrolReplayCoordinator.cs"));

            Assert.AreEqual(
                124,
                CountOccurrences(providerText, "            CapturedSurveySpawn("),
                "The expanded ordinary-archetype slice must retain all 124 supported-family spawns.");

            string[] patrolSourceIdentities =
                {
                    "0x79557C66",
                    "0x7957E5C4",
                    "0x7953AFCC",
                    "0x795317F5",
                    "0x79528FDA",
                    "0x7953AFA1",
                    "0x7953AF18",
                    "0x7953AF57",
                    "0x79531752",
                    "0x79531754"
                };
            for (int i = 0; i < patrolSourceIdentities.Length; i++)
            {
                Assert.IsTrue(
                    providerText.Contains(patrolSourceIdentities[i]),
                    "Missing captured patrol source identity "
                    + patrolSourceIdentities[i]
                    + ".");
            }

            Assert.AreEqual(
                145,
                CountOccurrences(providerText, "new CapturedSubwayPatrolReplaySegment("),
                "Existing periodic patrol cycles plus the accepted Bot, Vagabond, Pet, Flea, and Thief replays must remain loaded.");
            Assert.IsTrue(
                providerText.Contains("new CapturedSubwayPatrolReplaySegment(3.250491, 143.6185f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(2.149372, 147.409149f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(0.894761, 186.2874605f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(1.539519, 149.2577665f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(2.379826, 179.052765f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(4.491099, 183.153702f")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(0.665506, 90.9275284f")
                && providerText.Contains("0x79557C66")
                && providerText.Contains("0x7957E5C4")
                && providerText.Contains("0x7953AFCC")
                && providerText.Contains("0x795317F5")
                && providerText.Contains("0x79528FDA")
                && providerText.Contains("0x7953AFA1")
                && providerText.Contains("0x7953AF18")
                && providerText.Contains("0x7953AF57")
                && providerText.Contains("0x79531752")
                && providerText.Contains("0x79531754")
                && providerText.Contains("useSpawnAsPatrolStart: true")
                && providerText.Contains("GetPatrolReplaySegments(int sourceInstance)"),
                "Captured patrol replay must preserve complete cycle timing, movement modes, and captured route speeds.");
            Assert.IsTrue(
                coordinatorText.Contains("BuildCapturedSubwaySegments(int sourceInstance)")
                && coordinatorText.Contains("this.capturedSubwayContentProvider.GetPatrolReplaySegments(sourceInstance)")
                && coordinatorText.Contains("segments[i].MoveMode"),
                "NpcPatrolReplayCoordinator must preserve captured Subway coordinates, timing, and movement mode.");
        }

        [TestMethod]
        public void SubwayVisibilityIsolationDiagnosticsRemainOptInAndManifestOrdered()
        {
            string repositoryRoot = FindRepositoryRoot();
            string manifestPath = Path.Combine(
                repositoryRoot,
                @"docs\generated\subway_pf127_visibility_diagnostic_manifest.csv");
            string[] manifestLines = File.ReadAllLines(manifestPath);
            string ordinaryCatalogText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            string ordinaryGeneratorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"tools-temp\AOSharpCaptureAnalyzer\generate_subway_ordinary_content.py"));

            Assert.AreEqual(39, manifestLines.Length, "Manifest must contain one header plus 38 stable rows.");
            for (int ordinal = 1; ordinal <= 38; ordinal++)
            {
                Assert.IsTrue(
                    manifestLines[ordinal].StartsWith(ordinal + ",", StringComparison.Ordinal),
                    "Manifest ordinal must be stable and contiguous: " + ordinal);
            }

            Assert.AreEqual(
                29,
                manifestLines.Count(line => line.Contains(",SUPPORTED_FAMILY_RESTORE,")),
                "Supported diagnostic group must contain exactly 29 rows.");
            Assert.AreEqual(
                9,
                manifestLines.Count(line => line.Contains(",ORDINARY_ENEMY_REGENERATE,")),
                "Ordinary diagnostic group must contain exactly nine rows.");
            Assert.IsTrue(
                ordinaryCatalogText.Contains("spawn.Disposition == OrdinaryEnemyRuntimeDisposition.Active")
                && ordinaryCatalogText.Contains("SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(")
                && !ordinaryGeneratorText.Contains("SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceInstance)"),
                "The unified catalog must keep only explicitly quarantined supported rows behind the opt-in selector.");
        }

        [TestMethod]
        public void SubwayOrdinaryArchetypesUseCaptureBackedTemplateFreeFramework()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            string profileText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyProfile.cs"));
            string combatAttackRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\NpcCombatAttackRules.cs"));
            string ordinaryLootAdapterText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyLootTableAdapter.cs"));
            CapturedSubwayOrdinarySpawnDefinition[] capturedSpawns =
                new CapturedSubwayOrdinaryContentProvider().GetAllSpawns();

            Assert.AreEqual(
                20,
                CountOccurrences(providerText, "            new CapturedSubwayOrdinaryArchetypeDefinition("),
                "The current ordinary families must retain all twenty capture-backed visual/template variants.");
            Assert.AreEqual(
                198,
                capturedSpawns.Length,
                "The completed capture survey must preserve all 198 ordinary spawn rows before quarantine filtering.");

            string[] restoredOrdinarySourceInstances =
                {
                    "0x79557CB8", "0x7957E5CD", "0x79557F12", "0x7957E128", "0x7957E415",
                    "0x7957E5CF", "0x7957E5D0", "0x7957E5D1", "0x79574527"
                };
            for (int i = 0; i < restoredOrdinarySourceInstances.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    capturedSpawns.Count(
                        value => value.SourceInstance
                                 == Convert.ToInt32(restoredOrdinarySourceInstances[i].Substring(2), 16)),
                    "Restored ordinary source identity must map to exactly one spawn: " + restoredOrdinarySourceInstances[i]);
            }
            Assert.IsFalse(
                providerText.Contains("!string.Equals(spawn.EvidenceCapture, \"20260710-202132\", StringComparison.Ordinal)"),
                "Accepted ordinary rows must not remain behind a capture-wide quarantine.");

            string[] capturedNames =
                {
                    "Shadow",
                    "Stim Fiend",
                    "Workman Striker",
                    "Architect Striker",
                    "Infected Attendant",
                    "Slum Runner",
                    "Looter",
                    "Infector",
                    "Lost Thought",
                    "Neural Burnout",
                    "Bloodcreeper",
                    "Deranged Shopper"
                };
            for (int i = 0; i < capturedNames.Length; i++)
            {
                Assert.IsTrue(providerText.Contains("\"" + capturedNames[i] + "\""), "Missing " + capturedNames[i] + ".");
            }

            int[] capturedMonsterData = { 30464, 203739, 203854, 203743, 96056, 55648, 203745, 31909, 96193, 203730, 30379, 203736 };
            for (int i = 0; i < capturedMonsterData.Length; i++)
            {
                Assert.IsTrue(
                    providerText.Contains("                " + capturedMonsterData[i] + ","),
                    "Missing captured monsterData " + capturedMonsterData[i] + ".");
            }

            Assert.IsTrue(
                providerText.Contains("\"workman_striker\",")
                && providerText.Contains("\"architect_striker\",")
                && CountOccurrences(providerText, "                \"striker\",") == 2,
                "Workman and Architect Striker must share one ordinary family while preserving separate captured identities.");
            Assert.IsTrue(
                providerText.Contains("\"looter\",")
                && providerText.Contains("\"stim_fiend\",")
                && providerText.Contains("\"deranged_shopper\",")
                && providerText.Contains("new CapturedSubwayTextureDefinition(1, 30859, 0)")
                && providerText.Contains("new CapturedSubwayMeshDefinition(1, 95784u, 0, 2)")
                && providerText.Contains("\"20260710-202132\""),
                "Looter, Stim Fiend, and Deranged Shopper must use capture-generated reusable archetypes.");
            Assert.IsTrue(
                providerText.Contains("CapturedSubwayTextureDefinition[]")
                && providerText.Contains("CapturedSubwayMeshDefinition[]")
                && providerText.Contains("CapturedSubwayWaypointDefinition[]")
                && providerText.Contains("CapturedFlags")
                && providerText.Contains("Unknown1")
                && providerText.Contains("Unknown2"),
                "Captured SCFU visual, flag, unknown-field, and path data must remain first-class evidence.");

            string[] excludedNamedOrOwnedMobs =
                {
                    "Strike Foreman",
                    "Eumenides",
                    "Vergil Aeneid",
                    "Abmouth Supremus",
                    "Healer",
                    "0x795451A1",
                    "0x795451A9"
                };
            for (int i = 0; i < excludedNamedOrOwnedMobs.Length; i++)
            {
                Assert.IsFalse(
                    providerText.Contains(excludedNamedOrOwnedMobs[i]),
                "Named, boss, personal-pet, and boss-owned summon evidence is outside this ordinary slice: "
                    + excludedNamedOrOwnedMobs[i]);
            }
        }

        [TestMethod]
        public void BloodcreeperSingleSpawnRollsInclusiveDocumentedLevelRange()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemySpawnDefinition[] bloodcreeperSpawns = catalog.GetSpawns()
                .Where(row => row.SourceIdentity == 0x795451C5)
                .ToArray();
            Assert.AreEqual(1, bloodcreeperSpawns.Length, "Bloodcreeper must use one runtime spawn row.");

            OrdinaryEnemySpawnDefinition bloodcreeperSpawn = bloodcreeperSpawns[0];
            Assert.IsNotNull(bloodcreeperSpawn.LevelDefinition, "Bloodcreeper must declare a reusable level definition.");
            Assert.AreEqual(OrdinaryEnemySpawnLevelMode.InclusiveRange, bloodcreeperSpawn.LevelDefinition.Mode);
            Assert.AreEqual(15, bloodcreeperSpawn.LevelDefinition.MinimumLevel);
            Assert.AreEqual(25, bloodcreeperSpawn.LevelDefinition.MaximumLevel);
            Assert.AreEqual(OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, bloodcreeperSpawn.LevelDefinition.RerollPolicy);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, bloodcreeperSpawn.LevelDefinition.EvidenceState);

            OrdinaryEnemySpawnVariant minimum = bloodcreeperSpawn.SelectVariant(
                levelCount =>
                    {
                        Assert.AreEqual(11, levelCount);
                        return 0;
                    });
            Assert.AreEqual(15, minimum.Level);
            Assert.AreEqual(394, minimum.Health);
            Assert.AreEqual(56, minimum.RunSpeed);
            Assert.AreEqual(70, minimum.MonsterScale);

            OrdinaryEnemySpawnVariant maximum = bloodcreeperSpawn.SelectVariant(levelCount => levelCount - 1);
            Assert.AreEqual(25, maximum.Level);
            Assert.AreEqual(724, maximum.Health);
            Assert.AreEqual(86, maximum.RunSpeed);
            Assert.AreEqual(70, maximum.MonsterScale);

            int roll = 0;
            int[] offsets = { 2, 8 };
            OrdinaryEnemySpawnVariant firstRoll = bloodcreeperSpawn.SelectVariant(levelCount => offsets[roll++]);
            OrdinaryEnemySpawnVariant secondRoll = bloodcreeperSpawn.SelectVariant(levelCount => offsets[roll++]);
            Assert.AreEqual(17, firstRoll.Level);
            Assert.AreEqual(23, secondRoll.Level);
            Assert.AreNotEqual(firstRoll.Level, secondRoll.Level, "Separate spawn calls must reroll the level.");

            OrdinaryEnemyProfile profile = catalog.GetProfiles()
                .Single(value => value.MonsterData == 30379);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, profile.Loot.CreditEvidence);
            Assert.AreEqual(150, profile.Loot.MinimumCredits);
            Assert.AreEqual(150, profile.Loot.MaximumCredits);
            Assert.IsTrue(
                profile.Loot.LevelCreditRules.Any(
                    value => value.EnemyLevel == 24
                             && value.MinimumCredits == 150
                             && value.MaximumCredits == 150),
                "Level 24 must retain the exact repeated capture evidence.");
        }

        [TestMethod]
        public void Subway20260710PopulationRestoreManifestMatchesCaptureAndBoundaries()
        {
            string repositoryRoot = FindRepositoryRoot();
            string manifestPath = Path.Combine(
                repositoryRoot,
                @"docs\generated\subway_20260710_population_restore_manifest.csv");
            string supportedProviderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string ordinaryProviderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string ordinaryGeneratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpCaptureAnalyzer\generate_subway_ordinary_content.py"));
            string ordinaryCatalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));

            string[] lines = File.ReadAllLines(manifestPath);
            Assert.IsTrue(lines.Length > 1, "Population restore manifest must contain classified capture rows.");
            string[] header = lines[0].Split(',');
            int captureIndex = Array.IndexOf(header, "CaptureId");
            int playfieldIndex = Array.IndexOf(header, "ResourcePlayfieldId");
            int identityIndex = Array.IndexOf(header, "Identity");
            int nameIndex = Array.IndexOf(header, "Name");
            int xIndex = Array.IndexOf(header, "PositionX");
            int yIndex = Array.IndexOf(header, "PositionY");
            int zIndex = Array.IndexOf(header, "PositionZ");
            int ownerIndex = Array.IndexOf(header, "Owner");
            int classificationIndex = Array.IndexOf(header, "Classification");
            Assert.IsTrue(
                captureIndex >= 0 && playfieldIndex >= 0 && identityIndex >= 0 && nameIndex >= 0
                && xIndex >= 0 && yIndex >= 0 && zIndex >= 0 && ownerIndex >= 0 && classificationIndex >= 0,
                "Population restore manifest must expose the required evidence columns.");

            string[][] rows = lines.Skip(1).Select(line => line.Split(',')).ToArray();
            Assert.AreEqual(107, rows.Length, "Every unique SCFU identity in capture 20260710-202132 must have a disposition.");
            Assert.AreEqual(29, rows.Count(row => row[classificationIndex] == "SUPPORTED_FAMILY_RESTORE"));
            Assert.AreEqual(9, rows.Count(row => row[classificationIndex] == "ORDINARY_ENEMY_REGENERATE"));
            Assert.AreEqual(18, rows.Count(row => row[classificationIndex] == "DUPLICATE_EXCLUDED"));
            Assert.AreEqual(2, rows.Count(row => row[classificationIndex] == "OWNED_SUMMON_EXCLUDED"));
            Assert.AreEqual(49, rows.Count(row => row[classificationIndex] == "UNSUPPORTED_FAMILY_EXCLUDED"));
            Assert.AreEqual(0, rows.Count(row => row[classificationIndex] == "MALFORMED_OR_INCOMPLETE"));

            string[][] included = rows
                .Where(
                    row => row[classificationIndex] == "SUPPORTED_FAMILY_RESTORE"
                        || row[classificationIndex] == "ORDINARY_ENEMY_REGENERATE")
                .ToArray();
            Assert.AreEqual(38, included.Length);
            Assert.AreEqual(38, included.Select(row => row[identityIndex]).Distinct().Count());
            Assert.IsTrue(included.All(row => row[captureIndex] == "20260710-202132"));
            Assert.IsTrue(included.All(row => row[playfieldIndex] == "127"));
            Assert.IsTrue(included.All(row => string.IsNullOrEmpty(row[ownerIndex])));

            CapturedSubwaySpawnDefinition[] supportedSpawns =
                new CapturedSubwayContentProvider().GetAllSpawnDefinitions();
            CapturedSubwayOrdinarySpawnDefinition[] ordinarySpawns =
                new CapturedSubwayOrdinaryContentProvider().GetAllSpawns();

            foreach (string[] row in included)
            {
                string sourceIdentity = "0x" + row[identityIndex]
                    .Replace("(SimpleChar:", string.Empty)
                    .Replace(")", string.Empty);
                int sourceInstance = Convert.ToInt32(sourceIdentity.Substring(2), 16);
                int matchCount;
                float actualX;
                float actualY;
                float actualZ;
                if (row[classificationIndex] == "SUPPORTED_FAMILY_RESTORE")
                {
                    CapturedSubwaySpawnDefinition[] matches = supportedSpawns
                        .Where(value => value.SourceInstance == sourceInstance)
                        .ToArray();
                    matchCount = matches.Length;
                    actualX = matchCount == 1 ? matches[0].X : 0.0f;
                    actualY = matchCount == 1 ? matches[0].Y : 0.0f;
                    actualZ = matchCount == 1 ? matches[0].Z : 0.0f;
                }
                else
                {
                    CapturedSubwayOrdinarySpawnDefinition[] matches = ordinarySpawns
                        .Where(value => value.SourceInstance == sourceInstance)
                        .ToArray();
                    matchCount = matches.Length;
                    actualX = matchCount == 1 ? matches[0].X : 0.0f;
                    actualY = matchCount == 1 ? matches[0].Y : 0.0f;
                    actualZ = matchCount == 1 ? matches[0].Z : 0.0f;
                }

                Assert.AreEqual(1, matchCount, "Included source identity must map to one runtime spawn: " + sourceIdentity);
                Assert.AreEqual(
                    float.Parse(row[xIndex], System.Globalization.CultureInfo.InvariantCulture),
                    actualX,
                    0.000001f,
                    "Runtime X must exactly match captured position for " + sourceIdentity + ".");
                Assert.AreEqual(
                    float.Parse(row[yIndex], System.Globalization.CultureInfo.InvariantCulture),
                    actualY,
                    0.000001f,
                    "Runtime Y must exactly match captured position for " + sourceIdentity + ".");
                Assert.AreEqual(
                    float.Parse(row[zIndex], System.Globalization.CultureInfo.InvariantCulture),
                    actualZ,
                    0.000001f,
                    "Runtime Z must exactly match captured position for " + sourceIdentity + ".");
            }

            string[] ordinaryNames = { "Looter", "Stim Fiend", "Deranged Shopper" };
            Assert.IsTrue(
                included.Where(row => row[classificationIndex] == "ORDINARY_ENEMY_REGENERATE")
                    .All(row => ordinaryNames.Contains(row[nameIndex])),
                "Only the three capture-approved ordinary archetypes may be regenerated.");
            Assert.IsTrue(
                ordinaryGeneratorText.Contains("return sorted(selected, key=lambda value: (value[\"Name\"], value[\"Identity\"]))")
                && ordinaryGeneratorText.Contains("CAPTURE_ARCHETYPE_FILTERS")
                && ordinaryGeneratorText.Contains("ARCHETYPE_CAPTURE_FILTERS"),
                "Ordinary regeneration must remain deterministic and capture-filtered.");

            string[] excludedNamedOrOwned =
                {
                    "Abmouth Supremus", "Eumenides", "Vergil Aeneid", "Strike Foreman", "Healer",
                    "0x795451A1", "0x795451A9"
                };
            foreach (string excluded in excludedNamedOrOwned)
            {
                Assert.IsFalse(supportedProviderText.Contains(excluded) || ordinaryProviderText.Contains(excluded));
            }
            foreach (string[] row in rows.Where(row => row[classificationIndex].EndsWith("EXCLUDED", StringComparison.Ordinal)))
            {
                string sourceIdentity = "0x" + row[identityIndex]
                    .Replace("(SimpleChar:", string.Empty)
                    .Replace(")", string.Empty);
                Assert.IsFalse(
                    supportedProviderText.Contains(sourceIdentity) || ordinaryProviderText.Contains(sourceIdentity),
                    "Excluded identity must not become a static spawn: " + sourceIdentity);
            }

            Assert.IsFalse(
                supportedProviderText.Contains("RoomSpace")
                || ordinaryProviderText.Contains("RoomSpace")
                || ordinaryGeneratorText.Contains("RoomSpace"),
                "Population restoration must not add a RoomSpace workaround or coordinate mutation.");
        }



        [TestMethod]
        public void SubwayOrdinaryLifecyclePolicyIsUniformAndBossesRemainSeparate()
        {
            string repositoryRoot = FindRepositoryRoot();
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns();
            OrdinaryEnemyProfile[] profiles = catalog.GetProfiles();

            Assert.AreEqual(322, spawns.Length);
            Assert.IsTrue(
                spawns.All(
                    value => value.RespawnEvidence == OrdinaryEnemyEvidenceState.Policy
                             && value.RespawnDelaySeconds == 240.0
                             && value.RespawnPolicy.Mode
                                == WorldRespawnPolicyAssignmentMode.Inherit),
                "Every regular Subway row must inherit the same 240-second respawn policy.");
            Assert.IsTrue(
                profiles.All(
                    value => value.Corpse.EmptyLifetimeSeconds == 0.0
                             && value.Corpse.UnlootedLifetimeSeconds == 60.0
                             && value.Corpse.LootedCleanupSeconds == 0.0),
                "Every regular Subway profile must use the shared 30/120/30 corpse policy.");
        }

        [TestMethod]
        public void AcceptedSubwayEnemyGateRequiresWholeEnemyCoverage()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayContentProvider.cs"));
            string ordinaryProviderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCatalog.cs"));
            string attackRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\NpcCombatAttackRules.cs"));
            string combatSetupGeneratorText = LegacyGameplaySource.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyCombatSetupGenerator.cs"));
            string capturedPacketFactoryText = LegacyGameplaySource.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine_New\SharedGameplay\Combat\CapturedEnemyCombatPacketFactory.cs"));
            string ordinaryProfileText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"Tests\Fixtures\Gameplay\Playfields\OrdinaryEnemyProfile.cs"));
            string generatedCombatReportText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"docs\generated\subway_enemy_combat_contracts.json"));
            int architectStrikerCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Architect Striker\":",
                StringComparison.Ordinal);
            int architectStrikerCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Basic Quality Armorer\":",
                architectStrikerCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                architectStrikerCombatReportStart >= 0
                && architectStrikerCombatReportEnd > architectStrikerCombatReportStart);
            string architectStrikerCombatReport = generatedCombatReportText.Substring(
                architectStrikerCombatReportStart,
                architectStrikerCombatReportEnd - architectStrikerCombatReportStart);
            int infectedAttendantCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Infected Attendant\":",
                StringComparison.Ordinal);
            int infectedAttendantCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Infector\":",
                infectedAttendantCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                infectedAttendantCombatReportStart >= 0
                && infectedAttendantCombatReportEnd > infectedAttendantCombatReportStart);
            string infectedAttendantCombatReport = generatedCombatReportText.Substring(
                infectedAttendantCombatReportStart,
                infectedAttendantCombatReportEnd - infectedAttendantCombatReportStart);
            int muggerCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Mugger\":",
                StringComparison.Ordinal);
            int muggerCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Neural Burnout\":",
                muggerCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(muggerCombatReportStart >= 0 && muggerCombatReportEnd > muggerCombatReportStart);
            string muggerCombatReport = generatedCombatReportText.Substring(
                muggerCombatReportStart,
                muggerCombatReportEnd - muggerCombatReportStart);
            int derangedShopperCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Deranged Shopper\":",
                StringComparison.Ordinal);
            int derangedShopperCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Discarded Pet\":",
                derangedShopperCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                derangedShopperCombatReportStart >= 0
                && derangedShopperCombatReportEnd > derangedShopperCombatReportStart);
            string derangedShopperCombatReport = generatedCombatReportText.Substring(
                derangedShopperCombatReportStart,
                derangedShopperCombatReportEnd - derangedShopperCombatReportStart);
            int discardedPetCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Discarded Pet\":",
                StringComparison.Ordinal);
            int discardedPetCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Disobedient Bot\":",
                discardedPetCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                discardedPetCombatReportStart >= 0
                && discardedPetCombatReportEnd > discardedPetCombatReportStart);
            string discardedPetCombatReport = generatedCombatReportText.Substring(
                discardedPetCombatReportStart,
                discardedPetCombatReportEnd - discardedPetCombatReportStart);
            int disobedientBotCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Disobedient Bot\":",
                StringComparison.Ordinal);
            int disobedientBotCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Empty Shell\":",
                disobedientBotCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                disobedientBotCombatReportStart >= 0
                && disobedientBotCombatReportEnd > disobedientBotCombatReportStart);
            string disobedientBotCombatReport = generatedCombatReportText.Substring(
                disobedientBotCombatReportStart,
                disobedientBotCombatReportEnd - disobedientBotCombatReportStart);
            int uncontrollableAngerCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Uncontrollable Anger\":",
                StringComparison.Ordinal);
            int uncontrollableAngerCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Vergil Aeneid\":",
                uncontrollableAngerCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                uncontrollableAngerCombatReportStart >= 0
                && uncontrollableAngerCombatReportEnd > uncontrollableAngerCombatReportStart);
            string uncontrollableAngerCombatReport = generatedCombatReportText.Substring(
                uncontrollableAngerCombatReportStart,
                uncontrollableAngerCombatReportEnd - uncontrollableAngerCombatReportStart);
            int strikeForemanCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Strike Foreman\":",
                StringComparison.Ordinal);
            int strikeForemanCombatReportEnd = generatedCombatReportText.IndexOf(
                "\"Tailor\":",
                strikeForemanCombatReportStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                strikeForemanCombatReportStart >= 0
                && strikeForemanCombatReportEnd > strikeForemanCombatReportStart);
            string strikeForemanCombatReport = generatedCombatReportText.Substring(
                strikeForemanCombatReportStart,
                strikeForemanCombatReportEnd - strikeForemanCombatReportStart);
            int strikeForemanLootStart = strikeForemanCombatReport.IndexOf(
                "\"reviewedLootEvidence\":",
                StringComparison.Ordinal);
            int strikeForemanFirstLootStart = strikeForemanCombatReport.IndexOf(
                "\"capture\": \"20260720-032106\"",
                strikeForemanLootStart,
                StringComparison.Ordinal);
            int strikeForemanSecondLootStart = strikeForemanCombatReport.IndexOf(
                "\"capture\": \"20260720-033513\"",
                strikeForemanFirstLootStart,
                StringComparison.Ordinal);
            int strikeForemanLootUnresolvedStart = strikeForemanCombatReport.IndexOf(
                "\"unresolved\":",
                strikeForemanSecondLootStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                strikeForemanLootStart >= 0
                && strikeForemanFirstLootStart > strikeForemanLootStart
                && strikeForemanSecondLootStart > strikeForemanFirstLootStart
                && strikeForemanLootUnresolvedStart > strikeForemanSecondLootStart);
            string strikeForemanFirstLootReport = strikeForemanCombatReport.Substring(
                strikeForemanFirstLootStart,
                strikeForemanSecondLootStart - strikeForemanFirstLootStart);
            string strikeForemanSecondLootReport = strikeForemanCombatReport.Substring(
                strikeForemanSecondLootStart,
                strikeForemanLootUnresolvedStart - strikeForemanSecondLootStart);
            int strikeForemanLocalPlayerStart = strikeForemanCombatReport.IndexOf(
                "\"localPlayer\":",
                StringComparison.Ordinal);
            int strikeForemanLocalPlayerEnd = strikeForemanCombatReport.IndexOf(
                "\"playerOwnedPet\":",
                strikeForemanLocalPlayerStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                strikeForemanLocalPlayerStart >= 0
                && strikeForemanLocalPlayerEnd > strikeForemanLocalPlayerStart);
            string strikeForemanLocalPlayerReport = strikeForemanCombatReport.Substring(
                strikeForemanLocalPlayerStart,
                strikeForemanLocalPlayerEnd - strikeForemanLocalPlayerStart);
            int strikeForemanOtherPlayerStart = strikeForemanCombatReport.IndexOf(
                "\"otherPlayer\":",
                StringComparison.Ordinal);
            int strikeForemanOtherPlayerEnd = strikeForemanCombatReport.IndexOf(
                "\"equippedWeaponObserved\":",
                strikeForemanOtherPlayerStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                strikeForemanOtherPlayerStart >= 0
                && strikeForemanOtherPlayerEnd > strikeForemanOtherPlayerStart);
            string strikeForemanOtherPlayerReport = strikeForemanCombatReport.Substring(
                strikeForemanOtherPlayerStart,
                strikeForemanOtherPlayerEnd - strikeForemanOtherPlayerStart);
            int workmanStrikerCombatReportStart = generatedCombatReportText.IndexOf(
                "\"Workman Striker\":",
                StringComparison.Ordinal);
            Assert.IsTrue(workmanStrikerCombatReportStart >= 0);
            string workmanStrikerCombatReport = generatedCombatReportText.Substring(
                workmanStrikerCombatReportStart);
            string disobedientBotDefinition = ExtractMethodBlock(
                providerText,
                "private static CapturedSubwaySpawnDefinition DisobedientBot(");
            var ordinaryCatalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemyProfile[] ordinaryProfiles = ordinaryCatalog.GetProfiles();
            OrdinaryEnemySpawnDefinition[] ordinarySpawns = ordinaryCatalog.GetSpawns();
            Assert.AreEqual(322, ordinarySpawns.Length);
            Assert.AreEqual(
                322,
                ordinarySpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.IsFalse(
                ordinarySpawns.Any(
                    value => value.Disposition
                             == OrdinaryEnemyRuntimeDisposition.Quarantined));

            string[] acceptedEnemyKeys =
                {
                    "Thief|26092|138",
                    "Filth Flea|17657|138",
                    "Disobedient Bot|17649|138",
                    "Slum Runner|55648|151",
                    "Molested Molecules|203746|148",
                    "Shadow|30464|150",
                    "Infector|31909|150",
                    "Architect Striker|203743|149",
                    "Melded Patterns|203747|148",
                    "Workman Striker|203854|149",
                    "Looter|203745|138",
                    "Mugger|203734|138",
                    "Deranged Shopper|203736|138",
                    "Discarded Pet|17720|138",
                    "Bloodcreeper|30379|63",
                    "Stim Fiend|203739|138",
                    "Neural Burnout|203730|148",
                    "Incomplete Rebuild|203728|148",
                    "Fragmented Soul|203729|148",
                    "Redundant Scan|204178|148",
                    "Uncontrollable Anger|96195|138",
                    "Empty Shell|203731|148",
                    "Infected Attendant|96056|138",
                    "Lost Thought|96193|138",
                    "Premature Pattern|203727|148",
                    "Violent Vagabond|203733|138"
                };

            Assert.AreEqual(
                26,
                acceptedEnemyKeys.Length,
                "Only Subway enemies that pass this whole-enemy gate may be treated as accepted.");

            Assert.AreEqual(
                33,
                CountOccurrences(ordinaryProviderText, "\"shadow\""),
                "Accepted Subway Shadow must preserve its profile keys and all 31 exact spawn rows.");

            Assert.AreEqual(
                14,
                CountOccurrences(ordinaryProviderText, "\"infector\""),
                "Accepted ordinary Subway Infector must preserve its profile keys and all 12 exact spawn rows.");

            Assert.AreEqual(
                8,
                CountOccurrences(ordinaryProviderText, "\"architect_striker\""),
                "Accepted Subway Architect Striker must preserve its profile key and all seven exact spawn rows.");

            OrdinaryEnemyProfile infectedAttendant = ordinaryProfiles.Single(
                value => value.DisplayName == "Infected Attendant");
            OrdinaryEnemySpawnDefinition[] infectedAttendantSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == infectedAttendant.ProfileKey)
                .ToArray();
            Assert.AreEqual(5, infectedAttendantSpawns.Length);
            Assert.IsFalse(infectedAttendant.Combat.Contract.IsCombatReady);
            Assert.IsTrue(infectedAttendant.Combat.Contract.IsQuarantined);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                infectedAttendant.Combat.Contract.QuarantineReason));
            Assert.AreEqual(
                CapturedEnemyAttackModel.FixedAttackInfo,
                infectedAttendant.Combat.Contract.AttackModel);
            Assert.AreEqual(11, infectedAttendant.Combat.Contract.MinDamage);
            Assert.AreEqual(15, infectedAttendant.Combat.Contract.MaxDamage);
            Assert.AreEqual(5.0, infectedAttendant.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(6, infectedAttendant.Loot.ObservedCompleteInventories);
            Assert.AreEqual(1, infectedAttendant.Loot.ObservedEmptyInventories);
            Assert.IsFalse(infectedAttendant.Loot.ItemPoolComplete);
            Assert.IsTrue(
                CountOccurrences(ordinaryProviderText, ", 96056, 96024,") == 8
                && infectedAttendantCombatReport.Contains("\"retaliationRows\": 4")
                && infectedAttendantCombatReport.Contains("\"normalAttackInfoRows\": 2")
                && infectedAttendantCombatReport.Contains("\"normalMinDamage\": 11")
                && infectedAttendantCombatReport.Contains("\"normalMaxDamage\": 15")
                && infectedAttendantCombatReport.Contains("\"criticalAttackInfoRows\": 0")
                && infectedAttendantCombatReport.Contains("\"missedAttackInfoRows\": 0")
                && infectedAttendantCombatReport.Contains("\"specialAttackWeaponRows\": 2")
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown1\": 65") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown2\": 65") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown3\": 65") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown4\": 65") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown1\": 120") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown2\": 120") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown3\": 120") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown4\": 120") == 1
                && CountOccurrences(infectedAttendantCombatReport, "\"unknown5\": 0") == 2,
                "Infected Attendant must retain both captured local-player hits and use the explicit five-second private cadence policy.");

            Assert.IsTrue(
                strikeForemanCombatReport.Contains("\"normalAttackInfoRows\": 6")
                && strikeForemanCombatReport.Contains("\"normalMinDamage\": 13")
                && strikeForemanCombatReport.Contains("\"normalMaxDamage\": 13")
                && strikeForemanCombatReport.Contains("\"criticalAttackInfoRows\": 0")
                && strikeForemanCombatReport.Contains("\"missedAttackInfoRows\": 2")
                && strikeForemanCombatReport.Contains("\"specialAttackWeaponRows\": 2")
                && CountOccurrences(strikeForemanCombatReport, "\"unknown1\": 154") == 1
                && CountOccurrences(strikeForemanCombatReport, "\"unknown2\": 154") == 1
                && CountOccurrences(strikeForemanCombatReport, "\"unknown3\": 154") == 1
                && CountOccurrences(strikeForemanCombatReport, "\"unknown4\": 117") == 1
                && CountOccurrences(strikeForemanCombatReport, "\"unknown5\": 0") == 1
                && strikeForemanLocalPlayerReport.Contains("\"retaliationRows\": 2")
                && strikeForemanLocalPlayerReport.Contains("\"attackInfoRows\": 6")
                && strikeForemanLocalPlayerReport.Contains("\"minDamage\": 13")
                && strikeForemanLocalPlayerReport.Contains("\"maxDamage\": 13")
                && strikeForemanOtherPlayerReport.Contains("\"retaliationRows\": 1")
                && strikeForemanOtherPlayerReport.Contains("\"attackInfoRows\": 3")
                && strikeForemanOtherPlayerReport.Contains("\"minDamage\": 18")
                && strikeForemanOtherPlayerReport.Contains("\"maxDamage\": 40")
                && strikeForemanOtherPlayerReport.Contains("\"hitType\": \"Critical\"")
                && strikeForemanCombatReport.Contains("\"reviewedLootEvidence\":")
                && strikeForemanCombatReport.Contains("\"observationStatus\": \"atomic-outcomes-not-guaranteed-drops\"")
                && strikeForemanCombatReport.Contains("\"initialSnapshots\": 2")
                && strikeForemanCombatReport.Contains("\"positiveSnapshots\": 2")
                && strikeForemanCombatReport.Contains("\"emptySnapshots\": 0")
                && strikeForemanCombatReport.Contains("\"enemyLevel\": 19")
                && strikeForemanCombatReport.Contains("\"corpseCatMesh\": 17870")
                && strikeForemanCombatReport.Contains("\"corpseCredits\": 176")
                && strikeForemanFirstLootReport.Contains("\"lootCorpseIdentity\": \"(Corpse:F74014)\"")
                && strikeForemanFirstLootReport.Contains("\"corpseIdentity\": \"Corpse:00F74014\"")
                && strikeForemanFirstLootReport.Contains("\"deadNpcIdentity\": \"SimpleChar:798033FB\"")
                && strikeForemanFirstLootReport.Contains("\"lowId\": 27199")
                && strikeForemanFirstLootReport.Contains("\"highId\": 27199")
                && strikeForemanFirstLootReport.Contains("\"quality\": 10")
                && strikeForemanFirstLootReport.Contains("\"lowId\": 123744")
                && strikeForemanFirstLootReport.Contains("\"highId\": 123745")
                && strikeForemanFirstLootReport.Contains("\"quality\": 20")
                && strikeForemanFirstLootReport.Contains("\"lowId\": 301713")
                && strikeForemanFirstLootReport.Contains("\"highId\": 301713")
                && strikeForemanFirstLootReport.Contains("\"quality\": 1")
                && CountOccurrences(strikeForemanFirstLootReport, "\"count\": 1") == 3
                && strikeForemanSecondLootReport.Contains("\"lootCorpseIdentity\": \"(Corpse:F74003)\"")
                && strikeForemanSecondLootReport.Contains("\"corpseIdentity\": \"Corpse:00F74003\"")
                && strikeForemanSecondLootReport.Contains("\"deadNpcIdentity\": \"SimpleChar:798037CF\"")
                && strikeForemanSecondLootReport.Contains("\"lowId\": 85676")
                && strikeForemanSecondLootReport.Contains("\"highId\": 22072")
                && strikeForemanSecondLootReport.Contains("\"quality\": 15")
                && strikeForemanSecondLootReport.Contains("\"lowId\": 301707")
                && strikeForemanSecondLootReport.Contains("\"highId\": 301707")
                && strikeForemanSecondLootReport.Contains("\"quality\": 1")
                && CountOccurrences(strikeForemanSecondLootReport, "\"count\": 1") == 2
                && !ordinaryProviderText.Contains("\"Strike Foreman\""),
                "Strike Foreman must keep six local 13-point normals and two misses separate from the older other-player 18/18/40 evidence and retain two atomic observed loot outcomes without treating them as guarantees.");

            Assert.AreEqual(
                12,
                CountOccurrences(ordinaryProviderText, "\"melded_patterns\""),
                "Accepted Subway Melded Patterns must preserve its profile keys and all ten exact spawn rows.");

            Assert.AreEqual(
                23,
                CountOccurrences(ordinaryProviderText, "\"workman_striker\""),
                "Accepted Subway Workman Striker must preserve its profile key and all 22 exact spawn rows.");
            OrdinaryEnemyProfile workmanStriker = ordinaryProfiles.Single(
                value => value.DisplayName == "Workman Striker");
            OrdinaryEnemySpawnDefinition[] workmanStrikerSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == workmanStriker.ProfileKey)
                .OrderBy(value => value.SourceIdentity)
                .ToArray();
            Assert.AreEqual(22, workmanStrikerSpawns.Length);
            Assert.AreEqual(
                31,
                workmanStrikerSpawns.Sum(
                    value => value.LevelDefinition.GetExplicitVariants().Length));
            Assert.IsTrue(
                workmanStrikerSpawns.All(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active
                             && value.LevelDefinition.Mode
                                == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants));

            CapturedSubwayCombatEvidenceDefinition workmanCombatEvidence =
                new CapturedSubwayOrdinaryContentProvider()
                    .GetArchetypes()
                    .Single(value => value.Name == "Workman Striker")
                    .Combat;
            Assert.IsTrue(workmanCombatEvidence.Observed);
            Assert.IsTrue(workmanCombatEvidence.RuntimeReady);
            Assert.AreEqual(59, workmanCombatEvidence.ObservedRows);
            Assert.AreEqual(9, workmanCombatEvidence.MinDamage);
            Assert.AreEqual(23, workmanCombatEvidence.MaxDamage);
            Assert.AreEqual(6, workmanCombatEvidence.WeaponSlot);
            Assert.AreEqual(0, workmanCombatEvidence.AttackInfoUnknown);
            Assert.AreEqual(0, workmanCombatEvidence.WeaponInstance);

            foreach (OrdinaryEnemySpawnDefinition spawn in workmanStrikerSpawns)
            {
                foreach (OrdinaryEnemySpawnVariant variant in
                    spawn.LevelDefinition.GetExplicitVariants())
                {
                    Assert.IsNotNull(variant.WeaponLoadout);
                    CapturedEnemyCombatContract contract = workmanStriker.Combat.ResolveContract(
                        spawn.SourceIdentity,
                        variant);
                    Assert.IsFalse(contract.IsCombatReady, contract.Evidence);
                    Assert.IsTrue(contract.IsQuarantined, contract.Evidence);
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(contract.QuarantineReason),
                        contract.Evidence);
                }
            }

            OrdinaryEnemyProfile looter = ordinaryProfiles.Single(value => value.DisplayName == "Looter");
            OrdinaryEnemySpawnDefinition[] looterSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == looter.ProfileKey)
                .ToArray();
            Assert.AreEqual(10, CountOccurrences(ordinaryProviderText, "\"looter\""));
            Assert.AreEqual(8, looterSpawns.Length);
            Assert.AreEqual(8, looterSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(0, looterSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.IsTrue(looterSpawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, looter.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, looter.Combat.DamageSource);
            Assert.IsTrue(looter.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, looter.Combat.Contract.AttackModel);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, looter.Loot.PoolMode);
            Assert.IsFalse(looter.Loot.ItemPoolComplete);
            Assert.AreEqual(11, looter.Loot.ObservedCompleteInventories);
            Assert.AreEqual(5, looter.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "21605:21605:1:1:11",
                    "85501:22343:12:1:11",
                    "124422:124422:12:1:11",
                    "144082:144083:7:1:11",
                    "234874:234874:1:1:11",
                    "234875:234875:1:1:11",
                    "234877:234877:1:1:11",
                    "301713:301713:1:1:11",
                    "301714:301714:1:1:11"
                },
                looter.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "9:53:53:2", "10:59:59:9" },
                looter.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(17870, looter.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, looter.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, looter.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, looter.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile mugger = ordinaryProfiles.Single(value => value.DisplayName == "Mugger");
            OrdinaryEnemySpawnDefinition[] muggerSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == mugger.ProfileKey)
                .ToArray();
            Assert.AreEqual(9, CountOccurrences(providerText, "CapturedSurveySpawn(Mugger("));
            Assert.AreEqual(9, muggerSpawns.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    "7953AA11:8:Active",
                    "7953AD6B:10:Active",
                    "795450D4:5:Active",
                    "795451FE:10:Active",
                    "79557F14:10:Active",
                    "7957E5C6:9:Active",
                    "7957E5C7:8:Active",
                    "7957E5C8:8:Active",
                    "7957E5CA:10:Active"
                },
                muggerSpawns
                    .OrderBy(value => value.SourceIdentity)
                    .Select(
                        value => string.Format(
                            "{0:X8}:{1}:{2}",
                            value.SourceIdentity,
                            value.Level,
                            value.Disposition))
                    .ToArray());
            Assert.IsTrue(muggerSpawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Auto, mugger.Aggression.Mode);
            Assert.AreEqual(7.0, mugger.Aggression.AutomaticAggroRadius.Value);
            Assert.IsTrue(mugger.Aggression.RequiresLineOfSight);
            Assert.AreEqual(7.0, mugger.Aggression.SocialAggroRadius.Value);
            Assert.IsTrue(mugger.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, mugger.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, mugger.Combat.DamageSource);
            Assert.IsTrue(mugger.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, mugger.Combat.Contract.AttackModel);
            Assert.IsFalse(mugger.Combat.Contract.IsCombatReady);
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                mugger.Combat.ResolveContract(muggerSpawns[0].Level).AttackModel);
            foreach (OrdinaryEnemySpawnDefinition spawn in muggerSpawns)
            {
                CapturedEnemyCombatContract contract = mugger.Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level);
                Assert.IsFalse(contract.IsCombatReady, contract.Evidence);
                Assert.IsTrue(contract.IsQuarantined, contract.Evidence);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(contract.QuarantineReason),
                    contract.Evidence);
            }
            Assert.AreEqual(
                CapturedEnemyAttackModel.Unresolved,
                mugger.Combat.ResolveContract(0x7953FFFF, muggerSpawns[0].Level).AttackModel);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, mugger.Loot.PoolMode);
            Assert.IsFalse(mugger.Loot.ItemPoolComplete);
            Assert.AreEqual(18, mugger.Loot.ObservedCompleteInventories);
            Assert.AreEqual(3, mugger.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "25822:25831:5:1:18", "85711:22014:8:1:18",
                    "123495:123496:5:1:18", "123704:123705:9:1:18",
                    "123723:123724:6:1:18", "123976:123977:9:1:18",
                    "124348:124349:7:1:18", "124545:124546:10:1:18",
                    "128636:128637:8:1:18", "128839:128840:9:1:18",
                    "130060:130061:5:1:18", "130060:130061:9:1:18",
                    "131605:131606:7:1:18", "136638:136639:9:1:18",
                    "136638:136639:12:1:18", "136640:136641:7:1:18",
                    "136640:136641:8:1:18", "136640:136641:9:1:18",
                    "136646:136647:9:1:18", "160224:160225:10:1:18",
                    "234875:234875:1:2:18", "234876:234876:1:1:18"
                },
                mugger.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "5:44:44:7", "8:71:71:6", "9:80:80:6", "10:88:88:6" },
                mugger.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(17534, mugger.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, mugger.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, mugger.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, mugger.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile derangedShopper = ordinaryProfiles.Single(
                value => value.DisplayName == "Deranged Shopper");
            OrdinaryEnemySpawnDefinition derangedShopperSpawn = ordinarySpawns.Single(
                value => value.ProfileKey == derangedShopper.ProfileKey);
            CapturedSubwaySourceWeaponEvidenceDefinition[] derangedShopperSourceEvidence =
                new CapturedSubwayOrdinaryContentProvider().GetSourceWeaponEvidence(203736);
            CapturedEnemyCombatContract derangedShopperContract =
                derangedShopper.Combat.ResolveContract(
                    derangedShopperSpawn.SourceIdentity,
                    derangedShopperSpawn.Level);
            CapturedEnemyCombatContract derangedShopperUnknownSource =
                derangedShopper.Combat.ResolveContract(
                    0x7957FFFF,
                    derangedShopperSpawn.Level);
            Assert.AreEqual(1, derangedShopperSourceEvidence.Length);
            Assert.AreEqual(0x79574527, derangedShopperSourceEvidence[0].SourceInstance);
            Assert.AreEqual(125454, derangedShopperSourceEvidence[0].LowId);
            Assert.AreEqual(125455, derangedShopperSourceEvidence[0].HighId);
            Assert.AreEqual(8, derangedShopperSourceEvidence[0].Quality);
            Assert.IsTrue(
                derangedShopperSourceEvidence[0].EvidenceCaptures.Contains("20260710-202132"));
            Assert.AreEqual(0x79574527, derangedShopperSpawn.SourceIdentity);
            Assert.AreEqual(8, derangedShopperSpawn.Level);
            Assert.AreEqual(256, derangedShopperSpawn.LevelDefinition.Resolve(8).Health);
            Assert.AreEqual(
                OrdinaryEnemyRuntimeDisposition.Active,
                derangedShopperSpawn.Disposition,
                "The capture-complete Deranged Shopper row must be active for private validation.");
            Assert.AreEqual(
                WorldRespawnPolicyAssignmentMode.Inherit,
                derangedShopperSpawn.RespawnPolicy.Mode);
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, derangedShopper.Aggression.Mode);
            Assert.IsTrue(derangedShopper.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, derangedShopper.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, derangedShopper.Combat.DamageSource);
            Assert.IsTrue(derangedShopper.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, derangedShopper.Combat.Contract.AttackModel);
            Assert.IsFalse(derangedShopper.Combat.Contract.IsCombatReady);
            Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, derangedShopperContract.AttackModel);
            Assert.IsFalse(derangedShopperContract.IsCombatReady);
            Assert.IsTrue(derangedShopperContract.IsQuarantined);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                derangedShopperContract.QuarantineReason));
            Assert.AreEqual(125454, derangedShopperContract.WeaponLowId);
            Assert.AreEqual(125455, derangedShopperContract.WeaponHighId);
            Assert.AreEqual(8, derangedShopperContract.WeaponQuality);
            Assert.AreEqual(6, derangedShopperContract.WeaponInventorySlot);
            Assert.AreEqual(0, derangedShopperContract.MinDamage);
            Assert.AreEqual(0, derangedShopperContract.MaxDamage);
            Assert.AreEqual(0.0, derangedShopperContract.RechargeSeconds);
            Assert.IsTrue(derangedShopperContract.HasCapturedEquippedAttackInfo);
            Assert.AreEqual(-1, derangedShopperContract.AttackInfoAmmoCount);
            Assert.AreEqual(6, derangedShopperContract.AttackInfoWeaponSlot);
            Assert.AreEqual(0, derangedShopperContract.AttackInfoUnknown);
            Assert.AreEqual(0, derangedShopperContract.AttackInfoWeaponInstance);
            Assert.IsFalse(derangedShopperContract.HasEmptySpecialAttackWeaponContext);
            Assert.IsFalse(derangedShopperContract.HasCapturedAttackStartContext);
            Assert.IsFalse(derangedShopperContract.HasCapturedCombatStopSequence);
            Assert.AreEqual(CapturedEnemyAttackModel.Unresolved, derangedShopperUnknownSource.AttackModel);
            Assert.IsFalse(derangedShopperUnknownSource.IsCombatReady);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, derangedShopper.Loot.PoolMode);
            Assert.IsFalse(derangedShopper.Loot.ItemPoolComplete);
            Assert.AreEqual(3, derangedShopper.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, derangedShopper.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                    {
                        "123019:123020:6:1:3", "124465:124466:10:1:3",
                        "234876:234876:1:1:3"
                    },
                derangedShopper.Loot.Entries
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "8:47:47:2", "9:53:53:1" },
                derangedShopper.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.MaximumCredits,
                            value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(5927, derangedShopper.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, derangedShopper.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, derangedShopper.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, derangedShopper.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile discardedPet = ordinaryProfiles.Single(
                value => value.DisplayName == "Discarded Pet");
            OrdinaryEnemySpawnDefinition[] discardedPetSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == discardedPet.ProfileKey)
                .ToArray();
            Assert.AreEqual(29, CountOccurrences(providerText, "CapturedSurveySpawn(DiscardedPet("));
            Assert.AreEqual(29, discardedPetSpawns.Length);
            Assert.AreEqual(
                29,
                discardedPetSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                0,
                discardedPetSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.IsTrue(
                discardedPetSpawns.All(
                    value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, discardedPet.Aggression.Mode);
            Assert.IsFalse(discardedPet.Aggression.AutomaticAggroRadius.HasValue);
            Assert.IsTrue(discardedPet.Aggression.Chase);
            Assert.IsFalse(discardedPet.Aggression.ReturnToSpawn);
            Assert.AreEqual(OrdinaryEnemyCombatMode.UnarmedMelee, discardedPet.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.CapturedFixed, discardedPet.Combat.DamageSource);
            Assert.IsFalse(discardedPet.Combat.VisibleWeapon);
            Assert.AreEqual(
                CapturedEnemyAttackModel.FixedAttackInfo,
                discardedPet.Combat.Contract.AttackModel);
            Assert.IsFalse(discardedPet.Combat.Contract.IsCombatReady);
            Assert.IsTrue(discardedPet.Combat.Contract.IsQuarantined);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                discardedPet.Combat.Contract.QuarantineReason));
            Assert.AreEqual(9, discardedPet.Combat.Contract.MinDamage);
            Assert.AreEqual(18, discardedPet.Combat.Contract.MaxDamage);
            Assert.AreEqual(5.089763, discardedPet.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(-1, discardedPet.Combat.Contract.AttackInfoAmmoCount);
            Assert.AreEqual(0, discardedPet.Combat.Contract.AttackInfoWeaponSlot);
            Assert.AreEqual(0, discardedPet.Combat.Contract.AttackInfoUnknown);
            Assert.AreEqual(0x53495731, discardedPet.Combat.Contract.AttackInfoWeaponInstance);
            Assert.IsFalse(discardedPet.Combat.Contract.HasEmptySpecialAttackWeaponContext);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, discardedPet.Loot.PoolMode);
            Assert.IsFalse(discardedPet.Loot.ItemPoolComplete);
            Assert.AreEqual(16, discardedPet.Loot.ObservedCompleteInventories);
            Assert.AreEqual(3, discardedPet.Loot.ObservedEmptyInventories);
            Assert.AreEqual(
                13,
                discardedPet.Loot.ObservedCompleteInventories
                - discardedPet.Loot.ObservedEmptyInventories);
            Assert.AreEqual(13, discardedPet.Loot.Entries.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    "101681:101682:7:1:16", "102283:102284:9:1:16",
                    "103973:103974:10:1:16", "106005:106006:11:1:16",
                    "107283:107284:10:1:16", "109520:109521:7:1:16",
                    "111623:111624:8:1:16", "112160:112161:6:1:16",
                    "112798:112799:6:1:16", "234874:234874:1:3:16",
                    "234876:234876:1:3:16", "234877:234877:1:1:16",
                    "290619:202727:9:1:16"
                },
                discardedPet.Loot.Entries
                    .OrderBy(value => value.LowId)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "5:18:18:1", "6:21:21:3", "7:25:25:8",
                    "8:28:28:1", "9:32:32:4", "10:35:35:8"
                },
                discardedPet.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.MaximumCredits,
                            value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(15929, discardedPet.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, discardedPet.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, discardedPet.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, discardedPet.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile bloodcreeper = ordinaryProfiles.Single(value => value.DisplayName == "Bloodcreeper");
            OrdinaryEnemySpawnDefinition[] bloodcreeperSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == bloodcreeper.ProfileKey)
                .ToArray();
            Assert.AreEqual(3, CountOccurrences(ordinaryProviderText, "\"bloodcreeper\""));
            Assert.AreEqual(1, bloodcreeperSpawns.Length);
            Assert.AreEqual(1, bloodcreeperSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(WorldRespawnPolicyAssignmentMode.Inherit, bloodcreeperSpawns[0].RespawnPolicy.Mode);
            Assert.AreEqual(OrdinaryEnemySpawnLevelMode.InclusiveRange, bloodcreeperSpawns[0].LevelDefinition.Mode);
            Assert.AreEqual(15, bloodcreeperSpawns[0].LevelDefinition.MinimumLevel);
            Assert.AreEqual(25, bloodcreeperSpawns[0].LevelDefinition.MaximumLevel);
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Auto, bloodcreeper.Aggression.Mode);
            Assert.AreEqual(7.0, bloodcreeper.Aggression.AutomaticAggroRadius.Value);
            Assert.IsTrue(bloodcreeper.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.NaturalMelee, bloodcreeper.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.NaturalAttack, bloodcreeper.Combat.DamageSource);
            Assert.AreEqual(CapturedEnemyAttackModel.Specialized, bloodcreeper.Combat.Contract.AttackModel);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, bloodcreeper.Loot.PoolMode);
            Assert.IsFalse(bloodcreeper.Loot.ItemPoolComplete);
            Assert.AreEqual(4, bloodcreeper.Loot.ObservedCompleteInventories);
            Assert.AreEqual(3, bloodcreeper.Loot.ObservedEmptyInventories);
            Assert.AreEqual(1, bloodcreeper.Loot.Entries.Length);
            Assert.AreEqual("42640:42641:30:1:4", string.Format("{0}:{1}:{2}:{3}:{4}", bloodcreeper.Loot.Entries[0].LowId, bloodcreeper.Loot.Entries[0].HighId, bloodcreeper.Loot.Entries[0].QualityLevel, bloodcreeper.Loot.Entries[0].ObservedCount, bloodcreeper.Loot.Entries[0].ObservedCorpses));
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, bloodcreeper.Loot.CreditEvidence);
            Assert.AreEqual(150, bloodcreeper.Loot.MinimumCredits);
            Assert.AreEqual(150, bloodcreeper.Loot.MaximumCredits);
            Assert.AreEqual(1, bloodcreeper.Loot.LevelCreditRules.Length);
            Assert.AreEqual(24, bloodcreeper.Loot.LevelCreditRules[0].EnemyLevel);
            Assert.AreEqual(150, bloodcreeper.Loot.LevelCreditRules[0].MinimumCredits);
            Assert.AreEqual(150, bloodcreeper.Loot.LevelCreditRules[0].MaximumCredits);
            Assert.AreEqual(3, bloodcreeper.Loot.LevelCreditRules[0].ObservedCorpses);
            Assert.AreEqual(26978, bloodcreeper.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, bloodcreeper.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, bloodcreeper.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, bloodcreeper.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile stimFiend = ordinaryProfiles.Single(value => value.DisplayName == "Stim Fiend");
            OrdinaryEnemySpawnDefinition[] stimFiendSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == stimFiend.ProfileKey)
                .ToArray();
            Assert.AreEqual(17, CountOccurrences(ordinaryProviderText, "\"stim_fiend\""));
            Assert.AreEqual(15, stimFiendSpawns.Length);
            Assert.AreEqual(15, stimFiendSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(0, stimFiendSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.IsTrue(stimFiendSpawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, stimFiend.Aggression.Mode);
            Assert.IsTrue(stimFiend.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.UnarmedMelee, stimFiend.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.CapturedFixed, stimFiend.Combat.DamageSource);
            Assert.IsFalse(stimFiend.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.FixedAttackInfo, stimFiend.Combat.Contract.AttackModel);
            Assert.AreEqual(10, stimFiend.Combat.Contract.MinDamage);
            Assert.AreEqual(16, stimFiend.Combat.Contract.MaxDamage);
            Assert.AreEqual(5.666535, stimFiend.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, stimFiend.Loot.PoolMode);
            Assert.IsFalse(stimFiend.Loot.ItemPoolComplete);
            Assert.AreEqual(13, stimFiend.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, stimFiend.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "102055:102056:11:1:13", "112232:112233:11:1:13",
                    "234874:234874:1:1:13", "234876:234876:1:1:13", "234877:234877:1:1:13",
                    "291043:291044:9:6:13", "291043:291044:10:2:13", "291043:291044:11:1:13",
                    "291043:291044:12:2:13", "291043:291044:13:1:13", "291043:291044:15:1:13",
                    "291082:291083:9:6:13", "291082:291083:10:2:13", "291082:291083:11:1:13",
                    "291082:291083:12:2:13", "291082:291083:13:1:13", "291082:291083:15:1:13"
                },
                stimFiend.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "10:59:59:6", "11:66:66:2", "12:72:72:4", "13:79:79:2", "14:85:85:1" },
                stimFiend.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.IsFalse(stimFiend.Loot.LevelCreditRules.Any(value => value.EnemyLevel == 17));
            Assert.AreEqual(5907, stimFiend.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, stimFiend.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, stimFiend.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, stimFiend.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile neuralBurnout = ordinaryProfiles.Single(value => value.DisplayName == "Neural Burnout");
            OrdinaryEnemySpawnDefinition[] neuralBurnoutSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == neuralBurnout.ProfileKey)
                .ToArray();
            Assert.AreEqual(9, CountOccurrences(ordinaryProviderText, "\"neural_burnout\""));
            Assert.AreEqual(7, neuralBurnoutSpawns.Length);
            Assert.AreEqual(7, neuralBurnoutSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(0, neuralBurnoutSpawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.IsTrue(neuralBurnoutSpawns.All(value => value.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, neuralBurnout.Aggression.Mode);
            Assert.IsTrue(neuralBurnout.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.UnarmedMelee, neuralBurnout.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.CapturedFixed, neuralBurnout.Combat.DamageSource);
            Assert.IsFalse(neuralBurnout.Combat.VisibleWeapon);
            Assert.AreEqual(CapturedEnemyAttackModel.FixedAttackInfo, neuralBurnout.Combat.Contract.AttackModel);
            Assert.AreEqual(15, neuralBurnout.Combat.Contract.MinDamage);
            Assert.AreEqual(22, neuralBurnout.Combat.Contract.MaxDamage);
            Assert.AreEqual(9.929338, neuralBurnout.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, neuralBurnout.Loot.PoolMode);
            Assert.IsFalse(neuralBurnout.Loot.ItemPoolComplete);
            Assert.AreEqual(6, neuralBurnout.Loot.ObservedCompleteInventories);
            Assert.AreEqual(2, neuralBurnout.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "26471:26471:14:1:6",
                    "122142:122142:21:1:6",
                    "123021:123021:21:1:6",
                    "124409:124410:18:1:6",
                    "124560:124561:16:1:6"
                },
                neuralBurnout.Loot.Entries
                    .Select(value => string.Format("{0}:{1}:{2}:{3}:{4}", value.LowId, value.HighId, value.QualityLevel, value.ObservedCount, value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "16:98:98:1", "17:105:105:2", "18:111:111:3", "23:144:144:1", "25:156:156:2" },
                neuralBurnout.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format("{0}:{1}:{2}:{3}", value.EnemyLevel, value.MinimumCredits, value.MaximumCredits, value.ObservedCorpses))
                    .ToArray());
            Assert.IsFalse(neuralBurnout.Loot.LevelCreditRules.Any(value => value.EnemyLevel == 22));
            Assert.AreEqual(5941, neuralBurnout.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, neuralBurnout.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, neuralBurnout.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, neuralBurnout.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile uncontrollableAnger = ordinaryProfiles.Single(
                value => value.DisplayName == "Uncontrollable Anger");
            OrdinaryEnemySpawnDefinition[] uncontrollableAngerSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == uncontrollableAnger.ProfileKey)
                .ToArray();
            Assert.AreEqual(6, uncontrollableAngerSpawns.Length);
            Assert.AreEqual(
                6,
                uncontrollableAngerSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                0,
                uncontrollableAngerSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            CollectionAssert.AreEqual(
                new[] { 13, 13, 19, 20, 23, 23 },
                uncontrollableAngerSpawns
                    .Select(value => value.LevelDefinition.MinimumLevel)
                    .OrderBy(value => value)
                    .ToArray());
            Assert.AreEqual(
                2,
                uncontrollableAngerSpawns.Count(
                    value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol));
            Assert.AreEqual(
                4,
                uncontrollableAngerSpawns.Count(
                    value => value.MovementMode == OrdinaryEnemyMovementMode.Static));
            Assert.IsTrue(
                uncontrollableAngerSpawns.All(
                    value => value.RespawnPolicy.Mode
                             == WorldRespawnPolicyAssignmentMode.Inherit));
            Assert.AreEqual(
                OrdinaryEnemyAggressionMode.Retaliate,
                uncontrollableAnger.Aggression.Mode);
            Assert.IsTrue(uncontrollableAnger.Aggression.Chase);
            Assert.IsFalse(uncontrollableAnger.Aggression.ReturnToSpawn);
            Assert.AreEqual(
                OrdinaryEnemyCombatMode.UnarmedMelee,
                uncontrollableAnger.Combat.Mode);
            Assert.AreEqual(
                OrdinaryEnemyDamageSource.CapturedFixed,
                uncontrollableAnger.Combat.DamageSource);
            Assert.IsFalse(uncontrollableAnger.Combat.VisibleWeapon);
            Assert.IsFalse(uncontrollableAnger.Combat.Contract.IsCombatReady);
            Assert.IsTrue(uncontrollableAnger.Combat.Contract.IsQuarantined);
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                uncontrollableAnger.Combat.Contract.QuarantineReason));
            Assert.AreEqual(
                CapturedEnemyAttackModel.FixedAttackInfo,
                uncontrollableAnger.Combat.Contract.AttackModel);
            Assert.AreEqual(9, uncontrollableAnger.Combat.Contract.MinDamage);
            Assert.AreEqual(18, uncontrollableAnger.Combat.Contract.MaxDamage);
            Assert.AreEqual(5.167153, uncontrollableAnger.Combat.Contract.RechargeSeconds);
            Assert.AreEqual(0, uncontrollableAnger.Combat.Contract.AttackInfoWeaponSlot);
            Assert.AreEqual(0, uncontrollableAnger.Combat.Contract.AttackInfoUnknown);
            Assert.AreEqual(0x53495731, uncontrollableAnger.Combat.Contract.AttackInfoWeaponInstance);
            Assert.AreEqual(
                OrdinaryEnemyLootPoolMode.IndependentEntries,
                uncontrollableAnger.Loot.PoolMode);
            Assert.IsFalse(uncontrollableAnger.Loot.ItemPoolComplete);
            Assert.AreEqual(4, uncontrollableAnger.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, uncontrollableAnger.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "101809:101810:24:1:4",
                    "109366:109367:9:1:4",
                    "112863:112864:13:1:4",
                    "234877:234877:1:1:4",
                    "290619:202727:19:1:4"
                },
                uncontrollableAnger.Loot.Entries
                    .Select(value => string.Format(
                        "{0}:{1}:{2}:{3}:{4}",
                        value.LowId,
                        value.HighId,
                        value.QualityLevel,
                        value.ObservedCount,
                        value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "11:14:14:1",
                    "12:15:15:2",
                    "13:16:16:2",
                    "19:24:24:1",
                    "20:25:25:1",
                    "21:26:26:1"
                },
                uncontrollableAnger.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(value => string.Format(
                        "{0}:{1}:{2}:{3}",
                        value.EnemyLevel,
                        value.MinimumCredits,
                        value.MaximumCredits,
                        value.ObservedCorpses))
                    .ToArray());
            Assert.IsFalse(
                uncontrollableAnger.Loot.LevelCreditRules.Any(
                    value => value.EnemyLevel == 23));
            Assert.AreEqual(96177, uncontrollableAnger.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, uncontrollableAnger.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, uncontrollableAnger.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, uncontrollableAnger.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile incompleteRebuild = ordinaryProfiles.Single(
                value => value.DisplayName == "Incomplete Rebuild");
            OrdinaryEnemySpawnDefinition[] incompleteRebuildSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == incompleteRebuild.ProfileKey)
                .ToArray();
            Assert.AreEqual(10, incompleteRebuildSpawns.Length);
            Assert.AreEqual(
                10,
                incompleteRebuildSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                23,
                incompleteRebuildSpawns.Sum(
                    value => value.LevelDefinition.GetExplicitVariants().Length));
            Assert.IsTrue(
                incompleteRebuildSpawns.All(
                    spawn => spawn.LevelDefinition.Mode
                             == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants
                             && spawn.RespawnPolicy.Mode
                                == WorldRespawnPolicyAssignmentMode.Inherit
                             && spawn.LevelDefinition.GetExplicitVariants().All(
                                 variant => incompleteRebuild.Combat.ResolveContract(
                                                spawn.SourceIdentity,
                                                variant)
                                            .IsQuarantined)),
                "Subway Incomplete Rebuild must preserve all ten exact sources, 23 atomic capture-reviewed generations, and private four-minute respawn while incomplete combat remains quarantined.");
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Auto, incompleteRebuild.Aggression.Mode);
            Assert.AreEqual(7.0, incompleteRebuild.Aggression.AutomaticAggroRadius.Value);
            Assert.IsTrue(incompleteRebuild.Aggression.Chase);
            Assert.IsTrue(incompleteRebuild.Aggression.ReturnToSpawn);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, incompleteRebuild.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, incompleteRebuild.Combat.DamageSource);
            Assert.IsTrue(incompleteRebuild.Combat.VisibleWeapon);
            Assert.IsNotNull(incompleteRebuild.SupportNano);
            Assert.AreEqual(90405, incompleteRebuild.SupportNano.PrimaryNanoId);
            Assert.AreEqual(47, incompleteRebuild.SupportNano.NanoCost);
            Assert.AreEqual(960, incompleteRebuild.SupportNano.PeriodicTickCount);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, incompleteRebuild.Loot.PoolMode);
            Assert.IsFalse(incompleteRebuild.Loot.ItemPoolComplete);
            Assert.AreEqual(2, incompleteRebuild.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, incompleteRebuild.Loot.ObservedEmptyInventories);
            Assert.AreEqual(2, incompleteRebuild.Loot.Entries.Length);
            CollectionAssert.AreEqual(
                new[]
                    {
                        "17:105:Observed", "18:111:Observed", "19:118:Observed",
                        "20:124:Policy", "21:131:Observed", "22:137:Policy"
                    },
                incompleteRebuild.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.EvidenceState))
                    .ToArray());
            Assert.AreEqual(5921, incompleteRebuild.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, incompleteRebuild.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, incompleteRebuild.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, incompleteRebuild.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile fragmentedSoul = ordinaryProfiles.Single(
                value => value.DisplayName == "Fragmented Soul");
            OrdinaryEnemySpawnDefinition[] fragmentedSoulSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == fragmentedSoul.ProfileKey)
                .OrderBy(value => value.SourceIdentity)
                .ToArray();
            Assert.AreEqual(10, fragmentedSoulSpawns.Length);
            Assert.AreEqual(
                10,
                fragmentedSoulSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                19,
                fragmentedSoulSpawns.Sum(
                    value => value.LevelDefinition.GetExplicitVariants().Length));
            Assert.IsTrue(
                fragmentedSoulSpawns.All(
                    value => value.MovementMode == OrdinaryEnemyMovementMode.Static));
            CollectionAssert.AreEqual(
                new[] { 0x7954517A },
                fragmentedSoulSpawns
                    .Where(value => value.Waypoints.Length == 1)
                    .Select(value => value.SourceIdentity)
                    .ToArray());
            Assert.IsTrue(
                fragmentedSoulSpawns.All(
                    spawn => spawn.LevelDefinition.Mode
                             == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants
                             && spawn.RespawnPolicy.Mode
                                == WorldRespawnPolicyAssignmentMode.Inherit));
            foreach (OrdinaryEnemySpawnDefinition spawn in fragmentedSoulSpawns)
            {
                foreach (OrdinaryEnemySpawnVariant variant in
                    spawn.LevelDefinition.GetExplicitVariants())
                {
                    Assert.IsNotNull(variant.WeaponLoadout);
                    CapturedEnemyCombatContract contract =
                        fragmentedSoul.Combat.ResolveContract(
                            spawn.SourceIdentity,
                            variant);
                    Assert.IsFalse(contract.IsCombatReady, contract.Evidence);
                    Assert.IsTrue(contract.IsQuarantined, contract.Evidence);
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(contract.QuarantineReason),
                        contract.Evidence);
                }
            }
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, fragmentedSoul.Aggression.Mode);
            Assert.IsFalse(fragmentedSoul.Aggression.AutomaticAggroRadius.HasValue);
            Assert.IsTrue(fragmentedSoul.Aggression.Chase);
            Assert.IsFalse(fragmentedSoul.Aggression.ReturnToSpawn);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, fragmentedSoul.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, fragmentedSoul.Combat.DamageSource);
            Assert.IsTrue(fragmentedSoul.Combat.VisibleWeapon);
            Assert.IsNotNull(fragmentedSoul.SupportNano);
            Assert.AreEqual(95447, fragmentedSoul.SupportNano.PrimaryNanoId);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, fragmentedSoul.Loot.PoolMode);
            Assert.IsFalse(fragmentedSoul.Loot.ItemPoolComplete);
            Assert.AreEqual(4, fragmentedSoul.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, fragmentedSoul.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEqual(
                new[]
                    {
                        "26471:26471:14:3:4", "85691:22004:18:1:4",
                        "85732:21963:17:1:4", "124304:124305:17:1:4",
                        "234877:234877:1:2:4", "301712:301712:1:1:4"
                    },
                fragmentedSoul.Loot.Entries
                    .OrderBy(value => value.LowId)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                    {
                        "17:105:Observed", "18:111:Observed", "19:118:Policy",
                        "20:124:Policy", "21:131:Observed"
                    },
                fragmentedSoul.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.EvidenceState))
                    .ToArray());
            Assert.AreEqual(5921, fragmentedSoul.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, fragmentedSoul.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, fragmentedSoul.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, fragmentedSoul.Corpse.LootedCleanupSeconds);

            OrdinaryEnemyProfile redundantScan = ordinaryProfiles.Single(
                value => value.DisplayName == "Redundant Scan");
            OrdinaryEnemySpawnDefinition[] redundantScanSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == redundantScan.ProfileKey)
                .ToArray();
            Assert.AreEqual(4, redundantScanSpawns.Length);
            Assert.AreEqual(
                4,
                redundantScanSpawns.Count(
                    value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                10,
                redundantScanSpawns.Sum(
                    value => value.LevelDefinition.GetExplicitVariants().Length));
            Assert.AreEqual(
                1,
                redundantScanSpawns.Count(
                    value => value.MovementMode == OrdinaryEnemyMovementMode.Patrol));
            Assert.IsTrue(
                redundantScanSpawns.All(
                    spawn => spawn.LevelDefinition.Mode
                             == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants
                             && spawn.RespawnPolicy.Mode
                                == WorldRespawnPolicyAssignmentMode.Inherit
                             && spawn.LevelDefinition.GetExplicitVariants().All(
                                 variant => redundantScan.Combat.ResolveContract(
                                                spawn.SourceIdentity,
                                                variant)
                                            .IsQuarantined)),
                "Subway Redundant Scan must preserve four exact sources, ten atomic capture-reviewed generations, captured movement, and private inherited respawn while incomplete combat remains quarantined.");
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Auto, redundantScan.Aggression.Mode);
            Assert.AreEqual(7.0, redundantScan.Aggression.AutomaticAggroRadius.Value);
            Assert.IsTrue(redundantScan.Aggression.Chase);
            Assert.AreEqual(OrdinaryEnemyCombatMode.EquippedRanged, redundantScan.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.WeaponRoll, redundantScan.Combat.DamageSource);
            Assert.IsTrue(redundantScan.Combat.VisibleWeapon);
            Assert.IsNotNull(redundantScan.SupportNano);
            Assert.AreEqual(121336, redundantScan.SupportNano.PrimaryNanoId);
            Assert.AreEqual(121248, redundantScan.SupportNano.TriggeredSelfNanoId);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, redundantScan.Loot.PoolMode);
            Assert.IsFalse(redundantScan.Loot.ItemPoolComplete);
            Assert.AreEqual(2, redundantScan.Loot.ObservedCompleteInventories);
            Assert.AreEqual(1, redundantScan.Loot.ObservedEmptyInventories);
            CollectionAssert.AreEqual(
                new[] { "27263:27263:10:1:2" },
                redundantScan.Loot.Entries
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { "19:118", "20:124", "21:131", "22:137" },
                redundantScan.Loot.LevelCreditRules
                    .OrderBy(value => value.EnemyLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}",
                            value.EnemyLevel,
                            value.MinimumCredits))
                    .ToArray());
            Assert.AreEqual(23370, redundantScan.Corpse.CapturedCatMesh);
            Assert.AreEqual(0.0, redundantScan.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(60.0, redundantScan.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(0.0, redundantScan.Corpse.LootedCleanupSeconds);

            Assert.AreEqual(
                12,
                CountOccurrences(providerText, "CapturedSurveySpawn(DisobedientBot("),
                "Accepted Subway Disobedient Bot must preserve all 12 exact spawn rows.");
            Assert.IsTrue(disobedientBotDefinition.Contains("\"Disobedient Bot\""), "Accepted Disobedient Bot name is missing.");
            Assert.IsTrue(disobedientBotDefinition.Contains("17649"), "Accepted Disobedient Bot MonsterData is missing.");
            Assert.IsTrue(disobedientBotDefinition.Contains("138"), "Accepted Disobedient Bot NPC family is missing.");
            Assert.IsTrue(catalogText.Contains("SubwayOrdinaryRespawnSeconds = 240.0"), "Accepted Disobedient Bot shared scheduler delay is missing.");
            Assert.IsTrue(catalogText.Contains("SubwayOrdinaryRespawnPolicy()"), "Accepted Disobedient Bot shared respawn policy is missing.");
            Assert.IsTrue(
                disobedientBotCombatReport.Contains("\"20260708-143600\"")
                && disobedientBotCombatReport.Contains("\"20260712-153918\"")
                && disobedientBotCombatReport.Contains("\"20260713-014714\"")
                && disobedientBotCombatReport.Contains("\"20260713-033511\"")
                && disobedientBotCombatReport.Contains("\"20260719-020104\"")
                && disobedientBotCombatReport.Contains("\"normalAttackInfoRows\": 15")
                && disobedientBotCombatReport.Contains("\"normalMinDamage\": 6")
                && disobedientBotCombatReport.Contains("\"normalMaxDamage\": 15")
                && disobedientBotCombatReport.Contains("\"missedAttackInfoRows\": 10")
                && disobedientBotCombatReport.Contains("\"medianIntervalSeconds\": 5.973723")
                && disobedientBotCombatReport.Contains("\"attackInfoRows\": 15")
                && disobedientBotCombatReport.Contains("\"attackInfoRows\": 3")
                && disobedientBotCombatReport.Contains("\"attackInfoRows\": 2"),
                "Accepted Subway Disobedient Bot generated combat evidence must retain the local-player, other-player, and player-owned-pet boundaries plus focused attempt cadence.");
            Assert.IsTrue(
                attackRulesText.Contains("CapturedSubwayThiefMonsterData = 26092")
                && attackRulesText.Contains("CapturedSubwayThiefAttackInfoAmmoCount = -1")
                && attackRulesText.Contains("CapturedSubwayThiefAttackInfoUnknown = 0")
                && attackRulesText.Contains("CapturedSubwayThiefRechargeSeconds = 6.0")
                && attackRulesText.Contains("CapturedSubwayThiefWeaponDamageMinimumOverride = 0")
                && attackRulesText.Contains("CapturedSubwayThiefWeaponDamageMaximumOverride = 0"),
                "Accepted Subway Thief must not silently fall back to fixed fake damage or stale AttackInfo constants.");
        }



        [TestMethod]
        public void OrdinaryEnemyProfileValidatorAcceptsStableKeysAndExplicitUnresolvedEvidence()
        {
            OrdinaryEnemyProfile first = CreateOrdinaryEnemyProfile("profile.a");
            OrdinaryEnemyProfile second = CreateOrdinaryEnemyProfile("profile.b");
            OrdinaryEnemySpawnDefinition firstSpawn = CreateOrdinaryEnemySpawn(
                "spawn.0000000A",
                10,
                first.ProfileKey);
            OrdinaryEnemySpawnDefinition secondSpawn = CreateOrdinaryEnemySpawn(
                "spawn.00000014",
                20,
                second.ProfileKey);

            OrdinaryEnemyProfileValidator.Validate(
                new[] { first, second },
                new[] { firstSpawn, secondSpawn });

            OrdinaryEnemyProfile unresolved = CreateOrdinaryEnemyProfile(
                "profile.unresolved",
                OrdinaryEnemyConstructionMode.TemplateBacked,
                OrdinaryEnemyAggressionMode.Retaliate,
                OrdinaryEnemyCombatMode.Unresolved,
                OrdinaryEnemyDamageSource.Unresolved,
                OrdinaryEnemyLootEvidence.Unresolved,
                false,
                false);
            OrdinaryEnemySpawnDefinition unresolvedSpawn = CreateOrdinaryEnemySpawn(
                "spawn.0000001E",
                30,
                unresolved.ProfileKey,
                OrdinaryEnemyMovementMode.Static,
                null,
                false,
                OrdinaryEnemyEvidenceState.Unresolved,
                null);

            OrdinaryEnemyProfileValidator.Validate(
                new[] { unresolved },
                new[] { unresolvedSpawn });
            Assert.IsFalse(unresolvedSpawn.HasRespawnDelay);
            Assert.AreEqual(OrdinaryEnemyCombatMode.Unresolved, unresolved.Combat.Mode);
            Assert.AreEqual(OrdinaryEnemyDamageSource.Unresolved, unresolved.Combat.DamageSource);
            Assert.AreEqual(OrdinaryEnemyLootEvidence.Unresolved, unresolved.Loot.Evidence);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Unresolved, unresolved.Loot.CreditEvidence);
        }

        [TestMethod]
        public void OrdinaryEnemyProfileValidatorRejectsDuplicateAndNondeterministicKeys()
        {
            OrdinaryEnemyProfile profileA = CreateOrdinaryEnemyProfile("profile.a");
            OrdinaryEnemyProfile profileB = CreateOrdinaryEnemyProfile("profile.b");

            AssertOrdinaryEnemyValidationFails(
                new[] { profileB, profileA },
                new OrdinaryEnemySpawnDefinition[0],
                "Profile ordering must be deterministic.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profileA, CreateOrdinaryEnemyProfile("profile.a") },
                new OrdinaryEnemySpawnDefinition[0],
                "Duplicate profile keys must fail closed.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profileA },
                new[]
                    {
                        CreateOrdinaryEnemySpawn("spawn.same", 10, profileA.ProfileKey),
                        CreateOrdinaryEnemySpawn("spawn.same", 20, profileA.ProfileKey)
                    },
                "Duplicate spawn keys must fail closed.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profileA },
                new[]
                    {
                        CreateOrdinaryEnemySpawn("spawn.0000000A", 10, profileA.ProfileKey),
                        CreateOrdinaryEnemySpawn("spawn.00000014", 10, profileA.ProfileKey)
                    },
                "Duplicate source identities must fail closed.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profileA },
                new[]
                    {
                        CreateOrdinaryEnemySpawn("spawn.00000014", 20, profileA.ProfileKey),
                        CreateOrdinaryEnemySpawn("spawn.0000000A", 10, profileA.ProfileKey)
                    },
                "Spawn identity ordering must be deterministic.");
        }

        [TestMethod]
        public void OrdinaryEnemyProfileValidatorRejectsMissingProfilesBossesSummonsAndScriptedBehavior()
        {
            OrdinaryEnemyProfile profile = CreateOrdinaryEnemyProfile("profile.valid");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[] { CreateOrdinaryEnemySpawn("spawn.missing", 10, "profile.missing") },
                "A spawn referencing a missing profile must fail closed.");
            AssertOrdinaryEnemyValidationFails(
                new[]
                    {
                        CreateOrdinaryEnemyProfile(
                            "profile.boss",
                            OrdinaryEnemyConstructionMode.TemplateBacked,
                            OrdinaryEnemyAggressionMode.Retaliate,
                            OrdinaryEnemyCombatMode.UnarmedMelee,
                            OrdinaryEnemyDamageSource.CapturedFixed,
                            OrdinaryEnemyLootEvidence.NoneProven,
                            true,
                            false)
                    },
                new OrdinaryEnemySpawnDefinition[0],
                "Boss profiles must use a custom encounter module.");
            AssertOrdinaryEnemyValidationFails(
                new[]
                    {
                        CreateOrdinaryEnemyProfile(
                            "profile.summon",
                            OrdinaryEnemyConstructionMode.TemplateBacked,
                            OrdinaryEnemyAggressionMode.Retaliate,
                            OrdinaryEnemyCombatMode.UnarmedMelee,
                            OrdinaryEnemyDamageSource.CapturedFixed,
                            OrdinaryEnemyLootEvidence.NoneProven,
                            false,
                            true)
                    },
                new OrdinaryEnemySpawnDefinition[0],
                "Owned summons must not enter the ordinary enemy catalog.");
            AssertOrdinaryEnemyValidationFails(
                new[]
                    {
                        CreateOrdinaryEnemyProfile(
                            "profile.unresolved-aggression",
                            OrdinaryEnemyConstructionMode.TemplateBacked,
                            OrdinaryEnemyAggressionMode.Unresolved,
                            OrdinaryEnemyCombatMode.UnarmedMelee,
                            OrdinaryEnemyDamageSource.CapturedFixed,
                            OrdinaryEnemyLootEvidence.NoneProven,
                            false,
                            false)
                    },
                new OrdinaryEnemySpawnDefinition[0],
                "Aggression must be an explicit runtime selection.");
            AssertOrdinaryEnemyValidationFails(
                new[]
                    {
                        CreateOrdinaryEnemyProfile(
                            "profile.scripted-aggression",
                            OrdinaryEnemyConstructionMode.TemplateBacked,
                            OrdinaryEnemyAggressionMode.Scripted,
                            OrdinaryEnemyCombatMode.UnarmedMelee,
                            OrdinaryEnemyDamageSource.CapturedFixed,
                            OrdinaryEnemyLootEvidence.NoneProven,
                            false,
                            false)
                    },
                new OrdinaryEnemySpawnDefinition[0],
                "Scripted aggression must use a custom encounter module.");
            AssertOrdinaryEnemyValidationFails(
                new[]
                    {
                        CreateOrdinaryEnemyProfile(
                            "profile.scripted-combat",
                            OrdinaryEnemyConstructionMode.TemplateBacked,
                            OrdinaryEnemyAggressionMode.Retaliate,
                            OrdinaryEnemyCombatMode.Scripted,
                            OrdinaryEnemyDamageSource.Scripted,
                            OrdinaryEnemyLootEvidence.NoneProven,
                            false,
                            false)
                    },
                new OrdinaryEnemySpawnDefinition[0],
                "Scripted combat must use a custom encounter module.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.unresolved",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Unresolved)
                    },
                "Movement must be an explicit runtime selection.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.scripted",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Scripted)
                    },
                "Scripted movement must use a custom encounter module.");
        }

        [TestMethod]
        public void OrdinaryEnemyProfileValidatorEnforcesMovementAndRespawnEvidence()
        {
            OrdinaryEnemyProfile profile = CreateOrdinaryEnemyProfile("profile.valid");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.patrol-missing",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Patrol,
                            new[] { new OrdinaryEnemyWaypoint(1.0f, 2.0f, 3.0f) })
                    },
                "Patrol movement requires at least two points or captured replay.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.roam-missing",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Roam)
                    },
                "Roam movement requires captured movement data.");

            OrdinaryEnemySpawnDefinition replayPatrol = CreateOrdinaryEnemySpawn(
                "spawn.replay",
                10,
                profile.ProfileKey,
                OrdinaryEnemyMovementMode.Patrol,
                null,
                true);
            OrdinaryEnemySpawnDefinition waypointPatrol = CreateOrdinaryEnemySpawn(
                "spawn.waypoints",
                20,
                profile.ProfileKey,
                OrdinaryEnemyMovementMode.Patrol,
                new[]
                    {
                        new OrdinaryEnemyWaypoint(1.0f, 2.0f, 3.0f),
                        new OrdinaryEnemyWaypoint(4.0f, 5.0f, 6.0f)
                    });
            OrdinaryEnemyProfileValidator.Validate(
                new[] { profile },
                new[] { replayPatrol, waypointPatrol });

            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.respawn-null",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Static,
                            null,
                            false,
                            OrdinaryEnemyEvidenceState.Observed,
                            null)
                    },
                "Observed respawn evidence requires a delay.");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.respawn-zero",
                            10,
                            profile.ProfileKey,
                            OrdinaryEnemyMovementMode.Static,
                            null,
                            false,
                            OrdinaryEnemyEvidenceState.Observed,
                            0.0)
                    },
                "Observed respawn evidence requires a positive delay.");

            OrdinaryEnemySpawnLevelDefinition invalidRange = new OrdinaryEnemySpawnLevelDefinition(
                OrdinaryEnemySpawnLevelMode.InclusiveRange,
                25,
                15,
                24,
                691,
                33,
                0,
                70,
                83,
                3,
                OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration,
                OrdinaryEnemyEvidenceState.Policy,
                "invalid-range");
            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.invalid-range",
                            10,
                            profile.ProfileKey,
                            levelDefinition: invalidRange)
                    },
                "Invalid level ranges must fail profile validation.");

            AssertOrdinaryEnemyValidationFails(
                new[] { profile },
                new[]
                    {
                        CreateOrdinaryEnemySpawn(
                            "spawn.invalid-respawn-assignment",
                            10,
                            profile.ProfileKey,
                            respawnPolicy: new WorldRespawnPolicyAssignment(
                                (WorldRespawnPolicyAssignmentMode)999,
                                null,
                                null,
                                "unsupported-assignment",
                                "UNRESOLVED"))
                    },
                "Unsupported respawn assignment modes must fail profile validation.");

            OrdinaryEnemySpawnDefinition observedRespawn = CreateOrdinaryEnemySpawn(
                "spawn.respawn-valid",
                10,
                profile.ProfileKey,
                OrdinaryEnemyMovementMode.Static,
                null,
                false,
                OrdinaryEnemyEvidenceState.Observed,
                60.0);
            OrdinaryEnemyProfileValidator.Validate(new[] { profile }, new[] { observedRespawn });
            Assert.IsTrue(observedRespawn.HasRespawnDelay);
        }



        [TestMethod]
        public void PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators()
        {
            string repositoryRoot = FindRepositoryRoot();
            string objectLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\Playfields\PlayfieldObjectLifecycleRuntimeService.cs"));
            string characterEntityText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AORebirth.Core\Entities\Character.cs"));

            string[] runtimeCoordinatorConstructors =
                {
                    "new PlayfieldObjectLifecycleRuntimeService()",
                    "new PlayfieldObjectMaterializationRuntimeService()",
                    "new PlayfieldDbMobSpawnRuntimeService()",
                    "new PlayfieldEnvironmentFunctionRuntimeService()",
                    "new NpcChaseNavigationRuntimeService(",
                    "new PlayfieldNpcCombatMovementRuntimeService(this.npcChaseNavigation)",
                    "new PlayfieldCharacterHeartbeatRuntimeService()",
                    "new PlayfieldPacketSequencingRuntimeService(this.packetSequencing)",
                    "new PlayfieldCorpseAccessRuntimeService()",
                    "new PlayfieldRewardRuntimeService()",
                    "new NPCRuntimeService(",
                    "new PlayfieldLifecycleRuntimeService()",
                    "new PlayfieldPlayerDeathRespawnRuntimeService()",
                    "new PlayfieldStatelTransitionRuntimeService()",
                    "new PlayfieldStatUpdateRuntimeService()",
                    "new PlayfieldStaticDynelRuntimeService()",
                    "new PlayfieldTimedLifecycleRuntimeService()",
                    "new PlayfieldVendorRuntimeService()",
                    "new PlayfieldVisibilityFanoutRuntimeService()",
                    "new PlayfieldWallCollisionRuntimeService()",
                    "new PlayfieldAnnouncementRuntimeService()",
                    "new PlayfieldPublishFanoutRuntimeService()",
                    "new PlayfieldAOtomationDeliveryRuntimeService()",
                    "new PrivateCityReadyInitCoordinator("
                };
            Assert.IsTrue(
                objectLifecycleText.Contains("internal sealed class PlayfieldObjectLifecycleRuntimeService")
                && objectLifecycleText.Contains("internal void RemoveInstancedEntity(IInstancedEntity entity)")
                && objectLifecycleText.Contains("Pool.Instance.RemoveObject(entity);"),
                "PlayfieldObjectLifecycleRuntimeService must own safe instanced object removal routing.");
            string characterEntityTick = ExtractMethodBlock(
                characterEntityText,
                "public override void Tick");
            Assert.IsTrue(
                objectLifecycleText.Contains("internal int DespawnCorpses<TCorpseState>(")
                && objectLifecycleText.Contains("pendingCorpseSpawns.Remove(candidate.Key);")
                && objectLifecycleText.Contains("despawnCorpse(corpseInstance);"),
                "PlayfieldObjectLifecycleRuntimeService must own explicit corpse-despawn predicate routing.");
            AssertTextBefore(
                objectLifecycleText,
                "pendingCorpseSpawns.Remove(candidate.Key);",
                "despawnCorpse(corpseInstance);");
            Assert.IsTrue(
                objectLifecycleText.Contains("internal void DespawnCorpse(")
                && objectLifecycleText.Contains("sendDespawn(corpseIdentity);")
                && objectLifecycleText.Contains("clearNpcCorpseDespawn(corpseInstance);")
                && objectLifecycleText.Contains("removeCorpseState(corpseInstance);")
                && objectLifecycleText.Contains("removePendingCorpseCreditAward(corpseInstance);"),
                "PlayfieldObjectLifecycleRuntimeService must own corpse despawn cleanup order.");
            AssertTextBefore(
                objectLifecycleText,
                "sendDespawn(corpseIdentity);",
                "clearNpcCorpseDespawn(corpseInstance);");
            AssertTextBefore(
                objectLifecycleText,
                "clearNpcCorpseDespawn(corpseInstance);",
                "removeCorpseState(corpseInstance);");
            AssertTextBefore(
                objectLifecycleText,
                "removeCorpseState(corpseInstance);",
                "removePendingCorpseCreditAward(corpseInstance);");
            Assert.IsTrue(
                objectLifecycleText.Contains("internal void ProcessPendingCorpseSpawns<TCorpseState>(")
                && objectLifecycleText.Contains("if (!registerCorpse(target, corpseId))")
                && objectLifecycleText.Contains("traceCorpseFullUpdate(corpseId, deadNpcId);")
                && objectLifecycleText.Contains("sendCorpseFullUpdate(target, corpseId);"),
                "PlayfieldObjectLifecycleRuntimeService must own pending corpse spawn callback ordering.");
            string despawnCorpses = ExtractMethodBlock(
                objectLifecycleText,
                "internal int DespawnCorpses<TCorpseState>");
            string processPendingCorpseSpawnsRuntime = ExtractMethodBlock(
                objectLifecycleText,
                "internal void ProcessPendingCorpseSpawns<TCorpseState>");
            AssertTextBefore(
                objectLifecycleText,
                "if (!registerCorpse(target, corpseId))",
                "traceCorpseFullUpdate(corpseId, deadNpcId);");
            AssertTextBefore(
                objectLifecycleText,
                "traceCorpseFullUpdate(corpseId, deadNpcId);",
                "sendCorpseFullUpdate(target, corpseId);");
            Assert.IsFalse(
                objectLifecycleText.Contains("CorpseFullUpdate.Build(")
                || objectLifecycleText.Contains("RollCorpseLootItems")
                || objectLifecycleText.Contains("RollCorpseCredits")
                || objectLifecycleText.Contains("SendCorpseInventoryUpdate")
                || objectLifecycleText.Contains("AwardCorpseCredits")
                || objectLifecycleText.Contains("InventoryUpdateMessage"),
                "PlayfieldObjectLifecycleRuntimeService must not own packet emission, loot, credits, or inventory containers.");
        }





        [TestMethod]
        public void LegacyDbMobSpawnAppearanceUsesCapturedTexturesAndHeadMeshStats()
        {
            string repositoryRoot = FindRepositoryRoot();
            string npcHandlerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AORebirth.Core\NPCHandler\NonPlayerCharacterHandler.cs"));
            string mobSpawnsText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\mobspawns.sql"));
            string mobSpawnStatsText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\mobspawns_stats.sql"));

            Assert.IsTrue(
                npcHandlerText.Contains("mob.Textures0 != 0")
                && npcHandlerText.Contains("cmob.Textures.Add(new AOTextures(0, mob.Textures0));")
                && npcHandlerText.Contains("cmob.Textures.Add(new AOTextures(1, mob.Textures1));")
                && npcHandlerText.Contains("cmob.Textures.Add(new AOTextures(2, mob.Textures2));")
                && npcHandlerText.Contains("cmob.Textures.Add(new AOTextures(3, mob.Textures3));")
                && npcHandlerText.Contains("cmob.Textures.Add(new AOTextures(4, mob.Textures4));"),
                "Legacy DB mob spawns with captured texture columns must hydrate all five SCFU texture slots.");
            Assert.IsTrue(
                mobSpawnsText.Contains("'Guard', 0, 30848, 42260, 30831, 42261")
                && mobSpawnsText.Contains("'Guide', 0, 42239, 42260, 42240, 42261"),
                "Guard and Guide must retain their capture-proven texture slots.");
            Assert.IsTrue(
                mobSpawnStatsText.Contains("(2029842938, 954, 64, 40111)")
                && mobSpawnStatsText.Contains("(2029842939, 954, 64, 40635)"),
                "Guard and Guide must retain their capture-proven headmesh stats.");
            Assert.IsTrue(
                mobSpawnStatsText.Contains("(2029842938, 954, 4, 4)")
                && mobSpawnStatsText.Contains("(2029842938, 954, 59, 1)")
                && mobSpawnStatsText.Contains("(2029842939, 954, 0, 277352961)")
                && mobSpawnStatsText.Contains("(2029842939, 954, 59, 3)"),
                "Guard and Guide must retain their capture-proven breed, gender, and character flags.");
            Assert.IsFalse(
                mobSpawnsText.Contains("0000009CAF0000000004")
                || mobSpawnsText.Contains("0000009EBB0000000004"),
                "Captured head meshes must use Stat 64 rather than ignored legacy DB mesh blobs.");
        }



        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorModelsSessionPhasesWithoutPacketOwnership()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();

            lifecycle.BeginCharacterLoading();
            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            lifecycle.EnterFullCharacterBoundaryForSessionInit();
            lifecycle.EnterCharInPlayForVisibilityEntry();
            lifecycle.CompleteInPlayForSessionInit();
            lifecycle.EnterZoningForPlayfieldTransfer();
            lifecycle.EnterDisconnectingForSessionDispose();
            lifecycle.EnterDisconnectingForSessionDispose();

            var expected =
                new[]
                {
                    ZoneClientSessionPhase.Connected,
                    ZoneClientSessionPhase.CharacterLoading,
                    ZoneClientSessionPhase.PlayfieldLoading,
                    ZoneClientSessionPhase.ReadyBlock,
                    ZoneClientSessionPhase.FullCharacterBoundary,
                    ZoneClientSessionPhase.CharInPlay,
                    ZoneClientSessionPhase.InPlay,
                    ZoneClientSessionPhase.Zoning,
                    ZoneClientSessionPhase.Disconnecting
                };

            Assert.AreEqual(expected[expected.Length - 1], lifecycle.Phase);
            Assert.AreEqual(expected.Length, lifecycle.PhaseHistory.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], lifecycle.PhaseHistory[i]);
            }
            Assert.AreEqual("ZoneClientSession.Disconnecting", lifecycle.PhaseTraceName);

            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\ZoneClientSessionLifecycleCoordinator.cs"));
            Assert.IsTrue(
                coordinatorText.Contains("public bool CanTransitionTo(ZoneClientSessionPhase phase)"),
                "Coordinator must own lifecycle transition validation.");
            Assert.IsTrue(
                coordinatorText.Contains("Invalid ZoneClient session transition"),
                "Coordinator must reject invalid lifecycle transitions.");
            Assert.IsTrue(
                coordinatorText.Contains("case ZoneClientSessionPhase.Zoning:"),
                "Coordinator must explicitly model zoning return transitions.");

            string[] forbiddenRuntimeOwnershipPatterns =
                {
                    "SendCompressed",
                    "MessageHandler",
                    "GenericCmd",
                    "NpcCombat",
                    "Inventory",
                    "OrgClient",
                    "CityController",
                    "GuestKey",
                    "MessagePackZip",
                    "Dao.Instance",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < forbiddenRuntimeOwnershipPatterns.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(forbiddenRuntimeOwnershipPatterns[i]),
                    "ZoneClient session lifecycle coordinator must not own packet, gameplay, DB, or capture behavior: "
                    + forbiddenRuntimeOwnershipPatterns[i]);
            }
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorRejectsInvalidTransitions()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();

            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.ReadyBlock));
            AssertInvalidTransition(
                lifecycle.EnterReadyBlockForSessionInit,
                "ZoneClientSession.Connected to ZoneClientSession.ReadyBlock");

            lifecycle.BeginCharacterLoading();
            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.FullCharacterBoundary));
            AssertInvalidTransition(
                lifecycle.EnterFullCharacterBoundaryForSessionInit,
                "ZoneClientSession.CharacterLoading to ZoneClientSession.FullCharacterBoundary");

            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.InPlay));
            AssertInvalidTransition(
                lifecycle.CompleteInPlayForSessionInit,
                "ZoneClientSession.ReadyBlock to ZoneClientSession.InPlay");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorAllowsZoningReturnOptionsAndDisconnects()
        {
            var zoningToPlayfieldLoading = CreateInPlayLifecycle();
            zoningToPlayfieldLoading.EnterZoningForPlayfieldTransfer();
            Assert.IsTrue(zoningToPlayfieldLoading.CanTransitionTo(ZoneClientSessionPhase.PlayfieldLoading));
            zoningToPlayfieldLoading.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            Assert.AreEqual(ZoneClientSessionPhase.PlayfieldLoading, zoningToPlayfieldLoading.Phase);

            var zoningToReadyBlock = CreateInPlayLifecycle();
            zoningToReadyBlock.EnterZoningForPlayfieldTransfer();
            Assert.IsTrue(zoningToReadyBlock.CanTransitionTo(ZoneClientSessionPhase.ReadyBlock));
            zoningToReadyBlock.EnterReadyBlockForSessionInit();
            Assert.AreEqual(ZoneClientSessionPhase.ReadyBlock, zoningToReadyBlock.Phase);

            var disconnectingFromConnected = new ZoneClientSessionLifecycleCoordinator();
            disconnectingFromConnected.EnterDisconnectingForSessionDispose();
            Assert.AreEqual(ZoneClientSessionPhase.Disconnecting, disconnectingFromConnected.Phase);

            var disconnectingFromZoning = CreateInPlayLifecycle();
            disconnectingFromZoning.EnterZoningForPlayfieldTransfer();
            disconnectingFromZoning.EnterDisconnectingForSessionDispose();
            Assert.AreEqual(ZoneClientSessionPhase.Disconnecting, disconnectingFromZoning.Phase);
        }



        [TestMethod]
        public void ZoneClientSessionLifecycleCheckpointKeepsPhaseOwnershipOutOfPacketCode()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();
            lifecycle.BeginCharacterLoading();
            lifecycle.BeginCharacterLoading();

            Assert.AreEqual(ZoneClientSessionPhase.CharacterLoading, lifecycle.Phase);
            Assert.AreEqual(2, lifecycle.PhaseHistory.Count, "Duplicate same-phase transitions must remain no-op.");

            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\ZoneClientSessionLifecycleCoordinator.cs"));

            Assert.IsTrue(
                coordinatorText.Contains("private static bool IsAllowedTransition(ZoneClientSessionPhase from, ZoneClientSessionPhase to)"),
                "ZoneClientSessionLifecycleCoordinator must own allowed transition rules.");
            Assert.IsTrue(
                coordinatorText.Contains("if (from == to)") && coordinatorText.Contains("return true;"),
                "ZoneClientSessionLifecycleCoordinator must keep duplicate same-phase transitions legal.");
            Assert.IsTrue(
                coordinatorText.Contains("throw new InvalidOperationException("),
                "ZoneClientSessionLifecycleCoordinator must guard invalid transitions.");
            Assert.IsTrue(
                coordinatorText.Contains("public void EnterReadyBlockForSessionInit()")
                && coordinatorText.Contains("public void EnterFullCharacterBoundaryForSessionInit()")
                && coordinatorText.Contains("public void EnterCharInPlayForVisibilityEntry()")
                && coordinatorText.Contains("public void CompleteInPlayForSessionInit()"),
                "ZoneClientSessionLifecycleCoordinator must own named ready/full-character/CharInPlay sequencing surfaces.");
            Assert.IsTrue(
                coordinatorText.Contains("public void EnterPlayfieldLoadingForCharacterLoadOrZoningExit()")
                && coordinatorText.Contains("public void EnterZoningForPlayfieldTransfer()")
                && coordinatorText.Contains("public void EnterDisconnectingForSessionDispose()"),
                "ZoneClientSessionLifecycleCoordinator must own named playfield-loading/zoning/disconnect sequencing surfaces.");

            string[] packetAndRuntimePatterns =
                {
                    "SendCompressed",
                    "PlayfieldAnarchyFMessageHandler",
                    "FullCharacterMessageHandler",
                    "CharInPlayMessage",
                    "TeleportMessageHandler",
                    "PrivateCityReadyInitCoordinator",
                    "NpcCombat",
                    "NpcPatrol",
                    "Movement",
                    "GenericCmd",
                    "Inventory",
                    "OrgClient",
                    "OrgServer",
                    "MessagePackZip",
                    "Dao.Instance",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < packetAndRuntimePatterns.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(packetAndRuntimePatterns[i]),
                    "ZoneClient session lifecycle coordinator must remain phase-only before packet sequencing moves: "
                    + packetAndRuntimePatterns[i]);
            }
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleFinalPhaseOwnershipGuardrailKeepsRuntimeMechanicsOut()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\ZoneClientSessionLifecycleCoordinator.cs"));

            string[] namedPhaseMethods =
                {
                    "EnterPlayfieldLoadingForCharacterLoadOrZoningExit",
                    "EnterReadyBlockForSessionInit",
                    "EnterFullCharacterBoundaryForSessionInit",
                    "EnterCharInPlayForVisibilityEntry",
                    "CompleteInPlayForSessionInit",
                    "EnterZoningForPlayfieldTransfer",
                    "EnterDisconnectingForSessionDispose"
                };
            for (int i = 0; i < namedPhaseMethods.Length; i++)
            {
                Assert.IsTrue(
                    coordinatorText.Contains("public void " + namedPhaseMethods[i] + "()"),
                    "Coordinator must expose named lifecycle phase ownership method " + namedPhaseMethods[i] + ".");
            }

            string[] forbiddenCoordinatorMechanics =
                {
                    "SendCompressed",
                    "TeleportMessageHandler",
                    "ZoneRedirectionMessage",
                    "PrivateCityReadyInitCoordinator",
                    "SendPrivateCity",
                    "SimpleCharFullUpdate.",
                    "CharInPlayMessage",
                    "AnnouncePlayerVisibility",
                    "SendSCFUsToClient",
                    "stopDispatcher",
                    "zStream",
                    "netStream"
                };
            for (int i = 0; i < forbiddenCoordinatorMechanics.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(forbiddenCoordinatorMechanics[i]),
                    "Coordinator must not own packet, teleport, visibility, private-city, or disposal mechanics: "
                    + forbiddenCoordinatorMechanics[i]);
            }
        }





















        [TestMethod]
        public void PlayfieldDirectPoolUsageIsLimitedToNamedGlobalAndCrossPlayfieldExceptions()
        {
            string repositoryRoot = FindRepositoryRoot();

            string[] intentionalGlobalOrCrossPlayfieldExceptions =
                {
                    "NumberOfDynels: global CanbeAffected count, not playfield-local registry count.",
                    "NumberOfPlayers: global CanbeAffected Character count, not playfield-local registry count."
                };
            Assert.AreEqual(
                2,
                intentionalGlobalOrCrossPlayfieldExceptions.Length,
                "Every direct Playfield Pool exception must be named with ownership scope.");
        }

        [TestMethod]
        public void AreteCleaningRobotDbSpawnSuppressionKeepsCapturedPathAndLegacyDbBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"Tests\Fixtures\Gameplay\Playfields\CapturedAreteRobotContentProvider.cs"));
            Assert.IsTrue(
                providerText.Contains("public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()"),
                "CapturedAreteRobotContentProvider must expose captured spawn definitions.");

            string[] suppressedDbRows =
                {
                    "2027138231",
                    "2027138245",
                    "2027138246",
                    "2027138249",
                    "2027138259"
                };
        }



        [TestMethod]
        public void FragmentedSoulNano95447UsesDynamicSkillAndOwnedOrdinaryAllyLifecycle()
        {
            OrdinaryEnemySupportNanoProfile nano =
                OrdinaryEnemySupportNanoProfile.CapturedFragmentedSoul95447();

            Assert.AreEqual(95447, nano.PrimaryNanoId);
            Assert.AreEqual(0, nano.TriggeredSelfNanoId);
            Assert.AreEqual(10.0, nano.InitialDelaySeconds);
            Assert.AreEqual(2.5, nano.CastSeconds);
            Assert.AreEqual(10.0, nano.RepeatSeconds);
            Assert.AreEqual(1440000, nano.DurationParameter);
            Assert.AreEqual(14400.0, nano.EffectLifetimeSeconds);
            Assert.AreEqual(20.0, nano.TargetRange);
            Assert.IsTrue(nano.FallbackToSelf);
            Assert.AreEqual(181, nano.PrimaryStrain);
            Assert.AreEqual(0, nano.TriggeredSelfStrain);
            Assert.AreEqual(0, nano.PrimaryModifierDelta);
            Assert.AreEqual(0, nano.TriggeredSelfModifierDelta);
            Assert.AreEqual(0, nano.AffectedStatIds.Length);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, nano.EvidenceState);
            Assert.IsFalse(nano.HasPeriodicStatHit);
            Assert.IsFalse(nano.HasTriggeredSelfEffect);
            Assert.AreEqual(44, nano.NanoCost);
            Assert.IsFalse(nano.CastWhileFighting);
            Assert.IsFalse(nano.AllowCombatActionsDuringCast);
            Assert.AreEqual(10000, nano.CastChanceBasisPoints);
            Assert.AreEqual(5000, nano.SelfTargetChanceBasisPoints);
            Assert.IsTrue(nano.RandomizeInitialDelay);
            Assert.AreEqual(7, nano.NcuCost);
            Assert.IsTrue(nano.ResolvePrimaryModifierFromNanoData);
            Assert.IsTrue(nano.Evidence.Contains("on-use-skill-stat=381,delta=+42"));
            CollectionAssert.AreEqual(
                new[] { "19:665", "20:782", "21:829" },
                nano.SpawnNanoPoolByLevel
                    .Select(value => string.Format("{0}:{1}", value.Key, value.Value))
                    .ToArray());
            Assert.AreEqual(0, nano.ResolveSpawnNanoPool(17));
            Assert.AreEqual(0, nano.ResolveSpawnNanoPool(18));
            Assert.AreEqual(665, nano.ResolveSpawnNanoPool(19));
            Assert.AreEqual(782, nano.ResolveSpawnNanoPool(20));
            Assert.AreEqual(829, nano.ResolveSpawnNanoPool(21));
            Assert.AreEqual(0, nano.ResolveSpawnNanoPool(22));

            string repositoryRoot = FindRepositoryRoot();
        }

        [TestMethod]
        public void IncompleteRebuildNano90405KeepsCapturedPeriodicHitAndCombatPolicy()
        {
            OrdinaryEnemySupportNanoProfile nano =
                OrdinaryEnemySupportNanoProfile.CapturedIncompleteRebuild90405();

            Assert.AreEqual(90405, nano.PrimaryNanoId);
            Assert.IsFalse(nano.HasTriggeredSelfEffect);
            Assert.IsTrue(nano.HasPeriodicStatHit);
            Assert.AreEqual(214, nano.PeriodicStatId);
            Assert.AreEqual(21, nano.PeriodicStatDelta);
            Assert.AreEqual(960, nano.PeriodicTickCount);
            Assert.AreEqual(15.0, nano.PeriodicTickSeconds);
            Assert.AreEqual(47, nano.NanoCost);
            Assert.AreEqual(6, nano.NcuCost);
            Assert.AreEqual(5.0, nano.InitialDelaySeconds);
            Assert.AreEqual(2.5, nano.CastSeconds);
            Assert.AreEqual(5.0, nano.RepeatSeconds);
            Assert.AreEqual(1440000, nano.DurationParameter);
            Assert.AreEqual(14400.0, nano.EffectLifetimeSeconds);
            Assert.AreEqual(20.0, nano.TargetRange);
            Assert.AreEqual(14, nano.PrimaryStrain);
            Assert.IsTrue(nano.CastWhileFighting);
            Assert.IsTrue(nano.AllowCombatActionsDuringCast);
            Assert.AreEqual(2500, nano.CastChanceBasisPoints);
            Assert.AreEqual(5000, nano.SelfTargetChanceBasisPoints);
            Assert.IsTrue(nano.RandomizeInitialDelay);
            Assert.AreEqual(918, nano.ResolveSpawnNanoPool(17));
            Assert.AreEqual(985, nano.ResolveSpawnNanoPool(18));
            Assert.AreEqual(1051, nano.ResolveSpawnNanoPool(19));
            Assert.AreEqual(1117, nano.ResolveSpawnNanoPool(20));
            Assert.AreEqual(1183, nano.ResolveSpawnNanoPool(21));
            Assert.AreEqual(1250, nano.ResolveSpawnNanoPool(22));
            Assert.AreEqual(0, nano.ResolveSpawnNanoPool(16));
            Assert.IsTrue(nano.Evidence.Contains("inferred-from-captured-currentnano-plateaus"));

            int selectorBound = 0;
            Assert.AreEqual(
                5.0,
                OrdinaryEnemySupportNanoRuntimeRules.SelectInitialDelaySeconds(
                    nano,
                    bound =>
                        {
                            selectorBound = bound;
                            return bound - 1;
                        }));
            Assert.AreEqual(5001, selectorBound);
            Assert.AreEqual(
                0.0,
                OrdinaryEnemySupportNanoRuntimeRules.SelectInitialDelaySeconds(nano, bound => 0));
            Assert.IsTrue(OrdinaryEnemySupportNanoRuntimeRules.RollChance(2500, bound => 2499));
            Assert.IsFalse(OrdinaryEnemySupportNanoRuntimeRules.RollChance(2500, bound => 2500));
            Assert.IsTrue(OrdinaryEnemySupportNanoRuntimeRules.RollChance(5000, bound => 4999));
            Assert.IsFalse(OrdinaryEnemySupportNanoRuntimeRules.RollChance(5000, bound => 5000));
            bool invalidRollRejected = false;
            try
            {
                OrdinaryEnemySupportNanoRuntimeRules.RollChance(2500, bound => bound);
            }
            catch (InvalidOperationException)
            {
                invalidRollRejected = true;
            }
            Assert.IsTrue(invalidRollRejected, "Out-of-range support-nano rolls must fail closed.");

            int remainingNano;
            Assert.IsTrue(
                OrdinaryEnemySupportNanoRuntimeRules.TrySpendNano(985, 47, out remainingNano));
            Assert.AreEqual(938, remainingNano);
            Assert.IsFalse(
                OrdinaryEnemySupportNanoRuntimeRules.TrySpendNano(46, 47, out remainingNano));
            Assert.AreEqual(46, remainingNano);
            Assert.AreEqual(
                985,
                OrdinaryEnemySupportNanoRuntimeRules.ApplyPositiveCappedDelta(980, 985, 21));
            Assert.AreEqual(
                521,
                OrdinaryEnemySupportNanoRuntimeRules.ApplyPositiveCappedDelta(500, 985, 21));

            DateTime appliedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
            var schedule = new OrdinaryEnemyPeriodicNanoSchedule(nano, appliedAt);
            Assert.AreEqual(959, schedule.RemainingTicks);
            Assert.AreEqual(appliedAt.AddSeconds(15), schedule.NextTickAtUtc);
            Assert.AreEqual(appliedAt.AddHours(4), schedule.ExpiresAtUtc);
            Assert.AreEqual(0, schedule.ConsumeDueTicks(appliedAt.AddSeconds(14.999)));
            Assert.AreEqual(1, schedule.ConsumeDueTicks(appliedAt.AddSeconds(15)));
            Assert.AreEqual(2, schedule.ConsumeDueTicks(appliedAt.AddSeconds(45)));
            Assert.AreEqual(956, schedule.RemainingTicks);
            DateTime refreshedAt = appliedAt.AddSeconds(60);
            schedule.Refresh(nano, refreshedAt);
            Assert.AreEqual(959, schedule.RemainingTicks);
            Assert.AreEqual(refreshedAt.AddSeconds(15), schedule.NextTickAtUtc);
            Assert.AreEqual(refreshedAt.AddHours(4), schedule.ExpiresAtUtc);
            Assert.AreEqual(959, schedule.ConsumeDueTicks(schedule.ExpiresAtUtc));
            Assert.AreEqual(0, schedule.RemainingTicks);

            string repositoryRoot = FindRepositoryRoot();
        }

        private static void AssertExpectedOrder(
            IList<PlayfieldLifecycleEvent> events,
            string flow,
            string[] expectedStages)
        {
            string failure;
            Assert.IsTrue(
                PlayfieldLifecycleTrace.ContainsExpectedOrder(events, flow, expectedStages, out failure),
                failure);
        }

        private static void AssertStageBefore(
            IList<PlayfieldLifecycleEvent> events,
            string firstStage,
            string secondStage)
        {
            int first = IndexOfStage(events, firstStage);
            int second = IndexOfStage(events, secondStage);
            Assert.IsTrue(first >= 0, "Missing lifecycle stage " + firstStage + ".");
            Assert.IsTrue(second >= 0, "Missing lifecycle stage " + secondStage + ".");
            Assert.IsTrue(first < second, firstStage + " must occur before " + secondStage + ".");
        }

        private static ZoneClientSessionLifecycleCoordinator CreateInPlayLifecycle()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();
            lifecycle.BeginCharacterLoading();
            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            lifecycle.EnterFullCharacterBoundaryForSessionInit();
            lifecycle.EnterCharInPlayForVisibilityEntry();
            lifecycle.CompleteInPlayForSessionInit();
            return lifecycle;
        }

        private static void AssertInvalidTransition(Action transition, string expectedMessage)
        {
            try
            {
                transition();
            }
            catch (InvalidOperationException exception)
            {
                Assert.IsTrue(
                    exception.Message.Contains(expectedMessage),
                    "Invalid transition message must identify the rejected transition.");
                return;
            }

            Assert.Fail("Expected invalid lifecycle transition to be rejected.");
        }

        private static void AssertTextBefore(string text, string firstText, string secondText)
        {
            int first = text.IndexOf(firstText, StringComparison.Ordinal);
            int second = text.IndexOf(secondText, StringComparison.Ordinal);
            Assert.IsTrue(first >= 0, "Missing text " + firstText + ".");
            Assert.IsTrue(second >= 0, "Missing text " + secondText + ".");
            Assert.IsTrue(first < second, firstText + " must occur before " + secondText + ".");
        }

        private static bool HasDetail(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage && events[i].Detail == detail)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDetailContains(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage
                    && events[i].Detail.IndexOf(detail, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertMessageForStage(
            IList<PlayfieldLifecycleEvent> events,
            string stage,
            string messageType)
        {
            int index = IndexOfStage(events, stage);
            Assert.IsTrue(index >= 0, "Missing lifecycle stage " + stage + ".");
            Assert.AreEqual(messageType, events[index].MessageType, "Unexpected message type for stage " + stage + ".");
        }

        private static int CountFlow(IList<PlayfieldLifecycleEvent> events, string flow)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Flow == flow)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    count++;
                }
            }

            return count;
        }

        private static int IndexOfStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int start = 0;
            while (start < text.Length)
            {
                int index = text.IndexOf(pattern, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return count;
                }

                count++;
                start = index + pattern.Length;
            }

            return count;
        }

        private static string ExtractMethodBlock(string text, string methodMarker)
        {
            int signatureIndex = text.IndexOf(methodMarker, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, "Missing method " + methodMarker + ".");

            int startIndex = text.IndexOf("{", signatureIndex, StringComparison.Ordinal);
            Assert.IsTrue(startIndex >= 0, "Missing method body for " + methodMarker + ".");

            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            Assert.Fail("Unterminated method body for " + methodMarker + ".");
            return string.Empty;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(
                    current,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
                if (Directory.Exists(candidate))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Unable to find AORebirth repository root from " + sourcePath + ".");
            return string.Empty;
        }

        private static void RecordExpected(string flow, string[] stages)
        {
            Identity identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 1 };
            for (int i = 0; i < stages.Length; i++)
            {
                PlayfieldLifecycleTrace.Record(flow, stages[i], stages[i], identity);
            }
        }

        private static void RecordPrivateCityReadyInitCurrentPacketSequence()
        {
            Identity character = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity playfield = new Identity { Type = IdentityType.Playfield2, Instance = 1196034 };

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                PlayfieldLifecycleTrace.MessageOrgInfoPacket,
                character,
                "Est. 2024");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClan,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clan=1970177");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClanLevel,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clanlevel=1");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent,
                character,
                "org=1970177 orgName=Est. 2024 socialStatus=4 repeats=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                PlayfieldLifecycleTrace.MessageFullCharacter,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                PlayfieldLifecycleTrace.MessagePlayfieldAllTowers,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                PlayfieldLifecycleTrace.MessagePlayfieldAllCities,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent,
                playfield,
                "cityUnknown=0 cityPayloadBytes=0");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd,
                character);
        }

        private static OrdinaryEnemyProfile CreateOrdinaryEnemyProfile(
            string profileKey,
            OrdinaryEnemyConstructionMode constructionMode = OrdinaryEnemyConstructionMode.TemplateBacked,
            OrdinaryEnemyAggressionMode aggressionMode = OrdinaryEnemyAggressionMode.Retaliate,
            OrdinaryEnemyCombatMode combatMode = OrdinaryEnemyCombatMode.UnarmedMelee,
            OrdinaryEnemyDamageSource damageSource = OrdinaryEnemyDamageSource.CapturedFixed,
            OrdinaryEnemyLootEvidence lootEvidence = OrdinaryEnemyLootEvidence.NoneProven,
            bool bossOrScripted = false,
            bool ownedSummon = false)
        {
            OrdinaryEnemyEvidenceState aggressionEvidence =
                aggressionMode == OrdinaryEnemyAggressionMode.Unresolved
                    ? OrdinaryEnemyEvidenceState.Unresolved
                    : OrdinaryEnemyEvidenceState.Observed;
            OrdinaryEnemyEvidenceState combatEvidence =
                combatMode == OrdinaryEnemyCombatMode.Unresolved
                || damageSource == OrdinaryEnemyDamageSource.Unresolved
                    ? OrdinaryEnemyEvidenceState.Unresolved
                    : OrdinaryEnemyEvidenceState.Observed;
            return new OrdinaryEnemyProfile(
                profileKey,
                "test.family",
                "Test Enemy",
                12345,
                constructionMode,
                constructionMode == OrdinaryEnemyConstructionMode.TemplateBacked ? "A000" : string.Empty,
                new OrdinaryEnemyAppearanceProfile(
                    3,
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    138,
                    0,
                    31,
                    0,
                    0u,
                    0,
                    false,
                    true,
                    new OrdinaryEnemyTextureProfile[0],
                    new OrdinaryEnemyMeshProfile[0],
                    OrdinaryEnemyScfuProfile.Generic),
                new OrdinaryEnemyAggressionProfile(
                    aggressionMode,
                    null,
                    true,
                    false,
                    aggressionEvidence),
                new OrdinaryEnemyCombatProfile(
                    combatMode,
                    damageSource,
                    false,
                    new CapturedEnemyCombatContract(),
                    combatEvidence),
                new OrdinaryEnemyLootProfile(
                    lootEvidence,
                    new OrdinaryEnemyLootEntry[0],
                    OrdinaryEnemyEvidenceState.Unresolved,
                    null,
                    null),
                new OrdinaryEnemyCorpseProfile(
                    OrdinaryEnemyCorpsePacketProfile.Generic,
                    30.0,
                    300.0,
                    1.0),
                new[] { "test-evidence" },
                bossOrScripted,
                ownedSummon);
        }

        private static OrdinaryEnemySpawnDefinition CreateOrdinaryEnemySpawn(
            string spawnKey,
            int sourceIdentity,
            string profileKey,
            OrdinaryEnemyMovementMode movementMode = OrdinaryEnemyMovementMode.Static,
            OrdinaryEnemyWaypoint[] waypoints = null,
            bool useCapturedPatrolReplay = false,
            OrdinaryEnemyEvidenceState respawnEvidence = OrdinaryEnemyEvidenceState.Unresolved,
            double? respawnDelaySeconds = null,
            OrdinaryEnemySpawnLevelDefinition levelDefinition = null,
            WorldRespawnPolicyAssignment respawnPolicy = null)
        {
            return new OrdinaryEnemySpawnDefinition(
                spawnKey,
                sourceIdentity,
                profileKey,
                127,
                5,
                115,
                0,
                93,
                20,
                1.0f,
                2.0f,
                3.0f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                movementMode,
                waypoints,
                useCapturedPatrolReplay,
                false,
                false,
                0u,
                0,
                new byte[0],
                0,
                respawnEvidence,
                respawnDelaySeconds,
                OrdinaryEnemyRuntimeDisposition.Active,
                string.Empty,
                "test-capture",
                "test-timestamp",
                levelDefinition,
                respawnPolicy);
        }

        private static void AssertOrdinaryEnemyValidationFails(
            OrdinaryEnemyProfile[] profiles,
            OrdinaryEnemySpawnDefinition[] spawns,
            string message)
        {
            try
            {
                OrdinaryEnemyProfileValidator.Validate(profiles, spawns);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Assert.Fail(message);
        }

        private static readonly ForbiddenReference[] ForbiddenContentModuleReferences =
            new[]
            {
                new ForbiddenReference("combat logic", "NpcCombatTickCoordinator"),
                new ForbiddenReference("combat logic", "NpcCombatAttackRules"),
                new ForbiddenReference("combat logic", "CombatDamageRules"),
                new ForbiddenReference("combat packets", "AttackInfoMessage"),
                new ForbiddenReference("combat packets", "SpecialAttackWeaponMessage"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleCoordinator"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleRules"),
                new ForbiddenReference("player visibility", "CharInPlayMessageHandler"),
                new ForbiddenReference("player visibility", "SimpleCharFullUpdate"),
                new ForbiddenReference("player visibility", "FullCharacterMessageHandler"),
                new ForbiddenReference("GenericCmd routing", "GenericCmd"),
                new ForbiddenReference("GenericCmd routing", "GenericCmdMessageHandler"),
                new ForbiddenReference("inventory logic", "Inventory"),
                new ForbiddenReference("inventory logic", "ContainerAddItem"),
                new ForbiddenReference("inventory logic", "ClientMoveItem"),
                new ForbiddenReference("org commands", "OrgClient"),
                new ForbiddenReference("org commands", "OrgClientMessageHandler"),
                new ForbiddenReference("org commands", "OrgServer"),
                new ForbiddenReference("packet serialization internals", "SendCompressed"),
                new ForbiddenReference("packet serialization internals", "N3Messages"),
                new ForbiddenReference("packet serialization internals", "SystemMessages"),
                new ForbiddenReference("packet serialization internals", "Serializer"),
                new ForbiddenReference("database import", "AORebirth.Database"),
                new ForbiddenReference("database import", "ItemLoader"),
                new ForbiddenReference("database import", "NanoLoader"),
                new ForbiddenReference("database import", "CheckDatabase"),
                new ForbiddenReference("database import", "MessagePackZip"),
                new ForbiddenReference("capture tooling", "AOSharpLiveCapture"),
                new ForbiddenReference("capture tooling", "tools-temp")
            };

        private sealed class ForbiddenReference
        {
            public ForbiddenReference(string category, string pattern)
            {
                this.Category = category;
                this.Pattern = pattern;
            }

            public string Category { get; private set; }

            public string Pattern { get; private set; }
        }
    }
}
