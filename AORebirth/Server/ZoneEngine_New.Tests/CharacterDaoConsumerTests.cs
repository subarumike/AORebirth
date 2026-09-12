using System;
using System.Collections.Generic;
using System.IO;
using AORebirth.Database.Dao;
using AORebirth.Interfaces.Persistence.Characters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;

namespace ZoneEngine_New.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CharacterDaoConsumerTests
{
    [TestMethod]
    public void AdmissionUsesCurrentDirectoryOwnerAndRetainsReplayProtection()
    {
        WithIsolatedConfiguration(() =>
        {
            var directory = new DirectoryFake();
            var gate = new ZoneAdmissionGate(directory);
            var store = ZoneHandoffStore.Configured();
            var ticket = store.Issue("accountA", store.BeginLogin("accountA"), 101);
            var message = new ZoneLoginMessage { CharacterId = 101, Cookie1 = ticket.Cookie1, Cookie2 = ticket.Cookie2 };
            directory.Owner = "accountB";
            Assert.IsFalse(gate.Claim(message).Accepted);
            directory.Missing = true;
            Assert.IsFalse(gate.Claim(message).Accepted);
            directory.Missing = false;
            directory.Owner = "accountA";
            Assert.IsTrue(gate.Claim(message).Accepted);
            Assert.AreEqual("already_claimed", gate.Claim(message).Reason);
            Assert.AreEqual(101, directory.LastLookup);
        });
    }

    [TestMethod]
    public void DirectoryFailureCannotAdmitOrConsumeValidHandoff()
    {
        WithIsolatedConfiguration(() =>
        {
            var directory = new DirectoryFake { FailLookup = true };
            var gate = new ZoneAdmissionGate(directory);
            var store = ZoneHandoffStore.Configured();
            var ticket = store.Issue("accountA", store.BeginLogin("accountA"), 101);
            var message = new ZoneLoginMessage { CharacterId = 101, Cookie1 = ticket.Cookie1, Cookie2 = ticket.Cookie2 };
            Assert.ThrowsExactly<IOException>(() => gate.Claim(message));
            directory.FailLookup = false;
            Assert.IsTrue(gate.Claim(message).Accepted);
        });
    }

    [TestMethod]
    public void OnlineAdapterPreservesOwnershipOrderingAndAffectedRowFailure()
    {
        WithIsolatedConfiguration(() =>
        {
            var directory = new DirectoryFake();
            var repository = new MySqlCharacterRepository(new QuietLogger(), directory);
            using (CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, repository.SetOnline))
            {
                Assert.AreEqual(1, directory.Online);
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.ZoneOwned,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(101, repository.SetOffline));
                Assert.AreEqual(0, directory.OfflineCalls);
            }
            Assert.AreEqual(LoginOwnedOnlineCleanupResult.Cleared,
                CharacterOnlineOwnershipGuard.TryClearLoginOwnership(101, repository.SetOffline));
            Assert.AreEqual(0, directory.Online);
            Assert.AreEqual(1, directory.OnlineCalls);
            Assert.AreEqual(1, directory.OfflineCalls);
            directory.Affected = 0;
            Assert.ThrowsExactly<InvalidOperationException>(() => repository.SetOnline(101));
            Assert.ThrowsExactly<InvalidOperationException>(() => repository.SetOffline(101));
        });
    }

    private static void WithIsolatedConfiguration(Action action)
    {
        string root = Path.Combine(Path.GetTempPath(), "aorebirth-dao-consumer-" + Guid.NewGuid().ToString("N"));
        string? priorOwnership = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
        string? priorConnection = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
        Directory.CreateDirectory(root);
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", root);
            // The injected DAO is the only online-state persistence path; no connection is opened.
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", "Server=127.0.0.1;Database=dao_consumer_unused;User ID=unused");
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", priorOwnership);
            Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", priorConnection);
            Directory.Delete(root, true);
        }
    }

    private sealed class DirectoryFake : ICharacterDao
    {
        internal string Owner = "accountA";
        internal bool Missing, FailLookup;
        internal int LastLookup, Online, OnlineCalls, OfflineCalls;
        internal int Affected = 1;
        public CharacterDirectoryData LoadById(int id)
        {
            LastLookup = id;
            if (FailLookup) throw new IOException("Injected directory read failure.");
            return Missing ? null! : new CharacterDirectoryData { CharacterId = id, AccountUsername = Owner };
        }
        public int MarkOnline(int id) { OnlineCalls++; Online = 1; return Affected; }
        public int MarkOffline(int id) { OfflineCalls++; Online = 0; return Affected; }
        public CharacterDirectoryData LoadByName(string name) => throw new NotSupportedException();
        public IList<CharacterDirectoryData> ListForAccount(string account) => throw new NotSupportedException();
        public IList<CharacterDirectoryData> ListLoggedIn() => throw new NotSupportedException();
        public bool IsOwnedByAccount(string account, uint id) => throw new NotSupportedException();
        public StaleOnlineRecoveryData RecoverStaleOnline(string expectedDatabase) => throw new NotSupportedException();
    }

    private sealed class QuietLogger : IZoneLogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Error(Exception exception, string message) { }
        public IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }
}
