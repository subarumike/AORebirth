namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

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

        private const int XpReward = 290;

        private const int CreditReward = 1040;

        private const int RewardMessageDisplayXp = 1281;

        private const string RewardFeedbackText = "Received reward: 1281 XP, 1040 credits.";

        private const string MissionId = "Mission:5514B18E";

        private const string ObjectiveId = "mission_5514B18E_objective_questfullupdate";

        private const string RewardKey = "captured-xp-and-credits";

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
            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, MissionId);
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
                return RexB18ECompletionResult.Failed(
                    "B18E durable reward failed: " + rewardResult.Message);
            }

            MissionOperationResult b18fTransition = MissionRuntime.Service.CompleteAndActivateNextMission(
                characterId,
                MissionId,
                MissionRuntime.RexB18FQuestId);
            if (IsPersistenceFailure(b18fTransition))
            {
                return RexB18ECompletionResult.Failed(
                    "B18F handoff persistence failed: " + b18fTransition.Message);
            }

            bool deleteProjected = EnsureQuestProjection(
                source,
                "b18e-delete-projected",
                () => SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source));
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

            bool b18fProjected = EnsureQuestProjection(
                source,
                "b18f-preview-projected",
                () => SafeQuestFullUpdateSender.TrySendB18FPreview(source));
            if (!deleteProjected || !b18fProjected)
            {
                return RexB18ECompletionResult.Failed(
                    "B18E state and rewards are durable, but a client quest projection remains retryable.");
            }

            return RexB18ECompletionResult.Succeeded(
                "B18E completion applied persistently mission=" + MissionId
                + " rewardStatus=" + rewardResult.Status
                + " xpDelta=" + XpReward
                + " creditDelta=" + CreditReward
                + " displayXp=" + RewardMessageDisplayXp
                + " b18fMission=" + MissionRuntime.RexB18FQuestId
                + " deleteProjected=" + deleteProjected
                + " rewardFeedback=" + (feedback == null ? "already-projected" : feedback.Message)
                + " b18fProjected=" + b18fProjected);
        }

        private static MissionRewardExecutionResult ApplyPersistentRewards(ICharacter source)
        {
            var definition = new MissionRewardDefinition
                             {
                                 RewardKey = RewardKey,
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
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.xp,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = XpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.unsavedxp,
                                                         Kind = MissionStatMutationKind.AddClamped,
                                                         Value = XpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     },
                                                     new MissionCharacterStatMutation
                                                     {
                                                         StatIdentityType = (int)IdentityType.CanbeAffected,
                                                         StatId = (int)StatIds.lastxp,
                                                         Kind = MissionStatMutationKind.Set,
                                                         Value = XpReward,
                                                         MinimumValue = 0,
                                                         MaximumValue = uint.MaxValue
                                                     }
                                                 }
                             };
            MissionRewardExecutionResult result = MissionRuntime.Rewards.ExecuteAtomicCharacterStats(
                source.Identity.Instance,
                MissionId,
                definition,
                "capture:20260618-083035:rex-b18e-xp-credits");
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

            return result;
        }

        private static bool EnsureQuestProjection(
            ICharacter source,
            string flagKey,
            Func<RexQuestPreviewEmissionResult> sender)
        {
            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, MissionId, flagKey) != null)
            {
                return true;
            }

            RexQuestPreviewEmissionResult result = sender();
            if (result == null || !result.Emitted)
            {
                return false;
            }

            MissionOperationResult flag = MissionRuntime.Service.SetFlag(
                characterId,
                MissionId,
                flagKey,
                "true");
            return flag.Status == MissionOperationStatus.Applied
                   || flag.Status == MissionOperationStatus.AlreadyApplied;
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
                + "\" displayXp=1281 actualXpDelta=290 creditReward=1040 "
                + "source=20260618-083035/events.log:1076,system-messages.log:281 "
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
