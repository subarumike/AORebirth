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
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260721-loralei: Talk Lorelei → Lost Pet → Deliver reet + chip → Talk Vaughn.
    /// </summary>
    public static class LoreleiQuestRuntime
    {
        public const string AcceptOfferNodeId = "lorelei_accept";

        public const string DeliverOfferNodeId = "lorelei_deliver";

        public const string LollyCookieTradeNodeId = "lolly_greet";

        public const string LollyPickupNodeId = "lolly_pickup";

        public const string TalkToLoreleiQuestId = DoctorMasonQuestRuntime.TalkToLoreleiQuestId;

        public const string LostPetQuestId = "Mission:555BEA04";

        public const string DeliverQuestId = "Mission:555BEA05";

        public const string TalkToVaughnQuestId = "Mission:555BEA06";

        private const int LoreleiInstance = unchecked((int)0x78E0FC6B);

        private const int LollyInstance = unchecked((int)0x7985CAEC);

        private const int CookieItemId = 297370;

        private const int PetCageItemId = LoreleiCombineRules.PetCageItemId;

        private const int LoreleisReetItemId = LoreleiCombineRules.LoreleisReetItemId;

        private const int PetCageWithReetItemId = LoreleiCombineRules.PetCageWithReetItemId;

        private const int ProgrammedIdChipItemId = 296576;

        private const int IdCardRewardItemId = 296692;

        private const int DeliverTurnInXpReward = 2581;

        private const int DeliverTurnInCreditReward = 1440;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string LollyTradePrompt =
            "Drag and drop the item(s) you want to give to Lolly the Reet into one of the slots available and press \"accept\"";

        private const string LoreleiTradePrompt =
            "Drag and drop the item(s) you want to give to Lorelei the Bartender into one of the slots available and press \"accept\"";

        private const string DeliverTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!?@i!!!1q~";

        private const string LoreleiShoutText =
            "Lorelei the Bartender shouts: Stupid Bird! Why do you always have to run away!?!";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, LoreleiTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, LoreleiTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private static readonly HashSet<int> LollyCookieTradedByCharacter = new HashSet<int>();

        private static readonly HashSet<int> LollyPickedUpByCharacter = new HashSet<int>();

        private enum LoreleiTradeKind
        {
            LollyCookie = 0,
            LoreleiDeliver = 1
        }

        private sealed class LoreleiTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer1;

            public Identity StagedContainer2;

            public LoreleiTradeKind Kind;

            public string ResumeNodeId;
        }

        public static string ResolveLoreleiStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (IsMissionLifecycle(source, TalkToVaughnQuestId, true, true))
            {
                return null;
            }

            if (IsMissionLifecycle(source, DeliverQuestId, true, false))
            {
                return DeliverOfferNodeId;
            }

            if (IsMissionLifecycle(source, LostPetQuestId, true, false)
                || IsMissionLifecycle(source, TalkToLoreleiQuestId, true, false))
            {
                return "lorelei_greet";
            }

            return null;
        }

        public static string ResolveLollyStartNodeId(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (!IsMissionLifecycle(source, LostPetQuestId, true, false))
            {
                return null;
            }

            if (LollyPickedUpByCharacter.Contains(source.Identity.Instance)
                || HasInventoryItem(source, LoreleisReetItemId)
                || HasInventoryItem(source, PetCageWithReetItemId))
            {
                return null;
            }

            int instance = source.Identity.Instance;
            if (LollyCookieTradedByCharacter.Contains(instance))
            {
                return LollyPickupNodeId;
            }

            return LollyCookieTradeNodeId;
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null)
            {
                return false;
            }

            if (string.Equals(previousNodeId, AcceptOfferNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                if (IsMissionLifecycle(source, LostPetQuestId, true, true)
                    || IsMissionLifecycle(source, TalkToLoreleiQuestId, false, true))
                {
                    Log("lorelei-accept ignored — already progressed");
                    return true;
                }

                CompleteTalkLoreleiAndOfferLostPet(source);
                return true;
            }

            if (string.Equals(previousNodeId, LollyPickupNodeId, StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                if (LollyPickedUpByCharacter.Contains(source.Identity.Instance)
                    || HasInventoryItem(source, LoreleisReetItemId))
                {
                    Log("lolly-pickup ignored — already picked up");
                    return true;
                }

                if (!IsMissionLifecycle(source, LostPetQuestId, true, false))
                {
                    return false;
                }

                TryGrantOverflowItem(source, LoreleisReetItemId, 1);
                LollyPickedUpByCharacter.Add(source.Identity.Instance);
                LoreleiOasisMobRuntime.DespawnLolly(source);

                Log("lolly-pickup→reet character=" + source.Identity.ToString(true));
                return true;
            }

            return false;
        }

        public static void OnCombineSucceeded(ICharacter source, int resultLowId, int resultHighId)
        {
            if (source == null)
            {
                return;
            }

            int resultId = resultLowId > 0 ? resultLowId : resultHighId;
            if (resultId == PetCageWithReetItemId
                && IsMissionLifecycle(source, LostPetQuestId, true, false))
            {
                CompleteAndActivate(
                    source,
                    LostPetQuestId,
                    DeliverQuestId,
                    "mission_555BEA04_lost_pet");
                LoreleiTipSender.TrySendLostPetToDeliverHandoff(source);
            }
        }

        public static bool TryBeginLollyCookieTrade(ICharacter source, Identity lollyIdentity)
        {
            if (source == null || !IsMissionLifecycle(source, LostPetQuestId, true, false))
            {
                return false;
            }

            if (LollyPickedUpByCharacter.Contains(source.Identity.Instance)
                || HasInventoryItem(source, LoreleisReetItemId))
            {
                return false;
            }

            if (lollyIdentity.Type != IdentityType.CanbeAffected || lollyIdentity.Instance == 0)
            {
                lollyIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = LollyInstance
                                };
            }

            // Capture: KnubotAppendText "U giev cracker! Nao!" then StartTrade.
            KnuBotAppendTextMessageHandler.Default.Send(source, lollyIdentity, "U giev cracker! Nao!");
            return BeginTrade(
                source,
                lollyIdentity,
                LoreleiTradeKind.LollyCookie,
                1,
                LollyPickupNodeId);
        }

        public static bool TryBeginDeliverTrade(ICharacter source, Identity loreleiIdentity)
        {
            if (source == null || !IsMissionLifecycle(source, DeliverQuestId, true, false))
            {
                return false;
            }

            return BeginTrade(
                source,
                loreleiIdentity,
                LoreleiTradeKind.LoreleiDeliver,
                2,
                "lorelei_thanks");
        }

        public static bool TryStageLoreleiTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null)
            {
                return false;
            }

            bool isLorelei = IsLoreleiNpc(character, message.Target);
            bool isLolly = IsLollyNpc(character, message.Target);
            if (!isLorelei && !isLolly)
            {
                return false;
            }

            LoreleiTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                if (isLolly && IsMissionLifecycle(character, LostPetQuestId, true, false))
                {
                    BeginTrade(
                        character,
                        message.Target,
                        LoreleiTradeKind.LollyCookie,
                        1,
                        LollyPickupNodeId);
                }
                else if (isLorelei && IsMissionLifecycle(character, DeliverQuestId, true, false))
                {
                    BeginTrade(
                        character,
                        message.Target,
                        LoreleiTradeKind.LoreleiDeliver,
                        2,
                        "lorelei_thanks");
                }
                else
                {
                    return false;
                }

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
                else if (session.Kind == LoreleiTradeKind.LoreleiDeliver
                         && (session.StagedContainer2.Type == IdentityType.None
                             || session.StagedContainer2.Instance < 0))
                {
                    session.StagedContainer2 = message.Container;
                }
            }

            return true;
        }

        public static bool ShouldSuppressGenericLoreleiTradeRemove(
            ICharacter character,
            KnuBotTradeMessage message)
        {
            if (character == null || message == null)
            {
                return false;
            }

            if (!IsLoreleiNpc(character, message.Target) && !IsLollyNpc(character, message.Target))
            {
                return false;
            }

            return GetTradeSession(character) != null
                   || IsMissionLifecycle(character, LostPetQuestId, true, false)
                   || IsMissionLifecycle(character, DeliverQuestId, true, false);
        }

        public static bool TryFinishLoreleiTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            bool isLorelei = IsLoreleiNpc(source, message.Target);
            bool isLolly = IsLollyNpc(source, message.Target);
            if (!isLorelei && !isLolly)
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            LoreleiTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                if (isLolly && IsMissionLifecycle(source, LostPetQuestId, true, false))
                {
                    ApplyLollyCookieTrade(source, message.Target, Identity.None);
                    return true;
                }

                if (isLorelei && IsMissionLifecycle(source, DeliverQuestId, true, false))
                {
                    ApplyDeliverTurnIn(source, message.Target, Identity.None, Identity.None);
                    return true;
                }

                return false;
            }

            if (session.Kind == LoreleiTradeKind.LollyCookie)
            {
                ApplyLollyCookieTrade(source, message.Target, session.StagedContainer1);
            }
            else
            {
                ApplyDeliverTurnIn(source, message.Target, session.StagedContainer1, session.StagedContainer2);
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

            if (IsMissionLifecycle(source, TalkToVaughnQuestId, true, false))
            {
                LoreleiTipSender.TrySendTalkToVaughnTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, DeliverQuestId, true, false))
            {
                LoreleiTipSender.TrySendDeliverTipOnly(source);
                return true;
            }

            if (IsMissionLifecycle(source, LostPetQuestId, true, false))
            {
                LoreleiTipSender.TrySendLostPetTipOnly(source);
                return true;
            }

            return false;
        }

        private static void CompleteTalkLoreleiAndOfferLostPet(ICharacter source)
        {
            CompleteAndActivate(
                source,
                TalkToLoreleiQuestId,
                LostPetQuestId,
                "mission_555BEA03_talk_lorelei");
            LoreleiTipSender.TrySendTalkLoreleiToLostPetHandoff(source);
            TryGrantOverflowItem(source, PetCageItemId, 1);
            Log("lorelei-accept→lost-pet character=" + source.Identity.ToString(true));
        }

        private static void ApplyLollyCookieTrade(
            ICharacter source,
            Identity lollyTarget,
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
                if (!TryConsumeInventoryItem(source, staged, CookieItemId)
                    && !TryConsumeInventoryItem(source, Identity.None, CookieItemId))
                {
                    Log("lolly-cookie ABORTED — no cookie");
                    BeginTrade(source, lollyTarget, LoreleiTradeKind.LollyCookie, 1, LollyPickupNodeId);
                    return;
                }

                KnuBotRejectedItemsMessageHandler.Default.Send(source, lollyTarget, new Item[0], 0);
                LollyCookieTradedByCharacter.Add(instance);
                ForgetTradeSession(source);
                try
                {
                    // Capture 20260721-loralei: RejectedItems then AppendText pickup + AnswerList
                    // (do not close; second click must not be required).
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, lollyTarget))
                    {
                        KnuBotAppendTextMessageHandler.Default.Send(
                            source,
                            lollyTarget,
                            "The bird starts eating the tasty cookie. It is distracted, now is the time to catch it!");
                        KnuBotAnswerListMessageHandler.Default.Send(
                            source,
                            lollyTarget,
                            new[] { "(Quietly pick up the bird)", "Goodbye" });
                    }
                }
                catch
                {
                }

                Log("lolly-cookie-trade done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void ApplyDeliverTurnIn(
            ICharacter source,
            Identity loreleiTarget,
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
                bool consumedCage = TryConsumeInventoryItem(source, staged1, PetCageWithReetItemId)
                                    || TryConsumeInventoryItem(source, staged2, PetCageWithReetItemId)
                                    || TryConsumeInventoryItem(source, Identity.None, PetCageWithReetItemId);
                bool consumedChip = TryConsumeInventoryItem(source, staged1, ProgrammedIdChipItemId)
                                    || TryConsumeInventoryItem(source, staged2, ProgrammedIdChipItemId)
                                    || TryConsumeInventoryItem(source, Identity.None, ProgrammedIdChipItemId);
                if (!consumedCage || !consumedChip)
                {
                    Log("lorelei-deliver ABORTED — missing cage/chip");
                    BeginTrade(source, loreleiTarget, LoreleiTradeKind.LoreleiDeliver, 2, "lorelei_thanks");
                    return;
                }

                KnuBotRejectedItemsMessageHandler.Default.Send(source, loreleiTarget, new Item[0], 0);
                ApplyDeliverTurnInXpCredits(source);
                TrySendDeliverRewardFeedback(source);
                TryGrantOverflowItem(source, IdCardRewardItemId, 1);
                TrySendLoreleiShout(source);
                CompleteAndActivate(
                    source,
                    DeliverQuestId,
                    TalkToVaughnQuestId,
                    "mission_555BEA05_deliver_reet");
                LoreleiTipSender.TrySendDeliverToVaughnHandoff(source);
                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, loreleiTarget))
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, loreleiTarget);
                    }
                }
                catch
                {
                }

                Log("lorelei-deliver-turnin done character=" + source.Identity.ToString(true));
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
            Identity npcIdentity,
            LoreleiTradeKind kind,
            int slots,
            string resumeNodeId)
        {
            if (kind == LoreleiTradeKind.LollyCookie)
            {
                if (npcIdentity.Type != IdentityType.CanbeAffected || npcIdentity.Instance == 0)
                {
                    npcIdentity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = LollyInstance
                                  };
                }
            }
            else if (npcIdentity.Type != IdentityType.CanbeAffected || npcIdentity.Instance == 0)
            {
                npcIdentity = new Identity
                              {
                                  Type = IdentityType.CanbeAffected,
                                  Instance = LoreleiInstance
                              };
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new LoreleiTradeSession
                                                                    {
                                                                        NpcIdentity = npcIdentity,
                                                                        StagedContainer1 = Identity.None,
                                                                        StagedContainer2 = Identity.None,
                                                                        Kind = kind,
                                                                        ResumeNodeId = resumeNodeId
                                                                    };
            }

            string prompt = kind == LoreleiTradeKind.LollyCookie
                                ? LollyTradePrompt
                                : LoreleiTradePrompt;
            KnuBotStartTradeMessageHandler.Default.Send(source, npcIdentity, prompt, slots);
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
                        ObservationKey = "lorelei-force-complete",
                        Amount = 1,
                        EventType = "LoreleiQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static void ApplyDeliverTurnInXpCredits(ICharacter source)
        {
            MissionRewardDefinition definition = new MissionRewardDefinition
                                                {
                                                    RewardKey = "captured-lorelei-deliver-turnin-xp-credits",
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
                                                                Value = DeliverTurnInCreditReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.xp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = DeliverTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            },
                                                            new MissionCharacterStatMutation
                                                            {
                                                                StatIdentityType = (int)IdentityType.CanbeAffected,
                                                                StatId = (int)StatIds.unsavedxp,
                                                                Kind = MissionStatMutationKind.AddClamped,
                                                                Value = DeliverTurnInXpReward,
                                                                MinimumValue = 0,
                                                                MaximumValue = uint.MaxValue
                                                            }
                                                        }
                                                };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                DeliverQuestId,
                definition,
                "capture:20260721-loralei:lorelei-deliver-turnin-xp-credits");
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

        private static void TrySendDeliverRewardFeedback(ICharacter source)
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
                    FormattedMessage = DeliverTurnInRewardFeedback,
                    Unknown2 = 0
                });
        }

        private static void TrySendLoreleiShout(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            source.Controller.Client.SendCompressed(
                new ChatTextMessage
                {
                    Identity = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = LoreleiInstance
                               },
                    Text = LoreleiShoutText
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

        private static bool HasInventoryItem(ICharacter source, int itemId)
        {
            Identity unused;
            return TryFindItemContainer(source, itemId, out unused);
        }

        private static bool IsLoreleiNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == LoreleiInstance)
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
                   && npc.Name.IndexOf("Lorelei", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLollyNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == LollyInstance)
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
                   && npc.Name.IndexOf("Lolly", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static LoreleiTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                LoreleiTradeSession session;
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
            LogUtil.Debug(DebugInfoDetail.Engine, "LoreleiQuestRuntime " + message);
        }
    }
}
