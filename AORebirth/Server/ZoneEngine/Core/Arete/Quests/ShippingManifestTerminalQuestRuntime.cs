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
                Log("smt-reroute ignored — Return to Vernon already progressed");
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
                // Capture: RejectedItems [] Unknown2=1 — Hacker Tool kept (inspect).
                try
                {
                    KnuBotRejectedItemsMessageHandler.Default.Send(source, terminalTarget, new Item[0], 1);
                }
                catch (Exception ex)
                {
                    Log("smt-rejecteditems failed: " + ex.Message);
                }

                ForgetTradeSession(source);
                try
                {
                    if (!ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, terminalTarget))
                    {
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
                    MissionRuntime.Service.OfferMission(
                        instance,
                        VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId);
                    MissionRuntime.Service.AcceptMission(
                        instance,
                        VernonGodfrayQuestRuntime.ReturnToVernonGodfrayQuestId);
                }
            }

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

        private static bool IsShippingManifestTerminal(ICharacter source, Identity target)
        {
            if (target.Type == IdentityType.CanbeAffected
                && target.Instance == ShippingManifestTerminalInstance)
            {
                return true;
            }

            if (source?.Playfield == null)
            {
                return false;
            }

            ICharacter npc = Pool.Instance.GetObject<ICharacter>(source.Playfield.Identity, target);
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
