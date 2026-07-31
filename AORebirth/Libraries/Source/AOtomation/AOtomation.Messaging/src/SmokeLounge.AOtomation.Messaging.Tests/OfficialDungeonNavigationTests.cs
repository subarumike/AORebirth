namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AORebirth.Core.Playfields;
    using ZoneEngine.Core.Navigation;

    [TestClass]
    public class OfficialDungeonNavigationTests
    {
        private const double MaximumCapturedGroundDifference = 0.65;

        [TestMethod]
        public void Pf1931LoadsOfficialResourceIdentityAndConnectedRoomGraph()
        {
            OfficialDungeonGeometryLoadResult load = LoadGeometry();

            Assert.IsTrue(load.IsLoaded, load.Error);
            Assert.AreEqual(1931, load.Geometry.PlayfieldResource);
            Assert.AreEqual(1930, load.Geometry.TilemapResource);
            Assert.AreEqual(200, load.Geometry.Width);
            Assert.AreEqual(200, load.Geometry.Height);
            Assert.AreEqual(30, load.Geometry.RoomCount);
            Assert.IsTrue(load.Geometry.DoorConnectionCount > 0);
            Assert.IsTrue(load.Geometry.IsOfficialRoomGraphConnected());
            Assert.AreEqual(
                "759754a064c5740000bc2168a00bfc267b31f32d51fe331a69dfd592c3466804",
                load.Geometry.SourceSha256);

            string[] rooms = load.Geometry.RoomNames.ToArray();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "EntryHall",
                    "GreatHall",
                    "Present_temple",
                    "Present_chambers",
                    "Present_concourse",
                    "Future_guardian",
                    "Future"
                },
                rooms);
        }

        [TestMethod]
        public void AllTempleOrdinarySpawnsAndMurialPatrolGroundOnOfficialSurfaces()
        {
            OfficialDungeonChaseNavigationProvider provider = LoadProvider();
            OrdinaryEnemySpawnDefinition[] spawns =
                new CapturedTempleOfThreeWindsContentProvider().GetSpawns();

            Assert.AreEqual(167, spawns.Length);
            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                ChaseNavigationPoint grounded = AssertGrounded(
                    provider,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.SpawnKey);
                Assert.IsTrue(
                    provider.IsSegmentTraversable(grounded, grounded),
                    spawn.SpawnKey);

                foreach (OrdinaryEnemyWaypoint waypoint in spawn.Waypoints)
                {
                    AssertGrounded(
                        provider,
                        waypoint.X,
                        waypoint.Y,
                        waypoint.Z,
                        spawn.SpawnKey + ":patrol");
                }
            }

            OrdinaryEnemySpawnDefinition murial = spawns.Single(
                spawn => spawn.ProfileKey
                         == CapturedTempleOfThreeWindsContentProvider.MurialProfileKey);
            Assert.AreEqual(20, murial.Waypoints.Length);
            ChaseNavigationPoint previous = AssertGrounded(
                provider,
                murial.X,
                murial.Y,
                murial.Z,
                "Murial");
            foreach (OrdinaryEnemyWaypoint waypoint in murial.Waypoints)
            {
                ChaseNavigationPoint next = AssertGrounded(
                    provider,
                    waypoint.X,
                    waypoint.Y,
                    waypoint.Z,
                    "Murial patrol");
                Assert.IsTrue(
                    provider.IsSegmentTraversable(previous, next),
                    string.Format(
                        "Murial patrol segment {0:F3},{1:F3} -> {2:F3},{3:F3}",
                        previous.X,
                        previous.Z,
                        next.X,
                        next.Z));
                previous = next;
            }
        }

        [TestMethod]
        public void AllTempleNamedActorsSuccessorsAndAddsGroundOnOfficialSurfaces()
        {
            OfficialDungeonChaseNavigationProvider provider = LoadProvider();
            string[] names =
            {
                "Defender of the Three",
                "Windcaller Yatila",
                "Reverend Gulard",
                "The Re-Animator",
                "Acolyte Betany",
                "The Curator",
                "Nematet the Custodian of Time",
                "Guardian of Tomorrow",
                "Gartua the Doorkeeper",
                "Uklesh the Frozen",
                "Khalum",
                "Aztur the Immortal"
            };
            double[,] coordinates =
            {
                { 173.1958, 31.9949989, 266.324951 },
                { 95.31601, 13.0112486, 258.637878 },
                { 60.4321442, 16.0409985, 291.730774 },
                { 60.20344, 16.0112476, 295.703949 },
                { 46.1443329, 12.01125, 259.741333 },
                { 121.159302, 34.0749969, 352.137634 },
                { 171.324936, 36.0112457, 340.074097 },
                { 274.823364, 13.01125, 388.980774 },
                { 274.99, 14.2112513, 426.642548 },
                { 274.950745, 16.611248, 531.1443 },
                { 281.30542, 16.611248, 529.3965 },
                { 280.845642, 16.611248, 518.7123 }
            };

            Assert.AreEqual(names.Length, coordinates.GetLength(0));
            for (int index = 0; index < names.Length; index++)
            {
                AssertGrounded(
                    provider,
                    coordinates[index, 0],
                    coordinates[index, 1],
                    coordinates[index, 2],
                    names[index],
                    4.0);
            }

            AssertGrounded(provider, 65.80717, 16.01125, 292.15747, "Reanimated add 1");
            AssertGrounded(provider, 65.74661, 15.53284, 288.377, "Reanimated add 2");
        }

        [TestMethod]
        public void MajorRoomsHaveWalkableAnchorsWhileSolidBoundariesBlockMovementAndLos()
        {
            OfficialDungeonGeometry geometry = LoadGeometry().Geometry;
            OfficialDungeonChaseNavigationProvider provider = LoadProvider();
            string[] majorRooms =
            {
                "EntryHall",
                "GreatHall",
                "Present_temple",
                "Present_chambers",
                "Present_concourse",
                "Future_guardian",
                "Future"
            };

            foreach (string room in majorRooms)
            {
                ChaseNavigationPoint anchor;
                Assert.IsTrue(geometry.TryGetRoomAnchor(room, out anchor), room);
                Assert.IsTrue(provider.IsSegmentTraversable(anchor, anchor), room);
            }

            ChaseNavigationPoint greatHall;
            Assert.IsTrue(geometry.TryGetRoomAnchor("GreatHall", out greatHall));
            bool foundBlockedBoundary = false;
            for (int direction = 0; direction < 8 && !foundBlockedBoundary; direction++)
            {
                double angle = direction * Math.PI / 4.0;
                ChaseNavigationPoint endpoint = new ChaseNavigationPoint(
                    greatHall.X + (Math.Cos(angle) * 80.0),
                    greatHall.Y,
                    greatHall.Z + (Math.Sin(angle) * 80.0));
                if (!provider.IsSegmentTraversable(greatHall, endpoint)
                    && !provider.IsAttackLineTraversable(greatHall, endpoint))
                {
                    foundBlockedBoundary = true;
                }
            }

            Assert.IsTrue(
                foundBlockedBoundary,
                "No official GreatHall solid boundary blocked both movement and LOS.");
        }

        [TestMethod]
        public void Pf1931FactoryReusesImmutableGeometryAndDisposalClearsRouteState()
        {
            OfficialDungeonGeometryLoadResult first = Pf1931OfficialDungeonGeometryLoader.Current;
            OfficialDungeonGeometryLoadResult second = Pf1931OfficialDungeonGeometryLoader.Current;
            Assert.AreSame(first, second);
            Assert.AreSame(first.Geometry, second.Geometry);

            IPlayfieldChaseNavigationProvider provider =
                PlayfieldChaseNavigationProviderFactory.Create(1931);
            Assert.IsInstanceOfType(provider, typeof(OfficialDungeonChaseNavigationProvider));
            Assert.AreEqual(ChaseNavigationCapability.Supported, provider.Capability);

            var runtime = new NpcChaseNavigationRuntimeService(provider);
            ChaseNavigationPoint start = AssertGrounded(
                (OfficialDungeonChaseNavigationProvider)provider,
                271.4782,
                14.8112507,
                445.842255,
                "Murial runtime start");
            ChaseNavigationPoint target = AssertGrounded(
                (OfficialDungeonChaseNavigationProvider)provider,
                269.505005,
                14.8112478,
                481.484863,
                "Murial runtime target");

            runtime.UpdatePursuit(1001, 2001, start, target, 1.0, DateTime.UtcNow);
            Assert.IsTrue(runtime.ActiveStateCount > 0);
            runtime.Dispose();
            Assert.AreEqual(0, runtime.ActiveStateCount);
            ChaseNavigationPoint disposedProjection;
            Assert.IsFalse(runtime.TryProjectToSurface(start, out disposedProjection));
        }

        private static ChaseNavigationPoint AssertGrounded(
            OfficialDungeonChaseNavigationProvider provider,
            double x,
            double y,
            double z,
            string label,
            double maximumGroundDifference = MaximumCapturedGroundDifference)
        {
            ChaseNavigationPoint projected;
            Assert.IsTrue(
                provider.TryProjectToSurface(
                    new ChaseNavigationPoint(x, y, z),
                    x,
                    z,
                    out projected),
                label);
            Assert.AreEqual(x, projected.X, 0.000001, label);
            Assert.AreEqual(z, projected.Z, 0.000001, label);
            Assert.IsTrue(
                Math.Abs(projected.Y - y) <= maximumGroundDifference,
                string.Format(
                    "{0}: captured Y {1:F6}, official Y {2:F6}",
                    label,
                    y,
                    projected.Y));
            return projected;
        }

        private static OfficialDungeonChaseNavigationProvider LoadProvider()
        {
            OfficialDungeonGeometryLoadResult load = LoadGeometry();
            Assert.IsTrue(load.IsLoaded, load.Error);
            return new OfficialDungeonChaseNavigationProvider(1931, load);
        }

        private static OfficialDungeonGeometryLoadResult LoadGeometry()
        {
            string path = Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Content\Official\TempleOfThreeWinds\pf1931-dungeon-geometry.json");
            return Pf1931OfficialDungeonGeometryLoader.LoadPath(path);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("AORebirth repository root was not found.");
        }
    }
}
