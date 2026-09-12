namespace AORebirth.Tools.CharacterDaoValidation
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.Characters;
    using ChatEngine.CoreServer;

    internal static partial class Program
    {
        private static void RecoveryOwnershipChecks()
        {
            string previous = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
            string directory = Path.Combine(Path.GetTempPath(), "aorebirth-character-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", directory);
            try
            {
                Reset(); Seed(101, "owner", "Live", 1);
                string before = RecoverySnapshot();
                using (CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => { }))
                {
                    var observed = new ObservedConnection(application);
                    Expect<InvalidOperationException>(() => new MySqlCharacterDao(() => observed).RecoverStaleOnline(DatabaseName),
                        "recovery-refuses-in-process-zone-owner");
                    Require(observed.CommitCount == 0 && observed.RollbackCount == 1
                        && observed.Commands.All(x => !x.Sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)),
                        "live-owner-refusal-before-write-with-rollback");
                    Require(RecoverySnapshot() == before, "live-owner-refusal-preserves-all-character-fields");
                }

                Seed(102, "owner", "External", -1);
                before = RecoverySnapshot();
                using (Process external = StartOwnershipChild("hold", 102))
                {
                    try
                    {
                        Task<string> ready = external.StandardOutput.ReadLineAsync();
                        Require(ready.Wait(TimeSpan.FromSeconds(10)) && ready.Result == "OWNERSHIP_READY", "cross-process-owner-ready");
                        Expect<InvalidOperationException>(() => Dao().RecoverStaleOnline(DatabaseName), "recovery-refuses-cross-process-owner");
                        Require(RecoverySnapshot() == before, "cross-process-owner-refusal-preserves-all-rows");
                        Require(OwnershipAttempt(101) == 0, "partial-recovery-leases-released-after-refusal");
                    }
                    finally { StopOwnershipChild(external); }
                }

                var fenced = new ObservedConnection(application);
                fenced.BeforeCommit = () => Require(OwnershipAttempt(101) == 3, "ownership-fenced-before-database-commit");
                fenced.AfterCommit = () => Require(OwnershipAttempt(102) == 3, "ownership-fenced-after-durable-commit-before-return");
                var recovered = new MySqlCharacterDao(() => fenced).RecoverStaleOnline(DatabaseName);
                Require(recovered.RowsUpdated == 2 && recovered.PostUpdateNonzeroCount == 0, "stale-unowned-rows-recover-atomically");
                Require(OwnershipAttempt(101) == 0 && OwnershipAttempt(102) == 0, "all-recovery-leases-released-after-return");

                Seed(103, "owner", "Rollback", 1);
                var failing = new ObservedConnection(application) { FailurePoint = "commit-before" };
                Expect<InjectedFailure>(() => new MySqlCharacterDao(() => failing).RecoverStaleOnline(DatabaseName), "guarded-commit-failure-propagates");
                Require(Dao().LoadById(103).Online == 1 && OwnershipAttempt(103) == 0,
                    "guarded-rollback-preserves-online-and-releases-lease");

                using (var entered = new ManualResetEventSlim())
                using (var release = new ManualResetEventSlim())
                {
                    Task owner = Task.Run(() =>
                    {
                        using (CharacterOnlineOwnershipGuard.AcquireZoneOwnership(104, _ => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); })) { }
                    });
                    Require(entered.Wait(TimeSpan.FromSeconds(10)), "competing-ownership-critical-section-entered");
                    try
                    {
                        var timer = Stopwatch.StartNew();
                        Expect<InvalidOperationException>(() => Dao().RecoverStaleOnline(DatabaseName), "recovery-refuses-busy-ownership-monitor");
                        Require(timer.Elapsed < TimeSpan.FromSeconds(3), "recovery-does-not-wait-with-database-row-locks");
                    }
                    finally { release.Set(); owner.GetAwaiter().GetResult(); }
                }
                Require(Dao().LoadById(103).Online == 1, "ownership-contention-preserves-online");
                BotRecoveryOwnershipChecks();
            }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", previous);
                Directory.Delete(directory, true);
            }
        }

        private static string RecoverySnapshot()
        {
            return UnrelatedSnapshot() + "\n" + Normalize(Dao().ListForAccount("owner"));
        }

        private static void BotRecoveryOwnershipChecks()
        {
            Reset(); Seed(105, "owner", "ChatBot", 0);
            var owners = new ChatSessionOwnership<object>();
            var original = new object();
            var replacement = new object();
            Func<IDisposable> acquire = () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(105, id => Dao().MarkOnline(id));
            Action<uint> clear = id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, value => Dao().MarkOffline(value));
            try
            {
                Require(owners.Register(105, original, acquire), "bot-online-owner-registers");
                string before = RecoverySnapshot();
                Expect<InvalidOperationException>(() => Dao().RecoverStaleOnline(DatabaseName), "recovery-refuses-live-chat-only-bot");
                Require(RecoverySnapshot() == before && Dao().LoadById(105).Online == 1, "recovery-preserves-live-bot-row");
                Require(OwnershipAttempt(105) == 3, "bot-lease-protects-against-other-process-recovery");
                Require(owners.Register(105, replacement, acquire), "bot-replacement-registers");
                Require(!owners.Disconnect(105, original, clear, _ => { }), "old-bot-disconnect-cannot-clear-replacement");
                Expect<InvalidOperationException>(() => Dao().RecoverStaleOnline(DatabaseName), "recovery-refuses-replacement-bot");
                Require(Dao().LoadById(105).Online == 1, "replacement-bot-remains-online");
                Require(owners.Disconnect(105, replacement, clear, _ => { }), "current-bot-disconnect-cleans-up");
                Require(Dao().LoadById(105).Online == 0 && OwnershipAttempt(105) == 0, "bot-disconnect-clears-online-and-releases-lease");
                Dao().MarkOnline(105);
                var recovered = Dao().RecoverStaleOnline(DatabaseName);
                Require(recovered.RowsUpdated == 1 && recovered.PostUpdateNonzeroCount == 0,
                    "disconnected-unowned-bot-row-can-be-recovered");
            }
            finally
            {
                owners.Disconnect(105, replacement, clear, _ => { });
                owners.Disconnect(105, original, clear, _ => { });
            }
        }

        private static int RunOwnershipChild(string[] args)
        {
            if (Environment.GetEnvironmentVariable("AO_REBIRTH_CHARACTER_OWNERSHIP_TEST_CHILD") != "1"
                || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                return 2;
            try
            {
                if (args[1] == "hold")
                {
                    using (CharacterOnlineOwnershipGuard.AcquireZoneOwnership(id, _ => { }))
                    {
                        Console.WriteLine("OWNERSHIP_READY"); Console.Out.Flush(); Console.ReadLine();
                    }
                    return 0;
                }
                if (args[1] != "try") return 2;
                using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { id })) { }
                return 0;
            }
            catch (InvalidOperationException) { return 3; }
        }

        private static Process StartOwnershipChild(string mode, int id)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--ownership-child"); start.ArgumentList.Add(mode);
            start.ArgumentList.Add(id.ToString(CultureInfo.InvariantCulture));
            start.Environment["AO_REBIRTH_CHARACTER_OWNERSHIP_TEST_CHILD"] = "1";
            return Process.Start(start);
        }

        private static int OwnershipAttempt(int id)
        {
            using (Process child = StartOwnershipChild("try", id))
            {
                if (!child.WaitForExit(10000)) { child.Kill(true); child.WaitForExit(); throw new CheckFailure("ownership-child-timeout"); }
                return child.ExitCode;
            }
        }

        private static void StopOwnershipChild(Process child)
        {
            if (!child.HasExited) { child.StandardInput.WriteLine("release"); child.StandardInput.Flush(); }
            if (!child.WaitForExit(10000)) { child.Kill(true); child.WaitForExit(); throw new CheckFailure("ownership-holder-timeout"); }
            Require(child.ExitCode == 0, "cross-process-owner-clean-exit");
        }
    }
}
