#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace AORebirth.Communication.ISComV2Client
{
    #region Usings ...

    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using AORebirth.Communication.Messages;

    using MsgPack.Serialization;

    using Utility;

    #endregion

    /// <summary>
    /// Zone→ChatEngine ISCom client. Zone does not dial ChatEngine at startup.
    /// Link only when ChatEngine is already listening (operator starts it for LFT /
    /// owner-only pet SystemChat / etc.), then keepalive while linked.
    /// </summary>
    public class ISComV2Client : IDisposable
    {
        #region Fields

        private readonly ISComV2ClientBase clientBase = new ISComV2ClientBase();

        private readonly object linkLock = new object();

        private bool closing = false;

        private Thread connectorThread;

        private IPAddress serverAddress;

        private int serverPort;

        private bool disposed = false;

        private bool everLinked = false;

        private Thread linkWatchThread;

        #endregion

        #region Constructors and Destructors

        public ISComV2Client()
        {
            this.clientBase.ReceivedData += this.ClientBaseReceivedData;
            this.clientBase.Disconnected += this.ClientBaseDisconnected;
        }

        #endregion

        #region Delegates

        public delegate void OnConnectHandler(object sender, EventArgs e);

        public delegate void OnReceiveDataHandler(object sender, DynamicMessage e);

        public delegate void ReallyDisconnectedHandler(object sender, EventArgs e);

        #endregion

        #region Public Events

        public event OnConnectHandler OnConnect;

        public event OnReceiveDataHandler OnReceiveData;

        public event ReallyDisconnectedHandler ReallyDisconnected;

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// True only while the TCP socket is actually connected to ChatEngine.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return this.clientBase != null && this.clientBase.IsConnected;
            }
        }

        /// <summary>
        /// Remember ChatEngine endpoint. Does not dial. Starts a quiet watch so when
        /// the operator starts ChatEngine (pets / LFT), Zone links without spam.
        /// </summary>
        public void Configure(IPAddress address, int port)
        {
            this.serverAddress = address;
            this.serverPort = port;
            this.EnsureLinkWatchRunning();
        }

        /// <summary>
        /// Legacy entry: configure only. Does not force-dial a closed port.
        /// </summary>
        public bool Connect(IPAddress address, int port)
        {
            this.Configure(address, port);
            this.TryLinkIfChatEngineListening();
            return true;
        }

        /// <summary>
        /// Dial only if ChatEngine is already listening. No refuse spam.
        /// </summary>
        public bool TryLinkIfChatEngineListening()
        {
            if (this.IsConnected)
            {
                return true;
            }

            if (this.serverAddress == null || this.closing)
            {
                return false;
            }

            lock (this.linkLock)
            {
                if (this.IsConnected)
                {
                    return true;
                }

                // Do not rate-limit when CE is up — pet SystemChat must link on first command.
                if (!this.IsChatEngineListening())
                {
                    return false;
                }

                try
                {
                    this.clientBase.ResetForReconnect();
                    this.clientBase.Connect(this.serverAddress, this.serverPort);
                    if (this.OnConnect != null)
                    {
                        this.OnConnect(this, EventArgs.Empty);
                    }

                    this.everLinked = true;
                    this.EnsureConnectorRunning();
                    LogUtil.Debug(DebugInfoDetail.Engine, "ISCom connected to ChatEngine");
                    return true;
                }
                catch (Exception e)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "ISCom dial failed while ChatEngine listening: " + e.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Send when ChatEngine is linked. If ChatEngine is listening, link first.
        /// Returns false when ChatEngine is not running (normal for Zone-only work).
        /// </summary>
        public bool TrySend(DynamicMessage dataObject)
        {
            if (!this.IsConnected && !this.TryLinkIfChatEngineListening())
            {
                return false;
            }

            MessagePackSerializer<object> serializer = MessagePackSerializer.Create<object>();
            byte[] data = serializer.PackSingleObject(dataObject);
            byte[] finalData = new byte[8 + data.Length];
            BitConverter.GetBytes(0x00ff55aa).CopyTo(finalData, 0);
            BitConverter.GetBytes(data.Length).CopyTo(finalData, 4);
            Array.Copy(data, 0, finalData, 8, data.Length);
            this.clientBase.Send(finalData);
            return true;
        }

        public void Send(DynamicMessage dataObject)
        {
            if (!this.TrySend(dataObject))
            {
                throw new InvalidOperationException(
                    "ISCom Send while disconnected from ChatEngine (type="
                    + (dataObject == null ? "null" : dataObject.TypeName)
                    + ")");
            }
        }

        public bool TrySend(MessageBase dataObject)
        {
            var temp = new DynamicMessage();
            temp.DataObject = dataObject;
            return this.TrySend(temp);
        }

        public void Send(MessageBase dataObject)
        {
            if (!this.TrySend(dataObject))
            {
                throw new InvalidOperationException(
                    "ISCom Send while disconnected from ChatEngine (type="
                    + (dataObject == null ? "null" : dataObject.GetType().FullName)
                    + ")");
            }
        }

        public void ShutDown()
        {
            LogUtil.Debug(DebugInfoDetail.Engine, "Shutting down ISCom");
            this.closing = true;
            try
            {
                this.clientBase.ResetForReconnect();
            }
            catch (Exception)
            {
            }

            if (this.connectorThread != null)
            {
                while (this.connectorThread.IsAlive)
                {
                    Thread.Sleep(100);
                }
            }
        }

        #endregion

        #region Methods

        private bool IsChatEngineListening()
        {
            try
            {
                using (var probe = new TcpClient())
                {
                    IAsyncResult ar = probe.BeginConnect(this.serverAddress, this.serverPort, null, null);
                    // 200ms was too short under load — pet SystemChat then never linked.
                    if (!ar.AsyncWaitHandle.WaitOne(1500))
                    {
                        try
                        {
                            probe.Close();
                        }
                        catch (Exception)
                        {
                        }

                        return false;
                    }

                    probe.EndConnect(ar);
                    return probe.Connected;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void EnsureLinkWatchRunning()
        {
            if (this.linkWatchThread != null && this.linkWatchThread.IsAlive)
            {
                return;
            }

            this.linkWatchThread = new Thread(this.LinkWatch);
            this.linkWatchThread.IsBackground = true;
            this.linkWatchThread.Start();
        }

        /// <summary>
        /// Quietly link when ChatEngine appears. No logs while CE is down.
        /// </summary>
        private void LinkWatch()
        {
            while (!this.closing)
            {
                if (!this.IsConnected && this.serverAddress != null)
                {
                    this.TryLinkIfChatEngineListening();
                }

                Thread.Sleep(this.IsConnected ? 10000 : 2000);
            }
        }

        private void EnsureConnectorRunning()
        {
            if (this.connectorThread != null && this.connectorThread.IsAlive)
            {
                return;
            }

            this.connectorThread = new Thread(new ThreadStart(this.Connector));
            this.connectorThread.IsBackground = true;
            this.connectorThread.Start();
        }

        /// <summary>
        /// Keepalive only after a successful link. Never dials while ChatEngine is down.
        /// </summary>
        private void Connector()
        {
            Ping ping = new Ping();
            while (!this.closing)
            {
                if (this.serverAddress == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                bool linked = this.clientBase.IsConnected;
                if (!linked)
                {
                    // Only redial after we had a link before, and only if CE listens.
                    if (this.everLinked)
                    {
                        linked = this.TryLinkIfChatEngineListening();
                    }
                }

                if (!this.closing && linked && this.clientBase.IsConnected)
                {
                    try
                    {
                        // Direct write — avoid TrySend re-entry into link logic.
                        MessagePackSerializer<object> serializer = MessagePackSerializer.Create<object>();
                        var wrap = new DynamicMessage { DataObject = ping };
                        byte[] data = serializer.PackSingleObject(wrap);
                        byte[] finalData = new byte[8 + data.Length];
                        BitConverter.GetBytes(0x00ff55aa).CopyTo(finalData, 0);
                        BitConverter.GetBytes(data.Length).CopyTo(finalData, 4);
                        Array.Copy(data, 0, finalData, 8, data.Length);
                        this.clientBase.Send(finalData);
                    }
                    catch (Exception e)
                    {
                        linked = false;
                        try
                        {
                            this.clientBase.ResetForReconnect();
                        }
                        catch (Exception)
                        {
                        }

                        LogUtil.Debug(
                            DebugInfoDetail.Error,
                            "ISCom ping failed: " + e.Message);
                    }
                }

                if (!this.closing)
                {
                    Thread.Sleep(linked ? 5000 : 10000);
                }
            }
        }

        private void ClientBaseDisconnected(object sender, EventArgs e)
        {
            if (this.serverAddress == null)
            {
                this.RaiseReallyDisconnected();
                return;
            }

            // No log spam — operator starts ChatEngine when needed (LFT, pet SystemChat).
        }

        private void RaiseReallyDisconnected()
        {
            ReallyDisconnectedHandler handler = this.ReallyDisconnected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ClientBaseReceivedData(object sender, OnDataReceivedArgs e)
        {
            MessagePackSerializer<DynamicMessage> serializer = MessagePackSerializer.Create<DynamicMessage>();
            DynamicMessage tmp = serializer.UnpackSingleObject(e.dataBytes);

            if (this.OnReceiveData != null)
            {
                this.OnReceiveData(this, tmp);
            }
        }

        #endregion

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    this.clientBase.Dispose();
                }
            }

            this.disposed = true;
        }
    }
}
