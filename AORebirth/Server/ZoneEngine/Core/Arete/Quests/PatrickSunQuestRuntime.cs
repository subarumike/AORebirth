namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
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
    /// Capture 20260725-patric: Patrick Sun insurance / kill / yell side quest on Arete Landing.
    /// Tips Mission:5565C962 → 5565C963 → 5565C966; finish 2229 XP, 2200 credits,
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

        // Capture 20260725-patric Terminal:574187D0 (ICC Cell Structure Scanner).
        public const int CellScannerTerminalInstance = unchecked((int)0x574187D0);

        private const int PatrickSunInstance = unchecked((int)0x78E0FC7B);

        private const int AreteLandingPlayfieldId = 6553;

        // Capture TemplateAction LowId=36782 HighId=36777 Quality=30.
        private const int BeltLowId = 36782;

        private const int BeltHighId = 36777;

        private const int BeltQuality = 30;

        private const int FinishXpReward = 2229;

        private const int FinishCreditReward = 2200;

        // Capture FormatFeedback Message wire for "Received reward: 2229 XP, 2200 credits."
        private const string FinishRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!:l~";

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedOverflowNextFreeSlot = 0x6F;

        private const string RewardsGrantedFlag = "patrick-sun-rewards-granted";

        public static string ResolvePatrickStartNodeId(ICharacter source)
        {
            if (source == null || !IsInAreteLanding(source))
            {
                return null;
            }

            if (HasPendingYellTurnIn(source))
            {
                // Capture after death: tip stays on journal; re-assert live AbsoluteTime so Remain is valid.
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                    source,
                    PatrickSunTipSender.YellTipInstance);
                EnsureQuestActive(source, YellQuestId);
                PatrickSunTipSender.TrySendYellTipOnly(source);
                return YellRootNodeId;
            }

            if (IsMissionActive(source, TalkQuestId) && !IsMissionCompleted(source, TalkQuestId))
            {
                return ScannerRootNodeId;
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
                // Capture 20260725-patric:
                // Answer Cell Scanner → AppendText "Excellent..." → Talk tip delete + Yell QFU
                // → AnswerList Goodbye → HealthDamage/Death (2954 ms later, before player clicks Goodbye).
                EnsureQuestActive(source, YellQuestId);
                CompleteAndActivate(source, TalkQuestId, YellQuestId, "mission_5565C963_talk_patrick");
                EnsureQuestActive(source, YellQuestId);
                PatrickSunTipSender.TrySendTalkToYellHandoff(source);
                SchedulePatrickKill(source);
                Log("talk→yell handoff + scheduled kill character=" + source.Identity.ToString(true));
                return true;
            }

            // Capture: rewards land with Relax/belt AppendText after ICC-guard answer (yell_003→reward).
            if (string.Equals(previousNodeId, "patrick_yell_003", StringComparison.OrdinalIgnoreCase)
                || string.Equals(previousNodeId, YellRewardNodeId, StringComparison.OrdinalIgnoreCase))
            {
                CompleteYellQuest(source);
                return true;
            }

            return false;
        }

        public static bool TryHandleInsuranceTerminalUse(ICharacter source, Identity target)
        {
            if (source == null
                || target == null
                || target.Type != IdentityType.Terminal
                || target.Instance != unchecked((int)0x574187D0)
                || !IsInAreteLanding(source)
                || !IsMissionActive(source, InsuranceQuestId)
                || IsMissionCompleted(source, InsuranceQuestId))
            {
                return false;
            }

            // Capture 20260727-193403 proves only Terminal:574187D0 for this transition.
            CompleteAndActivate(source, InsuranceQuestId, TalkQuestId, "mission_5565C962_use_scanner");
            PatrickSunTipSender.TrySendInsuranceToTalkHandoff(source);
            Log(
                "insurance→talk handoff character="
                + source.Identity.ToString(true)
                + " terminal="
                + target.ToString(true));
            return true;
        }

        private static void StartInsuranceTip(ICharacter source)
        {
            if (!MissionRuntime.IsInitialized)
            {
                return;
            }

            int characterId = source.Identity.Instance;
            MissionRuntime.Service.OfferMission(characterId, InsuranceQuestId);
            MissionRuntime.Service.AcceptMission(characterId, InsuranceQuestId);
            PatrickSunTipSender.TrySendInsuranceTipOnly(source);
            Log("insurance tip started character=" + source.Identity.ToString(true));
        }

        // Capture: Excellent @19:34:57.630 → HealthDamage @19:35:00.584 (2954 ms).
        // Tip/Yell lands first; death follows while Goodbye options are on screen.
        private const int PatrickKillDelayMilliseconds = 2954;

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
                SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                    source,
                    PatrickSunTipSender.YellTipInstance);
                return;
            }

            // Delete tip first so journal clears even if a later grant step fails.
            SafeQuestFullUpdateSender.SendTipAction59AndDelete(
                source,
                PatrickSunTipSender.YellTipInstance);

            ApplyFinishXpCredits(source);
            TryGrantBelt(source);
            TrySendFinishRewardFeedback(source);
            FeedbackMessageHandler.Default.Send(source, 110, 108871108);

            if (MissionRuntime.IsInitialized)
            {
                EnsureQuestActive(source, YellQuestId);
                ForceCompleteTip(source.Identity.Instance, YellQuestId, "mission_5565C966_yell_patrick");
                MissionRuntime.Service.SetFlag(
                    source.Identity.Instance,
                    YellQuestId,
                    RewardsGrantedFlag,
                    "1");
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

            // Capture 20260725-patric #239: HealthDamage from Patrick before Death.
            source.Controller.Client.SendCompressed(
                new HealthDamageMessage
                {
                    Identity = source.Identity,
                    Unknown = 0,
                    Unknown1 = 0,
                    Unknown2 = unchecked((int)0xFFFECC9D),
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

            // Credits: MissionRuntime ledger or direct cash Set.
            bool cashApplied = false;
            if (MissionRuntime.IsInitialized)
            {
                MissionRewardDefinition cashDefinition = new MissionRewardDefinition
                                                        {
                                                            RewardKey = "captured-patrick-sun-yell-credits",
                                                            RewardType = "character-stats",
                                                            IsResolved = true,
                                                            StatMutations =
                                                                new[]
                                                                {
                                                                    new MissionCharacterStatMutation
                                                                    {
                                                                        StatIdentityType =
                                                                            (int)IdentityType.CanbeAffected,
                                                                        StatId = (int)StatIds.cash,
                                                                        Kind = MissionStatMutationKind.AddClamped,
                                                                        Value = FinishCreditReward,
                                                                        MinimumValue = 0,
                                                                        MaximumValue = uint.MaxValue
                                                                    }
                                                                }
                                                        };
                MissionRewardExecutionResult cashResult = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                    source.Identity.Instance,
                    YellQuestId,
                    cashDefinition,
                    "capture:20260725-patric:patrick-yell-credits");
                if (cashResult.Succeeded && cashResult.StatValues != null)
                {
                    foreach (MissionCharacterStatValue statValue in cashResult.StatValues)
                    {
                        if (statValue.StatId != (int)StatIds.cash)
                        {
                            continue;
                        }

                        uint value = statValue.Value <= 0
                                         ? 0
                                         : (uint)Math.Min(statValue.Value, uint.MaxValue);
                        source.Stats[StatIds.cash].Set(value);
                        cashApplied = true;
                    }

                    if (cashApplied)
                    {
                        StatMessageHandler.Default.SendChanged(source);
                    }
                }
            }

            if (!cashApplied)
            {
                long cashAfter = (long)source.Stats[StatIds.cash].Value + FinishCreditReward;
                if (cashAfter > uint.MaxValue)
                {
                    cashAfter = uint.MaxValue;
                }

                source.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendChanged(source);
            }

            // XP must use CombatXpRuntimeService — naive Stats[xp].Set does not update the bar/wire
            // (cash worked; feedback showed 2229 XP but XP did not land).
            bool xpApplied = CombatXpRuntimeService.AwardDirectXp(
                source,
                FinishXpReward,
                "patrick-sun-yell-2229xp");
            Log(
                "finish rewards cashApplied="
                + cashApplied
                + " xpApplied="
                + xpApplied
                + " character="
                + source.Identity.ToString(true));
        }

        private static bool HasPendingYellTurnIn(ICharacter source)
        {
            if (source == null)
            {
                return false;
            }

            if (MissionRuntime.IsInitialized
                && MissionRuntime.Service.GetFlag(
                    source.Identity.Instance,
                    YellQuestId,
                    RewardsGrantedFlag) != null)
            {
                return false;
            }

            if (IsMissionActive(source, YellQuestId) && !IsMissionCompleted(source, YellQuestId))
            {
                return true;
            }

            // Wire tip can exist on client after death even if Yell Accept was missed.
            return IsMissionCompleted(source, TalkQuestId) && !IsMissionCompleted(source, YellQuestId);
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
