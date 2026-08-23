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

namespace LoginEngine.CoreClient
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Text;
    using System.Threading;

    using Cell.Core;

    using AORebirth.Core.Components;
    using AORebirth.Core.EventHandlers.Events;
    using AORebirth.Database.Dao;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility;

    #endregion

    /// <summary>
    /// </summary>
    public class Client : ClientBase
    {
        #region Fields

        /// <summary>
        /// </summary>
        private readonly IBus bus;

        /// <summary>
        /// </summary>
        private readonly IMessageSerializer messageSerializer;

        /// <summary>
        /// </summary>
        private readonly object authenticationSync = new object();

        /// <summary>
        /// </summary>
        private string accountName = string.Empty;

        /// <summary>
        /// </summary>
        private string authenticatedAccountName = string.Empty;

        /// <summary>
        /// </summary>
        private long authenticationGeneration;

        /// <summary>
        /// </summary>
        private AuthenticationState authenticationState = AuthenticationState.AwaitingLogin;

        /// <summary>
        /// </summary>
        private string clientVersion = string.Empty;

        /// <summary>
        /// </summary>
        private ushort packetNumber = 1;

        /// <summary>
        /// </summary>
        private string serverSalt = string.Empty;

        private readonly LoginHandoffLifecycle handoffLifecycle;

        private Timer handoffTimeout;

        /// <summary>
        /// </summary>
        private enum AuthenticationState
        {
            AwaitingLogin,
            ChallengeIssued,
            Authenticating,
            Closed,
            Authenticated
        }

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        /// <param name="server">
        /// </param>
        /// <param name="messageSerializer">
        /// </param>
        /// <param name="bus">
        /// </param>
        public Client(ServerBase server, IMessageSerializer messageSerializer, IBus bus)
            : base(server)
        {
            this.messageSerializer = messageSerializer;
            this.bus = bus;
            this.handoffLifecycle = new LoginHandoffLifecycle(
                new CharacterDaoLoginHandoffOnlineStore(),
                Console.WriteLine);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public string AccountName
        {
            get
            {
                lock (this.authenticationSync)
                {
                    return this.accountName;
                }
            }

            set
            {
                lock (this.authenticationSync)
                {
                    this.accountName = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// </summary>
        public string ClientVersion
        {
            get
            {
                lock (this.authenticationSync)
                {
                    return this.clientVersion;
                }
            }

            set
            {
                lock (this.authenticationSync)
                {
                    this.clientVersion = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// </summary>
        public string ServerSalt
        {
            get
            {
                lock (this.authenticationSync)
                {
                    return this.serverSalt;
                }
            }

            set
            {
                lock (this.authenticationSync)
                {
                    this.serverSalt = value ?? string.Empty;
                }
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="receiver">
        /// </param>
        /// <param name="messageBody">
        /// </param>
        public void Send(int receiver, MessageBody messageBody)
        {
            // TODO: Investigate if reciever is a timestamp
            var message = new Message
                          {
                              Body = messageBody,
                              Header =
                                  new Header
                                  {
                                      MessageId = BitConverter.ToUInt16(new byte[] { 0xDF, 0xDF }, 0),
                                      PacketType = messageBody.PacketType,
                                      Unknown = 0x0001,
                                      Sender = 0x00000001,
                                      Receiver = receiver
                                  }
                          };
            byte[] buffer = this.messageSerializer.Serialize(message);

            buffer[0] = BitConverter.GetBytes(this.packetNumber)[0];
            buffer[1] = BitConverter.GetBytes(this.packetNumber)[1];
            this.packetNumber++;

            LogUtil.Debug(DebugInfoDetail.Network, "Sent:\r\n" + HexOutput.Output(buffer));

            if (buffer.Length % 4 > 0)
            {
                Array.Resize(ref buffer, buffer.Length + (4 - (buffer.Length % 4)));
            }

            this.Send(buffer);
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="value">
        /// </param>
        /// <returns>
        /// </returns>
        internal static string ToLogValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int length = Math.Min(value.Length, 128);
            var result = new StringBuilder(length);
            for (int index = 0; index < length; index++)
            {
                char character = value[index];
                result.Append(char.IsControl(character) ? '?' : character);
            }

            return result.ToString();
        }

        /// <summary>
        /// </summary>
        /// <param name="newAccountName">
        /// </param>
        /// <param name="newClientVersion">
        /// </param>
        /// <param name="newServerSalt">
        /// </param>
        internal bool BeginAuthentication(
            string newAccountName,
            string newClientVersion,
            string newServerSalt)
        {
            lock (this.authenticationSync)
            {
                if (this.authenticationState == AuthenticationState.Closed)
                {
                    return false;
                }

                this.accountName = newAccountName ?? string.Empty;
                this.clientVersion = newClientVersion ?? string.Empty;
                this.serverSalt = newServerSalt ?? string.Empty;
                this.authenticatedAccountName = string.Empty;
                unchecked
                {
                    this.authenticationGeneration++;
                }
                this.authenticationState =
                    string.IsNullOrWhiteSpace(this.accountName) || string.IsNullOrWhiteSpace(this.serverSalt)
                        ? AuthenticationState.AwaitingLogin
                        : AuthenticationState.ChallengeIssued;
                return this.authenticationState == AuthenticationState.ChallengeIssued;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="attemptedAccountName">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool CompleteAuthentication(string attemptedAccountName, long attemptedGeneration)
        {
            lock (this.authenticationSync)
            {
                if (this.authenticationState != AuthenticationState.Authenticating
                    || this.authenticationGeneration != attemptedGeneration
                    || string.IsNullOrWhiteSpace(attemptedAccountName)
                    || !string.Equals(
                        this.accountName,
                        attemptedAccountName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                this.accountName = attemptedAccountName;
                this.authenticatedAccountName = attemptedAccountName;
                this.authenticationState = AuthenticationState.Authenticated;
                this.serverSalt = string.Empty;
                return true;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="attemptedAccountName">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool HasAuthenticationChallenge(string attemptedAccountName)
        {
            lock (this.authenticationSync)
            {
                return this.authenticationState == AuthenticationState.ChallengeIssued
                       && !string.IsNullOrWhiteSpace(attemptedAccountName)
                       && string.Equals(
                           this.accountName,
                           attemptedAccountName,
                           StringComparison.OrdinalIgnoreCase)
                       && !string.IsNullOrWhiteSpace(this.serverSalt);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="attemptedAccountName">
        /// </param>
        /// <param name="challengedAccountName">
        /// </param>
        /// <param name="challengedServerSalt">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool TryBeginAuthenticationAttempt(
            string attemptedAccountName,
            out string challengedAccountName,
            out string challengedServerSalt,
            out long challengedGeneration)
        {
            lock (this.authenticationSync)
            {
                if (this.authenticationState != AuthenticationState.ChallengeIssued
                    || string.IsNullOrWhiteSpace(attemptedAccountName)
                    || !string.Equals(
                        this.accountName,
                        attemptedAccountName,
                        StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(this.serverSalt))
                {
                    challengedAccountName = string.Empty;
                    challengedServerSalt = string.Empty;
                    challengedGeneration = 0;
                    return false;
                }

                this.authenticationState = AuthenticationState.Authenticating;
                challengedAccountName = this.accountName;
                challengedServerSalt = this.serverSalt;
                challengedGeneration = this.authenticationGeneration;
                return true;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="authenticatedAccount">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool TryGetAuthenticatedAccountName(out string authenticatedAccount)
        {
            lock (this.authenticationSync)
            {
                if (this.authenticationState != AuthenticationState.Authenticated
                    || string.IsNullOrWhiteSpace(this.authenticatedAccountName))
                {
                    authenticatedAccount = string.Empty;
                    return false;
                }

                authenticatedAccount = this.authenticatedAccountName;
                return true;
            }
        }

        /// <summary>
        /// </summary>
        internal void RejectAuthentication()
        {
            lock (this.authenticationSync)
            {
                if (this.authenticationState == AuthenticationState.Closed)
                {
                    return;
                }

                this.accountName = string.Empty;
                this.authenticatedAccountName = string.Empty;
                this.clientVersion = string.Empty;
                this.serverSalt = string.Empty;
                unchecked
                {
                    this.authenticationGeneration++;
                }
                this.authenticationState = AuthenticationState.AwaitingLogin;
            }

            try
            {
                this.Send(0x00001F83, new LoginErrorMessage { Error = LoginError.InvalidUserNamePassword });
            }
            finally
            {
                this.Server.DisconnectClient(this);
            }
        }

        internal void MarkCharacterOnlineForHandoff(int characterId)
        {
            this.handoffLifecycle.MarkOnline(characterId);
        }

        internal void StartZoneHandoff()
        {
            this.handoffLifecycle.StartHandoff();
            this.StopHandoffTimer();
            this.handoffTimeout = new Timer(
                this.OnHandoffTimeout,
                null,
                ResolveHandoffTimeoutMilliseconds(),
                Timeout.Infinite);
        }

        internal void FailZoneHandoff(string reason)
        {
            this.StopHandoffTimer();
            this.TryCleanupHandoff(reason);
        }

        /// <summary>
        /// </summary>
        /// <param name="disposing">
        /// </param>
        protected override void Dispose(bool disposing)
        {
            this.StopHandoffTimer();
            this.TryCleanupHandoff("client-disconnect");

            lock (this.authenticationSync)
            {
                this.accountName = string.Empty;
                this.authenticatedAccountName = string.Empty;
                this.clientVersion = string.Empty;
                this.serverSalt = string.Empty;
                unchecked
                {
                    this.authenticationGeneration++;
                }
                this.authenticationState = AuthenticationState.Closed;
            }

            base.Dispose(disposing);
        }

        private void OnHandoffTimeout(object state)
        {
            this.TryCleanupHandoff("handoff-timeout");
            this.Server.DisconnectClient(this);
        }

        private void TryCleanupHandoff(string reason)
        {
            try
            {
                this.handoffLifecycle.CleanupLoginOwnership(reason);
            }
            catch (Exception exception)
            {
                this.Server.Warning(
                    this,
                    "Login handoff cleanup failed for character {0}: {1}",
                    this.handoffLifecycle.CharacterId,
                    exception.Message);
            }
        }

        private void StopHandoffTimer()
        {
            Timer timer = Interlocked.Exchange(ref this.handoffTimeout, null);
            if (timer != null)
            {
                timer.Dispose();
            }
        }

        private static int ResolveHandoffTimeoutMilliseconds()
        {
            const int defaultSeconds = 30;
            int seconds;
            string configured = Environment.GetEnvironmentVariable("AO_REBIRTH_LOGIN_HANDOFF_TIMEOUT_SECONDS");
            if (!int.TryParse(configured, out seconds) || seconds < 5 || seconds > 120)
            {
                seconds = defaultSeconds;
            }

            return seconds * 1000;
        }

        /// <summary>
        /// </summary>
        /// <param name="segment">
        /// </param>
        /// <returns>
        /// </returns>
        protected uint GetMessageNumber(BufferSegment segment)
        {
            var messageNumberArray = new byte[4];
            messageNumberArray[3] = segment.SegmentData[16];
            messageNumberArray[2] = segment.SegmentData[17];
            messageNumberArray[1] = segment.SegmentData[18];
            messageNumberArray[0] = segment.SegmentData[19];
            uint reply = BitConverter.ToUInt32(messageNumberArray, 0);
            return reply;
        }

        /// <summary>
        /// </summary>
        /// <param name="segment">
        /// </param>
        /// <returns>
        /// </returns>
        protected uint GetMessageNumber(byte[] segment)
        {
            var messageNumberArray = new byte[4];
            messageNumberArray[3] = segment[16];
            messageNumberArray[2] = segment[17];
            messageNumberArray[1] = segment[18];
            messageNumberArray[0] = segment[19];
            uint reply = BitConverter.ToUInt32(messageNumberArray, 0);
            return reply;
        }

        /// <summary>
        /// </summary>
        /// <param name="buffer">
        /// </param>
        /// <returns>
        /// </returns>
        protected override bool OnReceive(BufferSegment buffer)
        {
            Message message = null;

            var packet = new byte[this._remainingLength];
            Array.Copy(buffer.SegmentData, packet, this._remainingLength);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                "Offset: " + buffer.Offset.ToString() + " -- RemainingLength: " + this._remainingLength);
            LogUtil.Debug(DebugInfoDetail.Network, HexOutput.Output(packet));

            this._remainingLength = 0;
            try
            {
                message = this.messageSerializer.Deserialize(packet);
            }
            catch (Exception)
            {
                uint messageNumber = this.GetMessageNumber(packet);
                this.Server.Warning(
                    this,
                    "Client sent malformed message {0}",
                    messageNumber.ToString(CultureInfo.InvariantCulture));
                LogUtil.Debug(DebugInfoDetail.Error, HexOutput.Output(packet));
                return false;
            }

            buffer.IncrementUsage();

            if (message == null)
            {
                uint messageNumber = this.GetMessageNumber(packet);
                this.Server.Warning(
                    this,
                    "Client sent unknown message {0}",
                    messageNumber.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            this.bus.Publish(new MessageReceivedEvent(this, message));

            return true;
        }

        #endregion
    }
}
