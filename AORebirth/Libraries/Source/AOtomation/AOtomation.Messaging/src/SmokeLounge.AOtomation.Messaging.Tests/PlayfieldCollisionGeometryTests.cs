namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldCollisionGeometryTests
    {
        private const string ValidSourceSha256 =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestMethod]
        public void SegmentTriangleQueryIsTwoSidedAndReturnsNearestHit()
        {
            PlayfieldCollisionGeometry geometry = Geometry(
                Triangle(20, 1.0),
                Triangle(10, 0.0));
            SegmentTriangleHit forward;
            SegmentTriangleHit reverse;

            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-2.0, 0.0, 0.0),
                    new CollisionPoint3(2.0, 0.0, 0.0),
                    out forward));
            Assert.AreEqual(10, forward.TriangleId);
            Assert.AreEqual(0.5, forward.SegmentFraction, 0.000001);
            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(2.0, 0.0, 0.0),
                    new CollisionPoint3(-2.0, 0.0, 0.0),
                    out reverse));
            Assert.AreEqual(20, reverse.TriangleId);
            Assert.AreEqual(0.25, reverse.SegmentFraction, 0.000001);
        }

        [TestMethod]
        public void QueryUsesFullThreeDimensionalSurfaceAndIgnoresEndpointOnlyContact()
        {
            PlayfieldCollisionGeometry geometry = Geometry(Triangle(1, 0.0));
            SegmentTriangleHit hit;

            Assert.IsFalse(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-2.0, 3.0, 0.0),
                    new CollisionPoint3(2.0, 3.0, 0.0),
                    out hit));
            Assert.IsFalse(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-2.0, 0.0, 0.0),
                    new CollisionPoint3(0.0, 0.0, 0.0),
                    out hit));
        }

        [TestMethod]
        public void CoplanarCrossingAndSharedEdgeHitsAreBlocking()
        {
            var triangle = new CollisionTriangle(
                7,
                new CollisionPoint3(0.0, -1.0, -1.0),
                new CollisionPoint3(0.0, 1.0, -1.0),
                new CollisionPoint3(0.0, 0.0, 1.0));
            PlayfieldCollisionGeometry geometry = Geometry(triangle);
            SegmentTriangleHit hit;

            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(0.0, -2.0, 0.0),
                    new CollisionPoint3(0.0, 2.0, 0.0),
                    out hit));
            Assert.AreEqual(7, hit.TriangleId);
            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-1.0, 1.0, -1.0),
                    new CollisionPoint3(1.0, 1.0, -1.0),
                    out hit));
        }

        [TestMethod]
        public void GeometryOwnsADeepTriangleArrayCopy()
        {
            var source = new[] { Triangle(1, 0.0) };
            PlayfieldCollisionGeometry geometry = Geometry(source);
            source[0] = Triangle(2, 10.0);
            SegmentTriangleHit hit;

            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out hit));
            Assert.AreEqual(1, hit.TriangleId);
        }

        [TestMethod]
        public void BvhMatchesBruteForceAcrossDeterministicRandomizedSegments()
        {
            var random = new Random(127203748);
            var source = new CollisionTriangle[1024];
            for (int index = 0; index < source.Length; index++)
            {
                source[index] = RandomTriangle(random, index);
            }

            CollisionTriangle[] shuffled = (CollisionTriangle[])source.Clone();
            Shuffle(random, shuffled);
            PlayfieldCollisionGeometry orderedGeometry = Geometry(source);
            PlayfieldCollisionGeometry shuffledGeometry = Geometry(shuffled);
            var segments = new List<SegmentEndpoints>();
            for (int index = 0; index < 64; index++)
            {
                CollisionTriangle triangle = source[index];
                CollisionPoint3 center = TriangleCenter(triangle);
                segments.Add(SegmentThroughTriangle(center, index % 3));
            }

            for (int index = 0; index < 256; index++)
            {
                segments.Add(
                    new SegmentEndpoints(
                        RandomPoint(random, -130.0, 130.0),
                        RandomPoint(random, -130.0, 130.0)));
            }

            foreach (SegmentEndpoints segment in segments)
            {
                SegmentTriangleHit expected;
                SegmentTriangleHit actual;
                SegmentTriangleHit shuffledActual;
                bool expectedFound = orderedGeometry.TryFindFirstBlockingHitBruteForce(
                    segment.Start,
                    segment.End,
                    out expected);
                bool actualFound = orderedGeometry.TryFindFirstBlockingHit(
                    segment.Start,
                    segment.End,
                    out actual);
                bool shuffledFound = shuffledGeometry.TryFindFirstBlockingHit(
                    segment.Start,
                    segment.End,
                    out shuffledActual);

                Assert.AreEqual(expectedFound, actualFound, "BVH/brute-force hit result");
                Assert.AreEqual(expectedFound, shuffledFound, "input-order hit result");
                if (!expectedFound)
                {
                    continue;
                }

                AssertSameHit(expected, actual, "BVH/brute-force nearest hit");
                AssertSameHit(expected, shuffledActual, "input-order nearest hit");
            }
        }

        [TestMethod]
        public void BvhUsesTriangleIdToResolveEqualFractionIndependentOfInputOrder()
        {
            CollisionTriangle lowerId = Triangle(4, 0.0);
            CollisionTriangle higherId = Triangle(99, 0.0);
            PlayfieldCollisionGeometry first = Geometry(higherId, lowerId);
            PlayfieldCollisionGeometry second = Geometry(lowerId, higherId);
            SegmentTriangleHit firstHit;
            SegmentTriangleHit secondHit;

            Assert.IsTrue(
                first.TryFindFirstBlockingHit(
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out firstHit));
            Assert.IsTrue(
                second.TryFindFirstBlockingHit(
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out secondHit));
            Assert.AreEqual(4, firstHit.TriangleId);
            Assert.AreEqual(4, secondHit.TriangleId);
            AssertSameHit(firstHit, secondHit, "equal-fraction deterministic hit");
        }

        [TestMethod]
        public void BvhLargeMeshWorkGuardPrunesTriangleCandidates()
        {
            const int side = 256;
            var triangles = new CollisionTriangle[side * side];
            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    int id = (x * side) + z;
                    double planeX = x * 4.0;
                    double centerZ = z * 4.0;
                    triangles[id] = new CollisionTriangle(
                        id,
                        new CollisionPoint3(planeX, -1.0, centerZ - 1.0),
                        new CollisionPoint3(planeX, 1.0, centerZ - 1.0),
                        new CollisionPoint3(planeX, 0.0, centerZ + 1.0));
                }
            }

            PlayfieldCollisionGeometry geometry = Geometry(triangles);
            var start = new CollisionPoint3(-2.0, 0.0, 0.0);
            var end = new CollisionPoint3((side * 4.0) - 2.0, 0.0, 0.0);
            SegmentTriangleHit expected;
            SegmentTriangleHit actual;
            int examinedTriangleCount;

            Assert.IsTrue(geometry.TryFindFirstBlockingHitBruteForce(start, end, out expected));
            Assert.IsTrue(
                geometry.TryFindFirstBlockingHit(
                    start,
                    end,
                    out actual,
                    out examinedTriangleCount));
            AssertSameHit(expected, actual, "large-mesh nearest hit");
            Assert.IsTrue(
                examinedTriangleCount < triangles.Length / 8,
                "BVH examined "
                + examinedTriangleCount
                + " of "
                + triangles.Length
                + " triangles.");
        }

        [TestMethod]
        public void LoaderAcceptsDeterministicFlattenedSchemaAndExtraCaptureMetadata()
        {
            string json =
                "{\"schemaVersion\":1,\"playfieldResource\":127,"
                + "\"source\":\"capture\",\"sourceSha256\":\""
                + ValidSourceSha256.ToUpperInvariant()
                + "\","
                + "\"damageLineOfSightProbeHeight\":0,"
                + "\"damageLineOfSightProbeHeightEvidence\":\"synthetic-test-evidence\","
                + "\"coordinateMetadata\":{\"axis\":\"xyz\"},"
                + "\"rooms\":[{\"id\":1}],\"doors\":[],"
                + "\"triangles\":[{\"id\":4,"
                + "\"a\":{\"x\":0,\"y\":-1,\"z\":-1},"
                + "\"b\":{\"x\":0,\"y\":1,\"z\":-1},"
                + "\"c\":{\"x\":0,\"y\":0,\"z\":1}}]}";

            PlayfieldCollisionGeometryLoadResult result =
                Pf127CollisionGeometryLoader.LoadJson(json);

            Assert.IsTrue(result.IsLoaded, result.Error);
            Assert.AreEqual(127, result.Geometry.PlayfieldResource);
            Assert.AreEqual(1, result.Geometry.TriangleCount);
            Assert.AreEqual("capture", result.Geometry.Source);
            Assert.AreEqual(ValidSourceSha256.ToUpperInvariant(), result.Geometry.SourceSha256);
            Assert.AreEqual(0.0, result.Geometry.DamageLineOfSightProbeHeight, 0.000001);
            Assert.AreEqual(
                "synthetic-test-evidence",
                result.Geometry.DamageLineOfSightProbeHeightEvidence);
        }

        [TestMethod]
        public void LoaderRejectsMissingAndMalformedSourceSha256()
        {
            string sourceSha256 = "\"sourceSha256\":\"" + ValidSourceSha256 + "\",";
            PlayfieldCollisionGeometryLoadResult missing =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace(sourceSha256, string.Empty));
            PlayfieldCollisionGeometryLoadResult shortHash =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace(ValidSourceSha256, "0123456789abcdef"));
            PlayfieldCollisionGeometryLoadResult nonHexHash =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace(
                        ValidSourceSha256,
                        ValidSourceSha256.Substring(0, 63) + "G"));

            Assert.IsFalse(missing.IsLoaded);
            Assert.IsFalse(shortHash.IsLoaded);
            Assert.IsFalse(nonHexHash.IsLoaded);
            StringAssert.Contains(missing.Error, "sourceSha256");
            StringAssert.Contains(shortHash.Error, "64 hexadecimal characters");
            StringAssert.Contains(nonHexHash.Error, "64 hexadecimal characters");
        }

        [TestMethod]
        public void LoaderRejectsMissingOrInvalidRequiredProbeProfile()
        {
            string profile =
                "\"damageLineOfSightProbeHeight\":0.5,"
                + "\"damageLineOfSightProbeHeightEvidence\":\"synthetic-test-evidence\",";
            PlayfieldCollisionGeometryLoadResult missing =
                Pf127CollisionGeometryLoader.LoadJson(ValidJson().Replace(profile, string.Empty));
            PlayfieldCollisionGeometryLoadResult invalidHeight =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace(
                        "\"damageLineOfSightProbeHeight\":0.5",
                        "\"damageLineOfSightProbeHeight\":-0.1"));
            PlayfieldCollisionGeometryLoadResult excessiveHeight =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace(
                        "\"damageLineOfSightProbeHeight\":0.5",
                        "\"damageLineOfSightProbeHeight\":10.1"));
            PlayfieldCollisionGeometryLoadResult missingEvidence =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace("synthetic-test-evidence", string.Empty));

            Assert.IsFalse(missing.IsLoaded);
            Assert.IsFalse(invalidHeight.IsLoaded);
            Assert.IsFalse(excessiveHeight.IsLoaded);
            Assert.IsFalse(missingEvidence.IsLoaded);
            StringAssert.Contains(missing.Error, "damageLineOfSightProbeHeight");
            StringAssert.Contains(invalidHeight.Error, "between 0 and 10");
            StringAssert.Contains(excessiveHeight.Error, "between 0 and 10");
            StringAssert.Contains(missingEvidence.Error, "Evidence is required");
        }

        [TestMethod]
        public void LoaderRejectsWrongPlayfieldMissingCoordinatesAndDuplicateTriangles()
        {
            PlayfieldCollisionGeometryLoadResult wrongPlayfield =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace("\"playfieldResource\":127", "\"playfieldResource\":128"));
            PlayfieldCollisionGeometryLoadResult missingCoordinate =
                Pf127CollisionGeometryLoader.LoadJson(
                    ValidJson().Replace("\"x\":0,\"y\":-1,\"z\":-1", "\"x\":0,\"y\":-1"));
            string triangle =
                "{\"id\":1,\"a\":{\"x\":0,\"y\":-1,\"z\":-1},"
                + "\"b\":{\"x\":0,\"y\":1,\"z\":-1},"
                + "\"c\":{\"x\":0,\"y\":0,\"z\":1}}";
            PlayfieldCollisionGeometryLoadResult duplicate =
                Pf127CollisionGeometryLoader.LoadJson(
                    "{\"schemaVersion\":1,\"playfieldResource\":127,"
                    + "\"sourceSha256\":\"" + ValidSourceSha256 + "\","
                    + "\"damageLineOfSightProbeHeight\":0,"
                    + "\"damageLineOfSightProbeHeightEvidence\":\"synthetic-test-evidence\","
                    + "\"triangles\":["
                    + triangle
                    + ","
                    + triangle
                    + "]}");

            Assert.IsFalse(wrongPlayfield.IsLoaded);
            Assert.IsFalse(missingCoordinate.IsLoaded);
            Assert.IsFalse(duplicate.IsLoaded);
            StringAssert.Contains(duplicate.Error, "unique");
            ExpectException<ArgumentOutOfRangeException>(
                () => new CollisionTriangle(
                    1,
                    new CollisionPoint3(double.NaN, 0.0, 0.0),
                    new CollisionPoint3(0.0, 1.0, 0.0),
                    new CollisionPoint3(0.0, 0.0, 1.0)));
        }

        [TestMethod]
        public void ActivatedPolicyFailsClosedWhenGeometryIsMissing()
        {
            var unavailable = new NpcDamageLineOfSightRuntimeService(
                127,
                PlayfieldCollisionGeometryLoadResult.Failed("missing"));
            SegmentTriangleHit hit;
            bool inactiveRequirement =
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    false,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    null);
            bool activeRequirement =
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    null);

            Assert.IsTrue(NpcDamageLineOfSightRuntimeService.Pf127DamageLineOfSightActivated);
            Assert.IsFalse(inactiveRequirement);
            Assert.IsTrue(activeRequirement);
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.AllowedNotRequired,
                unavailable.Evaluate(
                    inactiveRequirement,
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out hit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedGeometryUnavailable,
                unavailable.Evaluate(
                    activeRequirement,
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out hit));
        }

        [TestMethod]
        public void ReviewedPf127AssetLoadsAndReplaysCapturedVergilClearAndBlockedSegments()
        {
            string root = FindRepositoryRoot();
            string assetPath = Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Content\Captured\Subway\pf127-geometry.json");
            string assetSha256;
            using (FileStream stream = File.OpenRead(assetPath))
            using (SHA256 sha256 = SHA256.Create())
            {
                assetSha256 = BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }

            Assert.AreEqual(
                "45e095a790ca5a3230c1d2fd84116d4e35af35e939d7ed82701133b368816088",
                assetSha256);
            PlayfieldCollisionGeometryLoadResult loaded =
                Pf127CollisionGeometryLoader.LoadPath(assetPath);

            Assert.IsTrue(loaded.IsLoaded, loaded.Error);
            Assert.AreEqual(198132, loaded.Geometry.TriangleCount);
            Assert.AreEqual(
                "6475b3bb25fc67db419c372f46807f682d02416ebaa43274a434a5525cbe62e5",
                loaded.Geometry.SourceSha256);
            Assert.AreEqual(0.0, loaded.Geometry.DamageLineOfSightProbeHeight);
            StringAssert.Contains(
                loaded.Geometry.DamageLineOfSightProbeHeightEvidence,
                "capture=20260714-202820");
            StringAssert.Contains(
                loaded.Geometry.DamageLineOfSightProbeHeightEvidence,
                "variant=raw;pairs=148;clear=41;blocked=107;nativeRejected=7;capturedPairs=155");
            StringAssert.Contains(
                loaded.Geometry.DamageLineOfSightProbeHeightEvidence,
                "selectionRule=native-consensus-geometry-zero-max-support-v1;supportMargin=11;rawAccepted=148;rawRejected=7;rawClear=41;rawBlocked=107;rawDistinctRays=87;rawCombat=0;rawPeriodic=148;rawGeometryDisagreements=0;plusOneAccepted=137;plusOneRejected=18;plusOneClear=48;plusOneBlocked=89;plusOneDistinctRays=78;plusOneCombat=0;plusOnePeriodic=137;plusOneGeometryDisagreements=0");

            var service = new NpcDamageLineOfSightRuntimeService(127, loaded);
            SegmentTriangleHit hit;
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.AllowedClear,
                service.Evaluate(
                    true,
                    new CollisionPoint3(261.303375, 73.01795, 98.3369446),
                    new CollisionPoint3(278.045074, 73.01795, 98.80104),
                    out hit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedBlocked,
                service.Evaluate(
                    true,
                    new CollisionPoint3(188.2448, 73.01637, 98.84238),
                    new CollisionPoint3(278.045074, 73.01795, 98.80104),
                    out hit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.AllowedClear,
                service.EvaluateAttackLine(
                    true,
                    new CollisionPoint3(278.045074, 73.01795, 98.80104),
                    new CollisionPoint3(246.9, 73.0, 95.5),
                    out hit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedBlocked,
                service.EvaluateAttackLine(
                    true,
                    new CollisionPoint3(121.809868, 73.01637, 98.90472),
                    new CollisionPoint3(187.0416, 73.3830261, 88.03114),
                    out hit));
        }

        [TestMethod]
        public void ActivatedSafetyPolicyCoversVergilAndExplicitContractOptInsOnly()
        {
            Assert.IsTrue(
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    null));
            Assert.IsTrue(
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    false));
            Assert.IsFalse(
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    31909,
                    false));
            Assert.IsTrue(
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    31909,
                    true));
            Assert.IsFalse(
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    false,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    true));
        }

        [TestMethod]
        public void RequiredPolicyDistinguishesBlockedClearAndInvalidSegments()
        {
            var service = new NpcDamageLineOfSightRuntimeService(
                127,
                PlayfieldCollisionGeometryLoadResult.Loaded(Geometry(Triangle(1, 0.0))));
            SegmentTriangleHit hit;
            bool activeRequirement =
                NpcDamageLineOfSightRuntimeService.IsDamageLineOfSightRequired(
                    true,
                    NpcDamageLineOfSightRuntimeService.VergilAeneidMonsterData,
                    null);

            Assert.IsTrue(activeRequirement);
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedBlocked,
                service.Evaluate(
                    activeRequirement,
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out hit));
            Assert.AreEqual(1, hit.TriangleId);
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.AllowedClear,
                service.Evaluate(
                    activeRequirement,
                    new CollisionPoint3(-1.0, 3.0, 0.0),
                    new CollisionPoint3(1.0, 3.0, 0.0),
                    out hit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedInvalidSegment,
                service.Evaluate(
                    activeRequirement,
                    new CollisionPoint3(1.0, 1.0, 1.0),
                    new CollisionPoint3(1.0, 1.0, 1.0),
                    out hit));
        }

        [TestMethod]
        public void RequiredPolicyAppliesConfiguredProbeHeightToRawEndpoints()
        {
            var elevatedWall = new CollisionTriangle(
                9,
                new CollisionPoint3(0.0, 1.5, -1.0),
                new CollisionPoint3(0.0, 2.5, -1.0),
                new CollisionPoint3(0.0, 2.0, 1.0));
            var geometry = new PlayfieldCollisionGeometry(
                1,
                127,
                "test",
                string.Empty,
                2.0,
                "synthetic-height-analysis",
                new[] { elevatedWall });
            var service = new NpcDamageLineOfSightRuntimeService(
                127,
                PlayfieldCollisionGeometryLoadResult.Loaded(geometry));
            SegmentTriangleHit directHit;
            SegmentTriangleHit policyHit;

            Assert.IsFalse(
                geometry.TryFindFirstBlockingHit(
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out directHit));
            Assert.AreEqual(
                NpcDamageLineOfSightDecision.DeniedBlocked,
                service.Evaluate(
                    true,
                    new CollisionPoint3(-1.0, 0.0, 0.0),
                    new CollisionPoint3(1.0, 0.0, 0.0),
                    out policyHit));
            Assert.AreEqual(9, policyHit.TriangleId);
        }

        [TestMethod]
        public void CombatWiringGatesNormalAndParallelDamageWithoutClearingAggro()
        {
            string root = FindRepositoryRoot();
            string coordinator = ReadPlayfieldSource(root, "NpcCombatTickCoordinator.cs");
            string contracts = ReadPlayfieldSource(root, "CapturedEnemyCombatContract.cs");
            string lineOfSight = ReadPlayfieldSource(root, "NpcDamageLineOfSightRuntimeService.cs");
            string normal = ExtractMethodBlock(coordinator, "internal void ProcessCombatTick");
            string parallel = ExtractMethodBlock(
                coordinator,
                "private void ProcessCapturedParallelAttackTicks");
            string gate = ExtractMethodBlock(coordinator, "private bool CanApplyNpcDamage");
            int normalDamage = normal.IndexOf(
                "int currentHealth = target.Stats[StatIds.health].Value;",
                StringComparison.Ordinal);
            int parallelDamage = parallel.IndexOf(
                "int currentHealth = target.Stats[StatIds.health].Value;",
                StringComparison.Ordinal);
            int fallbackAttackSource = normal.IndexOf(
                "CombatAttackSource attackSource = this.GetCombatAttackSource(attacker);",
                StringComparison.Ordinal);
            int normalDamageGate = normal.LastIndexOf(
                "this.CanApplyNpcDamage(",
                normalDamage,
                StringComparison.Ordinal);
            int vergilStart = contracts.IndexOf("case 203748:", StringComparison.Ordinal);
            int abmouthStart = contracts.IndexOf("case 155962:", StringComparison.Ordinal);
            int infectorStart = contracts.IndexOf("case 31909:", StringComparison.Ordinal);
            string vergilContract = contracts.Substring(vergilStart, abmouthStart - vergilStart);
            string abmouthContract = contracts.Substring(abmouthStart, infectorStart - abmouthStart);

            Assert.IsTrue(vergilContract.Contains("requiresDamageLineOfSight: true"));
            Assert.IsFalse(abmouthContract.Contains("requiresDamageLineOfSight: true"));
            Assert.IsFalse(coordinator.Contains("DamageLineOfSightHeightOffset"));
            Assert.IsTrue(
                gate.Contains("attacker.RawCoordinates.Y,")
                && gate.Contains("target.RawCoordinates.Y,"));
            Assert.IsTrue(gate.Contains("IsDamageLineOfSightRequired"));
            Assert.IsTrue(gate.Contains("Pf127DamageLineOfSightActivated"));
            Assert.IsTrue(gate.Contains("attacker.Stats[StatIds.monsterdata].Value"));
            Assert.IsTrue(gate.Contains("EvaluateAttackLine"));
            Assert.IsFalse(gate.Contains("hasCapturedContract"));
            Assert.IsTrue(
                lineOfSight.Contains("const bool Pf127DamageLineOfSightActivated = true"));
            string project = File.ReadAllText(
                Path.Combine(root, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
            StringAssert.Contains(
                project,
                @"<Content Include=""Content\Captured\Subway\pf127-geometry.json"">");
            Assert.IsTrue(
                lineOfSight.Contains("Geometry.DamageLineOfSightProbeHeight")
                && lineOfSight.Contains("Pf127ChaseNavigationProvider.AttackLineProbeHeight")
                && lineOfSight.Contains("start.Y + probeHeight")
                && lineOfSight.Contains("end.Y + probeHeight"));
            Assert.IsTrue(normalDamage > 0);
            Assert.IsTrue(fallbackAttackSource >= 0);
            Assert.IsTrue(normalDamageGate > fallbackAttackSource);
            Assert.IsTrue(parallelDamage > 0);
            Assert.IsTrue(
                parallel.LastIndexOf("this.CanApplyNpcDamage(", parallelDamage, StringComparison.Ordinal) >= 0);
            Assert.IsTrue(gate.Contains("NpcCombatAttackRules.OutOfRangeRetrySeconds"));
            Assert.IsFalse(gate.Contains("ClearInvalidNpcCombatTarget"));
            Assert.IsFalse(gate.Contains("ClearFightingTarget"));
            Assert.IsFalse(gate.Contains("StopFight"));
            Assert.IsFalse(gate.Contains("StopFollow"));
        }

        private static PlayfieldCollisionGeometry Geometry(params CollisionTriangle[] triangles)
        {
            return new PlayfieldCollisionGeometry(
                1,
                127,
                "test",
                string.Empty,
                0.5,
                "synthetic-test-evidence",
                triangles);
        }

        private static CollisionTriangle Triangle(int id, double x)
        {
            return new CollisionTriangle(
                id,
                new CollisionPoint3(x, -1.0, -1.0),
                new CollisionPoint3(x, 1.0, -1.0),
                new CollisionPoint3(x, 0.0, 1.0));
        }

        private static CollisionTriangle RandomTriangle(Random random, int id)
        {
            double x = RandomCoordinate(random, -100.0, 100.0);
            double y = RandomCoordinate(random, -100.0, 100.0);
            double z = RandomCoordinate(random, -100.0, 100.0);
            double firstExtent = RandomCoordinate(random, 0.5, 4.0);
            double secondExtent = RandomCoordinate(random, 0.5, 4.0);
            switch (id % 3)
            {
                case 0:
                    return new CollisionTriangle(
                        id,
                        new CollisionPoint3(x, y - firstExtent, z - secondExtent),
                        new CollisionPoint3(x, y + firstExtent, z - secondExtent),
                        new CollisionPoint3(x, y, z + secondExtent));
                case 1:
                    return new CollisionTriangle(
                        id,
                        new CollisionPoint3(x - firstExtent, y, z - secondExtent),
                        new CollisionPoint3(x + firstExtent, y, z - secondExtent),
                        new CollisionPoint3(x, y, z + secondExtent));
                default:
                    return new CollisionTriangle(
                        id,
                        new CollisionPoint3(x - firstExtent, y - secondExtent, z),
                        new CollisionPoint3(x + firstExtent, y - secondExtent, z),
                        new CollisionPoint3(x, y + secondExtent, z));
            }
        }

        private static CollisionPoint3 TriangleCenter(CollisionTriangle triangle)
        {
            return new CollisionPoint3(
                (triangle.A.X + triangle.B.X + triangle.C.X) / 3.0,
                (triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3.0,
                (triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3.0);
        }

        private static SegmentEndpoints SegmentThroughTriangle(CollisionPoint3 center, int axis)
        {
            if (axis == 0)
            {
                return new SegmentEndpoints(
                    new CollisionPoint3(center.X - 5.0, center.Y, center.Z),
                    new CollisionPoint3(center.X + 5.0, center.Y, center.Z));
            }

            return axis == 1
                       ? new SegmentEndpoints(
                           new CollisionPoint3(center.X, center.Y - 5.0, center.Z),
                           new CollisionPoint3(center.X, center.Y + 5.0, center.Z))
                       : new SegmentEndpoints(
                           new CollisionPoint3(center.X, center.Y, center.Z - 5.0),
                           new CollisionPoint3(center.X, center.Y, center.Z + 5.0));
        }

        private static CollisionPoint3 RandomPoint(Random random, double minimum, double maximum)
        {
            return new CollisionPoint3(
                RandomCoordinate(random, minimum, maximum),
                RandomCoordinate(random, minimum, maximum),
                RandomCoordinate(random, minimum, maximum));
        }

        private static double RandomCoordinate(Random random, double minimum, double maximum)
        {
            return minimum + (random.NextDouble() * (maximum - minimum));
        }

        private static void Shuffle(Random random, CollisionTriangle[] triangles)
        {
            for (int index = triangles.Length - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                CollisionTriangle temporary = triangles[index];
                triangles[index] = triangles[other];
                triangles[other] = temporary;
            }
        }

        private static void AssertSameHit(
            SegmentTriangleHit expected,
            SegmentTriangleHit actual,
            string context)
        {
            Assert.AreEqual(expected.TriangleId, actual.TriangleId, context + " triangle id");
            Assert.AreEqual(
                expected.SegmentFraction,
                actual.SegmentFraction,
                0.000000000001,
                context + " segment fraction");
            Assert.AreEqual(expected.Point.X, actual.Point.X, 0.000000000001, context + " point X");
            Assert.AreEqual(expected.Point.Y, actual.Point.Y, 0.000000000001, context + " point Y");
            Assert.AreEqual(expected.Point.Z, actual.Point.Z, 0.000000000001, context + " point Z");
        }

        private static string ValidJson()
        {
            return "{\"schemaVersion\":1,\"playfieldResource\":127,"
                   + "\"sourceSha256\":\"" + ValidSourceSha256 + "\","
                   + "\"damageLineOfSightProbeHeight\":0.5,"
                   + "\"damageLineOfSightProbeHeightEvidence\":\"synthetic-test-evidence\","
                   + "\"triangles\":[{"
                   + "\"id\":1,\"a\":{\"x\":0,\"y\":-1,\"z\":-1},"
                   + "\"b\":{\"x\":0,\"y\":1,\"z\":-1},"
                   + "\"c\":{\"x\":0,\"y\":0,\"z\":1}}]}";
        }

        private static void ExpectException<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected " + typeof(TException).Name + ".");
            }
            catch (TException)
            {
            }
        }

        private static string ReadPlayfieldSource(string root, string file)
        {
            return File.ReadAllText(
                    Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields", file))
                .Replace("\r\n", "\n");
        }

        private static string ExtractMethodBlock(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, "Method signature not found: " + signature);
            int openingBrace = source.IndexOf('{', signatureIndex);
            Assert.IsTrue(openingBrace >= 0, "Method opening brace not found: " + signature);
            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{')
                {
                    depth++;
                }
                else if (source[index] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(signatureIndex, index - signatureIndex + 1);
                    }
                }
            }

            Assert.Fail("Method closing brace not found: " + signature);
            return string.Empty;
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                string gitMarker = Path.Combine(current, ".git");
                if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            throw new InvalidOperationException("Repository root not found.");
        }

        private struct SegmentEndpoints
        {
            internal SegmentEndpoints(CollisionPoint3 start, CollisionPoint3 end)
            {
                this.Start = start;
                this.End = end;
            }

            internal CollisionPoint3 Start { get; private set; }

            internal CollisionPoint3 End { get; private set; }
        }
    }
}
