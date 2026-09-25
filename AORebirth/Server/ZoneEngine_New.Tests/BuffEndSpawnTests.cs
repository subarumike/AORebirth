namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Trade;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class BuffEndSpawnTests
    {
        const int NanoId = 205606;
        const string KhalHash = "KHAL";
        const int KhalLevel = 73;

        static readonly Identity Caster = new() { Type = IdentityType.CanbeAffected, Instance = 42 };

        [TestMethod]
        public void CancelRunsOnTerminateHitAndDoesNotRerunOnUse()
        {
            using var world = new TerminateWorld();
            Player player = world.Player;
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 40);
            NanoSpell spell = TestNanos.Create(
                NanoId,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, 20, 20]
                    }
                ],
                terminate:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, 7, 7]
                    }
                ]);
            world.Items.Add(spell);

            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(spell, Caster, DateTime.UtcNow, out _, out _));
            Assert.AreEqual(40, player.Stats.GetOrZero(CharacterStat.Health));

            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(player, NanoId));
            Assert.AreEqual(47, player.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(0, player.Buffs.Count);
        }

        [TestMethod]
        public void ExpiryRunsOnTerminateHit()
        {
            using var world = new TerminateWorld();
            Player player = world.Player;
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 40);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            NanoSpell spell = TestNanos.Create(
                NanoId,
                durationCentiseconds: 1000,
                terminate:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, 5, 5]
                    }
                ]);
            world.Items.Add(spell);

            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(spell, Caster, start, out _, out _));

            NanoRuntime.Tick(player, start.AddSeconds(9));
            Assert.AreEqual(1, player.Buffs.Count);
            Assert.AreEqual(40, player.Stats.GetOrZero(CharacterStat.Health));

            NanoRuntime.Tick(player, start.AddSeconds(10));
            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(45, player.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void SpawnMonster2WithoutPlayfieldFails()
        {
            var player = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 700 },
                new StubItemBuilder());
            var template = new ItemTemplate
            {
                Id = NanoId,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnTerminate] = [TestNanos.SpawnMonster2(KhalHash, KhalLevel)]
                }
            };

            Assert.IsFalse(template.ExecuteTerminateSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
        }

        [TestMethod]
        public void CancelSpawnsKhalAtOwnerFromOnTerminate()
        {
            using var world = new SummonWorld();
            Player player = world.Player;
            NanoSpell spell = TestNanos.Create(
                NanoId,
                terminate: [TestNanos.SpawnMonster2(KhalHash, KhalLevel)]);
            world.Items.Add(spell);

            Assert.AreEqual(
                BuffApplyDecision.Apply,
                player.TryApplyBuff(spell, Caster, DateTime.UtcNow, out _, out _));
            Assert.AreEqual(0, world.Summoned.Count());

            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(player, NanoId));
            NpcCharacter spawned = world.Summoned.Single();
            Assert.AreEqual("Khal", spawned.Name);
            Assert.AreEqual(KhalLevel, spawned.Stats.GetOrZero(CharacterStat.Level));
            Assert.AreEqual(SpawnSource.Summoned, spawned.SpawnSource);
            Assert.AreEqual(player.Position.x, spawned.Position.x);
            Assert.AreEqual(player.Position.z, spawned.Position.z);
        }

        [TestMethod]
        public void UnknownHashDoesNotSpawn()
        {
            using var world = new SummonWorld();
            Player player = world.Player;
            NanoSpell spell = TestNanos.Create(
                NanoId,
                terminate: [TestNanos.SpawnMonster2("MISS", KhalLevel)]);
            world.Items.Add(spell);
            player.TryApplyBuff(spell, Caster, DateTime.UtcNow, out _, out _);

            Assert.AreEqual(BuffRemovalOutcome.Removed, NanoRuntime.TryCancelBuff(player, NanoId));
            Assert.AreEqual(0, world.Summoned.Count());
        }

        sealed class TerminateWorld : IDisposable
        {
            readonly ServiceProvider _services;

            public StubItemBuilder Items { get; } = new();

            public Playfield Playfield { get; }

            public Player Player { get; }

            public TerminateWorld()
            {
                var registry = new DynelRegistry();
                var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, new Dictionary<int, Player>());

                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection()
                    .AddSingleton(registry)
                    .AddSingleton(manager)
                    .AddSingleton<IItemBuilder>(Items)
                    .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new ConcurrentDictionary<Character, byte>());

                Player = TestWorld.CreatePlayer(1);
                Player.Stats.Set(CharacterStat.MaxNCU, 60);
                Player.Playfield = Playfield;
                Player.Position = new Vector3(10, 4, 12);
            }

            public void Dispose() => _services.Dispose();
        }

        sealed class SummonWorld : IDisposable
        {
            readonly ServiceProvider _services;
            readonly InventoryFlushService _flush;
            readonly DynelRegistry _registry;

            public StubItemBuilder Items { get; } = new();

            public Playfield Playfield { get; }

            public Player Player { get; }

            public IEnumerable<NpcCharacter> Summoned
                => _registry.Dynels().OfType<NpcCharacter>();

            public SummonWorld()
            {
                const int playfieldId = 4582;
                _registry = new DynelRegistry();
                var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(manager, new Dictionary<int, Player>());

                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new Identity { Type = IdentityType.Playfield2, Instance = playfieldId });
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new ConcurrentDictionary<Character, byte>());

                var locality = new PlayfieldLocality(playfieldId, null);
                var catalog = new StubCatalog();
                var mobs = new Dictionary<string, MobTemplate>(StringComparer.Ordinal)
                {
                    [KhalHash] = new MobTemplate
                    {
                        Hash = KhalHash,
                        Name = "Khal",
                        Attackable = false,
                        Stats = new Dictionary<int, int> { [(int)CharacterStat.Level] = KhalLevel }
                    }
                };
                var data = new StubGameData(HashItemCatalog.Parse("{}"), mobs: mobs);
                var minter = new HashItemMinter(data, catalog, Items);
                _flush = new InventoryFlushService(
                    new Lazy<PlayfieldManager>(() => manager),
                    new SilentCoalesce(),
                    new StubLogger());
                var trades = new TradeService(
                    new StubLogger(),
                    data,
                    catalog,
                    minter,
                    new Ids(),
                    _flush,
                    new SilentTrade());
                var snapshots = new PlayfieldTransferTests.SnapshotStore();

                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton(locality)
                    .AddSingleton(manager)
                    .AddSingleton<IGameData>(data)
                    .AddSingleton<IItemBuilder>(Items)
                    .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                    .AddSingleton(new WorldSimulationAccess())
                    .AddSingleton(trades)
                    .AddSingleton(_flush)
                    .AddSingleton(minter)
                    .AddSingleton<SpawnService>(services => new SpawnService(
                        services,
                        _registry,
                        new StubLogger(),
                        Playfield,
                        manager,
                        data,
                        Items,
                        minter,
                        _flush,
                        trades,
                        new CharacterSnapshotService(snapshots, snapshots, new StubLogger())))
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);

                Player = TestWorld.CreatePlayer(1);
                Player.Stats.Set(CharacterStat.MaxNCU, 60);
                Player.Playfield = Playfield;
                Player.Position = new Vector3(10, 4, 12);
                _registry.Register(Player);
            }

            public void Dispose()
            {
                _flush.Dispose();
                _services.Dispose();
            }
        }

        sealed class Ids : IItemInstanceIdAllocator
        {
            int _next = 98000;

            public int Allocate() => _next++;
        }

        sealed class SilentCoalesce : ICharacterCoalesceCommit
        {
            public void Persist(
                IReadOnlyList<ItemInstanceRecord> inserts,
                IReadOnlyList<ItemLocationUpdate> updates,
                int characterId,
                IReadOnlyList<int> nanos,
                IReadOnlyList<ActiveNanoRecord>? activeNanos,
                IReadOnlyList<SkillLockRecord>? skillLocks)
            {
            }
        }

        sealed class SilentTrade : ITradePersistence
        {
            public void Persist(TradePersistenceBatch batch)
            {
            }
        }
    }
}
