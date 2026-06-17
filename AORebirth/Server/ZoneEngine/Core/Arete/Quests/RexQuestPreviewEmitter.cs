namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

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

            return SafeQuestFullUpdateSender.TrySendB18CPreview(source);
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
