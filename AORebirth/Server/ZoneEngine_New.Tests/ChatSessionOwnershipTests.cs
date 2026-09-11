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
        Assert.IsTrue(owners.Register(101, oldClient, () => online = 1));
        Assert.IsTrue(owners.Register(101, newClient, () => online = 1));
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
        owners.Register(101, oldClient, () => online = 1);
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
                Assert.IsTrue(owners.Register(101, newClient, () => online = 1));
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
        Assert.IsFalse(owners.Register(101, client, () => Assert.Fail()));
        Assert.AreEqual(0, owners.Clients.Count);
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
