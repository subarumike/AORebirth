namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;

    using ZoneEngine.Core.Playfields;

    internal sealed class Pf127LineOfSightPromotionResult
    {
        internal string OutputPath { get; set; }

        internal string SourceSha256 { get; set; }

        internal string LineOfSightSha256 { get; set; }

        internal string DoorStateSha256 { get; set; }

        internal string OutputSha256 { get; set; }

        internal string Evidence { get; set; }

        internal string ProbeVariant { get; set; }

        internal double ProbeHeight { get; set; }

        internal int PairCount { get; set; }

        internal int ClearPairCount { get; set; }

        internal int BlockedPairCount { get; set; }

        internal int NativeDisagreementPairCount { get; set; }
    }

    internal static class Pf127LineOfSightPromotionValidator
    {
        private const int SubwayPlayfieldResource = 127;
        private const int VergilAeneidMonsterData = 203748;
        private const int DoorIdentityType = 51016;
        private const int MaximumTriangleCount = 2000000;
        private const double CoordinateTolerance = 0.00001;
        private const int NegativeDoorIdentityInstance = -1070464897;
        private const string SelectionRule = "native-consensus-geometry-zero-max-support-v1";

        private const string GeometryFileName = "pf127-geometry.json";
        private const string LineOfSightFileName = "pf127-line-of-sight.csv";
        private const string DoorStateFileName = "pf127-door-state.csv";
        private const string DefaultReviewedGeometryFileName = "pf127-geometry.reviewed.json";

        internal static Pf127LineOfSightPromotionResult Promote(
            string captureFolder,
            string outputPath)
        {
            string fullCaptureFolder = RequireCaptureFolder(captureFolder);
            string geometryPath = Path.Combine(fullCaptureFolder, GeometryFileName);
            string lineOfSightPath = Path.Combine(fullCaptureFolder, LineOfSightFileName);
            string doorStatePath = Path.Combine(fullCaptureFolder, DoorStateFileName);
            RequireFile(geometryPath);
            RequireFile(lineOfSightPath);
            RequireFile(doorStatePath);

            string fullOutputPath = string.IsNullOrWhiteSpace(outputPath)
                                        ? Path.Combine(fullCaptureFolder, DefaultReviewedGeometryFileName)
                                        : Path.GetFullPath(outputPath);
            if (string.Equals(
                Path.GetFullPath(geometryPath),
                fullOutputPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The reviewed runtime JSON must not overwrite the canonical capture geometry.");
            }

            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException(
                    "The reviewed runtime JSON output directory does not exist: " + outputDirectory);
            }

            byte[] geometryBytes = File.ReadAllBytes(geometryPath);
            byte[] lineOfSightBytes = File.ReadAllBytes(lineOfSightPath);
            byte[] doorStateBytes = File.ReadAllBytes(doorStatePath);
            string geometryJson = ReadUtf8Text(geometryBytes, geometryPath);
            string sourceSha256 = ComputeSha256(geometryBytes);
            string lineOfSightSha256 = ComputeSha256(lineOfSightBytes);
            string doorStateSha256 = ComputeSha256(doorStateBytes);

            Dictionary<string, CanonicalDoorEvidence> expectedDoors;
            PlayfieldCollisionGeometry geometry = LoadUnreviewedGeometry(
                geometryJson,
                sourceSha256,
                out expectedDoors);
            Dictionary<DoorBatchKey, DoorBatch> doorBatches = LoadDoorBatches(
                doorStatePath,
                expectedDoors);
            List<LineOfSightPair> pairs = LoadVergilPairs(lineOfSightPath, doorBatches);
            ValidateEvidenceCoverage(pairs);

            CandidateEvaluation raw = EvaluateCandidate(geometry, pairs, "raw", 0.0);
            CandidateEvaluation plusOne = EvaluateCandidate(
                geometry,
                pairs,
                "plus-one-y",
                1.0);
            CandidateEvaluation selected;
            if (!raw.IsValid && !plusOne.IsValid)
            {
                throw new InvalidDataException(
                    "PF127 LOS promotion has no valid probe height. raw="
                    + raw.FailureSummary
                    + "; plus-one-y="
                    + plusOne.FailureSummary);
            }
            else if (raw.IsValid && plusOne.IsValid)
            {
                if (raw.NativeAgreementPairs == plusOne.NativeAgreementPairs)
                {
                    throw new InvalidDataException(
                        "PF127 LOS promotion is ambiguous: raw and plus-one-y have equal native-agreement support and both match canonical geometry.");
                }

                selected = raw.NativeAgreementPairs > plusOne.NativeAgreementPairs
                               ? raw
                               : plusOne;
            }
            else
            {
                selected = raw.IsValid ? raw : plusOne;
            }

            string captureReference = Path.GetFileName(
                fullCaptureFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string targetReference = pairs
                .Select(pair => pair.TargetIdentityKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .First();
            int clearPairs = selected.NativeClearPairs;
            int blockedPairs = selected.NativeBlockedPairs;
            string evidence = string.Format(
                CultureInfo.InvariantCulture,
                "capture={0};geometrySha256={1};lineOfSightSha256={2};doorStateSha256={3};monsterData={4};target={5};variant={6};pairs={7};clear={8};blocked={9};nativeRejected={10};capturedPairs={11};selectionRule={12};supportMargin={13};rawAccepted={14};rawRejected={15};rawClear={16};rawBlocked={17};rawDistinctRays={18};rawCombat={19};rawPeriodic={20};rawGeometryDisagreements={21};plusOneAccepted={22};plusOneRejected={23};plusOneClear={24};plusOneBlocked={25};plusOneDistinctRays={26};plusOneCombat={27};plusOnePeriodic={28};plusOneGeometryDisagreements={29}",
                captureReference,
                sourceSha256,
                lineOfSightSha256,
                doorStateSha256,
                VergilAeneidMonsterData,
                targetReference,
                selected.Variant,
                selected.NativeAgreementPairs,
                clearPairs,
                blockedPairs,
                selected.NativeApiDisagreements,
                pairs.Count,
                SelectionRule,
                Math.Abs(raw.NativeAgreementPairs - plusOne.NativeAgreementPairs),
                raw.NativeAgreementPairs,
                raw.NativeApiDisagreements,
                raw.NativeClearPairs,
                raw.NativeBlockedPairs,
                raw.DistinctRayCount,
                raw.CombatAgreementPairs,
                raw.PeriodicAgreementPairs,
                raw.GeometryDisagreements,
                plusOne.NativeAgreementPairs,
                plusOne.NativeApiDisagreements,
                plusOne.NativeClearPairs,
                plusOne.NativeBlockedPairs,
                plusOne.DistinctRayCount,
                plusOne.CombatAgreementPairs,
                plusOne.PeriodicAgreementPairs,
                plusOne.GeometryDisagreements);
            string reviewedJson = AddPromotionMetadata(
                geometryJson,
                sourceSha256,
                selected.ProbeHeight,
                evidence);
            PlayfieldCollisionGeometryLoadResult reviewed =
                Pf127CollisionGeometryLoader.LoadJson(reviewedJson);
            if (!reviewed.IsLoaded)
            {
                throw new InvalidDataException(
                    "The reviewed PF127 runtime JSON was rejected by the runtime loader: "
                    + reviewed.Error);
            }

            if (Math.Abs(reviewed.Geometry.DamageLineOfSightProbeHeight - selected.ProbeHeight)
                > CoordinateTolerance
                || !string.Equals(
                    reviewed.Geometry.DamageLineOfSightProbeHeightEvidence,
                    evidence,
                    StringComparison.Ordinal)
                || !string.Equals(
                    reviewed.Geometry.SourceSha256,
                    sourceSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The reviewed PF127 runtime JSON did not preserve the selected probe profile and evidence hashes.");
            }

            WriteAtomically(fullOutputPath, reviewedJson);
            byte[] outputBytes = File.ReadAllBytes(fullOutputPath);
            string persistedJson = ReadUtf8Text(outputBytes, fullOutputPath);
            PlayfieldCollisionGeometryLoadResult persisted =
                Pf127CollisionGeometryLoader.LoadJson(persistedJson);
            if (!persisted.IsLoaded)
            {
                throw new InvalidDataException(
                    "The persisted reviewed PF127 runtime JSON was rejected by the runtime loader: "
                    + persisted.Error);
            }

            return new Pf127LineOfSightPromotionResult
            {
                OutputPath = fullOutputPath,
                SourceSha256 = sourceSha256,
                LineOfSightSha256 = lineOfSightSha256,
                DoorStateSha256 = doorStateSha256,
                OutputSha256 = ComputeSha256(outputBytes),
                Evidence = evidence,
                ProbeVariant = selected.Variant,
                ProbeHeight = selected.ProbeHeight,
                PairCount = selected.NativeAgreementPairs,
                ClearPairCount = clearPairs,
                BlockedPairCount = blockedPairs,
                NativeDisagreementPairCount = selected.NativeApiDisagreements
            };
        }

        internal static int RunSelfTest()
        {
            string temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "AORebirth-PF127-Los-Promotion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                string successFolder = CreateSelfTestCapture(
                    temporaryRoot,
                    "success",
                    SelfTestScenario.Success);
                string canonicalPath = Path.Combine(successFolder, GeometryFileName);
                string sourceBefore = ComputeSha256(File.ReadAllBytes(canonicalPath));
                Pf127LineOfSightPromotionResult result = Promote(successFolder, null);
                AssertSelfTestEqual("plus-one-y", result.ProbeVariant, "selected probe variant");
                AssertSelfTestEqual(1.0, result.ProbeHeight, "selected probe height");
                AssertSelfTestEqual(2, result.PairCount, "accepted pair count");
                AssertSelfTestEqual(1, result.ClearPairCount, "clear pair count");
                AssertSelfTestEqual(1, result.BlockedPairCount, "blocked pair count");
                AssertSelfTestEqual(
                    sourceBefore,
                    ComputeSha256(File.ReadAllBytes(canonicalPath)),
                    "canonical capture hash after promotion");
                AssertSelfTest(
                    Pf127CollisionGeometryLoader.LoadPath(result.OutputPath).IsLoaded,
                    "reviewed output accepted by the runtime loader");

                string negativeDoorInstanceFolder = CreateSelfTestCapture(
                    temporaryRoot,
                    "negative-door-instance-success",
                    SelfTestScenario.NegativeDoorInstanceSuccess);
                Pf127LineOfSightPromotionResult negativeDoorInstance = Promote(
                    negativeDoorInstanceFolder,
                    null);
                AssertSelfTestEqual(
                    2,
                    negativeDoorInstance.PairCount,
                    "signed negative door Identity.Instance accepted");

                string periodicOnlyFolder = CreateSelfTestCapture(
                    temporaryRoot,
                    "periodic-only-success",
                    SelfTestScenario.PeriodicOnlySuccess);
                Pf127LineOfSightPromotionResult periodicOnly = Promote(
                    periodicOnlyFolder,
                    null);
                AssertSelfTestEqual(
                    2,
                    periodicOnly.PairCount,
                    "periodic-only accepted pair count");
                AssertSelfTestEqual(
                    1,
                    periodicOnly.ClearPairCount,
                    "periodic-only clear pair count");
                AssertSelfTestEqual(
                    1,
                    periodicOnly.BlockedPairCount,
                    "periodic-only blocked pair count");

                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "cross-target",
                        SelfTestScenario.CrossTargetPair),
                    "raw/+1 pair");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "split-state-identities",
                        SelfTestScenario.SplitStateAcrossIdentities),
                    "same exact Vergil identity");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "door-revision",
                        SelfTestScenario.DoorRevisionMismatch),
                    "raw/+1 pair");
                string streamedDoorSetFolder = CreateSelfTestCapture(
                    temporaryRoot,
                    "streamed-door-set-success",
                    SelfTestScenario.StreamedDoorSetSuccess);
                Pf127LineOfSightPromotionResult streamedDoorSet = Promote(
                    streamedDoorSetFolder,
                    null);
                AssertSelfTestEqual(
                    2,
                    streamedDoorSet.PairCount,
                    "streamed resident door set accepted");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "door-identity-string-mismatch",
                        SelfTestScenario.DoorIdentityStringMismatch),
                    "identity string does not match");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "door-link-mismatch",
                        SelfTestScenario.DoorLinkMismatch),
                    "explicit client-safe unavailable status");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "missing-hit",
                        SelfTestScenario.NativeBlockedWithoutTriangleHit),
                    "no valid probe height");
                ExpectPromotionFailure(
                    CreateSelfTestCapture(
                        temporaryRoot,
                        "ambiguous-height",
                        SelfTestScenario.AmbiguousHeight),
                    "ambiguous");

                Console.WriteLine("PF127 LOS promotion self-test PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "PF127 LOS promotion self-test FAIL: " + exception.Message);
                return 1;
            }
            finally
            {
                string fullTemporaryRoot = Path.GetFullPath(temporaryRoot);
                string fullSystemTemporaryPath = Path.GetFullPath(Path.GetTempPath());
                if (fullTemporaryRoot.StartsWith(
                    fullSystemTemporaryPath,
                    StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(fullTemporaryRoot))
                {
                    Directory.Delete(fullTemporaryRoot, true);
                }
            }
        }

        private static PlayfieldCollisionGeometry LoadUnreviewedGeometry(
            string json,
            string sourceSha256,
            out Dictionary<string, CanonicalDoorEvidence> expectedDoors)
        {
            Pf127GeometryDocumentDto document;
            object rawDocument;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                document = serializer.Deserialize<Pf127GeometryDocumentDto>(json);
                rawDocument = serializer.DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry JSON parse failed: " + exception.Message,
                    exception);
            }

            if (document == null
                || document.SchemaVersion != PlayfieldCollisionGeometry.SupportedSchemaVersion
                || document.PlayfieldResource != SubwayPlayfieldResource)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry must use schemaVersion 1 and playfieldResource 127.");
            }

            if (document.DamageLineOfSightProbeHeight.HasValue
                || !string.IsNullOrWhiteSpace(document.DamageLineOfSightProbeHeightEvidence))
            {
                throw new InvalidDataException(
                    "PF127 canonical capture geometry is already carrying runtime probe metadata; promote only the unmodified capture file.");
            }

            if (document.Triangles == null
                || document.Triangles.Length == 0
                || document.Triangles.Length > MaximumTriangleCount)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry must contain a supported nonempty triangle set.");
            }

            expectedDoors = ReadExpectedDoors(rawDocument);
            var triangles = new List<CollisionTriangle>(document.Triangles.Length);
            for (int index = 0; index < document.Triangles.Length; index++)
            {
                Pf127GeometryTriangleDto triangle = document.Triangles[index];
                if (triangle == null
                    || !triangle.Id.HasValue
                    || triangle.A == null
                    || triangle.B == null
                    || triangle.C == null)
                {
                    throw new InvalidDataException(
                        "PF127 canonical geometry triangle[" + index + "] is incomplete.");
                }

                triangles.Add(
                    new CollisionTriangle(
                        triangle.Id.Value,
                        ReadPoint(triangle.A, index, "a"),
                        ReadPoint(triangle.B, index, "b"),
                        ReadPoint(triangle.C, index, "c")));
            }

            try
            {
                // Probe metadata is deliberately ignored while both captured variants are replayed.
                // The temporary value cannot escape this method or be emitted as runtime content.
                return new PlayfieldCollisionGeometry(
                    document.SchemaVersion.Value,
                    document.PlayfieldResource.Value,
                    document.Source,
                    sourceSha256,
                    0.0,
                    "promotion-validation-only",
                    triangles);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry validation failed: " + exception.Message,
                    exception);
            }
        }

        private static CollisionPoint3 ReadPoint(
            Pf127GeometryPointDto point,
            int triangleIndex,
            string vertex)
        {
            if (!point.X.HasValue || !point.Y.HasValue || !point.Z.HasValue)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry triangle["
                    + triangleIndex
                    + "]."
                    + vertex
                    + " is incomplete.");
            }

            var result = new CollisionPoint3(point.X.Value, point.Y.Value, point.Z.Value);
            if (!result.IsFinite)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry triangle["
                    + triangleIndex
                    + "]."
                    + vertex
                    + " is nonfinite.");
            }

            return result;
        }

        private static Dictionary<string, CanonicalDoorEvidence> ReadExpectedDoors(object rawDocument)
        {
            var root = rawDocument as IDictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("PF127 canonical geometry root is not an object.");
            }

            object doorsValue;
            var doors = root.TryGetValue("doors", out doorsValue) ? doorsValue as object[] : null;
            object doorLinkSchemaVersionValue;
            int doorLinkSchemaVersion;
            if (!root.TryGetValue("doorLinkSchemaVersion", out doorLinkSchemaVersionValue)
                || !TryConvertInteger(doorLinkSchemaVersionValue, out doorLinkSchemaVersion)
                || doorLinkSchemaVersion != 1)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry doorLinkSchemaVersion must be 1.");
            }

            object doorLinkPolicyValue;
            string doorLinkPolicy;
            if (!root.TryGetValue("doorLinkCapturePolicy", out doorLinkPolicyValue)
                || !string.Equals(
                    doorLinkPolicy = doorLinkPolicyValue as string,
                    "unavailable_not_read_for_client_safety",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry doorLinkCapturePolicy must explicitly disable in-process link reads for client safety.");
            }

            object countsValue;
            var counts = root.TryGetValue("counts", out countsValue)
                             ? countsValue as IDictionary<string, object>
                             : null;
            object countValue;
            int declaredDoorCount;
            if (doors == null
                || counts == null
                || !counts.TryGetValue("doors", out countValue)
                || !TryConvertInteger(countValue, out declaredDoorCount)
                || declaredDoorCount <= 0
                || declaredDoorCount != doors.Length)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry must contain matching nonzero doors[] and counts.doors values.");
            }

            var expectedDoors = new Dictionary<string, CanonicalDoorEvidence>(StringComparer.Ordinal);
            for (int index = 0; index < doors.Length; index++)
            {
                var door = doors[index] as IDictionary<string, object>;
                object identityTypeValue;
                object identityInstanceValue;
                int identityType;
                int identityInstance;
                if (door == null
                    || !door.TryGetValue("identityType", out identityTypeValue)
                    || !door.TryGetValue("identityInstance", out identityInstanceValue)
                    || !TryConvertInteger(identityTypeValue, out identityType)
                    || !TryConvertInteger(identityInstanceValue, out identityInstance)
                    || identityType <= 0)
                {
                    throw new InvalidDataException(
                        "PF127 canonical geometry door["
                        + index.ToString(CultureInfo.InvariantCulture)
                        + "] is missing a positive numeric identity type or signed 32-bit identity instance.");
                }

                string identity = identityType.ToString(CultureInfo.InvariantCulture)
                                  + ":"
                                  + identityInstance.ToString(CultureInfo.InvariantCulture);
                CanonicalDoorEvidence evidence = new CanonicalDoorEvidence(
                    identity,
                    ReadNullableInteger(door, "rawLink1Index", "door " + identity),
                    ReadRequiredString(door, "link1Resolution", "door " + identity),
                    ReadNullableInteger(door, "room1Instance", "door " + identity),
                    ReadNullableInteger(door, "rawLink2Index", "door " + identity),
                    ReadRequiredString(door, "link2Resolution", "door " + identity),
                    ReadNullableInteger(door, "room2Instance", "door " + identity));
                evidence.ValidateClientSafeUnavailable("PF127 canonical geometry");
                if (expectedDoors.ContainsKey(identity))
                {
                    throw new InvalidDataException(
                        "PF127 canonical geometry contains duplicate door identity "
                        + identity
                        + ".");
                }

                expectedDoors.Add(identity, evidence);
            }

            if (expectedDoors.Count != declaredDoorCount)
            {
                throw new InvalidDataException(
                    "PF127 canonical geometry door identities do not match counts.doors.");
            }

            return expectedDoors;
        }

        private static string ReadRequiredString(
            IDictionary<string, object> source,
            string key,
            string context)
        {
            object value;
            string result;
            if (source == null
                || !source.TryGetValue(key, out value)
                || string.IsNullOrWhiteSpace(result = value as string))
            {
                throw new InvalidDataException(context + "." + key + " must be a nonempty string.");
            }

            return result;
        }

        private static int? ReadNullableInteger(
            IDictionary<string, object> source,
            string key,
            string context)
        {
            object value;
            if (source == null || !source.TryGetValue(key, out value))
            {
                throw new InvalidDataException(context + "." + key + " must be present.");
            }

            if (value == null)
            {
                return null;
            }

            int result;
            if (!TryConvertInteger(value, out result))
            {
                throw new InvalidDataException(context + "." + key + " must be an integer or null.");
            }

            return result;
        }

        private static Dictionary<DoorBatchKey, DoorBatch> LoadDoorBatches(
            string path,
            Dictionary<string, CanonicalDoorEvidence> expectedDoors)
        {
            CsvDocument csv = CsvDocument.Read(path);
            csv.RequireColumns(
                "Trigger",
                "Revision",
                "EvidenceBatchId",
                "ResourcePlayfieldId",
                "IdentityType",
                "IdentityInstance",
                "Identity",
                "PositionX",
                "PositionY",
                "PositionZ",
                "RotationX",
                "RotationY",
                "RotationZ",
                "RotationW",
                "DoorLinkSchemaVersion",
                "RawLink1Index",
                "Link1Resolution",
                "Room1Instance",
                "RawLink2Index",
                "Link2Resolution",
                "Room2Instance",
                "IsOpen",
                "IsLocked");
            var batches = new Dictionary<DoorBatchKey, DoorBatch>();
            foreach (CsvRow row in csv.Rows)
            {
                int resource = row.RequiredInteger("ResourcePlayfieldId");
                if (resource != SubwayPlayfieldResource)
                {
                    continue;
                }

                string trigger = row.RequiredValue("Trigger");
                long evidenceBatchId = row.RequiredPositiveLong("EvidenceBatchId");
                int revision = row.RequiredPositiveInteger("Revision");
                var key = new DoorBatchKey(trigger, evidenceBatchId, revision);
                DoorBatch batch;
                if (!batches.TryGetValue(key, out batch))
                {
                    batch = new DoorBatch(key);
                    batches.Add(key, batch);
                }

                int identityType = row.RequiredPositiveInteger("IdentityType");
                int identityInstance = row.RequiredInteger("IdentityInstance");
                string identity = identityType.ToString(CultureInfo.InvariantCulture)
                                  + ":"
                                  + identityInstance.ToString(CultureInfo.InvariantCulture);
                string identityText = row.RequiredValue("Identity");
                string expectedIdentityText = FormatDoorIdentityText(
                    identityType,
                    identityInstance);
                if (!string.Equals(identityText, expectedIdentityText, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "PF127 door-state CSV identity string does not match its numeric identity: expected "
                        + expectedIdentityText
                        + " but found "
                        + identityText
                        + ".");
                }

                if (!batch.DoorIdentities.Add(identity))
                {
                    throw new InvalidDataException(
                        "PF127 door-state batch contains duplicate door identity " + identity + ".");
                }

                row.RequiredFiniteDouble("PositionX");
                row.RequiredFiniteDouble("PositionY");
                row.RequiredFiniteDouble("PositionZ");
                row.RequiredFiniteDouble("RotationX");
                row.RequiredFiniteDouble("RotationY");
                row.RequiredFiniteDouble("RotationZ");
                row.RequiredFiniteDouble("RotationW");
                if (row.RequiredInteger("DoorLinkSchemaVersion") != 1)
                {
                    throw new InvalidDataException(
                        "PF127 door-state row " + identity + " has an unsupported door-link schema.");
                }

                CanonicalDoorEvidence observedDoor = CanonicalDoorEvidence.FromCsvRow(identity, row);
                observedDoor.ValidateClientSafeUnavailable("PF127 door-state CSV");
                CanonicalDoorEvidence expectedDoor;
                if (expectedDoors.TryGetValue(identity, out expectedDoor))
                {
                    expectedDoor.RequireSameLinkEvidence(observedDoor, "PF127 door-state CSV");
                }

                row.RequiredBoolean("IsOpen");
                row.RequiredBoolean("IsLocked");
            }

            foreach (DoorBatch batch in batches.Values)
            {
                if (batch.DoorIdentities.Count == 0)
                {
                    batch.IsComplete = false;
                    batch.IncompleteReason = "door-state batch has no resident door rows";
                }
                else
                {
                    batch.IsComplete = true;
                }
            }

            return batches;
        }

        private static List<LineOfSightPair> LoadVergilPairs(
            string path,
            IDictionary<DoorBatchKey, DoorBatch> doorBatches)
        {
            CsvDocument csv = CsvDocument.Read(path);
            csv.RequireColumns(
                "Trigger",
                "ProbeVariant",
                "ProbeHeight",
                "DoorStateRevision",
                "EvidenceBatchId",
                "ResourcePlayfieldId",
                "RuntimePlayfieldId",
                "LocalIdentity",
                "OriginX",
                "OriginY",
                "OriginZ",
                "TargetIdentity",
                "TargetIdentityType",
                "TargetIdentityInstance",
                "TargetName",
                "TargetMonsterData",
                "TargetIsNpc",
                "TargetX",
                "TargetY",
                "TargetZ",
                "SimpleCharIsInLineOfSight",
                "PlayfieldLineOfSight",
                "RaycastHit",
                "Usable",
                "Error");

            var pairs = new Dictionary<LineOfSightPairKey, LineOfSightPair>();
            foreach (CsvRow row in csv.Rows)
            {
                string monsterDataText = row.Value("TargetMonsterData");
                int monsterData;
                if (!int.TryParse(
                    monsterDataText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out monsterData)
                    || monsterData != VergilAeneidMonsterData)
                {
                    continue;
                }

                if (row.RequiredInteger("ResourcePlayfieldId") != SubwayPlayfieldResource)
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence is not bound to resource playfield 127.");
                }

                string trigger = row.RequiredValue("Trigger");
                if (!string.Equals(trigger, "combat", StringComparison.Ordinal)
                    && !string.Equals(trigger, "periodic", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence trigger must be combat or periodic.");
                }

                string targetName = row.RequiredValue("TargetName");
                if (!string.Equals(targetName, "Vergil Aeneid", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "MonsterData 203748 LOS evidence has an unexpected target name: "
                        + targetName);
                }

                if (!row.RequiredBoolean("TargetIsNpc") || !row.RequiredBoolean("Usable"))
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence must be an NPC row with all native probes usable.");
                }

                if (!string.IsNullOrEmpty(row.Value("Error")))
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence contains a probe error: " + row.Value("Error"));
                }

                int targetIdentityType = row.RequiredPositiveInteger("TargetIdentityType");
                int targetIdentityInstance = row.RequiredPositiveInteger("TargetIdentityInstance");
                string targetIdentity = row.RequiredValue("TargetIdentity");
                string targetIdentityKey = targetIdentityType.ToString(CultureInfo.InvariantCulture)
                                           + ":"
                                           + targetIdentityInstance.ToString(CultureInfo.InvariantCulture);
                long evidenceBatchId = row.RequiredPositiveLong("EvidenceBatchId");
                int revision = row.RequiredPositiveInteger("DoorStateRevision");
                string localIdentity = row.RequiredValue("LocalIdentity");
                string runtimePlayfieldId = row.RequiredValue("RuntimePlayfieldId");
                var key = new LineOfSightPairKey(
                    trigger,
                    evidenceBatchId,
                    revision,
                    localIdentity,
                    runtimePlayfieldId,
                    targetIdentityKey);
                LineOfSightPair pair;
                if (!pairs.TryGetValue(key, out pair))
                {
                    pair = new LineOfSightPair(key, targetIdentity, targetName);
                    pairs.Add(key, pair);
                }
                else if (!string.Equals(pair.TargetIdentity, targetIdentity, StringComparison.Ordinal)
                         || !string.Equals(pair.TargetName, targetName, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Vergil raw/+1 rows disagree on target identity metadata.");
                }

                var sample = new LineOfSightSample
                {
                    Variant = row.RequiredValue("ProbeVariant"),
                    ProbeHeight = row.RequiredFiniteDouble("ProbeHeight"),
                    Origin = new CollisionPoint3(
                        row.RequiredFiniteDouble("OriginX"),
                        row.RequiredFiniteDouble("OriginY"),
                        row.RequiredFiniteDouble("OriginZ")),
                    Target = new CollisionPoint3(
                        row.RequiredFiniteDouble("TargetX"),
                        row.RequiredFiniteDouble("TargetY"),
                        row.RequiredFiniteDouble("TargetZ")),
                    SimpleCharLineOfSight = row.RequiredBoolean("SimpleCharIsInLineOfSight"),
                    PlayfieldLineOfSight = row.RequiredBoolean("PlayfieldLineOfSight"),
                    RaycastHit = row.RequiredBoolean("RaycastHit")
                };
                if (sample.RaycastHit)
                {
                    row.RequiredFiniteDouble("RaycastHitX");
                    row.RequiredFiniteDouble("RaycastHitY");
                    row.RequiredFiniteDouble("RaycastHitZ");
                    row.RequiredFiniteDouble("RaycastNormalX");
                    row.RequiredFiniteDouble("RaycastNormalY");
                    row.RequiredFiniteDouble("RaycastNormalZ");
                }

                if (string.Equals(sample.Variant, "raw", StringComparison.Ordinal))
                {
                    if (pair.Raw != null)
                    {
                        throw new InvalidDataException(
                            "Vergil LOS evidence contains duplicate raw rows for one target/batch/revision.");
                    }

                    pair.Raw = sample;
                }
                else if (string.Equals(sample.Variant, "plus-one-y", StringComparison.Ordinal))
                {
                    if (pair.PlusOne != null)
                    {
                        throw new InvalidDataException(
                            "Vergil LOS evidence contains duplicate plus-one-y rows for one target/batch/revision.");
                    }

                    pair.PlusOne = sample;
                }
                else
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence has an unsupported probe variant: " + sample.Variant);
                }
            }

            var result = pairs.Values
                .OrderBy(pair => pair.Key.EvidenceBatchId)
                .ThenBy(pair => pair.TargetIdentityKey, StringComparer.Ordinal)
                .ToList();
            foreach (LineOfSightPair pair in result)
            {
                if (pair.Raw == null || pair.PlusOne == null)
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence has an incomplete raw/+1 pair for batch "
                        + pair.Key.EvidenceBatchId.ToString(CultureInfo.InvariantCulture)
                        + " target "
                        + pair.TargetIdentityKey
                        + ".");
                }

                ValidatePairShape(pair);
                var doorKey = new DoorBatchKey(
                    pair.Key.Trigger,
                    pair.Key.EvidenceBatchId,
                    pair.Key.DoorStateRevision);
                DoorBatch doorBatch;
                if (!doorBatches.TryGetValue(doorKey, out doorBatch) || !doorBatch.IsComplete)
                {
                    throw new InvalidDataException(
                        "Vergil LOS evidence has no complete matching door-state batch/revision for batch "
                        + pair.Key.EvidenceBatchId.ToString(CultureInfo.InvariantCulture)
                        + (doorBatch == null ? "." : ": " + doorBatch.IncompleteReason + "."));
                }
            }

            return result;
        }

        private static void ValidatePairShape(LineOfSightPair pair)
        {
            if (Math.Abs(pair.Raw.ProbeHeight) > CoordinateTolerance
                || Math.Abs(pair.PlusOne.ProbeHeight - 1.0) > CoordinateTolerance)
            {
                throw new InvalidDataException(
                    "Vergil raw/+1 pair has unexpected probe-height metadata.");
            }

            if (!NearlyEqual(pair.Raw.Origin.X, pair.PlusOne.Origin.X)
                || !NearlyEqual(pair.Raw.Origin.Z, pair.PlusOne.Origin.Z)
                || !NearlyEqual(pair.Raw.Target.X, pair.PlusOne.Target.X)
                || !NearlyEqual(pair.Raw.Target.Z, pair.PlusOne.Target.Z)
                || !NearlyEqual(pair.Raw.Origin.Y + 1.0, pair.PlusOne.Origin.Y)
                || !NearlyEqual(pair.Raw.Target.Y + 1.0, pair.PlusOne.Target.Y))
            {
                throw new InvalidDataException(
                    "Vergil raw/+1 pair endpoints do not differ by exactly one Y unit.");
            }

            if (pair.Raw.SimpleCharLineOfSight != pair.PlusOne.SimpleCharLineOfSight)
            {
                throw new InvalidDataException(
                    "Vergil raw/+1 pair disagrees on the native SimpleChar line-of-sight baseline.");
            }
        }

        private static void ValidateEvidenceCoverage(IList<LineOfSightPair> pairs)
        {
            if (pairs.Count == 0)
            {
                throw new InvalidDataException(
                    "PF127 LOS promotion requires identity-proven Vergil Aeneid evidence.");
            }

            bool sameIdentityHasBothStates = pairs
                .GroupBy(pair => pair.TargetIdentityKey, StringComparer.Ordinal)
                .Any(group => group.Any(pair => pair.NativeClear)
                              && group.Any(pair => !pair.NativeClear));
            if (!sameIdentityHasBothStates)
            {
                throw new InvalidDataException(
                    "PF127 LOS promotion requires at least one clear and one blocked sample for the same exact Vergil identity.");
            }
        }

        private static CandidateEvaluation EvaluateCandidate(
            PlayfieldCollisionGeometry geometry,
            IEnumerable<LineOfSightPair> pairs,
            string variant,
            double probeHeight)
        {
            var evaluation = new CandidateEvaluation(variant, probeHeight);
            var hitCache = new Dictionary<string, bool>(StringComparer.Ordinal);
            var acceptedRayKeys = new HashSet<string>(StringComparer.Ordinal);
            var clearTargets = new HashSet<string>(StringComparer.Ordinal);
            var blockedTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (LineOfSightPair pair in pairs)
            {
                LineOfSightSample sample = string.Equals(variant, "raw", StringComparison.Ordinal)
                                               ? pair.Raw
                                               : pair.PlusOne;
                string rayKey = RayKey(sample.Origin, sample.Target);
                bool geometryBlocked;
                if (!hitCache.TryGetValue(rayKey, out geometryBlocked))
                {
                    SegmentTriangleHit hit;
                    geometryBlocked = geometry.TryFindFirstBlockingHit(
                        sample.Origin,
                        sample.Target,
                        out hit);
                    hitCache.Add(rayKey, geometryBlocked);
                }

                bool nativeClear = sample.SimpleCharLineOfSight;
                bool nativeApisAgree = sample.PlayfieldLineOfSight == nativeClear
                                       && sample.RaycastHit == !nativeClear;
                if (!nativeApisAgree)
                {
                    evaluation.NativeApiDisagreements++;
                    continue;
                }

                evaluation.NativeAgreementPairs++;
                acceptedRayKeys.Add(rayKey);
                if (string.Equals(pair.Key.Trigger, "combat", StringComparison.Ordinal))
                {
                    evaluation.CombatAgreementPairs++;
                }
                else
                {
                    evaluation.PeriodicAgreementPairs++;
                }

                if (nativeClear)
                {
                    evaluation.NativeClearPairs++;
                    clearTargets.Add(pair.TargetIdentityKey);
                }
                else
                {
                    evaluation.NativeBlockedPairs++;
                    blockedTargets.Add(pair.TargetIdentityKey);
                }

                if (geometryBlocked == nativeClear)
                {
                    evaluation.GeometryDisagreements++;
                    if (nativeClear)
                    {
                        evaluation.FalseBlockingHits++;
                    }
                    else
                    {
                        evaluation.MissingBlockingHits++;
                    }
                }
            }

            evaluation.SameIdentityHasBothStates = clearTargets.Overlaps(blockedTargets);
            evaluation.DistinctRayCount = acceptedRayKeys.Count;

            return evaluation;
        }

        private static string AddPromotionMetadata(
            string canonicalJson,
            string sourceSha256,
            double probeHeight,
            string evidence)
        {
            int openingBrace = canonicalJson.IndexOf('{');
            if (openingBrace < 0)
            {
                throw new InvalidDataException("PF127 canonical geometry JSON has no root object.");
            }

            string newLine = canonicalJson.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                                 ? "\r\n"
                                 : "\n";
            int bodyStart = openingBrace + 1;
            if (canonicalJson.Length >= bodyStart + newLine.Length
                && string.Equals(
                    canonicalJson.Substring(bodyStart, newLine.Length),
                    newLine,
                    StringComparison.Ordinal))
            {
                bodyStart += newLine.Length;
            }

            var metadata = new StringBuilder();
            metadata.Append(canonicalJson.Substring(0, openingBrace + 1));
            metadata.Append(newLine);
            metadata.Append("  \"sourceSha256\": \"");
            metadata.Append(JsonEscape(sourceSha256));
            metadata.Append("\",");
            metadata.Append(newLine);
            metadata.Append("  \"damageLineOfSightProbeHeight\": ");
            metadata.Append(probeHeight.ToString("R", CultureInfo.InvariantCulture));
            metadata.Append(',');
            metadata.Append(newLine);
            metadata.Append("  \"damageLineOfSightProbeHeightEvidence\": \"");
            metadata.Append(JsonEscape(evidence));
            metadata.Append("\",");
            metadata.Append(newLine);
            metadata.Append(canonicalJson.Substring(bodyStart));
            return metadata.ToString();
        }

        private static string RequireCaptureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A finished capture folder is required.", "captureFolder");
            }

            string fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(
                    "The finished capture folder does not exist: " + fullPath);
            }

            return fullPath;
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The finished capture is missing required PF127 evidence: "
                    + Path.GetFileName(path),
                    path);
            }
        }

        private static string ReadUtf8Text(byte[] bytes, string path)
        {
            try
            {
                string value = new UTF8Encoding(false, true).GetString(bytes);
                return value.Length > 0 && value[0] == '\uFEFF' ? value.Substring(1) : value;
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException(path + " is not valid UTF-8.", exception);
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static string FormatDoorIdentityText(
            int identityType,
            int identityInstance)
        {
            if (identityType != DoorIdentityType)
            {
                throw new InvalidDataException(
                    "PF127 door-state CSV contains non-door identity type "
                    + identityType.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            return "(Door:"
                   + unchecked((uint)identityInstance).ToString("X8", CultureInfo.InvariantCulture)
                   + ")";
        }

        private static void WriteAtomically(string outputPath, string content)
        {
            string pendingPath = outputPath + ".pending";
            if (File.Exists(pendingPath))
            {
                File.Delete(pendingPath);
            }

            File.WriteAllText(pendingPath, content, new UTF8Encoding(false));
            if (File.Exists(outputPath))
            {
                File.Replace(pendingPath, outputPath, null, true);
            }
            else
            {
                File.Move(pendingPath, outputPath);
            }
        }

        private static string JsonEscape(string value)
        {
            var escaped = new StringBuilder();
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            escaped.Append("\\u");
                            escaped.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            escaped.Append(character);
                        }

                        break;
                }
            }

            return escaped.ToString();
        }

        private static bool TryConvertInteger(object value, out int result)
        {
            try
            {
                result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }

        private static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= CoordinateTolerance;
        }

        private static string RayKey(CollisionPoint3 origin, CollisionPoint3 target)
        {
            return string.Join(
                "|",
                origin.X.ToString("R", CultureInfo.InvariantCulture),
                origin.Y.ToString("R", CultureInfo.InvariantCulture),
                origin.Z.ToString("R", CultureInfo.InvariantCulture),
                target.X.ToString("R", CultureInfo.InvariantCulture),
                target.Y.ToString("R", CultureInfo.InvariantCulture),
                target.Z.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string CreateSelfTestCapture(
            string root,
            string name,
            SelfTestScenario scenario)
        {
            string folder = Path.Combine(root, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, GeometryFileName),
                SelfTestGeometryJson(scenario),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(folder, DoorStateFileName),
                SelfTestDoorStateCsv(scenario),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(folder, LineOfSightFileName),
                SelfTestLineOfSightCsv(scenario),
                new UTF8Encoding(false));
            return folder;
        }

        private static string SelfTestGeometryJson(SelfTestScenario scenario)
        {
            int doorIdentityInstance = SelfTestCanonicalDoorIdentityInstance(scenario);
            return "{\n"
                   + "  \"schemaVersion\": 1,\n"
                   + "  \"doorLinkSchemaVersion\": 1,\n"
                   + "  \"doorLinkCapturePolicy\": \"unavailable_not_read_for_client_safety\",\n"
                   + "  \"playfieldResource\": 127,\n"
                   + "  \"source\": \"synthetic-promotion-self-test\",\n"
                   + "  \"triangles\": [\n"
                   + "    { \"id\": 0, \"a\": { \"x\": 0, \"y\": 0, \"z\": 9 }, \"b\": { \"x\": 10, \"y\": 0, \"z\": 9 }, \"c\": { \"x\": 10, \"y\": 0, \"z\": 11 } },\n"
                   + "    { \"id\": 1, \"a\": { \"x\": 0, \"y\": 0, \"z\": 9 }, \"b\": { \"x\": 10, \"y\": 0, \"z\": 11 }, \"c\": { \"x\": 0, \"y\": 0, \"z\": 11 } },\n"
                   + "    { \"id\": 2, \"a\": { \"x\": 5, \"y\": 0, \"z\": -1 }, \"b\": { \"x\": 5, \"y\": 3, \"z\": -1 }, \"c\": { \"x\": 5, \"y\": 3, \"z\": 1 } },\n"
                   + "    { \"id\": 3, \"a\": { \"x\": 5, \"y\": 0, \"z\": -1 }, \"b\": { \"x\": 5, \"y\": 3, \"z\": 1 }, \"c\": { \"x\": 5, \"y\": 0, \"z\": 1 } }\n"
                   + "  ],\n"
                   + "  \"rooms\": [{ \"instance\": 1 }],\n"
                   + "  \"doors\": [{ \"identityType\": 51016, \"identityInstance\": "
                   + doorIdentityInstance.ToString(CultureInfo.InvariantCulture)
                   + ", \"rawLink1Index\": null, \"link1Resolution\": \"unavailable_not_read_for_client_safety\", \"room1Instance\": null, \"rawLink2Index\": null, \"link2Resolution\": \"unavailable_not_read_for_client_safety\", \"room2Instance\": null }],\n"
                   + "  \"counts\": { \"rooms\": 1, \"doors\": 1, \"meshes\": 1, \"vertices\": 12, \"triangles\": 4 }\n"
                   + "}\n";
        }

        private static string SelfTestDoorStateCsv(SelfTestScenario scenario)
        {
            var result = new StringBuilder();
            result.AppendLine("CapturedUtc,Trigger,Revision,EvidenceBatchId,ResourcePlayfieldId,RuntimePlayfieldId,IdentityType,IdentityInstance,Identity,Name,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,RotationW,DoorLinkSchemaVersion,RawLink1Index,Link1Resolution,Room1Instance,RawLink2Index,Link2Resolution,Room2Instance,IsOpen,IsLocked");
            string firstTrigger = scenario == SelfTestScenario.PeriodicOnlySuccess
                                      ? "periodic"
                                      : "combat";
            int canonicalDoorIdentityInstance = SelfTestCanonicalDoorIdentityInstance(scenario);
            int doorIdentityInstance = scenario == SelfTestScenario.StreamedDoorSetSuccess
                                           ? 2
                                           : canonicalDoorIdentityInstance;
            int identityTextInstance = scenario == SelfTestScenario.DoorIdentityStringMismatch
                                           ? doorIdentityInstance + 1
                                           : doorIdentityInstance;
            string linkEvidence = scenario == SelfTestScenario.DoorLinkMismatch
                                      ? "0,resolved,1,,unavailable_not_read_for_client_safety,,false,false"
                                      : ",unavailable_not_read_for_client_safety,,,unavailable_not_read_for_client_safety,,false,false";
            result.AppendLine(
                "2026-07-14T00:00:00Z,"
                + firstTrigger
                + ",1,1,127,127,51016,"
                + doorIdentityInstance.ToString(CultureInfo.InvariantCulture)
                + ","
                + FormatDoorIdentityText(DoorIdentityType, identityTextInstance)
                + ",Door,5,0,0,0,0,0,1,1,"
                + linkEvidence);
            int secondRevision = scenario == SelfTestScenario.DoorRevisionMismatch ? 2 : 1;
            result.AppendLine(
                "2026-07-14T00:00:01Z,periodic,"
                + secondRevision.ToString(CultureInfo.InvariantCulture)
                + ",2,127,127,51016,"
                + doorIdentityInstance.ToString(CultureInfo.InvariantCulture)
                + ","
                + FormatDoorIdentityText(DoorIdentityType, identityTextInstance)
                + ",Door,5,0,0,0,0,0,1,1,"
                + linkEvidence);
            return result.ToString();
        }

        private static int SelfTestCanonicalDoorIdentityInstance(SelfTestScenario scenario)
        {
            return scenario == SelfTestScenario.NegativeDoorInstanceSuccess
                   || scenario == SelfTestScenario.DoorIdentityStringMismatch
                       ? NegativeDoorIdentityInstance
                       : 1;
        }

        private static string SelfTestLineOfSightCsv(SelfTestScenario scenario)
        {
            const string header = "CapturedUtc,Trigger,ProbeVariant,ProbeHeight,DoorStateRevision,EvidenceBatchId,ResourcePlayfieldId,RuntimePlayfieldId,LocalIdentity,LocalName,OriginX,OriginY,OriginZ,TargetIdentity,TargetIdentityType,TargetIdentityInstance,TargetName,TargetMonsterData,TargetIsNpc,TargetX,TargetY,TargetZ,SimpleCharIsInLineOfSight,PlayfieldLineOfSight,RaycastHit,RaycastHitX,RaycastHitY,RaycastHitZ,RaycastNormalX,RaycastNormalY,RaycastNormalZ,Usable,Error";
            var rows = new List<string> { header };
            string rawTargetIdentityInstance = "1000222";
            string plusTargetIdentityInstance = scenario == SelfTestScenario.CrossTargetPair
                                                    ? "1000223"
                                                    : rawTargetIdentityInstance;
            string blockedTargetIdentityInstance =
                scenario == SelfTestScenario.SplitStateAcrossIdentities
                    ? "1000223"
                    : rawTargetIdentityInstance;
            string clearTrigger = scenario == SelfTestScenario.PeriodicOnlySuccess
                                      ? "periodic"
                                      : "combat";
            int clearRevision = scenario == SelfTestScenario.DoorRevisionMismatch ? 2 : 1;
            bool ambiguous = scenario == SelfTestScenario.AmbiguousHeight;
            double clearZ = ambiguous ? 5 : 10;

            rows.Add(
                SelfTestLosRow(
                    clearTrigger,
                    "raw",
                    0,
                    1,
                    1,
                    rawTargetIdentityInstance,
                    clearZ,
                    true,
                    ambiguous,
                    !ambiguous));
            rows.Add(
                SelfTestLosRow(
                    clearTrigger,
                    "plus-one-y",
                    1,
                    1,
                    1,
                    plusTargetIdentityInstance,
                    clearZ,
                    true,
                    true,
                    false));

            double blockedZ = scenario == SelfTestScenario.NativeBlockedWithoutTriangleHit ? 5 : 0;
            rows.Add(
                SelfTestLosRow(
                    "periodic",
                    "raw",
                    0,
                    clearRevision,
                    2,
                    blockedTargetIdentityInstance,
                    blockedZ,
                    false,
                    false,
                    true));
            rows.Add(
                SelfTestLosRow(
                    "periodic",
                    "plus-one-y",
                    1,
                    1,
                    2,
                    blockedTargetIdentityInstance,
                    blockedZ,
                    false,
                    false,
                    true));
            return string.Join("\n", rows.ToArray()) + "\n";
        }

        private static string SelfTestLosRow(
            string trigger,
            string variant,
            int height,
            int revision,
            int batch,
            string targetIdentityInstance,
            double z,
            bool simpleCharLineOfSight,
            bool playfieldLineOfSight,
            bool raycastHit)
        {
            string hitFields = raycastHit
                                   ? "5," + height.ToString(CultureInfo.InvariantCulture) + "," + z.ToString("R", CultureInfo.InvariantCulture) + ",1,0,0"
                                   : ",,,,,";
            return string.Join(
                ",",
                "2026-07-14T00:00:00Z",
                trigger,
                variant,
                height.ToString(CultureInfo.InvariantCulture),
                revision.ToString(CultureInfo.InvariantCulture),
                batch.ToString(CultureInfo.InvariantCulture),
                "127",
                "127",
                "50000:22",
                "Player",
                "0",
                height.ToString(CultureInfo.InvariantCulture),
                z.ToString("R", CultureInfo.InvariantCulture),
                "50000:" + targetIdentityInstance,
                "50000",
                targetIdentityInstance,
                "Vergil Aeneid",
                VergilAeneidMonsterData.ToString(CultureInfo.InvariantCulture),
                "true",
                "10",
                height.ToString(CultureInfo.InvariantCulture),
                z.ToString("R", CultureInfo.InvariantCulture),
                simpleCharLineOfSight ? "true" : "false",
                playfieldLineOfSight ? "true" : "false",
                raycastHit ? "true" : "false",
                hitFields,
                "true",
                string.Empty);
        }

        private static void ExpectPromotionFailure(string captureFolder, string expectedMessage)
        {
            try
            {
                Promote(captureFolder, null);
            }
            catch (InvalidDataException exception)
            {
                if (exception.Message.IndexOf(expectedMessage, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidDataException(
                        "Expected promotion failure containing '"
                        + expectedMessage
                        + "' but got: "
                        + exception.Message);
                }

                return;
            }

            throw new InvalidDataException(
                "Expected PF127 LOS promotion to fail: " + expectedMessage);
        }

        private static void AssertSelfTest(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException("Self-test assertion failed: " + message);
            }
        }

        private static void AssertSelfTestEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidDataException(
                    "Self-test assertion failed for "
                    + message
                    + ": expected="
                    + expected
                    + " actual="
                    + actual);
            }
        }

        private enum SelfTestScenario
        {
            Success,
            PeriodicOnlySuccess,
            CrossTargetPair,
            SplitStateAcrossIdentities,
            DoorRevisionMismatch,
            StreamedDoorSetSuccess,
            NegativeDoorInstanceSuccess,
            DoorIdentityStringMismatch,
            DoorLinkMismatch,
            NativeBlockedWithoutTriangleHit,
            AmbiguousHeight
        }

        private sealed class CandidateEvaluation
        {
            internal CandidateEvaluation(string variant, double probeHeight)
            {
                this.Variant = variant;
                this.ProbeHeight = probeHeight;
            }

            internal string Variant { get; private set; }

            internal double ProbeHeight { get; private set; }

            internal int NativeApiDisagreements { get; set; }

            internal int NativeAgreementPairs { get; set; }

            internal int NativeClearPairs { get; set; }

            internal int NativeBlockedPairs { get; set; }

            internal int DistinctRayCount { get; set; }

            internal int CombatAgreementPairs { get; set; }

            internal int PeriodicAgreementPairs { get; set; }

            internal bool SameIdentityHasBothStates { get; set; }

            internal int GeometryDisagreements { get; set; }

            internal int FalseBlockingHits { get; set; }

            internal int MissingBlockingHits { get; set; }

            internal bool IsValid
            {
                get
                {
                    return this.NativeAgreementPairs > 0
                           && this.NativeClearPairs > 0
                           && this.NativeBlockedPairs > 0
                           && this.SameIdentityHasBothStates
                           && this.GeometryDisagreements == 0;
                }
            }

            internal string FailureSummary
            {
                get
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "nativeAgreementPairs={0},nativeApiDisagreements={1},clear={2},blocked={3},distinctRays={4},combat={5},periodic={6},sameIdentityBothStates={7},geometryDisagreements={8},falseBlockingHits={9},missingBlockingHits={10}",
                        this.NativeAgreementPairs,
                        this.NativeApiDisagreements,
                        this.NativeClearPairs,
                        this.NativeBlockedPairs,
                        this.DistinctRayCount,
                        this.CombatAgreementPairs,
                        this.PeriodicAgreementPairs,
                        this.SameIdentityHasBothStates,
                        this.GeometryDisagreements,
                        this.FalseBlockingHits,
                        this.MissingBlockingHits);
                }
            }
        }

        private sealed class LineOfSightSample
        {
            internal string Variant { get; set; }

            internal double ProbeHeight { get; set; }

            internal CollisionPoint3 Origin { get; set; }

            internal CollisionPoint3 Target { get; set; }

            internal bool SimpleCharLineOfSight { get; set; }

            internal bool PlayfieldLineOfSight { get; set; }

            internal bool RaycastHit { get; set; }
        }

        private sealed class LineOfSightPair
        {
            internal LineOfSightPair(
                LineOfSightPairKey key,
                string targetIdentity,
                string targetName)
            {
                this.Key = key;
                this.TargetIdentity = targetIdentity;
                this.TargetName = targetName;
            }

            internal LineOfSightPairKey Key { get; private set; }

            internal string TargetIdentity { get; private set; }

            internal string TargetName { get; private set; }

            internal string TargetIdentityKey
            {
                get { return this.Key.TargetIdentityKey; }
            }

            internal LineOfSightSample Raw { get; set; }

            internal LineOfSightSample PlusOne { get; set; }

            internal bool NativeClear
            {
                get { return this.Raw.SimpleCharLineOfSight; }
            }
        }

        private struct LineOfSightPairKey : IEquatable<LineOfSightPairKey>
        {
            internal LineOfSightPairKey(
                string trigger,
                long evidenceBatchId,
                int doorStateRevision,
                string localIdentity,
                string runtimePlayfieldId,
                string targetIdentityKey)
            {
                this.Trigger = trigger;
                this.EvidenceBatchId = evidenceBatchId;
                this.DoorStateRevision = doorStateRevision;
                this.LocalIdentity = localIdentity;
                this.RuntimePlayfieldId = runtimePlayfieldId;
                this.TargetIdentityKey = targetIdentityKey;
            }

            internal string Trigger { get; private set; }

            internal long EvidenceBatchId { get; private set; }

            internal int DoorStateRevision { get; private set; }

            internal string LocalIdentity { get; private set; }

            internal string RuntimePlayfieldId { get; private set; }

            internal string TargetIdentityKey { get; private set; }

            public bool Equals(LineOfSightPairKey other)
            {
                return this.EvidenceBatchId == other.EvidenceBatchId
                       && this.DoorStateRevision == other.DoorStateRevision
                       && string.Equals(this.Trigger, other.Trigger, StringComparison.Ordinal)
                       && string.Equals(this.LocalIdentity, other.LocalIdentity, StringComparison.Ordinal)
                       && string.Equals(
                           this.RuntimePlayfieldId,
                           other.RuntimePlayfieldId,
                           StringComparison.Ordinal)
                       && string.Equals(
                           this.TargetIdentityKey,
                           other.TargetIdentityKey,
                           StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LineOfSightPairKey && this.Equals((LineOfSightPairKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + this.EvidenceBatchId.GetHashCode();
                    hash = (hash * 31) + this.DoorStateRevision;
                    hash = (hash * 31) + (this.Trigger ?? string.Empty).GetHashCode();
                    hash = (hash * 31) + (this.LocalIdentity ?? string.Empty).GetHashCode();
                    hash = (hash * 31) + (this.RuntimePlayfieldId ?? string.Empty).GetHashCode();
                    hash = (hash * 31) + (this.TargetIdentityKey ?? string.Empty).GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class CanonicalDoorEvidence
        {
            internal CanonicalDoorEvidence(
                string identity,
                int? rawLink1Index,
                string link1Resolution,
                int? room1Instance,
                int? rawLink2Index,
                string link2Resolution,
                int? room2Instance)
            {
                this.Identity = identity;
                this.RawLink1Index = rawLink1Index;
                this.Link1Resolution = link1Resolution;
                this.Room1Instance = room1Instance;
                this.RawLink2Index = rawLink2Index;
                this.Link2Resolution = link2Resolution;
                this.Room2Instance = room2Instance;
            }

            internal string Identity { get; private set; }
            internal int? RawLink1Index { get; private set; }
            internal string Link1Resolution { get; private set; }
            internal int? Room1Instance { get; private set; }
            internal int? RawLink2Index { get; private set; }
            internal string Link2Resolution { get; private set; }
            internal int? Room2Instance { get; private set; }

            internal static CanonicalDoorEvidence FromCsvRow(string identity, CsvRow row)
            {
                return new CanonicalDoorEvidence(
                    identity,
                    ReadNullableCsvInteger(row, "RawLink1Index"),
                    row.RequiredValue("Link1Resolution"),
                    ReadNullableCsvInteger(row, "Room1Instance"),
                    ReadNullableCsvInteger(row, "RawLink2Index"),
                    row.RequiredValue("Link2Resolution"),
                    ReadNullableCsvInteger(row, "Room2Instance"));
            }

            internal void ValidateClientSafeUnavailable(string source)
            {
                if (this.RawLink1Index.HasValue
                    || this.Room1Instance.HasValue
                    || this.RawLink2Index.HasValue
                    || this.Room2Instance.HasValue
                    || !string.Equals(
                        this.Link1Resolution,
                        "unavailable_not_read_for_client_safety",
                        StringComparison.Ordinal)
                    || !string.Equals(
                        this.Link2Resolution,
                        "unavailable_not_read_for_client_safety",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        source
                        + " door "
                        + this.Identity
                        + " must keep raw and resolved link values null with explicit client-safe unavailable status.");
                }
            }

            internal void RequireSameLinkEvidence(CanonicalDoorEvidence observed, string source)
            {
                if (observed == null
                    || this.RawLink1Index != observed.RawLink1Index
                    || !string.Equals(this.Link1Resolution, observed.Link1Resolution, StringComparison.Ordinal)
                    || this.Room1Instance != observed.Room1Instance
                    || this.RawLink2Index != observed.RawLink2Index
                    || !string.Equals(this.Link2Resolution, observed.Link2Resolution, StringComparison.Ordinal)
                    || this.Room2Instance != observed.Room2Instance)
                {
                    throw new InvalidDataException(
                        source + " door " + this.Identity + " client-safe unavailable link evidence does not match canonical geometry.");
                }

                observed.ValidateClientSafeUnavailable(source);
            }

            private static int? ReadNullableCsvInteger(CsvRow row, string column)
            {
                string value = row.Value(column);
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                int result;
                if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out result))
                {
                    throw new InvalidDataException(
                        "PF127 door-state " + column + " must be an integer or empty.");
                }

                return result;
            }

        }

        private sealed class DoorBatch
        {
            internal DoorBatch(DoorBatchKey key)
            {
                this.Key = key;
                this.DoorIdentities = new HashSet<string>(StringComparer.Ordinal);
            }

            internal DoorBatchKey Key { get; private set; }

            internal HashSet<string> DoorIdentities { get; private set; }

            internal bool IsComplete { get; set; }

            internal string IncompleteReason { get; set; }
        }

        private struct DoorBatchKey : IEquatable<DoorBatchKey>
        {
            internal DoorBatchKey(string trigger, long evidenceBatchId, int revision)
            {
                this.Trigger = trigger;
                this.EvidenceBatchId = evidenceBatchId;
                this.Revision = revision;
            }

            internal string Trigger { get; private set; }

            internal long EvidenceBatchId { get; private set; }

            internal int Revision { get; private set; }

            public bool Equals(DoorBatchKey other)
            {
                return this.EvidenceBatchId == other.EvidenceBatchId
                       && this.Revision == other.Revision
                       && string.Equals(this.Trigger, other.Trigger, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is DoorBatchKey && this.Equals((DoorBatchKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + this.EvidenceBatchId.GetHashCode();
                    hash = (hash * 31) + this.Revision;
                    hash = (hash * 31) + (this.Trigger ?? string.Empty).GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class CsvDocument
        {
            private CsvDocument(IList<string> headers, IList<CsvRow> rows)
            {
                this.Headers = headers;
                this.Rows = rows;
            }

            internal IList<string> Headers { get; private set; }

            internal IList<CsvRow> Rows { get; private set; }

            internal static CsvDocument Read(string path)
            {
                var parsed = new List<IList<string>>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length > 0)
                        {
                            parsed.Add(ParseCsvLine(line));
                        }
                    }
                }

                if (parsed.Count < 2)
                {
                    throw new InvalidDataException(
                        Path.GetFileName(path) + " must contain a header and evidence rows.");
                }

                IList<string> headers = parsed[0];
                if (headers.Count == 0 || headers.Distinct(StringComparer.Ordinal).Count() != headers.Count)
                {
                    throw new InvalidDataException(
                        Path.GetFileName(path) + " has an empty or duplicate CSV header.");
                }

                var rows = new List<CsvRow>();
                for (int index = 1; index < parsed.Count; index++)
                {
                    if (parsed[index].Count != headers.Count)
                    {
                        throw new InvalidDataException(
                            Path.GetFileName(path)
                            + " row "
                            + (index + 1).ToString(CultureInfo.InvariantCulture)
                            + " has "
                            + parsed[index].Count.ToString(CultureInfo.InvariantCulture)
                            + " columns; expected "
                            + headers.Count.ToString(CultureInfo.InvariantCulture)
                            + ".");
                    }

                    rows.Add(new CsvRow(path, index + 1, headers, parsed[index]));
                }

                return new CsvDocument(headers, rows);
            }

            internal void RequireColumns(params string[] columns)
            {
                var available = new HashSet<string>(this.Headers, StringComparer.Ordinal);
                foreach (string column in columns)
                {
                    if (!available.Contains(column))
                    {
                        throw new InvalidDataException(
                            "PF127 evidence CSV is missing required column " + column + ".");
                    }
                }
            }

            private static IList<string> ParseCsvLine(string line)
            {
                var fields = new List<string>();
                var current = new StringBuilder();
                bool quoted = false;
                for (int index = 0; index < line.Length; index++)
                {
                    char character = line[index];
                    if (quoted)
                    {
                        if (character == '"')
                        {
                            if (index + 1 < line.Length && line[index + 1] == '"')
                            {
                                current.Append('"');
                                index++;
                            }
                            else
                            {
                                quoted = false;
                            }
                        }
                        else
                        {
                            current.Append(character);
                        }
                    }
                    else if (character == ',' )
                    {
                        fields.Add(current.ToString());
                        current.Length = 0;
                    }
                    else if (character == '"' && current.Length == 0)
                    {
                        quoted = true;
                    }
                    else
                    {
                        current.Append(character);
                    }
                }

                if (quoted)
                {
                    throw new InvalidDataException("PF127 evidence CSV has an unterminated quoted field.");
                }

                fields.Add(current.ToString());
                return fields;
            }
        }

        private sealed class CsvRow
        {
            private readonly string path;
            private readonly int rowNumber;
            private readonly Dictionary<string, string> values;

            internal CsvRow(
                string path,
                int rowNumber,
                IEnumerable<string> headers,
                IEnumerable<string> fields)
            {
                this.path = path;
                this.rowNumber = rowNumber;
                this.values = headers.Zip(
                    fields,
                    (header, field) => new KeyValuePair<string, string>(header, field))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            }

            internal string Value(string column)
            {
                string value;
                return this.values.TryGetValue(column, out value) ? value : string.Empty;
            }

            internal string RequiredValue(string column)
            {
                string value = this.Value(column);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw this.Error(column + " is required");
                }

                return value;
            }

            internal int RequiredInteger(string column)
            {
                int value;
                if (!int.TryParse(
                    this.RequiredValue(column),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
                {
                    throw this.Error(column + " is not an integer");
                }

                return value;
            }

            internal int RequiredPositiveInteger(string column)
            {
                int value = this.RequiredInteger(column);
                if (value <= 0)
                {
                    throw this.Error(column + " must be positive");
                }

                return value;
            }

            internal long RequiredPositiveLong(string column)
            {
                long value;
                if (!long.TryParse(
                    this.RequiredValue(column),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value)
                    || value <= 0)
                {
                    throw this.Error(column + " must be a positive integer");
                }

                return value;
            }

            internal double RequiredFiniteDouble(string column)
            {
                double value;
                if (!double.TryParse(
                    this.RequiredValue(column),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                    || double.IsNaN(value)
                    || double.IsInfinity(value))
                {
                    throw this.Error(column + " must be a finite number");
                }

                return value;
            }

            internal bool RequiredBoolean(string column)
            {
                bool value;
                if (!bool.TryParse(this.RequiredValue(column), out value))
                {
                    throw this.Error(column + " must be true or false");
                }

                return value;
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException(
                    Path.GetFileName(this.path)
                    + " row "
                    + this.rowNumber.ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + message
                    + ".");
            }
        }
    }
}
