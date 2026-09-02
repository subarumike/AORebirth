namespace AORebirth.Database.Domain.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Enums;
    using AORebirth.Interfaces.Persistence.Missions;

    using Dapper;

    using Utility;

    using MissionAccountFlagRecord = AORebirth.Interfaces.Persistence.Missions.MissionAccountFlagData;
    using MissionAtomicStatRewardResult = AORebirth.Interfaces.Persistence.Missions.MissionAtomicStatRewardResultData;
    using MissionCharacterSnapshot = AORebirth.Interfaces.Persistence.Missions.MissionCharacterSnapshotData;
    using MissionCharacterStatMutation = AORebirth.Interfaces.Persistence.Missions.MissionStatMutationData;
    using MissionCharacterStatValue = AORebirth.Interfaces.Persistence.Missions.MissionStatValueData;
    using MissionFlagRecord = AORebirth.Interfaces.Persistence.Missions.MissionFlagData;
    using MissionKey = AORebirth.Interfaces.Persistence.Missions.MissionKeyData;
    using MissionObjectiveKey = AORebirth.Interfaces.Persistence.Missions.MissionObjectiveKeyData;
    using MissionObjectiveObservationRecord = AORebirth.Interfaces.Persistence.Missions.MissionObjectiveObservationData;
    using MissionObjectiveProgressRecord = AORebirth.Interfaces.Persistence.Missions.MissionObjectiveProgressData;
    using MissionRewardClaimResult = AORebirth.Interfaces.Persistence.Missions.MissionRewardClaimResultData;
    using MissionRewardKey = AORebirth.Interfaces.Persistence.Missions.MissionRewardKeyData;
    using MissionRewardStageRecord = AORebirth.Interfaces.Persistence.Missions.MissionRewardStageData;
    using MissionStateRecord = AORebirth.Interfaces.Persistence.Missions.MissionStateData;

    #endregion

    /// <summary>
    /// MySQL-backed authoritative mission repository. Every write operation is
    /// executed through a caller-scoped transaction and uses optimistic versions.
    /// </summary>
    public sealed class MySqlMissionDao : IMissionDao
    {
        private const string StartAreaQuestId = "system.new_character_start_area";
        private const string StartAreaFlagKey = "selection";
        private const string RollFeeRewardKey = "roll-fee";
        private const string RollFeeRewardType = "GeneratedMissionRollFee";

        private readonly Func<IDbConnection> connectionFactory;

        public MySqlMissionDao()
            : this(Connector.GetConnection)
        {
        }

        public MySqlMissionDao(Func<IDbConnection> connectionFactory)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            this.connectionFactory = connectionFactory;
        }

        public MissionStateRecord GetMission(MissionKey key)
        {
            ValidateMissionKey(key);
            using (IDbConnection connection = this.connectionFactory())
            {
                return QueryMission(connection, null, key, false);
            }
        }

        public IList<MissionStateRecord> GetMissions(int characterId)
        {
            ValidateCharacterId(characterId);
            using (IDbConnection connection = this.connectionFactory())
            {
                return QueryMissions(connection, null, characterId);
            }
        }

        public MissionCharacterSnapshot ReadCharacter(int characterId)
        {
            return this.Execute(
                characterId,
                transaction => ((MySqlMissionDaoTransaction)transaction).ReadCharacter());
        }

        public string ResolveCharacterAccountKey(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            const string Sql = "SELECT Username FROM characters WHERE Id=@CharacterId";
            using (IDbConnection connection = this.connectionFactory())
            {
                string accountKey = connection.Query<string>(Sql, new { CharacterId = characterId }).SingleOrDefault();
                return string.IsNullOrWhiteSpace(accountKey) ? null : accountKey.Trim();
            }
        }

        public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
        {
            accountKey = NormalizeAccountKey(accountKey);
            ValidateText(flagKey, "flagKey", 128, false);
            flagKey = flagKey.Trim();
            using (IDbConnection connection = this.connectionFactory())
            {
                return QueryAccountFlag(connection, null, accountKey, flagKey, false);
            }
        }

        public IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey)
        {
            accountKey = NormalizeAccountKey(accountKey);
            using (IDbConnection connection = this.connectionFactory())
            {
                return QueryAccountFlags(connection, null, accountKey);
            }
        }

        public T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation)
        {
            return this.Execute(characterId, null, operation);
        }

        public T Execute<T>(
            int characterId,
            string accountKey,
            Func<IMissionDaoTransaction, T> operation)
        {
            ValidateCharacterId(characterId);
            if (accountKey != null)
            {
                accountKey = NormalizeAccountKey(accountKey);
            }

            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            using (IDbConnection connection = this.connectionFactory())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    T result = operation(
                        new MySqlMissionDaoTransaction(
                            characterId,
                            accountKey,
                            connection,
                            transaction));
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request)
        {
            if (request == null
                || request.CharacterType <= 0
                || request.CharacterId <= 0
                || string.IsNullOrEmpty(request.BatchIdentity)
                || request.BatchIdentity.Length > 96
                || request.Fee <= 0
                || request.AppliedAtUtcTicks <= 0)
            {
                return RollFeeConflict("Roll-fee claim identity or amount is invalid.");
            }

            string questId = "generated-offer:" + request.BatchIdentity;
            using (IDbConnection connection = this.connectionFactory())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    int cashBefore = ReadCash(
                        connection,
                        transaction,
                        request.CharacterType,
                        request.CharacterId);
                    MissionRollFeeLedgerRow existing =
                        connection.Query<MissionRollFeeLedgerRow>(
                            "SELECT RewardType, Status, EffectReference FROM missionrewardledger "
                            + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                            + "AND RewardKey=@RewardKey FOR UPDATE",
                            new
                            {
                                CharacterId = request.CharacterId,
                                QuestId = questId,
                                RewardKey = RollFeeRewardKey
                            },
                            transaction).FirstOrDefault();
                    if (existing != null)
                    {
                        int recordedFee;
                        int recordedBefore;
                        int recordedAfter;
                        if (!string.Equals(existing.RewardType, RollFeeRewardType, StringComparison.Ordinal)
                            || existing.Status != (int)MissionRewardStatus.Applied
                            || !TryParseRollFeeEffectReference(
                                existing.EffectReference,
                                request.BatchIdentity,
                                out recordedFee,
                                out recordedBefore,
                                out recordedAfter)
                            || recordedFee != request.Fee)
                        {
                            transaction.Rollback();
                            return RollFeeConflict(
                                "Existing durable roll-fee claim conflicts with this batch.");
                        }

                        transaction.Commit();
                        return new MissionRollFeeResult
                               {
                                   Status = MissionRollFeeStatus.AlreadyApplied,
                                   CashBefore = recordedBefore,
                                   CashAfter = cashBefore,
                                   Failure = string.Empty
                               };
                    }

                    if (cashBefore < request.Fee)
                    {
                        transaction.Rollback();
                        return new MissionRollFeeResult
                               {
                                   Status = MissionRollFeeStatus.InsufficientCredits,
                                   CashBefore = cashBefore,
                                   CashAfter = cashBefore,
                                   Failure = "Insufficient credits for generated mission roll fee."
                               };
                    }

                    int cashAfter = cashBefore - request.Fee;
                    connection.Execute(
                        "INSERT INTO stats (Instance, Type, StatId, StatValue) "
                        + "VALUES (@Instance, @Type, @StatId, @StatValue) "
                        + "ON DUPLICATE KEY UPDATE StatValue=@StatValue",
                        new
                        {
                            Instance = request.CharacterId,
                            Type = request.CharacterType,
                            StatId = (int)StatIds.cash,
                            StatValue = cashAfter
                        },
                        transaction);

                    string effectReference = CreateRollFeeEffectReference(
                        request.BatchIdentity,
                        request.Fee,
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
                            CharacterId = request.CharacterId,
                            QuestId = questId,
                            RewardKey = RollFeeRewardKey,
                            RewardType = RollFeeRewardType,
                            Status = (int)MissionRewardStatus.Applied,
                            EffectReference = effectReference,
                            request.AppliedAtUtcTicks
                        },
                        transaction);
                    if (inserted != 1)
                    {
                        throw new InvalidOperationException(
                            "Durable generated mission roll-fee claim was not inserted exactly once.");
                    }

                    transaction.Commit();
                    return new MissionRollFeeResult
                           {
                               Status = MissionRollFeeStatus.Applied,
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

        public bool MarkStartAreaSelectionPending(int characterId)
        {
            if (characterId <= 0)
            {
                return false;
            }

            const string Sql =
                "INSERT INTO missionflags "
                + "(CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) "
                + "VALUES (@CharacterId, @QuestId, @FlagKey, @Value, @NowUtcTicks, @NowUtcTicks, 1) "
                + "ON DUPLICATE KEY UPDATE `Value`=`Value`";

            try
            {
                using (IDbConnection connection = this.connectionFactory())
                {
                    connection.Execute(
                        Sql,
                        new
                        {
                            CharacterId = characterId,
                            QuestId = StartAreaQuestId,
                            FlagKey = StartAreaFlagKey,
                            Value = MissionStartAreaSelectionStates.Pending,
                            NowUtcTicks = DateTime.UtcNow.Ticks
                        });
                }

                return string.Equals(
                    this.GetStartAreaSelectionState(characterId),
                    MissionStartAreaSelectionStates.Pending,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return false;
            }
        }

        public string GetStartAreaSelectionState(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            const string Sql =
                "SELECT `Value` FROM missionflags "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey LIMIT 1";

            try
            {
                using (IDbConnection connection = this.connectionFactory())
                {
                    return connection.Query<string>(
                            Sql,
                            new
                            {
                                CharacterId = characterId,
                                QuestId = StartAreaQuestId,
                                FlagKey = StartAreaFlagKey
                            })
                        .FirstOrDefault();
                }
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return null;
            }
        }

        public bool TryCompleteStartAreaSelection(int characterId, string selectedState)
        {
            if (characterId <= 0 || !IsCompletedStartAreaState(selectedState))
            {
                return false;
            }

            const string Sql =
                "UPDATE missionflags SET `Value`=@SelectedState, UpdatedAtUtcTicks=@NowUtcTicks, Version=Version+1 "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey AND `Value`=@PendingState";

            try
            {
                using (IDbConnection connection = this.connectionFactory())
                {
                    return connection.Execute(
                               Sql,
                               new
                               {
                                   CharacterId = characterId,
                                   QuestId = StartAreaQuestId,
                                   FlagKey = StartAreaFlagKey,
                                   PendingState = MissionStartAreaSelectionStates.Pending,
                                   SelectedState = selectedState,
                                   NowUtcTicks = DateTime.UtcNow.Ticks
                               }) == 1;
                }
            }
            catch (Exception exception)
            {
                LogUtil.ErrorException(exception);
                return false;
            }
        }

        private static int ReadCash(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterType,
            int characterId)
        {
            int? persisted = connection.Query<int?>(
                    "SELECT StatValue FROM stats WHERE Instance=@Instance AND Type=@Type "
                    + "AND StatId=@StatId FOR UPDATE",
                    new
                    {
                        Instance = characterId,
                        Type = characterType,
                        StatId = (int)StatIds.cash
                    },
                    transaction)
                .FirstOrDefault();
            return ClampCash(persisted.GetValueOrDefault());
        }

        private static int ClampCash(long cash)
        {
            const int ClientSafeMaxCash = 999999999;
            return cash < 0 ? 0 : cash > ClientSafeMaxCash ? ClientSafeMaxCash : (int)cash;
        }

        private static string CreateRollFeeEffectReference(
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

        private static bool TryParseRollFeeEffectReference(
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
            return parts.Length == 4
                   && string.Equals(parts[0], "batch=" + expectedBatchIdentity, StringComparison.Ordinal)
                   && TryParseRollFeePart(parts[1], "fee=", out fee)
                   && TryParseRollFeePart(parts[2], "before=", out cashBefore)
                   && TryParseRollFeePart(parts[3], "after=", out cashAfter)
                   && fee > 0
                   && cashBefore >= fee
                   && cashAfter == cashBefore - fee;
        }

        private static bool TryParseRollFeePart(string value, string prefix, out int parsed)
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

        private static MissionRollFeeResult RollFeeConflict(string failure)
        {
            return new MissionRollFeeResult
                   {
                       Status = MissionRollFeeStatus.Conflict,
                       Failure = failure ?? "Durable generated mission roll-fee claim conflict."
                   };
        }

        private static bool IsCompletedStartAreaState(string state)
        {
            return string.Equals(state, MissionStartAreaSelectionStates.Arete, StringComparison.Ordinal)
                   || string.Equals(
                       state,
                       MissionStartAreaSelectionStates.IccShuttleport,
                       StringComparison.Ordinal);
        }

        private static MissionStateRecord QueryMission(
            IDbConnection connection,
            IDbTransaction transaction,
            MissionKey key,
            bool forUpdate)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, "
                + "CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, "
                + "UpdatedAtUtcTicks, Version FROM missionstates "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId";

            return connection.Query<MissionStateRecord>(
                Sql + (forUpdate ? " FOR UPDATE" : string.Empty),
                new { key.CharacterId, key.QuestId },
                transaction).SingleOrDefault();
        }

        private static IList<MissionStateRecord> QueryMissions(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterId)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, "
                + "CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, "
                + "UpdatedAtUtcTicks, Version FROM missionstates "
                + "WHERE CharacterId=@CharacterId ORDER BY QuestId";

            return connection.Query<MissionStateRecord>(Sql, new { CharacterId = characterId }, transaction).ToList();
        }

        private static MissionObjectiveProgressRecord QueryObjective(
            IDbConnection connection,
            IDbTransaction transaction,
            MissionObjectiveKey key,
            bool forUpdate)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, "
                + "CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionobjectiveprogress "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND ObjectiveId=@ObjectiveId";

            return connection.Query<MissionObjectiveProgressRecord>(
                Sql + (forUpdate ? " FOR UPDATE" : string.Empty),
                new
                {
                    CharacterId = key.Mission.CharacterId,
                    QuestId = key.Mission.QuestId,
                    key.ObjectiveId
                },
                transaction).SingleOrDefault();
        }

        private static IList<MissionObjectiveProgressRecord> QueryObjectives(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterId)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, "
                + "CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionobjectiveprogress "
                + "WHERE CharacterId=@CharacterId ORDER BY QuestId, ObjectiveId";

            return connection.Query<MissionObjectiveProgressRecord>(
                Sql,
                new { CharacterId = characterId },
                transaction).ToList();
        }

        private static MissionFlagRecord QueryFlag(
            IDbConnection connection,
            IDbTransaction transaction,
            MissionKey key,
            string flagKey,
            bool forUpdate)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version "
                + "FROM missionflags WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND FlagKey=@FlagKey";

            return connection.Query<MissionFlagRecord>(
                Sql + (forUpdate ? " FOR UPDATE" : string.Empty),
                new { key.CharacterId, key.QuestId, FlagKey = flagKey },
                transaction).SingleOrDefault();
        }

        private static IList<MissionFlagRecord> QueryFlags(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterId)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version "
                + "FROM missionflags WHERE CharacterId=@CharacterId ORDER BY QuestId, FlagKey";

            return connection.Query<MissionFlagRecord>(
                Sql,
                new { CharacterId = characterId },
                transaction).ToList();
        }

        private static MissionAccountFlagRecord QueryAccountFlag(
            IDbConnection connection,
            IDbTransaction transaction,
            string accountKey,
            string flagKey,
            bool forUpdate)
        {
            const string Sql =
                "SELECT AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version "
                + "FROM missionaccountflags WHERE AccountKey=@AccountKey AND FlagKey=@FlagKey";

            return connection.Query<MissionAccountFlagRecord>(
                Sql + (forUpdate ? " FOR UPDATE" : string.Empty),
                new { AccountKey = accountKey, FlagKey = flagKey },
                transaction).SingleOrDefault();
        }

        private static IList<MissionAccountFlagRecord> QueryAccountFlags(
            IDbConnection connection,
            IDbTransaction transaction,
            string accountKey)
        {
            const string Sql =
                "SELECT AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version "
                + "FROM missionaccountflags WHERE AccountKey=@AccountKey ORDER BY FlagKey";

            return connection.Query<MissionAccountFlagRecord>(
                Sql,
                new { AccountKey = accountKey },
                transaction).ToList();
        }

        private static MissionRewardStageRecord QueryReward(
            IDbConnection connection,
            IDbTransaction transaction,
            MissionRewardKey key,
            bool forUpdate)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, "
                + "EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, "
                + "CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionrewardledger "
                + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey";

            return connection.Query<MissionRewardStageRecord>(
                Sql + (forUpdate ? " FOR UPDATE" : string.Empty),
                new
                {
                    CharacterId = key.Mission.CharacterId,
                    QuestId = key.Mission.QuestId,
                    key.RewardKey
                },
                transaction).SingleOrDefault();
        }

        private static IList<MissionRewardStageRecord> QueryRewards(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterId)
        {
            const string Sql =
                "SELECT CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, "
                + "EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, AppliedAtUtcTicks, "
                + "CreatedAtUtcTicks, UpdatedAtUtcTicks, Version FROM missionrewardledger "
                + "WHERE CharacterId=@CharacterId ORDER BY QuestId, RewardKey";

            return connection.Query<MissionRewardStageRecord>(
                Sql,
                new { CharacterId = characterId },
                transaction).ToList();
        }

        private static void ValidateCharacterId(int characterId)
        {
            if (characterId <= 0)
            {
                throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
            }
        }

        private static void ValidateMissionKey(MissionKey key)
        {
            ValidateCharacterId(key.CharacterId);
            ValidateText(key.QuestId, "questId", 128, false);
        }

        private static void ValidateObjectiveKey(MissionObjectiveKey key)
        {
            ValidateMissionKey(key.Mission);
            ValidateText(key.ObjectiveId, "objectiveId", 128, false);
        }

        private static void ValidateRewardKey(MissionRewardKey key)
        {
            ValidateMissionKey(key.Mission);
            ValidateText(key.RewardKey, "rewardKey", 191, false);
        }

        private static string NormalizeAccountKey(string accountKey)
        {
            ValidateText(accountKey, "accountKey", 32, false);
            string normalized = accountKey.Trim();
            ValidateText(normalized, "accountKey", 32, false);
            return normalized;
        }

        private static void ValidateText(string value, string parameterName, int maximumLength, bool allowNull)
        {
            if (value == null)
            {
                if (allowNull)
                {
                    return;
                }

                throw new ArgumentNullException(parameterName);
            }

            if (!allowNull && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(parameterName + " is required.", parameterName);
            }

            if (value.Length > maximumLength)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    parameterName + " exceeds the persisted maximum length of " + maximumLength + ".");
            }
        }

        private sealed class MissionRollFeeLedgerRow
        {
            public string RewardType { get; set; }

            public int Status { get; set; }

            public string EffectReference { get; set; }
        }

        private sealed class MySqlMissionDaoTransaction : IMissionDaoTransaction
        {
            private readonly IDbConnection connection;
            private readonly IDbTransaction transaction;

            public MySqlMissionDaoTransaction(
                int characterId,
                string accountKey,
                IDbConnection connection,
                IDbTransaction transaction)
            {
                this.CharacterId = characterId;
                this.AccountKey = accountKey;
                this.connection = connection;
                this.transaction = transaction;

                if (accountKey != null)
                {
                    const string AccountOwnerSql =
                        "SELECT Username FROM characters WHERE Id=@CharacterId FOR UPDATE";
                    string persistedAccountKey = connection.Query<string>(
                        AccountOwnerSql,
                        new { CharacterId = characterId },
                        transaction).SingleOrDefault();
                    if (string.IsNullOrWhiteSpace(persistedAccountKey)
                        || !string.Equals(
                            persistedAccountKey.Trim(),
                            accountKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Mission account scope does not own character " + characterId + ".");
                    }
                }
            }

            public int CharacterId { get; private set; }

            public string AccountKey { get; private set; }

            public MissionStateRecord GetMission(MissionKey key)
            {
                this.ValidateCharacterScope(key.CharacterId);
                ValidateMissionKey(key);
                return QueryMission(this.connection, this.transaction, key, true);
            }

            public IList<MissionStateRecord> GetMissions(int characterId)
            {
                this.ValidateCharacterScope(characterId);
                return QueryMissions(this.connection, this.transaction, characterId);
            }

            public void SaveMission(MissionKey key, MissionStateRecord record)
            {
                this.ValidateCharacterScope(key.CharacterId);
                ValidateMissionKey(key);
                if (record == null)
                {
                    throw new ArgumentNullException("record");
                }

                this.ValidateRecordMissionKey(key, record.CharacterId, record.QuestId);
                ValidateText(record.CurrentStepId, "CurrentStepId", 128, true);

                if (record.Version <= 0)
                {
                    const string InsertSql =
                        "INSERT INTO missionstates "
                        + "(CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, "
                        + "CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, "
                        + "UpdatedAtUtcTicks, Version) VALUES "
                        + "(@CharacterId, @QuestId, @State, @CurrentStepId, @OfferedAtUtcTicks, @AcceptedAtUtcTicks, "
                        + "@CompletedAtUtcTicks, @FailedAtUtcTicks, @AbandonedAtUtcTicks, @CreatedAtUtcTicks, "
                        + "@UpdatedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(InsertSql, record, this.transaction);
                    this.RequireSingleWrite(inserted, "mission insert", key.ToString(), record.Version);
                    record.Version = 1;
                    return;
                }

                const string UpdateSql =
                    "UPDATE missionstates SET State=@State, CurrentStepId=@CurrentStepId, "
                    + "OfferedAtUtcTicks=@OfferedAtUtcTicks, AcceptedAtUtcTicks=@AcceptedAtUtcTicks, "
                    + "CompletedAtUtcTicks=@CompletedAtUtcTicks, FailedAtUtcTicks=@FailedAtUtcTicks, "
                    + "AbandonedAtUtcTicks=@AbandonedAtUtcTicks, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, "
                    + "Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                    + "AND Version=@ExpectedVersion";

                long expectedVersion = record.Version;
                int updated = this.connection.Execute(
                    UpdateSql,
                    new
                    {
                        record.State,
                        record.CurrentStepId,
                        record.OfferedAtUtcTicks,
                        record.AcceptedAtUtcTicks,
                        record.CompletedAtUtcTicks,
                        record.FailedAtUtcTicks,
                        record.AbandonedAtUtcTicks,
                        record.UpdatedAtUtcTicks,
                        key.CharacterId,
                        key.QuestId,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);
                this.RequireSingleWrite(updated, "mission update", key.ToString(), expectedVersion);
                record.Version = expectedVersion + 1;
            }

            public MissionObjectiveProgressRecord GetObjective(MissionObjectiveKey key)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateObjectiveKey(key);
                return QueryObjective(this.connection, this.transaction, key, true);
            }

            public void SaveObjective(MissionObjectiveKey key, MissionObjectiveProgressRecord record)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateObjectiveKey(key);
                if (record == null)
                {
                    throw new ArgumentNullException("record");
                }

                this.ValidateRecordMissionKey(key.Mission, record.CharacterId, record.QuestId);
                if (!string.Equals(key.ObjectiveId, record.ObjectiveId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Objective record does not match the requested objective key.");
                }

                ValidateText(record.ObjectiveId, "ObjectiveId", 128, false);
                ValidateText(record.LastObservationKey, "LastObservationKey", 191, true);
                if (record.Progress < 0 || record.RequiredCount < 0)
                {
                    throw new ArgumentOutOfRangeException("record", "Objective progress and required count cannot be negative.");
                }

                if (record.Version <= 0)
                {
                    const string InsertSql =
                        "INSERT INTO missionobjectiveprogress "
                        + "(CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, "
                        + "CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES "
                        + "(@CharacterId, @QuestId, @ObjectiveId, @Progress, @RequiredCount, @LastObservationKey, "
                        + "@CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(InsertSql, record, this.transaction);
                    this.RequireSingleWrite(inserted, "objective insert", key.ToString(), record.Version);
                    record.Version = 1;
                    return;
                }

                const string UpdateSql =
                    "UPDATE missionobjectiveprogress SET Progress=@Progress, RequiredCount=@RequiredCount, "
                    + "LastObservationKey=@LastObservationKey, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, "
                    + "Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                    + "AND ObjectiveId=@ObjectiveId AND Version=@ExpectedVersion";

                long expectedVersion = record.Version;
                int updated = this.connection.Execute(
                    UpdateSql,
                    new
                    {
                        record.Progress,
                        record.RequiredCount,
                        record.LastObservationKey,
                        record.UpdatedAtUtcTicks,
                        key.Mission.CharacterId,
                        key.Mission.QuestId,
                        key.ObjectiveId,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);
                this.RequireSingleWrite(updated, "objective update", key.ToString(), expectedVersion);
                record.Version = expectedVersion + 1;
            }

            public bool TryAddObservation(MissionObjectiveObservationRecord observation)
            {
                if (observation == null)
                {
                    throw new ArgumentNullException("observation");
                }

                MissionObjectiveKey key = observation.ObjectiveKey;
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateObjectiveKey(key);
                ValidateText(observation.ObservationKey, "ObservationKey", 191, false);
                ValidateText(observation.EventType, "EventType", 64, false);
                ValidateText(observation.SourceIdentity, "SourceIdentity", 64, true);
                ValidateText(observation.TargetIdentity, "TargetIdentity", 64, true);
                observation.QuestId = key.Mission.QuestId;
                observation.ObjectiveId = key.ObjectiveId;
                observation.ObservationKey = observation.ObservationKey.Trim();

                const string Sql =
                    "INSERT IGNORE INTO missionobjectiveobservations "
                    + "(CharacterId, QuestId, ObjectiveId, ObservationKey, EventType, SourceIdentity, "
                    + "TargetIdentity, ObservedAtUtcTicks) VALUES "
                    + "(@CharacterId, @QuestId, @ObjectiveId, @ObservationKey, @EventType, @SourceIdentity, "
                    + "@TargetIdentity, @ObservedAtUtcTicks)";

                return this.connection.Execute(Sql, observation, this.transaction) == 1;
            }

            public MissionFlagRecord GetFlag(MissionKey key, string flagKey)
            {
                this.ValidateCharacterScope(key.CharacterId);
                ValidateMissionKey(key);
                ValidateText(flagKey, "flagKey", 128, false);
                flagKey = flagKey.Trim();
                return QueryFlag(this.connection, this.transaction, key, flagKey, true);
            }

            public void SaveFlag(MissionKey key, MissionFlagRecord flag)
            {
                this.ValidateCharacterScope(key.CharacterId);
                ValidateMissionKey(key);
                if (flag == null)
                {
                    throw new ArgumentNullException("flag");
                }

                this.ValidateRecordMissionKey(key, flag.CharacterId, flag.QuestId);
                ValidateText(flag.FlagKey, "FlagKey", 128, false);
                ValidateText(flag.Value, "Value", 1024, true);
                flag.FlagKey = flag.FlagKey.Trim();

                if (flag.Version <= 0)
                {
                    const string InsertSql =
                        "INSERT INTO missionflags "
                        + "(CharacterId, QuestId, FlagKey, `Value`, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) "
                        + "VALUES (@CharacterId, @QuestId, @FlagKey, @Value, @CreatedAtUtcTicks, @UpdatedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(InsertSql, flag, this.transaction);
                    this.RequireSingleWrite(inserted, "mission flag insert", key + "|" + flag.FlagKey, flag.Version);
                    flag.Version = 1;
                    return;
                }

                const string UpdateSql =
                    "UPDATE missionflags SET `Value`=@Value, UpdatedAtUtcTicks=@UpdatedAtUtcTicks, "
                    + "Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                    + "AND FlagKey=@FlagKey AND Version=@ExpectedVersion";

                long expectedVersion = flag.Version;
                int updated = this.connection.Execute(
                    UpdateSql,
                    new
                    {
                        flag.Value,
                        flag.UpdatedAtUtcTicks,
                        key.CharacterId,
                        key.QuestId,
                        flag.FlagKey,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);
                this.RequireSingleWrite(updated, "mission flag update", key + "|" + flag.FlagKey, expectedVersion);
                flag.Version = expectedVersion + 1;
            }

            public MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey)
            {
                accountKey = NormalizeAccountKey(accountKey);
                this.ValidateAccountScope(accountKey);
                ValidateText(flagKey, "flagKey", 128, false);
                accountKey = accountKey.Trim();
                flagKey = flagKey.Trim();
                return QueryAccountFlag(this.connection, this.transaction, accountKey, flagKey, true);
            }

            public void SaveAccountFlag(string accountKey, MissionAccountFlagRecord flag)
            {
                accountKey = NormalizeAccountKey(accountKey);
                this.ValidateAccountScope(accountKey);
                if (flag == null)
                {
                    throw new ArgumentNullException("flag");
                }

                if (!string.Equals(accountKey, flag.AccountKey, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Account flag does not match the transaction account scope.");
                }

                ValidateText(flag.FlagKey, "FlagKey", 128, false);
                ValidateText(flag.Value, "Value", 1024, true);
                ValidateText(flag.SourceQuestId, "SourceQuestId", 128, true);
                accountKey = accountKey.Trim();
                flag.AccountKey = accountKey;
                flag.FlagKey = flag.FlagKey.Trim();

                if (flag.Version <= 0)
                {
                    const string InsertSql =
                        "INSERT INTO missionaccountflags "
                        + "(AccountKey, FlagKey, `Value`, SourceQuestId, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) "
                        + "VALUES (@AccountKey, @FlagKey, @Value, @SourceQuestId, @CreatedAtUtcTicks, "
                        + "@UpdatedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(InsertSql, flag, this.transaction);
                    this.RequireSingleWrite(inserted, "account flag insert", accountKey + "|" + flag.FlagKey, flag.Version);
                    flag.Version = 1;
                    return;
                }

                const string UpdateSql =
                    "UPDATE missionaccountflags SET `Value`=@Value, SourceQuestId=@SourceQuestId, "
                    + "UpdatedAtUtcTicks=@UpdatedAtUtcTicks, Version=Version+1 "
                    + "WHERE AccountKey=@AccountKey AND FlagKey=@FlagKey AND Version=@ExpectedVersion";

                long expectedVersion = flag.Version;
                int updated = this.connection.Execute(
                    UpdateSql,
                    new
                    {
                        flag.Value,
                        flag.SourceQuestId,
                        flag.UpdatedAtUtcTicks,
                        AccountKey = accountKey,
                        flag.FlagKey,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);
                this.RequireSingleWrite(updated, "account flag update", accountKey + "|" + flag.FlagKey, expectedVersion);
                flag.Version = expectedVersion + 1;
            }

            public MissionRewardStageRecord GetReward(MissionRewardKey key)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateRewardKey(key);
                return QueryReward(this.connection, this.transaction, key, true);
            }

            public MissionRewardClaimResult TryClaimReward(
                MissionRewardKey key,
                string rewardType,
                string claimToken,
                long claimedAtUtcTicks,
                long claimExpiresAtUtcTicks)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateRewardKey(key);
                ValidateText(rewardType, "rewardType", 64, false);
                ValidateText(claimToken, "claimToken", 64, false);
                MissionStateRecord mission = QueryMission(
                    this.connection,
                    this.transaction,
                    key.Mission,
                    true);
                if (mission == null || mission.State != MissionLifecycleState.Completed)
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.Rejected,
                        null,
                        "Reward claims require a completed authoritative mission.");
                }

                if (claimedAtUtcTicks <= 0 || claimExpiresAtUtcTicks <= claimedAtUtcTicks)
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.Rejected,
                        null,
                        "Reward claim expiry must be later than the claim time.");
                }

                MissionRewardStageRecord existing = QueryReward(this.connection, this.transaction, key, true);
                if (existing == null)
                {
                    const string InsertSql =
                        "INSERT INTO missionrewardledger "
                        + "(CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, "
                        + "EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, "
                        + "AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES "
                        + "(@CharacterId, @QuestId, @RewardKey, @RewardType, @Status, 1, NULL, NULL, "
                        + "@ClaimToken, @ClaimedAtUtcTicks, @ClaimExpiresAtUtcTicks, 0, "
                        + "@ClaimedAtUtcTicks, @ClaimedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(
                        InsertSql,
                        new
                        {
                            CharacterId = key.Mission.CharacterId,
                            QuestId = key.Mission.QuestId,
                            key.RewardKey,
                            RewardType = rewardType,
                            Status = MissionRewardStatus.InProgress,
                            ClaimToken = claimToken,
                            ClaimedAtUtcTicks = claimedAtUtcTicks,
                            ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks
                        },
                        this.transaction);
                    this.RequireSingleWrite(inserted, "reward claim insert", key.ToString(), 0);

                    return CreateClaimResult(
                        MissionRewardClaimStatus.Claimed,
                        QueryReward(this.connection, this.transaction, key, true),
                        "Reward stage claimed.");
                }

                if (!string.Equals(existing.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.Rejected,
                        existing,
                        "Reward type does not match the durable reward stage.");
                }

                if (existing.Status == MissionRewardStatus.Applied)
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.AlreadyApplied,
                        existing,
                        "Reward stage is already applied.");
                }

                if (existing.Status == MissionRewardStatus.InProgress
                    && existing.ClaimExpiresAtUtcTicks > claimedAtUtcTicks)
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.Busy,
                        existing,
                        "Reward stage has an active claim.");
                }

                const string ReclaimSql =
                    "UPDATE missionrewardledger SET Status=@Status, Attempts=Attempts+1, LastError=NULL, "
                    + "ClaimToken=@ClaimToken, ClaimedAtUtcTicks=@ClaimedAtUtcTicks, "
                    + "ClaimExpiresAtUtcTicks=@ClaimExpiresAtUtcTicks, UpdatedAtUtcTicks=@ClaimedAtUtcTicks, "
                    + "Version=Version+1 WHERE CharacterId=@CharacterId AND QuestId=@QuestId "
                    + "AND RewardKey=@RewardKey AND Version=@ExpectedVersion";

                int reclaimed = this.connection.Execute(
                    ReclaimSql,
                    new
                    {
                        Status = MissionRewardStatus.InProgress,
                        ClaimToken = claimToken,
                        ClaimedAtUtcTicks = claimedAtUtcTicks,
                        ClaimExpiresAtUtcTicks = claimExpiresAtUtcTicks,
                        CharacterId = key.Mission.CharacterId,
                        QuestId = key.Mission.QuestId,
                        key.RewardKey,
                        ExpectedVersion = existing.Version
                    },
                    this.transaction);

                if (reclaimed != 1)
                {
                    return CreateClaimResult(
                        MissionRewardClaimStatus.Rejected,
                        QueryReward(this.connection, this.transaction, key, true),
                        "Reward claim lost an optimistic concurrency race.");
                }

                return CreateClaimResult(
                    MissionRewardClaimStatus.Claimed,
                    QueryReward(this.connection, this.transaction, key, true),
                    "Reward stage claimed for retry.");
            }

            public bool TryMarkRewardApplied(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string effectReference,
                long appliedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateRewardKey(key);
                ValidateText(claimToken, "claimToken", 64, false);
                ValidateText(effectReference, "effectReference", 255, true);
                if (appliedAtUtcTicks <= 0)
                {
                    stage = QueryReward(this.connection, this.transaction, key, true);
                    return false;
                }

                const string Sql =
                    "UPDATE missionrewardledger SET Status=@Status, EffectReference=@EffectReference, "
                    + "LastError=NULL, AppliedAtUtcTicks=@AppliedAtUtcTicks, "
                    + "ClaimExpiresAtUtcTicks=0, UpdatedAtUtcTicks=@AppliedAtUtcTicks, Version=Version+1 "
                    + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey "
                    + "AND Status=@ExpectedStatus AND ClaimToken=@ClaimToken AND Version=@ExpectedVersion";

                int updated = this.connection.Execute(
                    Sql,
                    new
                    {
                        Status = MissionRewardStatus.Applied,
                        EffectReference = effectReference,
                        AppliedAtUtcTicks = appliedAtUtcTicks,
                        CharacterId = key.Mission.CharacterId,
                        QuestId = key.Mission.QuestId,
                        key.RewardKey,
                        ExpectedStatus = MissionRewardStatus.InProgress,
                        ClaimToken = claimToken,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);

                stage = QueryReward(this.connection, this.transaction, key, true);
                return updated == 1;
            }

            public bool TryMarkRewardFailed(
                MissionRewardKey key,
                string claimToken,
                long expectedVersion,
                string error,
                long failedAtUtcTicks,
                out MissionRewardStageRecord stage)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateRewardKey(key);
                ValidateText(claimToken, "claimToken", 64, false);
                ValidateText(error, "error", 1024, true);
                if (failedAtUtcTicks <= 0)
                {
                    stage = QueryReward(this.connection, this.transaction, key, true);
                    return false;
                }

                const string Sql =
                    "UPDATE missionrewardledger SET Status=@Status, LastError=@LastError, "
                    + "ClaimExpiresAtUtcTicks=0, UpdatedAtUtcTicks=@FailedAtUtcTicks, Version=Version+1 "
                    + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey "
                    + "AND Status=@ExpectedStatus AND ClaimToken=@ClaimToken AND Version=@ExpectedVersion";

                int updated = this.connection.Execute(
                    Sql,
                    new
                    {
                        Status = MissionRewardStatus.Failed,
                        LastError = error,
                        FailedAtUtcTicks = failedAtUtcTicks,
                        CharacterId = key.Mission.CharacterId,
                        QuestId = key.Mission.QuestId,
                        key.RewardKey,
                        ExpectedStatus = MissionRewardStatus.InProgress,
                        ClaimToken = claimToken,
                        ExpectedVersion = expectedVersion
                    },
                    this.transaction);

                stage = QueryReward(this.connection, this.transaction, key, true);
                return updated == 1;
            }

            public MissionAtomicStatRewardResult TryApplyCharacterStatReward(
                MissionRewardKey key,
                string rewardType,
                IList<MissionCharacterStatMutation> mutations,
                string effectReference,
                long appliedAtUtcTicks)
            {
                this.ValidateCharacterScope(key.Mission.CharacterId);
                ValidateRewardKey(key);
                ValidateText(rewardType, "rewardType", 64, false);
                ValidateText(effectReference, "effectReference", 255, true);
                MissionStateRecord mission = QueryMission(
                    this.connection,
                    this.transaction,
                    key.Mission,
                    true);
                if (mission == null || mission.State != MissionLifecycleState.Completed)
                {
                    return CreateAtomicResult(
                        MissionAtomicRewardStatus.Rejected,
                        null,
                        new MissionCharacterStatValue[0],
                        "Character stat rewards require a completed authoritative mission.");
                }

                if (mutations == null || mutations.Count == 0 || appliedAtUtcTicks <= 0)
                {
                    return CreateAtomicResult(
                        MissionAtomicRewardStatus.Rejected,
                        null,
                        new MissionCharacterStatValue[0],
                        "At least one character stat mutation is required.");
                }

                MissionRewardStageRecord existing = QueryReward(this.connection, this.transaction, key, true);
                if (existing != null
                    && !string.Equals(existing.RewardType, rewardType, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateAtomicResult(
                        MissionAtomicRewardStatus.Rejected,
                        existing,
                        new MissionCharacterStatValue[0],
                        "Reward type does not match the durable reward stage.");
                }

                if (existing != null && existing.Status == MissionRewardStatus.Applied)
                {
                    return CreateAtomicResult(
                        MissionAtomicRewardStatus.AlreadyApplied,
                        existing,
                        this.ReadStatValues(mutations),
                        "Character stat reward is already applied.");
                }

                if (existing != null
                    && existing.Status == MissionRewardStatus.InProgress
                    && existing.ClaimExpiresAtUtcTicks > appliedAtUtcTicks)
                {
                    return CreateAtomicResult(
                        MissionAtomicRewardStatus.Rejected,
                        existing,
                        new MissionCharacterStatValue[0],
                        "Reward stage has an active non-stat claim.");
                }

                IList<MissionCharacterStatValue> values = this.ApplyStatMutations(mutations);
                if (existing == null)
                {
                    const string InsertSql =
                        "INSERT INTO missionrewardledger "
                        + "(CharacterId, QuestId, RewardKey, RewardType, Status, Attempts, LastError, "
                        + "EffectReference, ClaimToken, ClaimedAtUtcTicks, ClaimExpiresAtUtcTicks, "
                        + "AppliedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version) VALUES "
                        + "(@CharacterId, @QuestId, @RewardKey, @RewardType, @Status, 1, NULL, "
                        + "@EffectReference, NULL, @AppliedAtUtcTicks, 0, @AppliedAtUtcTicks, "
                        + "@AppliedAtUtcTicks, @AppliedAtUtcTicks, 1)";

                    int inserted = this.connection.Execute(
                        InsertSql,
                        new
                        {
                            CharacterId = key.Mission.CharacterId,
                            QuestId = key.Mission.QuestId,
                            key.RewardKey,
                            RewardType = rewardType,
                            Status = MissionRewardStatus.Applied,
                            EffectReference = effectReference,
                            AppliedAtUtcTicks = appliedAtUtcTicks
                        },
                        this.transaction);
                    this.RequireSingleWrite(inserted, "atomic reward insert", key.ToString(), 0);
                }
                else
                {
                    const string UpdateSql =
                        "UPDATE missionrewardledger SET Status=@Status, Attempts=Attempts+1, LastError=NULL, "
                        + "EffectReference=@EffectReference, ClaimToken=NULL, ClaimedAtUtcTicks=@AppliedAtUtcTicks, "
                        + "ClaimExpiresAtUtcTicks=0, AppliedAtUtcTicks=@AppliedAtUtcTicks, "
                        + "UpdatedAtUtcTicks=@AppliedAtUtcTicks, Version=Version+1 "
                        + "WHERE CharacterId=@CharacterId AND QuestId=@QuestId AND RewardKey=@RewardKey "
                        + "AND Version=@ExpectedVersion";

                    int updated = this.connection.Execute(
                        UpdateSql,
                        new
                        {
                            Status = MissionRewardStatus.Applied,
                            EffectReference = effectReference,
                            AppliedAtUtcTicks = appliedAtUtcTicks,
                            CharacterId = key.Mission.CharacterId,
                            QuestId = key.Mission.QuestId,
                            key.RewardKey,
                            ExpectedVersion = existing.Version
                        },
                        this.transaction);
                    this.RequireSingleWrite(updated, "atomic stat reward", key.ToString(), existing.Version);
                }

                return CreateAtomicResult(
                    MissionAtomicRewardStatus.Applied,
                    QueryReward(this.connection, this.transaction, key, true),
                    values,
                    "Character stat reward and reward ledger were applied in one database transaction.");
            }

            public MissionCharacterSnapshot ReadCharacter()
            {
                return new MissionCharacterSnapshot(
                    this.CharacterId,
                    QueryMissions(this.connection, this.transaction, this.CharacterId),
                    QueryObjectives(this.connection, this.transaction, this.CharacterId),
                    QueryFlags(this.connection, this.transaction, this.CharacterId),
                    QueryRewards(this.connection, this.transaction, this.CharacterId));
            }

            private IList<MissionCharacterStatValue> ApplyStatMutations(
                IList<MissionCharacterStatMutation> mutations)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var values = new List<MissionCharacterStatValue>();
                foreach (MissionCharacterStatMutation mutation in mutations)
                {
                    ValidateStatMutation(mutation);
                    string mutationKey = mutation.StatIdentityType + "|" + mutation.StatId;
                    if (!seen.Add(mutationKey))
                    {
                        throw new InvalidOperationException("Duplicate character stat mutation: " + mutationKey);
                    }

                    long currentValue = this.ReadStatValue(mutation.StatIdentityType, mutation.StatId);
                    long nextValue = mutation.Kind == MissionStatMutationKind.Set
                        ? Clamp(mutation.Value, mutation.MinimumValue, mutation.MaximumValue)
                        : AddClamped(
                            currentValue,
                            mutation.Value,
                            mutation.MinimumValue,
                            mutation.MaximumValue);

                    const string UpsertSql =
                        "INSERT INTO stats (Instance, Type, StatId, StatValue) "
                        + "VALUES (@Instance, @Type, @StatId, @StatValue) "
                        + "ON DUPLICATE KEY UPDATE StatValue=@StatValue";

                    this.connection.Execute(
                        UpsertSql,
                        new
                        {
                            Instance = this.CharacterId,
                            Type = mutation.StatIdentityType,
                            mutation.StatId,
                            StatValue = (int)nextValue
                        },
                        this.transaction);

                    values.Add(
                        new MissionCharacterStatValue
                        {
                            StatIdentityType = mutation.StatIdentityType,
                            StatId = mutation.StatId,
                            Value = nextValue
                        });
                }

                return values;
            }

            private IList<MissionCharacterStatValue> ReadStatValues(
                IList<MissionCharacterStatMutation> mutations)
            {
                var values = new List<MissionCharacterStatValue>();
                foreach (MissionCharacterStatMutation mutation in mutations)
                {
                    ValidateStatMutation(mutation);
                    values.Add(
                        new MissionCharacterStatValue
                        {
                            StatIdentityType = mutation.StatIdentityType,
                            StatId = mutation.StatId,
                            Value = this.ReadStatValue(mutation.StatIdentityType, mutation.StatId)
                        });
                }

                return values;
            }

            private long ReadStatValue(int statIdentityType, int statId)
            {
                const string Sql =
                    "SELECT StatValue FROM stats WHERE Instance=@Instance AND Type=@Type AND StatId=@StatId "
                    + "FOR UPDATE";

                int? value = this.connection.Query<int?>(
                    Sql,
                    new { Instance = this.CharacterId, Type = statIdentityType, StatId = statId },
                    this.transaction).SingleOrDefault();
                return value.HasValue ? value.Value : 0;
            }

            private void ValidateCharacterScope(int characterId)
            {
                ValidateCharacterId(characterId);
                if (characterId != this.CharacterId)
                {
                    throw new InvalidOperationException(
                        "Mission transaction cannot access character " + characterId
                        + " while scoped to character " + this.CharacterId + ".");
                }
            }

            private void ValidateAccountScope(string accountKey)
            {
                accountKey = NormalizeAccountKey(accountKey);
                if (this.AccountKey == null)
                {
                    throw new InvalidOperationException(
                        "Account-scoped mission flags require an explicitly account-scoped transaction.");
                }

                if (!string.Equals(this.AccountKey, accountKey, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Mission transaction cannot access account '" + accountKey
                        + "' while scoped to account '" + this.AccountKey + "'.");
                }
            }

            private void ValidateRecordMissionKey(MissionKey key, int characterId, string questId)
            {
                if (key.CharacterId != characterId
                    || !string.Equals(key.QuestId, questId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Mission record does not match the requested mission key.");
                }
            }

            private void RequireSingleWrite(int rows, string operation, string key, long expectedVersion)
            {
                if (rows != 1)
                {
                    throw new InvalidOperationException(
                        "Persistent " + operation + " failed optimistic concurrency for '" + key
                        + "' at version " + expectedVersion + ".");
                }
            }

            private static void ValidateStatMutation(MissionCharacterStatMutation mutation)
            {
                if (mutation == null)
                {
                    throw new ArgumentNullException("mutation");
                }

                if (mutation.StatIdentityType <= 0 || mutation.StatId < 0)
                {
                    throw new ArgumentOutOfRangeException("mutation", "Stat identity type and stat id are invalid.");
                }

                if (mutation.MinimumValue > mutation.MaximumValue
                    || mutation.MinimumValue < int.MinValue
                    || mutation.MaximumValue > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        "mutation",
                        "Stat mutation bounds must fit the persisted INT StatValue range.");
                }

                if (mutation.Kind != MissionStatMutationKind.AddClamped
                    && mutation.Kind != MissionStatMutationKind.Set)
                {
                    throw new ArgumentOutOfRangeException("mutation", "Unsupported stat mutation kind.");
                }
            }

            private static long AddClamped(long current, long delta, long minimum, long maximum)
            {
                if (delta > 0 && current > long.MaxValue - delta)
                {
                    return maximum;
                }

                if (delta < 0 && current < long.MinValue - delta)
                {
                    return minimum;
                }

                return Clamp(current + delta, minimum, maximum);
            }

            private static long Clamp(long value, long minimum, long maximum)
            {
                return value < minimum ? minimum : value > maximum ? maximum : value;
            }

            private static MissionRewardClaimResult CreateClaimResult(
                MissionRewardClaimStatus status,
                MissionRewardStageRecord stage,
                string message)
            {
                return new MissionRewardClaimResult
                {
                    Status = status,
                    Stage = stage == null ? null : stage.Clone(),
                    Message = message
                };
            }

            private static MissionAtomicStatRewardResult CreateAtomicResult(
                MissionAtomicRewardStatus status,
                MissionRewardStageRecord stage,
                IList<MissionCharacterStatValue> values,
                string message)
            {
                return new MissionAtomicStatRewardResult
                {
                    Status = status,
                    Stage = stage == null ? null : stage.Clone(),
                    StatValues = values ?? new MissionCharacterStatValue[0],
                    Message = message
                };
            }
        }
    }
}
