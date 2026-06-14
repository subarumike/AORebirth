using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace Cell.Core
{
    /// <summary>
    /// Base class for TCP-backed server clients.
    /// </summary>
    public abstract class ClientBase : IClient
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private static readonly BufferManager Buffers = BufferManager.Default;
        private static long totalBytesReceived;
        private static long totalBytesSent;

        private bool disposed;
        private uint bytesReceived;
        private uint bytesSent;

        protected Socket _tcpSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        protected ServerBase _server;
        protected IPEndPoint _udpEndpoint;
        protected BufferSegment _bufferSegment;
        protected int _offset;
        protected int _remainingLength;

        public const int BufferSize = CellDef.MAX_PBUF_SEGMENT_SIZE;

        protected ClientBase(ServerBase server)
        {
            _server = server;
            _bufferSegment = Buffers.CheckOut();
        }

        public static long TotalBytesSent
        {
            get { return totalBytesSent; }
        }

        public static long TotalBytesReceived
        {
            get { return totalBytesReceived; }
        }

        public ServerBase Server
        {
            get { return _server; }
        }

        public IPAddress ClientAddress
        {
            get
            {
                IPEndPoint remote = _tcpSock == null ? null : _tcpSock.RemoteEndPoint as IPEndPoint;
                return remote == null ? null : remote.Address;
            }
        }

        public int Port
        {
            get
            {
                IPEndPoint remote = _tcpSock == null ? null : _tcpSock.RemoteEndPoint as IPEndPoint;
                return remote == null ? -1 : remote.Port;
            }
        }

        public IPEndPoint UdpEndpoint
        {
            get { return _udpEndpoint; }
            set { _udpEndpoint = value; }
        }

        public Socket TcpSocket
        {
            get { return _tcpSock; }
            set
            {
                if (_tcpSock != null && _tcpSock != value && _tcpSock.Connected)
                {
                    CloseSocket(_tcpSock);
                }

                if (value != null)
                {
                    _tcpSock = value;
                }
            }
        }

        public uint ReceivedBytes
        {
            get { return bytesReceived; }
        }

        public uint SentBytes
        {
            get { return bytesSent; }
        }

        public bool IsConnected
        {
            get { return _tcpSock != null && _tcpSock.Connected; }
        }

        public void BeginReceive()
        {
            ResumeReceive();
        }

        public void Send(byte[] packet)
        {
            Send(packet, 0, packet.Length);
        }

        public void SendCopy(byte[] packet)
        {
            byte[] copy = new byte[packet.Length];
            Array.Copy(packet, copy, packet.Length);
            Send(copy, 0, copy.Length);
        }

        public void Send(BufferSegment segment, int length)
        {
            Send(segment.Buffer.Array, segment.Offset, length);
        }

        public virtual void Send(byte[] packet, int offset, int length)
        {
            if (!IsConnected)
            {
                return;
            }

            SocketAsyncEventArgs args = SocketHelpers.AcquireSocketArg();
            if (args == null)
            {
                log.Error("Client {0}'s SocketArgs are null", this);
                return;
            }

            args.Completed += SendAsyncComplete;
            args.SetBuffer(packet, offset, length);
            args.UserToken = this;

            bool willRaiseEvent;
            try
            {
                willRaiseEvent = _tcpSock.SendAsync(args);
            }
            catch
            {
                args.Completed -= SendAsyncComplete;
                SocketHelpers.ReleaseSocketArg(args);
                throw;
            }

            unchecked
            {
                bytesSent += (uint)length;
            }

            Interlocked.Add(ref totalBytesSent, length);

            if (!willRaiseEvent)
            {
                SendAsyncComplete(this, args);
            }
        }

        public void Connect(string host, int port)
        {
            Connect(IPAddress.Parse(host), port);
        }

        public void Connect(IPAddress addr, int port)
        {
            if (_tcpSock == null)
            {
                _tcpSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }

            if (_tcpSock.Connected)
            {
                _tcpSock.Disconnect(true);
            }

            _tcpSock.Connect(addr, port);
            BeginReceive();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return TcpSocket == null || !TcpSocket.Connected
                ? "<disconnected client>"
                : (TcpSocket.RemoteEndPoint ?? (object)"<unknown client>").ToString();
        }

        protected void EnsureBuffer()
        {
            BufferSegment newSegment = Buffers.CheckOut();
            Array.Copy(
                _bufferSegment.Buffer.Array,
                _bufferSegment.Offset + _offset,
                newSegment.Buffer.Array,
                newSegment.Offset,
                _remainingLength);

            _bufferSegment.DecrementUsage();
            _bufferSegment = newSegment;
            _offset = 0;
        }

        protected abstract bool OnReceive(BufferSegment buffer);

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            BufferSegment segment = _bufferSegment;
            _bufferSegment = null;
            if (segment != null && segment.Uses > 0)
            {
                segment.DecrementUsage();
            }

            Socket socket = _tcpSock;
            _tcpSock = null;
            if (socket != null)
            {
                CloseSocket(socket);
            }
        }

        ~ClientBase()
        {
            Dispose(false);
        }

        private static void SendAsyncComplete(object sender, SocketAsyncEventArgs args)
        {
            args.Completed -= SendAsyncComplete;
            SocketHelpers.ReleaseSocketArg(args);
        }

        private void ResumeReceive()
        {
            if (!IsConnected || _bufferSegment == null)
            {
                return;
            }

            SocketAsyncEventArgs socketArgs = SocketHelpers.AcquireSocketArg();
            int offset = _offset + _remainingLength;

            socketArgs.SetBuffer(_bufferSegment.Buffer.Array, _bufferSegment.Offset + offset, BufferSize - offset);
            socketArgs.UserToken = this;
            socketArgs.Completed += ReceiveAsyncComplete;

            bool willRaiseEvent;
            try
            {
                willRaiseEvent = _tcpSock.ReceiveAsync(socketArgs);
            }
            catch
            {
                socketArgs.Completed -= ReceiveAsyncComplete;
                SocketHelpers.ReleaseSocketArg(socketArgs);
                throw;
            }

            if (!willRaiseEvent)
            {
                ProcessReceive(socketArgs);
            }
        }

        private void ReceiveAsyncComplete(object sender, SocketAsyncEventArgs args)
        {
            ProcessReceive(args);
        }

        private void ProcessReceive(SocketAsyncEventArgs args)
        {
            try
            {
                int received = args.BytesTransferred;
                if (received == 0 || args.SocketError != SocketError.Success)
                {
                    _server.DisconnectClient(this, true);
                    return;
                }

                unchecked
                {
                    bytesReceived += (uint)received;
                }

                Interlocked.Add(ref totalBytesReceived, received);
                _remainingLength += received;

                if (OnReceive(_bufferSegment))
                {
                    _offset = 0;
                    _remainingLength = 0;
                    _bufferSegment.DecrementUsage();
                    _bufferSegment = Buffers.CheckOut();
                }
                else
                {
                    EnsureBuffer();
                }

                ResumeReceive();
            }
            catch (ObjectDisposedException)
            {
                _server.DisconnectClient(this, true);
            }
            catch (Exception e)
            {
                _server.Warning(this, e);
                _server.DisconnectClient(this, true);
            }
            finally
            {
                args.Completed -= ReceiveAsyncComplete;
                SocketHelpers.ReleaseSocketArg(args);
            }
        }

        private static void CloseSocket(Socket socket)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                socket.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
