namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class RexB18ECompletionHandler
    {
        public const string EnableEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION";

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const int XpReward = 1281;

        private const int CreditReward = 1040;

        private const string RewardFeedbackText = "Received reward: 1281 XP, 1040 credits.";

        private const string MissionId = "Mission:5514B18E";

        private const string ObjectiveId = "mission_5514B18E_objective_questfullupdate";

        // Quest description / QuestFullUpdate Unknown6=1040 Unknown8=1281 (SafeQuestFullUpdateSender B18E).
        private const string CreditRewardKey = "captured-rex-b18e-credits-1040";

        private const string XpAwardedFlag = "rex-b18e-xp-1281-awarded";

        public static bool IsCompletionEnabled
        {
            get
            {
                return AreteEnvironmentGate.IsDefaultEnabled(EnableEnvironmentVariableName);
            }
        }

        public static RexB18ECompletionResult TryCompleteOnReturn(
            ICharacter source,
            Identity npcIdentity,
            bool dialogueGateEnabled)
        {
            if (!IsRexLarsson(npcIdentity))
            {
                return RexB18ECompletionResult.NotApplicable();
            }

            bool questPreviewGateEnabled = RexQuestPreviewEmitter.IsQuestPreviewEnabled;
            bool b18dPreviewGateEnabled = RexB18DBoxProgressTracker.IsPreviewCompletionEnabled;
            bool completionGateEnabled = IsCompletionEnabled;
            if (!dialogueGateEnabled
                || !questPreviewGateEnabled
                || !b18dPreviewGateEnabled
                || !completionGateEnabled)
            {
                return RexB18ECompletionResult.Skipped(
                    "B18E completion skipped dialogueGate=" + dialogueGateEnabled
                    + " questPreviewGate=" + questPreviewGateEnabled
                    + " b18dPreviewGate=" + b18dPreviewGateEnabled
                    + " b18eCompletionGate=" + completionGateEnabled
                    + " attempted=false noAction59=true noCreditGrant=true noItems=true noInventory=true "
                    + "noDbMissionPersistence=true noMarcusStoneImplementation=true");
            }

            if (!IsValidPlayerInArete(source))
            {
                return RexB18ECompletionResult.Failed(
                    "B18E completion failed: source is missing, not a player, or not in Arete Landing 6553.");
            }

            if (!MissionRuntime.IsInitialized)
            {
                return RexB18ECompletionResult.Failed(
                    "B18E completion failed: persistent mission runtime is not initialized.");
            }

            int characterId = source.Identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord mission = EnsureB18EReadyForReturn(characterId);
            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                return RexB18ECompletionResult.Skipped(
                    "B18E completion skipped because the persistent mission is not active.");
            }

            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult objective = MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = characterId,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = "dialogue-return:" + npcIdentity.ToString(true),
                        Amount = 1,
                        EventType = "NpcDialogueOpen",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = npcIdentity.ToString(true)
                    });
                if (objective.Status != MissionOperationStatus.Applied
                    && objective.Status != MissionOperationStatus.AlreadyApplied
                    && objective.Status != MissionOperationStatus.DuplicateObservation)
                {
                    return RexB18ECompletionResult.Failed(
                        "B18E objective persistence failed: " + objective.Message);
                }

                MissionOperationResult completion = MissionRuntime.Service.CompleteMission(characterId, MissionId);
                if (completion.Status != MissionOperationStatus.Applied
                    && completion.Status != MissionOperationStatus.AlreadyApplied)
                {
                    return RexB18ECompletionResult.Failed(
                        "B18E completion persistence failed: " + completion.Message);
                }
            }

            MissionRewardExecutionResult rewardResult = ApplyPersistentRewards(source);
            if (!rewardResult.Succeeded)
            {
                // Still project Delete+B18F — reward ledger AlreadyApplied / partial failures
                // previously aborted the handoff and left Return to Rex stuck beside Marcus.
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_REX_B18E_COMPLETION reward status=\""
                    + rewardResult.Message
                    + "\" — continuing B18F client handoff");
            }

            MissionOperationResult b18fTransition = MissionRuntime.Service.CompleteAndActivateNextMission(
                characterId,
                MissionId,
                MissionRuntime.RexB18FQuestId);
            if (IsPersistenceFailure(b18fTransition))
            {
                MissionRuntime.Service.OfferMission(characterId, MissionRuntime.RexB18FQuestId);
                MissionRuntime.Service.AcceptMission(characterId, MissionRuntime.RexB18FQuestId);
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ARETE_REX_B18E_COMPLETION B18F handoff status="
                    + (b18fTransition == null ? "null" : b18fTransition.Status.ToString())
                    + " message=\""
                    + (b18fTransition == null ? "" : b18fTransition.Message)
                    + "\" — forced offer/accept + client projection");
            }

            RewardFeedbackResult feedback = null;
            if (MissionRuntime.Service.GetFlag(characterId, MissionId, "reward-feedback-projected") == null)
            {
                feedback = SendCapturedRewardFeedback(source);
                if (feedback.Sent)
                {
                    MissionRuntime.Service.SetFlag(
                        characterId,
                        MissionId,
                        "reward-feedback-projected",
                        "true");
                }
            }

            // Always re-project Delete+B18F. Flag-gated sends left Return to Rex stuck next to Marcus.
            RexQuestPreviewEmissionResult handoff = SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source);
            bool projected = handoff != null && handoff.Emitted;
            if (!projected)
            {
                SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
                projected = SafeQuestFullUpdateSender.TrySendB18FPreview(source).Emitted;
            }

            if (!projected)
            {
                return RexB18ECompletionResult.Failed(
                    "B18E state and rewards are durable, but a client quest projection remains retryable.");
            }

            return RexB18ECompletionResult.Succeeded(
                "B18E completion applied persistently mission=" + MissionId
                + " rewardStatus=" + rewardResult.Status
                + " xpDelta=" + XpReward
                + " creditDelta=" + CreditReward
                + " b18fMission=" + MissionRuntime.RexB18FQuestId
                + " handoffProjected=true"
                + " rewardFeedback=" + (feedback == null ? "already-projected" : feedback.Message));
        }

        private static ZoneEngine.Core.Missions.MissionStateRecord EnsureB18EReadyForReturn(int characterId)
        {
            ZoneEngine.Core.Missions.MissionStateRecord mission =
                MissionRuntime.Service.GetMission(characterId, MissionId);
            if (mission != null && mission.State == MissionLifecycleState.Offered)
            {
                MissionRuntime.Service.AcceptMission(characterId, MissionId);
                mission = MissionRuntime.Service.GetMission(characterId, MissionId);
            }

            if (mission != null
                && (mission.State == MissionLifecycleState.Active
                    || mission.State == MissionLifecycleState.Completed))
            {
                return mission;
            }

            // Cargo handoff can leave B18D Completed while B18E was only client-previewed.
            ZoneEngine.Core.Missions.MissionStateRecord b18d =
                MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18DQuestId);
            if (b18d == null || b18d.State != MissionLifecycleState.Completed)
            {
                return mission;
            }

            MissionRuntime.Service.OfferMission(characterId, MissionId);
            MissionRuntime.Service.AcceptMission(characterId, MissionId);
            return MissionRuntime.Service.GetMission(characterId, MissionId);
        }

        private static MissionRewardExecutionResult ApplyPersistentRewards(ICharacter source)
        {
            int characterId = source.Identity.Instance;
            bool cashApplied = false;
            var cashDefinition = new MissionRewardDefinition
                                 {
                                     RewardKey = CreditRewardKey,
                                     RewardType = "character-stats",
                                     IsResolved = true,
                                     StatMutations = new[]
                                                     {
                                                         new MissionCharacterStatMutation
                                                         {
                                                             StatIdentityType = (int)IdentityType.CanbeAffected,
                                                             StatId = (int)StatIds.cash,
                                                             Kind = MissionStatMutationKind.AddClamped,
                                                             Value = CreditReward,
                                                             MinimumValue = 0,
                                                             MaximumValue = uint.MaxValue
                                                         }
                                                     }
                                 };
            MissionRewardExecutionResult cashResult = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                characterId,
                MissionId,
                cashDefinition,
                "quest-description:rex-b18e-1040-credits");
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

            if (!cashApplied
                && (cashResult == null
                    || cashResult.Status != MissionRewardExecutionStatus.AlreadyApplied))
            {
                long cashAfter = (long)source.Stats[StatIds.cash].Value + CreditReward;
                if (cashAfter > uint.MaxValue)
                {
                    cashAfter = uint.MaxValue;
                }

                source.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendChanged(source);
                cashApplied = true;
            }

            bool xpApplied = MissionRuntime.Service.GetFlag(characterId, MissionId, XpAwardedFlag) != null;
            if (!xpApplied)
            {
                if (CombatXpRuntimeService.AwardDirectXp(source, XpReward, "rex-b18e-return-1281xp"))
                {
                    MissionRuntime.Service.SetFlag(characterId, MissionId, XpAwardedFlag, "true");
                    xpApplied = true;
                }
            }

            return new MissionRewardExecutionResult
                   {
                       Status = cashResult != null
                                    ? cashResult.Status
                                    : MissionRewardExecutionStatus.Applied,
                       Message = "credits="
                                 + (cashApplied ? CreditReward.ToString() : "skipped")
                                 + " xp="
                                 + (xpApplied ? XpReward.ToString() : "skipped")
                   };
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        private static RewardFeedbackResult SendCapturedRewardFeedback(ICharacter source)
        {
            if (source == null || source.Controller == null || source.Controller.Client == null)
            {
                return new RewardFeedbackResult
                       {
                           Sent = false,
                           Message = "Reward feedback skipped because source client is missing."
                       };
            }

            source.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = source.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = RewardFeedbackText,
                    Unknown2 = 0
                });

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_REX_B18E_COMPLETION reward feedback sent character="
                + source.Identity.ToString(true)
                + " message=\""
                + RewardFeedbackText
                + "\" xpReward=1281 creditReward=1040 "
                + "source=quest-description/B18E-QuestFullUpdate "
                + "safeFormatFeedback=true noAction59=true noItems=true noInventory=true");

            return new RewardFeedbackResult
                   {
                       Sent = true,
                       Message = "Reward feedback sent using existing FormatFeedbackMessage path."
                   };
        }

        private static bool IsRexLarsson(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == RexLarssonInstance;
        }

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null
                   && source.Controller is PlayerController
                   && source.Identity.Type == IdentityType.CanbeAffected
                   && source.Identity.Instance != 0
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private sealed class RewardFeedbackResult
        {
            public bool Sent { get; set; }

            public string Message { get; set; }
        }

    }

    public sealed class RexB18ECompletionResult
    {
        private RexB18ECompletionResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Attempted { get; private set; }

        public bool Completed { get; private set; }

        public string Message { get; private set; }

        public static RexB18ECompletionResult NotApplicable()
        {
            return new RexB18ECompletionResult();
        }

        public static RexB18ECompletionResult Skipped(string message)
        {
            return new RexB18ECompletionResult
                   {
                       IsApplicable = true,
                       Attempted = false,
                       Completed = false,
                       Message = message
                   };
        }

        public static RexB18ECompletionResult Succeeded(string message)
        {
            return new RexB18ECompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = true,
                       Message = message
                   };
        }

        public static RexB18ECompletionResult Failed(string message)
        {
            return new RexB18ECompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = false,
                       Message = message
                   };
        }
    }
}
