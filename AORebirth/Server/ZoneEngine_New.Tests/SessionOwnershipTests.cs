namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using AORebirth.Database.Dao;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    [DoNotParallelize]
    public sealed class SessionOwnershipTests
    {
        [TestMethod]
        public void LoginCleanupCannotClearAnAcceptedZoneOwnershipLease()
        {
            WithOwnershipDirectory(() =>
            {
                int online = 0;
                IDisposable first = CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => online = 1);
                IDisposable second = CharacterOnlineOwnershipGuard.AcquireZoneOwnership(101, _ => online = 1);
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.ZoneOwned,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(101, _ => online = 0));
                Assert.AreEqual(1, online);
                Parallel.For(0, 20, _ => first.Dispose());
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.ZoneOwned,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(101, _ => online = 0));
                second.Dispose();
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.Cleared,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(101, _ => online = 0));
                Assert.AreEqual(0, online);
            });
        }

        [TestMethod]
        public void FailedOnlineWriteDoesNotLeakAReferenceToTheOwnershipLease()
        {
            WithOwnershipDirectory(() =>
            {
                IDisposable lease = CharacterOnlineOwnershipGuard.AcquireZoneOwnership(102, _ => { });
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    CharacterOnlineOwnershipGuard.AcquireZoneOwnership(102, _ => throw new InvalidOperationException()));
                lease.Dispose();
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.Cleared,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(102, _ => { }));
            });
        }

        [TestMethod]
        public void FailedFirstOnlineWriteReleasesTheOwnershipGate()
        {
            WithOwnershipDirectory(() =>
            {
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    CharacterOnlineOwnershipGuard.AcquireZoneOwnership(105, _ => throw new InvalidOperationException()));
                Assert.AreEqual(LoginOwnedOnlineCleanupResult.Cleared,
                    CharacterOnlineOwnershipGuard.TryClearLoginOwnership(105, _ => { }));
            });
        }

        [TestMethod]
        public void FailedSpawnClearsOnlineOnlyAfterTheLastZoneOwnerReleases()
        {
            WithOwnershipDirectory(() =>
            {
                var characters = new TrackingCharacterRepository();
                var snapshots = new CharacterSnapshotService(characters, new RejectingStatRepository(), new StubLogger());
                IDisposable authoritative = snapshots.AcquireOnlineOwnership(106);
                IDisposable failedSpawn = snapshots.AcquireOnlineOwnership(106);
                snapshots.AbandonOnlineOwnership(106, failedSpawn);
                Assert.AreEqual(1, characters.Online);
                Assert.AreEqual(0, characters.OfflineWrites);
                snapshots.AbandonOnlineOwnership(106, authoritative);
                Assert.AreEqual(0, characters.Online);
                Assert.AreEqual(1, characters.OfflineWrites);
            });
        }

        [TestMethod]
        public void PlayerRetainsOwnershipThroughLinkDeadAndReconnectUntilDespawn()
        {
            Player player = TestWorld.CreatePlayer(103);
            var lease = new TrackingLease();
            player.AttachOnlineOwnership(lease);
            player.EnterOnline(new StubSession());
            player.EnterLinkDead(TimeSpan.FromSeconds(30));
            Assert.AreEqual(0, lease.DisposeCount);
            player.EnterOnline(new StubSession());
            Assert.IsNull(player.LinkDeadUntilUtc);
            Assert.AreEqual(0, lease.DisposeCount);
            Parallel.For(0, 20, _ => player.ReleaseOnlineOwnership());
            Assert.AreEqual(1, lease.DisposeCount);
        }

        [TestMethod]
        public void ClosedTransportCannotBeBoundToAPlayerByLateHydration()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // The dispatcher is never invoked: this isolates connection teardown from gameplay/DB services.
            var dispatcher = (ZoneMessageDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(ZoneMessageDispatcher));
            var session = new ZoneSession(Guid.NewGuid(), socket, new ZoneMessageCodec(), dispatcher, new StubLogger());
            Parallel.For(0, 20, _ => session.Close());
            Assert.AreEqual(SessionState.Closed, session.State);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.BindPlayer(TestWorld.CreatePlayer(104)));
            Assert.IsNull(session.Player);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.State = SessionState.InPlay);

            Player latePlayer = TestWorld.CreatePlayer(104);
            latePlayer.EnterLinkDead(TimeSpan.FromSeconds(30));
            DateTime? deadline = latePlayer.LinkDeadUntilUtc;
            Assert.ThrowsExactly<InvalidOperationException>(() => latePlayer.EnterOnline(session));
            Assert.IsNull(latePlayer.Session);
            Assert.AreEqual(PlayerConnectionPhase.LinkDead, latePlayer.ConnectionPhase);
            Assert.AreEqual(deadline, latePlayer.LinkDeadUntilUtc);
        }

        [TestMethod]
        public void DuplicateLoginClosesTheTransportBeforeHydrationCanRun()
        {
            // All dependencies are intentionally absent: the duplicate-session gate must run first.
            var handler = (ZoneLoginHandler)RuntimeHelpers.GetUninitializedObject(typeof(ZoneLoginHandler));
            foreach (SessionState state in new[] { SessionState.Loading, SessionState.SpawnReady, SessionState.InPlay, SessionState.Closed })
            {
                var session = new StubSession { State = state };
                handler.HandleAsync(new ZoneLoginMessage { CharacterId = 107 }, session);
                Assert.AreEqual(SessionState.Closed, session.State);
            }
        }

        [TestMethod]
        public void ClosedSessionsCannotBeResurrectedByQueuedSpawnOrReconnect()
        {
            // Queue completion must reject closed transports before touching world/DAO services.
            var spawns = (SpawnService)RuntimeHelpers.GetUninitializedObject(typeof(SpawnService));
            var session = new StubSession { State = SessionState.Closed };
            spawns.CompletePendingSpawn(session, new PendingSpawnInboundItem
            {
                Session = session,
                Hydration = new CharacterHydrationResult()
            });
            spawns.CompletePendingReconnect(session, new PendingReconnectInboundItem { Session = session, CharacterId = 108 });
            Assert.AreEqual(SessionState.Closed, session.State);
            Assert.IsNull(session.Player);
        }

        [TestMethod]
        public void QuarantinedSnapshotNeverWritesAnUncertainInMemoryAggregate()
        {
            var characters = new TrackingCharacterRepository();
            var snapshots = new CharacterSnapshotService(characters, new RejectingStatRepository(), new StubLogger());
            Player player = TestWorld.CreatePlayer(109);
            player.QuarantinePersistence();
            Assert.IsTrue(player.IsPersistenceQuarantined);
            Assert.ThrowsExactly<InvalidOperationException>(() => snapshots.Commit(player));
            Assert.AreEqual(0, characters.SnapshotWrites);
        }

        private static void WithOwnershipDirectory(Action action)
        {
            string? previous = Environment.GetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR");
            string directory = Path.Combine(Path.GetTempPath(), "aorebirth-ownership-test-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", directory);
            try { action(); }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_SESSION_OWNERSHIP_DIR", previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class TrackingLease : IDisposable
        {
            public int DisposeCount;
            public void Dispose() => Interlocked.Increment(ref DisposeCount);
        }

        private sealed class TrackingCharacterRepository : ICharacterRepository
        {
            public int Online;
            public int OfflineWrites;
            public int SnapshotWrites;
            public CharacterRecord? GetById(int characterId) => throw new InvalidOperationException("Unexpected character read.");
            public void SetOnline(int characterId) => Online = 1;
            public void SetOffline(int characterId) { Online = 0; OfflineWrites++; }
            public void SaveLocation(CharacterRecord character, int online) => throw new InvalidOperationException("Non-atomic snapshot.");
            public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) => SnapshotWrites++;
        }

        private sealed class RejectingStatRepository : IStatRepository
        {
            public IReadOnlyList<StatRecord> GetForCharacter(int characterId) => throw new InvalidOperationException("Unexpected stats read.");
            public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats) => throw new InvalidOperationException("Non-atomic snapshot.");
        }

        private sealed class StubSession : IZoneSession
        {
            public SessionState State { get; set; } = SessionState.Loading;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player;
            public void UnbindPlayer() => Player = null;
            public void Close() => State = SessionState.Closed;
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
            public void Send(byte[] packet) { }
            public void Send(Message message) { }
            public void Send(MessageBody body) { }
            public void Send(MessageBody body, int sender, int receiver) { }
            public void SendInitiateCompression() { }
        }
    }
}
