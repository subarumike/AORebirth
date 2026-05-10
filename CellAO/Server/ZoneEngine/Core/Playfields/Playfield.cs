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

namespace CellAO.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    using CellAO.Core.Entities;
    using CellAO.Core.Events;
    using CellAO.Core.Functions;
    using CellAO.Core.Inventory;
    using CellAO.Core.Items;
    using CellAO.Core.Network;
    using CellAO.Core.NPCHandler;
    using CellAO.Core.Statels;
    using CellAO.Core.Vector;
    using CellAO.Core.VendorHandler;
    using CellAO.Database.Dao;
    using CellAO.Database.Entities;
    using CellAO.Enums;
    using CellAO.Interfaces;
    using CellAO.ObjectManager;
    using CellAO.Stats.SpecialStats;

    using MemBus;
    using MemBus.Configurators;
    using MemBus.Support;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Script;

    using Config = Utility.Config.ConfigReadWrite;
    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public class Playfield : PooledObject, IPlayfield
    {
        #region Fields

        private readonly List<StaticDynel> staticDynels = new List<StaticDynel>();

        /// <summary>
        /// </summary>
        private readonly DisposeContainer memBusDisposeContainer = new DisposeContainer();

        /// <summary>
        /// </summary>
        private readonly IBus playfieldBus;

        /// <summary>
        /// </summary>
        private readonly ZoneServer server;

        /// <summary>
        /// </summary>
        private List<PlayfieldDistrict> districts = new List<PlayfieldDistrict>();

        /// <summary>
        /// </summary>
        private readonly Timer heartBeat;

        private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime> deadNpcDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DateTime> debugCorpseDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, DebugCorpseState> debugCorpses = new Dictionary<int, DebugCorpseState>();

        private readonly Dictionary<int, DebugCorpseState> pendingDebugCorpseSpawns = new Dictionary<int, DebugCorpseState>();

        private int nextDebugCorpseInstance = 0x00F0F000;

        private int nextDebugCorpseInventorySlot = 0x78;

        private const int DefaultNpcDeathAnimationKey = 0x1F7;

        private static readonly TimeSpan DeadNpcDespawnDelay = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan CorpseSpawnDelay = TimeSpan.FromMilliseconds(600);

        private static readonly Random LootRandom = new Random();

        private static readonly object LootRandomLock = new object();

        private static readonly CombatLootTableEntry[] DebugLootTable = CombatTestLootCatalog.BuildEntries();

        private static readonly Dictionary<int, int> MonsterDataToCorpseCatMesh =
            CombatCorpseVisuals.BuildMonsterDataToCorpseCatMeshMap();

        /// <summary>
        /// </summary>
        private readonly List<StatelData> statels = new List<StatelData>();

        /// <summary>
        /// </summary>
        private float x;

        private bool disposed = false;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        /// <param name="zoneServer">
        /// </param>
        /// <param name="playfieldIdentity">
        /// </param>
        public Playfield(ZoneServer zoneServer, Identity playfieldIdentity)
            : base(Identity.None, playfieldIdentity)
        {
            this.server = zoneServer;
            this.playfieldBus = BusSetup.StartWith<AsyncConfiguration>().Construct();

            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToClient>(SendAOtomationMessageToClient));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToPlayfield>(this.SendAOtomationMessageToPlayfield));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToPlayfieldOthers>(
                    this.SendAOtomationMessageToPlayfieldOthers));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageBodyToClient>(this.SendAOtomationMessageBodyToClient));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageBodiesToClient>(
                    this.SendAOtomationMessageBodiesToClient));
            this.memBusDisposeContainer.Add(this.playfieldBus.Subscribe<IMSendPlayerSCFUs>(this.SendSCFUsToClient));
            this.memBusDisposeContainer.Add(this.playfieldBus.Subscribe<IMExecuteFunction>(this.ExecuteFunction));
            this.heartBeat = new Timer(this.HeartBeatTimer, null, 10, 0);

            this.statels = PlayfieldLoader.PFData[this.Identity.Instance].Statels;
            this.LoadMobSpawns(playfieldIdentity);
            this.LoadVendors(playfieldIdentity);
            this.LoadStaticDynels(playfieldIdentity);
        }

        private void LoadStaticDynels(Identity playfieldIdentity)
        {
            IEnumerable<DBStaticDynel> dynels =
                StaticDynelDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance });
            foreach (DBStaticDynel sd in dynels)
            {
                List<GameTuple<CharacterStat, uint>> tempStats =
                    MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(sd.stats.ToArray());

                if (tempStats.Any(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid))
                {
                    int id = (int)tempStats.First(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid).Value2;
                    StaticDynel sdy = new StaticDynel(
                        this.Identity,
                        new Identity() { Type = (IdentityType)sd.Type, Instance = sd.Instance },
                        ItemLoader.ItemList[id]);

                    foreach (GameTuple<CharacterStat, uint> stat in tempStats)
                    {
                        if (sdy.Stats.ContainsKey((int)stat.Value1))
                        {
                            sdy.Stats[(int)stat.Value1] = (int)stat.Value2;
                            continue;
                        }
                        sdy.Stats.Add((int)stat.Value1, (int)stat.Value2);
                    }

                    sdy.Coordinate = new Coordinate(sd.X, sd.Y, sd.Z);
                    sdy.Heading = new Quaternion()
                                  {
                                      X = sd.HeadingX,
                                      Y = sd.HeadingY,
                                      Z = sd.HeadingZ,
                                      W = sd.HeadingW
                                  };
                }
            }
        }

        private void LoadVendors(Identity playfieldIdentity)
        {
            VendorHandler.SpawnVendorsForPlayfield(
                this,
                PlayfieldLoader.PFData[playfieldIdentity.Instance].Statels.Where(
                    x => x.Identity.Type == IdentityType.VendingMachine).ToArray());
        }

        private void LoadMobSpawns(Identity playfieldIdentity)
        {
            IEnumerable<DBMobSpawn> mobs = MobSpawnDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance });
            foreach (DBMobSpawn mob in mobs)
            {
                IEnumerable<DBMobSpawnStat> stats = MobSpawnStatDao.Instance.GetWhere(new { mob.Id, mob.Playfield });
                ICharacter cmob = NonPlayerCharacterHandler.InstantiateMobSpawn(
                    mob,
                    stats.ToArray(),
                    new NPCController(),
                    this);
                if (mob.KnuBotScriptName != "")
                {
                    ((NPCController)cmob.Controller).SetKnuBot(
                        ScriptCompiler.Instance.CreateKnuBot(mob.KnuBotScriptName, cmob.Identity));

                    /*                    if ((cmob.Stats[0].Value
                        & (int)SimpleCharFullUpdateFlags.IsImmune) == (int)SimpleCharFullUpdateFlags.IsImmune)
                    {
                        cmob.Stats[0].Value -= (int)SimpleCharFullUpdateFlags.IsImmune;
                        cmob.Stats[0].Value |= (int)SimpleCharFullUpdateFlags.UnknownFlag5;
                    }*/
                }
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public List<PlayfieldDistrict> Districts
        {
            get
            {
                return this.districts;
            }

            private set
            {
                this.districts = value;
            }
        }

        /// <summary>
        /// </summary>
        public List<Function> EnvironmentFunctions { get; private set; }

        /// <summary>
        /// </summary>
        public Expansions Expansion { get; set; }

        /// <summary>
        /// </summary>
        public IBus PlayfieldBus { get; set; }

        /// <summary>
        /// </summary>
        public float X
        {
            get
            {
                return this.X;
            }

            set
            {
                this.x = value;
            }
        }

        /// <summary>
        /// </summary>
        public float XScale { get; set; }

        /// <summary>
        /// </summary>
        public float Z { get; set; }

        /// <summary>
        /// </summary>
        public float ZScale { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public void Announce(Message message)
        {
            Announce(message.Body);
        }

        /// <summary>
        /// </summary>
        /// <param name="messageBody">
        /// </param>
        public void Announce(MessageBody messageBody)
        {
            foreach (Character entity in
                Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected)
                    .Where(x => x.InPlayfield(this.Identity)))
            {
                if (entity != null)
                {
                    // Make this whole thing unblocking with publishing single internal messages
                    if (entity.Controller.Client != null)
                    {
                        this.Publish(
                            new IMSendAOtomationMessageBodyToClient()
                            {
                                client = entity.Controller.Client,
                                Body = messageBody
                            });
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        public void AnnounceAppearanceUpdate(ICharacter character)
        {
            AppearanceUpdateMessageHandler.Default.Send(character);
        }

        /// <summary>
        /// </summary>
        /// <param name="messageBody">
        /// </param>
        /// <param name="dontSend">
        /// </param>
        public void AnnounceOthers(MessageBody messageBody, Identity dontSend)
        {
            foreach (Character entity in
                Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected)
                    .Where(xx => xx.InPlayfield(this.Identity)))
            {
                if (entity != null)
                {
                    if (entity.Identity != dontSend)
                    {
                        // Make this whole thing unblocking with publishing single internal messages
                        this.Publish(
                            new IMSendAOtomationMessageBodyToClient()
                            {
                                client = entity.Controller.Client,
                                Body = messageBody
                            });
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        public void Despawn(Identity identity)
        {
            this.Announce(DespawnMessageHandler.Default.Create(identity));
        }

        /// <summary>
        /// </summary>
        public void DisconnectAllClients()
        {
            IEnumerable<Character> templist = Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).ToList();
            for (int i = templist.Count() - 1; i >= 0; i--)
            {
                IEntity entity = templist.ElementAt(i);
                if ((entity as Character) != null)
                {
                    if ((entity as Character).Controller.Client != null)
                    {
                        this.server.DisconnectClient((entity as Character).Controller.Client);
                    }
                    (entity as Character).Dispose();
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <returns>
        /// </returns>
        public IInstancedEntity FindByIdentity(Identity identity)
        {
            return Pool.Instance.GetObject<IInstancedEntity>(identity);
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <typeparam name="T">
        /// </typeparam>
        /// <returns>
        /// </returns>
        public T FindByIdentity<T>(Identity identity) where T : class, IEntity
        {
            return Pool.Instance.GetObject<T>(identity);
        }

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        /// <param name="range">
        /// </param>
        /// <returns>
        /// </returns>
        public List<IDynel> FindInRange(IDynel dynel, float range)
        {
            List<IDynel> temp = new List<IDynel>();
            Coordinate coord = dynel.Coordinates();
            foreach (Dynel entity in
                Pool.Instance.GetAll<Dynel>((int)IdentityType.CanbeAffected).Where(xx => xx.InPlayfield(this.Identity)))
            {
                if (entity == dynel)
                {
                    continue;
                }

                if (entity.Coordinates().Distance2D(coord) <= range)
                {
                    temp.Add(entity);
                }
            }

            return temp;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool IsInstancedPlayfield()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public int NumberOfDynels()
        {
            return Pool.Instance.GetAll((int)IdentityType.CanbeAffected).Count();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public int NumberOfPlayers()
        {
            return Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).Count();
        }

        /// <summary>
        /// </summary>
        /// <param name="obj">
        /// </param>
        public void Publish(object obj)
        {
            this.playfieldBus.Publish(obj);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="body">
        /// </param>
        public void Send(IZoneClient client, MessageBody body)
        {
            this.Publish(new IMSendAOtomationMessageBodyToClient() { client = client, Body = body });
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="message">
        /// </param>
        public void Send(IZoneClient client, Message message)
        {
            this.Publish(new IMSendAOtomationMessageToClient() { client = client, message = message });
        }

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        /// <param name="destination">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <param name="playfield">
        /// </param>
        public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)
        {
            // Prevent client from entering this again
            if (dynel.DoNotDoTimers)
            {
                return;
            }
            Thread.Sleep(200);
            int dynelId = dynel.Identity.Instance;

            // Disable sending stat changes and wait a bit to clear the queue
            dynel.DoNotDoTimers = true;
            Thread.Sleep(1000);

            // Teleport to another playfield
            TeleportMessageHandler.Default.Send(
                dynel as ICharacter,
                destination.coordinate,
                (Vector.Quaternion)heading,
                playfield);

            // Send packet, disconnect, and other playfield waits for connect

            DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);
            this.AnnounceOthers(despawnMessage, dynel.Identity);
            dynel.RawCoordinates = new Vector3() { X = destination.x, Y = destination.y, Z = destination.z };
            dynel.RawHeading = new Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);

            // IMPORTANT!!
            // Dispose the character object, save new playfield data and then recreate it
            // else you would end up at weird coordinates in the same playfield

            // Save client object
            ZoneClient client = (ZoneClient)dynel.Controller.Client;

            // Set client=null so dynel can really dispose

            IPlayfield newPlayfield = this.server.PlayfieldById(playfield);
            Pool.Instance.GetObject<Playfield>(
                Identity.None,
                new Identity() { Type = playfield.Type, Instance = playfield.Instance });

            if (newPlayfield == null)
            {
                newPlayfield = new Playfield(this.server, playfield);
            }

            dynel.Playfield = newPlayfield;
            dynel.Controller.Client = null;
            dynel.IsTeleporting = true;
            dynel.Dispose();

            LogUtil.Debug(DebugInfoDetail.Database, "Saving to pf " + playfield.Instance);

            // TODO: Get new server ip from chatengine (which has to log all zoneengine's playfields)
            // for now, just transmit our ip and port

            IPAddress tempIp;
            if (IPAddress.TryParse(Config.Instance.CurrentConfig.ZoneIP, out tempIp) == false)
            {
                IPHostEntry zoneHost = Dns.GetHostEntry(Config.Instance.CurrentConfig.ZoneIP);
                foreach (IPAddress ip in zoneHost.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        tempIp = ip;
                        break;
                    }
                }
            }

            var redirect = new ZoneRedirectionMessage
                           {
                               ServerIpAddress = tempIp,
                               ServerPort = (ushort)this.server.TcpEndPoint.Port
                           };
            if (client != null)
            {
                client.SendCompressed(redirect);
            }
            // client.Server.DisconnectClient(client);
        }

        /// <summary>
        /// </summary>
        /// <param name="clientMessage">
        /// </param>
        public static void SendAOtomationMessageToClient(IMSendAOtomationMessageToClient clientMessage)
        {
            LogUtil.Debug(DebugInfoDetail.AoTomation, clientMessage.message.Body.GetType().ToString());
            clientMessage.client.SendCompressed(clientMessage.message.Body);
        }

        /// <summary>
        /// </summary>
        /// <param name="entity">
        /// </param>
        public void DisconnectClient(IInstancedEntity entity)
        {
            Pool.Instance.RemoveObject(entity);
        }

        /// <summary>
        /// </summary>
        /// <param name="imExecuteFunction">
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public void ExecuteFunction(IMExecuteFunction imExecuteFunction)
        {
            var user = (ITargetingEntity)this.FindNamedEntityByIdentity(imExecuteFunction.User);
            INamedEntity target;

            // TODO: Go over the targets, they can return item templates, inventory entries etc too
            switch (imExecuteFunction.Function.Target)
            {
                case 1:
                    target = (INamedEntity)user;
                    break;
                case 2:
                    throw new NotImplementedException("Target Wearer not implemented yet");
                case 3:
                    target = this.FindNamedEntityByIdentity(user.SelectedTarget);
                    break;
                case 14:
                    target = this.FindNamedEntityByIdentity(user.FightingTarget);
                    break;
                case 19: // Perhaps (if issued from a item) its the item itself
                    target = (INamedEntity)user;
                    break;
                case 23:
                    target = this.FindNamedEntityByIdentity(user.SelectedTarget);
                    break;
                case 26:
                    target = (INamedEntity)user;
                    break;
                case 100:
                    target = (INamedEntity)user;
                    break;
                default:
                    throw new NotImplementedException(
                        "Unknown target encountered: Target#:" + imExecuteFunction.Function.Target);
            }

            if (target == null)
            {
                var temp = user as Character;
                if (temp != null)
                {
                    if (temp.Controller.Client != null)
                    {
                        temp.Controller.Client.SendCompressed(
                            new ChatTextMessage { Identity = temp.Identity, Text = "No valid target found" });
                    }
                    return;
                }
            }

            FunctionCollection.Instance.CallFunction(
                imExecuteFunction.Function.FunctionType,
                (INamedEntity)user,
                (INamedEntity)user,
                target,
                imExecuteFunction.Function.Arguments.Values.ToArray());
        }

        public List<ICharacter> FindCharacterInRange(IDynel dynel, float range)
        {
            List<ICharacter> temp = new List<ICharacter>();
            Coordinate coord = dynel.Coordinates();
            foreach (ICharacter entity in
                Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                    .Where(xx => xx.InPlayfield(this.Identity)))
            {
                if (entity == dynel)
                {
                    continue;
                }

                if (((Character)entity).Coordinates().Distance2D(coord) <= range)
                {
                    temp.Add((Character)entity);
                }
            }

            return temp;
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <returns>
        /// </returns>
        public INamedEntity FindNamedEntityByIdentity(Identity identity)
        {
            return Pool.Instance.GetObject<INamedEntity>(identity);
        }

        /// <summary>
        /// </summary>
        /// <param name="global">
        /// </param>
        /// <returns>
        /// </returns>
        public Dictionary<Identity, string> ListAvailablePlayfields(bool global = true)
        {
            return this.server.ListAvailablePlayfields(global);
        }

        /// <summary>
        /// </summary>
        /// <param name="msg">
        /// </param>
        public void SendAOtMessageBodyToClient(IMSendAOtomationMessageBodyToClient msg)
        {
            msg.client.SendCompressed(msg.Body);
        }

        /// <summary>
        /// </summary>
        /// <param name="msg">
        /// </param>
        public void SendAOtomationMessageBodiesToClient(IMSendAOtomationMessageBodiesToClient msg)
        {
            foreach (MessageBody mb in msg.Bodies)
            {
                msg.client.SendCompressed(mb);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="msg">
        /// </param>
        public void SendAOtomationMessageBodyToClient(IMSendAOtomationMessageBodyToClient msg)
        {
            if (msg.client != null)
            {
                try
                {
                    LogUtil.Debug(DebugInfoDetail.AoTomation, msg.Body.GetType().ToString());
                    msg.client.SendCompressed(msg.Body);
                }
                catch (Exception e)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        msg.Body.GetType().ToString() + Environment.NewLine + e.Message);
                    // /!\ This happens sometimes, dont know why tho, need more investigation
                    // throw;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="clientMessage">
        /// </param>
        public void SendAOtomationMessageToPlayfield(IMSendAOtomationMessageToPlayfield clientMessage)
        {
            this.Announce(clientMessage.Body);
        }

        /// <summary>
        /// </summary>
        /// <param name="clientMessage">
        /// </param>
        public void SendAOtomationMessageToPlayfieldOthers(IMSendAOtomationMessageToPlayfieldOthers clientMessage)
        {
            this.AnnounceOthers(clientMessage.Body, clientMessage.Identity);
        }

        /// <summary>
        /// </summary>
        /// <param name="sendSCFUs">
        /// </param>
        public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)
        {
            Identity dontSendTo = sendSCFUs.toClient.Controller.Character.Identity;
            Identity playfieldIdentity = sendSCFUs.toClient.Controller.Character.Playfield.Identity;
            foreach (IEntity entity in
                Pool.Instance.GetAll<ICharacter>(playfieldIdentity, (int)IdentityType.CanbeAffected))
            {
                if (entity.Identity != dontSendTo)
                {
                    var temp = entity as Character;
                    if (temp != null)
                    {
                        SimpleCharFullUpdateMessage simpleCharFullUpdate = SimpleCharFullUpdate.ConstructMessage(temp);
                        sendSCFUs.toClient.SendCompressed(simpleCharFullUpdate);

                        var charInPlay = new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 };
                        sendSCFUs.toClient.SendCompressed(charInPlay);
                    }
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        private void CheckStatelCollision(ICharacter dynel)
        {
            foreach (StatelData sd in this.statels)
            {
                foreach (Event ev in
                    sd.Events.Where(
                        x =>
                            (x.EventType == EventType.OnCollide) || (x.EventType == EventType.OnEnter)
                            || (x.EventType == EventType.OnTargetInVicinity)))
                {
                    if (sd.Coord().Distance3D(dynel.Coordinates()) < 2.0f)
                    {
                        LogUtil.Debug(DebugInfoDetail.Statel, "Stepped on Statel " + sd.Identity.ToString(true));
                        LogUtil.Debug(DebugInfoDetail.Statel, ev.ToString());
                        ev.Perform(dynel, sd);
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        private void CheckWallCollision(ICharacter dynel)
        {
            WallCollisionResult wcr = WallCollision.CheckCollision(
                dynel.Coordinates(),
                dynel.Playfield.Identity.Instance);
            if (wcr != null)
            {
                int destPlayfield = wcr.SecondWall.DestinationPlayfield;
                if (destPlayfield > 0)
                {
                    LogUtil.Debug(DebugInfoDetail.Zoning, wcr.ToString());

                    PlayfieldDestination dest =
                        PlayfieldLoader.PFData[destPlayfield].Destinations[wcr.SecondWall.DestinationIndex];

                    LogUtil.Debug(DebugInfoDetail.Zoning, dest.ToString());

                    float newX = (dest.EndX - dest.StartX) * wcr.Factor + dest.StartX;
                    float newZ = (dest.EndZ - dest.StartZ) * wcr.Factor + dest.StartZ;
                    float dist = WallCollision.Distance(dest.StartX, dest.StartZ, dest.EndX, dest.EndZ);
                    float headDistX = (dest.EndX - dest.StartX) / dist;
                    float headDistZ = (dest.EndZ - dest.StartZ) / dist;
                    newX -= headDistZ * 8;
                    newZ += headDistX * 8;

                    Coordinate destinationCoordinate = new Coordinate(newX, dynel.RawCoordinates.Y, newZ);

                    this.Teleport(
                        (Character)dynel,
                        destinationCoordinate,
                        dynel.RawHeading,
                        new Identity() { Type = IdentityType.Playfield, Instance = destPlayfield });
                    return;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender">
        /// </param>
        private void HeartBeatTimer(object sender)
        {
            this.ProcessPendingDebugCorpseSpawns();
            this.ProcessDebugCorpseDespawns();

            IEnumerable<IEntity> dynels = null;
            dynels =
                Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                    .Where(
                        xx =>
                            xx.InPlayfield(this.Identity)
                            && (!xx.DoNotDoTimers || this.deadNpcDespawnTicks.ContainsKey(xx.Identity.Instance)));

            foreach (ICharacter dynel in dynels)
            {
                if (dynel != null)
                {
                    if (dynel.Starting)
                    {
                        continue;
                    }

                    if (this.ProcessDeadNpc(dynel))
                    {
                        continue;
                    }

                    if (dynel.DoNotDoTimers)
                    {
                        continue;
                    }

                    bool changed = false;
                    StatHealInterval healInterval = (StatHealInterval)dynel.Stats[StatIds.healinterval];
                    if (healInterval.LastTick < DateTime.UtcNow)
                    {
                        int interval = healInterval.Value;
                        int delta = dynel.Stats[StatIds.healdelta].Value;
                        dynel.Stats[StatIds.health].Value =
                            Math.Min(dynel.Stats[StatIds.life].Value, dynel.Stats[StatIds.health].Value + delta);
                        healInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(interval);
                        changed = true;
                    }

                    StatNanoInterval nanoInterval = (StatNanoInterval)dynel.Stats[StatIds.nanointerval];
                    if (nanoInterval.LastTick < DateTime.UtcNow)
                    {
                        int interval = nanoInterval.Value;
                        int delta = dynel.Stats[StatIds.nanodelta].Value;
                        dynel.Stats[StatIds.currentnano].Value += delta;
                        nanoInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(interval);
                        changed = true;
                    }

                    if (changed)
                    {
                        dynel.SendChangedStats();
                    }

                    this.DoCombatTick(dynel);

                    if (dynel.Controller.IsFollowing())
                    {
                        dynel.Controller.DoFollow();
                    }
                    else
                    {
                        if (dynel.Controller is NPCController)
                        {
                            if (dynel.Controller.State == CharacterState.Patrolling)
                            {
                                dynel.Controller.StartPatrolling();
                            }
                        }
                    }

                    if (dynel.Controller is PlayerController)
                    {
                        this.CheckWallCollision(dynel);
                        this.CheckStatelCollision(dynel);
                    }
                }
            }
            try
            {
                this.heartBeat.Change(10, 0);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void DoCombatTick(ICharacter attacker)
        {
            if (attacker.FightingTarget.Instance == 0)
            {
                this.nextCombatTicks.Remove(attacker.Identity.Instance);
                return;
            }

            DateTime nextTick;
            if (this.nextCombatTicks.TryGetValue(attacker.Identity.Instance, out nextTick)
                && nextTick > DateTime.UtcNow)
            {
                return;
            }

            ICharacter target = this.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null || !target.InPlayfield(this.Identity) || target.Stats[StatIds.health].Value <= 0)
            {
                attacker.SetFightingTarget(Identity.None);
                this.nextCombatTicks.Remove(attacker.Identity.Instance);
                return;
            }

            int currentHealth = target.Stats[StatIds.health].Value;
            int damage = this.CalculateCombatDamage(attacker);
            int newHealth = Math.Max(0, currentHealth - damage);
            bool killingHit = newHealth == 0;

            target.Stats[StatIds.health].Value = newHealth;
            if (!killingHit)
            {
                target.SendChangedStats();
            }

            this.Announce(
                new AttackInfoMessage
                {
                    Identity = attacker.Identity,
                    Target = target.Identity,
                    Unknown1 = damage,
                    Unknown2 = killingHit ? 40 : 1,
                    Unknown3 = killingHit ? 8 : 0,
                    Unknown4 = killingHit ? 4 : 0,
                    Unknown5 = killingHit ? 3 : 0,
                    Unknown6 = 0
                });
            if (!killingHit)
            {
                this.Announce(
                    new HealthDamageMessage
                    {
                        Identity = target.Identity,
                        Target = attacker.Identity,
                        Unknown1 = newHealth,
                        Unknown2 = damage,
                        Unknown3 = (int)StatIds.health,
                        Unknown4 = 0,
                        Unknown5 = 0
                    });
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Combat hit attacker={0} target={1} damage={2} health={3}/{4}",
                    attacker.Identity,
                    target.Identity,
                    damage,
                    newHealth,
                    target.Stats[StatIds.life].Value));

            if (killingHit)
            {
                if (target.Controller is NPCController)
                {
                    this.KillNpcTarget(target);
                }
                else
                {
                    attacker.SetFightingTarget(Identity.None);
                    this.nextCombatTicks.Remove(attacker.Identity.Instance);
                }

                return;
            }

            this.nextCombatTicks[attacker.Identity.Instance] = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        }

        private int CalculateCombatDamage(ICharacter attacker)
        {
            int minDamage = Math.Max(0, attacker.Stats[StatIds.mindamage].Value);
            int maxDamage = Math.Max(minDamage, attacker.Stats[StatIds.maxdamage].Value);
            int damageBonus = Math.Max(0, attacker.Stats[StatIds.damagebonus].Value);

            int fallbackDamage = attacker.Controller is PlayerController ? 15 : 1;

            if (maxDamage > 0)
            {
                return Math.Max(fallbackDamage, maxDamage + damageBonus);
            }

            return Math.Max(fallbackDamage, attacker.Stats[StatIds.level].Value + damageBonus);
        }

        private void KillNpcTarget(ICharacter target)
        {
            if (!(target.Controller is NPCController))
            {
                return;
            }

            Identity corpseIdentity = Identity.None;
            if (this.CanBuildKnownCorpseVisual(target))
            {
                corpseIdentity = this.AllocateDebugCorpseIdentity();
            }

            this.MarkNpcDead(target);
            this.StopFightingDeadTarget(target.Identity);
            this.SendNpcDeathAnimation(target);
            if (corpseIdentity != Identity.None)
            {
                this.ScheduleDebugCorpseSpawn(target, corpseIdentity);
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format("Skipping raw CorpseFullUpdate for {0}; no known MonsterData-to-CATMesh mapping.", target.Identity));
            }

            this.deadNpcDespawnTicks[target.Identity.Instance] = DateTime.UtcNow + DeadNpcDespawnDelay;

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC died target={0}", target.Identity));
        }

        public bool TryUseCorpse(ICharacter looter, Identity corpseIdentity)
        {
            if (looter == null || corpseIdentity.Type != IdentityType.Corpse)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseUse reject invalid looter={0} corpse={1}",
                        looter == null ? Identity.None : looter.Identity,
                        corpseIdentity));
                return false;
            }

            DebugCorpseState corpse;
            if (!this.debugCorpses.TryGetValue(corpseIdentity.Instance, out corpse))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseUse reject unknown corpse={0} looter={1} registeredCount={2}",
                        corpseIdentity,
                        looter.Identity,
                        this.debugCorpses.Count));
                return false;
            }

            if (corpse.ExpiresAtUtc <= DateTime.UtcNow)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("CorpseUse reject expired corpse={0} looter={1}", corpseIdentity, looter.Identity));
                this.DespawnDebugCorpse(corpseIdentity.Instance);
                return false;
            }

            bool wasOpened = corpse.Opened;
            corpse.Opened = true;

            if (corpse.HasUnlootedItems)
            {
                this.ExtendDebugCorpseLifetime(corpse, CombatCorpseRules.ItemLootCorpseLifetime, "corpse-use");
                if (corpse.NextUseSendsAccessActionOnly)
                {
                    this.SendCorpseLootAccessAction(looter, corpse);
                    this.SendUseActionFinished(looter);
                    corpse.NextUseSendsAccessActionOnly = false;
                }
                else
                {
                    this.SendCorpseInventoryUpdate(looter, corpse);
                    corpse.NextUseSendsAccessActionOnly = true;
                }
            }
            else if (!wasOpened)
            {
                this.SendCorpseInventoryUpdate(looter, corpse);
            }
            else
            {
                this.SendUseActionFinished(looter);
            }

            if (!corpse.HasUnlootedItems)
            {
                this.ScheduleDebugCorpseDespawn(
                    corpse,
                    CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay,
                    "opened-empty");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CorpseUse accepted corpse={0} deadNpc={1} looter={2} opened={3} lootClass={4}",
                    corpseIdentity,
                    corpse.DeadNpcIdentity,
                    looter.Identity,
                    corpse.Opened,
                    corpse.LootClass));

            return true;
        }

        public bool TryUseDeadNpcCorpse(ICharacter looter, Identity deadNpcIdentity, out Identity corpseIdentity)
        {
            corpseIdentity = Identity.None;

            if (looter == null || deadNpcIdentity.Type != IdentityType.CanbeAffected)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "DeadNpcCorpseUse reject invalid looter={0} deadNpc={1}",
                        looter == null ? Identity.None : looter.Identity,
                        deadNpcIdentity));
                return false;
            }

            DebugCorpseState corpse = this.debugCorpses.Values.FirstOrDefault(
                x => x.DeadNpcIdentity.Type == deadNpcIdentity.Type
                     && x.DeadNpcIdentity.Instance == deadNpcIdentity.Instance);

            if (corpse == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "DeadNpcCorpseUse reject unknown deadNpc={0} looter={1} registeredCount={2}",
                        deadNpcIdentity,
                        looter.Identity,
                        this.debugCorpses.Count));
                return false;
            }

            corpseIdentity = corpse.CorpseIdentity;
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "DeadNpcCorpseUse route deadNpc={0} corpse={1} looter={2}",
                    deadNpcIdentity,
                    corpseIdentity,
                    looter.Identity));
            return this.TryUseCorpse(looter, corpse.CorpseIdentity);
        }

        public bool TryLootCorpseItem(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement)
        {
            if (looter == null || sourceContainer.Type != IdentityType.Backpack)
            {
                return false;
            }

            int corpseInventorySlot = (sourceContainer.Instance >> 16) & 0xffff;
            DebugCorpseState corpse = this.debugCorpses.Values.FirstOrDefault(
                x => x.InventorySlot == corpseInventorySlot);

            if (corpse == null)
            {
                return false;
            }

            if (corpse.ExpiresAtUtc <= DateTime.UtcNow)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("CorpseLoot reject expired corpse={0} looter={1}", corpse.CorpseIdentity, looter.Identity));
                this.DespawnDebugCorpse(corpse.CorpseIdentity.Instance);
                return true;
            }

            if (target != looter.Identity)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject target mismatch source={0} target={1} looter={2}",
                        sourceContainer,
                        target,
                        looter.Identity));
                this.SendUseActionFinished(looter);
                return true;
            }

            int requestedLootSlot = sourceContainer.Instance & 0xffff;
            DebugCorpseLootItem lootItem = FindCorpseLootItem(corpse, requestedLootSlot);
            if (lootItem == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject missing item corpse={0} source={1} requestedSlot={2}",
                        corpse.CorpseIdentity,
                        sourceContainer,
                        requestedLootSlot));
                this.SendUseActionFinished(looter);
                return true;
            }

            if (CharacterHasUniqueItemAlready(looter, lootItem.Item))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject duplicate unique corpse={0} looter={1} source={2} item={3}/{4}",
                        corpse.CorpseIdentity,
                        looter.Identity,
                        sourceContainer,
                        lootItem.Item.LowID,
                        lootItem.Item.HighID));
                ChatTextMessageHandler.Default.Send(looter, "You already have this unique item.");
                this.SendUseActionFinished(looter);
                return true;
            }

            int targetPageNumber;
            int targetSlot;
            if (!this.TryResolveLootTargetSlot(looter, targetPlacement, out targetPageNumber, out targetSlot))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot reject no free inventory slot corpse={0} looter={1}",
                        corpse.CorpseIdentity,
                        looter.Identity));
                this.SendUseActionFinished(looter);
                return true;
            }

            InventoryError inventoryError;
            try
            {
                inventoryError = looter.BaseInventory.AddToPage(targetPageNumber, targetSlot, lootItem.Item);
            }
            catch (Exception e)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "CorpseLoot inventory add failed corpse={0} looter={1} targetSlot={2} error={3}",
                        corpse.CorpseIdentity,
                        looter.Identity,
                        targetSlot,
                        e.Message));
                this.SendUseActionFinished(looter);
                return true;
            }

            if (inventoryError != InventoryError.OK)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseLoot inventory add rejected corpse={0} looter={1} targetPage={2} targetSlot={3} error={4}",
                        corpse.CorpseIdentity,
                        looter.Identity,
                        targetPageNumber,
                        targetSlot,
                        inventoryError));

                if (inventoryError == InventoryError.HaveUniqueAlready)
                {
                    ChatTextMessageHandler.Default.Send(looter, "You already have this unique item.");
                }

                this.SendUseActionFinished(looter);
                return true;
            }

            lootItem.Looted = true;
            corpse.Opened = true;
            ContainerAddItemMessageHandler.Default.Send(
                looter,
                sourceContainer,
                targetPlacement == CombatCorpseRules.MoveToInventoryPlacement
                    ? CombatCorpseRules.MoveToInventoryPlacement
                    : targetSlot);

            if (!corpse.HasUnlootedItems)
            {
                this.ScheduleDebugCorpseDespawn(
                    corpse,
                    CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay,
                    "looted-empty");
            }
            else
            {
                this.ExtendDebugCorpseLifetime(corpse, CombatCorpseRules.ItemLootCorpseLifetime, "loot-remaining");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CorpseLoot accepted corpse={0} looter={1} source={2} lootSlot={3} targetSlot={4} remaining={5}",
                    corpse.CorpseIdentity,
                    looter.Identity,
                    sourceContainer,
                    lootItem.Slot,
                    targetSlot,
                    corpse.LootItems.Count(x => !x.Looted)));

            return true;
        }

        private static bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
        {
            if (character == null || character.BaseInventory == null || !IsUniqueItem(item))
            {
                return false;
            }

            foreach (IInventoryPage page in character.BaseInventory.Pages.Values)
            {
                foreach (KeyValuePair<int, IItem> existing in page.List())
                {
                    if (existing.Value != null
                        && !object.ReferenceEquals(existing.Value, item)
                        && existing.Value.LowID == item.LowID
                        && existing.Value.HighID == item.HighID)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsUniqueItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            ItemTemplate lowTemplate;
            if (ItemLoader.ItemList.TryGetValue(item.LowID, out lowTemplate)
                && lowTemplate.Stats.ContainsKey(0)
                && lowTemplate.IsUnique())
            {
                return true;
            }

            ItemTemplate highTemplate;
            if (ItemLoader.ItemList.TryGetValue(item.HighID, out highTemplate)
                && highTemplate.Stats.ContainsKey(0)
                && highTemplate.IsUnique())
            {
                return true;
            }

            return (item.GetAttribute(0) & (int)ItemFlags.Unique) != 0;
        }

        private bool ProcessDeadNpc(ICharacter character)
        {
            if (!(character.Controller is NPCController)
                || character.Stats[StatIds.health].Value > 0)
            {
                return false;
            }

            DateTime despawnTick;
            if (!this.deadNpcDespawnTicks.TryGetValue(character.Identity.Instance, out despawnTick))
            {
                this.deadNpcDespawnTicks[character.Identity.Instance] = DateTime.UtcNow + DeadNpcDespawnDelay;
                return true;
            }

            if (despawnTick > DateTime.UtcNow)
            {
                return true;
            }

            this.FinalizeNpcDespawn(character);
            return true;
        }

        private void MarkNpcDead(ICharacter target)
        {
            target.Stats[StatIds.health].Value = 0;
            target.Stats[StatIds.state].Value = 0;
            target.Stats[StatIds.currentstate].Value = 0;
            target.Stats[StatIds.actioncategory].Value = 0;
            target.Stats[StatIds.deadtimer].Value = 1;
            target.Stats[StatIds.itemanim].Value = DeathAnimationKeyFor(target);
            target.Stats[StatIds.corpseanimkey].Value = DeathAnimationKeyFor(target);
            target.Stats[StatIds.dieanim].Value = DeathAnimationKeyFor(target);
            target.Stats[StatIds.healdelta].Value = 0;
            target.Stats[StatIds.nanodelta].Value = 0;
            target.DoNotDoTimers = true;

            // TODO: Roll this NPC's loot table here, then keep/convert the dead dynel into a lootable corpse.
        }

        private void SendNpcDeathAnimation(ICharacter target)
        {
            this.Announce(
                new CharacterActionMessage
                {
                    Identity = target.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.Death,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = DeathAnimationKeyFor(target),
                    Unknown2 = 0
                });
        }

        private void FinalizeNpcDespawn(ICharacter target)
        {
            target.DoNotDoTimers = true;
            this.nextCombatTicks.Remove(target.Identity.Instance);
            this.deadNpcDespawnTicks.Remove(target.Identity.Instance);
            this.Despawn(target.Identity);
            Pool.Instance.RemoveObject((Character)target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC despawned target={0}", target.Identity));
        }

        private void SendDebugCorpseFullUpdate(ICharacter target, Identity corpseIdentity)
        {
            foreach (ICharacter character in
                Pool.Instance.GetAll<ICharacter>(this.Identity, (int)IdentityType.CanbeAffected))
            {
                ZoneClient client = character.Controller.Client as ZoneClient;
                if (client == null)
                {
                    continue;
                }

                client.SendCompressed(this.BuildDebugCorpseFullUpdate(target, corpseIdentity, character.Identity));
            }
        }

        private static TimeSpan CorpseLifetimeFor(CombatCorpseLootClass lootClass)
        {
            return CombatCorpseRules.LifetimeFor(lootClass);
        }

        private static CombatCorpseLootClass CorpseLootClassFor(ICharacter target, IList<DebugCorpseLootItem> lootItems)
        {
            // TODO: Add boss classification when real mob templates/loot tables are wired in.
            return CombatCorpseRules.LootClassFor(lootItems.Count, false);
        }

        private void ProcessDebugCorpseDespawns()
        {
            foreach (int corpseInstance in this.debugCorpseDespawnTicks
                .Where(x => x.Value <= DateTime.UtcNow)
                .Select(x => x.Key)
                .ToList())
            {
                this.DespawnDebugCorpse(corpseInstance);
            }
        }

        private void ProcessPendingDebugCorpseSpawns()
        {
            foreach (DebugCorpseState corpse in this.pendingDebugCorpseSpawns
                .Where(x => x.Value.SpawnsAtUtc <= DateTime.UtcNow)
                .Select(x => x.Value)
                .ToList())
            {
                this.pendingDebugCorpseSpawns.Remove(corpse.DeadNpcIdentity.Instance);

                ICharacter target = this.FindByIdentity<ICharacter>(corpse.DeadNpcIdentity);
                if (target == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Network,
                        string.Format(
                            "Skipping corpse spawn corpse={0}; dead NPC no longer exists deadNpc={1}",
                            corpse.CorpseIdentity,
                            corpse.DeadNpcIdentity));
                    continue;
                }

                this.RegisterDebugCorpse(target, corpse.CorpseIdentity);
                this.SendDebugCorpseFullUpdate(target, corpse.CorpseIdentity);
            }
        }

        private void ScheduleDebugCorpseSpawn(ICharacter target, Identity corpseIdentity)
        {
            DateTime spawnsAtUtc = DateTime.UtcNow + CorpseSpawnDelay;
            this.pendingDebugCorpseSpawns[target.Identity.Instance] =
                new DebugCorpseState
                {
                    CorpseIdentity = corpseIdentity,
                    DeadNpcIdentity = target.Identity,
                    Name = "Remains of " + target.Name,
                    LootClass = CombatCorpseLootClass.CreditsOnly,
                    SpawnsAtUtc = spawnsAtUtc
                };

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Corpse scheduled corpse={0} deadNpc={1} delayMs={2}",
                    corpseIdentity,
                    target.Identity,
                    (int)CorpseSpawnDelay.TotalMilliseconds));
        }

        private void RegisterDebugCorpse(ICharacter target, Identity corpseIdentity)
        {
            List<DebugCorpseLootItem> lootItems = RollDebugCorpseLootItems(target);
            CombatCorpseLootClass lootClass = CorpseLootClassFor(target, lootItems);
            TimeSpan lifetime = CorpseLifetimeFor(lootClass);
            DateTime expiresAtUtc = DateTime.UtcNow + lifetime;
            var state = new DebugCorpseState
            {
                CorpseIdentity = corpseIdentity,
                DeadNpcIdentity = target.Identity,
                Name = "Remains of " + target.Name,
                LootClass = lootClass,
                LootItems = lootItems,
                InventorySlot = this.AllocateDebugCorpseInventorySlot(),
                ExpiresAtUtc = expiresAtUtc
            };

            this.debugCorpses[corpseIdentity.Instance] = state;
            this.debugCorpseDespawnTicks[corpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse registered corpse={0} deadNpc={1} lifetimeSeconds={2} lootClass={3}",
                    corpseIdentity,
                    target.Identity,
                    (int)lifetime.TotalSeconds,
                    state.LootClass));
        }

        private void DespawnDebugCorpse(int corpseInstance)
        {
            Identity corpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = corpseInstance };
            this.Despawn(corpseIdentity);
            this.debugCorpseDespawnTicks.Remove(corpseInstance);
            this.debugCorpses.Remove(corpseInstance);
            LogUtil.Debug(DebugInfoDetail.Engine, string.Format("Corpse despawned corpse={0}", corpseIdentity));
        }

        private void ScheduleDebugCorpseDespawn(DebugCorpseState corpse, TimeSpan delay, string reason)
        {
            DateTime expiresAtUtc = DateTime.UtcNow + delay;
            corpse.ExpiresAtUtc = expiresAtUtc;
            this.debugCorpseDespawnTicks[corpse.CorpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse despawn scheduled corpse={0} delaySeconds={1} reason={2} remainingLoot={3}",
                    corpse.CorpseIdentity,
                    delay.TotalSeconds,
                    reason,
                    corpse.LootItems == null ? 0 : corpse.LootItems.Count(x => !x.Looted)));
        }

        private void ExtendDebugCorpseLifetime(DebugCorpseState corpse, TimeSpan minimumRemaining, string reason)
        {
            DateTime expiresAtUtc = DateTime.UtcNow + minimumRemaining;
            if (corpse.ExpiresAtUtc >= expiresAtUtc)
            {
                return;
            }

            corpse.ExpiresAtUtc = expiresAtUtc;
            this.debugCorpseDespawnTicks[corpse.CorpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse lifetime extended corpse={0} minimumRemainingSeconds={1} reason={2} remainingLoot={3}",
                    corpse.CorpseIdentity,
                    minimumRemaining.TotalSeconds,
                    reason,
                    corpse.LootItems == null ? 0 : corpse.LootItems.Count(x => !x.Looted)));
        }

        private class DebugCorpseState
        {
            public Identity CorpseIdentity { get; set; }

            public Identity DeadNpcIdentity { get; set; }

            public string Name { get; set; }

            public CombatCorpseLootClass LootClass { get; set; }

            public DateTime SpawnsAtUtc { get; set; }

            public DateTime ExpiresAtUtc { get; set; }

            public int InventorySlot { get; set; }

            public List<DebugCorpseLootItem> LootItems { get; set; }

            public bool Opened { get; set; }

            public bool NextUseSendsAccessActionOnly { get; set; }

            public bool HasUnlootedItems
            {
                get
                {
                    return this.LootItems != null && this.LootItems.Any(x => !x.Looted);
                }
            }
        }

        private class DebugCorpseLootItem
        {
            public int Slot { get; set; }

            public Item Item { get; set; }

            public bool Looted { get; set; }
        }

        private Identity AllocateDebugCorpseIdentity()
        {
            this.nextDebugCorpseInstance++;
            if (this.nextDebugCorpseInstance > 0x00F0FFFF)
            {
                this.nextDebugCorpseInstance = 0x00F0F001;
            }

            return new Identity
            {
                Type = IdentityType.Corpse,
                Instance = this.nextDebugCorpseInstance
            };
        }

        private int AllocateDebugCorpseInventorySlot()
        {
            int slot = this.nextDebugCorpseInventorySlot++;
            if (this.nextDebugCorpseInventorySlot > 0xff)
            {
                this.nextDebugCorpseInventorySlot = 0x78;
            }

            return slot;
        }

        private byte[] BuildDebugCorpseFullUpdate(ICharacter target, Identity corpseIdentity, Identity receiver)
        {
            const int originalEncodedNameLength = 27;
            const int nameOffset = 231;
            const int nameLengthOffset = 227;
            const int originalSuffixOffset = nameOffset + originalEncodedNameLength;

            byte[] template = HexToBytes(
                "0000000a0001019e000000003cac6f144f474e050000c76a00f0f00100000000080000000b00000000000000004504a4df41c5ea1244cb530d000000003e8fb30a000000003f75b5e0000002350000000000000000006f000046f200000000001818050000001700000000000002bd00000000000002be00000000000002bf000000000000019c000000010000016800000062000000df000000000000003b00000003000000040000000700000059000000010000019f0000c350000001a0776b95780000002a0000797e0000003d0000006f0000000800004650000000220000003c0000001b52656d61696e73206f66205268696e6f6d616e204d6f74686572000000000200000032000003f100000003000007e20000cf2738f46cbe0000000400000000000000010000000000000000000000000000000000000000000001f700000001000000040000798a000000000000c350776b9578000017a600000000000000000000000000000001000000000000000000000002000000000000000000000003000000000000000000000004000000000000000000000000");
            string corpseName = "Remains of " + target.Name;
            byte[] nameBytes = Encoding.ASCII.GetBytes(corpseName);
            int encodedNameLength = nameBytes.Length + 1;
            // CorpseFullUpdate resumes immediately after the encoded string's trailing null.
            // Padding this to four bytes shifts the animation/identity tail and the client
            // never registers the spawned corpse dynel.
            int newSuffixOffset = nameOffset + encodedNameLength;
            int afterNameDelta = newSuffixOffset - originalSuffixOffset;
            byte[] buffer = new byte[template.Length + afterNameDelta];

            Buffer.BlockCopy(template, 0, buffer, 0, nameOffset);
            Buffer.BlockCopy(nameBytes, 0, buffer, nameOffset, nameBytes.Length);
            Buffer.BlockCopy(
                template,
                originalSuffixOffset,
                buffer,
                newSuffixOffset,
                template.Length - originalSuffixOffset);

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, 8, this.server.Id);
            WriteInt32(buffer, 12, receiver.Instance);
            WriteInt32(buffer, 24, corpseIdentity.Instance);
            WriteSingle(buffer, 45, target.RawCoordinates.X);
            WriteSingle(buffer, 49, target.RawCoordinates.Y);
            WriteSingle(buffer, 53, target.RawCoordinates.Z);
            WriteInt32(buffer, 73, target.Playfield.Identity.Instance);
            WriteInt32(buffer, 143, target.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, 159, target.Stats[StatIds.sex].Value);
            WriteInt32(buffer, 167, target.Stats[StatIds.breed].Value);
            WriteInt32(buffer, 175, target.Stats[StatIds.race].Value);
            int corpseCatMesh = CorpseCatMeshFor(target);
            int corpseMonsterData = CorpseMonsterDataFor(target);
            WriteInt32(buffer, 191, target.Identity.Instance);
            WriteInt32(buffer, 199, corpseCatMesh);
            WriteInt32(buffer, nameLengthOffset, encodedNameLength);
            WriteInt32(buffer, 330 + afterNameDelta, corpseMonsterData);
            WriteInt32(buffer, 342 + afterNameDelta, target.Identity.Instance);

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "CorpseFullUpdate visual target={0} corpse={1} catMesh={2} monsterData={3} scale={4} sex={5} breed={6} race={7}",
                    target.Identity,
                    corpseIdentity,
                    corpseCatMesh,
                    corpseMonsterData,
                    target.Stats[StatIds.monsterscale].Value,
                    target.Stats[StatIds.sex].Value,
                    target.Stats[StatIds.breed].Value,
                    target.Stats[StatIds.race].Value));

            return buffer;
        }

        private bool CanBuildKnownCorpseVisual(ICharacter target)
        {
            return CombatCorpseVisuals.IsUsableVisualId(target.Stats[StatIds.catmesh].Value)
                   || MonsterDataToCorpseCatMesh.ContainsKey(target.Stats[StatIds.monsterdata].Value);
        }

        private static int CorpseCatMeshFor(ICharacter target)
        {
            return CombatCorpseVisuals.CorpseCatMeshFor(
                target.Stats[StatIds.catmesh].Value,
                target.Stats[StatIds.monsterdata].Value,
                MonsterDataToCorpseCatMesh);
        }

        private static int DeathAnimationKeyFor(ICharacter target)
        {
            return CombatCorpseVisuals.DeathAnimationKeyFor(
                target.Stats[StatIds.corpseanimkey].Value,
                target.Stats[StatIds.itemanim].Value,
                DefaultNpcDeathAnimationKey);
        }

        private static int CorpseMonsterDataFor(ICharacter target)
        {
            return CombatCorpseVisuals.CorpseMonsterDataFor(
                target.Stats[StatIds.monsterdata].Value,
                CorpseCatMeshFor(target));
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WritePacketLength(byte[] buffer, int length)
        {
            buffer[6] = (byte)((length >> 8) & 0xff);
            buffer[7] = (byte)(length & 0xff);
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WriteFixedAscii(byte[] buffer, int offset, int length, string value)
        {
            for (int i = 0; i < length; i++)
            {
                buffer[offset + i] = 0;
            }

            int count = Math.Min(value.Length, length - 1);
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = (byte)value[i];
            }
        }

        private void StopFightingDeadTarget(Identity deadTarget)
        {
            foreach (ICharacter character in
                Pool.Instance.GetAll<ICharacter>(this.Identity, (int)IdentityType.CanbeAffected))
            {
                if (character.FightingTarget == deadTarget)
                {
                    character.SetFightingTarget(Identity.None);
                    this.nextCombatTicks.Remove(character.Identity.Instance);
                    this.SendCombatStopMessage(character);
                }
            }
        }

        private void SendCombatStopMessage(ICharacter character)
        {
            var stopFight = new StopFightMessage { Identity = character.Identity, Unknown1 = 1 };

            this.Announce(stopFight);
        }

        private static List<DebugCorpseLootItem> RollDebugCorpseLootItems(ICharacter target)
        {
            var lootItems = new List<DebugCorpseLootItem>();

            foreach (CombatLootTableEntry entry in DebugLootTable.Where(
                x => x.Matches(
                    target.Name,
                    target.Stats[StatIds.monsterdata].Value,
                    target.Stats[StatIds.npcfamily].Value)))
            {
                if (!RollLootChance(entry.DropChancePercent))
                {
                    continue;
                }

                Item item = CreateLootItem(entry.ItemTemplateIds, entry.Quality);
                if (item == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "Loot roll skipped invalid item target={0} name={1}",
                            target.Identity,
                            target.Name));
                    continue;
                }

                lootItems.Add(new DebugCorpseLootItem { Slot = lootItems.Count, Item = item });
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Loot rolled target={0} name={1} monsterData={2} npcFamily={3} items={4}",
                    target.Identity,
                    target.Name,
                    target.Stats[StatIds.monsterdata].Value,
                    target.Stats[StatIds.npcfamily].Value,
                    lootItems.Count));

            return lootItems;
        }

        private static bool RollLootChance(int dropChancePercent)
        {
            if (dropChancePercent <= 0)
            {
                return false;
            }

            if (dropChancePercent >= 100)
            {
                return true;
            }

            lock (LootRandomLock)
            {
                return CombatCorpseRules.ShouldDrop(dropChancePercent, max => LootRandom.Next(max));
            }
        }

        private static Item CreateLootItem(IEnumerable<int> templateIds, int requestedQuality)
        {
            if (templateIds == null)
            {
                return null;
            }

            int quality = Math.Max(1, requestedQuality);
            foreach (int templateId in templateIds)
            {
                ItemTemplate template;
                if (!ItemLoader.ItemList.TryGetValue(templateId, out template))
                {
                    continue;
                }

                int lowId = template.GetLowId(quality);
                if (lowId == -1)
                {
                    lowId = templateId;
                }

                int highId = template.GetHighId(quality);
                if (highId == 1234567890)
                {
                    highId = lowId;
                }

                if (!ItemLoader.ItemList.ContainsKey(lowId) || !ItemLoader.ItemList.ContainsKey(highId))
                {
                    continue;
                }

                var item = new Item(quality, lowId, highId) { MultipleCount = 1 };
                return item;
            }

            return null;
        }

        private static DebugCorpseLootItem FindCorpseLootItem(DebugCorpseState corpse, int requestedLootSlot)
        {
            return CombatCorpseRules.FindLootItem(
                corpse.LootItems,
                requestedLootSlot,
                x => x.Slot,
                x => x.Looted);
        }

        private bool TryResolveLootTargetSlot(
            ICharacter looter,
            int targetPlacement,
            out int targetPageNumber,
            out int targetSlot)
        {
            targetPageNumber = -1;
            targetSlot = -1;

            if (targetPlacement == CombatCorpseRules.MoveToInventoryPlacement)
            {
                targetPageNumber = looter.BaseInventory.StandardPage;
                IInventoryPage targetPage = looter.BaseInventory.Pages[targetPageNumber];
                targetSlot = targetPage.FindFreeSlot();
                return targetSlot >= 0;
            }

            try
            {
                IInventoryPage targetPage = looter.BaseInventory.PageFromSlot(targetPlacement);
                if (targetPage == null)
                {
                    return false;
                }

                foreach (KeyValuePair<int, IInventoryPage> page in looter.BaseInventory.Pages)
                {
                    if (object.ReferenceEquals(page.Value, targetPage))
                    {
                        targetPageNumber = page.Key;
                        targetSlot = targetPlacement;
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                targetPageNumber = looter.BaseInventory.StandardPage;
                IInventoryPage targetPage = looter.BaseInventory.Pages[targetPageNumber];
                targetSlot = targetPage.FindFreeSlot();
                return targetSlot >= 0;
            }
        }

        private static InventoryEntry CreateCorpseInventoryEntry(DebugCorpseLootItem lootItem)
        {
            return new InventoryEntry
            {
                Slotnumber = lootItem.Slot,
                UnknownFlags = 0x00A1,
                Unknown1 = InventoryEntryCountFor(lootItem.Item),
                Identity = Identity.None,
                LowId = lootItem.Item.LowID,
                HighId = lootItem.Item.HighID,
                Quality = lootItem.Item.Quality,
                Unknown2 = 0
            };
        }

        private static short InventoryEntryCountFor(Item item)
        {
            return CombatCorpseRules.InventoryEntryCountFor(item.MultipleCount);
        }

        private void SendCorpseInventoryUpdate(ICharacter looter, DebugCorpseState corpse)
        {
            if (looter.Controller.Client == null)
            {
                return;
            }

            InventoryEntry[] entries = corpse.LootItems == null
                ? new InventoryEntry[0]
                : corpse.LootItems.Where(x => !x.Looted).Select(CreateCorpseInventoryEntry).ToArray();

            corpse.InventorySlot = this.AllocateDebugCorpseInventorySlot();

            looter.Controller.Client.SendCompressed(
                new InventoryUpdateMessage
                {
                    Identity = looter.Identity,
                    Unknown = 1,
                    NumberOfSlots = CombatCorpseRules.CorpseInventorySlots,
                    Unknown1 = 2,
                    Entries = entries,
                    BagIdentity = corpse.CorpseIdentity,
                    SlotnumberInMainInventory = corpse.InventorySlot,
                    Unknown2 = 1
                });

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse InventoryUpdate sent looter={0} corpse={1} slots={2} unknown1=2 slot={3} unknown2=1 entries={4}",
                    looter.Identity,
                    corpse.CorpseIdentity,
                    CombatCorpseRules.CorpseInventorySlots,
                    corpse.InventorySlot,
                    entries.Length));
        }

        private void SendCorpseLootAccessAction(ICharacter looter, DebugCorpseState corpse)
        {
            if (looter.Controller.Client == null)
            {
                return;
            }

            looter.Controller.Client.SendCompressed(
                new ActionMessage
                {
                    Identity = corpse.CorpseIdentity,
                    Unknown = 1,
                    ActionCode = 1,
                    ActionIdentity = 0x66,
                    Target = looter.Identity
                });

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse loot access Action sent looter={0} corpse={1} action=0x66",
                    looter.Identity,
                    corpse.CorpseIdentity));
        }

        private void SendUseActionFinished(ICharacter character)
        {
            if (character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.UseActionFinished,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        private void SendTargetClearMessage(ICharacter character)
        {
            var lookAt = new LookAtMessage { Identity = character.Identity, Target = Identity.None };

            if (character.Controller.Client != null)
            {
                character.Controller.Client.SendCompressed(lookAt);
            }

            this.Announce(lookAt);
        }

        private void SendCombatIdleState(ICharacter character)
        {
            character.Stats[StatIds.state].Value = 0;
            character.Stats[StatIds.currentstate].Value = 0;
            character.Stats[StatIds.actioncategory].Value = 0;

            if (character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.state,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.currentstate,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.actioncategory,
                                Value2 = 0
                            }
                        }
                });
            character.Controller.Client.SendCompressed(SimpleCharFullUpdate.ConstructMessage((Character)character));
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    // We wont save any NPCs to character table/character's stats table
                    this.DisconnectAllClients();
                    if (this.memBusDisposeContainer != null)
                    {
                        this.memBusDisposeContainer.Dispose();
                    }
                    if (this.heartBeat != null)
                    {
                        this.heartBeat.Dispose();
                    }
                }
            }
            this.disposed = true;

            base.Dispose(disposing);
        }
    }
}
