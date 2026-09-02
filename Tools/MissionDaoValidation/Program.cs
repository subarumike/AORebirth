namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using AORebirth.Database;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Interfaces.Persistence.Missions;

    using MySqlConnector;

    internal static class Program
    {
        private const string AcknowledgementEnvironment = "AO_REBIRTH_ALLOW_DISPOSABLE_MISSION_DAO_VALIDATION";
        private const string ContainerName = "aorebirth-mission-dao-validation";
        private const string DatabaseName = "aorebirth_mission_dao_validation";
        private const string DatabaseUser = "aorebirth_mission_validation";
        private const string Image = "mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d";
        private const string LabelKey = "org.aorebirth.purpose";
        private const string LabelValue = "mission-dao-disposable";
        private const string NetworkName = "aorebirth_mission_dao_validation_internal";
        private const uint Port = 33069;
        private const string VolumeName = "aorebirth_mission_dao_validation_data";

        private static int checks;

        private static int Main(string[] args)
        {
            if (args.Length != 1 || !string.Equals(args[0], "--run-disposable", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("REFUSED: exact --run-disposable argument required.");
                return 2;
            }

            if (!string.Equals(
                    Environment.GetEnvironmentVariable(AcknowledgementEnvironment),
                    "1",
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "REFUSED: AO_REBIRTH_ALLOW_DISPOSABLE_MISSION_DAO_VALIDATION=1 is required.");
                return 2;
            }

            DisposableMySql disposable = null;
            try
            {
                string root = Directory.GetCurrentDirectory();
                disposable = DisposableMySql.Create();
                using (MySqlConnection connection = WaitForMySql(disposable.RootConnectionString))
                {
                    ApplySchemas(connection, root);
                    SeedCharacters(connection);
                }

                var dao = new MySqlMissionDao(() => Open(disposable.ApplicationConnectionString));
                ValidateFactory();
                ValidateLifecycleAndNulls(dao);
                ValidateRollbackAndOwnership(dao);
                ValidateObservationConcurrency(dao);
                ValidateRewards(dao);
                ValidateRollFeeConcurrency(dao, disposable.RootConnectionString);
                ValidateStartArea(dao);

                Console.WriteLine("MISSION_DAO_MYSQL_INTEGRATION=PASS");
                Console.WriteLine("ROLLBACK_AND_CONCURRENCY_TESTS=PASS");
                Console.WriteLine("MISSION_DAO_CHECKS=" + checks.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("MISSION_DAO_MYSQL_INTEGRATION=FAIL");
                Console.Error.WriteLine("ERROR=" + exception.GetType().Name + ":" + exception.Message);
                return 1;
            }
            finally
            {
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }

        private static void ValidateFactory()
        {
            Require(
                DatabaseDaoFactory.CreateMissionDao() is MySqlMissionDao,
                "factory-mysql-classification");
        }

        private static void ValidateLifecycleAndNulls(IMissionDao dao)
        {
            Require(dao.GetMissions(101).Count == 0, "empty-missions");
            Require(dao.GetMission(new MissionKeyData(101, "missing")) == null, "missing-mission");
            Require(dao.ResolveCharacterAccountKey(101) == "mission_account", "character-ownership-read");

            var mission = new MissionStateData
                          {
                              CharacterId = 101,
                              QuestId = "dao.lifecycle",
                              State = MissionLifecycleState.Active,
                              CurrentStepId = null,
                              OfferedAtUtcTicks = 10,
                              AcceptedAtUtcTicks = 11,
                              CreatedAtUtcTicks = 10,
                              UpdatedAtUtcTicks = 11
                          };
            var objective = new MissionObjectiveProgressData
                            {
                                CharacterId = 101,
                                QuestId = "dao.lifecycle",
                                ObjectiveId = "objective.one",
                                Progress = 0,
                                RequiredCount = 2,
                                LastObservationKey = null,
                                CreatedAtUtcTicks = 10,
                                UpdatedAtUtcTicks = 11
                            };
            var flag = new MissionFlagData
                       {
                           CharacterId = 101,
                           QuestId = "dao.lifecycle",
                           FlagKey = " nullable-key ",
                           Value = null,
                           CreatedAtUtcTicks = 10,
                           UpdatedAtUtcTicks = 11
                       };
            var accountFlag = new MissionAccountFlagData
                              {
                                  AccountKey = "mission_account",
                                  FlagKey = "account-flag",
                                  Value = null,
                                  SourceQuestId = null,
                                  CreatedAtUtcTicks = 10,
                                  UpdatedAtUtcTicks = 11
                              };

            dao.Execute(
                101,
                "mission_account",
                transaction =>
                {
                    transaction.SaveMission(new MissionKeyData(101, "dao.lifecycle"), mission);
                    transaction.SaveObjective(
                        new MissionObjectiveKeyData(
                            new MissionKeyData(101, "dao.lifecycle"),
                            "objective.one"),
                        objective);
                    transaction.SaveFlag(new MissionKeyData(101, "dao.lifecycle"), flag);
                    transaction.SaveAccountFlag("mission_account", accountFlag);
                    return true;
                });

            Require(mission.Version == 1, "mission-insert-version");
            Require(objective.Version == 1, "objective-insert-version");
            Require(flag.Version == 1 && flag.FlagKey == "nullable-key", "flag-normalization");
            Require(accountFlag.Version == 1, "account-flag-version");

            MissionCharacterSnapshotData snapshot = dao.ReadCharacter(101);
            Require(snapshot.Missions.Count == 1, "snapshot-mission-count");
            Require(snapshot.Objectives.Count == 1, "snapshot-objective-count");
            Require(snapshot.Flags.Count == 1 && snapshot.Flags[0].Value == null, "snapshot-null-flag");
            Require(snapshot.Rewards.Count == 0, "snapshot-empty-rewards");
            Require(dao.GetAccountFlag("mission_account", "account-flag").Value == null, "account-null-value");

            mission.CurrentStepId = "step.two";
            mission.UpdatedAtUtcTicks = 12;
            dao.Execute(
                101,
                transaction =>
                {
                    transaction.SaveMission(new MissionKeyData(101, "dao.lifecycle"), mission);
                    return true;
                });
            Require(mission.Version == 2, "mission-update-version");
            Require(dao.GetMission(new MissionKeyData(101, "dao.lifecycle")).CurrentStepId == "step.two", "mission-update");
        }

        private static void ValidateRollbackAndOwnership(IMissionDao dao)
        {
            try
            {
                dao.Execute<int>(
                    101,
                    transaction =>
                    {
                        transaction.SaveMission(
                            new MissionKeyData(101, "dao.rollback"),
                            new MissionStateData
                            {
                                CharacterId = 101,
                                QuestId = "dao.rollback",
                                State = MissionLifecycleState.Active,
                                CreatedAtUtcTicks = 20,
                                UpdatedAtUtcTicks = 20
                            });
                        throw new ExpectedRollbackException();
                    });
                throw new InvalidOperationException("rollback-exception-was-swallowed");
            }
            catch (ExpectedRollbackException)
            {
            }

            Require(dao.GetMission(new MissionKeyData(101, "dao.rollback")) == null, "transaction-rollback");

            bool ownershipRejected = false;
            try
            {
                dao.Execute(101, "wrong_account", transaction => true);
            }
            catch (InvalidOperationException)
            {
                ownershipRejected = true;
            }

            Require(ownershipRejected, "account-ownership-mismatch");
        }

        private static void ValidateObservationConcurrency(IMissionDao dao)
        {
            Func<bool> add = () => dao.Execute(
                101,
                transaction => transaction.TryAddObservation(
                    new MissionObjectiveObservationData
                    {
                        CharacterId = 101,
                        QuestId = "dao.lifecycle",
                        ObjectiveId = "objective.one",
                        ObservationKey = "observation-1",
                        EventType = "Kill",
                        SourceIdentity = null,
                        TargetIdentity = "50000:77",
                        ObservedAtUtcTicks = 30
                    }));

            Task<bool> first = Task.Run(add);
            Task<bool> second = Task.Run(add);
            Task.WaitAll(first, second);
            Require((first.Result ? 1 : 0) + (second.Result ? 1 : 0) == 1, "observation-concurrency");
        }

        private static void ValidateRewards(IMissionDao dao)
        {
            MissionStateData mission = dao.GetMission(new MissionKeyData(101, "dao.lifecycle"));
            mission.State = MissionLifecycleState.Completed;
            mission.CompletedAtUtcTicks = 40;
            mission.UpdatedAtUtcTicks = 40;
            dao.Execute(
                101,
                transaction =>
                {
                    transaction.SaveMission(new MissionKeyData(101, "dao.lifecycle"), mission);
                    return true;
                });

            MissionRewardClaimResultData claim = dao.Execute(
                101,
                transaction => transaction.TryClaimReward(
                    new MissionRewardKeyData(new MissionKeyData(101, "dao.lifecycle"), "item"),
                    "item",
                    "claim-token",
                    41,
                    51));
            Require(claim.Status == MissionRewardClaimStatus.Claimed, "reward-claim");

            bool applied = dao.Execute(
                101,
                transaction =>
                {
                    MissionRewardStageData stage;
                    return transaction.TryMarkRewardApplied(
                        new MissionRewardKeyData(new MissionKeyData(101, "dao.lifecycle"), "item"),
                        "claim-token",
                        claim.Stage.Version,
                        null,
                        42,
                        out stage);
                });
            Require(applied, "reward-applied");

            MissionAtomicStatRewardResultData statReward = dao.Execute(
                101,
                transaction => transaction.TryApplyCharacterStatReward(
                    new MissionRewardKeyData(new MissionKeyData(101, "dao.lifecycle"), "cash-stat"),
                    "cash",
                    new[]
                    {
                        new MissionStatMutationData
                        {
                            StatIdentityType = 50000,
                            StatId = 61,
                            Kind = MissionStatMutationKind.AddClamped,
                            Value = 25,
                            MinimumValue = 0,
                            MaximumValue = 999999999
                        }
                    },
                    null,
                    43));
            Require(statReward.Status == MissionAtomicRewardStatus.Applied, "atomic-stat-reward");
            Require(statReward.StatValues.Single().Value == 25, "atomic-stat-value");
        }

        private static void ValidateRollFeeConcurrency(IMissionDao dao, string rootConnectionString)
        {
            var request = new MissionRollFeeRequest
                          {
                              CharacterType = 50000,
                              CharacterId = 102,
                              BatchIdentity = "batch-double-submit",
                              Fee = 5,
                              AppliedAtUtcTicks = 50
                          };
            Task<MissionRollFeeResult> first = Task.Run(() => dao.TryChargeRollFee(request));
            Task<MissionRollFeeResult> second = Task.Run(() => dao.TryChargeRollFee(request));
            Task.WaitAll(first, second);

            MissionRollFeeStatus[] statuses = { first.Result.Status, second.Result.Status };
            Require(statuses.Count(value => value == MissionRollFeeStatus.Applied) == 1, "roll-fee-applied-once");
            Require(statuses.Count(value => value == MissionRollFeeStatus.AlreadyApplied) == 1, "roll-fee-idempotent");
            using (MySqlConnection connection = Open(rootConnectionString))
            using (MySqlCommand command = new MySqlCommand(
                       "SELECT StatValue FROM stats WHERE Instance=102 AND Type=50000 AND StatId=61",
                       connection))
            {
                Require(Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 95, "roll-fee-cash");
            }
        }

        private static void ValidateStartArea(IMissionDao dao)
        {
            Require(dao.MarkStartAreaSelectionPending(103), "start-area-pending");
            Require(
                dao.GetStartAreaSelectionState(103) == MissionStartAreaSelectionStates.Pending,
                "start-area-read");
            Require(
                dao.TryCompleteStartAreaSelection(103, MissionStartAreaSelectionStates.IccShuttleport),
                "start-area-complete");
            Require(
                dao.GetStartAreaSelectionState(103) == MissionStartAreaSelectionStates.IccShuttleport,
                "start-area-completed-read");
            Require(
                !dao.TryCompleteStartAreaSelection(103, MissionStartAreaSelectionStates.Arete),
                "start-area-conditional-update");
        }

        private static void ApplySchemas(MySqlConnection connection, string root)
        {
            string directory = Path.Combine(
                root,
                "AORebirth",
                "Libraries",
                "Source",
                "AORebirth.Database",
                "SqlTables");
            foreach (string name in new[]
                     {
                         "characters.sql",
                         "stats.sql",
                         "missionstates.sql",
                         "missionobjectiveprogress.sql",
                         "missionobjectiveobservations.sql",
                         "missionflags.sql",
                         "missionaccountflags.sql",
                         "missionrewardledger.sql"
                     })
            {
                using (var command = new MySqlCommand(File.ReadAllText(Path.Combine(directory, name)), connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void SeedCharacters(MySqlConnection connection)
        {
            for (int id = 101; id <= 103; id++)
            {
                using (var command = new MySqlCommand(
                           "INSERT INTO characters "
                           + "(Id, Username, Name, FirstName, LastName, playfield, X, Y, Z, HeadingX, HeadingY, HeadingZ, HeadingW) "
                           + "VALUES (@Id, @Username, @Name, '', '', 1, 0, 0, 0, 0, 0, 0, 1)",
                           connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@Username", "mission_account");
                    command.Parameters.AddWithValue("@Name", "Mission" + id.ToString(CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }
            }

            using (var command = new MySqlCommand(
                       "INSERT INTO stats (Instance, Type, StatId, StatValue) VALUES (102, 50000, 61, 100)",
                       connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static MySqlConnection WaitForMySql(string connectionString)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                try
                {
                    return Open(connectionString);
                }
                catch (Exception exception)
                {
                    last = exception;
                    Thread.Sleep(500);
                }
            }

            throw new InvalidOperationException("disposable-mysql-startup-timeout", last);
        }

        private static MySqlConnection Open(string connectionString)
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static void Require(bool condition, string code)
        {
            checks++;
            if (!condition)
            {
                throw new InvalidOperationException(code);
            }
        }

        private sealed class ExpectedRollbackException : Exception
        {
        }

        private sealed class DisposableMySql : IDisposable
        {
            private string environmentFile;
            private bool containerCreated;
            private bool networkCreated;
            private bool volumeCreated;

            internal string ApplicationConnectionString { get; private set; }
            internal string RootConnectionString { get; private set; }

            internal static DisposableMySql Create()
            {
                var result = new DisposableMySql();
                try
                {
                    RequireDockerResourceAbsent("container", ContainerName);
                    RequireDockerResourceAbsent("network", NetworkName);
                    RequireDockerResourceAbsent("volume", VolumeName);
                    Docker("image", "inspect", Image);
                    RequirePortAvailable();

                    string rootPassword = RandomSecret();
                    string applicationPassword = RandomSecret();
                    result.environmentFile = Path.Combine(
                        Path.GetTempPath(),
                        "aorebirth-mission-dao-" + Guid.NewGuid().ToString("N") + ".env");
                    File.WriteAllLines(
                        result.environmentFile,
                        new[]
                        {
                            "MYSQL_ROOT_PASSWORD=" + rootPassword,
                            "MYSQL_DATABASE=" + DatabaseName,
                            "MYSQL_USER=" + DatabaseUser,
                            "MYSQL_PASSWORD=" + applicationPassword
                        });
                    File.SetAttributes(result.environmentFile, FileAttributes.Hidden | FileAttributes.Temporary);

                    Docker("network", "create", "--label", LabelKey + "=" + LabelValue, NetworkName);
                    result.networkCreated = true;
                    Docker("volume", "create", "--label", LabelKey + "=" + LabelValue, VolumeName);
                    result.volumeCreated = true;
                    Docker(
                        "run",
                        "--detach",
                        "--name",
                        ContainerName,
                        "--label",
                        LabelKey + "=" + LabelValue,
                        "--restart",
                        "no",
                        "--network",
                        NetworkName,
                        "--publish",
                        "127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + ":3306",
                        "--env-file",
                        result.environmentFile,
                        "--volume",
                        VolumeName + ":/var/lib/mysql",
                        Image);
                    result.containerCreated = true;

                    result.RootConnectionString = ConnectionString("root", rootPassword);
                    result.ApplicationConnectionString = ConnectionString(DatabaseUser, applicationPassword);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    if (this.containerCreated && HasExpectedLabel("container", ContainerName))
                    {
                        Docker("rm", "--force", ContainerName);
                    }

                    if (this.volumeCreated && HasExpectedLabel("volume", VolumeName))
                    {
                        Docker("volume", "rm", VolumeName);
                    }

                    if (this.networkCreated && HasExpectedLabel("network", NetworkName))
                    {
                        Docker("network", "rm", NetworkName);
                    }
                }
                finally
                {
                    if (!string.IsNullOrEmpty(this.environmentFile) && File.Exists(this.environmentFile))
                    {
                        File.SetAttributes(this.environmentFile, FileAttributes.Normal);
                        File.Delete(this.environmentFile);
                    }
                }
            }

            private static string ConnectionString(string user, string password)
            {
                return new MySqlConnectionStringBuilder
                       {
                           Server = IPAddress.Loopback.ToString(),
                           Port = Port,
                           Database = DatabaseName,
                           UserID = user,
                           Password = password,
                           SslMode = MySqlSslMode.None,
                           ConnectionTimeout = 3,
                           DefaultCommandTimeout = 30,
                           AllowUserVariables = true
                       }.ConnectionString;
            }
        }

        private static void RequirePortAvailable()
        {
            var listener = new TcpListener(IPAddress.Loopback, checked((int)Port));
            try
            {
                listener.Start();
            }
            finally
            {
                listener.Stop();
            }
        }

        private static string RandomSecret()
        {
            byte[] bytes = new byte[36];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static void RequireDockerResourceAbsent(string resource, string name)
        {
            ProcessResult result = DockerResult(resource, "inspect", name);
            if (result.ExitCode == 0)
            {
                throw new InvalidOperationException("disposable-docker-resource-already-exists-" + name);
            }
        }

        private static bool HasExpectedLabel(string resource, string name)
        {
            string format = resource == "container"
                ? "{{index .Config.Labels \"" + LabelKey + "\"}}"
                : "{{index .Labels \"" + LabelKey + "\"}}";
            ProcessResult result = DockerResult(resource, "inspect", "--format", format, name);
            return result.ExitCode == 0
                   && string.Equals(result.Output.Trim(), LabelValue, StringComparison.Ordinal);
        }

        private static void Docker(params string[] arguments)
        {
            ProcessResult result = DockerResult(arguments);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("docker-command-failed");
            }
        }

        private static ProcessResult DockerResult(params string[] arguments)
        {
            var start = new ProcessStartInfo("docker")
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using (Process process = Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult { ExitCode = process.ExitCode, Output = output };
            }
        }

        private sealed class ProcessResult
        {
            internal int ExitCode { get; set; }
            internal string Output { get; set; }
        }
    }
}
