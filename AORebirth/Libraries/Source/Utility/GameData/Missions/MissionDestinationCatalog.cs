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
    using System.Xml;

    /// <summary>
    /// Immutable entrance placements and destination pools loaded from editable runtime data.
    /// A cohort may reference the same placement repeatedly.
    /// </summary>
    public sealed class MissionDestinationCatalog
    {
        private const string PlacementFile = "MissionEntrancePlacements.json";
        private const string DestinationFile = "MissionDestinations.json";

        private static readonly IReadOnlyList<MissionEntrancePlacement> EmptyPlacements =
            new ReadOnlyCollection<MissionEntrancePlacement>(new MissionEntrancePlacement[0]);
        private readonly Dictionary<MissionPlacementIdentity, MissionEntrancePlacement> identities =
            new Dictionary<MissionPlacementIdentity, MissionEntrancePlacement>();
        private readonly Dictionary<CoordinateKey, MissionEntrancePlacement> coordinates =
            new Dictionary<CoordinateKey, MissionEntrancePlacement>();
        private readonly Dictionary<int, IReadOnlyList<MissionEntrancePlacement>> playfields;
        private readonly Dictionary<TerminalQlKey, IReadOnlyList<MissionEntrancePlacement>> terminalPools =
            new Dictionary<TerminalQlKey, IReadOnlyList<MissionEntrancePlacement>>();
        private readonly Dictionary<int, IReadOnlyList<MissionEntrancePlacement>> expectedMissionQls =
            new Dictionary<int, IReadOnlyList<MissionEntrancePlacement>>();
        private readonly Dictionary<MissionPlacementIdentity, MissionDestinationWorldPosition> worldPositions =
            new Dictionary<MissionPlacementIdentity, MissionDestinationWorldPosition>();
        private readonly HashSet<MissionPlacementIdentity> pooledIdentities = new HashSet<MissionPlacementIdentity>();

        private MissionDestinationCatalog(PlacementData[] placementRows, MissionDestinationPoolData[] poolRows)
        {
            var placementList = new List<MissionEntrancePlacement>();
            foreach (PlacementData row in placementRows)
            {
                ValidatePlacement(row);
                var placement = new MissionEntrancePlacement(row);
                var coordinate = new CoordinateKey(row.PlayfieldId, row.LocalXBits, row.LocalYBits, row.LocalZBits);
                Require(!identities.ContainsKey(placement.Identity), "Duplicate complete placement identity: " + placement.Identity);
                Require(!coordinates.ContainsKey(coordinate), "Ambiguous exact coordinate key for placement: " + placement.Identity);
                identities.Add(placement.Identity, placement);
                coordinates.Add(coordinate, placement);
                placementList.Add(placement);
                if (row.WorldPos != null)
                {
                    Require(row.WorldPos.PlayfieldIdentityType == 40016,
                        "Invalid WorldPos playfield identity type: " + placement.Identity);
                    worldPositions.Add(placement.Identity, new MissionDestinationWorldPosition(placement.Identity, row.WorldPos));
                }
            }

            foreach (MissionDestinationPoolData row in poolRows)
            {
                Require(row != null && row.TerminalPlayfieldId > 0 && row.ExpectedMissionQl > 0,
                    "Invalid destination pool terminal playfield or expected QL.");
                var key = new TerminalQlKey(row.TerminalPlayfieldId, row.ExpectedMissionQl);
                Require(!terminalPools.ContainsKey(key), "Duplicate terminal playfield and expected QL pool.");
                Require(row.DestinationIdentities != null && row.DestinationIdentities.Length > 0,
                    "Empty destination pool.");
                var pool = new List<MissionEntrancePlacement>();
                var unique = new HashSet<MissionPlacementIdentity>();
                foreach (MissionIdentityData destination in row.DestinationIdentities)
                {
                    Require(destination != null, "Null destination pool identity.");
                    var identity = new MissionPlacementIdentity(destination.IdentityType, destination.IdentityInstance);
                    MissionEntrancePlacement placement;
                    Require(identities.TryGetValue(identity, out placement), "Destination identity absent from placements: " + identity);
                    Require(worldPositions.ContainsKey(identity), "Destination identity has no WorldPos: " + identity);
                    Require(unique.Add(identity), "Duplicate destination identity in pool: " + identity);
                    pool.Add(placement);
                    pooledIdentities.Add(identity);
                }
                terminalPools.Add(key, pool.AsReadOnly());
            }

            foreach (var group in terminalPools.GroupBy(x => x.Key.ExpectedMissionQl))
            {
                var pool = group.SelectMany(x => x.Value).Select(x => x.Identity).Distinct()
                    .OrderBy(x => x.IdentityType).ThenBy(x => x.IdentityInstance)
                    .Select(x => identities[x]).ToList().AsReadOnly();
                expectedMissionQls.Add(group.Key, pool);
            }

            Placements = placementList.AsReadOnly();
            ObservedDestinations = pooledIdentities.OrderBy(x => x.IdentityType).ThenBy(x => x.IdentityInstance)
                .Select(x => identities[x]).ToList().AsReadOnly();
            ExpectedMissionQls = expectedMissionQls.Keys.OrderBy(x => x).ToList().AsReadOnly();
            playfields = placementList.GroupBy(x => x.PlayfieldId).ToDictionary(
                x => x.Key, x => (IReadOnlyList<MissionEntrancePlacement>)x.ToList().AsReadOnly());
        }

        public int Count { get { return Placements.Count; } }
        public IReadOnlyList<MissionEntrancePlacement> Placements { get; }
        public IReadOnlyList<MissionEntrancePlacement> ObservedDestinations { get; }
        public IReadOnlyList<int> ExpectedMissionQls { get; }
        public int TerminalPoolCount { get { return terminalPools.Count; } }
        public int ObservedWorldPositionCount { get { return worldPositions.Count; } }
        public bool DestinationUniquenessWithinCohortRequired { get { return false; } }

        /// <summary>
        /// Loads the two runtime catalogs from the caller's selected GameData root.
        /// Captures, analysis artifacts and source manifests are not runtime dependencies.
        /// </summary>
        public static MissionDestinationCatalog Load(string gameDataRoot)
        {
            if (string.IsNullOrWhiteSpace(gameDataRoot))
                throw new ArgumentException("A GameData root is required.", nameof(gameDataRoot));

            string directory = Path.Combine(gameDataRoot, "Missions", "Destinations");
            var placements = Read<PlacementCatalogData>(File.ReadAllBytes(Path.Combine(directory, PlacementFile)), PlacementFile);
            var destinations = Read<MissionDestinationData>(File.ReadAllBytes(Path.Combine(directory, DestinationFile)), DestinationFile);
            Require(placements != null && placements.SchemaVersion == 2 && placements.CatalogKind == "MISSION_ENTRANCE_PLACEMENTS",
                "Unsupported placement schema or catalog kind.");
            Require(destinations != null && destinations.SchemaVersion == 1 && destinations.CatalogKind == "MISSION_DESTINATIONS",
                "Unsupported destination schema or catalog kind.");
            Require(placements.Placements != null && placements.Placements.Length > 0, "Placement catalog is empty.");
            Require(destinations.Pools != null && destinations.Pools.Length > 0, "Destination catalog is empty.");
            return new MissionDestinationCatalog(placements.Placements, destinations.Pools);
        }

        /// <summary>
        /// Uses the terminal playfield's exact-QL pool when present, otherwise the union at that same QL.
        /// Missing QLs remain unsupported; no neighboring-QL or all-placement fallback is used.
        /// </summary>
        public bool TryGetDestinations(int terminalPlayfield, int expectedQl,
            out IReadOnlyList<MissionEntrancePlacement> destinations, out bool usedQlFallback)
        {
            usedQlFallback = false;
            if (terminalPools.TryGetValue(new TerminalQlKey(terminalPlayfield, expectedQl), out destinations))
                return true;
            if (expectedMissionQls.TryGetValue(expectedQl, out destinations))
            {
                usedQlFallback = true;
                return true;
            }
            destinations = EmptyPlacements;
            return false;
        }

        public bool TryGetObservedDestinations(int expectedMissionQl, out IReadOnlyList<MissionEntrancePlacement> destinations)
        {
            if (expectedMissionQls.TryGetValue(expectedMissionQl, out destinations))
                return true;
            destinations = EmptyPlacements;
            return false;
        }

        public bool TryGetWorldPosition(MissionPlacementIdentity identity, out MissionDestinationWorldPosition worldPosition)
        {
            return worldPositions.TryGetValue(identity, out worldPosition);
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
            return pooledIdentities.Contains(identity);
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
            Require(!string.IsNullOrEmpty(row.DisplayName) && row.RawNameHex != null
                && row.RawNameHex.Length > 0 && row.RawNameHex.Length % 2 == 0 && IsHex(row.RawNameHex, row.RawNameHex.Length),
                "Missing or malformed placement name bytes.");
            var characters = new char[row.RawNameHex.Length / 2];
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = (char)byte.Parse(row.RawNameHex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            Require(new string(characters) == row.DisplayName, "Display name differs from byte-preserving raw name.");
        }

        private readonly struct TerminalQlKey : IEquatable<TerminalQlKey>
        {
            private readonly int terminalPlayfield;

            public TerminalQlKey(int terminalPlayfield, int expectedMissionQl)
            {
                this.terminalPlayfield = terminalPlayfield;
                ExpectedMissionQl = expectedMissionQl;
            }

            public int ExpectedMissionQl { get; }

            public bool Equals(TerminalQlKey other)
            {
                return terminalPlayfield == other.terminalPlayfield && ExpectedMissionQl == other.ExpectedMissionQl;
            }

            public override bool Equals(object obj)
            {
                return obj is TerminalQlKey && Equals((TerminalQlKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return (terminalPlayfield * 397) ^ ExpectedMissionQl; }
            }
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
