namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

using AORebirth.Core.GameData;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class ScheolVicinityTrapTests
{
    const int TrapId = 930002;
    const int PlayfieldId = 592;
    const int RespawnSeconds = 300;
    const string CreatureHash = "CRTR";
    const string Warning = "A creature emerges from the ground.";
    static readonly Vector3 Entrance = new(60, 0, 60);
    static readonly Vector3 Outside = new(50, 0, 60);

    [TestMethod]
    public void TrapCleanupConsumesTheSpawnAndHonorsItsRespawnTimer()
    {
        using var field = new TrapPlayfield();
        Player first = field.AddPlayer(1);
        WorldItem original = field.Trap;
        field.Enter(first);

        Assert.AreEqual(1, field.Creatures.Length);
        Assert.AreEqual(1, Warns(first));
        Assert.AreEqual(100, first.Stats.GetOrZero(CharacterStat.Health));
        Assert.IsFalse(first.IsDead);
        Assert.AreEqual(0, first.Stats.GetOrZero(CharacterStat.TimeExist));
        Assert.IsNull(original.Playfield);
        Assert.IsFalse(field.Registry.TryGet(original.Identity, out _));
        Assert.AreEqual(0, field.Simulation.VicinityTriggerCount);
        Assert.AreEqual(0, field.Point.MemberCount);
        Assert.AreEqual(HashSpawnState.Dead, field.Point.State);
        Assert.IsTrue(field.Point.NextSpawnTime > DateTime.UtcNow);

        field.TickSpawner();
        Assert.AreEqual(0, field.Point.MemberCount, "The normal respawn delay must still apply.");
        field.Point.NextSpawnTime = DateTime.UtcNow.AddSeconds(-1);
        field.TickSpawner();
        WorldItem replacement = field.Trap;
        Assert.AreNotEqual(original.Identity, replacement.Identity);
        Assert.IsTrue(field.Registry.TryGet(replacement.Identity, out _));
        Assert.AreEqual(1, field.Simulation.VicinityTriggerCount);

        first.Position = Outside;
        field.TickVicinity();
        field.Enter(first);
        Assert.AreEqual(2, field.Creatures.Length);
        Assert.AreEqual(2, Warns(first));
        Assert.AreEqual(100, first.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(0, field.Point.MemberCount);
    }

    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(0, 5)]
    public void IneligibleCrossingDoesNotConsumeTheTrap(int gmLevel, int npcFamily)
    {
        using var field = new TrapPlayfield();
        Player ineligible = field.AddPlayer(1, gmLevel, npcFamily);
        Player eligible = field.AddPlayer(2);
        WorldItem armed = field.Trap;
        field.Enter(ineligible);
        Assert.AreEqual(100, ineligible.Stats.GetOrZero(CharacterStat.Health));
        Assert.IsFalse(ineligible.IsDead);
        Assert.AreEqual(0, Warns(ineligible));
        Assert.AreEqual(0, field.Creatures.Length);
        Assert.AreSame(armed, field.Trap);
        Assert.IsTrue(field.Registry.TryGet(armed.Identity, out _));

        field.Enter(eligible);
        Assert.AreEqual(100, eligible.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(1, Warns(eligible));
        Assert.AreEqual(1, field.Creatures.Length);
        Assert.AreEqual(0, field.Simulation.VicinityTriggerCount);
    }

    [TestMethod]
    public void CrossingsInTheSamePlayfieldTickConsumeOnlyOneTrap()
    {
        using var field = new TrapPlayfield();
        Player first = field.AddPlayer(1);
        Player second = field.AddPlayer(2);
        WorldItem armed = field.Trap;
        field.Enter(first, second);

        Assert.AreEqual(1, field.Creatures.Length);
        Assert.AreEqual(1, Warns(first) + Warns(second));
        Assert.AreEqual(100, first.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(100, second.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(0, field.Point.MemberCount);
        Assert.IsFalse(field.Registry.TryGet(armed.Identity, out _));
    }

    [TestMethod]
    public void OrdinaryVicinityActionStillAppliesToTheEnteringPlayer()
    {
        using var field = new TrapPlayfield(withTrap: false);
        Player player = field.AddPlayer(1);
        var template = new ItemTemplate
        {
            Id = TrapId + 1,
            SpellList = new()
            {
                [EventType.OnTargetInVicinity] =
                    [Action(FunctionType.Set, ItemTarget.Wearer, [(int)CharacterStat.CurrentNano, 27])]
            }
        };
        var identity = field.Registry.AllocateStaticDynelIdentity();
        var pad = new WorldItem(identity, template, template.Id, template.Id, 1)
        {
            Playfield = field.Playfield,
            Position = Entrance
        };
        field.Registry.Register(pad);
        Assert.IsTrue(field.Simulation.RegisterVicinityDynel(identity, template, Entrance.xf, Entrance.yf, Entrance.zf));
        field.Enter(player);

        Assert.AreEqual(27, player.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.IsTrue(field.Registry.TryGet(identity, out _));
        Assert.AreEqual(1, field.Simulation.VicinityTriggerCount);
    }

    static int Warns(Player player) => ((RecordingZoneSession)player.Session!).Sent
        .OfType<ChatTextMessage>().Count(message => message.Text == Warning);

    static ItemTemplate TrapTemplate() => new()
    {
        Id = TrapId,
        Quality = 1,
        Name = "Fixture proximity trap",
        SpellList = new()
        {
            [EventType.OnTargetInVicinity] =
            [
                Action(FunctionType.SpawnMonster2, ItemTarget.Wearer, [CreatureHash, 300, 600], Eligible()),
                Action(FunctionType.Text, ItemTarget.Wearer, [Warning], Eligible()),
                Action(FunctionType.Set, ItemTarget.User, [(int)CharacterStat.Health, 0]),
                Action(FunctionType.Set, ItemTarget.User, [(int)CharacterStat.TimeExist, 1])
            ]
        }
    };

    static List<ItemRequirement> Eligible() =>
    [
        new() { Operator = (int)Operator.EqualTo, StatNumber = (int)CharacterStat.NPCFamily },
        new() { Operator = (int)Operator.EqualTo, StatNumber = (int)CharacterStat.GmLevel },
        new() { Operator = (int)Operator.And }
    ];

    static ItemSpell Action(FunctionType function, ItemTarget target, List<object> arguments,
        List<ItemRequirement>? requirements = null) => new()
    {
        FunctionType = (int)function,
        Target = (int)target,
        TickCount = 1,
        Arguments = arguments,
        Requirements = requirements ?? []
    };

    sealed class TrapPlayfield : IDisposable
    {
        readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "ao-rebirth-vicinity-" + Guid.NewGuid().ToString("N"));
        readonly Lock _tick = new();
        readonly ServiceProvider _services;
        readonly PlayfieldLocality _locality;
        readonly HashSpawnSystem _spawns;
        internal Playfield Playfield { get; } = Blank<Playfield>();
        internal DynelRegistry Registry { get; } = new();
        internal PlayfieldWorldSimulation Simulation { get; }
        internal HashSpawnPoint Point { get; }
        internal WorldItem Trap => Point.MembersSnapshot().OfType<WorldItem>().Single();
        internal NpcCharacter[] Creatures => Registry.Dynels().OfType<NpcCharacter>().ToArray();

        internal TrapPlayfield(bool withTrap = true)
        {
            string areaData = Path.Combine(_dataRoot, "Playfields", PlayfieldId.ToString());
            Directory.CreateDirectory(areaData);
            File.WriteAllText(Path.Combine(_dataRoot, "NpcTemplates.json"),
                """{"CRTR":{"Templates":[{"Name":"Fixture creature","Level":300,"Stats":{"54":300,"1":100},"Features":["NoCombat"]}]}}""");
            if (withTrap)
            {
                var spawn = new PlayfieldSpawnEntry
                {
                    StaticTemplateId = TrapId, HashText = "TRAP", MinLevel = 1, MaxLevel = 1,
                    RespawnChance = 100, RespawnTime = RespawnSeconds, Position = [Entrance.xf, Entrance.yf, Entrance.zf]
                };
                File.WriteAllText(Path.Combine(areaData, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData
                {
                    SchemaVersion = 1, PlayfieldId = PlayfieldId, Spawns = [spawn]
                }));
            }

            SetField(Playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = PlayfieldId });
            SetField(Playfield, "<ResourceId>k__BackingField", PlayfieldId);
            SetField(Playfield, "_tickSync", _tick);
            SetField(Playfield, "_lifecycleSync", new Lock());
            SetField(Playfield, "_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            var logger = new StubLogger();
            var data = new GameDataStore(logger, null, _dataRoot);
            var items = new StubItemBuilder().Add(TrapTemplate());
            var catalog = new StubCatalog().Add(TrapId, 1);
            var minter = new HashItemMinter(data, catalog, items);
            var inventoryMoves = new InventoryMoveService(logger, Blank<InventoryFlushService>(), Blank<InventoryActionService>());
            _locality = new PlayfieldLocality(PlayfieldId, null);
            Simulation = PlayfieldWorldSimulation.Create(PlayfieldId, new PlayfieldGeometryData(),
                null, DestinationsCatalog.Instance, data, logger, catalog);
            _services = new ServiceCollection()
                .AddSingleton(Playfield).AddSingleton(Registry).AddSingleton(_locality)
                .AddSingleton<IZoneLogger>(logger).AddSingleton<IGameData>(data)
                .AddSingleton<IItemBuilder>(items).AddSingleton<IItemTemplateCatalog>(catalog)
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(inventoryMoves)
                .AddSingleton(new WorldSimulationAccess { Instance = Simulation })
                .AddSingleton(StaticObjectStateMachineCatalog.Empty).AddSingleton<AreaTriggerService>()
                .AddSingleton(provider => new SpawnService(provider, Registry, logger, Playfield,
                    Blank<PlayfieldManager>(), data, items, minter, Blank<InventoryFlushService>(),
                    Blank<TradeService>(), Blank<CharacterSnapshotService>()))
                .AddSingleton(provider => new HashSpawnSystem(Playfield, provider.GetRequiredService<SpawnService>(), data, logger))
                .BuildServiceProvider();
            SetField(Playfield, "_serviceProvider", _services);
            _spawns = _services.GetRequiredService<HashSpawnSystem>();
            _spawns.Initialize(_locality);
            Point = withTrap
                ? ((List<HashSpawnPoint>)GetField(_spawns, "_allPoints")).Single()
                : null!;
            if (withTrap)
                TickSpawner();
        }

        internal Player AddPlayer(int id, int gmLevel = 0, int npcFamily = 0)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Playfield = Playfield;
            player.Position = Outside;
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 100);
            player.Stats.Set(CharacterStat.MaxNanoEnergy, 100);
            player.Stats.Set(CharacterStat.GmLevel, gmLevel);
            player.Stats.Set(CharacterStat.NPCFamily, npcFamily);
            var session = new RecordingZoneSession { State = SessionState.InPlay };
            session.BindPlayer(player);
            player.Session = session;
            Registry.Register(player);
            _locality.RegisterDynel(player);
            return player;
        }

        internal void Enter(params Player[] players)
        {
            TickVicinity();
            foreach (Player player in players)
                player.Position = Entrance;
            TickVicinity();
        }

        internal void TickVicinity()
        {
            lock (_tick)
                Simulation.TickSoftTriggers(Playfield, 0.1);
        }

        internal void TickSpawner()
        {
            lock (_tick)
                typeof(HashSpawnSystem).GetMethod("OnCellTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(_spawns, [Point.CellId]);
        }

        public void Dispose()
        {
            _services.Dispose();
            Simulation.Dispose();
            Directory.Delete(_dataRoot, recursive: true);
        }

        static T Blank<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        static object GetField(object target, string name)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
        static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
