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
    /// Capture 20260721-Mason: Talk to Dr. Mason → Assemble Implant 1/2/3 → Show implant →
    /// Install gift implant 295706 → give Blank ICC + Biological Survey Nanobots → Lorelei tip.
    /// </summary>
    public static class DoctorMasonQuestRuntime
    {
        public const string AcceptOfferNodeId = "mason_001";

        public const string ShowOfferNodeId = "mason_show_001";

        public const string ChipOfferNodeId = "mason_chip_001";

        public const string TalkToDoctorMasonQuestId = VernonGodfrayQuestRuntime.TalkToDoctorMasonQuestId;

        public const string AssembleImplant1QuestId = "Mission:555BE9FD";

        public const string AssembleImplant2QuestId = "Mission:555BE9FE";

        public const string AssembleImplant3QuestId = "Mission:555BE9FF";

        public const string ShowDrMasonImplantQuestId = "Mission:555BEA00";

        public const string InstallTheImplantQuestId = "Mission:555BEA01";

        public const string TalkToDoctorMasonAfterInstallQuestId = "Mission:555BEA02";

        public const string TalkToLoreleiQuestId = "Mission:555BEA03";

        private const int DoctorMasonInstance = unchecked((int)0x78E0FC6C);

        // Combine result / tip handoff ids from capture.
        private const int Assemble1ResultItemId = 113127;

        private const int Assemble2ResultItemId = 113186;

        private const int Assemble3ResultItemId = 113440;

        // Capture show Accept → Overflow gift implant.
        private const int GiftLegImplantItemId = 295706;

        private const int BlankIccIdChipItemId = 296575;

        private const int BiologicalSurveyNanobotsItemId = 296574;

        // Capture final Accept → Overflow programmed chip.
        private const int ProgrammedIdChipItemId = 296576;

        private const int ChipTurnInXpReward = 2581;

        private const int ChipTurnInCreditReward = 1400;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string MasonTradePrompt =
            "Drag and drop the item(s) you want to give to Dr. Mason into one of the slots available and press \"accept\"";

        private const string ChipTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!1I~";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, MasonTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, MasonTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private enum MasonTradeKind
        {
            ShowImplant = 0,
            ChipTurnIn = 1
        }

        private sealed class MasonTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer1;

            public Identity StagedContainer2;

            public MasonTradeKind Kind;
        }

        public static string ResolveMasonStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (IsMissionLifecycle(source, TalkToLoreleiQuestId, true, true))
            {
                return null;
            }

            if (IsMissionLifecycle(source, TalkToDoctorMasonAfterInstallQuestId, true, false))
            {
                return ChipOfferNodeId;
            }

            if (IsMissionLifecycle(source, ShowDrMasonImplantQuestId, true, false)
                || IsMissionLifecycle(source, AssembleImplant3QuestId, false, true))
            {
                return ShowOfferNodeId;
            }

            if (IsMissionLifecycle(source, TalkToDoctorMasonQuestId, true, false))
            {
                return AcceptOfferNodeId;
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

            if (IsMissionLifecycle(source, AssembleImplant1QuestId, true, true)
                || IsMissionLifecycle(source, TalkToDoctorMasonQuestId, false, true))
            {
                Log("mason-accept ignored — already progressed");
                return true;
            }

            CompleteTalkMasonAndOfferAssemble1(source);
            return true;
        }

        public static void OnCombineSucceeded(ICharacter source, int resultLowId, int resultHighId)
        {
            if (source == null)
            {
                return;
            }

            if (DoctorMasonCombineRules.IsAssemble1Result(resultLowId)
                || DoctorMasonCombineRules.IsAssemble1Result(resultHighId))
            {
                if (IsMissionLifecycle(source, AssembleImplant1QuestId, true, false))
                {
                    CompleteAndActivate(
                        source,
                        AssembleImplant1QuestId,
                        AssembleImplant2QuestId,
                        "mission_555BE9FD_assemble_1");
                    DoctorMasonTipSender.TrySendAssemble1ToAssemble2Handoff(source);
                    Log("mason-assemble1-complete character=" + source.Identity.ToString(true));
                }

                return;
            }

            if (DoctorMasonCombineRules.IsAssemble2Result(resultLowId)
                || DoctorMasonCombineRules.IsAssemble2Result(resultHighId))
            {
                if (IsMissionLifecycle(source, AssembleImplant2QuestId, true, false))
                {
                    CompleteAndActivate(
                        source,
                        AssembleImplant2QuestId,
                        AssembleImplant3QuestId,
                        "mission_555BE9FE_assemble_2");
                    DoctorMasonTipSender.TrySendAssemble2ToAssemble3Handoff(source);
                    Log("mason-assemble2-complete character=" + source.Identity.ToString(true));
                }

                return;
            }

            if (DoctorMasonCombineRules.IsAssemble3Result(resultLowId)
                || DoctorMasonCombineRules.IsAssemble3Result(resultHighId))
            {
                if (IsMissionLifecycle(source, AssembleImplant3QuestId, true, false))
                {
                    CompleteAndActivate(
                        source,
                        AssembleImplant3QuestId,
                        ShowDrMasonImplantQuestId,
                        "mission_555BE9FF_assemble_3");
                    DoctorMasonTipSender.TrySendAssemble3ToShowHandoff(source);
                    Log("mason-assemble3-complete character=" + source.Identity.ToString(true));
                }
            }
        }

        /// <summary>
        /// Capture 20260721-Mason #758–#763: FormatFeedback + TemplateAction Overflow
        /// + ContainerAddItem — never AddTemplate for assemble results.
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
                ResolveCombineItemName(sourceItem),
                ResolveCombineItemName(targetItem),
                resultItem.Quality,
                ResolveCombineItemName(resultItem));

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = feedback,
                    Unknown2 = 0
                });

            int itemId = resultItem.LowID > 0 ? resultItem.LowID : resultItem.HighID;
            SendOverflowGrantPackets(source, itemId, resultItem.Quality > 0 ? resultItem.Quality : 1);
        }

        private static string ResolveCombineItemName(Item item)
        {
            if (item == null)
            {
                return "item";
            }

            string name = TradeSkill.Instance.GetItemName(item.LowID, item.HighID, item.Quality);
            return string.IsNullOrEmpty(name) ? "item" : name;
        }

        public static void OnGiftImplantEquipped(ICharacter source, int lowId, int highId)
        {
            if (source == null)
            {
                return;
            }

            if (lowId != GiftLegImplantItemId && highId != GiftLegImplantItemId)
            {
                return;
            }

            if (!IsMissionLifecycle(source, InstallTheImplantQuestId, true, false))
            {
                return;
            }

            CompleteAndActivate(
                source,
                InstallTheImplantQuestId,
                TalkToDoctorMasonAfterInstallQuestId,
                "mission_555BEA01_install_implant");
            DoctorMasonTipSender.TrySendInstallToTalkMasonHandoff(source);
            Log("mason-implant-installed character=" + source.Identity.ToString(true));
        }

        /// <summary>
        /// Capture 20260721-Mason: clinic Use → ClientMoveItemToInventory gift 295706 → ImplantPage:0x2B.
        /// Allow that gift while Install tip is active and surgery-clinic implant access is granted.
        /// </summary>
        public static bool ShouldAllowGiftImplantEquip(ICharacter source, IItem item)
        {
            if (source == null || item == null)
            {
                return false;
            }

            if (item.LowID != GiftLegImplantItemId && item.HighID != GiftLegImplantItemId)
            {
                return false;
            }

            if (!IsMissionLifecycle(source, InstallTheImplantQuestId, true, false))
            {
                return false;
            }

            Character concrete = source as Character;
            return concrete != null && concrete.HasImplantAccess();
        }

        public static bool TryBeginShowTrade(ICharacter source, Identity masonIdentity)
        {
            if (source == null || !IsMissionLifecycle(source, ShowDrMasonImplantQuestId, true, false))
            {
                return false;
            }

            return BeginTrade(source, masonIdentity, MasonTradeKind.ShowImplant, 1);
        }

        public static bool TryBeginChipTrade(ICharacter source, Identity masonIdentity)
        {
            if (source == null
                || !IsMissionLifecycle(source, TalkToDoctorMasonAfterInstallQuestId, true, false))
            {
                return false;
            }

            return BeginTrade(source, masonIdentity, MasonTradeKind.ChipTurnIn, 2);
        }

        public static bool TryStageMasonTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsMasonNpc(character, message.Target))
            {
                return false;
            }

            MasonTradeSession session = GetTradeSession(character);
            if (session == null
                && !IsMissionLifecycle(character, ShowDrMasonImplantQuestId, true, false)
                && !IsMissionLifecycle(character, TalkToDoctorMasonAfterInstallQuestId, true, false))
            {
                return false;
            }

            if (session == null)
            {
                MasonTradeKind kind = IsMissionLifecycle(
                                         character,
                                         TalkToDoctorMasonAfterInstallQuestId,
                                         true,
                                         false)
                                         ? MasonTradeKind.ChipTurnIn
                                         : MasonTradeKind.ShowImplant;
                BeginTrade(character, message.Target, kind, kind == MasonTradeKind.ChipTurnIn ? 2 : 1);
                session = GetTradeSession(character);
            }

            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                if (session.StagedContainer1.Type == IdentityType.None
                    || session.StagedContainer1.Instance < 0)
                {
                    session.StagedContainer1 = message.Container;
                }
                else if (session.Kind == MasonTradeKind.ChipTurnIn
                         && (session.StagedContainer2.Type == IdentityType.None
                             || session.StagedContainer2.Instance < 0))
                {
                    session.StagedContainer2 = message.Container;
                }
            }

            return true;
        }

        public static bool ShouldSuppressGenericMasonTradeRemove(
            ICharacter character,
            KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsMasonNpc(character, message.Target))
            {
                return false;
            }

            return GetTradeSession(character) != null
                   || IsMissionLifecycle(character, ShowDrMasonImplantQuestId, true, false)
                   || IsMissionLifecycle(character, TalkToDoctorMasonAfterInstallQuestId, true, false);
        }

        public static bool TryFinishMasonTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsMasonNpc(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            MasonTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                if (IsMissionLifecycle(source, TalkToDoctorMasonAfterInstallQuestId, true, false))
                {
                    ApplyChipTurnIn(source, message.Target, Identity.None, Identity.None);
                    return true;
                }

                if (IsMissionLifecycle(source, ShowDrMasonImplantQuestId, true, false))
                {
                    ApplyShowImplantTurnIn(source, message.Target, Identity.None);
                    return true;
                }

                return false;
            }

            if (session.Kind == MasonTradeKind.ChipTurnIn)
            {
                ApplyChipTurnIn(source, message.Target, session.StagedContainer1, session.StagedContainer2);
            }
            else
            {
                ApplyShowImplantTurnIn(source, message.Target, session.StagedContainer1);
            }

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

            if (IsMissionLifecycle(source, TalkToLoreleiQuestId, true, false))
            {
                DoctorMasonTipSender.TrySendTalkToLoreleiTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, TalkToDoctorMasonAfterInstallQuestId, true, false))
            {
                DoctorMasonTipSender.TrySendTalkToDoctorMasonAfterInstallTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, InstallTheImplantQuestId, true, false))
            {
                DoctorMasonTipSender.TrySendInstallTheImplantTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, ShowDrMasonImplantQuestId, true, false))
            {
                DoctorMasonTipSender.TrySendShowDrMasonImplantTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, AssembleImplant3QuestId, true, false))
            {
                DoctorMasonTipSender.TrySendAssembleImplant3TipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, AssembleImplant2QuestId, true, false))
            {
                DoctorMasonTipSender.TrySendAssembleImplant2TipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, AssembleImplant1QuestId, true, false))
            {
                DoctorMasonTipSender.TrySendAssembleImplant1TipOnly(source);
                return true;
            }

            return false;
        }

        private static void CompleteTalkMasonAndOfferAssemble1(ICharacter source)
        {
            CompleteAndActivate(
                source,
                TalkToDoctorMasonQuestId,
                AssembleImplant1QuestId,
                "mission_555BE9FC_talk_doctor_mason");
            DoctorMasonTipSender.TrySendTalkToDoctorMasonToAssemble1Handoff(source);
            Log("mason-accept→assemble1 character=" + source.Identity.ToString(true));
        }

        private static void ApplyShowImplantTurnIn(
            ICharacter source,
            Identity masonTarget,
            Identity staged)
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
                if (!TryConsumeInventoryItem(source, staged, Assemble3ResultItemId)
                    && !TryConsumeInventoryItem(source, Identity.None, Assemble3ResultItemId))
                {
                    Log("mason-show ABORTED — no finished implant");
                    BeginTrade(source, masonTarget, MasonTradeKind.ShowImplant, 1);
                    return;
                }

                KnuBotRejectedItemsMessageHandler.Default.Send(source, masonTarget, new Item[0], 0);
                TryGrantOverflowItem(source, GiftLegImplantItemId, 1);
                CompleteAndActivate(
                    source,
                    ShowDrMasonImplantQuestId,
                    InstallTheImplantQuestId,
                    "mission_555BEA00_show_implant");
                DoctorMasonTipSender.TrySendShowToInstallHandoff(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, masonTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, masonTarget);
                    }
                }
                catch
                {
                }

                Log("mason-show-turnin done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void ApplyChipTurnIn(
            ICharacter source,
            Identity masonTarget,
            Identity staged1,
            Identity staged2)
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
                bool consumedChip = TryConsumeInventoryItem(source, staged1, BlankIccIdChipItemId)
                                    || TryConsumeInventoryItem(source, staged2, BlankIccIdChipItemId)
                                    || TryConsumeInventoryItem(source, Identity.None, BlankIccIdChipItemId);
                bool consumedBots = TryConsumeInventoryItem(source, staged1, BiologicalSurveyNanobotsItemId)
                                    || TryConsumeInventoryItem(source, staged2, BiologicalSurveyNanobotsItemId)
                                    || TryConsumeInventoryItem(
                                        source,
                                        Identity.None,
                                        BiologicalSurveyNanobotsItemId);
                if (!consumedChip || !consumedBots)
                {
                    Log("mason-chip ABORTED — missing chip/bots");
                    BeginTrade(source, masonTarget, MasonTradeKind.ChipTurnIn, 2);
                    return;
                }

                KnuBotRejectedItemsMessageHandler.Default.Send(source, masonTarget, new Item[0], 0);
                ApplyChipTurnInXpCredits(source);
                TrySendChipRewardFeedback(source);
                TryGrantOverflowItem(source, ProgrammedIdChipItemId, 1);
                CompleteAndActivate(
                    source,
                    TalkToDoctorMasonAfterInstallQuestId,
                    TalkToLoreleiQuestId,
                    "mission_555BEA02_talk_mason_chip");
                DoctorMasonTipSender.TrySendTalkMasonToLoreleiHandoff(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, masonTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, masonTarget);
                    }
                }
                catch
                {
                }

                Log("mason-chip-turnin done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static bool BeginTrade(
            ICharacter source,
            Identity masonIdentity,
            MasonTradeKind kind,
            int slots)
        {
            if (masonIdentity.Type != IdentityType.CanbeAffected || masonIdentity.Instance == 0)
            {
                masonIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = DoctorMasonInstance
                                };
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new MasonTradeSession
                                                                    {
                                                                        NpcIdentity = masonIdentity,
                                                                        StagedContainer1 = Identity.None,
                                                                        StagedContainer2 = Identity.None,
                                                                        Kind = kind
                                                                    };
            }

            KnuBotStartTradeMessageHandler.Default.Send(source, masonIdentity, MasonTradePrompt, slots);
            return true;
        }

        private static void CompleteAndActivate(
            ICharacter source,
            string fromQuestId,
            string toQuestId,
            string objectiveId)
        {
            int instance = source.Identity.Instance;
            if (!MissionRuntime.IsInitialized)
            {
                return;
            }

            ForceCompleteHandoffTip(instance, fromQuestId, objectiveId);
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                fromQuestId,
                toQuestId);
            if (result.Status != MissionOperationStatus.Applied
                && result.Status != MissionOperationStatus.AlreadyApplied)
            {
                MissionRuntime.Service.OfferMission(instance, toQuestId);
                MissionRuntime.Service.AcceptMission(instance, toQuestId);
            }
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
                        ObservationKey = "doctor-mason-force-complete",
                        Amount = 1,
                        EventType = "DoctorMasonQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static void ApplyChipTurnInXpCredits(ICharacter source)
        {
            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                TalkToDoctorMasonAfterInstallQuestId,
                "arete-credits-awarded-mason-chip-turnin",
                ChipTurnInCreditReward,
                "arete-xp-awarded-mason-chip-turnin",
                ChipTurnInXpReward,
                "mason-chip-turnin-2581xp");
        }

        private static void TrySendChipRewardFeedback(ICharacter source)
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
                    FormattedMessage = ChipTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void TryGrantOverflowItem(ICharacter source, int itemId, int quality)
        {
            if (source == null || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("grant skipped missing template=" + itemId);
                return;
            }

            if (InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                SendOverflowGrantPackets(source, itemId, quality);
                return;
            }

            Item item;
            try
            {
                item = new Item(quality, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("grant create failed: " + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("grant failed status=" + grant.Status);
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
                    if (staged != null && (staged.LowID == itemId || staged.HighID == itemId))
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
            if (source == null || source.BaseInventory == null)
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

        private static bool IsMasonNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == DoctorMasonInstance)
            {
                return true;
            }

            if (source?.Playfield == null)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null
                   && !string.IsNullOrEmpty(npc.Name)
                   && npc.Name.IndexOf("Mason", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MasonTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                MasonTradeSession session;
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

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "DoctorMasonQuestRuntime " + message);
        }
    }
}
