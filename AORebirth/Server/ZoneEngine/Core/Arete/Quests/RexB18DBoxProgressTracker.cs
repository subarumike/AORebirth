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

    public static class RexB18DBoxProgressTracker
    {
        public const string EnableEnvironmentVariableName =
            "AO_REBIRTH_ENABLE_ARETE_REX_B18D_BOX_PROGRESS";

        private const int AreteLandingPlayfieldId = 6553;

        private const int CargoBoxInstance = unchecked((int)0x56D9B4AF);

        private const string MissionId = "Mission:5514B18D";

        private static readonly HashSet<string> HandoffSentByCharacter =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        public static bool IsProgressEnabled
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
                       && IsProgressEnabled;
            }
        }

        public static bool TryObserveBoxUse(
            ICharacter source,
            Identity target,
            out bool shouldSendHandoff)
        {
            shouldSendHandoff = false;

            if (!IsCargoBoxTarget(target))
            {
                return false;
            }

            if (!AreAllGatesEnabled)
            {
                Log(
                    "use ignored mission={0} reason=gates-disabled character={1} target={2} b18dGate={3} inMemoryOnly=true",
                    MissionId,
                    IdentityText(source),
                    target.ToString(true),
                    IsProgressEnabled);
                return false;
            }

            if (!IsValidPlayerInArete(source))
            {
                Log(
                    "use ignored mission={0} reason=invalid-player-or-playfield character={1} target={2} inMemoryOnly=true",
                    MissionId,
                    IdentityText(source),
                    target.ToString(true));
                return true;
            }

            string characterKey = MakeCharacterKey(source.Identity);
            lock (SyncRoot)
            {
                if (HandoffSentByCharacter.Contains(characterKey))
                {
                    Log(
                        "use ignored mission={0} reason=handoff-already-sent character={1} target={2} inMemoryOnly=true noDuplicateQuestPackets=true",
                        MissionId,
                        source.Identity.ToString(true),
                        target.ToString(true));
                    return true;
                }

                HandoffSentByCharacter.Add(characterKey);
                shouldSendHandoff = true;
            }

            Log(
                "use matched mission={0} character={1} target={2} signal=\"GenericCmd Action=Use\" evidence=20260614-194454/events.log:6327,6333 packetHandoffPending=true noRewards=true noDbWrites=true",
                MissionId,
                source.Identity.ToString(true),
                target.ToString(true));
            return true;
        }

        private static bool IsCargoBoxTarget(Identity target)
        {
            return target.Type == IdentityType.Terminal
                   && target.Instance == CargoBoxInstance;
        }

        private static bool IsValidPlayerInArete(ICharacter source)
        {
            return source != null
                   && source.Identity.Type == IdentityType.CanbeAffected
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
                "ARETE_REX_B18D_BOX_PROGRESS "
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
    }
}
