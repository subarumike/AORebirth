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

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;

    using Cell.Core;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using Ionic.Zlib;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Playfields;

    using IBus = MemBus.IBus;

    #endregion

    /// <summary>
    /// </summary>
    public class ZoneClient : ClientBase, IZoneClient
    {
        #region Fields

        /// <summary>
        /// </summary>
        public IPlayfield Playfield;

        public bool PreserveLogoutSitOnConnect { get; set; }

        /// <summary>
        /// True when this session resumed a pooled character after cross-zone redirect
        /// (new TCP session — lifecycle PhaseHistory does not contain Zoning).
        /// </summary>
        public bool IsPlayfieldTransferLogin { get; private set; }

        /// <summary>
        /// Real (UTC) time we last sent this client a <c>GameTimeMessage</c>. The client anchors its
        /// mission/quest countdown clock to that message (a fixed server epoch) and advances it in real
        /// time from there, re-anchoring on every login and zone-in. Mission expiry timestamps must be
        /// computed relative to this sync point, not wall-clock time, or the "Remain" value drifts every
        /// restart and jumps on zone change. See <see cref="ZoneEngine.Core.Perks.PerkResetMissionSender"/>.
        /// </summary>
        public DateTime LastGameTimeSyncUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// </summary>
        private readonly ZoneServer server;

        /// <summary>
        /// </summary>
        private readonly IBus bus;

        private readonly ZoneClientSessionLifecycleCoordinator sessionLifecycle;

        private readonly PacketSequencingCoordinator packetSequencing;

        /// <summary>
        /// </summary>
        private IController controller;

        /// <summary>
        /// </summary>
        private readonly IMessageSerializer messageSerializer;

        /// <summary>
        /// </summary>
        private NetworkStream netStream;

        private readonly object locker = new object();

        /// <summary>
        /// </summary>
        private short packetNumber = 0;

        /// <summary>
        /// </summary>
        private ZlibStream zStream;

        /// <summary>
        /// </summary>
        private bool zStreamSetup;

        private bool disposed = false;

        private IDisposable characterOnlineOwnership;

        private readonly Queue<QueuedOutboundPacket> sendQueue = new Queue<QueuedOutboundPacket>();

        private readonly string questNpcTransportDiagnosticSessionId = Guid.NewGuid().ToString("N");

        private Thread dispatcherThread;

        private bool stopDispatcher = false;

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
        public ZoneClient(ZoneServer server, IMessageSerializer messageSerializer, IBus bus)
            : base(server)
        {
            this.server = server;
            this.messageSerializer = messageSerializer;
            this.bus = bus;
            this.sessionLifecycle = new ZoneClientSessionLifecycleCoordinator();
            this.packetSequencing = new PacketSequencingCoordinator();
            this.dispatcherThread = new Thread(this.DispatchMessages);
            this.dispatcherThread.Start();
        }

        #endregion

        #region Public Properties

        public IController Controller
        {
            get
            {
                return this.controller;
            }
            set
            {
                this.controller = value;
            }
        }

        public ZoneClientSessionLifecycleCoordinator SessionLifecycle
        {
            get
            {
                return this.sessionLifecycle;
            }
        }

        public PacketSequencingCoordinator PacketSequencing
        {
            get
            {
                return this.packetSequencing;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="messageBody">
        /// </param>
        public void SendCompressed(MessageBody messageBody)
        {
            if ((this.controller == null) || (this.controller.Character == null))
            {
                return;
            }
            GridZoneInDiagnostics.LogOutboundMessage(this, messageBody);
            WorldEntrySummary.RecordOutboundMessage(this, messageBody);
            this.SendCompressed(messageBody, this.server.Id);
        }

        public void SendCompressed(MessageBody messageBody, int sender)
        {
            if ((this.controller == null) || (this.controller.Character == null))
            {
                return;
            }

            var message = new Message
                          {
                              Body = messageBody,
                              Header =
                                  new Header
                                  {
                                      MessageId = BitConverter.ToUInt16(new byte[] { 0xDF, 0xDF }, 0),
                                      PacketType = messageBody.PacketType,
                                      Unknown = 0x0001,
                                      Sender = sender,
                                      Receiver = this.Controller.Character.Identity.Instance
                                  }
                          };

            byte[] buffer;
            SubwayVisibilitySnapshotDiagnostics.OnSerializationStarted(messageBody);
            try
            {
                buffer = this.messageSerializer.Serialize(message);
                SubwayVisibilitySnapshotDiagnostics.OnSerializationCompleted(messageBody, buffer);
            }
            catch (Exception exception)
            {
                SubwayVisibilitySnapshotDiagnostics.OnSerializationFailed(messageBody, exception);
                throw;
            }
            CombatStartPacketDiagnostics.LogSerializedOutbound(
                "ZoneClient.SendCompressed",
                messageBody,
                sender,
                this.Controller.Character.Identity,
                buffer);
            int playfieldId = this.Controller.Character.Playfield == null
                                  ? 0
                                  : this.Controller.Character.Playfield.Identity.Instance;
            bool traceQuestNpcTransport = QuestNpcOutboundTransportDiagnostics.OnSerialized(
                this.questNpcTransportDiagnosticSessionId,
                this.Controller.Character.Identity,
                this.Controller.Character.Name,
                playfieldId,
                messageBody,
                buffer,
                EmitQuestNpcOutboundTransportDiagnostic);

            try
            {
                var queuedPacket = new QueuedOutboundPacket(buffer, traceQuestNpcTransport);
                lock (this.sendQueue)
                {
                    this.sendQueue.Enqueue(queuedPacket);
                    queuedPacket.QueueDepthAtEnqueue = this.sendQueue.Count;
                    if (traceQuestNpcTransport)
                    {
                        QuestNpcOutboundTransportDiagnostics.MarkEnqueued(buffer);
                    }

                }
            }
            catch (Exception exception)
            {
                if (traceQuestNpcTransport)
                {
                    QuestNpcOutboundTransportDiagnostics.OnQueueFailed(
                        buffer,
                        exception,
                        EmitQuestNpcOutboundTransportDiagnostic);
                }

                throw;
            }
            LogUtil.Debug(DebugInfoDetail.AoTomation, messageBody.GetType().ToString());
        }

        /// <summary>
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <exception cref="Exception">
        /// </exception>
        public void CreateCharacter(int charId)
        {
            DBCharacter character = CharacterDao.Instance.Get(charId);
            if (character == null)
            {
                throw new Exception("Character " + charId + " not found.");
            }

            bool isZoningReload = this.SessionLifecycle.Phase == ZoneClientSessionPhase.Zoning;
            this.IsPlayfieldTransferLogin = false;
            this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();

            // TODO: Save playfield type into Character table and use it accordingly
            IPlayfield pf =
                this.server.PlayfieldById(
                    new Identity() { Type = IdentityType.Playfield, Instance = character.Playfield });

            Identity characterIdentity = new Identity { Type = IdentityType.CanbeAffected, Instance = charId };
            Character pooledCharacter = Pool.Instance.GetObject<Character>(characterIdentity);
            if ((pooledCharacter != null) && (pooledCharacter.Controller is NPCController))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Removing NPC/player identity collision for " + characterIdentity.ToString(true)
                    + " while logging in character " + charId + ".");
                Pool.Instance.RemoveObject(pooledCharacter);
                pooledCharacter = null;
            }

            // Parent is immutable identity ownership; a mismatch means the pooled character belongs
            // to a stale playfield session and must not be reused for this login/reconnect.
            if (pooledCharacter != null && !pooledCharacter.Parent.Equals(pf.Identity))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Removing stale pooled player parent for " + characterIdentity.ToString(true)
                    + " parent=" + pooledCharacter.Parent.ToString(true)
                    + " currentPlayfield=" + pf.Identity.ToString(true) + ".");
                Pool.Instance.RemoveObject(pooledCharacter);
                pooledCharacter = null;
            }

            bool preserveLogoutSitOnConnect = false;
            if (pooledCharacter != null
                && !pooledCharacter.TryClaimReconnectOwnership(out preserveLogoutSitOnConnect))
            {
                if (!pooledCharacter.WaitForLogoutTimerDisposalToComplete(2000))
                {
                    throw new InvalidOperationException(
                        "Reconnect refused because the pending logout timer still owns "
                        + characterIdentity.ToString(true)
                        + ".");
                }

                DiscardUntrustedPooledCharacter(
                    pooledCharacter,
                    "pending logout timer disposal already claimed ownership");
                pooledCharacter = null;
                preserveLogoutSitOnConnect = false;
            }

            if (pooledCharacter == null)
            {
                this.Controller.Character = new Character(
                    pf.Identity,
                    characterIdentity,
                    this.Controller);
                this.controller.Character.Read();
            }
            else
            {
                this.Controller.Character = pooledCharacter;
                this.Controller.Character.Reconnect(this);
                this.IsPlayfieldTransferLogin = true;
                LogUtil.Debug(DebugInfoDetail.Engine, "Reconnected to Character " + charId);
            }

            // Always refresh from DB — reconnect skips Character.Read(), and client UI
            // resets on FullCharacter so server memory alone is not enough without a resync.
            Character playerCharacter = this.Controller.Character as Character;
            if (playerCharacter != null)
            {
                playerCharacter.ReloadTrainedPerksFromDatabase();
                // True reconnect (not zone hop): reload bags from DB. Zone hops keep memory.
                if (pooledCharacter != null && !isZoningReload)
                {
                    bool inventoryReadSucceeded = false;
                    if (playerCharacter.BaseInventory != null)
                    {
                        try
                        {
                            inventoryReadSucceeded = playerCharacter.BaseInventory.Read();
                        }
                        catch (Exception exception)
                        {
                            LogUtil.Debug(
                                DebugInfoDetail.Error,
                                "Reconnect inventory hydration threw for "
                                + characterIdentity.ToString(true)
                                + ": "
                                + exception.Message);
                            LogUtil.ErrorException(exception);
                        }
                    }

                    if (!inventoryReadSucceeded || !HasRequiredPlayerInventoryPages(playerCharacter))
                    {
                        DiscardUntrustedPooledCharacter(pooledCharacter, "inventory hydration was incomplete");
                        pooledCharacter = null;
                        this.IsPlayfieldTransferLogin = false;
                        preserveLogoutSitOnConnect = false;
                        this.Controller.Character = new Character(
                            pf.Identity,
                            characterIdentity,
                            this.Controller);
                        this.controller.Character.Read();
                        playerCharacter = this.Controller.Character as Character;
                        if (playerCharacter != null)
                        {
                            playerCharacter.ReloadTrainedPerksFromDatabase();
                        }
                    }
                }
            }

            this.PreserveLogoutSitOnConnect = preserveLogoutSitOnConnect;

            this.Controller.Character.Playfield = pf;
            this.Playfield = pf;
            this.Controller.Character.Stats.Read();
            CombatXpRuntimeService.NormalizeLevelStatBaseValue(this.Controller.Character);
            if (pooledCharacter == null)
            {
                MissionRuntime.ReloadForLogin(charId);
            }
            else if (isZoningReload)
            {
                MissionRuntime.ReloadForZoning(charId);
            }
            else
            {
                MissionRuntime.ReloadForReconnect(charId);
            }

            ActiveNanoRuntimeService.Default.TryRestoreZoneTransferStats(this.Controller.Character);
            this.controller.Character.Stats[StatIds.visualprofession].BaseValue = (uint)this.controller.Character.Stats[StatIds.profession].Value;
        }

        public void AcceptCharacterOnlineOwnership(int characterId)
        {
            if (this.characterOnlineOwnership != null)
            {
                throw new InvalidOperationException("Zone ownership was already accepted for this client.");
            }

            this.characterOnlineOwnership = CharacterOnlineOwnershipGuard.AcquireZoneOwnership(characterId);
            this.server.Info(
                this,
                "ZONE_HANDOFF event=ownership_accepted characterId={0} boundary=post-character-load-pre-session-init",
                characterId);
        }

        private static bool HasRequiredPlayerInventoryPages(Character character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            int[] requiredPages =
                {
                    (int)IdentityType.Inventory,
                    (int)IdentityType.WeaponPage,
                    (int)IdentityType.ArmorPage,
                    (int)IdentityType.ImplantPage,
                    (int)IdentityType.SocialPage,
                    (int)IdentityType.Bank
                };

            for (int i = 0; i < requiredPages.Length; i++)
            {
                IInventoryPage page;
                if (!character.BaseInventory.Pages.TryGetValue(requiredPages[i], out page)
                    || page == null
                    || !page.IsHydrated)
                {
                    return false;
                }
            }

            return true;
        }

        private static void DiscardUntrustedPooledCharacter(Character pooledCharacter, string reason)
        {
            if (pooledCharacter == null)
            {
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                "Discarding pooled player "
                + pooledCharacter.Identity.ToString(true)
                + " because "
                + reason
                + ".");

            try
            {
                Pool.Instance.RemoveObject(pooledCharacter);
            }
            catch (Exception exception)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "Pooled player discard could not remove object "
                    + pooledCharacter.Identity.ToString(true)
                    + ": "
                    + exception.Message);
            }
        }

        public void EnqueueOutboundCompressedBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            var queuedBuffer = new byte[buffer.Length];
            Buffer.BlockCopy(buffer, 0, queuedBuffer, 0, buffer.Length);

            lock (this.sendQueue)
            {
                this.sendQueue.Enqueue(new QueuedOutboundPacket(queuedBuffer, false));
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="buffer">
        /// </param>
        public void SendCompressed(byte[] buffer)
        {
            this.SendCompressed(buffer, QuestNpcOutboundTransportDiagnostics.IsTrackedBuffer(buffer));
        }

        private void SendCompressed(byte[] buffer, bool traceQuestNpcTransport)
        {
            if (buffer == null || buffer.Length < 2)
            {
                QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(
                    buffer,
                    "serialized buffer is shorter than the packet-number field",
                    EmitQuestNpcOutboundTransportDiagnostic);
                return;
            }

            // During zone reconnect the dispatcher can outlive a disposed stream.
            if (this.netStream == null || this.zStream == null)
            {
                SubwayVisibilitySnapshotDiagnostics.OnTransportUnavailable(buffer, "network or compression stream unavailable");
                if (traceQuestNpcTransport)
                {
                    QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(
                        buffer,
                        "network or compression stream unavailable",
                        EmitQuestNpcOutboundTransportDiagnostic);
                }

                return;
            }

            bool writeReturned = false;
            bool flushReturned = false;
            bool disconnectAfterTransportFailure = false;
            string transportUnavailableReason = string.Empty;
            Exception transportFailure = null;
            long zlibTotalIn = -1;
            long zlibTotalOut = -1;

            // We can not be multithreaded here. packet numbers would be jumbled
            lock (this.locker)
            {
                // Discard the packet for now, if we can not write to the stream
                if (this.netStream.CanWrite)
                {
                    byte[] pn = BitConverter.GetBytes(this.packetNumber++);
                    buffer[0] = pn[1];
                    buffer[1] = pn[0];
                    if (traceQuestNpcTransport)
                    {
                        QuestNpcOutboundTransportDiagnostics.OnPacketNumberAssigned(buffer);
                    }

                    try
                    {
                        SubwayVisibilitySnapshotDiagnostics.OnTransportStarted(buffer);
                        if (traceQuestNpcTransport)
                        {
                            QuestNpcOutboundTransportDiagnostics.OnWriteStarted(buffer);
                        }

                        this.zStream.Write(buffer, 0, buffer.Length);
                        writeReturned = true;
                        if (traceQuestNpcTransport)
                        {
                            zlibTotalIn = ZlibTotalInOrUnavailable(this.zStream);
                            zlibTotalOut = ZlibTotalOutOrUnavailable(this.zStream);
                            QuestNpcOutboundTransportDiagnostics.OnWriteReturned(
                                buffer,
                                buffer.Length,
                                zlibTotalIn,
                                zlibTotalOut);
                        }

                        this.zStream.Flush();
                        flushReturned = true;
                        if (traceQuestNpcTransport)
                        {
                            zlibTotalIn = ZlibTotalInOrUnavailable(this.zStream);
                            zlibTotalOut = ZlibTotalOutOrUnavailable(this.zStream);
                        }

                        SubwayVisibilitySnapshotDiagnostics.OnTransportCompleted(buffer);
                        if (ContainsTradeOpcode(buffer))
                        {
                            LogUtil.Debug(
                                DebugInfoDetail.Engine,
                                "OUT Trade wire len=" + buffer.Length.ToString(CultureInfo.InvariantCulture)
                                + " hex=" + BitConverter.ToString(buffer).Replace("-", string.Empty));
                        }
                    }
                    catch (Exception e)
                    {
                        transportFailure = e;
                        disconnectAfterTransportFailure = true;
                        if (traceQuestNpcTransport)
                        {
                            zlibTotalIn = ZlibTotalInOrUnavailable(this.zStream);
                            zlibTotalOut = ZlibTotalOutOrUnavailable(this.zStream);
                        }

                        SubwayVisibilitySnapshotDiagnostics.OnTransportFailed(buffer, e);
                        // Client already closed the TCP session (logout/crash/zone) — not a server bug.
                        if (IsClientTransportAbort(e))
                        {
                            LogUtil.Debug(
                                DebugInfoDetail.Network,
                                "Client closed connection during send: " + e.Message);
                        }
                        else
                        {
                            LogUtil.Debug(DebugInfoDetail.Error, "Error writing to zStream");
                            LogUtil.ErrorException(e);
                        }
                    }
                }
                else
                {
                    SubwayVisibilitySnapshotDiagnostics.OnTransportUnavailable(buffer, "network stream is not writable");
                    transportUnavailableReason = "network stream is not writable";
                }
            }

            if (traceQuestNpcTransport && flushReturned)
            {
                QuestNpcOutboundTransportDiagnostics.OnFlushReturned(
                    buffer,
                    zlibTotalIn,
                    zlibTotalOut,
                    EmitQuestNpcOutboundTransportDiagnostic);
            }
            else if (traceQuestNpcTransport && transportFailure != null)
            {
                if (writeReturned)
                {
                    QuestNpcOutboundTransportDiagnostics.OnFlushFailed(
                        buffer,
                        transportFailure,
                        zlibTotalIn,
                        zlibTotalOut,
                        EmitQuestNpcOutboundTransportDiagnostic);
                }
                else
                {
                    QuestNpcOutboundTransportDiagnostics.OnWriteFailed(
                        buffer,
                        transportFailure,
                        zlibTotalIn,
                        zlibTotalOut,
                        EmitQuestNpcOutboundTransportDiagnostic);
                }
            }
            else if (traceQuestNpcTransport && !string.IsNullOrEmpty(transportUnavailableReason))
            {
                QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(
                    buffer,
                    transportUnavailableReason,
                    EmitQuestNpcOutboundTransportDiagnostic);
            }

            if (disconnectAfterTransportFailure)
            {
                this.server.DisconnectClient(this);
            }

            LogUtil.Debug(DebugInfoDetail.Network, HexOutput.Output(buffer));
        }

        private static bool IsClientTransportAbort(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                SocketException socketException = current as SocketException;
                if (socketException != null)
                {
                    SocketError code = socketException.SocketErrorCode;
                    if (code == SocketError.ConnectionAborted
                        || code == SocketError.ConnectionReset
                        || code == SocketError.Shutdown
                        || code == SocketError.NotConnected
                        || code == SocketError.TimedOut)
                    {
                        return true;
                    }
                }

                string message = current.Message ?? string.Empty;
                if (message.IndexOf("aborted by the software in your host machine", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("Unable to write data to the transport connection", StringComparison.OrdinalIgnoreCase)
                       >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static bool ContainsTradeOpcode(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4)
            {
                return false;
            }

            for (int i = 0; i <= buffer.Length - 4; i++)
            {
                if (buffer[i] == 0x36 && buffer[i + 1] == 0x28 && buffer[i + 2] == 0x4F && buffer[i + 3] == 0x6E)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EmitQuestNpcOutboundTransportDiagnostic(string message)
        {
            LogUtil.Debug(DebugInfoDetail.Engine, message);
        }

        private static long ZlibTotalInOrUnavailable(ZlibStream stream)
        {
            try
            {
                return stream == null ? -1 : stream.TotalIn;
            }
            catch
            {
                return -1;
            }
        }

        private static long ZlibTotalOutOrUnavailable(ZlibStream stream)
        {
            try
            {
                return stream == null ? -1 : stream.TotalOut;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="messageBody">
        /// </param>
        public void SendInitiateCompressionMessage(MessageBody messageBody)
        {

            // IMPORTANT!!!!
            // DO NOT mess with this packet unless you're 9000% sure you know what you're doing.
            // This is NOT N3 message, but a special message type.
            // This is NOT fire and forget packet.
            // This is a negotiating packet which means that client and server have to agree on values.
            // out of sync = no go
            // What is hardcoded here is a working version. Changing this may break things.
            // ~Midian

            var comressionNegotiatePacket = new byte[]
                                            {
                                                0xdf, 0xdf,
                                                0x7f, 0x00,
                                                0x00, 0x01,
                                                0x00, 0x10,
                                                0x01, 0x00, // RecvCompression 0x01,0x00 Yes/0x00,0x00 No
                                                0x00, 0x00, // SendCompression 0x01,0x00 Yes/0x00,0x00 No
                                                0x00, 0x00, 0x00, 0x00
                                            };
            this.Send(comressionNegotiatePacket);
            this.packetNumber = 1;
            // TODO: Make compression choosable in config.xml
            
            /* var message = new Message
                          {
                              Body = messageBody,
                              Header =
                                  new Header
                                  {
                                      MessageId = 0xdfdf,
                                      PacketType = messageBody.PacketType,
                                      Unknown = 0x0001,

                                      
                                      Sender = 0x01000000,

                                      // 01000000 = uncompressed, 03000000 = compressed
                                      Receiver = 0 // this.character.Identity.Instance 
                                  }
                          };
            byte[] buffer = this.messageSerializer.Serialize(message);

            LogUtil.Debug(DebugInfoDetail.Network, HexOutput.Output(buffer));

            this.Send(buffer); */

            // Now create the compressed stream
            try
            {
                if (!this.zStreamSetup)
                {
                    // CreateIM the zStream
                    this.netStream = new NetworkStream(this.TcpSocket);
                    this.zStream = new ZlibStream(this.netStream, CompressionMode.Compress, CompressionLevel.BestSpeed);
                    this.zStream.FlushMode = FlushType.Sync;
                    this.zStreamSetup = true;
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="disposing">
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    this.sessionLifecycle.EnterDisconnectingForSessionDispose();

                    this.stopDispatcher = true;

                    while (this.stopDispatcher)
                    {
                        Thread.Sleep(10);
                    }

                    QuestNpcOutboundTransportDiagnostics.OnSessionDisposed(
                        this.questNpcTransportDiagnosticSessionId,
                        EmitQuestNpcOutboundTransportDiagnostic);

                    // Remove reference of character. Character getter is null-safe when
                    // the weak ref was never bound (early disconnect).
                    IController disconnectController = this.Controller;
                    ICharacter disconnectCharacter =
                        disconnectController == null ? null : disconnectController.Character;
                    if (disconnectCharacter != null)
                    {
                        int characterId = disconnectCharacter.Identity.Instance;
                        Playfield disconnectPlayfield = disconnectCharacter.Playfield as Playfield;
                        if (disconnectPlayfield != null)
                        {
                            disconnectPlayfield.ForgetVisibilityRecipient(disconnectCharacter.Identity);
                        }

                        bool preservePetRestore =
                            ActiveNanoRuntimeService.Default.HasZoneTransferStash(characterId);
                        PetRuntimeService.Default.OnCharacterDisconnected(
                            disconnectCharacter,
                            preservePetRestore);

                        bool isZoneTransfer =
                            ActiveNanoRuntimeService.Default.HasZoneTransferStash(characterId);
                        if (!isZoneTransfer)
                        {
                            // Leave game / crash: drop from team so others clear the gray slot.
                            TeamRuntime.OnCharacterDisconnected(disconnectCharacter);
                        }

                        if (!disconnectCharacter.InLogoutTimerPeriod())
                        {
                            if (!isZoneTransfer)
                            {
                                disconnectCharacter.EnterLogoutSitPosture();
                                disconnectController.State = CharacterState.Idle;
                                disconnectCharacter.StartLogoutTimer();
                            }
                        }

                        //if (this == this.character.Client)
                        // {
                        //this.character.Client = null;
                        // }
                    }
                    if (this.characterOnlineOwnership != null)
                    {
                        this.characterOnlineOwnership.Dispose();
                        this.characterOnlineOwnership = null;
                    }
                    // Client often aborts the TCP socket before we dispose. Closing ZlibStream
                    // flushes to NetworkStream and throws IOException/SocketException — expected.
                    this.CloseTransportStreamsQuietly();
                    this.controller = null;
                }
            }
            this.disposed = true;

            // Not needed anymore, since controller.character is a weakreference now and only lives in the Pool now
            // this.Controller.Character = null;

            base.Dispose(disposing);
        }

        private void CloseTransportStreamsQuietly()
        {
            try
            {
                if (this.zStream != null)
                {
                    this.zStream.Close();
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                this.zStream = null;
            }

            try
            {
                if (this.netStream != null)
                {
                    this.netStream.Close();
                }
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                this.netStream = null;
            }
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
        /// <exception cref="NotImplementedException">
        /// </exception>
        protected override bool OnReceive(BufferSegment buffer)
        {
            Message message = null;

            var packet = new byte[this._remainingLength];
            Array.Copy(buffer.SegmentData, packet, this._remainingLength);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Zone receive: {0} bytes, message {1}",
                    packet.Length,
                    this.GetMessageNumber(packet)));
            LogUtil.Debug(DebugInfoDetail.Network, "\r\nReceived: \r\n" + HexOutput.Output(packet));

            this._remainingLength = 0;
            try
            {
                message = this.messageSerializer.Deserialize(packet);
            }
            catch (Exception e)
            {
                uint messageNumber = this.GetMessageNumber(packet);
                this.Server.Warning(
                    this,
                    "Client sent malformed message {0}",
                    messageNumber.ToString(CultureInfo.InvariantCulture));
                LogUtil.ErrorException(e, false, "Zone deserialize failed for message {0}", messageNumber);
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

            LogUtil.Debug(DebugInfoDetail.Engine, "Zone message decoded: " + message.Body.GetType().FullName);

            // FUUUUUGLY

            Type wrapperType = typeof(MessageWrapper<>);
            Type genericWrapperType = wrapperType.MakeGenericType(message.Body.GetType());

            object wrapped = Activator.CreateInstance(genericWrapperType);
            wrapped.GetType().GetProperty("Client").SetValue(wrapped, (IZoneClient)this, null);
            wrapped.GetType().GetProperty("Message").SetValue(wrapped, message, null);
            wrapped.GetType().GetProperty("MessageBody").SetValue(wrapped, message.Body, null);

            this.bus.Publish(wrapped);

            return true;
        }

        private void DispatchMessages()
        {
            while (!this.stopDispatcher)
            {
                QueuedOutboundPacket queuedPacket = null;
                int remainingQueueDepth = -1;
                lock (this.sendQueue)
                {
                    if (this.sendQueue.Count > 0)
                    {
                        queuedPacket = this.sendQueue.Dequeue();
                        remainingQueueDepth = this.sendQueue.Count;
                    }
                }
                if (queuedPacket != null)
                {
                    if (queuedPacket.TraceQuestNpcTransport)
                    {
                        QuestNpcOutboundTransportDiagnostics.EmitEnqueued(
                            queuedPacket.Buffer,
                            queuedPacket.QueueDepthAtEnqueue,
                            EmitQuestNpcOutboundTransportDiagnostic);
                        QuestNpcOutboundTransportDiagnostics.OnDequeued(
                            queuedPacket.Buffer,
                            remainingQueueDepth,
                            EmitQuestNpcOutboundTransportDiagnostic);
                    }

                    this.SendCompressed(queuedPacket.Buffer, queuedPacket.TraceQuestNpcTransport);
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
            this.stopDispatcher = false;
        }

        private sealed class QueuedOutboundPacket
        {
            internal QueuedOutboundPacket(byte[] buffer, bool traceQuestNpcTransport)
            {
                this.Buffer = buffer;
                this.TraceQuestNpcTransport = traceQuestNpcTransport;
            }

            internal byte[] Buffer { get; private set; }

            internal bool TraceQuestNpcTransport { get; private set; }

            internal int QueueDepthAtEnqueue { get; set; }
        }

        #endregion
    }
}
