namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AreteCapturedQuestOwnershipTests
    {
        private static readonly string[] QuestSources =
            {
                "AntonioStacklundQuestRuntime.cs",
                "AntonioStacklundTipSender.cs",
                "AntonioStacklundCombineRules.cs",
                "KarliCappelleriQuestRuntime.cs",
                "KarliCappelleriTipSender.cs",
                "LeonoraMartyQuestRuntime.cs",
                "LeonoraMartyTipSender.cs",
                "PatrickSunQuestRuntime.cs",
                "PatrickSunTipSender.cs",
                "RemiGalloisQuestRuntime.cs",
                "RemiGalloisTipSender.cs",
                "ShinySwordQuestRuntime.cs",
                "ShinySwordTipSender.cs"
            };

        private static readonly string[] DialoguePacks =
            {
                "antonio-stacklund.dialogue.json",
                "karli-cappelleri.dialogue.json",
                "leonora-marty.dialogue.json",
                "patrick-sun.dialogue.json",
                "remi-gallois.dialogue.json",
                "greedy-desert-reet.dialogue.json"
            };

        [TestMethod]
        public void CapturedQuestSourcesAndDialoguePacksAreProductionContent()
        {
            string project = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");
            string manifest = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Content\Arete\flint-novak\manifest.json");

            foreach (string source in QuestSources)
            {
                AssertContains(project, @"Core\Arete\Quests\" + source);
            }

            foreach (string pack in DialoguePacks)
            {
                AssertContains(project, @"Content\Arete\flint-novak\dialogue\" + pack);
                AssertContains(manifest, "dialogue/" + pack);
            }
        }

        [TestMethod]
        public void CapturedNpcRegistrationsPreserveIdentityNameAndPlayfieldConstraints()
        {
            string router = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Dialogue\ContentDrivenNpcDialogueRouter.cs");

            AssertRegistration(router, "Antonio Stacklund", "0x78E0FC7C", "SimpleChar:78E0FC7C");
            AssertRegistration(router, "Karli Cappelleri", "0x799AD394", "SimpleChar:799AD394");
            AssertRegistration(router, "Leonora Marty", "0x78E0FC74", "SimpleChar:78E0FC74");
            AssertRegistration(router, "Patrick Sun", "0x78E0FC7B", "SimpleChar:78E0FC7B");
            AssertRegistration(router, "Remi Gallois", "0x78E0FC75", "SimpleChar:78E0FC75");
            AssertRegistration(router, "Greedy Desert Reet", "0x79978BB8", "SimpleChar:79978BB8");
            AssertInOrder(
                router,
                "KarliCappelleriNpcIdentity,",
                "8009,",
                "ARETE_KARLI_DIALOGUE");
            AssertContains(router, "AntonioStacklundRegistration,");
            AssertContains(router, "GreedyDesertReetRegistration,");
        }

        [TestMethod]
        public void DialogueDispatchPreservesCapturedBranchPacketAndTradeOrder()
        {
            string router = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Dialogue\ContentDrivenNpcDialogueRouter.cs");

            AssertInOrder(
                router,
                "DialogueSessionResult result = service.SelectOption(session, answerIndex);",
                "KarliCappelleriQuestRuntime.ApplyDoingBranchOverride(",
                "if (result.Session == null || !result.Session.IsActive)");
            AssertInOrder(
                router,
                "SendDialogueNode(source, result, registration, suppressOptionsForTradeHold);",
                "DispatchCapturedQuestAnswerSideEffects(");
            AssertInOrder(
                router,
                "SendDialoguePromptOnly(source, result, registration);",
                "TryHandleLeonoraTradeHoldSideEffect(");
            AssertContains(router, "ShinySwordQuestRuntime.TryBeginSwordTrade(");
            AssertContains(router, "RemiGalloisQuestRuntime.EmitAcceptTipAndHellfyre(source);");
            AssertContains(router, "AntonioStacklundQuestRuntime.TryHandleDialogueAnswer(");
            AssertContains(router, "CapturedAreteAntonioStacklundVendorInteractionHandler.Default.TryOpenShop(");
            AssertContains(router, "CapturedAreteRemiGalloisVendorInteractionHandler.Default.TryOpenShop(");
            AssertContains(router, "RemiGalloisQuestRuntime.IsCompleted(source)");
            Assert.IsFalse(
                ReadRepositoryFile(
                        @"AORebirth\Server\ZoneEngine\Content\Arete\flint-novak\dialogue\remi-gallois.dialogue.json")
                    .Contains("inferred post-complete"));
        }

        [TestMethod]
        public void CapturedItemAndWorldUseRoutesRunBeforeGenericFallbacks()
        {
            string inventory = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string interactions = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldInteractionRuntimeService.cs");
            string patrick = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\PatrickSunQuestRuntime.cs");

            AssertInOrder(
                inventory,
                "ShinySwordQuestRuntime.TryHandleShinySwordUse",
                "LeonoraMartyQuestRuntime.TryHandleCreditCardStealUse",
                "LeonoraMartyQuestRuntime.TryHandleVacuumPackedOmniMedSuitUse",
                "KarliCappelleriQuestRuntime.TryHandleFriendlyBuffUse",
                "TemplateActionMessageHandler.Default.Send(");
            AssertInOrder(
                interactions,
                "LeonoraMartyQuestRuntime.TryHandleCreditCardPickup",
                "PatrickSunQuestRuntime.TryHandleInsuranceTerminalUse",
                "InsuranceTerminalInteractionHandler.Default.TryHandleUse");
            AssertContains(patrick, "target.Instance != unchecked((int)0x574187D0)");
        }

        [TestMethod]
        public void CapturedTradeHandlersOwnStageSuppressionAndFinish()
        {
            string stage = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotTradeMessageHandler.cs");
            string finish = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotFinishTradeMessageHandler.cs");

            AssertContains(stage, "LeonoraMartyQuestRuntime.TryStageLeonoraTradeItem");
            AssertContains(stage, "ShinySwordQuestRuntime.TryStageSwordTradeItem");
            AssertContains(stage, "LeonoraMartyQuestRuntime.ShouldSuppressGenericLeonoraTradeRemove");
            AssertContains(stage, "ShinySwordQuestRuntime.ShouldSuppressGenericSwordTradeRemove");
            AssertInOrder(
                finish,
                "LeonoraMartyQuestRuntime.TryFinishLeonoraTrade(",
                "ShinySwordQuestRuntime.TryFinishSwordTrade(",
                "Pool.Instance.GetObject<ICharacter>");
        }

        [TestMethod]
        public void RemiDeathProgressRequiresExactCapturedMarauderEvidence()
        {
            string rewards = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRewardRuntimeService.cs");
            string remi = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\RemiGalloisQuestRuntime.cs");
            string owner = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteSandstormMarauderRuntime.cs");

            AssertContains(rewards, "RemiGalloisQuestRuntime.TryObserveNpcDeath(attacker, target);");
            AssertContains(remi, "AreteSandstormMarauderRuntime.IsRegisteredMarauder(target)");
            AssertContains(owner, "string.Equals(target.Name, MarauderName, StringComparison.OrdinalIgnoreCase)");
            AssertContains(owner, "target.Stats[StatIds.level].Value != MarauderLevel");
            AssertContains(owner, "target.Stats[StatIds.npcfamily].Value != MarauderNpcFamily");
            AssertContains(owner, "state.CurrentIdentity.Instance == target.Identity.Instance");
        }

        [TestMethod]
        public void AntonioCombinesUseCapturedQlOneOverflowAndTipLifecycle()
        {
            string tradeSkill = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\TradeSkill.cs");
            string receiver = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\TradeSkillReceiver.cs");

            AssertContains(tradeSkill, "AntonioStacklundCombineRules.TryMatch(");
            AssertContains(tradeSkill, "AntonioStacklundCombineRules.SourceProcessBonus(high)");
            AssertContains(tradeSkill, "AntonioStacklundCombineRules.TargetProcessBonus(high)");
            AssertContains(receiver, "bool antonioCombine =");
            AssertContains(receiver, "if ((masonAssemble || antonioCombine) && newItem.Quality != 1)");
            AssertInOrder(
                receiver,
                "else if (antonioCombine)",
                ".SendCombineResultClientPackets(",
                "AntonioStacklundQuestRuntime.OnCombineSucceeded(");
        }

        [TestMethod]
        public void CapturedTimingAndMovementLifecycleHaveNoInventedTimersOrLegacyLeonoraLoop()
        {
            string patrick = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\PatrickSunQuestRuntime.cs");
            string remi = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\RemiGalloisQuestRuntime.cs");
            string leonora = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\LeonoraMartyQuestRuntime.cs");
            string movement = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteMovementRuntimeService.cs");

            AssertContains(patrick, "private const int PatrickKillDelayMilliseconds = 2954;");
            Assert.IsFalse(patrick.Contains("PatrickKillDelayMilliseconds = 1200"));
            Assert.IsFalse(remi.Contains("QuellTipResendDelayMilliseconds"));
            Assert.IsFalse(remi.Contains("ReturnTipResendDelayMilliseconds"));
            Assert.IsFalse(remi.Contains("Thread.Sleep"));
            AssertInOrder(
                remi,
                "RemiGalloisTipSender.TrySendQuellTipOnly(source);",
                "TryGrantHellfyreLauncher(source);");
            Assert.IsFalse(leonora.Contains("AreteLeonoraMartyPatrolRuntime"));
            AssertContains(leonora, "playfield.SuspendCapturedAretePatrol(npc);");
            AssertContains(leonora, "playfield.ResumeCapturedAretePatrol(npc);");
            AssertContains(movement, "this.suspendedPatrols.Contains(character.Identity.Instance)");
            AssertContains(movement, "this.suspendedPatrols.Clear();");
        }

        [TestMethod]
        public void GreedyReetIdentityFailsClosedToOwnedRuntimeGeneration()
        {
            string quest = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\ShinySwordQuestRuntime.cs");
            string owner = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\LoreleiOasisMobRuntime.cs");

            AssertContains(quest, "LoreleiOasisMobRuntime.IsRegisteredGreedyDesertReet(npc)");
            AssertContains(owner, "string.Equals(npc.Name, \"Greedy Desert Reet\"");
            AssertContains(owner, "npc.Stats[StatIds.level].Value != 7");
            AssertContains(owner, "return OasisReetInstances.Contains(npc.Identity.Instance);");
            string router = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Dialogue\ContentDrivenNpcDialogueRouter.cs");
            AssertContains(router, "FindGreedyDesertReetRuntimeRegistration(npc)");
            AssertContains(router, "IsRegistration(registration, GreedyDesertReetRegistration)");
        }

        [TestMethod]
        public void AreteRewardDeltasAndRetryMarkersPreserveCapturedDurableSemantics()
        {
            string rex = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\RexB18ECompletionHandler.cs");
            AssertContains(rex, "private const int XpReward = 290;");
            AssertContains(rex, "Received reward: 1281 XP, 1040 credits.");
            AssertContains(rex, "displayXp=1281 actualXpDelta=290");
            AssertContains(rex, "rex-b18e-return-290xp");
            Assert.IsFalse(rex.Contains("private const int XpReward = 1281;"));

            string stan = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\StanGoodmanQuestRuntime.cs");
            AssertContains(stan, "buy-nano-tip-rewards-granted");
            AssertContains(stan, "buy-nano-tip-rewards-v2-granted");
            AssertContains(
                stan,
                "private static MissionRewardExecutionResult ApplyBuyNanoTipXpCredits");
            AssertContains(stan, "MissionRuntime.Rewards.ExecuteAtomicCharacterStats(");
            AssertInOrder(
                stan,
                "if (HasLegacyBuyNanoTipRewardsGranted(character))",
                "legacy marker migrated without replaying unjournaled stats",
                "MissionRewardExecutionResult statsResult = ApplyBuyNanoTipXpCredits(character);");
            AssertInOrder(
                stan,
                "MissionRewardExecutionResult statsResult = ApplyBuyNanoTipXpCredits(character);",
                "TrySendBuyNanoTipRewardFeedback(character);",
                "if (!TryGrantBuyNanoTipReward(character))",
                "MissionOperationResult completionMarker = MarkBuyNanoTipRewardsGranted(character);",
                "ForceCompleteHandoffTip(");
            Assert.IsFalse(stan.Contains("buy-nano-tip-2581xp"));
        }

        [TestMethod]
        public void MergedAreteTurnInsUseAtomicLegacyAwareRewardsBeforeConsumingItems()
        {
            string wounded = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\MarcusWoundedWorkersQuestRuntime.cs");
            AssertContains(wounded, "private const int StimReturnXpReward = 1281;");
            AssertContains(wounded, "marcus-wounded-xp-credits-1281-1040");
            AssertContains(wounded, "captured-marcus-stim-return-xp-credits-2076-1040");
            AssertContains(wounded, "LegacyRewardKeys = new[]");
            AssertInOrder(
                wounded,
                "if (!ApplyStimReturnRewards(source))",
                "TryConsumeStim(source, stagedContainer);");
            Assert.IsFalse(wounded.Contains("stim-return rewards live-fallback"));

            string flint = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\FlintBioComQuestRuntime.cs");
            AssertContains(flint, "LegacyRewardKeys = new[]");
            AssertContains(flint, "captured-alex-bio-com-turnin-xp-credits");
            AssertInOrder(
                flint,
                "if (!ApplyAlexTurnInXpCredits(source))",
                "TryConsumeBioCom(source, stagedContainer);");
            Assert.IsFalse(flint.Contains("ApplyDirectXpCreditsFallback"));

            string marcus = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Quests\RexMarcusChainCoordinator.cs");
            AssertContains(marcus, "captured-marcus-return-xp-credits");
            AssertContains(marcus, "LegacyMarcusReturnXpCreditsFlag");
            AssertInOrder(
                marcus,
                "if (!ApplyMarcusReturnRewards(source))",
                "TryConsumeSuppressant(source, stagedContainer);");
            AssertContains(marcus, "MergedMarcusReturnCreditRewardKey");
            AssertContains(marcus, "captured-marcus-return-xp-2076-recovery");
            AssertContains(marcus, "MissionRuntime.Rewards.IsRewardApplied(");
            Assert.IsFalse(marcus.Contains("CombatXpRuntimeService.AwardDirectXp("));
        }

        [TestMethod]
        public void AretePetLosAndPetChatRepairsRemainNarrowlyScoped()
        {
            string combat = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs");
            AssertContains(combat, "private const int AreteLandingPlayfieldId = 6553;");
            AssertContains(combat, "this.playfield.Identity.Instance == AreteLandingPlayfieldId");
            AssertInOrder(
                combat,
                "private bool CanApplyNpcDamage(",
                "this.playfield.Identity.Instance == AreteLandingPlayfieldId",
                "this.nextLineOfSightRetryTicks.Remove(attacker.Identity.Instance);");

            string packets = ReadRepositoryFile(@"AORebirth\Server\ChatEngine\Packets\MsgSystem.cs");
            AssertInOrder(
                packets,
                "public static byte[] Create(string message)",
                "MessageType.SystemMessage",
                "public static byte[] CreatePet(string message, int unk1, int unk2)",
                "MessageType.AnonymousMessage");

            string chatServer = ReadRepositoryFile(@"AORebirth\Server\ChatEngine\CoreServer\ChatServer.cs");
            AssertContains(chatServer, "MsgSystem.CreatePet(body, unk1, unk2)");
            AssertContains(chatServer, "LogUtil.Debug(DebugInfoDetail.Network, ok);");
            Assert.IsFalse(chatServer.Contains("Console.WriteLine(ok)"));
            Assert.IsFalse(chatServer.Contains("Console.WriteLine(miss)"));

            string tell = ReadRepositoryFile(@"AORebirth\Server\ChatEngine\PacketHandlers\Tell.cs");
            AssertContains(tell, "MsgSystem.Create(\"Player not online.\")");
        }

        [TestMethod]
        public void CaptureReadyCombatStillFailsClosedForUnsafeCatalogResolution()
        {
            string combat = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs");
            AssertContains(combat, "else if (!hasDirectCaptureCertification");
            AssertContains(combat, "string.IsNullOrWhiteSpace(resolutionFailure)");
            AssertContains(combat, "!resolutionFailure.StartsWith(");
            AssertContains(combat, "no canonical raw combat profile for ");
            Assert.IsFalse(
                combat.Contains("CapturedEnemyCombatKeepCertified"),
                "Ambiguous or mismatched catalog resolution must quarantine even a direct-ready contract.");
        }

        private static void AssertRegistration(
            string router,
            string name,
            string sourceHex,
            string identityText)
        {
            AssertContains(router, "unchecked((int)" + sourceHex + ")");
            AssertContains(router, "\"" + identityText + "\"");
            AssertContains(router, "\"" + name + "\"");
        }

        private static void AssertInOrder(string source, params string[] values)
        {
            int cursor = 0;
            foreach (string value in values)
            {
                int position = source.IndexOf(value, cursor, StringComparison.Ordinal);
                Assert.IsTrue(position >= cursor, "Missing or out-of-order text: " + value);
                cursor = position + value.Length;
            }
        }

        private static void AssertContains(string source, string value)
        {
            Assert.IsTrue(source.Contains(value), "Missing text: " + value);
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
