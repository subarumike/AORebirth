using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using Cell.Core.Exceptions;
using Cell.Core.Localization;
using NLog;

namespace Cell.Core
{
    public class UDPSendToArgs
    {
        private readonly ServerBase server;
        private readonly IPEndPoint client;

        public UDPSendToArgs(ServerBase srvr, IPEndPoint client)
        {
            server = srvr;
            this.client = client;
        }

        public ServerBase Server
        {
            get { return server; }
        }

        public IPEndPoint ClientIP
        {
            get { return client; }
        }
    }

    public delegate void ClientConnectedHandler(IClient client);
    public delegate void ClientDisconnectedHandler(IClient client, bool forced);

    /// <summary>
    /// Base class for AO Rebirth TCP/UDP engine listeners.
    /// </summary>
    public abstract class ServerBase : IDisposable
    {
        protected static readonly Logger log = LogManager.GetCurrentClassLogger();

        protected HashSet<IClient> _clients = new HashSet<IClient>();
        protected IPEndPoint _tcpEndpoint;
        protected IPEndPoint _udpEndpoint;
        protected Socket _tcpListen;
        protected Socket _udpListen;
        protected int _maxPendingCon = 100;
        protected bool _running;
        protected bool TcpEnabledEnabled;
        protected bool UdpEnabledEnabled;

        private readonly byte[] udpBuffer = new byte[1024];

        public event ClientConnectedHandler ClientConnected;
        public event ClientDisconnectedHandler ClientDisconnected;

        public virtual bool IsRunning
        {
            get { return _running; }
            set { _running = value; }
        }

        public virtual int MaximumPendingConnections
        {
            get { return _maxPendingCon; }
            set
            {
                if (value > 0)
                {
                    _maxPendingCon = value;
                }
            }
        }

        public virtual int TcpPort
        {
            get { return _tcpEndpoint.Port; }
            set { _tcpEndpoint.Port = value; }
        }

        public virtual int UdpPort
        {
            get { return _udpEndpoint.Port; }
            set { _udpEndpoint.Port = value; }
        }

        public virtual IPAddress TcpIP
        {
            get { return _tcpEndpoint.Address; }
            set { _tcpEndpoint.Address = value; }
        }

        public virtual IPAddress UdpIP
        {
            get { return _udpEndpoint.Address; }
            set { _udpEndpoint.Address = value; }
        }

        public virtual IPEndPoint TcpEndPoint
        {
            get { return _tcpEndpoint; }
            set { _tcpEndpoint = value; }
        }

        public virtual IPEndPoint UdpEndPoint
        {
            get { return _udpEndpoint; }
            set { _udpEndpoint = value; }
        }

        public int ClientCount
        {
            get
            {
                lock (_clients)
                {
                    return _clients.Count;
                }
            }
        }

        public string RootPath
        {
            get { return Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName; }
        }

        public bool TCPEnabled
        {
            get { return TcpEnabledEnabled; }
            set
            {
                if (_running && TcpEnabledEnabled != value)
                {
                    if (value)
                    {
                        StartTCP();
                    }
                    else
                    {
                        StopTCP();
                    }
                }
            }
        }

        public bool UDPEnabled
        {
            get { return UdpEnabledEnabled; }
            set
            {
                if (UdpEnabledEnabled && !value && _running)
                {
                    if (_udpListen != null)
                    {
                        _udpListen.Close();
                    }

                    UdpEnabledEnabled = false;
                }
                else if (!UdpEnabledEnabled && value && _running)
                {
                    StartUDP();
                }
            }
        }

        public ushort UdpCounter { get; set; }

        public virtual void Start(bool useTcp, bool useUdp)
        {
            try
            {
                if (_running)
                {
                    return;
                }

                log.Info(Resources.BaseStart);
                IsRunning = true;

                if (useTcp)
                {
                    StartTCP();
                }

                if (useUdp)
                {
                    StartUDP();
                }

                log.Info(Resources.ReadyForConnections, this);
            }
            catch (InvalidEndpointException ex)
            {
                log.Fatal(Resources.InvalidEndpoint, ex.Endpoint);
                Stop();
            }
            catch (NoAvailableAdaptersException)
            {
                log.Fatal(Resources.NoNetworkAdapters);
                Stop();
            }
        }

