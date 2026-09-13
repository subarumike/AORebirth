namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using AODB.Common.RDBObjects;
    using AORebirth.Database.Dao;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class TeleportDestinationCatalogTests
    {
        // Existing teleports DAO rows, verified against the staging database.
        [TestMethod]
        [DataRow(1186, 0xC0170320u, 0xC00404A2u, 175.00107f, 113.01496f)]
        [DataRow(2064, 0xC0030320u, 0xC0010810u, 191.00363f, 158.98544f)]
        public void Dao_destination_controls_both_arrival_and_reverse_exit(
            int destinationPf, uint sourceDoor, uint destinationDoor, float expectedX, float expectedZ)
        {
            var row = Route(destinationPf, sourceDoor, destinationDoor);
            var data = new GameDataStore(new StubLogger(), new TeleportDestinationCatalog(new[] { row }));
            var door = data.GetPlayfieldGeometry(800).Dynels!.Dynels.Single(d =>
                d.IdentityType == 51016 && unchecked((uint)d.IdentityInstance) == sourceDoor);
            Assert.IsTrue(PortalDoorLandingResolver.TryReadPortal(door, out var portal));
            Assert.AreEqual(unchecked((int)destinationDoor), portal.DoorInstance);
            Assert.IsTrue(portal.RecordsReturn);
            Assert.IsTrue(PortalDoorLandingResolver.TryResolveDoorLanding(
                data.GetPlayfieldGeometry(destinationPf), portal.DoorInstance, portal.DoorClearance, out var landing));
            Assert.AreEqual(expectedX, (float)landing.x, 0.001f);
            Assert.AreEqual(expectedZ, (float)landing.z, 0.001f);
            var exits = new Dictionary<int, HashSet<int>>();
            ExitProxyDoorCatalog.CollectFromDynels(new PlayfieldDynels { Dynels = new List<PlayfieldDynel> { door } }, exits);
            CollectionAssert.AreEquivalent(new[] { unchecked((int)destinationDoor) }, exits[destinationPf].ToArray());
            CollectionAssert.Contains(data.GetExitProxyDoorInstances(destinationPf).ToArray(), unchecked((int)destinationDoor));
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            using var world = PlayfieldWorldSimulation.Create(destinationPf, data.GetPlayfieldGeometry(destinationPf),
                data.GetPlayfieldMetaData(destinationPf), DestinationsCatalog.Instance, data, new StubLogger());
            var returnTo = new ProxyReturn { PlayfieldId = 800, DoorInstance = unchecked((int)sourceDoor) };
            var target = data.GetPlayfieldGeometry(destinationPf).Dynels!.Dynels.Single(d =>
                d.IdentityType == 51016 && unchecked((uint)d.IdentityInstance) == destinationDoor);
            using var outside = PlayfieldWorldSimulation.Create(800, data.GetPlayfieldGeometry(800),
                data.GetPlayfieldMetaData(800), DestinationsCatalog.Instance, data, new StubLogger());
            Assert.IsTrue(outside.TryResolveZoneCrossing(door.Position.X, door.Position.Z,
                new AORebirth.Core.Vector.Vector3(door.Position.X, door.Position.Y, door.Position.Z),
                new HashSet<int>(), default, out var entry));
            Assert.AreEqual(destinationPf, entry.DestPlayfieldId);
            AssertFacesAwayFromDoor(entry, target);
            Assert.IsTrue(world.TryResolveZoneCrossing(target.Position.X, target.Position.Z,
                new AORebirth.Core.Vector.Vector3(target.Position.X, target.Position.Y, target.Position.Z),
                new HashSet<int>(), returnTo, out var crossing));
            Assert.AreEqual(800, crossing.DestPlayfieldId);
            AssertFacesAwayFromDoor(crossing, door);
            var wrong = data.GetPlayfieldGeometry(destinationPf).Dynels!.Dynels.Single(d =>
                d.IdentityType == 51016 && unchecked((uint)d.IdentityInstance) == (0xC0000000u | (uint)destinationPf));
            Assert.IsFalse(world.TryResolveZoneCrossing(wrong.Position.X, wrong.Position.Z,
                new AORebirth.Core.Vector.Vector3(wrong.Position.X, wrong.Position.Y, wrong.Position.Z),
                new HashSet<int>(), returnTo, out _), "An unrelated room door must not become this character's return exit.");
        }

        [TestMethod]
        public void Conflicting_routes_are_rejected_instead_of_using_row_order()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => new TeleportDestinationCatalog(new[]
            {
                Route(1186, 0xC0170320u, 0xC00404A2u),
                Route(1186, 0xC0170320u, 0xC00004A2u)
            }));
        }

        [TestMethod]
        public void Another_identity_type_cannot_override_a_door_with_the_same_instance()
        {
            var row = Route(1186, 0xC0170320u, 0xC00404A2u);
            row.StatelType = 51035;
            var data = new GameDataStore(new StubLogger(), new TeleportDestinationCatalog(new[] { row }));
            var door = data.GetPlayfieldGeometry(800).Dynels!.Dynels.Single(d =>
                d.IdentityType == 51016 && unchecked((uint)d.IdentityInstance) == row.StatelInstance);
            Assert.IsTrue(PortalDoorLandingResolver.TryReadPortal(door, out var portal));
            Assert.AreEqual(unchecked((int)0xC00004A2u), portal.DoorInstance);
        }

        [TestMethod]
        [DataRow(1186, 51016, 0xC0010810u)]
        [DataRow(0, 0, 0u)]
        public void Invalid_or_disabled_route_cannot_fall_back_to_raw_destination(int pf, int type, uint target)
        {
            var row = Route(pf, 0xC0170320u, target);
            row.DestinationType = type;
            var data = new GameDataStore(new StubLogger(), new TeleportDestinationCatalog(new[]
            {
                row
            }));
            var door = data.GetPlayfieldGeometry(800).Dynels!.Dynels.Single(d =>
                d.IdentityType == 51016 && unchecked((uint)d.IdentityInstance) == row.StatelInstance);
            Assert.IsFalse(PortalDoorLandingResolver.TryReadPortal(door, out _));
        }

        static TeleportRoute Route(int pf, uint source, uint target) => new()
        {
            Playfield = 800, StatelType = 51016, StatelInstance = source,
            DestinationPlayfield = pf, DestinationType = 51016, DestinationInstance = target
        };

        static void AssertFacesAwayFromDoor(ZoneCrossing crossing, PlayfieldDynel door)
        {
            Assert.IsNotNull(crossing.Heading, "Door crossing discarded destination heading.");
            var forward = (AORebirth.Core.Vector.Vector3)crossing.Heading.RotateVector3(AORebirth.Core.Vector.Vector3.AxisZ);
            double awayX = crossing.Landing.x - door.Position.X;
            double awayZ = crossing.Landing.z - door.Position.Z;
            Assert.IsTrue(awayX * forward.x + awayZ * forward.z > 0,
                "Arrival must face away from the door frame along its existing landing clearance.");
            Assert.AreEqual((double)door.Heading.X, crossing.Heading.x);
            Assert.AreEqual((double)door.Heading.Y, crossing.Heading.y);
            Assert.AreEqual((double)door.Heading.Z, crossing.Heading.z);
            Assert.AreEqual((double)door.Heading.W, crossing.Heading.w);
        }
    }
}
