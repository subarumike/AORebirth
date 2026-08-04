namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260721-sara: Talk to Sarah → Find the thief → Use Remains of Shop Thief
    /// (Terminal:574187CF) → grant DNA-Locked Armor 295618 QL200 → Deliver tip →
    /// StartTrade → FinishTrade → Speak to Vernon Godfray.
    /// </summary>
    public static class SarahGreeneQuestRuntime
    {
        public const string AcceptOfferNodeId = "sarah_greene_001";

        public const string DeliverOfferNodeId = "sarah_deliver_001";

        public const string DeliverTradeHoldNodeId = "sarah_deliver_trade";

        public const string TalkToSarahGreeneQuestId = "Mission:555BE9F3";

        public const string FindTheThiefQuestId = "Mission:555BE9F5";

        public const string DeliverDnaLockedArmorQuestId = "Mission:555BE9F6";

        public const string SpeakToVernonGodfrayQuestId = "Mission:555BE9F7";

        private const int AreteLandingPlayfieldId = 6553;

        private const int SarahGreeneInstance = unchecked((int)0x78E0FC69);

        private const int RemainsOfShopThiefTemplateId = 295620;

        private const int RemainsOfShopThiefInstance = unchecked((int)0x574187CF);

        private const int DnaLockedArmorItemId = 295618;

        private const int DnaLockedArmorQuality = 200;

        private const int SarahTurnInRewardItemId = 296574;

        private const int SarahTurnInXpReward = 2229;

        private const int SarahTurnInCreditReward = 1280;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const int CapturedTradeSlotCount = 1;

        // Capture 20260721-sara FormatFeedback after armor Accept.
        private const string SarahTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!0&~";

        private const string SarahTradePrompt =
            "Drag and drop the item(s) you want to give to Sarah Greene into one of the slots available and press \"accept\"";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, SarahTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, SarahTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private sealed class SarahTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static string ResolveSarahStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (IsDeliverTipActive(source) || HasDnaLockedArmor(source))
            {
                return DeliverOfferNodeId;
            }

            return null;
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            if (!string.Equals(previousNodeId, AcceptOfferNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsMissionActiveOrCompleted(source, FindTheThiefQuestId)
                || IsMissionActiveOrCompleted(source, DeliverDnaLockedArmorQuestId)
                || IsMissionActiveOrCompleted(source, SpeakToVernonGodfrayQuestId)
                || HasDnaLockedArmor(source))
            {
                Log("accept-offer ignored — sarah chain already progressed");
                return true;
            }

            CompleteTalkToSarahAndOfferFindThief(source);
            return true;
        }

        public static bool TryBeginSarahTrade(ICharacter source, Identity sarahIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (!IsDeliverTipActive(source) && !HasDnaLockedArmor(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            if (sarahIdentity.Type != IdentityType.CanbeAffected || sarahIdentity.Instance == 0)
            {
                sarahIdentity = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = SarahGreeneInstance
                               };
            }

            BeginSarahTrade(source, sarahIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                sarahIdentity,
                SarahTradePrompt,
                CapturedTradeSlotCount);
            Log("sarah-start-trade character=" + source.Identity.ToString(true));
            return true;
        }

        public static bool TryStageSarahTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsSarahNpc(character, message.Target))
            {
                return false;
            }

            if (!HasDnaLockedArmor(character) && !IsDeliverTipActive(character) && GetTradeSession(character) == null)
            {
                return false;
            }

            BeginSarahTrade(character, message.Target);
            SarahTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "sarah-trade-staged character="
                    + character.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true)
                    + " target="
                    + message.Target.ToString(true));
            }

            return true;
        }

        public static bool ShouldSuppressGenericSarahTradeRemove(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsSarahNpc(character, message.Target))
            {
                return false;
            }

            return HasDnaLockedArmor(character) || IsDeliverTipActive(character) || GetTradeSession(character) != null;
        }

        public static bool TryFinishSarahTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsSarahNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            SarahTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                BeginSarahTrade(source, message.Target);
                session = GetTradeSession(source);
            }

            Identity staged = session != null ? session.StagedContainer : Identity.None;
            if (staged.Type == IdentityType.None || staged.Instance < 0)
            {
                if (!TryFindArmorContainer(source, out staged))
                {
                    Log("sarah-finish without staged armor — reopen trade");
                    BeginSarahTrade(source, message.Target);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        message.Target,
                        SarahTradePrompt,
                        CapturedTradeSlotCount);
                    return true;
                }
            }

            ApplySarahTradeTurnIn(source, message.Target, staged);
            return true;
        }

        /// <summary>
        /// Capture 20260721-sara: GenericCmd Use Terminal:574187CF Remains of Shop Thief.
        /// </summary>
        public static bool TryHandleShopThiefUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || target.Type != IdentityType.Terminal)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != AreteLandingPlayfieldId
                || !(character.Controller is PlayerController))
            {
                return false;
            }

            if (!IsRemainsOfShopThief(character, target))
            {
                return false;
            }

            if (!IsMissionLifecycle(character, FindTheThiefQuestId, true, false)
                && !HasDnaLockedArmor(character)
                && !IsMissionLifecycle(character, DeliverDnaLockedArmorQuestId, true, true))
            {
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            if (!HasDnaLockedArmor(character))
            {
                if (!ItemLoader.ItemList.ContainsKey(DnaLockedArmorItemId))
                {
                    Log("shop-thief skipped: ItemLoader missing armor id=" + DnaLockedArmorItemId);
                    return true;
                }

                Item armor;
                try
                {
                    armor = new Item(
                        DnaLockedArmorQuality,
                        DnaLockedArmorItemId,
                        DnaLockedArmorItemId);
                }
                catch (Exception ex)
                {
                    Log("armor create failed: " + ex.Message);
                    return true;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, armor);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    Log(
                        "armor grant failed status="
                        + grant.Status
                        + " invErr="
                        + grant.InventoryError);
                }
                else
                {
                    SendOverflowGrantPackets(character, DnaLockedArmorItemId, DnaLockedArmorQuality);
                }
            }

            CompleteFindThiefAndOfferDeliver(character);
            Log(
                "shop-thief-looted character="
                + character.Identity.ToString(true)
                + " target="
                + target.ToString(true));
            return true;
        }

        public static bool TrySyncTipsForLogin(ICharacter source)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            bool pastTalkSarah =
                IsMissionLifecycle(source, FindTheThiefQuestId, true, true)
                || IsMissionLifecycle(source, DeliverDnaLockedArmorQuestId, true, true)
                || IsMissionLifecycle(source, SpeakToVernonGodfrayQuestId, true, true);

            if (pastTalkSarah)
            {
                ForceCompleteHandoffTip(
                    characterId,
                    TalkToSarahGreeneQuestId,
                    "mission_555BE9F3_talk_sarah");
                FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555BE9F3));
            }

            if (IsMissionLifecycle(source, SpeakToVernonGodfrayQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendDeliverArmorToVernonHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, DeliverDnaLockedArmorQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendFindThiefToDeliverArmorHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, FindTheThiefQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendTalkSarahToFindThiefHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, TalkToSarahGreeneQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendDeliverFactoryToSarahAndNanoTipsHandoff(source);
                return true;
            }

            return false;
        }

        private static void CompleteTalkToSarahAndOfferFindThief(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    TalkToSarahGreeneQuestId,
                    "mission_555BE9F3_talk_sarah");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    TalkToSarahGreeneQuestId,
                    FindTheThiefQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, FindTheThiefQuestId);
                    MissionRuntime.Service.AcceptMission(instance, FindTheThiefQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendTalkSarahToFindThiefHandoff(source);
            Log("talk-sarah-complete→find-thief character=" + source.Identity.ToString(true));
        }

        private static void CompleteFindThiefAndOfferDeliver(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    FindTheThiefQuestId,
                    "mission_555BE9F5_find_thief");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    FindTheThiefQuestId,
                    DeliverDnaLockedArmorQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, DeliverDnaLockedArmorQuestId);
                    MissionRuntime.Service.AcceptMission(instance, DeliverDnaLockedArmorQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendFindThiefToDeliverArmorHandoff(source);
        }

        private static void CompleteDeliverAndOfferVernon(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    DeliverDnaLockedArmorQuestId,
                    "mission_555BE9F6_deliver_armor");
                MissionRuntime.Service.OfferMission(instance, SpeakToVernonGodfrayQuestId);
                MissionRuntime.Service.AcceptMission(instance, SpeakToVernonGodfrayQuestId);
            }

            SafeQuestFullUpdateSender.TrySendDeliverArmorToVernonHandoff(source);
        }

        private static void ApplySarahTradeTurnIn(ICharacter source, Identity sarahTarget, Identity stagedContainer)
        {
            int instance = source.Identity.Instance;
            lock (TradeSyncRoot)
            {
                if (!TurnInInFlightByCharacter.Add(instance))
                {
                    return;
                }
            }

            try
            {
                if (!TryConsumeInventoryItem(source, stagedContainer, DnaLockedArmorItemId))
                {
                    Log(
                        "sarah-turnin ABORTED — armor not consumed character="
                        + source.Identity.ToString(true)
                        + " staged="
                        + stagedContainer.ToString(true));
                    Identity reopenTarget = sarahTarget;
                    if (reopenTarget.Type != IdentityType.CanbeAffected || reopenTarget.Instance == 0)
                    {
                        reopenTarget = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = SarahGreeneInstance
                                       };
                    }

                    BeginSarahTrade(source, reopenTarget);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        reopenTarget,
                        SarahTradePrompt,
                        CapturedTradeSlotCount);
                }
                else
                {
                    try
                    {
                        KnuBotRejectedItemsMessageHandler.Default.Send(source, sarahTarget, new Item[0], 0);
                    }
                    catch (Exception ex)
                    {
                        Log("sarah-rejecteditems failed: " + ex.Message);
                    }

                    ApplySarahTurnInXpCredits(source);
                    TrySendSarahTurnInRewardFeedback(source);
                    TryGrantSarahTurnInRewardItem(source);
                    try
                    {
                        FeedbackMessageHandler.Default.Send(source, 110, 108871108);
                    }
                    catch (Exception ex)
                    {
                        Log("sarah-item-feedback failed: " + ex.Message);
                    }

                    CompleteDeliverAndOfferVernon(source);
                    ForgetTradeSession(source);
                    try
                    {
                        ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, sarahTarget);
                    }
                    catch (Exception ex)
                    {
                        Log("sarah-resume-dialogue failed: " + ex.Message);
                    }

                    Log("sarah-turnin done character=" + source.Identity.ToString(true));
                }
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static bool IsRemainsOfShopThief(ICharacter character, Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == RemainsOfShopThiefInstance)
            {
                return true;
            }

            StaticDynel dynel = Pool.Instance.GetObject<StaticDynel>(character.Playfield.Identity, target);
            if (dynel == null)
            {
                return false;
            }

            if (dynel.Template != null && dynel.Template.ID == RemainsOfShopThiefTemplateId)
            {
                return true;
            }

            int template;
            if (dynel.Stats != null
                && (dynel.Stats.TryGetValue((int)StatIds.acgitemtemplateid, out template)
                    || dynel.Stats.TryGetValue((int)StatIds.staticinstance, out template)))
            {
                return template == RemainsOfShopThiefTemplateId;
            }

            return false;
        }

        private static bool IsDeliverTipActive(ICharacter source)
        {
            return IsMissionLifecycle(source, DeliverDnaLockedArmorQuestId, true, false);
        }

        private static bool HasDnaLockedArmor(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       DnaLockedArmorItemId);
        }

        private static bool IsMissionActiveOrCompleted(ICharacter source, string questId)
        {
            return IsMissionLifecycle(source, questId, true, true);
        }

        private static bool IsMissionLifecycle(
            ICharacter source,
            string questId,
            bool includeActive,
            bool includeCompleted)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            if (mission == null)
            {
                return false;
            }

            if (includeActive && mission.State == MissionLifecycleState.Active)
            {
                return true;
            }

            return includeCompleted && mission.State == MissionLifecycleState.Completed;
        }

        private static void ForceCompleteHandoffTip(int characterId, string questId, string objectiveId)
        {
            if (!MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            if (mission == null || mission.State == MissionLifecycleState.Completed)
            {
                return;
            }

            if (mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, questId);
                mission = MissionRuntime.Service.GetMission(characterId, questId);
            }

            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return;
            }

            if (!string.IsNullOrEmpty(objectiveId))
            {
                MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = questId,
                        ObjectiveId = objectiveId,
                        ObservationKey = "sarah-greene-force-complete",
                        Amount = 1,
                        EventType = "SarahGreeneQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static void BeginSarahTrade(ICharacter source, Identity npcIdentity)
        {
            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new SarahTradeSession
                                                                    {
                                                                        NpcIdentity = npcIdentity,
                                                                        StagedContainer = Identity.None
                                                                    };
            }
        }

        private static SarahTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                SarahTradeSession session;
                return TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        private static void ForgetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static bool TryFindArmorContainer(ICharacter source, out Identity container)
        {
            container = Identity.None;
            if (source?.BaseInventory?.Pages == null)
            {
                return false;
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
                    if (item == null
                        || (item.LowID != DnaLockedArmorItemId && item.HighID != DnaLockedArmorItemId))
                    {
                        continue;
                    }

                    container = new Identity
                                {
                                    Type = (IdentityType)pageEntry.Key,
                                    Instance = slot.Key
                                };
                    return true;
                }
            }

            return false;
        }

        private static bool IsSarahNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == SarahGreeneInstance)
            {
                return true;
            }

            if (source == null)
            {
                return false;
            }

            SarahTradeSession session = GetTradeSession(source);
            if (session != null
                && session.NpcIdentity.Type == target.Type
                && session.NpcIdentity.Instance == target.Instance)
            {
                return true;
            }

            if (source.Playfield == null || target.Type != IdentityType.CanbeAffected)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null
                   && !string.IsNullOrEmpty(npc.Name)
                   && npc.Name.IndexOf("Greene", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
            {
                return false;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance >= 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null
                        && (staged.LowID == itemId || staged.HighID == itemId))
                    {
                        source.BaseInventory.RemoveItem((int)stagedContainer.Type, stagedContainer.Instance);
                        CharacterActionMessageHandler.Default.SendDeleteItem(
                            source,
                            (int)stagedContainer.Type,
                            stagedContainer.Instance);
                        return true;
                    }
                }
            }

            Identity found;
            if (!TryFindArmorContainer(source, out found))
            {
                return false;
            }

            source.BaseInventory.RemoveItem((int)found.Type, found.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(source, (int)found.Type, found.Instance);
            return true;
        }

        private static void ApplySarahTurnInXpCredits(ICharacter source)
        {
            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                DeliverDnaLockedArmorQuestId,
                "arete-credits-awarded-sarah-armor-turnin",
                SarahTurnInCreditReward,
                "arete-xp-awarded-sarah-armor-turnin",
                SarahTurnInXpReward,
                "sarah-armor-turnin-2229xp");
        }

        private static void TrySendSarahTurnInRewardFeedback(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = SarahTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void TryGrantSarahTurnInRewardItem(ICharacter source)
        {
            if (!ItemLoader.ItemList.ContainsKey(SarahTurnInRewardItemId))
            {
                Log("turnin reward missing ItemLoader id=" + SarahTurnInRewardItemId);
                return;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    SarahTurnInRewardItemId))
            {
                // Capture still emitted TemplateAction; unique-item feedback arrived separately.
                SendOverflowGrantPackets(source, SarahTurnInRewardItemId, 1);
                return;
            }

            Item reward;
            try
            {
                reward = new Item(1, SarahTurnInRewardItemId, SarahTurnInRewardItemId);
            }
            catch (Exception ex)
            {
                Log("turnin reward create failed: " + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, reward);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("turnin reward grant failed status=" + grant.Status);
                return;
            }

            SendOverflowGrantPackets(source, SarahTurnInRewardItemId, 1);
        }

        private static void SendOverflowGrantPackets(ICharacter source, int itemId, int quality)
        {
            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = quality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedTemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            source.Send(
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
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "SarahGreeneQuestRuntime " + message);
        }
    }
}