        public virtual void Stop()
        {
            log.Info(Resources.BaseStop);

            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            RemoveAllClients();

            if (_tcpListen != null)
            {
                _tcpListen.Close(60);
                _tcpListen = null;
            }

            if (_udpListen != null)
            {
                _udpListen.Close();
                _udpListen = null;
            }

            TcpEnabledEnabled = false;
            UdpEnabledEnabled = false;
        }

        public void DisconnectClient(IClient client, bool forced)
        {
            RemoveClient(client);

            try
            {
                OnClientDisconnected(client, forced);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                LogManager.GetLogger(CellDef.CORE_LOG_FNAME).ErrorException("Could not disconnect client", e);
            }
        }

        public void DisconnectClient(IClient client)
        {
            DisconnectClient(client, true);
        }

        public void RemoveAllClients()
        {
            IClient[] clients;
            lock (_clients)
            {
                clients = _clients.ToArray();
                _clients.Clear();
            }

            foreach (IClient client in clients)
            {
                try
                {
                    OnClientDisconnected(client, true);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception e)
                {
                    LogManager.GetLogger(CellDef.CORE_LOG_FNAME).Error(e.ToString());
                }
            }
        }

        public static void VerifyEndpointAddress(IPEndPoint endPoint)
        {
            if (endPoint.Address.Equals(IPAddress.Any) || endPoint.Address.Equals(IPAddress.Loopback))
            {
                return;
            }

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (interfaces.Length == 0)
            {
                throw new NoAvailableAdaptersException();
            }

            foreach (NetworkInterface iface in interfaces)
            {
                UnicastIPAddressInformationCollection addresses = iface.GetIPProperties().UnicastAddresses;
                if (addresses.Any(ipInfo => ipInfo.Address.Equals(endPoint.Address)))
                {
                    return;
                }
            }

            throw new InvalidEndpointException(endPoint);
        }

        public static IPAddress GetDefaultExternalIPAddress()
        {
            return IPAddress.Loopback;
        }

        public void StartUDP()
        {
            if (UdpEnabledEnabled || !_running)
            {
                return;
            }

            IPEndPoint udpEndpoint = new IPEndPoint(UdpIP, UdpPort);
            VerifyEndpointAddress(udpEndpoint);

            _udpListen = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpListen.Bind(udpEndpoint);

            StartReceivingUdp(null);

            UdpEnabledEnabled = true;
            Info(null, Resources.ListeningUDPSocket, UdpEndPoint);
        }

        public void Error(IClient client, Exception e)
        {
            Error(client, "Exception raised: " + e);
        }

        public virtual void Error(IClient client, string msg, params object[] parms)
        {
            log.Error(FormatLogString(client, msg, parms));
        }

        public virtual void Warning(IClient client, Exception e)
        {
            if (log.IsWarnEnabled)
            {
                log.Warn("{0} - {1}", client, e);
            }
        }

        public virtual void Warning(IClient client, string msg, params object[] parms)
        {
            if (log.IsWarnEnabled)
            {
                log.Warn(FormatLogString(client, msg, parms));
            }
        }

        public virtual void Info(IClient client, string msg, params object[] parms)
        {
            if (log.IsInfoEnabled)
            {
                log.Info(FormatLogString(client, msg, parms));
            }
        }

        public virtual void Debug(IClient client, string msg, params object[] parms)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(FormatLogString(client, msg, parms));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract IClient CreateClient(IPAddress address);
        protected abstract void OnReceiveUDP(int numBytes, byte[] buf, IPEndPoint ip);
        protected abstract void OnSendTo(IPEndPoint clientIP, int numBytes);

        protected void RemoveClient(IClient client)
        {
            lock (_clients)
            {
                _clients.Remove(client);
            }
        }

        protected virtual bool OnClientConnected(IClient client)
        {
            Info(client, Resources.ClientConnected);

            ClientConnectedHandler handler = ClientConnected;
            if (handler != null)
            {
                handler(client);
            }

            return true;
        }

        protected virtual void OnClientDisconnected(IClient client, bool forced)
        {
            Info(client, Resources.ClientDisconnected);

            ClientDisconnectedHandler handler = ClientDisconnected;
            if (handler != null)
            {
                handler(client, forced);
            }

            client.Dispose();
        }

        protected void StartTCP()
        {
            if (TcpEnabledEnabled || !_running)
            {
                return;
            }

            VerifyEndpointAddress(TcpEndPoint);
            _tcpListen = new Socket(TcpEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                _tcpListen.Bind(TcpEndPoint);
            }
            catch (Exception ex)
            {
                log.Error("Could not bind to Address {0}: {1}", TcpEndPoint, ex);
                return;
            }

            _tcpListen.Listen(MaximumPendingConnections);
            SocketHelpers.SetListenSocketOptions(_tcpListen);
            StartAccept(null);

            TcpEnabledEnabled = true;
            Info(null, Resources.ListeningTCPSocket, TcpEndPoint);
        }

