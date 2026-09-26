namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class VicinityTeleportTests
    {
        const int GridPlayfieldId = 152;
        const int GridExitPad = unchecked((int)0xC0000098);
        const int GridFloorPad = unchecked((int)0xC01A0098);

        [TestMethod]
        public void GridExitPadIsAVicinityTriggerAndDoesNotRefireWhileOccupied()
        {
            var data = new GameDataStore(new StubLogger());
            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(GridPlayfieldId);
            var pad = geometry.Dynels!.Dynels.Single(d => d.IdentityInstance == GridExitPad);
            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                GridPlayfieldId,
                geometry,
                data.GetPlayfieldMetaData(GridPlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            Assert.IsTrue(world.VicinityTriggerCount > 0);
            var onPad = new Vector3(pad.Position.X, pad.Position.Y, pad.Position.Z);
            var occupied = new HashSet<int>();
            Assert.IsTrue(world.TryResolveZoneCrossing(
                onPad.xf - 10f,
                onPad.zf,
                onPad,
                occupied,
                default,
                out ZoneCrossing crossing));
            Assert.AreEqual(ZoneTriggerKind.TargetVicinity, crossing.Trigger.Kind);
            Assert.IsFalse(world.TryResolveZoneCrossing(onPad.xf, onPad.zf, onPad, occupied, default, out _));

            var outside = new Vector3(onPad.x + 10, onPad.y, onPad.z);
            Assert.IsFalse(world.TryResolveZoneCrossing(outside.xf, outside.zf, outside, new HashSet<int>(), default, out _));
        }

        [TestMethod]
        public void GridExitPadRequiresComputerLiteracyAndSaysSoWhenItFails()
        {
            var data = new GameDataStore(new StubLogger());
            var pad = data.GetPlayfieldGeometry(GridPlayfieldId).Dynels!.Dynels
                .Single(d => d.IdentityInstance == GridExitPad);
            ItemTemplate template = DynelEventSpells.WithOnUseFromDynel(new ItemTemplate { Id = pad.TemplateId }, pad);
            List<ItemSpell> spells = template.SpellList[EventType.OnTargetInVicinity];
            ItemSpell teleport = spells.Single(spell => spell.Is(FunctionType.LineTeleport));
            ItemSpell text = spells.Single(spell => spell.Is(FunctionType.Text));
            Assert.AreEqual((int)CharacterStat.ComputerLiteracy, teleport.Requirements[0].StatNumber);
            Assert.AreEqual((int)Operator.GreaterThan, teleport.Requirements[0].Operator);
            Assert.AreEqual(75, teleport.Requirements[0].Value);
            Assert.AreEqual((int)Operator.LessThan, text.Requirements[0].Operator);

            var session = new RecordingSession();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Stats.Set(CharacterStat.ComputerLiteracy, 10, StatDetail.Base, dirty: true);

            Assert.IsTrue(template.ExecuteSpells(
                EventType.OnTargetInVicinity,
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
            Assert.IsNull(session.Destination);
            StringAssert.Contains(session.Chat, "75");
        }

        [TestMethod]
        public void LineTeleportPlayfieldZeroLandsOnTheCurrentPlayfieldsDestinationLine()
        {
            const int playfieldId = 100;
            const byte line = 6;
            DestinationsCatalog.Instance.Register(
                playfieldId,
                new Dictionary<byte, PlayfieldDestination>
                {
                    [line] = new PlayfieldDestination
                    {
                        DestinationId = (line << 16) | playfieldId,
                        StartX = 0,
                        StartY = 5,
                        StartZ = 0,
                        EndX = 0,
                        EndY = 5,
                        EndZ = 10
                    }
                });

            var session = new RecordingSession();
            Playfield source = BlankPlayfield(playfieldId);
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = source;
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 4, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 9, StatDetail.Base, dirty: true);

            var template = new ItemTemplate
            {
                SpellList =
                {
                    [EventType.OnTargetInVicinity] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.LineTeleport,
                            Arguments = new List<object> { (int)IdentityType.Playfield3, line << 16, 0 }
                        }
                    ]
                }
            };

            Assert.IsTrue(template.ExecuteSpells(
                EventType.OnTargetInVicinity,
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
            Assert.AreEqual(-4f, player.Position.xf, 0.001f);
            Assert.AreEqual(5f, player.Position.yf, 0.001f);
            Assert.AreEqual(5f, player.Position.zf, 0.001f);
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
            Assert.AreEqual(line << 16, session.IntrazoneKey);
            Assert.IsFalse(session.SamePlayfield, "Intrazone lifts must not send the zone-reloading teleport.");
            Assert.AreEqual(0, session.Transfers);
        }

        [TestMethod]
        public void IntrazoneLineTeleportKeepsTheCharactersHeading()
        {
            const int playfieldId = 101;
            const byte line = 3;
            DestinationsCatalog.Instance.Register(
                playfieldId,
                new Dictionary<byte, PlayfieldDestination>
                {
                    [line] = new PlayfieldDestination
                    {
                        DestinationId = (line << 16) | playfieldId,
                        StartX = 0, StartY = 5, StartZ = 0,
                        EndX = 10, EndY = 5, EndZ = 0
                    }
                });

            var session = new RecordingSession();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = BlankPlayfield(playfieldId);
            var facing = new Quaternion(0, 0.6, 0, 0.8);
            player.Rotation = facing;

            var template = new ItemTemplate
            {
                SpellList =
                {
                    [EventType.OnTargetInVicinity] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.LineTeleport,
                            Arguments = new List<object> { (int)IdentityType.Playfield3, (line << 16) | playfieldId, 0 }
                        }
                    ]
                }
            };

            Assert.IsTrue(template.ExecuteSpells(
                EventType.OnTargetInVicinity,
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
            Assert.AreEqual((line << 16) | playfieldId, session.IntrazoneKey);
            Assert.AreEqual(facing.yf, session.IntrazoneHeading!.yf, 0.0001f);
            Assert.AreEqual(facing.wf, player.Rotation.wf, 0.0001f);
            // Midpoint (5,5,0) shoved 4m to the left of +X is +Z.
            Assert.AreEqual(5f, player.Position.xf, 0.001f);
            Assert.AreEqual(4f, player.Position.zf, 0.001f);
        }

        [TestMethod]
        public void IntrazoneTeleportPacketIsTheSoftClientForm()
        {
            Player player = TestWorld.CreatePlayer(7);
            const int key = (23 << 16) | GridPlayfieldId;
            N3TeleportMessage message = ZoneSession.BuildIntrazoneTeleport(
                player, new Vector3(238.5, 37.3, 267.9), new Quaternion(0, 0, 0, 1), key);

            Assert.AreEqual((byte)0x61, message.Unknown1);
            Assert.AreEqual(Identity.None, message.ChangePlayfield);
            Assert.AreEqual((IdentityType)51104, message.Playfield.Type);
            Assert.AreEqual(0, message.Playfield.Instance);
            Assert.AreEqual(IdentityType.Playfield3, message.Playfield2.Type);
            Assert.AreEqual(key, message.Playfield2.Instance);
            Assert.AreEqual(8, message.Payload.Length);
        }

        [TestMethod]
        public void GridUpPadLineLandsOnTheUpperFloor()
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);

            Assert.IsTrue(PortalDoorLandingResolver.TryResolveLineLanding(
                DestinationsCatalog.Instance, GridPlayfieldId, 23, out Vector3 landing));
            Assert.AreEqual(238.5f, landing.xf, 0.2f);
            Assert.AreEqual(37.3f, landing.yf, 0.2f);
            Assert.AreEqual(267.9f, landing.zf, 0.2f);
        }

        [TestMethod]
        public void WalkingOntoAGridFloorPadTeleportsWhenTheSkillPasses()
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(GridPlayfieldId);
            var pad = geometry.Dynels!.Dynels.Single(d => d.IdentityInstance == GridFloorPad);
            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                GridPlayfieldId,
                geometry,
                data.GetPlayfieldMetaData(GridPlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            var session = new RecordingSession();
            Playfield playfield = BlankPlayfield(GridPlayfieldId, data, world);
            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = playfield;
            player.Stats.Set(CharacterStat.ComputerLiteracy, 200, StatDetail.Base, dirty: true);
            player.Position = new Vector3(pad.Position.X + 10f, pad.Position.Y, pad.Position.Z);
            registry.Register(player);

            world.TickSoftTriggers(playfield, 0.1);
            player.Position = new Vector3(pad.Position.X, pad.Position.Y, pad.Position.Z);
            world.TickSoftTriggers(playfield, 0.1);

            Assert.IsNotNull(session.IntrazoneKey);
            Assert.IsFalse(session.SamePlayfield);
            Assert.AreNotEqual(pad.Position.X, player.Position.xf, 0.5);
        }

        [TestMethod]
        public void JobeTeleportingRingUsesTheItemTemplateAndTransfers()
        {
            const int platformId = 4530;
            const int marketTemplate = 225426;
            const int marketPlayfield = 4532;
            var data = new GameDataStore(new StubLogger());
            var catalog = new ItemTemplateCatalog(new EmptyNames(), data, new StubLogger());
            Assert.IsTrue(catalog.TryGet(marketTemplate, out ItemTemplate marketItem));
            ItemSpell teleport = marketItem.SpellList[EventType.OnTargetInVicinity]
                .Single(spell => spell.Is(FunctionType.Teleport));
            Assert.IsTrue(teleport.TryReadInt(0, out int x) && x == 405);
            Assert.IsTrue(teleport.TryReadInt(1, out int y) && y == 367);
            Assert.IsTrue(teleport.TryReadInt(2, out int z) && z == 490);
            Assert.IsTrue(teleport.TryReadInt(3, out int dest) && dest == marketPlayfield);

            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(platformId);
            var ring = geometry.Dynels!.Dynels.Single(dynel => dynel.TemplateId == marketTemplate);
            var onRing = new Vector3(ring.Position.X, ring.Position.Y, ring.Position.Z);

            using (PlayfieldWorldSimulation withoutItems = PlayfieldWorldSimulation.Create(
                platformId,
                geometry,
                data.GetPlayfieldMetaData(platformId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger()))
            {
                Assert.IsFalse(withoutItems.TryResolveZoneCrossing(
                    onRing.xf,
                    onRing.zf,
                    onRing,
                    new HashSet<int>(),
                    default,
                    out _));
            }

            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                platformId,
                geometry,
                data.GetPlayfieldMetaData(platformId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);

            foreach (int templateId in new[] { 225426, 225427, 225402 })
            {
                var placed = geometry.Dynels.Dynels.Single(dynel => dynel.TemplateId == templateId);
                var at = new Vector3(placed.Position.X, placed.Position.Y, placed.Position.Z);
                Assert.IsTrue(world.TryResolveZoneCrossing(
                    at.xf - 10f,
                    at.zf,
                    at,
                    new HashSet<int>(),
                    default,
                    out ZoneCrossing hit));
                Assert.AreEqual(ZoneTriggerKind.TargetVicinity, hit.Trigger.Kind);
                Assert.IsTrue(hit.Trigger.VicinityEvents!.SpellList[EventType.OnTargetInVicinity]
                    .Any(spell => spell.Is(FunctionType.Teleport)));
            }

            Playfield market = BlankPlayfield(marketPlayfield, data);
            Playfield platform = BlankPlayfield(platformId, data, world, BlankManager(market));
            var session = new RecordingSession();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = platform;
            player.Position = new Vector3(onRing.x + 10, onRing.y, onRing.z);
            platform.GetRequiredService<DynelRegistry>().Register(player);

            world.TickSoftTriggers(platform, 0.1);
            player.Position = onRing;
            world.TickSoftTriggers(platform, 0.1);

            Assert.AreSame(market, session.Destination);
            Assert.AreEqual(405f, session.Landing.xf, 0.001f);
            Assert.AreEqual(367f, session.Landing.yf, 0.001f);
            Assert.AreEqual(490f, session.Landing.zf, 0.001f);
        }

        [TestMethod]
        public void JobeHarbourReturnRingProxiesToThePlatformDoor()
        {
            const int harbourId = 4531;
            const int platformId = 4530;
            const int returnTemplate = 225416;
            int plazaRing = unchecked((int)0xC00011B2);
            int harbourRing = unchecked((int)0xC00211B2);
            var data = new GameDataStore(new StubLogger());
            var catalog = new ItemTemplateCatalog(new EmptyNames(), data, new StubLogger());
            PlayfieldGeometryData harbourGeometry = data.GetPlayfieldGeometry(harbourId);
            var ring = harbourGeometry.Dynels!.Dynels.Single(dynel => dynel.TemplateId == returnTemplate);
            var onRing = new Vector3(ring.Position.X, ring.Position.Y, ring.Position.Z);

            using PlayfieldWorldSimulation harbour = PlayfieldWorldSimulation.Create(
                harbourId,
                harbourGeometry,
                data.GetPlayfieldMetaData(harbourId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);

            Assert.IsTrue(harbour.TryResolveZoneCrossing(
                onRing.xf - 10f,
                onRing.zf,
                onRing,
                new HashSet<int>(),
                default,
                out ZoneCrossing crossing));
            Assert.AreEqual(ZoneTriggerKind.TargetVicinity, crossing.Trigger.Kind);
            Assert.IsTrue(crossing.Trigger.VicinityEvents!.SpellList[EventType.OnTargetInVicinity]
                .Any(spell => spell.Is(FunctionType.TeleportProxy)));

            PlayfieldGeometryData platformGeometry = data.GetPlayfieldGeometry(platformId);
            var door = platformGeometry.Dynels!.Dynels.Single(dynel => dynel.IdentityInstance == harbourRing);
            Vector3 expected = PortalDoorLandingResolver.LandingInFront(
                new Vector3(door.Position.X, door.Position.Y, door.Position.Z),
                new Quaternion(door.Heading.X, door.Heading.Y, door.Heading.Z, door.Heading.W),
                PortalDoorLandingResolver.ProxyEntryDoorClearance);

            using PlayfieldWorldSimulation platform = PlayfieldWorldSimulation.Create(
                platformId,
                platformGeometry,
                data.GetPlayfieldMetaData(platformId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);
            int exits = platform.ExitTriggerCount;
            platform.RegisterExitProxyDoor(plazaRing);
            Assert.AreEqual(exits, platform.ExitTriggerCount);

            var routed = new GameDataStore(
                new StubLogger(),
                new TeleportDestinationCatalog(
                [
                    new TeleportRoute
                    {
                        Playfield = harbourId,
                        StatelType = (int)IdentityType.Door,
                        StatelInstance = unchecked((uint)ring.IdentityInstance),
                        DestinationPlayfield = platformId,
                        DestinationType = (int)IdentityType.Door,
                        DestinationInstance = unchecked((uint)harbourRing)
                    }
                ]));
            Playfield destination = BlankPlayfield(platformId, routed);
            Playfield source = BlankPlayfield(harbourId, routed, harbour, BlankManager(destination));
            var session = new RecordingSession();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = source;
            player.Position = new Vector3(onRing.x + 10, onRing.y, onRing.z);
            source.GetRequiredService<DynelRegistry>().Register(player);

            harbour.TickSoftTriggers(source, 0.1);
            player.Position = onRing;
            harbour.TickSoftTriggers(source, 0.1);

            Assert.AreSame(destination, session.Destination);
            Assert.AreEqual(expected.xf, session.Landing.xf, 0.001f);
            Assert.AreEqual(expected.yf, session.Landing.yf, 0.001f);
            Assert.AreEqual(expected.zf, session.Landing.zf, 0.001f);
            Assert.AreEqual(harbourId, player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance));
            Assert.AreEqual(ring.IdentityInstance, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
        }

        [TestMethod]
        public void JobeHarbourReturnRingLandsOnItsPlatformDestinationLine()
        {
            const int harbourId = 4531;
            const int platformId = 4530;
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            var catalog = new ItemTemplateCatalog(new EmptyNames(), data, new StubLogger());
            PlayfieldGeometryData harbourGeometry = data.GetPlayfieldGeometry(harbourId);
            var ring = harbourGeometry.Dynels!.Dynels.Single(dynel => dynel.TemplateId == 225416);
            var onRing = new Vector3(ring.Position.X, ring.Position.Y, ring.Position.Z);
            using PlayfieldWorldSimulation harbour = PlayfieldWorldSimulation.Create(
                harbourId,
                harbourGeometry,
                data.GetPlayfieldMetaData(harbourId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);

            Playfield destination = BlankPlayfield(platformId, data);
            Playfield source = BlankPlayfield(harbourId, data, harbour, BlankManager(destination));
            var session = new RecordingSession();
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = source;
            player.Position = new Vector3(onRing.x + 10, onRing.y, onRing.z);
            source.GetRequiredService<DynelRegistry>().Register(player);

            harbour.TickSoftTriggers(source, 0.1);
            player.Position = onRing;
            harbour.TickSoftTriggers(source, 0.1);

            Assert.AreSame(destination, session.Destination);
            Assert.AreEqual(276.1f, session.Landing.xf, 0.1f);
            Assert.AreEqual(576.1f, session.Landing.zf, 0.1f);
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
        }

        [TestMethod]
        public void JobePlatformExitDoorsFireFromTheLandingBelowThem()
        {
            const int platformId = 4530;
            const float landingY = 199.52f;
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            var catalog = new ItemTemplateCatalog(new EmptyNames(), data, new StubLogger());
            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(platformId);
            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                platformId,
                geometry,
                data.GetPlayfieldMetaData(platformId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);

            foreach (var (door, destId) in new[] { (unchecked((int)0xC00311B2), 540), (unchecked((int)0xC00411B2), 655), (unchecked((int)0xC00511B2), 730) })
            {
                var placed = geometry.Dynels!.Dynels.Single(d => d.IdentityInstance == door);
                Vector3 front = PortalDoorLandingResolver.LandingInFront(
                    new Vector3(placed.Position.X, landingY, placed.Position.Z),
                    new Quaternion(placed.Heading.X, placed.Heading.Y, placed.Heading.Z, placed.Heading.W),
                    3f);
                var atDoor = new Vector3(placed.Position.X, landingY, placed.Position.Z);
                Playfield destination = BlankPlayfield(destId, data);
                Playfield platform = BlankPlayfield(platformId, data, world, BlankManager(destination));
                var session = new RecordingSession();
                Player player = TestWorld.CreatePlayer(10 + destId);
                player.Session = session;
                session.BindPlayer(player);
                player.Playfield = platform;
                player.Position = front;
                platform.GetRequiredService<DynelRegistry>().Register(player);

                world.TickSoftTriggers(platform, 0.1);
                player.Position = atDoor;
                world.TickSoftTriggers(platform, 0.1);

                Assert.AreEqual(1, session.Transfers, door.ToString("X8"));
                Assert.AreSame(destination, session.Destination);
            }
        }

        [TestMethod]
        public void ReturningThroughTheHarbourRingDoesNotSendYouBack()
        {
            const int platformId = 4530;
            const int harbourTemplate = 225402;
            var data = new GameDataStore(new StubLogger());
            var catalog = new ItemTemplateCatalog(new EmptyNames(), data, new StubLogger());
            PlayfieldGeometryData geometry = data.GetPlayfieldGeometry(platformId);
            var ring = geometry.Dynels!.Dynels.Single(dynel => dynel.TemplateId == harbourTemplate);
            var onRing = new Vector3(ring.Position.X, ring.Position.Y, ring.Position.Z);
            Vector3 landing = PortalDoorLandingResolver.LandingInFront(
                onRing,
                new Quaternion(ring.Heading.X, ring.Heading.Y, ring.Heading.Z, ring.Heading.W),
                PortalDoorLandingResolver.ProxyEntryDoorClearance);

            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                platformId,
                geometry,
                data.GetPlayfieldMetaData(platformId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger(),
                catalog);
            Playfield platform = BlankPlayfield(platformId, data, world, BlankManager(BlankPlayfield(4531, data)));

            var remembered = new RecordingSession();
            Player leftInside = TestWorld.CreatePlayer(1);
            leftInside.Session = remembered;
            remembered.BindPlayer(leftInside);
            leftInside.Playfield = platform;
            leftInside.Position = onRing;
            platform.GetRequiredService<DynelRegistry>().Register(leftInside);
            world.TickSoftTriggers(platform, 0.1);
            Assert.AreEqual(0, remembered.Transfers);

            leftInside.Position = landing;
            world.TickSoftTriggers(platform, 0.1);
            Assert.AreEqual(1, remembered.Transfers);

            var returning = new RecordingSession();
            Player arrived = TestWorld.CreatePlayer(2);
            arrived.Session = returning;
            returning.BindPlayer(arrived);
            arrived.Playfield = platform;
            arrived.Position = onRing;
            platform.GetRequiredService<DynelRegistry>().Register(arrived);
            world.TickSoftTriggers(platform, 0.1);
            Assert.AreEqual(0, returning.Transfers);

            arrived.Position = landing;
            world.DropCharacterTriggerMemory(arrived.Identity.Instance);
            world.TickSoftTriggers(platform, 0.1);
            Assert.AreEqual(0, returning.Transfers);
        }

        static Playfield BlankPlayfield(
            int id,
            IGameData? data = null,
            PlayfieldWorldSimulation? world = null,
            PlayfieldManager? manager = null)
        {
            var playfield = (Playfield)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            Set(playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield, Instance = id });
            IGameData gameData = data ?? new StubGameData(HashItemCatalog.Parse("{}"), rootPath: System.IO.Path.GetTempPath());
            IServiceCollection services = new ServiceCollection()
                .AddSingleton(gameData)
                .AddSingleton(new WorldSimulationAccess { Instance = world })
                .AddSingleton(new DynelRegistry())
                .AddSingleton<IInventoryRepository>(new StubInventoryRepository())
                .AddSingleton<IItemBuilder>(new StubItemBuilder());
            if (manager != null)
                services.AddSingleton(manager);

            Set(playfield, "_serviceProvider", services.BuildServiceProvider());
            return playfield;
        }

        static PlayfieldManager BlankManager(Playfield destination)
        {
            var manager = (PlayfieldManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            Set(manager, "_sync", new Lock());
            Set(
                manager,
                "_playfields",
                new Dictionary<int, Playfield> { [destination.Identity.Instance] = destination });
            return manager;
        }

        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        sealed class RecordingSession : IZoneSession
        {
            public SessionState State { get; set; } = SessionState.InPlay;

            public Player? Player { get; private set; }

            public Playfield? Destination { get; private set; }

            public Vector3 Landing { get; private set; }

            public int Transfers { get; private set; }

            public string Chat { get; private set; } = string.Empty;

            public bool SamePlayfield { get; private set; }

            public void BindPlayer(Player player) => Player = player;

            public void UnbindPlayer() => Player = null;

            public void TransferToPlayfield(Playfield destination, Vector3 landing)
            {
                Transfers++;
                Destination = destination;
                Landing = landing;
            }

            public void TransferToPlayfield(Playfield destination, Vector3 landing, Quaternion heading)
                => TransferToPlayfield(destination, landing);

            public void SendSamePlayfieldRespawnTeleport(Vector3 landing) => SamePlayfield = true;

            public int? IntrazoneKey { get; private set; }

            public Quaternion? IntrazoneHeading { get; private set; }

            public void SendIntrazoneTeleport(Vector3 landing, Quaternion heading, int destinationKey)
            {
                IntrazoneKey = destinationKey;
                IntrazoneHeading = heading;
                Landing = landing;
            }

            public void Send(byte[] packet)
            {
            }

            public void Send(Message message) => Note(message);

            public void Send(MessageBody body) => Note(body);

            void Note(object message)
            {
                if (message is ChatTextMessage chat)
                    Chat = chat.Text ?? string.Empty;
            }

            public void Send(MessageBody body, int sender, int receiver)
            {
            }

            public void SendInitiateCompression()
            {
            }

            public void Close() => State = SessionState.Closed;
        }

        sealed class EmptyNames : IItemNameRepository
        {
            public bool TryGetName(int aoid, out string name)
            {
                name = string.Empty;
                return false;
            }

            public IReadOnlyDictionary<int, string> GetAllNames()
                => new Dictionary<int, string>();
        }
    }
}
