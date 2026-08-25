namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields.Hydration;

    [TestClass]
    public class PlayfieldHydrationSeamTests
    {
        [TestMethod]
        public void LegacyModeIsTheExplicitDefaultProductionRoute()
        {
            var materializer = new CountingMaterializer();
            var coordinator = new PlayfieldInstantiationCoordinator(PlayfieldHydrationMode.Legacy, materializer);

            Assert.AreEqual(PlayfieldHydrationMode.Legacy, coordinator.Mode);
            Assert.IsNull(coordinator.Materialize(127));
            Assert.AreEqual(1, materializer.CallCount);
            Assert.AreEqual(127, materializer.LastPlayfieldInstance);
        }

        [TestMethod]
        public void UnsupportedModesFailClosedBeforeAnyProviderOrMaterializerRuns()
        {
            var materializer = new CountingMaterializer();
            var coordinator = new PlayfieldInstantiationCoordinator(PlayfieldHydrationMode.Shadow, materializer);

            try
            {
                coordinator.Materialize(127);
                Assert.Fail("Shadow mode must remain disabled during Stage 1.");
            }
            catch (NotSupportedException)
            {
            }

            Assert.AreEqual(0, materializer.CallCount);
        }

        [TestMethod]
        public void LegacyImplementationIsInvokedExactlyOncePerCoordinatorCall()
        {
            int legacyFactoryCalls = 0;
            var materializer =
                new LegacyPlayfieldRuntimeMaterializer(
                    playfieldInstance =>
                    {
                        legacyFactoryCalls++;
                        Assert.AreEqual(1931, playfieldInstance);
                        return null;
                    });
            var coordinator = new PlayfieldInstantiationCoordinator(PlayfieldHydrationMode.Legacy, materializer);

            Assert.IsNull(coordinator.Materialize(1931));
            Assert.AreEqual(1, legacyFactoryCalls);
        }

        [TestMethod]
        public void DefinitionHydrationDoesNotRegisterSpawnsServicesOrRuntimeObjects()
        {
            int spawnRegistrations = 0;
            int serviceRegistrations = 0;
            IPlayfieldDefinitionHydrator hydrator = new PureDefinitionHydrator();

            PlayfieldHydrationResult result = hydrator.Hydrate(new PlayfieldHydrationRequest(127, 127));

            Assert.IsNotNull(result.Definition);
            Assert.AreEqual(0, spawnRegistrations);
            Assert.AreEqual(0, serviceRegistrations);
            Assert.IsFalse(
                typeof(HydratedPlayfieldDefinition).GetProperties(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public)
                    .Any(property => typeof(IPlayfield).IsAssignableFrom(property.PropertyType)));
        }

        [TestMethod]
        public void CanonicalOutputAndDigestAreStableAcrossInputOrdering()
        {
            HydratedPlayfieldDefinition left = CreateDefinition(false);
            HydratedPlayfieldDefinition right = CreateDefinition(true);

            Assert.AreEqual(
                PlayfieldDefinitionCanonicalizer.Serialize(left),
                PlayfieldDefinitionCanonicalizer.Serialize(right));
            Assert.AreEqual(
                PlayfieldDefinitionCanonicalizer.ComputeDigest(left),
                PlayfieldDefinitionCanonicalizer.ComputeDigest(right));
            Assert.AreEqual(64, PlayfieldDefinitionCanonicalizer.ComputeDigest(left).Length);
        }

        [TestMethod]
        public void EveryDefinitionPropertyHasAnExplicitCanonicalClassification()
        {
            string[] definitionProperties =
                typeof(HydratedPlayfieldDefinition).GetProperties(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Public)
                    .Select(property => property.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            string[] included = PlayfieldDefinitionCanonicalizer.CanonicalDefinitionPropertyNames
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] excluded = PlayfieldDefinitionCanonicalizer.ExplicitlyExcludedDefinitionPropertyNames
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(0, included.Intersect(excluded, StringComparer.Ordinal).Count());
            CollectionAssert.AreEquivalent(
                definitionProperties,
                included.Concat(excluded).ToArray(),
                "A definition property was added without an explicit canonical classification.");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "FormatVersion",
                    "PlayfieldInstance",
                    "ResourceIdentity",
                    "Name",
                    "Records",
                    "Provenance",
                    "Warnings",
                    "Conflicts"
                },
                included);
            Assert.AreEqual(0, excluded.Length, "No current definition property is silently excluded.");
        }

        [TestMethod]
        public void CanonicalAndDefinitionFormatVersionsHaveAnExplicitLockedMapping()
        {
            Assert.AreEqual(
                HydratedPlayfieldDefinition.CurrentFormatVersion,
                PlayfieldDefinitionCanonicalizer.SupportedDefinitionFormatVersion);
            Assert.AreEqual(1, PlayfieldDefinitionCanonicalizer.CurrentCanonicalFormatVersion);

            string canonical = PlayfieldDefinitionCanonicalizer.Serialize(CreateDefinition(false));
            StringAssert.Contains(canonical, "\"canonicalFormatVersion\":1");
            StringAssert.Contains(canonical, "\"definitionFormatVersion\":1");
        }

        [TestMethod]
        public void RepeatedCanonicalizationProducesIdenticalUtf8BytesAndDigest()
        {
            HydratedPlayfieldDefinition definition = CreateDefinition(false);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(
                PlayfieldDefinitionCanonicalizer.Serialize(definition));
            string expectedDigest = PlayfieldDefinitionCanonicalizer.ComputeDigest(definition);

            for (int iteration = 0; iteration < 5; iteration++)
            {
                CollectionAssert.AreEqual(
                    expectedBytes,
                    Encoding.UTF8.GetBytes(PlayfieldDefinitionCanonicalizer.Serialize(definition)));
                Assert.AreEqual(
                    expectedDigest,
                    PlayfieldDefinitionCanonicalizer.ComputeDigest(definition));
            }
        }

        [TestMethod]
        public void MeaningfulStaticDefinitionChangeChangesCanonicalBytesAndDigest()
        {
            HydratedPlayfieldDefinition original = CreateDefinition(false);
            HydratedPlayfieldDefinition changed = CreateDefinition(false);
            changed.Records[0].Values[0] = HydratedPlayfieldValue.Scalar("level", "9");

            Assert.AreNotEqual(
                PlayfieldDefinitionCanonicalizer.Serialize(original),
                PlayfieldDefinitionCanonicalizer.Serialize(changed));
            Assert.AreNotEqual(
                PlayfieldDefinitionCanonicalizer.ComputeDigest(original),
                PlayfieldDefinitionCanonicalizer.ComputeDigest(changed));
        }

        [TestMethod]
        public void FloatValuesUseExactInvariantRoundTripFormatting()
        {
            HydratedPlayfieldValue value = HydratedPlayfieldValue.Float("heading", 1.2345678f);

            Assert.AreEqual(1, value.Values.Count);
            Assert.AreEqual(
                1.2345678f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                value.Values[0]);
        }

        [TestMethod]
        public void RuntimeOnlyStateIsRejectedAndNeverSilentlySerialized()
        {
            HydratedPlayfieldDefinition definition = CreateDefinition(false);
            definition.Records[0].Values.Add(HydratedPlayfieldValue.Scalar("CurrentHp", "42"));
            definition.Records[0].Provenance.Add(
                Source(PlayfieldHydrationSourceKind.Runtime, "live-character", 99, PlayfieldProvenanceResolution.Accepted));
            IList<PlayfieldHydrationDiagnostic> diagnostics = new PlayfieldDefinitionValidator().Validate(definition);

            Assert.IsTrue(diagnostics.Any(value => value.Code == "RUNTIME_STATE_NOT_ALLOWED"));
            Assert.IsTrue(diagnostics.Any(value => value.Code == "RUNTIME_SOURCE_NOT_ALLOWED"));
            AssertCanonicalizationRejected(definition, "RUNTIME_STATE_NOT_ALLOWED");
        }

        [TestMethod]
        public void DuplicateRecordAndValueIdentitiesAreDiagnosed()
        {
            HydratedPlayfieldDefinition definition = CreateDefinition(false);
            definition.Records.Add(new HydratedPlayfieldRecord("spawn", "mob-1"));
            definition.Records[0].Values.Add(HydratedPlayfieldValue.Scalar("level", "9"));

            IList<PlayfieldHydrationDiagnostic> diagnostics = new PlayfieldDefinitionValidator().Validate(definition);

            Assert.IsTrue(diagnostics.Any(value => value.Code == "DUPLICATE_RECORD"));
            Assert.IsTrue(diagnostics.Any(value => value.Code == "DUPLICATE_VALUE"));
            AssertCanonicalizationRejected(definition, "DUPLICATE_RECORD");
        }

        [TestMethod]
        public void UnresolvedProvenanceRemainsExplicitAndProducesUnresolvedComparison()
        {
            HydratedPlayfieldDefinition expected = CreateDefinition(false);
            HydratedPlayfieldDefinition actual = CreateDefinition(false);
            actual.Provenance.Add(
                Source(PlayfieldHydrationSourceKind.GeneratedCapture, "NCNN", 80, PlayfieldProvenanceResolution.Unresolved));
            var validator = new PlayfieldDefinitionValidator();

            Assert.IsTrue(validator.Validate(actual).Any(value => value.Code == "UNRESOLVED_PROVENANCE"));
            Assert.IsTrue(
                new PlayfieldDefinitionComparer(validator).Compare(expected, actual)
                    .Any(value => value.Kind == PlayfieldDefinitionDifferenceKind.UnresolvedComparison));
        }

        [TestMethod]
        public void LegacySourcePrecedenceIsPinnedForStageOne()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "playfields.dat base metadata and statels",
                    "Playfields.xml playfield catalog metadata",
                    "database mob spawns with suppression policy",
                    "registered captured and hardcoded content modules",
                    "database and RDB-backed vendors",
                    "database static dynels",
                    "runtime dynel registry refresh"
                },
                LegacyPlayfieldSourcePrecedence.OrderedSources);
        }

        [TestMethod]
        public void ComparerClassifiesAllSupportedDifferenceKinds()
        {
            var validator = new PlayfieldDefinitionValidator();
            var comparer = new PlayfieldDefinitionComparer(validator);
            var kinds = new HashSet<PlayfieldDefinitionDifferenceKind>();

            HydratedPlayfieldDefinition missingExpected = CreateDefinition(false);
            HydratedPlayfieldDefinition missingActual = CreateDefinition(false);
            missingActual.Records.Clear();
            AddKinds(kinds, comparer.Compare(missingExpected, missingActual));
            AddKinds(kinds, comparer.Compare(missingActual, missingExpected));

            HydratedPlayfieldDefinition changed = CreateDefinition(false);
            changed.Records[0].Values[0] = HydratedPlayfieldValue.Scalar("level", "99");
            changed.Records[0].Values[1] = HydratedPlayfieldValue.Collection("tags", new[] { "boss" });
            changed.Provenance.Clear();
            changed.Provenance.Add(Source(PlayfieldHydrationSourceKind.Json, "replacement.json", 10, PlayfieldProvenanceResolution.Accepted));
            AddKinds(kinds, comparer.Compare(CreateDefinition(false), changed));

            HydratedPlayfieldDefinition duplicate = CreateDefinition(false);
            duplicate.Records.Add(new HydratedPlayfieldRecord("spawn", "mob-1"));
            AddKinds(kinds, comparer.Compare(CreateDefinition(false), duplicate));

            AddKinds(kinds, comparer.Compare(CreateDefinition(false), CreateDefinition(true)));

            HydratedPlayfieldDefinition unresolved = CreateDefinition(false);
            unresolved.Provenance.Add(Source(PlayfieldHydrationSourceKind.GeneratedCapture, "unknown", 20, PlayfieldProvenanceResolution.Unresolved));
            AddKinds(kinds, comparer.Compare(CreateDefinition(false), unresolved));

            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(PlayfieldDefinitionDifferenceKind)).Cast<PlayfieldDefinitionDifferenceKind>().ToArray(),
                kinds.ToArray());
        }

        private static HydratedPlayfieldDefinition CreateDefinition(bool reverseOrder)
        {
            var definition = new HydratedPlayfieldDefinition(127, 127, "Subway");
            definition.Provenance.Add(
                Source(PlayfieldHydrationSourceKind.ExtractedBinary, "playfields.dat", 0, PlayfieldProvenanceResolution.Accepted));
            definition.Warnings.Add("capture field not observed");

            var first = new HydratedPlayfieldRecord("spawn", "mob-1");
            first.Values.Add(HydratedPlayfieldValue.Scalar("level", "8"));
            first.Values.Add(HydratedPlayfieldValue.Collection("tags", new[] { "melee", "ordinary" }));
            first.Provenance.Add(
                Source(PlayfieldHydrationSourceKind.Database, "mobspawns:127:mob-1", 20, PlayfieldProvenanceResolution.Accepted));

            var second = new HydratedPlayfieldRecord("teleport", "exit-1");
            second.Values.Add(HydratedPlayfieldValue.Float("heading", 1.25f));
            second.Provenance.Add(
                Source(PlayfieldHydrationSourceKind.HardcodedCompatibility, "pf127-reverse-exit", 30, PlayfieldProvenanceResolution.Compatibility));

            if (reverseOrder)
            {
                first.Values.Reverse();
                definition.Records.Add(second);
                definition.Records.Add(first);
            }
            else
            {
                definition.Records.Add(first);
                definition.Records.Add(second);
            }

            return definition;
        }

        private static PlayfieldSourceProvenance Source(
            PlayfieldHydrationSourceKind kind,
            string identity,
            int order,
            PlayfieldProvenanceResolution resolution)
        {
            return new PlayfieldSourceProvenance(kind, identity, "digest", "test-adapter", order, resolution);
        }

        private static void AddKinds(
            ISet<PlayfieldDefinitionDifferenceKind> kinds,
            IEnumerable<PlayfieldDefinitionDifference> differences)
        {
            foreach (PlayfieldDefinitionDifference difference in differences)
            {
                kinds.Add(difference.Kind);
            }
        }

        private static void AssertCanonicalizationRejected(
            HydratedPlayfieldDefinition definition,
            string expectedDiagnosticCode)
        {
            try
            {
                PlayfieldDefinitionCanonicalizer.Serialize(definition);
                Assert.Fail("Invalid definition was canonicalized.");
            }
            catch (InvalidOperationException exception)
            {
                StringAssert.Contains(exception.Message, expectedDiagnosticCode);
            }
        }

        private sealed class CountingMaterializer : IPlayfieldRuntimeMaterializer
        {
            internal int CallCount { get; private set; }

            internal int LastPlayfieldInstance { get; private set; }

            public IPlayfield Materialize(PlayfieldRuntimeMaterializationRequest request)
            {
                this.CallCount++;
                this.LastPlayfieldInstance = request.PlayfieldInstance;
                return null;
            }
        }

        private sealed class PureDefinitionHydrator : IPlayfieldDefinitionHydrator
        {
            public PlayfieldHydrationResult Hydrate(PlayfieldHydrationRequest request)
            {
                return new PlayfieldHydrationResult(
                    new HydratedPlayfieldDefinition(
                        request.PlayfieldInstance,
                        request.ResourceIdentity,
                        "pure-definition"),
                    new PlayfieldHydrationDiagnostic[0]);
            }
        }
    }
}
