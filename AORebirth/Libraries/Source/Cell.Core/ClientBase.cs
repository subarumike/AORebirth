using System;
using System.Collections.Generic;
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
        private readonly object _sendSync = new object();
        private readonly Queue<PendingSend> _sendQueue = new Queue<PendingSend>();
        private PendingSend _activeSend;
        private SocketAsyncEventArgs _sendArgs;
        private bool _sendPending;
        private bool _sendDriving;
        private bool _sendStopped;
        private bool _disconnectRequested;
        private Exception _sendFailure;

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
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));
            if (length < 0 || length > segment.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            // A queued write must outlive the caller's lease on this pooled segment.
            byte[] copy = new byte[length];
            System.Buffer.BlockCopy(segment.Buffer.Array, segment.Offset, copy, 0, length);
            Send(copy, 0, length);
        }

        public virtual void Send(byte[] packet, int offset, int length)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            if (offset < 0 || offset > packet.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || length > packet.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return;

            Exception failure;
            lock (_sendSync)
            {
                if (disposed || _sendStopped || !IsConnected)
                    return;

                _sendQueue.Enqueue(new PendingSend(packet, offset, length));
                PumpSendLocked();
                failure = TakeSendFailureLocked();
            }
            DisconnectAfterSocketFailure(failure, false);
        }

        private void PumpSendLocked()
        {
            if (_sendDriving)
                return;

            _sendDriving = true;
            try
            {
                while (!_sendStopped && !_sendPending)
                {
                    if (_activeSend == null)
                    {
                        if (_sendQueue.Count == 0)
                            return;
                        _activeSend = _sendQueue.Dequeue();
                    }

                    try
                    {
                        if (_sendArgs == null)
                        {
                            _sendArgs = SocketHelpers.AcquireSocketArg();
                            if (_sendArgs == null)
                                throw new InvalidOperationException("Socket send arguments are unavailable.");
                            _sendArgs.Completed += SendAsyncComplete;
                            _sendArgs.UserToken = this;
                        }
                        _sendArgs.SetBuffer(_activeSend.Buffer, _activeSend.Offset, _activeSend.Remaining);
                        _sendPending = true;
                        if (SendAsync(_sendArgs))
                        {
                            if (_sendPending)
                                return;
                        }
                        else if (_sendPending)
                        {
                            _sendPending = false;
                            CompleteSendLocked(_sendArgs);
                        }
                    }
                    catch (Exception exception)
                    {
                        _sendPending = false;
                        FailSendLocked(exception);
                    }
                }
            }
            finally
            {
                _sendDriving = false;
                if (!_sendPending && (_sendStopped || (_activeSend == null && _sendQueue.Count == 0)))
                    ReleaseSendArgsLocked();
            }
        }

        private void CompleteSendLocked(SocketAsyncEventArgs args)
        {
            int transferred = args.BytesTransferred;
            if (args.SocketError == SocketError.Success && transferred > 0 && _activeSend != null
                && transferred <= _activeSend.Remaining)
            {
                unchecked { bytesSent += (uint)transferred; }
                Interlocked.Add(ref totalBytesSent, transferred);
                _activeSend.Offset += transferred;
                _activeSend.Remaining -= transferred;
                if (_activeSend.Remaining == 0)
                    _activeSend = null;
            }
            else if (!_sendStopped)
            {
                FailSendLocked(args.SocketError != SocketError.Success
                    ? (Exception)new SocketException((int)args.SocketError)
                    : new InvalidOperationException("Socket send completed without a valid positive byte count."));
            }

            if (_sendStopped)
                _activeSend = null;
        }

        private void FailSendLocked(Exception failure)
        {
            if (!_sendStopped)
            {
                _sendStopped = true;
                _sendFailure = failure;
            }
            _sendQueue.Clear();
            if (!_sendPending)
                _activeSend = null;
        }

        private Exception TakeSendFailureLocked()
        {
            // A completion can run before SendAsync returns. Its outer pump owns reporting after the gate is free.
            if (_sendDriving)
                return null;
            Exception failure = _sendFailure;
            _sendFailure = null;
            return failure;
        }

        private void ReleaseSendArgsLocked()
        {
            if (_sendPending || _sendDriving || _sendArgs == null)
                return;
            SocketAsyncEventArgs args = _sendArgs;
            _sendArgs = null;
            args.Completed -= SendAsyncComplete;
            args.UserToken = null;
            args.SetBuffer(null, 0, 0);
            SocketHelpers.ReleaseSocketArg(args);
        }

        private sealed class PendingSend
        {
            public readonly byte[] Buffer;
            public int Offset;
            public int Remaining;

            public PendingSend(byte[] buffer, int offset, int remaining)
            {
                Buffer = buffer;
                Offset = offset;
                Remaining = remaining;
            }
        }

        protected virtual bool SendAsync(SocketAsyncEventArgs args)
        {
            return _tcpSock.SendAsync(args);
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
            BufferSegment segment;
            Socket socket;
            lock (_sendSync)
            {
                if (disposed)
                    return;
                disposed = true;
                _sendStopped = true;
                _sendQueue.Clear();
                if (!_sendPending)
                {
                    _activeSend = null;
                    ReleaseSendArgsLocked();
                }

                segment = _bufferSegment;
                _bufferSegment = null;
                socket = _tcpSock;
                _tcpSock = null;
            }

            if (segment != null)
                segment.DecrementUsage();
            if (socket != null)
                CloseSocket(socket);
        }

        ~ClientBase()
        {
            Dispose(false);
        }

        private void SendAsyncComplete(object sender, SocketAsyncEventArgs args)
        {
            Exception failure;
            lock (_sendSync)
            {
                if (!ReferenceEquals(args, _sendArgs) || !_sendPending)
                    return;
                _sendPending = false;
                CompleteSendLocked(args);
                PumpSendLocked();
                failure = TakeSendFailureLocked();
            }
            DisconnectAfterSocketFailure(failure, false);
        }

        private void DisconnectAfterSocketFailure(Exception failure, bool disconnected)
        {
            if (failure == null && !disconnected)
                return;
            lock (_sendSync)
            {
                if (disposed || _disconnectRequested)
                    return;
                _disconnectRequested = true;
            }

            // Disconnect notifications can send to other clients; never run them under the I/O ownership gate.
            try
            {
                if (failure != null && _server != null)
                    _server.Warning(this, failure);
            }
            catch (Exception)
            {
                // A logging failure must not prevent closing the failed connection.
            }
            try
            {
                if (_server != null)
                    _server.DisconnectClient(this, true);
            }
            finally
            {
                Dispose();
            }
        }

        private void ResumeReceive()
        {
            BufferSegment operationSegment;
            Socket socket;
            int offset;
            lock (_sendSync)
            {
                if (disposed || !IsConnected || _bufferSegment == null)
                    return;
                operationSegment = _bufferSegment;
                operationSegment.IncrementUsage();
                socket = _tcpSock;
                offset = _offset + _remainingLength;
            }

            SocketAsyncEventArgs socketArgs = null;
            bool willRaiseEvent;
            try
            {
                socketArgs = SocketHelpers.AcquireSocketArg();
                if (socketArgs == null)
                    throw new InvalidOperationException("Socket receive arguments are unavailable.");
                socketArgs.UserToken = operationSegment;
                socketArgs.SetBuffer(operationSegment.Buffer.Array, operationSegment.Offset + offset, BufferSize - offset);
                socketArgs.Completed += ReceiveAsyncComplete;
                willRaiseEvent = socket.ReceiveAsync(socketArgs);
            }
            catch (Exception exception)
            {
                if (socketArgs == null)
                    operationSegment.DecrementUsage();
                else
                    ReleaseReceiveArgs(socketArgs);
                DisconnectAfterSocketFailure(exception, true);
                return;
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
                BufferSegment operationSegment = args.UserToken as BufferSegment;
                int received;
                SocketError socketError;
                lock (_sendSync)
                {
                    if (disposed || operationSegment == null || _bufferSegment == null
                        || !ReferenceEquals(operationSegment, _bufferSegment))
                        return;
                    received = args.BytesTransferred;
                    socketError = args.SocketError;
                    if (received > 0 && socketError == SocketError.Success)
                    {
                        unchecked { bytesReceived += (uint)received; }
                        Interlocked.Add(ref totalBytesReceived, received);
                        _remainingLength += received;
                    }
                }
                if (received == 0 || socketError != SocketError.Success)
                {
                    DisconnectAfterSocketFailure(socketError == SocketError.Success
                        ? null : new SocketException((int)socketError), true);
                    return;
                }

                bool consumed = OnReceive(operationSegment);
                lock (_sendSync)
                {
                    if (disposed || _bufferSegment == null || !ReferenceEquals(operationSegment, _bufferSegment))
                        return;
                    if (consumed)
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
                }

                ResumeReceive();
            }
            catch (ObjectDisposedException)
            {
                DisconnectAfterSocketFailure(null, true);
            }
            catch (Exception e)
            {
                DisconnectAfterSocketFailure(e, true);
            }
            finally
            {
                ReleaseReceiveArgs(args);
            }
        }

        private void ReleaseReceiveArgs(SocketAsyncEventArgs args)
        {
            BufferSegment operationSegment = args.UserToken as BufferSegment;
            args.Completed -= ReceiveAsyncComplete;
            args.UserToken = null;
            args.SetBuffer(null, 0, 0);
            if (operationSegment != null)
                operationSegment.DecrementUsage();
            SocketHelpers.ReleaseSocketArg(args);
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
