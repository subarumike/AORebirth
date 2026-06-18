namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

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

        private static readonly Dictionary<string, RexB18ECompletionState> StateByCharacter =
            new Dictionary<string, RexB18ECompletionState>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new object();

        public static bool IsCompletionEnabled
        {
            get
            {
                return IsTruthy(Environment.GetEnvironmentVariable(EnableEnvironmentVariableName));
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

            RexMissionChainState chainState = RexMissionChainStateStore.GetState(source);
            if (chainState < RexMissionChainState.B18EPreviewed)
            {
                return RexB18ECompletionResult.Skipped(
                    "B18E completion skipped because Rex chain state is "
                    + chainState
                    + ". b18ePreviewRequired=true noAction59=true noRewards=true noQuestDelete=true");
            }

            if (chainState >= RexMissionChainState.B18FPreviewed)
            {
                return RexB18ECompletionResult.Skipped(
                    "B18E completion skipped because B18F was already previewed. duplicateCompletionBlocked=true "
                    + "noDuplicateXp=true noDuplicateQuestDelete=true noDuplicateB18F=true");
            }

            RexB18ECompletionState completionState = GetOrCreateState(source.Identity);

            RexQuestPreviewEmissionResult deleteResult = null;
            if (!completionState.B18EDeleteSent)
            {
                deleteResult = SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
                if (!deleteResult.Emitted)
                {
                    return RexB18ECompletionResult.Failed(
                        "B18E completion failed before XP/B18F because Quest Delete was not emitted. "
                        + "message=\"" + deleteResult.Message + "\" noAction59=true noXpGranted=true "
                        + "noCreditsGranted=true noRewardFeedback=true noItems=true noInventory=true "
                        + "noDbMissionPersistence=true");
                }

                lock (SyncRoot)
                {
                    completionState.B18EDeleteSent = true;
                }
            }

            RewardFeedbackResult rewardFeedbackResult = null;
            if (!completionState.RewardFeedbackSent)
            {
                rewardFeedbackResult = SendCapturedRewardFeedback(source);
                lock (SyncRoot)
                {
                    completionState.RewardFeedbackSent = rewardFeedbackResult.Sent;
                }
            }

            CreditGrantResult creditResult = null;
            if (!completionState.CreditsGranted)
            {
                creditResult = GrantCapturedCredits(source);
                lock (SyncRoot)
                {
                    completionState.CreditsGranted = true;
                    completionState.CashBefore = creditResult.CashBefore;
                    completionState.CashAfter = creditResult.CashAfter;
                }
            }

            XpGrantResult xpResult = null;
            if (!completionState.XpGranted)
            {
                xpResult = GrantCapturedXp(source);
                lock (SyncRoot)
                {
                    completionState.XpGranted = true;
                    completionState.XpBefore = xpResult.XpBefore;
                    completionState.XpAfter = xpResult.XpAfter;
                }
            }

            RexQuestPreviewEmissionResult b18fResult = null;
            if (!completionState.B18FQuestFullUpdateSent)
            {
                b18fResult = SafeQuestFullUpdateSender.TrySendB18FPreview(source);
                if (!b18fResult.Emitted)
                {
                    RexMissionChainStateStore.AdvanceAtLeast(
                        source,
                        RexMissionChainState.B18ECompleted,
                        "B18E delete and XP applied; B18F QuestFullUpdate retry still pending");

                    return RexB18ECompletionResult.Failed(
                        "B18E completion partially applied but B18F QuestFullUpdate was not emitted. "
                        + "b18eDeleteSent=true rewardFeedbackSent=" + completionState.RewardFeedbackSent
                        + " creditsGranted=true xpGranted=true message=\"" + b18fResult.Message + "\" "
                        + "noAction59=true noItems=true noInventory=true "
                        + "noDbMissionPersistence=true noMarcusStoneImplementation=true");
                }

                lock (SyncRoot)
                {
                    completionState.B18FQuestFullUpdateSent = true;
                }
            }

            RexMissionChainStateStore.AdvanceAtLeast(
                source,
                RexMissionChainState.B18FPreviewed,
                "B18E return completed and B18F QuestFullUpdate sent");

            return RexB18ECompletionResult.Succeeded(
                "B18E completion applied mission=Mission:5514B18E deleteSent="
                + completionState.B18EDeleteSent
                + " xpGranted=true xpDelta=290 xpBefore="
                + completionState.XpBefore
                + " xpAfter="
                + completionState.XpAfter
                + " creditsGranted=true creditDelta=1040 cashBefore="
                + completionState.CashBefore
                + " cashAfter="
                + completionState.CashAfter
                + " rewardFeedbackSent="
                + completionState.RewardFeedbackSent
                + " rewardFeedbackMessage=\""
                + RewardFeedbackText
                + "\" rewardMessageDisplayXp="
                + RewardMessageDisplayXp
                + " b18fQuestFullUpdateSent="
                + completionState.B18FQuestFullUpdateSent
                + " b18fMission=Mission:5514B18F nextNpc=SimpleChar:782DE567 "
                + "deleteMessage=\""
                + (deleteResult == null ? "already-sent" : deleteResult.Message)
                + "\" xpMessage=\""
                + (xpResult == null ? "already-granted" : xpResult.Message)
                + "\" creditMessage=\""
                + (creditResult == null ? "already-granted" : creditResult.Message)
                + "\" rewardFeedbackStatus=\""
                + (rewardFeedbackResult == null ? "already-sent" : rewardFeedbackResult.Message)
                + "\" b18fMessage=\""
                + (b18fResult == null ? "already-sent" : b18fResult.Message)
                + "\" noAction59=true noApplied1281Xp=true noItems=true noInventory=true "
                + "noDbMissionPersistence=true noMarcusStoneImplementation=true");
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

        private static CreditGrantResult GrantCapturedCredits(ICharacter source)
        {
            uint cashBeforeBase = source.Stats[StatIds.cash].BaseValue;
            int cashBefore = CashStatRules.Clamp(cashBeforeBase);
            int cashAfter = CashStatRules.Clamp((long)cashBefore + CreditReward);

            source.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendChanged(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_REX_B18E_COMPLETION credit grant applied character="
                + source.Identity.ToString(true)
                + " cashBefore=" + cashBefore.ToString(CultureInfo.InvariantCulture)
                + " cashBeforeBase=" + cashBeforeBase.ToString(CultureInfo.InvariantCulture)
                + " cashAfter=" + cashAfter.ToString(CultureInfo.InvariantCulture)
                + " creditDelta=1040 source=20260618-083035/events.log:1077,system-messages.log:282 "
                + "noAction59=true noItems=true noInventory=true noDbMissionPersistence=true");

            return new CreditGrantResult
                   {
                       CashBefore = cashBefore,
                       CashAfter = cashAfter,
                       Message = "Credits +1040 applied using existing cash stat update path."
                   };
        }

        private static XpGrantResult GrantCapturedXp(ICharacter source)
        {
            uint xpBefore = source.Stats[StatIds.xp].BaseValue;
            uint unsavedXpBefore = source.Stats[StatIds.unsavedxp].BaseValue;
            uint xpAfter = AddClamped(xpBefore, XpReward);
            uint unsavedXpAfter = AddClamped(unsavedXpBefore, XpReward);

            source.Stats[StatIds.xp].Set(xpAfter);
            source.Stats[StatIds.lastxp].Set((uint)XpReward);
            source.Stats[StatIds.unsavedxp].Set(unsavedXpAfter);
            StatMessageHandler.Default.SendChanged(source);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "ARETE_REX_B18E_COMPLETION xp grant applied character="
                + source.Identity.ToString(true)
                + " xpBefore=" + xpBefore.ToString(CultureInfo.InvariantCulture)
                + " xpAfter=" + xpAfter.ToString(CultureInfo.InvariantCulture)
                + " xpDelta=290 displayXp=1281 displayXpNotApplied=true "
                + "source=20260618-083035/events.log:923,1078 "
                + "noItems=true noInventory=true noDbMissionPersistence=true");

            return new XpGrantResult
                   {
                       XpBefore = xpBefore,
                       XpAfter = xpAfter,
                       Message = "XP +290 applied using existing stat update path."
                   };
        }

        private static uint AddClamped(uint value, int delta)
        {
            uint safeDelta = (uint)Math.Max(0, delta);
            if (value > uint.MaxValue - safeDelta)
            {
                return uint.MaxValue;
            }

            return value + safeDelta;
        }

        private static RexB18ECompletionState GetOrCreateState(Identity identity)
        {
            lock (SyncRoot)
            {
                string key = MakeCharacterKey(identity);
                RexB18ECompletionState state;
                if (!StateByCharacter.TryGetValue(key, out state))
                {
                    state = new RexB18ECompletionState();
                    StateByCharacter[key] = state;
                }

                return state;
            }
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

        private static string MakeCharacterKey(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString("X8", CultureInfo.InvariantCulture);
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

        private sealed class RexB18ECompletionState
        {
            public bool B18EDeleteSent { get; set; }

            public bool RewardFeedbackSent { get; set; }

            public bool CreditsGranted { get; set; }

            public bool XpGranted { get; set; }

            public bool B18FQuestFullUpdateSent { get; set; }

            public uint XpBefore { get; set; }

            public uint XpAfter { get; set; }

            public int CashBefore { get; set; }

            public int CashAfter { get; set; }
        }

        private sealed class RewardFeedbackResult
        {
            public bool Sent { get; set; }

            public string Message { get; set; }
        }

        private sealed class CreditGrantResult
        {
            public int CashBefore { get; set; }

            public int CashAfter { get; set; }

            public string Message { get; set; }
        }

        private sealed class XpGrantResult
        {
            public uint XpBefore { get; set; }

            public uint XpAfter { get; set; }

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
