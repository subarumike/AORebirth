namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Core.GameData;
using AORebirth.Database.Domain.Quests;
using AORebirth.Interfaces.Persistence.Missions;
using AORebirth.Interfaces.Persistence.Shops;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Metrics;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;
using ZoneEngine_New.Core.Teams;
using ZoneEngine_New.Core.Trade;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
[DoNotParallelize]
public sealed class QuestDungeonDestinationTests
{
    public TestContext TestContext { get; set; } = null!;

    static readonly Lazy<MissionDestinationCatalog> Catalog = new(() =>
        MissionDestinationCatalog.Load(Path.Combine(RepositoryRoot(), "AORebirth", "GameData")));
    static readonly Lazy<GameDataStore> RuntimeData = new(() =>
    {
        string? root = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(root), "Set AO_REBIRTH_GAMEDATA_PATH to existing runtime GameData for the real procedural dungeon tests.");
        return new GameDataStore(new StubLogger(), null, root!);
    });

    [TestMethod]
    public void AcceptancePersistsSelectedIdentityAndPublishesLinkedKeyOnlyAfterCommit()
    {
        using var f = new Fixture();
        Assert.IsTrue(f.Offer.EntranceInstance < 0, "Exercise the catalog identity's high bit through the signed protocol representation.");
        f.Store.BeforeAccept = () =>
        {
            Assert.IsTrue(Monitor.IsEntered(f.Player.PersistenceGate));
            Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
            Assert.AreEqual(0, f.Session.Sent.Count);
        };
        Assert.IsNull(f.Accept());
        QuestDungeonParameters parameters = f.Parameters();
        Assert.AreEqual(f.Offer.EntranceType, parameters.EntranceType);
        Assert.AreEqual(f.Offer.EntranceInstance, parameters.EntranceInstance);
        Assert.AreEqual(f.Entrance.PlayfieldId, parameters.EntrancePlayfield);
        Assert.AreEqual(f.Entrance.LocalXBits, BitConverter.SingleToUInt32Bits(parameters.EntranceX));
        Assert.AreEqual(f.Entrance.LocalYBits, BitConverter.SingleToUInt32Bits(parameters.EntranceY));
        Assert.AreEqual(f.Entrance.LocalZBits, BitConverter.SingleToUInt32Bits(parameters.EntranceZ));
        Assert.AreEqual(f.Offer.EntranceLow, parameters.BuildingLowId);
        Assert.AreEqual(f.Offer.EntranceHigh, parameters.BuildingHighId);
        Assert.AreEqual(f.Offer.DestinationType, parameters.DestinationType);
        Item key = f.Player.Inventory.Inventory.Content.Values.Single();
        Assert.IsTrue(key.IsPersisted);
        Assert.AreEqual(QuestDungeonService.KeyLowId, key.LowId);
        Assert.AreEqual(QuestDungeonService.KeyItemType, (int)key.Identity.Type);
        Assert.AreEqual(f.Store.Generated!.QuestId, f.Store.Links[key.InstanceId]);
        Assert.AreEqual(f.Player.Identity.Instance, f.Store.Keys.Single().ContainerInstance);
        Assert.IsInstanceOfType<SimpleItemFullUpdateMessage>(f.Session.Sent[0]);
        Assert.IsInstanceOfType<ContainerAddItemMessage>(f.Session.Sent[1]);
        Assert.IsInstanceOfType<TemplateActionMessage>(f.Session.Sent[2]);
        Assert.IsInstanceOfType<TemplateActionMessage>(f.Session.Sent[3]);
        Assert.IsTrue(f.Session.Sent.OfType<QuestFullUpdateMessage>().Any());
        Assert.AreEqual("That mission is no longer offered.", f.Accept());
        Assert.AreEqual(1, f.Store.AcceptCalls);
        Assert.AreEqual(1, f.Player.Inventory.Inventory.Content.Count);
    }

    [TestMethod]
    [DataRow("type")]
    [DataRow("identity")]
    [DataRow("playfield")]
    [DataRow("playfieldIdentity")]
    [DataRow("worldType")]
    [DataRow("x")]
    [DataRow("y")]
    [DataRow("z")]
    [DataRow("offsetX")]
    [DataRow("offsetZ")]
    public void AcceptanceRejectsAnyMismatchWithoutReconstructingIdentityFromCoordinates(string field)
    {
        using var f = new Fixture();
        switch (field)
        {
            case "type": f.Offer.EntranceType++; break;
            case "identity": f.Offer.EntranceInstance = 0; break;
            case "playfield": f.Offer.DestinationPlayfield++; break;
            case "playfieldIdentity": f.Offer.DestinationInstance++; break;
            case "worldType": f.Offer.DestinationType++; break;
            case "x": f.Offer.DestinationX = MathF.BitIncrement(f.Offer.DestinationX); break;
            case "y": f.Offer.DestinationY = MathF.BitIncrement(f.Offer.DestinationY); break;
            case "z": f.Offer.DestinationZ = MathF.BitIncrement(f.Offer.DestinationZ); break;
            case "offsetX": f.Offer.EntranceLow++; break;
            case "offsetZ": f.Offer.EntranceHigh++; break;
        }
        Assert.AreEqual("That mission's location is unavailable.", f.Accept());
        Assert.AreEqual(0, f.Store.AcceptCalls);
        Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
        Assert.AreEqual(0, f.Session.Sent.Count);
    }

    [TestMethod]
    public void ParameterRoundTripKeepsSignedInstanceAndHistoricalImplicitTypeButRejectsWrongType()
    {
        var parameters = new QuestDungeonParameters
        {
            Seed = 42, GeneratorVersion = DungeonLayoutGenerator.CurrentVersion,
            DungeonPlayfield = QuestDungeonIds.FirstPlayfield + 100, EntranceInstance = unchecked((int)0xF1234567)
        };
        Assert.IsTrue(QuestDungeonParameters.TryParse(parameters.ToJson(), out var restored));
        Assert.AreEqual(parameters.EntranceInstance, restored.EntranceInstance);
        Assert.AreEqual((int)IdentityType.MissionEntrance, restored.EntranceType);
        string historical = parameters.ToJson().Replace("\"EntranceType\":56006,", string.Empty, StringComparison.Ordinal);
        Assert.IsFalse(historical.Contains("\"EntranceType\"", StringComparison.Ordinal));
        Assert.IsTrue(QuestDungeonParameters.TryParse(historical, out restored));
        Assert.AreEqual((int)IdentityType.MissionEntrance, restored.EntranceType);
        parameters.EntranceType = (int)IdentityType.Terminal;
        Assert.IsFalse(QuestDungeonParameters.TryParse(parameters.ToJson(), out _));
    }

    [TestMethod]
    public void RealProceduralLayoutPreservesHighBitIdentityAndSameSeedPayload()
    {
        using var f = new Fixture();
        Assert.IsTrue(f.Layouts.TryGenerate(42, DungeonLayoutGenerator.CurrentVersion, f.Offer.EntranceInstance, out var first));
        var independent = new DungeonLayoutGenerator(RuntimeData.Value, new StubLogger());
        Assert.IsTrue(independent.TryGenerate(42, DungeonLayoutGenerator.CurrentVersion, f.Offer.EntranceInstance, out var second));
        Assert.AreEqual(f.Offer.EntranceInstance, first.Generator.Identity.Instance);
        Assert.AreEqual(DungeonLayoutGenerator.BuildingIdentityType, (int)first.Generator.Identity.Type);
        CollectionAssert.AreEqual(first.GeneratorPayload, second.GeneratorPayload);
        Assert.IsTrue(first.GeneratorPayload.Length > 0);
        Assert.AreEqual(first.Spawn.x, second.Spawn.x);
        Assert.AreEqual(first.Spawn.y, second.Spawn.y);
        Assert.AreEqual(first.Spawn.z, second.Spawn.z);
    }

    [TestMethod]
    public void RawWxyzRotationMatchesCurrentDynelsHeadingAtEveryExactPlacementIdentity()
    {
        int matched = 0, informative = 0;
        var differences = new List<string>();
        foreach (var group in Catalog.Value.Placements.GroupBy(row => row.PlayfieldId))
        {
            var dynels = RuntimeData.Value.GetPlayfieldGeometry(group.Key).Dynels?.Dynels;
            if (dynels == null) continue;
            foreach (var entrance in group)
            {
                var records = dynels.Where(row => unchecked((uint)row.IdentityType) == entrance.IdentityType
                    && unchecked((uint)row.IdentityInstance) == entrance.IdentityInstance).ToArray();
                if (records.Length == 0) continue;
                Assert.AreEqual(1, records.Length, "Current Dynels data repeats a complete placement identity.");
                var record = records[0];
                matched++;
                uint[] raw = [BitConverter.SingleToUInt32Bits(entrance.RotationComponent0), BitConverter.SingleToUInt32Bits(entrance.RotationComponent1),
                    BitConverter.SingleToUInt32Bits(entrance.RotationComponent2), BitConverter.SingleToUInt32Bits(entrance.RotationComponent3)];
                uint[] heading = [BitConverter.SingleToUInt32Bits(record.Heading.X), BitConverter.SingleToUInt32Bits(record.Heading.Y),
                    BitConverter.SingleToUInt32Bits(record.Heading.Z), BitConverter.SingleToUInt32Bits(record.Heading.W)];
                if (raw.Distinct().Count() > 1) informative++;
                uint[] xyzw = [raw[1], raw[2], raw[3], raw[0]];
                if (!xyzw.SequenceEqual(heading)) differences.Add(entrance.IdentityType.ToString("X8") + ":" + entrance.IdentityInstance.ToString("X8")
                    + " raw=" + string.Join(",", raw.Select(value => value.ToString("X8"))) + " heading=" + string.Join(",", heading.Select(value => value.ToString("X8"))));
            }
        }
        TestContext.WriteLine("Runtime data={0}; exact identity matches={1}; informative rotations={2}; slot differences={3}", RuntimeData.Value.RootPath, matched, informative, differences.Count);
        Assert.AreEqual(Catalog.Value.Count, matched, "Every placement must match current Dynels by its complete identity. Root=" + RuntimeData.Value.RootPath);
        Assert.IsTrue(informative > 0, "The mapping check requires informative rotations.");
        Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences.Take(5)));
    }

    [TestMethod]
    public void ExactCarriedKeyCreatesActualDungeonThenTransfersToItsSpawnAndReusesWorld()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        QuestDungeonParameters parameters = f.Parameters();
        Assert.IsFalse(f.Manager.TryGetQuestDungeon(parameters.DungeonPlayfield, out _));
        Assert.IsTrue(f.Service.TryEnter(f.Player, f.Target));
        Assert.IsTrue(f.Manager.TryGetQuestDungeon(parameters.DungeonPlayfield, out var world));
        Assert.AreSame(f.Entrance, world.Entrance);
        Assert.AreEqual(f.Store.Generated!.QuestId, world.QuestId);
        Assert.AreEqual(f.Offer.EntranceInstance, world.Layout.Generator.Identity.Instance);
        Assert.AreSame(world, f.Session.Transfers.Single().World);
        Assert.AreEqual(world.Layout.Spawn.x, f.Session.Transfers[0].Position.x);
        Assert.AreEqual(world.Layout.Spawn.y, f.Session.Transfers[0].Position.y);
        Assert.AreEqual(world.Layout.Spawn.z, f.Session.Transfers[0].Position.z);
        Assert.AreSame(world, f.Manager.GetOrCreateQuestDungeon(parameters.DungeonPlayfield, world.QuestId, world.Layout, f.Entrance));
        Assert.IsTrue(world.GetRequiredService<DynelRegistry>().Dynels().OfType<Door>().Any(), "Actual dungeon Build must materialize doors.");
    }

    [TestMethod]
    [DataRow("wrongType")]
    [DataRow("missingIdentity")]
    [DataRow("missingKey")]
    [DataRow("wrongQuestEntrance")]
    [DataRow("wrongPlayfield")]
    [DataRow("horizontalRange")]
    [DataRow("verticalRange")]
    [DataRow("expired")]
    public void EntryRequiresCurrentExactIdentityCarriedKeyPlayfieldRangeAndExpiry(string denial)
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        Identity target = f.Target;
        switch (denial)
        {
            case "wrongType": target = new Identity { Type = IdentityType.Terminal, Instance = target.Instance }; break;
            case "missingIdentity": target = new Identity { Type = target.Type, Instance = 0 }; break;
            case "missingKey": f.Player.Inventory.Inventory.Content.Clear(); break;
            case "wrongQuestEntrance":
                var parameters = f.Parameters(); parameters.EntranceInstance = 0;
                f.Store.Generated!.AcgBuildingGeneratorJson = parameters.ToJson(); f.RestartService(); break;
            case "wrongPlayfield": f.SetExteriorIdentity(f.Entrance.PlayfieldId + 1); break;
            case "horizontalRange": f.Player.Position = new Vector3(f.Entrance.LocalX + 10.01, f.Entrance.LocalY, f.Entrance.LocalZ); break;
            case "verticalRange": f.Player.Position = new Vector3(f.Entrance.LocalX, f.Entrance.LocalY + 14.01, f.Entrance.LocalZ); break;
            case "expired": f.Store.Generated!.ExpiresAtUtcTicks = DateTime.UtcNow.AddMinutes(-1).Ticks; f.RestartService(); break;
        }
        Assert.IsFalse(f.Service.TryEnter(f.Player, target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        Assert.IsFalse(f.Manager.TryGetQuestDungeon(f.Parameters().DungeonPlayfield, out _));
    }

    [TestMethod]
    public void KeyCopyAndRestoreKeepTheAcceptedEntranceNameAndDurableLink()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        var key = f.Player.Inventory.Inventory.Content.Single();
        int duplicatorSlot = f.Player.Inventory.Inventory.FindFreeSlot();
        Item duplicator = TestWorld.CreateItem(lowId: QuestDungeonService.DuplicatorLowId, instanceId: 700000);
        f.Player.Inventory.Inventory.Add(duplicatorSlot, duplicator);
        f.Session.Sent.Clear();
        Assert.IsTrue(f.Service.TryDuplicate(f.Player,
            new Identity { Type = IdentityType.Inventory, Instance = key.Key },
            new Identity { Type = IdentityType.Inventory, Instance = duplicatorSlot }));
        Assert.AreEqual(2, f.Player.Inventory.Inventory.Content.Values.Count(item => item.LowId == QuestDungeonService.KeyLowId));
        Assert.AreSame(duplicator, f.Player.Inventory.Inventory.Content[duplicatorSlot]);
        Assert.AreEqual(2, f.Store.Links.Count);
        Assert.IsTrue(f.Store.Links.Values.All(id => id == f.Store.Generated!.QuestId));
        f.Session.Sent.Clear();
        f.Service.RestoreKeys(f.Player);
        var updates = f.Session.Sent.OfType<SimpleItemFullUpdateMessage>().ToArray();
        Assert.AreEqual(2, updates.Length);
        Assert.IsTrue(updates.All(update => update.Name == "Mission key to " + f.Entrance.DisplayName + '\0'));
    }

    [TestMethod]
    public void StoredAcceptedIdentityRecreatesDungeonForLoginWithoutFuzzyLookup()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        f.RestartService();
        QuestDungeonParameters parameters = f.Parameters();
        Assert.IsTrue(f.Service.TryGetEntrance(f.Store.Generated!.QuestId, out var resolved));
        Assert.AreSame(f.Entrance, resolved);
        var login = f.Service.ResolveLogin(parameters.DungeonPlayfield);
        Assert.IsInstanceOfType<QuestDungeonPlayfield>(login.Playfield);
        Assert.AreSame(f.Entrance, ((QuestDungeonPlayfield)login.Playfield).Entrance);
        Assert.IsNotNull(login.Position);
    }

    [TestMethod]
    public void JournalDeletionSavesAbandonmentBeforeEndingKeysAndPreventsEntry()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        Identity journal = f.Session.Sent.OfType<QuestFullUpdateMessage>().Single().Quests.Single().QuestId;
        string frozenDestination = f.Store.Generated!.AcgBuildingGeneratorJson;
        int keyInstance = f.Player.Inventory.Inventory.Content.Values.Single().InstanceId;
        f.Store.BeforeEndKeys = questId =>
        {
            Assert.AreEqual(f.Store.Generated.QuestId, questId);
            Assert.AreEqual((int)QuestState.Abandoned, f.Store.Character!.State);
            Assert.IsTrue(f.Session.Sent.OfType<QuestMessage>().Any(message => message.Action == QuestAction.Delete
                && message.Mission.Equals(journal)), "Persist abandonment and delete the journal entry before ending its keys.");
        };
        Assert.IsTrue(f.Abandon(journal.Instance));
        Assert.AreEqual(1, f.Store.EndKeysCalls);
        Assert.AreEqual(0, f.Store.Links.Count);
        CollectionAssert.AreEqual(new[] { keyInstance }, f.Store.RetiredKeys.ToArray());
        Assert.AreEqual(frozenDestination, f.Store.Generated.AcgBuildingGeneratorJson, "Ending a quest must not change its frozen entrance.");
        Assert.IsFalse(f.Abandon(journal.Instance), "An already ended journal entry must not end keys twice.");
        Assert.AreEqual(1, f.Store.EndKeysCalls);
        f.Persistence.BeforeMutation = batch =>
        {
            Assert.AreEqual(0, f.Store.Links.Count);
            Assert.AreEqual(keyInstance, batch.Locations.Single().InstanceId);
        };
        Assert.IsFalse(f.Service.TryEnter(f.Player, f.Target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        Assert.AreEqual(0, f.Player.Inventory.Inventory.Content.Count);
        Assert.IsTrue(f.Session.Sent.OfType<CharacterActionMessage>().Any(message => message.Action == CharacterActionType.DeleteItem));
        f.AssertNoErrors();
    }

    [TestMethod]
    public void UnlinkedKeyRemovalCommitsBeforeDeletingOnlyTheRetiredKeyFromInventory()
    {
        using var f = new Fixture();
        Assert.IsNull(f.Accept());
        var key = f.Player.Inventory.Inventory.Content.Single();
        int unrelatedSlot = f.Player.Inventory.Inventory.FindFreeSlot();
        Item unrelated = TestWorld.CreateItem(lowId: QuestDungeonService.DuplicatorLowId, instanceId: 700001);
        f.Player.Inventory.Inventory.Add(unrelatedSlot, unrelated);
        f.Store.Links.Remove(key.Value.InstanceId);
        f.Session.Sent.Clear();
        f.Persistence.BeforeMutation = batch =>
        {
            Assert.IsTrue(Monitor.IsEntered(f.Player.PersistenceGate));
            Assert.AreSame(key.Value, f.Player.Inventory.Inventory.Content[key.Key]);
            Assert.IsFalse(f.Session.Sent.OfType<CharacterActionMessage>().Any());
            CollectionAssert.AreEqual(new[] { key.Value.InstanceId }, f.Store.RetiredKeys.ToArray());
            ItemLocationUpdate retired = batch.Locations.Single();
            Assert.AreEqual(key.Value.InstanceId, retired.InstanceId);
            Assert.AreEqual((int)IdentityType.None, retired.ContainerType);
            Assert.AreEqual(f.Player.Identity.Instance, retired.ContainerInstance);
        };
        f.Service.RestoreKeys(f.Player);
        Assert.AreEqual(1, f.Persistence.MutationCalls);
        Assert.IsFalse(f.Player.Inventory.Inventory.Content.ContainsKey(key.Key));
        Assert.AreSame(unrelated, f.Player.Inventory.Inventory.Content.Single().Value);
        CharacterActionMessage deletion = f.Session.Sent.OfType<CharacterActionMessage>().Single();
        Assert.AreEqual(CharacterActionType.DeleteItem, deletion.Action);
        Assert.AreEqual(IdentityType.Inventory, deletion.Target.Type);
        Assert.AreEqual(key.Key, deletion.Target.Instance);
        Assert.AreEqual(0, f.Session.Sent.OfType<SimpleItemFullUpdateMessage>().Count());
        f.Service.RestoreKeys(f.Player);
        Assert.AreEqual(1, f.Persistence.MutationCalls, "A removed key must not be retired or deleted again on restore.");
        Assert.IsFalse(f.Service.TryEnter(f.Player, f.Target));
        Assert.AreEqual(0, f.Session.Transfers.Count);
        f.AssertNoErrors();
    }

    static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate this test build's repository root.");
    }

    sealed class Fixture : IDisposable
    {
        internal readonly MissionEntrancePlacement Entrance;
        internal readonly Player Player = TestWorld.CreatePlayer(77);
        internal readonly Session Session = new();
        internal readonly QuestStore Store = new();
        internal readonly OfferDao Offers = new();
        internal readonly DungeonLayoutGenerator Layouts;
        internal readonly PlayfieldManager Manager;
        internal readonly GeneratedMissionOffer Offer;
        internal readonly Persistence Persistence = new();
        internal QuestDungeonService Service = null!;
        readonly DiagnosticLogger _logger = new();
        readonly StubItemBuilder _items = new();
        readonly StubCatalog _templates = new();
        readonly StubInstanceIdAllocator _ids = new();
        readonly InventoryFlushService _flush;
        readonly InventoryActionService _actions;
        readonly QuestService _quests;
        readonly Playfield _exterior = Blank<Playfield>();

        internal Fixture()
        {
            Entrance = Catalog.Value.Placements.First(row => row.IdentityInstance > int.MaxValue && Catalog.Value.TryGetWorldPosition(row.Identity, out _));
            Assert.IsTrue(Catalog.Value.TryGetWorldPosition(Entrance.Identity, out var worldPos));
            Offer = new GeneratedMissionOffer
            {
                OwnerId = Player.Identity.Instance, OfferType = 0xDAC3, OfferInstance = 100,
                State = GeneratedMissionState.Offered, ExpiresAtUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
                EntranceType = unchecked((int)Entrance.IdentityType), EntranceInstance = unchecked((int)Entrance.IdentityInstance),
                DestinationType = unchecked((int)worldPos.PlayfieldIdentityType), DestinationInstance = Entrance.PlayfieldId,
                DestinationPlayfield = Entrance.PlayfieldId, DestinationX = Entrance.LocalX, DestinationY = Entrance.LocalY, DestinationZ = Entrance.LocalZ,
                EntranceLow = worldPos.WorldOffsetX, EntranceHigh = worldPos.WorldOffsetZ,
                MissionType = 1, Quality = 10, Title = "Destination fixture", Description = "Destination integration test"
            };
            Offers.Offer = Offer;
            Store.Offer = Offer;
            var lazyManager = new Lazy<PlayfieldManager>(() => Manager ?? throw new InvalidOperationException("Fixture manager is not initialized."));
            _flush = new InventoryFlushService(lazyManager, Persistence, _logger);
            _actions = new InventoryActionService(Persistence, _flush, _ids, _logger);
            _quests = new QuestService(new QuestCatalog(RuntimeData.Value, _logger), Store, _items, _flush, _logger);
            Layouts = new DungeonLayoutGenerator(RuntimeData.Value, _logger);
            Assert.IsTrue(Layouts.IsAvailable, "Existing runtime GameData must contain procedural style 324. Root=" + RuntimeData.Value.RootPath + "; " + _logger.Errors);
            var minter = new HashItemMinter(RuntimeData.Value, _templates, _items);
            var authored = new AuthoredQuestService(null!, _flush, _items, _templates, _ids,
                new AuthoredQuestCatalog(new InteractionContent()), _logger);
            Manager = new PlayfieldManager(_logger, new MessageRouter([], _logger), new PlayerHydrator(_items, RuntimeData.Value),
                RuntimeData.Value, _items, minter, new StubInventoryRepository(), _ids,
                new InventoryMoveService(_logger, _flush, _actions), _flush,
                new TradeService(_logger, RuntimeData.Value, _templates, minter, _ids, _flush, Persistence),
                Blank<CharacterSnapshotService>(), new PlayfieldMetricsRegistry(), new TeamService(), authored, _templates, new Shops(), _quests);
            SetExteriorIdentity(Entrance.PlayfieldId);
            Player.Playfield = _exterior;
            Player.Position = new Vector3(Entrance.LocalX, Entrance.LocalY, Entrance.LocalZ);
            Player.Stats.Set(CharacterStat.Health, 100);
            Player.Stats.Set(CharacterStat.MaxHealth, 100);
            Player.QuestLog = new QuestLog();
            Player.Session = Session;
            Session.BindPlayer(Player);
            RestartService();
        }

        internal Identity Target => new() { Type = (IdentityType)Entrance.IdentityType, Instance = unchecked((int)Entrance.IdentityInstance) };
        internal bool Abandon(int journalInstance) => _quests.TryAbandon(Player, journalInstance);
        internal void AssertNoErrors() => Assert.AreEqual(string.Empty, _logger.Errors);
        internal string? Accept()
        {
            string? result = Service.AcceptOffer(Player, new Identity { Type = (IdentityType)Offer.OfferType, Instance = Offer.OfferInstance });
            if (result == null)
            {
                QuestDungeonParameters parameters = Parameters();
                Assert.IsTrue(Layouts.TryGenerate(parameters.Seed, parameters.GeneratorVersion, parameters.EntranceInstance, out var layout));
                Assert.IsNotNull(layout.Doors, "Existing style data must provide its door templates.");
                _templates.Add(layout.Doors.DoorTemplate, 1).Add(layout.Doors.ExitTemplate, 1);
            }
            return result;
        }
        internal QuestDungeonParameters Parameters()
        {
            Assert.IsNotNull(Store.Generated);
            Assert.IsTrue(QuestDungeonParameters.TryParse(Store.Generated.AcgBuildingGeneratorJson, out var parameters));
            return parameters;
        }
        internal void SetExteriorIdentity(int playfield) => typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_exterior, new Identity { Type = IdentityType.Playfield, Instance = playfield });
        internal void RestartService()
        {
            Service?.Dispose();
            Service = new QuestDungeonService(Store, _quests, Catalog.Value, Layouts, Offers, _items, _ids,
                _flush, _actions, new Lazy<PlayfieldManager>(() => Manager), RuntimeData.Value, _logger);
        }
        public void Dispose() { Service?.Dispose(); Manager.Dispose(); _flush.Dispose(); }
    }

    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    sealed class DiagnosticLogger : ZoneEngine_New.Core.Logging.IZoneLogger
    {
        internal string Errors = string.Empty;
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) => Errors += message + Environment.NewLine;
        public void Error(Exception exception, string message) => Errors += message + " " + exception + Environment.NewLine;
        public ZoneEngine_New.Core.Logging.IZoneLogger CreateForPlayfield(int playfieldId) => this;
    }

    sealed class Session : IZoneSession
    {
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        internal readonly List<MessageBody> Sent = [];
        internal readonly List<(Playfield World, Vector3 Position)> Transfers = [];
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, Vector3 landing) => Transfers.Add((destination, landing));
        public void Send(byte[] packet) => throw new AssertFailedException("Unexpected raw packet.");
        public void Send(Message message) => Sent.Add(message.Body);
        public void Send(MessageBody body) => Sent.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Send(body);
        public void SendInitiateCompression() => throw new AssertFailedException();
        public void Close() => State = SessionState.Closed;
    }

    sealed class Persistence : ICharacterCoalesceCommit, IInventoryMutationPersistence, ITradePersistence
    {
        internal Action<InventoryMutationBatch>? BeforeMutation;
        internal int MutationCalls;
        public void Persist(IReadOnlyList<ItemInstanceRecord> inserts, IReadOnlyList<ItemLocationUpdate> updates,
            int characterId, IReadOnlyList<int> nanos, IReadOnlyList<ActiveNanoRecord>? activeNanos, IReadOnlyList<SkillLockRecord>? skillLocks)
            => throw new AssertFailedException("Clean quest grants must not use an independent inventory commit.");
        public void Persist(InventoryMutationBatch batch)
        {
            Assert.IsNotNull(BeforeMutation, "Unexpected inventory mutation.");
            MutationCalls++;
            BeforeMutation(batch);
        }
        public void Persist(TradePersistenceBatch batch) => throw new AssertFailedException("Unexpected trade commit.");
    }

    sealed class Shops : IShopDao
    {
        public IList<ShopVendorData> ListForPlayfield(int playfieldId) => [];
    }

    sealed class QuestStore : ICharacterQuestStore
    {
        internal GeneratedMissionOffer Offer = null!;
        internal GeneratedQuestRow? Generated;
        internal CharacterQuestRow? Character;
        internal readonly Dictionary<int, string> Links = [];
        internal readonly List<QuestDungeonKeyItemRow> Keys = [];
        internal Action? BeforeAccept;
        internal Action<string>? BeforeEndKeys;
        internal int AcceptCalls;
        internal int EndKeysCalls;
        internal readonly List<int> RetiredKeys = [];
        public bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, long nowUtcTicks)
        {
            AcceptCalls++;
            BeforeAccept?.Invoke();
            Assert.AreEqual(Offer.OwnerId, ownerId);
            Assert.AreEqual(Offer.OfferType, offerType);
            Assert.AreEqual(Offer.OfferInstance, offerInstance);
            if (Offer.State != GeneratedMissionState.Offered) return false;
            Offer.State = GeneratedMissionState.Active;
            Generated = quest; Character = characterRow; Keys.Add(key); Links.Add(key.InstanceId, quest.QuestId);
            return true;
        }
        public string? TryAddDuplicateKey(int sourceKeyInstanceId, QuestDungeonKeyItemRow copy, int maxKeys, long nowUtcTicks)
        {
            if (!Links.TryGetValue(sourceKeyInstanceId, out string? quest) || Links.Count >= maxKeys) return null;
            Links.Add(copy.InstanceId, quest); Keys.Add(copy); return quest;
        }
        public IList<CharacterQuestRow> Load(int characterId) => Character != null ? [Character] : [];
        public IList<GeneratedQuestRow> LoadGeneratedFor(int characterId) => Generated != null ? [Generated] : [];
        public GeneratedQuestRow? LoadGenerated(string questId) => Generated?.QuestId == questId ? Generated : null;
        public IDictionary<int, string> LoadKeyQuests(IReadOnlyCollection<int> keyInstanceIds)
            => Links.Where(pair => keyInstanceIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        public IList<int> RetireUnlinkedKeys(IReadOnlyCollection<int> keyInstanceIds, int keyLowId)
        {
            Assert.AreEqual(QuestDungeonService.KeyLowId, keyLowId);
            int[] retired = keyInstanceIds.Where(id => !Links.ContainsKey(id)).ToArray();
            foreach (int id in retired) if (!RetiredKeys.Contains(id)) RetiredKeys.Add(id);
            return retired;
        }
        public IList<string> LoadExpiredQuestsWithKeys(long nowUtcTicks, int limit) => [];
        public int PurgeEndedGeneratedQuests(long cutoffUtcTicks, int limit) => 0;
        public int RetireDeadKeysForCharacter(int characterId, int keyLowId, IReadOnlyCollection<int> ownedContainerTypes, int bagContainerType) => 0;
        public void Save(CharacterQuestRow row) => Character = row;
        public void SaveGenerated(GeneratedQuestRow row) => Generated = row;
        public IList<RetiredDungeonKey> EndQuestKeys(string questId)
        {
            EndKeysCalls++;
            BeforeEndKeys?.Invoke(questId);
            var retired = Keys.Where(key => Links.TryGetValue(key.InstanceId, out string? linked) && linked == questId)
                .Select(key => new RetiredDungeonKey { KeyInstanceId = key.InstanceId,
                    ContainerType = key.ContainerType, ContainerInstance = key.ContainerInstance }).ToArray();
            foreach (var key in retired) { Links.Remove(key.KeyInstanceId); RetiredKeys.Add(key.KeyInstanceId); }
            return retired;
        }
        public void CloseCharacterQuests(string questId, int state, long nowUtcTicks) => throw new AssertFailedException();
    }

    sealed class OfferDao : IGeneratedMissionDao
    {
        internal GeneratedMissionOffer Offer = null!;
        public IList<GeneratedMissionOffer> ReadOffers(int owner) => owner == Offer.OwnerId ? [Offer] : [];
        public int ReserveIdentities(string sequence, int count) => throw new AssertFailedException();
        public GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch) => throw new AssertFailedException();
        public GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance) => throw new AssertFailedException();
        public IList<GeneratedMissionBinding> ReadAccepted(int owner) => throw new AssertFailedException();
        public GeneratedMissionBinding ReadAccepted(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult Observe(GeneratedMissionObservation observation) => throw new AssertFailedException();
        public GeneratedMissionResult Complete(GeneratedMissionCompletion completion) => throw new AssertFailedException();
        public GeneratedMissionResult End(int owner, int type, int instance, GeneratedMissionState state, long now) => throw new AssertFailedException();
        public GeneratedMissionResult AdvanceCleanup(int owner, int type, int instance, long version, long mask, long now) => throw new AssertFailedException();
        public IList<GeneratedMissionObject> ReadObjects(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult UpdateObjects(int owner, int type, int instance, IList<GeneratedMissionObject> objects, long now) => throw new AssertFailedException();
        public GeneratedMissionResult SavePosition(int owner, int type, int instance, int pf, float x, float y, float z, long now) => throw new AssertFailedException();
        public IList<MissionItemInstanceData> ReadArtifacts(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult CleanupArtifacts(int owner, int type, int instance, long now) => throw new AssertFailedException();
        public GeneratedMissionResult ClaimCorpseCredits(int owner, int type, int instance, int npc, int cash, long now) => throw new AssertFailedException();
    }
}
