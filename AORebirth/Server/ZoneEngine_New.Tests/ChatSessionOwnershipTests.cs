using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AORebirth.Database.Dao;
using ChatEngine.CoreServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZoneEngine_New.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ChatSessionOwnershipTests
{
    [TestMethod]
    public void OldDisconnectCannotClearOrRemoveReplacement()
    {
        var owners = new ChatSessionOwnership<object>();
        var oldClient = new object();
        var newClient = new object();
        int online = 0, cleared = 0, removed = 0;
        Assert.IsTrue(owners.Register(101, oldClient, () => { online = 1; return new EmptyLease(); }));
        Assert.IsTrue(owners.Register(101, newClient, () => { online = 1; return new EmptyLease(); }));
        Assert.IsFalse(owners.Disconnect(101, oldClient, _ => { online = 0; cleared++; }, _ => removed++));
        Assert.AreSame(newClient, owners.Clients[101]);
        Assert.AreEqual(1, online);
        Assert.AreEqual(0, cleared);
        Assert.AreEqual(0, removed);
        Assert.IsFalse(owners.Register(101, oldClient, () => throw new Exception("Obsolete client marked online.")));
    }

    [TestMethod]
    public void CurrentChatDisconnectPreservesZoneOwnedOnlineState()
    {
        WithOwnershipDirectory(() =>
        {
            var owners = new ChatSessionOwnership<object>();
            var client = new object();
            int online = 0, removed = 0;
            owners.Register(101, client, null!);
            using (CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => online = 1))
            {
                Assert.IsTrue(owners.Disconnect(101, client,
                    id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, _ => online = 0),
                    _ => removed++));
                Assert.AreEqual(1, online);
                Assert.AreEqual(1, removed);
                Assert.AreEqual(0, owners.Clients.Count);
            }
        });
    }

    [TestMethod]
    public void RegistrationWaitsUntilOldOfflineWriteCompletes()
    {
        var owners = new ChatSessionOwnership<object>();
        var oldClient = new object();
        var newClient = new object();
        using var clearing = new ManualResetEventSlim();
        using var releaseClear = new ManualResetEventSlim();
        using var registrationStarted = new ManualResetEventSlim();
        int online = 0;
        owners.Register(101, oldClient, () => { online = 1; return new EmptyLease(); });
        Task disconnect = Task.Run(() => owners.Disconnect(101, oldClient, _ =>
        {
            clearing.Set();
            if (!releaseClear.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            online = 0;
        }, _ => { }));
        Task? register = null;
        try
        {
            Assert.IsTrue(clearing.Wait(TimeSpan.FromSeconds(5)));
            register = Task.Run(() =>
            {
                registrationStarted.Set();
                Assert.IsTrue(owners.Register(101, newClient, () => { online = 1; return new EmptyLease(); }));
            });
            Assert.IsTrue(registrationStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsFalse(register.Wait(100), "Registration must not race the old offline write.");
        }
        finally { releaseClear.Set(); }
        Assert.IsTrue(Task.WaitAll(new[] { disconnect, register! }, TimeSpan.FromSeconds(5)));
        Assert.AreSame(newClient, owners.Clients[101]);
        Assert.AreEqual(1, online);
    }

    [TestMethod]
    public void DisconnectedClientCannotRegisterAfterCallback()
    {
        var owners = new ChatSessionOwnership<object>();
        var client = new object();
        Assert.IsFalse(owners.Disconnect(0, client, _ => Assert.Fail(), _ => Assert.Fail()));
        Assert.IsFalse(owners.Register(101, client, () => throw new Exception("Disconnected client acquired ownership.")));
        Assert.AreEqual(0, owners.Clients.Count);
    }

    [TestMethod]
    public void BotReplacementKeepsOwnershipUntilCurrentDisconnect()
    {
        WithOwnershipDirectory(() =>
        {
            var owners = new ChatSessionOwnership<object>();
            var oldClient = new object();
            var current = new object();
            int online = 0;
            Func<IDisposable> acquire = () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => online = 1);
            Action<uint> clear = id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, _ => online = 0);
            try
            {
                Assert.IsTrue(owners.Register(101, oldClient, acquire));
                Assert.ThrowsException<InvalidOperationException>(() => CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 }));
                Assert.IsTrue(owners.Register(101, current, acquire));
                Assert.IsFalse(owners.Disconnect(101, oldClient, _ => Assert.Fail(), _ => Assert.Fail()));
                Assert.AreEqual(1, online);
                Assert.ThrowsException<InvalidOperationException>(() => CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 }));
                Assert.IsTrue(owners.Disconnect(101, current, clear, _ => { }));
                Assert.AreEqual(0, online);
                using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 })) { }
            }
            finally { owners.Disconnect(101, current, clear, _ => { }); owners.Disconnect(101, oldClient, clear, _ => { }); }
        });
    }

    [TestMethod]
    public void FailedBotOnlineWritePreservesCurrentOwnerAndReleasesFailedLease()
    {
        WithOwnershipDirectory(() =>
        {
            var owners = new ChatSessionOwnership<object>();
            var current = new object();
            var failed = new object();
            int online = 0;
            try
            {
                owners.Register(101, current, () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => online = 1));
                Assert.ThrowsException<IOException>(() => owners.Register(101, failed,
                    () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => throw new IOException("Online write failed."))));
                Assert.AreSame(current, owners.Clients[101]);
                Assert.AreEqual(1, online);
                Assert.IsFalse(owners.Disconnect(101, failed, _ => Assert.Fail(), _ => Assert.Fail()));
                Assert.ThrowsException<InvalidOperationException>(() => CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 }));
            }
            finally { owners.Disconnect(101, current, id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, _ => online = 0), _ => { }); }
            using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 })) { }

            Assert.ThrowsException<IOException>(() => owners.Register(102, new object(),
                () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(102, _ => throw new IOException("First online write failed."))));
            Assert.IsFalse(owners.Clients.ContainsKey(102));
            using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 102 })) { }
        });
    }

    [TestMethod]
    public void FailedBotOfflineWriteStillReleasesLeaseAndRetiresClient()
    {
        WithOwnershipDirectory(() =>
        {
            var owners = new ChatSessionOwnership<object>();
            var client = new object();
            int removed = 0;
            owners.Register(101, client, () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => { }));
            Assert.ThrowsException<IOException>(() => owners.Disconnect(101, client,
                id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, _ => throw new IOException("Offline write failed.")),
                _ => removed++));
            Assert.AreEqual(1, removed);
            Assert.AreEqual(0, owners.Clients.Count);
            Assert.IsFalse(owners.Register(101, client, () => throw new Exception("Failed disconnect client registered.")));
            using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101 })) { }
        });
    }

    private sealed class EmptyLease : IDisposable
    {
        public void Dispose() { }
    }

    [TestMethod]
    public void FailedCharacterReselectionDisconnectsOriginalBotOwnership()
    {
        WithOwnershipDirectory(() =>
        {
            var owners = new ChatSessionOwnership<object>();
            var client = new object();
            uint cleared = 0, removed = 0;
            try
            {
                owners.Register(101, client, () => CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => { }));
                Assert.ThrowsException<InvalidOperationException>(() => owners.Register(102, client,
                    () => throw new Exception("Repeated selection acquired another lease.")));
                Assert.AreSame(client, owners.Clients[101]);
                Assert.IsFalse(owners.Clients.ContainsKey(102));
                Assert.IsTrue(owners.Disconnect(102, client,
                    id => CharacterOnlineOwnershipGuard.TryClearLoginOwnership((int)id, _ => cleared = id),
                    id => removed = id));
                Assert.AreEqual(101u, cleared);
                Assert.AreEqual(101u, removed);
                Assert.AreEqual(0, owners.Clients.Count);
                using (CharacterOnlineOwnershipGuard.AcquireStaleRecoveryOwnership(new[] { 101, 102 })) { }
            }
            finally { owners.Disconnect(101, client, _ => { }, _ => { }); }
        });
    }

    private static void WithOwnershipDirectory(Action action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aorebirth-chat-ownership-" + Guid.NewGuid().ToString("N"));
        string? old = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
        Directory.CreateDirectory(directory);
        try
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", directory);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", old);
            Directory.Delete(directory, true);
        }
    }
}
