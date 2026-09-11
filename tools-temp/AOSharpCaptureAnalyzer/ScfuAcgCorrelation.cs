namespace AOSharpCaptureAnalyzer
{
    #region Using declarations

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using AORebirth.CaptureProtocol;
    using AORebirth.Core.Playfields.OfficialPlacements;

    #endregion

    internal sealed class ScfuAcgObservation
    {
        internal string CapturedUtc { get; set; }

        internal string Direction { get; set; }

        internal string Sequence { get; set; }

        internal string Identity { get; set; }

        internal string Name { get; set; }

        internal int? PlayfieldId { get; set; }

        internal float PositionX { get; set; }

        internal float PositionY { get; set; }

        internal float PositionZ { get; set; }

        internal uint MonsterData { get; set; }

        internal bool IsNpc { get; set; }

        internal static ScfuAcgObservation From(
            RawScfuCaptureMetadata metadata,
            RawSimpleCharFullUpdate message)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            return new ScfuAcgObservation
            {
                CapturedUtc = metadata.CapturedUtc,
                Direction = metadata.Direction,
                Sequence = metadata.Sequence,
                Identity = message.Identity.ToString(),
                Name = message.Name,
                PlayfieldId = message.PlayfieldId,
                PositionX = message.Position.X,
                PositionY = message.Position.Y,
                PositionZ = message.Position.Z,
                MonsterData = message.MonsterData,
                IsNpc = message.Npc != null
            };
        }
    }

    internal sealed class ScfuAcgCorrelationSummary
    {
        internal int RowCount { get; set; }

        internal int ExactRecordCount { get; set; }

        internal int ExactHashCount { get; set; }

        internal int AmbiguousCount { get; set; }

        internal int UnmatchedNpcCount { get; set; }

        internal int NonNpcCount { get; set; }

        internal int UniqueHashCount { get; set; }

        internal string OutputPath { get; set; }
    }

    internal sealed class ScfuAcgCorrelationMatch
    {
        internal string Status { get; set; }

        internal IList<OfficialPlayfieldPlacement> Candidates { get; set; }

        internal string CanonicalAcgHashText { get; set; }
    }

    internal static class ScfuAcgCorrelation
    {
        private const string OutputFileName = "scfu-acg-correlation.csv";

        private static readonly Encoding OutputEncoding = new UTF8Encoding(false);

        internal static ScfuAcgCorrelationSummary Export(
            string captureFolder,
            IList<ScfuAcgObservation> observations)
        {
            if (string.IsNullOrWhiteSpace(captureFolder))
            {
                throw new ArgumentException("Capture folder is required.", "captureFolder");
            }

            if (observations == null)
            {
                throw new ArgumentNullException("observations");
            }

            string fullCaptureFolder = Path.GetFullPath(captureFolder);
            if (!Directory.Exists(fullCaptureFolder))
            {
                throw new DirectoryNotFoundException(
                    "Capture folder is missing: " + fullCaptureFolder);
            }

            string corpusRoot = ResolveRepositoryCorpusRoot();
            var catalog = new OfficialPlayfieldPlacementCatalog(corpusRoot);
            var placementsByPlayfield = new Dictionary<int, IList<OfficialPlayfieldPlacement>>();
            var summary = new ScfuAcgCorrelationSummary
            {
                RowCount = observations.Count,
                OutputPath = Path.Combine(fullCaptureFolder, OutputFileName)
            };
            var uniqueHashes = new HashSet<string>(StringComparer.Ordinal);
            string pendingPath = summary.OutputPath + ".pending";
            DeleteIfExists(pendingPath);

            try
            {
                using (var output = new StreamWriter(pendingPath, false, OutputEncoding))
                {
                    output.WriteLine(Header());
                    foreach (ScfuAcgObservation observation in observations)
                    {
                        IList<OfficialPlayfieldPlacement> placements =
                            GetPlacements(catalog, placementsByPlayfield, observation.PlayfieldId);
                        ScfuAcgCorrelationMatch match = Correlate(observation, placements);
                        Count(summary, match);
                        if (!string.IsNullOrEmpty(match.CanonicalAcgHashText))
                        {
                            uniqueHashes.Add(match.CanonicalAcgHashText);
                        }

                        output.WriteLine(FormatRow(observation, match));
                    }
                }

                summary.UniqueHashCount = uniqueHashes.Count;
                PromoteFile(pendingPath, summary.OutputPath);
                return summary;
            }
            catch
            {
                DeleteIfExists(pendingPath);
                throw;
            }
        }

        internal static ScfuAcgCorrelationMatch Correlate(
            ScfuAcgObservation observation,
            IList<OfficialPlayfieldPlacement> placements)
        {
            if (observation == null)
            {
                throw new ArgumentNullException("observation");
            }

            if (!observation.IsNpc)
            {
                return Match("NON_NPC", new List<OfficialPlayfieldPlacement>(), null);
            }

            if (!observation.PlayfieldId.HasValue)
            {
                return Match("NO_PLAYFIELD", new List<OfficialPlayfieldPlacement>(), null);
            }

            var candidates = (placements ?? new List<OfficialPlayfieldPlacement>())
                .Where(
                    placement => placement != null
                                 && placement.PositionX.HasValue
                                 && placement.PositionZ.HasValue
                                 && (float)placement.PositionX.Value == observation.PositionX
                                 && (float)placement.PositionZ.Value == observation.PositionZ)
                .OrderBy(placement => placement.OfficialSpawnRecordId, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                return Match("NO_EXACT_XZ_MATCH", candidates, null);
            }

            if (candidates.Count == 1)
            {
                return Match(
                    "EXACT_XZ_UNIQUE_RECORD",
                    candidates,
                    candidates[0].CanonicalAcgHashText);
            }

            string[] hashes = candidates
                .Select(placement => placement.CanonicalAcgHashText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (hashes.Length == 1)
            {
                return Match("EXACT_XZ_UNIQUE_HASH", candidates, hashes[0]);
            }

            return Match("EXACT_XZ_AMBIGUOUS_HASH", candidates, null);
        }

        internal static void RunSelfTest()
        {
            var observation = new ScfuAcgObservation
            {
                Identity = "(SimpleChar:1)",
                Name = "Fixture",
                PlayfieldId = 4582,
                PositionX = 10.25f,
                PositionY = 999.0f,
                PositionZ = 20.5f,
                MonsterData = 123,
                IsNpc = true
            };
            var first = Placement("record-1", "TEST", 10.25, 30.0, 20.5);
            ScfuAcgCorrelationMatch unique = Correlate(
                observation,
                new List<OfficialPlayfieldPlacement> { first });
            Require(unique.Status == "EXACT_XZ_UNIQUE_RECORD", "unique record status");
            Require(unique.CanonicalAcgHashText == "TEST", "unique record hash");

            var sameHash = Placement("record-2", "TEST", 10.25, 40.0, 20.5);
            ScfuAcgCorrelationMatch hashOnly = Correlate(
                observation,
                new List<OfficialPlayfieldPlacement> { first, sameHash });
            Require(hashOnly.Status == "EXACT_XZ_UNIQUE_HASH", "unique hash status");
            Require(hashOnly.CanonicalAcgHashText == "TEST", "unique hash value");

            var otherHash = Placement("record-3", "OTHR", 10.25, 50.0, 20.5);
            ScfuAcgCorrelationMatch ambiguous = Correlate(
                observation,
                new List<OfficialPlayfieldPlacement> { first, otherHash });
            Require(
                ambiguous.Status == "EXACT_XZ_AMBIGUOUS_HASH",
                "ambiguous hash status");
            Require(
                string.IsNullOrEmpty(ambiguous.CanonicalAcgHashText),
                "ambiguous hash stays empty");

            ScfuAcgCorrelationMatch absent = Correlate(
                observation,
                new List<OfficialPlayfieldPlacement>
                {
                    Placement("record-4", "MISS", 10.251, 30.0, 20.5)
                });
            Require(absent.Status == "NO_EXACT_XZ_MATCH", "no-match status");

            observation.IsNpc = false;
            ScfuAcgCorrelationMatch nonNpc = Correlate(
                observation,
                new List<OfficialPlayfieldPlacement> { first });
            Require(nonNpc.Status == "NON_NPC", "non-NPC status");
        }

        private static OfficialPlayfieldPlacement Placement(
            string id,
            string hash,
            double x,
            double y,
            double z)
        {
            return new OfficialPlayfieldPlacement
            {
                OfficialSpawnRecordId = id,
                CanonicalAcgHashText = hash,
                PositionX = x,
                PositionY = y,
                PositionZ = z,
                OfficialAcgHashWireBytes = "54 53 45 54",
                OfficialAcgHashNativeUInt32 = 0x54455354
            };
        }

        private static ScfuAcgCorrelationMatch Match(
            string status,
            IList<OfficialPlayfieldPlacement> candidates,
            string hash)
        {
            return new ScfuAcgCorrelationMatch
            {
                Status = status,
                Candidates = candidates,
                CanonicalAcgHashText = hash
            };
        }

        private static IList<OfficialPlayfieldPlacement> GetPlacements(
            OfficialPlayfieldPlacementCatalog catalog,
            IDictionary<int, IList<OfficialPlayfieldPlacement>> cache,
            int? playfieldId)
        {
            if (!playfieldId.HasValue)
            {
                return new List<OfficialPlayfieldPlacement>();
            }

            IList<OfficialPlayfieldPlacement> placements;
            if (!cache.TryGetValue(playfieldId.Value, out placements))
            {
                OfficialPlayfieldPlacementShard shard;
                string failure;
                placements = catalog.TryGetPlayfield(playfieldId.Value, out shard, out failure)
                                 ? catalog.GetPlacements(playfieldId.Value)
                                 : new List<OfficialPlayfieldPlacement>();
                cache.Add(playfieldId.Value, placements);
            }

            return placements;
        }

        private static void Count(
            ScfuAcgCorrelationSummary summary,
            ScfuAcgCorrelationMatch match)
        {
            switch (match.Status)
            {
                case "EXACT_XZ_UNIQUE_RECORD":
                    summary.ExactRecordCount++;
                    break;
                case "EXACT_XZ_UNIQUE_HASH":
                    summary.ExactHashCount++;
                    break;
                case "EXACT_XZ_AMBIGUOUS_HASH":
                    summary.AmbiguousCount++;
                    break;
                case "NON_NPC":
                    summary.NonNpcCount++;
                    break;
                default:
                    summary.UnmatchedNpcCount++;
                    break;
            }
        }

        private static string Header()
        {
            return string.Join(
                ",",
                new[]
                {
                    "CapturedUtc",
                    "Direction",
                    "Sequence",
                    "Identity",
                    "Name",
                    "PlayfieldId",
                    "PositionX",
                    "PositionY",
                    "PositionZ",
                    "MonsterData",
                    "CorrelationStatus",
                    "CandidateRecordCount",
                    "CanonicalAcgHashText",
                    "OfficialAcgHashWireBytes",
                    "OfficialAcgHashNativeUInt32",
                    "OfficialSpawnRecordId",
                    "SourceNpcId",
                    "OfficialPositionX",
                    "OfficialPositionY",
                    "OfficialPositionZ",
                    "VerticalDelta",
                    "OfficialIdentityResolutionStatus",
                    "OfficialExistingAoRebirthProfile",
                    "OfficialRuntimeActivationAuthorized",
                    "Detail"
                });
        }

        private static string FormatRow(
            ScfuAcgObservation observation,
            ScfuAcgCorrelationMatch match)
        {
            OfficialPlayfieldPlacement record =
                match.Candidates.Count == 1 ? match.Candidates[0] : null;
            OfficialPlayfieldPlacement hashRecord =
                !string.IsNullOrEmpty(match.CanonicalAcgHashText)
                    ? match.Candidates.FirstOrDefault()
                    : null;
            string detail = Detail(match.Status);

            return string.Join(
                ",",
                new[]
                {
                    Csv(observation.CapturedUtc),
                    Csv(observation.Direction),
                    Csv(observation.Sequence),
                    Csv(observation.Identity),
                    Csv(observation.Name),
                    Csv(observation.PlayfieldId.HasValue
                            ? observation.PlayfieldId.Value.ToString(CultureInfo.InvariantCulture)
                            : string.Empty),
                    Csv(observation.PositionX.ToString("R", CultureInfo.InvariantCulture)),
                    Csv(observation.PositionY.ToString("R", CultureInfo.InvariantCulture)),
                    Csv(observation.PositionZ.ToString("R", CultureInfo.InvariantCulture)),
                    Csv(observation.MonsterData.ToString(CultureInfo.InvariantCulture)),
                    Csv(match.Status),
                    Csv(match.Candidates.Count.ToString(CultureInfo.InvariantCulture)),
                    Csv(match.CanonicalAcgHashText),
                    Csv(hashRecord == null ? null : hashRecord.OfficialAcgHashWireBytes),
                    Csv(
                        hashRecord == null || !hashRecord.OfficialAcgHashNativeUInt32.HasValue
                            ? null
                            : hashRecord.OfficialAcgHashNativeUInt32.Value.ToString(
                                CultureInfo.InvariantCulture)),
                    Csv(record == null ? null : record.OfficialSpawnRecordId),
                    Csv(
                        record == null || !record.SourceNpcId.HasValue
                            ? null
                            : record.SourceNpcId.Value.ToString(CultureInfo.InvariantCulture)),
                    Csv(FormatDouble(record == null ? null : record.PositionX)),
                    Csv(FormatDouble(record == null ? null : record.PositionY)),
                    Csv(FormatDouble(record == null ? null : record.PositionZ)),
                    Csv(
                        record == null || !record.PositionY.HasValue
                            ? null
                            : (observation.PositionY - record.PositionY.Value).ToString(
                                "R",
                                CultureInfo.InvariantCulture)),
                    Csv(record == null ? null : record.IdentityResolutionStatus),
                    Csv(record == null ? null : record.ExistingAoRebirthProfile),
                    Csv(
                        record == null || !record.RuntimeActivationAuthorized.HasValue
                            ? null
                            : record.RuntimeActivationAuthorized.Value.ToString()),
                    Csv(detail)
                });
        }

        private static string Detail(string status)
        {
            switch (status)
            {
                case "EXACT_XZ_UNIQUE_RECORD":
                    return "Captured and official X/Z are identical IEEE-754 float32 values; Y is retained only as a ground-height delta.";
                case "EXACT_XZ_UNIQUE_HASH":
                    return "Multiple official records share the exact captured float32 X/Z, but all carry the same ACGHash; record identity remains unresolved.";
                case "EXACT_XZ_AMBIGUOUS_HASH":
                    return "Multiple official records with different ACGHash values share the exact captured float32 X/Z; no hash is promoted.";
                case "NO_EXACT_XZ_MATCH":
                    return "No official placement has the exact captured float32 X/Z; proximity and display name are not used.";
                case "NO_PLAYFIELD":
                    return "SCFU has no playfield id; no official placement lookup is possible.";
                case "NON_NPC":
                    return "SCFU is not NPCInfo and is excluded from placement correlation.";
                default:
                    return string.Empty;
            }
        }

        private static string FormatDouble(double? value)
        {
            return value.HasValue
                       ? value.Value.ToString("R", CultureInfo.InvariantCulture)
                       : null;
        }

        private static string ResolveRepositoryCorpusRoot()
        {
            foreach (string seed in new[]
                     {
                         Environment.CurrentDirectory,
                         AppDomain.CurrentDomain.BaseDirectory
                     })
            {
                var directory = new DirectoryInfo(Path.GetFullPath(seed));
                while (directory != null)
                {
                    string candidate = Path.Combine(
                        directory.FullName,
                        "docs",
                        "generated",
                        "playfields");
                    if (File.Exists(
                        Path.Combine(candidate, "official-placement-corpus-manifest.json")))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException(
                "Tracked official placement corpus was not found beneath the repository root.");
        }

        private static string Csv(string value)
        {
            string normalized = value ?? string.Empty;
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        private static void PromoteFile(string pendingPath, string outputPath)
        {
            DeleteIfExists(outputPath);
            File.Move(pendingPath, outputPath);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition)
            {
                throw new InvalidOperationException("ACG correlation self-test failed: " + label);
            }
        }
    }
}
