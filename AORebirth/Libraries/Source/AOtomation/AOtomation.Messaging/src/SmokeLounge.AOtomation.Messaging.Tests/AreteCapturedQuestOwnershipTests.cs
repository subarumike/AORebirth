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
