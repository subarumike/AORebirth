namespace AORebirth.LinuxBuild.Stage7MySqlSecurityIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using AORebirth.Database;

    internal static class Stage7CharacterCleanupFixture
    {
        private static readonly CleanupTableContract[] CleanupTables =
        {
            new CleanupTableContract(
                "organizations",
                "SELECT Id,Creation,Name,LeaderId,GovernmentForm,Description,Objective,History,Tax,Bank,Comission,ContractsId,CityID,TowerFieldId "
                + "FROM organizations WHERE LeaderId=@characterId"),
            new CleanupTableContract(
                "items",
                "SELECT Id,ContainerType,ContainerInstance,ContainerPlacement,LowId,HighId,Quality,MultipleCount "
                + "FROM items WHERE ContainerType=@characterId"),
            new CleanupTableContract(
                "instanceditems",
                "SELECT Id,ContainerType,ContainerInstance,ContainerPlacement,Itemtype,LowId,HighId,Quality,MultipleCount,"
                + "X,Y,Z,HeadingX,HeadingY,HeadingZ,HeadingW,stats FROM instanceditems WHERE ContainerType=@characterId"),
            new CleanupTableContract(
                "receivedmessages",
                "SELECT Id,PlayerId,ReceivedId FROM receivedmessages WHERE PlayerId=@characterId"),
            new CleanupTableContract(
                "stats",
                "SELECT Id,Instance,Type,StatId,StatValue FROM stats WHERE Type=50000 AND Instance=@characterId"),
            new CleanupTableContract(
                "missionflags",
                "SELECT Id,CharacterId,QuestId,FlagKey,`Value`,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version "
                + "FROM missionflags WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "missionstates",
                "SELECT Id,CharacterId,QuestId,State,CurrentStepId,OfferedAtUtcTicks,AcceptedAtUtcTicks,"
                + "CompletedAtUtcTicks,FailedAtUtcTicks,AbandonedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version "
                + "FROM missionstates WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "missionobjectiveprogress",
                "SELECT Id,CharacterId,QuestId,ObjectiveId,Progress,RequiredCount,LastObservationKey,"
                + "CreatedAtUtcTicks,UpdatedAtUtcTicks,Version FROM missionobjectiveprogress WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "missionobjectiveobservations",
                "SELECT Id,CharacterId,QuestId,ObjectiveId,ObservationKey,EventType,SourceIdentity,TargetIdentity,"
                + "ObservedAtUtcTicks FROM missionobjectiveobservations WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "missionrewardledger",
                "SELECT Id,CharacterId,QuestId,RewardKey,RewardType,Status,Attempts,EffectReference,LastError,ClaimToken,"
                + "ClaimedAtUtcTicks,ClaimExpiresAtUtcTicks,AppliedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version "
                + "FROM missionrewardledger WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "characterstimers",
                "SELECT Id,CharacterId,Strain,Timespan,`Function` FROM characterstimers WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "charactersactivenanos",
                "SELECT Id,CharacterId,NanoId,Strain FROM charactersactivenanos WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "charactersmeshs",
                "SELECT Id,CharacterId,Playfield,MeshValue1,MeshValue2,MeshValue3 FROM charactersmeshs "
                + "WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "charactersuploadednanos",
                "SELECT Id,CharacterId,NanoId FROM charactersuploadednanos WHERE CharacterId=@characterId"),
            new CleanupTableContract(
                "charactersperks",
                "SELECT Id,CharacterId,PacketId FROM charactersperks WHERE CharacterId=@characterId")
        };

        internal static Stage7CharacterCleanupSnapshot Capture(int characterId)
        {
            var rowsByTable = new Dictionary<string, string[]>(StringComparer.Ordinal);
            using (IDbConnection connection = Connector.GetConnection())
            {
                foreach (CleanupTableContract table in CleanupTables)
                {
                    var rows = new List<string>();
                    try
                    {
                        using (IDbCommand command = connection.CreateCommand())
                        {
                            command.CommandText = table.SelectSql;
                            AddParameter(command, "@characterId", characterId);
                            using (IDataReader reader = command.ExecuteReader())
                            {
                                string columns = string.Join(
                                    "|",
                                    Enumerable.Range(0, reader.FieldCount)
                                        .Select(index => EncodeText(reader.GetName(index))));
                                while (reader.Read())
                                {
                                    var values = new string[reader.FieldCount];
                                    for (int index = 0; index < reader.FieldCount; index++)
                                    {
                                        values[index] = EncodeValue(reader.GetValue(index));
                                    }

                                    rows.Add(columns + "#" + string.Join("|", values));
                                }
                            }
                        }
                    }
                    catch
                    {
                        throw new Stage7SecurityContractException(
                            "sentinel-snapshot-" + table.Name);
                    }

                    rows.Sort(StringComparer.Ordinal);
                    rowsByTable.Add(table.Name, rows.ToArray());
                }
            }

            return new Stage7CharacterCleanupSnapshot(rowsByTable);
        }

        internal static void Seed(int ownedCharacterId, int foreignCharacterId, string token)
        {
            using (IDbConnection connection = Connector.GetConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                SeedCharacter(connection, transaction, ownedCharacterId, foreignCharacterId, token, "owned", 1);
                SeedCharacter(connection, transaction, foreignCharacterId, ownedCharacterId, token, "foreign", 2);
                transaction.Commit();
            }
        }

        private static void SeedCharacter(
            IDbConnection connection,
            IDbTransaction transaction,
            int characterId,
            int receivedId,
            string token,
            string role,
            int marker)
        {
            string questId = "stage71.cleanup." + token + "." + role;
            string objectiveId = "objective." + role;
            long tickBase = 638718048000000000L + (marker * 1000L);
            string phase = "organization";

            try
            {

            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO organizations "
                    + "(Creation,Name,LeaderId,GovernmentForm,Description,Objective,History,Tax,Bank,Comission,ContractsId,CityID,TowerFieldId) "
                    + "VALUES (@creation,@name,@characterId,@governmentForm,@description,@objective,@history,@tax,@bank,@comission,@contractsId,@cityId,@towerFieldId)",
                    P("@creation", new DateTime(2026, 1, 7, 1, 2, marker, DateTimeKind.Unspecified)),
                    P("@name", "S71" + marker.ToString(CultureInfo.InvariantCulture) + token),
                    P("@characterId", characterId),
                    P("@governmentForm", 70 + marker),
                    P("@description", "stage71-" + role + "-description"),
                    P("@objective", "stage71-" + role + "-objective"),
                    P("@history", "stage71-" + role + "-history"),
                    P("@tax", 700 + marker),
                    P("@bank", 710000L + marker),
                    P("@comission", 720 + marker),
                    P("@contractsId", 730 + marker),
                    P("@cityId", 740 + marker),
                    P("@towerFieldId", 750 + marker)),
                "sentinel-organization-insert");

            phase = "item";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO items "
                    + "(ContainerType,ContainerInstance,ContainerPlacement,LowId,HighId,Quality,MultipleCount) "
                    + "VALUES (@characterId,@containerInstance,@placement,@lowId,@highId,@quality,@multipleCount)",
                    P("@characterId", characterId),
                    P("@containerInstance", 7100 + marker),
                    P("@placement", 2000000000 + marker),
                    P("@lowId", 710001 + marker),
                    P("@highId", 710101 + marker),
                    P("@quality", 70 + marker),
                    P("@multipleCount", 7 + marker)),
                "sentinel-item-insert");

            phase = "instanceditem";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO instanceditems "
                    + "(ContainerType,ContainerInstance,ContainerPlacement,Itemtype,LowId,HighId,Quality,MultipleCount,"
                    + "X,Y,Z,HeadingX,HeadingY,HeadingZ,HeadingW,stats) "
                    + "VALUES (@characterId,@containerInstance,@placement,@itemType,@lowId,@highId,@quality,@multipleCount,"
                    + "@x,@y,@z,@headingX,@headingY,@headingZ,@headingW,@stats)",
                    P("@characterId", characterId),
                    P("@containerInstance", 7200 + marker),
                    P("@placement", 1999999900 + marker),
                    P("@itemType", 53019),
                    P("@lowId", 720001 + marker),
                    P("@highId", 720101 + marker),
                    P("@quality", 80 + marker),
                    P("@multipleCount", 9 + marker),
                    P("@x", 1.25f + marker),
                    P("@y", 2.5f + marker),
                    P("@z", 3.75f + marker),
                    P("@headingX", 0.1f * marker),
                    P("@headingY", 0.2f * marker),
                    P("@headingZ", 0.3f * marker),
                    P("@headingW", 0.4f * marker),
                    P("@stats", new byte[] { 0x71, checked((byte)marker), 0x00, 0x7f })),
                "sentinel-instanceditem-insert");

            phase = "receivedmessage";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO receivedmessages (PlayerId,ReceivedId) VALUES (@characterId,@receivedId)",
                    P("@characterId", characterId),
                    P("@receivedId", receivedId)),
                "sentinel-receivedmessage-insert");

            phase = "stat";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO stats (Instance,Type,StatId,StatValue) VALUES (@characterId,50000,@statId,@statValue)",
                    P("@characterId", characterId),
                    P("@statId", 2000000100 + marker),
                    P("@statValue", 760000 + marker)),
                "sentinel-stat-insert");

            phase = "missionflag";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO missionflags "
                    + "(CharacterId,QuestId,FlagKey,`Value`,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                    + "VALUES (@characterId,@questId,@flagKey,@value,@created,@updated,@version)",
                    P("@characterId", characterId),
                    P("@questId", questId),
                    P("@flagKey", "flag." + role),
                    P("@value", "value-" + role),
                    P("@created", tickBase + 1),
                    P("@updated", tickBase + 2),
                    P("@version", 10 + marker)),
                "sentinel-missionflag-insert");

            phase = "missionstate";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO missionstates "
                    + "(CharacterId,QuestId,State,CurrentStepId,OfferedAtUtcTicks,AcceptedAtUtcTicks,CompletedAtUtcTicks,"
                    + "FailedAtUtcTicks,AbandonedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                    + "VALUES (@characterId,@questId,@state,@step,@offered,@accepted,@completed,@failed,@abandoned,@created,@updated,@version)",
                    P("@characterId", characterId),
                    P("@questId", questId),
                    P("@state", 20 + marker),
                    P("@step", "step." + role),
                    P("@offered", tickBase + 10),
                    P("@accepted", tickBase + 11),
                    P("@completed", tickBase + 12),
                    P("@failed", tickBase + 13),
                    P("@abandoned", tickBase + 14),
                    P("@created", tickBase + 15),
                    P("@updated", tickBase + 16),
                    P("@version", 20 + marker)),
                "sentinel-missionstate-insert");

            phase = "missionobjectiveprogress";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO missionobjectiveprogress "
                    + "(CharacterId,QuestId,ObjectiveId,Progress,RequiredCount,LastObservationKey,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                    + "VALUES (@characterId,@questId,@objectiveId,@progress,@required,@observation,@created,@updated,@version)",
                    P("@characterId", characterId),
                    P("@questId", questId),
                    P("@objectiveId", objectiveId),
                    P("@progress", 30 + marker),
                    P("@required", 40 + marker),
                    P("@observation", "last." + role),
                    P("@created", tickBase + 20),
                    P("@updated", tickBase + 21),
                    P("@version", 30 + marker)),
                "sentinel-missionobjectiveprogress-insert");

            phase = "missionobjectiveobservation";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO missionobjectiveobservations "
                    + "(CharacterId,QuestId,ObjectiveId,ObservationKey,EventType,SourceIdentity,TargetIdentity,ObservedAtUtcTicks) "
                    + "VALUES (@characterId,@questId,@objectiveId,@observationKey,@eventType,@source,@target,@observed)",
                    P("@characterId", characterId),
                    P("@questId", questId),
                    P("@objectiveId", objectiveId),
                    P("@observationKey", "observation." + role),
                    P("@eventType", "stage71-" + role),
                    P("@source", "source-" + role),
                    P("@target", "target-" + role),
                    P("@observed", tickBase + 30)),
                "sentinel-missionobjectiveobservation-insert");

            phase = "missionrewardledger";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO missionrewardledger "
                    + "(CharacterId,QuestId,RewardKey,RewardType,Status,Attempts,EffectReference,LastError,ClaimToken,"
                    + "ClaimedAtUtcTicks,ClaimExpiresAtUtcTicks,AppliedAtUtcTicks,CreatedAtUtcTicks,UpdatedAtUtcTicks,Version) "
                    + "VALUES (@characterId,@questId,@rewardKey,@rewardType,@status,@attempts,@effect,@error,@claimToken,"
                    + "@claimed,@expires,@applied,@created,@updated,@version)",
                    P("@characterId", characterId),
                    P("@questId", questId),
                    P("@rewardKey", "reward." + role),
                    P("@rewardType", "type-" + role),
                    P("@status", 40 + marker),
                    P("@attempts", 50 + marker),
                    P("@effect", "effect-" + role),
                    P("@error", "error-" + role),
                    P("@claimToken", "claim-" + role),
                    P("@claimed", tickBase + 40),
                    P("@expires", tickBase + 41),
                    P("@applied", tickBase + 42),
                    P("@created", tickBase + 43),
                    P("@updated", tickBase + 44),
                    P("@version", 40 + marker)),
                "sentinel-missionrewardledger-insert");

            phase = "characterstimer";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO characterstimers (CharacterId,Strain,Timespan,`Function`) "
                    + "VALUES (@characterId,@strain,@timespan,@function)",
                    P("@characterId", characterId),
                    P("@strain", 7700 + marker),
                    P("@timespan", 7800 + marker),
                    P("@function", new byte[] { 0x72, checked((byte)marker), 0x10, 0xff })),
                "sentinel-characterstimer-insert");

            phase = "charactersactivenano";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO charactersactivenanos (CharacterId,NanoId,Strain) VALUES (@characterId,@nanoId,@strain)",
                    P("@characterId", characterId),
                    P("@nanoId", 790000 + marker),
                    P("@strain", 7900 + marker)),
                "sentinel-charactersactivenano-insert");

            phase = "charactersmesh";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO charactersmeshs (CharacterId,Playfield,MeshValue1,MeshValue2,MeshValue3) "
                    + "VALUES (@characterId,@playfield,@mesh1,@mesh2,@mesh3)",
                    P("@characterId", characterId),
                    P("@playfield", 8000 + marker),
                    P("@mesh1", 8100 + marker),
                    P("@mesh2", 8200 + marker),
                    P("@mesh3", 8300 + marker)),
                "sentinel-charactersmesh-insert");

            phase = "charactersuploadednano";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO charactersuploadednanos (CharacterId,NanoId) VALUES (@characterId,@nanoId)",
                    P("@characterId", characterId),
                    P("@nanoId", 840000 + marker)),
                "sentinel-charactersuploadednano-insert");

            phase = "charactersperk";
            RequireOne(
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO charactersperks (CharacterId,PacketId) VALUES (@characterId,@packetId)",
                    P("@characterId", characterId),
                    P("@packetId", 850000 + marker)),
                "sentinel-charactersperk-insert");
            }
            catch (Stage7SecurityContractException)
            {
                throw;
            }
            catch
            {
                throw new Stage7SecurityContractException(
                    "sentinel-" + role + "-" + phase + "-sql");
            }
        }

        private static int Execute(
            IDbConnection connection,
            IDbTransaction transaction,
            string sql,
            params SqlParameterValue[] parameters)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                foreach (SqlParameterValue parameter in parameters)
                {
                    AddParameter(command, parameter.Name, parameter.Value);
                }

                return command.ExecuteNonQuery();
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static SqlParameterValue P(string name, object value)
        {
            return new SqlParameterValue(name, value);
        }

        private static void RequireOne(int rows, string code)
        {
            if (rows != 1)
            {
                throw new Stage7SecurityContractException(code);
            }
        }

        private static string EncodeValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "null";
            }

            var bytes = value as byte[];
            if (bytes != null)
            {
                return "bytes:" + Convert.ToBase64String(bytes);
            }

            string text;
            var dateTime = value as DateTime?;
            if (dateTime.HasValue)
            {
                text = dateTime.Value.ToString("O", CultureInfo.InvariantCulture);
            }
            else
            {
                var formattable = value as IFormattable;
                text = formattable == null
                           ? Convert.ToString(value, CultureInfo.InvariantCulture)
                           : formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.GetType().FullName + ":" + EncodeText(text ?? string.Empty);
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private sealed class CleanupTableContract
        {
            internal CleanupTableContract(string name, string selectSql)
            {
                this.Name = name;
                this.SelectSql = selectSql;
            }

            internal string Name { get; private set; }

            internal string SelectSql { get; private set; }
        }

        private sealed class SqlParameterValue
        {
            internal SqlParameterValue(string name, object value)
            {
                this.Name = name;
                this.Value = value;
            }

            internal string Name { get; private set; }

            internal object Value { get; private set; }
        }
    }

    internal sealed class Stage7CharacterCleanupSnapshot
    {
        private readonly IDictionary<string, string[]> rowsByTable;

        internal Stage7CharacterCleanupSnapshot(IDictionary<string, string[]> rowsByTable)
        {
            this.rowsByTable = rowsByTable;
        }

        internal bool HasRowsInEveryTable
        {
            get { return this.rowsByTable.Count > 0 && this.rowsByTable.Values.All(rows => rows.Length > 0); }
        }

        internal bool IsEmpty
        {
            get { return this.rowsByTable.Values.All(rows => rows.Length == 0); }
        }

        internal bool Matches(Stage7CharacterCleanupSnapshot other)
        {
            if (other == null || this.rowsByTable.Count != other.rowsByTable.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string[]> pair in this.rowsByTable)
            {
                string[] otherRows;
                if (!other.rowsByTable.TryGetValue(pair.Key, out otherRows)
                    || !pair.Value.SequenceEqual(otherRows, StringComparer.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
