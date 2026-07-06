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
                && attackHandlerText.Contains("this.SendAttackState(character, message.Target, message.Action);"),
                "AttackMessageHandler must route player attack start/cancel through the player combat boundary while keeping packet echo order.");
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
                && playfieldText.Contains("this.runtimeSystems.CleanupPlayerDeathCombat(")
                && playfieldText.Contains("this.SendPlayerDeathAnimation(target);"),
                "Playfield must keep player death behavior while routing death lifecycle entry through the facade.");
            string playerDeath = ExtractMethodBlock(playfieldText, "private void KillPlayerTarget");
            AssertTextBefore(
                playerDeath,
                "this.MarkPlayerDead(target);",
                "target.SendChangedStats();");
            AssertTextBefore(
                playerDeath,
                "target.SendChangedStats();",
                "this.runtimeSystems.CleanupPlayerDeathCombat(");
            AssertTextBefore(
                playerDeath,
                "this.runtimeSystems.CleanupPlayerDeathCombat(",
                "this.SendPlayerDeathAnimation(target);");
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
            string objectLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectLifecycleRuntimeService.cs"));
            string objectMaterializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
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
            string playerDeathRespawnText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldPlayerDeathRespawnRuntimeService.cs"));
            string statelTransitionText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs"));
            string materializationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"));
            string timedLifecycleText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldTimedLifecycleRuntimeService.cs"));
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
                    "new PlayfieldCorpseAccessRuntimeService()",
                    "new PlayfieldRewardRuntimeService()",
                    "new NPCRuntimeService(playfield, this.dynelRegistry, this.rewards)",
                    "new PlayfieldLifecycleRuntimeService()",
                    "new PlayfieldPlayerDeathRespawnRuntimeService()",
                    "new PlayfieldStatelTransitionRuntimeService()",
                    "new PlayfieldTimedLifecycleRuntimeService()",
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
            Assert.AreEqual(
                1,
                CountOccurrences(npcRuntimeText, "new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent)"),
                "NPCRuntimeService must own captured Arete robot content provider construction.");
            Assert.AreEqual(
                1,
                CountOccurrences(npcRuntimeText, "new NpcPatrolReplayCoordinator(this.capturedAreteRobotContent)"),
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
                && runtimeSystemsText.Contains("private readonly PlayfieldObjectMaterializationRuntimeService objectMaterialization")
                && runtimeSystemsText.Contains("private readonly PlayfieldPlayerDeathRespawnRuntimeService playerDeathRespawn")
                && runtimeSystemsText.Contains("private readonly PlayfieldStatelTransitionRuntimeService statelTransitions")
                && runtimeSystemsText.Contains("private readonly PlayfieldTimedLifecycleRuntimeService timedLifecycle")
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
                && runtimeSystemsText.Contains("this.statelTransitions.ClearContactState(dynelId);"),
                "PlayfieldRuntimeSystems must delegate runtime entry points through named runtime services.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldLifecycleRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldLifecycleRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldPlayerDeathRespawnRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldPlayerDeathRespawnRuntimeService.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldTimedLifecycleRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldTimedLifecycleRuntimeService.");
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
                || timedLifecycleText.Contains("CheckWallCollision")
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
                && heartbeatTimer.Contains("this.ProcessCharacterRegeneration")
                && heartbeatTimer.Contains("this.DoCombatTick")
                && heartbeatTimer.Contains("this.ProcessCharacterFollow")
                && heartbeatTimer.Contains("this.ProcessPlayerCollisionChecks"),
                "Playfield heartbeat must delegate timed lifecycle sequencing through PlayfieldRuntimeSystems.");
            Assert.IsFalse(
                heartbeatTimer.Contains("foreach (ICharacter dynel in dynels)")
                || heartbeatTimer.Contains("this.runtimeSystems.ProcessDeadNpcDespawn(dynel)")
                || heartbeatTimer.Contains("this.runtimeSystems.ProcessNpcPatrolTick(dynel)"),
                "Playfield heartbeat must not directly own character lifecycle loop sequencing.");
            Assert.IsTrue(
                playfieldText.Contains("private void ProcessCharacterRegeneration(ICharacter dynel)")
                && playfieldText.Contains("dynel.Stats[StatIds.health].Value")
                && playfieldText.Contains("dynel.SendChangedStats();")
                && playfieldText.Contains("private void ProcessCharacterFollow(ICharacter dynel)")
                && playfieldText.Contains("dynel.Controller.DoFollow();")
                && playfieldText.Contains("private void ProcessPlayerCollisionChecks(ICharacter dynel)")
                && playfieldText.Contains("this.CheckWallCollision(dynel);")
                && playfieldText.Contains("this.CheckStatelCollision(dynel);"),
                "Playfield must retain regeneration, follow, and collision behavior behind scheduler callbacks.");
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
                teleport.Contains("this.runtimeSystems.PreparePlayfieldTransfer(")
                && teleport.Contains("this.ClearPlayfieldTransferContactState")
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
                && npcCombatTickText.Contains("this.playfield.ClearInvalidNpcCombatTarget(attacker);"),
                "NpcCombatTickCoordinator must route NPC combat clear decisions through the runtime ownership boundary.");
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
                && npcRuntimeText.Contains("this.StartCombatWithAcquiredTarget(attacker, target);")
                && npcRuntimeText.Contains("private void StartCombatWithAcquiredTarget(ICharacter attacker, ICharacter target)")
                && npcRuntimeText.Contains("target.SetFightingTarget(attacker.Identity);")
                && npcRuntimeText.Contains("this.ResetCombatTick(target);"),
                "NPCRuntimeService must own NPC aggro acquisition and combat-start orchestration.");
            Assert.IsTrue(
                timedLifecycleText.Contains("processNpcPatrolTick(dynel);"),
                "Timed lifecycle scheduling must delegate NPC patrol ticks through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                npcRuntimeText.Contains("internal void ProcessPatrolTick(ICharacter character)")
                && npcRuntimeText.Contains("character.Controller.DoFollow();")
                && npcRuntimeText.Contains("character.Controller.StartPatrolling();"),
                "NPCRuntimeService must own NPC patrol/follow tick orchestration while controllers keep behavior.");
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
                "sendCorpseContainerAddItem(looter, sourceContainer, targetPlacement);");
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

            string createStaticDynel =
                ExtractMethodBlock(playfieldText, "private IEntity CreateStaticDynel(PlayfieldStaticDynelDefinition staticDynel)");
            Assert.IsTrue(
                createStaticDynel.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must remain the runtime static dynel construction owner in this slice.");
            Assert.IsFalse(
                createStaticDynel.Contains("StaticDynelDao.Instance.GetWhere"),
                "Playfield static dynel loading must not own DB row access.");
            Assert.IsFalse(
                createStaticDynel.Contains("MessagePackZip.DeserializeData"),
                "Playfield static dynel loading must not own static dynel stat deserialization.");
            Assert.IsTrue(
                playfieldText.Contains("private IEnumerable<DBMobSpawn> LoadMobSpawnDefinitions(Identity playfieldIdentity)")
                && playfieldText.Contains("MobSpawnDao.Instance.GetWhere")
                && playfieldText.Contains("private IEnumerable<DBMobSpawnStat> LoadMobSpawnStats(DBMobSpawn mob)")
                && playfieldText.Contains("MobSpawnStatDao.Instance.GetWhere")
                && playfieldText.Contains("private ICharacter InstantiateDbMobSpawn(DBMobSpawn mob, DBMobSpawnStat[] stats)")
                && playfieldText.Contains("NonPlayerCharacterHandler.InstantiateMobSpawn")
                && playfieldText.Contains("new NPCController()")
                && playfieldText.Contains("private void AttachMobSpawnKnuBot(DBMobSpawn mob, ICharacter cmob)")
                && playfieldText.Contains("ScriptCompiler.Instance.CreateKnuBot")
                && playfieldText.Contains("private void SpawnVendors(StatelData[] vendorStatels)")
                && playfieldText.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels)"),
                "Playfield must keep DB loading, object construction, script creation, and vendor spawning callbacks.");
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
                && projectText.Contains(@"Core\Playfields\PlayfieldObjectMaterializationRuntimeService.cs"),
                "ZoneEngine project must compile PlayfieldContentDataProvider and PlayfieldObjectMaterializationRuntimeService.");
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

            string spawnVendors = ExtractMethodBlock(playfieldText, "private void SpawnVendors(StatelData[] vendorStatels)");
            string createStaticDynel =
                ExtractMethodBlock(playfieldText, "private IEntity CreateStaticDynel(PlayfieldStaticDynelDefinition staticDynel)");
            string checkStatelCollision = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision(ICharacter dynel)");

            Assert.IsTrue(
                spawnVendors.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels);"),
                "Playfield must remain the vendor runtime spawning owner.");
            Assert.IsTrue(
                createStaticDynel.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must remain the StaticDynel runtime construction owner.");
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
                teleportMethod,
                "lifecycleClient.PacketSequencing.RunPlayfieldTransferBeginSequence(",
                "TeleportMessageHandler.Default.Send(");
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
                playfieldText.Contains("lifecycleClient.PacketSequencing.RunPlayfieldTransferBeginSequence(")
                && playfieldText.Contains("lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer,"),
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
                playfieldText.Contains("this.Announce(SimpleCharFullUpdate.ConstructMessage(temp))")
                && playfieldText.Contains("charInPlay = new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 };")
                && playfieldText.Contains("this.Announce(charInPlay)")
                && playfieldText.Contains("public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)"),
                "SCFU and CharInPlay broadcast mechanics must remain in Playfield.");
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
                && runtimeSystemsText.Contains("internal PacketSequencingCoordinator PacketSequencing"),
                "PlayfieldRuntimeSystems must expose the packet sequencing coordinator for playfield-local sequencing.");
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
                CountOccurrences(playfieldText, "this.runtimeSystems.PacketSequencing.RunSimpleCharFullUpdateCharInPlaySequence("),
                "Playfield must route both existing-player and joining-player SCFU/CharInPlay pairs through PacketSequencingCoordinator.");
            Assert.IsTrue(
                privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(")
                && privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence("),
                "PrivateCityReadyInitCoordinator must route private-city ready/init packet order through PacketSequencingCoordinator.");
            Assert.IsTrue(
                playfieldText.Contains("lifecycleClient.PacketSequencing.RunPlayfieldTransferBeginSequence(")
                && playfieldText.Contains("lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer,")
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
                playfieldText.Contains("SimpleCharFullUpdateMessage simpleCharFullUpdate = SimpleCharFullUpdate.ConstructMessage(temp);")
                && playfieldText.Contains("() => sendSCFUs.toClient.SendCompressed(simpleCharFullUpdate)")
                && playfieldText.Contains("charInPlay = new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 };")
                && playfieldText.Contains("() => sendSCFUs.toClient.SendCompressed(charInPlay)")
                && playfieldText.Contains("() => this.Announce(SimpleCharFullUpdate.ConstructMessage(temp))")
                && playfieldText.Contains("() => this.Announce(charInPlay)"),
                "Visibility packet construction and send expressions must remain in Playfield.");
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

            string teleportMethod = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");
            string createCharacterMethod = ExtractMethodBlock(
                zoneClientText,
                "public void CreateCharacter(int charId)");

            AssertTextBefore(
                teleportMethod,
                "if (this.TryCompleteGridTeleportInCurrentPlayfield(dynel, destination, heading, playfield))",
                "this.runtimeSystems.PreparePlayfieldTransfer(");
            AssertTextBefore(
                teleportMethod,
                "this.runtimeSystems.PreparePlayfieldTransfer(",
                "lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer,");
            AssertTextBefore(
                lifecycleText,
                "clearTransferContactState(dynel.Identity.Instance);",
                "disableTimers(dynel);");
            AssertTextBefore(
                teleportMethod,
                "lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer,",
                "TeleportMessageHandler.Default.Send(");
            AssertTextBefore(
                teleportMethod,
                "TeleportMessageHandler.Default.Send(",
                "DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);");
            AssertTextBefore(
                teleportMethod,
                "DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);",
                "this.AnnounceOthers(despawnMessage, dynel.Identity);");
            AssertTextBefore(
                teleportMethod,
                "this.AnnounceOthers(despawnMessage, dynel.Identity);",
                "dynel.RawCoordinates = new Vector3()");
            AssertTextBefore(
                teleportMethod,
                "ZoneClient client = (ZoneClient)dynel.Controller.Client;",
                "IPlayfield newPlayfield = this.server.PlayfieldById(playfield);");
            AssertTextBefore(
                teleportMethod,
                "IPlayfield newPlayfield = this.server.PlayfieldById(playfield);",
                "Pool.Instance.GetObject<Playfield>(");
            AssertTextBefore(
                teleportMethod,
                "Pool.Instance.GetObject<Playfield>(",
                "if (newPlayfield == null)");
            AssertTextBefore(
                teleportMethod,
                "newPlayfield = new Playfield(this.server, playfield);",
                "dynel.Playfield = newPlayfield;");
            AssertTextBefore(
                teleportMethod,
                "dynel.Playfield = newPlayfield;",
                "dynel.Controller.Client = null;");
            AssertTextBefore(
                teleportMethod,
                "dynel.Controller.Client = null;",
                "dynel.IsTeleporting = true;");
            AssertTextBefore(
                teleportMethod,
                "dynel.IsTeleporting = true;",
                "dynel.Dispose();");
            AssertTextBefore(
                teleportMethod,
                "dynel.Dispose();",
                "var redirect = new ZoneRedirectionMessage");
            AssertTextBefore(
                teleportMethod,
                "var redirect = new ZoneRedirectionMessage",
                "client.SendCompressed(redirect);");

            AssertTextBefore(
                createCharacterMethod,
                "this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();",
                "this.server.PlayfieldById(");
            AssertTextBefore(
                createCharacterMethod,
                "this.server.PlayfieldById(",
                "this.Controller.Character = new Character(");

            Assert.IsTrue(
                teleportMethod.Contains("lifecycleClient.PacketSequencing.RunPlayfieldTransferBeginSequence("),
                "The guarded zoning phase-entry and teleport-send order may be routed through PacketSequencingCoordinator.");
            Assert.IsFalse(
                packetSequencingText.Contains("TeleportMessageHandler")
                || packetSequencingText.Contains("ZoneRedirectionMessage")
                || packetSequencingText.Contains("PlayfieldById")
                || packetSequencingText.Contains("dynel.Dispose"),
                "PacketSequencingCoordinator must not own teleport packet construction, destination lookup, or disposal mechanics.");
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
            Assert.AreEqual(
                2,
                CountOccurrences(playfieldText, "this.runtimeSystems.PacketSequencing.RunSimpleCharFullUpdateCharInPlaySequence("),
                "PacketSequencingCoordinator must own both SCFU -> CharInPlay visibility pair sequences.");
            Assert.IsTrue(
                privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPreFullCharacterOrgInitSequence(")
                && privateCityReadyInitText.Contains("client.PacketSequencing.RunPrivateCityPlayfieldReadyBlockSequence("),
                "PacketSequencingCoordinator must own private-city org/stat and towers/cities sequencing.");
            Assert.IsTrue(
                playfieldText.Contains("lifecycleClient.PacketSequencing.RunPlayfieldTransferBeginSequence("),
                "PacketSequencingCoordinator must own zoning entry before teleport packet sequencing.");

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
                teleportMethod.Contains("IPlayfield newPlayfield = this.server.PlayfieldById(playfield);")
                && teleportMethod.Contains("newPlayfield = new Playfield(this.server, playfield);")
                && teleportMethod.Contains("DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);")
                && teleportMethod.Contains("this.AnnounceOthers(despawnMessage, dynel.Identity);")
                && teleportMethod.Contains("dynel.RawCoordinates = new Vector3()")
                && teleportMethod.Contains("dynel.RawHeading = new Vector.Quaternion")
                && teleportMethod.Contains("dynel.Controller.Client = null;")
                && teleportMethod.Contains("dynel.Dispose();")
                && teleportMethod.Contains("var redirect = new ZoneRedirectionMessage")
                && teleportMethod.Contains("client.SendCompressed(redirect);"),
                "Destination lookup, despawn broadcast, coordinate mutation, client detach/dispose, and redirect must remain in Playfield.");
            Assert.IsTrue(
                localTeleportMethod.Contains("TeleportMessageHandler.Default.SendLocal("),
                "Same-playfield local teleport packet path must remain outside PacketSequencingCoordinator.");
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
                    "this.runtimeSystems.CharacterEntities()",
                    "this.runtimeSystems.Characters()",
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
            string dynelDropPosition = ExtractMethodBlock(playfieldText, "private Coordinate DynelDropPosition(Identity identity)");
            string findNamed = ExtractMethodBlock(playfieldText, "public INamedEntity FindNamedEntityByIdentity(Identity identity)");

            Assert.IsTrue(
                announce.Contains("this.runtimeSystems.CharacterEntities()"),
                "Announce must use registry-backed character visibility views.");
            Assert.IsTrue(
                announceOthers.Contains("this.runtimeSystems.CharacterEntities()"),
                "AnnounceOthers must use registry-backed character visibility views.");
            Assert.IsTrue(
                sendScfus.Contains("this.runtimeSystems.Characters()"),
                "SendSCFUsToClient must use registry-backed current-playfield character views.");
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
            string teleport = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");

            string[] intentionalGlobalOrCrossPlayfieldExceptions =
                {
                    "DisconnectAllClients: global CanbeAffected character scan for server shutdown/dispose.",
                    "NumberOfDynels: global CanbeAffected count, not playfield-local registry count.",
                    "NumberOfPlayers: global CanbeAffected Character count, not playfield-local registry count.",
                    "Teleport: cross-playfield Pool.GetObject<Playfield> handoff path."
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
                teleport.Contains("Pool.Instance.GetObject<Playfield>("),
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
