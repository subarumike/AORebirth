#nullable disable

namespace AORebirth.Database.Domain.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    /// <summary>One row of <c>characterquests</c>: a player's state on one quest.</summary>
    public sealed class CharacterQuestRow
    {
        public int CharacterId { get; set; }

        /// <summary>Quests.json hash for template quests; the <c>generatedquests</c> id for generated quests.</summary>
        public string QuestId { get; set; }

        /// <summary>0 template (Quests.json), 1 generated.</summary>
        public int Source { get; set; }

        /// <summary>1 active, 2 completed, 3 failed, 4 expired.</summary>
        public int State { get; set; }

        public int Progress { get; set; }

        public int RequiredCount { get; set; }

        public long AssignedAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }
    }

    /// <summary>One row of <c>generatedquests</c>: a generated quest, shared by everyone assigned to it.</summary>
    public sealed class GeneratedQuestRow
    {
        public string QuestId { get; set; }

        /// <summary>0 character, 1 team.</summary>
        public int OwnerType { get; set; }

        public int OwnerId { get; set; }

        /// <summary>The quest in Quests.json format.</summary>
        public string DefinitionJson { get; set; }

        /// <summary>Optional ACG building generator data for the quest's generated building.</summary>
        public string AcgBuildingGeneratorJson { get; set; }

        public long CreatedAtUtcTicks { get; set; }

        /// <summary>Absolute UTC expiry.</summary>
        public long ExpiresAtUtcTicks { get; set; }

        public long UpdatedAtUtcTicks { get; set; }
    }

    /// <summary>A mission key item row to insert together with its <c>questdungeonkeys</c> link.</summary>
    public sealed class QuestDungeonKeyItemRow
    {
        public int InstanceId { get; set; }

        public int ContainerType { get; set; }

        public int ContainerInstance { get; set; }

        public int ContainerPlacement { get; set; }

        public int ItemType { get; set; }

        public int LowId { get; set; }

        public int HighId { get; set; }

        public int Quality { get; set; }

        public int Source { get; set; }

        public long CreatedAtUtcTicks { get; set; }
    }

    /// <summary>A key retired when its quest ended, with where it was stored (and its bag's location).</summary>
    public sealed class RetiredDungeonKey
    {
        public int KeyInstanceId { get; set; }

        public int ContainerType { get; set; }

        public int ContainerInstance { get; set; }

        /// <summary>When the key was in a bag: the bag's own container, else 0.</summary>
        public int ParentContainerType { get; set; }

        public int ParentContainerInstance { get; set; }
    }

    /// <summary>
    /// Storage for <c>characterquests</c>, <c>generatedquests</c> and <c>questdungeonkeys</c>. The only mission
    /// table it touches is <c>generatedmissionoffers</c>, to claim an offer when it becomes a quest. Each call
    /// opens its own connection; multi-row changes run in one transaction.
    /// </summary>
    public sealed class MySqlCharacterQuestDao
    {
        const string CharacterColumns =
            "CharacterId,QuestId,Source,State,Progress,RequiredCount,AssignedAtUtcTicks,UpdatedAtUtcTicks";

        const string GeneratedColumns =
            "QuestId,OwnerType,OwnerId,DefinitionJson,AcgBuildingGeneratorJson,CreatedAtUtcTicks,ExpiresAtUtcTicks,UpdatedAtUtcTicks";

        readonly Func<IDbConnection> connectionFactory;

        public MySqlCharacterQuestDao(Func<IDbConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IList<CharacterQuestRow> LoadForCharacter(int characterId)
        {
            if (characterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(characterId));

            return Query(
                "SELECT " + CharacterColumns + " FROM characterquests WHERE CharacterId=@Id ORDER BY AssignedAtUtcTicks",
                ReadCharacter,
                "@Id",
                characterId);
        }

        /// <summary>The generated quests a character holds a row for.</summary>
        public IList<GeneratedQuestRow> LoadGeneratedForCharacter(int characterId)
        {
            if (characterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(characterId));

            return Query(
                "SELECT g.QuestId,g.OwnerType,g.OwnerId,g.DefinitionJson,g.AcgBuildingGeneratorJson,"
                + "g.CreatedAtUtcTicks,g.ExpiresAtUtcTicks,g.UpdatedAtUtcTicks "
                + "FROM generatedquests g JOIN characterquests c ON c.QuestId=g.QuestId "
                + "WHERE c.CharacterId=@Id AND c.Source=1",
                ReadGenerated,
                "@Id",
                characterId);
        }

        /// <summary>One generated quest by id, or null.</summary>
        public GeneratedQuestRow LoadGenerated(string questId)
        {
            if (string.IsNullOrEmpty(questId))
                throw new ArgumentException("Quest id is required.", nameof(questId));

            IList<GeneratedQuestRow> rows = Query(
                "SELECT " + GeneratedColumns + " FROM generatedquests WHERE QuestId=@Id",
                ReadGenerated,
                "@Id",
                questId);
            return rows.Count == 0 ? null : rows[0];
        }

        /// <summary>Inserts the row or replaces the stored one for the same character and quest.</summary>
        public void SaveCharacterQuest(CharacterQuestRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));
            if (row.CharacterId <= 0 || string.IsNullOrEmpty(row.QuestId))
                throw new ArgumentException("Character and quest id are required.", nameof(row));

            Execute(
                "INSERT INTO characterquests (" + CharacterColumns + ") VALUES "
                + "(@CharacterId,@QuestId,@Source,@State,@Progress,@RequiredCount,@Assigned,@Updated) "
                + "ON DUPLICATE KEY UPDATE Source=VALUES(Source),State=VALUES(State),Progress=VALUES(Progress),"
                + "RequiredCount=VALUES(RequiredCount),AssignedAtUtcTicks=VALUES(AssignedAtUtcTicks),"
                + "UpdatedAtUtcTicks=VALUES(UpdatedAtUtcTicks)",
                "@CharacterId", row.CharacterId,
                "@QuestId", row.QuestId,
                "@Source", row.Source,
                "@State", row.State,
                "@Progress", row.Progress,
                "@RequiredCount", row.RequiredCount,
                "@Assigned", row.AssignedAtUtcTicks,
                "@Updated", row.UpdatedAtUtcTicks);
        }

        /// <summary>Inserts the generated quest or replaces the stored one with the same id.</summary>
        public void SaveGenerated(GeneratedQuestRow row)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrEmpty(row.QuestId) || string.IsNullOrEmpty(row.DefinitionJson))
                throw new ArgumentException("Quest id and definition are required.", nameof(row));

            Execute(
                "INSERT INTO generatedquests (" + GeneratedColumns + ") VALUES "
                + "(@QuestId,@OwnerType,@OwnerId,@Definition,@Acg,@Created,@Expires,@Updated) "
                + "ON DUPLICATE KEY UPDATE OwnerType=VALUES(OwnerType),OwnerId=VALUES(OwnerId),"
                + "DefinitionJson=VALUES(DefinitionJson),AcgBuildingGeneratorJson=VALUES(AcgBuildingGeneratorJson),"
                + "ExpiresAtUtcTicks=VALUES(ExpiresAtUtcTicks),UpdatedAtUtcTicks=VALUES(UpdatedAtUtcTicks)",
                "@QuestId", row.QuestId,
                "@OwnerType", row.OwnerType,
                "@OwnerId", row.OwnerId,
                "@Definition", row.DefinitionJson,
                "@Acg", (object)row.AcgBuildingGeneratorJson ?? DBNull.Value,
                "@Created", row.CreatedAtUtcTicks,
                "@Expires", row.ExpiresAtUtcTicks,
                "@Updated", row.UpdatedAtUtcTicks);
        }

        /// <summary>
        /// Accepts a rolled mission offer as a generated quest with a dungeon, in one transaction: claims the offer
        /// (only while it is still offered to <paramref name="ownerId"/> and unexpired), stores the quest and the
        /// owner's quest row, and gives the mission key with its dungeon link. False when the offer was already
        /// taken, replaced or expired; nothing is written then.
        /// </summary>
        public bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, long nowUtcTicks)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            if (characterRow == null) throw new ArgumentNullException(nameof(characterRow));
            if (key == null) throw new ArgumentNullException(nameof(key));

            return Transaction((c, t) =>
            {
                // Offered = 1, Active = 2 (GeneratedMissionState). The state guard makes a second accept a no-op.
                if (Execute(c, t, "UPDATE generatedmissionoffers SET State=2,Version=Version+1 "
                        + "WHERE OwnerId=@Owner AND OfferType=@Type AND OfferInstance=@Instance AND State=1 AND ExpiresAtUtcTicks>@Now",
                        "@Owner", ownerId, "@Type", offerType, "@Instance", offerInstance, "@Now", nowUtcTicks) != 1)
                    return false;

                InsertGenerated(c, t, quest);
                InsertCharacterQuest(c, t, characterRow);
                InsertKey(c, t, key, quest.QuestId);
                return true;
            });
        }

        /// <summary>
        /// Adds a copy of <paramref name="sourceKeyInstanceId"/> for the same quest: only while that key is still
        /// linked, its quest is unexpired and the quest has fewer than <paramref name="maxKeys"/> keys. The source
        /// link and the quest's key count are locked for the check. Returns the quest id, or null when refused.
        /// </summary>
        public string TryAddDuplicateKey(int sourceKeyInstanceId, QuestDungeonKeyItemRow copy, int maxKeys, long nowUtcTicks)
        {
            if (copy == null) throw new ArgumentNullException(nameof(copy));

            return Transaction((c, t) =>
            {
                string questId;
                using (IDbCommand command = Prepare(c, t,
                    "SELECT k.QuestId FROM questdungeonkeys k JOIN generatedquests g ON g.QuestId=k.QuestId "
                    + "WHERE k.KeyInstanceId=@Key AND g.ExpiresAtUtcTicks>@Now FOR UPDATE",
                    new object[] { "@Key", sourceKeyInstanceId, "@Now", nowUtcTicks }))
                    questId = command.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(questId))
                    return null;

                long count;
                using (IDbCommand command = Prepare(c, t, "SELECT COUNT(*) FROM questdungeonkeys WHERE QuestId=@Quest FOR UPDATE",
                    new object[] { "@Quest", questId }))
                    count = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
                if (count >= maxKeys)
                    return null;

                InsertKey(c, t, copy, questId);
                return questId;
            });
        }

        /// <summary>The quest each of <paramref name="keyInstanceIds"/> opens; unlinked keys are absent.</summary>
        public IDictionary<int, string> LoadKeyQuests(IReadOnlyCollection<int> keyInstanceIds)
        {
            var result = new Dictionary<int, string>();
            if (keyInstanceIds == null || keyInstanceIds.Count == 0)
                return result;

            var parameters = new List<object>();
            string list = InList(keyInstanceIds, "@K", parameters);
            foreach (KeyValuePair<int, string> row in Query(
                "SELECT KeyInstanceId,QuestId FROM questdungeonkeys WHERE KeyInstanceId IN (" + list + ")",
                r => new KeyValuePair<int, string>(r.GetInt32(0), r.GetString(1)), parameters.ToArray()))
                result[row.Key] = row.Value;
            return result;
        }

        /// <summary>
        /// Ends a quest's dungeon keys in one transaction: deletes every key link and retires the key items
        /// (container type 0, placement = own instance, so the location index stays unique). Returns each key and
        /// where it was, so online holders can be updated in memory.
        /// </summary>
        public IList<RetiredDungeonKey> EndQuestKeys(string questId)
        {
            if (string.IsNullOrEmpty(questId)) throw new ArgumentException("Quest id is required.", nameof(questId));

            return Transaction((c, t) =>
            {
                var keys = new List<RetiredDungeonKey>();
                using (IDbCommand command = Prepare(c, t,
                    "SELECT k.KeyInstanceId,i.ContainerType,i.ContainerInstance,p.ContainerType,p.ContainerInstance "
                    + "FROM questdungeonkeys k LEFT JOIN item_instances i ON i.InstanceId=k.KeyInstanceId "
                    + "LEFT JOIN item_instances p ON p.InstanceId=i.ContainerInstance "
                    + "WHERE k.QuestId=@Quest FOR UPDATE",
                    new object[] { "@Quest", questId }))
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        keys.Add(new RetiredDungeonKey
                        {
                            KeyInstanceId = reader.GetInt32(0),
                            ContainerType = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            ContainerInstance = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            ParentContainerType = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            ParentContainerInstance = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                        });
                }

                if (keys.Count == 0)
                    return keys;

                Execute(c, t, "DELETE FROM questdungeonkeys WHERE QuestId=@Quest", "@Quest", questId);
                var parameters = new List<object>();
                string list = InList(keys.ConvertAll(key => key.KeyInstanceId), "@K", parameters);
                Execute(c, t, "UPDATE item_instances SET ContainerType=0,ContainerInstance=0,ContainerPlacement=InstanceId "
                    + "WHERE InstanceId IN (" + list + ") AND ContainerType<>0", parameters.ToArray());
                return keys;
            });
        }

        /// <summary>
        /// Retires those of <paramref name="keyInstanceIds"/> that are mission keys (<paramref name="keyLowId"/>)
        /// with no link any more. Linked keys are left alone. Returns the ids retired.
        /// </summary>
        public IList<int> RetireUnlinkedKeys(IReadOnlyCollection<int> keyInstanceIds, int keyLowId)
        {
            if (keyInstanceIds == null || keyInstanceIds.Count == 0)
                return new List<int>();

            return Transaction((c, t) =>
            {
                var parameters = new List<object> { "@Low", keyLowId };
                string list = InList(keyInstanceIds, "@K", parameters);
                var dead = new List<int>();
                using (IDbCommand command = Prepare(c, t,
                    "SELECT i.InstanceId FROM item_instances i LEFT JOIN questdungeonkeys k ON k.KeyInstanceId=i.InstanceId "
                    + "WHERE i.InstanceId IN (" + list + ") AND i.LowId=@Low AND i.ContainerType<>0 AND k.KeyInstanceId IS NULL FOR UPDATE",
                    parameters.ToArray()))
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        dead.Add(reader.GetInt32(0));
                }

                if (dead.Count == 0)
                    return dead;

                var retire = new List<object>();
                string deadList = InList(dead, "@D", retire);
                Execute(c, t, "UPDATE item_instances SET ContainerType=0,ContainerInstance=0,ContainerPlacement=InstanceId "
                    + "WHERE InstanceId IN (" + deadList + ")", retire.ToArray());
                return dead;
            });
        }

        /// <summary>
        /// Retires every mission key (<paramref name="keyLowId"/>) a character owns that no longer opens anything:
        /// on its pages and bank (<paramref name="ownedContainerTypes"/> with the character as container) and inside
        /// bags stored there (<paramref name="bagContainerType"/>). Run before the character's items are loaded, so
        /// dead keys never reach memory. Returns how many were retired.
        /// </summary>
        public int RetireDeadKeysForCharacter(int characterId, int keyLowId, IReadOnlyCollection<int> ownedContainerTypes, int bagContainerType)
        {
            if (characterId <= 0 || ownedContainerTypes == null || ownedContainerTypes.Count == 0)
                return 0;

            return Transaction((c, t) =>
            {
                var parameters = new List<object> { "@Id", characterId, "@Low", keyLowId, "@Bag", bagContainerType };
                string types = InList(ownedContainerTypes, "@T", parameters);
                var dead = new List<int>();
                using (IDbCommand command = Prepare(c, t,
                    "SELECT i.InstanceId FROM item_instances i LEFT JOIN questdungeonkeys k ON k.KeyInstanceId=i.InstanceId "
                    + "WHERE i.LowId=@Low AND k.KeyInstanceId IS NULL AND ("
                    + "(i.ContainerInstance=@Id AND i.ContainerType IN (" + types + ")) "
                    + "OR (i.ContainerType=@Bag AND i.ContainerInstance IN (SELECT b.InstanceId FROM item_instances b "
                    + "WHERE b.ContainerInstance=@Id AND b.ContainerType IN (" + types + ")))) FOR UPDATE",
                    parameters.ToArray()))
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        dead.Add(reader.GetInt32(0));
                }

                if (dead.Count == 0)
                    return 0;

                var retire = new List<object>();
                string list = InList(dead, "@D", retire);
                return Execute(c, t, "UPDATE item_instances SET ContainerType=0,ContainerInstance=0,ContainerPlacement=InstanceId "
                    + "WHERE InstanceId IN (" + list + ")", retire.ToArray());
            });
        }

        /// <summary>Quests that still have dungeon keys but have expired, oldest first, at most <paramref name="limit"/>.</summary>
        public IList<string> LoadExpiredQuestsWithKeys(long nowUtcTicks, int limit)
        {
            return Query(
                "SELECT g.QuestId FROM generatedquests g WHERE g.ExpiresAtUtcTicks<=@Now "
                + "AND EXISTS (SELECT 1 FROM questdungeonkeys k WHERE k.QuestId=g.QuestId) "
                + "ORDER BY g.ExpiresAtUtcTicks LIMIT " + Math.Max(1, limit).ToString(System.Globalization.CultureInfo.InvariantCulture),
                r => r.GetString(0),
                "@Now", nowUtcTicks);
        }

        /// <summary>
        /// Deletes generated quests that expired before <paramref name="cutoffUtcTicks"/> and have no keys left,
        /// with their character rows, at most <paramref name="limit"/> per call. Returns how many quests went.
        /// </summary>
        public int PurgeEndedGeneratedQuests(long cutoffUtcTicks, int limit)
        {
            return Transaction((c, t) =>
            {
                var ids = new List<string>();
                using (IDbCommand command = Prepare(c, t,
                    "SELECT g.QuestId FROM generatedquests g WHERE g.ExpiresAtUtcTicks<@Cutoff "
                    + "AND NOT EXISTS (SELECT 1 FROM questdungeonkeys k WHERE k.QuestId=g.QuestId) "
                    + "ORDER BY g.ExpiresAtUtcTicks LIMIT " + Math.Max(1, limit).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " FOR UPDATE",
                    new object[] { "@Cutoff", cutoffUtcTicks }))
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        ids.Add(reader.GetString(0));
                }

                if (ids.Count == 0)
                    return 0;

                var parameters = new List<object>();
                string list = InList(ids, "@Q", parameters);
                Execute(c, t, "DELETE FROM characterquests WHERE QuestId IN (" + list + ")", parameters.ToArray());
                Execute(c, t, "DELETE FROM generatedquests WHERE QuestId IN (" + list + ")", parameters.ToArray());
                return ids.Count;
            });
        }

        /// <summary>Marks every still-active character row of <paramref name="questId"/> with <paramref name="state"/>.</summary>
        public void CloseCharacterQuests(string questId, int state, long nowUtcTicks)
        {
            Execute("UPDATE characterquests SET State=@State,UpdatedAtUtcTicks=@Now WHERE QuestId=@Quest AND State=1",
                "@State", state, "@Now", nowUtcTicks, "@Quest", questId);
        }

        static void InsertGenerated(IDbConnection c, IDbTransaction t, GeneratedQuestRow row)
        {
            Execute(c, t, "INSERT INTO generatedquests (" + GeneratedColumns + ") VALUES "
                + "(@QuestId,@OwnerType,@OwnerId,@Definition,@Acg,@Created,@Expires,@Updated)",
                "@QuestId", row.QuestId, "@OwnerType", row.OwnerType, "@OwnerId", row.OwnerId,
                "@Definition", row.DefinitionJson, "@Acg", (object)row.AcgBuildingGeneratorJson ?? DBNull.Value,
                "@Created", row.CreatedAtUtcTicks, "@Expires", row.ExpiresAtUtcTicks, "@Updated", row.UpdatedAtUtcTicks);
        }

        static void InsertCharacterQuest(IDbConnection c, IDbTransaction t, CharacterQuestRow row)
        {
            Execute(c, t, "INSERT INTO characterquests (" + CharacterColumns + ") VALUES "
                + "(@CharacterId,@QuestId,@Source,@State,@Progress,@RequiredCount,@Assigned,@Updated)",
                "@CharacterId", row.CharacterId, "@QuestId", row.QuestId, "@Source", row.Source, "@State", row.State,
                "@Progress", row.Progress, "@RequiredCount", row.RequiredCount,
                "@Assigned", row.AssignedAtUtcTicks, "@Updated", row.UpdatedAtUtcTicks);
        }

        static void InsertKey(IDbConnection c, IDbTransaction t, QuestDungeonKeyItemRow key, string questId)
        {
            Execute(c, t, "INSERT INTO item_instances (InstanceId,ContainerType,ContainerInstance,ContainerPlacement,ItemType,LowId,HighId,Quality,StackCount,Source) "
                + "VALUES (@Id,@Type,@Owner,@Slot,@ItemType,@Low,@High,@Quality,1,@Source)",
                "@Id", key.InstanceId, "@Type", key.ContainerType, "@Owner", key.ContainerInstance, "@Slot", key.ContainerPlacement,
                "@ItemType", key.ItemType, "@Low", key.LowId, "@High", key.HighId, "@Quality", key.Quality, "@Source", key.Source);
            Execute(c, t, "INSERT INTO questdungeonkeys (KeyInstanceId,QuestId,CreatedAtUtcTicks) VALUES (@Key,@Quest,@Created)",
                "@Key", key.InstanceId, "@Quest", questId, "@Created", key.CreatedAtUtcTicks);
        }

        /// <summary>Appends one parameter per value and returns the placeholder list.</summary>
        static string InList<T>(IEnumerable<T> values, string prefix, List<object> parameters)
        {
            var names = new List<string>();
            int index = 0;
            foreach (T value in values)
            {
                string name = prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                names.Add(name);
                parameters.Add(name);
                parameters.Add(value);
                index++;
            }

            return string.Join(",", names);
        }

        static CharacterQuestRow ReadCharacter(IDataReader reader) => new CharacterQuestRow
        {
            CharacterId = reader.GetInt32(0),
            QuestId = reader.GetString(1),
            Source = reader.GetInt32(2),
            State = reader.GetInt32(3),
            Progress = reader.GetInt32(4),
            RequiredCount = reader.GetInt32(5),
            AssignedAtUtcTicks = reader.GetInt64(6),
            UpdatedAtUtcTicks = reader.GetInt64(7)
        };

        static GeneratedQuestRow ReadGenerated(IDataReader reader) => new GeneratedQuestRow
        {
            QuestId = reader.GetString(0),
            OwnerType = reader.GetInt32(1),
            OwnerId = reader.GetInt32(2),
            DefinitionJson = reader.GetString(3),
            AcgBuildingGeneratorJson = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAtUtcTicks = reader.GetInt64(5),
            ExpiresAtUtcTicks = reader.GetInt64(6),
            UpdatedAtUtcTicks = reader.GetInt64(7)
        };

        IList<T> Query<T>(string sql, Func<IDataReader, T> read, params object[] parameters)
        {
            var rows = new List<T>();
            using (IDbConnection connection = Open())
            using (IDbCommand command = Prepare(connection, null, sql, parameters))
            using (IDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    rows.Add(read(reader));
            }

            return rows;
        }

        void Execute(string sql, params object[] parameters)
        {
            using (IDbConnection connection = Open())
            using (IDbCommand command = Prepare(connection, null, sql, parameters))
                command.ExecuteNonQuery();
        }

        static int Execute(IDbConnection connection, IDbTransaction transaction, string sql, params object[] parameters)
        {
            using (IDbCommand command = Prepare(connection, transaction, sql, parameters))
                return command.ExecuteNonQuery();
        }

        /// <summary>Runs <paramref name="action"/> in one transaction; it commits unless the action throws.</summary>
        T Transaction<T>(Func<IDbConnection, IDbTransaction, T> action)
        {
            using (IDbConnection connection = Open())
            using (IDbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                T result = action(connection, transaction);
                transaction.Commit();
                return result;
            }
        }

        IDbConnection Open()
        {
            IDbConnection connection = connectionFactory() ?? throw new InvalidOperationException("Connection factory returned null.");
            if (connection.State != ConnectionState.Open)
                connection.Open();
            return connection;
        }

        /// <summary><paramref name="parameters"/> alternate name, value.</summary>
        static IDbCommand Prepare(IDbConnection connection, IDbTransaction transaction, string sql, object[] parameters)
        {
            IDbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            for (int i = 0; i + 1 < parameters.Length; i += 2)
            {
                IDbDataParameter parameter = command.CreateParameter();
                parameter.ParameterName = (string)parameters[i];
                parameter.Value = parameters[i + 1];
                command.Parameters.Add(parameter);
            }

            return command;
        }
    }
}
