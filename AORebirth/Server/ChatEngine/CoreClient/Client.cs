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

namespace ChatEngine.CoreClient
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using Cell.Core;

    using ChatEngine.Channels;
    using ChatEngine.CoreServer;

    using Utility;

    #endregion

    /// <summary>
    /// The client.
    /// </summary>
    public class Client : ClientBase
    {
        #region Fields

        /// <summary>
        /// </summary>
        public HashSet<ChannelBase> Channels = new HashSet<ChannelBase>();

        /// <summary>
        /// </summary>
        public bool IsBot = false;

        /// <summary>
        /// Private known clients collection
        /// </summary>
        private readonly List<uint> knownClients;

        private readonly Dictionary<uint, byte[]> buddyStatusFlags = new Dictionary<uint, byte[]>();

        private readonly object buddyStatusSync = new object();

        private readonly object privateGroupSync = new object();

        private readonly HashSet<uint> privateGroupInvitations = new HashSet<uint>();

        private uint privateGroupId;

        /// <summary>
        /// </summary>
        private ushort packetNumber = 1;

        // The shared TCP client supplies fixed-size chunks, not complete chat frames.
        private readonly byte[] receiveHeader = new byte[4];

        private int receiveHeaderLength;

        private byte[] receivePacket;

        private int receivePacketLength;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class. 
        /// The client.
        /// </summary>
        /// <param name="srvr">
        /// </param>
        public Client(ChatServer srvr)
            : base(srvr)
        {
            this.Character = new Character(0, null);
            this.AuthenticatedUsername = string.Empty;
            this.ServerSalt = string.Empty;
            this.knownClients = new List<uint>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Client"/> class. 
        /// The client.
        /// </summary>
        public Client()
            : base(null)
        {
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// The character.
        /// </summary>
        public Character Character { get; set; }

        /// <summary>
        /// Account username proven by the encrypted chat login request.
        /// </summary>
        internal string AuthenticatedUsername { get; set; }

        /// <summary>
        /// The known clients.
        /// </summary>
        public List<uint> KnownClients
        {
            get
            {
                return this.knownClients;
            }
        }

        internal void SetBuddyStatusFlags(uint buddyId, byte[] statusFlags)
        {
            if (statusFlags == null || statusFlags.Length != 3)
            {
                return;
            }

            lock (this.buddyStatusSync)
            {
                this.buddyStatusFlags[buddyId] = (byte[])statusFlags.Clone();
            }
        }

        /// <summary>
        /// The status flags for <paramref name="buddyId"/>: what the client sent when it added the buddy, else
        /// <paramref name="fallback"/> (00 01 01 for a stored buddy; a tell partner who is not one keeps 00 01 00).
        /// </summary>
        internal byte[] GetBuddyStatusFlags(uint buddyId, byte[] fallback = null)
        {
            lock (this.buddyStatusSync)
            {
                byte[] statusFlags;
                if (this.buddyStatusFlags.TryGetValue(buddyId, out statusFlags))
                {
                    return (byte[])statusFlags.Clone();
                }

                return fallback != null ? (byte[])fallback.Clone() : new byte[] { 0x00, 0x01, 0x01 };
            }
        }

        /// <summary>
        /// This character's buddies, loaded once at login and kept current on add, so a login or logout does not read
        /// every connected client's buddy list from the database.
        /// </summary>
        private readonly HashSet<uint> buddyIds = new HashSet<uint>();

        internal void SetBuddies(IEnumerable<int> ids)
        {
            lock (this.buddyStatusSync)
            {
                this.buddyIds.Clear();
                foreach (int id in ids)
                {
                    if (id > 0)
                    {
                        this.buddyIds.Add(unchecked((uint)id));
                    }
                }
            }
        }

        internal void AddBuddyId(uint buddyId)
        {
            lock (this.buddyStatusSync)
            {
                this.buddyIds.Add(buddyId);
            }
        }

        internal bool HasBuddy(uint buddyId)
        {
            lock (this.buddyStatusSync)
            {
                return this.buddyIds.Contains(buddyId);
            }
        }

        internal uint PrivateGroupId
        {
            get
            {
                lock (this.privateGroupSync)
                {
                    return this.privateGroupId;
                }
            }

            set
            {
                lock (this.privateGroupSync)
                {
                    this.privateGroupId = value;
                }
            }
        }

        internal void AddPrivateGroupInvitation(uint privateGroupId)
        {
            lock (this.privateGroupSync)
            {
                this.privateGroupInvitations.Add(privateGroupId);
            }
        }

        internal bool ConsumePrivateGroupInvitation(uint privateGroupId)
        {
            lock (this.privateGroupSync)
            {
                return this.privateGroupInvitations.Remove(privateGroupId);
            }
        }

        /// <summary>
        /// The server salt.
        /// </summary>
        public string ServerSalt { get; set; }

        /// <summary>
        /// UTC when this chat client finished character login (LFT login-echo grace).
        /// </summary>
        public DateTime ChatAuthenticatedUtc { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public ChatServer ChatServer()
        {
            return this.Server as ChatServer;
        }

        /// <summary>
        /// </summary>
        /// <param name="receiver">
        /// </param>
        /// <param name="packetBytes">
        /// </param>
        public void Send(int receiver, byte[] packetBytes)
        {
            packetBytes[0] = BitConverter.GetBytes(this.packetNumber)[0];
            packetBytes[1] = BitConverter.GetBytes(this.packetNumber)[1];
            this.packetNumber++;

            LogUtil.Debug(DebugInfoDetail.Network, "\r\nSent:\r\n" + HexOutput.Output(packetBytes));

            if (packetBytes.Length % 4 > 0)
            {
                Array.Resize(ref packetBytes, packetBytes.Length + (4 - (packetBytes.Length % 4)));
            }

            this.Send(packetBytes);
        }

        #endregion

        #region Methods

        // NV: Should this be here or inside Character...

        /// <summary>
        /// The get message number.
        /// </summary>
        /// <param name="packet">
        /// </param>
        /// <returns>
        /// The get message number.
        /// </returns>
        protected ushort GetMessageNumber(byte[] packet)
        {
            ushort reply = BitConverter.ToUInt16(new[] { packet[1], packet[0] }, 0);
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
            int offset = buffer.Offset + this._offset;
            int remaining = this._remainingLength;
            while (remaining > 0)
            {
                if (this.receivePacket == null)
                {
                    int headerBytes = Math.Min(this.receiveHeader.Length - this.receiveHeaderLength, remaining);
                    Buffer.BlockCopy(buffer.Buffer.Array, offset, this.receiveHeader, this.receiveHeaderLength, headerBytes);
                    this.receiveHeaderLength += headerBytes;
                    offset += headerBytes;
                    remaining -= headerBytes;
                    if (this.receiveHeaderLength < this.receiveHeader.Length)
                        break;

                    // The native header is type(2) + unsigned big-endian body length(2).
                    int bodyLength = (this.receiveHeader[2] << 8) | this.receiveHeader[3];
                    this.receivePacket = new byte[this.receiveHeader.Length + bodyLength];
                    Buffer.BlockCopy(this.receiveHeader, 0, this.receivePacket, 0, this.receiveHeader.Length);
                    this.receivePacketLength = this.receiveHeader.Length;
                    this.receiveHeaderLength = 0;
                }

                int bodyBytes = Math.Min(this.receivePacket.Length - this.receivePacketLength, remaining);
                if (bodyBytes > 0)
                {
                    Buffer.BlockCopy(buffer.Buffer.Array, offset, this.receivePacket, this.receivePacketLength, bodyBytes);
                    this.receivePacketLength += bodyBytes;
                    offset += bodyBytes;
                    remaining -= bodyBytes;
                }
                if (this.receivePacketLength < this.receivePacket.Length)
                    break;

                // A handler may send this array asynchronously (Ping does), so never reuse it.
                byte[] packet = this.receivePacket;
                this.receivePacket = null;
                this.receivePacketLength = 0;
                LogUtil.Debug(DebugInfoDetail.Network, "\r\nReceived:\r\n" + HexOutput.Output(packet));
                ushort messageNumber = this.GetMessageNumber(packet);
                Parser parser = new Parser();
                // Unknown complete opcodes are logged by Parser; their frame is still consumed.
                parser.Parse(this, packet, messageNumber);
                // A handler may disconnect this client before the next coalesced frame.
                if (this._tcpSock == null)
                    return true;
            }

            // Every supplied byte was consumed or retained privately. Release the shared chunk.
            return true;
        }

        #endregion
    }
}
