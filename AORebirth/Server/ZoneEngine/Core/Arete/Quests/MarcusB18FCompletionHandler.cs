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

    public static class MarcusB18FCompletionHandler
    {
        private const int AreteLandingPlayfieldId = 6553;

        private const int MarcusStoneInstance = unchecked((int)0x782DE567);

        private const string B18FCompletionSourceNodeId = "marcus_195107_b18f_002";

        private const int B18FCompletionAnswerIndex = 0;

        private const string B18FCompletionOptionText =
            "So, let me guess... You need some help with the fire?";

        private static readonly Dictionary<string, MarcusB18FCompletionState> StateByCharacter =
            new Dictionary<string, MarcusB18FCompletionState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        public static MarcusB18FCompletionResult TryCompleteFromDialogue(
            ICharacter source,
            Identity npcIdentity,
            string previousNodeId,
            int answerIndex,
            string optionText,
            bool dialogueGateEnabled)
        {
            if (!IsB18FCompletionOption(previousNodeId, answerIndex))
            {
                return MarcusB18FCompletionResult.NotApplicable();
            }

            if (!string.Equals(optionText, B18FCompletionOptionText, StringComparison.Ordinal))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion blocked because captured option text did not match. "
                    + "node=" + previousNodeId
                    + " answer=" + answerIndex
                    + " noQuestDelete=true noB194=true noItem296780=true");
            }

            if (!dialogueGateEnabled)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because dialogue routing gate is disabled. "
                    + "attempted=false noQuestDelete=true noB194=true noItem296780=true");
            }

            if (!IsMarcusStone(npcIdentity))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion failed: target is not Marcus Stone. noQuestDelete=true noB194=true");
            }

            if (!IsValidPlayerInArete(source))
            {
                return MarcusB18FCompletionResult.Failed(
                    "Marcus B18F completion failed: source is missing, not a player, or not in Arete Landing 6553. "
                    + "noQuestDelete=true noB194=true");
            }

            RexMissionChainState chainState = RexMissionChainStateStore.GetState(source);
            if (chainState < RexMissionChainState.B18FPreviewed)
            {
                return MarcusB18FCompletionResult.Skipped(
                    "Marcus B18F completion skipped because Rex chain state is "
                    + chainState
                    + ". requiredState=B18FPreviewed noQuestDelete=true noB194=true");
            }

            MarcusB18FCompletionState completionState;
            lock (SyncRoot)
            {
                completionState = GetOrCreateState(source.Identity);
                if (completionState.TransitionCompleted)
                {
                    return MarcusB18FCompletionResult.Skipped(
                        "Marcus B18F completion skipped because B194 was already previewed. "
                        + "duplicateBlocked=true noDuplicateQuestDelete=true noDuplicateB194=true");
                }

                if (completionState.TransitionInProgress)
                {
                    return MarcusB18FCompletionResult.Skipped(
                        "Marcus B18F completion skipped because the transition is already in progress. "
                        + "duplicateBlocked=true noDuplicateQuestDelete=true noDuplicateB194=true");
                }

                completionState.TransitionInProgress = true;
            }

            RexQuestPreviewEmissionResult deleteResult = null;
            RexQuestPreviewEmissionResult b194Result = null;
            try
            {
                if (!completionState.B18FDeleteSent)
                {
                    deleteResult = SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
                    if (!deleteResult.Emitted)
                    {
                        return MarcusB18FCompletionResult.Failed(
                            "Marcus B18F completion failed before B194 because B18F Quest Delete was not emitted. "
                            + "message=\"" + deleteResult.Message + "\" noB194=true noItem296780=true "
                            + "noRewards=true noPersistence=true");
                    }

                    lock (SyncRoot)
                    {
                        completionState.B18FDeleteSent = true;
                    }
                }

                if (!completionState.B194QuestFullUpdateSent)
                {
                    b194Result = SafeQuestFullUpdateSender.TrySendB194Preview(source);
                    if (!b194Result.Emitted)
                    {
                        return MarcusB18FCompletionResult.Failed(
                            "Marcus B18F completion partially applied but B194 QuestFullUpdate was not emitted. "
                            + "b18fDeleteSent=true message=\"" + b194Result.Message + "\" "
                            + "item296780Deferred=true noRewards=true noPersistence=true");
                    }

                    lock (SyncRoot)
                    {
                        completionState.B194QuestFullUpdateSent = true;
                        completionState.TransitionCompleted = true;
                    }
                }

                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ARETE_MARCUS_B18F_COMPLETION transition applied character="
                    + source.Identity.ToString(true)
                    + " node=" + previousNodeId
                    + " answer=" + answerIndex
                    + " missionDelete=Mission:5514B18F nextQuestFullUpdate=Mission:5514B194 "
                    + "source=20260614-195107/events.log:1645-1646,packets.hex.log:1407 "
                    + "rawReplay=false duplicateGuard=processLocal item296780Deferred=true "
                    + "noRewards=true noTrade=true noDbMissionPersistence=true");

                return MarcusB18FCompletionResult.Succeeded(
                    "Marcus B18F completion applied b18fDeleteSent="
                    + completionState.B18FDeleteSent
                    + " b194QuestFullUpdateSent="
                    + completionState.B194QuestFullUpdateSent
                    + " deleteMessage=\""
                    + (deleteResult == null ? "already-sent" : deleteResult.Message)
                    + "\" b194Message=\""
                    + (b194Result == null ? "already-sent" : b194Result.Message)
                    + "\" item296780Deferred=true noRewards=true noTrade=true noDbMissionPersistence=true");
            }
            finally
            {
                lock (SyncRoot)
                {
                    completionState.TransitionInProgress = false;
                }
            }
        }

        private static bool IsB18FCompletionOption(string previousNodeId, int answerIndex)
        {
            return string.Equals(previousNodeId, B18FCompletionSourceNodeId, StringComparison.OrdinalIgnoreCase)
                   && answerIndex == B18FCompletionAnswerIndex;
        }

        private static bool IsMarcusStone(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == MarcusStoneInstance;
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

        private static MarcusB18FCompletionState GetOrCreateState(Identity identity)
        {
            string key = MakeCharacterKey(identity);
            MarcusB18FCompletionState state;
            if (!StateByCharacter.TryGetValue(key, out state))
            {
                state = new MarcusB18FCompletionState();
                StateByCharacter[key] = state;
            }

            return state;
        }

        private static string MakeCharacterKey(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture);
        }

        private sealed class MarcusB18FCompletionState
        {
            public bool TransitionInProgress { get; set; }

            public bool B18FDeleteSent { get; set; }

            public bool B194QuestFullUpdateSent { get; set; }

            public bool TransitionCompleted { get; set; }
        }
    }

    public sealed class MarcusB18FCompletionResult
    {
        private MarcusB18FCompletionResult()
        {
        }

        public bool IsApplicable { get; private set; }

        public bool Attempted { get; private set; }

        public bool Completed { get; private set; }

        public string Message { get; private set; }

        public static MarcusB18FCompletionResult NotApplicable()
        {
            return new MarcusB18FCompletionResult();
        }

        public static MarcusB18FCompletionResult Skipped(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = false,
                       Completed = false,
                       Message = message
                   };
        }

        public static MarcusB18FCompletionResult Succeeded(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = true,
                       Message = message
                   };
        }

        public static MarcusB18FCompletionResult Failed(string message)
        {
            return new MarcusB18FCompletionResult
                   {
                       IsApplicable = true,
                       Attempted = true,
                       Completed = false,
                       Message = message
                   };
        }
    }
}
