namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

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

        public static bool IsPreviewCompletionEnabled
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

            if (!MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(source.Identity.Instance, MissionId);
            if (IsTerminalFailure(offer))
            {
                return false;
            }

            MissionOperationResult accept = MissionRuntime.Service.AcceptMission(source.Identity.Instance, MissionId);
            if (IsTerminalFailure(accept))
            {
                return false;
            }

            Log(
                "activated mission={0} character={1} progress=0/{2} previewReceived=true persistent=true",
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

            if (!MissionRuntime.IsInitialized)
            {
                return true;
            }

            ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(source.Identity.Instance, MissionId);
            if (mission == null
                || (mission.State != MissionLifecycleState.Active
                    && mission.State != MissionLifecycleState.Completed))
            {
                // Not our quested cargo use — let coordinator emit capture-backed reject.
                return false;
            }

            string observationKey = "terminal-use:" + target.ToString(true);
            ObjectiveProgressRecord progressSnapshot;
            if (mission.State == MissionLifecycleState.Active)
            {
                MissionOperationResult observation = MissionRuntime.Service.ObserveObjective(
                    new MissionObjectiveObservation
                    {
                        CharacterId = source.Identity.Instance,
                        QuestId = MissionId,
                        ObjectiveId = ObjectiveId,
                        ObservationKey = observationKey,
                        Amount = 1,
                        EventType = "GenericCmd:Use",
                        SourceIdentity = source.Identity.ToString(true),
                        TargetIdentity = target.ToString(true)
                    });
                if (observation.Status != MissionOperationStatus.Applied
                    && observation.Status != MissionOperationStatus.AlreadyApplied
                    && observation.Status != MissionOperationStatus.DuplicateObservation)
                {
                    return true;
                }

                progressSnapshot = ToRuntimeProgress(observation.Objective, observationKey);
            }
            else
            {
                MissionObjectiveProgressRecord persistedProgress = MissionRuntime.Service.GetObjective(
                    source.Identity.Instance,
                    MissionId,
                    ObjectiveId);
                progressSnapshot = ToRuntimeProgress(
                    persistedProgress,
                    persistedProgress == null ? observationKey : persistedProgress.LastObservationKey);
            }

            if (progressSnapshot == null || !progressSnapshot.Completed)
            {
                return true;
            }

            MissionOperationResult completion = MissionRuntime.Service.CompleteAndActivateNextMission(
                source.Identity.Instance,
                MissionId,
                MissionRuntime.RexB18EQuestId);
            bool handoffReady = completion.Status == MissionOperationStatus.Applied
                                || completion.Status == MissionOperationStatus.AlreadyApplied;
            if (handoffReady)
            {
                RexMissionChainStateStore.AdvanceAtLeast(
                    source,
                    RexMissionChainState.B18EPreviewed,
                    "B18D completion activated B18E persistently");
            }

            bool deleteProjected = handoffReady
                                   && EnsureQuestProjection(
                                       source,
                                       "b18d-delete-projected",
                                       () => SafeQuestFullUpdateSender.TrySendB18DQuestDelete(source));
            bool b18eProjected = handoffReady
                                 && EnsureQuestProjection(
                                     source,
                                     "b18e-preview-projected",
                                     () => SafeQuestFullUpdateSender.TrySendB18EPreview(source));

            Log(
                "objective observed mission={0} character={1} target={2} signal=\"GenericCmd Action=Use\" evidence=20260614-194454/events.log:6327,6333 progress={3}/{4} complete=true persistent=true b18dQuestDeleteProjected={5} b18eQuestFullUpdateProjected={6}",
                MissionId,
                source.Identity.ToString(true),
                target.ToString(true),
                progressSnapshot.CurrentCount,
                progressSnapshot.RequiredCount,
                deleteProjected,
                b18eProjected);

            return true;
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

            RexQuestPreviewEmissionResult projection = sender();
            if (projection == null || !projection.Emitted)
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
                "ARETE_REX_B18D_PREVIEW "
                + string.Format(CultureInfo.InvariantCulture, format, args));
        }

    }
}
