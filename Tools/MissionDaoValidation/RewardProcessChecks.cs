namespace AORebirth.Tools.MissionDaoValidation
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using AORebirth.Database.Domain.Missions;
    using AORebirth.Database.Domain.Quests;
    using AORebirth.Interfaces.Persistence.Missions;
    using MySqlConnector;
    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static partial class Program
    {
        private const string RewardWorkerReady = "REWARD_WORKER_READY";

        private static void ValidateRewardProcessRecovery(MySqlMissionDao dao, string connectionString)
        {
            var quests = new MySqlCharacterQuestDao(() => new MySqlConnection(connectionString));
            string password = new MySqlConnectionStringBuilder(connectionString).Password;
            int index = 0;
            foreach (string point in new[] { "before-transaction", "pending", "before-commit", "after-commit" })
            {
                string quest = "crash-reward." + point;
                int item = 921000 + index;
                int slot = 84 + index++;
                SeedCompletionQuest(quests, quest);
                ResetCompletionStats(connectionString);
                if (point == "before-commit" || point == "after-commit")
                {
                    dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                        .TryEarnCharacterQuest(EarnRequest(quest)));
                }

                Require(RunRewardProcess(point, quest, item, slot, password, terminate: true) == RewardWorkerReady,
                    "reward-worker-terminated-" + point);
                bool applied = point == "after-commit";
                Require(ReadCompletionStat(connectionString, CharacterStat.Cash) == (applied ? 117 : 100)
                    && ReadCompletionStat(connectionString, CharacterStat.XP) == (applied ? 223 : 200)
                    && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + item) == (applied ? 1 : 0),
                    "reward-process-authoritative-state-" + point);
                string result = RunRewardProcess("recover", quest, item, slot, password, terminate: false);
                Require(result == (applied ? "AlreadyApplied" : "Applied"),
                    "reward-fresh-process-authoritative-recovery-" + point);
                Require(RunRewardProcess("recover", quest, item, slot, password, terminate: false) == "AlreadyApplied"
                    && ReadCompletionStat(connectionString, CharacterStat.Cash) == 117
                    && ReadCompletionStat(connectionString, CharacterStat.XP) == 223
                    && Scalar(connectionString, "SELECT COUNT(*) FROM item_instances WHERE InstanceId=" + item) == 1,
                    "reward-fresh-process-no-duplicate-" + point);
            }
            MeasureOrdinaryRewardCompletion(dao, quests, connectionString);
        }

        private static void MeasureOrdinaryRewardCompletion(MySqlMissionDao dao, MySqlCharacterQuestDao quests, string connectionString)
        {
            // Warm the same command shapes before collecting measurements.
            TimeSpan elapsed = TimeSpan.Zero;
            TimeSpan cpu = TimeSpan.Zero;
            long allocations = 0;
            int commands = 0;
            const int iterations = 12;
            using (Process process = Process.GetCurrentProcess())
            {
                for (int i = -2; i < iterations; i++)
                {
                    string quest = "completion.measure-" + (i + 2);
                    SeedCompletionQuest(quests, quest);
                    ResetCompletionStats(connectionString);
                    dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                        .TryEarnCharacterQuest(EarnRequest(quest)));
                    var request = CompleteRequest(quest);
                    var measuredConnection = new FaultConnection(connectionString);
                    var measuredDao = new MySqlMissionDao(() => measuredConnection);
                    process.Refresh();
                    TimeSpan startCpu = process.TotalProcessorTime;
                    long startAllocations = GC.GetAllocatedBytesForCurrentThread();
                    long start = Stopwatch.GetTimestamp();
                    var result = measuredDao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                        .TryCompleteCharacterQuest(request));
                    TimeSpan elapsedIteration = Stopwatch.GetElapsedTime(start);
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - startAllocations;
                    process.Refresh();
                    if (i >= 0)
                    {
                        elapsed += elapsedIteration;
                        allocations += allocated;
                        cpu += process.TotalProcessorTime - startCpu;
                        commands += measuredConnection.CommandCount;
                    }
                    Require(result.Status == CharacterQuestCompletionStatus.Applied && measuredConnection.LastTransaction != null,
                        "reward-completion-measure-" + (i + 2));
                }
            }
            Console.WriteLine(FormattableString.Invariant(
                $"REWARD_DAO_MEASUREMENT operations={iterations} commands={commands} transactions={iterations} elapsedMs={elapsed.TotalMilliseconds:F3} processCpuMs={cpu.TotalMilliseconds:F3} callingThreadBytes={allocations}"));
        }

        private static string RunRewardProcess(string point, string quest, int item, int slot, string password, bool terminate)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true
            };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add("--run-disposable");
            start.ArgumentList.Add("--reward-worker");
            start.ArgumentList.Add(point);

            using (var child = Process.Start(start))
            {
                try
                {
                    child.StandardInput.WriteLine(password);
                    child.StandardInput.WriteLine(quest);
                    child.StandardInput.WriteLine(item);
                    child.StandardInput.WriteLine(slot);
                    child.StandardInput.Flush();
                    string result = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                    if (terminate)
                    {
                        if (result != RewardWorkerReady)
                            throw new InvalidOperationException("Reward worker did not reach " + point + ".");
                        child.Kill(entireProcessTree: true);
                    }
                    if (!child.WaitForExit(10000) || (!terminate && child.ExitCode != 0))
                        throw new InvalidOperationException("Reward worker failed its bounded process operation.");
                    return result;
                }
                finally
                {
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        child.WaitForExit(10000);
                    }
                }
            }
        }

        private static int RunRewardWorker(string point)
        {
            try
            {
                if (!Console.IsInputRedirected || !Console.IsOutputRedirected
                    || !HasExpectedLabel("container", ContainerName))
                    return 2;
                string password = Console.ReadLine();
                string quest = Console.ReadLine();
                int item = int.Parse(Console.ReadLine());
                int slot = int.Parse(Console.ReadLine());
                bool followUp = point.StartsWith("follow-up-", StringComparison.Ordinal);
                string expectedPrefix = followUp ? "follow-up.process-" : "crash-reward.";
                if (string.IsNullOrEmpty(password) || !quest.StartsWith(expectedPrefix, StringComparison.Ordinal))
                    return 2;
                var connection = new MySqlConnectionStringBuilder
                {
                    Server = IPAddress.Loopback.ToString(), Port = Port, Database = DatabaseName,
                    UserID = DatabaseUser, Password = password, ConnectionTimeout = 3,
                    DefaultCommandTimeout = 15, SslMode = MySqlSslMode.None, AllowUserVariables = true
                };
                var dao = new MySqlMissionDao(() => Open(connection.ConnectionString));
                if (followUp)
                    return RunQuestFollowUpWorker(dao, point, quest, item, slot);

                if (point == "before-transaction")
                    ParkRewardWorker();
                if (point == "pending" || point == "recover")
                {
                    var earned = dao.Execute(CompletionOwner, tx => ((ICharacterQuestCompletionTransaction)tx)
                        .TryEarnCharacterQuest(EarnRequest(quest)));
                    if (earned.Status != CharacterQuestCompletionStatus.Earned
                        && earned.Status != CharacterQuestCompletionStatus.AlreadyEarned
                        && earned.Status != CharacterQuestCompletionStatus.AlreadyApplied)
                        return 1;
                    if (point == "pending")
                        ParkRewardWorker();
                }
                if (point != "before-commit" && point != "after-commit" && point != "recover")
                    return 2;
                var completed = dao.Execute(CompletionOwner, tx =>
                {
                    var result = ((ICharacterQuestCompletionTransaction)tx)
                        .TryCompleteCharacterQuest(CompleteRequest(quest, CompletionItem(item, slot)));
                    if (point == "before-commit" && result.Status == CharacterQuestCompletionStatus.Applied)
                        ParkRewardWorker();
                    return result;
                });
                if (point == "after-commit" && completed.Status == CharacterQuestCompletionStatus.Applied)
                    ParkRewardWorker();
                Console.WriteLine(completed.Status);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("REWARD_WORKER_FAILED=" + point + ":" + exception.GetType().Name
                    + (exception is MySqlException provider ? ":" + provider.Number : string.Empty));
                return 1;
            }
        }

        private static void ParkRewardWorker()
        {
            Console.WriteLine(RewardWorkerReady);
            Console.Out.Flush();
            // Keep the actual application process and transaction alive until the test parent kills it.
            Thread.Sleep(TimeSpan.FromSeconds(35));
            throw new TimeoutException("The validation parent did not terminate the worker.");
        }
    }
}