        protected void StopTCP()
        {
            if (!TcpEnabledEnabled)
            {
                return;
            }

            try
            {
                if (_tcpListen != null)
                {
                    _tcpListen.Close();
                }
            }
            catch (Exception ex)
            {
                log.Warn("Exception occurred while closing TCP listener {0}: {1}", TcpEndPoint, ex);
            }

            _tcpListen = null;
            TcpEnabledEnabled = false;
            Info(null, Resources.ListeningTCPSocketStopped, TcpEndPoint);
        }

        protected void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (_tcpListen == null)
            {
                return;
            }

            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += AcceptEventCompleted;
            }
            else
            {
                acceptEventArg.AcceptSocket = null;
            }

            bool willRaiseEvent = _tcpListen.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        protected void StartReceivingUdp(SocketAsyncEventArgs args)
        {
            if (_udpListen == null)
            {
                return;
            }

            if (args == null)
            {
                args = new SocketAsyncEventArgs();
                args.Completed += UdpRecvEventCompleted;
            }

            args.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            args.SetBuffer(udpBuffer, 0, udpBuffer.Length);

            bool willRaiseEvent = _udpListen.ReceiveAsync(args);
            if (!willRaiseEvent)
            {
                ProcessUdpReceive(args);
            }
        }

        protected void SendUDP(byte[] buf, IPEndPoint client)
        {
            if (_udpListen != null)
            {
                _udpListen.BeginSendTo(buf, 0, buf.Length, SocketFlags.None, client, SendToCallback, new UDPSendToArgs(this, client));
            }
        }

        protected static string FormatLogString(IClient client, string msg, params object[] parms)
        {
            msg = parms == null ? msg : string.Format(msg, parms);
            return client == null ? msg : string.Format("({0}) -> {1}", client, msg);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_running)
            {
                Stop();
            }
        }

        ~ServerBase()
        {
            Dispose(false);
        }

        private void AcceptEventCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs args)
        {
            try
            {
                if (!_running)
                {
                    LogManager.GetLogger(CellDef.CORE_LOG_FNAME).Info(Resources.ServerNotRunning);
                    return;
                }

                IPAddress address = ((IPEndPoint)args.AcceptSocket.RemoteEndPoint).Address;
                IClient client = CreateClient(address);
                client.TcpSocket = args.AcceptSocket;
                client.BeginReceive();

                StartAccept(args);

                if (OnClientConnected(client))
                {
                    lock (_clients)
                    {
                        _clients.Add(client);
                    }
                }
                else
                {
                    client.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException e)
            {
                LogManager.GetLogger(CellDef.CORE_LOG_FNAME).WarnException(Resources.SocketExceptionAsyncAccept, e);
            }
            catch (Exception e)
            {
                LogManager.GetLogger(CellDef.CORE_LOG_FNAME).FatalException(Resources.FatalAsyncAccept, e);
            }
        }

        private void UdpRecvEventCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessUdpReceive(e);
        }

        private void ProcessUdpReceive(SocketAsyncEventArgs args)
        {
            try
            {
                int numBytes = args.BytesTransferred;
                IPEndPoint endpoint = args.RemoteEndPoint as IPEndPoint;

                OnReceiveUDP(numBytes, udpBuffer, endpoint);
                StartReceivingUdp(args);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException e)
            {
                LogManager.GetLogger(CellDef.CORE_LOG_FNAME).WarnException(Resources.SocketExceptionAsyncAccept, e);
            }
            catch (Exception e)
            {
                LogManager.GetLogger(CellDef.CORE_LOG_FNAME).FatalException(Resources.FatalAsyncAccept, e);
            }
        }

        private static void SendToCallback(IAsyncResult ar)
        {
            UDPSendToArgs args = ar.AsyncState as UDPSendToArgs;
            try
            {
                if (args != null)
                {
                    int numBytes = args.Server._udpListen.EndSendTo(ar);
                    args.Server.OnSendTo(args.ClientIP, numBytes);
                }
            }
            catch (Exception e)
            {
                if (args != null)
                {
                    args.Server.Error(null, e);
                }
            }
        }
    }
}
