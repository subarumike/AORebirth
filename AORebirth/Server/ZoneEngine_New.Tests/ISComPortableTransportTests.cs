namespace ZoneEngine_New.Tests
{
    using System.Net;
    using System.Net.Sockets;
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
    }
}
