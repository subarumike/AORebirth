// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                    "Parameter2=500");
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
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2, "Parameter2=500"),
                    "Robot death trace must preserve captured CharacterAction Death Parameter2=500.");
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
        public void CombatStartPacketsUseLiveCompatibleBaseFlagAndDoNotEmitDefAggTutorialText()
        {
            string repositoryRoot = FindRepositoryRoot();
            string attackHandlerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string npcCombatTickText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));

            Assert.IsTrue(
                attackHandlerText.Contains("x.Unknown = 0;"),
                "Player attack-start echo must use the live-captured AttackMessage base Unknown=0 shape.");
            Assert.IsTrue(
                clientConnectedText.Contains("SetStat(client, StatIds.state, 0);"),
                "Login/actionable player state must keep the live-captured State=0 baseline before combat.");
            Assert.IsFalse(
                clientConnectedText.Contains("SetStat(client, StatIds.state, 1000001);"),
                "Login/actionable player state must not prime the client with the invalid State=1000001 combat/tutorial condition.");
            Assert.IsTrue(
                playfieldText.Contains("new AttackInfoMessage")
                && playfieldText.Contains("Unknown = 0,")
                && npcCombatTickText.Contains("new AttackInfoMessage")
                && npcCombatTickText.Contains("Unknown = 0,"),
                "Player and NPC AttackInfo packets must use the live-captured base Unknown=0 shape.");
            Assert.IsTrue(
                npcCombatTickText.Contains("NpcCombatAttackRules.DefaultCombatTickSeconds")
                && npcCombatTickText.Contains("now + TimeSpan.FromSeconds(initialDelaySeconds)")
                && npcCombatTickText.Contains(
                    "capturedContract.AttackStartDelaySeconds + capturedContract.FirstHitDelaySeconds")
                && !npcCombatTickText.Contains("this.nextCombatTicks.Remove(attacker.Identity.Instance);"),
                "NPC combat start must not emit immediate first-hit AttackInfo before the live-compatible combat-start window.");
            Assert.IsFalse(
                attackHandlerText.Contains("Use the Def-Agg slider in the Stats view to change between defensive and aggressive.")
                || playfieldText.Contains("Use the Def-Agg slider in the Stats view to change between defensive and aggressive.")
                || npcCombatTickText.Contains("Use the Def-Agg slider in the Stats view to change between defensive and aggressive.")
                || clientConnectedText.Contains("Use the Def-Agg slider in the Stats view to change between defensive and aggressive."),
                "Combat-start paths must not server-emit the client Def-Agg tutorial text.");
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
                500,
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2,
                "Cleaning robot CharacterAction Death Parameter2 must stay capture-backed.");
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
            Assert.AreEqual(9, NpcCombatAttackRules.CapturedSubwayThiefDamage);
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
        public void SubwayThiefCombatContractPreservesLiveEnvelopeMovementAndDeathOrder()
        {
            string repositoryRoot = FindRepositoryRoot();
            string contractText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string controllerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Controllers\NPCController.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            Assert.IsTrue(
                contractText.Contains("case 26092:")
                && contractText.Contains("HasCapturedAttackStartContext = true")
                && contractText.Contains("HasCapturedEquippedAttackInfo = true")
                && contractText.Contains("HasCapturedCombatStopSequence = true")
                && contractText.Contains("SendStopFightOnDeath = sendStopFightOnDeath")
                && contractText.Contains("CapturedSubwayThiefMovementTransitionDelaySeconds")
                && contractText.Contains("CapturedSubwayThiefAttackInfoAmmoCount")
                && contractText.Contains("CapturedSubwayThiefAttackInfoUnknown"),
                "MonsterData 26092 must retain the live-derived Thief attack contract.");

            Assert.IsTrue(
                coordinatorText.Contains("pendingCapturedAttackStarts")
                && coordinatorText.Contains("pendingCapturedMovementTransitions")
                && coordinatorText.Contains(
                    "capturedContract.AttackStartDelaySeconds + capturedContract.FirstHitDelaySeconds")
                && coordinatorText.Contains("+ capturedContract.MovementTransitionDelaySeconds")
                && coordinatorText.Contains("capturedContract.HasCapturedAttackStartContext")
                && coordinatorText.Contains("capturedContract.HasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("? capturedContract.AttackInfoAmmoCount")
                && coordinatorText.Contains("? capturedContract.AttackInfoUnknown")
                && coordinatorText.Contains(": 40,")
                && coordinatorText.Contains(": 4,"),
                "Thief timing and AttackInfo overrides must stay contract-gated while legacy equipped NPC fields remain unchanged.");

            string capturedStopBlock = ExtractMethodBlock(
                controllerText,
                "public void StopFollowForCapturedCombatRange(");
            AssertTextBefore(capturedStopBlock, "new FollowTargetInfo", "new StopMovingCmdMessage");
            AssertTextBefore(capturedStopBlock, "new StopMovingCmdMessage", "new SetPosMessage");
            AssertTextBefore(capturedStopBlock, "new SetPosMessage", "new FollowCoordinateInfo");

            AssertTextBefore(
                ExtractMethodBlock(npcRuntimeText, "internal void BeginNpcDeath("),
                "this.playfield.StopDyingNpcCombatState(target);",
                "this.playfield.SendNpcDeathAnimation(target);");
            Assert.IsTrue(
                ExtractMethodBlock(playfieldText, "internal void StopDyingNpcCombatState(")
                    .Contains("capturedContract.SendStopFightOnDeath"),
                "The live-captured Thief StopFight must be emitted before its Death action.");
        }

        [TestMethod]
        public void SubwayFilthFleaCombatUsesCapturedPoisonAndMeleeAttackContext()
        {
            Assert.AreEqual(17657, NpcCombatAttackRules.CapturedSubwayFilthFleaMonsterData);
            Assert.AreEqual(15, NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonDamage);
            Assert.AreEqual(3, NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeDamage);
            Assert.AreEqual(1, NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonWeaponSlot);
            Assert.AreEqual(0, NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeWeaponSlot);
            Assert.AreEqual(0x45504148, NpcCombatAttackRules.CapturedSubwayFilthFleaStickToHeadTag);
            Assert.AreEqual(0x415A5553, NpcCombatAttackRules.CapturedSubwayFilthFleaArmsTag);
            Assert.AreEqual(3650, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaInitialAttackSeconds * 1000));
            Assert.AreEqual(1580, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaPoisonRechargeSeconds * 1000));
            Assert.AreEqual(2800, (int)(NpcCombatAttackRules.CapturedSubwayFilthFleaMeleeRechargeSeconds * 1000));

            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            Assert.IsTrue(
                coordinatorText.Contains("this.AnnounceCapturedEnemyAttackStartContext(attacker, capturedContract);")
                && coordinatorText.Contains("private void AnnounceCapturedEnemyAttackStartContext(")
                && coordinatorText.Contains("CapturedEnemyCombatContract capturedContract)")
                && coordinatorText.Contains("Specials = new SpecialAttack[0]")
                && coordinatorText.Contains("Unknown = 0,")
                && coordinatorText.Contains("pendingCapturedMovementTransitions")
                && coordinatorText.Contains("capturedContract.MovementTransitionDelaySeconds")
                && coordinatorText.Contains("hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("AttackInfoAmmoCount = hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("AttackInfoUnk1 = hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("DamageBonus = 0,"),
                "Thief must use its captured attack context, delayed movement transition, and fixed normal-hit envelope without reusing weapon max-damage as flat add damage.");
            int contextIndex = coordinatorText.IndexOf(
                "this.AnnounceCapturedSubwayFilthFleaAttackStartContext(attacker);",
                StringComparison.Ordinal);
            int poisonContextIndex = coordinatorText.IndexOf(
                "CreateCapturedSubwayFilthFleaSpecialAttacks()",
                StringComparison.Ordinal);
            int attackInfoIndex = coordinatorText.IndexOf(
                "this.AnnounceCombatDamage(",
                StringComparison.Ordinal);

            Assert.IsTrue(contextIndex >= 0, "Flea combat start must announce captured attack context.");
            Assert.IsTrue(poisonContextIndex >= 0, "Flea combat must expose captured natural attack templates.");
            Assert.IsTrue(attackInfoIndex > contextIndex, "Flea context must be established before AttackInfo damage.");
        }

        [TestMethod]
        public void PlayerCombatRuntimeServiceFinalBoundaryOwnsLifecycleOrchestrationOnly()
        {
            string repositoryRoot = FindRepositoryRoot();
            string attackHandlerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string playerCombatText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayerCombatRuntimeService.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
            string checkpointText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"docs\generated\player_combat_lifecycle_ownership_checkpoint_20260705.md"));

            Assert.IsTrue(
                playerCombatText.Contains("internal sealed class PlayerCombatRuntimeService")
                && playerCombatText.Contains("internal void StartAttack(")
                && playerCombatText.Contains("internal void CancelAttack(")
                && playerCombatText.Contains("internal void ResetCombatTick(")
                && playerCombatText.Contains("internal void ProcessCombatTick(")
                && playerCombatText.Contains("internal void ClearFightingTarget(")
                && playerCombatText.Contains("internal void ClearInvalidCombatTarget(")
                && playerCombatText.Contains("internal void CleanupDeathCombat(")
                && playerCombatText.Contains("internal void BeginDeath("),
                "PlayerCombatRuntimeService must expose named player combat lifecycle seams.");
            Assert.IsTrue(
                playerCombatText.Contains("resetCombatTick(attacker);")
                && playerCombatText.Contains("beginDeath(target);"),
                "PlayerCombatRuntimeService must leave reset and death seams as pass-through orchestration.");
            Assert.IsTrue(
                playerCombatText.Contains("character.SetTarget(target);")
                && playerCombatText.Contains("character.SetFightingTarget(target);")
                && playerCombatText.Contains("resetCombatTick(character.Identity);"),
                "PlayerCombatRuntimeService must own player attack-start state mutation and tick reset orchestration.");
            Assert.IsTrue(
                playerCombatText.Contains("internal void CancelAttack(ICharacter character, Action<Identity> resetCombatTick)")
                && playerCombatText.Contains("character.SetFightingTarget(Identity.None);")
                && CountOccurrences(playerCombatText, "resetCombatTick(character.Identity);") == 2,
                "PlayerCombatRuntimeService must own player attack cancel state clear and tick reset orchestration.");
            Assert.IsTrue(
                playerCombatText.Contains("internal void ClearFightingTarget(ICharacter character, Action<Identity> clearCombatTracking)")
                && playerCombatText.Contains("clearCombatTracking(character.Identity);"),
                "PlayerCombatRuntimeService must own player fighting-target stop/clear orchestration.");
            Assert.IsTrue(
                playerCombatText.Contains("Func<Identity, ICharacter> findTarget")
                && playerCombatText.Contains("Func<ICharacter, bool> isValidTarget")
                && playerCombatText.Contains("Action<ICharacter, ICharacter> logInvalidTarget")
                && playerCombatText.Contains("Action<ICharacter, ICharacter> processValidatedCombatTick")
                && playerCombatText.Contains("if (attacker.FightingTarget.Instance == 0)")
                && playerCombatText.Contains("clearCombatTracking(attacker.Identity);")
                && playerCombatText.Contains("ICharacter target = findTarget(attacker.FightingTarget);")
                && playerCombatText.Contains("if (!isValidTarget(target))")
                && playerCombatText.Contains(
                    "this.ClearInvalidCombatTarget(attacker, target, logInvalidTarget, clearCombatTracking);")
                && playerCombatText.Contains("processValidatedCombatTick(attacker, target);"),
                "PlayerCombatRuntimeService must own player combat tick target/clear orchestration.");
            string invalidTargetClear = ExtractMethodBlock(playerCombatText, "internal void ClearInvalidCombatTarget");
            Assert.IsTrue(
                invalidTargetClear.Contains("Require(logInvalidTarget, \"logInvalidTarget\");")
                && invalidTargetClear.Contains("Require(clearCombatTracking, \"clearCombatTracking\");")
                && invalidTargetClear.Contains("logInvalidTarget(attacker, target);")
                && invalidTargetClear.Contains("this.ClearFightingTarget(attacker, clearCombatTracking);"),
                "PlayerCombatRuntimeService must own invalid player combat target cleanup.");
            AssertTextBefore(
                invalidTargetClear,
                "logInvalidTarget(attacker, target);",
                "this.ClearFightingTarget(attacker, clearCombatTracking);");
            string deathCombatCleanup = ExtractMethodBlock(playerCombatText, "internal void CleanupDeathCombat");
            Assert.IsTrue(
                deathCombatCleanup.Contains("Require(clearCombatTracking, \"clearCombatTracking\");")
                && deathCombatCleanup.Contains("Require(stopFightingDeadTarget, \"stopFightingDeadTarget\");")
                && deathCombatCleanup.Contains("Require(sendCombatStop, \"sendCombatStop\");")
                && deathCombatCleanup.Contains("target.SetTarget(Identity.None);")
                && deathCombatCleanup.Contains("this.ClearFightingTarget(target, clearCombatTracking);")
                && deathCombatCleanup.Contains("stopFightingDeadTarget(target.Identity);")
                && deathCombatCleanup.Contains("sendCombatStop(target);"),
                "PlayerCombatRuntimeService must own player death combat cleanup orchestration.");
            AssertTextBefore(
                deathCombatCleanup,
                "target.SetTarget(Identity.None);",
                "this.ClearFightingTarget(target, clearCombatTracking);");
            AssertTextBefore(
                deathCombatCleanup,
                "this.ClearFightingTarget(target, clearCombatTracking);",
                "stopFightingDeadTarget(target.Identity);");
            AssertTextBefore(
                deathCombatCleanup,
                "stopFightingDeadTarget(target.Identity);",
                "sendCombatStop(target);");
            Assert.IsFalse(
                playerCombatText.Contains("CombatDamageRules")
                || playerCombatText.Contains("Announce(")
                || playerCombatText.Contains("AttackInfo")
                || playerCombatText.Contains("StopFightMessage")
                || playerCombatText.Contains("NPCController")
                || playerCombatText.Contains("NPCRuntimeService")
                || playerCombatText.Contains("Inventory")
                || playerCombatText.Contains("Corpse"),
                "PlayerCombatRuntimeService must not own algorithms, packets, NPC runtime, inventory, or corpse behavior.");

            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayerCombatRuntimeService playerCombat;")
                && runtimeSystemsText.Contains("this.playerCombat = new PlayerCombatRuntimeService();")
                && runtimeSystemsText.Contains("this.playerCombat.StartAttack(character, target, resetCombatTick);")
                && runtimeSystemsText.Contains("this.playerCombat.CancelAttack(character, resetCombatTick);")
                && runtimeSystemsText.Contains("this.playerCombat.ResetCombatTick(attacker, resetCombatTick);")
                && runtimeSystemsText.Contains("this.playerCombat.ProcessCombatTick(")
                && runtimeSystemsText.Contains("processValidatedCombatTick);")
                && runtimeSystemsText.Contains("this.playerCombat.ClearFightingTarget(character, clearCombatTracking);")
                && runtimeSystemsText.Contains("this.playerCombat.CleanupDeathCombat(")
                && runtimeSystemsText.Contains("this.playerCombat.BeginDeath(target, beginDeath);"),
                "PlayfieldRuntimeSystems must own and expose the player combat runtime facade.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayerCombatRuntimeService.cs"),
                "ZoneEngine project must compile PlayerCombatRuntimeService.");
            Assert.IsFalse(
                npcRuntimeText.Contains("PlayerCombatRuntimeService")
                || npcRuntimeText.Contains("StartPlayerAttack")
                || npcRuntimeText.Contains("BeginPlayerDeath"),
                "NPCRuntimeService must remain NPC-only.");

            Assert.IsTrue(
                attackHandlerText.Contains("this.StartPlayerAttack(character, message.Target);")
                && attackHandlerText.Contains("this.CancelPlayerAttack(character);")
                && attackHandlerText.Contains("playfield.StartPlayerAttack(character, target);")
                && attackHandlerText.Contains("playfield.CancelPlayerAttack(character);")
                && attackHandlerText.Contains("this.SendCombatStartSpecialAttackWeapon(character);")
                && attackHandlerText.Contains("this.SendAttackState(character, message.Target, message.Action);")
                && attackHandlerText.Contains("x.Unknown = 0;"),
                "AttackMessageHandler must route player attack start/cancel through the player combat boundary while keeping the live-compatible attack echo shape.");
            int combatStartWeaponIndex = attackHandlerText.IndexOf(
                "this.SendCombatStartSpecialAttackWeapon(character);",
                StringComparison.Ordinal);
            int attackEchoIndex = attackHandlerText.IndexOf(
                "this.SendAttackState(character, message.Target, message.Action);",
                StringComparison.Ordinal);
            Assert.IsTrue(
                combatStartWeaponIndex >= 0
                && attackEchoIndex >= 0
                && combatStartWeaponIndex < attackEchoIndex
                && attackHandlerText.Contains("CombatStartSpecialAttackUnknown1 = 13")
                && attackHandlerText.Contains("CombatStartSpecialAttackUnknown2 = 25")
                && attackHandlerText.Contains("CombatStartSpecialAttackUnknown3 = 13")
                && attackHandlerText.Contains("CombatStartSpecialAttackUnknown4 = 33")
                && attackHandlerText.Contains("CombatStartSpecialAttackUnknown5 = 100")
                && attackHandlerText.Contains("Unknown4 = \"MAAT\"")
                && attackHandlerText.Contains("Unknown4 = \"DIIT\"")
                && attackHandlerText.Contains("Unknown4 = \"BRAW\""),
                "AttackMessageHandler must send the live-captured player SpecialAttackWeapon state before the attack echo on valid combat start.");
            Assert.IsFalse(
                attackHandlerText.Contains("Use the Def-Agg slider in the Stats view to change between defensive and aggressive."),
                "AttackMessageHandler must not server-emit the client Def-Agg tutorial text on combat start.");
            Assert.IsTrue(
                attackHandlerText.Contains("target == null")
                && attackHandlerText.Contains("ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target)")
                && attackHandlerText.Contains("this.SendAttackState(character, Identity.None, 0);"),
                "AttackMessageHandler must preserve invalid/suppressed attack cancellation packet echo.");
            Assert.IsTrue(
                attackHandlerText.Contains("playfield.AcquireNpcAggro(character, target);"),
                "AttackMessageHandler must keep NPC aggro delegated through Playfield after player attack start.");
            Assert.IsFalse(
                attackHandlerText.Contains("AnnounceCombatDamage")
                || attackHandlerText.Contains("HandleCombatKillingHit")
                || attackHandlerText.Contains("KillPlayerTarget"),
                "AttackMessageHandler must not own combat damage, killing-hit, or death lifecycle behavior.");

            string combatTick = ExtractMethodBlock(playfieldText, "private void DoCombatTick");
            Assert.IsTrue(
                combatTick.Contains("if (attacker.Controller is NPCController)")
                && combatTick.Contains("this.runtimeSystems.ProcessNpcCombatTick(attacker);")
                && combatTick.Contains("this.runtimeSystems.ProcessPlayerCombatTick(")
                && combatTick.Contains("this.FindPlayerCombatTarget")
                && combatTick.Contains("this.IsValidPlayerCombatTarget")
                && combatTick.Contains("this.LogInvalidPlayerCombatTickTarget")
                && combatTick.Contains("this.ProcessValidatedPlayerCombatTick"),
                "Playfield DoCombatTick must delegate player combat tick entry through PlayfieldRuntimeSystems.");

            string playerCombatTick = ExtractMethodBlock(playfieldText, "private void ProcessValidatedPlayerCombatTick");
            Assert.IsTrue(
                playfieldText.Contains("private ICharacter FindPlayerCombatTarget(Identity target)")
                && playfieldText.Contains("private bool IsValidPlayerCombatTarget(ICharacter target)")
                && playfieldText.Contains("private void LogInvalidPlayerCombatTickTarget(ICharacter attacker, ICharacter target)")
                && playerCombatTick.Contains("CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);")
                && playerCombatTick.Contains("this.HandleCombatKillingHit(attacker, target);"),
                "Playfield must keep target lookup helpers and validated tick algorithms behind service callbacks.");
            Assert.IsTrue(
                playfieldText.Contains("public void StartPlayerAttack(ICharacter character, Identity target)")
                && playfieldText.Contains("this.runtimeSystems.StartPlayerAttack(character, target, this.ResetCombatTick);")
                && !playfieldText.Contains("private void ApplyPlayerAttackStart(ICharacter character, Identity target)"),
                "Playfield must route player attack-start orchestration through the player combat facade.");
            Assert.IsTrue(
                playfieldText.Contains("public void CancelPlayerAttack(ICharacter character)")
                && playfieldText.Contains("this.runtimeSystems.CancelPlayerAttack(character, this.ResetCombatTick);")
                && !playfieldText.Contains("private void ApplyPlayerAttackCancel(ICharacter character)")
                && playfieldText.Contains("private void ResetPlayerCombatTick(Identity attacker)")
                && playfieldText.Contains("this.runtimeSystems.ResetPlayerCombatTick(attacker, this.ResetPlayerCombatTick);"),
                "Playfield must route player attack cancel orchestration through the player combat facade.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.BeginPlayerDeath(target, this.KillPlayerTarget);")
                && playfieldText.Contains("private void KillPlayerTarget(ICharacter target)")
                && playfieldText.Contains("this.MarkPlayerDead(target);")
                && playfieldText.Contains("this.runtimeSystems.RunPlayerDeathStatUpdateSequence(")
                && playfieldText.Contains("this.runtimeSystems.CleanupPlayerDeathCombat(")
                && playfieldText.Contains("this.SendPlayerDeathAnimation"),
                "Playfield must keep player death behavior while routing death stat-update ordering through the facade.");
            string playerDeath = ExtractMethodBlock(playfieldText, "private void KillPlayerTarget");
            AssertTextBefore(
                playerDeath,
                "this.MarkPlayerDead(target);",
                "this.runtimeSystems.RunPlayerDeathStatUpdateSequence(");
            AssertTextBefore(
                playerDeath,
                "SendChangedStats,",
                "this.runtimeSystems.CleanupPlayerDeathCombat(");
            AssertTextBefore(
                playerDeath,
                "this.runtimeSystems.CleanupPlayerDeathCombat(",
                "this.SendPlayerDeathAnimation");
            string playerRespawn = ExtractMethodBlock(playfieldText, "public void RespawnPlayer");
            Assert.IsTrue(
                playerRespawn.Contains("this.runtimeSystems.ProcessPlayerRespawn(")
                && playerRespawn.Contains("this.ClearCombatTracking")
                && playerRespawn.Contains("this.StopFightingDeadTarget")
                && playerRespawn.Contains("this.SendCombatStopMessage")
                && runtimeSystemsText.Contains("x => this.CleanupPlayerDeathCombat(x, clearCombatTracking, stopFightingDeadTarget, sendCombatStop)")
                && !playerRespawn.Contains("character.SetTarget(Identity.None);")
                && !playerRespawn.Contains("character.SetFightingTarget(Identity.None);")
                && !playerRespawn.Contains("this.ClearCombatTracking(character.Identity);")
                && !playerRespawn.Contains("this.StopFightingDeadTarget(character.Identity);")
                && !playerRespawn.Contains("this.SendCombatStopMessage(character);"),
                "Player death respawn combat cleanup must route through the player combat facade.");
            Assert.IsTrue(
                playfieldText.Contains("internal void StopFightingDeadTarget(Identity deadTarget)")
                && playfieldText.Contains("if (character.Controller is NPCController)")
                && playfieldText.Contains("this.ClearNpcFightingTarget(character);")
                && playfieldText.Contains("this.runtimeSystems.ClearPlayerFightingTarget(character, this.ClearCombatTracking);")
                && playfieldText.Contains("this.SendCombatStopMessage(character);"),
                "Playfield must keep mixed player/NPC StopFight packet emission while routing player target clear through the facade.");
            Assert.IsTrue(
                playfieldText.Contains("internal void ClearCombatTracking(Identity identity)")
                && playfieldText.Contains("this.nextCombatTicks.Remove(identity.Instance);")
                && playfieldText.Contains("this.runtimeSystems.ClearNpcCombatTracking(identity);"),
                "Playfield currently owns shared combat tick tracking while delegating NPC tracking cleanup.");

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
        public void CapturedAreteRobotContentProviderPreservesSpawnDefinitions()
        {
            var provider = new CapturedAreteRobotContentProvider();
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();

            Assert.AreEqual(7, spawns.Length);
            Assert.AreEqual("Malfunctioning Cleaning Robot", CapturedAreteRobotContentProvider.RobotName);
            Assert.AreEqual(297023, CapturedAreteRobotContentProvider.MonsterData);
            Assert.AreEqual(0x79225E7C, spawns[0].SourceInstance);
            Assert.AreEqual(12, spawns[0].Health);
            Assert.AreEqual(1, spawns[0].Level);
            Assert.AreEqual(6, spawns[0].RunSpeed);
            Assert.AreEqual(3617.86938f, spawns[0].X);
            Assert.AreEqual(51.7449989f, spawns[0].Y);
            Assert.AreEqual(784.657471f, spawns[0].Z);
            Assert.AreEqual(3622.77563f, spawns[0].PatrolX);
            Assert.AreEqual(52.5f, spawns[0].PatrolY);
            Assert.AreEqual(798.800964f, spawns[0].PatrolZ);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesPatrolReplayPathAndMissingFileFallback()
        {
            Assert.AreEqual(
                @"Content\Captured\Arete\cleaning_robot_patrol_replay.csv",
                CapturedAreteRobotContentProvider.PatrolReplayRelativePath);
            Assert.AreEqual(
                @"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260629-193121\movement-packets.csv",
                CapturedAreteRobotContentProvider.EvidenceCapturePatrolReplayRelativePath);

            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });

            Assert.AreEqual(string.Empty, provider.FindPatrolReplayPath());
            Assert.AreEqual(0, provider.GetPatrolReplaySegments(0x79225E7C).Length);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderLoadsCommittedPatrolReplayData()
        {
            var provider = new CapturedAreteRobotContentProvider();
            string replayPath = provider.FindPatrolReplayPath();

            Assert.IsTrue(File.Exists(replayPath));
            Assert.IsTrue(
                replayPath.IndexOf("tools-temp", StringComparison.OrdinalIgnoreCase) < 0,
                "Runtime replay data must load from committed content, not tools-temp captures.");

            Assert.AreEqual(35, provider.GetPatrolReplaySegments(0x79225E7D).Length);
            Assert.AreEqual(40, provider.GetPatrolReplaySegments(0x79225E7C).Length);
            Assert.AreEqual(38, provider.GetPatrolReplaySegments(0x79225E77).Length);
            Assert.AreEqual(31, provider.GetPatrolReplaySegments(0x79225E7A).Length);
            Assert.AreEqual(39, provider.GetPatrolReplaySegments(0x79225E78).Length);
            Assert.AreEqual(29, provider.GetPatrolReplaySegments(0x79225E79).Length);
            Assert.AreEqual(18, provider.GetPatrolReplaySegments(0x79225E76).Length);

            CapturedAreteRobotPatrolReplaySegment first =
                provider.GetPatrolReplaySegments(0x79225E7D)[0];
            Assert.AreEqual(3605.55493f, first.StartX);
            Assert.AreEqual(51.7449989f, first.StartY);
            Assert.AreEqual(773.164246f, first.StartZ);
            Assert.AreEqual(3602.2915f, first.EndX);
            Assert.AreEqual(52.5f, first.EndY);
            Assert.AreEqual(787.929504f, first.EndZ);

            CapturedAreteRobotPatrolReplaySegment[] lastRoute =
                provider.GetPatrolReplaySegments(0x79225E7C);
            CapturedAreteRobotPatrolReplaySegment last = lastRoute[lastRoute.Length - 1];
            Assert.AreEqual(3612.93481f, last.StartX);
            Assert.AreEqual(52.1349983f, last.StartY);
            Assert.AreEqual(787.84082f, last.StartZ);
            Assert.AreEqual(3611.29053f, last.EndX);
            Assert.AreEqual(52.5f, last.EndY);
            Assert.AreEqual(778.074585f, last.EndZ);
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
                Assert.AreEqual(40, assigned.Length);

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
                        "count=7 monsterData=297023"));
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                        spawnCreatedDetail));
                Assert.IsTrue(
                    spawnCreatedDetail.IndexOf(
                        "sourceInstance=79225E7C monsterData=297023 hp=12 level=1 runSpeed=6",
                        StringComparison.Ordinal) >= 0);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                        "sourceInstance=79225E7C segments=40"));
            }
        }

        [TestMethod]
        public void PlayfieldContentModulesDoNotOwnRuntimeSystems()
        {
            string contentDirectory = Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
            string[] sourceFiles = Directory.GetFiles(contentDirectory, "*.cs", SearchOption.AllDirectories);

            Assert.IsTrue(sourceFiles.Length >= 4, "Expected current Playfield content-module files to be scanned.");

            foreach (string sourceFile in sourceFiles)
            {
                string text = File.ReadAllText(sourceFile);
                Assert.IsTrue(
                    text.Contains("namespace ZoneEngine.Core.Playfields.Content"),
                    "Content guardrail only applies to the content namespace: " + sourceFile);

                for (int i = 0; i < ForbiddenContentModuleReferences.Length; i++)
                {
                    ForbiddenReference forbidden = ForbiddenContentModuleReferences[i];
                    Assert.IsFalse(
                        text.IndexOf(forbidden.Pattern, StringComparison.Ordinal) >= 0,
                        string.Format(
                            "Playfield content modules must define content only; forbidden {0} reference '{1}' found in {2}.",
                            forbidden.Category,
                            forbidden.Pattern,
                            sourceFile));
                }
            }
        }

        [TestMethod]
        public void PrivateCityContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PrivateCityContentModule.cs");
            string runtimeSystemsPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string runtimeSystemsText = File.ReadAllText(runtimeSystemsPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class PrivateCityContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                runtimeSystemsText.Contains("new PrivateCityContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the private-city content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\PrivateCityContentModule.cs"),
                "ZoneEngine project must compile the private-city content module skeleton.");
        }

        [TestMethod]
        public void MontroyalContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MontroyalContentModule.cs");
            string runtimeSystemsPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string runtimeSystemsText = File.ReadAllText(runtimeSystemsPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class MontroyalContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("private const int MontroyalPlayfieldInstance = 655"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                runtimeSystemsText.Contains("new MontroyalContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the Montroyal content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\MontroyalContentModule.cs"),
                "ZoneEngine project must compile the Montroyal content module skeleton.");
        }

        [TestMethod]
        public void SubwayContentModuleRegistersCapturedNpcSpawnsWithoutOwningRuntimeSystems()
        {
            string repositoryRoot = FindRepositoryRoot();
            string moduleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\SubwayContentModule.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwaySpawnOrchestrator.cs"));
            string scfuPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs"));
            string scfuMessageText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\SimpleCharFullUpdateMessage.cs"));
            string scfuSerializerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\Serializers\Custom\SimpleCharFullUpdateSerializer.cs"));

            Assert.IsTrue(moduleText.Contains("public sealed class SubwayContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("private const int SubwayPlayfieldInstance = 127"));
            Assert.IsTrue(
                providerText.Contains("public const int SubwayPlayfieldInstance = 127"),
                "Captured Subway NPC spawns must bind to the live/client-visible PF127 Subway proxy resource.");
            Assert.IsTrue(moduleText.Contains("registration.RegisterCapturedNpcSpawns();"));
            Assert.IsTrue(
                moduleText.Contains("return false;"),
                "Subway content module must not suppress unrelated DB mob spawns in this first slice.");
            Assert.IsFalse(
                moduleText.Contains("CapturedSubwaySpawnOrchestrator")
                || moduleText.Contains("NonPlayerCharacterHandler"),
                "Subway content module must stay content-only and not own NPC runtime orchestration.");

            Assert.IsTrue(
                runtimeSystemsText.Contains("new SubwayContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the Subway content module.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\SubwayContentModule.cs")
                && projectText.Contains(@"Core\Playfields\CapturedSubwayContentProvider.cs")
                && projectText.Contains(@"Core\Playfields\CapturedSubwaySpawnOrchestrator.cs"),
                "ZoneEngine project must compile the Subway content files.");

            Assert.IsTrue(
                npcRuntimeText.Contains("new CapturedSubwayContentProvider()")
                && npcRuntimeText.Contains("new CapturedSubwaySpawnOrchestrator(")
                && npcRuntimeText.Contains("this.capturedSubwaySpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);"),
                "NPCRuntimeService must own the Subway captured spawn path.");

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
                95,
                CountOccurrences(spawnDefinitionsText, "CapturedSurveySpawn("),
                "CapturedSubwayContentProvider must preserve the 95 already-supported ordinary Subway spawns.");
            Assert.IsFalse(
                providerText.Contains("122002"),
                "CapturedSubwayContentProvider must bind content to resource/playfield 127, not capture object Playfield2:122002.");
            Assert.IsTrue(
                orchestratorText.Contains("SetMobStat(mobCharacter, StatIds.monsterdata, spawn.MonsterData);")
                && orchestratorText.Contains("var fullUpdate = SimpleCharFullUpdate.ConstructMessage(mobCharacter);")
                && orchestratorText.Contains("playfield.Announce(fullUpdate);"),
                "Captured Subway spawns must remain visible, attackable NPCs using existing runtime/corpse paths.");
            Assert.IsFalse(
                orchestratorText.Contains("SetMobStat(mobCharacter, StatIds.catmesh, spawn.MonsterData);")
                || orchestratorText.Contains("SetMobStat(mobCharacter, StatIds.displaycatmesh, spawn.MonsterData);"),
                "Captured Subway spawns must not overwrite template mesh stats with MonsterData ids.");
            Assert.IsTrue(
                orchestratorText.Contains("ClearTemplateHeadMesh(mobCharacter);")
                && orchestratorText.Contains("mobCharacter.MeshLayer.RemoveMesh(0, 0, 0, 4);"),
                "Captured Subway no-headmesh mobs must clear template zero mesh layers to preserve live Meshes=count=0 SCFU shape.");
            Assert.IsTrue(
                scfuMessageText.Contains("public byte[] ExtendedTextureOverrideData { get; set; }")
                && scfuSerializerText.Contains("SimpleCharFullUpdateFlags.HasExtendedTextures")
                && scfuSerializerText.Contains("streamWriter.WriteBytes(scfu.ExtendedTextureOverrideData);"),
                "SimpleCharFullUpdate must be able to emit captured extended texture override data.");
            Assert.IsTrue(
                scfuPacketText.Contains("private const int SubwayPlayfieldResource = 127;")
                && scfuPacketText.Contains("private const int SubwayFilthFleaMonsterData = 17657;")
                && scfuPacketText.Contains("private const string SubwayFilthFleaName = \"Filth Flea\"")
                && scfuPacketText.Contains("CapturedSubwayFilthFleaExtendedTextureOverrideData")
                && scfuPacketText.Contains("0x4D, 0x61, 0x74, 0x65,")
                && scfuPacketText.Contains("0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x39")
                && scfuPacketText.Contains("IsCapturedSubwayFilthFlea(charPlayfield, monsterData, charName)"),
                "Captured Subway Filth Flea must emit the live Material #9 extended texture override block only for PF127 monsterData 17657.");
            string thiefFactory = ExtractMethodBlock(
                providerText,
                "private static CapturedSubwaySpawnDefinition Thief");
            Assert.IsTrue(
                thiefFactory.Contains("\"Thief\"")
                && thiefFactory.Contains("26092")
                && thiefFactory.Contains("40694")
                && thiefFactory.Contains("138")
                && providerText.Contains("CapturedSurveySpawn(Thief(0x7953AEA5, 5, 115, 72.7292557f, 115.61483f, 313.1308f, 93, 20))"),
                "Captured Subway Thief must preserve live monsterData, scale, head mesh, run speed, NPC family, and current surveyed position.");
            Assert.IsTrue(
                orchestratorText.Contains("CapturedSubwayThiefMonsterData = 26092")
                && orchestratorText.Contains("CapturedSubwayThiefBodyMesh = 160561")
                && orchestratorText.Contains("CapturedSubwayThiefBackMesh = 7777")
                && orchestratorText.Contains("mobCharacter.Textures.Add(new AOTextures(0, 0x24CA));")
                && orchestratorText.Contains("mobCharacter.Textures.Add(new AOTextures(1, 0x2219));")
                && orchestratorText.Contains("mobCharacter.Textures.Add(new AOTextures(2, 0x24CC));")
                && orchestratorText.Contains("mobCharacter.Textures.Add(new AOTextures(3, 0x24CB));")
                && orchestratorText.Contains("mobCharacter.Textures.Add(new AOTextures(4, 0x24CD));")
                && orchestratorText.Contains("mobCharacter.MeshLayer.AddMesh(0, CapturedSubwayThiefBodyMesh, 0, 2);")
                && orchestratorText.Contains("mobCharacter.MeshLayer.AddMesh(1, CapturedSubwayThiefBackMesh, 0, 2);"),
                "Captured Subway Thief must apply the live texture IDs and three-mesh humanoid appearance shape.");
            Assert.IsTrue(
                scfuMessageText.Contains("public SimpleCharFullUpdateFlags AdditionalFlags { get; set; }")
                && scfuMessageText.Contains("public SimpleCharFullUpdateFlags SuppressedFlags { get; set; }")
                && scfuMessageText.Contains("public Vector3[] Waypoints { get; set; }")
                && scfuSerializerText.Contains("SimpleCharFullUpdateFlags.HasWaypoints")
                && scfuSerializerText.Contains("streamWriter.WriteInt32(scfu.Waypoints.Length);")
                && scfuSerializerText.Contains("flags |= scfu.AdditionalFlags;")
                && scfuSerializerText.Contains("flags &= ~scfu.SuppressedFlags;"),
                "SimpleCharFullUpdate must be able to emit captured waypoint data and capture-only flag deltas.");
            Assert.IsTrue(
                scfuPacketText.Contains("private const int SubwayThiefMonsterData = 26092")
                && scfuPacketText.Contains("private const string SubwayThiefName = \"Thief\"")
                && scfuPacketText.Contains("CapturedSubwayThiefAppearanceValue = 0x00122002")
                && scfuPacketText.Contains("CapturedSubwayThiefUnknown1")
                && scfuPacketText.Contains("scfu.Version = 58;")
                && scfuPacketText.Contains("scfu.Appearance.Value = CapturedSubwayThiefAppearanceValue;")
                && scfuPacketText.Contains("SimpleCharFullUpdateFlags.UnknownFlag6 | SimpleCharFullUpdateFlags.IsPet")
                && scfuPacketText.Contains("scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;")
                && scfuPacketText.Contains("IsCapturedSubwayThief(charPlayfield, monsterData, charName)"),
                "Captured Subway Thief must emit the live version, appearance value, unknown movement bytes, and flag mask only for PF127 monsterData 26092.");
        }

        [TestMethod]
        public void SubwayExistingPopulationAndPatrolReplayRemainLoaded()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwaySpawnOrchestrator.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcPatrolReplayCoordinator.cs"));
            string npcControllerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Controllers\NPCController.cs"));

            Assert.AreEqual(
                95,
                CountOccurrences(providerText, "            CapturedSurveySpawn("),
                "The expanded ordinary-archetype slice must not disturb the 95 previously supported spawns.");

            string[] patrolSourceIdentities =
                {
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
                45,
                CountOccurrences(providerText, "new CapturedSubwayPatrolReplaySegment("),
                "The four moving mobs must load complete periodic NpcPath cycles from capture 20260709-164414.");
            Assert.IsTrue(
                providerText.Contains("new CapturedSubwayPatrolReplaySegment(0.665506, 90.9275284f")
                && providerText.Contains("0x7953AF18")
                && providerText.Contains("0x7953AF57")
                && providerText.Contains("0x79531752")
                && providerText.Contains("0x79531754")
                && providerText.Contains("GetPatrolReplaySegments(int sourceInstance)"),
                "Captured patrol replay must preserve complete cycle timing, movement modes, and captured route speeds.");
            Assert.IsTrue(
                orchestratorText.Contains("this.patrolReplay.AssignCapturedSubwayReplay(")
                && orchestratorText.Contains("mobCharacter.AddWaypoint(start, false);")
                && orchestratorText.Contains("mobCharacter.AddWaypoint(end, false);")
                && orchestratorText.Contains("npcController.SetCapturedPatrolReplaySegments(segments, false, true);")
                && orchestratorText.Contains("npcController.State = CharacterState.Patrolling;"),
                "Subway spawn orchestration must announce live SCFU waypoints and retain exact captured segment starts.");
            Assert.IsTrue(
                coordinatorText.Contains("BuildCapturedSubwaySegments(int sourceInstance)")
                && coordinatorText.Contains("this.capturedSubwayContentProvider.GetPatrolReplaySegments(sourceInstance)")
                && coordinatorText.Contains("segments[i].MoveMode"),
                "NpcPatrolReplayCoordinator must preserve captured Subway coordinates, timing, and movement mode.");
            Assert.IsTrue(
                npcControllerText.Contains("private bool IsCapturedIdlePatrolReplay()")
                && npcControllerText.Contains("&& this.HasCapturedPatrolReplay();")
                && npcControllerText.Contains("&& this.Character.FightingTarget.Equals(Identity.None)")
                && npcControllerText.Contains("segment.MoveMode == EnemyBehaviorContract.RunMoveMode")
                && npcControllerText.Contains(": capturedStart")
                && npcControllerText.Contains("capturedPatrolReplayBatchesZeroDelaySegments")
                && npcControllerText.Contains("segment.DelayAfterSeconds > 0.0")
                && !npcControllerText.Contains("IsCapturedCleaningRobotIdlePatrol"),
                "Subway replay must preserve captured starts/movement modes, batch same-time corrections, and stop when combat begins.");

            AssertTextBefore(
                ExtractMethodBlock(orchestratorText, "private void SpawnCapturedSubwayMob("),
                "var fullUpdate = SimpleCharFullUpdate.ConstructMessage(mobCharacter);",
                "this.activateNpc(mobCharacter);");
            AssertTextBefore(
                ExtractMethodBlock(orchestratorText, "private void SpawnCapturedSubwayMob("),
                "this.activateNpc(mobCharacter);",
                "playfield.Announce(fullUpdate);");

            string scfuPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs"));
            Assert.IsTrue(
                scfuPacketText.Contains("character.Waypoints.Count > 1")
                && scfuPacketText.Contains("scfu.Version = 58;")
                && scfuPacketText.Contains("scfu.Waypoints ="),
                "Moving Subway NPC SCFU must match live version 58 with its initial two-point path.");

            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            AssertTextBefore(
                ExtractMethodBlock(npcRuntimeText, "internal void StopDyingNpcCombatState"),
                "npcController.SnapshotCurrentMotionPosition();",
                "npcController.StopFollow();");
        }

        [TestMethod]
        public void SubwayOrdinaryArchetypesUseCaptureBackedTemplateFreeFramework()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinarySpawnOrchestrator.cs"));
            string runtimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string combatText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string scfuText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            Assert.AreEqual(
                10,
                CountOccurrences(providerText, "            new CapturedSubwayOrdinaryArchetypeDefinition("),
                "The nine ordinary families must retain ten captured visual/template variants because Workman and Architect Striker differ.");
            Assert.AreEqual(
                126,
                CountOccurrences(providerText, "            new CapturedSubwayOrdinarySpawnDefinition("),
                "The completed capture survey must register all 126 spatially deduplicated ordinary spawn positions.");

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
                    "Neural Burnout"
                };
            for (int i = 0; i < capturedNames.Length; i++)
            {
                Assert.IsTrue(providerText.Contains("\"" + capturedNames[i] + "\""), "Missing " + capturedNames[i] + ".");
            }

            int[] capturedMonsterData = { 30464, 203739, 203854, 203743, 96056, 55648, 203745, 31909, 96193, 203730 };
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
                providerText.Contains("CapturedSubwayTextureDefinition[]")
                && providerText.Contains("CapturedSubwayMeshDefinition[]")
                && providerText.Contains("CapturedSubwayWaypointDefinition[]")
                && providerText.Contains("CapturedFlags")
                && providerText.Contains("Unknown1")
                && providerText.Contains("Unknown2"),
                "Captured SCFU visual, flag, unknown-field, and path data must remain first-class evidence.");
            Assert.IsTrue(
                providerText.Contains("new CapturedSubwayWaypointDefinition(")
                && orchestratorText.Contains("foreach (CapturedSubwayWaypointDefinition waypoint in spawn.Waypoints)")
                && orchestratorText.Contains("controller.State = CharacterState.Patrolling;"),
                "Captured SCFU movement paths must load where the captures supplied them.");

            Assert.IsFalse(
                orchestratorText.Contains("SpawnMobFromTemplate") || orchestratorText.Contains("MobTemplateDao"),
                "Ordinary Subway archetypes must never substitute guessed database templates for capture evidence.");
            Assert.IsTrue(
                orchestratorText.Contains("Pool.Instance.GetFreeInstance<Character>")
                && orchestratorText.Contains("ApplyCapturedStats(character, spawn, archetype)")
                && orchestratorText.Contains("ApplyCapturedAppearance(character, archetype)")
                && orchestratorText.Contains("CapturedSubwayOrdinaryRuntimeRegistry.Register")
                && orchestratorText.Contains("character.Stats.SetBaseValueWithoutTriggering("),
                "Template-free ordinary NPC construction must still use the standard attackable Character runtime.");
            Assert.IsFalse(
                ExtractMethodBlock(orchestratorText, "private static void SetMobStat(").Contains(".Value = value"),
                "Template-free captured NPC initialization must not trigger derived stats before all base values exist.");
            Assert.IsTrue(
                scfuText.Contains("CapturedSubwayOrdinaryRuntimeRegistry.TryGet")
                && scfuText.Contains("scfu.AdditionalFlags = spawn.CapturedFlags;")
                && scfuText.Contains("scfu.SuppressedFlags = ~spawn.CapturedFlags;")
                && scfuText.Contains("scfu.Unknown1 = spawn.Unknown1.ToArray();")
                && scfuText.Contains("archetype.Textures.Select(")
                && scfuText.Contains("archetype.Meshes.Select("),
                "SCFU construction must emit the captured ordinary appearance and exact optional-field shape.");
            Assert.IsTrue(
                combatText.Contains("capturedOrdinary.Archetype.Combat.Observed")
                && combatText.Contains("MinDamage = combat.MinDamage")
                && combatText.Contains("RechargeSeconds = combat.RechargeSeconds > 0"),
                "Ordinary combat must use observed damage and timing without changing unrelated global combat behavior.");
            Assert.IsTrue(
                providerText.Contains("DropGroupHash = \"captured-subway-ordinary\"")
                && playfieldText.Contains("CapturedSubwayOrdinaryLootTable")
                && playfieldText.Contains("lootSource = \"captured-subway-ordinary\""),
                "Corpse loot must prefer only captured ordinary evidence before debug/database fallbacks.");

            Assert.IsTrue(
                runtimeText.Contains("new CapturedSubwayOrdinaryContentProvider()")
                && runtimeText.Contains("new CapturedSubwayOrdinarySpawnOrchestrator(")
                && runtimeText.Contains("this.capturedSubwayOrdinarySpawns.SpawnForPlayfield(this.playfield, playfieldIdentity);")
                && projectText.Contains(@"Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs")
                && projectText.Contains(@"Core\Playfields\CapturedSubwayOrdinarySpawnOrchestrator.cs"),
                "PF127 runtime and project wiring must include the ordinary capture-backed slice.");

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
        public void SubwayFilthFleaCorpseUsesCapturedLiveVisualTemplate()
        {
            string repositoryRoot = FindRepositoryRoot();
            string corpsePacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs"));

            Assert.IsTrue(
                corpsePacketText.Contains("SubwayPlayfieldResource = 127")
                && corpsePacketText.Contains("SubwayFilthFleaMonsterData = 17657")
                && corpsePacketText.Contains("SubwayFilthFleaName = \"Filth Flea\"")
                && corpsePacketText.Contains("CapturedSubwayFilthFleaPacketLength = 457"),
                "Subway Filth Flea corpse selection must stay scoped to the captured PF127 identity and packet length.");
            Assert.IsTrue(
                corpsePacketText.Contains("CapturedSubwayFilthFleaTemplate")
                && corpsePacketText.Contains("01000007E24D617465726961")
                && corpsePacketText.Contains("6C202339"),
                "The live Material #9 flea corpse visual tail from capture 20260709-164414 must remain present.");

            string buildMethod = ExtractMethodBlock(
                corpsePacketText,
                "public static byte[] Build(");
            Assert.IsTrue(
                buildMethod.Contains("IsCapturedSubwayFilthFlea(deadNpc)")
                && buildMethod.Contains("return BuildCapturedSubwayFilthFlea("),
                "PF127 Filth Flea corpses must select the capture-backed visual packet before generic corpse construction.");

            string capturedBuildMethod = ExtractMethodBlock(
                corpsePacketText,
                "private static byte[] BuildCapturedSubwayFilthFlea(");
            Assert.IsTrue(
                capturedBuildMethod.Contains("CapturedSubwayFilthFleaTemplate.Clone()")
                && capturedBuildMethod.Contains("WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);")
                && capturedBuildMethod.Contains("WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);")
                && capturedBuildMethod.Contains("WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);")
                && capturedBuildMethod.Contains("CapturedSubwayFilthFleaTailDeadNpcInstanceOffset"),
                "Captured flea corpse construction must retain the live visual payload while patching runtime identities.");
        }

        [TestMethod]
        public void KnownPlayfieldContentModulesAreRegisteredExactlyOnceThroughCoordinatorPath()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentCoordinator.cs"));
            string registrationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentRegistration.cs"));

            string[] expectedModules =
                {
                    "AreteContentModule",
                    "MontroyalContentModule",
                    "SubwayContentModule",
                    "PrivateCityContentModule"
                };

            Assert.IsTrue(
                playfieldText.Contains("private readonly PlayfieldRuntimeSystems runtimeSystems"),
                "Playfield must own runtime systems through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.MaterializeStartupObjects("),
                "Playfield must enter startup content materialization through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldContentCoordinator content"),
                "PlayfieldRuntimeSystems must own PlayfieldContentCoordinator.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.content.RegisterContent(this.playfield, playfieldIdentity);"),
                "PlayfieldRuntimeSystems must delegate content registration through PlayfieldContentCoordinator.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.RegisterContent,"),
                "PlayfieldRuntimeSystems must pass content registration into startup materialization.");

            int coordinatorIndex = runtimeSystemsText.IndexOf("new PlayfieldContentCoordinator(", StringComparison.Ordinal);
            Assert.IsTrue(coordinatorIndex >= 0, "Missing PlayfieldContentCoordinator construction.");

            int previousIndex = coordinatorIndex;
            for (int i = 0; i < expectedModules.Length; i++)
            {
                string constructor = "new " + expectedModules[i] + "()";
                Assert.AreEqual(
                    1,
                    CountOccurrences(runtimeSystemsText, constructor),
                    expectedModules[i] + " must be registered exactly once.");
                Assert.AreEqual(
                    0,
                    CountOccurrences(playfieldText, constructor),
                    "Playfield must not directly construct " + expectedModules[i] + ".");

                int moduleIndex = runtimeSystemsText.IndexOf(constructor, coordinatorIndex, StringComparison.Ordinal);
                Assert.IsTrue(moduleIndex > previousIndex, expectedModules[i] + " is not in expected coordinator order.");
                previousIndex = moduleIndex;
            }

            Assert.IsTrue(
                coordinatorText.Contains("new PlayfieldContentRegistration(playfield, playfieldIdentity)"),
                "PlayfieldContentCoordinator must create PlayfieldContentRegistration.");
            Assert.IsTrue(
                coordinatorText.Contains("module.Register(registration)"),
                "PlayfieldContentCoordinator must dispatch registrations through PlayfieldContentRegistration.");
            Assert.IsTrue(
                registrationText.Contains("public sealed class PlayfieldContentRegistration"),
                "PlayfieldContentRegistration must remain the registration boundary.");
        }

        [TestMethod]
        public void PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string attackHandlerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string npcCombatMovementText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldNpcCombatMovementRuntimeService.cs"));
            string objectLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectLifecycleRuntimeService.cs"));
            string objectMaterializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
            string dbMobSpawnText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldDbMobSpawnRuntimeService.cs"));
            string environmentFunctionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldEnvironmentFunctionRuntimeService.cs"));
            string staticDynelRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStaticDynelRuntimeService.cs"));
            string vendorRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVendorRuntimeService.cs"));
            string corpseAccessText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldCorpseAccessRuntimeService.cs"));
            string rewardRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRewardRuntimeService.cs"));
            string lifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldLifecycleRuntimeService.cs"));
            string transferText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTransferRuntimeService.cs"));
            string playerDeathRespawnText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldPlayerDeathRespawnRuntimeService.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));
            string wallCollisionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldWallCollisionRuntimeService.cs"));
            string statUpdateText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatUpdateRuntimeService.cs"));
            string materializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
            string timedLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTimedLifecycleRuntimeService.cs"));
            string packetSequencesText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldPacketSequencingRuntimeService.cs"));
            string visibilityFanoutText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityFanoutRuntimeService.cs"));
            string visibilityPacketText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));
            string announcementText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldAnnouncementRuntimeService.cs"));
            string publishFanoutText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldPublishFanoutRuntimeService.cs"));
            string characterHeartbeatText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldCharacterHeartbeatRuntimeService.cs"));
            string aotomationDeliveryText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldAOtomationDeliveryRuntimeService.cs"));
            string npcCombatTickText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string corpseLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCorpseLifecycleCoordinator.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            string[] runtimeCoordinatorConstructors =
                {
                    "new PlayfieldObjectLifecycleRuntimeService()",
                    "new PlayfieldObjectMaterializationRuntimeService()",
                    "new PlayfieldDbMobSpawnRuntimeService()",
                    "new PlayfieldEnvironmentFunctionRuntimeService()",
                    "new PlayfieldNpcCombatMovementRuntimeService()",
                    "new PlayfieldCharacterHeartbeatRuntimeService()",
                    "new PlayfieldPacketSequencingRuntimeService(this.packetSequencing)",
                    "new PlayfieldCorpseAccessRuntimeService()",
                    "new PlayfieldRewardRuntimeService()",
                    "new NPCRuntimeService(playfield, this.dynelRegistry, this.rewards)",
                    "new PlayfieldLifecycleRuntimeService()",
                    "new PlayfieldPlayerDeathRespawnRuntimeService()",
                    "new PlayfieldStatelTransitionRuntimeService()",
                    "new PlayfieldStatUpdateRuntimeService()",
                    "new PlayfieldStaticDynelRuntimeService()",
                    "new PlayfieldTimedLifecycleRuntimeService()",
                    "new PlayfieldVendorRuntimeService()",
                    "new PlayfieldVisibilityFanoutRuntimeService()",
                    "new PlayfieldVisibilityPacketRuntimeService(this.visibilityFanout, this.packetSequences)",
                    "new PlayfieldWallCollisionRuntimeService()",
                    "new PlayfieldAnnouncementRuntimeService()",
                    "new PlayfieldPublishFanoutRuntimeService()",
                    "new PlayfieldAOtomationDeliveryRuntimeService()",
                    "new PrivateCityReadyInitCoordinator("
                };
            for (int i = 0; i < runtimeCoordinatorConstructors.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    CountOccurrences(runtimeSystemsText, runtimeCoordinatorConstructors[i]),
                    "PlayfieldRuntimeSystems must own " + runtimeCoordinatorConstructors[i] + ".");
                Assert.AreEqual(
                    0,
                    CountOccurrences(playfieldText, runtimeCoordinatorConstructors[i]),
                    "Playfield must not directly construct " + runtimeCoordinatorConstructors[i] + ".");
            }

            Assert.AreEqual(
                0,
                CountOccurrences(runtimeSystemsText, "new NpcCorpseLifecycleCoordinator("),
                "PlayfieldRuntimeSystems must delegate NPC corpse coordinator construction to NPCRuntimeService.");
            Assert.AreEqual(
                0,
                CountOccurrences(runtimeSystemsText, "new NpcCombatTickCoordinator(playfield)"),
                "PlayfieldRuntimeSystems must delegate NPC combat coordinator construction to NPCRuntimeService.");
            Assert.AreEqual(
                1,
                CountOccurrences(npcRuntimeText, "new NpcCorpseLifecycleCoordinator(playfield, this.RemoveNpcHome)"),
                "NPCRuntimeService must own NPC corpse lifecycle coordinator construction.");
            Assert.IsTrue(
                corpseLifecycleText.Contains("private readonly Action<Identity> removeNpcHome;")
                && corpseLifecycleText.Contains("this.removeNpcHome(target.Identity);"),
                "NpcCorpseLifecycleCoordinator must delegate home-state cleanup through NPCRuntimeService.");
            Assert.IsFalse(
                corpseLifecycleText.Contains("this.playfield.RemoveNpcHome(target.Identity);"),
                "NpcCorpseLifecycleCoordinator must not route NPC home cleanup back through Playfield.");
            Assert.AreEqual(
                1,
                CountOccurrences(npcRuntimeText, "new NpcCombatTickCoordinator(playfield)"),
                "NPCRuntimeService must own NPC combat tick coordinator construction.");
            Assert.IsTrue(
                npcRuntimeText.Contains("private readonly PlayfieldDynelRegistry dynelRegistry;"),
                "NPCRuntimeService must own NPC registry integration.");
            Assert.IsTrue(
                objectLifecycleText.Contains("internal sealed class PlayfieldObjectLifecycleRuntimeService")
                && objectLifecycleText.Contains("internal void RemoveInstancedEntity(IInstancedEntity entity)")
                && objectLifecycleText.Contains("Pool.Instance.RemoveObject(entity);"),
                "PlayfieldObjectLifecycleRuntimeService must own safe instanced object removal routing.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldObjectLifecycleRuntimeService objectLifecycle;")
                && runtimeSystemsText.Contains("this.objectLifecycle.RemoveInstancedEntity(entity);")
                && playfieldText.Contains("this.runtimeSystems.RemoveInstancedEntity(entity);"),
                "Playfield object removals must route through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                playfieldText.Contains("Pool.Instance.RemoveObject(entity);"),
                "Playfield must not directly remove instanced entities from Pool.");
            Assert.IsTrue(
                objectMaterializationText.Contains("internal sealed class PlayfieldObjectMaterializationRuntimeService")
                && objectMaterializationText.Contains("internal void MaterializeStartupObjects(")
                && objectMaterializationText.Contains("this.MaterializeDbMobSpawns(")
                && objectMaterializationText.Contains("registerContent(playfieldIdentity);")
                && objectMaterializationText.Contains("this.MaterializeVendors(")
                && objectMaterializationText.Contains("this.MaterializeStaticDynels(")
                && objectMaterializationText.Contains("refreshDynelRegistry();"),
                "PlayfieldObjectMaterializationRuntimeService must own startup object materialization sequencing.");
            Assert.IsFalse(
                objectMaterializationText.Contains("MobSpawnDao")
                || objectMaterializationText.Contains("MobSpawnStatDao")
                || objectMaterializationText.Contains("NonPlayerCharacterHandler")
                || objectMaterializationText.Contains("new NPCController")
                || objectMaterializationText.Contains("ScriptCompiler")
                || objectMaterializationText.Contains("VendorHandler")
                || objectMaterializationText.Contains("new StaticDynel")
                || objectMaterializationText.Contains("SendCompressed")
                || objectMaterializationText.Contains("Announce(")
                || objectMaterializationText.Contains("Stats["),
                "PlayfieldObjectMaterializationRuntimeService must not own DB loading, object construction, script creation, vendor spawning, packets, or stat algorithms.");
            Assert.IsTrue(
                dbMobSpawnText.Contains("internal sealed class PlayfieldDbMobSpawnRuntimeService")
                && dbMobSpawnText.Contains("internal IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions(Identity playfieldIdentity)")
                && dbMobSpawnText.Contains("MobSpawnDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance })")
                && dbMobSpawnText.Contains("internal IEnumerable<DBMobSpawnStat> LoadMobSpawnStats(DBMobSpawn mob)")
                && dbMobSpawnText.Contains("MobSpawnStatDao.Instance.GetWhere(new { mob.Id, mob.Playfield })")
                && dbMobSpawnText.Contains("internal ICharacter InstantiateDbMobSpawn(DBMobSpawn mob, DBMobSpawnStat[] stats, Playfield playfield)")
                && dbMobSpawnText.Contains("NonPlayerCharacterHandler.InstantiateMobSpawn(")
                && dbMobSpawnText.Contains("new NPCController()")
                && dbMobSpawnText.Contains("internal void AttachMobSpawnKnuBot(DBMobSpawn mob, ICharacter cmob)")
                && dbMobSpawnText.Contains("ScriptCompiler.Instance.CreateKnuBot(mob.KnuBotScriptName, cmob.Identity)"),
                "PlayfieldDbMobSpawnRuntimeService must own DB mob spawn data loading, NPC construction callback, and KnuBot attachment.");
            Assert.IsFalse(
                playfieldText.Contains("private IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions")
                || playfieldText.Contains("private IEnumerable<DBMobSpawnStat> LoadMobSpawnStats")
                || playfieldText.Contains("private ICharacter InstantiateDbMobSpawn")
                || playfieldText.Contains("private void AttachMobSpawnKnuBot"),
                "Playfield must not directly own DB mob spawn loading or construction callbacks.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldDbMobSpawnRuntimeService dbMobSpawns")
                && runtimeSystemsText.Contains("this.dbMobSpawns = new PlayfieldDbMobSpawnRuntimeService();")
                && runtimeSystemsText.Contains("this.dbMobSpawns.LoadMobSpawnDefinitions")
                && runtimeSystemsText.Contains("this.dbMobSpawns.LoadMobSpawnStats")
                && runtimeSystemsText.Contains("(mob, stats) => this.dbMobSpawns.InstantiateDbMobSpawn(mob, stats, this.playfield)")
                && runtimeSystemsText.Contains("this.dbMobSpawns.AttachMobSpawnKnuBot"),
                "PlayfieldRuntimeSystems must route DB mob spawn materialization through the DB mob spawn runtime service.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldDbMobSpawnRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldDbMobSpawnRuntimeService.");
            Assert.IsTrue(
                environmentFunctionText.Contains("internal sealed class PlayfieldEnvironmentFunctionRuntimeService")
                && environmentFunctionText.Contains("internal void ExecuteFunction(")
                && environmentFunctionText.Contains("switch (imExecuteFunction.Function.Target)")
                && environmentFunctionText.Contains("case 1:")
                && environmentFunctionText.Contains("case 2:")
                && environmentFunctionText.Contains("case 3:")
                && environmentFunctionText.Contains("case 14:")
                && environmentFunctionText.Contains("case 19:")
                && environmentFunctionText.Contains("case 23:")
                && environmentFunctionText.Contains("case 26:")
                && environmentFunctionText.Contains("case 100:")
                && environmentFunctionText.Contains("sendNoValidTargetMessage(character, \"No valid target found\");")
                && environmentFunctionText.Contains("FunctionCollection.Instance.CallFunction("),
                "PlayfieldEnvironmentFunctionRuntimeService must own environment function target routing and function dispatch.");
            Assert.IsFalse(
                environmentFunctionText.Contains("ChatTextMessage")
                || environmentFunctionText.Contains("SendCompressed")
                || environmentFunctionText.Contains("Pool.Instance")
                || environmentFunctionText.Contains("Teleport("),
                "PlayfieldEnvironmentFunctionRuntimeService must not own packet construction, sends, Pool lookup, or teleport mechanics.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldEnvironmentFunctionRuntimeService environmentFunctions")
                && runtimeSystemsText.Contains("this.environmentFunctions = new PlayfieldEnvironmentFunctionRuntimeService();")
                && runtimeSystemsText.Contains("internal void ExecuteFunction(")
                && runtimeSystemsText.Contains("this.environmentFunctions.ExecuteFunction("),
                "PlayfieldRuntimeSystems must route environment function execution through the environment function runtime service.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ExecuteFunction(")
                && playfieldText.Contains("private static void SendNoValidFunctionTargetMessage(Character character, string text)")
                && playfieldText.Contains("new ChatTextMessage { Identity = character.Identity, Text = text }"),
                "Playfield must delegate environment function routing while keeping client feedback packet construction.");
            Assert.IsFalse(
                playfieldText.Contains("FunctionCollection.Instance.CallFunction(")
                || playfieldText.Contains("switch (imExecuteFunction.Function.Target)"),
                "Playfield must not directly own environment function dispatch or target routing.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldEnvironmentFunctionRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldEnvironmentFunctionRuntimeService.");
            Assert.IsTrue(
                staticDynelRuntimeText.Contains("internal sealed class PlayfieldStaticDynelRuntimeService")
                && staticDynelRuntimeText.Contains("internal IEntity CreateStaticDynel(Identity playfieldIdentity, PlayfieldStaticDynelDefinition staticDynel)")
                && staticDynelRuntimeText.Contains("new StaticDynel(playfieldIdentity, staticDynel.Identity, staticDynel.Template)")
                && staticDynelRuntimeText.Contains("foreach (GameTuple<CharacterStat, uint> stat in staticDynel.Stats)")
                && staticDynelRuntimeText.Contains("sdy.Stats[(int)stat.Value1] = (int)stat.Value2;")
                && staticDynelRuntimeText.Contains("sdy.Stats.Add((int)stat.Value1, (int)stat.Value2);")
                && staticDynelRuntimeText.Contains("sdy.Coordinate = staticDynel.Coordinate;")
                && staticDynelRuntimeText.Contains("sdy.Heading = staticDynel.Heading;"),
                "PlayfieldStaticDynelRuntimeService must own static dynel runtime construction from content definitions.");
            Assert.IsFalse(
                staticDynelRuntimeText.Contains("StaticDynelDao")
                || staticDynelRuntimeText.Contains("MessagePackZip.DeserializeData")
                || staticDynelRuntimeText.Contains("SendCompressed")
                || staticDynelRuntimeText.Contains("Pool.Instance")
                || staticDynelRuntimeText.Contains("VendorHandler"),
                "PlayfieldStaticDynelRuntimeService must not own content DB loading, deserialization, packets, Pool registration, or vendor spawning.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldStaticDynelRuntimeService staticDynelRuntime")
                && runtimeSystemsText.Contains("this.staticDynelRuntime = new PlayfieldStaticDynelRuntimeService();")
                && runtimeSystemsText.Contains("staticDynel => this.staticDynelRuntime.CreateStaticDynel(playfieldIdentity, staticDynel)"),
                "PlayfieldRuntimeSystems must route static dynel construction through the static dynel runtime service.");
            Assert.IsFalse(
                playfieldText.Contains("private IEntity CreateStaticDynel(")
                || playfieldText.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must not directly own static dynel runtime construction.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldStaticDynelRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldStaticDynelRuntimeService.");
            Assert.IsTrue(
                vendorRuntimeText.Contains("internal sealed class PlayfieldVendorRuntimeService")
                && vendorRuntimeText.Contains("internal void SpawnVendors(Playfield playfield, StatelData[] vendorStatels)")
                && vendorRuntimeText.Contains("VendorHandler.SpawnVendorsForPlayfield(playfield, vendorStatels);"),
                "PlayfieldVendorRuntimeService must own vendor runtime spawning.");
            Assert.IsFalse(
                vendorRuntimeText.Contains("StaticDynelDao")
                || vendorRuntimeText.Contains("MessagePackZip.DeserializeData")
                || vendorRuntimeText.Contains("new StaticDynel")
                || vendorRuntimeText.Contains("SendCompressed")
                || vendorRuntimeText.Contains("Pool.Instance"),
                "PlayfieldVendorRuntimeService must not own content DB loading, static dynel construction, packets, or Pool registration.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldVendorRuntimeService vendors")
                && runtimeSystemsText.Contains("this.vendors = new PlayfieldVendorRuntimeService();")
                && runtimeSystemsText.Contains("vendorStatels => this.vendors.SpawnVendors(this.playfield, vendorStatels)"),
                "PlayfieldRuntimeSystems must route vendor spawning through the vendor runtime service.");
            Assert.IsFalse(
                playfieldText.Contains("private void SpawnVendors(StatelData[] vendorStatels)")
                || playfieldText.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels)"),
                "Playfield must not directly own vendor runtime spawning.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldVendorRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldVendorRuntimeService.");
            Assert.AreEqual(
                1,
                CountOccurrences(npcRuntimeText, "new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent)"),
                "NPCRuntimeService must own captured Arete robot content provider construction.");
            Assert.AreEqual(
                1,
                CountOccurrences(
                    npcRuntimeText,
                    "new NpcPatrolReplayCoordinator(this.capturedAreteRobotContent, this.capturedSubwayContent)"),
                "NPCRuntimeService must own NPC patrol replay coordinator construction.");
            Assert.AreEqual(
                1,
                CountOccurrences(
                    npcRuntimeText,
                    "new CapturedAreteRobotSpawnOrchestrator("),
                "NPCRuntimeService must own captured Arete robot spawn orchestration construction.");
            Assert.IsTrue(
                npcRuntimeText.Contains("this.ActivateNpc"),
                "NPCRuntimeService must pass NPC activation ownership into captured robot spawning.");
            Assert.IsTrue(
                npcRuntimeText.Contains(
                    "private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();"),
                "NPCRuntimeService must own NPC home state storage.");
            Assert.IsTrue(
                npcRuntimeText.Contains(
                    "private readonly Dictionary<int, DateTime> corpseDespawnTicks = new Dictionary<int, DateTime>();"),
                "NPCRuntimeService must own corpse despawn scheduling state.");
            Assert.IsFalse(
                playfieldText.Contains("private readonly Dictionary<int, DateTime> corpseDespawnTicks"),
                "Playfield must not own corpse despawn scheduling state.");
            Assert.IsFalse(
                playfieldText.Contains("private readonly Dictionary<int, NpcHomeState> npcHomeStates"),
                "Playfield must not own NPC home state storage.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly NPCRuntimeService npcRuntime")
                && runtimeSystemsText.Contains("private readonly PlayfieldCorpseAccessRuntimeService corpseAccess")
                && runtimeSystemsText.Contains("private readonly PlayfieldLifecycleRuntimeService lifecycle")
                && runtimeSystemsText.Contains("private readonly PlayfieldNpcCombatMovementRuntimeService npcCombatMovement")
                && runtimeSystemsText.Contains("private readonly PlayfieldObjectMaterializationRuntimeService objectMaterialization")
                && runtimeSystemsText.Contains("private readonly PlayfieldPacketSequencingRuntimeService packetSequences")
                && runtimeSystemsText.Contains("private readonly PlayfieldPlayerDeathRespawnRuntimeService playerDeathRespawn")
                && runtimeSystemsText.Contains("private readonly PlayfieldStatelTransitionRuntimeService statelTransitions")
                && runtimeSystemsText.Contains("private readonly PlayfieldStatUpdateRuntimeService statUpdates")
                && runtimeSystemsText.Contains("private readonly PlayfieldStaticDynelRuntimeService staticDynelRuntime")
                && runtimeSystemsText.Contains("private readonly PlayfieldTimedLifecycleRuntimeService timedLifecycle")
                && runtimeSystemsText.Contains("private readonly PlayfieldTransferRuntimeService transfers")
                && runtimeSystemsText.Contains("private readonly PlayfieldCharacterHeartbeatRuntimeService characterHeartbeat")
                && runtimeSystemsText.Contains("private readonly PlayfieldVendorRuntimeService vendors")
                && runtimeSystemsText.Contains("private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout")
                && runtimeSystemsText.Contains("private readonly PlayfieldVisibilityPacketRuntimeService visibilityPackets")
                && runtimeSystemsText.Contains("private readonly PlayfieldWallCollisionRuntimeService wallCollision")
                && runtimeSystemsText.Contains("private readonly PlayfieldAnnouncementRuntimeService announcements")
                && runtimeSystemsText.Contains("private readonly PlayfieldPublishFanoutRuntimeService publishFanout")
                && runtimeSystemsText.Contains("private readonly PlayfieldAOtomationDeliveryRuntimeService aotomationDelivery")
                && runtimeSystemsText.Contains("internal void ProcessHeartbeatTimedLifecycle(")
                && runtimeSystemsText.Contains("this.timedLifecycle.ProcessHeartbeatLifecycle(")
                && runtimeSystemsText.Contains("this.Characters")
                && runtimeSystemsText.Contains("this.HasPendingDeadNpcDespawn")
                && runtimeSystemsText.Contains("this.ProcessDeadNpcDespawn")
                && runtimeSystemsText.Contains("this.ProcessNpcPatrolTick")
                && runtimeSystemsText.Contains("internal void ProcessPlayerRespawn(")
                && runtimeSystemsText.Contains("this.playerDeathRespawn.ProcessPlayerRespawn(")
                && runtimeSystemsText.Contains("x => this.CleanupPlayerDeathCombat(x, clearCombatTracking, stopFightingDeadTarget, sendCombatStop)")
                && runtimeSystemsText.Contains("internal void PreparePlayfieldTransfer(")
                && runtimeSystemsText.Contains("this.lifecycle.PreparePlayfieldTransfer(")
                && runtimeSystemsText.Contains("this.npcRuntime.ActivateNpc(character);")
                && runtimeSystemsText.Contains("internal void MaterializeStartupObjects(")
                && runtimeSystemsText.Contains("this.objectMaterialization.MaterializeStartupObjects(")
                && runtimeSystemsText.Contains("this.RegisterContent,")
                && runtimeSystemsText.Contains("this.TryResolveVendorStatels,")
                && runtimeSystemsText.Contains("this.ResolveStaticDynels,")
                && runtimeSystemsText.Contains("this.RefreshDynelRegistry);")
                && runtimeSystemsText.Contains("this.npcRuntime.RegisterNpcHome(character);")
                && runtimeSystemsText.Contains("internal void DespawnNpcImmediately(")
                && runtimeSystemsText.Contains(
                    "this.npcRuntime.DespawnNpcImmediately(target, stopFightingDeadTarget, cancelPendingCorpseSpawn);")
                && runtimeSystemsText.Contains("internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)")
                && runtimeSystemsText.Contains("this.npcRuntime.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);")
                && runtimeSystemsText.Contains("internal void ClearNpcCorpseDespawn(int corpseInstance)")
                && runtimeSystemsText.Contains("this.npcRuntime.ClearNpcCorpseDespawn(corpseInstance);")
                && runtimeSystemsText.Contains("internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)")
                && runtimeSystemsText.Contains("this.npcRuntime.ProcessDueNpcCorpseDespawns(utcNow, despawnCorpse);")
                && runtimeSystemsText.Contains("this.npcRuntime.SpawnCapturedNpcContent(playfieldIdentity);")
                && runtimeSystemsText.Contains("this.npcRuntime.BeginNpcDeath(attacker, target);")
                && runtimeSystemsText.Contains("internal bool ProcessDeadNpcDespawn(ICharacter character)")
                && runtimeSystemsText.Contains("return this.npcRuntime.ProcessDeadNpcDespawn(character);")
                && runtimeSystemsText.Contains("this.npcRuntime.ProcessCombatTick(attacker);")
                && runtimeSystemsText.Contains("this.npcRuntime.ClearInvalidCombatTarget(attacker);")
                && runtimeSystemsText.Contains("this.npcRuntime.ClearFightingTarget(character);")
                && runtimeSystemsText.Contains("this.npcRuntime.StopDyingNpcCombatState(target);")
                && runtimeSystemsText.Contains("this.npcRuntime.AcquireAggro(attacker, target);")
                && runtimeSystemsText.Contains("this.npcRuntime.ProcessPatrolTick(character);")
                && runtimeSystemsText.Contains("this.npcRuntime.ClearCombatTracking(identity);")
                && runtimeSystemsText.Contains("return this.corpseAccess.TryUseCorpse(")
                && runtimeSystemsText.Contains("return this.corpseAccess.TryUseDeadNpcCorpse(")
                && runtimeSystemsText.Contains("return this.corpseAccess.TryLootCorpseItem(")
                && runtimeSystemsText.Contains("this.corpseAccess.ProcessPendingCorpseCreditAwards(")
                && runtimeSystemsText.Contains("this.statelTransitions.CheckStatelCollision(")
                && runtimeSystemsText.Contains("this.statelTransitions.PrimeStatelCollisionContacts(")
                && runtimeSystemsText.Contains("this.statelTransitions.ClearContactState(dynelId);")
                && runtimeSystemsText.Contains("this.wallCollision.CheckWallCollision(")
                && runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToCharacterClients(")
                && runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToOtherCharacterClients(")
                && runtimeSystemsText.Contains("this.visibilityPackets.SendExistingCharacterVisibilityToClient(")
                && runtimeSystemsText.Contains("this.visibilityPackets.AnnounceJoiningCharacterVisibility(")
                && runtimeSystemsText.Contains("this.announcements.AnnounceToCharacterClients(")
                && runtimeSystemsText.Contains("this.announcements.AnnounceToOtherCharacterClients(")
                && runtimeSystemsText.Contains("this.publishFanout.PublishMessageBodyToClient(")
                && runtimeSystemsText.Contains("this.publishFanout.PublishMessageToClient(")
                && runtimeSystemsText.Contains("this.publishFanout.DispatchMessageToPlayfield(")
                && runtimeSystemsText.Contains("this.publishFanout.DispatchMessageToPlayfieldOthers(")
                && runtimeSystemsText.Contains("this.aotomationDelivery.SendMessageToClient(")
                && runtimeSystemsText.Contains("this.aotomationDelivery.SendMessageBodyToClient(")
                && runtimeSystemsText.Contains("this.aotomationDelivery.SendMessageBodiesToClient(")
                && runtimeSystemsText.Contains("this.aotomationDelivery.SendMessageToPlayfield(")
                && runtimeSystemsText.Contains("this.aotomationDelivery.SendMessageToPlayfieldOthers(")
                && runtimeSystemsText.Contains("this.npcCombatMovement.IsInCombatRange(")
                && runtimeSystemsText.Contains("this.npcCombatMovement.UpdateNpcMeleeFollowHold(")
                && runtimeSystemsText.Contains("this.npcCombatMovement.TryMoveNpcIntoCombatRange(")
                && runtimeSystemsText.Contains("this.characterHeartbeat.ProcessRegeneration(")
                && runtimeSystemsText.Contains("this.characterHeartbeat.ProcessFollow(")
                && runtimeSystemsText.Contains("this.characterHeartbeat.ProcessPlayerCollisionChecks(")
                && runtimeSystemsText.Contains("this.statUpdates.SendChangedStats(")
                && runtimeSystemsText.Contains("this.statUpdates.SendChangedStatsIfChanged(")
                && runtimeSystemsText.Contains("this.statUpdates.SendChangedStatsIfClient(")
                && runtimeSystemsText.Contains("this.statUpdates.RunPlayerDeathStatUpdateSequence(")
                && runtimeSystemsText.Contains("internal void TransferToPlayfield(")
                && runtimeSystemsText.Contains("this.transfers.TransferToPlayfield(")
                && runtimeSystemsText.Contains("this.packetSequences.RunPlayfieldTransferBeginSequence(")
                && runtimeSystemsText.Contains("internal void CompletePlayfieldTransfer(")
                && runtimeSystemsText.Contains("this.transfers.CompletePlayfieldTransfer("),
                "PlayfieldRuntimeSystems must delegate runtime entry points through named runtime services.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldLifecycleRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldLifecycleRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldPlayerDeathRespawnRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldPlayerDeathRespawnRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldCharacterHeartbeatRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldCharacterHeartbeatRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldTimedLifecycleRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldTimedLifecycleRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldTransferRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldTransferRuntimeService.");
            Assert.IsTrue(
                characterHeartbeatText.Contains("internal sealed class PlayfieldCharacterHeartbeatRuntimeService")
                && characterHeartbeatText.Contains("internal void ProcessRegeneration(ICharacter dynel, Action<ICharacter> sendChangedStats)")
                && characterHeartbeatText.Contains("StatHealInterval healInterval")
                && characterHeartbeatText.Contains("StatNanoInterval nanoInterval")
                && characterHeartbeatText.Contains("sendChangedStats(dynel);")
                && characterHeartbeatText.Contains("internal void ProcessFollow(ICharacter dynel)")
                && characterHeartbeatText.Contains("dynel.Controller.DoFollow();")
                && characterHeartbeatText.Contains("internal void ProcessPlayerCollisionChecks(")
                && characterHeartbeatText.Contains("checkWallCollision(dynel);")
                && characterHeartbeatText.Contains("checkStatelCollision(dynel);"),
                "PlayfieldCharacterHeartbeatRuntimeService must own regeneration, follow, and player-collision callback sequencing.");
            Assert.IsFalse(
                characterHeartbeatText.Contains("DoCombatTick")
                || characterHeartbeatText.Contains("WallCollision.CheckCollision(")
                || characterHeartbeatText.Contains("TeleportMessageHandler")
                || characterHeartbeatText.Contains("CorpseFullUpdate")
                || characterHeartbeatText.Contains("Inventory"),
                "PlayfieldCharacterHeartbeatRuntimeService must not own combat, wall-routing internals, packets, or inventory.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldPacketSequencingRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldPacketSequencingRuntimeService.");
            Assert.IsTrue(
                packetSequencesText.Contains("internal sealed class PlayfieldPacketSequencingRuntimeService")
                && packetSequencesText.Contains("internal void RunVisibilityPacketPairSequence(")
                && packetSequencesText.Contains("this.packetSequencing.RunSimpleCharFullUpdateCharInPlaySequence(")
                && packetSequencesText.Contains("internal void RunPlayfieldTransferBeginSequence(")
                && packetSequencesText.Contains("this.packetSequencing.RunPlayfieldTransferBeginSequence("),
                "PlayfieldPacketSequencingRuntimeService must own playfield-local packet order orchestration.");
            Assert.IsFalse(
                packetSequencesText.Contains("SimpleCharFullUpdate.")
                || packetSequencesText.Contains("SimpleCharFullUpdateMessage")
                || packetSequencesText.Contains("CharInPlayMessage")
                || packetSequencesText.Contains("TeleportMessageHandler")
                || packetSequencesText.Contains("ZoneRedirectionMessage")
                || packetSequencesText.Contains("SendCompressed")
                || packetSequencesText.Contains("Publish(")
                || packetSequencesText.Contains("Pool.Instance"),
                "PlayfieldPacketSequencingRuntimeService must not own packet construction, sends, transport, or Pool lookups.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldVisibilityFanoutRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldVisibilityFanoutRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldVisibilityPacketRuntimeService.");
            Assert.IsTrue(
                visibilityFanoutText.Contains("internal sealed class PlayfieldVisibilityFanoutRuntimeService")
                && visibilityFanoutText.Contains("internal void AnnounceToCharacterClients(")
                && visibilityFanoutText.Contains("internal void AnnounceToOtherCharacterClients(")
                && visibilityFanoutText.Contains("internal void FanoutExistingCharactersForScfu("),
                "PlayfieldVisibilityFanoutRuntimeService must own visibility recipient fanout entry points.");
            Assert.IsFalse(
                visibilityFanoutText.Contains("SimpleCharFullUpdate")
                || visibilityFanoutText.Contains("CharInPlayMessage")
                || visibilityFanoutText.Contains("SendCompressed")
                || visibilityFanoutText.Contains("Publish(")
                || visibilityFanoutText.Contains("IMSend")
                || visibilityFanoutText.Contains("LogUtil")
                || visibilityFanoutText.Contains("PacketSequencing")
                || visibilityFanoutText.Contains("Pool.Instance"),
                "PlayfieldVisibilityFanoutRuntimeService must not own packet construction, sends, logging, sequencing, or Pool scans.");
            Assert.IsTrue(
                visibilityPacketText.Contains("internal sealed class PlayfieldVisibilityPacketRuntimeService")
                && visibilityPacketText.Contains("internal void SendExistingCharacterVisibilityToClient(")
                && visibilityPacketText.Contains("this.visibilityFanout.FanoutExistingCharactersForScfu(")
                && visibilityPacketText.Contains("SimpleCharFullUpdate.ConstructMessage(temp)")
                && visibilityPacketText.Contains("this.packetSequences.RunVisibilityPacketPairSequence(")
                && visibilityPacketText.Contains("LogUtil.Debug(")
                && visibilityPacketText.Contains("internal void AnnounceJoiningCharacterVisibility(")
                && visibilityPacketText.Contains("announceVisibilityMessage(SimpleCharFullUpdate.ConstructMessage(temp))")
                && visibilityPacketText.Contains("announceVisibilityMessage(charInPlay)"),
                "PlayfieldVisibilityPacketRuntimeService must own visibility packet-pair construction, sequencing delegation, and debug logging.");
            Assert.IsFalse(
                visibilityPacketText.Contains("SendCompressed")
                || visibilityPacketText.Contains("Publish(")
                || visibilityPacketText.Contains("Pool.Instance"),
                "PlayfieldVisibilityPacketRuntimeService must not own direct transport, publish wrappers, or Pool scans.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldAnnouncementRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldAnnouncementRuntimeService.");
            Assert.IsTrue(
                announcementText.Contains("internal sealed class PlayfieldAnnouncementRuntimeService")
                && announcementText.Contains("internal void AnnounceToCharacterClients(")
                && announcementText.Contains("foreach (Character entity in characters)")
                && announcementText.Contains("if (entity.Controller.Client != null)")
                && announcementText.Contains("sendMessageBodyToClient(entity.Controller.Client, messageBody);")
                && announcementText.Contains("internal void AnnounceToOtherCharacterClients(")
                && announcementText.Contains("if (entity.Identity != excludedIdentity)"),
                "PlayfieldAnnouncementRuntimeService must own message announcement recipient fanout orchestration.");
            Assert.IsFalse(
                announcementText.Contains("SimpleCharFullUpdate")
                || announcementText.Contains("CharInPlayMessage")
                || announcementText.Contains("SendCompressed")
                || announcementText.Contains("Publish(")
                || announcementText.Contains("IMSend")
                || announcementText.Contains("LogUtil")
                || announcementText.Contains("PacketSequencing")
                || announcementText.Contains("Pool.Instance"),
                "PlayfieldAnnouncementRuntimeService must not own packet construction, direct sends, publish wrappers, logging, sequencing, or Pool scans.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.AnnounceMessageToCharacterClients(messageBody, this.Send);")
                && playfieldText.Contains(
                    "this.runtimeSystems.AnnounceMessageToOtherCharacterClients(messageBody, dontSend, this.Send);"),
                "Playfield Announce methods must delegate message fanout through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                playfieldText.Contains("entity.Controller.Client,\r\n                            messageBody,\r\n                            this.Publish")
                || playfieldText.Contains("this.runtimeSystems.AnnounceToCharacterClients(")
                || playfieldText.Contains("this.runtimeSystems.AnnounceToOtherCharacterClients("),
                "Playfield must not retain direct announcement recipient/send orchestration.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldPublishFanoutRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldPublishFanoutRuntimeService.");
            Assert.IsTrue(
                publishFanoutText.Contains("internal sealed class PlayfieldPublishFanoutRuntimeService")
                && publishFanoutText.Contains("internal void PublishMessageBodyToClient(")
                && publishFanoutText.Contains("new IMSendAOtomationMessageBodyToClient")
                && publishFanoutText.Contains("internal void PublishMessageToClient(")
                && publishFanoutText.Contains("new IMSendAOtomationMessageToClient")
                && publishFanoutText.Contains("internal void DispatchMessageToPlayfield(")
                && publishFanoutText.Contains("internal void DispatchMessageToPlayfieldOthers("),
                "PlayfieldPublishFanoutRuntimeService must own internal publish/send fanout wrapper orchestration.");
            Assert.IsFalse(
                publishFanoutText.Contains("SimpleCharFullUpdate")
                || publishFanoutText.Contains("CharInPlayMessage")
                || publishFanoutText.Contains("TeleportMessageHandler")
                || publishFanoutText.Contains("ZoneRedirectionMessage")
                || publishFanoutText.Contains("SendCompressed")
                || publishFanoutText.Contains("PacketSequencing")
                || publishFanoutText.Contains("Pool.Instance"),
                "PlayfieldPublishFanoutRuntimeService must not own packet construction, direct sends, sequencing, or Pool lookups.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldAOtomationDeliveryRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldAOtomationDeliveryRuntimeService.");
            Assert.IsTrue(
                aotomationDeliveryText.Contains("internal sealed class PlayfieldAOtomationDeliveryRuntimeService")
                && aotomationDeliveryText.Contains("internal void SendMessageToClient(")
                && aotomationDeliveryText.Contains("clientMessage.client.SendCompressed(clientMessage.message.Body);")
                && aotomationDeliveryText.Contains("internal void SendMessageBodyToClient(")
                && aotomationDeliveryText.Contains("message.client.SendCompressed(message.Body);")
                && aotomationDeliveryText.Contains("internal void SendMessageBodiesToClient(")
                && aotomationDeliveryText.Contains("foreach (MessageBody messageBody in message.Bodies)")
                && aotomationDeliveryText.Contains("internal void SendMessageToPlayfield(")
                && aotomationDeliveryText.Contains("dispatchToPlayfield(clientMessage.Body);")
                && aotomationDeliveryText.Contains("internal void SendMessageToPlayfieldOthers(")
                && aotomationDeliveryText.Contains("dispatchToPlayfieldOthers(clientMessage.Body, clientMessage.Identity);"),
                "PlayfieldAOtomationDeliveryRuntimeService must own AOtomation bus message delivery.");
            Assert.IsFalse(
                aotomationDeliveryText.Contains("SimpleCharFullUpdate")
                || aotomationDeliveryText.Contains("CharInPlayMessage")
                || aotomationDeliveryText.Contains("TeleportMessageHandler")
                || aotomationDeliveryText.Contains("ZoneRedirectionMessage")
                || aotomationDeliveryText.Contains("PacketSequencing")
                || aotomationDeliveryText.Contains("Pool.Instance"),
                "PlayfieldAOtomationDeliveryRuntimeService must not own packet construction, sequencing, or Pool lookups.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.DeliverAOtomationMessageToClient")
                && playfieldText.Contains("this.runtimeSystems.DeliverAOtomationMessageBodyToClient")
                && playfieldText.Contains("this.runtimeSystems.DeliverAOtomationMessageBodiesToClient")
                && playfieldText.Contains("this.runtimeSystems.DeliverAOtomationMessageToPlayfield(message, this.Announce)")
                && playfieldText.Contains("this.runtimeSystems.DeliverAOtomationMessageToPlayfieldOthers("),
                "Playfield bus subscriptions must delegate AOtomation delivery through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                playfieldText.Contains("public static void SendAOtomationMessageToClient")
                || playfieldText.Contains("public void SendAOtomationMessageBodyToClient")
                || playfieldText.Contains("public void SendAOtomationMessageBodiesToClient")
                || playfieldText.Contains("public void SendAOtomationMessageToPlayfield")
                || playfieldText.Contains("public void SendAOtomationMessageToPlayfieldOthers"),
                "Playfield must not retain AOtomation delivery handlers after extraction.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldStatUpdateRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldStatUpdateRuntimeService.");
            Assert.IsTrue(
                statUpdateText.Contains("internal sealed class PlayfieldStatUpdateRuntimeService")
                && statUpdateText.Contains("internal void SendChangedStats(")
                && statUpdateText.Contains("internal void SendChangedStatsIfChanged(")
                && statUpdateText.Contains("internal void SendChangedStatsIfClient(")
                && statUpdateText.Contains("internal void RunPlayerDeathStatUpdateSequence(")
                && statUpdateText.Contains("sendChangedStats(target);")
                && statUpdateText.Contains("cleanupDeathCombat(target);")
                && statUpdateText.Contains("sendDeathAnimation(target);"),
                "PlayfieldStatUpdateRuntimeService must own stat-update callback and death stat-send ordering.");
            Assert.IsFalse(
                statUpdateText.Contains("Stats[")
                || statUpdateText.Contains("StatMessage")
                || statUpdateText.Contains("SendCompressed")
                || statUpdateText.Contains("Stats.Write")
                || statUpdateText.Contains("CashStatRules")
                || statUpdateText.Contains("CombatDamageRules")
                || statUpdateText.Contains("Pool.Instance"),
                "PlayfieldStatUpdateRuntimeService must not own stat math, packet construction, persistence, combat rules, or Pool lookups.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldNpcCombatMovementRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldNpcCombatMovementRuntimeService.");
            Assert.IsTrue(
                npcCombatMovementText.Contains("internal sealed class PlayfieldNpcCombatMovementRuntimeService")
                && npcCombatMovementText.Contains("internal bool IsInCombatRange(")
                && npcCombatMovementText.Contains("internal void UpdateNpcMeleeFollowHold(")
                && npcCombatMovementText.Contains("internal void TryMoveNpcIntoCombatRange(")
                && npcCombatMovementText.Contains("internal static double GetCombatDistance(")
                && npcCombatMovementText.Contains("internal static bool IsCapturedCleaningRobot(")
                && npcCombatMovementText.Contains("private void MoveNpcTowardCombatTarget("),
                "PlayfieldNpcCombatMovementRuntimeService must own NPC range, chase, and follow-target movement decisions.");
            Assert.IsFalse(
                npcCombatMovementText.Contains("SetPosMessage")
                || npcCombatMovementText.Contains("this.Announce(")
                || npcCombatMovementText.Contains("SendCompressed")
                || npcCombatMovementText.Contains("AttackInfo")
                || npcCombatMovementText.Contains("Stats.Write")
                || npcCombatMovementText.Contains("Pool.Instance"),
                "PlayfieldNpcCombatMovementRuntimeService must not own packet construction, sends, attack packets, persistence, or Pool lookups.");
            Assert.IsTrue(
                lifecycleText.Contains("internal sealed class PlayfieldLifecycleRuntimeService")
                && lifecycleText.Contains("internal void PreparePlayfieldTransfer(")
                && lifecycleText.Contains("Thread.Sleep(200);")
                && lifecycleText.Contains("clearTransferContactState(dynel.Identity.Instance);")
                && lifecycleText.Contains("disableTimers(dynel);")
                && lifecycleText.Contains("Thread.Sleep(1000);"),
                "PlayfieldLifecycleRuntimeService must own playfield-transfer sequencing.");
            Assert.IsFalse(
                lifecycleText.Contains("SendCompressed")
                || lifecycleText.Contains("TeleportMessageHandler")
                || lifecycleText.Contains("ZoneRedirectionMessage")
                || lifecycleText.Contains("FullCharacterMessageHandler")
                || lifecycleText.Contains("SimpleCharFullUpdate")
                || lifecycleText.Contains("Stats[")
                || lifecycleText.Contains("Pool.Instance")
                || lifecycleText.Contains("PlayfieldById")
                || lifecycleText.Contains("Announce("),
                "PlayfieldLifecycleRuntimeService must not own packet construction, object lookup, stats algorithms, or transport.");
            Assert.IsTrue(
                transferText.Contains("internal sealed class PlayfieldTransferRuntimeService")
                && transferText.Contains("internal void CompletePlayfieldTransfer(")
                && transferText.Contains("announceDespawn(dynel);")
                && transferText.Contains("applyTransferState(dynel, destination, heading);")
                && transferText.Contains("ZoneClient client = captureClient(dynel);")
                && transferText.Contains("IPlayfield newPlayfield = resolveDestinationPlayfield(playfield);")
                && transferText.Contains("finalizeTransferDispose(dynel, newPlayfield);")
                && transferText.Contains("sendRedirect(client);"),
                "PlayfieldTransferRuntimeService must own post-send cross-playfield handoff orchestration.");
            Assert.IsFalse(
                transferText.Contains("TeleportMessageHandler")
                || transferText.Contains("ZoneRedirectionMessage")
                || transferText.Contains("SendCompressed")
                || transferText.Contains("PlayfieldById")
                || transferText.Contains("new Playfield(")
                || transferText.Contains("DespawnMessageHandler")
                || transferText.Contains("AnnounceOthers")
                || transferText.Contains("Pool.Instance"),
                "PlayfieldTransferRuntimeService must not own packet construction, transport, lookup, or Playfield callbacks.");
            Assert.IsTrue(
                playerDeathRespawnText.Contains("internal sealed class PlayfieldPlayerDeathRespawnRuntimeService")
                && playerDeathRespawnText.Contains("internal void ProcessPlayerRespawn(")
                && playerDeathRespawnText.Contains("logCorpseVisualSkipped(character, corpseIdentity);")
                && playerDeathRespawnText.Contains("sendDeathSocialStatus(character);")
                && playerDeathRespawnText.Contains("markPlayerRespawned(character);")
                && playerDeathRespawnText.Contains("sendDeathRespawnStateStats(character);")
                && playerDeathRespawnText.Contains("stopMovement(character);")
                && playerDeathRespawnText.Contains("cleanupDeathCombat(character);")
                && playerDeathRespawnText.Contains("sendChangedStats(character);")
                && playerDeathRespawnText.Contains("logRespawnRequested(character, corpseIdentity, destinationPlayfield, destination);")
                && playerDeathRespawnText.Contains("enableTimers(character);")
                && playerDeathRespawnText.Contains("tryCompleteCurrentPlayfieldRespawn(dynel, destination, character.RawHeading, destinationPlayfield)")
                && playerDeathRespawnText.Contains("transferToRespawnPlayfield(dynel, destination, character.RawHeading, destinationPlayfield);"),
                "PlayfieldPlayerDeathRespawnRuntimeService must own player death/respawn packet-state sequencing.");
            AssertTextBefore(
                playerDeathRespawnText,
                "logCorpseVisualSkipped(character, corpseIdentity);",
                "sendDeathSocialStatus(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "sendDeathSocialStatus(character);",
                "markPlayerRespawned(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "markPlayerRespawned(character);",
                "sendDeathRespawnStateStats(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "sendDeathRespawnStateStats(character);",
                "stopMovement(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "stopMovement(character);",
                "cleanupDeathCombat(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "cleanupDeathCombat(character);",
                "sendChangedStats(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "sendChangedStats(character);",
                "logRespawnRequested(character, corpseIdentity, destinationPlayfield, destination);");
            AssertTextBefore(
                playerDeathRespawnText,
                "logRespawnRequested(character, corpseIdentity, destinationPlayfield, destination);",
                "enableTimers(character);");
            AssertTextBefore(
                playerDeathRespawnText,
                "enableTimers(character);",
                "if (tryCompleteCurrentPlayfieldRespawn(dynel, destination, character.RawHeading, destinationPlayfield))");
            Assert.IsFalse(
                playerDeathRespawnText.Contains("SendCompressed")
                || playerDeathRespawnText.Contains("TeleportMessageHandler")
                || playerDeathRespawnText.Contains("ZoneRedirectionMessage")
                || playerDeathRespawnText.Contains("FullCharacterMessageHandler")
                || playerDeathRespawnText.Contains("SimpleCharFullUpdate")
                || playerDeathRespawnText.Contains("Stats[")
                || playerDeathRespawnText.Contains("Pool.Instance")
                || playerDeathRespawnText.Contains("PlayfieldById")
                || playerDeathRespawnText.Contains("Announce("),
                "PlayfieldPlayerDeathRespawnRuntimeService must not own packet construction, object lookup, stats algorithms, or transport.");
            Assert.IsTrue(
                statelTransitionText.Contains("internal sealed class PlayfieldStatelTransitionRuntimeService")
                && statelTransitionText.Contains("internal void CheckStatelCollision(")
                && statelTransitionText.Contains("internal void PrimeStatelCollisionContacts(")
                && statelTransitionText.Contains("internal void ClearContactState(int dynelId)")
                && statelTransitionText.Contains("internal static void ArmPostZoneCollisionGrace(ICharacter character)")
                && statelTransitionText.Contains("private bool TryHandleCapturedMontroyalPrivateCityEntry(")
                && statelTransitionText.Contains("private bool TryHandleUserConfirmedMontroyalPrivateCityExit(")
                && statelTransitionText.Contains("ev.Perform(dynel, sd);"),
                "PlayfieldStatelTransitionRuntimeService must own statel contact, grace, event, and private-city transition orchestration.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.CheckStatelCollision(")
                && playfieldText.Contains("this.runtimeSystems.PrimeStatelCollisionContacts(dynel, this.collisionStatels);")
                && playfieldText.Contains("this.runtimeSystems.ClearStatelTransitionContactState(dynelId);")
                && playfieldText.Contains("PlayfieldStatelTransitionRuntimeService.ArmPostZoneCollisionGrace(character);"),
                "Playfield must delegate statel collision/contact/grace orchestration through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                playfieldText.Contains("private readonly Dictionary<int, HashSet<string>> statelEnterContacts")
                || playfieldText.Contains("private readonly HashSet<int> statelCollisionInitializedCharacters")
                || playfieldText.Contains("private static readonly Dictionary<int, DateTime> postZoneCollisionGraceUntil")
                || playfieldText.Contains("private bool TryHandleCapturedMontroyalPrivateCityEntry")
                || playfieldText.Contains("private bool TryHandleUserConfirmedMontroyalPrivateCityExit")
                || playfieldText.Contains("private static string BuildStatelContactKey")
                || playfieldText.Contains("private static bool IsInStatelCollisionRange"),
                "Playfield must not retain moved statel transition orchestration state or helpers.");
            Assert.IsFalse(
                statelTransitionText.Contains("TeleportMessageHandler")
                || statelTransitionText.Contains("ZoneRedirectionMessage")
                || statelTransitionText.Contains("SendCompressed")
                || statelTransitionText.Contains("PlayfieldLoader")
                || statelTransitionText.Contains("OrganizationDao")
                || statelTransitionText.Contains("new Identity"),
                "PlayfieldStatelTransitionRuntimeService must not own packet construction, transport, playfield lookup, DB lookup, or handoff identity construction.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldWallCollisionRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldWallCollisionRuntimeService.");
            Assert.IsTrue(
                wallCollisionText.Contains("internal sealed class PlayfieldWallCollisionRuntimeService")
                && wallCollisionText.Contains("internal void CheckWallCollision(")
                && wallCollisionText.Contains("isPostZoneCollisionGraceActive(dynel)")
                && wallCollisionText.Contains("WallCollision.CheckCollision(")
                && wallCollisionText.Contains("PlayfieldLoader.PFData.ContainsKey(destPlayfield)")
                && wallCollisionText.Contains("PlayfieldDestination dest = destinationPlayfieldData.Destinations[destinationIndex];")
                && wallCollisionText.Contains("float dist = WallCollision.Distance(")
                && wallCollisionText.Contains("teleportToPlayfield("),
                "PlayfieldWallCollisionRuntimeService must own wall-collision routing and destination-coordinate orchestration.");
            Assert.IsFalse(
                wallCollisionText.Contains("TeleportMessageHandler")
                || wallCollisionText.Contains("ZoneRedirectionMessage")
                || wallCollisionText.Contains("SendCompressed")
                || wallCollisionText.Contains("new Identity")
                || wallCollisionText.Contains("Pool.Instance"),
                "PlayfieldWallCollisionRuntimeService must not own packet construction, transport, identity construction, or Pool lookup.");
            Assert.IsTrue(
                timedLifecycleText.Contains("internal sealed class PlayfieldTimedLifecycleRuntimeService")
                && timedLifecycleText.Contains("internal void ProcessHeartbeatLifecycle(")
                && timedLifecycleText.Contains("processPendingCorpseSpawns();")
                && timedLifecycleText.Contains("processCorpseDespawns();")
                && timedLifecycleText.Contains("processPendingCorpseCreditAwards();")
                && timedLifecycleText.Contains("xx.InPlayfield(playfieldIdentity)")
                && timedLifecycleText.Contains("hasPendingDeadNpcDespawn(xx.Identity)")
                && timedLifecycleText.Contains("if (dynel.Starting)")
                && timedLifecycleText.Contains("if (processDeadNpcDespawn(dynel))")
                && timedLifecycleText.Contains("if (dynel.DoNotDoTimers)")
                && timedLifecycleText.Contains("processRegeneration(dynel);")
                && timedLifecycleText.Contains("processCombatTick(dynel);")
                && timedLifecycleText.Contains("processNpcPatrolTick(dynel);")
                && timedLifecycleText.Contains("processFollow(dynel);")
                && timedLifecycleText.Contains("processPlayerCollision(dynel);"),
                "PlayfieldTimedLifecycleRuntimeService must own heartbeat lifecycle sequencing.");
            Assert.IsFalse(
                timedLifecycleText.Contains("Stats[")
                || timedLifecycleText.Contains("SendChangedStats")
                || timedLifecycleText.Contains("DoCombatTick")
                || timedLifecycleText.Contains("CheckStatelCollision")
                || timedLifecycleText.Contains("Announce(")
                || timedLifecycleText.Contains("AttackInfo")
                || timedLifecycleText.Contains("CorpseFullUpdate")
                || timedLifecycleText.Contains("Inventory"),
                "PlayfieldTimedLifecycleRuntimeService must not own algorithms, packets, collision internals, or inventory.");

            string heartbeatTimer = ExtractMethodBlock(playfieldText, "private void HeartBeatTimer");
            Assert.IsTrue(
                heartbeatTimer.Contains("this.runtimeSystems.ProcessHeartbeatTimedLifecycle(")
                && heartbeatTimer.Contains("this.ProcessPendingCorpseSpawns")
                && heartbeatTimer.Contains("this.ProcessCorpseDespawns")
                && heartbeatTimer.Contains("this.ProcessPendingCorpseCreditAwards")
                && heartbeatTimer.Contains("this.runtimeSystems.ProcessCharacterRegeneration(dynel, SendChangedStats)")
                && heartbeatTimer.Contains("this.DoCombatTick")
                && heartbeatTimer.Contains("this.runtimeSystems.ProcessCharacterFollow")
                && heartbeatTimer.Contains("this.runtimeSystems.ProcessPlayerCollisionChecks("),
                "Playfield heartbeat must delegate timed lifecycle sequencing through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                heartbeatTimer.Contains("foreach (ICharacter dynel in dynels)")
                || heartbeatTimer.Contains("this.runtimeSystems.ProcessDeadNpcDespawn(dynel)")
                || heartbeatTimer.Contains("this.runtimeSystems.ProcessNpcPatrolTick(dynel)"),
                "Playfield heartbeat must not directly own character lifecycle loop sequencing.");
            Assert.IsTrue(
                !playfieldText.Contains("private void ProcessCharacterRegeneration(ICharacter dynel)")
                && !playfieldText.Contains("private void ProcessCharacterFollow(ICharacter dynel)")
                && !playfieldText.Contains("private void ProcessPlayerCollisionChecks(ICharacter dynel)")
                && characterHeartbeatText.Contains("dynel.Stats[StatIds.health].Value")
                && characterHeartbeatText.Contains("dynel.Controller.DoFollow();")
                && characterHeartbeatText.Contains("checkWallCollision(dynel);")
                && characterHeartbeatText.Contains("checkStatelCollision(dynel);"),
                "Playfield must delegate non-combat character heartbeat behavior to PlayfieldCharacterHeartbeatRuntimeService.");
            string checkWallCollision = ExtractMethodBlock(playfieldText, "private void CheckWallCollision(ICharacter dynel)");
            Assert.IsTrue(
                checkWallCollision.Contains("this.runtimeSystems.CheckWallCollision(")
                && checkWallCollision.Contains("PlayfieldStatelTransitionRuntimeService.IsPostZoneCollisionGraceActive")
                && checkWallCollision.Contains("this.TeleportToPlayfield"),
                "Playfield must delegate wall-collision routing while keeping post-zone grace and teleport callbacks.");
            Assert.IsFalse(
                checkWallCollision.Contains("WallCollision.CheckCollision(")
                || checkWallCollision.Contains("PlayfieldLoader.PFData")
                || checkWallCollision.Contains("WallCollision.Distance(")
                || checkWallCollision.Contains("new Identity"),
                "Playfield must not retain wall-collision destination lookup, coordinate math, or teleport identity construction.");
            string respawnPlayer = ExtractMethodBlock(playfieldText, "public void RespawnPlayer");
            Assert.IsTrue(
                respawnPlayer.Contains("this.ResolvePlayerRespawnLocation(character, out destination, out destinationPlayfield);")
                && respawnPlayer.Contains("Identity corpseIdentity = this.AllocateCorpseIdentity();")
                && respawnPlayer.Contains("this.runtimeSystems.ProcessPlayerRespawn(")
                && respawnPlayer.Contains("this.LogSkippedPlayerCorpseVisual")
                && respawnPlayer.Contains("this.TryCompleteDeathRespawnInCurrentPlayfield")
                && respawnPlayer.Contains("this.Teleport"),
                "Playfield must route player respawn sequencing through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                respawnPlayer.Contains("character.StopMovement();")
                || respawnPlayer.Contains("character.DoNotDoTimers = false;")
                || respawnPlayer.Contains("character.SendChangedStats();"),
                "Playfield RespawnPlayer must not directly own moved player respawn sequencing.");
            string teleport = ExtractMethodBlock(playfieldText, "public void Teleport");
            Assert.IsTrue(
                teleport.Contains("this.runtimeSystems.TransferToPlayfield(")
                && teleport.Contains("this.ClearPlayfieldTransferContactState")
                && teleport.Contains("CapturePlayfieldTransferEnterZoningPhase")
                && teleport.Contains("DisableTimersForPlayfieldTransfer"),
                "Playfield teleport must route transfer cleanup sequencing through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                teleport.Contains("Thread.Sleep(200)")
                || teleport.Contains("Thread.Sleep(1000)")
                || teleport.Contains("this.statelEnterContacts.Remove(dynelId)")
                || teleport.Contains("this.statelCollisionInitializedCharacters.Remove(dynelId)")
                || teleport.Contains("dynel.DoNotDoTimers = true"),
                "Playfield teleport must not directly own moved transfer cleanup sequencing.");

            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.SendPrivateCityPlayfieldReadyBlock(client, character);"),
                "Playfield must delegate private-city ready block sending through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ProcessNpcCombatTick(attacker);"),
                "Playfield must delegate NPC combat ticks through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ClearInvalidNpcCombatTarget(attacker);")
                && playfieldText.Contains("this.runtimeSystems.ClearNpcCombatTracking(identity);")
                && playfieldText.Contains("this.runtimeSystems.ClearNpcFightingTarget(character);")
                && playfieldText.Contains("this.runtimeSystems.StopDyingNpcCombatState(target);"),
                "Playfield must delegate NPC combat stop/clear orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                npcCombatTickText.Contains("this.playfield.ClearNpcCombatTracking(attacker.Identity);")
                && npcCombatTickText.Contains("this.playfield.ClearInvalidNpcCombatTarget(attacker);")
                && npcCombatTickText.Contains("if (attacker == null || this.playfield == null)"),
                "NpcCombatTickCoordinator must route NPC combat clear decisions through the runtime ownership boundary.");
            Assert.IsTrue(
                playfieldText.Contains("internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)")
                && playfieldText.Contains("return this.runtimeSystems.IsInNpcCombatRange(attacker, target, range);")
                && playfieldText.Contains("this.runtimeSystems.UpdateNpcMeleeFollowHold(")
                && playfieldText.Contains("this.runtimeSystems.TryMoveNpcIntoCombatRange(")
                && playfieldText.Contains("private void MoveNpcToCombatPosition(")
                && playfieldText.Contains("new SetPosMessage"),
                "Playfield must delegate NPC combat movement decisions while retaining SetPos packet construction.");
            Assert.IsTrue(
                npcCombatMovementText.Contains("MoveCombatPositionToward(")
                && npcCombatMovementText.Contains("EnemyBehaviorContract.MaxPlayerChaseProjectionDistance")
                && npcCombatMovementText.Contains("moveNpcToPosition(attacker, attackerPosition);")
                && npcCombatMovementText.Contains("npcController.Follow(target.Identity, stopDistance);")
                && npcCombatMovementText.Contains("npcController.StopFollow();")
                && npcCombatMovementText.Contains("logNpcBrain(\"FollowTargetStart\"")
                && npcCombatMovementText.Contains("logNpcBrain(\"FollowTargetContinue\""),
                "NPC combat movement service must own initial SetPos, continuous follow start/continuation, and stop-follow decisions.");
            string moveNpcTowardCombatTarget = ExtractMethodBlock(
                npcCombatMovementText,
                "private void MoveNpcTowardCombatTarget(");
            Assert.IsFalse(
                moveNpcTowardCombatTarget.Contains("npcController.StopFollow();")
                || moveNpcTowardCombatTarget.Contains("moveNpcToPosition(attacker, nextPosition)"),
                "Generic NPC chase must not clear follow state and warp through periodic SetPos steps.");
            Assert.IsFalse(
                playfieldText.Contains("private void MoveNpcTowardCombatTarget(")
                || playfieldText.Contains("private void MoveCapturedCleaningRobotTowardCombatTarget(")
                || playfieldText.Contains("private static AORebirth.Core.Vector.Vector3 GetCombatPosition("),
                "Playfield must not retain moved NPC combat movement helpers.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void ClearInvalidCombatTarget(ICharacter attacker)")
                && npcRuntimeText.Contains("internal void ClearFightingTarget(ICharacter character)")
                && npcRuntimeText.Contains("internal void StopDyingNpcCombatState(ICharacter target)")
                && npcRuntimeText.Contains("character.SetFightingTarget(Identity.None);")
                && npcRuntimeText.Contains("target.SetTarget(Identity.None);")
                && npcRuntimeText.Contains("npcController.StopFollow();"),
                "NPCRuntimeService must own NPC combat stop/clear state orchestration.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void BeginNpcDeath(ICharacter attacker, ICharacter target)")
                && npcRuntimeText.Contains("this.corpseLifecycle.HasPendingDeadNpcDespawn(target.Identity)")
                && npcRuntimeText.Contains("this.playfield.MarkNpcDead(target);")
                && npcRuntimeText.Contains("this.playfield.StopFightingDeadTarget(target.Identity);")
                && npcRuntimeText.Contains("this.playfield.StopDyingNpcCombatState(target);")
                && npcRuntimeText.Contains("this.playfield.SendNpcDeathAnimation(target);")
                && npcRuntimeText.Contains("this.rewards.RunNpcDeathRewardHooks(attacker, target, this.playfield.AwardCombatXp);")
                && npcRuntimeText.Contains("this.ScheduleNpcDeathCorpseSpawn(target, corpseIdentity);")
                && npcRuntimeText.Contains("this.ScheduleDeadNpcDespawn(target);"),
                "NPCRuntimeService must own NPC death lifecycle orchestration order.");
            Assert.IsTrue(
                rewardRuntimeText.Contains("internal sealed class PlayfieldRewardRuntimeService")
                && rewardRuntimeText.Contains("internal void RunNpcDeathRewardHooks(")
                && rewardRuntimeText.Contains("RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);")
                && rewardRuntimeText.Contains("awardCombatXp(attacker, target);"),
                "PlayfieldRewardRuntimeService must own named NPC death reward hook orchestration.");
            AssertTextBefore(
                rewardRuntimeText,
                "RexB18CObjectiveProgressTracker.TryObserveNpcDeath(attacker, target);",
                "awardCombatXp(attacker, target);");
            Assert.IsFalse(
                npcRuntimeText.Contains("RexB18CObjectiveProgressTracker.TryObserveNpcDeath")
                || npcRuntimeText.Contains("private void RunNpcDeathRewardHooks"),
                "NPCRuntimeService must delegate quest/XP reward hook orchestration to PlayfieldRewardRuntimeService.");
            Assert.IsFalse(
                rewardRuntimeText.Contains("CalculateCombatXpReward")
                || rewardRuntimeText.Contains("SendCompressed")
                || rewardRuntimeText.Contains("Stats.Write")
                || rewardRuntimeText.Contains("RollCorpseLootItems")
                || rewardRuntimeText.Contains("AwardCorpseCredits"),
                "PlayfieldRewardRuntimeService must not own XP algorithms, packet emission, persistence, loot, or credits.");
            Assert.IsTrue(
                npcRuntimeText.Contains("private void ScheduleNpcDeathCorpseSpawn(ICharacter target, Identity corpseIdentity)")
                && npcRuntimeText.Contains("this.playfield.ScheduleCorpseSpawn(target, corpseIdentity);")
                && npcRuntimeText.Contains("Skipping corpse visual spawn for {0}; no known MonsterData-to-CATMesh mapping."),
                "NPCRuntimeService must own named NPC death corpse spawn hook orchestration.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal bool ProcessDeadNpcDespawn(ICharacter character)")
                && npcRuntimeText.Contains("this.corpseLifecycle.TryGetDeadNpcDespawn(character.Identity, out despawnTick)")
                && npcRuntimeText.Contains("this.BeginNpcDeath(null, character);")
                && npcRuntimeText.Contains("this.FinalizeNpcDespawn(character);"),
                "NPCRuntimeService must own dead NPC processing orchestration.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void ScheduleNpcCorpseDespawn(Identity corpseIdentity, DateTime expiresAtUtc)")
                && npcRuntimeText.Contains("internal void ProcessDueNpcCorpseDespawns(DateTime utcNow, Action<int> despawnCorpse)")
                && npcRuntimeText.Contains("this.corpseDespawnTicks")
                && npcRuntimeText.Contains("despawnCorpse(corpseInstance);")
                && npcRuntimeText.Contains("internal void ClearNpcCorpseDespawn(int corpseInstance)")
                && npcRuntimeText.Contains("private void ScheduleDeadNpcDespawn(ICharacter target)")
                && npcRuntimeText.Contains("this.corpseLifecycle.ScheduleDeadNpcDespawn(target);"),
                "NPCRuntimeService must expose named NPC corpse/despawn timing orchestration methods.");
            Assert.IsTrue(
                corpseLifecycleText.Contains("internal void ScheduleDeadNpcDespawn(ICharacter target)")
                && corpseLifecycleText.Contains("internal bool TryGetDeadNpcDespawn(Identity identity, out DateTime despawnTick)")
                && corpseLifecycleText.Contains("this.deadNpcDespawnTicks[target.Identity.Instance]"),
                "NpcCorpseLifecycleCoordinator must remain the dead-NPC timing state helper.");
            Assert.IsFalse(
                corpseLifecycleText.Contains("this.playfield.MarkNpcDead(target);")
                || corpseLifecycleText.Contains("RexB18CObjectiveProgressTracker.TryObserveNpcDeath")
                || corpseLifecycleText.Contains("this.playfield.ScheduleCorpseSpawn(target, corpseIdentity);"),
                "NpcCorpseLifecycleCoordinator must not own NPC death lifecycle orchestration.");
            Assert.IsTrue(
                attackHandlerText.Contains("playfield.AcquireNpcAggro(character, target);")
                && playfieldText.Contains("this.runtimeSystems.AcquireNpcAggro(attacker, target);"),
                "Attack handling must route NPC aggro acquisition through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                attackHandlerText.Contains("target.SetFightingTarget(character.Identity);")
                || attackHandlerText.Contains("NpcAiProfiles.CanRetaliate"),
                "AttackMessageHandler must not own NPC aggro acquisition rules.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void AcquireAggro(ICharacter attacker, ICharacter target)")
                && npcRuntimeText.Contains("NpcAiProfiles.CanRetaliate(npcController.AiProfile)")
                && npcRuntimeText.Contains("this.StartCombatWithAcquiredTarget(attacker, target, capturedContract);")
                && npcRuntimeText.Contains("private void StartCombatWithAcquiredTarget(")
                && npcRuntimeText.Contains("target.SetFightingTarget(attacker.Identity);")
                && npcRuntimeText.Contains("npcController.StopFollowForCombatRange(attacker.Coordinates().coordinate);")
                && npcRuntimeText.Contains("this.ResetCombatTick(target);"),
                "NPCRuntimeService must own NPC aggro acquisition, patrol cancellation, and combat-start orchestration.");
            Assert.IsTrue(
                timedLifecycleText.Contains("processNpcPatrolTick(dynel);"),
                "Timed lifecycle scheduling must delegate NPC patrol ticks through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void ProcessPatrolTick(ICharacter character)")
                && npcRuntimeText.Contains("if (character.FightingTarget.Instance != 0)")
                && npcRuntimeText.Contains("character.Controller.DoFollow();")
                && npcRuntimeText.Contains("character.Controller.StartPatrolling();"),
                "NPCRuntimeService must keep combat follow active while preventing patrol replay during combat.");
            Assert.IsTrue(
                npcCombatTickText.Contains("maintainMovementDuringRecharge")
                && npcCombatTickText.Contains("!this.playfield.IsInCombatRange(attacker, target, attackSource.Range)")
                && npcCombatTickText.Contains("this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);")
                && npcCombatTickText.Contains("this.playfield.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);")
                && npcCombatTickText.Contains(
                    "npcController.StopFollowForCapturedCombatRange(target.Coordinates().coordinate);"),
                "Captured and existing known combat paths must maintain chase and melee hold while Thief preserves its delayed transition.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.SpawnCapturedNpcContent(playfieldIdentity);"),
                "Playfield must delegate captured NPC spawn orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.MaterializeStartupObjects(")
                && runtimeSystemsText.Contains("this.ActivateNpc,")
                && runtimeSystemsText.Contains("this.objectMaterialization.MaterializeStartupObjects("),
                "Playfield must delegate DB-spawned NPC activation through PlayfieldRuntimeSystems materialization callbacks.");
            Assert.IsFalse(
                playfieldText.Contains("this.runtimeSystems.RegisterDynel(cmob);"),
                "Playfield must not route DB-spawned NPC activation through the generic dynel registration path.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.RegisterNpcHome(character);"),
                "Playfield must delegate NPC home registration through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                playfieldText.Contains("this.runtimeSystems.RemoveNpcHome(identity);"),
                "Playfield must not own NPC home removal after NPCRuntimeService callback wiring.");
            Assert.IsFalse(
                runtimeSystemsText.Contains("this.npcRuntime.RemoveNpcHome(identity);"),
                "PlayfieldRuntimeSystems must not expose unused NPC home removal after callback wiring.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.DespawnNpcImmediately("),
                "Playfield must delegate immediate NPC despawn through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                runtimeSystemsText.Contains("RemoveNpcImmediately")
                || npcRuntimeText.Contains("RemoveNpcImmediately")
                || playfieldText.Contains("this.runtimeSystems.RemoveNpcImmediately("),
                "Immediate NPC despawn APIs must use despawn naming instead of generic removal naming.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ProcessDueNpcCorpseDespawns(DateTime.UtcNow, this.DespawnCorpse);")
                && playfieldText.Contains("this.runtimeSystems.ProcessPendingCorpseSpawns(")
                && playfieldText.Contains("this.runtimeSystems.DespawnCorpses(")
                && playfieldText.Contains("this.runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);")
                && playfieldText.Contains("this.runtimeSystems.ScheduleNpcCorpseDespawn(corpse.CorpseIdentity, expiresAtUtc);")
                && playfieldText.Contains("this.runtimeSystems.DespawnCorpse("),
                "Playfield must delegate corpse spawn/despawn scheduling, due checks, and cleanup ordering through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                objectLifecycleText.Contains("internal int DespawnCorpses<TCorpseState>(")
                && objectLifecycleText.Contains("pendingCorpseSpawns.Remove(deadNpcIdentity(corpse).Instance);")
                && objectLifecycleText.Contains("despawnCorpse(corpseInstance);"),
                "PlayfieldObjectLifecycleRuntimeService must own explicit corpse-despawn predicate routing.");
            AssertTextBefore(
                objectLifecycleText,
                "pendingCorpseSpawns.Remove(deadNpcIdentity(corpse).Instance);",
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
                && objectLifecycleText.Contains("registerCorpse(target, corpseId);")
                && objectLifecycleText.Contains("traceCorpseFullUpdate(corpseId, deadNpcId);")
                && objectLifecycleText.Contains("sendCorpseFullUpdate(target, corpseId);"),
                "PlayfieldObjectLifecycleRuntimeService must own pending corpse spawn callback ordering.");
            AssertTextBefore(
                objectLifecycleText,
                "registerCorpse(target, corpseId);",
                "traceCorpseFullUpdate(corpseId, deadNpcId);");
            AssertTextBefore(
                objectLifecycleText,
                "traceCorpseFullUpdate(corpseId, deadNpcId);",
                "sendCorpseFullUpdate(target, corpseId);");
            string processPendingCorpseSpawns = ExtractMethodBlock(playfieldText, "private void ProcessPendingCorpseSpawns");
            Assert.IsTrue(
                processPendingCorpseSpawns.Contains("this.runtimeSystems.ProcessPendingCorpseSpawns(")
                && processPendingCorpseSpawns.Contains("this.RegisterCorpse")
                && processPendingCorpseSpawns.Contains("this.TraceCorpseFullUpdate")
                && processPendingCorpseSpawns.Contains("this.SendCorpseFullUpdate"),
                "Playfield must delegate pending corpse spawn orchestration and keep packet/loot callbacks.");
            Assert.IsFalse(
                processPendingCorpseSpawns.Contains("foreach (CorpseState corpse")
                || processPendingCorpseSpawns.Contains("this.pendingCorpseSpawns.Remove"),
                "Playfield must not own pending corpse spawn loop orchestration.");
            Assert.IsTrue(
                playfieldText.Contains("private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)")
                && playfieldText.Contains("client.SendCompressed(")
                && playfieldText.Contains("CorpseFullUpdate.Build("),
                "Playfield intentionally keeps corpse packet emission outside NPCRuntimeService.");
            Assert.IsTrue(
                playfieldText.Contains("private void RegisterCorpse(ICharacter target, Identity corpseIdentity)")
                && playfieldText.Contains("private void DespawnCorpse(int corpseInstance)")
                && playfieldText.Contains("this.corpses[corpseIdentity.Instance] = state;")
                && playfieldText.Contains("x => this.corpses.Remove(x)")
                && playfieldText.Contains("x => this.pendingCorpseCreditAwards.Remove(x)"),
                "Playfield intentionally keeps corpse state storage while object lifecycle owns despawn cleanup order.");
            Assert.IsTrue(
                playfieldText.Contains("private List<CorpseLootItem> RollCorpseLootItems(ICharacter target)")
                && playfieldText.Contains("private static int RollCorpseCredits(ICharacter target)")
                && playfieldText.Contains("private void SendCorpseInventoryUpdate(ICharacter looter, CorpseState corpse)")
                && playfieldText.Contains("private void AwardCorpseCredits(ICharacter looter, CorpseState corpse)"),
                "Playfield intentionally keeps loot, credit, and corpse container construction outside NPCRuntimeService.");
            Assert.IsTrue(
                corpseAccessText.Contains("internal sealed class PlayfieldCorpseAccessRuntimeService")
                && corpseAccessText.Contains("internal bool TryUseCorpse<TCorpseState>(")
                && corpseAccessText.Contains("internal bool TryUseDeadNpcCorpse<TCorpseState>(")
                && corpseAccessText.Contains("internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(")
                && corpseAccessText.Contains("internal void ProcessPendingCorpseCreditAwards<TAward, TCorpseState>("),
                "PlayfieldCorpseAccessRuntimeService must own corpse use, loot, and pending credit orchestration entry points.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.TryUseCorpse(")
                && playfieldText.Contains("this.runtimeSystems.TryUseDeadNpcCorpse(")
                && playfieldText.Contains("this.runtimeSystems.TryLootCorpseItem(")
                && playfieldText.Contains("this.runtimeSystems.ProcessPendingCorpseCreditAwards("),
                "Playfield must route corpse access and loot orchestration through PlayfieldRuntimeSystems.");
            AssertTextBefore(
                corpseAccessText,
                "sendCorpseInventoryUpdate(looter, corpse);",
                "scheduleCorpseCreditAward(looter, corpse);");
            AssertTextBefore(
                corpseAccessText,
                "setLooted(corpseLootItem, true);",
                "sendCorpseContainerAddItem(looter, sourceContainer, transferResult.TargetSlot);");
            Assert.IsFalse(
                playfieldText.Contains("private void SendCorpseInventoryUpdateAndCredits"),
                "Playfield must not keep the combined corpse inventory/credits orchestration helper.");
            Assert.IsFalse(
                corpseAccessText.Contains("SendCompressed")
                || corpseAccessText.Contains("InventoryUpdateMessage")
                || corpseAccessText.Contains("ContainerAddItemMessage")
                || corpseAccessText.Contains("new Item(")
                || corpseAccessText.Contains("BaseInventory")
                || corpseAccessText.Contains("Stats.Write")
                || corpseAccessText.Contains("RollCorpseLootItems")
                || corpseAccessText.Contains("RollCorpseCredits")
                || corpseAccessText.Contains("AwardCorpseCredits"),
                "PlayfieldCorpseAccessRuntimeService must not own packet construction, item materialization, inventory algorithms, persistence, loot, or credit math.");
            Assert.IsFalse(
                npcRuntimeText.Contains("SendCompressed")
                || npcRuntimeText.Contains("CorpseFullUpdate.Build(")
                || npcRuntimeText.Contains("this.corpses[")
                || npcRuntimeText.Contains("RollCorpseLootItems")
                || npcRuntimeText.Contains("RollCorpseCredits")
                || npcRuntimeText.Contains("SendCorpseInventoryUpdate")
                || npcRuntimeText.Contains("AwardCorpseCredits"),
                "NPCRuntimeService must not own packet emission, corpse storage, loot, credits, or corpse containers.");
            Assert.IsFalse(
                objectLifecycleText.Contains("CorpseFullUpdate.Build(")
                || objectLifecycleText.Contains("RollCorpseLootItems")
                || objectLifecycleText.Contains("RollCorpseCredits")
                || objectLifecycleText.Contains("SendCorpseInventoryUpdate")
                || objectLifecycleText.Contains("AwardCorpseCredits")
                || objectLifecycleText.Contains("InventoryUpdateMessage"),
                "PlayfieldObjectLifecycleRuntimeService must not own packet emission, loot, credits, or inventory containers.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.BeginNpcDeath(attacker, target);"),
                "Playfield must delegate NPC corpse lifecycle start through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.ProcessDeadNpcDespawn")
                && timedLifecycleText.Contains("if (processDeadNpcDespawn(dynel))"),
                "Timed lifecycle scheduling must delegate dead NPC processing through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldRuntimeSystems.cs")
                && projectText.Contains(@"Core\Playfields\NPCRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldObjectLifecycleRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldCorpseAccessRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldRewardRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldRuntimeSystems, NPCRuntimeService, object lifecycle, corpse access, statel transition, and reward runtime services.");

            string immediateRemove = ExtractMethodBlock(npcRuntimeText, "internal void DespawnNpcImmediately");
            Assert.IsTrue(
                immediateRemove.Contains("target == null || target.Identity.Type != IdentityType.CanbeAffected"),
                "NPCRuntimeService must preserve the immediate NPC removal guard.");
            int stopFightIndex = immediateRemove.IndexOf(
                "stopFightingDeadTarget(target.Identity);",
                StringComparison.Ordinal);
            int cancelCorpseIndex = immediateRemove.IndexOf(
                "cancelPendingCorpseSpawn(target.Identity);",
                StringComparison.Ordinal);
            int finalizeIndex = immediateRemove.IndexOf(
                "this.FinalizeNpcDespawn(target);",
                StringComparison.Ordinal);
            Assert.IsTrue(
                stopFightIndex >= 0 && stopFightIndex < cancelCorpseIndex && cancelCorpseIndex < finalizeIndex,
                "Immediate NPC removal must preserve stop-fight, pending-corpse cancellation, then final despawn order.");

            string playfieldImmediateRemove = ExtractMethodBlock(playfieldText, "public void DespawnNpcImmediately");
            Assert.IsFalse(
                playfieldImmediateRemove.Contains("this.StopFightingDeadTarget(target.Identity);")
                || playfieldImmediateRemove.Contains("this.pendingCorpseSpawns.Remove(target.Identity.Instance);")
                || playfieldImmediateRemove.Contains("this.runtimeSystems.FinalizeNpcDespawn(target);"),
                "Playfield DespawnNpcImmediately must not own immediate NPC removal sequencing.");
        }

        [TestMethod]
        public void CorpseLootCreditGuardrailPreservesAccessTransferAndCreditOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string corpseAccessText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldCorpseAccessRuntimeService.cs"));
            string corpseInteractionRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CorpseInteractionRules.cs"));
            string inventoryRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs"));

            string playfieldUseCorpse = ExtractMethodBlock(playfieldText, "public bool TryUseCorpse");
            string playfieldLootCorpseItem = ExtractMethodBlock(playfieldText, "public bool TryLootCorpseItem");
            string playfieldPendingCredits = ExtractMethodBlock(playfieldText, "private void ProcessPendingCorpseCreditAwards");
            string corpseUse = ExtractMethodBlock(corpseAccessText, "internal bool TryUseCorpse<TCorpseState>(");
            string corpseLoot = ExtractMethodBlock(corpseAccessText, "internal bool TryLootCorpseItem<TCorpseState, TCorpseLootItem>(");
            string inventoryAndCredits =
                ExtractMethodBlock(corpseAccessText, "private void SendCorpseInventoryUpdateAndCredits<TCorpseState>(");
            string sendCorpseInventoryUpdate =
                ExtractMethodBlock(playfieldText, "private void SendCorpseInventoryUpdate");
            string sendCorpseContainerAddItem =
                ExtractMethodBlock(playfieldText, "private void SendCorpseContainerAddItem");
            string scheduleCorpseCreditAward =
                ExtractMethodBlock(playfieldText, "private void ScheduleCorpseCreditAward");
            string awardCorpseCredits = ExtractMethodBlock(playfieldText, "private void AwardCorpseCredits");
            string sendStatChangedMessage = ExtractMethodBlock(playfieldText, "private static void SendStatChangedMessage");

            Assert.IsTrue(
                playfieldUseCorpse.Contains("this.runtimeSystems.TryUseCorpse(")
                && playfieldUseCorpse.Contains("this.SendCorpseInventoryUpdate")
                && playfieldUseCorpse.Contains("this.ScheduleCorpseCreditAward"),
                "Playfield must delegate corpse access sequencing while retaining packet and credit callbacks.");
            Assert.IsTrue(
                corpseUse.Contains("this.SendCorpseInventoryUpdateAndCredits(")
                && corpseUse.Contains("if (hasUnlootedItems(corpse))")
                && corpseUse.Contains("else"),
                "Corpse access service must use the captured InventoryUpdate open path for item-bearing and empty corpses.");
            Assert.IsFalse(
                corpseUse.Contains("sendCorpseLootAccessAction")
                || corpseUse.Contains("sendUseActionFinished")
                || corpseUse.Contains("NextUseSendsAccessActionOnly"),
                "Corpse Use must not take the old unproven action-only path instead of the captured InventoryUpdate open path.");
            AssertTextBefore(
                inventoryAndCredits,
                "sendCorpseInventoryUpdate(looter, corpse);",
                "scheduleCorpseCreditAward(looter, corpse);");
            Assert.IsTrue(
                playfieldText.Contains("private static readonly TimeSpan CorpseCreditAwardDelay = TimeSpan.FromMilliseconds(500);")
                && corpseInteractionRulesText.Contains("public const int CorpseUseAcknowledgeDelayMilliseconds = 550;"),
                "Capture-backed corpse credit payout must stay after InventoryUpdate and before the delayed GenericCmd success ack.");

            Assert.IsTrue(
                playfieldLootCorpseItem.Contains("this.runtimeSystems.TryLootCorpseItem(")
                && playfieldLootCorpseItem.Contains("this.runtimeSystems.CharacterHasUniqueItemAlready")
                && playfieldLootCorpseItem.Contains("this.runtimeSystems.TryAddCorpseLootItem")
                && playfieldLootCorpseItem.Contains("this.SendCorpseContainerAddItem"),
                "Playfield must delegate corpse item transfer sequencing while retaining packet callbacks.");
            AssertTextBefore(
                corpseLoot,
                "characterHasUniqueItemAlready(looter, item)",
                "tryAddCorpseLootItem(looter, item, targetPlacement)");
            AssertTextBefore(
                corpseLoot,
                "tryAddCorpseLootItem(looter, item, targetPlacement)",
                "setLooted(corpseLootItem, true);");
            AssertTextBefore(corpseLoot, "setLooted(corpseLootItem, true);", "setOpened(corpse, true);");
            AssertTextBefore(
                corpseLoot,
                "setOpened(corpse, true);",
                "sendCorpseContainerAddItem(looter, sourceContainer, transferResult.TargetSlot);");
            Assert.IsTrue(
                corpseLoot.Contains("sourceContainer.Type != IdentityType.Backpack")
                && corpseLoot.Contains("int corpseInventoryHandleValue = (sourceContainer.Instance >> 16) & 0xffff;")
                && corpseLoot.Contains("int requestedLootSlot = sourceContainer.Instance & 0xffff;"),
                "Corpse loot transfer must accept the opened corpse container source encoding and decode handle plus slot.");
            Assert.IsTrue(
                corpseLoot.Contains("if (corpseLootItem == null)")
                && corpseLoot.Contains("sendUseActionFinished(looter);"),
                "Missing, already-looted, or empty corpse slots must fail safely without producing items.");
            AssertTextBefore(
                corpseLoot,
                "CorpseLootInventoryTransferResult transferResult = tryAddCorpseLootItem(looter, item, targetPlacement);",
                "setLooted(corpseLootItem, true);");
            Assert.IsTrue(
                inventoryRuntimeText.Contains("public bool CharacterHasUniqueItemAlready(")
                && inventoryRuntimeText.Contains("public CorpseLootInventoryTransferResult TryAddCorpseLootItem(")
                && runtimeSystemsText.Contains("return this.inventoryContainer.CharacterHasUniqueItemAlready(character, item);")
                && runtimeSystemsText.Contains("return this.inventoryContainer.TryAddCorpseLootItem(looter, item, targetPlacement);"),
                "InventoryContainerRuntimeService must own unique validation and inventory insertion helpers.");

            Assert.IsTrue(
                playfieldPendingCredits.Contains("this.runtimeSystems.ProcessPendingCorpseCreditAwards(")
                && playfieldPendingCredits.Contains("this.pendingCorpseCreditAwards")
                && playfieldPendingCredits.Contains("this.AwardCorpseCredits"),
                "Playfield must delegate due credit-award iteration through runtime systems while retaining the credit award callback.");
            Assert.IsTrue(
                scheduleCorpseCreditAward.Contains("this.pendingCorpseCreditAwards.ContainsKey(corpse.CorpseIdentity.Instance)")
                && scheduleCorpseCreditAward.Contains("corpse.CreditsLooted || corpse.Credits <= 0")
                && scheduleCorpseCreditAward.Contains("this.pendingCorpseCreditAwards[corpse.CorpseIdentity.Instance]"),
                "Playfield must keep pending corpse credit storage ownership and must not schedule duplicate or zero-credit payouts.");
            Assert.IsTrue(
                awardCorpseCredits.Contains("corpse.CreditsLooted = true;")
                && awardCorpseCredits.Contains("CashStatRules.Clamp")
                && awardCorpseCredits.Contains("looter.Stats[StatIds.cash].Set((uint)cashAfter);")
                && awardCorpseCredits.Contains("this.runtimeSystems.SendChangedStatsIfClient(")
                && sendStatChangedMessage.Contains("StatMessageHandler.Default.SendChanged(character);")
                && awardCorpseCredits.Contains("looter.Stats.Write();"),
                "Playfield must keep corpse credit mutation, stat packet callback, and persistence ownership.");
            Assert.IsFalse(
                awardCorpseCredits.Contains("FormatFeedbackMessage")
                || awardCorpseCredits.Contains("ChatTextMessageHandler")
                || awardCorpseCredits.Contains("SendRewardFeedback")
                || awardCorpseCredits.Contains("StatIds.xp")
                || awardCorpseCredits.Contains("UnsavedXP"),
                "Corpse credit payout must be Cash stat only; capture did not prove chat feedback or XP from corpse interaction.");
            Assert.IsFalse(
                corpseLoot.Contains("AwardCorpseCredits")
                || corpseLoot.Contains("Stats[StatIds.cash].Set")
                || corpseLoot.Contains("CashStatRules"),
                "Item loot transfer must not independently award corpse credits.");

            Assert.IsTrue(
                playfieldText.Contains("private readonly Dictionary<int, CorpseState> corpses")
                && playfieldText.Contains("private readonly Dictionary<int, PendingCorpseCreditAward> pendingCorpseCreditAwards"),
                "Playfield must keep corpse and pending credit state storage for now.");
            Assert.IsTrue(
                sendCorpseInventoryUpdate.Contains("new InventoryUpdateMessage")
                && sendCorpseContainerAddItem.Contains("new ContainerAddItemMessage")
                && sendCorpseInventoryUpdate.Contains("NumberOfSlots = CombatCorpseRules.CorpseInventorySlots")
                && sendCorpseInventoryUpdate.Contains("Unknown1 = 2")
                && sendCorpseInventoryUpdate.Contains("BagIdentity = corpse.CorpseIdentity")
                && sendCorpseInventoryUpdate.Contains("SlotnumberInMainInventory = corpse.InventoryHandle")
                && sendCorpseInventoryUpdate.Contains("Unknown2 = 1"),
                "Playfield must keep corpse packet construction ownership for now.");
            Assert.IsTrue(
                sendCorpseInventoryUpdate.Contains("corpse.LootItems == null")
                && sendCorpseInventoryUpdate.Contains("new InventoryEntry[0]")
                && sendCorpseInventoryUpdate.Contains("corpse.LootItems.Where(x => !x.Looted).Select(CreateCorpseInventoryEntry).ToArray()"),
                "Empty corpses must open with zero inventory entries, while item-bearing corpses expose current unlooted corpse items.");
            Assert.IsTrue(
                playfieldText.Contains("LowId = lootItem.Item.LowID")
                && playfieldText.Contains("HighId = lootItem.Item.HighID")
                && playfieldText.Contains("Quality = lootItem.Item.Quality"),
                "Corpse InventoryUpdate entries must expose item ids and quality from corpse state.");
            Assert.IsFalse(
                playfieldText.Contains("SendCorpseCreditFeedback"),
                "Corpse credit payout must not retain an unproven chat/feedback helper.");
            Assert.IsFalse(
                corpseAccessText.Contains("InventoryUpdateMessage")
                || corpseAccessText.Contains("ContainerAddItemMessage")
                || corpseAccessText.Contains("ActionMessage")
                || corpseAccessText.Contains("SendCompressed")
                || corpseAccessText.Contains("Stats[StatIds.cash].Set")
                || corpseAccessText.Contains("Stats.Write")
                || corpseAccessText.Contains("private readonly Dictionary<int, PendingCorpseCreditAward>")
                || corpseAccessText.Contains("new PendingCorpseCreditAward")
                || corpseAccessText.Contains("new Item("),
                "PlayfieldCorpseAccessRuntimeService must not own packets, credit mutation, pending-credit storage, or item materialization.");
        }

        [TestMethod]
        public void PlayfieldContentDataProviderOwnsStaticContentDataResolution()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs"));
            string materializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
            string dbMobSpawnText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldDbMobSpawnRuntimeService.cs"));
            string staticDynelRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStaticDynelRuntimeService.cs"));
            string vendorRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVendorRuntimeService.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            Assert.IsTrue(
                providerText.Contains("internal sealed class PlayfieldContentDataProvider"),
                "PlayfieldContentDataProvider must be the named content data boundary.");
            Assert.IsTrue(
                providerText.Contains("internal List<StatelData> ResolveStatels(Identity playfieldIdentity)"),
                "Provider must own statel resolution.");
            Assert.IsTrue(
                providerText.Contains(
                    "internal bool TryResolveVendorStatels("),
                "Provider must own vendor statel resolution.");
            Assert.IsTrue(
                providerText.Contains("internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)"),
                "Provider must own collision-capable statel filtering.");
            Assert.IsTrue(
                providerText.Contains(
                    "internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)"),
                "Provider must own static dynel definition resolution.");
            Assert.IsTrue(
                providerText.Contains("PlayfieldLoader.PFData.TryGetValue"),
                "Provider must own PlayfieldLoader statel data access.");
            Assert.IsTrue(
                providerText.Contains("StaticDynelDao.Instance.GetWhere"),
                "Provider must own static dynel DB row access.");
            Assert.IsTrue(
                providerText.Contains("MessagePackZip.DeserializeData"),
                "Provider must own static dynel stat payload deserialization.");
            Assert.IsTrue(
                providerText.Contains("IdentityType.VendingMachine"),
                "Provider must preserve the existing vendor statel filter.");
            Assert.IsTrue(
                providerText.Contains("x.EventType == EventType.OnCollide")
                && providerText.Contains("x.EventType == EventType.OnEnter")
                && providerText.Contains("x.EventType == EventType.OnTargetInVicinity"),
                "Provider must preserve the existing collision statel event filter.");
            Assert.IsTrue(
                providerText.Contains("internal sealed class PlayfieldStaticDynelDefinition"),
                "Provider must expose static dynel definitions rather than spawning runtime objects.");

            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldContentDataProvider contentData"),
                "PlayfieldRuntimeSystems must own PlayfieldContentDataProvider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains(
                    "this.contentData = new PlayfieldContentDataProvider(isPrivateCityPlayfieldCandidate);"),
                "PlayfieldRuntimeSystems must construct the content data provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveStatels(playfieldIdentity);"),
                "Runtime systems must delegate statel data resolution to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains(
                    "return this.contentData.TryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels);"),
                "Runtime systems must delegate vendor statel data resolution to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveCollisionStatels(statels);"),
                "Runtime systems must delegate collision statel filtering to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveStaticDynels(playfieldIdentity);"),
                "Runtime systems must delegate static dynel data resolution to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldObjectMaterializationRuntimeService objectMaterialization")
                && runtimeSystemsText.Contains("this.objectMaterialization.MaterializeStartupObjects("),
                "Runtime systems must own startup object materialization through the materialization service.");

            string constructor = ExtractMethodBlock(playfieldText, "public Playfield(ZoneServer zoneServer, Identity playfieldIdentity)");
            AssertTextBefore(
                constructor,
                "this.runtimeSystems.ResolveStatels(playfieldIdentity)",
                "this.runtimeSystems.RegisterStatels(this.statels);");
            AssertTextBefore(
                constructor,
                "this.runtimeSystems.RegisterStatels(this.statels);",
                "this.collisionStatels = this.runtimeSystems.ResolveCollisionStatels(this.statels);");
            AssertTextBefore(
                constructor,
                "this.collisionStatels = this.runtimeSystems.ResolveCollisionStatels(this.statels);",
                "this.runtimeSystems.MaterializeStartupObjects(");
            Assert.IsFalse(
                constructor.Contains("this.LoadMobSpawns(playfieldIdentity);")
                || constructor.Contains("this.runtimeSystems.RegisterContent(playfieldIdentity);")
                || constructor.Contains("this.LoadVendors(playfieldIdentity);")
                || constructor.Contains("this.LoadStaticDynels(playfieldIdentity);")
                || constructor.Contains("this.runtimeSystems.RefreshDynelRegistry();"),
                "Playfield constructor must not directly own startup object materialization sequence.");

            AssertTextBefore(
                materializationText,
                "this.MaterializeDbMobSpawns(",
                "registerContent(playfieldIdentity);");
            AssertTextBefore(
                materializationText,
                "registerContent(playfieldIdentity);",
                "this.MaterializeVendors(");
            AssertTextBefore(
                materializationText,
                "this.MaterializeVendors(",
                "this.MaterializeStaticDynels(");
            AssertTextBefore(
                materializationText,
                "this.MaterializeStaticDynels(",
                "refreshDynelRegistry();");

            string checkStatelCollision = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision(ICharacter dynel)");
            string primeStatelCollisionContacts =
                ExtractMethodBlock(playfieldText, "private void PrimeStatelCollisionContacts(ICharacter dynel)");
            Assert.IsTrue(
                playfieldText.Contains("private readonly StatelData[] collisionStatels"),
                "Playfield must keep a provider-filtered collision statel view.");
            Assert.IsTrue(
                checkStatelCollision.Contains("this.collisionStatels"),
                "Playfield CheckStatelCollision must pass provider-filtered collision statels to runtime systems.");
            Assert.IsTrue(
                primeStatelCollisionContacts.Contains("this.collisionStatels"),
                "Playfield PrimeStatelCollisionContacts must pass provider-filtered collision statels to runtime systems.");
            Assert.IsFalse(
                primeStatelCollisionContacts.Contains("sd.Events.Any")
                || checkStatelCollision.Contains("foreach (StatelData sd in this.collisionStatels)"),
                "Playfield must not own collision-capable statel selection or the statel collision loop.");
            Assert.IsTrue(
                statelTransitionText.Contains("foreach (StatelData sd in collisionStatels)")
                && statelTransitionText.Contains("ev.Perform(dynel, sd);"),
                "PlayfieldStatelTransitionRuntimeService must own collision statel iteration and event firing.");

            Assert.IsTrue(
                staticDynelRuntimeText.Contains("new StaticDynel(playfieldIdentity, staticDynel.Identity, staticDynel.Template)")
                && staticDynelRuntimeText.Contains("foreach (GameTuple<CharacterStat, uint> stat in staticDynel.Stats)")
                && staticDynelRuntimeText.Contains("sdy.Coordinate = staticDynel.Coordinate;")
                && staticDynelRuntimeText.Contains("sdy.Heading = staticDynel.Heading;"),
                "PlayfieldStaticDynelRuntimeService must own runtime static dynel construction.");
            Assert.IsFalse(
                staticDynelRuntimeText.Contains("StaticDynelDao.Instance.GetWhere"),
                "Static dynel runtime construction must not own DB row access.");
            Assert.IsFalse(
                staticDynelRuntimeText.Contains("MessagePackZip.DeserializeData"),
                "Static dynel runtime construction must not own static dynel stat deserialization.");
            Assert.IsFalse(
                playfieldText.Contains("private IEntity CreateStaticDynel(")
                || playfieldText.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must not directly own static dynel runtime construction.");
            Assert.IsTrue(
                dbMobSpawnText.Contains("internal IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions(Identity playfieldIdentity)")
                && dbMobSpawnText.Contains("MobSpawnDao.Instance.GetWhere")
                && dbMobSpawnText.Contains("internal IEnumerable<DBMobSpawnStat> LoadMobSpawnStats(DBMobSpawn mob)")
                && dbMobSpawnText.Contains("MobSpawnStatDao.Instance.GetWhere")
                && dbMobSpawnText.Contains("internal ICharacter InstantiateDbMobSpawn(DBMobSpawn mob, DBMobSpawnStat[] stats, Playfield playfield)")
                && dbMobSpawnText.Contains("NonPlayerCharacterHandler.InstantiateMobSpawn")
                && dbMobSpawnText.Contains("new NPCController()")
                && dbMobSpawnText.Contains("internal void AttachMobSpawnKnuBot(DBMobSpawn mob, ICharacter cmob)")
                && dbMobSpawnText.Contains("ScriptCompiler.Instance.CreateKnuBot")
                && vendorRuntimeText.Contains("internal void SpawnVendors(Playfield playfield, StatelData[] vendorStatels)")
                && vendorRuntimeText.Contains("VendorHandler.SpawnVendorsForPlayfield(playfield, vendorStatels);"),
                "DB mob spawn runtime service must own DB loading, object construction, and script creation callbacks while vendor runtime service owns vendor spawning.");
            Assert.IsFalse(
                playfieldText.Contains("private IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions")
                || playfieldText.Contains("private IEnumerable<DBMobSpawnStat> LoadMobSpawnStats")
                || playfieldText.Contains("private ICharacter InstantiateDbMobSpawn")
                || playfieldText.Contains("private void AttachMobSpawnKnuBot")
                || playfieldText.Contains("private void SpawnVendors(StatelData[] vendorStatels)")
                || playfieldText.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels)"),
                "Playfield must not directly own DB mob spawn loading, construction callbacks, or vendor spawning callbacks.");
            Assert.IsFalse(
                materializationText.Contains("MobSpawnDao")
                || materializationText.Contains("MobSpawnStatDao")
                || materializationText.Contains("NonPlayerCharacterHandler")
                || materializationText.Contains("new NPCController")
                || materializationText.Contains("ScriptCompiler")
                || materializationText.Contains("VendorHandler")
                || materializationText.Contains("new StaticDynel")
                || materializationText.Contains("StaticDynelDao"),
                "Materialization service must not own DB loading or object construction.");

            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldContentDataProvider.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldDbMobSpawnRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldStaticDynelRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\PlayfieldVendorRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldContentDataProvider, PlayfieldObjectMaterializationRuntimeService, PlayfieldDbMobSpawnRuntimeService, PlayfieldStaticDynelRuntimeService, and PlayfieldVendorRuntimeService.");
        }

        [TestMethod]
        public void PlayfieldContentDataProviderDoesNotOwnRuntimeSystemsOrPacketFlows()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));
            string materializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
            string staticDynelRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStaticDynelRuntimeService.cs"));
            string vendorRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVendorRuntimeService.cs"));

            Assert.IsTrue(
                providerText.Contains("StaticDynelDao.Instance.GetWhere"),
                "Provider must own static dynel definition data selection.");
            Assert.IsTrue(
                providerText.Contains("private StatelData[] ResolveVendorStatels(IEnumerable<StatelData> statels)"),
                "Provider must own vendor statel filtering.");
            Assert.IsTrue(
                providerText.Contains("internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)"),
                "Provider must own collision-capable statel filtering.");

            string[] forbiddenRuntimeOwnershipPatterns =
                {
                    "new StaticDynel",
                    "VendorHandler.SpawnVendorsForPlayfield",
                    "SendCompressed",
                    "N3Messages",
                    "SystemMessages",
                    "MessageHandler",
                    "GenericCmd",
                    "NpcCombat",
                    "CombatDamageRules",
                    "NpcCorpse",
                    "Inventory",
                    "ContainerAddItem",
                    "ClientMoveItem",
                    "OrgClient",
                    "OrgServer",
                    "PrivateCityReadyInitCoordinator",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < forbiddenRuntimeOwnershipPatterns.Length; i++)
            {
                Assert.IsFalse(
                    providerText.Contains(forbiddenRuntimeOwnershipPatterns[i]),
                    "PlayfieldContentDataProvider must not own runtime or packet behavior: "
                    + forbiddenRuntimeOwnershipPatterns[i]);
            }

            string checkStatelCollision = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision(ICharacter dynel)");

            Assert.IsTrue(
                vendorRuntimeText.Contains("VendorHandler.SpawnVendorsForPlayfield(playfield, vendorStatels);"),
                "PlayfieldVendorRuntimeService must own vendor runtime spawning.");
            Assert.IsFalse(
                playfieldText.Contains("private void SpawnVendors(StatelData[] vendorStatels)")
                || playfieldText.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels)"),
                "Playfield must not directly own vendor runtime spawning.");
            Assert.IsTrue(
                staticDynelRuntimeText.Contains("new StaticDynel(playfieldIdentity, staticDynel.Identity, staticDynel.Template)")
                && staticDynelRuntimeText.Contains("foreach (GameTuple<CharacterStat, uint> stat in staticDynel.Stats)"),
                "PlayfieldStaticDynelRuntimeService must own StaticDynel runtime construction.");
            Assert.IsFalse(
                playfieldText.Contains("private IEntity CreateStaticDynel(")
                || playfieldText.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must not directly own StaticDynel runtime construction.");
            Assert.IsTrue(
                materializationText.Contains("tryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels)")
                && materializationText.Contains("spawnVendors(vendorStatels);")
                && materializationText.Contains("registerDynel(instantiateStaticDynel(staticDynel));"),
                "PlayfieldObjectMaterializationRuntimeService must own vendor and static dynel materialization loops.");
            Assert.IsTrue(
                checkStatelCollision.Contains("this.runtimeSystems.CheckStatelCollision(")
                && checkStatelCollision.Contains("this.TeleportToPlayfield"),
                "Playfield must delegate statel collision runtime orchestration while keeping teleport callback ownership.");
            Assert.IsTrue(
                statelTransitionText.Contains("IsInStatelCollisionRange(sd, dynel)")
                && statelTransitionText.Contains("ev.Perform(dynel, sd);"),
                "PlayfieldStatelTransitionRuntimeService must own statel collision runtime check/event orchestration.");
            Assert.IsFalse(
                statelTransitionText.Contains("VendorHandler.SpawnVendorsForPlayfield")
                || statelTransitionText.Contains("new StaticDynel")
                || statelTransitionText.Contains("StaticDynelDao.Instance.GetWhere"),
                "PlayfieldStatelTransitionRuntimeService must not own content data, static dynel construction, or vendor spawning.");
            Assert.IsFalse(
                materializationText.Contains("VendorHandler.SpawnVendorsForPlayfield")
                || materializationText.Contains("new StaticDynel")
                || materializationText.Contains("StaticDynelDao.Instance.GetWhere"),
                "PlayfieldObjectMaterializationRuntimeService must not own vendor implementation, static dynel construction, or content DB selection.");
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
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
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
        public void ZoneClientSessionLifecycleBoundaryIsWiredAroundExistingLoginReadyAndZoningFlow()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string zoneLoginText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ZoneLoginMessageHandler.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string transferText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTransferRuntimeService.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
            string teleportMethod = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");
            string disposeMethod = ExtractMethodBlock(zoneClientText, "protected override void Dispose(bool disposing)");

            Assert.IsTrue(
                zoneClientText.Contains("private readonly ZoneClientSessionLifecycleCoordinator sessionLifecycle"),
                "ZoneClient must own the session lifecycle coordinator.");
            Assert.IsTrue(
                zoneClientText.Contains("public ZoneClientSessionLifecycleCoordinator SessionLifecycle"),
                "ZoneClient must expose the session lifecycle boundary to existing handlers.");
            Assert.IsTrue(
                projectText.Contains(@"Core\ZoneClientSessionLifecycleCoordinator.cs"),
                "ZoneEngine project must compile the session lifecycle coordinator.");

            AssertTextBefore(
                zoneLoginText,
                "zc.SessionLifecycle.BeginCharacterLoading();",
                "zc.CreateCharacter(message.CharacterId);");
            AssertTextBefore(
                zoneClientText,
                "this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();",
                "this.server.PlayfieldById(");
            AssertTextBefore(
                clientConnectedText,
                "client.PacketSequencing.BeginSessionReadyBlock(",
                "PlayfieldAnarchyFMessageHandler.Default.Send");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit,",
                "() => FullCharacterMessageHandler.Default.Send(client.Controller.Character)");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterCharInPlayForVisibilityEntry,",
                "() => currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character)");
            AssertTextBefore(
                clientConnectedText,
                "client.PacketSequencing.CompleteSessionInitialization(",
                "client.Controller.Character.DoNotDoTimers = false;");
            AssertTextBefore(
                transferText,
                "this.packetSequences.RunPlayfieldTransferBeginSequence(",
                "sendTeleportPacket);");
            AssertTextBefore(
                disposeMethod,
                "this.sessionLifecycle.EnterDisconnectingForSessionDispose();",
                "this.stopDispatcher = true;");
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
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
            string zoneLoginText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ZoneLoginMessageHandler.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

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

            Assert.IsTrue(
                clientConnectedText.Contains("() => FullCharacterMessageHandler.Default.Send(client.Controller.Character)"),
                "FullCharacter packet emission must still remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                clientConnectedText.Contains("() => currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character)"),
                "CharInPlay/visibility packet emission must still remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                playfieldText.Contains("TeleportMessageHandler.Default.Send("),
                "Teleport packet emission must still remain outside the lifecycle coordinator.");

            string markerSurfaces = zoneLoginText + clientConnectedText + playfieldText;
            Assert.IsFalse(
                markerSurfaces.Contains("ZoneClientSessionPhase."),
                "Packet/runtime surfaces must not own lifecycle enum transition rules directly.");
            Assert.IsFalse(
                markerSurfaces.Contains("CanTransitionTo("),
                "Packet/runtime surfaces must call named coordinator transition methods instead of owning transition validity.");
            Assert.IsFalse(
                markerSurfaces.Contains("BeginReadyBlock()")
                || markerSurfaces.Contains("BeginFullCharacterBoundary()")
                || markerSurfaces.Contains("MarkCharInPlay()")
                || markerSurfaces.Contains("MarkInPlay()"),
                "Packet/runtime surfaces must not use loose ready/full-character/CharInPlay lifecycle marker names.");
            Assert.IsFalse(
                markerSurfaces.Contains("BeginPlayfieldLoading()")
                || markerSurfaces.Contains("BeginZoning()")
                || markerSurfaces.Contains("BeginDisconnecting()"),
                "Packet/runtime surfaces must not use loose playfield-loading/zoning/disconnect lifecycle marker names.");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleFinalPhaseOwnershipGuardrailKeepsRuntimeMechanicsOut()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

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

            string runtimeSurfaces = zoneClientText + clientConnectedText + playfieldText;
            Assert.IsFalse(
                runtimeSurfaces.Contains("TransitionTo("),
                "Runtime packet/session surfaces must not call the raw phase transition helper.");
            Assert.IsFalse(
                runtimeSurfaces.Contains("ZoneClientSessionPhase."),
                "Runtime packet/session surfaces must not own direct lifecycle phase enum transitions.");

            Assert.IsTrue(
                zoneClientText.Contains("this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();")
                && zoneClientText.Contains("this.sessionLifecycle.EnterDisconnectingForSessionDispose();"),
                "ZoneClient must use named coordinator methods for playfield-loading/zoning-exit and disconnect phases.");
            Assert.IsTrue(
                clientConnectedText.Contains("client.PacketSequencing.BeginSessionReadyBlock(client.SessionLifecycle.EnterReadyBlockForSessionInit);")
                && clientConnectedText.Contains("client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit,")
                && clientConnectedText.Contains("client.SessionLifecycle.EnterCharInPlayForVisibilityEntry,")
                && clientConnectedText.Contains("client.PacketSequencing.CompleteSessionInitialization(")
                && clientConnectedText.Contains("client.SessionLifecycle.CompleteInPlayForSessionInit);"),
                "ClientConnected must route ready/full-character/CharInPlay/InPlay phases through named coordinator methods.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.TransferToPlayfield(")
                && playfieldText.Contains("CapturePlayfieldTransferEnterZoningPhase"),
                "Playfield teleport must route zoning entry through the named coordinator method.");

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

            Assert.IsTrue(
                playfieldText.Contains("TeleportMessageHandler.Default.Send(")
                && playfieldText.Contains("new ZoneRedirectionMessage")
                && playfieldText.Contains("client.SendCompressed(redirect);"),
                "Teleport/redirection packet mechanics must remain in Playfield.");
            Assert.IsTrue(
                playfieldText.Contains("SendPrivateCityPreFullCharacterReadyBlock")
                && playfieldText.Contains("SendPrivateCityPlayfieldReadyBlock")
                && playfieldText.Contains("this.runtimeSystems.SendPrivateCity"),
                "Private-city ready/init packet construction and delegation must remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.AnnounceJoiningCharacterVisibility(character, body => this.Announce(body));")
                && playfieldText.Contains("public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)"),
                "SCFU and CharInPlay broadcast entry points must remain in Playfield.");
            Assert.IsTrue(
                zoneClientText.Contains("this.stopDispatcher = true;")
                && zoneClientText.Contains("this.zStream.Close();")
                && zoneClientText.Contains("this.netStream.Close();"),
                "Engine/client disposal mechanics must remain in ZoneClient.");
        }

        [TestMethod]
        public void PacketSequencingCoordinatorOwnsSessionInitializationOrderWithoutOwningPackets()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketSequencingCoordinator.cs"));
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string transferText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTransferRuntimeService.cs"));
            string visibilityPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));
            string privateCityReadyInitText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PrivateCityReadyInitCoordinator.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            Assert.IsTrue(
                coordinatorText.Contains("public sealed class PacketSequencingCoordinator"),
                "PacketSequencingCoordinator must be the named session packet sequencing boundary.");
            Assert.IsTrue(
                projectText.Contains(@"Core\PacketSequencingCoordinator.cs"),
                "ZoneEngine project must compile the packet sequencing coordinator.");
            Assert.IsTrue(
                zoneClientText.Contains("private readonly PacketSequencingCoordinator packetSequencing")
                && zoneClientText.Contains("public PacketSequencingCoordinator PacketSequencing"),
                "ZoneClient must own and expose the packet sequencing coordinator.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PacketSequencingCoordinator packetSequencing")
                && runtimeSystemsText.Contains("internal void SendExistingCharacterVisibilityToClient(")
                && runtimeSystemsText.Contains("internal void AnnounceJoiningCharacterVisibility(")
                && runtimeSystemsText.Contains("internal void RunPlayfieldTransferBeginSequence("),
                "PlayfieldRuntimeSystems must expose named visibility and packet sequencing entry points for playfield-local sequencing.");
            Assert.IsTrue(
                clientConnectedText.Contains("client.PacketSequencing.BeginSessionReadyBlock(")
                && clientConnectedText.Contains("client.PacketSequencing.RunSessionReadyFullCharacterSequence(")
                && clientConnectedText.Contains("client.PacketSequencing.RunVisibilityInitializationSequence(")
                && clientConnectedText.Contains("client.PacketSequencing.CompleteSessionInitialization("),
                "ClientConnected must route session packet sequencing through PacketSequencingCoordinator.");

            string readyFullCharacterSequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunSessionReadyFullCharacterSequence");
            AssertTextBefore(readyFullCharacterSequence, "Execute(recordReadyBlockBegin", "Execute(recordSimpleCharFullUpdate");
            AssertTextBefore(readyFullCharacterSequence, "Execute(recordSimpleCharFullUpdate", "Execute(sendSimpleCharFullUpdate");
            AssertTextBefore(readyFullCharacterSequence, "Execute(sendSimpleCharFullUpdate", "Execute(prepareFullCharacterState");
            AssertTextBefore(readyFullCharacterSequence, "Execute(prepareFullCharacterState", "Execute(sendPreFullCharacterReadyBlock");
            AssertTextBefore(readyFullCharacterSequence, "Execute(sendPreFullCharacterReadyBlock", "Execute(recordFullCharacter");
            AssertTextBefore(readyFullCharacterSequence, "Execute(recordFullCharacter", "Execute(enterFullCharacterBoundary");
            AssertTextBefore(readyFullCharacterSequence, "Execute(enterFullCharacterBoundary", "Execute(sendFullCharacter");
            AssertTextBefore(readyFullCharacterSequence, "Execute(sendFullCharacter", "Execute(sendPlayfieldReadyBlock");
            AssertTextBefore(readyFullCharacterSequence, "Execute(sendPlayfieldReadyBlock", "Execute(recordReadyBlockEnd");

            string visibilitySequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunVisibilityInitializationSequence");
            AssertTextBefore(visibilitySequence, "Execute(recordJoinerReady", "Execute(enterCharInPlay");
            AssertTextBefore(visibilitySequence, "Execute(enterCharInPlay", "Execute(announceJoiningCharacter");
            AssertTextBefore(visibilitySequence, "Execute(announceJoiningCharacter", "Execute(sendExistingCharacterSnapshots");

            string simpleCharFullUpdateCharInPlaySequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunSimpleCharFullUpdateCharInPlaySequence");
            AssertTextBefore(simpleCharFullUpdateCharInPlaySequence, "Execute(recordSimpleCharFullUpdate", "Execute(sendSimpleCharFullUpdate");
            AssertTextBefore(simpleCharFullUpdateCharInPlaySequence, "Execute(sendSimpleCharFullUpdate", "Execute(prepareCharInPlay");
            AssertTextBefore(simpleCharFullUpdateCharInPlaySequence, "Execute(prepareCharInPlay", "Execute(recordCharInPlay");
            AssertTextBefore(simpleCharFullUpdateCharInPlaySequence, "Execute(recordCharInPlay", "Execute(sendCharInPlay");

            AssertTextBefore(
                clientConnectedText,
                "() => SimpleCharFullUpdate.SendToPlayfield(client)",
                "GuestKeyGeneratorInteractionHandler.ProcessCityAccessCardLifetimes(client.Controller.Character);");
            AssertTextBefore(
                clientConnectedText,
                "Packets.WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);",
                "currentPlayfield.SendPrivateCityPreFullCharacterReadyBlock(client, client.Controller.Character);");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit,",
                "() => FullCharacterMessageHandler.Default.Send(client.Controller.Character)");
            Assert.AreEqual(
                2,
                CountOccurrences(playfieldText, "this.runtimeSystems.SendExistingCharacterVisibilityToClient(")
                + CountOccurrences(playfieldText, "this.runtimeSystems.AnnounceJoiningCharacterVisibility("),
                "Playfield must route both existing-player and joining-player SCFU/CharInPlay pairs through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(")
                && privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence("),
                "PrivateCityReadyInitCoordinator must route private-city ready/init packet order through PacketSequencingCoordinator.");
            Assert.IsTrue(
                transferText.Contains("this.packetSequences.RunPlayfieldTransferBeginSequence(")
                && transferText.Contains("Action enterZoningPhase = captureEnterZoningPhase(dynel);")
                && playfieldText.Contains("() => TeleportMessageHandler.Default.Send("),
                "Playfield must route zoning phase entry before teleport packet send through PacketSequencingCoordinator.");

            string privateCityOrgInitSequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunPrivateCityPreFullCharacterOrgInitSequence");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendOrgInfoPacket", "Execute(sendInitialSocialStatus");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendInitialSocialStatus", "Execute(sendOrganizationId");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendOrganizationId", "Execute(sendOrganizationRank");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendOrganizationRank", "Execute(sendSocialStatusRepeat1");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendSocialStatusRepeat1", "Execute(sendSocialStatusRepeat2");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendSocialStatusRepeat2", "Execute(sendSocialStatusRepeat3");
            AssertTextBefore(privateCityOrgInitSequence, "Execute(sendSocialStatusRepeat3", "Execute(recordOrgInitSent");

            string privateCityReadyBlockSequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunPrivateCityPlayfieldReadyBlockSequence");
            AssertTextBefore(privateCityReadyBlockSequence, "Execute(sendPlayfieldAllTowers", "Execute(recordPlayfieldAllTowers");
            AssertTextBefore(privateCityReadyBlockSequence, "Execute(recordPlayfieldAllTowers", "Execute(sendPlayfieldAllCities");
            AssertTextBefore(privateCityReadyBlockSequence, "Execute(sendPlayfieldAllCities", "Execute(recordPlayfieldAllCities");
            AssertTextBefore(privateCityReadyBlockSequence, "Execute(recordPlayfieldAllCities", "Execute(recordTowersCitiesSent");

            string playfieldTransferBeginSequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunPlayfieldTransferBeginSequence");
            AssertTextBefore(playfieldTransferBeginSequence, "Execute(enterZoningPhase", "Execute(sendTeleportPacket");

            string[] packetAndRuntimePatterns =
                {
                    "SendCompressed",
                    "PlayfieldAnarchyFMessageHandler",
                    "FullCharacterMessageHandler",
                    "SimpleCharFullUpdate.",
                    "CharInPlayMessage",
                    "PlayfieldAllTowersMessage",
                    "PlayfieldAllCitiesMessage",
                    "PrivateCityReadyInitCoordinator",
                    "GenericCmd",
                    "InventoryContainerRuntimeService",
                    "OrgClient",
                    "AOSharpLiveCapture"
                };
            for (int i = 0; i < packetAndRuntimePatterns.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(packetAndRuntimePatterns[i]),
                    "PacketSequencingCoordinator must own sequencing only, not packet construction/runtime systems: "
                    + packetAndRuntimePatterns[i]);
            }

            Assert.IsTrue(
                clientConnectedText.Contains("() => FullCharacterMessageHandler.Default.Send(client.Controller.Character)")
                && clientConnectedText.Contains("() => currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character)")
                && clientConnectedText.Contains("() => currentPlayfield.SendSCFUsToClient(new IMSendPlayerSCFUs { toClient = client })"),
                "Session packet send expressions must remain in ClientConnected for these sequencing slices.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.SendExistingCharacterVisibilityToClient(")
                && playfieldText.Contains("body => sendSCFUs.toClient.SendCompressed(body)")
                && playfieldText.Contains("this.runtimeSystems.AnnounceJoiningCharacterVisibility(character, body => this.Announce(body));"),
                "Visibility packet send callbacks must remain in Playfield while packet construction moves behind runtime systems.");
            Assert.IsTrue(
                privateCityReadyInitText.Contains("new OrgInfoPacketMessage")
                && privateCityReadyInitText.Contains("new PlayfieldAllTowersMessage")
                && privateCityReadyInitText.Contains("new PlayfieldAllCitiesMessage")
                && privateCityReadyInitText.Contains("this.SendPrivateCityStatValue(client, character, StatIds.socialstatus, 4, 1)")
                && privateCityReadyInitText.Contains("this.SendPrivateCityStat(client, character, StatIds.clan, 0)")
                && privateCityReadyInitText.Contains("this.SendPrivateCityStat(client, character, StatIds.clanlevel, 0)"),
                "Private-city packet construction and stat send expressions must remain in PrivateCityReadyInitCoordinator.");
        }

        [TestMethod]
        public void ZoningTeleportSequencingGuardrailKeepsRuntimeHandoffAndRedirectOrder()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string packetSequencingText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketSequencingCoordinator.cs"));
            string lifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldLifecycleRuntimeService.cs"));
            string transferText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTransferRuntimeService.cs"));

            string teleportMethod = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");
            string createCharacterMethod = ExtractMethodBlock(
                zoneClientText,
                "public void CreateCharacter(int charId)");

            AssertTextBefore(
                teleportMethod,
                "if (this.TryCompleteGridTeleportInCurrentPlayfield(dynel, destination, heading, playfield))",
                "this.runtimeSystems.TransferToPlayfield(");
            AssertTextBefore(
                transferText,
                "this.lifecycle.PreparePlayfieldTransfer(",
                "Action enterZoningPhase = captureEnterZoningPhase(dynel);");
            AssertTextBefore(
                lifecycleText,
                "clearTransferContactState(dynel.Identity.Instance);",
                "disableTimers(dynel);");
            AssertTextBefore(
                transferText,
                "Action enterZoningPhase = captureEnterZoningPhase(dynel);",
                "this.packetSequences.RunPlayfieldTransferBeginSequence(");
            AssertTextBefore(
                transferText,
                "this.packetSequences.RunPlayfieldTransferBeginSequence(",
                "this.CompletePlayfieldTransfer(");
            AssertTextBefore(
                transferText,
                "announceDespawn(dynel);",
                "applyTransferState(dynel, destination, heading);");
            AssertTextBefore(
                transferText,
                "applyTransferState(dynel, destination, heading);",
                "ZoneClient client = captureClient(dynel);");
            AssertTextBefore(
                transferText,
                "ZoneClient client = captureClient(dynel);",
                "IPlayfield newPlayfield = resolveDestinationPlayfield(playfield);");
            AssertTextBefore(
                transferText,
                "IPlayfield newPlayfield = resolveDestinationPlayfield(playfield);",
                "finalizeTransferDispose(dynel, newPlayfield);");
            AssertTextBefore(
                transferText,
                "finalizeTransferDispose(dynel, newPlayfield);",
                "sendRedirect(client);");

            AssertTextBefore(
                createCharacterMethod,
                "this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();",
                "this.server.PlayfieldById(");
            AssertTextBefore(
                createCharacterMethod,
                "this.server.PlayfieldById(",
                "this.Controller.Character = new Character(");

            Assert.IsTrue(
                teleportMethod.Contains("this.runtimeSystems.TransferToPlayfield("),
                "Playfield must route non-local transfer orchestration through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                packetSequencingText.Contains("TeleportMessageHandler")
                || packetSequencingText.Contains("ZoneRedirectionMessage")
                || packetSequencingText.Contains("PlayfieldById")
                || packetSequencingText.Contains("dynel.Dispose"),
                "PacketSequencingCoordinator must not own teleport packet construction, destination lookup, or disposal mechanics.");
            Assert.IsFalse(
                transferText.Contains("TeleportMessageHandler")
                || transferText.Contains("ZoneRedirectionMessage")
                || transferText.Contains("SendCompressed")
                || transferText.Contains("PlayfieldById")
                || transferText.Contains("new Playfield("),
                "PlayfieldTransferRuntimeService must not own teleport packet construction, transport, or destination lookup.");
            Assert.IsTrue(
                transferText.Contains("internal void TransferToPlayfield(")
                && transferText.Contains("this.lifecycle.PreparePlayfieldTransfer(")
                && transferText.Contains("captureEnterZoningPhase(dynel)")
                && transferText.Contains("this.packetSequences.RunPlayfieldTransferBeginSequence(")
                && transferText.Contains("this.CompletePlayfieldTransfer("),
                "PlayfieldTransferRuntimeService must own non-local transfer orchestration around lifecycle prep, zoning entry sequencing, and handoff completion.");
            Assert.IsTrue(
                playfieldText.Contains("private void AnnouncePlayfieldTransferDespawn(Dynel dynel)")
                && playfieldText.Contains("DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);")
                && playfieldText.Contains("this.AnnounceOthers(despawnMessage, dynel.Identity);")
                && playfieldText.Contains("private static void ApplyPlayfieldTransferState(Dynel dynel, Coordinate destination, IQuaternion heading)")
                && playfieldText.Contains("dynel.RawCoordinates = new Vector3()")
                && playfieldText.Contains("dynel.RawHeading = new Vector.Quaternion")
                && playfieldText.Contains("private IPlayfield ResolveOrCreatePlayfieldTransferDestination(Identity playfield)")
                && playfieldText.Contains("IPlayfield newPlayfield = this.server.PlayfieldById(playfield);")
                && playfieldText.Contains("newPlayfield = new Playfield(this.server, playfield);")
                && playfieldText.Contains("private static void CompletePlayfieldTransferDispose(Dynel dynel, IPlayfield newPlayfield)")
                && playfieldText.Contains("dynel.Controller.Client = null;")
                && playfieldText.Contains("dynel.IsTeleporting = true;")
                && playfieldText.Contains("dynel.Dispose();")
                && playfieldText.Contains("private void SendPlayfieldTransferRedirect(ZoneClient client, Identity playfield)")
                && playfieldText.Contains("var redirect = new ZoneRedirectionMessage")
                && playfieldText.Contains("client.SendCompressed(redirect);"),
                "Playfield must keep transfer callbacks that own despawn broadcast, state mutation, destination lookup, disposal, and redirect send.");
            Assert.IsFalse(
                teleportMethod.Contains("PlayfieldLifecycleTrace."),
                "No zoning PlayfieldLifecycleTrace points exist yet; this guardrail protects current lifecycle/order text instead.");
        }

        [TestMethod]
        public void PacketSequencingCoordinatorFinalOwnershipGuardrailKeepsRuntimeMechanicsOut()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketSequencingCoordinator.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string privateCityReadyInitText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PrivateCityReadyInitCoordinator.cs"));

            string[] ownedSequenceMethods =
                {
                    "public void BeginSessionReadyBlock(",
                    "public void RunSessionReadyFullCharacterSequence(",
                    "public void RunVisibilityInitializationSequence(",
                    "public void RunSimpleCharFullUpdateCharInPlaySequence(",
                    "public void RunPrivateCityPreFullCharacterOrgInitSequence(",
                    "public void RunPrivateCityPlayfieldReadyBlockSequence(",
                    "public void RunPlayfieldTransferBeginSequence(",
                    "public void CompleteSessionInitialization("
                };
            for (int i = 0; i < ownedSequenceMethods.Length; i++)
            {
                Assert.IsTrue(
                    coordinatorText.Contains(ownedSequenceMethods[i]),
                    "PacketSequencingCoordinator must own sequence method " + ownedSequenceMethods[i]);
            }

            Assert.IsTrue(
                clientConnectedText.Contains("client.PacketSequencing.RunSessionReadyFullCharacterSequence(")
                && clientConnectedText.Contains("client.PacketSequencing.RunVisibilityInitializationSequence("),
                "PacketSequencingCoordinator must own session ready/full-character/visibility initialization sequencing.");
            string visibilityPacketSequenceText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));
            Assert.AreEqual(
                2,
                CountOccurrences(visibilityPacketSequenceText, "this.packetSequences.RunVisibilityPacketPairSequence("),
                "Playfield visibility packet runtime must own both SCFU -> CharInPlay visibility pair sequence entry points.");
            Assert.IsTrue(
                privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(")
                && privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence("),
                "PacketSequencingCoordinator must own private-city org/stat and towers/cities sequencing.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.TransferToPlayfield("),
                "PlayfieldRuntimeSystems must own non-local transfer orchestration entry from Playfield.");

            string[] forbiddenCoordinatorOwnership =
                {
                    "new OrgInfoPacketMessage",
                    "new PlayfieldAllTowersMessage",
                    "new PlayfieldAllCitiesMessage",
                    "SimpleCharFullUpdate.",
                    "new CharInPlayMessage",
                    "FullCharacterMessageHandler",
                    "SendCompressed",
                    "MessageSerializer",
                    "NetworkStream",
                    "zStream",
                    "netStream",
                    "PlayfieldById",
                    "new Playfield(",
                    "DespawnMessageHandler",
                    "AnnounceOthers",
                    "RawCoordinates",
                    "RawHeading",
                    "Controller.Client = null",
                    "IsTeleporting",
                    "dynel.Dispose",
                    "ZoneRedirectionMessage",
                    "SendLocal"
                };
            for (int i = 0; i < forbiddenCoordinatorOwnership.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(forbiddenCoordinatorOwnership[i]),
                    "PacketSequencingCoordinator must not own runtime mechanics or packet construction: "
                    + forbiddenCoordinatorOwnership[i]);
            }

            string teleportMethod = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");
            string localTeleportMethod = ExtractMethodBlock(
                playfieldText,
                "private bool TryCompleteGridTeleportInCurrentPlayfield(");

            Assert.IsTrue(
                playfieldText.Contains("private void AnnouncePlayfieldTransferDespawn(Dynel dynel)")
                && playfieldText.Contains("private static void ApplyPlayfieldTransferState(Dynel dynel, Coordinate destination, IQuaternion heading)")
                && playfieldText.Contains("private IPlayfield ResolveOrCreatePlayfieldTransferDestination(Identity playfield)")
                && playfieldText.Contains("private static void CompletePlayfieldTransferDispose(Dynel dynel, IPlayfield newPlayfield)")
                && playfieldText.Contains("private void SendPlayfieldTransferRedirect(ZoneClient client, Identity playfield)"),
                "Destination lookup, despawn broadcast, coordinate mutation, client detach/dispose, and redirect callbacks must remain in Playfield.");
            Assert.IsTrue(
                localTeleportMethod.Contains("TeleportMessageHandler.Default.SendLocal("),
                "Same-playfield local teleport packet path must remain outside PacketSequencingCoordinator.");
        }

        [TestMethod]
        public void TeleportZoningHandoffGuardrailKeepsStatelAndPrivateCityRoutingStable()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));

            string checkStatelCollisionMethod = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision");
            string teleportToPlayfieldMethod = ExtractMethodBlock(playfieldText, "private void TeleportToPlayfield");
            string localTeleportMethod = ExtractMethodBlock(
                playfieldText,
                "private bool TryCompleteGridTeleportInCurrentPlayfield(");
            string checkStatelCollisionRuntimeMethod = ExtractMethodBlock(
                statelTransitionText,
                "internal void CheckStatelCollision(");
            string privateCityEntryMethod = ExtractMethodBlock(
                statelTransitionText,
                "private bool TryHandleCapturedMontroyalPrivateCityEntry(");
            string privateCityExitMethod = ExtractMethodBlock(
                statelTransitionText,
                "private bool TryHandleUserConfirmedMontroyalPrivateCityExit(");

            Assert.IsTrue(
                checkStatelCollisionMethod.Contains("this.runtimeSystems.CheckStatelCollision(")
                && checkStatelCollisionMethod.Contains("ResolveCapturedMontroyalPrivateCityInstance")
                && checkStatelCollisionMethod.Contains("ResolveCharacterOrganizationInstance")
                && checkStatelCollisionMethod.Contains("x => x.StopMovement()")
                && checkStatelCollisionMethod.Contains("this.SendCapturedPrivateCityEntrySocialStatus")
                && checkStatelCollisionMethod.Contains("this.TeleportToPlayfield"),
                "Playfield must keep statel collision as orchestration callbacks into the runtime boundary.");
            Assert.IsTrue(
                teleportToPlayfieldMethod.Contains("this.Teleport(")
                && teleportToPlayfieldMethod.Contains("new Identity { Type = IdentityType.Playfield, Instance = playfieldInstance }"),
                "Playfield must keep destination identity construction at the teleport handoff boundary.");

            AssertTextBefore(
                checkStatelCollisionRuntimeMethod,
                "if (IsPostZoneCollisionGraceActive(dynel))",
                "this.TryHandleCapturedMontroyalPrivateCityEntry(");
            AssertTextBefore(
                checkStatelCollisionRuntimeMethod,
                "this.TryHandleCapturedMontroyalPrivateCityEntry(",
                "this.TryHandleUserConfirmedMontroyalPrivateCityExit(");
            AssertTextBefore(
                checkStatelCollisionRuntimeMethod,
                "this.TryHandleUserConfirmedMontroyalPrivateCityExit(",
                "foreach (StatelData sd in collisionStatels)");

            AssertTextBefore(
                privateCityEntryMethod,
                "int destinationPlayfieldId = resolvePrivateCityDestinationPlayfield(character);",
                "Coordinate destination = ResolveCapturedMontroyalEntryDestination(destinationPlayfieldId);");
            AssertTextBefore(
                privateCityEntryMethod,
                "Coordinate destination = ResolveCapturedMontroyalEntryDestination(destinationPlayfieldId);",
                "stopMovement(character);");
            AssertTextBefore(
                privateCityEntryMethod,
                "stopMovement(character);",
                "sendCapturedPrivateCityEntrySocialStatus(character);");
            AssertTextBefore(
                privateCityEntryMethod,
                "sendCapturedPrivateCityEntrySocialStatus(character);",
                "teleportToPlayfield(dynel, destination, heading, destinationPlayfieldId);");

            AssertTextBefore(
                privateCityExitMethod,
                "var destination = new Coordinate(",
                "stopMovement(character);");
            AssertTextBefore(
                privateCityExitMethod,
                "stopMovement(character);",
                "teleportToPlayfield(dynel, destination, heading, CapturedMontroyalEntrySourcePlayfieldId);");

            AssertTextBefore(
                localTeleportMethod,
                "TeleportMessageHandler.Default.SendLocal(",
                "dynel.RawCoordinates = new AORebirth.Core.Vector.Vector3");
            AssertTextBefore(
                localTeleportMethod,
                "dynel.RawHeading = new AORebirth.Core.Vector.Quaternion",
                "this.PrimeStatelCollisionContacts(character);");

            string[] forbiddenStatelRuntimeOwnership =
                {
                    "ZoneRedirectionMessage",
                    "TeleportMessageHandler.Default.Send(",
                    "TeleportMessageHandler.Default.SendLocal(",
                    "PlayfieldById",
                    "new Playfield(",
                    "client.SendCompressed",
                    "dynel.Dispose",
                    "Pool.Instance"
                };
            for (int i = 0; i < forbiddenStatelRuntimeOwnership.Length; i++)
            {
                Assert.IsFalse(
                    statelTransitionText.Contains(forbiddenStatelRuntimeOwnership[i]),
                    "Statel transition runtime must not own Playfield handoff mechanics: "
                    + forbiddenStatelRuntimeOwnership[i]);
            }
        }

        [TestMethod]
        public void PlayfieldDynelRegistryIsOwnedByRuntimeSystemsAndFeedsSafeLookupPaths()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string registryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldDynelRegistry.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            string[] registryApi =
                {
                    "internal void RefreshFromPool()",
                    "internal void Register(IEntity entity)",
                    "internal void Unregister(Identity identity)",
                    "internal void RegisterStatels(IEnumerable<StatelData> playfieldStatels)",
                    "internal IInstancedEntity FindByIdentity(Identity identity)",
                    "internal T FindByIdentity<T>(Identity identity)",
                    "internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)",
                    "internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)",
                    "internal ReadOnlyCollection<ICharacter> Characters()",
                    "internal ReadOnlyCollection<Character> CharacterEntities()",
                    "internal ReadOnlyCollection<ICharacter> Players()",
                    "internal ReadOnlyCollection<ICharacter> Npcs()",
                    "internal ReadOnlyCollection<Vendor> Vendors()",
                    "internal ReadOnlyCollection<StaticDynel> StaticDynels()",
                    "internal ReadOnlyCollection<StatelData> Statels()",
                    "internal ReadOnlyCollection<StatelData> Terminals()",
                    "internal ReadOnlyCollection<StatelData> Doors()"
                };

            Assert.IsTrue(
                registryText.Contains("internal sealed class PlayfieldDynelRegistry"),
                "PlayfieldDynelRegistry must be the named server-side dynel registry boundary.");
            for (int i = 0; i < registryApi.Length; i++)
            {
                Assert.IsTrue(
                    registryText.Contains(registryApi[i]),
                    "Missing PlayfieldDynelRegistry API: " + registryApi[i]);
            }

            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldDynelRegistry dynelRegistry"),
                "PlayfieldRuntimeSystems must own PlayfieldDynelRegistry.");
            Assert.AreEqual(
                1,
                CountOccurrences(runtimeSystemsText, "new PlayfieldDynelRegistry(playfieldIdentity)"),
                "PlayfieldRuntimeSystems must construct one dynel registry.");
            Assert.AreEqual(
                0,
                CountOccurrences(playfieldText, "new PlayfieldDynelRegistry("),
                "Playfield must not directly construct PlayfieldDynelRegistry.");

            string[] runtimeDelegations =
                {
                    "this.dynelRegistry.RefreshFromPool();",
                    "this.dynelRegistry.Register(entity);",
                    "this.dynelRegistry.Unregister(identity);",
                    "this.dynelRegistry.RegisterStatels(statels);",
                    "return this.dynelRegistry.FindByIdentity(identity);",
                    "return this.dynelRegistry.FindByIdentity<T>(identity);",
                    "return this.dynelRegistry.FindDynelsInRange(dynel, range);",
                    "return this.dynelRegistry.FindCharactersInRange(dynel, range);",
                    "return this.dynelRegistry.Characters();",
                    "return this.dynelRegistry.CharacterEntities();",
                    "return this.dynelRegistry.StaticDynels();"
                };
            for (int i = 0; i < runtimeDelegations.Length; i++)
            {
                Assert.IsTrue(
                    runtimeSystemsText.Contains(runtimeDelegations[i]),
                    "PlayfieldRuntimeSystems must delegate through registry: " + runtimeDelegations[i]);
            }

            string[] playfieldDelegations =
                {
                    "this.runtimeSystems.RegisterStatels(this.statels);",
                    "this.runtimeSystems.MaterializeStartupObjects(",
                    "return this.runtimeSystems.FindByIdentity(identity);",
                    "return this.runtimeSystems.FindByIdentity<T>(identity);",
                    "return this.runtimeSystems.FindDynelsInRange(dynel, range).ToList();",
                    "return this.runtimeSystems.FindCharactersInRange(dynel, range).ToList();",
                    "this.runtimeSystems.AnnounceMessageToCharacterClients(",
                    "this.runtimeSystems.AnnounceMessageToOtherCharacterClients(",
                    "this.runtimeSystems.SendExistingCharacterVisibilityToClient(",
                    "this.runtimeSystems.AnnounceJoiningCharacterVisibility(",
                    "this.runtimeSystems.StaticDynels()"
                };
            for (int i = 0; i < playfieldDelegations.Length; i++)
            {
                Assert.IsTrue(
                    playfieldText.Contains(playfieldDelegations[i]),
                    "Playfield must route the first safe dynel lookup slice through runtime systems: "
                    + playfieldDelegations[i]);
            }
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.ActivateNpc,")
                && runtimeSystemsText.Contains("this.RegisterDynel,")
                && runtimeSystemsText.Contains("this.RefreshDynelRegistry);"),
                "PlayfieldRuntimeSystems must route materialized NPC activation, dynel registration, and registry refresh through the registry boundary.");

            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldDynelRegistry.cs"),
                "ZoneEngine project must compile PlayfieldDynelRegistry.");
        }

        [TestMethod]
        public void PlayfieldVisibilityLookupsUseDynelRegistryBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string visibilityFanoutText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityFanoutRuntimeService.cs"));
            string visibilityPacketText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));
            string announcementText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldAnnouncementRuntimeService.cs"));
            string publishFanoutText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldPublishFanoutRuntimeService.cs"));

            Assert.IsTrue(
                runtimeSystemsText.Contains("internal ReadOnlyCollection<ICharacter> Characters()")
                && runtimeSystemsText.Contains("return this.dynelRegistry.Characters();"),
                "PlayfieldRuntimeSystems must expose current-playfield character visibility views.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("internal ReadOnlyCollection<Character> CharacterEntities()")
                && runtimeSystemsText.Contains("return this.dynelRegistry.CharacterEntities();"),
                "PlayfieldRuntimeSystems must expose concrete Character views for existing broadcast paths.");

            string announce = ExtractMethodBlock(playfieldText, "public void Announce(MessageBody messageBody)");
            string announceOthers = ExtractMethodBlock(playfieldText, "public void AnnounceOthers(MessageBody messageBody, Identity dontSend)");
            string sendScfus = ExtractMethodBlock(playfieldText, "public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)");
            string announcePlayerVisibility = ExtractMethodBlock(playfieldText, "public void AnnouncePlayerVisibility(ICharacter character)");
            string dynelDropPosition = ExtractMethodBlock(playfieldText, "private Coordinate DynelDropPosition(Identity identity)");
            string findNamed = ExtractMethodBlock(playfieldText, "public INamedEntity FindNamedEntityByIdentity(Identity identity)");

            Assert.IsTrue(
                announce.Contains("this.runtimeSystems.AnnounceMessageToCharacterClients(messageBody, this.Send);")
                && !announce.Contains("this.runtimeSystems.PublishMessageBodyToClient("),
                "Announce must delegate message fanout orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                announceOthers.Contains(
                    "this.runtimeSystems.AnnounceMessageToOtherCharacterClients(messageBody, dontSend, this.Send);")
                && !announceOthers.Contains("this.runtimeSystems.PublishMessageBodyToClient("),
                "AnnounceOthers must delegate message fanout orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                sendScfus.Contains("this.runtimeSystems.SendExistingCharacterVisibilityToClient(")
                && sendScfus.Contains("body => sendSCFUs.toClient.SendCompressed(body)")
                && !sendScfus.Contains("SimpleCharFullUpdate.ConstructMessage(temp)")
                && !sendScfus.Contains("LogUtil.Debug("),
                "SendSCFUsToClient must delegate existing-character visibility packet orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                announcePlayerVisibility.Contains("this.runtimeSystems.AnnounceJoiningCharacterVisibility(character, body => this.Announce(body));")
                && !announcePlayerVisibility.Contains("CharInPlayMessage")
                && !announcePlayerVisibility.Contains("SimpleCharFullUpdate.ConstructMessage(temp)"),
                "AnnouncePlayerVisibility must delegate joining-character visibility packet orchestration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToCharacterClients(this.CharacterEntities(), publishToCharacterClient);")
                && runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToOtherCharacterClients(")
                && runtimeSystemsText.Contains("this.visibilityPackets.SendExistingCharacterVisibilityToClient("),
                "PlayfieldRuntimeSystems must feed visibility fanout and visibility packet orchestration from registry-backed views.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.visibilityPackets.SendExistingCharacterVisibilityToClient(")
                && runtimeSystemsText.Contains("this.visibilityPackets.AnnounceJoiningCharacterVisibility("),
                "PlayfieldRuntimeSystems must route visibility packet orchestration through PlayfieldVisibilityPacketRuntimeService.");
            Assert.IsTrue(
                visibilityFanoutText.Contains("foreach (Character entity in characters)")
                && visibilityFanoutText.Contains("if (entity.Controller.Client != null)")
                && visibilityFanoutText.Contains("if (entity.Identity != excludedIdentity)")
                && visibilityFanoutText.Contains("foreach (ICharacter entity in characters)")
                && visibilityFanoutText.Contains("bool senderEqualsRecipient = entity.Identity == dontSendTo;")
                && visibilityFanoutText.Contains("bool senderInRecipientPlayfield = entity.InPlayfield(playfieldIdentity);")
                && visibilityFanoutText.Contains("sent = sendExistingCharacter(entity);")
                && visibilityFanoutText.Contains("logVisibilityCandidate(entity, senderEqualsRecipient, senderInRecipientPlayfield, sent);"),
                "Visibility fanout service must own recipient selection and iteration order.");
            Assert.IsFalse(
                visibilityFanoutText.Contains("SimpleCharFullUpdate")
                || visibilityFanoutText.Contains("CharInPlayMessage")
                || visibilityFanoutText.Contains("SendCompressed")
                || visibilityFanoutText.Contains("Publish(")
                || visibilityFanoutText.Contains("IMSend")
                || visibilityFanoutText.Contains("LogUtil")
                || visibilityFanoutText.Contains("PacketSequencing")
                || visibilityFanoutText.Contains("Pool.Instance"),
                "Visibility fanout service must not own packet construction, sends, logging, sequencing, or Pool scans.");
            Assert.IsTrue(
                visibilityPacketText.Contains("this.visibilityFanout.FanoutExistingCharactersForScfu(")
                && visibilityPacketText.Contains("this.packetSequences.RunVisibilityPacketPairSequence(")
                && visibilityPacketText.Contains("sendVisibilityMessage(simpleCharFullUpdate)")
                && visibilityPacketText.Contains("announceVisibilityMessage(charInPlay)")
                && visibilityPacketText.Contains("LogUtil.Debug("),
                "Visibility packet service must own packet pair orchestration and debug logging.");
            Assert.IsFalse(
                visibilityPacketText.Contains("SendCompressed")
                || visibilityPacketText.Contains("Publish(")
                || visibilityPacketText.Contains("Pool.Instance"),
                "Visibility packet service must not own direct transport, publish wrappers, or Pool scans.");
            Assert.IsTrue(
                announcementText.Contains("foreach (Character entity in characters)")
                && announcementText.Contains("if (entity.Controller.Client != null)")
                && announcementText.Contains("if (entity.Identity != excludedIdentity)")
                && announcementText.Contains("sendMessageBodyToClient(entity.Controller.Client, messageBody);"),
                "Announcement service must own message-recipient fanout and client-send callback ordering.");
            Assert.IsFalse(
                announcementText.Contains("SimpleCharFullUpdate")
                || announcementText.Contains("CharInPlayMessage")
                || announcementText.Contains("SendCompressed")
                || announcementText.Contains("Publish(")
                || announcementText.Contains("IMSend")
                || announcementText.Contains("LogUtil")
                || announcementText.Contains("PacketSequencing")
                || announcementText.Contains("Pool.Instance"),
                "Announcement service must not own packet construction, direct sends, publish wrappers, logging, sequencing, or Pool scans.");
            Assert.IsTrue(
                publishFanoutText.Contains("new IMSendAOtomationMessageBodyToClient")
                && publishFanoutText.Contains("new IMSendAOtomationMessageToClient")
                && publishFanoutText.Contains("announce(body)")
                && publishFanoutText.Contains("announceOthers(body, excludedIdentity)"),
                "Publish fanout service must own internal message fanout wrapper construction and playfield-message dispatch.");
            Assert.IsFalse(
                announce.Contains("new IMSendAOtomationMessageBodyToClient")
                || announceOthers.Contains("new IMSendAOtomationMessageBodyToClient"),
                "Playfield visibility broadcast methods must not own internal send-message wrapper construction.");
            Assert.IsTrue(
                dynelDropPosition.Contains("this.runtimeSystems.FindByIdentity<IDynel>(identity)"),
                "Dynel drop lookup must use registry-backed identity lookup.");
            Assert.IsTrue(
                findNamed.Contains("this.runtimeSystems.FindByIdentity<INamedEntity>(identity)"),
                "Named entity lookup must use registry-backed typed identity lookup.");

            string[] visibilityLookupBlocks =
                {
                    announce,
                    announceOthers,
                    sendScfus,
                    visibilityFanoutText,
                    dynelDropPosition,
                    findNamed
                };
            for (int i = 0; i < visibilityLookupBlocks.Length; i++)
            {
                Assert.IsFalse(
                    visibilityLookupBlocks[i].Contains("Pool.Instance.GetAll"),
                    "Visibility lookup blocks must not scan Pool directly.");
                Assert.IsFalse(
                    visibilityLookupBlocks[i].Contains("Pool.Instance.GetObject"),
                "Visibility lookup blocks must not use direct Pool identity lookup.");
            }
        }

        [TestMethod]
        public void PlayfieldRemainingSafeCharacterLoopsUseDynelRegistryBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string timedLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTimedLifecycleRuntimeService.cs"));

            string heartBeat = ExtractMethodBlock(playfieldText, "private void HeartBeatTimer(object sender)");
            string runtimeTimedLifecycle = ExtractMethodBlock(runtimeSystemsText, "internal void ProcessHeartbeatTimedLifecycle");
            string corpseFullUpdate =
                ExtractMethodBlock(playfieldText, "private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)");
            string stopFightingDeadTarget =
                ExtractMethodBlock(playfieldText, "internal void StopFightingDeadTarget(Identity deadTarget)");

            Assert.IsTrue(
                heartBeat.Contains("this.runtimeSystems.ProcessHeartbeatTimedLifecycle("),
                "Playfield heartbeat must route current-playfield character loops through the timed lifecycle boundary.");
            Assert.IsTrue(
                runtimeTimedLifecycle.Contains("this.Characters")
                && timedLifecycleText.Contains("characters()"),
                "Timed lifecycle boundary must use the registry-backed character view from PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                heartBeat.Contains("Pool.Instance.GetAll")
                || runtimeTimedLifecycle.Contains("Pool.Instance.GetAll")
                || timedLifecycleText.Contains("Pool.Instance.GetAll"),
                "Timed lifecycle character loop must not scan Pool directly.");

            string[] movedLoopBlocks =
                {
                    corpseFullUpdate,
                    stopFightingDeadTarget
                };
            for (int i = 0; i < movedLoopBlocks.Length; i++)
            {
                Assert.IsTrue(
                    movedLoopBlocks[i].Contains("this.runtimeSystems.Characters()"),
                    "Current-playfield character loop must use registry-backed character view.");
                Assert.IsFalse(
                    movedLoopBlocks[i].Contains("Pool.Instance.GetAll"),
                    "Current-playfield character loop must not scan Pool directly.");
            }
        }

        [TestMethod]
        public void PlayfieldDirectPoolUsageIsLimitedToNamedGlobalAndCrossPlayfieldExceptions()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            string disconnectAllClients = ExtractMethodBlock(playfieldText, "public void DisconnectAllClients()");
            string numberOfDynels = ExtractMethodBlock(playfieldText, "public int NumberOfDynels()");
            string numberOfPlayers = ExtractMethodBlock(playfieldText, "public int NumberOfPlayers()");
            string transferDestination = ExtractMethodBlock(
                playfieldText,
                "private IPlayfield ResolveOrCreatePlayfieldTransferDestination(Identity playfield)");

            string[] intentionalGlobalOrCrossPlayfieldExceptions =
                {
                    "DisconnectAllClients: global CanbeAffected character scan for server shutdown/dispose.",
                    "NumberOfDynels: global CanbeAffected count, not playfield-local registry count.",
                    "NumberOfPlayers: global CanbeAffected Character count, not playfield-local registry count.",
                    "Playfield transfer destination helper: cross-playfield Pool.GetObject<Playfield> handoff path."
                };
            Assert.AreEqual(
                4,
                intentionalGlobalOrCrossPlayfieldExceptions.Length,
                "Every direct Playfield Pool exception must be named with ownership scope.");

            Assert.IsTrue(
                disconnectAllClients.Contains(
                    "Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).ToList()"),
                intentionalGlobalOrCrossPlayfieldExceptions[0]);
            Assert.IsTrue(
                numberOfDynels.Contains("Pool.Instance.GetAll((int)IdentityType.CanbeAffected).Count()"),
                intentionalGlobalOrCrossPlayfieldExceptions[1]);
            Assert.IsTrue(
                numberOfPlayers.Contains(
                    "Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).Count()"),
                intentionalGlobalOrCrossPlayfieldExceptions[2]);
            Assert.IsTrue(
                transferDestination.Contains("Pool.Instance.GetObject<Playfield>("),
                intentionalGlobalOrCrossPlayfieldExceptions[3]);

            Assert.AreEqual(
                3,
                CountOccurrences(playfieldText, "Pool.Instance.GetAll"),
                "Future direct Playfield Pool scans are blocked unless added to this explicit exception list.");
            Assert.AreEqual(
                1,
                CountOccurrences(playfieldText, "Pool.Instance.GetObject"),
                "Future direct Playfield Pool identity lookups are blocked unless added to this explicit exception list.");
        }

        [TestMethod]
        public void AreteCleaningRobotDbSpawnSuppressionKeepsCapturedPathAndLegacyDbBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string areteContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\AreteContentModule.cs"));
            string montroyalContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MontroyalContentModule.cs"));
            string privateCityContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PrivateCityContentModule.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentCoordinator.cs"));
            string registrationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentRegistration.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs"));
            string materializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));

            Assert.IsTrue(
                npcRuntimeText.Contains("new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent)"),
                "Arete captured robot spawns must keep using CapturedAreteRobotContentProvider.");
            Assert.IsTrue(
                npcRuntimeText.Contains(
                    "new CapturedAreteRobotSpawnOrchestrator("),
                "Arete captured robot spawns must keep using CapturedAreteRobotSpawnOrchestrator.");
            Assert.IsTrue(
                orchestratorText.Contains("private readonly Action<ICharacter> activateNpc;")
                && orchestratorText.Contains("this.activateNpc(mobCharacter);"),
                "Captured robot spawns must activate through the NPCRuntimeService-owned callback.");
            Assert.IsTrue(
                areteContentText.Contains("registration.RegisterCapturedNpcSpawns();"),
                "Arete captured robot spawns must enter through content-module registration.");
            Assert.IsTrue(
                registrationText.Contains("this.playfield.SpawnCapturedNpcContent(this.playfieldIdentity);"),
                "Captured NPC spawn registration must route through Playfield into NPCRuntimeService.");
            Assert.IsFalse(
                areteContentText.Contains("CapturedAreteRobotSpawnOrchestrator"),
                "AreteContentModule must not own captured NPC runtime orchestration.");
            Assert.IsFalse(
                areteContentText.Contains("NpcPatrolReplayCoordinator"),
                "AreteContentModule must not own patrol replay runtime coordination.");
            Assert.IsTrue(
                providerText.Contains("public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()"),
                "CapturedAreteRobotContentProvider must expose captured spawn definitions.");
            Assert.IsTrue(
                orchestratorText.Contains("CapturedAreteRobotSpawnDefinition[] spawns = this.capturedRobotContent.GetSpawnDefinitions();"),
                "CapturedAreteRobotSpawnOrchestrator must load captured spawns from the provider.");
            Assert.IsTrue(
                orchestratorText.Contains("foreach (CapturedAreteRobotSpawnDefinition spawn in spawns)"),
                "CapturedAreteRobotSpawnOrchestrator must spawn each captured robot definition.");

            Assert.IsFalse(
                playfieldText.Contains("private static bool IsAreteCleaningRobotTestSpawn"),
                "Arete DB suppression predicate must not remain inline in Playfield.");
            Assert.IsTrue(
                coordinatorText.Contains("module.ShouldSuppressDbMobSpawn(playfieldInstance, mobSpawnId)"),
                "PlayfieldContentCoordinator must dispatch DB spawn suppression through content modules.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.content.ShouldSuppressDbMobSpawn(mob.Playfield, mob.Id);"),
                "PlayfieldRuntimeSystems must ask the content coordinator for DB spawn suppression.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.ShouldSuppressDbMobSpawn,"),
                "PlayfieldRuntimeSystems must pass the DB spawn suppression guard into object materialization.");

            string materializeDbMobSpawns =
                ExtractMethodBlock(materializationText, "private void MaterializeDbMobSpawns");
            int filterIndex = materializeDbMobSpawns.IndexOf("if (shouldSuppressDbMobSpawn(mob))", StringComparison.Ordinal);
            Assert.IsTrue(
                filterIndex >= 0,
                "Object materialization must still call the Arete robot suppression guard before mob stat loading.");
            int continueIndex = materializeDbMobSpawns.IndexOf("continue;", filterIndex, StringComparison.Ordinal);
            int loadStatsIndex =
                materializeDbMobSpawns.IndexOf("loadMobSpawnStats(mob).ToArray()", filterIndex, StringComparison.Ordinal);
            Assert.IsTrue(
                continueIndex > filterIndex && continueIndex < loadStatsIndex,
                "Suppressed legacy DB rows must be skipped before DB spawn stats are loaded.");

            string suppressionMethod = ExtractMethodBlock(areteContentText, "public bool ShouldSuppressDbMobSpawn");
            string coordinatorMethod = ExtractMethodBlock(coordinatorText, "public bool ShouldSuppressDbMobSpawn");
            int playfieldGateIndex = suppressionMethod.IndexOf(
                "playfieldInstance != PrivateAretePlayfieldInstance",
                StringComparison.Ordinal);
            int idSwitchIndex = suppressionMethod.IndexOf("switch (mob.Id)", StringComparison.Ordinal);
            if (idSwitchIndex < 0)
            {
                idSwitchIndex = suppressionMethod.IndexOf("switch (mobSpawnId)", StringComparison.Ordinal);
            }

            Assert.IsTrue(
                areteContentText.Contains("private const int PrivateAretePlayfieldInstance = 6553"),
                "Suppression must preserve the Arete PF 6553 constant.");
            Assert.IsTrue(playfieldGateIndex >= 0, "Suppression must remain gated to Arete PF 6553.");
            Assert.IsTrue(
                idSwitchIndex > playfieldGateIndex,
                "Suppression must check the Arete PF 6553 gate before matching DB mob row ids.");
            Assert.IsTrue(
                coordinatorMethod.Contains("return true;") && coordinatorMethod.Contains("return false;"),
                "Coordinator must suppress only when a content module owns the DB row.");
            Assert.AreEqual(5, CountOccurrences(suppressionMethod, "case "), "Only the captured legacy DB rows may be suppressed.");

            string[] suppressedDbRows =
                {
                    "2027138231",
                    "2027138245",
                    "2027138246",
                    "2027138249",
                    "2027138259"
                };
            for (int i = 0; i < suppressedDbRows.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    CountOccurrences(suppressionMethod, "case " + suppressedDbRows[i] + ":"),
                    "Legacy Arete DB row " + suppressedDbRows[i] + " must remain suppressed exactly once.");
            }

            Assert.IsFalse(
                montroyalContentText.Contains("case 2027138231:"),
                "Montroyal module must not suppress Arete DB rows.");
            Assert.IsFalse(
                privateCityContentText.Contains("case 2027138231:"),
                "Private-city module must not suppress Arete DB rows.");
            Assert.IsTrue(
                montroyalContentText.Contains("public bool ShouldSuppressDbMobSpawn")
                && montroyalContentText.Contains("return false;"),
                "Montroyal module must leave DB spawns unaffected.");
            Assert.IsTrue(
                privateCityContentText.Contains("public bool ShouldSuppressDbMobSpawn")
                && privateCityContentText.Contains("return false;"),
                "Private-city module must leave DB spawns unaffected.");
            Assert.IsTrue(
                suppressionMethod.Contains("default:") && suppressionMethod.Contains("return false;"),
                "Non-matching DB spawns, including non-Arete DB spawns, must remain unaffected.");
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
