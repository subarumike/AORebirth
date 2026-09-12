namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class BuildingExitProxyTests
    {
        [TestMethod]
        [DataRow(954)]
        [DataRow(1186)]
        [DataRow(2064)]
        public void Pf800_building_destinations_bake_a_return_exit(int buildingPlayfieldId)
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);

            Assert.IsTrue(
                PlayfieldManager.RequiresWorldSimulation(data, buildingPlayfieldId),
                $"PF {buildingPlayfieldId} was routed to the non-zoning indoor runtime.");

            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                buildingPlayfieldId,
                data.GetPlayfieldGeometry(buildingPlayfieldId),
                data.GetPlayfieldMetaData(buildingPlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            Assert.IsTrue(
                world.ExitTriggerCount > 0,
                $"PF {buildingPlayfieldId} did not bake a return exit for its PF 800 TeleportProxy landing.");
        }

        [TestMethod]
        [DataRow(1186)]
        [DataRow(2064)]
        public void Pf800_building_return_resolves_the_exterior_source_door(int buildingPlayfieldId)
        {
            var data = new GameDataStore(new StubLogger());
            var sourceDoor = data.GetPlayfieldGeometry(800).Dynels!.Dynels.First(d =>
                PortalDoorLandingResolver.TryReadPortal(d, out PortalDestination destination)
                && destination.PlayfieldId == buildingPlayfieldId
                && destination.RecordsReturn);
            PortalDoorLandingResolver.TryReadPortal(sourceDoor, out PortalDestination portal);
            var interiorDoor = data.GetPlayfieldGeometry(buildingPlayfieldId).Dynels!.Dynels.First(d =>
                d.IdentityInstance == portal.DoorInstance
                && d.IdentityType == (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.Door);

            Assert.IsTrue(
                PortalDoorLandingResolver.TryResolveDoorLanding(
                    data.GetPlayfieldGeometry(800),
                    sourceDoor.IdentityInstance,
                    PortalDoorLandingResolver.ExitDoorClearance,
                    out _),
                $"PF {buildingPlayfieldId} records exterior dynel {sourceDoor.IdentityType}:{sourceDoor.IdentityInstance:X8}, which is not a PF 800 Door landing.");
        }

        [TestMethod]
        public void Pf2064_return_exit_matches_the_legacy_statel_collision_envelope()
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            var sourceDoor = data.GetPlayfieldGeometry(800).Dynels!.Dynels.First(d =>
                PortalDoorLandingResolver.TryReadPortal(d, out PortalDestination destination)
                && destination.PlayfieldId == 2064
                && destination.RecordsReturn);
            PortalDoorLandingResolver.TryReadPortal(sourceDoor, out PortalDestination portal);
            var interiorDoor = data.GetPlayfieldGeometry(2064).Dynels!.Dynels.First(d =>
                d.IdentityInstance == portal.DoorInstance
                && d.IdentityType == (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.Door);

            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                2064,
                data.GetPlayfieldGeometry(2064),
                data.GetPlayfieldMetaData(2064),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            Assert.IsTrue(world.TryResolveZoneCrossing(
                interiorDoor.Position.X + 1.5f,
                interiorDoor.Position.Z + 3f,
                new AORebirth.Core.Vector.Vector3(
                    interiorDoor.Position.X + 1.5f,
                    interiorDoor.Position.Y + 5.5f,
                    interiorDoor.Position.Z),
                new HashSet<int>(),
                new ProxyReturn { PlayfieldId = 800, DoorInstance = sourceDoor.IdentityInstance },
                out ZoneCrossing crossing));
            Assert.AreEqual(800, crossing.DestPlayfieldId);
        }
    }
}
