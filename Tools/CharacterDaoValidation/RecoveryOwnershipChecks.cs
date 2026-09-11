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
