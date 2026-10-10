namespace Utility.GameData.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Security.Cryptography;
    using System.Xml;

    /// <summary>
    /// Exact placement and bounded captured-condition lookup. A roll may reference the same placement
    /// repeatedly: DESTINATION_UNIQUENESS_WITHIN_COHORT_REQUIRED=NO.
    /// </summary>
    public sealed class MissionDestinationCatalog
    {
        public const string ClientPlacementClassification = "CLIENT_ACGENTRANCE_PLACEMENT";
        public const string ObservedClassification = "OBSERVED_RANDOM_MISSION_DESTINATION";
        public const string UnobservedClassification = "CLIENT_ACGENTRANCE_NOT_YET_OBSERVED_IN_RANDOM_MISSION_CAPTURE";

        private const string PlacementFile = "MissionEntrancePlacements.json";
        private const string ObservationFile = "ObservedMissionDestinations.json";
        private const string ManifestFile = "MissionDestinationCatalogManifest.json";
        private const string SelectionFile = "MissionDestinationSelection.json";
        private const string SelectionPolicy = "UNIFORM_DISTINCT_DESTINATIONS_WITH_REPLACEMENT_NOT_RETAIL_WEIGHTED";

        private readonly Dictionary<MissionPlacementIdentity, MissionEntrancePlacement> identities;
        private readonly Dictionary<CoordinateKey, MissionEntrancePlacement> coordinates;
        private readonly Dictionary<int, IReadOnlyList<MissionEntrancePlacement>> playfields;
        private readonly Dictionary<MissionPlacementIdentity, ObservedMissionDestinationEvidence> evidence;
        private readonly Dictionary<MissionDestinationCondition, IReadOnlyList<MissionEntrancePlacement>> conditions =
            new Dictionary<MissionDestinationCondition, IReadOnlyList<MissionEntrancePlacement>>();
        private readonly Dictionary<MissionPlacementIdentity, MissionDestinationWorldPosition> worldPositions =
            new Dictionary<MissionPlacementIdentity, MissionDestinationWorldPosition>();
        private static readonly IReadOnlyList<MissionEntrancePlacement> EmptyPlacements =
            new ReadOnlyCollection<MissionEntrancePlacement>(new MissionEntrancePlacement[0]);

        private MissionDestinationCatalog(
            CatalogManifestData manifest,
            PlacementData[] placementRows,
            ObservedDestinationData[] observedRows)
        {
            identities = new Dictionary<MissionPlacementIdentity, MissionEntrancePlacement>();
            coordinates = new Dictionary<CoordinateKey, MissionEntrancePlacement>();
            evidence = new Dictionary<MissionPlacementIdentity, ObservedMissionDestinationEvidence>();
            var placementList = new List<MissionEntrancePlacement>();
            var observedList = new List<ObservedMissionDestinationEvidence>();
            var observedPlayfields = new HashSet<int>();
            foreach (PlacementData row in placementRows)
            {
                ValidatePlacement(row);
                var placement = new MissionEntrancePlacement(row);
                var key = new CoordinateKey(row.PlayfieldId, row.LocalXBits, row.LocalYBits, row.LocalZBits);
                Require(!identities.ContainsKey(placement.Identity), "Duplicate complete placement identity: " + placement.Identity);
                Require(!coordinates.ContainsKey(key), "Ambiguous exact coordinate key for placement: " + placement.Identity);
                identities.Add(placement.Identity, placement);
                coordinates.Add(key, placement);
                placementList.Add(placement);
            }

            long observationCount = 0;
            foreach (ObservedDestinationData row in observedRows)
            {
                ValidateObserved(row);
                var identity = new MissionPlacementIdentity(row.IdentityType, row.IdentityInstance);
                MissionEntrancePlacement placement;
                Require(identities.TryGetValue(identity, out placement), "Observed identity absent from full catalog: " + identity);
                Require(!evidence.ContainsKey(identity), "Duplicate observed identity: " + identity);
                Require(placement.ObservationClassification == ObservedClassification, "Contradictory observed classification: " + identity);
                var observation = new ObservedMissionDestinationEvidence(row, placement.PlayfieldId);
                evidence.Add(identity, observation);
                observedList.Add(observation);
                observedPlayfields.Add(placement.PlayfieldId);
                observationCount += row.ObservationCount;
            }

            Require(observationCount == manifest.RawBackedObservationCount, "Raw-backed observation total mismatch.");
            Require(observedPlayfields.Count == manifest.ObservedPlayfieldCount, "Observed playfield count mismatch.");
            foreach (MissionEntrancePlacement placement in placementList)
            {
                Require(
                    (placement.ObservationClassification == ObservedClassification) == evidence.ContainsKey(placement.Identity),
                    "Placement observation classification contradicts observed catalog: " + placement.Identity);
            }

            Placements = placementList.AsReadOnly();
            ObservedDestinations = observedList.AsReadOnly();
            playfields = placementList.GroupBy(x => x.PlayfieldId).ToDictionary(
                x => x.Key, x => (IReadOnlyList<MissionEntrancePlacement>)x.ToList().AsReadOnly());
            SourceEvidenceCommit = manifest.SourceEvidenceCommit;
            CapturedConditions = new ReadOnlyCollection<MissionDestinationCondition>(new MissionDestinationCondition[0]);
        }

        public int Count { get { return Placements.Count; } }
        public IReadOnlyList<MissionEntrancePlacement> Placements { get; }
        public IReadOnlyList<ObservedMissionDestinationEvidence> ObservedDestinations { get; }
        public string SourceEvidenceCommit { get; }
        public bool DestinationUniquenessWithinCohortRequired { get { return false; } }
        public bool HasObservedSelection { get { return conditions.Count > 0; } }
        public int ObservedConditionCount { get { return conditions.Count; } }
        public int ObservedWorldPositionCount { get { return worldPositions.Count; } }
        public IReadOnlyList<MissionDestinationCondition> CapturedConditions { get; private set; }

        /// <summary>
        /// The caller supplies its already-selected GameData root. Normal loading reads only
        /// compact content only. Runtime callers require the fourth, captured-selection file;
        /// three-file foundation consumers retain lookup-only support. An invalid present file always fails.
        /// </summary>
        public static MissionDestinationCatalog Load(string gameDataRoot, string repositoryRoot = null, bool requireObservedSelection = false)
        {
            if (string.IsNullOrWhiteSpace(gameDataRoot))
                throw new ArgumentException("A GameData root is required.", nameof(gameDataRoot));

            string directory = Path.Combine(gameDataRoot, "Missions", "Destinations");
            var manifest = Read<CatalogManifestData>(File.ReadAllBytes(Path.Combine(directory, ManifestFile)), ManifestFile);
            ValidateManifest(manifest);
            byte[] placementBytes = File.ReadAllBytes(Path.Combine(directory, PlacementFile));
            byte[] observationBytes = File.ReadAllBytes(Path.Combine(directory, ObservationFile));
            CheckHash(placementBytes, manifest.Files.Single(x => x.Path == PlacementFile).Sha256, PlacementFile);
            CheckHash(observationBytes, manifest.Files.Single(x => x.Path == ObservationFile).Sha256, ObservationFile);

            if (repositoryRoot != null)
            {
                if (string.IsNullOrWhiteSpace(repositoryRoot))
                    throw new ArgumentException("A nonempty development repository root is required.", nameof(repositoryRoot));
                foreach (SourceHashData source in manifest.Sources.Concat(new[] { manifest.Generator }))
                {
                    CheckHash(File.ReadAllBytes(Path.Combine(repositoryRoot, source.Path.Replace('/', Path.DirectorySeparatorChar))),
                        source.Sha256, source.Path);
                }
            }

            var placements = Read<PlacementCatalogData>(placementBytes, PlacementFile);
            var observed = Read<ObservedCatalogData>(observationBytes, ObservationFile);
            Require(placements != null && placements.SchemaVersion == 1 && placements.CatalogKind == "CLIENT_ACGENTRANCE_PLACEMENTS",
                "Unsupported placement schema or catalog kind.");
            Require(observed != null && observed.SchemaVersion == 1 && observed.CatalogKind == "OBSERVED_RANDOM_MISSION_DESTINATIONS",
                "Unsupported observation schema or catalog kind.");
            Require(placements.Placements != null && placements.Placements.Length == manifest.FullPlacementCount,
                "Full placement row count mismatch.");
            Require(observed.Destinations != null && observed.Destinations.Length == manifest.ObservedDestinationCount,
                "Observed destination row count mismatch.");
            var catalog = new MissionDestinationCatalog(manifest, placements.Placements, observed.Destinations);
            string selectionPath = Path.Combine(directory, SelectionFile);
            if (File.Exists(selectionPath))
            {
                catalog.LoadSelection(Read<MissionSelectionData>(File.ReadAllBytes(selectionPath), SelectionFile), directory);
            }
            else if (requireObservedSelection)
                throw new InvalidDataException("Captured mission destination selection data is required: " + SelectionFile);
            return catalog;
        }

        public bool TryGetObservedDestinations(MissionDestinationCondition condition, out IReadOnlyList<MissionEntrancePlacement> destinations)
        {
            if (condition != null && conditions.TryGetValue(condition, out destinations))
                return true;
            destinations = EmptyPlacements;
            return false;
        }

        public bool TryGetWorldPosition(MissionPlacementIdentity identity, out MissionDestinationWorldPosition worldPosition)
        {
            return worldPositions.TryGetValue(identity, out worldPosition);
        }

        private void LoadSelection(MissionSelectionData document, string directory)
        {
            Require(document != null && document.Manifest != null && document.Payload != null, "Missing selection document.");
            MissionSelectionManifestData manifest = document.Manifest;
            Require(manifest.SchemaVersion == 1
                && manifest.SourceEvidenceCommit == "f07bb3c1a99218433e7d459c95ba29ccb22c36b2"
                && IsHex(manifest.PayloadSha256, 64), "Unsupported captured selection provenance.");
            Require(manifest.FoundationFiles != null && manifest.FoundationFiles.Length == 3,
                "Selection must identify all three foundation files.");
            var foundationPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceHashData source in manifest.FoundationFiles)
            {
                ValidateSource(source);
                Require((source.Path == PlacementFile || source.Path == ObservationFile || source.Path == ManifestFile)
                    && foundationPaths.Add(source.Path), "Invalid selection foundation file.");
                CheckHash(File.ReadAllBytes(Path.Combine(directory, source.Path)), source.Sha256, source.Path);
            }
            Require(manifest.Sources != null && manifest.Sources.Length > 0, "Selection source provenance is absent.");
            var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceHashData source in manifest.Sources)
            {
                ValidateSource(source);
                Require(sourcePaths.Add(source.Path), "Duplicate selection source provenance.");
            }
            ValidateSource(manifest.Generator);
            Require(manifest.Generator.Path == "Tools/mission_destination_selection.py", "Unexpected selection generator.");
            MissionSelectionPayloadData payload = document.Payload;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(MissionSelectionPayloadData),
                    new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 1000000 }).WriteObject(stream, payload);
                CheckHash(stream.ToArray(), manifest.PayloadSha256, "canonical selection payload");
            }
            Require(payload.SchemaVersion == 1 && payload.CatalogKind == "CAPTURED_MISSION_DESTINATION_SELECTION"
                && payload.SelectionPolicy == SelectionPolicy && payload.RawBackedObservationCount == 92830,
                "Unsupported captured selection contract.");
            Require(payload.WorldPositions != null && payload.WorldPositions.Length == 812
                && payload.Conditions != null && payload.Conditions.Length == 547, "Captured selection count mismatch.");
            foreach (MissionWorldPositionData row in payload.WorldPositions)
            {
                Require(row != null && row.PlayfieldIdentityType == 40016, "Invalid captured WorldPos row.");
                var identity = new MissionPlacementIdentity(row.IdentityType, row.IdentityInstance);
                Require(evidence.ContainsKey(identity) && !worldPositions.ContainsKey(identity),
                    "Duplicate or unobserved WorldPos identity: " + identity);
                worldPositions.Add(identity, new MissionDestinationWorldPosition(row));
            }
            var conditionList = new List<MissionDestinationCondition>();
            var selectedIdentities = new HashSet<MissionPlacementIdentity>();
            int associationCount = 0;
            foreach (MissionConditionData row in payload.Conditions)
            {
                Require(row != null && row.CharacterLevel >= 1 && row.CharacterLevel <= 220
                    && row.ExpectedMissionQl >= 1 && row.ExpectedMissionQl <= 250
                    && row.DifficultyDetent >= 1 && row.DifficultyDetent <= 11 && row.FactionSide >= 0
                    && row.Breed > 0 && row.Profession > 0 && row.TerminalPlayfieldId > 0
                    && row.TerminalIdentityType > 0 && row.TerminalIdentityInstance > 0
                    && row.SecondarySliderBytes != null && row.SecondarySliderBytes.Length == 6
                    && row.SecondarySliderBytes.All(x => x >= 0 && x <= 255), "Invalid captured condition.");
                Require(new[] { "FIND_ITEM", "FIND_PERSON", "KILL_PERSON", "REPAIR", "RETURN_ITEM" }.Contains(row.MissionType),
                    "Unknown captured mission type.");
                var condition = new MissionDestinationCondition(row.CharacterLevel, row.ExpectedMissionQl, row.DifficultyDetent,
                    row.FactionSide, row.Breed, row.Profession, row.TerminalPlayfieldId, row.TerminalIdentityType,
                    row.TerminalIdentityInstance, row.SecondarySliderBytes.Select(x => (byte)x).ToArray(), row.MissionType);
                Require(!conditions.ContainsKey(condition), "Duplicate captured joint condition.");
                Require(row.DestinationIdentities != null && row.DestinationIdentities.Length > 0, "Empty captured destination set.");
                var destinations = new List<MissionEntrancePlacement>();
                var unique = new HashSet<MissionPlacementIdentity>();
                foreach (MissionIdentityData destination in row.DestinationIdentities)
                {
                    Require(destination != null, "Null captured destination identity.");
                    var identity = new MissionPlacementIdentity(destination.IdentityType, destination.IdentityInstance);
                    Require(worldPositions.ContainsKey(identity) && unique.Add(identity), "Unobserved or duplicate condition destination.");
                    ObservedMissionDestinationEvidence observed = evidence[identity];
                    Require(observed.ObservedCharacterLevels.Contains(row.CharacterLevel)
                        && observed.ObservedExpectedMissionQls.Contains(row.ExpectedMissionQl)
                        && observed.ObservedFactionSideValues.Contains(row.FactionSide)
                        && observed.ObservedTerminalPlayfields.Contains(row.TerminalPlayfieldId)
                        && observed.ObservedMissionTypes.Contains(row.MissionType), "Condition contradicts foundation observations.");
                    destinations.Add(identities[identity]);
                    selectedIdentities.Add(identity);
                }
                associationCount += destinations.Count;
                conditions.Add(condition, destinations.AsReadOnly());
                conditionList.Add(condition);
            }
            Require(associationCount == 25296 && selectedIdentities.SetEquals(evidence.Keys),
                "Captured selection associations or identity coverage mismatch.");
            CapturedConditions = conditionList.AsReadOnly();
        }

        public MissionEntrancePlacement GetByIdentity(uint identityType, uint identityInstance)
        {
            return identities[new MissionPlacementIdentity(identityType, identityInstance)];
        }

        public bool TryGetByIdentity(uint identityType, uint identityInstance, out MissionEntrancePlacement placement)
        {
            return identities.TryGetValue(new MissionPlacementIdentity(identityType, identityInstance), out placement);
        }

        public bool TryGetByExactWorldPos(int playfieldId, uint xBits, uint yBits, uint zBits, out MissionEntrancePlacement placement)
        {
            return coordinates.TryGetValue(new CoordinateKey(playfieldId, xBits, yBits, zBits), out placement);
        }

        public IReadOnlyList<MissionEntrancePlacement> GetPlacementsForPlayfield(int playfieldId)
        {
            IReadOnlyList<MissionEntrancePlacement> result;
            return playfields.TryGetValue(playfieldId, out result) ? result : EmptyPlacements;
        }

        public bool IsObservedRandomMissionDestination(MissionPlacementIdentity identity)
        {
            return evidence.ContainsKey(identity);
        }

        public ObservedMissionDestinationEvidence GetObservedEvidence(MissionPlacementIdentity identity)
        {
            ObservedMissionDestinationEvidence result;
            return evidence.TryGetValue(identity, out result) ? result : null;
        }

        private static void ValidateManifest(CatalogManifestData manifest)
        {
            Require(manifest != null && manifest.SchemaVersion == 1, "Unsupported catalog manifest schema.");
            Require(IsHex(manifest.SourceEvidenceCommit, 40), "Invalid source evidence commit.");
            // Version 1 is the explicitly bounded accepted corpus, not an inferred eligibility pool.
            Require(manifest.FullPlacementCount == 2242 && manifest.ObservedDestinationCount == 812
                && manifest.ObservedPlayfieldCount == 22 && manifest.UnobservedPlacementCount == 1430
                && manifest.RawBackedObservationCount == 92830 && manifest.MissingRawOffersExcluded == 355
                && manifest.MissingRawOffersPromoted == 0 && manifest.OperationalEntranceKeysResolved == 0
                && !manifest.DestinationUniquenessWithinCohortRequired, "Contradictory version 1 corpus manifest.");
            Require(manifest.Files != null && manifest.Files.Length == 2 && manifest.Files.All(x => x != null),
                "Manifest must identify exactly two compact data files.");
            Require(manifest.Files.Count(x => x.Path == PlacementFile && x.RowCount == manifest.FullPlacementCount) == 1
                && manifest.Files.Count(x => x.Path == ObservationFile && x.RowCount == manifest.ObservedDestinationCount) == 1,
                "Unexpected or repeated manifest data file.");
            foreach (CatalogFileData file in manifest.Files)
            {
                Require(IsHex(file.Sha256, 64), "Invalid content hash: " + file.Path);
            }
            Require(manifest.Sources != null && manifest.Sources.Length > 0, "Source evidence hash inventory is absent.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceHashData source in manifest.Sources)
            {
                ValidateSource(source);
                Require(paths.Add(source.Path), "Duplicate source provenance path: " + source.Path);
            }
            ValidateSource(manifest.Generator);
            Require(manifest.Generator.Path == "Tools/mission_destination_catalog.py", "Unexpected catalog generator path.");
        }

        private static void ValidateSource(SourceHashData source)
        {
            Require(source != null && !string.IsNullOrWhiteSpace(source.Path), "Source provenance is absent.");
            Require(!Path.IsPathRooted(source.Path) && source.Path.IndexOf('\\') < 0 && source.Path.IndexOf(':') < 0
                && source.Path.Split('/').All(x => x.Length > 0 && x != "." && x != ".."),
                "Source provenance must be repository-relative: " + source.Path);
            Require(IsHex(source.Sha256, 64), "Invalid source provenance hash: " + source.Path);
        }

        private static void ValidatePlacement(PlacementData row)
        {
            Require(row != null, "Null placement row.");
            Require(row.IdentityType > 0 && row.IdentityInstance > 0, "Invalid complete placement identity.");
            Require(row.PlayfieldId > 0, "Invalid placement playfield.");
            ValidateCoordinate(row.LocalX, row.LocalXBits, "X");
            ValidateCoordinate(row.LocalY, row.LocalYBits, "Y");
            ValidateCoordinate(row.LocalZ, row.LocalZBits, "Z");
            ValidateBinary32(row.RotationComponent0, "rotation 0");
            ValidateBinary32(row.RotationComponent1, "rotation 1");
            ValidateBinary32(row.RotationComponent2, "rotation 2");
            ValidateBinary32(row.RotationComponent3, "rotation 3");
            Require(row.Classification == ClientPlacementClassification
                && (row.ObservationClassification == ObservedClassification || row.ObservationClassification == UnobservedClassification),
                "Unknown placement evidence classification.");
            Require(!row.OperationalEntranceKey.HasValue && !row.EffectiveStatBd.HasValue,
                "Operational entrance key and effective stat BD must remain unresolved nulls.");
            Require(!string.IsNullOrEmpty(row.DisplayName) && row.RawNameHex != null
                && row.RawNameHex.Length > 0 && row.RawNameHex.Length % 2 == 0 && IsHex(row.RawNameHex, row.RawNameHex.Length),
                "Missing or malformed placement name provenance.");
            var characters = new char[row.RawNameHex.Length / 2];
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = (char)byte.Parse(row.RawNameHex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            Require(new string(characters) == row.DisplayName, "Display name differs from byte-preserving raw name.");
            Require(row.NameProvenance != null && !string.IsNullOrWhiteSpace(row.NameProvenance.Encoding)
                && !string.IsNullOrWhiteSpace(row.NameProvenance.ResolutionPath)
                && row.NameProvenance.ResourceType > 0 && row.NameProvenance.ResourceInstance > 0 && row.NameProvenance.SourceOffset >= 0,
                "Incomplete name source provenance.");
            Require(row.TemplateInstance > 0 && row.SourcePlacementContainer != null
                && row.SourcePlacementContainer.ResourceType > 0 && row.SourcePlacementContainer.ResourceInstance > 0
                && row.SourceRecordOffset >= 0 && row.SourceRecordLength > 0 && IsHex(row.SourceRecordSha256, 64)
                && IsHex(row.SourceDatabaseSha256, 64), "Incomplete placement source provenance.");
        }

        private static void ValidateObserved(ObservedDestinationData row)
        {
            Require(row != null && row.IdentityType > 0 && row.IdentityInstance > 0, "Invalid observed destination identity.");
            Require(row.Classification == ObservedClassification, "Invalid observed destination classification.");
            Require(row.ObservationCount > 0 && row.RequestCount > 0 && row.CohortCount > 0 && row.SessionCount > 0
                && row.RequestCount <= row.ObservationCount && row.CohortCount <= row.ObservationCount
                && row.SessionCount <= row.RequestCount && row.SessionCount <= row.CohortCount,
                "Invalid observed evidence counts.");
            ValidateSet(row.ObservedExpectedMissionQls, "expected mission QLs", x => x > 0);
            ValidateSet(row.ObservedCharacterLevels, "character levels", x => x > 0);
            ValidateSet(row.ObservedTerminalPlayfields, "terminal playfields", x => x > 0);
            ValidateSet(row.ObservedMissionTypes, "mission types", x => !string.IsNullOrWhiteSpace(x));
            ValidateSet(row.ObservedFactionSides, "faction sides", x => !string.IsNullOrWhiteSpace(x));
            ValidateSet(row.ObservedFactionSideValues, "faction side values", x => x >= 0);
            Require(row.ObservedFactionSides.Length == 1 && row.ObservedFactionSides[0] == "Omni"
                && row.ObservedFactionSideValues.Length == 1 && row.ObservedFactionSideValues[0] == 2
                && row.FactionEvidenceClassification == "OBSERVED_WITH_OMNI", "Contradictory observed faction evidence.");
        }

        private static void ValidateSet<T>(T[] values, string label, Func<T, bool> valid)
        {
            Require(values != null && values.Length > 0 && values.All(valid)
                && new HashSet<T>(values).Count == values.Length, "Missing, invalid or repeated observed " + label + ".");
        }

        private static void ValidateCoordinate(double value, uint bits, string axis)
        {
            float decoded = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            ValidateBinary32(value, "local " + axis);
            Require(!float.IsNaN(decoded) && !float.IsInfinity(decoded) && value == (double)decoded
                && BitConverter.ToUInt32(BitConverter.GetBytes((float)value), 0) == bits,
                "Coordinate number and exact binary32 bits disagree for " + axis + ".");
        }

        private static void ValidateBinary32(double value, string label)
        {
            float single = (float)value;
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && !float.IsNaN(single)
                && !float.IsInfinity(single) && (double)single == value, "Invalid or rounded binary32 " + label + ".");
        }

        private static T Read<T>(byte[] bytes, string label)
        {
            RequireSingleDocument(bytes, label);
            try
            {
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max))
                {
                    var settings = new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 1000000 };
                    T result = (T)new DataContractJsonSerializer(typeof(T), settings).ReadObject(reader);
                    while (reader.Read())
                    {
                        Require(reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace,
                            "Unexpected content after catalog JSON: " + label);
                    }
                    return result;
                }
            }
            catch (SerializationException ex)
            {
                throw new InvalidDataException("Malformed catalog JSON: " + label, ex);
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("Malformed catalog JSON: " + label, ex);
            }
        }

        private static void RequireSingleDocument(byte[] bytes, string label)
        {
            // The BCL JSON reader stops at the first document. Check its boundary here;
            // DataContractJsonSerializer remains responsible for JSON grammar and member types.
            int start = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            while (start < bytes.Length && IsJsonWhitespace(bytes[start]))
            {
                start++;
            }
            Require(start < bytes.Length && bytes[start] == '{', "Expected one JSON catalog object: " + label);
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            for (int index = start; index < bytes.Length; index++)
            {
                byte current = bytes[index];
                if (quoted)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        quoted = false;
                    }
                    continue;
                }
                if (current == '"')
                {
                    quoted = true;
                }
                else if (current == '{' || current == '[')
                {
                    depth++;
                    Require(depth <= 128, "Excessive catalog JSON nesting: " + label);
                }
                else if (current == '}' || current == ']')
                {
                    depth--;
                    if (depth != 0)
                        continue;
                    Require(current == '}', "Expected catalog object closing delimiter: " + label);
                    for (int trailing = index + 1; trailing < bytes.Length; trailing++)
                    {
                        Require(IsJsonWhitespace(bytes[trailing]), "Unexpected content after catalog JSON: " + label);
                    }
                    return;
                }
            }
            throw new InvalidDataException("Incomplete catalog JSON object or string: " + label);
        }

        private static bool IsJsonWhitespace(byte value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        private static bool IsHex(string value, int length)
        {
            return value != null && value.Length == length && value.All(
                x => (x >= '0' && x <= '9') || (x >= 'a' && x <= 'f') || (x >= 'A' && x <= 'F'));
        }

        private static void CheckHash(byte[] bytes, string expected, string label)
        {
            using (SHA256 hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                Require(string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), "SHA-256 mismatch: " + label);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidDataException(message);
        }

        private readonly struct CoordinateKey : IEquatable<CoordinateKey>
        {
            private readonly int playfield;
            private readonly uint x;
            private readonly uint y;
            private readonly uint z;

            public CoordinateKey(int playfield, uint x, uint y, uint z)
            {
                this.playfield = playfield;
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public bool Equals(CoordinateKey other)
            {
                return playfield == other.playfield && x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is CoordinateKey && Equals((CoordinateKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return (((playfield * 397) ^ (int)x) * 397 ^ (int)y) * 397 ^ (int)z; }
            }
        }
    }
}
