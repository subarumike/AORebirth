namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using AORebirth.World.Contracts.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Pets;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Teams;
using ZoneEngine_New.Core.Trade;
using ZoneEngine_New.Core.WorldSimulation;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class HealthArrivalSynchronizationTests
{
    const int ArrivingCharacterId = 95041;
    const int RetainedNanoId = 602001;
    const int RemovedNanoId = 602002;
    const int RetainedHealthBonus = 200;
    const int RemovedHealthBonus = 300;
    // An unnamed PC at level 1 without optional prefixes has its HP fields at byte 67 of the N3 body.
    const int MinimalPlayerHealthOffset = 67;
    const int MessageHeaderLength = 16;

    [TestMethod]
    [DataRow(4692, 4437, "1254FF")]
    [DataRow(4692, 4436, "12541154")]
    [DataRow(4408, 3699, "11380E73")]
    [DataRow(100000, 85901, "000186A000014F8D")]
    public void SpawnSnapshotWritesTheClientHealthValueForEachWireWidth(int max, int current, string expectedFields)
    {
        byte[] body = MakeSpawnBody(max, current);
        byte[] expected = Convert.FromHexString(expectedFields);
        CollectionAssert.AreEqual(expected, body.AsSpan(MinimalPlayerHealthOffset, expected.Length).ToArray());
    }

    [TestMethod]
    [DataRow(4692, 4437, "1254FF")]
    [DataRow(4692, 4436, "12541154")]
    [DataRow(4408, 3699, "11380E73")]
    [DataRow(100000, 85901, "000186A000014F8D")]
    public void SpawnSnapshotReadsTheClientHealthValueForEachWireWidth(int max, int current, string capturedFields)
    {
        byte[] body = MakeSpawnBody(max, current);
        Convert.FromHexString(capturedFields).CopyTo(body, MinimalPlayerHealthOffset);
        using var stream = new MemoryStream(body);
        using var reader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(stream);
        var resolver = new SerializerResolverBuilder<MessageBody>().Build();
        var result = (SimpleCharFullUpdateMessage)new SimpleCharFullUpdateSerializer()
            .Deserialize(reader, new SerializationContext(resolver));
        Assert.AreEqual(max, result.Health);
        Assert.AreEqual(max - current, result.HealthDamage);
    }

    static byte[] MakeSpawnBody(int max, int current)
    {
        var message = new SimpleCharFullUpdateMessage
        {
            Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = ArrivingCharacterId },
            Version = 58,
            Coordinates = new SmokeLounge.AOtomation.Messaging.GameData.Vector3(),
            Appearance = new Appearance(),
            CharacterInfo = new SimplePcInfo(),
            Level = 1,
            Health = max,
            HealthDamage = max - current,
            Unknown1 = [],
        };
        using var stream = new MemoryStream();
        using var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(stream);
        writer.Position = MessageHeaderLength;
        var resolver = new SerializerResolverBuilder<MessageBody>().Build();
        new SimpleCharFullUpdateSerializer().Serialize(writer, new SerializationContext(resolver), message);
        return stream.ToArray()[MessageHeaderLength..];
    }

    [TestMethod]
    public void CrossInstanceArrivalPublishesSettledVitalsAfterRemovingAPlayshiftNano()
    {
        using var world = new ArrivalWorld();

        RecordingZoneSession owner = world.Arrive(new TransferEnvelope());
        Player player = owner.Player!;

        Assert.IsTrue(player.TryGetBuff(RetainedNanoId, out _));
        Assert.IsFalse(player.TryGetBuff(RemovedNanoId, out _));
        AssertSettledSnapshots(world, owner, player);
    }

    [TestMethod]
    public void RetainedReconnectPublishesSettledVitalsForABuffRemovedWhileLinkDead()
    {
        using var world = new ArrivalWorld();
        RecordingZoneSession previous = world.Arrive(null);
        Player player = previous.Player!;
        Assert.IsTrue(player.TryGetBuff(RemovedNanoId, out _), "A normal login keeps this still-active buff.");
        Assert.AreEqual(ExpectedBaseHealth(player) + RetainedHealthBonus + RemovedHealthBonus,
            player.Stats.GetOrZero(CharacterStat.MaxHealth));

        previous.UnbindPlayer();
        player.EnterLinkDead(TimeSpan.FromSeconds(60));
        world.ObservingSession.Sent.Clear();
        var reconnect = new RecordingZoneSession { State = SessionState.Loading };
        world.OnOwner(() =>
        {
            Assert.AreEqual(BuffRemovalOutcome.Removed, player.TryRemoveBuff(RemovedNanoId, BuffRemovalReason.Stripped, out _));
            world.Spawn.CompletePendingReconnect(reconnect, new PendingReconnectInboundItem
            {
                Session = reconnect,
                CharacterId = ArrivingCharacterId,
            });
        });

        Assert.AreSame(player, reconnect.Player, "A reconnect must keep the live aggregate.");
        Assert.IsNull(player.LinkDeadUntilUtc);
        AssertSettledSnapshots(world, reconnect, player, expectObserverSpawn: false);
    }

    static void AssertSettledSnapshots(ArrivalWorld world, RecordingZoneSession owner, Player player, bool expectObserverSpawn = true)
    {
        int expectedBase = ExpectedBaseHealth(player);
        int expectedMax = expectedBase + RetainedHealthBonus;
        long originalMax = expectedMax + RemovedHealthBonus;
        int expectedHealth = (int)((originalMax / 2) * expectedMax / originalMax);
        Assert.AreEqual(expectedMax, player.Stats.GetOrZero(CharacterStat.MaxHealth));
        Assert.AreEqual(expectedHealth, player.Stats.GetOrZero(CharacterStat.Health));

        SimpleCharFullUpdateMessage self = owner.Sent.OfType<SimpleCharFullUpdateMessage>()
            .Single(packet => packet.Identity == player.Identity);
        FullCharacterMessage full = owner.Sent.OfType<FullCharacterMessage>().Single();
        Assert.IsTrue(owner.Sent.IndexOf(self) < owner.Sent.IndexOf(full));
        Assert.IsFalse(owner.Sent.TakeWhile(packet => !ReferenceEquals(packet, self)).OfType<StatMessage>()
            .Any(packet => packet.Stats.Any(tuple => tuple.Value1 is CharacterStat.Health or CharacterStat.MaxHealth)));
        Assert.AreEqual((uint)expectedBase, full.Stats2.Single(tuple => tuple.Value1 == (int)CharacterStat.MaxHealth).Value2);
        Assert.AreEqual((uint)expectedHealth, full.Stats2.Single(tuple => tuple.Value1 == (int)CharacterStat.Health).Value2);

        AssertSpawnVitals(self, expectedHealth, expectedMax);
        if (expectObserverSpawn)
            AssertSpawnVitals(world.ObservingSession.Sent.OfType<SimpleCharFullUpdateMessage>()
                .Single(packet => packet.Identity == player.Identity), expectedHealth, expectedMax);

        world.OnOwner(() => new LookAtMessageHandler().Handle(new LookAtMessage { Target = player.Identity }, world.ObservingSession));
        var inspected = (CharacterInfoPacket)world.ObservingSession.Sent.OfType<InfoPacketMessage>().Last().Info;
        Assert.AreEqual(expectedHealth, inspected.Health);
        Assert.AreEqual(expectedMax, inspected.MaxHealth);
        world.OnOwner(() => Assert.IsFalse(world.Playfield.RebasePendingStats(player), "The arrival already consumed the queued rebase."));
    }

    static void AssertSpawnVitals(SimpleCharFullUpdateMessage packet, int health, int maxHealth)
    {
        Assert.AreEqual(maxHealth, packet.Health);
        Assert.AreEqual(health, packet.Health - packet.HealthDamage);
        CollectionAssert.AreEqual(new[] { RetainedNanoId }, packet.ActiveNanos.Select(nano => nano.NanoIdentity.Instance).ToArray());
    }

    static int ExpectedBaseHealth(Player player) => MaxHealthCalculator.Compute(1, 1, 1, 1,
        player.Stats.GetOrZero(CharacterStat.BodyDevelopment));

    sealed class ArrivalWorld : IDisposable
    {
        readonly Lock _tick = new();
        readonly ServiceProvider _services;
        readonly InventoryFlushService _flush;
        readonly PlayfieldManager _manager = Blank<PlayfieldManager>();
        readonly PlayfieldLocality _locality = new(500, null);
        readonly Player _observer = TestWorld.CreatePlayer(95042);
        public Playfield Playfield { get; } = Blank<Playfield>();
        public SpawnService Spawn { get; }
        public RecordingZoneSession ObservingSession { get; } = new() { State = SessionState.InPlay };

        public ArrivalWorld()
        {
            var logger = new StubLogger();
            var data = new ArrivalGameData();
            var registry = new DynelRegistry();
            var teams = new TeamService(dispatchOnOwner: (player, action) => player.Playfield?.DispatchPlayerProjection(player, action));
            NanoSpell playshiftRestricted = TestNanos.Create(RemovedNanoId, flags: NanoFlags.IsBuff,
                modifiers: [TestNanos.Modify(CharacterStat.MaxHealth, RemovedHealthBonus)]);
            playshiftRestricted.Actions.Add(new ItemAction
            {
                ActionType = (int)ActionType.PlayShiftRequirements,
                Requirements =
                [
                    new() { StatNumber = (int)CharacterStat.Level, Operator = (int)Operator.GreaterThan, Value = 200 },
                ],
            });
            var items = new StubItemBuilder().Add(TestNanos.Create(RetainedNanoId, flags: NanoFlags.IsBuff,
                modifiers: [TestNanos.Modify(CharacterStat.MaxHealth, RetainedHealthBonus)]))
                .Add(playshiftRestricted);
            _services = new ServiceCollection().AddSingleton<IZoneLogger>(logger).AddSingleton<IItemBuilder>(items)
                .AddSingleton<IGameData>(data).AddSingleton(new PlayerHydrator(items, data)).AddSingleton(registry)
                .AddSingleton(_locality).AddSingleton(teams).AddSingleton(new WorldSimulationAccess())
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton(new PetService(Playfield, data, logger)).BuildServiceProvider();
            Set(_manager, "_sync", new Lock());
            Set(_manager, "_playersByCharacterId", new Dictionary<int, Player>());
            Set(_manager, "_pendingMoveByCharacter", new ConcurrentDictionary<int, int>());
            Set(_manager, "<Teams>k__BackingField", teams);
            Set(Playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = 500 });
            Set(Playfield, "<ResourceId>k__BackingField", 500);
            Set(Playfield, "_tickSync", _tick);
            Set(Playfield, "_lifecycleSync", new Lock());
            Set(Playfield, "_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            Set(Playfield, "_serviceProvider", _services);
            Set(Playfield, "_dynelRegistry", registry);
            Set(Playfield, "_playfieldManager", _manager);
            var persistence = new ArrivalPersistence();
            _flush = new InventoryFlushService(new Lazy<PlayfieldManager>(() => _manager), persistence, logger);
            Spawn = new SpawnService(_services, registry, logger, Playfield, _manager, data, items,
                new HashItemMinter(data, new StubCatalog(), items), _flush, Blank<TradeService>(),
                new CharacterSnapshotService(persistence, persistence, logger));

            _observer.Playfield = Playfield;
            _observer.Position = new Vector3(11, 0, 10);
            _observer.EnterOnline(ObservingSession);
            registry.Register(_observer);
            _locality.RegisterDynel(_observer);
            _locality.ActivatePlayerVisibility(_observer);
        }

        public void OnOwner(Action action)
        {
            lock (_tick)
                action();
        }

        public RecordingZoneSession Arrive(TransferEnvelope? envelope)
        {
            var session = new RecordingZoneSession { State = SessionState.Loading };
            OnOwner(() => Spawn.CompletePendingSpawn(session, new PendingSpawnInboundItem
            {
                Session = session,
                Hydration = Hydration(),
                Envelope = envelope,
            }));
            return session;
        }

        public void Dispose()
        {
            if (_manager.FindPlayer(ArrivingCharacterId, out Player player))
                player.ReleaseOnlineOwnership();
            _flush.Dispose();
            _services.Dispose();
        }

        static CharacterHydrationResult Hydration()
        {
            var stats = new Dictionary<CharacterStat, int>
            {
                [CharacterStat.Flags] = 0x00081241, [CharacterStat.Breed] = 1, [CharacterStat.Sex] = 2,
                [CharacterStat.Profession] = 1, [CharacterStat.Fatness] = 0, [CharacterStat.Race] = 1,
                [CharacterStat.HeadMesh] = 40683, [CharacterStat.VisualFlags] = 31, [CharacterStat.Scale] = 100,
                [CharacterStat.Level] = 1, [CharacterStat.TitleLevel] = 1, [CharacterStat.Side] = 0,
                [CharacterStat.Expansion] = 171, [CharacterStat.Strength] = 6, [CharacterStat.Agility] = 6,
                [CharacterStat.Stamina] = 6, [CharacterStat.Intelligence] = 6, [CharacterStat.Sense] = 6,
                [CharacterStat.Psychic] = 6, [CharacterStat.BodyDevelopment] = 5, [CharacterStat.NanoPool] = 5,
                [CharacterStat.Health] = 50, [CharacterStat.MaxHealth] = 100, [CharacterStat.PercentRemainingHealth] = 50,
                [CharacterStat.MaxNanoEnergy] = 29, [CharacterStat.CurrentNano] = 20, [CharacterStat.RunSpeed] = 100,
            };
            long deadline = DateTime.UtcNow.AddHours(1).Ticks;
            return new CharacterHydrationResult
            {
                Character = new CharacterRecord { Id = ArrivingCharacterId, Name = "HP arrival", Playfield = 500,
                    X = 10, Z = 10, HeadingW = 1 },
                Stats = stats.Select(entry => new StatRecord { StatId = (int)entry.Key, StatValue = entry.Value }).ToArray(),
                ActiveNanos = [
                    new ActiveNanoRecord { NanoId = RetainedNanoId, NanoInstance = 1, ExpiresAtUtcTicks = deadline },
                    new ActiveNanoRecord { NanoId = RemovedNanoId, NanoInstance = 2, ExpiresAtUtcTicks = deadline },
                ],
            };
        }

        static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    sealed class ArrivalGameData : IGameData
    {
        public string RootPath => AppContext.BaseDirectory;
        public int MobTemplateCount => 0;
        public int HashTemplateCount => 0;
        public int HashInstanceCount => 0;
        public int MonsterDataCount => 0;
        public int XpLevelCount => 0;

        public bool TryGetXpLevel(int level, out XpLevelEntry entry) { entry = null!; return false; }
        public bool TryGetCatMesh(int monsterData, out int catMesh) { catMesh = 0; return false; }
        public bool TryGetPlayfieldCharacterAppearanceOverride(int playfieldId, out uint monsterData) { monsterData = 0; return false; }
        public bool TryGetMobTemplate(string hash, out MobTemplate template) => throw new NotSupportedException();
        public MobTemplate RequireMobTemplate(string hash) => throw new NotSupportedException();
        public bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes) => throw new NotSupportedException();
        public bool TryGetHashInstance(string hash, out HashInstance instance) => throw new NotSupportedException();
        public bool TryResolveHashInstance(string hash, out HashInstance instance) => throw new NotSupportedException();
        public bool CanResolveItemHash(string hash) => throw new NotSupportedException();
        public void CollectHashSpawns(string hash, List<HashInstance> into) => throw new NotSupportedException();
        public void CollectMobSpawns(string hash, int? level, List<MobTemplate> into) => throw new NotSupportedException();
        public void CollectHashLeafInstances(string hash, List<HashInstance> into) => throw new NotSupportedException();
        public bool TryGetVendingMachine(int templateId, out VendingMachineDefinition definition) => throw new NotSupportedException();
        public PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId) => throw new NotSupportedException();
        public PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId) => throw new NotSupportedException();
        public PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId) => throw new NotSupportedException();
        public IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId) => throw new NotSupportedException();
        public IReadOnlyCollection<int>? GetConfiguredExitProxyDoorInstances(int playfieldId) => throw new NotSupportedException();
    }

    sealed class ArrivalPersistence : ICharacterRepository, IStatRepository, ICharacterCoalesceCommit
    {
        public CharacterRecord? GetById(int id) => throw new NotSupportedException();
        public void SetOnline(int id) { }
        public void SetOffline(int id) { }
        public void SaveLocation(CharacterRecord character, int online) => throw new NotSupportedException();
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats) { }
        public bool SaveOnlineCheckpoint(int characterId, CharacterRecord? location, IReadOnlyList<StatRecord> stats) => true;
        public IReadOnlyList<StatRecord> GetForCharacter(int id) => [];
        public void UpsertForCharacter(int id, IReadOnlyList<StatRecord> stats) => throw new NotSupportedException();

        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates,
            int characterId, IReadOnlyList<int> uploadedNanoIds, IReadOnlyList<ActiveNanoRecord>? activeNanos,
            IReadOnlyList<SkillLockRecord>? skillLocks) { }
    }
}
