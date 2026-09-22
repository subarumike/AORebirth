namespace AORebirth.LinuxBuild.Stage7MySqlSecurityIntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Reflection;

    using AO.Core.Encryption;

    using AORebirth.Core.Components;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using Cell.Core;

    using LoginEngine.Component;
    using LoginEngine.CoreClient;
    using LoginEngine.CoreServer;
    using LoginEngine.MessageHandlers;

    using MySqlConnector;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility.Config;

    internal static class Program
    {
        private const string AcknowledgementEnvironment = "AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ACK";
        private const string AcknowledgementValue = "AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ONLY";
        private const string ConfigurationEnvironment = "AO_REBIRTH_CONFIG_PATH";
        private const string ConnectionEnvironment = "AO_REBIRTH_MYSQL_CONNECTION";
        private const string ExpectedDatabase = "aorebirth_chatengine_stage6";
        private const string ExpectedServer = "127.0.0.1";
        private const uint ExpectedPort = 33067;
        private const string ExpectedUser = "aorebirth_stage6";
        private const string RequiredSqlTypeEnvironment = "AO_REBIRTH_REQUIRED_SQL_TYPE";
        private const string RequiredSqlTypeValue = "MySql";
        // CharacterName seeds 23 base stats, StarterVitalStats adds 2, and StarterXpStats adds 5.
        private const int ExpectedCreatedStatCount = 30;

        // SoldierStarterLoadout defines slots 64-71 and the Rubi-Ka selection path creates one pending flag.
        private const int ExpectedCreatedItemCount = 8;
        private const int ExpectedCreatedMissionFlagCount = 1;
        private const int ExpectedRubiKaPlayfield = 6553;
        private const ushort ExpectedZonePort = 7501;

        private static readonly string[] ExpectedTables =
        {
            "characterstimers",
            "characters",
            "charactersactivenanos",
            "charactersmeshs",
            "charactersuploadednanos",
            "charactersperks",
            "instanceditems",
            "itemnames",
            "items",
            "login",
            "missionaccountflags",
            "missionflags",
            "missionobjectiveobservations",
            "missionobjectiveprogress",
            "missionrewardledger",
            "missionstates",
            "mobdroptable",
            "mobspawns",
            "mobspawnsactivenanos",
            "mobspawnsinventory",
            "mobspawnsmeshs",
            "mobspawnsuploadednanos",
            "mobspawns_stats",
            "mobtemplate",
            "organizations",
            "proxydestinations",
            "receivedmessages",
            "shopinventorytemplates",
            "staticdynels",
            "stats",
            "teleports",
            "tradeskill",
            "vendors",
            "vendortemplate"
        };

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                return ValidateOffline();
            }

            if (args.Length != 1
                || !string.Equals(args[0], "--run-disposable", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "REFUSED: Stage 7.1 MySQL security integration requires the exact --run-disposable flag.");
                return 2;
            }

            return RunDisposable();
        }

        private static int ValidateOffline()
        {
            try
            {
                Stage7SecuritySourceContract.Verify();
                const string offlineUsername = "stage71_offline_account";
                const string offlinePassword = "Stage71-Offline-Password";
                var offlineSalt = new byte[32];
                for (int index = 0; index < offlineSalt.Length; index++)
                {
                    offlineSalt[index] = checked((byte)(index + 1));
                }

                string offlineCredentials = DeterministicLoginKeyEncoder.Create(
                    offlineUsername,
                    offlinePassword,
                    offlineSalt);
                string decodedUsername;
                string decodedSalt;
                string decodedPassword;
                new LoginEncryption().DecryptLoginKey(
                    offlineCredentials,
                    out decodedUsername,
                    out decodedSalt,
                    out decodedPassword);
                Require(
                    string.Equals(decodedUsername, offlineUsername, StringComparison.Ordinal),
                    "offline-encoder-username");
                Require(
                    string.Equals(decodedSalt, ToLowerHex(offlineSalt), StringComparison.Ordinal),
                    "offline-encoder-salt");
                Require(
                    string.Equals(decodedPassword, offlinePassword, StringComparison.Ordinal),
                    "offline-encoder-password");

                ValidateConnectionTarget(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline;ConnectionProtocol=Sockets");
                ExpectTargetRejection(
                    "Server=0.0.0.0;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=3306;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=cellao_codex_clean;User ID=aorebirth_stage6;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=root;Password=offline");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline;ConnectionProtocol=Pipe;PipeName=stage7_wrong_pipe");
                ExpectTargetRejection(
                    "Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;User ID=aorebirth_stage6;Password=offline;ConnectionProtocol=UnixSocket");

                using (HandlerHarness harness = HandlerHarness.Create())
                {
                    new CreateCharacterHandler().Handle(
                        harness.Client,
                        CreateMessage(CreateRubiKaSoldier("StageOffline")));
                    RequireRejectedAndClosed(harness, "offline-preauth-create-not-rejected");
                }

                Console.WriteLine(
                    "PASS: Stage 7.1 offline target/encoder/preauth guards; database=closed listeners=0.");
                return 0;
            }
            catch (Stage7SecurityContractException exception)
            {
                Console.Error.WriteLine("FAIL: Stage 7.1 offline contract code=" + exception.Code + ".");
                return 1;
            }
            catch
            {
                Console.Error.WriteLine("FAIL: Stage 7.1 offline contract code=unexpected.");
                return 1;
            }
        }

        private static int RunDisposable()
        {
            string failureCode = null;
            bool cleanupFailed = false;
            bool fixtureScopeEstablished = false;
            string currentPhase = "initial-guards";
            var fixture = FixtureScope.Create();
            Stage7SecurityDatabaseBaseline cleanBaseline = null;

            try
            {
                Require(
                    string.Equals(
                        Environment.GetEnvironmentVariable(AcknowledgementEnvironment),
                        AcknowledgementValue,
                        StringComparison.Ordinal),
                    "missing-exact-disposable-acknowledgement");
                Require(
                    string.Equals(
                        Environment.GetEnvironmentVariable(RequiredSqlTypeEnvironment),
                        RequiredSqlTypeValue,
                        StringComparison.Ordinal),
                    "missing-exact-required-sql-type");

                currentPhase = "configuration-guards";
                string configurationPath = Environment.GetEnvironmentVariable(ConfigurationEnvironment);
                Require(!string.IsNullOrWhiteSpace(configurationPath), "missing-configuration-path");
                Require(Path.IsPathFullyQualified(configurationPath), "configuration-path-not-absolute");
                Require(File.Exists(configurationPath), "configuration-path-not-file");

                string connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironment);
                Require(!string.IsNullOrWhiteSpace(connectionString), "missing-connection-environment");
                ValidateConnectionTarget(connectionString);
                ValidateProductionConfiguration(connectionString);
                currentPhase = "database-identity";
                ValidateDatabaseIdentity();
                currentPhase = "schema-contract";
                ValidateSchemaContract();
                currentPhase = "fixture-collision-guards";
                ValidateFixtureCollisions(fixture);
                cleanBaseline = Stage7SecurityDatabaseBaseline.Capture();
                Require(cleanBaseline.IsEmpty, "fixture-cleanup-table-collision");
                fixtureScopeEstablished = true;

                currentPhase = "login-a-insert";
                InsertLogin(fixture.AccountA, fixture.PasswordA, "Seven", "OneA");
                currentPhase = "login-b-insert";
                InsertLogin(fixture.AccountB, fixture.PasswordB, "Seven", "OneB");
                currentPhase = "foreign-character-insert";
                fixture.ForeignCharacterId = InsertForeignCharacter(fixture.AccountB, fixture.ForeignCharacterName);
                fixture.KnownCharacterIds.Add(fixture.ForeignCharacterId);

                currentPhase = "preauthentication-guards";
                VerifyPreAuthenticationGuards(fixture);
                currentPhase = "credential-guards";
                VerifyInvalidCredentialGuards(fixture);
                currentPhase = "canonical-create";
                fixture.OwnedCharacterId = VerifyAuthenticationAndCanonicalCreation(fixture);
                fixture.KnownCharacterIds.Add(fixture.OwnedCharacterId);
                currentPhase = "created-character-verification";
                VerifyCreatedCharacter(fixture);
                currentPhase = "cleanup-sentinel-seed";
                SeedAndVerifyCleanupSentinels(fixture);
                currentPhase = "foreign-character-guards";
                VerifyForeignCharacterGuards(fixture);
                currentPhase = "owned-select-offline";
                VerifyOwnedSelectionAndOffline(fixture);
                currentPhase = "owned-delete";
                VerifyOwnedDeletion(fixture);
            }
            catch (Stage7SecurityContractException exception)
            {
                failureCode = exception.Code;
            }
            catch
            {
                failureCode = "unexpected-" + currentPhase;
            }
            finally
            {
                if (fixtureScopeEstablished)
                {
                    try
                    {
                        CleanupFixture(fixture, cleanBaseline);
                    }
                    catch
                    {
                        cleanupFailed = true;
                    }
                }
            }

            if (cleanupFailed)
            {
                Console.Error.WriteLine(
                    "FAIL: Stage 7.1 disposable MySQL security integration code=fixture-cleanup; manual disposal of the isolated database is required.");
                return 1;
            }

            if (failureCode != null)
            {
                Console.Error.WriteLine(
                    "FAIL: Stage 7.1 disposable MySQL security integration code=" + failureCode + ".");
                return 1;
            }

            Console.WriteLine(
                "PASS: Stage 7.1 listener-free authentication/ownership/create-select-delete acceptance; fixture residue=0 listeners=0.");
            return 0;
        }

        private static void ValidateProductionConfiguration(string connectionString)
        {
            Config configuration = ConfigReadWrite.Instance.CurrentConfig;
            Require(configuration != null, "production-config-null");
            Require(
                string.Equals(configuration.SQLType, "MySql", StringComparison.Ordinal),
                "production-config-provider");
            Require(
                string.Equals(configuration.MysqlConnection, connectionString, StringComparison.Ordinal),
                "production-config-environment-overlay");
            Require(
                string.Equals(configuration.ZoneIP, ExpectedServer, StringComparison.Ordinal),
                "production-config-zone-ip");
            Require(configuration.ZonePort == ExpectedZonePort, "production-config-zone-port");
        }

        private static void ValidateDatabaseIdentity()
        {
            using (IDbConnection connection = Connector.GetConnection())
            {
                Require(connection.State == ConnectionState.Open, "production-connector-not-open");
                Require(
                    string.Equals(
                        Convert.ToString(ExecuteScalar(connection, "SELECT DATABASE()"), CultureInfo.InvariantCulture),
                        ExpectedDatabase,
                        StringComparison.Ordinal),
                    "active-database-identity");
                Require(
                    string.Equals(
                        Convert.ToString(
                            ExecuteScalar(connection, "SELECT SUBSTRING_INDEX(CURRENT_USER(), '@', 1)"),
                            CultureInfo.InvariantCulture),
                        ExpectedUser,
                        StringComparison.Ordinal),
                    "active-database-user");
            }
        }

        private static void ValidateSchemaContract()
        {
            using (IDbConnection connection = Connector.GetConnection())
            {
                var actualTables = new HashSet<string>(StringComparer.Ordinal);
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT table_name FROM information_schema.tables "
                        + "WHERE table_schema=DATABASE() AND table_type='BASE TABLE'";
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            actualTables.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture));
                        }
                    }
                }

                Require(actualTables.Count == ExpectedTables.Length, "schema-table-count");
                foreach (string tableName in ExpectedTables)
                {
                    Require(actualTables.Contains(tableName), "schema-table-set");
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT 1 FROM `" + tableName + "` LIMIT 0";
                        using (IDataReader reader = command.ExecuteReader())
                        {
                        }
                    }
                }

                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM information_schema.columns "
                        + "WHERE table_schema=DATABASE() AND table_name='characters' AND column_name='Online'") == 1,
                    "schema-characters-online-column");
                Require(
                    ExecuteScalarLong(connection, "SELECT COUNT(*) FROM characters WHERE Online <> 0") == 0,
                    "schema-online-character-residue");

                foreach (string tableName in Stage7SecurityDatabaseBaseline.TrackedTables)
                {
                    Require(
                        ExecuteScalarLong(connection, "SELECT COUNT(*) FROM `" + tableName + "`") == 0,
                        "schema-mutable-table-not-empty");
                }

                Require(
                    ExecuteScalarLong(connection, "SELECT COUNT(*) FROM receivedmessages") == 0,
                    "schema-mutable-table-not-empty");
            }
        }

        private static void ValidateFixtureCollisions(FixtureScope fixture)
        {
            Require(!string.Equals(fixture.AccountA, fixture.AccountB, StringComparison.Ordinal), "fixture-account-identity");
            Require(LoginDataDao.Instance.GetByUsername(fixture.AccountA) == null, "fixture-account-a-collision");
            Require(LoginDataDao.Instance.GetByUsername(fixture.AccountB) == null, "fixture-account-b-collision");
            Require(CharacterDao.Instance.GetByCharName(fixture.PreAuthenticationCharacterName) == null, "fixture-preauth-name-collision");
            Require(CharacterDao.Instance.GetByCharName(fixture.OwnedCharacterName) == null, "fixture-owned-name-collision");
            Require(CharacterDao.Instance.GetByCharName(fixture.ForeignCharacterName) == null, "fixture-foreign-name-collision");
            Require(
                Stage7SecurityDatabaseBaseline.Capture().IsEmpty,
                "fixture-cleanup-table-collision");
        }

        private static void InsertLogin(string username, string password, string firstName, string lastName)
        {
            var encryption = new LoginEncryption();
            var login = new DBLoginData
            {
                AccountFlags = 0,
                AllowedCharacters = 8,
                CreationDate = DateTime.UtcNow,
                Email = username + "@invalid.example",
                Expansions = 0,
                FirstName = firstName,
                Flags = 0,
                GM = 0,
                LastName = lastName,
                Password = encryption.GeneratePasswordHash(password),
                Username = username
            };

            int rows = LoginDataDao.Instance.Add(login);
            Require(rows == 1 && login.Id > 0, "fixture-login-insert");
        }

        private static int InsertForeignCharacter(string username, string characterName)
        {
            var character = new DBCharacter
            {
                BuddyList = string.Empty,
                FirstName = "Foreign",
                HeadingW = 1,
                HeadingX = 0,
                HeadingY = 0,
                HeadingZ = 0,
                LastName = "Fixture",
                Name = characterName,
                Online = 0,
                Playfield = ExpectedRubiKaPlayfield,
                Textures0 = 0,
                Textures1 = 0,
                Textures2 = 0,
                Textures3 = 0,
                Textures4 = 0,
                Username = username,
                X = 3607.6f,
                Y = 52.4f,
                Z = 785.7f
            };

            int rows = CharacterDao.Instance.Add(character);
            Require(rows == 1 && character.Id > 0, "fixture-foreign-character-insert");
            return character.Id;
        }

        private static void VerifyPreAuthenticationGuards(FixtureScope fixture)
        {
            VerifyNoDatabaseMutation("preauth-create", () =>
            {
                using (HandlerHarness createHarness = HandlerHarness.Create())
                {
                    new CreateCharacterHandler().Handle(
                        createHarness.Client,
                        CreateMessage(CreateRubiKaSoldier(fixture.PreAuthenticationCharacterName)));
                    RequireRejectedAndClosed(createHarness, "preauth-create-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("preauth-select", () =>
            {
                using (HandlerHarness selectHarness = HandlerHarness.Create())
                {
                    new SelectCharacterHandler().Handle(
                        selectHarness.Client,
                        CreateMessage(new SelectCharacterMessage { CharacterId = fixture.ForeignCharacterId }));
                    RequireRejectedAndClosed(selectHarness, "preauth-select-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("preauth-delete", () =>
            {
                using (HandlerHarness deleteHarness = HandlerHarness.Create())
                {
                    new DeleteCharacterHandler().Handle(
                        deleteHarness.Client,
                        CreateMessage(new DeleteCharacterMessage { CharacterId = fixture.ForeignCharacterId }));
                    RequireRejectedAndClosed(deleteHarness, "preauth-delete-not-rejected");
                }
            });
        }

        private static void VerifyInvalidCredentialGuards(FixtureScope fixture)
        {
            VerifyNoDatabaseMutation("wrong-password", () =>
            {
                using (HandlerHarness harness = HandlerHarness.Create())
                {
                    byte[] salt = IssueChallenge(harness, fixture.AccountA);
                    string credentials = DeterministicLoginKeyEncoder.Create(
                        fixture.AccountA,
                        fixture.PasswordA + "-wrong",
                        salt);
                    SubmitCredentials(harness, fixture.AccountA, credentials);
                    RequireRejectedAndClosed(harness, "wrong-password-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("credential-username-mismatch", () =>
            {
                using (HandlerHarness harness = HandlerHarness.Create())
                {
                    byte[] salt = IssueChallenge(harness, fixture.AccountA);
                    string credentials = DeterministicLoginKeyEncoder.Create(
                        fixture.AccountA,
                        fixture.PasswordA,
                        salt);
                    SubmitCredentials(harness, fixture.AccountB, credentials);
                    RequireRejectedAndClosed(harness, "credential-username-mismatch-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("credential-replay", () =>
            {
                string replayCredentials;
                using (HandlerHarness harness = Authenticate(
                    fixture.AccountA,
                    fixture.PasswordA,
                    fixture.AccountA,
                    out replayCredentials))
                {
                    SubmitCredentials(harness, fixture.AccountA, replayCredentials);
                    RequireRejectedAndClosed(harness, "credential-replay-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("replacement-challenge", () =>
            {
                using (HandlerHarness harness = HandlerHarness.Create())
                {
                    byte[] firstSalt = IssueChallenge(harness, fixture.AccountA);
                    string firstCredentials = DeterministicLoginKeyEncoder.Create(
                        fixture.AccountA,
                        fixture.PasswordA,
                        firstSalt);
                    IssueChallenge(harness, fixture.AccountB);
                    SubmitCredentials(harness, fixture.AccountA, firstCredentials);
                    RequireRejectedAndClosed(harness, "replacement-challenge-not-rejected");
                }
            });
        }

        private static int VerifyAuthenticationAndCanonicalCreation(FixtureScope fixture)
        {
            string challengedIdentity = fixture.AccountA.ToUpperInvariant();
            using (HandlerHarness harness = Authenticate(
                challengedIdentity,
                fixture.PasswordA,
                fixture.AccountA))
            {
                harness.Client.AccountName = fixture.AccountB;
                harness.Serializer.SerializedMessages.Clear();
                new CreateCharacterHandler().Handle(
                    harness.Client,
                    CreateMessage(CreateRubiKaSoldier(fixture.OwnedCharacterName)));

                Require(harness.Serializer.SerializedMessages.Count == 1, "owned-create-response-count");
                var response = harness.Serializer.SerializedMessages[0].Body as CharacterCreatedMessage;
                Require(response != null && response.CharacterId > 0, "owned-create-response-type");

                DBCharacter character = CharacterDao.Instance.Get(response.CharacterId);
                Require(character != null, "owned-create-character-missing");
                Require(
                    string.Equals(character.Username, fixture.AccountA, StringComparison.Ordinal),
                    "canonical-account-tamper");
                Require(
                    !string.Equals(character.Username, fixture.AccountB, StringComparison.Ordinal),
                    "canonical-account-replaced-by-public-property");
                return response.CharacterId;
            }
        }

        private static void VerifyCreatedCharacter(FixtureScope fixture)
        {
            DBCharacter character = CharacterDao.Instance.Get(fixture.OwnedCharacterId);
            Require(character != null, "created-character-read");
            Require(
                string.Equals(character.Name, fixture.OwnedCharacterName, StringComparison.Ordinal)
                && string.Equals(character.Username, fixture.AccountA, StringComparison.Ordinal),
                "created-character-identity");
            Require(
                CharacterDao.Instance.IsCharacterOnAccount(
                    fixture.AccountA,
                    unchecked((uint)fixture.OwnedCharacterId)),
                "created-character-owner-positive");
            Require(
                !CharacterDao.Instance.IsCharacterOnAccount(
                    fixture.AccountB,
                    unchecked((uint)fixture.OwnedCharacterId)),
                "created-character-owner-negative");
            Require(
                character.Playfield == ExpectedRubiKaPlayfield
                && Math.Abs(character.X - 3607.6f) < 0.01f
                && Math.Abs(character.Y - 52.4f) < 0.01f
                && Math.Abs(character.Z - 785.7f) < 0.01f,
                "created-character-arete-location");
            Require(character.Online == 0, "created-character-not-offline");

            using (IDbConnection connection = Connector.GetConnection())
            {
                ParameterValue characterId = Parameter("@characterId", fixture.OwnedCharacterId);
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM stats WHERE Type=50000 AND Instance=@characterId",
                        characterId) == ExpectedCreatedStatCount,
                    "created-character-stat-count");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(DISTINCT StatId) FROM stats WHERE Type=50000 AND Instance=@characterId",
                        characterId) == ExpectedCreatedStatCount,
                    "created-character-stat-distinct-count");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM items WHERE containertype=@characterId",
                        characterId) == ExpectedCreatedItemCount,
                    "created-character-item-count");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM items WHERE containertype=@characterId "
                        + "AND containerinstance=@inventory AND containerplacement BETWEEN 64 AND 71",
                        characterId,
                        Parameter("@inventory", (int)IdentityType.Inventory)) == ExpectedCreatedItemCount,
                    "created-character-item-shape");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM instanceditems WHERE containertype=@characterId",
                        characterId) == 0,
                    "created-character-instanced-item-count");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM missionflags WHERE CharacterId=@characterId",
                        characterId) == ExpectedCreatedMissionFlagCount,
                    "created-character-missionflag-count");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM missionflags WHERE CharacterId=@characterId "
                        + "AND QuestId=@questId AND FlagKey=@flagKey AND `Value`=@flagValue",
                        characterId,
                        Parameter("@questId", "system.new_character_start_area"),
                        Parameter("@flagKey", "selection"),
                        Parameter("@flagValue", NewCharacterStartAreaSelectionDao.PendingState)) == 1,
                    "created-character-missionflag-shape");
            }
        }

        private static void VerifyForeignCharacterGuards(FixtureScope fixture)
        {
            VerifyNoDatabaseMutation("foreign-select", () =>
            {
                using (HandlerHarness selectHarness = Authenticate(
                    fixture.AccountA,
                    fixture.PasswordA,
                    fixture.AccountA))
                {
                    selectHarness.Serializer.SerializedMessages.Clear();
                    new SelectCharacterHandler().Handle(
                        selectHarness.Client,
                        CreateMessage(new SelectCharacterMessage { CharacterId = fixture.ForeignCharacterId }));
                    RequireRejectedAndClosed(selectHarness, "foreign-select-not-rejected");
                }
            });

            VerifyNoDatabaseMutation("foreign-delete", () =>
            {
                using (HandlerHarness deleteHarness = Authenticate(
                    fixture.AccountA,
                    fixture.PasswordA,
                    fixture.AccountA))
                {
                    deleteHarness.Serializer.SerializedMessages.Clear();
                    new DeleteCharacterHandler().Handle(
                        deleteHarness.Client,
                        CreateMessage(new DeleteCharacterMessage { CharacterId = fixture.ForeignCharacterId }));
                    RequireRejectedAndClosed(deleteHarness, "foreign-delete-not-rejected");
                }
            });

            DBCharacter foreign = CharacterDao.Instance.Get(fixture.ForeignCharacterId);
            Require(
                foreign != null
                && foreign.Online == 0
                && string.Equals(foreign.Username, fixture.AccountB, StringComparison.Ordinal),
                "foreign-character-not-unchanged");
        }

        private static void SeedAndVerifyCleanupSentinels(FixtureScope fixture)
        {
            Stage7CharacterCleanupFixture.Seed(
                fixture.OwnedCharacterId,
                fixture.ForeignCharacterId,
                fixture.SentinelToken);
            Require(
                Stage7CharacterCleanupFixture.Capture(fixture.OwnedCharacterId).HasRowsInEveryTable,
                "owned-cleanup-sentinel-shape");
            Require(
                Stage7CharacterCleanupFixture.Capture(fixture.ForeignCharacterId).HasRowsInEveryTable,
                "foreign-cleanup-sentinel-shape");
        }

        private static void VerifyOwnedSelectionAndOffline(FixtureScope fixture)
        {
            Stage7SecurityDatabaseBaseline before = Stage7SecurityDatabaseBaseline.Capture();
            using (HandlerHarness harness = Authenticate(
                fixture.AccountA,
                fixture.PasswordA,
                fixture.AccountA))
            {
                harness.Serializer.SerializedMessages.Clear();
                new SelectCharacterHandler().Handle(
                    harness.Client,
                    CreateMessage(new SelectCharacterMessage { CharacterId = fixture.OwnedCharacterId }));

                Require(harness.Serializer.SerializedMessages.Count == 1, "owned-select-response-count");
                var response = harness.Serializer.SerializedMessages[0].Body as ZoneInfoMessage;
                Require(response != null, "owned-select-response-type");
                Require(response.CharacterId == fixture.OwnedCharacterId, "owned-select-zone-character");
                Require(
                    response.ServerIpAddress != null
                    && response.ServerIpAddress.Equals(IPAddress.Loopback),
                    "owned-select-zone-ip");
                Require(response.ServerPort == ExpectedZonePort, "owned-select-zone-port");
            }

            Require(CharacterDao.Instance.IsOnline(fixture.OwnedCharacterId) == 1, "owned-select-online-transition");
            CharacterDao.Instance.SetOffline(fixture.OwnedCharacterId);
            Require(CharacterDao.Instance.IsOnline(fixture.OwnedCharacterId) == 0, "owned-select-offline-transition");
            Require(
                before.Matches(Stage7SecurityDatabaseBaseline.Capture()),
                "owned-select-unexpected-database-mutation");
        }

        private static void VerifyOwnedDeletion(FixtureScope fixture)
        {
            Stage7CharacterCleanupSnapshot ownedBefore =
                Stage7CharacterCleanupFixture.Capture(fixture.OwnedCharacterId);
            Stage7CharacterCleanupSnapshot foreignBefore =
                Stage7CharacterCleanupFixture.Capture(fixture.ForeignCharacterId);
            Require(ownedBefore.HasRowsInEveryTable, "owned-delete-cleanup-sentinel-missing");
            Require(foreignBefore.HasRowsInEveryTable, "foreign-delete-cleanup-sentinel-missing");

            using (HandlerHarness harness = Authenticate(
                fixture.AccountA,
                fixture.PasswordA,
                fixture.AccountA))
            {
                harness.Serializer.SerializedMessages.Clear();
                new DeleteCharacterHandler().Handle(
                    harness.Client,
                    CreateMessage(new DeleteCharacterMessage { CharacterId = fixture.OwnedCharacterId }));

                Require(harness.Serializer.SerializedMessages.Count == 1, "owned-delete-response-count");
                var response = harness.Serializer.SerializedMessages[0].Body as CharacterDeletedMessage;
                Require(
                    response != null && response.CharacterId == fixture.OwnedCharacterId,
                    "owned-delete-response-type");
            }

            Require(CharacterDao.Instance.Get(fixture.OwnedCharacterId) == null, "owned-delete-character-residue");
            DBCharacter foreign = CharacterDao.Instance.Get(fixture.ForeignCharacterId);
            Require(
                foreign != null
                && foreign.Online == 0
                && string.Equals(foreign.Username, fixture.AccountB, StringComparison.Ordinal),
                "owned-delete-foreign-character-changed");
            Require(
                Stage7CharacterCleanupFixture.Capture(fixture.OwnedCharacterId).IsEmpty,
                "owned-delete-dependent-row-residue");
            Require(
                foreignBefore.Matches(Stage7CharacterCleanupFixture.Capture(fixture.ForeignCharacterId)),
                "owned-delete-foreign-dependent-row-changed");
            using (IDbConnection connection = Connector.GetConnection())
            {
                ParameterValue characterId = Parameter("@characterId", fixture.OwnedCharacterId);
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM stats WHERE Type=50000 AND Instance=@characterId",
                        characterId) == 0,
                    "owned-delete-stat-residue");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM items WHERE containertype=@characterId",
                        characterId) == 0,
                    "owned-delete-item-residue");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM instanceditems WHERE containertype=@characterId",
                        characterId) == 0,
                    "owned-delete-instanced-item-residue");
                Require(
                    ExecuteScalarLong(
                        connection,
                        "SELECT COUNT(*) FROM missionflags WHERE CharacterId=@characterId",
                        characterId) == 0,
                    "owned-delete-missionflag-residue");
            }
        }

        private static HandlerHarness Authenticate(
            string challengedAccount,
            string password,
            string expectedCanonicalAccount)
        {
            string credentials;
            return Authenticate(
                challengedAccount,
                password,
                expectedCanonicalAccount,
                out credentials);
        }

        private static HandlerHarness Authenticate(
            string challengedAccount,
            string password,
            string expectedCanonicalAccount,
            out string credentials)
        {
            HandlerHarness harness = HandlerHarness.Create();
            try
            {
                byte[] salt = IssueChallenge(harness, challengedAccount);
                credentials = DeterministicLoginKeyEncoder.Create(
                    challengedAccount,
                    password,
                    salt);
                SubmitCredentials(harness, challengedAccount, credentials);

                Require(harness.Serializer.SerializedMessages.Count == 1, "authentication-credentials-response-count");
                Require(
                    harness.Serializer.SerializedMessages[0].Body is CharacterListMessage,
                    "authentication-credentials-response-type");
                Require(
                    string.Equals(harness.Client.AccountName, expectedCanonicalAccount, StringComparison.Ordinal),
                    "authentication-canonical-account");
                Require(
                    string.Equals(harness.Client.ServerSalt, string.Empty, StringComparison.Ordinal),
                    "authentication-salt-not-cleared");
                return harness;
            }
            catch
            {
                harness.Dispose();
                throw;
            }
        }

        private static byte[] IssueChallenge(HandlerHarness harness, string accountName)
        {
            harness.Serializer.SerializedMessages.Clear();
            new UserLoginHandler().Handle(
                harness.Client,
                CreateMessage(
                    new UserLoginMessage
                    {
                        UserName = accountName,
                        ClientVersion = "18.8.53_EP1"
                    }));
            Require(harness.Serializer.SerializedMessages.Count == 1, "authentication-salt-response-count");
            var saltResponse = harness.Serializer.SerializedMessages[0].Body as ServerSaltMessage;
            Require(
                saltResponse != null
                && saltResponse.ServerSalt != null
                && saltResponse.ServerSalt.Length == 32,
                "authentication-salt-response-shape");
            foreach (byte value in saltResponse.ServerSalt)
            {
                Require(value != 0, "authentication-salt-zero-byte");
            }

            return (byte[])saltResponse.ServerSalt.Clone();
        }

        private static void SubmitCredentials(
            HandlerHarness harness,
            string accountName,
            string credentials)
        {
            harness.Serializer.SerializedMessages.Clear();
            new UserCredentialsHandler().Handle(
                harness.Client,
                CreateMessage(
                    new UserCredentialsMessage
                    {
                        UserName = accountName,
                        Credentials = credentials
                    }));
        }

        private static CreateCharacterMessage CreateRubiKaSoldier(string name)
        {
            return new CreateCharacterMessage
            {
                Unknown1 = new byte[49],
                Name = name,
                Breed = Breed.Solitus,
                Gender = Gender.Male,
                Profession = Profession.Soldier,
                Level = 1,
                AreaName = "Rubi-Ka",
                Unknown2 = 0,
                Unknown3 = 0,
                HeadMesh = 40001,
                MonsterScale = 100,
                Fatness = Fatness.Normal,
                StarterArea = StarterArea.RubiKa
            };
        }

        private static Message CreateMessage(MessageBody body)
        {
            return new Message
            {
                Header = new Header
                {
                    MessageId = 0xDFDF,
                    PacketType = body.PacketType,
                    Unknown = 1,
                    Sender = 1,
                    Receiver = 1
                },
                Body = body
            };
        }

        private static void RequireRejectedAndClosed(HandlerHarness harness, string code)
        {
            Require(harness.Serializer.SerializedMessages.Count == 1, code + "-response-count");
            var error = harness.Serializer.SerializedMessages[0].Body as LoginErrorMessage;
            Require(error != null && error.Error == LoginError.InvalidUserNamePassword, code + "-response-type");
            MethodInfo beginAuthentication = typeof(Client).GetMethod(
                "BeginAuthentication",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(beginAuthentication != null, code + "-closed-state-method");
            bool accepted = (bool)beginAuthentication.Invoke(
                harness.Client,
                new object[] { "stage71_closed_probe", "stage7.1", "closed-probe-salt" });
            Require(!accepted, code + "-connection-not-closed");
        }

        private static void VerifyNoDatabaseMutation(string code, Action action)
        {
            Stage7SecurityDatabaseBaseline before = Stage7SecurityDatabaseBaseline.Capture();
            action();
            Require(
                before.Matches(Stage7SecurityDatabaseBaseline.Capture()),
                code + "-database-mutation");
        }

        private static void CleanupFixture(
            FixtureScope fixture,
            Stage7SecurityDatabaseBaseline cleanBaseline)
        {
            var characterIds = new HashSet<int>(fixture.KnownCharacterIds);
            using (IDbConnection connection = Connector.GetConnection())
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText =
                        "SELECT Id FROM characters WHERE Username=@accountA OR Username=@accountB "
                        + "OR Name=@preauthName OR Name=@ownedName OR Name=@foreignName";
                    AddParameter(command, "@accountA", fixture.AccountA);
                    AddParameter(command, "@accountB", fixture.AccountB);
                    AddParameter(command, "@preauthName", fixture.PreAuthenticationCharacterName);
                    AddParameter(command, "@ownedName", fixture.OwnedCharacterName);
                    AddParameter(command, "@foreignName", fixture.ForeignCharacterName);
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            characterIds.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                        }
                    }
                }

                var organizationIds = new HashSet<int>();
                foreach (int characterId in characterIds)
                {
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT Id FROM organizations WHERE LeaderId=@characterId";
                        AddParameter(command, "@characterId", characterId);
                        using (IDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                organizationIds.Add(
                                    Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                            }
                        }
                    }

                    foreach (string tableName in Stage7SecurityDatabaseBaseline.CharacterIdTables)
                    {
                        ExecuteNonQuery(
                            connection,
                            transaction,
                            "DELETE FROM `" + tableName + "` WHERE CharacterId=@characterId",
                            Parameter("@characterId", characterId));
                    }

                    ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM receivedmessages WHERE PlayerId=@characterId",
                        Parameter("@characterId", characterId));

                    foreach (string tableName in Stage7SecurityDatabaseBaseline.ContainerTypeTables)
                    {
                        ExecuteNonQuery(
                            connection,
                            transaction,
                            "DELETE FROM `" + tableName + "` WHERE containertype=@characterId",
                            Parameter("@characterId", characterId));
                    }

                    ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM stats WHERE Type=50000 AND Instance=@characterId",
                        Parameter("@characterId", characterId));
                }

                foreach (int organizationId in organizationIds)
                {
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM stats WHERE StatValue=@organizationId AND StatId=@clanStatId",
                        Parameter("@organizationId", organizationId),
                        Parameter("@clanStatId", (int)StatIds.clan));
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM organizations WHERE Id=@organizationId",
                        Parameter("@organizationId", organizationId));
                }

                foreach (int characterId in characterIds)
                {
                    ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM characters WHERE Id=@characterId",
                        Parameter("@characterId", characterId));
                }

                ExecuteNonQuery(
                    connection,
                    transaction,
                    "DELETE FROM login WHERE Username=@accountA OR Username=@accountB",
                    Parameter("@accountA", fixture.AccountA),
                    Parameter("@accountB", fixture.AccountB));
                transaction.Commit();
            }

            Require(cleanBaseline != null, "fixture-clean-baseline-missing");
            Require(
                cleanBaseline.Matches(Stage7SecurityDatabaseBaseline.Capture()),
                "fixture-baseline-residue");
        }

        private static void ValidateConnectionTarget(string connectionString)
        {
            MySqlConnectionStringBuilder builder;
            try
            {
                builder = new MySqlConnectionStringBuilder(connectionString);
            }
            catch
            {
                throw new Stage7SecurityContractException("connection-format");
            }

            Require(
                string.Equals(builder.Server, ExpectedServer, StringComparison.Ordinal),
                "connection-server-not-exact-loopback");
            Require(builder.Port == ExpectedPort, "connection-port-not-isolated");
            Require(
                builder.ConnectionProtocol == MySqlConnectionProtocol.Sockets,
                "connection-protocol-not-sockets");
            Require(
                string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal),
                "connection-database-not-disposable");
            Require(
                string.Equals(builder.UserID, ExpectedUser, StringComparison.Ordinal),
                "connection-user-not-disposable");
            Require(!string.IsNullOrWhiteSpace(builder.Password), "connection-password-empty");
        }

        private static void ExpectTargetRejection(string connectionString)
        {
            try
            {
                ValidateConnectionTarget(connectionString);
            }
            catch (Stage7SecurityContractException)
            {
                return;
            }

            throw new Stage7SecurityContractException("offline-target-guard-accepted-invalid-target");
        }

        private static object ExecuteScalar(
            IDbConnection connection,
            string commandText,
            params ParameterValue[] parameters)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                AddParameters(command, parameters);
                return command.ExecuteScalar();
            }
        }

        private static long ExecuteScalarLong(
            IDbConnection connection,
            string commandText,
            params ParameterValue[] parameters)
        {
            return Convert.ToInt64(
                ExecuteScalar(connection, commandText, parameters),
                CultureInfo.InvariantCulture);
        }

        private static int ExecuteNonQuery(
            IDbConnection connection,
            IDbTransaction transaction,
            string commandText,
            params ParameterValue[] parameters)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = commandText;
                AddParameters(command, parameters);
                return command.ExecuteNonQuery();
            }
        }

        private static void AddParameters(IDbCommand command, IEnumerable<ParameterValue> parameters)
        {
            foreach (ParameterValue parameter in parameters)
            {
                AddParameter(command, parameter.Name, parameter.Value);
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private static ParameterValue Parameter(string name, object value)
        {
            return new ParameterValue(name, value);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var result = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static FieldInfo GetInheritedField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            throw new Stage7SecurityContractException("listener-field-missing");
        }

        private static void Require(bool condition, string code)
        {
            if (!condition)
            {
                throw new Stage7SecurityContractException(code);
            }
        }

        private sealed class FixtureScope
        {
            private FixtureScope()
            {
                this.KnownCharacterIds = new HashSet<int>();
            }

            internal string AccountA { get; private set; }

            internal string AccountB { get; private set; }

            internal int ForeignCharacterId { get; set; }

            internal string ForeignCharacterName { get; private set; }

            internal HashSet<int> KnownCharacterIds { get; private set; }

            internal int OwnedCharacterId { get; set; }

            internal string OwnedCharacterName { get; private set; }

            internal string PasswordA { get; private set; }

            internal string PasswordB { get; private set; }

            internal string PreAuthenticationCharacterName { get; private set; }

            internal string SentinelToken { get; private set; }

            internal static FixtureScope Create()
            {
                string token = Guid.NewGuid().ToString("N");
                return new FixtureScope
                {
                    AccountA = "s71a_" + token.Substring(0, 20),
                    AccountB = "s71b_" + token.Substring(4, 20),
                    ForeignCharacterName = "F" + token.Substring(0, 18),
                    OwnedCharacterName = "O" + token.Substring(8, 18),
                    PasswordA = "Stage71-A-" + Guid.NewGuid().ToString("N"),
                    PasswordB = "Stage71-B-" + Guid.NewGuid().ToString("N"),
                    PreAuthenticationCharacterName = "P" + token.Substring(12, 18),
                    SentinelToken = token.Substring(0, 16)
                };
            }
        }

        private sealed class HandlerHarness : IDisposable
        {
            private HandlerHarness(
                LoginServer server,
                CaptureClient client,
                RecordingSerializer serializer)
            {
                this.Server = server;
                this.Client = client;
                this.Serializer = serializer;
            }

            internal CaptureClient Client { get; private set; }

            internal RecordingSerializer Serializer { get; private set; }

            internal LoginServer Server { get; private set; }

            internal static HandlerHarness Create()
            {
                var serializer = new RecordingSerializer();
                var bus = new RecordingBus();
                var factory = new ClientFactory(serializer, bus);
                var server = new LoginServer(factory);
                CaptureClient client = null;
                try
                {
                    client = new CaptureClient(server, serializer, bus);
                    RequireListenerFree(server);
                    return new HandlerHarness(server, client, serializer);
                }
                catch
                {
                    try
                    {
                        if (client != null) client.Dispose();
                    }
                    finally
                    {
                        server.Dispose();
                    }

                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    RequireListenerFree(this.Server);
                }
                finally
                {
                    try
                    {
                        this.Client.Dispose();
                    }
                    finally
                    {
                        this.Server.Dispose();
                    }
                }
            }

            private static void RequireListenerFree(LoginServer server)
            {
                Require(!server.IsRunning, "handler-harness-server-running");
                Require(server.ClientCount == 0, "handler-harness-client-count");
                Require(!server.TCPEnabled, "handler-harness-tcp-enabled");
                Require(!server.UDPEnabled, "handler-harness-udp-enabled");
                Require(server.TcpEndPoint == null, "handler-harness-tcp-endpoint");
                Require(server.UdpEndPoint == null, "handler-harness-udp-endpoint");
                Require(
                    GetInheritedField(server.GetType(), "_tcpListen").GetValue(server) == null,
                    "handler-harness-tcp-listener");
                Require(
                    GetInheritedField(server.GetType(), "_udpListen").GetValue(server) == null,
                    "handler-harness-udp-listener");
            }
        }

        private sealed class CaptureClient : Client
        {
            internal CaptureClient(ServerBase server, IMessageSerializer serializer, IBus bus)
                : base(server, serializer, bus)
            {
            }

            public override void Send(byte[] packet, int offset, int length)
            {
            }
        }

        private sealed class RecordingSerializer : IMessageSerializer
        {
            internal readonly List<Message> SerializedMessages = new List<Message>();

            public Message Deserialize(byte[] buffer)
            {
                throw new NotSupportedException("The listener-free Stage 7.1 harness does not deserialize network data.");
            }

            public byte[] Serialize(Message message)
            {
                this.SerializedMessages.Add(message);
                return new byte[] { 0, 0, 0, 0 };
            }
        }

        private sealed class RecordingBus : IBus
        {
            public void Publish(object message)
            {
                throw new NotSupportedException("The listener-free Stage 7.1 harness invokes handlers directly.");
            }

            public IDisposable Subscribe<T>(Action<T> action)
            {
                return new EmptySubscription();
            }
        }

        private sealed class EmptySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private sealed class ParameterValue
        {
            internal ParameterValue(string name, object value)
            {
                this.Name = name;
                this.Value = value;
            }

            internal string Name { get; private set; }

            internal object Value { get; private set; }
        }

    }
}
