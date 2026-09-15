namespace ZoneEngine_New.Tests
{
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AORebirth.Communication.ISComV2Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class ISComPortableTransportTests
    {
        [TestMethod]
        public async Task SharedNet10TransportConnectsOnLoopbackWithoutWindowsIoControl()
        {
            using var timeout = new CancellationTokenSource(10000);
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var client = new ISComV2ClientBase();
            var accept = listener.AcceptSocketAsync(timeout.Token);
            client.Connect(IPAddress.Loopback, port);
            using Socket peer = await accept;
            Assert.IsTrue(peer.Connected);
            Assert.IsTrue(IPAddress.IsLoopback(((IPEndPoint)peer.RemoteEndPoint!).Address));
        }

        [TestMethod]
        public void DisposeClosesNeverConnectedSocketAndIsIdempotent()
        {
            var client = new ISComV2ClientBase();
            Socket ownedSocket = client.TcpSocket;

            client.Dispose();
            client.Dispose();

            Assert.IsTrue(ownedSocket.SafeHandle.IsClosed);
            Assert.IsNull(client.TcpSocket);
        }

        [TestMethod]
        public void DisposeAfterSocketWasClosedLeavesNoOwnedSocket()
        {
            var client = new ISComV2ClientBase();
            Socket ownedSocket = client.TcpSocket;
            ownedSocket.Dispose();

            client.Dispose();
            client.Dispose();

            Assert.IsTrue(ownedSocket.SafeHandle.IsClosed);
            Assert.IsNull(client.TcpSocket);
        }

        [TestMethod]
        public void LateDisconnectCompletionDoesNotRecreateDisposedSocket()
        {
            var client = new ISComV2ClientBase();
            int disconnectedEvents = 0;
            client.Disconnected += (_, _) => disconnectedEvents++;
            client.Dispose();

            // Model a receive completion already queued when Dispose won the
            // race. The actual disconnect handler must keep ownership empty.
            typeof(ISComV2ClientBase).GetMethod(
                "ServerDisconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(client, null);

            Assert.IsNull(client.TcpSocket);
            Assert.AreEqual(0, disconnectedEvents);
            client.Dispose();
        }

        [TestMethod]
        public async Task PeerCloseAndClientDisposeCanRaceWithoutRecreatingSocket()
        {
            using var timeout = new CancellationTokenSource(10000);
            for (int iteration = 0; iteration < 25; iteration++)
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                using var client = new ISComV2ClientBase();
                var accept = listener.AcceptSocketAsync(timeout.Token);
                client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                using Socket peer = await accept;
                var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task peerClose = Task.Run(async () => { await start.Task; peer.Dispose(); });
                Task clientClose = Task.Run(async () => { await start.Task; client.Dispose(); });

                start.SetResult(true);
                await Task.WhenAll(peerClose, clientClose).WaitAsync(timeout.Token);

                Assert.IsNull(client.TcpSocket, "Socket ownership survived disposal on iteration " + iteration);
                client.Dispose();
            }
        }
    }
}
