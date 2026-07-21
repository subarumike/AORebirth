namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Rex→Marcus→Flint chain coordinator (20260719-Rex-Markus-stone).
    /// Owns phase resolution and atomic client handoffs so dialogue open/answer paths
    /// cannot re-inject leftover mission windows.
    /// </summary>
    public static class RexMarcusChainCoordinator
    {
        public const string RexReturnNodeId = "rex_194454_006";

        public const string MarcusFireRootNodeId = "marcus_195107_b18f_001";

        public const string MarcusReturnNodeId = "marcus_return_001";

        public const string MarcusReturnTradeNodeId = "marcus_return_trade";

        public const string MarcusPostCompleteNodeId = "marcus_return_003";

        public const string MarcusHealReturnNodeId = MarcusWoundedWorkersQuestRuntime.HealReturnNodeId;

        public const string MarcusHealTradeNodeId = MarcusWoundedWorkersQuestRuntime.HealTradeNodeId;

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const int CargoBoxInstance = unchecked((int)0x56D9B4AF);

        private const int CompactFireSuppressantItemId = 296780;

        // Capture 20260719-185137 / Rex-Markus-stone events.log:12197 — ID-card starter item after Accept.
        private const int MarcusReturnIdentityCardItemId = 296569;

        private const int MarcusReturnXpReward = 1281;

        private const int MarcusReturnCreditReward = 1080;

        // Capture events.log:12191-12192 wire body (do not invent English).
        private const string MarcusReturnRewardFeedback = "~&!!!\":$'O\"ui!!!0'i!!!-]~";

        private const string CargoRejectFeedback = "~&!!!\":!o[Im";

        private const string MarcusReturnCardGrantedFlag = "marcus-return-item-296569";

        private static readonly Dictionary<int, MarcusTradeSession> TradeSessionsByCharacter =
            new Dictionary<int, MarcusTradeSession>();

        private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

        private static readonly object TradeSyncRoot = new object();

        public static RexMarcusChainPhase GetPhase(ICharacter character)
        {
            if (character == null)
            {
                return RexMarcusChainPhase.None;
            }

            return GetPhase(character.Identity);
        }

        /// <summary>
        /// Client tip journal clears on every zone/relog. Re-emit Active Arete tips
        /// (Talk to Flint Novak + optional wounded-workers tips) from MissionRuntime.
        ///
        /// Important: GetPhase can be Flint when B196 is Completed even if Flint was never
        /// Offer/Accept'd into DB (QFU-only tip). Relog must ensure Flint is Active first.
        /// </summary>
        public static bool TryResendActiveTipsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            bool sent = ResendActiveTipsNow(source, "login-immediate");

            // Client often drops journal QFUs during FullCharacter; same delay pattern as nano restore.
            ICharacter captured = source;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        Thread.Sleep(900);
                        ResendActiveTipsNow(captured, "login-delayed");
                    }
                    catch (Exception e)
                    {
                        Log("login tip delayed resync failed: " + e.Message);
                    }
                });

            return sent;
        }

        private static bool ResendActiveTipsNow(ICharacter source, string trigger)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null
                || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            EnsureFlintPersistenceForChain(source);

            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB199QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);

            bool sent = false;

            // Find / Deliver / Surveillance / Plant / Bill / Kneecapping / Report tips.
            if (FlintBioComQuestRuntime.TryResendActiveTip(source))
            {
                sent = true;
            }
            else if (IsActiveOrOffered(flint))
            {
                // Main tip — Talk to Flint Novak must survive relog.
                ReanchorGameTimeForTipJournal(source);
                RexQuestPreviewEmissionResult flintResult = SafeQuestFullUpdateSender.TrySendFlintPreview(source);
                sent |= flintResult != null && flintResult.Emitted;
            }

            // Side tips stack beside Flint; never replace Flint.
            if (IsActiveOrOffered(b19a) && !MarcusWoundedWorkersQuestRuntime.HasCompletedStimReturn(source))
            {
                RexQuestPreviewEmissionResult b19aResult = SafeQuestFullUpdateSender.TrySendB19APreview(source);
                sent |= b19aResult != null && b19aResult.Emitted;
            }
            else if (IsActiveOrOffered(b199))
            {
                RexQuestPreviewEmissionResult b199Result = SafeQuestFullUpdateSender.TrySendB199Preview(source);
                sent |= b199Result != null && b199Result.Emitted;
            }

            Log(
                "tip resync trigger="
                + trigger
                + " character="
                + source.Identity.ToString(true)
                + " flintState="
                + (flint == null ? "missing" : flint.State.ToString())
                + " b199="
                + IsActiveOrOffered(b199)
                + " b19a="
                + IsActiveOrOffered(b19a)
                + " sent="
                + sent);

            return sent;
        }

        /// <summary>
        /// If Marcus suppressant return is done and Flint is not Completed, ensure Flint is Active
        /// so login can re-project Talk to Flint Novak.
        /// </summary>
        private static void EnsureFlintPersistenceForChain(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            if (IsCompleted(flint) || IsActiveOrOffered(flint))
            {
                if (flint != null && flint.State == MissionLifecycleState.Offered)
                {
                    MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexFlintQuestId);
                }

                return;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            bool needsFlint = IsCompleted(b196) || GetPhase(source) == RexMarcusChainPhase.Flint;
            if (!needsFlint)
            {
                return;
            }

            try
            {
                if (flint == null)
                {
                    MissionOperationResult offer = MissionRuntime.Service.OfferMission(
                        characterId,
                        MissionRuntime.RexFlintQuestId);
                    Log("ensure Flint Offer status=" + (offer == null ? "null" : offer.Status.ToString()));
                }

                MissionOperationResult accept = MissionRuntime.Service.AcceptMission(
                    characterId,
                    MissionRuntime.RexFlintQuestId);
                Log("ensure Flint Accept status=" + (accept == null ? "null" : accept.Status.ToString()));
            }
            catch (Exception e)
            {
                Log("ensure Flint persistence failed: " + e.Message);
            }
        }

        private static void ReanchorGameTimeForTipJournal(ICharacter source)
        {
            var client = source != null && source.Controller != null
                             ? source.Controller.Client as ZoneClient
                             : null;
            if (client == null || source == null)
            {
                return;
            }

            client.SendCompressed(
                new GameTimeMessage
                {
                    Identity =
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = source.Identity.Instance
                        },
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                });
            client.LastGameTimeSyncUtc = DateTime.UtcNow;
        }

        public static RexMarcusChainPhase GetPhase(Identity identity)
        {
            if (!MissionRuntime.IsInitialized
                || identity.Type != IdentityType.CanbeAffected
                || identity.Instance == 0)
            {
                return RexMarcusChainPhase.None;
            }

            int characterId = identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB199QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b194 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB194QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b18f =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18FQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b18e =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18EQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b18d =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18DQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b18c =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18CQuestId);

            // 1) Active/Offered tip wins first (downstream-first).
            // Wounded-workers is a side quest stacked beside Flint (main). Prefer B19A/B199 for
            // Marcus dialogue routing while that branch is active; Flint tip stays in the window.
            // Never let a *completed* later step mask an earlier *active* talk objective
            // (e.g. leftover B194 Completed must not open Thanks while B18F Talk to Marcus is Active).
            if (IsActiveOrOffered(b19a))
            {
                return RexMarcusChainPhase.ReturnMarcusStim;
            }

            if (IsActiveOrOffered(b199))
            {
                return RexMarcusChainPhase.HealWorkers;
            }

            if (IsActiveOrOffered(flint))
            {
                return RexMarcusChainPhase.Flint;
            }

            if (IsActiveOrOffered(b196))
            {
                return RexMarcusChainPhase.ReturnMarcus;
            }

            if (IsActiveOrOffered(b194))
            {
                return RexMarcusChainPhase.Extinguish;
            }

            if (IsActiveOrOffered(b18f))
            {
                return RexMarcusChainPhase.TalkMarcus;
            }

            if (IsActiveOrOffered(b18e))
            {
                return RexMarcusChainPhase.ReturnRex;
            }

            if (IsActiveOrOffered(b18d))
            {
                return RexMarcusChainPhase.Cargo;
            }

            if (IsActiveOrOffered(b18c))
            {
                return RexMarcusChainPhase.Robots;
            }

            // 2) Completed-only advancement when nothing is still Active/Offered.
            if (IsCompleted(flint))
            {
                return RexMarcusChainPhase.Done;
            }

            if (IsCompleted(b196))
            {
                return RexMarcusChainPhase.Flint;
            }

            if (IsCompleted(b194))
            {
                return RexMarcusChainPhase.ReturnMarcus;
            }

            if (IsCompleted(b18f))
            {
                return RexMarcusChainPhase.Extinguish;
            }

            if (IsCompleted(b18e))
            {
                return RexMarcusChainPhase.TalkMarcus;
            }

            if (IsCompleted(b18d))
            {
                return RexMarcusChainPhase.ReturnRex;
            }

            if (IsCompleted(b18c))
            {
                return RexMarcusChainPhase.Cargo;
            }

            return RexMarcusChainPhase.None;
        }

        public static string ResolveRexStartNodeId(ICharacter source)
        {
            RexMarcusChainPhase phase = GetPhase(source);
            if (phase == RexMarcusChainPhase.ReturnRex)
            {
                return RexReturnNodeId;
            }

            // TalkMarcus+ → idle/root; never re-QFU Talk to Marcus on open.
            return null;
        }

        public static string ResolveMarcusStartNodeId(ICharacter source)
        {
            // Cleanup stacked tips / stale Active B196 before choosing the node, otherwise
            // a dirty Return-to-Marcus tip wins over Return-to-Marcus-Stone (heal trade).
            CleanupStaleMarcusClientTips(source);
            EnsureReturnMarcusPersistence(source);

            RexMarcusChainPhase phase = GetPhase(source);
            switch (phase)
            {
                case RexMarcusChainPhase.ReturnMarcus:
                    return MarcusReturnNodeId;
                case RexMarcusChainPhase.TalkMarcus:
                    return MarcusFireRootNodeId;
                case RexMarcusChainPhase.ReturnMarcusStim:
                    return MarcusHealReturnNodeId;
                case RexMarcusChainPhase.HealWorkers:
                case RexMarcusChainPhase.Flint:
                case RexMarcusChainPhase.Done:
                    return MarcusPostCompleteNodeId;
                case RexMarcusChainPhase.Extinguish:
                    // After suppressant: idle root — post-complete looked like "quest over".
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Rex open: complete Return→Marcus on return-open. If already TalkMarcus+, re-project
        /// full Delete+B18F so a missed first-talk packet cannot require a second conversation.
        /// </summary>
        public static void OnRexOpen(ICharacter source, bool dialogueGateEnabled)
        {
            if (source == null)
            {
                return;
            }

            RexMarcusChainPhase phase = GetPhase(source);
            Log("rex-open phase=" + phase + " character=" + IdentityText(source));

            if (phase == RexMarcusChainPhase.ReturnRex)
            {
                RexB18ECompletionHandler.TryCompleteOnReturn(
                    source,
                    new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance },
                    dialogueGateEnabled);
                return;
            }

            if (phase >= RexMarcusChainPhase.TalkMarcus)
            {
                SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source);
            }
        }

        public static void OnMarcusOpen(ICharacter source)
        {
            RexMarcusChainPhase phase = GetPhase(source);
            Log("marcus-open phase=" + phase + " character=" + IdentityText(source));

            if (phase >= RexMarcusChainPhase.TalkMarcus)
            {
                SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
            }

            // Only sync Return-to-Marcus while that tip is still the active suppressant objective.
            // Never re-open B196 after suppressant turn-in / Flint / wounded-workers (capture 20260719-224226).
            EnsureReturnMarcusPersistence(source);
            CleanupStaleMarcusClientTips(source);

            // Prior generic Remove ate suppressant with no FinishTrade — repair on reopen.
            // Must not fire after B196 completed or during wounded-workers (was re-granting Unique 296569).
            if (ShouldRepairStolenSuppressantTurnIn(source))
            {
                Log("marcus-open stolen-suppressant repair character=" + IdentityText(source));
                ApplyMarcusTradeTurnIn(
                    source,
                    new Identity { Type = IdentityType.CanbeAffected, Instance = MarcusStoneInstance },
                    Identity.None,
                    "MarcusOpenRepair");
            }
        }

        /// <summary>
        /// Client tip cleanup when persistence already moved past suppressant return.
        /// Screenshot evidence: stacked "Return to Marcus" + "Return to Marcus Stone" + Flint.
        /// </summary>
        private static void CleanupStaleMarcusClientTips(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB199QuestId);
            RexMarcusChainPhase phase = GetPhase(source);

            // Suppressant return already done — never leave B196 tip stacked beside Flint/wounded tips.
            bool pastSuppressantReturn =
                phase == RexMarcusChainPhase.Flint
                || phase == RexMarcusChainPhase.HealWorkers
                || phase == RexMarcusChainPhase.ReturnMarcusStim
                || phase == RexMarcusChainPhase.Done
                || IsActiveOrOffered(b199)
                || IsActiveOrOffered(b19a)
                || IsCompleted(b199)
                || IsCompleted(b19a)
                || IsCompleted(b196);

            if (pastSuppressantReturn)
            {
                // Dirty persistence can still have B196 Active after wounded/Flint tips were offered
                // (screenshot: stacked Return to Marcus + Return to Marcus Stone + Flint).
                if (IsActiveOrOffered(b196)
                    && (IsActiveOrOffered(b199)
                        || IsActiveOrOffered(b19a)
                        || IsCompleted(b199)
                        || IsCompleted(b19a)
                        || phase == RexMarcusChainPhase.Flint
                        || phase == RexMarcusChainPhase.HealWorkers
                        || phase == RexMarcusChainPhase.ReturnMarcusStim
                        || phase == RexMarcusChainPhase.Done))
                {
                    try
                    {
                        MissionRuntime.Service.CompleteMission(characterId, MissionRuntime.RexB196QuestId);
                    }
                    catch (Exception e)
                    {
                        Log("cleanup stale B196 complete failed: " + e.Message);
                    }
                }

                SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
            }

            // Heal side-quest finished: delete Return to Marcus Stone tip only — never touch Flint.
            if (MarcusWoundedWorkersQuestRuntime.HasCompletedStimReturn(source) || IsCompleted(b19a))
            {
                if (IsActiveOrOffered(b19a))
                {
                    try
                    {
                        // Prefer abandon if objectives are incomplete; CompleteMission does not throw.
                        MissionRuntime.Service.AbandonMission(characterId, MissionRuntime.RexB19AQuestId);
                    }
                    catch (Exception e)
                    {
                        Log("cleanup finished B19A abandon failed: " + e.Message);
                    }
                }

                SafeQuestFullUpdateSender.TrySendB19ACompletionCleanup(source);
                SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
                return;
            }

            // Use Stim (B199) and Return to Marcus Stone (B19A) must never stack with each other —
            // B19A only after stim use. Flint (main) stays stacked beside either tip.
            if (phase == RexMarcusChainPhase.HealWorkers || IsActiveOrOffered(b199))
            {
                if (IsActiveOrOffered(b19a))
                {
                    try
                    {
                        MissionRuntime.Service.AbandonMission(characterId, MissionRuntime.RexB19AQuestId);
                    }
                    catch (Exception e)
                    {
                        Log("cleanup premature B19A abandon failed: " + e.Message);
                    }
                }

                SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
                SafeQuestFullUpdateSender.TrySendB199Preview(source);
            }
            else if (phase == RexMarcusChainPhase.ReturnMarcusStim || IsActiveOrOffered(b19a))
            {
                SafeQuestFullUpdateSender.TrySendB19APreview(source);
            }
        }

        private static void EnsureReturnMarcusPersistence(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            if (IsActiveOrOffered(b196))
            {
                return;
            }

            // Critical: completed suppressant return must never be re-offered (was stacking tips
            // and re-running turn-in → duplicate Unique Nano Transmitter 296569 every Marcus click).
            if (IsCompleted(b196))
            {
                return;
            }

            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB199QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            if (IsActiveOrOffered(flint)
                || IsCompleted(flint)
                || IsActiveOrOffered(b199)
                || IsCompleted(b199)
                || IsActiveOrOffered(b19a)
                || IsCompleted(b19a))
            {
                return;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b194 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB194QuestId);
            if (!IsCompleted(b194))
            {
                return;
            }

            MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB196QuestId);
            MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB196QuestId);
            SafeQuestFullUpdateSender.TrySendB194ToB196Handoff(source);
            Log("ensure-return-marcus character=" + IdentityText(source));
        }

        public static bool OnRexAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex,
            bool dialogueGateEnabled)
        {
            if (source == null)
            {
                return false;
            }

            // Accept robots → B18C once.
            if (string.Equals(previousNodeId, "rex_194454_004", StringComparison.OrdinalIgnoreCase)
                && answerIndex == 0)
            {
                RexQuestPreviewEmissionResult preview = RexQuestPreviewEmitter.TryEmitB18CPreview(
                    source,
                    new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance },
                    previousNodeId,
                    answerIndex,
                    dialogueGateEnabled);
                return preview != null && preview.Emitted;
            }

            // Return-path answers: complete/handoff again so one conversation always updates the window.
            if (IsRexReturnPathNode(previousNodeId) && answerIndex == 0)
            {
                RexMarcusChainPhase phase = GetPhase(source);
                if (phase == RexMarcusChainPhase.ReturnRex)
                {
                    RexB18ECompletionResult result = RexB18ECompletionHandler.TryCompleteOnReturn(
                        source,
                        new Identity { Type = IdentityType.CanbeAffected, Instance = RexLarssonInstance },
                        dialogueGateEnabled);
                    return result != null && result.Completed;
                }

                if (phase >= RexMarcusChainPhase.TalkMarcus)
                {
                    RexQuestPreviewEmissionResult handoff =
                        SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source);
                    return handoff != null && handoff.Emitted;
                }
            }

            return false;
        }

        private static bool IsRexReturnPathNode(string nodeId)
        {
            return string.Equals(nodeId, RexReturnNodeId, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rex_194454_007", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rex_194454_008", StringComparison.OrdinalIgnoreCase);
        }

        public static bool OnMarcusAnswer(
            ICharacter source,
            string previousNodeId,
            int answerIndex,
            string optionText,
            bool dialogueGateEnabled)
        {
            if (source == null)
            {
                return false;
            }

            if (MarcusWoundedWorkersQuestRuntime.TryHandleDialogueAnswer(
                source,
                previousNodeId,
                answerIndex))
            {
                return true;
            }

            RexMarcusChainPhase phase = GetPhase(source);

            // Fire handout only while Talk to Marcus is the active objective.
            if (phase != RexMarcusChainPhase.TalkMarcus)
            {
                return false;
            }

            MarcusB18FCompletionResult fire = MarcusB18FCompletionHandler.TryCompleteFromDialogue(
                source,
                new Identity { Type = IdentityType.CanbeAffected, Instance = MarcusStoneInstance },
                previousNodeId,
                answerIndex,
                optionText,
                dialogueGateEnabled);
            return fire != null && fire.Completed;
        }

        public static bool TryBeginMarcusReturnTrade(ICharacter source, Identity marcusIdentity)
        {
            if (source == null)
            {
                return false;
            }

            // Router already gated on Thanks from marcus_return_001. Do not block StartTrade on
            // phase drift — missing StartTrade leaves the player with no Accept path.
            if (marcusIdentity.Type != IdentityType.CanbeAffected || marcusIdentity.Instance == 0)
            {
                marcusIdentity = new Identity
                                {
                                    Type = IdentityType.CanbeAffected,
                                    Instance = MarcusStoneInstance
                                };
            }

            BeginMarcusTrade(source, marcusIdentity, MarcusTradeKind.Suppressant);
            KnuBotStartTradeMessageHandler.Default.Send(
                source,
                marcusIdentity,
                "Drag and drop the item(s) you want to give to Marcus Stone into one of the slots available and press \"accept\"",
                1);
            Log(
                "marcus-return-trade-opened character="
                + IdentityText(source)
                + " target="
                + marcusIdentity.ToString(true)
                + " phase="
                + GetPhase(source));
            return true;
        }

        public static void BeginMarcusTradeSession(
            ICharacter source,
            Identity marcusIdentity,
            MarcusTradeKind kind)
        {
            BeginMarcusTrade(source, marcusIdentity, kind);
        }

        public enum MarcusTradeKind
        {
            Suppressant = 0,
            Stim = 1
        }

        public static bool OnCargoUse(ICharacter source, Identity target)
        {
            if (!IsCargoBoxTarget(target) || source == null)
            {
                return false;
            }

            if (GetPhase(source) != RexMarcusChainPhase.Cargo)
            {
                return false;
            }

            RexB18DBoxProgressTracker.TryObserveBoxUse(source, target);
            return true;
        }

        public static bool TryRejectCargoWithoutQuest(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            if (client == null || message == null || !IsCargoBoxTarget(target))
            {
                return false;
            }

            ICharacter source = client.Controller != null ? client.Controller.Character : null;
            if (source == null || !(source.Controller is PlayerController))
            {
                return false;
            }

            if (source.Playfield == null
                || source.Playfield.Identity.Instance != AreteLandingPlayfieldId)
            {
                return false;
            }

            RexMarcusChainPhase phase = GetPhase(source);
            if (phase == RexMarcusChainPhase.Cargo)
            {
                return false;
            }

            // Capture 20260719-203251: Temp1=2 + FormatFeedback wire body (no invented English).
            try
            {
                if (source.Controller.Client != null)
                {
                    source.Controller.Client.SendCompressed(
                        new FormatFeedbackMessage
                        {
                            Identity = source.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            FormattedMessage = CargoRejectFeedback,
                            Unknown2 = 0
                        });
                }
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_REX_CARGO_REJECT feedback failed: " + e.Message);
            }

            GenericCmdMessageHandler.Default.AcknowledgeDenied(source, message);
            Log(
                "cargo-reject-without-quest character="
                + source.Identity.ToString(true)
                + " phase="
                + phase
                + " target="
                + target.ToString(true)
                + " feedback=\""
                + CargoRejectFeedback
                + "\"");
            return true;
        }

        public static bool TryStageMarcusTradeItem(ICharacter source, KnuBotTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            bool isMarcus = IsMarcusStoneNpc(source, message.Target);
            MarcusTradeSession session = GetTradeSession(source);
            // Only claim Marcus Stone trades. B196 returnTip / suppressant inventory must not steal
            // Alex BioCom Accept (ZoneEngineLog 2026-07-21 13:02:42 marcus-trade on Alex target).
            if (!isMarcus && session == null)
            {
                return false;
            }

            bool returnTip = IsMarcusReturnTip(source);
            bool stimReturnTip = MarcusWoundedWorkersQuestRuntime.IsStimReturnTip(source);
            bool stolenRepair = ShouldRepairStolenSuppressantTurnIn(source);
            IItem item = TryGetTradeContainerItem(source, message.Container);
            bool isSuppressant = IsSuppressantItem(item);
            bool isStim = MarcusWoundedWorkersQuestRuntime.IsHealthRegenStim(item);

            MarcusTradeKind kind = stimReturnTip || isStim
                                       ? MarcusTradeKind.Stim
                                       : MarcusTradeKind.Suppressant;
            if (session != null && session.Kind == MarcusTradeKind.Stim)
            {
                kind = MarcusTradeKind.Stim;
            }

            if (session == null)
            {
                BeginMarcusTrade(source, message.Target, kind);
                session = GetTradeSession(source);
            }
            else
            {
                session.NpcIdentity = message.Target;
                if (kind == MarcusTradeKind.Stim)
                {
                    session.Kind = MarcusTradeKind.Stim;
                }
            }

            if (session != null)
            {
                lock (TradeSyncRoot)
                {
                    session.StagedContainer = message.Container;
                    session.NpcIdentity = message.Target;
                    if (isSuppressant || stolenRepair)
                    {
                        session.HasSuppressant = true;
                    }

                    if (isStim || stimReturnTip)
                    {
                        session.HasStim = true;
                    }
                }
            }

            Log(
                "marcus-trade-stage character="
                + IdentityText(source)
                + " marcus="
                + isMarcus
                + " container="
                + message.Container.ToString(true)
                + " item="
                + (item == null ? "<null>" : (item.LowID + "/" + item.HighID))
                + " suppressant="
                + isSuppressant
                + " stim="
                + isStim
                + " kind="
                + kind
                + " returnTip="
                + returnTip
                + " stimReturnTip="
                + stimReturnTip
                + " stolenRepair="
                + stolenRepair
                + " phase="
                + GetPhase(source));

            // Capture 20260719-224226 stim return: wait for FinishTrade Accept — do not auto-take.
            if (kind == MarcusTradeKind.Stim || stimReturnTip || isStim)
            {
                return true;
            }

            // Suppressant path: private client often never Accepts — complete on drag to Marcus only.
            if (isMarcus && (isSuppressant || stolenRepair || returnTip || (session != null && session.HasSuppressant)))
            {
                ApplyMarcusTradeTurnIn(
                    source,
                    message.Target,
                    message.Container,
                    "KnuBotTrade");
            }

            return true;
        }

        public static bool TryFinishMarcusTrade(ICharacter source, KnuBotFinishTradeMessage message)
        {
            if (message == null || source == null)
            {
                return false;
            }

            bool isMarcus = IsMarcusStoneNpc(source, message.Target);
            MarcusTradeSession session = GetTradeSession(source);
            // Only claim Marcus Stone FinishTrade — never steal Alex/Bill/Stan Accept.
            if (!isMarcus && session == null)
            {
                return false;
            }

            bool returnTip = IsMarcusReturnTip(source);
            bool stimReturnTip = MarcusWoundedWorkersQuestRuntime.IsStimReturnTip(source);
            bool stolenRepair = ShouldRepairStolenSuppressantTurnIn(source);

            if (message.Decline != 0)
            {
                ForgetTradeSession(source);
                return true;
            }

            MarcusTradeKind kind = session != null
                                       ? session.Kind
                                       : (stimReturnTip ? MarcusTradeKind.Stim : MarcusTradeKind.Suppressant);
            if (session == null)
            {
                BeginMarcusTrade(source, message.Target, kind);
                session = GetTradeSession(source);
            }

            Identity staged = session == null ? Identity.None : session.StagedContainer;
            if (kind == MarcusTradeKind.Stim || stimReturnTip)
            {
                ApplyStimTradeTurnIn(source, message.Target, staged, "KnuBotFinishTrade");
            }
            else
            {
                ApplyMarcusTradeTurnIn(source, message.Target, staged, "KnuBotFinishTrade");
            }

            return true;
        }

        /// <summary>
        /// B194 done, Flint not started, suppressant already gone — prior Remove ate the turn-in.
        /// </summary>
        private static bool ShouldRepairStolenSuppressantTurnIn(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            RexMarcusChainPhase phase = GetPhase(source);
            if (phase == RexMarcusChainPhase.Flint
                || phase == RexMarcusChainPhase.Done
                || phase == RexMarcusChainPhase.HealWorkers
                || phase == RexMarcusChainPhase.ReturnMarcusStim)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b196 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId);
            if (IsCompleted(b196))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b199 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB199QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord b19a =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB19AQuestId);
            if (IsActiveOrOffered(b199)
                || IsCompleted(b199)
                || IsActiveOrOffered(b19a)
                || IsCompleted(b19a))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b194 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB194QuestId);
            if (!IsCompleted(b194) && phase < RexMarcusChainPhase.ReturnMarcus)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            if (IsActiveOrOffered(flint) || IsCompleted(flint))
            {
                return false;
            }

            // Already holding the Unique nano transmitter → suppressant turn-in already paid out.
            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                MarcusReturnIdentityCardItemId))
            {
                return false;
            }

            if (MissionRuntime.Service.GetFlag(
                    characterId,
                    MissionRuntime.RexB196QuestId,
                    MarcusReturnCardGrantedFlag) != null)
            {
                return false;
            }

            return !InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                CompactFireSuppressantItemId);
        }

        private static bool IsMarcusReturnTip(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            RexMarcusChainPhase phase = GetPhase(source);
            if (phase == RexMarcusChainPhase.ReturnMarcus)
            {
                return true;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            if (IsActiveOrOffered(
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB196QuestId)))
            {
                return true;
            }

            // B194 completed → return tip even before B196 persistence catches up.
            ZoneEngine.Core.Missions.MissionStateRecord b194 =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB194QuestId);
            ZoneEngine.Core.Missions.MissionStateRecord flint =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexFlintQuestId);
            return IsCompleted(b194) && !IsActiveOrOffered(flint) && !IsCompleted(flint);
        }

        private static bool IsSuppressantItem(IItem item)
        {
            return item != null
                   && (item.LowID == CompactFireSuppressantItemId
                       || item.HighID == CompactFireSuppressantItemId);
        }

        private static IItem TryGetTradeContainerItem(ICharacter source, Identity container)
        {
            if (source == null || source.BaseInventory == null || container.Type == IdentityType.None)
            {
                return null;
            }

            try
            {
                IInventoryPage page;
                if (!source.BaseInventory.Pages.TryGetValue((int)container.Type, out page) || page == null)
                {
                    return null;
                }

                return page[container.Instance];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ApplyMarcusTradeTurnIn(
            ICharacter source,
            Identity marcusTarget,
            Identity stagedContainer,
            string trigger)
        {
            if (source == null)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            lock (TradeSyncRoot)
            {
                if (TurnInInFlightByCharacter.Contains(characterId))
                {
                    return;
                }

                TurnInInFlightByCharacter.Add(characterId);
            }

            try
            {
                EnsureReturnMarcusPersistence(source);
                RexMarcusChainPhase phase = GetPhase(source);
                Log(
                    "marcus-trade-turnin begin character="
                    + IdentityText(source)
                    + " trigger="
                    + trigger
                    + " phase="
                    + phase
                    + " target="
                    + marcusTarget.ToString(true));

                TryConsumeSuppressant(source, stagedContainer);

                try
                {
                    KnuBotRejectedItemsMessageHandler.Default.Send(
                        source,
                        marcusTarget,
                        new Item[0],
                        0);
                }
                catch (Exception e)
                {
                    Log("marcus-trade-rejecteditems failed: " + e.Message);
                }

                CompleteMarcusReturnAndHandoffFlint(source);
                ForgetTradeSession(source);

                try
                {
                    ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, marcusTarget);
                }
                catch (Exception e)
                {
                    Log("marcus-trade-resume-dialogue failed: " + e.Message);
                }

                Log(
                    "marcus-trade-turnin done character="
                    + IdentityText(source)
                    + " trigger="
                    + trigger
                    + " phaseNow="
                    + GetPhase(source));
            }
            catch (Exception e)
            {
                Log("marcus-trade-turnin EXCEPTION: " + e);
                try
                {
                    SafeQuestFullUpdateSender.TrySendB196ToFlintHandoff(source);
                    TryGrantMarcusReturnIdentityCard(source);
                }
                catch (Exception inner)
                {
                    Log("marcus-trade-turnin recovery failed: " + inner.Message);
                }
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(characterId);
                }
            }
        }

        public static bool IsCargoBoxTarget(Identity target)
        {
            return target.Type == IdentityType.Terminal && target.Instance == CargoBoxInstance;
        }

        private static void CompleteMarcusReturnAndHandoffFlint(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (MissionRuntime.IsInitialized)
            {
                try
                {
                    int characterId = source.Identity.Instance;
                    string missionId = MissionRuntime.RexB196QuestId;
                    ZoneEngine.Core.Missions.MissionStateRecord mission =
                        MissionRuntime.Service.GetMission(characterId, missionId);
                    if (mission == null
                        || mission.State == MissionLifecycleState.Offered
                        || mission.State == MissionLifecycleState.Completed)
                    {
                        if (mission == null || mission.State == MissionLifecycleState.Offered)
                        {
                            MissionRuntime.Service.OfferMission(characterId, missionId);
                            MissionRuntime.Service.AcceptMission(characterId, missionId);
                            mission = MissionRuntime.Service.GetMission(characterId, missionId);
                        }
                    }

                    if (mission != null && mission.State == MissionLifecycleState.Active)
                    {
                        MissionRuntime.Service.ObserveObjective(
                            new MissionObjectiveObservation
                            {
                                CharacterId = characterId,
                                QuestId = missionId,
                                ObjectiveId = "mission_5514b196_objective_questfullupdate",
                                ObservationKey = "marcus-trade-suppressant",
                                Amount = 1,
                                EventType = "KnuBotFinishTrade",
                                SourceIdentity = source.Identity.ToString(true),
                                TargetIdentity = "SimpleChar:782DE567"
                            });
                        MissionRuntime.Service.CompleteMission(characterId, missionId);
                    }
                    else if (mission != null && mission.State != MissionLifecycleState.Completed)
                    {
                        MissionRuntime.Service.CompleteMission(characterId, missionId);
                    }

                    ForceCompleteIfNeeded(characterId, MissionRuntime.RexB18FQuestId);
                    ForceCompleteIfNeeded(characterId, MissionRuntime.RexB194QuestId);
                    ForceCompleteIfNeeded(characterId, MissionRuntime.RexB18EQuestId);

                    ApplyMarcusReturnRewards(source);
                    SendMarcusReturnRewardFeedback(source);

                    MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexFlintQuestId);
                    MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexFlintQuestId);
                    EnsureFlintPersistenceForChain(source);
                }
                catch (Exception e)
                {
                    Log("marcus-return persistence failed: " + e.Message);
                }
            }

            TryGrantMarcusReturnIdentityCard(source);

            // Capture 20260719-224226: Action59 + Delete Return to Marcus + QFU Flint — always project.
            RexQuestPreviewEmissionResult handoff =
                SafeQuestFullUpdateSender.TrySendB196ToFlintHandoff(source);
            if (handoff == null || !handoff.Emitted)
            {
                SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
                SafeQuestFullUpdateSender.TrySendB196CompletionCleanup(source);
                SafeQuestFullUpdateSender.TrySendFlintPreview(source);
            }

            Log(
                "marcus-return-trade-complete character="
                + source.Identity.ToString(true)
                + " flintProjected="
                + (handoff != null && handoff.Emitted));
        }

        /// <summary>
        /// Capture 20260719-185137 events.log:12197-12203 — TemplateAction 296569 to OverflowWindow,
        /// ContainerAddItem slot 111, Feedback category 110 / message 108871108.
        /// </summary>
        private static void TryGrantMarcusReturnIdentityCard(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(
                    characterId,
                    MissionRuntime.RexB196QuestId,
                    MarcusReturnCardGrantedFlag) != null)
            {
                // Unique quest item — never re-project TemplateAction on reopen (was duplicating visually
                // and, when Unique failed, adding real inventory copies on every Marcus click).
                return;
            }

            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                source,
                MarcusReturnIdentityCardItemId))
            {
                MissionRuntime.Service.SetFlag(
                    characterId,
                    MissionRuntime.RexB196QuestId,
                    MarcusReturnCardGrantedFlag,
                    "item:" + MarcusReturnIdentityCardItemId);
                return;
            }

            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null
                || !ItemLoader.ItemList.ContainsKey(MarcusReturnIdentityCardItemId))
            {
                Log(
                    "marcus-return-card-grant skipped character="
                    + IdentityText(source)
                    + " reason=inventory-or-itemloader");
                return;
            }

            Item item;
            try
            {
                item = new Item(1, MarcusReturnIdentityCardItemId, MarcusReturnIdentityCardItemId);
                if (item.MultipleCount < 1)
                {
                    item.MultipleCount = 1;
                }
            }
            catch (Exception e)
            {
                Log("marcus-return-card-create failed: " + e.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "marcus-return-card-grant failed character="
                    + IdentityText(source)
                    + " status="
                    + grant.Status);
                return;
            }

            SendMarcusReturnIdentityCardPackets(source, MarcusReturnIdentityCardItemId);
            MissionRuntime.Service.SetFlag(
                characterId,
                MissionRuntime.RexB196QuestId,
                MarcusReturnCardGrantedFlag,
                "item:" + MarcusReturnIdentityCardItemId);
            Log(
                "marcus-return-card-granted character="
                + IdentityText(source)
                + " item="
                + MarcusReturnIdentityCardItemId);
        }

        private static void SendMarcusReturnIdentityCardPackets(ICharacter source, int itemId)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = itemId,
                    ItemHighId = itemId,
                    Quality = 1,
                    Unknown1 = 1,
                    Unknown2 = 87,
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
                    TargetPlacement = 0x6F
                });
            FeedbackMessageHandler.Default.Send(source, 110, 108871108);
        }

        private static void ApplyMarcusReturnRewards(ICharacter source)
        {
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = "captured-marcus-return-xp-credits",
                                 RewardType = "character-stats",
                                 IsResolved = true,
                                 StatMutations = new[]
                                                 {
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.cash,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = MarcusReturnCreditReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.xp,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = MarcusReturnXpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.unsavedxp,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = MarcusReturnXpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.lastxp,
                                                         Kind = MissionStatMutationKind.Set,
                                                         Value = MarcusReturnXpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     }
                                                 }
                             };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                MissionRuntime.RexB196QuestId,
                definition,
                "capture:20260719-Rex-Markus-stone:marcus-b196-xp-credits");
            if (result.Succeeded && result.StatValues != null)
            {
                foreach (MissionCharacterStatValue statValue in result.StatValues)
                {
                    uint value = statValue.Value <= 0
                                     ? 0
                                     : (uint)Math.Min(statValue.Value, uint.MaxValue);
                    source.Stats[(StatIds)statValue.StatId].Set(value);
                }

                StatMessageHandler.Default.SendChanged(source);
            }
        }

        private static void SendMarcusReturnRewardFeedback(ICharacter source)
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
                        FormattedMessage = MarcusReturnRewardFeedback,
                        Unknown2 = 0
                    });
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_MARCUS_RETURN reward feedback failed: " + e.Message);
            }
        }

        private static void TryConsumeSuppressant(ICharacter source, Identity stagedContainer)
        {
            if (source == null || source.BaseInventory == null)
            {
                return;
            }

            if (stagedContainer.Type != IdentityType.None && stagedContainer.Instance > 0)
            {
                IInventoryPage stagedPage;
                if (source.BaseInventory.Pages.TryGetValue((int)stagedContainer.Type, out stagedPage)
                    && stagedPage != null)
                {
                    IItem staged = stagedPage[stagedContainer.Instance];
                    if (staged != null
                        && (staged.LowID == CompactFireSuppressantItemId
                            || staged.HighID == CompactFireSuppressantItemId))
                    {
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
                }
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in source.BaseInventory.Pages)
            {
                IInventoryPage page = pageEntry.Value;
                if (page == null)
                {
                    continue;
                }

                foreach (KeyValuePair<int, IItem> entry in page.List())
                {
                    IItem item = entry.Value;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.LowID != CompactFireSuppressantItemId
                        && item.HighID != CompactFireSuppressantItemId)
                    {
                        continue;
                    }

                    page.Remove(entry.Key);
                    try
                    {
                        if (source.BaseInventory.Write())
                        {
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                source,
                                pageEntry.Key,
                                entry.Key);
                            return;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    page.Add(entry.Key, item);
                }
            }
        }

        private static void ApplyStimTradeTurnIn(
            ICharacter source,
            Identity marcusTarget,
            Identity stagedContainer,
            string trigger)
        {
            if (source == null)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            lock (TradeSyncRoot)
            {
                if (TurnInInFlightByCharacter.Contains(characterId))
                {
                    return;
                }

                TurnInInFlightByCharacter.Add(characterId);
            }

            try
            {
                MarcusWoundedWorkersQuestRuntime.CompleteStimReturnTurnIn(
                    source,
                    marcusTarget,
                    stagedContainer,
                    trigger);
                ForgetTradeSession(source);
            }
            finally
            {
                lock (TradeSyncRoot)
                {
                    TurnInInFlightByCharacter.Remove(characterId);
                }
            }
        }

        private static void BeginMarcusTrade(
            ICharacter source,
            Identity marcusIdentity,
            MarcusTradeKind kind)
        {
            if (source == null || source.Identity.Instance <= 0)
            {
                return;
            }

            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter[source.Identity.Instance] = new MarcusTradeSession
                                                                   {
                                                                       NpcIdentity = marcusIdentity,
                                                                       Kind = kind
                                                                   };
            }
        }

        private static MarcusTradeSession GetTradeSession(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            MarcusTradeSession session;
            lock (TradeSyncRoot)
            {
                TradeSessionsByCharacter.TryGetValue(source.Identity.Instance, out session);
            }

            return session;
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

        private static void ForceCompleteIfNeeded(int characterId, string questId)
        {
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

            string objectiveId = "mission_"
                                 + questId.Replace("Mission:", string.Empty).ToLowerInvariant()
                                 + "_objective_questfullupdate";
            MissionRuntime.Service.ObserveObjective(
                new MissionObjectiveObservation
                {
                    CharacterId = characterId,
                    QuestId = questId,
                    ObjectiveId = objectiveId,
                    ObservationKey = "rex-marcus-chain-force-complete",
                    Amount = 1,
                    EventType = "ChainCoordinator",
                    SourceIdentity = string.Empty,
                    TargetIdentity = string.Empty
                });
            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static bool IsActiveOrOffered(ZoneEngine.Core.Missions.MissionStateRecord mission)
        {
            return mission != null
                   && (mission.State == MissionLifecycleState.Offered
                       || mission.State == MissionLifecycleState.Active);
        }

        private static bool IsCompleted(ZoneEngine.Core.Missions.MissionStateRecord mission)
        {
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        /// <summary>
        /// Live Arete Marcus uses a dynamic spawn id; dossier 782DE567 is only the content key.
        /// Match dossier or live name — never require an existing trade session.
        /// </summary>
        private static bool IsMarcusStoneNpc(ICharacter source, Identity identity)
        {
            if (identity.Type != IdentityType.CanbeAffected || identity.Instance == 0)
            {
                return false;
            }

            if (identity.Instance == MarcusStoneInstance)
            {
                return true;
            }

            if (source == null || source.Playfield == null)
            {
                return false;
            }

            try
            {
                ICharacter npc = AORebirth.ObjectManager.Pool.Instance.GetObject<ICharacter>(
                    source.Playfield.Identity,
                    identity);
                return npc != null
                       && !string.IsNullOrWhiteSpace(npc.Name)
                       && npc.Name.IndexOf("Marcus Stone", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string IdentityText(ICharacter source)
        {
            return source == null ? "<null>" : source.Identity.ToString(true);
        }

        private static void Log(string message)
        {
            // Error channel so turn-in appears in ZoneEngine.err/out during smoke.
            LogUtil.Debug(DebugInfoDetail.Error, "ARETE_REX_MARCUS_CHAIN " + message);
        }

        private sealed class MarcusTradeSession
        {
            public Identity NpcIdentity { get; set; }

            public Identity StagedContainer { get; set; }

            public bool HasSuppressant { get; set; }

            public bool HasStim { get; set; }

            public MarcusTradeKind Kind { get; set; }
        }
    }

    public enum RexMarcusChainPhase
    {
        None = 0,
        Robots = 1,
        Cargo = 2,
        ReturnRex = 3,
        TalkMarcus = 4,
        Extinguish = 5,
        ReturnMarcus = 6,
        HealWorkers = 7,
        ReturnMarcusStim = 8,
        Flint = 9,
        Done = 10
    }
}
