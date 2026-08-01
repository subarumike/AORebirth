namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Data;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Database;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using Dapper;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Mission-terminal roll fee. Capture 20260717-mission-terminal / Mission terminal2:
    /// each QuestAlternative request deducts credits equal to character level, then
    /// FormatFeedback "{N} credits were deducted from your account." (yellow system chat),
    /// then the 5-offer roll reply. Example: level 175 → 175 credits.
    /// </summary>
    internal static class MissionRollFeeService
    {
        /// <summary>
        /// Attempts to charge the roll fee. On success updates Cash and sends the yellow deduct message.
        /// On failure (not enough credits) sends a yellow notice and returns false — caller should not roll.
        /// </summary>
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

            MissionRollFeeApplyResult result =
                MissionRollFeeClaimRepository.TryApply(
                    (int)character.Identity.Type,
                    character.Identity.Instance,
                    batchIdentity,
                    fee,
                    DateTime.UtcNow.Ticks);
            cashBefore = result.CashBefore;
            cashAfter = result.CashAfter;
            if (result.Status == MissionRollFeeApplyStatus.InsufficientCredits)
            {
                insufficientCredits = true;
                return false;
            }

            if (result.Status != MissionRollFeeApplyStatus.Applied
                && result.Status != MissionRollFeeApplyStatus.AlreadyApplied)
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
            StatMessageHandler.Default.SendSingle(
                character,
                (int)StatIds.cash,
                (uint)cashAfter);

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

        internal static void NotifyRollFeeRejected(
            ICharacter character,
            int fee,
            int cashBefore)
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

        internal static bool TryRecoverAndSendForLogin(
            IZoneClient client,
            ICharacter character)
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
                                NotifyRollFeeRejected(
                                    character,
                                    appliedFee,
                                    cashBefore);
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

                    NotifyRollFeeApplied(
                        character,
                        appliedFee,
                        cashBefore,
                        cashAfter);
                    if (!MissionOfferStore.TryPublishBatch(
                            feeBatch,
                            DateTime.UtcNow,
                            out failure))
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
                    pendingResponse.QuestInfos == null
                        ? 0
                        : pendingResponse.QuestInfos.Length);
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

    internal enum MissionRollFeeApplyStatus
    {
        Applied = 1,
        AlreadyApplied = 2,
        InsufficientCredits = 3,
        Conflict = 4
    }

    internal sealed class MissionRollFeeApplyResult
    {
        internal MissionRollFeeApplyStatus Status { get; set; }

        internal int CashBefore { get; set; }

        internal int CashAfter { get; set; }

        internal string Failure { get; set; }
    }

    internal static class MissionRollFeeClaimRepository
    {
        private const string RewardKey = "roll-fee";

        private const string RewardType = "GeneratedMissionRollFee";

        internal static MissionRollFeeApplyResult TryApply(
            int characterType,
            int characterInstance,
            string batchIdentity,
            int fee,
            long appliedAtUtcTicks)
        {
            if (characterType <= 0
                || characterInstance <= 0
                || string.IsNullOrEmpty(batchIdentity)
                || batchIdentity.Length > 96
                || fee <= 0
                || appliedAtUtcTicks <= 0)
            {
                return Conflict("Roll-fee claim identity or amount is invalid.");
            }

            string questId = "generated-offer:" + batchIdentity;
            using (IDbConnection connection = Connector.GetConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    int cashBefore = ReadCash(
                        connection,
                        transaction,
                        characterType,
                        characterInstance);
                    MissionRollFeeLedgerRow existing =
                        connection.Query<MissionRollFeeLedgerRow>(
                            "SELECT RewardType, Status, EffectReference FROM missionrewardledger "
                            + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                            + "AND RewardKey=@RewardKey FOR UPDATE",
                            new
                            {
                                CharacterId = characterInstance,
                                QuestId = questId,
                                RewardKey
                            },
                            transaction).FirstOrDefault();
                    if (existing != null)
                    {
                        int recordedFee;
                        int recordedBefore;
                        int recordedAfter;
                        if (!string.Equals(
                                existing.RewardType,
                                RewardType,
                                StringComparison.Ordinal)
                            || existing.Status != (int)MissionRewardStatus.Applied
                            || !TryParseEffectReference(
                                existing.EffectReference,
                                batchIdentity,
                                out recordedFee,
                                out recordedBefore,
                                out recordedAfter)
                            || recordedFee != fee)
                        {
                            transaction.Rollback();
                            return Conflict(
                                "Existing durable roll-fee claim conflicts with this batch.");
                        }

                        transaction.Commit();
                        return new MissionRollFeeApplyResult
                               {
                                   Status = MissionRollFeeApplyStatus.AlreadyApplied,
                                   CashBefore = recordedBefore,
                                   CashAfter = cashBefore,
                                   Failure = string.Empty
                               };
                    }

                    if (cashBefore < fee)
                    {
                        transaction.Rollback();
                        return new MissionRollFeeApplyResult
                               {
                                   Status = MissionRollFeeApplyStatus.InsufficientCredits,
                                   CashBefore = cashBefore,
                                   CashAfter = cashBefore,
                                   Failure = "Insufficient credits for generated mission roll fee."
                               };
                    }

                    int cashAfter = cashBefore - fee;
                    connection.Execute(
                        "INSERT INTO stats (Instance, Type, StatId, StatValue) "
                        + "VALUES (@Instance, @Type, @StatId, @StatValue) "
                        + "ON DUPLICATE KEY UPDATE StatValue=@StatValue",
                        new
                        {
                            Instance = characterInstance,
                            Type = characterType,
                            StatId = (int)StatIds.cash,
                            StatValue = cashAfter
                        },
                        transaction);

                    string effectReference =
                        CreateEffectReference(
                            batchIdentity,
                            fee,
                            cashBefore,
                            cashAfter);
                    int inserted = connection.Execute(
                        "INSERT INTO missionrewardledger "
                        + "(CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, "
                        + "EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, "
                        + "AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES "
                        + "(@CharacterId, @QuestId, @RewardKey, @RewardType, @Status, 1, NULL, "
                        + "@EffectReference, NULL, @AppliedAtUtcTicks, 0, @AppliedAtUtcTicks, "
                        + "@AppliedAtUtcTicks, @AppliedAtUtcTicks, 1)",
                        new
                        {
                            CharacterId = characterInstance,
                            QuestId = questId,
                            RewardKey,
                            RewardType,
                            Status = (int)MissionRewardStatus.Applied,
                            EffectReference = effectReference,
                            AppliedAtUtcTicks = appliedAtUtcTicks
                        },
                        transaction);
                    if (inserted != 1)
                    {
                        throw new InvalidOperationException(
                            "Durable generated mission roll-fee claim was not inserted exactly once.");
                    }

                    transaction.Commit();
                    return new MissionRollFeeApplyResult
                           {
                               Status = MissionRollFeeApplyStatus.Applied,
                               CashBefore = cashBefore,
                               CashAfter = cashAfter,
                               Failure = string.Empty
                           };
                }
                catch
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                    }

                    throw;
                }
            }
        }

        private static int ReadCash(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterType,
            int characterInstance)
        {
            int? persisted =
                connection.Query<int?>(
                    "SELECT StatValue FROM stats WHERE Instance=@Instance AND Type=@Type "
                    + "AND StatId=@StatId FOR UPDATE",
                    new
                    {
                        Instance = characterInstance,
                        Type = characterType,
                        StatId = (int)StatIds.cash
                    },
                    transaction).FirstOrDefault();
            return CashStatRules.Clamp(persisted.GetValueOrDefault());
        }

        private static string CreateEffectReference(
            string batchIdentity,
            int fee,
            int cashBefore,
            int cashAfter)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "batch={0};fee={1};before={2};after={3}",
                batchIdentity,
                fee,
                cashBefore,
                cashAfter);
        }

        private static bool TryParseEffectReference(
            string effectReference,
            string expectedBatchIdentity,
            out int fee,
            out int cashBefore,
            out int cashAfter)
        {
            fee = 0;
            cashBefore = 0;
            cashAfter = 0;
            if (string.IsNullOrEmpty(effectReference))
            {
                return false;
            }

            string[] parts = effectReference.Split(';');
            if (parts.Length != 4
                || !string.Equals(
                    parts[0],
                    "batch=" + expectedBatchIdentity,
                    StringComparison.Ordinal)
                || !TryParsePart(parts[1], "fee=", out fee)
                || !TryParsePart(parts[2], "before=", out cashBefore)
                || !TryParsePart(parts[3], "after=", out cashAfter))
            {
                return false;
            }

            return fee > 0
                   && cashBefore >= fee
                   && cashAfter == cashBefore - fee;
        }

        private static bool TryParsePart(
            string value,
            string prefix,
            out int parsed)
        {
            parsed = 0;
            return value != null
                   && value.StartsWith(prefix, StringComparison.Ordinal)
                   && int.TryParse(
                       value.Substring(prefix.Length),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out parsed);
        }

        private static MissionRollFeeApplyResult Conflict(string failure)
        {
            return new MissionRollFeeApplyResult
                   {
                       Status = MissionRollFeeApplyStatus.Conflict,
                       Failure = failure ?? "Durable generated mission roll-fee claim conflict."
                   };
        }

        private sealed class MissionRollFeeLedgerRow
        {
            public string RewardType { get; set; }

            public int Status { get; set; }

            public string EffectReference { get; set; }
        }
    }
}
