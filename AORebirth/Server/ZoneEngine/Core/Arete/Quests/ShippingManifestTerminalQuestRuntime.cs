namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
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
    /// Capture 20260721-Vernon-Godfray: Shipping Manifest Terminal (78E0FC6A)
    /// Access → Apply Hacker Tool → StartTrade inspect/keep → Re-route → Return to Vernon tip.
    /// </summary>
    public static class ShippingManifestTerminalQuestRuntime
    {
        public const string ApplyHackerToolNodeId = "smt_002";

        public const string TradeHoldNodeId = "smt_trade";

        public const string ReRouteNodeId = "smt_003";

        private const int ShippingManifestTerminalInstance = unchecked((int)0x78E0FC6A);

        private const int HackerToolItemId = VernonGodfrayCombineRules.HackerToolItemId;

        private const int CapturedTradeSlotCount = 1;

        // Vernon / brain inspect redraw (TemplateAction Unknown1/2 + Overflow slot).
        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string TerminalTradePrompt =
            "Drag and drop the item(s) you want to give to Shipping Manifest Terminal into one of the slots available and press \"accept\"";

        private static readonly object TradeSyncRoot = new object();

        private static readonly Dictionary<int, TerminalTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, TerminalTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private sealed class TerminalTradeSession
        {
            public Identity NpcIdentity;

            public Identity StagedContainer;
        }

        public static bool TryBeginTerminalTrade(ICharacter source, Identity terminalIdentity)
        {
            if (source == null)
            {
                return false;
            }

            if (!IsCargoLiftingActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            if (terminalIdentity.Type != IdentityType.CanbeAffected || terminalIdentity.Instance == 0)
            {
                terminalIdentity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = ShippingManifestTerminalInstance
                                  };
            }

            BeginTrade(source, terminalIdentity);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                terminalIdentity,
                TerminalTradePrompt,
                CapturedTradeSlotCount);
            Log("smt-start-trade character=" + source.Identity.ToString(true));
            return true;
        }

        public static bool TryStageTerminalTradeItem(ICharacter character, KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsShippingManifestTerminal(character, message.Target))
            {
                return false;
            }

            if (!IsCargoLiftingActive(character) && GetTradeSession(character) == null)
            {
                return false;
            }

            BeginTrade(character, message.Target);
            TerminalTradeSession session = GetTradeSession(character);
            if (session == null)
            {
                return true;
            }

            session.NpcIdentity = message.Target;
            if (message.Container.Type != IdentityType.None && message.Container.Instance >= 0)
            {
                session.StagedContainer = message.Container;
                Log(
                    "smt-trade-staged character="
                    + character.Identity.ToString(true)
                    + " container="
                    + message.Container.ToString(true));
            }

            return true;
        }

        public static bool ShouldSuppressGenericTerminalTradeRemove(
            ICharacter character,
            KnuBotTradeMessage message)
        {
            if (character == null || message == null || !IsShippingManifestTerminal(character, message.Target))
            {
                return false;
            }

            return IsCargoLiftingActive(character) || GetTradeSession(character) != null;
        }

        public static bool TryFinishTerminalTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (source == null || message == null || !IsShippingManifestTerminal(source, message.Target))
            {
                return false;
            }

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            if (!IsCargoLiftingActive(source) && GetTradeSession(source) == null)
            {
                return false;
            }

            ApplyTerminalHackInspect(source, message.Target);
            return true;
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0)
            {
                return false;
            }

            if (!string.Equals(previousNodeId, ReRouteNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsMissionLifecycle(source, VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId, true, true))
            {
                // Capture 20260801-105429: Cargo tip must still be deleted even if Return
                // was already offered (otherwise journal keeps both tips).
                SafeQuestFullUpdateSender.TrySendCargoLiftingToReturnVernonHandoff(source);
                Log("smt-reroute — Return already progressed; force Cargo tip delete");
                return true;
            }

            CompleteCargoAndOfferReturn(source);
            return true;
        }

        private static void ApplyTerminalHackInspect(ICharacter source, Identity terminalTarget)
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
                // Capture 20260802-cargo-lift: RejectedItems [] Unknown2=1 — Hacker Tool kept.
                try
                {
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, terminalTarget, new Item[0], 1);
                }
                catch (Exception ex)
                {
                    Log("smt-rejecteditems failed: " + ex.Message);
                }

                // Empty RejectedItems matches capture but client trade chrome still hides the
                // icon — Vernon/brain Overflow TemplateAction redraw restores Hacker Tool.
                TryForceReturnHackerTool(source);

                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, terminalTarget))
                    {
                        Log("smt-resume failed — CloseChat (no Re-route); character="
                            + source.Identity.ToString(true)
                            + " target="
                            + terminalTarget.ToString(true));
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, terminalTarget);
                    }
                }
                catch (Exception ex)
                {
                    Log("smt-resume-dialogue failed: " + ex.Message);
                    try
                    {
                        KnuBotCloseChatWindowMessageHandler.Default.Send(source, terminalTarget);
                    }
                    catch
                    {
                    }
                }

                Log("smt-hack-inspect done character=" + source.Identity.ToString(true));
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(instance);
                }
            }
        }

        private static void CompleteCargoAndOfferReturn(ICharacter source)
        {
            int instance = source.Identity.Instance;
            if (MissionRuntime.IsInitialized)
            {
                ForceCompleteHandoffTip(
                    instance,
                    VernonGodfrayQuestRuntime.CargoLiftingQuestId,
                    "mission_555BE9FA_cargo_lifting");
                MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(
                    instance,
                    VernonGodfrayQuestRuntime.CargoLiftingQuestId,
                    VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId);
                if (result.Status != MissionOperationStatus.Applied
                    && result.Status != MissionOperationStatus.AlreadyApplied)
                {
                    // Ensure Cargo is completed even when activate-next fails, so login sync
                    // does not re-emit the Cargo Lifting tip beside Return to Vernon.
                    MissionRuntime.Service.CompleteMission(
                        instance,
                        VernonGodfrayQuestRuntime.CargoLiftingQuestId);
                    MissionRuntime.Service.OfferMission(
                        instance,
                        VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId);
                    MissionRuntime.Service.AcceptMission(
                        instance,
                        VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId);
                }
            }

            // Capture 20260801-105429: Quest Delete Mission Cargo, then QFU Return to Vernon.
            SafeQuestFullUpdateSender.TrySendCargoLiftingToReturnVernonHandoff(source);
            Log("smt-cargo-complete→return-vernon character=" + source.Identity.ToString(true));
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
                        ObservationKey = "shipping-manifest-force-complete",
                        Amount = 1,
                        EventType = "ShippingManifestTerminalQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static bool IsCargoLiftingActive(ICharacter source)
        {
            return IsMissionLifecycle(source, VernonGodfrayQuestRuntime.CargoLiftingQuestId, true, false);
        }

        private static void TryForceReturnHackerTool(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            bool hasTool = InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                           && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                               source,
                               HackerToolItemId);
            if (!hasTool)
            {
                try
                {
                    Item item = new Item(1, HackerToolItemId, HackerToolItemId);
                    QuestRewardInventoryGrantResult grant =
                        InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
                    if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                    {
                        Log(
                            "smt-force-return-hacker grant failed status="
                            + grant.Status
                            + " invErr="
                            + grant.InventoryError);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log("smt-force-return-hacker grant failed: " + ex.Message);
                    return;
                }

                Log("smt-force-return-hacker grant character=" + source.Identity.ToString(true));
            }

            SendOverflowGrantPackets(source, HackerToolItemId, 1);
            Log("smt-force-return-hacker refresh character=" + source.Identity.ToString(true));
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

        private static bool IsShippingManifestTerminal(ICharacter source, Identity target)
        {
            if (target.Type != IdentityType.CanbeAffected || target.Instance == 0)
            {
                return false;
            }

            if (target.Instance == ShippingManifestTerminalInstance)
            {
                return true;
            }

            int poolInstance;
            if (AORebirth.Core.Playfields.AreteLandingSpawn.TryGetLivingPoolInstance(
                    ShippingManifestTerminalInstance,
                    out poolInstance)
                && poolInstance != 0
                && target.Instance == poolInstance)
            {
                return true;
            }

            if (source?.Playfield == null)
            {
                return false;
            }

            ICharacter npc = null;
            try
            {
                npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
            }
            catch
            {
                npc = null;
            }

            return npc != null
                   && string.Equals(npc.Name, "Shipping Manifest Terminal", StringComparison.OrdinalIgnoreCase);
        }

        private static void BeginTrade(ICharacter source, Identity terminalIdentity)
        {
            if (source == null)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new TerminalTradeSession
                                                                    {
                                                                        NpcIdentity = terminalIdentity,
                                                                        StagedContainer = Identity.None
                                                                    };
            }
        }

        private static TerminalTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            lock (TradeSyncRoot)
            {
                TerminalTradeSession session;
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
            try
            {
                LogUtil.Debug(DebugInfoDetail.Engine, "ShippingManifestTerminalQuestRuntime " + message);
            }
            catch
            {
            }
        }
    }
}
