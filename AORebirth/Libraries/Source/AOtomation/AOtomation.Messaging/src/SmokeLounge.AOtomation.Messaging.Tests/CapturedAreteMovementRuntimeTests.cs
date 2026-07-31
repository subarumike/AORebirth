namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class CapturedAreteMovementRuntimeTests
    {
        private const string Header =
            "ObservationId,EquivalentObservationCount,CapturedUtc,Sequence,Behavior,"
            + "NpcFamily,MonsterData,Level,CapturedPlayfieldId,RuntimePlayfieldId,Name,"
            + "SourceIdentity,SourceGeneration,RouteSignature,StartX,StartY,StartZ,"
            + "EndX,EndY,EndZ,DelayAfterSeconds,PathCount";

        private static readonly DateTime Epoch =
            new DateTime(2026, 7, 22, 15, 24, 54, DateTimeKind.Utc);

        [TestMethod]
        public void CommittedCatalogLoadsAllPromotableObservationsAndExcludesScripted()
        {
            CapturedAreteMovementCatalog catalog = CapturedAreteMovementCatalog.LoadDefault();

            Assert.IsTrue(catalog.IsValid, catalog.FailureReason);
            Assert.AreEqual(8229, catalog.SourceObservationCount);
            Assert.AreEqual(8121, catalog.RuntimeObservationCount);
            Assert.AreEqual(7208, catalog.Count(CapturedAreteMovementBehavior.Patrol));
            Assert.AreEqual(597, catalog.Count(CapturedAreteMovementBehavior.Spawn));
            Assert.AreEqual(98, catalog.Count(CapturedAreteMovementBehavior.Chase));
            Assert.AreEqual(22, catalog.Count(CapturedAreteMovementBehavior.Flee));
            Assert.AreEqual(196, catalog.Count(CapturedAreteMovementBehavior.Leash));
        }

        [TestMethod]
        public void LoaderPreservesExactEvidenceAndFailsClosedOnIdentityMismatch()
        {
            string directory = CreateDataset(
                new[]
                {
                    Row(
                        "patrol",
                        "SimpleChar:00000001",
                        0,
                        0,
                        0.0,
                        0.5,
                        1.25,
                        2.5,
                        3.75,
                        2)
                });
            try
            {
                CapturedAreteMovementCatalog catalog =
                    CapturedAreteMovementCatalog.Load(directory, 2, 1);
                Assert.IsTrue(catalog.IsValid, catalog.FailureReason);
                Assert.AreEqual(2, catalog.SourceObservationCount);

                string patrol = Path.Combine(directory, "patrol.csv");
                File.WriteAllText(
                    patrol,
                    Header
                    + Environment.NewLine
                    + Row(
                        "patrol",
                        "BadIdentity:1",
                        1,
                        1,
                        0.0,
                        0.5,
                        1.25,
                        2.5,
                        3.75,
                        2));
                CapturedAreteMovementCatalog invalid =
                    CapturedAreteMovementCatalog.Load(directory, 2, 1);
                Assert.IsFalse(invalid.IsValid);
                StringAssert.Contains(invalid.FailureReason, "identity-evidence-mismatch");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void PatrolVariantSelectionIsDeterministicPerSpawnGeneration()
        {
            string directory = CreateDataset(
                new[]
                {
                    Row("patrol", "SimpleChar:00000001", 1, 1, 0, 0, 0, 1, 0, 1),
                    Row("patrol", "SimpleChar:00000002", 1, 2, 0, 0, 0, 2, 0, 1)
                });
            try
            {
                CapturedAreteMovementCatalog catalog =
                    CapturedAreteMovementCatalog.Load(directory, 2, 2);
                var first = new CapturedAreteMovementRuntimeCoordinator(catalog);
                CapturedAreteMovementActorEvidence generationOne = Actor(101, 1, 0, 0);
                Assert.IsTrue(first.Activate(generationOne));
                string firstIdentity;
                int firstGeneration;
                Assert.IsTrue(first.TryGetCapturedIdentity(101, out firstIdentity, out firstGeneration));
                Assert.AreEqual("SimpleChar:00000001", firstIdentity);

                var second = new CapturedAreteMovementRuntimeCoordinator(catalog);
                CapturedAreteMovementActorEvidence generationTwo = Actor(102, 2, 0, 0);
                Assert.IsTrue(second.Activate(generationTwo));
                string secondIdentity;
                int secondGeneration;
                Assert.IsTrue(second.TryGetCapturedIdentity(102, out secondIdentity, out secondGeneration));
                Assert.AreEqual("SimpleChar:00000002", secondIdentity);

                var repeat = new CapturedAreteMovementRuntimeCoordinator(catalog);
                Assert.IsTrue(repeat.Activate(Actor(103, 2, 0, 0)));
                string repeatedIdentity;
                int repeatedGeneration;
                Assert.IsTrue(repeat.TryGetCapturedIdentity(103, out repeatedIdentity, out repeatedGeneration));
                Assert.AreEqual(secondIdentity, repeatedIdentity);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void LifecycleConditionsActivateOnlyTheirCapturedBehavior()
        {
            string directory = CreateDataset(
                new[]
                {
                    Row("spawn", "SimpleChar:00000001", 1, 1, 0, 0, 0, 1, 0, 1),
                    Row("patrol", "SimpleChar:00000001", 1, 2, 0, 0, 0, 1, 0, 1),
                    Row("chase", "SimpleChar:00000001", 1, 3, 0, 0, 0, 2, 0, 1),
                    Row("flee", "SimpleChar:00000001", 1, 4, 0, 0, 0, -2, 0, 1),
                    Row("leash", "SimpleChar:00000001", 1, 5, 0, 0, 0, 2, 0, 1)
                });
            try
            {
                CapturedAreteMovementCatalog catalog =
                    CapturedAreteMovementCatalog.Load(directory, 5, 5);
                CapturedAreteMovementObservation selected;

                AssertDecision(
                    catalog,
                    Actor(1, 1, 0, 0),
                    CapturedAreteMovementBehavior.Spawn,
                    CapturedAreteMovementDecisionKind.Movement);
                AssertDecision(
                    catalog,
                    Actor(2, 1, 0, 0),
                    CapturedAreteMovementBehavior.Patrol,
                    CapturedAreteMovementDecisionKind.Movement);

                CapturedAreteMovementActorEvidence chase = Actor(3, 1, 0, 0);
                chase.Fighting = true;
                chase.TargetPosition = Point(10, 0);
                var chaseRuntime = new CapturedAreteMovementRuntimeCoordinator(catalog);
                Assert.IsTrue(chaseRuntime.Activate(chase));
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Movement,
                    chaseRuntime.Select(
                        chase,
                        CapturedAreteMovementBehavior.Chase,
                        Epoch,
                        out selected));

                CapturedAreteMovementActorEvidence flee = Actor(4, 1, 0, 0);
                flee.Fighting = true;
                flee.TargetPosition = Point(10, 0);
                var fleeRuntime = new CapturedAreteMovementRuntimeCoordinator(catalog);
                Assert.IsTrue(fleeRuntime.Activate(flee));
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Movement,
                    fleeRuntime.Select(
                        flee,
                        CapturedAreteMovementBehavior.Flee,
                        Epoch,
                        out selected));

                CapturedAreteMovementActorEvidence leash = Actor(5, 1, 0, 0);
                leash.ReturningHome = true;
                leash.HomePosition = Point(10, 0);
                var leashRuntime = new CapturedAreteMovementRuntimeCoordinator(catalog);
                Assert.IsTrue(leashRuntime.Activate(leash));
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Movement,
                    leashRuntime.Select(
                        leash,
                        CapturedAreteMovementBehavior.Leash,
                        Epoch,
                        out selected));

                CapturedAreteMovementActorEvidence idle = Actor(6, 1, 0, 0);
                var rejected = new CapturedAreteMovementRuntimeCoordinator(catalog);
                Assert.IsTrue(rejected.Activate(idle));
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Fallback,
                    rejected.Select(
                        idle,
                        CapturedAreteMovementBehavior.Chase,
                        Epoch,
                        out selected));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void IdleControllerCanEnterCapturedPatrolWithoutWaypointState()
        {
            Assert.IsTrue(
                CapturedAreteMovementRuntimeCoordinator.PatrolConditionMatches(
                    true,
                    false));
            Assert.IsFalse(
                CapturedAreteMovementRuntimeCoordinator.PatrolConditionMatches(
                    false,
                    false));
            Assert.IsFalse(
                CapturedAreteMovementRuntimeCoordinator.PatrolConditionMatches(
                    true,
                    true));
        }

        [TestMethod]
        public void CapturedAggroCatalogLoadsNpcFirstDistancesAndFailsClosed()
        {
            CapturedAreteAggroCatalog catalog = CapturedAreteAggroCatalog.LoadDefault();
            var rollerrat = new CapturedAreteMovementActorEvidence
                            {
                                RuntimeIdentity = 1,
                                SpawnGeneration = 1,
                                NpcFamily = 55,
                                MonsterData = 17687,
                                Level = 5,
                                PlayfieldId = 6553,
                                Name = "Rollerrat",
                                Position = Point(0, 0)
                            };
            double radius;

            Assert.IsTrue(catalog.IsValid, catalog.FailureReason);
            Assert.AreEqual(11, catalog.Count);
            Assert.IsTrue(catalog.TryGetRadius(rollerrat, out radius));
            Assert.AreEqual(16.639269, radius, 0.000001);

            rollerrat.Level = 4;
            Assert.IsFalse(catalog.TryGetRadius(rollerrat, out radius));
            rollerrat.Level = 5;
            rollerrat.Name = "Uncaptured Rollerrat";
            Assert.IsFalse(catalog.TryGetRadius(rollerrat, out radius));
        }

        [TestMethod]
        public void TimingInterruptionAndGeometryMismatchFallBackWithoutInventingRoute()
        {
            string directory = CreateDataset(
                new[]
                {
                    Row("patrol", "SimpleChar:00000001", 1, 1, 0, 0, 0, 1, 0, 1, 5),
                    Row("patrol", "SimpleChar:00000001", 1, 2, 1.75, 0, 0, 2, 0, 1)
                });
            try
            {
                CapturedAreteMovementCatalog catalog =
                    CapturedAreteMovementCatalog.Load(directory, 2, 2);
                var runtime = new CapturedAreteMovementRuntimeCoordinator(catalog);
                CapturedAreteMovementActorEvidence actor = Actor(1, 1, 0, 0);
                CapturedAreteMovementObservation selected;
                Assert.IsTrue(runtime.Activate(actor));
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Movement,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch,
                        out selected));
                Assert.AreEqual(5.0, selected.DelayAfterSeconds, 0.0001);

                actor.Position = Point(1, 0);
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Waiting,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch.AddSeconds(4),
                        out selected));

                actor.Position = Point(1.75, 0);
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Fallback,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch.AddSeconds(5),
                        out selected));

                runtime.Interrupt(actor.RuntimeIdentity);
                actor.Position = Point(50, 50);
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Fallback,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch.AddSeconds(6),
                        out selected));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void MetadataMismatchFailsClosedAndRemovesRegeneratedRuntimeIdentity()
        {
            string directory = CreateDataset(
                new[]
                {
                    Row("patrol", "SimpleChar:00000001", 1, 1, 0, 0, 0, 1, 0, 1)
                });
            try
            {
                CapturedAreteMovementCatalog catalog =
                    CapturedAreteMovementCatalog.Load(directory, 1, 1);
                var runtime = new CapturedAreteMovementRuntimeCoordinator(catalog);
                CapturedAreteMovementActorEvidence actor = Actor(1, 1, 0, 0);
                CapturedAreteMovementObservation selected;
                Assert.IsTrue(runtime.Activate(actor));

                actor.MonsterData++;
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Fallback,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch,
                        out selected));

                runtime.Remove(actor.RuntimeIdentity);
                actor.MonsterData--;
                Assert.AreEqual(
                    CapturedAreteMovementDecisionKind.Fallback,
                    runtime.Select(
                        actor,
                        CapturedAreteMovementBehavior.Patrol,
                        Epoch,
                        out selected));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void AssertDecision(
            CapturedAreteMovementCatalog catalog,
            CapturedAreteMovementActorEvidence actor,
            CapturedAreteMovementBehavior behavior,
            CapturedAreteMovementDecisionKind expected)
        {
            var runtime = new CapturedAreteMovementRuntimeCoordinator(catalog);
            Assert.IsTrue(runtime.Activate(actor));
            CapturedAreteMovementObservation selected;
            Assert.AreEqual(expected, runtime.Select(actor, behavior, Epoch, out selected));
        }

        private static CapturedAreteMovementActorEvidence Actor(
            int runtimeIdentity,
            int generation,
            double x,
            double z)
        {
            return new CapturedAreteMovementActorEvidence
                   {
                       RuntimeIdentity = runtimeIdentity,
                       SpawnGeneration = generation,
                       NpcFamily = 1019,
                       MonsterData = 297023,
                       Level = 2,
                       PlayfieldId = CapturedAreteMovementCatalog.RuntimePlayfieldId,
                       Name = "Captured Test NPC",
                       Position = Point(x, z)
                   };
        }

        private static CapturedAreteMovementPoint Point(double x, double z)
        {
            return new CapturedAreteMovementPoint(x, 0, z);
        }

        private static string CreateDataset(IEnumerable<string> rows)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "captured-arete-movement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var byBehavior = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                             {
                                 { "patrol", new List<string>() },
                                 { "spawn", new List<string>() },
                                 { "chase", new List<string>() },
                                 { "flee", new List<string>() },
                                 { "leash", new List<string>() }
                             };
            foreach (string row in rows)
            {
                string[] columns = row.Split(',');
                byBehavior[columns[4]].Add(row);
            }

            foreach (KeyValuePair<string, List<string>> entry in byBehavior)
            {
                File.WriteAllText(
                    Path.Combine(directory, entry.Key + ".csv"),
                    Header
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, entry.Value));
            }

            return directory;
        }

        private static string Row(
            string behavior,
            string sourceIdentity,
            int sourceGeneration,
            long sequence,
            double startX,
            double startY,
            double startZ,
            double endX,
            double endZ,
            int equivalentCount,
            double delay = 0)
        {
            return string.Join(
                ",",
                new[]
                {
                    "test-" + sequence.ToString(CultureInfo.InvariantCulture),
                    equivalentCount.ToString(CultureInfo.InvariantCulture),
                    Epoch.AddMilliseconds(sequence).ToString("O", CultureInfo.InvariantCulture),
                    sequence.ToString(CultureInfo.InvariantCulture),
                    behavior,
                    "1019",
                    "297023",
                    "2",
                    CapturedAreteMovementCatalog.CapturedPlayfieldId.ToString(CultureInfo.InvariantCulture),
                    CapturedAreteMovementCatalog.RuntimePlayfieldId.ToString(CultureInfo.InvariantCulture),
                    "Captured Test NPC",
                    sourceIdentity,
                    sourceGeneration.ToString(CultureInfo.InvariantCulture),
                    "route-" + sequence.ToString(CultureInfo.InvariantCulture),
                    startX.ToString(CultureInfo.InvariantCulture),
                    startY.ToString(CultureInfo.InvariantCulture),
                    startZ.ToString(CultureInfo.InvariantCulture),
                    endX.ToString(CultureInfo.InvariantCulture),
                    startY.ToString(CultureInfo.InvariantCulture),
                    endZ.ToString(CultureInfo.InvariantCulture),
                    delay.ToString(CultureInfo.InvariantCulture),
                    "2"
                });
        }
    }
}
