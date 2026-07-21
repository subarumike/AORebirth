namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class RexB18CObjectiveProgressTracker
    {
        public const string EnableEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS";

        private const string DialogueGateEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

        private const string QuestPreviewGateEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW";

        private const int AreteLandingPlayfieldId = 6553;

        private const string MissionId = "Mission:5514B18C";

        private const string ObjectiveId = "mission_5514B18C_objective_questfullupdate";

        private const string ObjectiveType = "CapturedKillCountObjective";

        private const string TargetName = "Malfunctioning Cleaning Robot";

        private const int RequiredCount = 5;

        public static bool IsProgressEnabled
        {
            get
            {
                return AreteEnvironmentGate.IsDefaultEnabled(EnableEnvironmentVariableName);
            }
        }

        public static bool AreAllGatesEnabled
        {
            get
            {
                return AreteEnvironmentGate.IsDefaultEnabled(DialogueGateEnvironmentVariableName)
                       && AreteEnvironmentGate.IsDefaultEnabled(QuestPreviewGateEnvironmentVariableName)
                       && IsProgressEnabled;
            }
        }

        public static bool TryActivateFromPreview(
            ICharacter source,
            RexQuestPreviewEmissionResult previewResult)
        {
            if (previewResult == null || !previewResult.Emitted)
            {
                return false;
            }

            if (!AreAllGatesEnabled)
            {
                Log(
                    "activation skipped mission={0} allGates=false dialogueGate={1} questPreviewGate={2} progressGate={3} noPersistence=true noCompletion=true noQuestDelete=true",
                    MissionId,
                    AreteEnvironmentGate.IsDefaultEnabled(DialogueGateEnvironmentVariableName),
                    AreteEnvironmentGate.IsDefaultEnabled(QuestPreviewGateEnvironmentVariableName),
                    IsProgressEnabled);
                return false;
            }

            if (!IsValidPlayerInArete(source))
            {
                Log(
                    "activation failed mission={0} reason=invalid-player-or-playfield source={1} noPersistence=true noCompletion=true noQuestDelete=true",
                    MissionId,
                    IdentityText(source));
                return false;
            }

            if (!MissionRuntime.IsInitialized)
            {
                Log("activation failed mission={0} reason=mission-runtime-not-initialized", MissionId);
                return false;
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(source.Identity.Instance, MissionId);
            if (IsTerminalFailure(offer))
            {
                Log("activation failed mission={0} status={1} message=\"{2}\"", MissionId, offer.Status, offer.Message);
                return false;
            }

            MissionOperationResult accept = MissionRuntime.Service.AcceptMission(source.Identity.Instance, MissionId);
            if (IsTerminalFailure(accept))
            {
                Log("activation failed mission={0} status={1} message=\"{2}\"", MissionId, accept.Status, accept.Message);
                return false;
            }

            Log(
                "activated mission={0} character={1} progress=0/{2} persistent=true",
                MissionId,
                source.Identity.ToString(true),
                RequiredCount);
            RexMissionChainStateStore.AdvanceAtLeast(
                source,
                RexMissionChainState.B18CPreviewed,
                "B18C preview activated");

            return true;
        }

        public static bool HasActiveProgress(ICharacter source)
        {
            return GetProgressForCharacter(source) != null;
        }

        public static RexB18CProgressUpdateResult TryObserveNpcDeath(
            ICharacter attacker,
            ICharacter target)
        {
            if (!AreAllGatesEnabled)
            {
                return RexB18CProgressUpdateResult.NotApplicable();
            }

            if (attacker == null || target == null)
            {
                return RexB18CProgressUpdateResult.Ignored("missing attacker or target");
            }

            if (!(attacker.Controller is PlayerController))
            {
                return RexB18CProgressUpdateResult.Ignored("attacker is not a player");
            }

            if (!IsInAreteLanding(attacker))
            {
                return RexB18CProgressUpdateResult.Ignored("attacker is not in Arete Landing");
            }

            if (!MissionRuntime.IsInitialized)
            {
                return RexB18CProgressUpdateResult.Ignored("mission runtime is not initialized");
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(attacker.Identity.Instance, MissionId);
            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                return RexB18CProgressUpdateResult.Ignored("no active or completed B18C mission for attacker");
            }

            string targetName = EffectiveName(target);
            bool targetMatches = string.Equals(targetName, TargetName, StringComparison.OrdinalIgnoreCase);
            if (!targetMatches)
            {
                return RexB18CProgressUpdateResult.Ignored("target name did not match");
            }

            string observationKey = "npc-death:" + target.Identity.ToString(true);
            ObjectiveProgressRecord matchedProgress;
            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult observation = MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = attacker.Identity.Instance,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = observationKey,
                        Amount = 1,
                        EventType = "KillNpcTarget:CharacterAction:Death",
                        SourceIdentity = attacker.Identity.ToString(true),
                        TargetIdentity = target.Identity.ToString(true)
                    });
                if (observation.Status != MissionOperationStatus.Applied
                    && observation.Status != MissionOperationStatus.AlreadyApplied
                    && observation.Status != MissionOperationStatus.DuplicateObservation)
                {
                    return RexB18CProgressUpdateResult.Ignored(observation.Message ?? observation.Status.ToString());
                }

                matchedProgress = ToRuntimeProgress(observation.Objective, observationKey);
            }
            else
            {
                MissionObjectiveProgressRecord persistedProgress = MissionRuntime.Service.GetObjective(
                    attacker.Identity.Instance,
                    MissionId,
                    ObjectiveId);
                matchedProgress = ToRuntimeProgress(
                    persistedProgress,
                    persistedProgress == null ? observationKey : persistedProgress.LastObservationKey);
            }

            LogProgress(attacker, target, matchedProgress);

            RexB18CProgressFeedbackSender.TrySend(attacker, matchedProgress);
            if (matchedProgress != null && matchedProgress.Completed)
            {
                MissionOperationResult completion = MissionRuntime.Service.CompleteAndActivateNextMission(
                    attacker.Identity.Instance,
                    MissionId,
                    MissionRuntime.RexB18DQuestId);
                if (completion.Status == MissionOperationStatus.Applied
                    || completion.Status == MissionOperationStatus.AlreadyApplied)
                {
                    RexMissionChainStateStore.AdvanceAtLeast(
                        attacker,
                        RexMissionChainState.B18DPreviewed,
                        "B18C completion activated B18D persistently");
                    EnsureCompletionHandoffProjection(attacker);
                }
            }

            return RexB18CProgressUpdateResult.MatchedProgress(matchedProgress);
        }

        private static bool EnsureCompletionHandoffProjection(ICharacter source)
        {
            const string flagKey = "b18c-completion-handoff-projected";
            int characterId = source.Identity.Instance;
            if (MissionRuntime.Service.GetFlag(characterId, MissionId, flagKey) != null)
            {
                return true;
            }

            if (!SafeQuestFullUpdateSender.TrySendB18CCompletionHandoff(source))
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

        public static ObjectiveProgressRecord GetProgressForCharacter(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            if (!MissionRuntime.IsInitialized)
            {
                return null;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, MissionId);
            if (mission == null)
            {
                return null;
            }

            MissionObjectiveProgressRecord progress = MissionRuntime.Service.GetObjective(
                source.Identity.Instance,
                MissionId,
                ObjectiveId);
            return ToRuntimeProgress(progress, progress == null ? null : progress.LastObservationKey);
        }

        private static void LogProgress(
            ICharacter attacker,
            ICharacter target,
            ObjectiveProgressRecord progress)
        {
            if (progress.Completed)
            {
                Log(
                    "progress mission={0} character={1} target={2} targetName=\"{3}\" progress={4}/{5} complete=true inMemoryOnly=true capturedCompletionHandoffPending=true noRewards=true noDbWrites=true noPersistence=true",
                    MissionId,
                    IdentityText(attacker),
                    IdentityText(target),
                    EffectiveName(target),
                    progress.CurrentCount,
                    progress.RequiredCount);
                return;
            }

            Log(
                "progress mission={0} character={1} target={2} targetName=\"{3}\" progress={4}/{5} complete=false inMemoryOnly=true noMissionCompletion=true noQuestDelete=true noRewards=true noDbWrites=true",
                MissionId,
                IdentityText(attacker),
                IdentityText(target),
                EffectiveName(target),
                progress.CurrentCount,
                progress.RequiredCount);
        }

        private static ObjectiveProgressRecord CopyProgress(ObjectiveProgressRecord progress)
        {
            if (progress == null)
            {
                return null;
            }

            return new ObjectiveProgressRecord
                   {
                       MissionId = progress.MissionId,
                       ObjectiveId = progress.ObjectiveId,
                       ObjectiveType = progress.ObjectiveType,
                       CurrentCount = progress.CurrentCount,
                       RequiredCount = progress.RequiredCount,
                       Completed = progress.Completed,
                       MatchedEvidenceCount = progress.MatchedEvidenceCount,
                       IgnoredEvidenceCount = progress.IgnoredEvidenceCount,
                       LastMatchedEvidenceReference = progress.LastMatchedEvidenceReference
                   };
        }

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null
                   && source.Controller is PlayerController
                   && source.Identity.Type == IdentityType.CanbeAffected
                   && source.Identity.Instance != 0
                   && IsInAreteLanding(source);
        }

        private static bool IsInAreteLanding(ICharacter character)
        {
            return character != null
                   && character.Playfield != null
                   && character.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

        private static string EffectiveName(ICharacter character)
        {
            if (character == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(character.Name))
            {
                return character.Name;
            }

            string fullName = ((character.FirstName ?? string.Empty) + " " + (character.LastName ?? string.Empty)).Trim();
            return fullName;
        }

        private static ObjectiveProgressRecord ToRuntimeProgress(
            MissionObjectiveProgressRecord progress,
            string evidenceReference)
        {
            if (progress == null)
            {
                return null;
            }

            return new ObjectiveProgressRecord
                   {
                       MissionId = MissionId,
                       ObjectiveId = ObjectiveId,
                       ObjectiveType = ObjectiveType,
                       CurrentCount = progress.Progress,
                       RequiredCount = progress.RequiredCount,
                       Completed = progress.Progress >= progress.RequiredCount,
                       MatchedEvidenceCount = progress.Progress,
                       IgnoredEvidenceCount = 0,
                       LastMatchedEvidenceReference = evidenceReference
                   };
        }

        private static bool IsTerminalFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        private static string IdentityText(ICharacter character)
        {
            return character == null ? "<null>" : character.Identity.ToString(true);
        }

        private static void Log(string format, params object[] args)
        {
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_REX_B18C_PROGRESS "
                + string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private sealed class RexB18CProgressState
        {
            public Identity CharacterIdentity { get; set; }

            public string CharacterIdentityText { get; set; }

            public ObjectiveProgressRecord Progress { get; set; }

            public DateTime ActivatedAtUtc { get; set; }

            public bool CompletionHandoffSent { get; set; }
        }

        private static class RexB18CProgressFeedbackSender
        {
            private const int FeedbackCategoryId = 110;

            private const int FeedbackMessageId = 249817907;

            public static bool TrySend(ICharacter character, ObjectiveProgressRecord progress)
            {
                if (character == null || character.Controller == null || character.Controller.Client == null)
                {
                    return false;
                }

                if (progress == null
                    || !RexB18CFeedbackPolicy.ShouldSendPerKillFeedback(
                        progress.CurrentCount,
                        progress.RequiredCount))
                {
                    return false;
                }

                // Capture: remaining-count FormatFeedback for 1/5-4/5, then Feedback 110/249817907 for 1/5-5/5.
                string formatFeedback = GetCapturedRemainingCountFeedback(progress.CurrentCount);
                if (!string.IsNullOrEmpty(formatFeedback))
                {
                    character.Controller.Client.SendCompressed(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            FormattedMessage = formatFeedback,
                            Unknown2 = 0
                        });
                }

                character.Controller.Client.SendCompressed(
                    new FeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        CategoryId = FeedbackCategoryId,
                        MessageId = FeedbackMessageId
                    });

                Log(
                    "feedback sent mission={0} character={1} progress={2}/{3} remainingFormat={4} sender=server",
                    MissionId,
                    IdentityText(character),
                    progress.CurrentCount,
                    progress.RequiredCount,
                    !string.IsNullOrEmpty(formatFeedback));
                return true;
            }

            private static string GetCapturedRemainingCountFeedback(int currentCount)
            {
                // Encoded remaining counts from capture system-messages.log (client renders as
                // "You need to kill N more Malfunctioning Cleaning Robot").
                switch (currentCount)
                {
                    case 1:
                        return "~&!!!\":$nZiAi!!!!%s\u001e" + TargetName;
                    case 2:
                        return "~&!!!\":$nZiAi!!!!$s\u001e" + TargetName;
                    case 3:
                        return "~&!!!\":$nZiAi!!!!#s\u001e" + TargetName;
                    case 4:
                        return "~&!!!\":$nZiAi!!!!\"s\u001e" + TargetName;
                    default:
                        return null;
                }
            }
        }
    }

    public sealed class RexB18CProgressUpdateResult
    {
        private RexB18CProgressUpdateResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Matched { get; private set; }

        public string Message { get; private set; }

        public ObjectiveProgressRecord Progress { get; private set; }

        public static RexB18CProgressUpdateResult NotApplicable()
        {
            return new RexB18CProgressUpdateResult();
        }

        public static RexB18CProgressUpdateResult Ignored(string message)
        {
            return new RexB18CProgressUpdateResult
                   {
                       IsApplicable = true,
                       Matched = false,
                       Message = message
                   };
        }

        public static RexB18CProgressUpdateResult MatchedProgress(ObjectiveProgressRecord progress)
        {
            return new RexB18CProgressUpdateResult
                   {
                       IsApplicable = true,
                       Matched = true,
                       Progress = progress
                   };
        }
    }
}
