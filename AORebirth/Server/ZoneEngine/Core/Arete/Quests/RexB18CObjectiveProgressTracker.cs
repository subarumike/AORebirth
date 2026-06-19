namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Controllers;

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

        private static readonly Dictionary<string, RexB18CProgressState> ProgressByCharacter =
            new Dictionary<string, RexB18CProgressState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

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

            string characterKey = MakeCharacterKey(source.Identity);
            var progress = new ObjectiveProgressRecord
                           {
                               MissionId = MissionId,
                               ObjectiveId = ObjectiveId,
                               ObjectiveType = ObjectiveType,
                               CurrentCount = 0,
                               RequiredCount = RequiredCount,
                               Completed = false,
                               MatchedEvidenceCount = 0,
                               IgnoredEvidenceCount = 0,
                               LastMatchedEvidenceReference = null
                           };

            lock (SyncRoot)
            {
                ProgressByCharacter[characterKey] =
                    new RexB18CProgressState
                    {
                        CharacterIdentity = source.Identity,
                        CharacterIdentityText = source.Identity.ToString(true),
                        Progress = progress,
                        ActivatedAtUtc = DateTime.UtcNow
                    };
            }

            Log(
                "activated mission={0} character={1} progress=0/{2} inMemoryOnly=true noPersistence=true noCompletion=true noQuestDelete=true noRewards=true",
                MissionId,
                source.Identity.ToString(true),
                RequiredCount);
            RexMissionChainStateStore.AdvanceAtLeast(
                source,
                RexMissionChainState.B18CPreviewed,
                "B18C preview activated");

            return true;
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

            string targetName = EffectiveName(target);
            bool targetMatches = string.Equals(targetName, TargetName, StringComparison.OrdinalIgnoreCase);
            string characterKey = MakeCharacterKey(attacker.Identity);
            RexB18CProgressState state;
            ObjectiveProgressRecord matchedProgress;
            bool shouldSendCompletionHandoff = false;

            lock (SyncRoot)
            {
                if (!ProgressByCharacter.TryGetValue(characterKey, out state))
                {
                    if (targetMatches)
                    {
                        Log(
                            "death ignored mission={0} reason=no-active-preview character={1} target={2} targetName=\"{3}\" inMemoryOnly=true",
                            MissionId,
                            IdentityText(attacker),
                            IdentityText(target),
                            targetName);
                    }

                    return RexB18CProgressUpdateResult.Ignored("no active B18C preview for attacker");
                }

                if (!targetMatches)
                {
                    state.Progress.IgnoredEvidenceCount++;
                    return RexB18CProgressUpdateResult.Ignored("target name did not match");
                }

                state.Progress.MatchedEvidenceCount++;
                state.Progress.LastMatchedEvidenceReference =
                    "live:KillNpcTarget:CharacterAction Action=Death target="
                    + target.Identity.ToString(true);

                if (!state.Progress.Completed)
                {
                    state.Progress.CurrentCount = Math.Min(
                        state.Progress.CurrentCount + 1,
                        state.Progress.RequiredCount);
                    state.Progress.Completed =
                        state.Progress.CurrentCount >= state.Progress.RequiredCount;
                }
                else
                {
                    state.Progress.IgnoredEvidenceCount++;
                }

                if (state.Progress.Completed && !state.CompletionHandoffSent)
                {
                    state.CompletionHandoffSent = true;
                    shouldSendCompletionHandoff = true;
                }

                LogProgress(attacker, target, state.Progress);
                matchedProgress = CopyProgress(state.Progress);
            }

            RexB18CProgressFeedbackSender.TrySend(attacker, matchedProgress);
            if (matchedProgress != null && matchedProgress.Completed)
            {
                RexMissionChainStateStore.AdvanceAtLeast(
                    attacker,
                    RexMissionChainState.B18CObjectiveComplete,
                    "B18C kill count reached required progress");
            }

            if (shouldSendCompletionHandoff)
            {
                bool handoffSent = SafeQuestFullUpdateSender.TrySendB18CCompletionHandoff(attacker);
                if (handoffSent)
                {
                    RexB18DBoxProgressTracker.TryActivateFromPreview(attacker);
                }
            }

            return RexB18CProgressUpdateResult.MatchedProgress(matchedProgress);
        }

        public static ObjectiveProgressRecord GetProgressForCharacter(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            RexB18CProgressState state;
            lock (SyncRoot)
            {
                if (ProgressByCharacter.TryGetValue(MakeCharacterKey(source.Identity), out state))
                {
                    return CopyProgress(state.Progress);
                }
            }

            return null;
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

        private static string MakeCharacterKey(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture);
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
                    Log(
                        "feedback skipped mission={0} reason=missing-client progress={1}/{2} noQuestDelete=true noCompletion=true",
                        MissionId,
                        ProgressCount(progress),
                        RequiredCount);
                    return false;
                }

                if (progress == null)
                {
                    Log(
                        "feedback skipped mission={0} reason=missing-progress noQuestDelete=true noCompletion=true",
                        MissionId);
                    return false;
                }

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
                    "feedback sent mission={0} character={1} progress={2}/{3} formatFeedback={4} feedbackCategory={5} feedbackMessage={6} sender=server capturedSource=20260614-194454/system-messages.log completionHandoffCandidate={7} noRewards=true noDbWrites=true",
                    MissionId,
                    IdentityText(character),
                    progress.CurrentCount,
                    progress.RequiredCount,
                    !string.IsNullOrEmpty(formatFeedback),
                    FeedbackCategoryId,
                    FeedbackMessageId,
                    progress.Completed);

                return true;
            }

            private static string GetCapturedRemainingCountFeedback(int currentCount)
            {
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

            private static int ProgressCount(ObjectiveProgressRecord progress)
            {
                return progress == null ? 0 : progress.CurrentCount;
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
