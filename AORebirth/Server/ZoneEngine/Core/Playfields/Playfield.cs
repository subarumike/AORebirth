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

namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

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
    using ZoneEngine.Core.Arete.Quests;
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

        private readonly PlayfieldRuntimeSystems runtimeSystems;

        private readonly Dictionary<int, DateTime> nextCombatTicks = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, int> lastCombatWeaponSlots = new Dictionary<int, int>();

        private readonly Dictionary<int, CorpseState> corpses = new Dictionary<int, CorpseState>();

        private readonly Dictionary<int, CorpseState> pendingCorpseSpawns = new Dictionary<int, CorpseState>();

        private readonly Dictionary<int, PendingCorpseCreditAward> pendingCorpseCreditAwards =
            new Dictionary<int, PendingCorpseCreditAward>();

        private int nextCorpseInstance = 0x00F0F000;

        private int nextCorpseInventoryHandle = 0x70;

        private int nextCorpseLootItemInstance = 0x00200000;

        private const int CorpseLootItemIdentityType = 0x09000001;

        private static readonly TimeSpan CorpseCreditAwardDelay = TimeSpan.FromMilliseconds(500);

        private const int DefaultNpcDeathAnimationKey = 0x1F7;

        private const int DefaultPlayerDeathAnimationKey = 500;

        private const int DeathRespawnActionParameter1 = 1000020;

        private const int DeathRespawnActionParameter2 = 295830;

        private const int PrivateCityPlayfieldMinInstance = 0x100000;

        private const int PrivateCityPlayfieldMaxInstance = 0x12FFFF;

        private const int UnknownPlayfieldSizeFallback = 100000;

        private const string CapturedOwnedPrivateCityOrganizationName = "Est. 2024";

        private const double MaxMeleeCombatDistance = NpcCombatAttackRules.MaxMeleeCombatDistance;

        private const double MaxMeleeFollowHoldDistance = 3.0;

        private const double MinNpcCombatMoveDistance = 0.3;

        private const string CapturedCleaningRobotName = "Malfunctioning Cleaning Robot";

        private const int CapturedCleaningRobotMonsterData = 297023;

        private const int CapturedCleaningRobotCorpseCatMesh = 297018;

        private const int CapturedCleaningRobotCorpseCredits = 5;

        private const double CapturedCleaningRobotFollowStopDistance = 0.0;

        private static readonly int[][] CapturedCleaningRobotLootOutcomes =
        {
            new[] { 42620 },
            new int[0],
            new[] { 36779, 84142 },
            new int[0],
            new[] { 297289 },
            new int[0],
            new int[0],
            new[] { 70558, 155685 },
            new[] { 297289, 150306 },
            new int[0],
            new[] { 155666 },
            new int[0],
            new[] { 70564 },
            new[] { 155666 },
            new[] { 155687 },
            new[] { 70565 },
            new[] { 155684 },
            new int[0]
        };

        private const int UnarmedAttackInfoAmmoCount = -1;

        private const int PlayerUnarmedAttackInfoWeaponSlot = 0;

        private const int PlayerUnarmedAttackInfoWeaponInstance = 100;

        private const int NormalAttackInfoHitType = NpcCombatAttackRules.NormalAttackInfoHitType;

        private const int MissingItemStatValue = 1234567890;

        private const double DefaultCombatTickSeconds = NpcCombatAttackRules.DefaultCombatTickSeconds;

        private const double OutOfRangeRetrySeconds = NpcCombatAttackRules.OutOfRangeRetrySeconds;

        private const int RubiKaStartPlayfield = 4582;

        private const int GridPlayfield = 152;

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

        private readonly StatelData[] collisionStatels = new StatelData[0];

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
            this.runtimeSystems =
                new PlayfieldRuntimeSystems(
                    this,
                    this.Identity,
                    IsPrivateCityPlayfieldCandidate,
                    PlayfieldStatelTransitionRuntimeService.IsCapturedMontroyalPrivateCityInstance,
                    ResolveCharacterOrganizationInstance,
                    ResolveOrganizationName,
                    ResolveCharacterStatWireValue);

            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToClient>(
                    this.runtimeSystems.DeliverAOtomationMessageToClient));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToPlayfield>(
                    message => this.runtimeSystems.DeliverAOtomationMessageToPlayfield(message, this.Announce)));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageToPlayfieldOthers>(
                    message => this.runtimeSystems.DeliverAOtomationMessageToPlayfieldOthers(
                        message,
                        this.AnnounceOthers)));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageBodyToClient>(
                    this.runtimeSystems.DeliverAOtomationMessageBodyToClient));
            this.memBusDisposeContainer.Add(
                this.playfieldBus.Subscribe<IMSendAOtomationMessageBodiesToClient>(
                    this.runtimeSystems.DeliverAOtomationMessageBodiesToClient));
            this.memBusDisposeContainer.Add(this.playfieldBus.Subscribe<IMSendPlayerSCFUs>(this.SendSCFUsToClient));
            this.memBusDisposeContainer.Add(this.playfieldBus.Subscribe<IMExecuteFunction>(this.ExecuteFunction));
            this.heartBeat = new Timer(this.HeartBeatTimer, null, 10, 0);

            this.statels = this.runtimeSystems.ResolveStatels(playfieldIdentity);
            this.runtimeSystems.RegisterStatels(this.statels);
            this.collisionStatels = this.runtimeSystems.ResolveCollisionStatels(this.statels);
            this.runtimeSystems.MaterializeStartupObjects(
                playfieldIdentity,
                this.statels);
        }

        internal void SpawnCapturedNpcContent(Identity playfieldIdentity)
        {
            this.runtimeSystems.SpawnCapturedNpcContent(playfieldIdentity);
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
            this.runtimeSystems.AnnounceMessageToCharacterClients(messageBody, this.Send);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        public void AnnounceAppearanceUpdate(ICharacter character)
        {
            AppearanceUpdateMessageHandler.Default.Send(character);
        }

        public static void ArmPostZoneCollisionGrace(ICharacter character)
        {
            PlayfieldStatelTransitionRuntimeService.ArmPostZoneCollisionGrace(character);
        }

        /// <summary>
        /// </summary>
        /// <param name="messageBody">
        /// </param>
        /// <param name="dontSend">
        /// </param>
        public void AnnounceOthers(MessageBody messageBody, Identity dontSend)
        {
            this.runtimeSystems.AnnounceMessageToOtherCharacterClients(messageBody, dontSend, this.Send);
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
            IDynel dynel = this.runtimeSystems.FindByIdentity<IDynel>(identity);
            return dynel != null ? dynel.Coordinates() : new Coordinate();
        }

        public void DespawnNpcImmediately(ICharacter target)
        {
            this.runtimeSystems.DespawnNpcImmediately(
                target,
                this.StopFightingDeadTarget,
                this.CancelPendingNpcCorpseSpawn);
        }

        private void CancelPendingNpcCorpseSpawn(Identity deadNpcIdentity)
        {
            this.pendingCorpseSpawns.Remove(deadNpcIdentity.Instance);
        }

        public void RegisterNpcHome(ICharacter character)
        {
            this.runtimeSystems.RegisterNpcHome(character);
        }

        public void AcquireNpcAggro(ICharacter attacker, ICharacter target)
        {
            this.runtimeSystems.AcquireNpcAggro(attacker, target);
        }

        internal void ClearInvalidNpcCombatTarget(ICharacter attacker)
        {
            this.runtimeSystems.ClearInvalidNpcCombatTarget(attacker);
        }

        internal void ClearNpcCombatTracking(Identity identity)
        {
            this.runtimeSystems.ClearNpcCombatTracking(identity);
        }

        internal void ClearNpcFightingTarget(ICharacter character)
        {
            this.runtimeSystems.ClearNpcFightingTarget(character);
        }

        public int DespawnCorpses(Func<string, Identity, bool> shouldDespawn)
        {
            return this.runtimeSystems.DespawnCorpses(
                this.pendingCorpseSpawns,
                this.corpses,
                shouldDespawn,
                corpse => corpse.Name,
                corpse => corpse.DeadNpcIdentity,
                this.DespawnCorpse);
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
            return this.runtimeSystems.FindByIdentity(identity);
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
            return this.runtimeSystems.FindByIdentity<T>(identity);
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
            return this.runtimeSystems.FindDynelsInRange(dynel, range).ToList();
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

        public static bool IsPrivateCityPlayfieldCandidate(Identity playfieldIdentity)
        {
            if (playfieldIdentity.Type != IdentityType.Playfield
                && playfieldIdentity.Type != IdentityType.Playfield2)
            {
                return false;
            }

            int instance = playfieldIdentity.Instance;
            // Live captures observed dynamic private city playfields 0x104868, 0x116000, 0x120005, 0x121001, 0x124002, and 0x12400D.
            if (instance < PrivateCityPlayfieldMinInstance || instance > PrivateCityPlayfieldMaxInstance)
            {
                return false;
            }

            if (PlayfieldStatelTransitionRuntimeService.IsCapturedMontroyalPrivateCityInstance(instance))
            {
                return true;
            }

            return Playfields.GetPlayfieldX(instance) == UnknownPlayfieldSizeFallback
                   && Playfields.GetPlayfieldZ(instance) == UnknownPlayfieldSizeFallback;
        }

        public void SendPrivateCityPlayfieldReadyBlock(ZoneClient client, ICharacter character)
        {
            this.runtimeSystems.SendPrivateCityPlayfieldReadyBlock(client, character);
        }

        public void SendPrivateCityPreFullCharacterReadyBlock(ZoneClient client, ICharacter character)
        {
            this.runtimeSystems.SendPrivateCityPreFullCharacterReadyBlock(client, character);
        }

        public bool TryHandleGenericCmdUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            return this.runtimeSystems.TryHandleGenericCmdUse(client, message, target);
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
            this.runtimeSystems.PublishMessageBodyToClient(client, body, this.Publish);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="message">
        /// </param>
        public void Send(IZoneClient client, Message message)
        {
            this.runtimeSystems.PublishMessageToClient(client, message, this.Publish);
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

            if (this.TryCompleteGridTeleportInCurrentPlayfield(dynel, destination, heading, playfield))
            {
                return;
            }

            this.runtimeSystems.TransferToPlayfield(
                dynel,
                destination,
                heading,
                playfield,
                this.ClearPlayfieldTransferContactState,
                DisableTimersForPlayfieldTransfer,
                CapturePlayfieldTransferEnterZoningPhase,
                () => TeleportMessageHandler.Default.Send(
                    dynel as ICharacter,
                    destination.coordinate,
                    (Vector.Quaternion)heading,
                    playfield),
                this.AnnouncePlayfieldTransferDespawn,
                ApplyPlayfieldTransferState,
                CapturePlayfieldTransferClient,
                this.ResolveOrCreatePlayfieldTransferDestination,
                CompletePlayfieldTransferDispose,
                client => this.SendPlayfieldTransferRedirect(client, playfield));
        }

        private void ClearPlayfieldTransferContactState(int dynelId)
        {
            this.runtimeSystems.ClearStatelTransitionContactState(dynelId);
        }

        private static void DisableTimersForPlayfieldTransfer(Dynel dynel)
        {
            dynel.DoNotDoTimers = true;
        }

        private void AnnouncePlayfieldTransferDespawn(Dynel dynel)
        {
            DespawnMessage despawnMessage = DespawnMessageHandler.Default.Create(dynel.Identity);
            this.AnnounceOthers(despawnMessage, dynel.Identity);
        }

        private static void ApplyPlayfieldTransferState(Dynel dynel, Coordinate destination, IQuaternion heading)
        {
            dynel.RawCoordinates = new Vector3() { X = destination.x, Y = destination.y, Z = destination.z };
            dynel.RawHeading = new Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);
        }

        private static ZoneClient CapturePlayfieldTransferClient(Dynel dynel)
        {
            return (ZoneClient)dynel.Controller.Client;
        }

        private static Action CapturePlayfieldTransferEnterZoningPhase(Dynel dynel)
        {
            ZoneClient lifecycleClient = dynel.Controller == null ? null : dynel.Controller.Client as ZoneClient;
            return lifecycleClient == null
                       ? null
                       : (Action)lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer;
        }

        private IPlayfield ResolveOrCreatePlayfieldTransferDestination(Identity playfield)
        {
            IPlayfield newPlayfield = this.server.PlayfieldById(playfield);
            Pool.Instance.GetObject<Playfield>(
                Identity.None,
                new Identity() { Type = playfield.Type, Instance = playfield.Instance });

            if (newPlayfield == null)
            {
                newPlayfield = new Playfield(this.server, playfield);
            }

            return newPlayfield;
        }

        private static void CompletePlayfieldTransferDispose(Dynel dynel, IPlayfield newPlayfield)
        {
            dynel.Playfield = newPlayfield;
            dynel.Controller.Client = null;
            dynel.IsTeleporting = true;
            dynel.Dispose();
        }

        private void SendPlayfieldTransferRedirect(ZoneClient client, Identity playfield)
        {
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

        private bool TryCompleteGridTeleportInCurrentPlayfield(
            Dynel dynel,
            Coordinate destination,
            IQuaternion heading,
            Identity playfield)
        {
            if (this.Identity.Instance != GridPlayfield
                || playfield.Type != this.Identity.Type
                || playfield.Instance != this.Identity.Instance)
            {
                return false;
            }

            ICharacter character = dynel as ICharacter;
            if (character == null
                || character.Controller == null
                || character.Controller.Client == null)
            {
                return false;
            }

            float fromX = dynel.RawCoordinates.X;
            float fromY = dynel.RawCoordinates.Y;
            float fromZ = dynel.RawCoordinates.Z;

            TeleportMessageHandler.Default.SendLocal(
                character,
                destination.coordinate,
                new AORebirth.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf));

            dynel.RawCoordinates = new AORebirth.Core.Vector.Vector3
                                   {
                                       x = destination.x,
                                       y = destination.y,
                                       z = destination.z
                                   };
            dynel.RawHeading = new AORebirth.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);
            this.PrimeStatelCollisionContacts(character);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Grid current-playfield teleport completed character={0} playfield={1} fromCoords={2:F1},{3:F1},{4:F1} toCoords={5:F1},{6:F1},{7:F1}",
                    dynel.Identity.ToString(true),
                    this.Identity.Instance,
                    fromX,
                    fromY,
                    fromZ,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="entity">
        /// </param>
        public void DisconnectClient(IInstancedEntity entity)
        {
            this.runtimeSystems.RemoveInstancedEntity(entity);
        }

        /// <summary>
        /// </summary>
        /// <param name="imExecuteFunction">
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public void ExecuteFunction(IMExecuteFunction imExecuteFunction)
        {
            this.runtimeSystems.ExecuteFunction(
                imExecuteFunction,
                this.FindNamedEntityByIdentity,
                SendNoValidFunctionTargetMessage);
        }

        private static void SendNoValidFunctionTargetMessage(Character character, string text)
        {
            character.Controller.Client.SendCompressed(new ChatTextMessage { Identity = character.Identity, Text = text });
        }

        public List<ICharacter> FindCharacterInRange(IDynel dynel, float range)
        {
            return this.runtimeSystems.FindCharactersInRange(dynel, range).ToList();
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <returns>
        /// </returns>
        public INamedEntity FindNamedEntityByIdentity(Identity identity)
        {
            return this.runtimeSystems.FindByIdentity<INamedEntity>(identity);
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
        /// <param name="sendSCFUs">
        /// </param>
        public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)
        {
            this.runtimeSystems.SendExistingCharacterVisibilityToClient(
                sendSCFUs.toClient.Controller.Character,
                body => sendSCFUs.toClient.SendCompressed(body));
        }

        public void AnnouncePlayerVisibility(ICharacter character)
        {
            this.runtimeSystems.AnnounceJoiningCharacterVisibility(character, body => this.Announce(body));
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        private void CheckStatelCollision(ICharacter dynel)
        {
            this.runtimeSystems.CheckStatelCollision(
                dynel,
                this.Identity,
                this.collisionStatels,
                ResolveCapturedMontroyalPrivateCityInstance,
                ResolveCharacterOrganizationInstance,
                x => x.StopMovement(),
                this.SendCapturedPrivateCityEntrySocialStatus,
                this.TeleportToPlayfield);
        }

        private void PrimeStatelCollisionContacts(ICharacter dynel)
        {
            this.runtimeSystems.PrimeStatelCollisionContacts(dynel, this.collisionStatels);
        }

        private static int ResolveCapturedMontroyalPrivateCityInstance(ICharacter character)
        {
            int organizationInstance = ResolveCharacterOrganizationInstance(character);
            int organizationCityId = ResolveOrganizationCityId(organizationInstance);
            return PlayfieldStatelTransitionRuntimeService.ResolveCapturedMontroyalPrivateCityInstance(
                organizationInstance,
                organizationCityId);
        }

        private void TeleportToPlayfield(
            Dynel dynel,
            Coordinate destination,
            AORebirth.Core.Vector.Quaternion heading,
            int playfieldInstance)
        {
            this.Teleport(
                dynel,
                destination,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = playfieldInstance });
        }

        private static int ResolveOrganizationCityId(int organizationInstance)
        {
            if (organizationInstance <= 0)
            {
                return 0;
            }

            try
            {
                DBOrganization organization = OrganizationDao.Instance.Get(organizationInstance);
                return organization == null ? 0 : organization.CityId;
            }
            catch
            {
                return 0;
            }
        }

        private static string ResolveOrganizationName(int organizationInstance)
        {
            if (organizationInstance <= 0)
            {
                return string.Empty;
            }

            try
            {
                DBOrganization organization = OrganizationDao.Instance.Get(organizationInstance);
                if (organization != null && !string.IsNullOrEmpty(organization.Name))
                {
                    return organization.Name;
                }
            }
            catch
            {
            }

            return PlayfieldStatelTransitionRuntimeService.IsCapturedOwnedPrivateCityOrganization(organizationInstance)
                       ? CapturedOwnedPrivateCityOrganizationName
                       : string.Empty;
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            return ResolveCharacterStatValue(character, StatIds.clan);
        }

        private static int ResolveCharacterStatValue(ICharacter character, StatIds statId)
        {
            if (character == null)
            {
                return 0;
            }

            uint baseValue = character.Stats[statId].BaseValue;
            if (baseValue > 0 && baseValue <= int.MaxValue)
            {
                return (int)baseValue;
            }

            return character.Stats[statId].Value;
        }

        private static uint ResolveCharacterStatWireValue(ICharacter character, StatIds statId)
        {
            int value = ResolveCharacterStatValue(character, statId);
            return value < 0 ? 0u : (uint)value;
        }

        /// <summary>
        /// </summary>
        /// <param name="dynel">
        /// </param>
        private void CheckWallCollision(ICharacter dynel)
        {
            this.runtimeSystems.CheckWallCollision(
                dynel,
                PlayfieldStatelTransitionRuntimeService.IsPostZoneCollisionGraceActive,
                this.TeleportToPlayfield);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender">
        /// </param>
        private void HeartBeatTimer(object sender)
        {
            try
            {
                this.runtimeSystems.ProcessHeartbeatTimedLifecycle(
                    this.Identity,
                    this.ProcessPendingCorpseSpawns,
                    this.ProcessCorpseDespawns,
                    this.ProcessPendingCorpseCreditAwards,
                    dynel => this.runtimeSystems.ProcessCharacterRegeneration(dynel, SendChangedStats),
                    this.DoCombatTick,
                    this.runtimeSystems.ProcessCharacterFollow,
                    dynel => this.runtimeSystems.ProcessPlayerCollisionChecks(
                        dynel,
                        this.CheckWallCollision,
                        this.CheckStatelCollision));
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
            ICharacter character = this.FindByIdentity<ICharacter>(attacker);
            if (character != null && character.Controller is NPCController)
            {
                this.runtimeSystems.ResetNpcCombatTick(character);
            }
            else
            {
                this.runtimeSystems.ResetPlayerCombatTick(attacker, this.ResetPlayerCombatTick);
            }
        }

        public void StartPlayerAttack(ICharacter character, Identity target)
        {
            this.runtimeSystems.StartPlayerAttack(character, target, this.ResetCombatTick);
        }

        public void CancelPlayerAttack(ICharacter character)
        {
            this.runtimeSystems.CancelPlayerAttack(character, this.ResetCombatTick);
        }

        private void ResetPlayerCombatTick(Identity attacker)
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
            this.runtimeSystems.ProcessPlayerRespawn(
                character,
                dynel,
                corpseIdentity,
                destination,
                destinationPlayfield,
                this.LogSkippedPlayerCorpseVisual,
                this.SendDeathSocialStatus,
                this.MarkPlayerRespawned,
                this.SendDeathRespawnStateStats,
                StopCharacterMovement,
                SendChangedStats,
                this.LogPlayerRespawnRequested,
                EnableCharacterTimers,
                this.TryCompleteDeathRespawnInCurrentPlayfield,
                this.Teleport,
                this.ClearCombatTracking,
                this.StopFightingDeadTarget,
                this.SendCombatStopMessage);
        }

        private void LogSkippedPlayerCorpseVisual(ICharacter character, Identity corpseIdentity)
        {
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Player corpse visual skipped target={0} corpse={1}; current CorpseFullUpdate template is NPC-loot oriented and breaks modern death teleport flow.",
                    character.Identity,
                    corpseIdentity));
        }

        private void LogPlayerRespawnRequested(
            ICharacter character,
            Identity corpseIdentity,
            Identity destinationPlayfield,
            Coordinate destination)
        {
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
        }

        private static void StopCharacterMovement(ICharacter character)
        {
            character.StopMovement();
        }

        private static void SendChangedStats(ICharacter character)
        {
            character.SendChangedStats();
        }

        private static void EnableCharacterTimers(ICharacter character)
        {
            character.DoNotDoTimers = false;
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
                new AORebirth.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf),
                destinationPlayfield);

            dynel.RawCoordinates = new AORebirth.Core.Vector.Vector3
                                   {
                                       x = destination.x,
                                       y = destination.y,
                                       z = destination.z
                                   };
            dynel.RawHeading = new AORebirth.Core.Vector.Quaternion(heading.xf, heading.yf, heading.zf, heading.wf);

            PlayfieldAnarchyFMessageHandler.Default.Send(character);
            SimpleCharFullUpdate.SendToPlayfield(client);
            this.SendDeathSocialStatus(character);
            this.SendDeathRespawnStateStats(character);

            var sendSCFUs = new IMSendPlayerSCFUs { toClient = client };
            this.SendSCFUsToClient(sendSCFUs);

            foreach (StaticDynel staticDynel in this.runtimeSystems.StaticDynels())
            {
                SimpleItemFullUpdateMessageHandler.Default.Send(character, staticDynel);
            }

            WeaponItemFullUpdate.SendWeaponDefinitions(character);
            this.SendDeathRespawnGameTime(character);
            this.SendDeathSocialStatus(character);
            FullCharacterMessageHandler.Default.Send(character);
            this.SendDeathRespawnPlayfieldReadyBlock(client, character);
            this.SendDeathRespawnAction(character);
            this.runtimeSystems.EnsureWeaponVisualMeshes(character, false);
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
            if (attacker.Controller is NPCController)
            {
                this.runtimeSystems.ProcessNpcCombatTick(attacker);
                return;
            }

            this.runtimeSystems.ProcessPlayerCombatTick(
                attacker,
                this.ClearCombatTracking,
                this.FindPlayerCombatTarget,
                this.IsValidPlayerCombatTarget,
                this.LogInvalidPlayerCombatTickTarget,
                this.ProcessValidatedPlayerCombatTick);
        }

        private ICharacter FindPlayerCombatTarget(Identity target)
        {
            return this.FindByIdentity<ICharacter>(target);
        }

        private bool IsValidPlayerCombatTarget(ICharacter target)
        {
            return target != null && target.InPlayfield(this.Identity) && target.Stats[StatIds.health].Value > 0;
        }

        private void LogInvalidPlayerCombatTickTarget(ICharacter attacker, ICharacter target)
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
        }

        private void ProcessValidatedPlayerCombatTick(ICharacter attacker, ICharacter target)
        {
            CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);
            DateTime nextTick;
            DateTime now = DateTime.UtcNow;
            if (this.nextCombatTicks.TryGetValue(attacker.Identity.Instance, out nextTick)
                && nextTick > now)
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

            int currentHealth = target.Stats[StatIds.health].Value;
            int damage = this.CalculateCombatDamage(attacker, attackSource);
            int newHealth = Math.Max(0, currentHealth - damage);
            bool killingHit = newHealth == 0;

            target.Stats[StatIds.health].Value = newHealth;
            this.AnnounceCombatDamage(
                attacker,
                target,
                damage,
                attackSource,
                attackSource.UsesEquippedWeapon
                    ? CombatDamageSource.WeaponAutoAttack
                    : CombatDamageSource.UnarmedAutoAttack);
            this.runtimeSystems.SendChangedStats(target, SendChangedStats);
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
                this.HandleCombatKillingHit(attacker, target);
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

        internal bool IsInCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            return this.runtimeSystems.IsInNpcCombatRange(attacker, target, range);
        }

        internal static double GetCombatDistance(ICharacter attacker, ICharacter target)
        {
            return PlayfieldNpcCombatMovementRuntimeService.GetCombatDistance(attacker, target);
        }

        internal static bool IsCapturedCleaningRobot(ICharacter character)
        {
            return PlayfieldNpcCombatMovementRuntimeService.IsCapturedCleaningRobot(character);
        }

        internal static void LogNpcBrain(string state, string reason, ICharacter attacker, ICharacter target, double range, double distance)
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
                    "CombatAttackInfoSend source={0} attacker={1} target={2} dmg={3} u2={4} u3={5} u4={6} u5={7} u6={8} weaponBased={9} atkDefault={10} atkDamageType={11} atkWeaponType={12} atkEquippedWeapons={13}",
                    source,
                    attacker.Identity,
                    target.Identity,
                    damage,
                    attackSource.AttackInfoAmmoCount,
                    attackSource.AttackInfoWeaponSlot,
                    attackSource.AttackInfoUnk1,
                    attackSource.AttackInfoHitType,
                    attackSource.AttackInfoWeaponInstance,
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
                    Unknown6 = attackSource.AttackInfoWeaponInstance
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
                int attackInfoWeaponSlot = this.GetUnarmedAttackInfoWeaponSlot(attacker);
                int attackInfoDamage = this.GetUnarmedAttackDamage(attacker, attackInfoWeaponSlot);
                return new CombatAttackSource
                       {
                           MinDamage = attackInfoDamage,
                           MaxDamage = attackInfoDamage,
                           DamageBonus = NormalizeCombatItemStat(attacker.Stats[StatIds.damagebonus].Value, 0),
                           Range = MaxMeleeCombatDistance,
                           RechargeSeconds = IsCapturedCleaningRobot(attacker)
                                                 ? NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds
                                                 : DefaultCombatTickSeconds,
                           UsesEquippedWeapon = false,
                           AttackInfoAmmoCount = UnarmedAttackInfoAmmoCount,
                           AttackInfoWeaponSlot = attackInfoWeaponSlot,
                           AttackInfoUnk1 = 0,
                           AttackInfoHitType = NormalAttackInfoHitType,
                           AttackInfoWeaponInstance = this.GetUnarmedAttackInfoWeaponInstance(attacker)
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
                       AttackInfoHitType = NormalAttackInfoHitType,
                       AttackInfoWeaponInstance = 0
                    };
        }

        private int GetUnarmedAttackInfoWeaponSlot(ICharacter attacker)
        {
            return PlayerUnarmedAttackInfoWeaponSlot;
        }

        private int GetUnarmedAttackDamage(ICharacter attacker, int attackInfoWeaponSlot)
        {
            return Math.Max(
                NormalizeCombatItemStat(attacker.Stats[StatIds.mindamage].Value, 0),
                NormalizeCombatItemStat(attacker.Stats[StatIds.maxdamage].Value, 0));
        }

        private int GetUnarmedAttackInfoWeaponInstance(ICharacter attacker)
        {
            return PlayerUnarmedAttackInfoWeaponInstance;
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

        internal void UpdateNpcMeleeFollowHold(ICharacter attacker, ICharacter target, double range)
        {
            this.runtimeSystems.UpdateNpcMeleeFollowHold(
                attacker,
                target,
                range,
                this.MoveNpcToCombatPosition,
                LogNpcBrain);
        }

        internal void TryMoveNpcIntoCombatRange(ICharacter attacker, ICharacter target, double range)
        {
            this.runtimeSystems.TryMoveNpcIntoCombatRange(
                attacker,
                target,
                range,
                this.MoveNpcToCombatPosition,
                LogNpcBrain);
        }

        private void MoveNpcToCombatPosition(ICharacter attacker, AORebirth.Core.Vector.Vector3 nextPosition)
        {
            attacker.Coordinates(nextPosition);
            this.Announce(
                new SetPosMessage
                {
                    Identity = attacker.Identity,
                    Coordinates =
                        new Vector3
                        {
                            X = nextPosition.xf,
                            Y = nextPosition.yf,
                            Z = nextPosition.zf
                        },
                    Unknown1 = 0
                });
        }

        private void KillNpcTarget(ICharacter attacker, ICharacter target)
        {
            if (!(target.Controller is NPCController))
            {
                return;
            }

            this.runtimeSystems.BeginNpcDeath(attacker, target);
        }

        internal void HandleCombatKillingHit(ICharacter attacker, ICharacter target)
        {
            if (target.Controller is NPCController)
            {
                this.KillNpcTarget(attacker, target);
            }
            else if (target.Controller is PlayerController)
            {
                this.runtimeSystems.BeginPlayerDeath(target, this.KillPlayerTarget);
            }
            else
            {
                if (attacker.Controller is NPCController)
                {
                    this.ClearNpcFightingTarget(attacker);
                }
                else
                {
                    this.runtimeSystems.ClearPlayerFightingTarget(attacker, this.ClearCombatTracking);
                }
            }
        }

        internal void StopDyingNpcCombatState(ICharacter target)
        {
            this.runtimeSystems.StopDyingNpcCombatState(target);

            if (IsCapturedCleaningRobot(target))
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    target.Identity);
                this.SendCombatStopMessage(target);
            }
        }

        internal void AwardCombatXp(ICharacter attacker, ICharacter target)
        {
            if (attacker == null || target == null || !(attacker.Controller is PlayerController))
            {
                return;
            }

            int xpReward = CalculateCombatXpReward(attacker, target);
            if (xpReward <= 0)
            {
                return;
            }

            uint xpBeforeBase = attacker.Stats[StatIds.xp].BaseValue;
            int xpBefore = xpBeforeBase > int.MaxValue ? int.MaxValue : (int)xpBeforeBase;
            long xpAfterLong = (long)xpBefore + xpReward;
            int xpAfter = xpAfterLong > int.MaxValue ? int.MaxValue : (int)xpAfterLong;

            ulong unsavedXpAfter = (ulong)attacker.Stats[StatIds.unsavedxp].BaseValue + (uint)xpReward;
            attacker.Stats[StatIds.xp].Set((uint)xpAfter);
            attacker.Stats[StatIds.lastxp].Set((uint)xpReward);
            attacker.Stats[StatIds.unsavedxp].Set((uint)Math.Min((ulong)int.MaxValue, unsavedXpAfter));

            if (attacker.Controller != null && attacker.Controller.Client != null)
            {
                StatMessageHandler.Default.SendChanged(attacker);
                this.SendRewardFeedback(
                    attacker,
                    string.Format(CultureInfo.InvariantCulture, "You received {0} xp.", xpReward));
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Combat XP awarded attacker={0} target={1} xp={2} xpBeforeBase={3} xpAfter={4}",
                    attacker.Identity,
                    target.Identity,
                    xpReward,
                    xpBeforeBase,
                    xpAfter));

            attacker.Stats.Write();
        }

        private static int CalculateCombatXpReward(ICharacter attacker, ICharacter target)
        {
            int targetXp = target.Stats[StatIds.xp].Value;
            if (targetXp > 0)
            {
                return targetXp;
            }

            int targetLevel = Math.Max(1, target.Stats[StatIds.level].Value);
            int attackerLevel = Math.Max(1, attacker.Stats[StatIds.level].Value);
            if (targetLevel < Math.Max(1, attackerLevel - 10))
            {
                return 1;
            }

            return Math.Max(1, targetLevel);
        }

        private void KillPlayerTarget(ICharacter target)
        {
            if (!(target.Controller is PlayerController))
            {
                return;
            }

            this.MarkPlayerDead(target);
            this.runtimeSystems.RunPlayerDeathStatUpdateSequence(
                target,
                SendChangedStats,
                x => this.runtimeSystems.CleanupPlayerDeathCombat(
                    x,
                    this.ClearCombatTracking,
                    this.StopFightingDeadTarget,
                    this.SendCombatStopMessage),
                this.SendPlayerDeathAnimation);

            LogUtil.Debug(DebugInfoDetail.Network, string.Format("Player died target={0}", target.Identity));
        }

        public bool TryUseCorpse(ICharacter looter, Identity corpseIdentity)
        {
            return this.runtimeSystems.TryUseCorpse(
                looter,
                corpseIdentity,
                this.corpses,
                CombatCorpseRules.ItemLootCorpseLifetime,
                CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay,
                corpse => corpse.DeadNpcIdentity,
                corpse => corpse.ExpiresAtUtc,
                corpse => corpse.HasUnlootedItems,
                (corpse, opened) => { corpse.Opened = opened; },
                corpse => corpse.LootClass,
                this.DespawnCorpse,
                this.ExtendCorpseLifetime,
                this.SendCorpseInventoryUpdate,
                this.ScheduleCorpseCreditAward,
                this.ScheduleCorpseDespawn);
        }

        public bool TryUseDeadNpcCorpse(ICharacter looter, Identity deadNpcIdentity, out Identity corpseIdentity)
        {
            return this.runtimeSystems.TryUseDeadNpcCorpse(
                looter,
                deadNpcIdentity,
                this.corpses.Values,
                corpse => corpse.CorpseIdentity,
                corpse => corpse.DeadNpcIdentity,
                corpse => corpse.CreatedAtUtc,
                this.TryUseCorpse,
                out corpseIdentity);
        }

        public bool TryLootCorpseItem(ICharacter looter, Identity sourceContainer, Identity target, int targetPlacement)
        {
            int requestedLootSlot = sourceContainer.Instance & 0xffff;
            return this.runtimeSystems.TryLootCorpseItem(
                looter,
                sourceContainer,
                target,
                targetPlacement,
                this.corpses.Values,
                corpse => corpse.InventoryHandle,
                corpse => corpse.CorpseIdentity,
                corpse => corpse.ExpiresAtUtc,
                corpse => corpse.HasUnlootedItems,
                corpse => corpse.LootItems.Count(x => !x.Looted),
                corpse => FindCorpseLootItem(corpse, requestedLootSlot),
                lootItem => lootItem.Item,
                lootItem => lootItem.Slot,
                (lootItem, looted) => { lootItem.Looted = looted; },
                (corpse, opened) => { corpse.Opened = opened; },
                this.runtimeSystems.CharacterHasUniqueItemAlready,
                (character, text) => ChatTextMessageHandler.Default.Send(character, text),
                this.SendUseActionFinished,
                this.runtimeSystems.TryAddCorpseLootItem,
                this.SendCorpseContainerAddItem,
                this.ScheduleCorpseDespawn,
                this.ExtendCorpseLifetime,
                this.DespawnCorpse,
                CombatCorpseRules.ItemLootCorpseLifetime,
                CombatCorpseRules.EmptyCorpseCleanupAfterOpenedDelay);
        }

        private void SendCorpseContainerAddItem(ICharacter looter, Identity sourceContainer, int targetPlacement)
        {
            if (looter.Controller == null || looter.Controller.Client == null)
            {
                return;
            }

            looter.Controller.Client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = looter.Identity,
                    SourceContainer = sourceContainer,
                    TargetPlacement = targetPlacement,
                    Target = looter.Identity,
                    Unknown = 0
                },
                looter.Identity.Instance);
        }

        internal void MarkNpcDead(ICharacter target)
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

        private void SendCapturedPrivateCityEntrySocialStatus(ICharacter target)
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
                                Value2 = 4
                            }
                        }
                },
                false);

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Private city entry social status target={0} socialStatus=4 unknown=1 evidence=live_capture_20260622-101935",
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

        internal void SendNpcDeathAnimation(ICharacter target)
        {
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                PlayfieldLifecycleTrace.MessageCharacterActionDeath,
                target.Identity,
                    "Parameter2=" + DeathAnimationKeyFor(target));
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
            this.SendEmptyPlayfieldTowersAndCities(client);

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

        private void SendEmptyPlayfieldTowersAndCities(ZoneClient client)
        {
            this.SendPlayfieldTowersAndCities(client, 0, new byte[0]);
        }

        private void SendPlayfieldTowersAndCities(ZoneClient client, byte cityUnknown, byte[] cityPayload)
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
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                PlayfieldLifecycleTrace.MessagePlayfieldAllTowers,
                playfieldIdentity);

            client.SendCompressed(
                new PlayfieldAllCitiesMessage
                {
                    Identity = playfieldIdentity,
                    Unknown = cityUnknown,
                    Payload = cityPayload ?? new byte[0]
                });
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                PlayfieldLifecycleTrace.MessagePlayfieldAllCities,
                playfieldIdentity);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent,
                playfieldIdentity,
                "cityUnknown=" + cityUnknown + " cityPayloadBytes=" + (cityPayload == null ? 0 : cityPayload.Length));
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

        internal void ClearCombatTracking(Identity identity)
        {
            this.nextCombatTicks.Remove(identity.Instance);
            this.lastCombatWeaponSlots.Remove(identity.Instance);
            this.runtimeSystems.ClearNpcCombatTracking(identity);
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

            foreach (ICharacter character in this.runtimeSystems.Characters())
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
                        corpseMonsterData,
                        this.CorpseCreditsFor(corpseIdentity)));
            }

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "CorpseFullUpdate visual target={0} corpse={1} catMesh={2} monsterData={3} credits={4} scale={5} sex={6} breed={7} race={8}",
                    target.Identity,
                    corpseIdentity,
                    corpseCatMesh,
                    corpseMonsterData,
                    this.CorpseCreditsFor(corpseIdentity),
                    target.Stats[StatIds.monsterscale].Value,
                    target.Stats[StatIds.sex].Value,
                    target.Stats[StatIds.breed].Value,
                    target.Stats[StatIds.race].Value));
        }

        private int CorpseCreditsFor(Identity corpseIdentity)
        {
            CorpseState corpse;
            return this.corpses.TryGetValue(corpseIdentity.Instance, out corpse)
                       ? corpse.Credits
                       : 0;
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
            this.runtimeSystems.ProcessDueNpcCorpseDespawns(DateTime.UtcNow, this.DespawnCorpse);
        }

        private void ProcessPendingCorpseSpawns()
        {
            this.runtimeSystems.ProcessPendingCorpseSpawns(
                this.pendingCorpseSpawns,
                corpse => corpse.SpawnsAtUtc,
                corpse => corpse.CorpseIdentity,
                corpse => corpse.DeadNpcIdentity,
                identity => this.FindByIdentity<ICharacter>(identity),
                this.RegisterCorpse,
                this.TraceCorpseFullUpdate,
                this.SendCorpseFullUpdate);
        }

        internal void ScheduleCorpseSpawn(ICharacter target, Identity corpseIdentity)
        {
            DateTime spawnsAtUtc = DateTime.UtcNow + NpcCorpseLifecycleRules.CorpseSpawnDelay;
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
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                PlayfieldLifecycleTrace.StageCorpseSpawnScheduled,
                "CorpseSpawnScheduled",
                corpseIdentity,
                "deadNpc=" + target.Identity + " delayMs="
                + ((int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds));

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Corpse scheduled corpse={0} deadNpc={1} delayMs={2}",
                    corpseIdentity,
                    target.Identity,
                    (int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds));
        }

        private void RegisterCorpse(ICharacter target, Identity corpseIdentity)
        {
            List<CorpseLootItem> lootItems = this.RollCorpseLootItems(target);
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
                InventoryHandle = this.AllocateCorpseInventoryHandle(),
                ExpiresAtUtc = expiresAtUtc
            };

            this.corpses[corpseIdentity.Instance] = state;
            this.runtimeSystems.ScheduleNpcCorpseDespawn(corpseIdentity, expiresAtUtc);

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

        private void TraceCorpseFullUpdate(Identity corpseIdentity, Identity deadNpcIdentity)
        {
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                PlayfieldLifecycleTrace.StageCorpseFullUpdate,
                PlayfieldLifecycleTrace.MessageCorpseFullUpdate,
                corpseIdentity,
                "deadNpc=" + deadNpcIdentity);
        }

        private void DespawnCorpse(int corpseInstance)
        {
            this.runtimeSystems.DespawnCorpse(
                corpseInstance,
                this.Despawn,
                this.runtimeSystems.ClearNpcCorpseDespawn,
                x => this.corpses.Remove(x),
                x => this.pendingCorpseCreditAwards.Remove(x));
        }

        private void ScheduleCorpseDespawn(CorpseState corpse, TimeSpan delay, string reason)
        {
            DateTime expiresAtUtc = DateTime.UtcNow + delay;
            corpse.ExpiresAtUtc = expiresAtUtc;
            this.runtimeSystems.ScheduleNpcCorpseDespawn(corpse.CorpseIdentity, expiresAtUtc);

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
            this.runtimeSystems.ScheduleNpcCorpseDespawn(corpse.CorpseIdentity, expiresAtUtc);

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

            public int InventoryHandle { get; set; }

            public List<CorpseLootItem> LootItems { get; set; }

            public int Credits { get; set; }

            public bool CreditsLooted { get; set; }

            public bool Opened { get; set; }

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

            public Identity LootIdentity { get; set; }

            public bool Looted { get; set; }
        }

        private class PendingCorpseCreditAward
        {
            public Identity LooterIdentity { get; set; }

            public int CorpseInstance { get; set; }

            public DateTime DueAtUtc { get; set; }
        }

        internal Identity AllocateCorpseIdentity()
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

        private int AllocateCorpseInventoryHandle()
        {
            int handle = this.nextCorpseInventoryHandle++;
            if (this.nextCorpseInventoryHandle > 0xff)
            {
                this.nextCorpseInventoryHandle = 0x70;
            }

            return handle;
        }

        private Identity AllocateCorpseLootItemIdentity()
        {
            this.nextCorpseLootItemInstance++;
            if (this.nextCorpseLootItemInstance > 0x00FFFFFF)
            {
                this.nextCorpseLootItemInstance = 0x00200001;
            }

            return new Identity
            {
                Type = (IdentityType)CorpseLootItemIdentityType,
                Instance = this.nextCorpseLootItemInstance
            };
        }

        internal bool CanBuildKnownCorpseVisual(ICharacter target)
        {
            return IsCapturedCleaningRobot(target)
                   || CombatCorpseVisuals.IsUsableVisualId(target.Stats[StatIds.catmesh].Value)
                   || MonsterDataToCorpseCatMesh.ContainsKey(target.Stats[StatIds.monsterdata].Value);
        }

        private static int CorpseCatMeshFor(ICharacter target)
        {
            if (IsCapturedCleaningRobot(target))
            {
                return CapturedCleaningRobotCorpseCatMesh;
            }

            return CombatCorpseVisuals.CorpseCatMeshFor(
                target.Stats[StatIds.catmesh].Value,
                target.Stats[StatIds.monsterdata].Value,
                MonsterDataToCorpseCatMesh);
        }

        private static int DeathAnimationKeyFor(ICharacter target)
        {
            if (IsCapturedCleaningRobot(target))
            {
                return NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2;
            }

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

        internal void StopFightingDeadTarget(Identity deadTarget)
        {
            foreach (ICharacter character in this.runtimeSystems.Characters())
            {
                if (character.FightingTarget == deadTarget)
                {
                    if (character.Controller is NPCController)
                    {
                        this.ClearNpcFightingTarget(character);
                    }
                    else
                    {
                        this.runtimeSystems.ClearPlayerFightingTarget(character, this.ClearCombatTracking);
                    }

                    PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                        PlayfieldLifecycleTrace.StageAttackerStopFight,
                        PlayfieldLifecycleTrace.MessageStopFight,
                        character.Identity,
                        "deadTarget=" + deadTarget);
                    this.SendCombatStopMessage(character);
                }
            }
        }

        private void SendCombatStopMessage(ICharacter character)
        {
            var stopFight = new StopFightMessage { Identity = character.Identity, Unknown1 = 1 };

            this.Announce(stopFight);
        }

        private List<CorpseLootItem> RollCorpseLootItems(ICharacter target)
        {
            var lootItems = new List<CorpseLootItem>();

            if (IsCapturedCleaningRobot(target))
            {
                return this.RollCapturedCleaningRobotLootItems();
            }

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

                lootItems.Add(
                    new CorpseLootItem
                    {
                        Slot = lootItems.Count,
                        Item = item,
                        LootIdentity = this.AllocateCorpseLootItemIdentity()
                    });
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

        private List<CorpseLootItem> RollCapturedCleaningRobotLootItems()
        {
            var lootItems = new List<CorpseLootItem>();
            int[] outcome = CapturedCleaningRobotLootOutcomes[NextLootRandom(CapturedCleaningRobotLootOutcomes.Length)];

            foreach (int templateId in outcome)
            {
                Item item = CreateLootItem(new[] { templateId }, 1);
                if (item == null)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        string.Format(
                            "Captured cleaning robot loot skipped missing item template templateId={0}",
                            templateId));
                    continue;
                }

                lootItems.Add(
                    new CorpseLootItem
                    {
                        Slot = lootItems.Count,
                        Item = item,
                        LootIdentity = this.AllocateCorpseLootItemIdentity()
                    });
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Captured cleaning robot loot rolled items={0} source=live-capture-20260629-142800",
                    lootItems.Count));

            return lootItems;
        }

        private static int RollCorpseCredits(ICharacter target)
        {
            if (target == null)
            {
                return 0;
            }

            if (IsCapturedCleaningRobot(target))
            {
                return CapturedCleaningRobotCorpseCredits;
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

        private static InventoryEntry CreateCorpseInventoryEntry(CorpseLootItem lootItem)
        {
            return new InventoryEntry
            {
                Slotnumber = lootItem.Slot,
                UnknownFlags = 0x00A1,
                Unknown1 = InventoryEntryCountFor(lootItem.Item),
                Identity = lootItem.LootIdentity,
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
                    SlotnumberInMainInventory = corpse.InventoryHandle,
                    Unknown2 = 1
                },
                looter.Identity.Instance);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse InventoryUpdate sent looter={0} corpse={1} slots={2} unknown1=2 handle={3} unknown2=1 entries={4}",
                    looter.Identity,
                    corpse.CorpseIdentity,
                    CombatCorpseRules.CorpseInventorySlots,
                    corpse.InventoryHandle,
                    entries.Length));
        }

        private void ScheduleCorpseCreditAward(ICharacter looter, CorpseState corpse)
        {
            if (looter == null || corpse == null || corpse.CreditsLooted || corpse.Credits <= 0)
            {
                return;
            }

            if (this.pendingCorpseCreditAwards.ContainsKey(corpse.CorpseIdentity.Instance))
            {
                return;
            }

            DateTime dueAtUtc = DateTime.UtcNow + CorpseCreditAwardDelay;
            this.pendingCorpseCreditAwards[corpse.CorpseIdentity.Instance] =
                new PendingCorpseCreditAward
                {
                    CorpseInstance = corpse.CorpseIdentity.Instance,
                    LooterIdentity = looter.Identity,
                    DueAtUtc = dueAtUtc
                };

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Corpse credits scheduled corpse={0} looter={1} credits={2} delayMs={3}",
                    corpse.CorpseIdentity,
                    looter.Identity,
                    corpse.Credits,
                    (int)CorpseCreditAwardDelay.TotalMilliseconds));
        }

        private void ProcessPendingCorpseCreditAwards()
        {
            this.runtimeSystems.ProcessPendingCorpseCreditAwards(
                this.pendingCorpseCreditAwards,
                this.corpses,
                award => award.DueAtUtc,
                award => award.CorpseInstance,
                award => award.LooterIdentity,
                corpse => corpse.CorpseIdentity,
                identity => this.FindByIdentity<ICharacter>(identity),
                looter => looter.InPlayfield(this.Identity),
                this.AwardCorpseCredits);
        }

        private void AwardCorpseCredits(ICharacter looter, CorpseState corpse)
        {
            if (looter == null || corpse == null || corpse.CreditsLooted || corpse.Credits <= 0)
            {
                return;
            }

            uint cashBeforeBase = looter.Stats[StatIds.cash].BaseValue;
            int cashBefore = CashStatRules.Clamp(cashBeforeBase);
            corpse.CreditsLooted = true;
            int cashAfter = CashStatRules.Clamp((long)cashBefore + corpse.Credits);

            looter.Stats[StatIds.cash].Set((uint)cashAfter);
            this.runtimeSystems.SendChangedStatsIfClient(looter, CharacterHasClient, SendStatChangedMessage);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "Corpse credits awarded corpse={0} looter={1} credits={2} cashBeforeBase={3} cashAfter={4} inventoryHandle={5}",
                    corpse.CorpseIdentity,
                    looter.Identity,
                    corpse.Credits,
                    cashBeforeBase,
                    cashAfter,
                    corpse.InventoryHandle));

            looter.Stats.Write();
        }

        private static bool CharacterHasClient(ICharacter character)
        {
            return character.Controller != null && character.Controller.Client != null;
        }

        private static void SendStatChangedMessage(ICharacter character)
        {
            StatMessageHandler.Default.SendChanged(character);
        }

        private void SendRewardFeedback(ICharacter character, string text)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown1 = 0,
                    FormattedMessage = text,
                    Unknown2 = 0
                },
                character.Identity.Instance);

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Reward feedback sent char={0} text={1}",
                    character.Identity,
                    text));
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

            public int AttackInfoWeaponInstance { get; set; }
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
