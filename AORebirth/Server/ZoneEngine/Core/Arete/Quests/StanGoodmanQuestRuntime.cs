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
    /// Capture 20260720-goldman: Stan Goodman dialogue → accept job →
    /// Action59+Delete Talk to Stan (555B4366) + QFU Buy a Lockpick (555BD124).
    /// Capture 20260721-lockpick: Use sealed 295999 → TemplateAction 95577 (Unknown2=87) +
    /// consume sealed + Action59+Delete Buy Lockpick + QFU Strongbox (555BE9C5).
    /// Capture 20260721-afgter dog lockpick goodman: UseItemOnItem Lock Pick on
    /// Terminal:574187CE → grant 248306 + tip Deliver to Stan (555BE9F2);
    /// Stan deliver dialogue → StartTrade → FinishTrade → Talk to Sarah + Buy Nano Programs.
    /// </summary>
    public static class StanGoodmanQuestRuntime
    {
        public const string AcceptJobNodeId = "stan_goldman_003";

        public const string DeliverOfferNodeId = "stan_deliver_001";

        public const string DeliverTradeHoldNodeId = "stan_deliver_trade";

        public const string TalkToStanQuestId = "Mission:555B4366";

        public const string BuyLockpickQuestId = "Mission:555BD124";

        public const string StrongboxQuestId = "Mission:555BE9C5";

        public const string DeliverAntonioFactoryQuestId = "Mission:555BE9F2";

        public const string TalkToSarahGreeneQuestId = "Mission:555BE9F3";

        public const string BuyNanoProgramsQuestId = "Mission:555BE9F4";

        private const int AreteLandingPlayfieldId = 6553;

        private const int StanGoodmanInstance = unchecked((int)0x78E0FC65);

        private const int SealedLockpickItemId = 295999;

        private const int LockPickItemId = 95577;

        private const int MerchantsStrongboxTemplateId = 295604;

        private const int MerchantsStrongboxInstance = unchecked((int)0x574187CE);

        private const int AntoniosAdaptionFactoryItemId = 248306;

        // Capture 20260721-afgter dog lockpick goodman TemplateAction after Stan Accept.
        private const int StanTurnInRewardItemId = 296572;

        private const int StanTurnInXpReward = 2596;

        private const int StanTurnInCreditReward = 1240;

        // Capture 20260730-212921: Use Doctor/Bureaucrat crystal →
        // "Received reward: 2581 XP, 1160 credits." + Overflow 223373 QL25.
        private const int BuyNanoTipXpReward = 2581;

        private const int BuyNanoTipCreditReward = 1160;

        private const int BuyNanoProgramsTipInstance = unchecked((int)0x555BE9F4);

        private const string LegacyBuyNanoTipRewardsGrantedFlag = "buy-nano-tip-rewards-granted";

        // The legacy flag was written before stats and inventory grants. The v2 marker is
        // written only after every durable reward step succeeds.
        private const string BuyNanoTipRewardsGrantedFlag = "buy-nano-tip-rewards-v2-granted";

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const int CapturedTradeSlotCount = 4;

        // Capture 20260721-afgter dog lockpick goodman FormatFeedback (Adaption spelling on wire).
        private const string LockpickSuccessFeedback =
            "~&!!!\":!!!)<sOYou successfully picked this lock and obtained the Antonio's Adaption Factory.";

        // Capture wire FormattedMessage for XP/credits after factory turn-in.
        private const string StanTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!?Oi!!!/S~";

        // Capture 20260730-212921 FormatFeedback wire (decodes to Received reward: 2581 XP, 1160 credits.).
        private const string BuyNanoTipRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!.X~";

        private const string StanTradePrompt =
            "Drag and drop the item(s) you want to give to Stanley Goodman into one of the slots available and press \"accept\"";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, StanTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, StanTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private sealed class StanTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static string ResolveStanStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (IsDeliverTipActive(source) || HasAntonioFactory(source))
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

            if (!string.Equals(previousNodeId, AcceptJobNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Never re-offer Buy Lockpick once deliver/factory chain is active.
            if (IsDeliverTipActive(source)
                || HasAntonioFactory(source)
                || IsMissionActiveOrCompleted(source, TalkToSarahGreeneQuestId)
                || IsMissionActiveOrCompleted(source, BuyNanoProgramsQuestId)
                || IsMissionActiveOrCompleted(source, DeliverAntonioFactoryQuestId)
                || IsMissionActiveOrCompleted(source, StrongboxQuestId)
                || IsMissionActiveOrCompleted(source, BuyLockpickQuestId))
            {
                Log("accept-job ignored — lockpick chain already progressed");
                return true;
            }

            CompleteTalkToStanAndOfferBuyLockpick(source);
            return true;
        }

        public static bool TryBeginStanTrade(ICharacter source, Identity stanIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (!IsDeliverTipActive(source) && !HasAntonioFactory(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            if (stanIdentity.Type != IdentityType.CanbeAffected || stanIdentity.Instance == 0)
            {
                stanIdentity = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = StanGoodmanInstance
                               };
            }

            BeginStanTrade(source, stanIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                stanIdentity,
                StanTradePrompt,
                CapturedTradeSlotCount);
            Log(
                "stan-trade-opened character="
                + source.Identity.ToString(true)
                + " hasFactory="
                + HasAntonioFactory(source));
            return true;
        }

        public static bool TryStageStanTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null || !IsStanNpc(source, message.Target))
            {
                return false;
            }

            if (!HasAntonioFactory(source) && !IsDeliverTipActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            EnsureStanTradeSession(source, message.Target);
            StanTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance > 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "stan-trade-staged character="
                    + source.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true));
            }

            return true;
        }

        public static bool ShouldSuppressGenericStanTradeRemove(ICharacter source, Identity target)
        {
            if (source == null || !IsStanNpc(source, target))
            {
                return false;
            }

            return HasAntonioFactory(source) || IsDeliverTipActive(source) || GetTradeSession(source) != null;
        }

        public static bool TryFinishStanTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            // Only claim Stan's own FinishTrade — never steal Accept from Sarah/Bill/Alex/etc.
            if (!IsStanNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            StanTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                EnsureStanTradeSession(source, message.Target);
                session = GetTradeSession(source);
            }

            Identity stagedContainer = session != null ? session.StagedContainer : Identity.None;
            ApplyStanTradeTurnIn(source, message.Target, stagedContainer);
            return true;
        }

        /// <summary>
        /// Capture 20260730-212921: tip rewards land after opening the Nanoprogram Container,
        /// not at the vendor purchase click. Keep this no-op so buy does not race with Use.
        /// </summary>
        public static bool TryCompleteBuyNanoTipOnVendorPurchase(ICharacter character, IItem[] boughtItems)
        {
            return false;
        }

        /// <summary>
        /// Capture 20260730-212921: Use purchased Nanoprogram Container → tip rewards + Delete.
        /// Side-effect only; returns false so normal crystal Use continues after package unpack.
        /// </summary>
        public static bool TryCompleteBuyNanoTipOnCrystalUse(ICharacter character, Item item)
        {
            if (character == null || item == null)
            {
                return false;
            }

            if (!CapturedAreteMarcoSpidaVendorContentProvider.IsCapturedNanoCrystalItemId(item.LowID)
                && !CapturedAreteMarcoSpidaVendorContentProvider.IsCapturedNanoCrystalItemId(item.HighID))
            {
                return false;
            }

            TryCompleteBuyNanoTip(character, "crystal-use item=" + item.LowID + "/" + item.HighID);
            return false;
        }

        private static bool TryCompleteBuyNanoTip(ICharacter character, string reason)
        {
            if (character == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = character.Identity.Instance;

            // Fully rewarded already — tip delete only (no repeat).
            if (HasBuyNanoTipRewardsGranted(character))
            {
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, BuyNanoProgramsTipInstance);
                ForceCompleteHandoffTip(
                    characterId,
                    BuyNanoProgramsQuestId,
                    "buy_nano_programs");
                Log(
                    "buy-nano tip already rewarded — tip delete only ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
                return false;
            }

            // Heal path: earlier builds could ForceComplete/Delete the tip without granting
            // XP/credits/223373. Mission may be Completed while rewards flag is missing.
            bool missionActive = IsMissionLifecycle(character, BuyNanoProgramsQuestId, true, false);
            bool missionCompleted = IsMissionLifecycle(character, BuyNanoProgramsQuestId, false, true);
            if (!missionActive && !missionCompleted)
            {
                if (!TryEnsureBuyNanoMissionActive(character))
                {
                    Log(
                        "buy-nano tip complete skipped (mission not active) ("
                        + reason
                        + ") character="
                        + character.Identity.ToString(true));
                    return false;
                }
            }

            if (missionCompleted)
            {
                Log(
                    "buy-nano tip heal — mission was completed without rewards ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
            }

            // The regressed build wrote its legacy marker before unjournaled stat and item grants.
            // Do not replay those stats automatically; finish the retry-safe item/mission handoff
            // and migrate to the corrected marker without guessing character history.
            if (HasLegacyBuyNanoTipRewardsGranted(character))
            {
                if (!TryGrantBuyNanoTipReward(character))
                {
                    return false;
                }

                MissionOperationResult migratedMarker = MarkBuyNanoTipRewardsGranted(character);
                if (IsMissionMutationFailure(migratedMarker))
                {
                    return false;
                }

                ForceCompleteHandoffTip(
                    characterId,
                    BuyNanoProgramsQuestId,
                    "buy_nano_programs");
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, BuyNanoProgramsTipInstance);
                Log(
                    "buy-nano tip legacy marker migrated without replaying unjournaled stats ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
                return false;
            }

            // Capture 20260730-212921 order after package unpack:
            // FormatFeedback XP/credits → Overflow 223373 → Quest Delete.
            MissionRewardExecutionResult statsResult = ApplyBuyNanoTipXpCredits(character);
            if (statsResult == null || !statsResult.Succeeded)
            {
                Log(
                    "buy-nano tip stats remain retryable ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
                return false;
            }

            TrySendBuyNanoTipRewardFeedback(character);
            if (!TryGrantBuyNanoTipReward(character))
            {
                Log(
                    "buy-nano tip item reward remains retryable ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
                return false;
            }

            MissionOperationResult completionMarker = MarkBuyNanoTipRewardsGranted(character);
            if (IsMissionMutationFailure(completionMarker))
            {
                Log(
                    "buy-nano tip completion marker remains retryable ("
                    + reason
                    + ") character="
                    + character.Identity.ToString(true));
                return false;
            }

            ForceCompleteHandoffTip(
                characterId,
                BuyNanoProgramsQuestId,
                "buy_nano_programs");
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(character, BuyNanoProgramsTipInstance);

            Log(
                "buy-nano tip complete ("
                + reason
                + ") character="
                + character.Identity.ToString(true)
                + " reward=223373 ql25 xp="
                + BuyNanoTipXpReward
                + " credits="
                + BuyNanoTipCreditReward);
            return true;
        }

        private static bool TryEnsureBuyNanoMissionActive(ICharacter character)
        {
            if (character == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Tip may be visible from QFU while MissionRuntime missed Accept (login edge).
            // Auto-accept when Stan factory tip is past Offer, or Buy Nano already Offered.
            if (!IsMissionActiveOrCompleted(character, DeliverAntonioFactoryQuestId)
                && !IsMissionActiveOrCompleted(character, TalkToSarahGreeneQuestId)
                && !IsMissionActiveOrCompleted(character, BuyNanoProgramsQuestId)
                && !IsMissionActiveOrCompleted(character, StrongboxQuestId)
                && !IsMissionActiveOrCompleted(character, BuyLockpickQuestId))
            {
                return false;
            }

            int characterId = character.Identity.Instance;
            MissionRuntime.Service.OfferMission(characterId, BuyNanoProgramsQuestId);
            MissionRuntime.Service.AcceptMission(characterId, BuyNanoProgramsQuestId);
            return IsMissionLifecycle(character, BuyNanoProgramsQuestId, true, false)
                   || IsMissionLifecycle(character, BuyNanoProgramsQuestId, false, true);
        }

        private static bool HasBuyNanoTipRewardsGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       BuyNanoProgramsQuestId,
                       BuyNanoTipRewardsGrantedFlag) != null;
        }

        private static bool HasLegacyBuyNanoTipRewardsGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       BuyNanoProgramsQuestId,
                       LegacyBuyNanoTipRewardsGrantedFlag) != null;
        }

        private static MissionOperationResult MarkBuyNanoTipRewardsGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return null;
            }

            return MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                BuyNanoProgramsQuestId,
                BuyNanoTipRewardsGrantedFlag,
                "1");
        }

        private static bool IsMissionMutationFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        /// <summary>
        /// Login heal: Talk to Stan stayed Active because completes skipped ObserveObjective.
        /// Prefer later Stan-chain tips; clear Talk to Stan ghost when past it.
        /// </summary>
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
            bool pastTalkStan =
                IsMissionLifecycle(source, BuyLockpickQuestId, true, true)
                || IsMissionLifecycle(source, StrongboxQuestId, true, true)
                || IsMissionLifecycle(source, DeliverAntonioFactoryQuestId, true, true)
                || IsMissionLifecycle(source, TalkToSarahGreeneQuestId, true, true)
                || IsMissionLifecycle(source, BuyNanoProgramsQuestId, true, true);

            if (pastTalkStan || IsMissionLifecycle(source, TalkToStanQuestId, true, true))
            {
                if (pastTalkStan)
                {
                    ForceCompleteHandoffTip(
                        characterId,
                        TalkToStanQuestId,
                        "mission_555B4366_talk_to_stan");
                    FlintKneecappingTipWire.TryDeleteTip(source, unchecked((int)0x555B4366));
                }
            }

            // Completed Buy Nano with rewards granted — tip must stay deleted (no repeat).
            // Do NOT ForceComplete-only when rewards flag is missing: that locked characters
            // out of XP/credits/223373. Missing rewards are healed on next buy/open.
            if (HasBuyNanoTipRewardsGranted(source))
            {
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, BuyNanoProgramsTipInstance);
                ForceCompleteHandoffTip(characterId, BuyNanoProgramsQuestId, "buy_nano_programs");
            }
            else if (IsMissionLifecycle(source, BuyNanoProgramsQuestId, false, true))
            {
                // Tip deleted/completed without rewards — keep tip deleted in journal UI but
                // leave heal for next Marco buy/open (do not re-offer tip).
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(source, BuyNanoProgramsTipInstance);
            }

            if (IsMissionLifecycle(source, BuyNanoProgramsQuestId, true, false)
                && !HasBuyNanoTipRewardsGranted(source))
            {
                SafeQuestFullUpdateSender.SendBuyNanoProgramsTipForLogin(source);
                return true;
            }

            if (IsMissionLifecycle(source, DeliverAntonioFactoryQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendStrongboxToDeliverFactoryHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, StrongboxQuestId, true, false))
            {
                // Always clear stuck Buy Lockpick tip when Strongbox is the live tip.
                SafeQuestFullUpdateSender.TrySendBuyLockpickToStrongboxHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, BuyLockpickQuestId, true, false))
            {
                // Past Buy Lockpick in wire tip but MissionRuntime still Active — delete, don't re-offer.
                if (IsMissionLifecycle(source, StrongboxQuestId, false, true)
                    || IsMissionLifecycle(source, DeliverAntonioFactoryQuestId, true, true))
                {
                    SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                        source,
                        unchecked((int)0x555BD124));
                    return true;
                }

                SafeQuestFullUpdateSender.TrySendTalkStanToBuyLockpickHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, TalkToStanQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendReportAlexToTalkStanHandoff(source);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Capture 20260721-lockpick @22:45:47: Use Inventory sealed package → Lock Pick + tip handoff.
        /// </summary>
        public static bool TryHandleSealedLockpickUse(
            ICharacter character,
            Identity itemPosition,
            Item item)
        {
            if (character == null
                || item == null
                || (item.LowID != SealedLockpickItemId && item.HighID != SealedLockpickItemId))
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(LockPickItemId))
            {
                Log("sealed-use skipped: ItemLoader missing Lock Pick id=" + LockPickItemId);
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(character)
                || character.Controller == null
                || character.Controller.Client == null)
            {
                Log("sealed-use skipped: inventory/client missing");
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LockPickItemId))
            {
                Item lockPick;
                try
                {
                    lockPick = new Item(1, LockPickItemId, LockPickItemId);
                }
                catch (Exception ex)
                {
                    Log("Lock Pick create failed: " + ex.Message);
                    return false;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, lockPick);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    Log(
                        "Lock Pick grant failed status="
                        + grant.Status
                        + " invErr="
                        + grant.InventoryError);
                    return false;
                }

                SendOverflowGrantPackets(character, LockPickItemId, 1);
            }

            // Capture: TemplateAction sealed Unknown2=3 at placement, then DeleteItem.
            TemplateActionMessageHandler.Default.Send(
                character,
                item,
                (int)itemPosition.Type,
                itemPosition.Instance);
            character.BaseInventory.RemoveItem((int)itemPosition.Type, itemPosition.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(
                character,
                (int)itemPosition.Type,
                itemPosition.Instance);

            CompleteBuyLockpickAndOfferStrongbox(character);
            Log(
                "sealed-lockpick-opened→lockpick+strongbox character="
                + character.Identity.ToString(true)
                + " slot="
                + itemPosition);
            return true;
        }

        /// <summary>
        /// Capture 20260721-afgter dog lockpick goodman @23:04:25:
        /// UseItemOnItem Lock Pick (Inventory) on Merchant's Strongbox (Terminal:574187CE).
        /// </summary>
        public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action)
                != UseItemOnItemInteractionRouteMode.UseItemOnItem)
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

            Identity itemIdentity = message.Target[0];
            Identity strongboxIdentity = message.Target[1];
            if (strongboxIdentity.Type != IdentityType.Terminal)
            {
                return false;
            }

            IItem lockPick = ResolveInventoryItem(character, itemIdentity);
            if (lockPick == null
                || (lockPick.LowID != LockPickItemId && lockPick.HighID != LockPickItemId))
            {
                return false;
            }

            if (!IsMerchantsStrongbox(character, strongboxIdentity))
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(AntoniosAdaptionFactoryItemId))
            {
                Log("strongbox-pick skipped: ItemLoader missing factory id=" + AntoniosAdaptionFactoryItemId);
                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    AntoniosAdaptionFactoryItemId))
            {
                Item factory;
                try
                {
                    factory = new Item(1, AntoniosAdaptionFactoryItemId, AntoniosAdaptionFactoryItemId);
                }
                catch (Exception ex)
                {
                    Log("factory create failed: " + ex.Message);
                    return true;
                }

                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, factory);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    Log(
                        "factory grant failed status="
                        + grant.Status
                        + " invErr="
                        + grant.InventoryError);
                }
                else
                {
                    SendOverflowGrantPackets(character, AntoniosAdaptionFactoryItemId, 1);
                }
            }

            try
            {
                character.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = LockpickSuccessFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception ex)
            {
                Log("lockpick feedback failed: " + ex.Message);
            }

            CompleteStrongboxAndOfferDeliverFactory(character);
            Log(
                "strongbox-picked character="
                + character.Identity.ToString(true)
                + " target="
                + strongboxIdentity.ToString(true));
            return true;
        }

        private static bool IsMerchantsStrongbox(ICharacter character, Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == MerchantsStrongboxInstance)
            {
                return true;
            }

            StaticDynel dynel = Pool.Instance.GetObject<StaticDynel>(character.Playfield.Identity, target);
            if (dynel == null)
            {
                return false;
            }

            if (dynel.Template != null && dynel.Template.ID == MerchantsStrongboxTemplateId)
            {
                return true;
            }

            int template;
            if (dynel.Stats != null
                && (dynel.Stats.TryGetValue((int)StatIds.acgitemtemplateid, out template)
                    || dynel.Stats.TryGetValue((int)StatIds.staticinstance, out template)))
            {
                return template == MerchantsStrongboxTemplateId;
            }

            return false;
        }

        private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
        {
            IInventoryPage sourcePage;
            if (character.BaseInventory != null
                && character.BaseInventory.Pages.TryGetValue((int)itemIdentity.Type, out sourcePage)
                && sourcePage != null)
            {
                return sourcePage[itemIdentity.Instance];
            }

            return null;
        }

        private static void CompleteTalkToStanAndOfferBuyLockpick(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    TalkToStanQuestId,
                    "mission_555B4366_talk_to_stan");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    TalkToStanQuestId,
                    BuyLockpickQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, BuyLockpickQuestId);
                    MissionRuntime.Service.AcceptMission(instance, BuyLockpickQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendTalkStanToBuyLockpickHandoff(source);
            Log("talk-stan-complete→buy-lockpick character=" + source.Identity.ToString(true));
        }

        private static void CompleteBuyLockpickAndOfferStrongbox(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    BuyLockpickQuestId,
                    "mission_555BD124_buy_lockpick");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    BuyLockpickQuestId,
                    StrongboxQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, StrongboxQuestId);
                    MissionRuntime.Service.AcceptMission(instance, StrongboxQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendBuyLockpickToStrongboxHandoff(source);
        }

        private static void CompleteStrongboxAndOfferDeliverFactory(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    StrongboxQuestId,
                    "mission_555BE9C5_strongbox");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    StrongboxQuestId,
                    DeliverAntonioFactoryQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, DeliverAntonioFactoryQuestId);
                    MissionRuntime.Service.AcceptMission(instance, DeliverAntonioFactoryQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendStrongboxToDeliverFactoryHandoff(source);
        }

        private static void ApplyStanTradeTurnIn(ICharacter source, Identity stanTarget, Identity stagedContainer)
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
                if (!TryConsumeInventoryItem(source, stagedContainer, AntoniosAdaptionFactoryItemId))
                {
                    Log(
                        "stan-turnin ABORTED — factory not consumed character="
                        + source.Identity.ToString(true)
                        + " staged="
                        + stagedContainer.ToString(true)
                        + " hasItem="
                        + HasAntonioFactory(source));
                    Identity reopenTarget = stanTarget;
                    if (reopenTarget.Type != IdentityType.CanbeAffected || reopenTarget.Instance == 0)
                    {
                        reopenTarget = new Identity
                                       {
                                           Type = IdentityType.CanbeAffected,
                                           Instance = StanGoodmanInstance
                                       };
                    }

                    BeginStanTrade(source, reopenTarget);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        reopenTarget,
                        StanTradePrompt,
                        CapturedTradeSlotCount);
                }
                else
                {
                    try
                    {
                        KnuBotRejectedItemsMessageHandler.Default.Send(source, stanTarget, new Item[0], 0);
                    }
                    catch (Exception ex)
                    {
                        Log("stan-rejecteditems failed: " + ex.Message);
                    }

                    // Capture 20260801-102913 system-messages #996-1001:
                    // FormatFeedback → Cash → XP → Overflow grant → Feedback 110.
                    TrySendStanTurnInRewardFeedback(source);
                    ApplyStanTurnInXpCredits(source);
                    TryGrantStanTurnInRewardItem(source);
                    try
                    {
                        FeedbackMessageHandler.Default.Send(source, 110, 108871108);
                    }
                    catch (Exception ex)
                    {
                        Log("stan-item-feedback failed: " + ex.Message);
                    }

                    CompleteDeliverFactoryAndOfferNextTips(source);
                    ForgetTradeSession(source);
                    try
                    {
                        ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, stanTarget);
                    }
                    catch (Exception ex)
                    {
                        Log("stan-resume-dialogue failed: " + ex.Message);
                    }

                    Log("stan-turnin done character=" + source.Identity.ToString(true));
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

        private static void CompleteDeliverFactoryAndOfferNextTips(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    DeliverAntonioFactoryQuestId,
                    "mission_555BE9F2_deliver_factory");
                MissionRuntime.Service.OfferMission(instance, TalkToSarahGreeneQuestId);
                MissionRuntime.Service.AcceptMission(instance, TalkToSarahGreeneQuestId);
                MissionRuntime.Service.OfferMission(instance, BuyNanoProgramsQuestId);
                MissionRuntime.Service.AcceptMission(instance, BuyNanoProgramsQuestId);
            }

            SafeQuestFullUpdateSender.TrySendDeliverFactoryToSarahAndNanoTipsHandoff(source);
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
                        ObservationKey = "stan-goodman-force-complete",
                        Amount = 1,
                        EventType = "StanGoodmanQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        /// <summary>
        /// Capture 20260730-212921: Overflow TemplateAction 223373 QL25
        /// (Nano Crystal Composite Attribute Boost) after XP/credits feedback.
        /// </summary>
        private static bool TryGrantBuyNanoTipReward(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            int rewardId = CapturedAreteMarcoSpidaVendorContentProvider.BuyNanoTipRewardItemId;
            int rewardQl = CapturedAreteMarcoSpidaVendorContentProvider.BuyNanoTipRewardQuality;

            if (!ItemLoader.ItemList.ContainsKey(rewardId))
            {
                Log(
                    "buy-nano tip reward ItemLoader missing id="
                    + rewardId
                    + " (Composite Attribute Boost)");
                return false;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    rewardId))
            {
                SendOverflowGrantPackets(character, rewardId, rewardQl);
                Log("buy-nano tip reward already carried id=" + rewardId);
                return true;
            }

            Item reward;
            try
            {
                reward = new Item(rewardQl, rewardId, rewardId);
            }
            catch (Exception ex)
            {
                Log("buy-nano tip reward create failed: " + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, reward);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "buy-nano tip reward inventory grant status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError
                    + " id="
                    + rewardId
                    + " (retry retained)");
                return false;
            }

            // Capture order: Overflow TemplateAction + ContainerAddItem for 223373 QL25.
            // Do not also AddTemplate — that duplicates the crystal in inventory (2× Composite Attribute Boost).
            SendOverflowGrantPackets(character, rewardId, rewardQl);

            Log("buy-nano tip reward granted id=" + rewardId + " ql=" + rewardQl);
            return true;
        }

        private static MissionRewardExecutionResult ApplyBuyNanoTipXpCredits(ICharacter source)
        {
            if (source?.Stats == null || !MissionRuntime.IsInitialized || MissionRuntime.Rewards == null)
            {
                return null;
            }

            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-buy-nano-tip-xp-credits",
                                                    RewardType = "character-stats",
                                                    IsResolved = true,
                                                    StatMutations =
                                                        new[]
                                                        {
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.cash,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BuyNanoTipCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BuyNanoTipXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = BuyNanoTipXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.lastxp,
                                                                Kind = MissionStatMutationKind.Set,
                                                                Value = BuyNanoTipXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                BuyNanoProgramsQuestId,
                definition,
                "capture:20260730-212921:buy-nano-tip-xp-credits");
            if (result == null || !result.Succeeded || result.StatValues == null)
            {
                return result;
            }

            foreach (MissionCharacterStatValue statValue in result.StatValues)
            {
                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                source.Stats[(StatIds)statValue.StatId].Set(value);
            }

            StatMessageHandler.Default.SendChanged(source);
            return result;
        }

        private static void TrySendBuyNanoTipRewardFeedback(ICharacter source)
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
                    FormattedMessage = BuyNanoTipRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void ApplyStanTurnInXpCredits(ICharacter source)
        {
            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                DeliverAntonioFactoryQuestId,
                "arete-credits-awarded-stan-factory-turnin",
                StanTurnInCreditReward,
                "arete-xp-awarded-stan-factory-turnin",
                StanTurnInXpReward,
                "stan-factory-turnin-2596xp");
        }

        private static void TrySendStanTurnInRewardFeedback(ICharacter source)
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
                    FormattedMessage = StanTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void TryGrantStanTurnInRewardItem(ICharacter source)
        {
            if (source == null || !ItemLoader.ItemList.ContainsKey(StanTurnInRewardItemId))
            {
                Log("stan reward grant skipped: missing template " + StanTurnInRewardItemId);
                return;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    StanTurnInRewardItemId))
            {
                return;
            }

            Item reward;
            try
            {
                reward = new Item(1, StanTurnInRewardItemId, StanTurnInRewardItemId);
            }
            catch (Exception ex)
            {
                Log("stan reward create failed: " + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, reward);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("stan reward grant failed status=" + grant.Status);
                return;
            }

            SendOverflowGrantPackets(source, StanTurnInRewardItemId, 1);
        }

        private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
        {
            if (source == null || source.BaseInventory == null || itemId <= 0)
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

            return TryRemoveFactoryAnywhere(source, itemId);
        }

        private static bool TryRemoveFactoryAnywhere(ICharacter source, int itemId)
        {
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
                    if (item == null || (item.LowID != itemId && item.HighID != itemId))
                    {
                        continue;
                    }

                    source.BaseInventory.RemoveItem(pageEntry.Key, slot.Key);
                    CharacterActionMessageHandler.Default.SendDeleteItem(source, pageEntry.Key, slot.Key);
                    return true;
                }
            }

            return false;
        }

        private static bool HasAntonioFactory(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       AntoniosAdaptionFactoryItemId);
        }

        private static bool IsDeliverTipActive(ICharacter source)
        {
            return IsMissionLifecycle(source, DeliverAntonioFactoryQuestId, true, false);
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

            if (includeActive
                && (mission.State == MissionLifecycleState.Active
                    || mission.State == MissionLifecycleState.Offered))
            {
                return true;
            }

            return includeCompleted && mission.State == MissionLifecycleState.Completed;
        }

        private static bool IsStanNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == StanGoodmanInstance)
            {
                return true;
            }

            if (source?.Playfield == null || target.Type != IdentityType.CanbeAffected)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null
                   && !string.IsNullOrEmpty(npc.Name)
                   && npc.Name.IndexOf("Goodman", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static StanTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                StanTradeSession session;
                return TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session)
                           ? session
                           : null;
            }
        }

        private static void BeginStanTrade(ICharacter source, Identity stanIdentity)
        {
            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new StanTradeSession
                                                                    {
                                                                        NpcIdentity = stanIdentity,
                                                                        StagedContainer = Identity.None
                                                                    };
            }
        }

        private static void EnsureStanTradeSession(ICharacter source, Identity stanIdentity)
        {
            if (GetTradeSession(source) == null)
            {
                BeginStanTrade(source, stanIdentity);
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
            LogUtil.Debug(DebugInfoDetail.Engine, "StanGoodmanQuestRuntime " + message);
        }
    }
}
