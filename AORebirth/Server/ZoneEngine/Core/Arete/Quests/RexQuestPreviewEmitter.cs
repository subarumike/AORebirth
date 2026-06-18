namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

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
                return IsTruthy(Environment.GetEnvironmentVariable(EnableEnvironmentVariableName));
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

            RexQuestPreviewEmissionResult result = SafeQuestFullUpdateSender.TrySendB18CPreview(source);
            RexB18CObjectiveProgressTracker.TryActivateFromPreview(source, result);
            return result;
        }

        private static bool IsB18CPreviewOption(string previousNodeId, int answerIndex)
        {
            return string.Equals(previousNodeId, B18CPreviewSourceNodeId, StringComparison.OrdinalIgnoreCase)
                   && answerIndex == B18CPreviewAnswerIndex;
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
        private static readonly Dictionary<string, RexMissionChainState> StateByCharacter =
            new Dictionary<string, RexMissionChainState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

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
            if (identity.Type != IdentityType.CanbeAffected || identity.Instance == 0)
            {
                return RexMissionChainState.NoRexMission;
            }

            RexMissionChainState state;
            lock (SyncRoot)
            {
                if (StateByCharacter.TryGetValue(MakeCharacterKey(identity), out state))
                {
                    return state;
                }
            }

            return RexMissionChainState.NoRexMission;
        }

        public static void AdvanceAtLeast(
            ICharacter character,
            RexMissionChainState targetState,
            string reason)
        {
            if (character == null
                || character.Identity.Type != IdentityType.CanbeAffected
                || character.Identity.Instance == 0)
            {
                return;
            }

            RexMissionChainState currentState;
            RexMissionChainState nextState;
            string characterText = character.Identity.ToString(true);
            lock (SyncRoot)
            {
                string key = MakeCharacterKey(character.Identity);
                if (!StateByCharacter.TryGetValue(key, out currentState))
                {
                    currentState = RexMissionChainState.NoRexMission;
                }

                nextState = currentState < targetState ? targetState : currentState;
                StateByCharacter[key] = nextState;
            }

            if (nextState != currentState)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ARETE_REX_CHAIN_STATE character=" + characterText
                    + " from=" + currentState
                    + " to=" + nextState
                    + " reason=\"" + (reason ?? string.Empty) + "\""
                    + " inMemoryOnly=true noPersistence=true noRewards=true noInventory=true noXpCredits=true");
            }
        }

        private static string MakeCharacterKey(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture);
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
