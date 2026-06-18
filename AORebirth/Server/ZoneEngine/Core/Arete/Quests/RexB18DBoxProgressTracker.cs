namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Controllers;

    #endregion

    public static class RexB18DBoxProgressTracker
    {
        public const string EnableEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW";

        private const int AreteLandingPlayfieldId = 6553;

        private const int CargoBoxInstance = unchecked((int)0x56D9B4AF);

        private const string MissionId = "Mission:5514B18D";

        private const string ObjectiveId = "mission_5514B18D_objective_questfullupdate";

        private const string ObjectiveType = "CapturedUseInteractObjective";

        private const int RequiredCount = 1;

        private static readonly Dictionary<string, RexB18DPreviewState> PreviewByCharacter =
            new Dictionary<string, RexB18DPreviewState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        public static bool IsPreviewCompletionEnabled
        {
            get
            {
                return IsTruthy(Environment.GetEnvironmentVariable(EnableEnvironmentVariableName));
            }
        }

        public static bool AreAllGatesEnabled
        {
            get
            {
                return RexB18CObjectiveProgressTracker.AreAllGatesEnabled
                       && IsPreviewCompletionEnabled;
            }
        }

        public static bool TryActivateFromPreview(ICharacter source)
        {
            if (!AreAllGatesEnabled)
            {
                Log(
                    "activation skipped mission={0} allGates=false b18dPreviewGate={1} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true noRewards=true",
                    MissionId,
                    IsPreviewCompletionEnabled);
                return false;
            }

            if (!IsValidPlayerInArete(source))
            {
                Log(
                    "activation failed mission={0} reason=invalid-player-or-playfield source={1} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true",
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
                PreviewByCharacter[characterKey] =
                    new RexB18DPreviewState
                    {
                        CharacterIdentity = source.Identity,
                        CharacterIdentityText = source.Identity.ToString(true),
                        Progress = progress,
                        ActivatedAtUtc = DateTime.UtcNow
                    };
            }

            Log(
                "activated mission={0} character={1} progress=0/{2} previewReceived=true inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true noRewards=true noDbWrites=true",
                MissionId,
                source.Identity.ToString(true),
                RequiredCount);
            RexMissionChainStateStore.AdvanceAtLeast(
                source,
                RexMissionChainState.B18DPreviewed,
                "B18D preview activated from B18C handoff");

            return true;
        }

        public static bool TryObserveBoxUse(ICharacter source, Identity target)
        {
            if (!IsCargoBoxTarget(target))
            {
                return false;
            }

            if (!AreAllGatesEnabled)
            {
                Log(
                    "use ignored mission={0} reason=gates-disabled character={1} target={2} b18dPreviewGate={3} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true",
                    MissionId,
                    IdentityText(source),
                    target.ToString(true),
                    IsPreviewCompletionEnabled);
                return false;
            }

            if (!IsValidPlayerInArete(source))
            {
                Log(
                    "use ignored mission={0} reason=invalid-player-or-playfield character={1} target={2} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true",
                    MissionId,
                    IdentityText(source),
                    target.ToString(true));
                return true;
            }

            string characterKey = MakeCharacterKey(source.Identity);
            ObjectiveProgressRecord progressSnapshot;
            bool shouldSendB18DToB18EHandoff = false;
            lock (SyncRoot)
            {
                RexB18DPreviewState state;
                if (!PreviewByCharacter.TryGetValue(characterKey, out state))
                {
                    Log(
                        "use ignored mission={0} reason=no-active-b18d-preview character={1} target={2} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true",
                        MissionId,
                        source.Identity.ToString(true),
                        target.ToString(true));
                    return true;
                }

                if (state.Progress.Completed)
                {
                    state.Progress.IgnoredEvidenceCount++;
                    Log(
                        "use ignored mission={0} reason=already-preview-complete character={1} target={2} progress={3}/{4} inMemoryOnly=true noDuplicateQuestPackets=true noDuplicateQuestDelete=true noDuplicateB18E=true noAction59=true noRewards=true",
                        MissionId,
                        source.Identity.ToString(true),
                        target.ToString(true),
                        state.Progress.CurrentCount,
                        state.Progress.RequiredCount);
                    return true;
                }

                state.Progress.MatchedEvidenceCount++;
                state.Progress.CurrentCount = RequiredCount;
                state.Progress.Completed = true;
                state.Progress.LastMatchedEvidenceReference =
                    "live:GenericCmd Action=Use target=" + target.ToString(true);
                if (!state.B18DToB18EHandoffAttempted)
                {
                    state.B18DToB18EHandoffAttempted = true;
                    shouldSendB18DToB18EHandoff = true;
                }

                progressSnapshot = CopyProgress(state.Progress);
            }

            RexMissionChainStateStore.AdvanceAtLeast(
                source,
                RexMissionChainState.B18DObjectiveComplete,
                "B18D exact Cargo Box use observed");
            Log(
                "objective observed mission={0} character={1} target={2} signal=\"GenericCmd Action=Use\" evidence=20260614-194454/events.log:6327,6333 progress={3}/{4} complete=true previewOnly=true inMemoryOnly=true b18dQuestDeletePending={5} b18eQuestFullUpdatePending={5} noAction59=true noRewards=true noInventory=true noXpCredits=true noDbWrites=true noB18ECompletion=true",
                MissionId,
                source.Identity.ToString(true),
                target.ToString(true),
                progressSnapshot.CurrentCount,
                progressSnapshot.RequiredCount,
                shouldSendB18DToB18EHandoff);
            if (shouldSendB18DToB18EHandoff)
            {
                RexQuestPreviewEmissionResult b18dDeleteResult =
                    SafeQuestFullUpdateSender.TrySendB18DQuestDelete(source);
                Log(
                    "b18d questdelete send result mission=Mission:5514B18D character={0} attempted={1} emitted={2} message=\"{3}\" source=20260614-194454/packets.hex.log:5765 rawReplay=false noAction59=true b18dWindowCleanupOnly=true noCompletionSemantics=true noRewards=true noInventory=true noXpCredits=true noDbWrites=true",
                    source.Identity.ToString(true),
                    b18dDeleteResult.Attempted,
                    b18dDeleteResult.Emitted,
                    b18dDeleteResult.Message);

                RexQuestPreviewEmissionResult b18eResult =
                    SafeQuestFullUpdateSender.TrySendB18EPreview(source);
                if (b18eResult.Emitted)
                {
                    RexMissionChainStateStore.AdvanceAtLeast(
                        source,
                        RexMissionChainState.B18EPreviewed,
                        "B18E QuestFullUpdate sent after B18D preview completion");
                }

                Log(
                    "b18e questfullupdate send result mission=Mission:5514B18E character={0} attempted={1} emitted={2} message=\"{3}\" source=20260614-194454/packets.hex.log:5767 noAction59=true noQuestDelete=true noRewards=true noInventory=true noXpCredits=true noDbWrites=true noB18ECompletion=true",
                    source.Identity.ToString(true),
                    b18eResult.Attempted,
                    b18eResult.Emitted,
                    b18eResult.Message);
            }

            return true;
        }

        public static ObjectiveProgressRecord GetProgressForCharacter(ICharacter source)
        {
            if (source == null)
            {
                return null;
            }

            RexB18DPreviewState state;
            lock (SyncRoot)
            {
                if (PreviewByCharacter.TryGetValue(MakeCharacterKey(source.Identity), out state))
                {
                    return CopyProgress(state.Progress);
                }
            }

            return null;
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

        private static bool IsCargoBoxTarget(Identity target)
        {
            return target.Type == IdentityType.Terminal
                   && target.Instance == CargoBoxInstance;
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
                "ARETE_REX_B18D_PREVIEW "
                + string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class RexB18DPreviewState
        {
            public Identity CharacterIdentity { get; set; }

            public string CharacterIdentityText { get; set; }

            public ObjectiveProgressRecord Progress { get; set; }

            public DateTime ActivatedAtUtc { get; set; }

            public bool B18DToB18EHandoffAttempted { get; set; }
        }
    }
}
