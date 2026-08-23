using System;
using System.Collections.Generic;
using System.IO;

using AORebirth.Database.Dao;
using LoginEngine.CoreClient;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class LoginHandoffLifecycleTests
    {
        public static void Run()
        {
            MarkWithoutHandoffDisconnectClears();
            DisconnectBeforeAcceptanceClears();
            ExplicitHandoffFailureClears();
            HandoffTimeoutClears();
            ZoneAcceptancePreventsClear();
            DisconnectAfterAcceptancePreservesOnline();
            AcceptanceDisconnectRaceHasDeterministicWinners();
            CleanupTwiceIsIdempotent();
            UnrelatedRowsAreUntouched();
            ExceptionBeforeHandoffClears();
            DatabaseCleanupFailureIsVisible();
            NormalHandoffPathEndsOnline();
            ConfiguredOwnershipDirectoryFailureIsVisible();
            Console.WriteLine("PASS: LoginEngine pre-handoff Online lifecycle tests");
        }

        private static void ConfiguredOwnershipDirectoryFailureIsVisible()
        {
            const string variable = "AO_REBIRTH_SESSION_OWNERSHIP_DIR";
            string original = Environment.GetEnvironmentVariable(variable);
            string unusablePath = Path.GetTempFileName();
            bool threw = false;
            try
            {
                Environment.SetEnvironmentVariable(variable, unusablePath);
                CharacterOnlineOwnershipGuard.TryClearLoginOwnership(14);
            }
            catch (IOException)
            {
                threw = true;
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, original);
                File.Delete(unusablePath);
            }

            Require(threw, "configured ownership directory failure fell back or was hidden");
        }

        private static void MarkWithoutHandoffDisconnectClears()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(1);
            lifecycle.CleanupLoginOwnership("disconnect");
            Require(!store.IsOnline(1), "disconnect before handoff left Online set");
        }

        private static void DisconnectBeforeAcceptanceClears()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(2);
            lifecycle.StartHandoff();
            lifecycle.CleanupLoginOwnership("disconnect-before-accept");
            Require(!store.IsOnline(2), "disconnect before Zone acceptance left Online set");
        }

        private static void ExplicitHandoffFailureClears()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(3);
            lifecycle.StartHandoff();
            lifecycle.CleanupLoginOwnership("handoff-failure");
            Require(!store.IsOnline(3), "explicit handoff failure left Online set");
        }

        private static void HandoffTimeoutClears()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(4);
            lifecycle.StartHandoff();
            lifecycle.CleanupLoginOwnership("handoff-timeout");
            Require(!store.IsOnline(4), "handoff timeout left Online set");
        }

        private static void ZoneAcceptancePreventsClear()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(5);
            lifecycle.StartHandoff();
            store.ZoneOwns = true;
            lifecycle.CleanupLoginOwnership("disconnect");
            Require(store.IsOnline(5), "Zone-owned character was cleared");
            Require(lifecycle.State == LoginHandoffState.ZoneAccepted, "Zone ownership was not recorded");
        }

        private static void DisconnectAfterAcceptancePreservesOnline()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(6);
            lifecycle.StartHandoff();
            lifecycle.RecordZoneAccepted("zone-boundary");
            lifecycle.CleanupLoginOwnership("post-accept-disconnect");
            Require(store.IsOnline(6), "post-acceptance disconnect cleared Online");
            Require(store.ClearCalls == 0, "post-acceptance disconnect called cleanup DAO");
        }

        private static void AcceptanceDisconnectRaceHasDeterministicWinners()
        {
            FakeStore acceptanceStore;
            LoginHandoffLifecycle acceptanceFirst = Create(out acceptanceStore);
            acceptanceFirst.MarkOnline(7);
            acceptanceFirst.StartHandoff();
            acceptanceStore.AcceptZone(7);
            acceptanceFirst.RecordZoneAccepted("acceptance-won");
            acceptanceFirst.CleanupLoginOwnership("disconnect-lost");
            Require(acceptanceStore.IsOnline(7), "acceptance-winning race ended offline");

            FakeStore cleanupStore;
            LoginHandoffLifecycle cleanupFirst = Create(out cleanupStore);
            cleanupFirst.MarkOnline(8);
            cleanupFirst.StartHandoff();
            cleanupFirst.CleanupLoginOwnership("disconnect-won");
            cleanupStore.AcceptZone(8);
            cleanupFirst.RecordZoneAccepted("acceptance-after-cleanup");
            Require(cleanupStore.IsOnline(8), "cleanup-winning race was not reasserted by Zone acceptance");
        }

        private static void CleanupTwiceIsIdempotent()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(9);
            lifecycle.CleanupLoginOwnership("first");
            lifecycle.CleanupLoginOwnership("second");
            Require(store.ClearCalls == 1, "cleanup was not exactly-once/idempotent");
        }

        private static void UnrelatedRowsAreUntouched()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            store.SetOnline(100);
            lifecycle.MarkOnline(10);
            lifecycle.CleanupLoginOwnership("disconnect");
            Require(store.IsOnline(100), "cleanup touched an unrelated character row");
        }

        private static void ExceptionBeforeHandoffClears()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(11);
            lifecycle.CleanupLoginOwnership("pre-handoff-exception");
            Require(!store.IsOnline(11), "pre-handoff exception left Online set");
        }

        private static void DatabaseCleanupFailureIsVisible()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(12);
            store.ClearThrows = true;
            bool threw = false;
            try
            {
                lifecycle.CleanupLoginOwnership("database-failure");
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Require(threw, "database cleanup failure was hidden");
            Require(store.IsOnline(12), "failed database cleanup was falsely reported as repaired");
            Require(lifecycle.State != LoginHandoffState.CleanupCompleted, "failed cleanup advanced lifecycle state");
        }

        private static void NormalHandoffPathEndsOnline()
        {
            FakeStore store;
            LoginHandoffLifecycle lifecycle = Create(out store);
            lifecycle.MarkOnline(13);
            lifecycle.StartHandoff();
            lifecycle.CleanupLoginOwnership("normal-login-socket-close");
            store.AcceptZone(13);
            lifecycle.RecordZoneAccepted("zone-acceptance");
            Require(store.IsOnline(13), "normal Login-to-Zone handoff ended offline");
        }

        private static LoginHandoffLifecycle Create(out FakeStore store)
        {
            store = new FakeStore();
            return new LoginHandoffLifecycle(store, delegate { });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeStore : ILoginHandoffOnlineStore
        {
            private readonly Dictionary<int, bool> online = new Dictionary<int, bool>();

            public bool ZoneOwns { get; set; }

            public bool ClearThrows { get; set; }

            public int ClearCalls { get; private set; }

            public void SetOnline(int characterId)
            {
                this.online[characterId] = true;
            }

            public LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId)
            {
                this.ClearCalls++;
                if (this.ClearThrows)
                {
                    throw new InvalidOperationException("database cleanup failed");
                }

                if (this.ZoneOwns)
                {
                    return LoginOwnedOnlineCleanupResult.ZoneOwned;
                }

                this.online[characterId] = false;
                return LoginOwnedOnlineCleanupResult.Cleared;
            }

            public void AcceptZone(int characterId)
            {
                this.ZoneOwns = true;
                this.online[characterId] = true;
            }

            public bool IsOnline(int characterId)
            {
                bool value;
                return this.online.TryGetValue(characterId, out value) && value;
            }
        }
    }
}
