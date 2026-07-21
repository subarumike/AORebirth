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
            string ordinaryRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string weaponItemFullUpdateText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs"));
            string visibilityPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));

            Assert.IsTrue(
                contractText.Contains("case 26092:")
                && contractText.Contains("HasCapturedAttackStartContext = true")
                && contractText.Contains("HasCapturedEquippedAttackInfo = true")
                && contractText.Contains("HasCapturedCombatStopSequence = true")
                && contractText.Contains("SendStopFightOnDeath = sendStopFightOnDeath")
                && contractText.Contains("ApplyCapturedEquippedAttackDisplayStats(character, weapon)")
                && contractText.Contains("ApplyWeaponStatIfPresent(character, weapon, StatIds.defaultattacktype)")
                && contractText.Contains("ApplyWeaponStatIfPresent(character, weapon, StatIds.damagetype)")
                && contractText.Contains("ApplyWeaponStatIfPresent(character, weapon, StatIds.weapontype)")
                && contractText.Contains("CapturedSubwayThiefMovementTransitionDelaySeconds")
                && contractText.Contains("CapturedSubwayThiefAttackInfoAmmoCount")
                && contractText.Contains("CapturedSubwayThiefAttackInfoUnknown")
                && contractText.Contains("CapturedSubwayThiefWeaponDamageMinimumOverride")
                && contractText.Contains("CapturedSubwayThiefWeaponDamageMaximumOverride"),
                "MonsterData 26092 must retain the live-derived Thief attack contract and captured weapon display stats while using the equipped weapon roll for damage.");

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
                "Thief timing and AttackInfo overrides must stay contract-gated while legacy equipped NPC fields remain unchanged; damage must flow through the equipped weapon roll.");
            Assert.IsFalse(
                coordinatorText.Contains("CombatCapturedSubwayThiefDamageSuppressed")
                || coordinatorText.Contains("AO_REBIRTH_ENABLE_SUBWAY_THIEF_DIAGNOSTIC_DAMAGE")
                || coordinatorText.Contains("capturedSubwayThiefDiagnosticDamageSent"),
                "The temporary one-hit Thief diagnostic damage gate must be removed after live proof that the client renders projectile damage.");
            Assert.IsTrue(
                ordinaryRuntimeText.Contains("playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);")
                && weaponItemFullUpdateText.Contains("SendWeaponDefinitions(ICharacter character, bool announceToPlayfield = false)")
                && weaponItemFullUpdateText.Contains("CreateWeaponDefinitionMessages(ICharacter character)")
                && weaponItemFullUpdateText.Contains("CharacterStat.Energy, ResolveEnergy(item)")
                && weaponItemFullUpdateText.Contains("return uint.MaxValue;")
                && weaponItemFullUpdateText.Contains("AddStatIfPresent(stats, CharacterStat.AttackDelay, item.GetAttribute((int)StatIds.itemdelay))")
                && weaponItemFullUpdateText.Contains("AddStatIfPresent(stats, CharacterStat.RechargeDelay, item.GetAttribute((int)StatIds.rechargedelay))")
                && visibilityPacketText.Contains("sendVisibilityMessage(simpleCharFullUpdate);")
                && visibilityPacketText.Contains("this.SendWeaponDefinitionsForVisibility(")
                && visibilityPacketText.Contains("WeaponItemFullUpdate.CreateWeaponDefinitionMessages(owner)")
                && visibilityPacketText.Contains("WeaponItemFullUpdate.LogObserverWeaponDefinition(owner, recipient, message)")
                && visibilityPacketText.Contains("this.visibilityInterest.MarkVisibleEntry(recipient, source);"),
                "Captured equipped Subway NPC weapons must enter through global interest, retain live-shaped item stats, and be replayed after SCFU but before CharInPlay.");

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
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            Assert.IsTrue(
                coordinatorText.Contains("this.AnnounceCapturedSpecialAttackSequenceContext(attacker, specialAttackSequence);")
                && coordinatorText.Contains("private void AnnounceCapturedSpecialAttackSequenceContext(")
                && coordinatorText.Contains("CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)")
                && coordinatorText.Contains("Specials = new SpecialAttack[0]")
                && coordinatorText.Contains("Unknown = 0,")
                && coordinatorText.Contains("pendingCapturedMovementTransitions")
                && coordinatorText.Contains("capturedContract.MovementTransitionDelaySeconds")
                && coordinatorText.Contains("hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("AttackInfoAmmoCount = hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("AttackInfoUnk1 = hasCapturedEquippedAttackInfo")
                && coordinatorText.Contains("weapon.GetAttribute((int)StatIds.damagebonus)")
                && coordinatorText.Contains("DamageBonus = damageBonus,"),
                "Captured equipped AttackInfo must preserve its packet shape without zeroing the equipped item's own damage bonus.");
            int contextIndex = coordinatorText.IndexOf(
                "this.AnnounceCapturedSpecialAttackSequenceContext(attacker, specialAttackSequence);",
                StringComparison.Ordinal);
            int poisonContextIndex = coordinatorText.IndexOf(
                "CreateCapturedSpecialAttacks(specialAttackSequence.SpecialAttacks)",
                StringComparison.Ordinal);
            int attackInfoIndex = coordinatorText.IndexOf(
                "this.AnnounceCombatDamage(",
                StringComparison.Ordinal);

            Assert.IsTrue(contextIndex >= 0, "Flea combat start must announce captured attack context.");
            Assert.IsTrue(poisonContextIndex >= 0, "Flea combat must expose captured natural attack templates.");
            Assert.IsTrue(attackInfoIndex > contextIndex, "Flea context must be established before AttackInfo damage.");
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
                filthFleaFactory.Contains("respawnDelaySeconds: 240.0"),
                "Filth Flea must retain the captured four-minute post-despawn respawn schedule.");
            Assert.IsTrue(
                catalogText.Contains("bool preserveFilthFleaFallback = monsterData == 17657;")
                && catalogText.Contains("preserveFilthFleaFallback ? 23 : (int?)null")
                && catalogText.Contains("preserveFilthFleaFallback ? 79 : (int?)null"),
                "Filth Flea must retain captured Subway corpse credit evidence from completed corpse full-update captures.");
        }

        [TestMethod]
        public void PvpAuthorizationDoesNotBlockHostileNpcRetaliationInHighGas()
        {
            string repositoryRoot = FindRepositoryRoot();
            string rulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PlayerVersusPlayerCombatRules.cs"));
            string playerControlledCombatant = ExtractMethodBlock(
                rulesText,
                "internal static bool IsPlayerControlledCombatant");
            string canEngage = ExtractMethodBlock(
                rulesText,
                "internal static bool CanEngagePlayerVersusPlayerCombat");

            Assert.IsTrue(
                playerControlledCombatant.Contains("IsPlayerCharacter(character)")
                && playerControlledCombatant.Contains("PetCombatRules.IsPlayerOwnedPet(character)"),
                "PvP authorization must identify only players and player-owned pets as player-controlled attackers.");
            Assert.IsTrue(
                canEngage.Contains("!IsPlayerControlledCombatant(attacker)")
                && canEngage.Contains("!IsProtectedPlayerVersusPlayerTarget(target)")
                && canEngage.Contains("return true;"),
                "Ordinary hostile NPCs must bypass player suppression-gas authorization and retain player retaliation targets.");
            AssertTextBefore(
                canEngage,
                "!IsPlayerControlledCombatant(attacker)",
                "int attackerGas = ResolveSuppressionGas(attacker);");
            Assert.IsTrue(
                canEngage.Contains("return IsPvpFlagged(attacker) || IsPvpFlagged(target);"),
                "Player and player-owned-pet combat against protected targets must remain suppression-gas gated.");
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
                && playfieldText.Contains("private bool IsValidPlayerCombatTarget(ICharacter attacker, ICharacter target)")
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
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string capturedLootDefinitions = ExtractMethodBlock(
                providerText,
                "public CapturedSubwayLootDefinition[] GetLootDefinitions()");
            string corpseRegistration = ExtractMethodBlock(
                playfieldText,
                "private void RegisterCorpse(ICharacter target, Identity corpseIdentity)");
            string corpseVisualMap = ExtractMethodBlock(
                corpseRulesText,
                "public static Dictionary<int, int> BuildMonsterDataToCorpseCatMeshMap()");

            Assert.IsTrue(
                corpseVisualMap.Contains("{ 17649, 15215 }"),
                "Disobedient Bot must use the corpse CATMesh captured in both official-live fights.");

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
            string globalLootText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            string ordinaryLootAdapterText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyLootTableAdapter.cs"));
            Assert.IsTrue(
                globalLootText.Contains("OrdinaryEnemyLootTableAdapter.Build(")
                && ordinaryLootAdapterText.Contains("loot.LevelCreditRules.FirstOrDefault")
                && ordinaryLootAdapterText.Contains("value.EnemyLevel == targetLevel")
                && ordinaryLootAdapterText.Contains("LootRollMode.WeightedOne")
                && ordinaryLootAdapterText.Contains("EmptyWeight = loot.EmptyWeight"),
                "Runtime corpse credits must adapt the observed rule for the enemy's level into the global registry.");
            Assert.IsTrue(
                corpseRegistration.Contains("GlobalLootRuntimeService.Generate(target, this.Identity.Instance)")
                && corpseRegistration.Contains("int credits = generatedLoot.Credits;")
                && corpseRegistration.Contains("CorpseLootClassFor(target, lootItems, credits)")
                && corpseRegistration.Contains("Credits = credits"),
                "Captured Bot credits must create a regular loot-bearing corpse through the shared runtime.");
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
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
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
                moduleText.Contains("OrdinaryEnemyRuntimeService")
                || moduleText.Contains("NonPlayerCharacterHandler"),
                "Subway content module must stay content-only and not own NPC runtime orchestration.");

            Assert.IsTrue(
                runtimeSystemsText.Contains("new SubwayContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the Subway content module.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\SubwayContentModule.cs")
                && projectText.Contains(@"Core\Playfields\CapturedSubwayContentProvider.cs")
                && projectText.Contains(@"Core\Playfields\OrdinaryEnemyProfile.cs")
                && projectText.Contains(@"Core\Playfields\OrdinaryEnemyCatalog.cs")
                && projectText.Contains(@"Core\Playfields\OrdinaryEnemyRuntimeService.cs"),
                "ZoneEngine project must compile the Subway content files.");

            Assert.IsTrue(
                npcRuntimeText.Contains("new CapturedSubwayContentProvider()")
                && npcRuntimeText.Contains("new OrdinaryEnemyCatalog(")
                && npcRuntimeText.Contains("new OrdinaryEnemyRuntimeService(")
                && npcRuntimeText.Contains("new WorldPopulationController(")
                && npcRuntimeText.Contains("this.worldPopulation.ActivatePlayfield(playfieldIdentity);"),
                "NPCRuntimeService must route ordinary activation through the global population controller.");

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
                orchestratorText.Contains("SetMobStat(character, StatIds.monsterdata, profile.MonsterData, profile.ConstructionMode);")
                && orchestratorText.Contains("this.activateNpc(character);")
                && orchestratorText.Contains("playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);"),
                "Captured Subway spawns must register through the global visibility-interest spawn hook while retaining existing runtime/corpse paths.");
            Assert.IsFalse(
                orchestratorText.Contains("SetMobStat(character, StatIds.catmesh")
                || orchestratorText.Contains("SetMobStat(character, StatIds.displaycatmesh"),
                "Captured Subway spawns must not overwrite template mesh stats with MonsterData ids.");
            Assert.IsTrue(
                catalogText.Contains("source.HeadMesh == 0")
                && orchestratorText.Contains("character.MeshLayer.RemoveMesh(0, 0, 0, 4);"),
                "Captured Subway no-headmesh mobs must clear template zero mesh layers to preserve live Meshes=count=0 SCFU shape.");
            Assert.IsTrue(
                scfuMessageText.Contains("public byte[] ExtendedTextureOverrideData { get; set; }")
                && scfuSerializerText.Contains("SimpleCharFullUpdateFlags.HasExtendedTextures")
                && scfuSerializerText.Contains("streamWriter.WriteBytes(scfu.ExtendedTextureOverrideData);"),
                "SimpleCharFullUpdate must be able to emit captured extended texture override data.");
            Assert.IsTrue(
                scfuPacketText.Contains("private const int SubwayPlayfieldResource = 127;")
                && catalogText.Contains("source.MonsterData == 17657")
                && catalogText.Contains("OrdinaryEnemyScfuProfile.CapturedFilthFlea")
                && scfuPacketText.Contains("CapturedSubwayFilthFleaExtendedTextureOverrideData")
                && scfuPacketText.Contains("0x4D, 0x61, 0x74, 0x65,")
                && scfuPacketText.Contains("0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x39")
                && scfuPacketText.Contains("OrdinaryEnemyScfuProfile.CapturedFilthFlea"),
                "Captured Subway Filth Flea must emit the live Material #9 extended texture override block only for PF127 monsterData 17657.");
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
                catalogText.Contains("source.MonsterData == 26092")
                && catalogText.Contains("new OrdinaryEnemyTextureProfile(0, 0x24CA, 0)")
                && catalogText.Contains("new OrdinaryEnemyTextureProfile(1, 0x2219, 0)")
                && catalogText.Contains("new OrdinaryEnemyTextureProfile(2, 0x24CC, 0)")
                && catalogText.Contains("new OrdinaryEnemyTextureProfile(3, 0x24CB, 0)")
                && catalogText.Contains("new OrdinaryEnemyTextureProfile(4, 0x24CD, 0)")
                && catalogText.Contains("new OrdinaryEnemyMeshProfile(0, 160561u, 0, 2)")
                && catalogText.Contains("new OrdinaryEnemyMeshProfile(1, 7777u, 0, 2)")
                && orchestratorText.Contains("foreach (OrdinaryEnemyTextureProfile texture in appearance.Textures)")
                && orchestratorText.Contains("foreach (OrdinaryEnemyMeshProfile mesh in appearance.Meshes)"),
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
                catalogText.Contains("source.MonsterData == 26092")
                && catalogText.Contains("0x00122002u")
                && catalogText.Contains("OrdinaryEnemyScfuProfile.CapturedThief")
                && scfuPacketText.Contains("CapturedSubwayThiefUnknown1")
                && scfuPacketText.Contains("scfu.Version = 58;")
                && scfuPacketText.Contains("scfu.Appearance.Value = ordinaryRuntime.Profile.Appearance.AppearanceValue;")
                && scfuPacketText.Contains("SimpleCharFullUpdateFlags.UnknownFlag6 | SimpleCharFullUpdateFlags.IsPet")
                && scfuPacketText.Contains("scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;")
                && scfuPacketText.Contains("ordinaryRuntime.Profile.Appearance.ScfuProfile"),
                "Captured Subway Thief must emit the live version, appearance value, unknown movement bytes, and flag mask only for PF127 monsterData 26092.");
        }

        [TestMethod]
        public void SubwayExistingPopulationAndPatrolReplayRemainLoaded()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcPatrolReplayCoordinator.cs"));
            string npcControllerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Controllers\NPCController.cs"));

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
                orchestratorText.Contains("this.patrolReplay.AssignCapturedSubwayReplay(")
                && orchestratorText.Contains("character.AddWaypoint(start, false);")
                && orchestratorText.Contains("character.AddWaypoint(end, false);")
                && orchestratorText.Contains("controller.SetCapturedPatrolReplaySegments(")
                && orchestratorText.Contains("spawn.UseSpawnAsPatrolStart)")
                && orchestratorText.Contains("controller.State = CharacterState.Patrolling;"),
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
                ExtractMethodBlock(orchestratorText, "private bool Spawn("),
                "OrdinaryEnemyRuntimeRegistry.Register(character.Identity.Instance, runtimeDefinition);",
                "this.activateNpc(character);");
            AssertTextBefore(
                ExtractMethodBlock(orchestratorText, "private bool Spawn("),
                "this.activateNpc(character);",
                "playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);");

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
        public void SubwayVisibilityIsolationDiagnosticsRemainOptInAndManifestOrdered()
        {
            string repositoryRoot = FindRepositoryRoot();
            string manifestPath = Path.Combine(
                repositoryRoot,
                @"docs\generated\subway_pf127_visibility_diagnostic_manifest.csv");
            string[] manifestLines = File.ReadAllLines(manifestPath);
            string selectionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\SubwayVisibilityDiagnosticSelection.cs"));
            string diagnosticText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\SubwayVisibilitySnapshotDiagnostics.cs"));
            string visibilityText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityPacketRuntimeService.cs"));
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string ordinaryCatalogText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
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
                selectionText.Contains("SubwayVisibilityDiagnosticConfiguration.Disabled")
                && selectionText.Contains("bool selected = current.Enabled && current.SelectedSourceInstances.Contains(sourceInstance);")
                && selectionText.Contains("return selected;")
                && selectionText.Contains("ALL_38 requires all 38 explicit manifest identities"),
                "Diagnostic selection must fail closed and require explicit identities.");
            Assert.IsTrue(
                ordinaryCatalogText.Contains("spawn.Disposition == OrdinaryEnemyRuntimeDisposition.Active")
                && ordinaryCatalogText.Contains("SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(")
                && !ordinaryGeneratorText.Contains("SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceInstance)"),
                "The unified catalog must keep only explicitly quarantined supported rows behind the opt-in selector.");

            AssertTextBefore(
                visibilityText,
                "sendVisibilityMessage(simpleCharFullUpdate);",
                "this.SendWeaponDefinitionsForVisibility(");
            AssertTextBefore(
                visibilityText,
                "this.SendWeaponDefinitionsForVisibility(",
                "sendVisibilityMessage(charInPlay);");
            AssertTextBefore(
                zoneClientText,
                "SubwayVisibilitySnapshotDiagnostics.OnSerializationStarted(messageBody);",
                "buffer = this.messageSerializer.Serialize(message);");
            AssertTextBefore(
                zoneClientText,
                "buffer = this.messageSerializer.Serialize(message);",
                "SubwayVisibilitySnapshotDiagnostics.OnSerializationCompleted(messageBody, buffer);");
            Assert.IsTrue(
                (diagnosticText.Contains("SCFU_SERIALIZATION_COMPLETED")
                 || diagnosticText.Contains("PacketPrefix(record.Kind) + \"_SERIALIZATION_COMPLETED\""))
                && diagnosticText.Contains("ENEMY_SEQUENCE_COMPLETED")
                && diagnosticText.Contains("SNAPSHOT_COMPLETED")
                && diagnosticText.Contains("total_serialized_bytes")
                && diagnosticText.Contains("last_completed_enemy_identity"),
                "Diagnostic ledger must preserve serialization, enemy, and snapshot completion markers.");
        }

        [TestMethod]
        public void SubwayOrdinaryArchetypesUseCaptureBackedTemplateFreeFramework()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string profileText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyProfile.cs"));
            string populationText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string runtimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string combatContractText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs"));
            string combatAttackRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatAttackRules.cs"));
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string globalLootText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            string ordinaryLootAdapterText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyLootTableAdapter.cs"));
            string scfuText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
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
            Assert.IsTrue(
                providerText.Contains("new CapturedSubwayWaypointDefinition(")
                && catalogText.Contains("source.Waypoints")
                && orchestratorText.Contains("foreach (OrdinaryEnemyWaypoint waypoint in spawn.Waypoints)")
                && orchestratorText.Contains("controller.State = CharacterState.Patrolling;"),
                "Captured SCFU movement paths must load where the captures supplied them.");

            Assert.IsTrue(
                catalogText.Contains("OrdinaryEnemyConstructionMode.CapturedDirect")
                && orchestratorText.Contains("if (profile.ConstructionMode == OrdinaryEnemyConstructionMode.TemplateBacked)")
                && orchestratorText.Contains("Pool.Instance.GetFreeInstance<Character>")
                && orchestratorText.Contains("ApplyStats(character, variant, profile)")
                && orchestratorText.Contains("ApplyAppearance(character, profile)")
                && orchestratorText.Contains("OrdinaryEnemyRuntimeRegistry.Register")
                && orchestratorText.Contains("character.Stats.SetBaseValueWithoutTriggering("),
                "Captured-direct ordinary NPC construction must use the standard attackable Character runtime without guessing a template.");
            Assert.IsTrue(
                scfuText.Contains("OrdinaryEnemyRuntimeRegistry.TryGet")
                && scfuText.Contains("scfu.AdditionalFlags = capturedFlags;")
                && scfuText.Contains("scfu.SuppressedFlags = ~capturedFlags;")
                && scfuText.Contains("scfu.Unknown1 = spawn.CapturedScfuUnknown1.ToArray();")
                && scfuText.Contains("appearance.Textures.Select(")
                && scfuText.Contains("appearance.Meshes.Select("),
                "SCFU construction must emit the captured ordinary appearance and exact optional-field shape.");
            Assert.IsTrue(
                catalogText.Contains("CapturedSubwayCombatCatalog.ForOrdinary(archetype)")
                && combatContractText.Contains("internal static CapturedEnemyCombatContract ForOrdinary(")
                && combatContractText.Contains("if (combat == null || !combat.Observed)")
                && combatContractText.Contains("return CapturedEnemyCombatContract.FixedAttack(")
                && combatContractText.Contains("combat.MinDamage")
                && combatContractText.Contains("combat.MaxDamage")
                && combatContractText.Contains("combat.RechargeSeconds"),
                "Ordinary combat must use the shared captured contract and fail closed when AttackInfo is unobserved.");
            Assert.IsTrue(
                catalogText.Contains("DropGroupHash = \"ordinary-enemy-profile\"")
                && globalLootText.Contains("EnsureOrdinary"),
                "Captured ordinary loot evidence must be adapted into the global registry.");

            Assert.IsTrue(
                runtimeText.Contains("new CapturedSubwayOrdinaryContentProvider()")
                && runtimeText.Contains("new OrdinaryEnemyCatalog(")
                && runtimeText.Contains("new OrdinaryEnemyRuntimeService(")
                && runtimeText.Contains("new WorldPopulationController(")
                && runtimeText.Contains("this.worldPopulation.ActivatePlayfield(playfieldIdentity);")
                && projectText.Contains(@"Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs")
                && projectText.Contains(@"Core\Playfields\OrdinaryEnemyCatalog.cs")
                && projectText.Contains(@"Core\Playfields\OrdinaryEnemyRuntimeService.cs")
                && projectText.Contains(@"Core\Playfields\WorldPopulationController.cs"),
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

            Assert.IsTrue(
                providerText.Contains("0x795451C5")
                && !providerText.Contains("0x795450A1")
                && catalogText.Contains("BuildCapturedOrdinarySpawnPolicies()")
                && catalogText.Contains("new OrdinaryEnemySpawnLevelDefinition(")
                && catalogText.Contains("OrdinaryEnemySpawnLevelMode.InclusiveRange")
                && catalogText.Contains("OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration")
                && profileText.Contains("internal sealed class OrdinaryEnemySpawnVariant")
                && profileText.Contains("internal sealed class OrdinaryEnemySpawnLevelDefinition")
                && profileText.Contains("internal sealed class OrdinaryEnemyLevelSelectionState")
                && profileText.Contains("return this.Resolve(this.MinimumLevel + offset);")
                && orchestratorText.Contains("Func<int, int> levelSelector = null")
                && orchestratorText.Contains("selectionState.ResolveForGeneration(")
                && orchestratorText.Contains("OrdinaryEnemySpawnVariant variant = spawnGeneration.SelectedVariant;")
                && !orchestratorText.Contains("bloodcreeper")
                && !orchestratorText.Contains("30379")
                && orchestratorText.Contains("ApplyStats(character, variant, profile);")
                && catalogText.Contains("BloodcreeperAutomaticAggroRadius = 7.0")
                && catalogText.Contains("RespawnPolicyKey = \"ordinary.bloodcreeper.240\"")
                && catalogText.Contains("OrdinaryEnemyEvidenceState.Policy")
                && profileText.Contains("this.RespawnEvidence == OrdinaryEnemyEvidenceState.Policy")
                && populationText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && populationText.Contains("WorldRespawnPolicyResolver.Resolve(")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(")
                && catalogText.Contains("20260716-033326,20260716-034104")
                && ordinaryLootAdapterText.Contains("loot.CreditEvidence == OrdinaryEnemyEvidenceState.Policy")
                && ordinaryLootAdapterText.Contains("LootEvidenceConfidence.Inferred")
                && combatContractText.Contains("CapturedSubwayBloodcreeperSpitInitialSeconds")
                && combatContractText.Contains("CapturedSubwayBloodcreeperBiteInitialSeconds")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperBiteMinimumDamage = 21")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperBiteMaximumDamage = 35")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperSpitMinimumDamage = 21")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperSpitMaximumDamage = 41")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperBiteLowTemplate = 121091")
                && combatAttackRulesText.Contains("CapturedSubwayBloodcreeperSpitLowTemplate = 121094")
                && corpseRulesText.Contains("{ 30379, 26978 }"),
                "Bloodcreeper must retain one ranged-level spawn, proactive aggro, dual rolled natural attacks, exact corpse visual, and captured-plus-policy credit handling.");
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
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string ordinaryProviderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string ordinaryGeneratorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"tools-temp\AOSharpCaptureAnalyzer\generate_subway_ordinary_content.py"));
            string ordinaryCatalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string ordinaryRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));

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
            Assert.IsTrue(
                ordinaryCatalogText.Contains("CapturedSubwayContentProvider.IsRuntimeQuarantined(source.SourceInstance)")
                && !ordinaryCatalogText.Contains("QuarantinedOrdinaryCapture")
                && ordinaryRuntimeText.Contains("SpawnMobFromTemplate")
                && ordinaryRuntimeText.Contains("Pool.Instance.GetFreeInstance<Character>")
                && ordinaryRuntimeText.Contains("spawn.SourceIdentity")
                && ordinaryRuntimeText.Contains("OrdinaryEnemyRuntimeRegistry.Register")
                && npcRuntimeText.Contains("OrdinaryEnemyRuntimeRegistry.Remove"),
                "Current identity allocation and NPC death/despawn lifecycle ownership must remain unchanged.");
        }

        [TestMethod]
        public void SubwayFilthFleaCorpseUsesCapturedLiveVisualTemplate()
        {
            string repositoryRoot = FindRepositoryRoot();
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string corpsePacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs"));

            Assert.IsTrue(
                catalogText.Contains("source.MonsterData == 17657")
                && catalogText.Contains("OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea")
                && corpsePacketText.Contains("OrdinaryEnemyRuntimeRegistry.TryGet")
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
                buildMethod.Contains("OrdinaryEnemyRuntimeRegistry.TryGet(")
                && buildMethod.Contains("OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea")
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
        public void SubwayOrdinaryLifecyclePolicyIsUniformAndBossesRemainSeparate()
        {
            string repositoryRoot = FindRepositoryRoot();
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            string populationText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs"));
            string encounterText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
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
                    value => value.Corpse.EmptyLifetimeSeconds == 30.0
                             && value.Corpse.UnlootedLifetimeSeconds == 120.0
                             && value.Corpse.LootedCleanupSeconds == 30.0),
                "Every regular Subway profile must use the shared 30/120/30 corpse policy.");
            Assert.IsTrue(
                corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("EmptyCorpseLifetime = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)")
                && populationText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0"),
                "Shared regular-mob lifecycle constants must remain exact.");
            Assert.IsTrue(
                encounterText.Contains("CapturedNamedBossRespawnDelay = TimeSpan.FromMinutes(10)")
                && CountOccurrences(encounterText, "1800.0,\n                1800.0,") == 3,
                "Subway bosses must retain ten-minute respawns and 30-minute corpses.");
        }

        [TestMethod]
        public void AcceptedSubwayEnemyGateRequiresWholeEnemyCoverage()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayContentProvider.cs"));
            string ordinaryProviderText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs"));
            string catalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));
            string combatContractText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs"));
            string attackRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatAttackRules.cs"));
            string movementCoordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));
            string movementRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldNpcCombatMovementRuntimeService.cs"));
            string ordinaryProfileText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyProfile.cs"));
            string ordinaryRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string heartbeatRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldCharacterHeartbeatRuntimeService.cs"));
            string weaponPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs"));
            string scfuPacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\SimpleCharFullUpdate.cs"));
            string corpsePacketText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            string worldPopulationControllerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs"));
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
            int discardedPetContractStart = combatContractText.IndexOf(
                "case 17720:",
                StringComparison.Ordinal);
            int discardedPetContractEnd = combatContractText.IndexOf(
                "case 17649:",
                discardedPetContractStart,
                StringComparison.Ordinal);
            Assert.IsTrue(
                discardedPetContractStart >= 0
                && discardedPetContractEnd > discardedPetContractStart);
            string discardedPetContractCase = combatContractText.Substring(
                discardedPetContractStart,
                discardedPetContractEnd - discardedPetContractStart);
            string disobedientBotDefinition = ExtractMethodBlock(
                providerText,
                "private static CapturedSubwaySpawnDefinition DisobedientBot(");
            string ordinaryCombatContract = ExtractMethodBlock(
                combatContractText,
                "internal static CapturedEnemyCombatContract ForOrdinary(");
            string workmanStrikerCombatContract = ExtractMethodBlock(
                combatContractText,
                "private static CapturedEnemyCombatContract ForWorkmanStriker(");
            string meldedPatternsCombatContract = ExtractMethodBlock(
                combatContractText,
                "private static CapturedEnemyCombatContract ForMeldedPatterns(");
            string sourceSpecificWeaponCombatContract = ExtractMethodBlock(
                combatContractText,
                "private static CapturedEnemyCombatContract ForSourceSpecificWeaponArchetype(");
            string looterCombatContract = ExtractMethodBlock(
                combatContractText,
                "private static CapturedEnemyCombatContract ForLooter(");
            string muggerCombatContract = ExtractMethodBlock(
                combatContractText,
                "internal static CapturedEnemyCombatContract ForSupportedSourceWeapon(");
            string derangedShopperCombatContract = ExtractMethodBlock(
                combatContractText,
                "private static CapturedEnemyCombatContract ForDerangedShopper(");
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

            Assert.IsTrue(
                providerText.Contains("private static CapturedSubwaySpawnDefinition FilthFlea(")
                && catalogText.Contains("SubwayOrdinaryRespawnSeconds = 240.0")
                && catalogText.Contains("SubwayOrdinaryRespawnPolicy()")
                && providerText.Contains("Filth Flea: 18 complete official-live corpse opens")
                && combatContractText.Contains("case 17657:")
                && combatContractText.Contains("CapturedEnemyCombatContract.CapturedSpecialSequence(")
                && attackRulesText.Contains("CapturedSubwayFilthFleaMonsterData = 17657")
                && movementCoordinatorText.Contains("AnnounceCapturedSpecialAttackSequenceContext(")
                && movementCoordinatorText.Contains("CreateCapturedSpecialAttacks(")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && catalogText.Contains("source.MonsterData == 17657")
                && catalogText.Contains("OrdinaryEnemyScfuProfile.CapturedFilthFlea")
                && scfuPacketText.Contains("CapturedSubwayFilthFleaExtendedTextureOverrideData")
                && corpsePacketText.Contains("CapturedSubwayFilthFleaPacketLength = 457")
                && corpsePacketText.Contains("BuildCapturedSubwayFilthFlea(")
                && catalogText.Contains("bool preserveFilthFleaFallback = monsterData == 17657;")
                && catalogText.Contains("preserveFilthFleaFallback ? 23 : (int?)null")
                && catalogText.Contains("preserveFilthFleaFallback ? 79 : (int?)null"),
                "Accepted Subway Filth Flea must keep spawn, movement/chase, combat, appearance, corpse visual, loot, credits, and four-minute respawn coverage together.");

            Assert.IsTrue(
                ordinaryProviderText.Contains("\"slum_runner\"")
                && ordinaryProviderText.Contains("\"Slum Runner\"")
                && ordinaryProviderText.Contains("55648")
                && ordinaryProviderText.Contains("new CapturedSubwayCombatEvidenceDefinition(")
                && ordinaryProviderText.Contains("4.210628")
                && ordinaryProviderText.Contains("31774")
                && ordinaryProviderText.Contains("20260716-222201")
                && catalogText.Contains("SubwayOrdinaryRespawnSeconds = 240.0")
                && catalogText.Contains("PF127 Subway regular mobs use the shared 240-second respawn policy")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Slum Runner must keep its 24 exact spawns, expanded captured attack cadence and loot sample, shared chase, CATMesh/credits, shared four-minute respawn, and ordinary corpse lifetimes together.");

            Assert.IsTrue(
                ordinaryProviderText.Contains("\"molested_molecules\"")
                && ordinaryProviderText.Contains("\"Molested Molecules\"")
                && ordinaryProviderText.Contains("203746")
                && ordinaryProviderText.Contains("4.749995")
                && ordinaryProviderText.Contains("new CapturedSubwayCombatEvidenceDefinition(")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(27199, 27199, 10, 1, 3, 3333)")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(121743, 121744, 25, 1, 3, 3333)")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(301712, 301712, 1, 1, 3, 3333)")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(301713, 301713, 1, 1, 3, 3333)")
                && ordinaryProviderText.Contains("20260716-034104")
                && ordinaryProviderText.Contains("20260716-221358")
                && ordinaryProviderText.Contains("203746, 5921")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Molested Molecules must keep its nine exact spawns, captured attack range/cadence, shared chase, three strict loot outcomes, CATMesh/credits, private four-minute respawn policy, and ordinary corpse lifetimes together.");

            Assert.AreEqual(
                33,
                CountOccurrences(ordinaryProviderText, "\"shadow\""),
                "Accepted Subway Shadow must preserve its profile keys and all 31 exact spawn rows.");
            Assert.IsTrue(
                ordinaryProviderText.Contains("\"Shadow\"")
                && ordinaryProviderText.Contains("30464")
                && ordinaryProviderText.Contains("5.299336")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(234875, 234875, 1, 2, 15, 1333)")
                && CountOccurrences(ordinaryProviderText, ", 30464, 30434,") == 20
                && ordinaryProfiles.Single(value => value.DisplayName == "Shadow").Loot.ObservedEmptyInventories == 7
                && !ordinaryProfiles.Single(value => value.DisplayName == "Shadow").Loot.ItemPoolComplete
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && !ordinaryCombatContract.Contains("critical")
                && generatedCombatReportText.Contains("\"Shadow\":")
                && generatedCombatReportText.Contains("\"normalAttackInfoRows\": 56")
                && generatedCombatReportText.Contains("\"normalMinDamage\": 11")
                && generatedCombatReportText.Contains("\"normalMaxDamage\": 39")
                && generatedCombatReportText.Contains("\"criticalAttackInfoRows\": 2")
                && generatedCombatReportText.Contains("\"criticalMinDamage\": 30")
                && generatedCombatReportText.Contains("\"criticalMaxDamage\": 44")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && worldPopulationControllerText.Contains("DelayStartsAt = RespawnDelayStartsAt.NpcDespawn")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Shadow must keep 31 exact spawns, fixed normal-only combat, report-only criticals, shared chase, 15 strict incomplete-pool loot outcomes, CATMesh/credits, private four-minute respawn, and ordinary corpse lifetimes together.");

            Assert.AreEqual(
                14,
                CountOccurrences(ordinaryProviderText, "\"infector\""),
                "Accepted ordinary Subway Infector must preserve its profile keys and all 12 exact spawn rows.");
            Assert.IsTrue(
                ordinaryProviderText.Contains("\"Infector\"")
                && ordinaryProviderText.Contains("31909")
                && ordinaryProviderText.Contains("5.049360")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(101735, 101736, 21, 1, 14, 714)")
                && CountOccurrences(ordinaryProviderText, ", 31909, 31868,") == 23
                && ordinaryProfiles.Single(value => value.DisplayName == "Infector").Loot.ObservedEmptyInventories == 8
                && !ordinaryProfiles.Single(value => value.DisplayName == "Infector").Loot.ItemPoolComplete
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && !ordinaryCombatContract.Contains("31909")
                && combatContractText.Contains("case 31909:")
                && generatedCombatReportText.Contains("\"Infector\":")
                && generatedCombatReportText.Contains("\"normalAttackInfoRows\": 54")
                && generatedCombatReportText.Contains("\"normalMinDamage\": 15")
                && generatedCombatReportText.Contains("\"normalMaxDamage\": 36")
                && generatedCombatReportText.Contains("\"criticalAttackInfoRows\": 3")
                && generatedCombatReportText.Contains("\"criticalMinDamage\": 52")
                && generatedCombatReportText.Contains("\"criticalMaxDamage\": 75")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted ordinary Subway Infector must keep 12 exact spawns, 23 exact credit corpses, its generic fixed normal contract isolated from Abmouth-owned specialization, report-only criticals, strict incomplete-pool loot, CATMesh/credits, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

            Assert.AreEqual(
                8,
                CountOccurrences(ordinaryProviderText, "\"architect_striker\""),
                "Accepted Subway Architect Striker must preserve its profile key and all seven exact spawn rows.");
            Assert.IsTrue(
                ordinaryProviderText.Contains("\"Architect Striker\"")
                && ordinaryProviderText.Contains("203743")
                && ordinaryProviderText.Contains("5.425420")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(122482, 122483, 14, 1, 6, 1667)")
                && CountOccurrences(ordinaryProviderText, ", 203743, 17870,") == 6
                && ordinaryProfiles.Single(value => value.DisplayName == "Architect Striker").Loot.ObservedCompleteInventories == 6
                && ordinaryProfiles.Single(value => value.DisplayName == "Architect Striker").Loot.ObservedEmptyInventories == 1
                && !ordinaryProfiles.Single(value => value.DisplayName == "Architect Striker").Loot.ItemPoolComplete
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && architectStrikerCombatReport.Contains("\"normalAttackInfoRows\": 18")
                && architectStrikerCombatReport.Contains("\"normalMinDamage\": 10")
                && architectStrikerCombatReport.Contains("\"normalMaxDamage\": 17")
                && architectStrikerCombatReport.Contains("\"criticalAttackInfoRows\": 1")
                && architectStrikerCombatReport.Contains("\"criticalMinDamage\": 38")
                && architectStrikerCombatReport.Contains("\"criticalMaxDamage\": 38")
                && architectStrikerCombatReport.Contains("\"missedAttackInfoRows\": 1")
                && architectStrikerCombatReport.Contains("\"specialAttackWeaponRows\": 2")
                && CountOccurrences(architectStrikerCombatReport, "\"unknown1\": 87") == 1
                && CountOccurrences(architectStrikerCombatReport, "\"unknown2\": 87") == 1
                && CountOccurrences(architectStrikerCombatReport, "\"unknown3\": 87") == 1
                && CountOccurrences(architectStrikerCombatReport, "\"unknown4\": 87") == 1
                && CountOccurrences(architectStrikerCombatReport, "\"unknown5\": 0") == 1
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Architect Striker must keep seven exact spawns, its captured fixed normal contract without an invented weapon, report-only critical, strict incomplete-pool loot, CATMesh/credits, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

            OrdinaryEnemyProfile infectedAttendant = ordinaryProfiles.Single(
                value => value.DisplayName == "Infected Attendant");
            OrdinaryEnemySpawnDefinition[] infectedAttendantSpawns = ordinarySpawns
                .Where(value => value.ProfileKey == infectedAttendant.ProfileKey)
                .ToArray();
            Assert.AreEqual(5, infectedAttendantSpawns.Length);
            Assert.IsTrue(infectedAttendant.Combat.Contract.IsCombatReady);
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
                && strikeForemanCombatReport.Contains("\"runtimeStatus\": \"report-only-dormant\"")
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
                "Strike Foreman must keep six local 13-point normals and two misses separate from the older other-player 18/18/40 evidence, retain two atomic observed loot outcomes without treating them as guarantees, and remain named/report-only/dormant.");

            Assert.AreEqual(
                12,
                CountOccurrences(ordinaryProviderText, "\"melded_patterns\""),
                "Accepted Subway Melded Patterns must preserve its profile keys and all ten exact spawn rows.");
            Assert.IsTrue(
                ordinaryProviderText.Contains("\"Melded Patterns\"")
                && ordinaryProviderText.Contains("203747")
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(122672, 122673, 15, 1, 4, 2500)")
                && CountOccurrences(ordinaryProviderText, ", 203747, 23368,") == 10
                && ordinaryProfiles.Single(value => value.DisplayName == "Melded Patterns").Loot.ObservedEmptyInventories == 1
                && !ordinaryProfiles.Single(value => value.DisplayName == "Melded Patterns").Loot.ItemPoolComplete
                && meldedPatternsCombatContract.Contains("20260716-034559")
                && meldedPatternsCombatContract.Contains("combat.ObservedRows == 7")
                && meldedPatternsCombatContract.Contains("combat.MinDamage == 21")
                && meldedPatternsCombatContract.Contains("combat.MaxDamage == 34")
                && meldedPatternsCombatContract.Contains("CapturedEnemyCombatContract.EquippedWeapon(")
                && meldedPatternsCombatContract.Contains("CapturedSubwayMeldedPatternsWeaponLowTemplate")
                && meldedPatternsCombatContract.Contains("CapturedSubwayMeldedPatternsWeaponHighTemplate")
                && meldedPatternsCombatContract.Contains("CapturedSubwayMeldedPatternsWeaponQuality")
                && !meldedPatternsCombatContract.Contains("FixedAttack(")
                && !meldedPatternsCombatContract.Contains("EquippedWeaponWithEmptySpecialAttackContext(")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Melded Patterns must keep ten exact spawns, exact QL20 weapon-owned damage/recharge without invented attack context, strict incomplete-pool loot, CATMesh/credits, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.IsTrue(
                ordinaryProviderText.Contains("\"Workman Striker\"")
                && ordinaryProviderText.Contains("203854")
                && ordinaryProviderText.Contains("5.139163")
                && CountOccurrences(ordinaryProviderText, "new CapturedSubwaySourceWeaponEvidenceDefinition(") == 32
                && CountOccurrences(ordinaryProviderText, "new CapturedSubwayGenerationVariantDefinition(203854,") == 31
                && ordinaryProviderText.Contains("new CapturedSubwayLootEvidenceDefinition(202719, 202720, 14, 2, 30, 667)")
                && CountOccurrences(ordinaryProviderText, ", 203854, 17899,") == 40
                && workmanStriker.Loot.PoolMode == OrdinaryEnemyLootPoolMode.IndependentEntries
                && !workmanStriker.Loot.ItemPoolComplete
                && workmanStriker.Loot.ObservedCompleteInventories == 30
                && workmanStriker.Loot.ObservedEmptyInventories == 8
                && catalogText.Contains("archetype.MonsterData == WorkmanStrikerMonsterData")
                && catalogText.Contains("CapturedSubwayCombatCatalog.ForOrdinary(")
                && ordinaryRuntimeText.Contains("profile.Combat.ResolveContract(spawn.SourceIdentity, variant)")
                && workmanStrikerCombatContract.Contains("requires a selected capture-reviewed atomic generation variant")
                && combatContractText.Contains("combat != null && combat.Observed")
                && combatContractText.Contains("Workman Striker combat requires one exact reviewed atomic level/stat/weapon generation")
                && combatContractText.Contains("CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(")
                && combatContractText.Contains("captured SIW shapes remain report-only")
                && workmanStrikerCombatReport.Contains("\"normalAttackInfoRows\": 59")
                && workmanStrikerCombatReport.Contains("\"normalMinDamage\": 9")
                && workmanStrikerCombatReport.Contains("\"normalMaxDamage\": 23")
                && workmanStrikerCombatReport.Contains("\"criticalAttackInfoRows\": 7")
                && workmanStrikerCombatReport.Contains("\"criticalMinDamage\": 28")
                && workmanStrikerCombatReport.Contains("\"criticalMaxDamage\": 42")
                && workmanStrikerCombatReport.Contains("\"missedAttackInfoRows\": 14")
                && workmanStrikerCombatReport.Contains("\"specialAttackWeaponRows\": 20")
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown1\": 100") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown2\": 100") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown3\": 100") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown4\": 100") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown1\": 72") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown2\": 72") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown3\": 72") == 1
                && CountOccurrences(workmanStrikerCombatReport, "\"unknown4\": 72") == 1
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Workman Striker must keep 22 exact sources, 31 capture-reviewed atomic generations, item-owned normal damage/recharge, captured AttackInfo, report-only critical/SIW evidence, strict incomplete-pool loot, CATMesh/credits, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

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
                    Assert.AreEqual(
                        CapturedEnemyAttackModel.EquippedWeapon,
                        contract.AttackModel,
                        contract.Evidence);
                    Assert.IsTrue(contract.IsCombatReady);
                    Assert.AreEqual(variant.WeaponLoadout.LowId, contract.WeaponLowId);
                    Assert.AreEqual(variant.WeaponLoadout.HighId, contract.WeaponHighId);
                    Assert.AreEqual(variant.WeaponLoadout.Quality, contract.WeaponQuality);
                    Assert.AreEqual(6, contract.WeaponInventorySlot);
                    Assert.AreEqual(0, contract.MinDamage);
                    Assert.AreEqual(0, contract.MaxDamage);
                    Assert.AreEqual(0.0, contract.RechargeSeconds);
                    Assert.IsTrue(contract.HasCapturedEquippedAttackInfo);
                    Assert.AreEqual(-1, contract.AttackInfoAmmoCount);
                    Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
                    Assert.AreEqual(0, contract.AttackInfoUnknown);
                    Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
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
            Assert.AreEqual(30.0, looter.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, looter.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, looter.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                looterSpawns.All(
                    value => looter.Combat.ResolveContract(
                            value.SourceIdentity,
                            value.Level).AttackModel
                        == CapturedEnemyAttackModel.EquippedWeapon)
                && CountOccurrences(ordinaryProviderText, ", 203745, 17870,") == 11
                && catalogText.Contains("archetype.MonsterData == LooterMonsterData")
                && looterCombatContract.Contains("ForSourceSpecificWeaponArchetype")
                && ordinaryCombatContract.Contains("aggregate weapon fallback is forbidden")
                && sourceSpecificWeaponCombatContract.Contains("if (matches != 1 || matched == null)")
                && sourceSpecificWeaponCombatContract.Contains("item owns normal damage and recharge")
                && !sourceSpecificWeaponCombatContract.Contains("specialAttackWeapon")
                && generatedCombatReportText.Contains("\"Looter\":")
                && generatedCombatReportText.Contains("\"normalAttackInfoRows\": 15")
                && generatedCombatReportText.Contains("\"normalMinDamage\": 11")
                && generatedCombatReportText.Contains("\"normalMaxDamage\": 11")
                && generatedCombatReportText.Contains("\"criticalAttackInfoRows\": 1")
                && generatedCombatReportText.Contains("\"criticalMinDamage\": 25")
                && generatedCombatReportText.Contains("\"criticalMaxDamage\": 25")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Looter must keep eight exact source weapons and dispositions, fail-closed aggregate/missing/conflicting/unknown selection, item-owned visible weapon damage/recharge, report-only critical, strict incomplete-pool loot, CATMesh/credits, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.AreEqual(OrdinaryEnemyAggressionMode.Retaliate, mugger.Aggression.Mode);
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
                Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                Assert.IsTrue(contract.IsCombatReady);
                Assert.AreEqual(121567, contract.WeaponLowId);
                Assert.AreEqual(121567, contract.WeaponHighId);
                Assert.AreEqual(1, contract.WeaponQuality);
                Assert.AreEqual(6, contract.WeaponInventorySlot);
                Assert.AreEqual(0, contract.MinDamage);
                Assert.AreEqual(0, contract.MaxDamage);
                Assert.AreEqual(0.0, contract.RechargeSeconds);
                Assert.IsTrue(contract.HasCapturedEquippedAttackInfo);
                Assert.AreEqual(-1, contract.AttackInfoAmmoCount);
                Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
                Assert.AreEqual(0, contract.AttackInfoUnknown);
                Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
                Assert.IsFalse(contract.HasEmptySpecialAttackWeaponContext);
                Assert.IsFalse(contract.HasCapturedAttackStartContext);
                Assert.IsFalse(contract.HasCapturedCombatStopSequence);
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
            Assert.AreEqual(30.0, mugger.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, mugger.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, mugger.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                muggerSpawns.All(
                    value => mugger.Combat.ResolveContract(
                            value.SourceIdentity,
                            value.Level).AttackModel
                        == CapturedEnemyAttackModel.EquippedWeapon)
                && CountOccurrences(ordinaryProviderText, ", 203734, 17534,") == 25
                && combatContractText.Contains("Mugger combat requires an exact captured source identity; aggregate weapon fallback is forbidden")
                && muggerCombatContract.Contains("HasCompleteMuggerSourceWeaponEvidence")
                && muggerCombatContract.Contains("if (matches != 1 || matched == null)")
                && muggerCombatContract.Contains("EquippedWeaponWithCapturedAttackInfo")
                && muggerCombatContract.Contains("item owns runtime damage, damage bonus, and recharge")
                && muggerCombatContract.Contains("criticals are report-only")
                && muggerCombatContract.Contains("no empty SIW or captured attack-start/stop context")
                && ordinaryRuntimeText.Contains("profile.Combat.ResolveContract(spawn.SourceIdentity, variant)")
                && muggerCombatReport.Contains("\"normalAttackInfoRows\": 38")
                && muggerCombatReport.Contains("\"normalMinDamage\": 9")
                && muggerCombatReport.Contains("\"normalMaxDamage\": 12")
                && muggerCombatReport.Contains("\"criticalAttackInfoRows\": 3")
                && muggerCombatReport.Contains("\"criticalMinDamage\": 21")
                && muggerCombatReport.Contains("\"criticalMaxDamage\": 21")
                && muggerCombatReport.Contains("\"medianRechargeSeconds\": 5.816469")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && worldPopulationControllerText.Contains("DelayStartsAt = RespawnDelayStartsAt.NpcDespawn")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Mugger must keep all nine exact source weapons, spawn levels, and dispositions; fail-closed aggregate/missing/conflicting/unknown selection; item-owned damage/recharge with captured AttackInfo shape; report-only criticals; strict 18-open incomplete-pool loot; exact CATMesh/level credits; shared chase; private four-minute respawn; and ordinary corpse lifetimes together.");

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
            Assert.IsTrue(derangedShopperContract.IsCombatReady);
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
            Assert.AreEqual(30.0, derangedShopper.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, derangedShopper.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, derangedShopper.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                ordinaryCombatContract.Contains("DerangedShopperMonsterData")
                && derangedShopperCombatContract.Contains("evidence.Length != 1")
                && derangedShopperCombatContract.Contains("125454")
                && derangedShopperCombatContract.Contains("125455")
                && derangedShopperCombatContract.Contains("EquippedWeaponWithCapturedAttackInfo")
                && derangedShopperCombatContract.Contains("ten normal local-player hits span 7..15")
                && derangedShopperCombatContract.Contains("one 27-point critical is report-only")
                && derangedShopperCombatContract.Contains("six captured misses")
                && derangedShopperCombatContract.Contains("empty SpecialAttackWeapon 56/45/45/45/0")
                && derangedShopperCombatContract.Contains("attack-start, StopFight, and death context")
                && derangedShopperCombatContract.Contains("item owns runtime damage, damage bonus, and recharge")
                && derangedShopperCombatContract.Contains("runtime behavior is unchanged")
                && ordinaryRuntimeText.Contains("profile.Combat.ResolveContract(spawn.SourceIdentity, variant)")
                && derangedShopperCombatReport.Contains("\"normalAttackInfoRows\": 10")
                && derangedShopperCombatReport.Contains("\"normalMinDamage\": 7")
                && derangedShopperCombatReport.Contains("\"normalMaxDamage\": 15")
                && derangedShopperCombatReport.Contains("\"criticalAttackInfoRows\": 1")
                && derangedShopperCombatReport.Contains("\"criticalMinDamage\": 27")
                && derangedShopperCombatReport.Contains("\"criticalMaxDamage\": 27")
                && derangedShopperCombatReport.Contains("\"missedAttackInfoRows\": 7")
                && derangedShopperCombatReport.Contains("\"missedAttackShapes\": [")
                && derangedShopperCombatReport.Contains("\"ammoCount\": -1")
                && derangedShopperCombatReport.Contains("\"weaponSlot\": 6")
                && derangedShopperCombatReport.Contains("\"unknown\": 0")
                && derangedShopperCombatReport.Contains("\"rows\": 7")
                && derangedShopperCombatReport.Contains("\"specialAttackWeaponRows\": 1")
                && derangedShopperCombatReport.Contains("\"unknown1\": 56")
                && derangedShopperCombatReport.Contains("\"unknown2\": 45")
                && derangedShopperCombatReport.Contains("\"unknown3\": 45")
                && derangedShopperCombatReport.Contains("\"unknown4\": 45")
                && derangedShopperCombatReport.Contains("\"unknown5\": 0")
                && derangedShopperCombatReport.Contains("\"equippedWeaponShapes\": [")
                && derangedShopperCombatReport.Contains("\"lowId\": 125454")
                && derangedShopperCombatReport.Contains("\"highId\": 125455")
                && derangedShopperCombatReport.Contains("\"quality\": 8")
                && derangedShopperCombatReport.Contains("20260710-202132")
                && derangedShopperCombatReport.Contains("(SimpleChar:79574527)")
                && derangedShopperCombatReport.Contains("20260720-031025")
                && derangedShopperCombatReport.Contains("(SimpleChar:79803651)")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && worldPopulationControllerText.Contains("DelayStartsAt = RespawnDelayStartsAt.NpcDespawn")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Deranged Shopper must keep its one active source, exact QL8 source-owned weapon and captured AttackInfo shape, fail-closed aggregate/unknown/missing/conflicting selection, item-owned damage/recharge, ten normal hits at 7..15, report-only critical, seven aggregate misses, evidence-only SIW/start/stop/death context, strict three-open incomplete-pool loot, exact CATMesh/credits, shared chase, inherited private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.IsTrue(discardedPet.Combat.Contract.IsCombatReady);
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
            Assert.AreEqual(30.0, discardedPet.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, discardedPet.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, discardedPet.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                combatContractText.Contains("case 17720:")
                && combatContractText.Contains("AttackInfoAmmoCount = attackInfoAmmoCount")
                && discardedPetContractCase.Contains("CapturedSubwayDiscardedPetWeaponTag")
                && discardedPetContractCase.Contains("-1")
                && discardedPet.Combat.Contract.Evidence.Contains("37 normal local-player")
                && discardedPet.Combat.Contract.Evidence.Contains("criticals remain report-only")
                && discardedPet.Combat.Contract.Evidence.Contains("conventional median 5.089763")
                && discardedPetCombatReport.Contains("\"normalAttackInfoRows\": 37")
                && discardedPetCombatReport.Contains("\"normalMinDamage\": 9")
                && discardedPetCombatReport.Contains("\"normalMaxDamage\": 18")
                && discardedPetCombatReport.Contains("\"criticalAttackInfoRows\": 4")
                && discardedPetCombatReport.Contains("\"criticalMinDamage\": 30")
                && discardedPetCombatReport.Contains("\"criticalMaxDamage\": 33")
                && discardedPetCombatReport.Contains("\"medianRechargeSeconds\": 5.079568")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && worldPopulationControllerText.Contains("DelayStartsAt = RespawnDelayStartsAt.NpcDespawn")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Discarded Pet must keep all 29 exact active spawns, captured SIW1 9..18 normal roll and cadence, report-only critical observations, retaliatory chase without proactive aggro or return-to-spawn, strict 16-open incomplete-pool loot, exact CATMesh/level credits, inherited private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.AreEqual(30.0, bloodcreeper.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, bloodcreeper.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, bloodcreeper.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                CountOccurrences(ordinaryProviderText, ", 30379, 26978,") == 1
                && combatContractText.Contains("CapturedSubwayBloodcreeperSpitInitialSeconds")
                && combatContractText.Contains("CapturedSubwayBloodcreeperSpitRechargeSeconds")
                && combatContractText.Contains("CapturedSubwayBloodcreeperBiteInitialSeconds")
                && combatContractText.Contains("CapturedSubwayBloodcreeperBiteRechargeSeconds")
                && combatContractText.Contains("CapturedSubwayBloodcreeperSpecialAttackWeaponLastValue")
                && catalogText.Contains("SubwayOrdinaryRespawnPolicy()")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Bloodcreeper must keep its one exact active spawn, L15..25 generation policy, auto aggro and chase, dual captured natural attacks, strict incomplete-pool loot, L24 exact/private-band credit policy, CATMesh, inherited private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.AreEqual(30.0, stimFiend.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, stimFiend.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, stimFiend.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                CountOccurrences(ordinaryProviderText, ", 203739, 5907,") == 15
                && generatedCombatReportText.Contains("\"Stim Fiend\":")
                && generatedCombatReportText.Contains("\"normalAttackInfoRows\": 13")
                && generatedCombatReportText.Contains("\"normalMinDamage\": 10")
                && generatedCombatReportText.Contains("\"normalMaxDamage\": 16")
                && generatedCombatReportText.Contains("\"criticalAttackInfoRows\": 0")
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Stim Fiend must keep 15 exact spawn dispositions, captured fixed normal-only combat, strict incomplete-pool loot, only observed level-credit rows with L17 unresolved, CATMesh, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.AreEqual(30.0, neuralBurnout.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, neuralBurnout.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, neuralBurnout.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                CountOccurrences(ordinaryProviderText, ", 203730, 5941,") == 9
                && generatedCombatReportText.Contains("\"Neural Burnout\":")
                && generatedCombatReportText.Contains("\"normalAttackInfoRows\": 7")
                && generatedCombatReportText.Contains("\"normalMinDamage\": 15")
                && generatedCombatReportText.Contains("\"normalMaxDamage\": 22")
                && generatedCombatReportText.Contains("\"criticalAttackInfoRows\": 1")
                && generatedCombatReportText.Contains("\"criticalMinDamage\": 51")
                && generatedCombatReportText.Contains("\"criticalMaxDamage\": 51")
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Neural Burnout must keep seven exact active spawns, captured fixed normal combat with report-only critical, strict incomplete-pool loot, only observed level-credit rows with L22 unresolved, CATMesh, shared chase, private four-minute respawn, and ordinary corpse lifetimes together.");

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
            Assert.IsTrue(uncontrollableAnger.Combat.Contract.IsCombatReady);
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
            Assert.AreEqual(30.0, uncontrollableAnger.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, uncontrollableAnger.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, uncontrollableAnger.Corpse.LootedCleanupSeconds);
            Assert.IsTrue(
                CountOccurrences(ordinaryProviderText, ", 96195, 96177,") == 8
                && uncontrollableAngerCombatReport.Contains("\"retaliationRows\": 12")
                && uncontrollableAngerCombatReport.Contains("\"normalAttackInfoRows\": 7")
                && uncontrollableAngerCombatReport.Contains("\"normalMinDamage\": 9")
                && uncontrollableAngerCombatReport.Contains("\"normalMaxDamage\": 18")
                && uncontrollableAngerCombatReport.Contains("\"criticalAttackInfoRows\": 1")
                && uncontrollableAngerCombatReport.Contains("\"criticalMinDamage\": 19")
                && uncontrollableAngerCombatReport.Contains("\"criticalMaxDamage\": 19")
                && uncontrollableAngerCombatReport.Contains("\"missedAttackInfoRows\": 9")
                && uncontrollableAngerCombatReport.Contains("\"attackInfoRows\": 4")
                && uncontrollableAngerCombatReport.Contains("\"minDamage\": 25")
                && uncontrollableAngerCombatReport.Contains("\"maxDamage\": 42")
                && uncontrollableAngerCombatReport.Contains("\"attackInfoRows\": 1")
                && uncontrollableAngerCombatReport.Contains("\"minDamage\": 19")
                && uncontrollableAngerCombatReport.Contains("\"maxDamage\": 19")
                && uncontrollableAngerCombatReport.Contains("\"reviewedTargetCadence\"")
                && uncontrollableAngerCombatReport.Contains("5.1165513")
                && uncontrollableAngerCombatReport.Contains("5.1671525")
                && uncontrollableAngerCombatReport.Contains("10.1003489")
                && uncontrollableAngerCombatReport.Contains("\"runtimeRechargeSeconds\": 5.167153")
                && ordinaryCombatContract.Contains("CapturedEnemyCombatContract.FixedAttack(")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue")
                && worldPopulationControllerText.Contains("OrdinaryEnemyDefaultRespawnSeconds = 240.0")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Uncontrollable Anger must keep six exact active spawns, two captured patrols, local-player damage separated from Killer-pet and other-player evidence, the reviewed full cadence window, strict loot and exact observed credits, CATMesh, shared chase, inherited private respawn, and ordinary corpse lifetimes together.");

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
                                            .IsCombatReady)),
                "Accepted Subway Incomplete Rebuild must preserve all ten exact sources, 23 atomic capture-reviewed generations, exact per-generation weapons, and private four-minute respawn together.");
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
            Assert.AreEqual(30.0, incompleteRebuild.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, incompleteRebuild.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, incompleteRebuild.Corpse.LootedCleanupSeconds);

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
                    Assert.AreEqual(CapturedEnemyAttackModel.EquippedWeapon, contract.AttackModel);
                    Assert.IsTrue(contract.IsCombatReady);
                    Assert.AreEqual(variant.WeaponLoadout.LowId, contract.WeaponLowId);
                    Assert.AreEqual(variant.WeaponLoadout.HighId, contract.WeaponHighId);
                    Assert.AreEqual(variant.WeaponLoadout.Quality, contract.WeaponQuality);
                    Assert.AreEqual(6, contract.WeaponInventorySlot);
                    Assert.AreEqual(0, contract.MinDamage);
                    Assert.AreEqual(0, contract.MaxDamage);
                    Assert.AreEqual(0.0, contract.RechargeSeconds);
                    Assert.IsTrue(contract.HasCapturedEquippedAttackInfo);
                    Assert.AreEqual(24, contract.AttackInfoAmmoCount);
                    Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
                    Assert.AreEqual(0, contract.AttackInfoUnknown);
                    Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
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
            Assert.AreEqual(30.0, fragmentedSoul.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, fragmentedSoul.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, fragmentedSoul.Corpse.LootedCleanupSeconds);

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
                                            .IsCombatReady)),
                "Accepted Subway Redundant Scan must preserve four exact sources, ten atomic capture-reviewed generations, exact per-generation weapons, captured movement, and private inherited respawn together.");
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
            Assert.AreEqual(30.0, redundantScan.Corpse.EmptyLifetimeSeconds);
            Assert.AreEqual(120.0, redundantScan.Corpse.UnlootedLifetimeSeconds);
            Assert.AreEqual(30.0, redundantScan.Corpse.LootedCleanupSeconds);

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
                combatContractText.Contains("case 17649:")
                && providerText.Contains("CapturedSubwayCombatCatalog.For(name, monsterData, level)")
                && combatContractText.Contains("15 Disobedient Bot SIW1 normal local-player hits span 6-15 damage")
                && combatContractText.Contains("three other-player hits and two player-owned Killer-pet hits remain separate")
                && combatContractText.Contains("SpecialAttackWeapon contexts are capture-backed for levels 5, 6, 8, 9, and 10")
                && combatContractText.Contains("including the level-5 terminal value 22")
                && combatContractText.Contains("level 7 explicitly using the bounded 35/45 midpoint policy")
                && combatContractText.Contains("Disobedient Bot SIW1 attack context is unresolved for level")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotMinimumDamage = 6")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotMaximumDamage = 15")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotRechargeSeconds = 5.973723")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotWeaponTag = 0x53495731")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponValue = 30")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel6SpecialAttackWeaponValue = 35")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel7SpecialAttackWeaponPolicyValue = 40")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel8SpecialAttackWeaponValue = 45")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel9SpecialAttackWeaponValue = 49")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel10SpecialAttackWeaponValue = 54")
                && attackRulesText.Contains("CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponLastValue = 22")
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel6SpecialAttackWeaponValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel7SpecialAttackWeaponPolicyValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel8SpecialAttackWeaponValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel9SpecialAttackWeaponValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel10SpecialAttackWeaponValue") == 1
                && CountOccurrences(combatContractText, "CapturedSubwayDisobedientBotLevel5SpecialAttackWeaponLastValue") == 1
                && catalogText.Contains("level => CapturedSubwayCombatCatalog.For(first.Name, first.MonsterData, level)")
                && ordinaryProfileText.Contains("CapturedEnemyCombatContract ResolveContract(int level)")
                && ordinaryRuntimeText.Contains("profile.Combat.ResolveContract(spawn.SourceIdentity, variant)")
                && ordinaryRuntimeText.Contains("combatContract.AttackModel")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue"),
                "Accepted Subway Disobedient Bot must preserve level-aware SIW1 context, captured damage/attempt cadence, and shared chase while failing closed outside bounded levels.");
            Assert.IsTrue(
                providerText.Contains("234877")
                && providerText.Contains("104683")
                && providerText.Contains("113398")
                && catalogText.Contains("if (monsterData == 17649)")
                && catalogText.Contains("OrdinaryEnemyLootPoolMode.WeightedOne")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(5, 6, 6, 2")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(6, 8, 8, 3")
                && catalogText.Contains("20260719-020104")
                && catalogText.Contains("new OrdinaryEnemyLevelCreditRule(10, 12, 12, 2")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)"),
                "Accepted Subway Disobedient Bot must preserve strict weighted loot evidence, exact credits, CATMesh behavior, and ordinary corpse lifetimes.");
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
                providerText.Contains("CapturedSurveySpawn(Thief(0x7953AEA5, 5, 146, 72.7292557f, 115.61483f, 313.1308f, 93, 20, useSpawnAsPatrolStart: true, healthDamage: 31))")
                && providerText.Contains("this.HealthDamage = healthDamage;")
                && catalogText.Contains("source.HealthDamage,")
                && catalogText.Contains("monsterData == 26092 ? 1.0 : (double?)null")
                && catalogText.Contains("monsterData == 26092 ? 1 : (int?)null")
                && heartbeatRuntimeText.Contains("ordinaryDefinition.Profile.Combat.HealthRegenIntervalSeconds")
                && heartbeatRuntimeText.Contains("ordinaryDefinition.Profile.Combat.RegenerateHealthWhileInCombat")
                && providerText.Contains("new CapturedSubwayPatrolReplaySegment(4.548876")
                && providerText.Contains("new CapturedSubwayLootDefinition(")
                && providerText.Contains("\"Thief\"")
                && providerText.Contains("26092")
                && providerText.Contains("138")
                && providerText.Contains("297055")
                && providerText.Contains("10000"),
                "Accepted Subway Thief must have captured max/current health, patrol start, respawn, guaranteed handbag loot, and identity-specific loot evidence together.");

            Assert.IsTrue(
                combatContractText.Contains("case 26092:")
                && combatContractText.Contains("CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext(")
                && combatContractText.Contains("121567")
                && combatContractText.Contains("CapturedSubwayThiefAttackStartDelaySeconds")
                && combatContractText.Contains("CapturedSubwayThiefMovementTransitionDelaySeconds")
                && combatContractText.Contains("CapturedSubwayThiefFirstHitDelaySeconds")
                && combatContractText.Contains("CapturedSubwayThiefRechargeSeconds")
                && combatContractText.Contains("CapturedSubwayThiefAttackInfoAmmoCount")
                && combatContractText.Contains("CapturedSubwayThiefAttackInfoUnknown")
                && combatContractText.Contains("CapturedSubwayThiefSpecialAttackWeaponUnknown1"),
                "Accepted Subway Thief must keep one combat contract containing weapon, attack context, movement transition, timing, and AttackInfo context.");
            Assert.IsTrue(
                attackRulesText.Contains("CapturedSubwayThiefMonsterData = 26092")
                && attackRulesText.Contains("CapturedSubwayThiefAttackInfoAmmoCount = -1")
                && attackRulesText.Contains("CapturedSubwayThiefAttackInfoUnknown = 0")
                && attackRulesText.Contains("CapturedSubwayThiefRechargeSeconds = 6.0")
                && attackRulesText.Contains("CapturedSubwayThiefWeaponDamageMinimumOverride = 0")
                && attackRulesText.Contains("CapturedSubwayThiefWeaponDamageMaximumOverride = 0"),
                "Accepted Subway Thief must not silently fall back to fixed fake damage or stale AttackInfo constants.");

            Assert.IsTrue(
                movementCoordinatorText.Contains("capturedContract.HasCapturedAttackStartContext")
                && movementCoordinatorText.Contains("capturedContract.MovementTransitionDelaySeconds")
                && movementRuntimeText.Contains("FollowTargetStart")
                && movementRuntimeText.Contains("FollowTargetContinue"),
                "Accepted Subway Thief must be covered by captured attack-start transition and generic combat follow/chase movement.");

            Assert.IsTrue(
                weaponPacketText.Contains("owner.Stats[StatIds.monsterdata].Value == 26092")
                && weaponPacketText.Contains("string.Equals(owner.Name, \"Thief\"")
                && weaponPacketText.Contains("CharacterStat.Energy")
                && weaponPacketText.Contains("CharacterStat.AttackDelay")
                && weaponPacketText.Contains("CharacterStat.RechargeDelay"),
                "Accepted Subway Thief must announce a live-shaped equipped weapon definition so the client renders projectile damage.");

            Assert.IsTrue(
                catalogText.Contains("source.MonsterData == 26092")
                && catalogText.Contains("0x00122002u")
                && catalogText.Contains("OrdinaryEnemyScfuProfile.CapturedThief")
                && scfuPacketText.Contains("ordinaryRuntime.Profile.Appearance.ScfuProfile")
                && scfuPacketText.Contains("OrdinaryEnemyScfuProfile.CapturedThief"),
                "Accepted Subway Thief must retain its identity-specific SCFU appearance and movement bytes.");
            Assert.IsTrue(
                catalogText.Contains("OrdinaryEnemyCorpsePacketProfile.CapturedThief")
                && corpsePacketText.Contains("OrdinaryEnemyRuntimeRegistry.TryGet")
                && corpsePacketText.Contains("CapturedSubwayThiefPacketLength = 412")
                && corpsePacketText.Contains("CapturedSubwayThiefTemplate")
                && corpsePacketText.Contains("BuildCapturedSubwayThief("),
                "Accepted Subway Thief must retain the exact captured corpse visual packet path.");

            string registerCorpse = ExtractMethodBlock(playfieldText, "private void RegisterCorpse");
            Assert.IsTrue(
                playfieldText.Contains("CapturedSubwayThiefCorpseCatMesh = 5907")
                && playfieldText.Contains("private static bool UsesCapturedThiefCorpseProfile(ICharacter target)")
                && playfieldText.Contains("OrdinaryEnemyRuntimeRegistry.TryGet")
                && !registerCorpse.Contains("if (!state.HasUnlootedItems)")
                && registerCorpse.Contains("this.runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);")
                && corpseRulesText.Contains("EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("EmptyCorpseLifetime = TimeSpan.FromSeconds(30)")
                && corpseRulesText.Contains("RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)")
                && catalogText.Contains("OrdinaryEnemyCorpsePacketProfile.CapturedThief")
                && CountOccurrences(catalogText, "30.0,\n                120.0,\n                30.0") == 3,
                "Accepted Subway Thief must keep its captured corpse visual, two-minute loot-bearing lifetime across close/reopen, and universal 30-second empty cleanup.");
        }

        [TestMethod]
        public void OrdinaryEnemyRuntimeFailsClosedAndCleansProfileLifecycleState()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string spawnMethod = ExtractMethodBlock(
                runtimeText,
                "private bool Spawn(");
            int combatFailureStart = spawnMethod.IndexOf(
                "if (!combatReady)",
                StringComparison.Ordinal);
            int combatFailureReturn = combatFailureStart < 0
                ? -1
                : spawnMethod.IndexOf(
                    "return false;",
                    combatFailureStart,
                    StringComparison.Ordinal);
            int runtimeRegistration = spawnMethod.IndexOf(
                "OrdinaryEnemyRuntimeRegistry.Register(character.Identity.Instance",
                StringComparison.Ordinal);

            Assert.IsTrue(
                playfieldText.Contains("GlobalLootRuntimeService.Generate(target, this.Identity.Instance)")
                && !playfieldText.Contains("RollCorpseLootItems")
                && !playfieldText.Contains("GetDatabaseLootTable")
                && !playfieldText.Contains("DebugLootTable"),
                "All corpse loot must resolve through the global service, with unresolved ordinary loot failing closed there.");

            Assert.IsTrue(
                playfieldText.Contains("ordinaryDefinition.Profile.Corpse.UnlootedLifetimeSeconds")
                && playfieldText.Contains("ordinaryDefinition.Profile.Corpse.LootedCleanupSeconds")
                && playfieldText.Contains("selectedCorpse.ItemLootLifetime")
                && playfieldText.Contains("selectedCorpse.EmptyCleanupDelay"),
                "Corpse access and final-loot cleanup must consume the ordinary profile lifetime values.");

            Assert.IsTrue(
                combatFailureStart >= 0
                && combatFailureReturn > combatFailureStart
                && runtimeRegistration > combatFailureReturn
                && spawnMethod.Contains("CapturedEnemyCombatRuntimeRegistry.Remove(character.Identity.Instance);")
                && spawnMethod.IndexOf("this.activateNpc(character);", StringComparison.Ordinal)
                   > combatFailureReturn,
                "An unresolved or unequippable ordinary combat contract must reject the population generation before runtime registration or activation.");

            Assert.IsTrue(
                runtimeText.Contains("this.activeRuntimeIdentityBySource.ContainsKey(spawn.SourceIdentity)")
                && runtimeText.Contains("internal void ClearRuntimeState(int playfieldInstance)")
                && runtimeText.Contains("OrdinaryEnemyRuntimeRegistry.RemoveForPlayfield(playfieldInstance)")
                && npcRuntimeText.Contains("this.worldPopulation.ClearPlayfield(this.playfield.Identity.Instance)")
                && npcRuntimeText.Contains("this.ordinaryEnemies.ClearRuntimeState(this.playfield.Identity.Instance)")
                && runtimeSystemsText.Contains("internal void ClearNpcRuntimeState()")
                && playfieldText.Contains("this.runtimeSystems.ClearNpcRuntimeState();"),
                "Spawn/reset/dispose paths must prevent duplicates and clear runtime, combat, diagnostic, and profile registry state.");
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
                    "new PlayfieldVisibilityInterestRuntimeService(",
                    "new PlayfieldSpatialCharacterIndex(visibilityPolicy)",
                    "new PlayfieldVisibilityPacketRuntimeService(",
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

            Assert.IsTrue(
                runtimeSystemsText.Contains("PlayfieldVisibilityInterestPolicy.FromEnvironment()")
                && runtimeSystemsText.Contains("this.visibilityFanout,")
                && runtimeSystemsText.Contains("this.packetSequences,")
                && runtimeSystemsText.Contains("this.visibilityInterest);")
                && visibilityPacketText.Contains("PlayfieldVisibilityInterestRuntimeService visibilityInterest)"),
                "PlayfieldRuntimeSystems must construct one global interest service and inject it as the third visibility-packet dependency.");

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
                && visibilityPacketText.Contains("this.visibilityInterest.SelectInitialCharacters(recipient)")
                && visibilityPacketText.Contains("this.visibilityInterest.ReconcileInitializedRecipients(")
                && visibilityPacketText.Contains("sendVisibilityMessage(simpleCharFullUpdate);")
                && visibilityPacketText.Contains("this.SendWeaponDefinitionsForVisibility(")
                && visibilityPacketText.Contains("sendVisibilityMessage(charInPlay);"),
                "PlayfieldVisibilityPacketRuntimeService must own bounded interest entry, shared packet-pair construction, sequencing delegation, and debug logging.");
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
                && announcementText.Contains("if (entity?.Controller?.Client != null)")
                && announcementText.Contains("sendMessageBodyToClient(entity.Controller.Client, messageBody);")
                && announcementText.Contains("internal void AnnounceToOtherCharacterClients(")
                && announcementText.Contains("&& entity.Identity != excludedIdentity")
                && announcementText.Contains("&& entity.Controller?.Client != null"),
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
                moveNpcTowardCombatTarget.Contains("moveNpcToPosition(attacker, nextPosition)"),
                "Generic NPC chase must not warp through periodic SetPos steps.");
            Assert.IsTrue(
                moveNpcTowardCombatTarget.Contains("navigationResult.HasDestination")
                && moveNpcTowardCombatTarget.Contains("npcController.MoveTo("),
                "Geometry-aware chase segments must continue through the existing controller movement pipeline.");
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
                && rewardRuntimeText.Contains("awardCombatXp(attacker, target);"),
                "PlayfieldRewardRuntimeService must own named NPC death reward hook orchestration.");
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
                && npcCombatTickText.Contains("npcController.StopFollowForCapturedCombatRange(")
                && npcCombatTickText.Contains("movementDestination);"),
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
                playfieldText.Contains("this.runtimeSystems.ProcessDueNpcCorpseDespawns(utcNow, this.DespawnCorpse);")
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
                && playfieldText.Contains("this.corpseInventoryService.Create(state);")
                && playfieldText.Contains("x => this.corpseInventoryService.Remove(x)")
                && playfieldText.Contains("x => this.pendingCorpseCreditAwards.Remove(x)"),
                "The corpse inventory service must own state while object lifecycle preserves despawn cleanup order.");
            Assert.IsTrue(
                playfieldText.Contains("GlobalLootRuntimeService.Generate(target, this.Identity.Instance)")
                && playfieldText.Contains("private static readonly GlobalLootRuntimeService GlobalLootRuntimeService")
                && playfieldText.Contains("private void SendCorpseInventoryUpdate(ICharacter looter, CorpseState corpse)")
                && playfieldText.Contains("private void AwardCorpseCredits(ICharacter looter, CorpseState corpse)"),
                "Global services must own loot and corpse state while Playfield retains packet and character-credit application callbacks.");
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
            string corpseRulesText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\CombatCorpseRules.cs"));
            string ordinaryCatalogText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyCatalog.cs"));

            string playfieldUseCorpse = ExtractMethodBlock(playfieldText, "public bool TryUseCorpse");
            string playfieldLootCorpseItem = ExtractMethodBlock(playfieldText, "public bool TryLootCorpseItem");
            string registerCorpse = ExtractMethodBlock(playfieldText, "private void RegisterCorpse");
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
                && playfieldUseCorpse.Contains("corpse.InventoryHandle = this.AllocateCorpseInventoryHandle();")
                && playfieldUseCorpse.Contains("this.ScheduleCorpseCreditAward")
                && playfieldUseCorpse.Contains("corpse => corpse.IsEmpty"),
                "Playfield must delegate corpse access sequencing while retaining packet and credit callbacks.");
            Assert.IsTrue(
                corpseUse.Contains("this.SendCorpseInventoryUpdateAndCredits(")
                && corpseUse.Contains("if (!isEmpty(corpse))")
                && corpseUse.Contains("if (isEmpty(corpse))")
                && corpseUse.Contains("if (opened(corpse))")
                && corpseUse.Contains("setOpened(corpse, false);")
                && corpseUse.Contains("refreshCorpseInventoryHandle(corpse);")
                && corpseUse.Contains("sendCorpseCloseAction(looter, corpse);")
                && corpseUse.Contains("sendUseActionFinished(looter);")
                && corpseUse.Contains("return true;")
                && corpseUse.Contains("else"),
                "Corpse access must preserve the captured open, close, and reopen alternation.");
            Assert.IsFalse(
                corpseUse.Contains("NextUseSendsAccessActionOnly")
                || corpseUse.Contains("sendCorpseLootAccessAction"),
                "Corpse reopen must not retain the rejected refresh-plus-action hypothesis.");
            Assert.IsTrue(
                playfieldText.Contains("private void SendCorpseCloseAction")
                && playfieldText.Contains("ActionIdentity = 0x66"),
                "Captured corpse close must emit Action 0x66 only through the close branch.");
            AssertTextBefore(corpseUse, "if (opened(corpse))", "setOpened(corpse, false);");
            AssertTextBefore(corpseUse, "setOpened(corpse, false);", "refreshCorpseInventoryHandle(corpse);");
            AssertTextBefore(corpseUse, "refreshCorpseInventoryHandle(corpse);", "sendCorpseCloseAction(looter, corpse);");
            AssertTextBefore(corpseUse, "sendCorpseCloseAction(looter, corpse);", "sendUseActionFinished(looter);");
            AssertTextBefore(corpseUse, "sendUseActionFinished(looter);", "setOpened(corpse, true);");
            AssertTextBefore(
                inventoryAndCredits,
                "sendCorpseInventoryUpdate(looter, corpse);",
                "scheduleCorpseCreditAward(looter, corpse);");
            Assert.IsTrue(
                playfieldText.Contains("private static readonly TimeSpan CorpseCreditAwardDelay = TimeSpan.FromMilliseconds(500);")
                && corpseInteractionRulesText.Contains("public const int CorpseUseAcknowledgeDelayMilliseconds = 550;"),
                "Capture-backed corpse credit payout must stay after InventoryUpdate and before the delayed GenericCmd success ack.");
            Assert.IsTrue(
                !registerCorpse.Contains("if (!state.HasUnlootedItems)")
                && registerCorpse.Contains("this.runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);")
                && corpseRulesText.Contains("public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30);")
                && corpseRulesText.Contains("public static readonly TimeSpan EmptyCorpseLifetime = TimeSpan.FromSeconds(30);")
                && corpseRulesText.Contains("public static readonly TimeSpan RegularLootCorpseLifetime = TimeSpan.FromMinutes(2);")
                && registerCorpse.Contains("CombatCorpseLootClass lootClass = CorpseLootClassFor(target, lootItems, credits);")
                && corpseRulesText.Contains("unlootedItemCount <= 0 && unlootedCredits <= 0")
                && CountOccurrences(ordinaryCatalogText, "30.0,\n                120.0,\n                30.0") == 3,
                "Regular loot-bearing corpses must retain two minutes, while every born-empty or fully emptied corpse uses exactly 30 seconds.");
            AssertTextBefore(
                registerCorpse,
                "this.corpseInventoryService.Create(state);",
                "this.runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);");

            Assert.IsTrue(
                playfieldLootCorpseItem.Contains("this.runtimeSystems.TryLootCorpseItem(")
                && playfieldLootCorpseItem.Contains("this.runtimeSystems.CharacterHasUniqueItemAlready")
                && playfieldLootCorpseItem.Contains("this.runtimeSystems.TryAddCorpseLootItem")
                && playfieldLootCorpseItem.Contains("this.SendCorpseContainerAddItem")
                && playfieldLootCorpseItem.Contains("corpse => corpse.IsEmpty")
                && corpseLoot.Contains("if (isEmpty(corpse))"),
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
                awardCorpseCredits.Contains("this.corpseInventoryService.RemoveCredits(corpse.CorpseIdentity, DateTime.UtcNow)")
                && awardCorpseCredits.Contains("CashStatRules.Clamp")
                && awardCorpseCredits.Contains("looter.Stats[StatIds.cash].Set((uint)cashAfter);")
                && awardCorpseCredits.Contains("this.runtimeSystems.SendChangedStatsIfClient(")
                && sendStatChangedMessage.Contains("StatMessageHandler.Default.SendChanged(character);")
                && awardCorpseCredits.Contains("looter.Stats.Write();")
                && awardCorpseCredits.Contains("if (corpse.IsEmpty)")
                && awardCorpseCredits.Contains("this.ScheduleCorpseDespawn(corpse, corpse.EmptyCleanupDelay, \"credits-empty\");"),
                "Playfield must keep corpse credit mutation, stat packet callback, persistence ownership, and start cleanup only after credits actually empty the corpse.");
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
                playfieldText.Contains("private readonly CorpseInventoryService corpseInventoryService")
                && playfieldText.Contains("private readonly Dictionary<int, PendingCorpseCreditAward> pendingCorpseCreditAwards"),
                "The global corpse service must own corpse state while Playfield retains delayed credit scheduling.");
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
        public void ExistingCharacterSnapshotsInitializeOnceFromClientConnectedWithoutInboundCharInPlay()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketSequencingCoordinator.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string charInPlayText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharInPlayMessageHandler.cs"));
            string visibilitySequence = ExtractMethodBlock(
                coordinatorText,
                "public void RunVisibilityInitializationSequence");

            Assert.AreEqual(
                1,
                CountOccurrences(visibilitySequence, "Execute(sendExistingCharacterSnapshots"),
                "The shared visibility sequence must execute the existing-character snapshot exactly once.");
            AssertTextBefore(
                visibilitySequence,
                "Execute(announceJoiningCharacter",
                "Execute(sendExistingCharacterSnapshots");
            Assert.AreEqual(
                1,
                CountOccurrences(
                    clientConnectedText,
                    "currentPlayfield.SendSCFUsToClient(new IMSendPlayerSCFUs { toClient = client })"),
                "ClientConnected must initiate one existing-character snapshot for the joining client.");
            Assert.IsFalse(
                charInPlayText.Contains("SendSCFUsToClient")
                || charInPlayText.Contains("IMSendPlayerSCFUs"),
                "Inbound CharInPlay must not be required for or duplicate the initial existing-character snapshot.");
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
            AssertTextBefore(
                visibilitySequence,
                "Execute(announceJoiningCharacter",
                "Execute(sendExistingCharacterSnapshots");

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
                "FullCharacterMessageHandler.Default.Send(client.Controller.Character);");
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
                clientConnectedText.Contains("FullCharacterMessageHandler.Default.Send(client.Controller.Character);")
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
        public void SubwayProxyExitUsesOfficialLandingAndSuppressesDelayedEntryBounce()
        {
            string repositoryRoot = FindRepositoryRoot();
            string rulesText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\SubwayTeleportProxyDestinationRules.cs"));
            string exitProxyText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\exitproxyplayfield.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));

            Assert.IsTrue(
                statelTransitionText.Contains("private const float CapturedSubwayEntryRadius = 4.0f;")
                && statelTransitionText.Contains("private static readonly TimeSpan PostZoneCollisionGrace = TimeSpan.FromSeconds(3);")
                && statelTransitionText.Contains("private readonly HashSet<int> capturedSubwayEntryContacts")
                && statelTransitionText.Contains("this.capturedSubwayEntryContacts.Contains(dynelId)")
                && statelTransitionText.Contains("this.capturedSubwayEntryContacts.Remove(dynelId)")
                && rulesText.Contains("public const float CapturedMainExitLandingX = 3304.028f;")
                && rulesText.Contains("public const float CapturedMainExitLandingY = 35.11f;")
                && rulesText.Contains("public const float CapturedMainExitLandingZ = 837.9951f;")
                && rulesText.Contains("public const float CapturedMainExitHeadingY = -0.4771534f;")
                && rulesText.Contains("public const float CapturedMainExitHeadingW = 0.87882f;")
                && exitProxyText.Contains("SubwayTeleportProxyDestinationRules.TryResolveMainExitOverride("),
                "The Subway main exit must use the official-live landing while preserving post-zone grace and contact-edge suppression against a delayed bounce.");
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
                1,
                CountOccurrences(visibilityPacketSequenceText, "this.packetSequences.RunVisibilityPacketPairSequence("),
                "Playfield visibility packet runtime must keep one shared SCFU -> weapons -> CharInPlay packet-pair implementation.");
            string initialVisibility = ExtractMethodBlock(
                visibilityPacketSequenceText,
                "internal void SendExistingCharacterVisibilityToClient(");
            string joiningVisibility = ExtractMethodBlock(
                visibilityPacketSequenceText,
                "internal void AnnounceJoiningCharacterVisibility(");
            string sharedEntryVisibility = ExtractMethodBlock(
                visibilityPacketSequenceText,
                "private bool SendCharacterVisibilityEntry(");
            Assert.IsTrue(
                initialVisibility.Contains("this.SendCharacterVisibilityEntry(")
                && joiningVisibility.Contains("this.SendCharacterVisibilityEntry(")
                && sharedEntryVisibility.Contains("sendVisibilityMessage(simpleCharFullUpdate);")
                && sharedEntryVisibility.Contains("this.SendWeaponDefinitionsForVisibility(")
                && sharedEntryVisibility.Contains("sendVisibilityMessage(charInPlay);"),
                "Initial and joining interest paths must invoke the same ordered SCFU -> weapons -> CharInPlay implementation.");
            AssertTextBefore(
                sharedEntryVisibility,
                "sendVisibilityMessage(simpleCharFullUpdate);",
                "this.SendWeaponDefinitionsForVisibility(");
            AssertTextBefore(
                sharedEntryVisibility,
                "this.SendWeaponDefinitionsForVisibility(",
                "sendVisibilityMessage(charInPlay);");
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
            string visibilityInterestText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityInterestRuntimeService.cs"));
            string visibilityStateText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityInterestState.cs"));
            string visibilityPolicyText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVisibilityInterestPolicy.cs"));
            string spatialIndexText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldSpatialCharacterIndex.cs"));
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
                announcePlayerVisibility.Contains("this.runtimeSystems.AnnounceJoiningCharacterVisibility(")
                && announcePlayerVisibility.Contains("this.SendVisibilityMessage,")
                && announcePlayerVisibility.Contains("this.SendVisibilityLeave);")
                && !announcePlayerVisibility.Contains("CharInPlayMessage")
                && !announcePlayerVisibility.Contains("SimpleCharFullUpdate.ConstructMessage(temp)"),
                "AnnouncePlayerVisibility must delegate targeted joining-character entry and leave delivery through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToCharacterClients(this.CharacterEntities(), publishToCharacterClient);")
                && runtimeSystemsText.Contains("this.visibilityFanout.AnnounceToOtherCharacterClients(")
                && runtimeSystemsText.Contains("this.visibilityPackets.SendExistingCharacterVisibilityToClient("),
                "PlayfieldRuntimeSystems must feed visibility fanout and visibility packet orchestration from registry-backed views.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.visibilityPackets.SendExistingCharacterVisibilityToClient(")
                && runtimeSystemsText.Contains("this.visibilityPackets.AnnounceJoiningCharacterVisibility(")
                && runtimeSystemsText.Contains("this.visibilityInterest.VisibleRecipientsForSource(sourceIdentity)"),
                "PlayfieldRuntimeSystems must route visibility entry, leave, and scoped recipient lookup through the global interest boundary.");
            Assert.IsTrue(
                visibilityPolicyText.Contains("internal float EnterRadius")
                && visibilityPolicyText.Contains("internal float LeaveRadius")
                && spatialIndexText.Contains("internal IReadOnlyList<ICharacter> Query(Coordinate center, float radius)")
                && visibilityInterestText.Contains("PlayfieldVisibilityInterestState<ICharacter>")
                && visibilityStateText.Contains("visibleSourcesByRecipient")
                && visibilityStateText.Contains("visibleRecipientsBySource")
                && visibilityStateText.Contains("ReconcileInitializedRecipients("),
                "Global visibility interest must own bounded policy, spatial candidate queries, bidirectional state, and hysteresis reconciliation.");
            Assert.IsTrue(
                visibilityFanoutText.Contains("foreach (Character entity in characters)")
                && visibilityFanoutText.Contains("if (entity.Controller.Client != null)")
                && visibilityFanoutText.Contains("if (entity.Identity != excludedIdentity)")
                && visibilityFanoutText.Contains("foreach (ICharacter entity in characters)")
                && visibilityFanoutText.Contains("bool senderEqualsRecipient = entity.Identity == dontSendTo;")
                && visibilityFanoutText.Contains("bool senderInRecipientPlayfield = entity.InPlayfield(playfieldIdentity);")
                && visibilityFanoutText.Contains("sent = sendExistingCharacter(entity);")
                && visibilityFanoutText.Contains("logVisibilityCandidate(entity, senderEqualsRecipient, senderInRecipientPlayfield, sent);"),
                "Visibility fanout service must retain packet-agnostic iteration over the spatially selected character set.");
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
                && visibilityPacketText.Contains("this.visibilityInterest.SelectInitialCharacters(recipient)")
                && visibilityPacketText.Contains("this.visibilityInterest.ReconcileInitializedRecipients(")
                && visibilityPacketText.Contains("this.packetSequences.RunVisibilityPacketPairSequence(")
                && visibilityPacketText.Contains("sendVisibilityMessage(simpleCharFullUpdate)")
                && visibilityPacketText.Contains("sendVisibilityMessage(charInPlay)")
                && visibilityPacketText.Contains("LogUtil.Debug("),
                "Visibility packet service must use bounded interest selection while retaining shared packet-pair orchestration and debug logging.");
            Assert.IsFalse(
                visibilityPacketText.Contains("SendCompressed")
                || visibilityPacketText.Contains("Publish(")
                || visibilityPacketText.Contains("Pool.Instance"),
                "Visibility packet service must not own direct transport, publish wrappers, or Pool scans.");
            Assert.IsTrue(
                announcementText.Contains("foreach (Character entity in characters)")
                && announcementText.Contains("if (entity?.Controller?.Client != null)")
                && announcementText.Contains("&& entity.Identity != excludedIdentity")
                && announcementText.Contains("&& entity.Controller?.Client != null")
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
                    visibilityInterestText,
                    spatialIndexText,
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

            Assert.IsTrue(
                corpseFullUpdate.Contains("this.runtimeSystems.VisibleRecipientsForSource(target.Identity)")
                && corpseFullUpdate.Contains("this.SendCorpseFullUpdateToRecipient(")
                && !corpseFullUpdate.Contains("this.runtimeSystems.Characters()")
                && !corpseFullUpdate.Contains("Pool.Instance.GetAll"),
                "Corpse full updates must target only recipients that currently know the dead NPC source.");

            string[] movedLoopBlocks =
                {
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

        [TestMethod]
        public void RedundantScanSupportNanoRuntimeKeepsCapturedPacketOrderAndReversibleOwnedState()
        {
            string repositoryRoot = FindRepositoryRoot();
            string runtimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string npcRuntimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string castHandlerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CastNanoMessageHandler.cs"));
            string buffHandlerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\BuffMessageHandler.cs"));
            string actionHandlerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharacterActionMessageHandler.cs"));

            string finish = ExtractMethodBlock(
                runtimeText,
                "private void FinishSupportNanoCast(");
            AssertTextBefore(finish, "FinishNanoCasting(", "if (target == null");
            AssertTextBefore(finish, "FinishNanoCasting(", "SendAddNanoBuff(target");
            AssertTextBefore(
                finish,
                "SendAddNanoBuff(target",
                "NotifyActiveNanoDurationToPlayfield(");
            AssertTextBefore(
                finish,
                "profile.PrimaryNanoId,",
                "SendTriggeredSelfCast(");
            AssertTextBefore(
                finish,
                "SendTriggeredSelfCast(",
                "SendAddNanoBuff(caster");
            Assert.IsTrue(
                finish.Contains("profile.TriggeredSelfNanoId")
                && CountOccurrences(finish, "profile.DurationParameter") == 2,
                "Redundant Scan must announce both captured duration packets.");

            string apply = ExtractMethodBlock(
                runtimeText,
                "private bool ApplyOrRefreshTransientNanoEffect(");
            Assert.IsTrue(
                apply.Contains("existing.ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds)")
                && apply.Contains("return false;")
                && apply.Contains("recipient.Stats[statId].Modifier += modifierDelta")
                && apply.Contains("recipient.ActiveNanos[activeNanoKey] = new ActiveNanoState"),
                "Transient NPC nanos must refresh without restacking and project active state for late observers.");
            Assert.IsTrue(
                runtimeText.Contains("recipient.Stats[statId].Modifier -= state.ModifierDelta")
                && runtimeText.Contains("ProcessExpiredSupportNanoEffects(DateTime utcNow)")
                && runtimeText.Contains("RemoveAllTransientNanoEffects();")
                && runtimeText.Contains("NotifyCharacterDied(ICharacter character)"),
                "Transient NPC nano cleanup must reverse only its owned modifier deltas on expiry, death, and reset.");
            Assert.IsFalse(runtimeText.Contains("ActiveNanoRuntimeService"));
            Assert.IsFalse(runtimeText.Contains("CharacterActiveNanosDao"));

            Assert.IsTrue(
                npcRuntimeText.Contains("this.ordinaryEnemies.ProcessExpiredSupportNanoEffects(utcNow);")
                && npcRuntimeText.Contains("this.ordinaryEnemies.NotifyCharacterDied(target);")
                && CountOccurrences(
                    npcRuntimeText,
                    "this.ordinaryEnemies.TryProcessSupportNano(") == 2,
                "NPC runtime must own deterministic expiry, death cleanup, and patrol/combat cast pauses.");
            Assert.IsTrue(
                castHandlerText.Contains("public void SendNpcCast(")
                && castHandlerText.Contains("x.Caster = Identity.None;")
                && castHandlerText.Contains("public void SendTriggeredSelfCast(")
                && castHandlerText.Contains("x.Unknown1 = 1;"));
            Assert.IsTrue(
                buffHandlerText.Contains("public void SendAddNanoBuff(")
                && buffHandlerText.Contains("Type = (IdentityType)character.Identity.Instance")
                && actionHandlerText.Contains("public void NotifyActiveNanoDurationToPlayfield(")
                && actionHandlerText.Contains("true);"),
                "NPC Buff and SetNanoDuration packets must broadcast to the playfield instead of a nonexistent NPC client.");
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
            string runtimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string dynamicLookup = ExtractMethodBlock(
                runtimeText,
                "internal static bool TryResolveNanoDataStaticModifier(");
            string targetSelection = ExtractMethodBlock(
                runtimeText,
                "private ICharacter FindSupportNanoTarget(");
            string targetEligibility = ExtractMethodBlock(
                runtimeText,
                "private static bool IsOrdinaryEnemy(");
            string finish = ExtractMethodBlock(
                runtimeText,
                "private void FinishSupportNanoCast(");
            string apply = ExtractMethodBlock(
                runtimeText,
                "private bool ApplyOrRefreshTransientNanoEffect(");
            string remove = ExtractMethodBlock(
                runtimeText,
                "private void RemoveTransientNanoEffect(");

            Assert.IsTrue(
                dynamicLookup.Contains("NanoLoader.NanoList.TryGetValue(nanoId, out nano)")
                && runtimeText.Contains("nano.Events.Count != 1")
                && runtimeText.Contains("onUse.EventType != EventType.OnUse")
                && runtimeText.Contains("function.FunctionType != (int)FunctionType.Skill")
                && runtimeText.Contains("function.Target != (int)ItemTarget.Target")
                && runtimeText.Contains("function.TickCount != 1")
                && runtimeText.Contains("function.TickInterval != 0")
                && runtimeText.Contains("!function.dolocalstats")
                && runtimeText.Contains("function.Requirements.Count != 0")
                && runtimeText.Contains("function.Arguments.Values.Count != 2")
                && runtimeText.Contains("statId = function.Arguments.Values[0].AsInt32();")
                && runtimeText.Contains("modifierDelta = function.Arguments.Values[1].AsInt32();"),
                "Nano 95447 must resolve its one target Skill effect dynamically from NanoLoader data instead of hard-coding stat 381 or delta +42 in runtime.");
            Assert.IsTrue(
                targetSelection.Contains("candidate.Identity != caster.Identity")
                && targetSelection.Contains("IsOrdinaryEnemy(candidate)")
                && targetSelection.Contains("profile.FallbackToSelf")
                && targetEligibility.Contains("OrdinaryEnemyRuntimeRegistry.TryGet("),
                "Nano 95447 must target any ordinary ally, with self fallback, rather than only another Fragmented Soul.");
            Assert.IsTrue(
                finish.Contains("profile.ResolvePrimaryModifierFromNanoData")
                && finish.Contains("primaryAffectedStatIds = new[] { primaryModifierStatId }")
                && finish.Contains("ApplyOrRefreshTransientNanoEffect("),
                "Nano 95447 must carry the dynamically resolved stat and delta into the transient-effect lifecycle.");
            Assert.IsTrue(
                apply.Contains("existing.ExpiresAtUtc = utcNow.AddSeconds(profile.EffectLifetimeSeconds)")
                && apply.Contains("return false;")
                && apply.Contains("recipient.Stats[statId].Modifier += modifierDelta")
                && apply.Contains("ModifierDelta = modifierDelta")
                && apply.Contains("StatIds = (int[])(affectedStatIds ?? profile.AffectedStatIds).Clone()"),
                "Refreshing nano 95447 must extend expiry without stacking a second +42 delta.");
            Assert.IsTrue(
                remove.Contains("recipient.Stats[statId].Modifier -= state.ModifierDelta")
                && remove.Contains("recipient.ActiveNanos.Remove(state.ActiveNanoKey)"),
                "Nano 95447 cleanup must remove only the modifier delta and active state owned by its transient effect.");
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
            string runtimeText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string process = ExtractMethodBlock(
                runtimeText,
                "internal bool TryProcessSupportNano(");
            string finish = ExtractMethodBlock(
                runtimeText,
                "private void FinishSupportNanoCast(");
            string periodic = ExtractMethodBlock(
                runtimeText,
                "private bool ApplyOrRefreshPeriodicNanoHit(");

            Assert.IsTrue(
                process.Contains("profile.CastWhileFighting")
                && process.Contains("profile.AllowCombatActionsDuringCast")
                && process.Contains("profile.CastChanceBasisPoints")
                && process.Contains("OrdinaryEnemySupportNanoRuntimeRules.TrySpendNano(")
                && process.Contains("StatMessageHandler.Default.AnnounceSingle("),
                "Incomplete Rebuild casts must use the captured combat/resource policy without pausing attacks.");
            Assert.IsTrue(
                finish.Contains("profile.HasPeriodicStatHit")
                && finish.Contains("profile.HasTriggeredSelfEffect"),
                "Primary-only periodic nanos must not emit a fabricated triggered-self cast.");
            Assert.IsTrue(
                periodic.Contains("new OrdinaryEnemyPeriodicNanoSchedule(profile, utcNow)")
                && periodic.Contains("existing.PeriodicSchedule.Refresh(profile, utcNow)")
                && periodic.Contains("profile.NcuCost"),
                "The immediate +21 hit must leave exactly 959 captured 15-second ticks and project NCU cost.");
            Assert.IsTrue(
                runtimeText.Contains("state.PeriodicSchedule.ConsumeDueTicks(utcNow)")
                && runtimeText.Contains("OrdinaryEnemySupportNanoRuntimeRules.ApplyPositiveCappedDelta(")
                && runtimeText.Contains("RemoveTransientNanoEffectsForCaster("),
                "Periodic nano hits must cap at MaxNano and clean up on caster removal or death.");
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
