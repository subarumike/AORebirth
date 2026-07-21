namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture 20260720-190432: tip advances after each Personalized Robot Brain combine,
    /// and Alex turn-in of Personalized Basic Robot Brain (tip 4/4).
    /// </summary>
    public static class PersonalizedRobotBrainQuestRuntime
    {
        public const string AlexBrainTurnInNodeId = "alex_190432_brain_turnin";

        public const string Tip1QuestId = "Mission:555B4367";

        public const string Tip2QuestId = "Mission:555B4368";

        public const string Tip3QuestId = "Mission:555B4369";

        public const string Tip4QuestId = "Mission:555B436A";

        private const int Tip1Instance = unchecked((int)0x555B4367);

        private const int Tip2Instance = unchecked((int)0x555B4368);

        private const int Tip3Instance = unchecked((int)0x555B4369);

        private const int Tip4Instance = unchecked((int)0x555B436A);

        private const int AlexGibbsInstance = unchecked((int)0x78E0FC61);

        private static readonly object Gate = new object();

        private static readonly Dictionary<int, BrainTradeSession> TradeByCharacter =
            new Dictionary<int, BrainTradeSession>();

        private sealed class BrainTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static void OnCombineSucceeded(ICharacter source, int resultLowId, int resultHighId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            if (resultLowId == PersonalizedRobotBrainCombineRules.NanoSensorLowId
                || resultHighId == PersonalizedRobotBrainCombineRules.NanoSensorHighId
                || resultLowId == PersonalizedRobotBrainCombineRules.NanoSensorHighId)
            {
                AdvanceTip(source, Tip1QuestId, Tip1Instance, Tip2QuestId, Tip2Instance, 2);
                return;
            }

            if (resultLowId == PersonalizedRobotBrainCombineRules.BasicRobotBrainLowId
                || resultHighId == PersonalizedRobotBrainCombineRules.BasicRobotBrainHighId
                || resultLowId == PersonalizedRobotBrainCombineRules.BasicRobotBrainHighId)
            {
                AdvanceTip(source, Tip2QuestId, Tip2Instance, Tip3QuestId, Tip3Instance, 3);
                return;
            }

            if (PersonalizedRobotBrainCombineRules.IsPersonalizedBrain(resultLowId, resultHighId))
            {
                AdvanceTip(source, Tip3QuestId, Tip3Instance, Tip4QuestId, Tip4Instance, 4);
            }
        }

        public static string ResolveAlexStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            // Inventory is authoritative: tip 4 may be missing from MissionRuntime after
            // a combine when OfferMission lagged, but the crafted brain is still held.
            if (HasPersonalizedBrain(source) || IsTipActive(source, Tip4QuestId))
            {
                return AlexBrainTurnInNodeId;
            }

            return null;
        }

        public static bool TryHandleAlexDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            // Trade open is owned by ContentDrivenNpcDialogueRouter trade-hold side effect.
            if (string.Equals(previousNodeId, AlexBrainTurnInNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool TryBeginBrainTurnInTrade(ICharacter source, Identity alexIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (alexIdentity.Type != IdentityType.CanbeAffected || alexIdentity.Instance == 0)
            {
                alexIdentity = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = AlexGibbsInstance
                               };
            }

            // Capture 20260721-001538: Answer → StartTrade only (same as BioCom Deliver).
            // Never send Quest Delete/QFU here — tip packets around StartTrade strip slots/Accept.
            BeginTrade(source, alexIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                alexIdentity,
                "Drag and drop the item(s) you want to give to Alex Gibbs into one of the slots available and press \"accept\"",
                1);
            Log(
                "brain-turnin-trade-opened character="
                + source.Identity.ToString(true)
                + " target="
                + alexIdentity.ToString(true)
                + " slots=1");
            return true;
        }

        public static bool TryStageBrainTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            BrainTradeSession existing = GetTrade(source);
            // Live Alex spawn instance != catalog 0x78E0FC61 (ZoneEngineLog: CanbeAffected:1000010).
            bool alex = IsAlexGibbs(source, message.Target)
                        || (existing != null && existing.NpcIdentity.Instance == message.Target.Instance);
            if (!alex)
            {
                return false;
            }

            if (!IsTipActive(source, Tip4QuestId) && existing == null && !HasPersonalizedBrain(source))
            {
                return false;
            }

            BeginTrade(source, message.Target);
            BrainTradeSession session = GetTrade(source);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance > 0)
            {
                // Capture: client stages Inventory slot then FinishTrade. Record any slot;
                // finish path verifies it is the Personalized Brain (or finds one in bags).
                session.StagedContainer = message.Container;
            }

            // Never let generic Remove delete the brain during inspect staging.
            return true;
        }

        public static bool ShouldSuppressGenericAlexTradeRemove(ICharacter source, Identity target)
        {
            if (source == null)
            {
                return false;
            }

            BrainTradeSession existing = GetTrade(source);
            bool alex = IsAlexGibbs(source, target)
                        || (existing != null && existing.NpcIdentity.Instance == target.Instance);
            if (!alex)
            {
                return false;
            }

            return existing != null
                   || IsTipActive(source, Tip4QuestId)
                   || HasPersonalizedBrain(source);
        }

        public static bool TryFinishBrainTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            BrainTradeSession session = GetTrade(source);
            bool tipActive = IsTipActive(source, Tip4QuestId);
            bool hasBrain = HasPersonalizedBrain(source);
            bool isAlex = IsAlexGibbs(source, message.Target)
                          || (session != null
                              && (IsAlexGibbs(source, session.NpcIdentity)
                                  || session.NpcIdentity.Instance == message.Target.Instance));

            // Only claim Alex brain inspect — never steal BioCom Deliver finish.
            // ZoneEngineLog 00:32:53: IsAlex catalog-only miss → FlintBioCom ate FinishTrade,
            // generic Remove already deleted 156026 on Trade.
            if (!isAlex || (session == null && !tipActive && !hasBrain))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTrade(source);
                return true;
            }

            if (session == null)
            {
                BeginTrade(source, message.Target);
                session = GetTrade(source);
            }

            Identity alexTarget = message.Target.Instance != 0
                                      ? message.Target
                                      : (session != null ? session.NpcIdentity : message.Target);
            if (alexTarget.Type != IdentityType.CanbeAffected || alexTarget.Instance == 0)
            {
                alexTarget = new Identity
                             {
                                 Type = IdentityType.CanbeAffected,
                                 Instance = AlexGibbsInstance
                             };
            }

            Identity staged = session != null ? session.StagedContainer : Identity.None;
            int brainLowId;
            int brainHighId;
            int brainQuality;
            // Capture 20260721-001538: inspect/keep — RejectedItems [] Unknown2=1, no DeleteItem.
            // Accept if staged brain OR any Personalized Brain still in inventory.
            if (!TryFindPersonalizedBrain(source, staged, out brainLowId, out brainHighId, out brainQuality)
                && !TryFindPersonalizedBrain(source, Identity.None, out brainLowId, out brainHighId, out brainQuality))
            {
                brainLowId = PersonalizedRobotBrainCombineRules.PersonalizedBasicRobotBrainLowId;
                brainHighId = PersonalizedRobotBrainCombineRules.PersonalizedBasicRobotBrainLowId;
                brainQuality = 1;
                if (!HasPersonalizedBrain(source) && staged.Type == IdentityType.None)
                {
                    Log("brain-finish ignored: no Personalized Robot Brain to show");
                    BeginTrade(source, alexTarget);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        alexTarget,
                        "Drag and drop the item(s) you want to give to Alex Gibbs into one of the slots available and press \"accept\"",
                        1);
                    return true;
                }
            }

            // Prefer QL1 capture shape when high template missing.
            if (!ItemLoader.ItemList.ContainsKey(brainHighId))
            {
                brainHighId = brainLowId;
            }

            // Capture 20260721-001538: RejectedItems [] Unknown2=1 → return → AnswerList → tip deletes.
            KnuBotRejectedItemsMessageHandler.Default.Send(source, alexTarget, new Item[0], 1);
            TryForceReturnPersonalizedBrain(source, brainLowId, brainHighId, brainQuality);
            ForgetTrade(source);
            try
            {
                ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, alexTarget);
            }
            catch (Exception ex)
            {
                Log("brain-turnin resume-dialogue failed: " + ex.Message);
            }

            // Tip cleanup after dialogue resume — never before StartTrade / mid-trade chrome.
            CompleteTip4(source);

            Log("brain-turnin-complete character=" + source.Identity.ToString(true));
            return true;
        }

        private static void AdvanceTip(
            ICharacter source,
            string fromQuestId,
            int fromInstance,
            string toQuestId,
            int toInstance,
            int step)
        {
            int characterInstance = source.Identity.Instance;
            FlintKneecappingTipWire.TryDeleteTip(source, fromInstance);
            // Capture: only the current tip step is visible (plus Talk to Stan).
            if (step >= 2)
            {
                FlintKneecappingTipWire.TryDeleteTip(source, Tip1Instance);
            }

            if (step >= 3)
            {
                FlintKneecappingTipWire.TryDeleteTip(source, Tip2Instance);
            }

            if (step >= 4)
            {
                FlintKneecappingTipWire.TryDeleteTip(source, Tip3Instance);
            }

            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.CompleteMission(characterInstance, fromQuestId);
                MissionRuntime.Service.OfferMission(characterInstance, toQuestId);
                MissionRuntime.Service.AcceptMission(characterInstance, toQuestId);
            }

            switch (step)
            {
                case 2:
                    SafeQuestFullUpdateSender.TrySendTradeskillBasicBrainTip(source);
                    break;
                case 3:
                    SafeQuestFullUpdateSender.TrySendTradeskillPersonalizedBrainTip(source);
                    break;
                case 4:
                    SafeQuestFullUpdateSender.TrySendTradeskillShowBrainTip(source);
                    break;
            }

            Log(
                "tip-advance→"
                + toQuestId
                + " character="
                + source.Identity.ToString(true)
                + " source=20260721-001538");
        }

        private static void CompleteTip4(ICharacter source)
        {
            FlintKneecappingTipWire.TryDeleteTip(source, Tip4Instance);
            FlintKneecappingTipWire.TryDeleteTip(source, Tip3Instance);
            FlintKneecappingTipWire.TryDeleteTip(source, Tip2Instance);
            FlintKneecappingTipWire.TryDeleteTip(source, Tip1Instance);
            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, Tip4QuestId);
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, Tip3QuestId);
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, Tip2QuestId);
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, Tip1QuestId);

                // Never leave early-chain tips after Tip 4 (Surveillance Uplink is first Alex tip).
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, "Mission:5514B19D");
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, "Mission:5514B19E");
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, "Mission:5514B19F");
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, "Mission:5514B1A0");
                MissionRuntime.Service.CompleteMission(source.Identity.Instance, "Mission:555B4365");
            }

            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555A4A49));
            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x5514B19D));
            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555A4E3B));
            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555A4E3C));
            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555A4E3D));
            FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555B4365));

            // Soft refresh Talk to Stan only — stacks beside Tip 4 while Tip 4 was up;
            // after Tip 4 complete, main quest tip stays. Capture 20260720-190432.
            if (MissionRuntime.IsInitialized && IsTalkToStanStillOpen(source))
            {
                SafeQuestFullUpdateSender.TryRefreshTalkToStanTip(source);
            }
        }

        private static bool IsTalkToStanStillOpen(ICharacter source)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, "Mission:555B4366");
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool TryFindPersonalizedBrain(
            ICharacter source,
            Identity stagedContainer,
            out int lowId,
            out int highId,
            out int quality)
        {
            lowId = 0;
            highId = 0;
            quality = 1;
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance > 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (IsPersonalizedBrainItem(staged))
                    {
                        lowId = staged.LowID;
                        highId = staged.HighID;
                        quality = staged.Quality > 0 ? staged.Quality : 1;
                        return true;
                    }
                }
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in page.List())
                {
                    IItem item = slot.Value;
                    if (!IsPersonalizedBrainItem(item))
                    {
                        continue;
                    }

                    lowId = item.LowID;
                    highId = item.HighID;
                    quality = item.Quality > 0 ? item.Quality : 1;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPersonalizedBrainItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            return PersonalizedRobotBrainCombineRules.IsPersonalizedBrain(item.LowID, item.HighID);
        }

        public static bool HasPersonalizedBrain(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    PersonalizedRobotBrainCombineRules.PersonalizedBasicRobotBrainLowId)
                || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    PersonalizedRobotBrainCombineRules.PersonalizedBasicRobotBrainHighId))
            {
                return true;
            }

            if (source.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pair in source.BaseInventory.Pages)
            {
                IInventoryPage page = pair.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> slot in page.List())
                {
                    if (IsPersonalizedBrainItem(slot.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void TryForceReturnPersonalizedBrain(
            ICharacter source,
            int lowId,
            int highId,
            int quality)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(source))
            {
                return;
            }

            int ql = quality > 0 ? quality : 1;
            // Capture 20260721-001538: TemplateAction 156026/156026 ql1.
            int templateId = lowId > 0 ? lowId : PersonalizedRobotBrainCombineRules.PersonalizedBasicRobotBrainLowId;
            int highTemplateId = highId > 0 ? highId : templateId;
            if (!ItemLoader.ItemList.ContainsKey(templateId))
            {
                Log("brain return skipped missing template=" + templateId);
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(highTemplateId))
            {
                highTemplateId = templateId;
            }

            // Always ensure inventory holds the brain (inspect never consumes it).
            if (!HasPersonalizedBrain(source))
            {
                Item item;
                try
                {
                    item = new Item(ql, templateId, highTemplateId);
                }
                catch (Exception)
                {
                    try
                    {
                        item = new Item(ql, templateId, templateId);
                        highTemplateId = templateId;
                    }
                    catch (Exception ex)
                    {
                        Log("brain return create failed: " + ex.Message);
                        return;
                    }
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    Log("brain return grant failed status=" + grant.Status);
                    return;
                }
            }

            // Client trade UI can hide the brain even when the server still holds it.
            source.Controller.Client.SendCompressed(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = templateId,
                    ItemHighId = highTemplateId,
                    Quality = ql,
                    Unknown1 = 1,
                    Unknown2 = 87,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            source.Controller.Client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = source.Identity.Instance
                             },
                    TargetPlacement = 0x6F
                });
            Log("brain returned character=" + source.Identity.ToString(true));
        }

        private static bool IsTipActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        /// <summary>
        /// Catalog instance OR live spawn named Alex Gibbs (ZoneEngineLog target=1000010).
        /// </summary>
        private static bool IsAlexGibbs(ICharacter source, Identity identity)
        {
            if (identity.Type == IdentityType.CanbeAffected && identity.Instance == AlexGibbsInstance)
            {
                return true;
            }

            if (source == null || source.Playfield == null)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, identity);
            return npc != null && string.Equals(npc.Name, "Alex Gibbs", StringComparison.OrdinalIgnoreCase);
        }

        private static void BeginTrade(ICharacter source, Identity npc)
        {
            lock (Gate)
            {
                TradeByCharacter[source.Identity.Instance] = new BrainTradeSession
                                                             {
                                                                 NpcIdentity = npc,
                                                                 StagedContainer = Identity.None
                                                             };
            }
        }

        private static BrainTradeSession GetTrade(ICharacter source)
        {
            lock (Gate)
            {
                BrainTradeSession session;
                TradeByCharacter.TryGetValue(source.Identity.Instance, out session);
                return session;
            }
        }

        private static void ForgetTrade(ICharacter source)
        {
            lock (Gate)
            {
                TradeByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "PersonalizedRobotBrainQuestRuntime " + message);
        }
    }
}
