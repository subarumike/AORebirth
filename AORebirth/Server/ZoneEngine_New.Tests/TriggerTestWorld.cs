namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using Microsoft.Extensions.DependencyInjection;
    using AORebirth.Core.GameData;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>Real trigger/door services and two independently recorded sessions; persistence rejects every call.</summary>
    internal sealed class TriggerTestWorld : IDisposable
    {
        readonly ServiceProvider _services;
        public Playfield Playfield { get; }
        public DynelRegistry Registry { get; } = new();
        public PlayfieldWorldSimulation World { get; }
        public AreaTriggerService Triggers { get; }
        public PlayfieldLocality Locality { get; }
        public Player First { get; }
        public Player Second { get; }
        public RecordingZoneSession FirstSession => (RecordingZoneSession)First.Session!;
        public RecordingZoneSession SecondSession => (RecordingZoneSession)Second.Session!;

        public TriggerTestWorld(StaticObjectStateMachineCatalog? catalog = null)
        {
            Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            Locality = new PlayfieldLocality(1, null);
            // The real constructor initializes door ownership/LOS. No geometry, database or native collision is needed.
            var gameData = new StubGameData(HashItemCatalog.Parse(null), mobs: new Dictionary<string, MobTemplate>());
            World = (PlayfieldWorldSimulation)Activator.CreateInstance(typeof(PlayfieldWorldSimulation),
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
                args: new object?[] { 1, null, null, null, gameData, new StubLogger() }, culture: null)!;
            var access = new WorldSimulationAccess { Instance = World };
            var services = new ServiceCollection()
                .AddSingleton(Playfield).AddSingleton(Registry).AddSingleton(Locality).AddSingleton(access)
                .AddSingleton<IGameData>(gameData)
                .AddSingleton<IZoneLogger>(new StubLogger())
                .AddSingleton<IItemBuilder>(new StubItemBuilder())
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(catalog ?? StaticObjectStateMachineCatalog.Empty)
                .AddSingleton<AreaTriggerService>();
            _services = services.BuildServiceProvider();
            SetField("_serviceProvider", _services);
            SetField("_lifecycleSync", new Lock());
            SetField("_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            Triggers = _services.GetRequiredService<AreaTriggerService>();
            First = AddPlayer(1);
            Second = AddPlayer(2);
        }

        void SetField(string name, object value) => typeof(Playfield)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Playfield, value);

        public Player AddPlayer(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            var session = new RecordingZoneSession { State = SessionState.InPlay };
            session.BindPlayer(player);
            player.Session = session;
            player.Playfield = Playfield;
            Registry.Register(player);
            Locality.RegisterDynel(player);
            return player;
        }

        public StaticDynel Add(int id, double x, double y, double z, params ItemSpell[] rows)
            => Add(id, IdentityType.Terminal, new ItemTemplate
            {
                Id = id, Name = "control", SpellList = new() { [AORebirth.Enums.EventType.OnUse] = new(rows) }
            }, new Vector3(x, y, z));

        public StaticDynel AddDoor(int id, double x, double y, double z, bool locked = true)
        {
            StaticDynel door = Add(id, IdentityType.Door, new ItemTemplate
            {
                Id = id, Stats = new() { [CharacterStat.Flags] = locked ? Door.LockedFlag : 0 }
            }, new Vector3(x, y, z));
            World.BindStaticDoor(door);
            return door;
        }

        public StaticDynel Add(int id, IdentityType type, ItemTemplate template, Vector3 position)
        {
            var item = new PlayfieldStaticDynel(new Identity { Type = type, Instance = id }, template)
            { Playfield = Playfield, Position = position };
            Registry.Register(item);
            Locality.RegisterDynel(item);
            Triggers.Register(item);
            return item;
        }

        public void Tick(double seconds) => Triggers.Tick(seconds);
        public void Dispose() { Triggers.Dispose(); World.Dispose(); _services.Dispose(); }
    }
}
