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

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class FlintBioComQuestRuntime
    {
        private sealed class AlexTradeSession
        {
            public Identity NpcIdentity { get; set; }

            public Identity StagedContainer { get; set; }
        }

        public const string AcceptNodeId = "flint_072904_002";

        public const string AlexTradeOfferNodeId = "alex_074847_001";

        public const string AlexTradeHoldNodeId = "alex_074847_trade";

        public const int BioAnalyzingComputerItemId = 156020;

        // Capture 20260720-flint TemplateAction: LowId=HighId=156020 (not 156021).
        public const int BioAnalyzingComputerItemHighId = 156020;

        public const int BlankInfoChipItemId = 296570;

        public const int RebuiltHc12SecTecMonitorItemId = 295800;

        private const int AlexGibbsInstance = 2028010593;

        // Must match Content/Arete/flint-novak/quests/flint-novak.quests.json (not handoff fallback).
        private const string FindObjectiveId = "mission_5514B19B_kill_junkyard_robots";

        private const string KillTargetName = "Cleaning Robot";

        private const int CleaningRobotMonsterData = 297023;

        private const int RequiredKillCount = 7;

        private const string BioComGrantFlag = "bio-com-granted";

        private const string AlexTurnInRewardsFlag = "alex-turnin-rewards";

        // Capture 20260731-184635 FormatFeedback: "Received reward: 2229 XP, 1120 credits."
        private const int TurnInXpReward = 2229;

        private const int TurnInCreditReward = 1120;

        // Capture 20260731-184635 FormatFeedback wire.
        private const string TurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!.0~";

        private const int AreteLandingPlayfieldId = 6553;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, AlexTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, AlexTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null
                || !string.Equals(previousNodeId, AcceptNodeId, StringComparison.OrdinalIgnoreCase)
                || answerIndex != 0)
            {
                return false;
            }

            return TryAcceptFindQuest(source);
        }

        public static bool TryBeginAlexTrade(ICharacter source, Identity alexIdentity)
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

            // Do NOT grant Bio Com here — clicking Alex was duplicating the item.
            BeginAlexTrade(source, alexIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                alexIdentity,
                "Drag and drop the item(s) you want to give to Alex Gibbs into one of the slots available and press \"accept\"",
                1);
            Log(
                "alex-trade-opened character="
                + source.Identity.ToString(true)
                + " target="
                + alexIdentity.ToString(true));
            return true;
        }

        public static bool TryStageAlexTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (source == null || message == null || !IsAlexGibbsNpc(source, message.Target))
            {
                return false;
            }

            if (!IsDeliverTipActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            // Tip 4 brain show: never stage into BioCom session.
            if (PersonalizedRobotBrainQuestRuntime.IsShowBrainTipActive(source)
                || PersonalizedRobotBrainQuestRuntime.HasPersonalizedBrain(source))
            {
                return false;
            }

            BeginAlexTrade(source, message.Target);
            AlexTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance > 0)
            {
                session.StagedContainer = message.Container;
            }

            return true;
        }

        public static bool TryFinishAlexTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null)
            {
                return false;
            }

            bool isAlex = IsAlexGibbsNpc(source, message.Target);
            if (!isAlex)
            {
                return false;
            }

            // Deliver BioCom tip only. Never claim Personalized Robot Brain turn-in
            // (that wrongly re-offers Surveillance Uplink after Kneecapping).
            if (!IsDeliverTipActive(source))
            {
                return false;
            }

            // Tip 4 brain inspect shares Alex — do not swallow FinishTrade.
            if (PersonalizedRobotBrainQuestRuntime.IsShowBrainTipActive(source)
                || PersonalizedRobotBrainQuestRuntime.HasPersonalizedBrain(source))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            AlexTradeSession session = GetTradeSession(source);
            if (session == null)
            {
                BeginAlexTrade(source, message.Target);
                session = GetTradeSession(source);
            }

            Identity stagedContainer = session != null ? session.StagedContainer : Identity.None;
            // Capture 20260720-flint: player stages Bio Com then clicks Accept (FinishTrade).
            // Never auto-complete / consume from inventory without a staged Bio Com.
            if (!IsStagedBioCom(source, stagedContainer))
            {
                Log("alex-finish ignored: Bio Analyzing Computer not staged in trade");
                // Return false so Tip-4 brain finish (or other Alex trades) can claim.
                return false;
            }

            ApplyAlexTradeTurnIn(source, message.Target, stagedContainer);
            return true;
        }

        public static bool TryAcceptFindQuest(ICharacter source)
        {
            if (!IsValidPlayerInArete(source) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int instance = source.Identity.Instance;
            MissionOperationResult offer = MissionRuntime.Service.OfferMission(instance, "Mission:5514B19B");
            if (IsTerminalFailure(offer))
            {
                Log("find offer failed status=" + offer.Status + " msg=" + offer.Message);
                return false;
            }

            MissionOperationResult accept = MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19B");
            if (IsTerminalFailure(accept))
            {
                Log("find accept failed status=" + accept.Status + " msg=" + accept.Message);
                return false;
            }

            ClearLocalKillProgress(instance);

            ZoneEngine.Core.Missions.MissionStateRecord flintTalk =
                MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
            if (flintTalk != null
                && (flintTalk.State == MissionLifecycleState.Active
                    || flintTalk.State == MissionLifecycleState.Offered))
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B198");
            }

            RexQuestPreviewEmissionResult handoff = SafeQuestFullUpdateSender.TrySendFlintToFindBioHandoff(source);
            Log(
                "find accepted character="
                + source.Identity.ToString(true)
                + " handoff="
                + (handoff == null ? "null" : handoff.Message));
            return handoff != null && handoff.Emitted;
        }

        public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return false;
            }

            if (!IsInAreteLanding(attacker) || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Capture 20260720-flint: only "Cleaning Robot" corpses advance the tip
            // (Cleanmeister kills do not emit Junkyard Robots feedback).
            if (!IsFindBioKillTarget(target))
            {
                return false;
            }

            int characterId = attacker.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, "Mission:5514B19B");
            if (mission == null)
            {
                return false;
            }

            if (mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, "Mission:5514B19B");
                mission = MissionRuntime.Service.GetMission(characterId, "Mission:5514B19B");
            }

            if (mission == null || mission.State != MissionLifecycleState.Active)
            {
                return false;
            }

            string observationKey = "npc-death:" + target.Identity.ToString(true);
            MissionOperationResult observe = MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = "Mission:5514B19B",
                    ObjectiveId = FindObjectiveId,
                    ObservationKey = observationKey,
                    Amount = 1,
                    EventType = "KillNpcTarget:CharacterAction:Death",
                    SourceIdentity = attacker.Identity.ToString(true),
                    TargetIdentity = target.Identity.ToString(true)
                });

            int progress;
            if (observe.Status == MissionOperationStatus.Applied
                || observe.Status == MissionOperationStatus.AlreadyApplied
                || observe.Status == MissionOperationStatus.DuplicateObservation)
            {
                MissionObjectiveProgressRecord objective = observe.Objective
                                                          ?? MissionRuntime.Service.GetObjective(
                                                              characterId,
                                                              "Mission:5514B19B",
                                                              FindObjectiveId);
                progress = objective != null ? objective.Progress : 0;
            }
            else
            {
                // Local fallback when MissionRuntime objective id/state is stale from older agents.
                progress = AdvanceLocalKillProgress(characterId, observationKey);
                Log(
                    "observe fallback status="
                    + observe.Status
                    + " msg="
                    + observe.Message
                    + " localProgress="
                    + progress
                    + " character="
                    + attacker.Identity.ToString(true));
            }

            if (progress <= 0)
            {
                return false;
            }

            TrySendKillFeedback(attacker, progress, RequiredKillCount);
            if (progress >= RequiredKillCount)
            {
                ClearLocalKillProgress(characterId);
                CompleteFindAndOfferDeliver(attacker);
            }

            return true;
        }

        private static bool IsFindBioKillTarget(ICharacter target)
        {
            if (target == null)
            {
                return false;
            }

            if (string.Equals(EffectiveName(target), KillTargetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Same mesh/monsterData as capture Cleaning Robot (297023); name must still be Cleaning Robot.
            try
            {
                return target.Stats != null
                       && target.Stats[StatIds.monsterdata].Value == CleaningRobotMonsterData
                       && EffectiveName(target).IndexOf("Cleaning Robot", StringComparison.OrdinalIgnoreCase) >= 0
                       && EffectiveName(target).IndexOf("Cleanmeister", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly object KillProgressSyncRoot = new object();

        private static readonly Dictionary<int, int> LocalKillProgressByCharacter = new Dictionary<int, int>();

        private static readonly Dictionary<int, HashSet<string>> LocalObservedDeathsByCharacter =
            new Dictionary<int, HashSet<string>>();

        private static int AdvanceLocalKillProgress(int characterId, string observationKey)
        {
            lock (KillProgressSyncRoot)
            {
                HashSet<string> seen;
                if (!LocalObservedDeathsByCharacter.TryGetValue(characterId, out seen) || seen == null)
                {
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    LocalObservedDeathsByCharacter[characterId] = seen;
                }

                int progress;
                if (!LocalKillProgressByCharacter.TryGetValue(characterId, out progress))
                {
                    progress = 0;
                }

                if (!seen.Add(observationKey ?? string.Empty))
                {
                    return progress;
                }

                progress = Math.Min(RequiredKillCount, progress + 1);
                LocalKillProgressByCharacter[characterId] = progress;
                return progress;
            }
        }

        private static void ClearLocalKillProgress(int characterId)
        {
            lock (KillProgressSyncRoot)
            {
                LocalKillProgressByCharacter.Remove(characterId);
                LocalObservedDeathsByCharacter.Remove(characterId);
            }
        }

        public static bool TryResendActiveTip(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            // Plant/Deliver/Kneecapping/Report tips own the journal once accepted — never re-emit
            // Surveillance Uplink (leaves Remain 00:00 ghosts when delete missed earlier).
            if (SurveillanceUplinkQuestRuntime.TrySyncTipsForLogin(source))
            {
                return true;
            }

            int instance = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord deliver =
                MissionRuntime.Service.GetMission(instance, "Mission:5514B19C");
            if (IsActiveOrOffered(deliver))
            {
                // Only top-up if Deliver tip is active and Bio Com is missing (no spam on every login).
                if (!HasBioCom(source))
                {
                    TryGrantBioCom(source);
                }

                RexQuestPreviewEmissionResult result = SafeQuestFullUpdateSender.TrySendDeliverBioPreview(source);
                return result != null && result.Emitted;
            }

            ZoneEngine.Core.Missions.MissionStateRecord find =
                MissionRuntime.Service.GetMission(instance, "Mission:5514B19B");
            if (IsActiveOrOffered(find))
            {
                RexQuestPreviewEmissionResult result = SafeQuestFullUpdateSender.TrySendFindBioPreview(source);
                return result != null && result.Emitted;
            }

            return false;
        }

        private static void ApplyAlexTradeTurnIn(ICharacter source, Identity alexTarget, Identity stagedContainer)
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
                TryConsumeBioCom(source, stagedContainer);
                try
                {
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, alexTarget, new Item[0], 0);
                }
                catch (Exception ex)
                {
                    Log("alex-rejecteditems failed: " + ex.Message);
                }

                ApplyAlexTurnInXpCredits(source);
                TrySendTurnInRewardFeedback(source);
                TryGrantAlexTurnInItems(source);
                CompleteDeliverAndOfferUplink(source);
                ForgetTradeSession(source);
                try
                {
                    ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, alexTarget);
                }
                catch (Exception ex)
                {
                    Log("alex-resume-dialogue failed: " + ex.Message);
                }

                Log("alex-turnin done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void CompleteDeliverAndOfferUplink(ICharacter source)
        {
            int instance = source.Identity.Instance;
            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B19C",
                "Mission:5514B19D");
            if (IsTerminalFailure(result))
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19C");
                MissionRuntime.Service.OfferMission(instance, "Mission:5514B19D");
                MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19D");
            }

            SafeQuestFullUpdateSender.TrySendDeliverBioToSurveillanceUplinkHandoff(source);
        }

        private static void CompleteFindAndOfferDeliver(ICharacter source)
        {
            int instance = source.Identity.Instance;
            // Capture 20260720-flint @20:48:16: one TemplateAction 156020 then tip handoff.
            TryGrantBioCom(source);

            MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                instance,
                "Mission:5514B19B",
                "Mission:5514B19C");
            if (IsTerminalFailure(result))
            {
                MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19B");
                MissionRuntime.Service.OfferMission(instance, "Mission:5514B19C");
                MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19C");
            }

            SafeQuestFullUpdateSender.TrySendFindBioToDeliverHandoff(source);
            Log("find→deliver handoff character=" + source.Identity.ToString(true));
        }

        private static void TryGrantAlexTurnInItems(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int instance = source.Identity.Instance;
            bool hasChip = InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                BlankInfoChipItemId);
            bool hasMonitor = InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                RebuiltHc12SecTecMonitorItemId);
            if (hasChip && hasMonitor)
            {
                MissionRuntime.Service.SetFlag(
                    instance,
                    "Mission:5514B19C",
                    AlexTurnInRewardsFlag,
                    "items:" + BlankInfoChipItemId + "+" + RebuiltHc12SecTecMonitorItemId);
                return;
            }

            // Capture 20260731-184635 @16:47:00: Blank Info Chip 296570 + Rebuilt HC-12 295800.
            if (!hasChip)
            {
                GrantSingleRewardItem(source, BlankInfoChipItemId);
            }

            if (!hasMonitor)
            {
                GrantSingleRewardItem(source, RebuiltHc12SecTecMonitorItemId);
            }

            MissionRuntime.Service.SetFlag(
                instance,
                "Mission:5514B19C",
                AlexTurnInRewardsFlag,
                "items:" + BlankInfoChipItemId + "+" + RebuiltHc12SecTecMonitorItemId);
        }

        private static void GrantSingleRewardItem(ICharacter source, int itemId)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(itemId))
            {
                Log("reward grant skipped item=" + itemId);
                return;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                return;
            }

            Item item;
            try
            {
                item = new Item(1, itemId, itemId);
            }
            catch (Exception ex)
            {
                Log("reward create failed item=" + itemId + " err=" + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("reward grant failed item=" + itemId + " status=" + grant.Status);
                return;
            }

            SendOverflowGrantPackets(source, itemId, 1);
            if (itemId == BlankInfoChipItemId)
            {
                FeedbackMessageHandler.Default.Send(source, 110, 108871108);
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

        private static void ApplyAlexTurnInXpCredits(ICharacter source)
        {
            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                "Mission:5514B19C",
                "arete-credits-awarded-flint-biocom-turnin",
                TurnInCreditReward,
                "arete-xp-awarded-flint-biocom-turnin",
                TurnInXpReward,
                "flint-biocom-alex-turnin-2229xp");
        }

        private static void TrySendTurnInRewardFeedback(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            try
            {
                source.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = source.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = TurnInRewardFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception ex)
            {
                Log("alex reward feedback failed: " + ex.Message);
            }
        }

        private static bool TryGrantBioCom(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int instance = source.Identity.Instance;
            if (HasBioCom(source))
            {
                MissionRuntime.Service.SetFlag(
                    instance,
                    "Mission:5514B19B",
                    BioComGrantFlag,
                    "item:" + BioAnalyzingComputerItemId);
                return true;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("bio-com grant skipped: inventory/client missing");
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(BioAnalyzingComputerItemId))
            {
                Log("bio-com grant skipped: ItemLoader missing id=" + BioAnalyzingComputerItemId);
                return false;
            }

            Item item;
            try
            {
                // Capture 20260720-flint: LowId=HighId=156020.
                item = new Item(1, BioAnalyzingComputerItemId, BioAnalyzingComputerItemId);
            }
            catch (Exception ex)
            {
                Log("bio-com item create failed: " + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log("bio-com grant failed status=" + grant.Status);
                return false;
            }

            SendOverflowGrantPackets(source, BioAnalyzingComputerItemId, 1);
            MissionRuntime.Service.SetFlag(
                instance,
                "Mission:5514B19B",
                BioComGrantFlag,
                "item:" + BioAnalyzingComputerItemId);
            Log("bio-com granted character=" + source.Identity.ToString(true));
            return true;
        }

        private static bool HasBioCom(ICharacter source)
        {
            return source != null
                   && InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                   && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                       source,
                       BioAnalyzingComputerItemId);
        }

        private static bool IsStagedBioCom(ICharacter source, Identity stagedContainer)
        {
            if (source == null
                || source.BaseInventory == null
                || stagedContainer.Type == IdentityType.None
                || stagedContainer.Instance <= 0)
            {
                return false;
            }

            IInventoryPage page;
            if (!source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out page) || page == null)
            {
                return false;
            }

            return IsBioCom(page[stagedContainer.Instance]);
        }

        private static void TryConsumeBioCom(ICharacter source, Identity stagedContainer)
        {
            if (source == null || source.BaseInventory == null)
            {
                return;
            }

            // Only remove the staged trade slot — never vacuum every Bio Com from inventory.
            if (stagedContainer.Type == IdentityType.None || stagedContainer.Instance <= 0)
            {
                return;
            }

            IInventoryPage stagedPage;
            if (!source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                || stagedPage == null)
            {
                return;
            }

            IItem staged = stagedPage[stagedContainer.Instance];
            if (!IsBioCom(staged))
            {
                return;
            }

            stagedPage.Remove(stagedContainer.Instance);
            try
            {
                if (source.BaseInventory.Write())
                {
                    CharacterActionMessageHandler.Default.SendDeleteItem(
                        source,
                        (int)stagedContainer.Type,
                        stagedContainer.Instance);
                    return;
                }
            }
            catch (Exception)
            {
            }

            stagedPage.Add(stagedContainer.Instance, staged);
        }

        private static void TrySendKillFeedback(ICharacter character, int currentCount, int requiredCount)
        {
            if (character == null
                || character.Controller == null
                || character.Controller.Client == null
                || currentCount <= 0
                || currentCount >= requiredCount)
            {
                return;
            }

            string feedback = GetCapturedRemainingCountFeedback(currentCount);
            if (string.IsNullOrEmpty(feedback))
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = feedback,
                    Unknown2 = 0
                });
        }

        private static string GetCapturedRemainingCountFeedback(int currentCount)
        {
            switch (currentCount)
            {
                case 1:
                    return "~&!!!\":$nZiAi!!!!'s\u001eJunkyard Robots";
                case 2:
                    return "~&!!!\":$nZiAi!!!!&s\u001eJunkyard Robots";
                case 3:
                    return "~&!!!\":$nZiAi!!!!%s\u001eJunkyard Robots";
                case 4:
                    return "~&!!!\":$nZiAi!!!!$s\u001eJunkyard Robots";
                case 5:
                    return "~&!!!\":$nZiAi!!!!#s\u001eJunkyard Robots";
                case 6:
                    return "~&!!!\":$nZiAi!!!!\"s\u001eJunkyard Robots";
                default:
                    return null;
            }
        }

        public static bool IsDeliverBioDialogueActive(ICharacter source)
        {
            return IsDeliverTipActive(source);
        }

        private static bool IsDeliverTipActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;

            // Past Bio deliver: Report-to-Alex / Talk-to-Stan / tradeskill must not reopen Flint item dialog.
            ZoneEngine.Core.Missions.MissionStateRecord report =
                MissionRuntime.Service.GetMission(characterId, "Mission:555B4365");
            ZoneEngine.Core.Missions.MissionStateRecord talkStan =
                MissionRuntime.Service.GetMission(characterId, "Mission:555B4366");
            ZoneEngine.Core.Missions.MissionStateRecord nanoSensor =
                MissionRuntime.Service.GetMission(characterId, "Mission:555B4367");
            if (IsActiveOrOffered(report)
                || (report != null && report.State == MissionLifecycleState.Completed)
                || IsActiveOrOffered(talkStan)
                || (talkStan != null && talkStan.State == MissionLifecycleState.Completed)
                || IsActiveOrOffered(nanoSensor)
                || (nanoSensor != null && nanoSensor.State == MissionLifecycleState.Completed))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord deliver =
                MissionRuntime.Service.GetMission(characterId, "Mission:5514B19C");
            if (IsActiveOrOffered(deliver))
            {
                return true;
            }

            // Tip-only / ledger desync: Bio Com in bag after Find complete, Deliver not finished.
            if (!HasBioCom(source))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord find =
                MissionRuntime.Service.GetMission(characterId, "Mission:5514B19B");
            ZoneEngine.Core.Missions.MissionStateRecord uplink =
                MissionRuntime.Service.GetMission(characterId, "Mission:5514B19D");
            bool findDone = find != null && find.State == MissionLifecycleState.Completed;
            bool uplinkStarted = IsActiveOrOffered(uplink)
                                 || (uplink != null && uplink.State == MissionLifecycleState.Completed);
            bool deliverDone = deliver != null && deliver.State == MissionLifecycleState.Completed;
            return findDone && !deliverDone && !uplinkStarted;
        }

        private static bool IsAlexGibbsNpc(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected && target.Instance == AlexGibbsInstance)
            {
                return true;
            }

            if (source == null || source.Playfield == null)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            return npc != null && string.Equals(npc.Name, "Alex Gibbs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBioCom(IItem item)
        {
            return item != null
                   && (item.LowID == BioAnalyzingComputerItemId
                       || item.HighID == BioAnalyzingComputerItemId
                       || item.LowID == BioAnalyzingComputerItemHighId
                       || item.HighID == BioAnalyzingComputerItemHighId);
        }

        private static void BeginAlexTrade(ICharacter source, Identity alexIdentity)
        {
            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new AlexTradeSession
                                                                     {
                                                                         NpcIdentity = alexIdentity,
                                                                         StagedContainer = Identity.None
                                                                     };
            }
        }

        private static AlexTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                AlexTradeSession session;
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

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null && source.Controller is PlayerController && IsInAreteLanding(source);
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source != null
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static bool IsActiveOrOffered(ZoneEngine.Core.Missions.MissionStateRecord mission)
        {
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool IsTerminalFailure(MissionOperationResult result)
        {
            return result == null
                   || (result.Status != MissionOperationStatus.Applied
                       && result.Status != MissionOperationStatus.AlreadyApplied);
        }

        private static string EffectiveName(ICharacter character)
        {
            return character == null ? string.Empty : (character.Name ?? string.Empty).Trim();
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "FlintBioCom " + message);
        }
    }
}
