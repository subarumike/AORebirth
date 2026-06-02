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
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
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
        private readonly Dictionary<int, int> lastCombatWeaponSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, DateTime> deadNpcDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, NpcHomeState> npcHomeStates = new Dictionary<int, NpcHomeState>();

        private readonly Dictionary<int, DateTime> corpseDespawnTicks = new Dictionary<int, DateTime>();

        private readonly Dictionary<int, CorpseState> corpses = new Dictionary<int, CorpseState>();

        private readonly Dictionary<int, CorpseState> pendingCorpseSpawns = new Dictionary<int, CorpseState>();

        private int nextCorpseInstance = 0x00F0F000;

        private int nextCorpseInventorySlot = 0x70;

        private const int DefaultNpcDeathAnimationKey = 0x1F7;

        private const int DefaultPlayerDeathAnimationKey = 500;

        private const int DeathRespawnActionParameter1 = 1000020;

        private const int DeathRespawnActionParameter2 = 295830;

        private static readonly TimeSpan DeadNpcDespawnDelay = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan CorpseSpawnDelay = TimeSpan.FromMilliseconds(600);

        private const double MaxMeleeCombatDistance = 8.0;

        private const double MaxMeleeFollowHoldDistance = 3.0;

        private const int MissingItemStatValue = 1234567890;

        private const double DefaultCombatTickSeconds = 2.0;

        private const double OutOfRangeRetrySeconds = 1.0;

        private const int RubiKaStartPlayfield = 4582;

        private const int RubiKaStartX = 939;

        private const int RubiKaStartY = 20;

        private const int RubiKaStartZ = 732;

        private const int ShadowlandsStartPlayfield = 4001;

        private const int ShadowlandsStartX = 850;

        private const int ShadowlandsStartY = 43;

        private const int ShadowlandsStartZ = 565;

        private static readonly Random LootRandom = new Random();

        private static readonly object LootRandomLock = new object();

        private static readonly CombatLootTableEntry[] DebugLootTable = CombatTestLootCatalog.BuildEntries();

        private static readonly object DatabaseLootTableLock = new object();

        private static CombatLootTableEntry[] databaseLootTable = new CombatLootTableEntry[0];

        private static bool databaseLootTableLoaded;

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

        private Coordinate DynelDropPosition(Identity identity)
        {
            IDynel dynel = Pool.Instance.GetObject<IDynel>(identity);
            return dynel != null ? dynel.Coordinates() : new Coordinate();
        }

        public void DespawnNpcImmediately(ICharacter target)
        {
            if (target == null || target.Identity.Type != IdentityType.CanbeAffected)
            {
                return;
            }

            this.StopFightingDeadTarget(target.Identity);
            this.pendingCorpseSpawns.Remove(target.Identity.Instance);
            this.FinalizeNpcDespawn(target);
        }

        public void RegisterNpcHome(ICharacter character)
        {
            if (character == null || !(character.Controller is NPCController))
            {
                return;
            }

            this.npcHomeStates[character.Identity.Instance] =
                new NpcHomeState
                {
                    Coordinates = new Coordinate(character.Coordinates())
                };
        }

        public int DespawnCorpses(Func<string, Identity, bool> shouldDespawn)
        {
            if (shouldDespawn == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (CorpseState corpse in this.pendingCorpseSpawns
                .Where(x => shouldDespawn(x.Value.Name, x.Value.DeadNpcIdentity))
                .Select(x => x.Value)
                .ToList())
            {
                this.pendingCorpseSpawns.Remove(corpse.DeadNpcIdentity.Instance);
                removed++;
            }

            foreach (int corpseInstance in this.corpses
                .Where(x => shouldDespawn(x.Value.Name, x.Value.DeadNpcIdentity))
                .Select(x => x.Key)
                .ToList())
            {
                this.DespawnCorpse(corpseInstance);
                removed++;
            }

            return removed;
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
            try
            {
                this.ProcessPendingCorpseSpawns();
                this.ProcessCorpseDespawns();

                IEnumerable<IEntity> dynels = null;
                dynels =
                    Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                        .Where(
                            xx =>
                                xx.InPlayfield(this.Identity)
                                && (!xx.DoNotDoTimers || this.deadNpcDespawnTicks.ContainsKey(xx.Identity.Instance)))
                        .ToList();

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
                        int healIntervalSeconds = healInterval.Value;
                        int healDelta = dynel.Stats[StatIds.healdelta].Value;
                        if (healIntervalSeconds > 0
                            && healDelta != 0
                            && healInterval.LastTick < DateTime.UtcNow)
                        {
                            dynel.Stats[StatIds.health].Value =
                                Math.Min(dynel.Stats[StatIds.life].Value, dynel.Stats[StatIds.health].Value + healDelta);
                            healInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(healIntervalSeconds);
                            changed = true;
                        }

                        StatNanoInterval nanoInterval = (StatNanoInterval)dynel.Stats[StatIds.nanointerval];
                        int nanoIntervalSeconds = nanoInterval.Value;
                        int nanoDelta = dynel.Stats[StatIds.nanodelta].Value;
                        if (nanoIntervalSeconds > 0
                            && nanoDelta != 0
                            && nanoInterval.LastTick < DateTime.UtcNow)
                        {
                            dynel.Stats[StatIds.currentnano].Value += nanoDelta;
                            nanoInterval.LastTick = DateTime.UtcNow + TimeSpan.FromSeconds(nanoIntervalSeconds);
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
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e, false, "Playfield heartbeat failed for {0}", this.Identity);
            }
            finally
            {
                try
                {
                    this.heartBeat.Change(10, 0);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        public void ResetCombatTick(Identity attacker)
        {
            this.nextCombatTicks.Remove(attacker.Instance);
        }

        public void RespawnPlayer(ICharacter character)
        {
            if (character == null)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Player death respawn skipped: character=null.");
                return;
            }

            if (!(character.Controller is PlayerController))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Player death respawn skipped: controller={0} character={1}",
                        character.Controller == null ? "null" : character.Controller.GetType().FullName,
                        character.Identity));
                return;
            }

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Player death respawn skipped: character is not Dynel character={0}",
                        character.Identity));
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death respawn entered target={0} pf={1}",
                    character.Identity,
                    this.Identity));

            Coordinate destination;
            Identity destinationPlayfield;
            this.ResolvePlayerRespawnLocation(character, out destination, out destinationPlayfield);

            Identity corpseIdentity = this.AllocateCorpseIdentity();
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player corpse visual skipped target={0} corpse={1}; current CorpseFullUpdate template is NPC-loot oriented and breaks modern death teleport flow.",
                    character.Identity,
                    corpseIdentity));
            this.SendDeathSocialStatus(character);
            this.MarkPlayerRespawned(character);
            this.SendDeathRespawnStateStats(character);
            character.StopMovement();
            character.SetTarget(Identity.None);
            character.SetFightingTarget(Identity.None);
            this.nextCombatTicks.Remove(character.Identity.Instance);
            this.lastCombatWeaponSlots.Remove(character.Identity.Instance);
            this.StopFightingDeadTarget(character.Identity);
            this.SendCombatStopMessage(character);
            character.SendChangedStats();

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death respawn requested target={0} corpse={1} destination={2}:{3} pos={4:0.00},{5:0.00},{6:0.00}",
                    character.Identity,
                    corpseIdentity,
                    destinationPlayfield.Type,
                    destinationPlayfield.Instance,
                    destination.x,
                    destination.y,
                    destination.z));

            character.DoNotDoTimers = false;
            if (this.TryCompleteDeathRespawnInCurrentPlayfield(dynel, destination, character.RawHeading, destinationPlayfield))
            {
                return;
            }

            this.Teleport(dynel, destination, character.RawHeading, destinationPlayfield);
        }

        private bool TryCompleteDeathRespawnInCurrentPlayfield(
            Dynel dynel,
            Coordinate destination,
            IQuaternion heading,
            Identity destinationPlayfield)
        {
            if (destinationPlayfield.Type != this.Identity.Type || destinationPlayfield.Instance != this.Identity.Instance)
            {
                return false;
            }

            ICharacter character = dynel as ICharacter;
            ZoneClient client = dynel.Controller == null ? null : dynel.Controller.Client as ZoneClient;
            if (character == null || client == null)
            {
                return false;
            }

            TeleportMessageHandler.Default.Send(
                character,
                destination.coordinate,
                new CellAO.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf),
                destinationPlayfield);

            dynel.RawCoordinates = new CellAO.Core.Vector.Vector3
                                   {
                                       x = destination.x,
                                       y = destination.y,
                                       z = destination.z
                                   };
            dynel.RawHeading = new CellAO.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);

            PlayfieldAnarchyFMessageHandler.Default.Send(character);
            SimpleCharFullUpdate.SendToPlayfield(client);
            this.SendDeathSocialStatus(character);
            this.SendDeathRespawnStateStats(character);

            var sendSCFUs = new IMSendPlayerSCFUs { toClient = client };
            this.SendSCFUsToClient(sendSCFUs);

            foreach (StaticDynel staticDynel in Pool.Instance.GetAll<StaticDynel>(this.Identity))
            {
                SimpleItemFullUpdateMessageHandler.Default.Send(character, staticDynel);
            }

            WeaponItemFullUpdate.SendWeaponDefinitions(character);
            this.SendDeathRespawnGameTime(character);
            this.SendDeathSocialStatus(character);
            FullCharacterMessageHandler.Default.Send(character);
            this.SendDeathRespawnPlayfieldReadyBlock(client, character);
            this.SendDeathRespawnAction(character);
            ClientMoveItemToInventoryMessageHandler.EnsureWeaponVisualMeshes(character, false);
            AppearanceUpdateMessageHandler.Default.Send(character);

            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death respawn completed in current playfield target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}",
                    character.Identity,
                    destinationPlayfield.Type,
                    destinationPlayfield.Instance,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private void SendDeathRespawnGameTime(ICharacter character)
        {
            character.Send(
                new GameTimeMessage
                {
                    Identity = character.Identity,
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                },
                false);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death respawn game time target={0}",
                    character.Identity));
        }

        private void DoCombatTick(ICharacter attacker)
        {
            if (attacker.FightingTarget.Instance == 0)
            {
                this.nextCombatTicks.Remove(attacker.Identity.Instance);
                this.lastCombatWeaponSlots.Remove(attacker.Identity.Instance);
                return;
            }

            ICharacter target = this.FindByIdentity<ICharacter>(attacker.FightingTarget);
            if (target == null || !target.InPlayfield(this.Identity) || target.Stats[StatIds.health].Value <= 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    string.Format(
                        "CombatTickTargetInvalid attacker={0} target={1} found={2} inPlayfield={3} health={4}",
                        attacker.Identity,
                        attacker.FightingTarget,
                        target != null,
                        target != null && target.InPlayfield(this.Identity),
                        target == null ? 0 : target.Stats[StatIds.health].Value));
                if (attacker.Controller is NPCController)
                {
                    double invalidDistance = target == null
                                                 ? -1.0
                                                 : GetCombatDistance(attacker, target);
                    LogNpcBrain("Idle", "target-invalid", attacker, target, 0.0, invalidDistance);
                }

                attacker.SetFightingTarget(Identity.None);
                this.nextCombatTicks.Remove(attacker.Identity.Instance);
                this.lastCombatWeaponSlots.Remove(attacker.Identity.Instance);
                return;
            }

            CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);
            if (attacker.Controller is NPCController
                && !this.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                return;
            }

            if (attacker.Controller is NPCController && !ShouldNpcStopForCombatAttack(attackSource))
            {
                this.UpdateNpcMeleeFollowHold(attacker, target, attackSource.Range);
            }

            DateTime nextTick;
            if (this.nextCombatTicks.TryGetValue(attacker.Identity.Instance, out nextTick)
                && nextTick > DateTime.UtcNow)
            {
                return;
            }

            if (!this.IsInCombatRange(attacker, target, attackSource.Range))
            {
                this.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);
                this.nextCombatTicks[attacker.Identity.Instance] =
                    DateTime.UtcNow + TimeSpan.FromSeconds(OutOfRangeRetrySeconds);
                return;
            }

            if (attacker.Controller is NPCController && ShouldNpcStopForCombatAttack(attackSource))
            {
                this.StopNpcFollowIfInCombatRange(attacker, target, attackSource.Range);
            }

            int currentHealth = target.Stats[StatIds.health].Value;
            int damage = this.CalculateCombatDamage(attacker, attackSource);
            int newHealth = Math.Max(0, currentHealth - damage);
            bool killingHit = newHealth == 0;

            target.Stats[StatIds.health].Value = newHealth;
            target.SendChangedStats();
            this.AnnounceCombatDamage(
                attacker,
                target,
                damage,
                attackSource,
                attackSource.UsesEquippedWeapon
                    ? CombatDamageSource.WeaponAutoAttack
                    : CombatDamageSource.UnarmedAutoAttack);
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Combat hit attacker={0} target={1} damage={2} health={3}/{4} weaponBased={5} slot={6}",
                    attacker.Identity,
                    target.Identity,
                    damage,
                    newHealth,
                    target.Stats[StatIds.life].Value,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attackSource.AttackInfoWeaponSlot));

            if (killingHit)
            {
                if (target.Controller is NPCController)
                {
                    this.KillNpcTarget(target);
                }
                else if (target.Controller is PlayerController)
                {
                    this.KillPlayerTarget(target);
                }
                else
                {
                    attacker.SetFightingTarget(Identity.None);
                    this.nextCombatTicks.Remove(attacker.Identity.Instance);
                    this.lastCombatWeaponSlots.Remove(attacker.Identity.Instance);
                }

                return;
            }

            this.nextCombatTicks[attacker.Identity.Instance] =
                DateTime.UtcNow + TimeSpan.FromSeconds(attackSource.RechargeSeconds);
        }

        private int CalculateCombatDamage(ICharacter attacker, CombatAttackSource attackSource)
        {
            return CombatDamageRules.Calculate(
                attackSource.MinDamage,
                attackSource.MaxDamage,
                attackSource.DamageBonus,
                attacker.Stats[StatIds.level].Value,
                attacker.Controller is PlayerController);
        }

        private bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            return GetCombatDistance(attacker, target) <= range;
        }

        private static CellAO.Core.Vector.Vector3 GetCombatPosition(ICharacter character)
        {
            if (character.Controller is PlayerController)
            {
                Vector3 raw = character.RawCoordinates;
                CellAO.Core.Vector.Vector3 rawPosition =
                    new CellAO.Core.Vector.Vector3(raw.X, raw.Y, raw.Z);
                CellAO.Core.Vector.Vector3 predictedPosition = character.Coordinates().coordinate;
                return MoveCombatPositionToward(
                    rawPosition,
                    predictedPosition,
                    EnemyBehaviorContract.MaxPlayerChaseProjectionDistance);
            }

            return character.Coordinates().coordinate;
        }

        private static CellAO.Core.Vector.Vector3 MoveCombatPositionToward(
            CellAO.Core.Vector.Vector3 start,
            CellAO.Core.Vector.Vector3 destination,
            double maxDistance)
        {
            double distance = start.Distance2D(destination);
            if (distance < 0.001 || maxDistance <= 0)
            {
                return new CellAO.Core.Vector.Vector3(start.x, start.y, start.z);
            }

            double step = Math.Min(distance, maxDistance);
            double factor = step / distance;
            return new CellAO.Core.Vector.Vector3(
                start.x + ((destination.x - start.x) * factor),
                start.y + ((destination.y - start.y) * factor),
                start.z + ((destination.z - start.z) * factor));
        }

        private static double GetCombatDistance(ICharacter attacker, ICharacter target)
        {
            return GetCombatPosition(attacker).Distance2D(GetCombatPosition(target));
        }

        private static void LogNpcBrain(string state, string reason, ICharacter attacker, ICharacter target, double range, double distance)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NPCBRAIN state={0} reason={1} npc={2} target={3} dist={4:0.00} range={5:0.00}",
                    state,
                    reason,
                    attacker.Identity.ToString(true),
                    target == null ? Identity.None.ToString(true) : target.Identity.ToString(true),
                    distance,
                    range));
        }

        private void StopNpcFollowIfInCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null || !npcController.IsFollowing())
            {
                return;
            }

            if (!this.IsInCombatRange(attacker, target, range))
            {
                return;
            }

            LogNpcBrain(
                "InRangeAttacking",
                "in-range",
                attacker,
                target,
                range,
                GetCombatDistance(attacker, target));
            npcController.StopFollowForCombatRange(GetCombatPosition(target));
        }

        private void AnnounceCombatDamage(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatAttackSource attackSource,
            CombatDamageSource source)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackInfoSend source={0} attacker={1} target={2} dmg={3} u2={4} u3={5} u4={6} u5={7} u6=0 weaponBased={8} atkDefault={9} atkDamageType={10} atkWeaponType={11} atkEquippedWeapons={12}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage,
                    attackSource.AttackInfoAmmoCount,
                    attackSource.AttackInfoWeaponSlot,
                    attackSource.AttackInfoUnk1,
                    attackSource.AttackInfoHitType,
                    attackSource.UsesEquippedWeapon ? 1 : 0,
                    attacker.Stats[StatIds.defaultattacktype].Value,
                    attacker.Stats[StatIds.damagetype].Value,
                    attacker.Stats[StatIds.weapontype].Value,
                    attacker.Stats[StatIds.equippedweapons].Value));

            this.Announce(
                new AttackInfoMessage
                {
                    Identity = attacker.Identity,
                    Target = target.Identity,
                    Unknown1 = damage,
                    Unknown2 = attackSource.AttackInfoAmmoCount,
                    Unknown3 = attackSource.AttackInfoWeaponSlot,
                    Unknown4 = attackSource.AttackInfoUnk1,
                    Unknown5 = attackSource.AttackInfoHitType,
                    Unknown6 = 0
                });

            this.AnnounceHealthDamageIfNeeded(attacker, target, damage, source);
        }

        private void AnnounceHealthDamageIfNeeded(
            ICharacter attacker,
            ICharacter target,
            int damage,
            CombatDamageSource source)
        {
            if (!ShouldSendHealthDamage(source))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatHealthDamageSkip source={0} attacker={1} target={2} dmg={3}",
                        source,
                        attacker.Identity,
                        target.Identity,
                        damage));
                return;
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatHealthDamageSend source={0} attacker={1} target={2} dmg={3}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage));

            this.Announce(
                new HealthDamageMessage
                {
                    Identity = attacker.Identity,
                    Unknown1 = damage,
                    Unknown2 = 0,
                    Unknown3 = 0,
                    Unknown4 = 0,
                    Target = target.Identity,
                    Unknown5 = 0
                });
        }

        private static bool ShouldSendHealthDamage(CombatDamageSource source)
        {
            // Keep normal weapon/unarmed auto-attacks as AttackInfo-only.
            return source != CombatDamageSource.WeaponAutoAttack
                   && source != CombatDamageSource.UnarmedAutoAttack;
        }

        private CombatAttackSource GetCombatAttackSource(ICharacter attacker)
        {
            EquippedCombatWeapon equippedWeapon = this.GetEquippedCombatWeapon(attacker);
            if (equippedWeapon == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        "CombatAttackSource unarmed attacker={0} mindmg={1} maxdmg={2} bonus={3} defaultattack={4} damagetype={5} weapontype={6} equippedweapons={7}",
                        attacker.Identity,
                        attacker.Stats[StatIds.mindamage].Value,
                        attacker.Stats[StatIds.maxdamage].Value,
                        attacker.Stats[StatIds.damagebonus].Value,
                        attacker.Stats[StatIds.defaultattacktype].Value,
                        attacker.Stats[StatIds.damagetype].Value,
                        attacker.Stats[StatIds.weapontype].Value,
                        attacker.Stats[StatIds.equippedweapons].Value));
                bool isNpcAttacker = attacker.Controller is NPCController;
                return new CombatAttackSource
                       {
                           MinDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0),
                           MaxDamage = NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0),
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = MaxMeleeCombatDistance,
                           RechargeSeconds = DefaultCombatTickSeconds,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = 0,
                           AttackInfoWeaponSlot = 0,
                           AttackInfoUnk1 = 0,
                           AttackInfoHitType = isNpcAttacker ? 3 : 0
                        };
            }

            IItem weapon = equippedWeapon.Item;
            int minDamage = NormalizeCombatItemStat(weapon.GetAttribute((int)StatIds.mindamage), 0);
            int maxDamage = NormalizeCombatItemStat(weapon.GetAttribute((int)StatIds.maxdamage), 0);
            int damageBonus = NormalizeCombatItemStat(weapon.GetAttribute((int)StatIds.damagebonus), 0);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CombatAttackSource weapon attacker={0} item={1}/{2} slot={3} min={4} max={5} rangeRaw={6}",
                    attacker.Identity,
                    weapon.LowID,
                    weapon.HighID,
                    equippedWeapon.Slot,
                    minDamage,
                    maxDamage,
                    weapon.GetAttribute((int)StatIds.attackrange)));

            return new CombatAttackSource
                   {
                       MinDamage = minDamage,
                       MaxDamage = maxDamage,
                       DamageBonus = damageBonus,
                       Range = NormalizeCombatRange(weapon.GetAttribute((int)StatIds.attackrange)),
                       RechargeSeconds = NormalizeCombatDelaySeconds(
                           weapon.GetAttribute((int)StatIds.itemdelay),
                           weapon.GetAttribute((int)StatIds.rechargedelay)),
                       UsesEquippedWeapon = true,
                       AttackInfoAmmoCount = 40,
                       AttackInfoWeaponSlot = equippedWeapon.Slot,
                       AttackInfoUnk1 = 4,
                       AttackInfoHitType = 3
                    };
        }

        private EquippedCombatWeapon GetEquippedCombatWeapon(ICharacter attacker)
        {
            if (attacker.BaseInventory == null
                || !attacker.BaseInventory.Pages.ContainsKey((int)IdentityType.WeaponPage))
            {
                this.lastCombatWeaponSlots.Remove(attacker.Identity.Instance);
                return null;
            }

            IInventoryPage weaponPage = attacker.BaseInventory.Pages[(int)IdentityType.WeaponPage];
            IItem rightHand = weaponPage[(int)WeaponSlots.Righthand];
            IItem leftHand = weaponPage[(int)WeaponSlots.LeftHand];
            bool rightHandUsable = this.IsWieldableCombatWeapon(rightHand);
            bool leftHandUsable = this.IsWieldableCombatWeapon(leftHand);

            if (rightHandUsable && leftHandUsable)
            {
                int attackerInstance = attacker.Identity.Instance;
                int lastSlot;
                if (this.lastCombatWeaponSlots.TryGetValue(attackerInstance, out lastSlot)
                    && lastSlot == (int)WeaponSlots.Righthand)
                {
                    this.lastCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.LeftHand;
                    return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
                }

                this.lastCombatWeaponSlots[attackerInstance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (rightHandUsable)
            {
                this.lastCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.Righthand;
                return new EquippedCombatWeapon { Item = rightHand, Slot = (int)WeaponSlots.Righthand };
            }

            if (leftHandUsable)
            {
                this.lastCombatWeaponSlots[attacker.Identity.Instance] = (int)WeaponSlots.LeftHand;
                return new EquippedCombatWeapon { Item = leftHand, Slot = (int)WeaponSlots.LeftHand };
            }

            this.lastCombatWeaponSlots.Remove(attacker.Identity.Instance);
            return null;
        }

        private static int NormalizeCombatItemStat(int value, int fallback)
        {
            return value == MissingItemStatValue ? fallback : value;
        }

        private bool IsWieldableCombatWeapon(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.ItemActions != null && item.ItemActions.Any(x => x.ActionType == ActionType.ToWield))
            {
                return true;
            }

            // Some valid hand weapons in stripped/incomplete datasets are missing explicit ToWield actions.
            // Fall back to combat-bearing item stats to keep equipped hand weapons from being treated as fists.
            return NormalizeCombatItemStat(item.GetAttribute((int)StatIds.mindamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.maxdamage), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.attackrange), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.itemdelay), 0) > 0
                   || NormalizeCombatItemStat(item.GetAttribute((int)StatIds.rechargedelay), 0) > 0;
        }

        private static double NormalizeCombatRange(int range)
        {
            int normalizedRange = NormalizeCombatItemStat(range, 0);
            if (normalizedRange <= 0)
            {
                return MaxMeleeCombatDistance;
            }

            return normalizedRange > 1000 ? normalizedRange / 100.0 : normalizedRange;
        }

        private static double NormalizeCombatDelaySeconds(int attackDelay, int rechargeDelay)
        {
            int normalizedAttackDelay = NormalizeCombatItemStat(attackDelay, 0);
            int normalizedRechargeDelay = NormalizeCombatItemStat(rechargeDelay, 0);
            int totalCentiseconds = normalizedAttackDelay + normalizedRechargeDelay;

            if (totalCentiseconds <= 0)
            {
                return DefaultCombatTickSeconds;
            }

            return Math.Max(0.25, totalCentiseconds / 100.0);
        }

        private static double BuildNpcCombatFollowStopDistance(double range)
        {
            return range > MaxMeleeCombatDistance ? range : 0.0;
        }

        private static bool ShouldNpcStopForCombatAttack(CombatAttackSource attackSource)
        {
            return attackSource.Range > MaxMeleeCombatDistance;
        }

        private void UpdateNpcMeleeFollowHold(ICharacter attacker, ICharacter target, double range)
        {
            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            double distance = GetCombatDistance(attacker, target);
            bool closeEnoughToHold = distance <= MaxMeleeFollowHoldDistance;
            npcController.SuppressMotionSegmentUpdates(closeEnoughToHold);
            if (closeEnoughToHold || npcController.IsFollowing())
            {
                return;
            }

            LogNpcBrain(
                "Chasing",
                "melee-separated",
                attacker,
                target,
                range,
                distance);
            npcController.Follow(target.Identity, BuildNpcCombatFollowStopDistance(range));
        }

        private void TryMoveNpcIntoCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            NPCController npcController = attacker.Controller as NPCController;
            if (npcController == null)
            {
                return;
            }

            npcController.SuppressMotionSegmentUpdates(false);
            if (npcController.IsFollowing())
            {
                return;
            }

            LogNpcBrain(
                "Chasing",
                "out-of-range",
                attacker,
                target,
                range,
                GetCombatDistance(attacker, target));
            npcController.Follow(target.Identity, BuildNpcCombatFollowStopDistance(range));
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
                corpseIdentity = this.AllocateCorpseIdentity();
            }

            this.MarkNpcDead(target);
            this.StopFightingDeadTarget(target.Identity);
            this.SendNpcDeathAnimation(target);
            if (corpseIdentity != Identity.None)
            {
                this.ScheduleCorpseSpawn(target, corpseIdentity);
            }
            else
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("Skipping corpse visual spawn for {0}; no known MonsterData-to-CATMesh mapping.", target.Identity));
            }

            this.deadNpcDespawnTicks[target.Identity.Instance] = DateTime.UtcNow + DeadNpcDespawnDelay;

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC died target={0}", target.Identity));
        }

        private void KillPlayerTarget(ICharacter target)
        {
            if (!(target.Controller is PlayerController))
            {
                return;
            }

            this.MarkPlayerDead(target);
            target.SendChangedStats();
            target.SetTarget(Identity.None);
            target.SetFightingTarget(Identity.None);
            this.nextCombatTicks.Remove(target.Identity.Instance);
            this.lastCombatWeaponSlots.Remove(target.Identity.Instance);
            this.StopFightingDeadTarget(target.Identity);
            this.SendCombatStopMessage(target);
            this.SendPlayerDeathAnimation(target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("Player died target={0}", target.Identity));
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

            CorpseState corpse;
            if (!this.corpses.TryGetValue(corpseIdentity.Instance, out corpse))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "CorpseUse reject unknown corpse={0} looter={1} registeredCount={2}",
                        corpseIdentity,
                        looter.Identity,
                        this.corpses.Count));
                return false;
            }

            if (corpse.ExpiresAtUtc <= DateTime.UtcNow)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format("CorpseUse reject expired corpse={0} looter={1}", corpseIdentity, looter.Identity));
                this.DespawnCorpse(corpseIdentity.Instance);
                return false;
            }

            bool wasOpened = corpse.Opened;
            corpse.Opened = true;

            if (corpse.HasUnlootedItems)
            {
                this.ExtendCorpseLifetime(corpse, CombatCorpseRules.ItemLootCorpseLifetime, "corpse-use");
                if (corpse.NextUseSendsAccessActionOnly)
                {
                    this.SendCorpseLootAccessAction(looter, corpse);
                    this.SendUseActionFinished(looter);
                    corpse.NextUseSendsAccessActionOnly = false;
                }
                else
                {
                    this.SendCorpseInventoryUpdateAndCredits(looter, corpse);
                    corpse.NextUseSendsAccessActionOnly = true;
                }
            }
            else if (!wasOpened)
            {
                this.SendCorpseInventoryUpdateAndCredits(looter, corpse);
            }
            else
            {
                this.SendUseActionFinished(looter);
            }

            if (!corpse.HasUnlootedItems)
            {
                this.ScheduleCorpseDespawn(
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

            CorpseState corpse = this.corpses.Values
                .Where(
                    x => x.DeadNpcIdentity.Type == deadNpcIdentity.Type
                         && x.DeadNpcIdentity.Instance == deadNpcIdentity.Instance)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.CorpseIdentity.Instance)
                .FirstOrDefault();

            if (corpse == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "DeadNpcCorpseUse reject unknown deadNpc={0} looter={1} registeredCount={2}",
                        deadNpcIdentity,
                        looter.Identity,
                        this.corpses.Count));
                return false;
            }

            corpseIdentity = corpse.CorpseIdentity;
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "DeadNpcCorpseUse route deadNpc={0} corpse={1} looter={2} created={3:o}",
                    deadNpcIdentity,
                    corpseIdentity,
                    looter.Identity,
                    corpse.CreatedAtUtc));
            return this.TryUseCorpse(looter, corpse.CorpseIdentity);
        }

        public bool TryLootCorpseItem(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement)
        {
            if (looter == null || sourceContainer.Type != IdentityType.Backpack)
            {
                return false;
            }

            int corpseInventorySlot = (sourceContainer.Instance >> 16) & 0xffff;
            CorpseState corpse = this.corpses.Values.FirstOrDefault(
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
                this.DespawnCorpse(corpse.CorpseIdentity.Instance);
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
            CorpseLootItem lootItem = FindCorpseLootItem(corpse, requestedLootSlot);
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

            looter.BaseInventory.Write();
            lootItem.Looted = true;
            corpse.Opened = true;
            // Ack the concrete destination slot. Echoing 0x6F here is interpreted by the
            // current client as a credits amount side-effect in loot feedback text.
            ContainerAddItemMessageHandler.Default.Send(
                looter,
                sourceContainer,
                targetSlot);
            if (looter.Controller != null)
            {
                uint authoritativeCash = looter.Stats[StatIds.cash].BaseValue;
                StatMessageHandler.Default.SendSingle(looter, (int)StatIds.cash, authoritativeCash);
                looter.Stats[StatIds.cash].Changed = false;
            }

            if (!corpse.HasUnlootedItems)
            {
                this.ScheduleCorpseDespawn(
                    corpse,
                    CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay,
                    "looted-empty");
            }
            else
            {
                this.ExtendCorpseLifetime(corpse, CombatCorpseRules.ItemLootCorpseLifetime, "loot-remaining");
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CorpseLoot accepted corpse={0} looter={1} source={2} lootSlot={3} targetSlot={4} ackPlacement={5} cashResync={6} remaining={7}",
                    corpse.CorpseIdentity,
                    looter.Identity,
                    sourceContainer,
                    lootItem.Slot,
                    targetSlot,
                    targetSlot,
                    looter.Stats[StatIds.cash].BaseValue,
                    corpse.LootItems.Count(x => !x.Looted)));

            return true;
        }

        private static bool CharacterHasUniqueItemAlready(ICharacter character, IItem item)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            return InventoryItemRules.HasSameUniqueItem(
                item,
                character.BaseInventory.Pages.Values.SelectMany(page => page.List()).Select(existing => existing.Value));
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
        }

        private void MarkPlayerDead(ICharacter target)
        {
            target.Stats[StatIds.health].Value = 0;
            target.Stats[StatIds.state].Value = 0;
            target.Stats[StatIds.currentstate].Value = 0;
            target.Stats[StatIds.actioncategory].Value = 0;
            target.Stats[StatIds.deadtimer].Value = 1;
            target.Stats[StatIds.healdelta].Value = 0;
            target.Stats[StatIds.nanodelta].Value = 0;
        }

        private void MarkPlayerRespawned(ICharacter target)
        {
            target.CalculateSkills();
            int maxHealth = Math.Max(1, target.Stats[StatIds.life].Value);
            target.Stats[StatIds.health].Value = Math.Max(1, maxHealth / 3);
            target.Stats[StatIds.state].Value = 0;
            target.Stats[StatIds.currentstate].Value = 0;
            target.Stats[StatIds.actioncategory].Value = 0;
            target.Stats[StatIds.deadtimer].Value = 0;
            target.Stats[StatIds.deadtimer].BaseValue = 0;
            target.Stats[StatIds.currentmovementmode].Value = (int)MoveModes.Run;
            target.Stats[StatIds.currentmovementmode].BaseValue = (uint)MoveModes.Run;
            target.Stats[StatIds.prevmovementmode].Value = (int)MoveModes.Run;
            target.Stats[StatIds.prevmovementmode].BaseValue = (uint)MoveModes.Run;
            target.Stats[StatIds.specialcondition].Value = 3;
            target.Stats[StatIds.specialcondition].BaseValue = 3;
            target.Stats[StatIds.damageoverridetype].Value = 0;
            target.Stats[StatIds.damageoverridetype].BaseValue = 0;
            target.Stats[StatIds.deathreason].Value = 0;
            target.Stats[StatIds.deathreason].BaseValue = 0;
        }

        private void SendDeathRespawnStateStats(ICharacter target)
        {
            target.Send(
                new StatMessage
                {
                    Identity = target.Identity,
                    Unknown = 0,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.Health,
                                Value2 = (uint)Math.Max(0, target.Stats[StatIds.health].Value)
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.CurrentNano,
                                Value2 = (uint)Math.Max(0, target.Stats[StatIds.currentnano].Value)
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.DeadTimer,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.state,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.CurrentState,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.ActionCategory,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.specialcondition,
                                Value2 = 3
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.damageoverridetype,
                                Value2 = 0
                            },
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.deathreason,
                                Value2 = 0
                            }
                        }
                },
                false);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death respawn state stats target={0} hp={1}/{2} nano={3} deadTimer=0",
                    target.Identity,
                    target.Stats[StatIds.health].Value,
                    target.Stats[StatIds.life].Value,
                    target.Stats[StatIds.currentnano].Value));
        }

        private void SendDeathSocialStatus(ICharacter target)
        {
            target.Send(
                new StatMessage
                {
                    Identity = target.Identity,
                    Unknown = 1,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.SocialStatus,
                                Value2 = 0
                            }
                        }
                },
                false);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player death social status target={0} socialStatus=0 unknown=1",
                    target.Identity));
        }

        private void ResolvePlayerRespawnLocation(
            ICharacter character,
            out Coordinate destination,
            out Identity destinationPlayfield)
        {
            ResolveStarterRespawnLocation(character, out destination, out destinationPlayfield);

            int savedPlayfield = character.Stats[StatIds.tempsaveplayfield].Value;
            int savedX = character.Stats[StatIds.tempsavex].Value;
            int savedY = character.Stats[StatIds.tempsavey].Value;
            if (savedPlayfield <= 0 || savedX <= 0 || savedY <= 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Network,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Player respawn using starter fallback target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}",
                        character.Identity,
                        destinationPlayfield.Type,
                        destinationPlayfield.Instance,
                        destination.x,
                        destination.y,
                        destination.z));
                return;
            }

            destination = new Coordinate(savedX, character.RawCoordinates.Y, savedY);
            destinationPlayfield = new Identity
                                   {
                                       Type = IdentityType.Playfield,
                                       Instance = savedPlayfield
                                   };

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player respawn using temp save target={0} destination={1}:{2} pos={3:0.00},{4:0.00},{5:0.00}",
                    character.Identity,
                    destinationPlayfield.Type,
                    destinationPlayfield.Instance,
                    destination.x,
                    destination.y,
                    destination.z));
        }

        private static void ResolveStarterRespawnLocation(
            ICharacter character,
            out Coordinate destination,
            out Identity destinationPlayfield)
        {
            int startPlayfield = RubiKaStartPlayfield;
            int startX = RubiKaStartX;
            int startY = RubiKaStartY;
            int startZ = RubiKaStartZ;

            if ((character != null)
                && (character.Playfield != null)
                && (character.Playfield.Identity.Instance == ShadowlandsStartPlayfield))
            {
                startPlayfield = ShadowlandsStartPlayfield;
                startX = ShadowlandsStartX;
                startY = ShadowlandsStartY;
                startZ = ShadowlandsStartZ;
            }

            destination = new Coordinate(startX, startY, startZ);
            destinationPlayfield = new Identity
                                   {
                                       Type = IdentityType.Playfield,
                                       Instance = startPlayfield
                                   };
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

        private void SendPlayerDeathAnimation(ICharacter target)
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
                    Parameter2 = DefaultPlayerDeathAnimationKey,
                    Unknown2 = 0
                });
        }

        private void SendDeathRespawnAction(ICharacter character)
        {
            character.Send(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.DeathRespawn,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = DeathRespawnActionParameter1,
                    Parameter2 = DeathRespawnActionParameter2,
                    Unknown2 = 0
                },
                false);
        }

        private void SendDeathRespawnPlayfieldReadyBlock(ZoneClient client, ICharacter character)
        {
            var playfieldIdentity = new Identity
                                    {
                                        Type = IdentityType.Playfield2,
                                        Instance = this.Identity.Instance
                                    };

            client.SendCompressed(
                new PlayfieldAllTowersMessage
                {
                    Identity = playfieldIdentity,
                    Unknown1 = new TowerProxyBase[0]
                });

            client.SendCompressed(
                new PlayfieldAllCitiesMessage
                {
                    Identity = playfieldIdentity,
                    Payload = new byte[0]
                });

            client.SendCompressed(
                new SpecialAttackWeaponMessage
                {
                    Identity = character.Identity,
                    Specials = CreateDefaultPlayerSpecialAttacks(),
                    Unknown1 = 6,
                    Unknown2 = 6,
                    Unknown3 = 6,
                    Unknown4 = 6,
                    Unknown5 = 100
                });
        }

        private static SpecialAttack[] CreateDefaultPlayerSpecialAttacks()
        {
            return new[]
                   {
                       new SpecialAttack
                       {
                           Unknown1 = 0x0000AAC0,
                           Unknown2 = 0x00023569,
                           Unknown3 = 0x00000064,
                           Unknown4 = "MAAT"
                       },
                       new SpecialAttack
                       {
                           Unknown1 = 0x0000A431,
                           Unknown2 = 0x0000A430,
                           Unknown3 = 0x00000090,
                           Unknown4 = "DIIT"
                       },
                       new SpecialAttack
                       {
                           Unknown1 = 0x00011294,
                           Unknown2 = 0x00011295,
                           Unknown3 = 0x0000008E,
                           Unknown4 = "BRAW"
                       }
                   };
        }

        private void FinalizeNpcDespawn(ICharacter target)
        {
            target.DoNotDoTimers = true;
            this.nextCombatTicks.Remove(target.Identity.Instance);
            this.lastCombatWeaponSlots.Remove(target.Identity.Instance);
            this.deadNpcDespawnTicks.Remove(target.Identity.Instance);
            this.npcHomeStates.Remove(target.Identity.Instance);
            this.Despawn(target.Identity);
            Pool.Instance.RemoveObject((Character)target);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("NPC despawned target={0}", target.Identity));
        }

        private void SendPlayerCorpseFullUpdate(ICharacter target, Identity corpseIdentity)
        {
            this.SendCorpseFullUpdate(target, corpseIdentity);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player corpse visual sent target={0} corpse={1}",
                    target.Identity,
                    corpseIdentity));
        }

        private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)
        {
            int corpseCatMesh = CorpseCatMeshFor(target);
            int corpseMonsterData = CorpseMonsterDataFor(target);

            foreach (ICharacter character in
                Pool.Instance.GetAll<ICharacter>(this.Identity, (int)IdentityType.CanbeAffected))
            {
                ZoneClient client = character.Controller.Client as ZoneClient;
                if (client == null)
                {
                    continue;
                }

                client.SendCompressed(
                    CorpseFullUpdate.Build(
                        target,
                        corpseIdentity,
                        character.Identity,
                        this.server.Id,
                        corpseCatMesh,
                        corpseMonsterData));
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
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
        }

        private static TimeSpan CorpseLifetimeFor(CombatCorpseLootClass lootClass)
        {
            return CombatCorpseRules.LifetimeFor(lootClass);
        }

        private static CombatCorpseLootClass CorpseLootClassFor(ICharacter target, IList<CorpseLootItem> lootItems)
        {
            // Boss classification is intentionally conservative until we have capture-backed
            // identification rules for major encounter tiers.
            return CombatCorpseRules.LootClassFor(lootItems.Count, false);
        }

        private void ProcessCorpseDespawns()
        {
            foreach (int corpseInstance in this.corpseDespawnTicks
                .Where(x => x.Value <= DateTime.UtcNow)
                .Select(x => x.Key)
                .ToList())
            {
                this.DespawnCorpse(corpseInstance);
            }
        }

        private void ProcessPendingCorpseSpawns()
        {
            foreach (CorpseState corpse in this.pendingCorpseSpawns
                .Where(x => x.Value.SpawnsAtUtc <= DateTime.UtcNow)
                .Select(x => x.Value)
                .ToList())
            {
                this.pendingCorpseSpawns.Remove(corpse.DeadNpcIdentity.Instance);

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

                this.RegisterCorpse(target, corpse.CorpseIdentity);
                this.SendCorpseFullUpdate(target, corpse.CorpseIdentity);
            }
        }

        private void ScheduleCorpseSpawn(ICharacter target, Identity corpseIdentity)
        {
            DateTime spawnsAtUtc = DateTime.UtcNow + CorpseSpawnDelay;
            this.pendingCorpseSpawns[target.Identity.Instance] =
                new CorpseState
                {
                    CorpseIdentity = corpseIdentity,
                    DeadNpcIdentity = target.Identity,
                    Name = "Remains of " + target.Name,
                    LootClass = CombatCorpseLootClass.CreditsOnly,
                    CreatedAtUtc = DateTime.UtcNow,
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

        private void RegisterCorpse(ICharacter target, Identity corpseIdentity)
        {
            List<CorpseLootItem> lootItems = RollCorpseLootItems(target);
            CombatCorpseLootClass lootClass = CorpseLootClassFor(target, lootItems);
            int credits = RollCorpseCredits(target);
            TimeSpan lifetime = CorpseLifetimeFor(lootClass);
            DateTime expiresAtUtc = DateTime.UtcNow + lifetime;
            var state = new CorpseState
            {
                CorpseIdentity = corpseIdentity,
                DeadNpcIdentity = target.Identity,
                Name = "Remains of " + target.Name,
                LootClass = lootClass,
                CreatedAtUtc = DateTime.UtcNow,
                LootItems = lootItems,
                Credits = credits,
                InventorySlot = this.AllocateCorpseInventorySlot(),
                ExpiresAtUtc = expiresAtUtc
            };

            this.corpses[corpseIdentity.Instance] = state;
            this.corpseDespawnTicks[corpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse registered corpse={0} deadNpc={1} lifetimeSeconds={2} lootClass={3} credits={4}",
                    corpseIdentity,
                    target.Identity,
                    (int)lifetime.TotalSeconds,
                    state.LootClass,
                    state.Credits));
        }

        private void DespawnCorpse(int corpseInstance)
        {
            Identity corpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = corpseInstance };
            this.Despawn(corpseIdentity);
            this.corpseDespawnTicks.Remove(corpseInstance);
            this.corpses.Remove(corpseInstance);
            LogUtil.Debug(DebugInfoDetail.Engine, string.Format("Corpse despawned corpse={0}", corpseIdentity));
        }

        private void ScheduleCorpseDespawn(CorpseState corpse, TimeSpan delay, string reason)
        {
            DateTime expiresAtUtc = DateTime.UtcNow + delay;
            corpse.ExpiresAtUtc = expiresAtUtc;
            this.corpseDespawnTicks[corpse.CorpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse despawn scheduled corpse={0} delaySeconds={1} reason={2} remainingLoot={3}",
                    corpse.CorpseIdentity,
                    delay.TotalSeconds,
                    reason,
                    corpse.LootItems == null ? 0 : corpse.LootItems.Count(x => !x.Looted)));
        }

        private void ExtendCorpseLifetime(CorpseState corpse, TimeSpan minimumRemaining, string reason)
        {
            DateTime expiresAtUtc = DateTime.UtcNow + minimumRemaining;
            if (corpse.ExpiresAtUtc >= expiresAtUtc)
            {
                return;
            }

            corpse.ExpiresAtUtc = expiresAtUtc;
            this.corpseDespawnTicks[corpse.CorpseIdentity.Instance] = expiresAtUtc;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse lifetime extended corpse={0} minimumRemainingSeconds={1} reason={2} remainingLoot={3}",
                    corpse.CorpseIdentity,
                    minimumRemaining.TotalSeconds,
                    reason,
                    corpse.LootItems == null ? 0 : corpse.LootItems.Count(x => !x.Looted)));
        }

        private class CorpseState
        {
            public Identity CorpseIdentity { get; set; }

            public Identity DeadNpcIdentity { get; set; }

            public string Name { get; set; }

            public CombatCorpseLootClass LootClass { get; set; }

            public DateTime CreatedAtUtc { get; set; }

            public DateTime SpawnsAtUtc { get; set; }

            public DateTime ExpiresAtUtc { get; set; }

            public int InventorySlot { get; set; }

            public List<CorpseLootItem> LootItems { get; set; }

            public int Credits { get; set; }

            public bool CreditsLooted { get; set; }

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

        private class CorpseLootItem
        {
            public int Slot { get; set; }

            public Item Item { get; set; }

            public bool Looted { get; set; }
        }

        private Identity AllocateCorpseIdentity()
        {
            this.nextCorpseInstance++;
            if (this.nextCorpseInstance > 0x00F0FFFF)
            {
                this.nextCorpseInstance = 0x00F0F001;
            }

            return new Identity
            {
                Type = IdentityType.Corpse,
                Instance = this.nextCorpseInstance
            };
        }

        private int AllocateCorpseInventorySlot()
        {
            int slot = this.nextCorpseInventorySlot++;
            if (this.nextCorpseInventorySlot > 0xff)
            {
                this.nextCorpseInventorySlot = 0x70;
            }

            return slot;
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

        private void StopFightingDeadTarget(Identity deadTarget)
        {
            foreach (ICharacter character in
                Pool.Instance.GetAll<ICharacter>(this.Identity, (int)IdentityType.CanbeAffected))
            {
                if (character.FightingTarget == deadTarget)
                {
                    character.SetFightingTarget(Identity.None);
                    this.nextCombatTicks.Remove(character.Identity.Instance);
                    this.lastCombatWeaponSlots.Remove(character.Identity.Instance);
                    this.SendCombatStopMessage(character);
                }
            }
        }

        private void SendCombatStopMessage(ICharacter character)
        {
            var stopFight = new StopFightMessage { Identity = character.Identity, Unknown1 = 1 };

            this.Announce(stopFight);
        }

        private static List<CorpseLootItem> RollCorpseLootItems(ICharacter target)
        {
            var lootItems = new List<CorpseLootItem>();

            int monsterData = target.Stats[StatIds.monsterdata].Value;
            int npcFamily = target.Stats[StatIds.npcfamily].Value;
            int level = target.Stats[StatIds.level].Value;
            List<CombatLootTableEntry> matchingEntries = DebugLootTable.Where(
                x => x.Matches(target.Name, monsterData, npcFamily)).ToList();
            string lootSource = "debug";

            if (matchingEntries.Count == 0)
            {
                matchingEntries = GetDatabaseLootTable().Where(
                    x => x.Matches(target.Name, monsterData, npcFamily)).ToList();
                lootSource = "database";
            }

            if (matchingEntries.Count == 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        "Loot roll found no configured table entries target={0} name={1} monsterData={2} npcFamily={3}",
                        target.Identity,
                        target.Name,
                        monsterData,
                        npcFamily));
            }

            foreach (CombatLootTableEntry entry in matchingEntries)
            {
                if (!RollLootChance(entry))
                {
                    continue;
                }

                Item item = CreateLootItem(entry, level);
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

                lootItems.Add(new CorpseLootItem { Slot = lootItems.Count, Item = item });
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Loot rolled target={0} name={1} monsterData={2} npcFamily={3} items={4} source={5}",
                    target.Identity,
                    target.Name,
                    monsterData,
                    npcFamily,
                    lootItems.Count,
                    lootSource));

            return lootItems;
        }

        private static int RollCorpseCredits(ICharacter target)
        {
            if (target == null)
            {
                return 0;
            }

            int monsterData = target.Stats[StatIds.monsterdata].Value;
            lock (LootRandomLock)
            {
                return CombatCorpseRules.RollObservedCredits(
                    target.Name,
                    monsterData,
                    max => LootRandom.Next(max));
            }
        }

        private static CombatLootTableEntry[] GetDatabaseLootTable()
        {
            if (databaseLootTableLoaded)
            {
                return databaseLootTable;
            }

            lock (DatabaseLootTableLock)
            {
                if (databaseLootTableLoaded)
                {
                    return databaseLootTable;
                }

                try
                {
                    DBMobTemplate[] mobTemplates = MobTemplateDao.Instance.GetAll().ToArray();
                    DBMobDroptable[] dropTable = MobDroptableDao.Instance.GetAll().ToArray();
                    databaseLootTable = CombatMobLootCatalog.BuildEntries(mobTemplates, dropTable);
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        string.Format(
                            "Loaded database mob loot entries={0} templates={1} drops={2}",
                            databaseLootTable.Length,
                            mobTemplates.Length,
                            dropTable.Length));
                }
                catch (Exception e)
                {
                    databaseLootTable = new CombatLootTableEntry[0];
                    LogUtil.Debug(DebugInfoDetail.Error, "Database mob loot load failed: " + e.Message);
                }

                databaseLootTableLoaded = true;
                return databaseLootTable;
            }
        }

        private static bool RollLootChance(CombatLootTableEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            int chance = entry.EffectiveDropChanceBasisPoints;
            if (chance <= 0)
            {
                return false;
            }

            if (chance >= 10000)
            {
                return true;
            }

            lock (LootRandomLock)
            {
                return CombatCorpseRules.ShouldDropBasisPoints(chance, max => LootRandom.Next(max));
            }
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

        private static Item CreateLootItem(CombatLootTableEntry entry, int targetLevel)
        {
            if (entry == null)
            {
                return null;
            }

            if (entry.ItemTemplates != null && entry.ItemTemplates.Length > 0)
            {
                return CreateLootItem(entry.ItemTemplates, targetLevel);
            }

            return CreateLootItem(entry.ItemTemplateIds, entry.Quality);
        }

        private static Item CreateLootItem(IEnumerable<CombatLootItemTemplate> itemTemplates, int targetLevel)
        {
            if (itemTemplates == null)
            {
                return null;
            }

            List<CombatLootItemTemplate> candidates =
                itemTemplates.Where(x => CanDropLootTemplate(x, targetLevel)).ToList();

            while (candidates.Count > 0)
            {
                int index = NextLootRandom(candidates.Count);
                CombatLootItemTemplate candidate = candidates[index];
                candidates.RemoveAt(index);

                Item item = CreateLootItem(candidate, targetLevel);
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static bool CanDropLootTemplate(CombatLootItemTemplate itemTemplate, int targetLevel)
        {
            if (itemTemplate == null || itemTemplate.LowId <= 0)
            {
                return false;
            }

            if (itemTemplate.RangeCheck == 0 || targetLevel <= 0)
            {
                return true;
            }

            int minQuality = Math.Max(1, itemTemplate.MinQuality);
            int maxQuality = Math.Max(minQuality, itemTemplate.MaxQuality);
            return targetLevel >= minQuality && targetLevel <= maxQuality;
        }

        private static Item CreateLootItem(CombatLootItemTemplate itemTemplate, int targetLevel)
        {
            int lowId = itemTemplate.LowId;
            int highId = itemTemplate.HighId <= 0 ? lowId : itemTemplate.HighId;

            if (!ItemLoader.ItemList.ContainsKey(lowId))
            {
                return null;
            }

            if (!ItemLoader.ItemList.ContainsKey(highId))
            {
                highId = lowId;
            }

            int quality = QualityForLootTemplate(itemTemplate, targetLevel);
            return new Item(quality, lowId, highId) { MultipleCount = 1 };
        }

        private static int QualityForLootTemplate(CombatLootItemTemplate itemTemplate, int targetLevel)
        {
            int minQuality = Math.Max(1, itemTemplate.MinQuality);
            int maxQuality = Math.Max(minQuality, itemTemplate.MaxQuality);

            if (itemTemplate.RangeCheck != 0 && targetLevel > 0)
            {
                return Math.Min(maxQuality, Math.Max(minQuality, targetLevel));
            }

            return minQuality;
        }

        private static int NextLootRandom(int max)
        {
            lock (LootRandomLock)
            {
                return LootRandom.Next(max);
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

        private static CorpseLootItem FindCorpseLootItem(CorpseState corpse, int requestedLootSlot)
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

        private static InventoryEntry CreateCorpseInventoryEntry(CorpseLootItem lootItem)
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

        private void SendCorpseInventoryUpdate(ICharacter looter, CorpseState corpse)
        {
            if (looter.Controller.Client == null)
            {
                return;
            }

            InventoryEntry[] entries = corpse.LootItems == null
                ? new InventoryEntry[0]
                : corpse.LootItems.Where(x => !x.Looted).Select(CreateCorpseInventoryEntry).ToArray();

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

        private void SendCorpseInventoryUpdateAndCredits(ICharacter looter, CorpseState corpse)
        {
            this.SendCorpseInventoryUpdate(looter, corpse);
            this.AwardCorpseCredits(looter, corpse);
        }

        private void AwardCorpseCredits(ICharacter looter, CorpseState corpse)
        {
            if (looter == null || corpse == null || corpse.CreditsLooted || corpse.Credits <= 0)
            {
                return;
            }

            int cashBeforeVisible = looter.Stats[StatIds.cash].Value;
            uint cashBeforeBase = looter.Stats[StatIds.cash].BaseValue;
            int cashBefore = cashBeforeVisible < 0 ? 0 : cashBeforeVisible;
            corpse.CreditsLooted = true;
            int cashAfter = cashBefore + corpse.Credits;
            if (cashAfter < 0)
            {
                cashAfter = 0;
            }

            looter.Stats[StatIds.cash].Set((uint)cashAfter);
            if (looter.Controller != null)
            {
                StatMessageHandler.Default.SendSingle(looter, (int)StatIds.cash, (uint)cashAfter);
            }
            looter.Stats[StatIds.cash].Changed = false;

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse credits awarded corpse={0} looter={1} credits={2} cashBeforeVisible={3} cashBeforeBase={4} cashAfter={5} inventorySlot={6}",
                    corpse.CorpseIdentity,
                    looter.Identity,
                    corpse.Credits,
                    cashBefore,
                    cashBeforeBase,
                    cashAfter,
                    corpse.InventorySlot));

            looter.Stats.Write();
        }

        private void SendCorpseLootAccessAction(ICharacter looter, CorpseState corpse)
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

        private class NpcHomeState
        {
            public Coordinate Coordinates { get; set; }
        }

        private class CombatAttackSource
        {
            public int MinDamage { get; set; }

            public int MaxDamage { get; set; }

            public int DamageBonus { get; set; }

            public double Range { get; set; }

            public double RechargeSeconds { get; set; }

            public bool UsesEquippedWeapon { get; set; }

            public int AttackInfoAmmoCount { get; set; }

            public int AttackInfoWeaponSlot { get; set; }

            public int AttackInfoUnk1 { get; set; }

            public int AttackInfoHitType { get; set; }
        }

        private enum CombatDamageSource
        {
            WeaponAutoAttack,
            UnarmedAutoAttack,
            DamageOverTime,
            HealOverTime,
            Nano,
            Environment
        }

        private class EquippedCombatWeapon
        {
            public IItem Item { get; set; }

            public int Slot { get; set; }
        }
    }
}
