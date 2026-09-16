namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Enums;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using DaoState = AORebirth.Interfaces.Persistence.Missions.MissionLifecycleState;

internal sealed class AuthoredQuestTests
{
    internal sealed class World : IDisposable
    {
        internal readonly Player Player = TestWorld.CreatePlayer(111);
        internal readonly Session Session = new();
        internal readonly AuthoredMissionTestDao Dao = new();
        internal readonly ErrorLogger Logger = new();
        internal readonly DynelRegistry Registry = new();
        internal readonly NpcContentActivationService Npcs;
        internal DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
        readonly InventoryFlushService _flush;
        internal InventoryFlushService Flush => _flush;
        readonly ServiceProvider _services;
        internal World(int playfield = 6553)
        {
            Player.Session = Session; Session.BindPlayer(Player);
            Player.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, new Identity { Type = IdentityType.Playfield, Instance = playfield });
            var locality = new PlayfieldLocality(playfield, null);
            Npcs = new(Player.Playfield, Registry, locality, new StubItemBuilder(), new TemplateCatalog());
            _services = new ServiceCollection().AddSingleton(locality).AddSingleton(Registry).AddSingleton(Npcs).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Player.Playfield, _services);
            Registry.Register(Player);
            var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
            typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Dictionary<int, Player>());
            _flush = new(new Lazy<PlayfieldManager>(() => manager), new NoIndependentFlush(), new StubLogger());
        }
        internal Item Add(int template)
        {
            var item = TestWorld.CreateItem(lowId: template, highId: template, instanceId: 10); item.StackCount = 1;
            Player.Inventory.Inventory.Add(64, item);
            Dao.Items.Add(10, new() { InstanceId = 10, ContainerType = 104, ContainerInstance = 111, ContainerPlacement = 64,
                ItemType = item.Identity.Type != IdentityType.None ? (int)item.Identity.Type : item.Definition.ItemType,
                LowId = template, HighId = template, Quality = 1, StackCount = 1, Source = (byte)item.Source });
            return item;
        }
        internal NpcCharacter AddNpc(string contentIdentity, bool accepted = true, bool vendor = false, int instance = 222)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new StubItemBuilder())
            { Playfield = Player.Playfield, Position = Player.Position, Name = "Fixture accepted actor" };
            Registry.Register(npc);
            if (accepted) Npcs.Bind(npc, new("fixture:" + instance, "accepted-test-contract", contentIdentity,
                Player.Playfield!.Identity.Instance, true, vendor));
            return npc;
        }
        public void Dispose() { _flush.Dispose(); _services.Dispose(); }
    }
    sealed class Ids : IItemInstanceIdAllocator { int _next = 1000; public int Allocate() => _next++; }
    internal sealed class ErrorLogger : ZoneEngine_New.Core.Logging.IZoneLogger
    {
        internal string LastError = string.Empty;
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => LastError = message;
        public void Error(Exception exception, string message) => LastError = message + " " + exception;
        public ZoneEngine_New.Core.Logging.IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }
    sealed class TemplateCatalog : IItemTemplateCatalog
    {
        public bool TryGet(int id, out ItemTemplate template) { template = Require(id); return true; }
        public ItemTemplate Require(int id) => new() { Id = id, Quality = 1 };
    }
    sealed class NoIndependentFlush : ICharacterCoalesceCommit
    { public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates, int owner, IReadOnlyList<int> nanos) => throw new InvalidOperationException("No independent item transaction expected."); }
    internal sealed class Session : IZoneSession
    {
        internal readonly List<object> Messages = [];
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new NotSupportedException();
        public void Send(byte[] packet) => Messages.Add(packet);
        public void Send(Message message) => Messages.Add(message.Body);
        public void Send(MessageBody body) => Messages.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new NotSupportedException();
        public void Close() => State = SessionState.Closed;
    }
}
