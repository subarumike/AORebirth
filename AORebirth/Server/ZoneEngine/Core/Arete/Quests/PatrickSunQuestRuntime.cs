namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Capture 20260801-Patrick Sun: Patrick Sun insurance / kill / yell side quest on Arete Landing.
    /// Tips Mission:5565C962 → 5565C963 → 5565C966; finish 2596 XP, 2200 credits,
    /// Belt Component Platform 300X (36782/36777 ql30).
    /// </summary>
    public static class PatrickSunQuestRuntime
    {
        public const string RootNodeId = "patrick_001";

        public const string AcceptNodeId = "patrick_006_accept";

        public const string ScannerRootNodeId = "patrick_scanner_001";

        public const string YellRootNodeId = "patrick_yell_001";

        public const string YellRewardNodeId = "patrick_yell_reward";

        public const string InsuranceQuestId = "Mission:5565C962";

        public const string TalkQuestId = "Mission:5565C963";

        public const string YellQuestId = "Mission:5565C966";

        // Capture 20260801-Patrick Sun live Terminal:574187D0; playfields.dat Terminal:C00D1999 tpl=300813.
        public const int CellScannerTerminalInstance = AreteCellStructureScannerSave.LiveTerminalInstance;

        public const int CellScannerPlayfieldStatelInstance =
            AreteCellStructureScannerSave.PlayfieldStatelInstance;

        public const int CellScannerTemplateId = AreteCellStructureScannerSave.TemplateId;

        private const int PatrickSunInstance = unchecked((int)0x78E0FC7B);

        private const int AreteLandingPlayfieldId = 6553;

        // Capture TemplateAction LowId=36782 HighId=36777 Quality=30.
        private const int BeltLowId = 36782;

        private const int BeltHighId = 36777;

        private const int BeltQuality = 30;

        private const int FinishXpReward = 2596;

        private const int FinishCreditReward = 2200;

        // Capture FormatFeedback Message wire for "Received reward: 2596 XP, 2200 credits."
        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!?Oi!!!:l~";

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string RewardsGrantedFlag = "patrick-sun-rewards-granted";

        private const string InsurancePendingFlag = "patrick-insurance-pending";

        private const string TalkPendingFlag = "patrick-talk-pending";

        private const string YellPendingFlag = "patrick-yell-pending";

        private static readonly object TipSyncRoot = new object();

        private static readonly HashSet<int> InsurancePendingByCharacter = new HashSet<int>();

        private static readonly HashSet<int> TalkPendingByCharacter = new HashSet<int>();

        private static readonly HashSet<int> YellPendingByCharacter = new HashSet<int>();

        public static string ResolvePatrickStartNodeId(ICharacter source)
        {
            if (source == null || !IsInAreteLanding(source))
            {
                return null;
            }

            if (HasCompletedPatrickQuest(source))
            {
                // Capture: quest is one-shot after yell rewards; still allow greeting, no tip restart.
                return RootNodeId;
            }

            if (HasPendingYellTurnIn(source))
            {
                // Capture after death: tip stays on journal; re-assert live AbsoluteTime so Remain is valid.
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                    source,
                    PatrickSunTipSender.YellTipInstance);
                EnsureQuestActive(source, YellQuestId);
                MarkYellPending(source);
                PatrickSunTipSender.TrySendYellTipOnly(source);
                return YellRootNodeId;
            }

            if (HasPendingTalkTip(source))
            {
                return ScannerRootNodeId;
            }

            // Heal: insurance tip was offered before pending flags existed (Mike 20260801).
            if (HasMissionOfferedOrActive(source, InsuranceQuestId))
            {
                MarkInsurancePending(source);
            }

            return RootNodeId;
        }

        public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
        {
            if (source == null || answerIndex != 0 || string.IsNullOrEmpty(previousNodeId))
            {
                return false;
            }

            if (string.Equals(previousNodeId, "patrick_005", StringComparison.OrdinalIgnoreCase))
            {
                StartInsuranceTip(source);
                return true;
            }

            if (string.Equals(previousNodeId, ScannerRootNodeId, StringComparison.OrdinalIgnoreCase))
            {
                // Capture 20260801-Patrick Sun:
                // Answer Cell Scanner → AppendText "Excellent..." → Talk tip delete + Yell QFU
                // → AnswerList Goodbye → HealthDamage/Death (4355 ms later, before player clicks Goodbye).
                ClearTalkPending(source);
                MarkYellPending(source);
                EnsureQuestActive(source, YellQuestId);
                CompleteAndActivate(source, TalkQuestId, YellQuestId, "mission_5565C963_talk_patrick");
                EnsureQuestActive(source, YellQuestId);
                PatrickSunTipSender.TrySendTalkToYellHandoff(source);
                SchedulePatrickKill(source);
                Log("talk→yell handoff + scheduled kill character=" + source.Identity.ToString(true));
                return true;
            }

            // Capture: rewards land with Relax/belt AppendText after ICC-guard answer (yell_003→reward).
            // Only complete on yell_003 — reward-node Goodbye must not grant a second time.
            if (string.Equals(previousNodeId, "patrick_yell_003", StringComparison.OrdinalIgnoreCase))
            {
                CompleteYellQuest(source);
                return true;
            }

            return false;
        }

        public static bool TryHandleInsuranceTerminalUse(ICharacter source, Identity target)
        {
            if (source == null
                || !AreteCellStructureScannerSave.IsTarget(target)
                || !IsInAreteLanding(source))
            {
                return false;
            }

            // Log before save — prior builds threw inside TrySave and never ACKed (client Use spam).
            Log(
                "cell scanner Use begin character="
                + source.Identity.ToString(true)
                + " target="
                + target.ToString(true));

            bool saved = false;
            try
            {
                // Capture 20260801-091856 / 20260801-Patrick Sun: SaveChar first, then tip handoff.
                saved = AreteCellStructureScannerSave.TrySave(source);
            }
            catch (Exception ex)
            {
                Log("cell scanner SaveChar exception: " + ex);
            }

            bool pendingInsurance = HasPendingInsuranceTip(source);
            if (pendingInsurance && saved)
            {
                try
                {
                    // Capture replay: Quest Delete insurance tip → QFU "Talk to Patrick Sun".
                    ClearInsurancePending(source);
                    MarkTalkPending(source);
                    CompleteAndActivate(source, InsuranceQuestId, TalkQuestId, "mission_5565C962_use_scanner");
                    PatrickSunTipSender.TrySendInsuranceToTalkHandoff(source);
                    Log(
                        "insurance→talk handoff character="
                        + source.Identity.ToString(true)
                        + " terminal="
                        + target.ToString(true));
                }
                catch (Exception ex)
                {
                    Log("insurance→talk handoff exception: " + ex);
                }
            }
            else
            {
                Log(
                    "cell scanner use character="
                    + source.Identity.ToString(true)
                    + " saved="
                    + saved
                    + " pendingInsurance="
                    + pendingInsurance
                    + " target="
                    + target.ToString(true));
            }

            // Always claim the Use so GenericCmd ACK is sent (stops client retry spam).
            return true;
        }

        private static bool HasCompletedPatrickQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       YellQuestId,
                       RewardsGrantedFlag) != null;
        }

        private static bool HasPendingInsuranceTip(ICharacter source)
        {
            if (source == null || HasCompletedPatrickQuest(source) || HasPendingTalkTip(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            lock (TipSyncRoot)
            {
                if (InsurancePendingByCharacter.Contains(characterId))
                {
                    return true;
                }
            }

            if (MissionRuntime.IsInitialized
                && HasTipFlag(source, InsuranceQuestId, InsurancePendingFlag))
            {
                return true;
            }

            // Do not treat Completed insurance as blocking — Offer stays AlreadyApplied and tip
            // wire can still be live while MissionRuntime never tracked Active (Mike 20260801).
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.IsInitialized
                    ? MissionRuntime.Service.GetMission(characterId, InsuranceQuestId)
                    : null;
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool HasPendingTalkTip(ICharacter source)
        {
            if (source == null || HasCompletedPatrickQuest(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            lock (TipSyncRoot)
            {
                if (TalkPendingByCharacter.Contains(characterId))
                {
                    return true;
                }
            }

            if (MissionRuntime.IsInitialized
                && HasTipFlag(source, TalkQuestId, TalkPendingFlag))
            {
                return true;
            }

            return IsMissionActive(source, TalkQuestId) && !IsMissionCompleted(source, TalkQuestId);
        }

        private static void StartInsuranceTip(ICharacter source)
        {
            if (source == null || HasCompletedPatrickQuest(source))
            {
                Log(
                    "insurance tip skipped completed character="
                    + (source == null ? "<null>" : source.Identity.ToString(true)));
                return;
            }

            if (HasPendingTalkTip(source) || HasPendingYellTurnIn(source))
            {
                Log(
                    "insurance tip skipped already-advanced character="
                    + source.Identity.ToString(true));
                return;
            }

            MarkInsurancePending(source);
            if (MissionRuntime.IsInitialized)
            {
                int characterId = source.Identity.Instance;
                MissionRuntime.Service.OfferMission(characterId, InsuranceQuestId);
                MissionRuntime.Service.AcceptMission(characterId, InsuranceQuestId);
                TrySetTipFlag(source, InsuranceQuestId, InsurancePendingFlag);
            }

            PatrickSunTipSender.TrySendInsuranceTipOnly(source);
            Log("insurance tip started character=" + source.Identity.ToString(true));
        }

        private static void MarkInsurancePending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                InsurancePendingByCharacter.Add(source.Identity.Instance);
                TalkPendingByCharacter.Remove(source.Identity.Instance);
                YellPendingByCharacter.Remove(source.Identity.Instance);
            }
        }

        private static void ClearInsurancePending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                InsurancePendingByCharacter.Remove(source.Identity.Instance);
            }

            TrySetTipFlag(source, InsuranceQuestId, InsurancePendingFlag, "0");
        }

        private static void MarkTalkPending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                TalkPendingByCharacter.Add(source.Identity.Instance);
                InsurancePendingByCharacter.Remove(source.Identity.Instance);
            }

            TrySetTipFlag(source, TalkQuestId, TalkPendingFlag);
        }

        private static void ClearTalkPending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                TalkPendingByCharacter.Remove(source.Identity.Instance);
            }

            TrySetTipFlag(source, TalkQuestId, TalkPendingFlag, "0");
        }

        private static void MarkYellPending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                YellPendingByCharacter.Add(source.Identity.Instance);
                TalkPendingByCharacter.Remove(source.Identity.Instance);
                InsurancePendingByCharacter.Remove(source.Identity.Instance);
            }

            TrySetTipFlag(source, YellQuestId, YellPendingFlag);
        }

        private static void ClearYellPending(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            lock (TipSyncRoot)
            {
                YellPendingByCharacter.Remove(source.Identity.Instance);
            }

            TrySetTipFlag(source, YellQuestId, YellPendingFlag, "0");
        }

        private static bool HasMissionOfferedOrActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null
                   && (mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Offered);
        }

        private static bool HasTipFlag(ICharacter source, string questId, string flagKey)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(flagKey))
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionFlagRecord flag =
                MissionRuntime.Service.GetFlag(source.Identity.Instance, questId, flagKey);
            return flag != null && string.Equals(flag.Value, "1", StringComparison.Ordinal);
        }

        private static void TrySetTipFlag(
            ICharacter source,
            string questId,
            string flagKey,
            string value = "1")
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

            try
            {
                int characterId = source.Identity.Instance;
                MissionRuntime.Service.OfferMission(characterId, questId);
                MissionRuntime.Service.AcceptMission(characterId, questId);
                MissionRuntime.Service.SetFlag(characterId, questId, flagKey, value);
            }
            catch (Exception ex)
            {
                Log("tip flag persist skipped key=" + flagKey + " err=" + ex.Message);
            }
        }

        // Capture 20260801-Patrick Sun: Cell Scanner answer @07:02:11.003 → HealthDamage @07:02:15.358 (4355 ms).
        // Tip/Yell lands first; death follows while Goodbye options are on screen.
        private const int PatrickKillDelayMilliseconds = 4355;

        private static void SchedulePatrickKill(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            ICharacter captured = source;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    try
                    {
                        Thread.Sleep(PatrickKillDelayMilliseconds);
                        KillPlayerAfterPatrickDemonstration(captured);
                    }
                    catch (Exception ex)
                    {
                        Log("scheduled patrick kill failed err=" + ex.Message);
                    }
                });
        }

        private static void KillPlayerAfterPatrickDemonstration(ICharacter source)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("kill skipped reason=no-client");
                return;
            }

            Log("patrick kill requested character=" + source.Identity.ToString(true));

            AORebirth.Core.Playfields.Playfield playfield =
                source.Playfield as AORebirth.Core.Playfields.Playfield;
            if (playfield == null)
            {
                Log("kill skipped reason=no-playfield character=" + source.Identity.ToString(true));
                return;
            }

            try
            {
                SendPatrickKillHealthDamage(source);
                playfield.ForcePlayerDeath(source);
                Log("patrick kill applied character=" + source.Identity.ToString(true));
            }
            catch (Exception ex)
            {
                Log("patrick kill failed character=" + source.Identity.ToString(true) + " err=" + ex.Message);
            }
        }

        private static void CompleteYellQuest(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            if (MissionRuntime.IsInitialized
                && MissionRuntime.Service.GetFlag(
                    source.Identity.Instance,
                    YellQuestId,
                    RewardsGrantedFlag) != null)
            {
                // Still wipe stuck journal tip if a prior grant left Remain 00:00 on client.
                ClearYellPending(source);
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                    source,
                    PatrickSunTipSender.YellTipInstance);
                return;
            }

            // Claim once-only before grants so double dialogue answers cannot 2x XP/credits/belt.
            if (MissionRuntime.IsInitialized)
            {
                try
                {
                    MissionRuntime.Service.SetFlag(
                        source.Identity.Instance,
                        YellQuestId,
                        RewardsGrantedFlag,
                        "1");
                }
                catch (Exception ex)
                {
                    Log("yell rewards flag failed: " + ex.Message);
                }
            }

            ClearYellPending(source);

            // Capture 20260801-Patrick Sun #748-753:
            // FormatFeedback → Cash → XP → TemplateAction → ContainerAdd → Feedback → tip delete.
            TrySendFinishRewardFeedback(source);
            ApplyFinishXpCredits(source);
            TryGrantBelt(source);
            FeedbackMessageHandler.Default.Send(source, 110, 108871108);

            // Delete tip after reward wire so journal clears with the grant burst.
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                source,
                PatrickSunTipSender.YellTipInstance);

            if (MissionRuntime.IsInitialized)
            {
                EnsureQuestActive(source, YellQuestId);
                ForceCompleteTip(source.Identity.Instance, YellQuestId, "mission_5565C966_yell_patrick");
            }

            Log("yell complete rewards character=" + source.Identity.ToString(true));
        }

        private static void SendPatrickKillHealthDamage(ICharacter source)
        {
            if (source?.Controller?.Client == null)
            {
                return;
            }

            int targetHp = source.Stats[StatIds.health].Value;
            if (targetHp < 1)
            {
                targetHp = 1;
            }

            // Capture 20260801-Patrick Sun #407: HealthDamage from Patrick before Death.
            source.Controller.Client.SendCompressed(
                new HealthDamageMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    Unknown1 = 0,
                    Unknown2 = unchecked((int)0xFFFEE668),
                    Unknown3 = targetHp,
                    Unknown4 = 5,
                    Target = new Identity
                             {
                                 Type = IdentityType.CanbeAffected,
                                 Instance = PatrickSunInstance
                             },
                    Unknown5 = 0
                });
        }

        private static void TryGrantBelt(ICharacter source)
        {
            if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source)
                || source.Controller == null
                || source.Controller.Client == null)
            {
                Log("belt grant skipped reason=no-inventory-or-client");
                return;
            }

            if (!ItemLoader.ItemList.ContainsKey(BeltLowId)
                && !ItemLoader.ItemList.ContainsKey(BeltHighId))
            {
                Log("belt grant skipped reason=missing-ItemLoader-template");
                return;
            }

            int createId = ItemLoader.ItemList.ContainsKey(BeltLowId) ? BeltLowId : BeltHighId;
            Item item;
            try
            {
                item = new Item(BeltQuality, createId, BeltHighId);
            }
            catch (Exception ex)
            {
                Log("belt create failed err=" + ex.Message);
                return;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant == null || grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                Log(
                    "belt grant failed status="
                    + (grant == null ? "null" : grant.Status.ToString()));
                return;
            }

            source.Send(
                new TemplateActionMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    ItemLowId = BeltLowId,
                    ItemHighId = BeltHighId,
                    Quality = BeltQuality,
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

        private static void ApplyFinishXpCredits(ICharacter source)
        {
            if (source == null)
            {
                return;
            }

            AreteQuestRewardGrants.GrantCreditsAndXpOnce(
                source,
                YellQuestId,
                "arete-credits-awarded-patrick-yell",
                FinishCreditReward,
                "arete-xp-awarded-patrick-yell",
                FinishXpReward,
                "patrick-sun-yell-2596xp");
            Log("finish rewards character=" + source.Identity.ToString(true));
        }

        private static bool HasPendingYellTurnIn(ICharacter source)
        {
            if (source == null || HasCompletedPatrickQuest(source))
            {
                return false;
            }

            int characterId = source.Identity.Instance;
            lock (TipSyncRoot)
            {
                if (YellPendingByCharacter.Contains(characterId))
                {
                    return true;
                }
            }

            if (HasTipFlag(source, YellQuestId, YellPendingFlag))
            {
                return true;
            }

            if (IsMissionActive(source, YellQuestId) && !IsMissionCompleted(source, YellQuestId))
            {
                return true;
            }

            // Wire tip can exist on client after death even if Yell Accept was missed.
            return HasPendingTalkTip(source) == false
                   && IsMissionCompleted(source, TalkQuestId)
                   && !IsMissionCompleted(source, YellQuestId);
        }

        private static void EnsureQuestActive(ICharacter source, string questId)
        {
            if (source == null || !MissionRuntime.IsInitialized || string.IsNullOrEmpty(questId))
            {
                return;
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, questId);
            if (mission != null && mission.State == MissionLifecycleState.Active)
            {
                return;
            }

            if (mission == null || mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.OfferMission(characterId, questId);
                MissionRuntime.Service.AcceptMission(characterId, questId);
            }
        }

        private static void TrySendFinishRewardFeedback(ICharacter source)
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
                    FormattedMessage = FinishRewardFeedback,
                    Unknown2 = 0
                });
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

            ForceCompleteTip(instance, fromQuestId, objectiveId);
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

        private static void ForceCompleteTip(int characterId, string questId, string objectiveId)
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
                        ObservationKey = "patrick-sun-force-complete",
                        Amount = 1,
                        EventType = "PatrickSunQuestRuntime",
                        SourceIdentity = string.Empty,
                        TargetIdentity = string.Empty
                    });
            }

            MissionRuntime.Service.CompleteMission(characterId, questId);
        }

        private static bool IsMissionActive(ICharacter source, string questId)
        {
            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        private static bool IsMissionCompleted(ICharacter source, string questId)
        {
            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(source.Identity.Instance, questId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source != null
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static void Log(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "PatrickSunQuestRuntime " + message);
        }
    }
}
