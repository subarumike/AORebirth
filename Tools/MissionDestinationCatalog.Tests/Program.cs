namespace AORebirth.MissionDestinationCatalogTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;
    using Utility.GameData.Missions;

    internal static class Program
    {
        private const string PlacementsFile = "MissionEntrancePlacements.json";
        private const string ObservedFile = "ObservedMissionDestinations.json";
        private const string ManifestFile = "MissionDestinationCatalogManifest.json";
        private static readonly string Root = Directory.GetCurrentDirectory();
        private static readonly string GameDataRoot = Path.Combine(Root, "AORebirth", "GameData");
        private static readonly string ContentRoot = Path.Combine(GameDataRoot, "Missions", "Destinations");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
        private static readonly List<string> Failures = new List<string>();
        private static int passed;

        private static int Main()
        {
            try
            {
                var catalog = MissionDestinationCatalog.Load(GameDataRoot, Root);
                Run("full and observed counts", () => Counts(catalog));
                Run("every complete identity and exact bit coordinate lookup", () => ExactLookups(catalog));
                Run("PF505 anchors and unsigned identities", () => Anchors(catalog));
                Run("incorrect bits have no rounded fallback", () => IncorrectBits(catalog));
                Run("positive and negative zero coordinate bits remain distinct", () => SignedZero(catalog));
                Run("identical cross-playfield coordinates remain distinct", () => CrossPlayfield(catalog));
                Run("repeated names preserve every placement", () => RepeatedNames(catalog));
                Run("Latin-1 raw name provenance", () => RawName(catalog));
                Run("every observed metadata lookup and unobserved classification", () => Evidence(catalog));
                Run("deeply immutable catalog and evidence collections", () => Immutability(catalog));
                Run("standalone copied catalog needs no research corpus", Standalone);
                Run("duplicate placement identity rejected", () => Reject(PlacementsFile, doc =>
                {
                    var rows = Rows(doc, "Placements");
                    rows[1]["IdentityType"] = rows[0]["IdentityType"];
                    rows[1]["IdentityInstance"] = rows[0]["IdentityInstance"];
                }));
                Run("ambiguous exact coordinate key rejected", () => Reject(PlacementsFile, doc =>
                {
                    var rows = Rows(doc, "Placements");
                    foreach (var key in new[] { "PlayfieldId", "LocalX", "LocalY", "LocalZ", "LocalXBits", "LocalYBits", "LocalZBits" })
                        rows[1][key] = rows[0][key];
                }));
                Run("unsupported placement schema rejected", () => Reject(PlacementsFile, doc => doc["SchemaVersion"] = 999));
                Run("unsupported observed schema rejected", () => Reject(ObservedFile, doc => doc["SchemaVersion"] = 999));
                Run("unsupported manifest schema rejected", () => Reject(ManifestFile, doc => doc["SchemaVersion"] = 999));
                Run("wrong catalog kind rejected", () => Reject(PlacementsFile, doc => doc["CatalogKind"] = "ELIGIBLE_POOL"));
                Run("placement row count mismatch rejected", () => Reject(PlacementsFile, doc =>
                    doc["Placements"] = Rows(doc, "Placements").Skip(1).ToArray()));
                Run("manifest count contradiction rejected", () => Reject(ManifestFile, doc => doc["FullPlacementCount"] = 2241));
                Run("unknown observed identity rejected", () => Reject(ObservedFile, doc => Rows(doc, "Destinations")[0]["IdentityInstance"] = 1));
                Run("duplicate observed identity rejected", () => Reject(ObservedFile, doc =>
                {
                    var rows = Rows(doc, "Destinations");
                    rows[1]["IdentityType"] = rows[0]["IdentityType"];
                    rows[1]["IdentityInstance"] = rows[0]["IdentityInstance"];
                }));
                Run("observed count mismatch rejected", () => Reject(ObservedFile, doc =>
                    doc["Destinations"] = Rows(doc, "Destinations").Skip(1).ToArray()));
                Run("observed playfield count mismatch rejected", () => Reject(ManifestFile, doc => doc["ObservedPlayfieldCount"] = 23));
                Run("bit pattern and numeric coordinate contradiction rejected", () => Reject(PlacementsFile, doc =>
                {
                    var row = Rows(doc, "Placements")[0];
                    row["LocalXBits"] = Convert.ToUInt32(row["LocalXBits"], CultureInfo.InvariantCulture) ^ 1U;
                }));
                Run("nonfinite binary32 coordinate rejected", () => Reject(PlacementsFile, doc =>
                {
                    var row = Rows(doc, "Placements")[0];
                    row["LocalX"] = 1E100;
                    row["LocalXBits"] = 0x7F800000U;
                }));
                Run("nonfinite binary32 rotation rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["RotationComponent0"] = 1E100));
                Run("sub-ULP coordinate rounding rejected", () => Reject(PlacementsFile, doc =>
                {
                    var row = Rows(doc, "Placements")[0];
                    row["LocalX"] = Convert.ToDouble(row["LocalX"], CultureInfo.InvariantCulture) + 1E-10;
                }));
                Run("sub-ULP rotation rounding rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["RotationComponent0"] = 1.0000000001));
                Run("zero playfield rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["PlayfieldId"] = 0));
                Run("fabricated operational entrance key rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["OperationalEntranceKey"] = 0));
                Run("fabricated effective BD rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["EffectiveStatBd"] = 0));
                Run("missing explicit operational key rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0].Remove("OperationalEntranceKey")));
                Run("null provenance rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["NameProvenance"] = null));
                Run("descriptive name and raw bytes contradiction rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["DisplayName"] = "wrong name"));
                Run("ineligible classification rejected", () => Reject(PlacementsFile, doc => Rows(doc, "Placements")[0]["ObservationClassification"] = "INELIGIBLE"));
                Run("missing raw promotion rejected", () => Reject(ManifestFile, doc => doc["MissingRawOffersPromoted"] = 1));
                Run("cohort uniqueness assertion rejected", () => Reject(ManifestFile, doc => doc["DestinationUniquenessWithinCohortRequired"] = true));
                Run("observed count hierarchy contradiction rejected", () => Reject(ObservedFile, doc =>
                {
                    var row = Rows(doc, "Destinations")[0];
                    row["RequestCount"] = Convert.ToInt32(row["ObservationCount"], CultureInfo.InvariantCulture) + 1;
                }));
                Run("null evidence collection rejected", () => Reject(ObservedFile, doc => Rows(doc, "Destinations")[0]["ObservedMissionTypes"] = null));
                Run("manifest payload hash mismatch rejected", () => Reject(ManifestFile, doc =>
                    Rows(doc, "Files")[0]["Sha256"] = new string('0', 64)));
                Run("development source hash mismatch rejected", () => Reject(ManifestFile, doc =>
                    Rows(doc, "Sources")[0]["Sha256"] = new string('0', 64), true));
                Run("manifest source path escape rejected", () => Reject(ManifestFile, doc => Rows(doc, "Sources")[0]["Path"] = "../outside.json"));
                Run("malformed JSON rejected", MalformedJson);
                Run("valid whitespace and escaped structural characters accepted", LexicalStrings);
                Run("incomplete root document rejected", () => RejectRaw(PlacementsFile, text =>
                    text.Substring(0, text.LastIndexOf('}'))));
                Run("trailing second JSON document rejected", () => RejectRaw(PlacementsFile, text => text + "{}"));
                Run("duplicate contradictory JSON member rejected", () => RejectRaw(PlacementsFile, text =>
                    "{\"SchemaVersion\":999," + text.Substring(1)));
                if (Failures.Count > 0)
                {
                    foreach (var failure in Failures)
                        Console.Error.WriteLine(failure);
                    return 1;
                }
                Console.WriteLine("PASS: " + passed + " catalog loader tests");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error);
                return 1;
            }
        }

        private static void Run(string name, Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                Failures.Add("FAIL: " + name + ": " + error);
                return;
            }
            passed++;
        }

        private static void Counts(MissionDestinationCatalog catalog)
        {
            Require(catalog.Count == 2242 && catalog.Placements.Count == 2242, "full count");
            Require(catalog.Placements.Select(p => p.Identity).Distinct().Count() == 2242, "unique identities");
            Require(catalog.ObservedDestinations.Count == 812, "observed count");
            Require(catalog.ObservedDestinations.Select(e => catalog.GetByIdentity(e.IdentityType, e.IdentityInstance).PlayfieldId).Distinct().Count() == 22, "observed playfields");
            Require(catalog.ObservedDestinations.Sum(e => e.ObservationCount) == 92830, "independent observations");
        }

        private static void ExactLookups(MissionDestinationCatalog catalog)
        {
            foreach (var placement in catalog.Placements)
            {
                MissionEntrancePlacement actual;
                Require(ReferenceEquals(placement, catalog.GetByIdentity(placement.IdentityType, placement.IdentityInstance)), "identity lookup");
                Require(catalog.TryGetByIdentity(placement.IdentityType, placement.IdentityInstance, out actual) && ReferenceEquals(placement, actual), "try identity");
                Require(catalog.TryGetByExactWorldPos(placement.PlayfieldId, placement.LocalXBits, placement.LocalYBits, placement.LocalZBits, out actual) && ReferenceEquals(placement, actual), "exact bits lookup");
                Require(Bits(placement.LocalX) == placement.LocalXBits && Bits(placement.LocalY) == placement.LocalYBits && Bits(placement.LocalZ) == placement.LocalZBits, "binary32 preservation");
                Require(catalog.GetPlacementsForPlayfield(placement.PlayfieldId).Contains(placement), "playfield lookup");
            }
            MissionEntrancePlacement absent;
            Require(!catalog.TryGetByIdentity(1, 1, out absent) && absent == null, "unknown complete identity");
            Require(catalog.GetPlacementsForPlayfield(int.MaxValue).Count == 0, "unknown playfield");
        }

        private static void Anchors(MissionDestinationCatalog catalog)
        {
            var names = new[] { "Central Desert Den", "South Desert Den", "Mantis Hive" };
            var instances = new[] { 0xC00001F9U, 0xC00101F9U, 0xC01201F9U };
            for (var index = 0; index < names.Length; index++)
            {
                var row = catalog.GetByIdentity(56006, instances[index]);
                Require(row.PlayfieldId == 505 && row.DisplayName == names[index], "PF505 anchor");
                Require(row.IdentityInstance > int.MaxValue, "unsigned instance retained");
            }
        }

        private static void IncorrectBits(MissionDestinationCatalog catalog)
        {
            var anchor = catalog.GetByIdentity(56006, 0xC00001F9U);
            MissionEntrancePlacement absent;
            Require(!catalog.TryGetByExactWorldPos(anchor.PlayfieldId, anchor.LocalXBits ^ 1U, anchor.LocalYBits, anchor.LocalZBits, out absent) && absent == null, "one bit must not match");
            Require(!catalog.TryGetByExactWorldPos(int.MaxValue, anchor.LocalXBits, anchor.LocalYBits, anchor.LocalZBits, out absent), "wrong playfield must not match");
        }

        private static void CrossPlayfield(MissionDestinationCatalog catalog)
        {
            var group = catalog.Placements.GroupBy(p => Tuple.Create(p.LocalXBits, p.LocalYBits, p.LocalZBits,
                Bits(p.RotationComponent0), Bits(p.RotationComponent1), Bits(p.RotationComponent2), Bits(p.RotationComponent3)))
                .First(g => g.Select(p => p.PlayfieldId).Distinct().Count() > 1);
            Require(group.Select(p => p.Identity).Distinct().Count() == group.Count(), "distinct identities");
            foreach (var expected in group)
            {
                MissionEntrancePlacement actual;
                Require(catalog.TryGetByExactWorldPos(expected.PlayfieldId, expected.LocalXBits, expected.LocalYBits, expected.LocalZBits, out actual), "cross playfield lookup");
                Require(actual.Identity.Equals(expected.Identity), "playfield participates in coordinate key");
            }
        }

        private static void SignedZero(MissionDestinationCatalog catalog)
        {
            var placement = catalog.Placements.First(p => p.LocalYBits == 0);
            MissionEntrancePlacement absent;
            Require(!catalog.TryGetByExactWorldPos(placement.PlayfieldId, placement.LocalXBits, 0x80000000U, placement.LocalZBits, out absent), "negative zero must not alias positive zero");
        }

        private static void RepeatedNames(MissionDestinationCatalog catalog)
        {
            var family = catalog.Placements.Where(p => p.DisplayName == "a building").ToArray();
            Require(family.Length == 188 && family.Select(p => p.Identity).Distinct().Count() == 188, "large repeated name family");
            Require(family.Count(p => catalog.IsObservedRandomMissionDestination(p.Identity)) == 85, "observed name family");
            Require(family.Where(p => catalog.IsObservedRandomMissionDestination(p.Identity)).Sum(p => catalog.GetObservedEvidence(p.Identity).ObservationCount) == 26631, "name-family evidence aggregate");
        }

        private static void RawName(MissionDestinationCatalog catalog)
        {
            var placement = catalog.GetByIdentity(56006, 0xC0000280U);
            Require(placement.DisplayName == "\u00c6nima HQ" && placement.RawNameHex == "c66e696d61204851", "source raw name");
            Require(placement.NameProvenance.Encoding.Contains("Latin-1"), "byte-preserving encoding provenance");
        }

        private static void Evidence(MissionDestinationCatalog catalog)
        {
            var unobserved = 0;
            foreach (var placement in catalog.Placements)
            {
                Require(placement.Classification == "CLIENT_ACGENTRANCE_PLACEMENT", "placement classification");
                Require(placement.OperationalEntranceKey == null && placement.EffectiveStatBd == null, "unresolved operational keys");
                var evidence = catalog.GetObservedEvidence(placement.Identity);
                Require((evidence != null) == catalog.IsObservedRandomMissionDestination(placement.Identity), "evidence membership");
                if (evidence == null)
                {
                    unobserved++;
                    Require(placement.ObservationClassification == "CLIENT_ACGENTRANCE_NOT_YET_OBSERVED_IN_RANDOM_MISSION_CAPTURE", "unobserved is not ineligible");
                    continue;
                }
                Require(evidence.Identity.Equals(placement.Identity), "joined evidence identity");
                Require(placement.ObservationClassification == "OBSERVED_RANDOM_MISSION_DESTINATION", "observed classification");
                Require(evidence.ObservationCount >= evidence.RequestCount && evidence.RequestCount >= evidence.SessionCount, "aggregate hierarchy");
                Require(evidence.CohortCount <= evidence.ObservationCount && evidence.SessionCount > 0, "cohort count");
                Require(evidence.ObservedFactionSides.SequenceEqual(new[] { "Omni" }) && evidence.ObservedFactionSideValues.SequenceEqual(new[] { 2 }), "observed faction association");
                Require(evidence.FactionEvidenceClassification == "OBSERVED_WITH_OMNI", "faction is not an exclusion rule");
                Require(evidence.ObservedExpectedMissionQls.Count > 0 && evidence.ObservedMissionTypes.Count > 0, "observed metadata");
            }
            Require(unobserved == 1430, "all unobserved placements retained");
            Require(catalog.GetObservedEvidence(new MissionPlacementIdentity(1, 1)) == null, "unknown evidence absent");
        }

        private static void Immutability(MissionDestinationCatalog catalog)
        {
            RejectMutation(catalog.Placements);
            RejectMutation(catalog.ObservedDestinations);
            RejectMutation(catalog.GetPlacementsForPlayfield(505));
            foreach (var evidence in catalog.ObservedDestinations)
            {
                RejectMutation(evidence.ObservedExpectedMissionQls);
                RejectMutation(evidence.ObservedCharacterLevels);
                RejectMutation(evidence.ObservedMissionTypes);
                RejectMutation(evidence.ObservedTerminalPlayfields);
                RejectMutation(evidence.ObservedFactionSides);
                RejectMutation(evidence.ObservedFactionSideValues);
            }
            foreach (var type in new[] { typeof(MissionEntrancePlacement), typeof(ObservedMissionDestinationEvidence),
                typeof(MissionPlacementNameProvenance), typeof(MissionPlacementContainer), typeof(MissionPlacementIdentity) })
            {
                Require(!type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(p => p.GetSetMethod() != null), "public mutable model property");
                Require(!type.GetFields(BindingFlags.Instance | BindingFlags.Public).Any(f => !f.IsInitOnly), "public mutable model field");
            }
        }

        private static void RejectMutation<T>(IReadOnlyList<T> values)
        {
            var mutable = values as IList<T>;
            if (mutable == null)
                return;
            var addRejected = false;
            try { mutable.Add(default(T)); }
            catch (NotSupportedException) { addRejected = true; }
            Require(addRejected, "Collection accepted an appended item");
            if (values.Count == 0)
                return;
            var replacementRejected = false;
            try { mutable[0] = default(T); }
            catch (NotSupportedException) { replacementRejected = true; }
            Require(replacementRejected, "Collection accepted item replacement");
        }

        private static void Standalone()
        {
            using (var fixture = new Fixture())
            {
                var standalone = MissionDestinationCatalog.Load(fixture.GameDataRoot);
                Counts(standalone);
                Require(Directory.GetFiles(fixture.GameDataRoot, "*", SearchOption.AllDirectories).Length == 3, "runtime consumes only three catalog files");
            }
        }

        private static void Reject(string file, Action<Dictionary<string, object>> mutation, bool validateEvidence = false)
        {
            using (var fixture = new Fixture())
            {
                var path = Path.Combine(fixture.ContentRoot, file);
                var document = Read(path);
                mutation(document);
                Write(path, document);
                if (file != ManifestFile)
                    fixture.RefreshPayloadHashes();
                ExpectRejected(() => MissionDestinationCatalog.Load(fixture.GameDataRoot, validateEvidence ? Root : null));
            }
        }

        private static void MalformedJson()
        {
            using (var fixture = new Fixture())
            {
                File.WriteAllText(Path.Combine(fixture.ContentRoot, PlacementsFile), "{malformed", new UTF8Encoding(false));
                fixture.RefreshPayloadHashes();
                ExpectRejected(() => MissionDestinationCatalog.Load(fixture.GameDataRoot));
            }
        }

        private static void RejectRaw(string file, Func<string, string> mutation)
        {
            using (var fixture = new Fixture())
            {
                var path = Path.Combine(fixture.ContentRoot, file);
                File.WriteAllText(path, mutation(File.ReadAllText(path, Encoding.UTF8)), new UTF8Encoding(false));
                fixture.RefreshPayloadHashes();
                ExpectRejected(() => MissionDestinationCatalog.Load(fixture.GameDataRoot));
            }
        }

        private static void LexicalStrings()
        {
            using (var fixture = new Fixture())
            {
                const string name = "a {room} [\"path\\door\"]";
                var path = Path.Combine(fixture.ContentRoot, PlacementsFile);
                var document = Read(path);
                var row = Rows(document, "Placements")[0];
                row["DisplayName"] = name;
                row["RawNameHex"] = BitConverter.ToString(Encoding.ASCII.GetBytes(name)).Replace("-", string.Empty).ToLowerInvariant();
                File.WriteAllText(path, "\r\n\t" + Json.Serialize(document) + "\t\r\n ", new UTF8Encoding(false));
                fixture.RefreshPayloadHashes();
                var catalog = MissionDestinationCatalog.Load(fixture.GameDataRoot);
                var placement = catalog.GetByIdentity(Convert.ToUInt32(row["IdentityType"], CultureInfo.InvariantCulture),
                    Convert.ToUInt32(row["IdentityInstance"], CultureInfo.InvariantCulture));
                Require(placement.DisplayName == name, "JSON string structural characters were not preserved");
            }
        }

        private static void ExpectRejected(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Malformed catalog was not rejected as invalid data");
        }

        private static Dictionary<string, object>[] Rows(Dictionary<string, object> document, string key)
        {
            return ((IEnumerable)document[key]).Cast<Dictionary<string, object>>().ToArray();
        }

        private static Dictionary<string, object> Read(string path)
        {
            return Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
        }

        private static void Write(string path, Dictionary<string, object> document)
        {
            File.WriteAllText(path, Json.Serialize(document), new UTF8Encoding(false));
        }

        private static uint Bits(float number)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(number), 0);
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "AORebirthMissionCatalogTests-" + Guid.NewGuid().ToString("N"));

            internal Fixture()
            {
                GameDataRoot = Path.Combine(root, "GameData");
                ContentRoot = Path.Combine(GameDataRoot, "Missions", "Destinations");
                Directory.CreateDirectory(ContentRoot);
                foreach (var name in new[] { PlacementsFile, ObservedFile, ManifestFile })
                    File.Copy(Path.Combine(Program.ContentRoot, name), Path.Combine(ContentRoot, name));
            }

            internal string GameDataRoot { get; }
            internal string ContentRoot { get; }

            internal void RefreshPayloadHashes()
            {
                var path = Path.Combine(ContentRoot, ManifestFile);
                var manifest = Read(path);
                foreach (var file in Rows(manifest, "Files"))
                    file["Sha256"] = Hash(Path.Combine(ContentRoot, (string)file["Path"]));
                Write(path, manifest);
            }

            public void Dispose()
            {
                // The fixture owns this freshly generated path beneath the system temporary directory.
                var resolved = Path.GetFullPath(root);
                var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("AORebirthMissionCatalogTests-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Fixture cleanup escaped its temporary directory");
                Directory.Delete(resolved, true);
            }
        }
    }
}
