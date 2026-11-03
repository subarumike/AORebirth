namespace ZoneEngine_New.Tests
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Connected synthetic clients exercise the real plaintext session sender and codec. This does not perform
    /// authentication, launch an AO client or establish that the AO client animates the received door state.
    /// </summary>
    [TestClass]
    public sealed class DoorNetworkIntegrationTests
    {
        // Every network operation and session shutdown has a five-second deadline; no unbounded socket waits.
        static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        [Timeout(60000)]
        public async Task TwoConnectedClientsReceiveInitialOpenRelockAndReplayedDoorState()
        {
            using var world = new TriggerTestWorld();
            StaticDynel door = world.AddDoor(10, 10, 0, 0);
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            await using var first = await WirePeer.ConnectAsync(listener, world.First);
            await using var second = await WirePeer.ConnectAsync(listener, world.Second);

            world.World.Doors.SendStates(first.Session);
            world.World.Doors.SendStates(second.Session);
            await AssertMatchingStates(first, second, door.Identity, locked: true, open: false, inhibit: false);

            Assert.IsTrue(world.World.ApplyDoorFlags(world.Playfield, door, Door.OpenFlag, closeInhibit: true));
            await AssertMatchingStates(first, second, door.Identity, locked: false, open: true, inhibit: true);

            Assert.IsTrue(world.World.ApplyDoorFlags(world.Playfield, door, Door.LockedFlag));
            await AssertMatchingStates(first, second, door.Identity, locked: true, open: false, inhibit: true);

            // This is the same authoritative replay used for a newly entering session, exercised over the wire.
            world.World.Doors.SendStates(first.Session);
            world.World.Doors.SendStates(second.Session);
            await AssertMatchingStates(first, second, door.Identity, locked: true, open: false, inhibit: true);
            Assert.IsFalse(first.Session.IsClosed);
            Assert.IsFalse(second.Session.IsClosed);
            Assert.AreEqual(0, first.Log.Errors.Count);
            Assert.AreEqual(0, second.Log.Errors.Count);
        }

        static async Task AssertMatchingStates(WirePeer first, WirePeer second, Identity identity,
            bool locked, bool open, bool inhibit)
        {
            byte[][] packets = await Task.WhenAll(first.ReadPacketAsync(), second.ReadPacketAsync());
            // Header receiver differs per player. The complete N3 body must be identical.
            CollectionAssert.AreEqual(packets[0].AsSpan(16).ToArray(), packets[1].AsSpan(16).ToArray());
            var codec = new ZoneMessageCodec();
            for (int i = 0; i < packets.Length; i++)
            {
                var decoded = codec.Deserialize(packets[i]);
                Assert.IsNotNull(decoded);
                Assert.AreEqual(i == 0 ? first.Player.Identity.Instance : second.Player.Identity.Instance, decoded.Header.Receiver);
                Assert.IsInstanceOfType<DoorStatusUpdateMessage>(decoded.Body);
                var status = (DoorStatusUpdateMessage)decoded.Body;
                Assert.AreEqual(identity, status.Identity);
                Assert.AreEqual(2, status.Unknown1);
                Assert.AreEqual((byte)(locked ? 1 : 0), status.Unknown2);
                Assert.AreEqual((byte)(open ? 1 : 0), status.Unknown3);
                Assert.AreEqual(0, status.Unknown4);
                Assert.AreEqual((byte)(inhibit ? 1 : 0), status.Unknown5);
                Assert.AreEqual(0, status.Unknown6.Length);
            }
        }

        sealed class WirePeer : IAsyncDisposable
        {
            readonly TcpClient _client;
            readonly CancellationTokenSource _lifetime = new();
            readonly Task _run;

            WirePeer(TcpClient client, ZoneSession session, Player player, WireLogger log)
            {
                _client = client;
                Session = session;
                Player = player;
                Log = log;
                _run = session.RunAsync(_lifetime.Token);
            }

            public ZoneSession Session { get; }
            public Player Player { get; }
            public WireLogger Log { get; }

            public static async Task<WirePeer> ConnectAsync(TcpListener listener, Player player)
            {
                var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
                Socket? accepted = null;
                ZoneSession? session = null;
                try
                {
                    using var deadline = new CancellationTokenSource(NetworkTimeout);
                    var endpoint = (IPEndPoint)listener.LocalEndpoint;
                    await client.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token);
                    accepted = await listener.AcceptSocketAsync(deadline.Token);
                    accepted.NoDelay = true;
                    var logger = new WireLogger();
                    // No inbound packets are sent. Login is deliberately not exercised; the existing fixture
                    // supplies synthetic in-play players, just as the non-network door tests do.
                    var unusedLogin = (ZoneLoginHandler)RuntimeHelpers.GetUninitializedObject(typeof(ZoneLoginHandler));
                    var dispatcher = new ZoneMessageDispatcher(unusedLogin, logger);
                    session = new ZoneSession(Guid.NewGuid(), accepted, new ZoneMessageCodec(), dispatcher, logger);
                    player.Session?.UnbindPlayer();
                    session.BindPlayer(player);
                    player.Session = session;
                    session.State = SessionState.InPlay;
                    return new WirePeer(client, session, player, logger);
                }
                catch
                {
                    session?.UnbindPlayer();
                    session?.Close();
                    accepted?.Dispose();
                    client.Dispose();
                    throw;
                }
            }

            public async Task<byte[]> ReadPacketAsync()
            {
                using var deadline = new CancellationTokenSource(NetworkTimeout);
                NetworkStream stream = _client.GetStream();
                byte[] header = new byte[16];
                await stream.ReadExactlyAsync(header, deadline.Token);
                int size = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(6, 2));
                Assert.IsTrue(size >= 16 && size <= 8192, "Invalid session frame size.");
                byte[] packet = new byte[size];
                header.CopyTo(packet, 0);
                await stream.ReadExactlyAsync(packet.AsMemory(16), deadline.Token);
                int padding = (4 - size % 4) % 4;
                if (padding != 0)
                {
                    byte[] pad = new byte[padding];
                    await stream.ReadExactlyAsync(pad, deadline.Token);
                    CollectionAssert.AreEqual(new byte[padding], pad);
                }
                return packet;
            }

            public async ValueTask DisposeAsync()
            {
                // These fixtures have no persistent character lease/manager. Detach before closing transport,
                // rather than invoking unrelated production logout or database paths during test cleanup.
                Session.UnbindPlayer();
                if (ReferenceEquals(Player.Session, Session))
                {
                    Player.Session = null;
                }
                Session.Close();
                _lifetime.Cancel();
                try
                {
                    await _run.WaitAsync(NetworkTimeout);
                }
                finally
                {
                    _client.Dispose();
                    _lifetime.Dispose();
                }
            }
        }

        sealed class WireLogger : IZoneLogger
        {
            public ConcurrentQueue<string> Errors { get; } = new();
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) => Errors.Enqueue(message);
            public void Error(Exception exception, string message) => Errors.Enqueue(message + ": " + exception.Message);
            public IZoneLogger CreateForPlayfield(int playfieldId) => this;
        }
    }
}
