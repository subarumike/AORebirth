namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Missions;

    #endregion

    public static class RexQuestPreviewEmitter
    {
        public const string EnableEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW";

        private const int AreteLandingPlayfieldId = 6553;

        private const int RexLarssonInstance = unchecked((int)0x782DE568);

        private const string B18CPreviewSourceNodeId = "rex_194454_004";

        private const int B18CPreviewAnswerIndex = 0;

        public static bool IsQuestPreviewEnabled
        {
            get
            {
                return AreteEnvironmentGate.IsDefaultEnabled(EnableEnvironmentVariableName);
            }
        }

        public static RexQuestPreviewEmissionResult TryEmitB18CPreview(
            ICharacter source,
            Identity npcIdentity,
            string previousNodeId,
            int answerIndex,
            bool dialogueGateEnabled)
        {
            if (!IsB18CPreviewOption(previousNodeId, answerIndex))
            {
                return RexQuestPreviewEmissionResult.NotApplicable();
            }

            bool questPreviewGateEnabled = IsQuestPreviewEnabled;
            if (!dialogueGateEnabled || !questPreviewGateEnabled)
            {
                return RexQuestPreviewEmissionResult.Skipped(
                    "B18C quest preview skipped dialogueGate="
                    + dialogueGateEnabled
                    + " questPreviewGate="
                    + questPreviewGateEnabled
                    + " attempted=false noPersistence=true noRewards=true noCompletion=true");
            }

            if (source == null)
            {
                return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed: source character missing.");
            }

            RexMissionChainState chainState = RexMissionChainStateStore.GetState(source);
            if (chainState != RexMissionChainState.NoRexMission)
            {
                return RexQuestPreviewEmissionResult.Skipped(
                    "B18C quest preview skipped because Rex chain state is "
                    + chainState
                    + ". duplicateOfferBlocked=true noPersistence=true noRewards=true noCompletion=true");
            }

            if (!IsRexLarsson(npcIdentity))
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C quest preview failed: target is not Rex Larsson.");
            }

            if (!IsInAreteLanding(source))
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C quest preview failed: source character is not in Arete Landing 6553.");
            }

            if (!MissionRuntime.IsInitialized)
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C quest preview failed: persistent mission runtime is not initialized.");
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(
                source.Identity.Instance,
                MissionRuntime.RexB18CQuestId);
            if (IsPersistenceFailure(offer))
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C quest preview failed before packet projection: " + offer.Message);
            }

            MissionOperationResult accept = MissionRuntime.Service.AcceptMission(
                source.Identity.Instance,
                MissionRuntime.RexB18CQuestId);
            if (IsPersistenceFailure(accept))
            {
                return RexQuestPreviewEmissionResult.Failed(
                    "B18C quest acceptance failed before packet projection: " + accept.Message);
            }

            RexQuestPreviewEmissionResult result = SafeQuestFullUpdateSender.TrySendB18CPreview(source);
            RexB18CObjectiveProgressTracker.TryActivateFromPreview(source, result);
            return result;
        }

        private static bool IsB18CPreviewOption(string previousNodeId, int answerIndex)
        {
            return string.Equals(previousNodeId, B18CPreviewSourceNodeId, StringComparison.OrdinalIgnoreCase)
                   && answerIndex == B18CPreviewAnswerIndex;
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result == null
                   || result.Status == MissionOperationStatus.Rejected
                   || result.Status == MissionOperationStatus.NotFound
                   || result.Status == MissionOperationStatus.Unresolved;
        }

        private static bool IsRexLarsson(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == RexLarssonInstance;
        }

        private static bool IsInAreteLanding(ICharacter source)
        {
            return source != null
                   && source.Playfield != null
                   && source.Playfield.Identity.Instance == AreteLandingPlayfieldId;
        }

    }

    public enum RexMissionChainState
    {
        NoRexMission = 0,
        B18CPreviewed = 1,
        B18CObjectiveComplete = 2,
        B18DPreviewed = 3,
        B18DObjectiveComplete = 4,
        B18EPreviewed = 5,
        B18ECompleted = 6,
        B18FPreviewed = 7
    }

    public static class RexMissionChainStateStore
    {
        public static RexMissionChainState GetState(ICharacter character)
        {
            if (character == null)
            {
                return RexMissionChainState.NoRexMission;
            }

            return GetState(character.Identity);
        }

        public static RexMissionChainState GetState(Identity identity)
        {
            if (!MissionRuntime.IsInitialized
                || identity.Type != IdentityType.CanbeAffected
                || identity.Instance == 0)
            {
                return RexMissionChainState.NoRexMission;
            }

            int characterId = identity.Instance;
            ZoneEngine.Core.Missions.MissionStateRecord b18f = MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18FQuestId);
            if (IsOfferedOrLater(b18f))
            {
                return RexMissionChainState.B18FPreviewed;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b18e = MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18EQuestId);
            if (b18e != null && b18e.State == MissionLifecycleState.Completed)
            {
                return RexMissionChainState.B18ECompleted;
            }

            if (IsOfferedOrLater(b18e))
            {
                return RexMissionChainState.B18EPreviewed;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b18d = MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18DQuestId);
            if (b18d != null && b18d.State == MissionLifecycleState.Completed)
            {
                return RexMissionChainState.B18DObjectiveComplete;
            }

            if (IsOfferedOrLater(b18d))
            {
                return RexMissionChainState.B18DPreviewed;
            }

            ZoneEngine.Core.Missions.MissionStateRecord b18c = MissionRuntime.Service.GetMission(characterId, MissionRuntime.RexB18CQuestId);
            if (b18c != null && b18c.State == MissionLifecycleState.Completed)
            {
                return RexMissionChainState.B18CObjectiveComplete;
            }

            if (IsOfferedOrLater(b18c))
            {
                return RexMissionChainState.B18CPreviewed;
            }

            return RexMissionChainState.NoRexMission;
        }

        public static void AdvanceAtLeast(
            ICharacter character,
            RexMissionChainState targetState,
            string reason)
        {
            if (!MissionRuntime.IsInitialized
                || character == null
                || character.Identity.Type != IdentityType.CanbeAffected
                || character.Identity.Instance == 0)
            {
                return;
            }

            int characterId = character.Identity.Instance;
            RexMissionChainState currentState = GetState(character);
            MissionOperationResult operation = null;

            if (targetState >= RexMissionChainState.B18CPreviewed
                && currentState < RexMissionChainState.B18CPreviewed)
            {
                operation = EnsureActive(characterId, MissionRuntime.RexB18CQuestId);
            }

            if (targetState >= RexMissionChainState.B18CObjectiveComplete)
            {
                operation = MissionRuntime.Service.CompleteMission(characterId, MissionRuntime.RexB18CQuestId);
            }

            if (targetState >= RexMissionChainState.B18DPreviewed)
            {
                operation = EnsureActive(characterId, MissionRuntime.RexB18DQuestId);
            }

            if (targetState >= RexMissionChainState.B18DObjectiveComplete)
            {
                operation = MissionRuntime.Service.CompleteMission(characterId, MissionRuntime.RexB18DQuestId);
            }

            if (targetState >= RexMissionChainState.B18EPreviewed)
            {
                operation = EnsureActive(characterId, MissionRuntime.RexB18EQuestId);
            }

            if (targetState >= RexMissionChainState.B18ECompleted)
            {
                operation = MissionRuntime.Service.CompleteMission(characterId, MissionRuntime.RexB18EQuestId);
            }

            if (targetState >= RexMissionChainState.B18FPreviewed)
            {
                operation = EnsureActive(characterId, MissionRuntime.RexB18FQuestId);
            }

            RexMissionChainState nextState = GetState(character);
            if (nextState != currentState || (operation != null && operation.Status == MissionOperationStatus.Rejected))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ARETE_REX_CHAIN_STATE character=" + character.Identity.ToString(true)
                    + " from=" + currentState
                    + " to=" + nextState
                    + " target=" + targetState
                    + " status=" + (operation == null ? "none" : operation.Status.ToString())
                    + " reason=\"" + (reason ?? string.Empty) + "\" persistent=true");
            }
        }

        private static MissionOperationResult EnsureActive(int characterId, string questId)
        {
            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (offer.Status == MissionOperationStatus.Rejected
                || offer.Status == MissionOperationStatus.Unresolved
                || offer.Status == MissionOperationStatus.NotFound)
            {
                return offer;
            }

            return MissionRuntime.Service.AcceptMission(characterId, questId);
        }

        private static bool IsOfferedOrLater(ZoneEngine.Core.Missions.MissionStateRecord mission)
        {
            return mission != null
                   && (mission.State == MissionLifecycleState.Offered
                       || mission.State == MissionLifecycleState.Active
                       || mission.State == MissionLifecycleState.Completed);
        }
    }

    public sealed class RexQuestPreviewEmissionResult
    {
        private RexQuestPreviewEmissionResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Attempted { get; private set; }

        public bool Emitted { get; private set; }

        public string Message { get; private set; }

        public static RexQuestPreviewEmissionResult NotApplicable()
        {
            return new RexQuestPreviewEmissionResult();
        }

        public static RexQuestPreviewEmissionResult Skipped(string message)
        {
            return new RexQuestPreviewEmissionResult
            {
                IsApplicable = true,
                Attempted = false,
                Emitted = false,
                Message = message
            };
        }

        public static RexQuestPreviewEmissionResult Sent(string message)
        {
            return new RexQuestPreviewEmissionResult
            {
                IsApplicable = true,
                Attempted = true,
                Emitted = true,
                Message = message
            };
        }

        public static RexQuestPreviewEmissionResult Failed(string message)
        {
            return new RexQuestPreviewEmissionResult
            {
                IsApplicable = true,
                Attempted = true,
                Emitted = false,
                Message = message
            };
        }
    }
}
