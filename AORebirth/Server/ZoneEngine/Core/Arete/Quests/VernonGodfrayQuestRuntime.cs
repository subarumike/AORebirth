namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
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
    /// Capture 20260721-Vernon-Godfray: Speak to Vernon → "... well what?" grants
    /// Omni-Tek Technical Library 248377 + Hacker Tool 87810 → tip Hacking Skills →
    /// combine → Give Hacked Library → StartTrade turn-in → Cargo Lifting.
    /// </summary>
    public static class VernonGodfrayQuestRuntime
    {
        public const string AcceptWellWhatNodeId = "vernon_002";

        public const string HackOfferNodeId = "vernon_hack_offer";

        public const string HackTradeHoldNodeId = "vernon_hack_trade";

        public const string ReturnOfferNodeId = "vernon_return_001";

        public const string ReturnTradeHoldNodeId = "vernon_return_trade";

        public const string SpeakToVernonGodfrayQuestId = "Mission:555BE9F7";

        public const string HackingSkillsQuestId = "Mission:555BE9F8";

        public const string GiveHackedTechnicalLibraryQuestId = "Mission:555BE9F9";

        public const string CargoLiftingQuestId = "Mission:555BE9FA";

        public const string ReturnToVernonGodfrayQuestId = "Mission:555BE9FB";

        public const string TalkToDoctorMasonQuestId = "Mission:555BE9FC";

        private const int VernonGodfrayInstance = unchecked((int)0x78E0FC68);

        private const int OmniTekTechnicalLibraryItemId = VernonGodfrayCombineRules.OmniTekTechnicalLibraryItemId;

        private const int HackerToolItemId = VernonGodfrayCombineRules.HackerToolItemId;

        private const int HackedTechnicalLibraryItemId = VernonGodfrayCombineRules.HackedTechnicalLibraryItemId;

        // Stan turn-in reward; Return tip asks player to give this to Vernon.
        private const int UnprogrammedIdentificationChipItemId = 296572;

        // Capture 20260721-Vernon-Godfray return Accept → Overflow 296575 Blank ICC ID Chip.
        private const int BlankIccIdChipItemId = 296575;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const int CapturedTradeSlotCount = 1;

        private const int HackTurnInXpReward = 2229;

        private const int HackTurnInCreditReward = 1320;

        private const int ReturnTurnInXpReward = 2229;

        private const int ReturnTurnInCreditReward = 1360;

        // Capture 20260721-Vernon-Godfray FormatFeedback after Hacked Library Accept.
        private const string HackTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!0N~";

        // Capture 20260721-Vernon-Godfray FormatFeedback after return chip Accept (1360 credits).
        private const string ReturnTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!1!~";

        private const string VernonTradePrompt =
            "Drag and drop the item(s) you want to give to Vernon Godfray into one of the slots available and press \"accept\"";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, VernonTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, VernonTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private enum VernonTradeKind
        {
            HackInspect = 0,
            ReturnChip = 1
        }

        private sealed class VernonTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;

            public VernonTradeKind Kind;
        }

        public static string ResolveVernonStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            // Return visit after Shipping Manifest Terminal.
            if (IsMissionLifecycle(source, ReturnToVernonGodfrayQuestId, true, false))
            {
                return ReturnOfferNodeId;
            }

            // Cargo Lifting / Give already done: never reopen hack-offer (Overflow spam).
            // If Cargo tip is Active but library was lost on a prior Accept, restore it.
            if (IsMissionLifecycle(source, CargoLiftingQuestId, true, true)
                || IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, false, true))
            {
                if (IsMissionLifecycle(source, CargoLiftingQuestId, true, false))
                {
                    TryEnsureHackedTechnicalLibrary(source);
                }

                return null;
            }

            // Give Hacked Library tip, or already holding 295756 before tip mission synced.
            if (IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, true, false)
                || HasHackedTechnicalLibrary(source))
            {
                return HackOfferNodeId;
            }

            if (IsMissionLifecycle(source, HackingSkillsQuestId, true, false)
                || HasHackedLibraryMaterials(source))
            {
                return "vernon_003";
            }

            return null;
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: combine → Delete Hacking Skills + QFU Give Hacked Library.
        /// </summary>
        public static void OnCombineSucceeded(ICharacter source, int resultLowId, int resultHighId)
        {
            if (source == null
                || !VernonGodfrayCombineRules.IsHackedTechnicalLibrary(resultLowId, resultHighId))
            {
                return;
            }

            CompleteHackingSkillsAndOfferDeliver(source);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray #288–#290: FormatFeedback + TemplateAction Overflow
        /// + ContainerAddItem. AddTemplate crashes the client for Hacked Technical Library.
        /// </summary>
        public static void SendCombineResultClientPackets(
            ICharacter source,
            Item sourceItem,
            Item targetItem,
            Item resultItem)
        {
            if (source?.Controller?.Client == null || resultItem == null)
            {
                return;
            }

            string feedback = string.Format(
                "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".",
                ResolveItemName(sourceItem),
                ResolveItemName(targetItem),
                resultItem.Quality,
                ResolveItemName(resultItem));

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = feedback,
                    Unknown2 = 0
                });

            SendOverflowGrantPackets(source, resultItem.LowID > 0 ? resultItem.LowID : resultItem.HighID, resultItem.Quality);
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: "I was able to hack the device." → StartTrade (1 slot).
        /// </summary>
        public static bool TryBeginVernonHackTrade(ICharacter source, Identity vernonIdentity)
        {
            if (source == null)
            {
                return false;
            }

            // Past Give-Library stage: never reopen hack trade (Cargo / completed give).
            if (IsMissionLifecycle(source, CargoLiftingQuestId, true, true)
                || IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, false, true)
                || IsMissionLifecycle(source, ReturnToVernonGodfrayQuestId, true, true))
            {
                return false;
            }

            // Sarah pattern: tip Active OR item in inventory (client tip can exist while
            // MissionRuntime lag). Without this, StartTrade fails → trade-hold node has only
            // "(Continue after trade)" → router CloseChatWindow.
            if (!IsGiveLibraryTipActive(source)
                && !HasHackedTechnicalLibrary(source)
                && GetTradeSession(source) == null)
            {
                return false;
            }

            if (vernonIdentity.Type != IdentityType.CanbeAffected || vernonIdentity.Instance == 0)
            {
                vernonIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = VernonGodfrayInstance
                                };
            }

            BeginVernonTrade(source, vernonIdentity, VernonTradeKind.HackInspect);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                vernonIdentity,
                VernonTradePrompt,
                CapturedTradeSlotCount);
            Log("vernon-start-trade character=" + source.Identity.ToString(true));
            return true;
        }

        /// <summary>
        /// Capture 20260721-Vernon-Godfray: "I've returned. The terminal was hacked." → StartTrade.
        /// </summary>
        public static bool TryBeginVernonReturnTrade(ICharacter source, Identity vernonIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (!IsReturnTipActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            if (vernonIdentity.Type != IdentityType.CanbeAffected || vernonIdentity.Instance == 0)
            {
                vernonIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = VernonGodfrayInstance
                                };
            }

            TryEnsureUnprogrammedChip(source);
            BeginVernonTrade(source, vernonIdentity, VernonTradeKind.ReturnChip);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                vernonIdentity,
                VernonTradePrompt,
                CapturedTradeSlotCount);
            Log("vernon-return-start-trade character=" + source.Identity.ToString(true));
            return true;
        }

        public static bool TryStageVernonTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsVernonNpc(character, message.Target))
            {
                return false;
            }

            if (!IsGiveLibraryTipActive(character)
                && !HasHackedTechnicalLibrary(character)
                && !IsReturnTipActive(character)
                && GetTradeSession(character) == null)
            {
                return false;
            }

            if (!IsReturnTipActive(character)
                && (IsMissionLifecycle(character, CargoLiftingQuestId, true, true)
                    || IsMissionLifecycle(character, GiveHackedTechnicalLibraryQuestId, false, true)))
            {
                return false;
            }

            VernonTradeKind kind = IsReturnTipActive(character)
                                       ? VernonTradeKind.ReturnChip
                                       : VernonTradeKind.HackInspect;
            VernonTradeSession existing = GetTradeSession(character);
            if (existing != null)
            {
                kind = existing.Kind;
            }

            BeginVernonTrade(character, message.Target, kind);
            VernonTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "vernon-trade-staged character="
                    + character.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true)
                    + " kind="
                    + kind);
            }

            return true;
        }

        public static bool ShouldSuppressGenericVernonTradeRemove(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsVernonNpc(character, message.Target))
            {
                return false;
            }

            if (IsReturnTipActive(character) || GetTradeSession(character) != null)
            {
                return true;
            }

            return (IsGiveLibraryTipActive(character) || HasHackedTechnicalLibrary(character))
                   && !IsMissionLifecycle(character, CargoLiftingQuestId, true, true)
                   && !IsMissionLifecycle(character, GiveHackedTechnicalLibraryQuestId, false, true);
        }

        public static bool TryFinishVernonTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsVernonNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            VernonTradeSession session = GetTradeSession(source);
            bool returnTrade = IsReturnTipActive(source)
                               || (session != null && session.Kind == VernonTradeKind.ReturnChip);
            if (returnTrade)
            {
                Identity stagedReturn = session != null ? session.StagedContainer : Identity.None;
                ApplyVernonReturnTradeTurnIn(source, message.Target, stagedReturn);
                return true;
            }

            if (IsMissionLifecycle(source, CargoLiftingQuestId, true, true)
                || IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, false, true))
            {
                return false;
            }

            if (!IsGiveLibraryTipActive(source)
                && !HasHackedTechnicalLibrary(source)
                && session == null)
            {
                return false;
            }

            Identity staged = session != null ? session.StagedContainer : Identity.None;
            if (staged.Type == IdentityType.None || staged.Instance < 0)
            {
                if (!TryFindItemContainer(source, HackedTechnicalLibraryItemId, out staged))
                {
                    Log("vernon-finish without staged library — reopen trade");
                    BeginVernonTrade(source, message.Target, VernonTradeKind.HackInspect);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        message.Target,
                        VernonTradePrompt,
                        CapturedTradeSlotCount);
                    return true;
                }
            }

            ApplyVernonHackTradeTurnIn(source, message.Target, staged);
            return true;
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            if (!string.Equals(previousNodeId, AcceptWellWhatNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsMissionLifecycle(source, HackingSkillsQuestId, true, true)
                || HasStarterTools(source))
            {
                Log("well-what ignored — vernon tools/tip already progressed");
                return true;
            }

            TryGrantStarterTools(source);
            CompleteSpeakAndOfferHackingSkills(source);
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

            if (IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, true, false))
            {
                // Earlier combine crash could leave tip Active without item 295756.
                TryEnsureHackedTechnicalLibrary(source);
                // Login: tip-only (no Action59 delete). Capture wire QFU — DTO path crashed client.
                SafeQuestFullUpdateSender.TrySendGiveHackedTechnicalLibraryTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, CargoLiftingQuestId, true, false))
            {
                // Capture: inspect never consumes the library — do not Overflow-refresh on login
                // (TemplateAction spam looks like a new Hacked Library on every reconnect/click).
                TryEnsureHackedTechnicalLibrary(source);
                SafeQuestFullUpdateSender.TrySendCargoLiftingTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, ReturnToVernonGodfrayQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendReturnToVernonGodfrayTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, TalkToDoctorMasonQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendTalkToDoctorMasonTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, HackingSkillsQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendSpeakVernonToHackingSkillsHandoff(source);
                return true;
            }

            if (IsMissionLifecycle(source, SpeakToVernonGodfrayQuestId, true, false))
            {
                SafeQuestFullUpdateSender.TrySendDeliverArmorToVernonHandoff(source);
                return true;
            }

            return false;
        }

        private static void CompleteSpeakAndOfferHackingSkills(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    SpeakToVernonGodfrayQuestId,
                    "mission_555BE9F7_speak_vernon");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    SpeakToVernonGodfrayQuestId,
                    HackingSkillsQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, HackingSkillsQuestId);
                    MissionRuntime.Service.AcceptMission(instance, HackingSkillsQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendSpeakVernonToHackingSkillsHandoff(source);
            Log("speak-vernon-complete→hacking-skills character=" + source.Identity.ToString(true));
        }

        private static void CompleteHackingSkillsAndOfferDeliver(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    HackingSkillsQuestId,
                    "mission_555BE9F8_hacking_skills");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    HackingSkillsQuestId,
                    GiveHackedTechnicalLibraryQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, GiveHackedTechnicalLibraryQuestId);
                    MissionRuntime.Service.AcceptMission(instance, GiveHackedTechnicalLibraryQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendHackingSkillsToGiveLibraryHandoff(source);
            Log(
                "hacking-skills-complete→give-library character="
                + source.Identity.ToString(true));
        }

        private static void TryGrantStarterTools(ICharacter source)
        {
            TryGrantOverflowItem(source, OmniTekTechnicalLibraryItemId, 1);
            TryGrantOverflowItem(source, HackerToolItemId, 1);
        }

        private static void TryGrantOverflowItem(ICharacter source, int itemId, int quality)
        {
            if (source == null || itemId <= 0)
            {
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("grant skipped: ItemLoader missing id=" + itemId);
                return;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                return;
            }

            Item item;
            try
            {
                item = new Item(quality, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("item create failed id=" + itemId + " err=" + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "grant failed id="
                    + itemId
                    + " status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError);
                return;
            }

            SendOverflowGrantPackets(source, itemId, quality);
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

        private static bool HasStarterTools(ICharacter source)
        {
            return InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                           source,
                           OmniTekTechnicalLibraryItemId)
                       || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                           source,
                           HackerToolItemId));
        }

        private static void TryEnsureHackedTechnicalLibrary(ICharacter source)
        {
            if (source == null || HasHackedTechnicalLibrary(source))
            {
                return;
            }

            TryGrantOverflowItem(source, VernonGodfrayCombineRules.HackedTechnicalLibraryItemId, 1);
        }

        private static string ResolveItemName(Item item)
        {
            if (item == null)
            {
                return "Unknown";
            }

            if (item.HighID == VernonGodfrayCombineRules.HackerToolItemId
                || item.LowID == VernonGodfrayCombineRules.HackerToolItemId)
            {
                return "Hacker Tool";
            }

            if (item.HighID == VernonGodfrayCombineRules.OmniTekTechnicalLibraryItemId
                || item.LowID == VernonGodfrayCombineRules.OmniTekTechnicalLibraryItemId)
            {
                return "Omni-Tek Technical Library";
            }

            if (VernonGodfrayCombineRules.IsHackedTechnicalLibrary(item.LowID, item.HighID))
            {
                return "Hacked Technical Library";
            }

            try
            {
                return TradeSkill.Instance.GetItemName(item.LowID, item.HighID, item.Quality);
            }
            catch
            {
                return "Unknown";
            }
        }

        private static bool HasHackedLibraryMaterials(ICharacter source)
        {
            return HasStarterTools(source);
        }

        private static bool HasHackedTechnicalLibrary(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       HackedTechnicalLibraryItemId);
        }

        private static bool IsGiveLibraryTipActive(ICharacter source)
        {
            return IsMissionLifecycle(source, GiveHackedTechnicalLibraryQuestId, true, false);
        }

        private static bool IsReturnTipActive(ICharacter source)
        {
            return IsMissionLifecycle(source, ReturnToVernonGodfrayQuestId, true, false);
        }

        private static void BeginVernonTrade(
            ICharacter source,
            Identity vernonIdentity,
            VernonTradeKind kind)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new VernonTradeSession
                                                                    {
                                                                        NpcIdentity = vernonIdentity,
                                                                        StagedContainer = Identity.None,
                                                                        Kind = kind
                                                                    };
            }
        }

        private static VernonTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                VernonTradeSession session;
                TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session);
                return session;
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

        private static void ApplyVernonHackTradeTurnIn(
            ICharacter source,
            Identity vernonTarget,
            Identity stagedContainer)
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
                // Capture 20260721-Vernon-Godfray: inspect/keep — RejectedItems [] Unknown2=1,
                // no DeleteItem. Vernon returns the Hacked Technical Library after inspect.
                if (!HasHackedTechnicalLibrary(source)
                    && (stagedContainer.Type == IdentityType.None || stagedContainer.Instance < 0)
                    && !TryFindItemContainer(source, HackedTechnicalLibraryItemId, out stagedContainer))
                {
                    Log(
                        "vernon-hack-turnin ABORTED — no library to inspect character="
                        + source.Identity.ToString(true));
                    BeginVernonTrade(source, vernonTarget, VernonTradeKind.HackInspect);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        vernonTarget,
                        VernonTradePrompt,
                        CapturedTradeSlotCount);
                    return;
                }

                try
                {
                    // Capture 20260721-Vernon-Godfray: RejectedItems [] Unknown2=1 (no item payload).
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, vernonTarget, new Item[0], 1);
                }
                catch (Exception ex)
                {
                    Log("vernon-rejecteditems failed: " + ex.Message);
                }

                // Empty RejectedItems keeps server item, but client trade chrome still hides the
                // icon — same as PersonalizedRobotBrain: Overflow TemplateAction redraw once.
                TryForceReturnHackedTechnicalLibrary(source);
                ApplyHackTurnInXpCredits(source);
                TrySendHackTurnInRewardFeedback(source);
                CompleteGiveLibraryAndOfferCargoLifting(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, vernonTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, vernonTarget);
                    }
                }
                catch (Exception ex)
                {
                    Log("vernon-resume-dialogue failed: " + ex.Message);
                    try
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, vernonTarget);
                    }
                    catch
                    {
                    }
                }

                Log("vernon-hack-turnin done (library kept) character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        /// <summary>
        /// Inspect never consumes the library. RejectedItems [] matches capture; Overflow
        /// TemplateAction redraws the client icon after trade chrome (brain return pattern).
        /// Call only from turn-in — not on OpenChat / login (that looked like duplicate grants).
        /// </summary>
        private static void TryForceReturnHackedTechnicalLibrary(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            if (!HasHackedTechnicalLibrary(source))
            {
                TryGrantOverflowItem(source, HackedTechnicalLibraryItemId, 1);
                Log("vernon-force-return-library grant character=" + source.Identity.ToString(true));
                return;
            }

            SendOverflowGrantPackets(source, HackedTechnicalLibraryItemId, 1);
            Log("vernon-force-return-library refresh character=" + source.Identity.ToString(true));
        }

        private static void CompleteGiveLibraryAndOfferCargoLifting(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    GiveHackedTechnicalLibraryQuestId,
                    "mission_555BE9F9_give_hacked_library");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    GiveHackedTechnicalLibraryQuestId,
                    CargoLiftingQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, CargoLiftingQuestId);
                    MissionRuntime.Service.AcceptMission(instance, CargoLiftingQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendGiveLibraryToCargoLiftingHandoff(source);
        }

        private static void ApplyVernonReturnTradeTurnIn(
            ICharacter source,
            Identity vernonTarget,
            Identity stagedContainer)
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
                // Capture: consume Unprogrammed Identification Chip 296572.
                if (!TryConsumeInventoryItem(source, stagedContainer, UnprogrammedIdentificationChipItemId))
                {
                    Log("vernon-return-turnin ABORTED — no chip character=" + source.Identity.ToString(true));
                    TryEnsureUnprogrammedChip(source);
                    BeginVernonTrade(source, vernonTarget, VernonTradeKind.ReturnChip);
                    KnuBotStartTradeMessageHandler.Default.Send(
                        source,
                        vernonTarget,
                        VernonTradePrompt,
                        CapturedTradeSlotCount);
                    return;
                }

                try
                {
                    // Capture: RejectedItems [] Unknown2=0 (accepted/consumed).
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, vernonTarget, new Item[0], 0);
                }
                catch (Exception ex)
                {
                    Log("vernon-return-rejecteditems failed: " + ex.Message);
                }

                TryGrantBlankIccIdChip(source);
                ApplyReturnTurnInXpCredits(source);
                TrySendReturnTurnInRewardFeedback(source);
                CompleteReturnAndOfferDoctorMason(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, vernonTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, vernonTarget);
                    }
                }
                catch (Exception ex)
                {
                    Log("vernon-return-resume failed: " + ex.Message);
                    try
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, vernonTarget);
                    }
                    catch
                    {
                    }
                }

                Log("vernon-return-turnin done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void CompleteReturnAndOfferDoctorMason(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    ReturnToVernonGodfrayQuestId,
                    "mission_555BE9FB_return_vernon");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    ReturnToVernonGodfrayQuestId,
                    TalkToDoctorMasonQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    MissionRuntime.Service.OfferMission(instance, TalkToDoctorMasonQuestId);
                    MissionRuntime.Service.AcceptMission(instance, TalkToDoctorMasonQuestId);
                }
            }

            SafeQuestFullUpdateSender.TrySendReturnVernonToDoctorMasonHandoff(source);
        }

        private static void ApplyReturnTurnInXpCredits(ICharacter source)
        {
            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-vernon-return-chip-turnin-xp-credits",
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
                                                                Value = ReturnTurnInCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = ReturnTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = ReturnTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                ReturnToVernonGodfrayQuestId,
                definition,
                "capture:20260721-Vernon-Godfray:vernon-return-turnin-xp-credits");
            if (!result.Succeeded || result.StatValues == null)
            {
                return;
            }

            foreach (MissionCharacterStatValue statValue in result.StatValues)
            {
                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                source.Stats[(StatIds)statValue.StatId].Set(value);
            }

            StatMessageHandler.Default.SendChanged(source);
        }

        private static void TrySendReturnTurnInRewardFeedback(ICharacter source)
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
                    FormattedMessage = ReturnTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void TryGrantBlankIccIdChip(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    BlankIccIdChipItemId))
            {
                SendOverflowGrantPackets(source, BlankIccIdChipItemId, 1);
                return;
            }

            TryGrantOverflowItem(source, BlankIccIdChipItemId, 1);
        }

        private static void TryEnsureUnprogrammedChip(ICharacter source)
        {
            if (source == null
                || !InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    source,
                    UnprogrammedIdentificationChipItemId))
            {
                return;
            }

            TryGrantOverflowItem(source, UnprogrammedIdentificationChipItemId, 1);
            Log("vernon-ensure-chip grant character=" + source.Identity.ToString(true));
        }

        private static void ApplyHackTurnInXpCredits(ICharacter source)
        {
            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-vernon-hack-library-turnin-xp-credits",
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
                                                                Value = HackTurnInCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = HackTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = HackTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                GiveHackedTechnicalLibraryQuestId,
                definition,
                "capture:20260721-Vernon-Godfray:vernon-hack-turnin-xp-credits");
            if (!result.Succeeded || result.StatValues == null)
            {
                return;
            }

            foreach (MissionCharacterStatValue statValue in result.StatValues)
            {
                uint value = statValue.Value <= 0
                                 ? 0
                                 : (uint)Math.Min(statValue.Value, uint.MaxValue);
                source.Stats[(StatIds)statValue.StatId].Set(value);
            }

            StatMessageHandler.Default.SendChanged(source);
        }

        private static void TrySendHackTurnInRewardFeedback(ICharacter source)
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
                    FormattedMessage = HackTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static bool IsVernonNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == VernonGodfrayInstance)
            {
                return true;
            }

            if (source == null)
            {
                return false;
            }

            VernonTradeSession session = GetTradeSession(source);
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
                   && npc.Name.IndexOf("Vernon", StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (!TryFindItemContainer(source, itemId, out found))
            {
                return false;
            }

            source.BaseInventory.RemoveItem((int)found.Type, found.Instance);
            CharacterActionMessageHandler.Default.SendDeleteItem(source, (int)found.Type, found.Instance);
            return true;
        }

        private static bool TryFindItemContainer(ICharacter source, int itemId, out Identity container)
        {
            container = Identity.None;
            if (source == null || source.BaseInventory == null || itemId <= 0)
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
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID == itemId || item.HighID == itemId)
                    {
                        container = new Identity { Type = (IdentityType)pageEntry.Key, Instance = slot.Key };
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryFindHackedLibraryContainer(ICharacter source, out Identity container)
        {
            return TryFindItemContainer(source, HackedTechnicalLibraryItemId, out container);
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
                        ObservationKey = "vernon-godfray-force-complete",
                        Amount = 1,
                        EventType = "VernonGodfrayQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "VernonGodfrayQuestRuntime " + message);
        }
    }
}
