namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.Interfaces.Persistence.Missions;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// Mission-terminal roll fee. Capture 20260717-mission-terminal / Mission terminal2:
    /// each QuestAlternative request deducts credits equal to character level, then
    /// FormatFeedback "{N} credits were deducted from your account." (yellow system chat),
    /// then the 5-offer roll reply. Example: level 175 -> 175 credits.
    /// </summary>
    internal static class MissionRollFeeService
    {
        private static readonly object SyncRoot = new object();

        private static IMissionDao missionDao;

        internal static void Initialize(IMissionDao dao)
        {
            if (dao == null)
            {
                throw new ArgumentNullException("dao");
            }

            lock (SyncRoot)
            {
                missionDao = dao;
            }
        }

        public static bool TryChargeRollFee(
            ICharacter character,
            string batchIdentity,
            int expectedFee,
            out int fee,
            out int cashBefore,
            out int cashAfter,
            out bool insufficientCredits,
            out string failure)
        {
            fee = 0;
            cashBefore = 0;
            cashAfter = 0;
            insufficientCredits = false;
            failure = string.Empty;
            if (character == null
                || string.IsNullOrEmpty(batchIdentity)
                || expectedFee <= 0)
            {
                failure = "Exact character and durable roll batch are required.";
                return false;
            }

            int level = character.Stats[StatIds.level].Value;
            fee = MissionRollFeeRules.FeeForLevel(level);
            if (fee != expectedFee)
            {
                failure = "Durable roll-fee amount no longer matches the character level.";
                return false;
            }

            character.WriteStats();

            IMissionDao dao;
            lock (SyncRoot)
            {
                dao = missionDao;
            }

            if (dao == null)
            {
                throw new InvalidOperationException("Mission roll-fee persistence has not been initialized.");
            }

            MissionRollFeeResult result = dao.TryChargeRollFee(
                new MissionRollFeeRequest
                {
                    CharacterType = (int)character.Identity.Type,
                    CharacterId = character.Identity.Instance,
                    BatchIdentity = batchIdentity,
                    Fee = fee,
                    AppliedAtUtcTicks = DateTime.UtcNow.Ticks
                });
            cashBefore = result.CashBefore;
            cashAfter = result.CashAfter;
            if (result.Status == MissionRollFeeStatus.InsufficientCredits)
            {
                insufficientCredits = true;
                return false;
            }

            if (result.Status != MissionRollFeeStatus.Applied
                && result.Status != MissionRollFeeStatus.AlreadyApplied)
            {
                failure = result.Failure;
                return false;
            }

            return true;
        }

        internal static void NotifyRollFeeApplied(
            ICharacter character,
            int fee,
            int cashBefore,
            int cashAfter)
        {
            if (character == null || fee <= 0 || cashBefore < fee || cashAfter < 0)
            {
                throw new ArgumentException("Valid applied roll-fee details are required.");
            }

            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);

            SendYellowFeedback(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} credits were deducted from your account.",
                    fee));

            MissionDiagnostics.Log(
                "ROLL-FEE char={0} fee={1} cashBefore={2} cashAfter={3}",
                character.Identity.Instance,
                fee,
                cashBefore,
                cashAfter);
        }

        internal static void NotifyRollFeeRejected(ICharacter character, int fee, int cashBefore)
        {
            if (character == null || fee <= 0 || cashBefore < 0)
            {
                throw new ArgumentException("Valid rejected roll-fee details are required.");
            }

            SendYellowFeedback(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "You need {0} credits to request a mission.",
                    fee));
            MissionDiagnostics.Log(
                "ROLL-FEE-FAIL char={0} fee={1} cash={2}",
                character.Identity.Instance,
                fee,
                cashBefore);
        }

        internal static bool TryRecoverAndSendForLogin(IZoneClient client, ICharacter character)
        {
            if (client == null || character == null)
            {
                return false;
            }

            lock (MissionOfferStore.AuthorityGate)
            {
                MissionAcgBindingRuntime.Initialize();
                bool feeClaimFound;
                MissionOfferBatchHandle feeBatch;
                int persistedFee;
                QuestAlternativeMessage feeResponse;
                string failure;
                if (!MissionOfferStore.TryGetFeeChargePending(
                        character.Identity,
                        DateTime.UtcNow,
                        out feeClaimFound,
                        out feeBatch,
                        out persistedFee,
                        out feeResponse,
                        out failure))
                {
                    MissionDiagnostics.Log(
                        "ROLL-RESTORE-FAIL char={0} phase=fee-claim error={1}",
                        character.Identity.Instance,
                        failure);
                    return false;
                }

                if (feeClaimFound)
                {
                    int appliedFee;
                    int cashBefore;
                    int cashAfter;
                    bool insufficientCredits;
                    if (!TryChargeRollFee(
                            character,
                            feeBatch.BatchIdentity,
                            persistedFee,
                            out appliedFee,
                            out cashBefore,
                            out cashAfter,
                            out insufficientCredits,
                            out failure))
                    {
                        if (insufficientCredits)
                        {
                            string discardFailure;
                            if (MissionOfferStore.TryDiscardBatch(
                                    feeBatch,
                                    DateTime.UtcNow,
                                    "RecoveredRollFeeRejected",
                                    out discardFailure))
                            {
                                NotifyRollFeeRejected(character, appliedFee, cashBefore);
                            }
                            else
                            {
                                failure = discardFailure;
                            }
                        }

                        MissionDiagnostics.Log(
                            "ROLL-RESTORE-FAIL char={0} phase=fee-apply error={1}",
                            character.Identity.Instance,
                            failure);
                        return false;
                    }

                    NotifyRollFeeApplied(character, appliedFee, cashBefore, cashAfter);
                    if (!MissionOfferStore.TryPublishBatch(feeBatch, DateTime.UtcNow, out failure))
                    {
                        MissionDiagnostics.Log(
                            "ROLL-RESTORE-FAIL char={0} phase=publish error={1}",
                            character.Identity.Instance,
                            failure);
                        return false;
                    }
                }

                bool pendingFound;
                MissionOfferBatchHandle pendingBatch;
                QuestAlternativeMessage pendingResponse;
                if (!MissionOfferStore.TryGetPendingRollForLogin(
                        character.Identity,
                        DateTime.UtcNow,
                        out pendingFound,
                        out pendingBatch,
                        out pendingResponse,
                        out failure))
                {
                    MissionDiagnostics.Log(
                        "ROLL-RESTORE-FAIL char={0} phase=pending error={1}",
                        character.Identity.Instance,
                        failure);
                    return false;
                }

                if (!pendingFound)
                {
                    return feeClaimFound;
                }

                client.SendCompressed(pendingResponse);
                MissionDiagnostics.Log(
                    "ROLL-RESTORE char={0} batch={1} offers={2}",
                    character.Identity.Instance,
                    pendingBatch.BatchIdentity,
                    pendingResponse.QuestInfos == null ? 0 : pendingResponse.QuestInfos.Length);
                return true;
            }
        }

        private static void SendYellowFeedback(ICharacter character, string plainText)
        {
            if (character == null || string.IsNullOrEmpty(plainText))
            {
                return;
            }

            // Capture: FormatFeedback alone paints yellow system chat (TokenBoard / insurance path).
            character.Send(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(plainText)
                });
        }
    }
}
