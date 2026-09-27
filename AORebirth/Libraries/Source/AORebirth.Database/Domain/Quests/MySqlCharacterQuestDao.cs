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

    /// <summary>
    /// Storage for <c>characterquests</c> and <c>generatedquests</c>. Self-contained: it does not touch the
    /// authored or generated mission tables. Each call opens its own connection.
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
            using (IDbCommand command = Prepare(connection, sql, parameters))
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
            using (IDbCommand command = Prepare(connection, sql, parameters))
                command.ExecuteNonQuery();
        }

        IDbConnection Open()
        {
            IDbConnection connection = connectionFactory() ?? throw new InvalidOperationException("Connection factory returned null.");
            if (connection.State != ConnectionState.Open)
                connection.Open();
            return connection;
        }

        /// <summary><paramref name="parameters"/> alternate name, value.</summary>
        static IDbCommand Prepare(IDbConnection connection, string sql, object[] parameters)
        {
            IDbCommand command = connection.CreateCommand();
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
