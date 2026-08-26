namespace AORebirth.Core.Playfields.OfficialPlacements
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;

    #endregion

    /// <summary>
    /// Shared, fail-closed access to the packaged official static-placement corpus.
    /// Loading validates evidence integrity only and never materializes runtime spawns.
    /// </summary>
    internal sealed class OfficialPlayfieldPlacementCatalog
    {
        private const int SupportedManifestSchemaVersion = 1;

        private const int SupportedShardSchemaVersion = 2;

        private const int ExpectedResourceType = 1000014;

        private const int ExpectedResourceCount = 630;

        private const int ExpectedParsedResourceCount = 627;

        private const int ExpectedParserLimitedResourceCount = 3;

        private const int ExpectedDistrictCount = 4146;

        private const int ExpectedPlacementCount = 32805;

        private const int ExpectedUniqueAcgHashCount = 4016;

        private const int ExpectedRuntimeActivationAuthorizedCount = 25;

        private const int Pf4582PlayfieldId = 4582;

        private const int Pf4582OfficialPlacementCount = 207;

        private const string ParsedStatus = "PARSED";

        private const string ParserLimitedStatus = "MALFORMED_FOR_CURRENT_EXTRACTOR";

        private const string CorpusManifestFileName = "official-placement-corpus-manifest.json";

        private const string PlacementIndexFileName = "official-placement-index.json";

        private const string PlacementSummaryFileName = "official-placement-summary.json";

        private const string AcgHashInventoryFileName = "official-acghash-inventory.json";

        private static readonly int[] ExpectedParserLimitedPlayfieldIds = { 103, 615, 4805 };

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly UTF8Encoding OutputUtf8 = new UTF8Encoding(false);

        private readonly string corpusRoot;

        private readonly string corpusManifestSha256;

        private readonly OfficialPlayfieldPlacementCorpusManifest manifest;

        private readonly Dictionary<int, OfficialPlayfieldPlacementShard> shards =
            new Dictionary<int, OfficialPlayfieldPlacementShard>();

        private readonly Dictionary<string, OfficialPlayfieldPlacement> placementsByStableId =
            new Dictionary<string, OfficialPlayfieldPlacement>(StringComparer.Ordinal);

        internal OfficialPlayfieldPlacementCatalog(string corpusRoot)
        {
            if (string.IsNullOrWhiteSpace(corpusRoot))
            {
                throw new ArgumentException(
                    "An explicit packaged official-placement corpus root is required.",
                    "corpusRoot");
            }

            this.corpusRoot = Path.GetFullPath(corpusRoot);
            Require(
                Directory.Exists(this.corpusRoot),
                "Official placement corpus directory is missing: " + this.corpusRoot);

            string manifestPath = RequireExactFile(this.corpusRoot, CorpusManifestFileName);
            this.corpusManifestSha256 = HashFile(manifestPath);
            this.manifest = DeserializeJson<OfficialPlayfieldPlacementCorpusManifest>(manifestPath);

            this.ValidateManifest();
            this.ValidateGlobalArtifact(PlacementIndexFileName, this.manifest.IndexSha256);
            this.ValidateGlobalArtifact(PlacementSummaryFileName, this.manifest.SummarySha256);
            this.ValidateGlobalArtifact(AcgHashInventoryFileName, this.manifest.AcgHashInventorySha256);
            this.LoadAndValidateShards();
        }

        internal OfficialPlayfieldPlacementCorpusManifest Manifest
        {
            get { return this.manifest; }
        }

        internal string CorpusManifestSha256
        {
            get { return this.corpusManifestSha256; }
        }

        /// <summary>
        /// Resolves the one exact-cased packaged corpus path beneath a ZoneEngine output.
        /// There is intentionally no repository or developer-machine fallback.
        /// </summary>
        internal static string ResolveRuntimeCorpusRoot(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("ZoneEngine base directory is required.", "baseDirectory");
            }

            string root = Path.GetFullPath(baseDirectory);
            Require(Directory.Exists(root), "ZoneEngine base directory is missing: " + root);
            root = RequireExactDirectory(root, "Content");
            root = RequireExactDirectory(root, "Official");
            return RequireExactDirectory(root, "PlayfieldPlacements");
        }

        internal bool TryGetPlayfield(
            int playfieldId,
            out OfficialPlayfieldPlacementShard shard,
            out string failure)
        {
            shard = null;
            failure = string.Empty;
            if (playfieldId <= 0)
            {
                failure = "Official placement playfield id must be positive.";
                return false;
            }

            if (!this.shards.TryGetValue(playfieldId, out shard))
            {
                failure = "Official placement playfield is not present in the corpus: "
                    + playfieldId.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return true;
        }

        internal OfficialPlayfieldPlacementShard GetPlayfield(int playfieldId)
        {
            OfficialPlayfieldPlacementShard shard;
            string failure;
            if (!this.TryGetPlayfield(playfieldId, out shard, out failure))
            {
                throw new KeyNotFoundException(failure);
            }

            return shard;
        }

        internal OfficialPlayfieldPlacementShard GetPlayfieldOrThrow(int playfieldId)
        {
            return this.GetPlayfield(playfieldId);
        }

        internal IList<OfficialPlayfieldPlacementDistrict> GetDistricts(int playfieldId)
        {
            return new ReadOnlyCollection<OfficialPlayfieldPlacementDistrict>(
                this.GetPlayfield(playfieldId).Districts);
        }

        internal IList<OfficialPlayfieldPlacement> GetPlacements(int playfieldId)
        {
            return new ReadOnlyCollection<OfficialPlayfieldPlacement>(
                this.GetPlayfield(playfieldId).Records);
        }

        internal OfficialPlayfieldPlacement GetByOfficialSpawnRecordId(
            string officialSpawnRecordId)
        {
            if (string.IsNullOrWhiteSpace(officialSpawnRecordId))
            {
                throw new ArgumentException(
                    "Official placement stable id is required.",
                    "officialSpawnRecordId");
            }

            OfficialPlayfieldPlacement placement;
            if (!this.placementsByStableId.TryGetValue(officialSpawnRecordId, out placement))
            {
                throw new KeyNotFoundException(
                    "Official placement stable id is not present in the corpus: "
                    + officialSpawnRecordId);
            }

            return placement;
        }

        /// <summary>
        /// Writes a platform-neutral canonical build manifest and a whitelisted env provenance file.
        /// Neither artifact authorizes runtime placement activation.
        /// </summary>
        internal void WriteValidationArtifacts(
            string sourceSha,
            string buildPlatform,
            string placementManifestOutput,
            string provenanceOutput)
        {
            string normalizedSourceSha = NormalizeSourceSha(sourceSha);
            string normalizedBuildPlatform = NormalizeBuildPlatform(buildPlatform);
            Require(
                !string.IsNullOrWhiteSpace(placementManifestOutput),
                "Official placement build-manifest output path is required.");
            Require(
                !string.IsNullOrWhiteSpace(provenanceOutput),
                "Official placement provenance output path is required.");

            string manifestOutputPath = Path.GetFullPath(placementManifestOutput);
            string provenanceOutputPath = Path.GetFullPath(provenanceOutput);
            this.RequireSafeOutputPath(manifestOutputPath);
            this.RequireSafeOutputPath(provenanceOutputPath);
            Require(
                !string.Equals(
                    manifestOutputPath,
                    provenanceOutputPath,
                    StringComparison.OrdinalIgnoreCase),
                "Official placement build manifest and provenance outputs must be different files.");

            string buildManifest = this.CreateCanonicalBuildManifest(normalizedSourceSha);
            byte[] buildManifestBytes = OutputUtf8.GetBytes(buildManifest);
            string buildManifestSha256 = HashBytes(buildManifestBytes);
            WriteUtf8NoBom(manifestOutputPath, buildManifest);

            string provenance = this.CreateCanonicalProvenance(
                normalizedSourceSha,
                normalizedBuildPlatform,
                buildManifestSha256);
            WriteUtf8NoBom(provenanceOutputPath, provenance);
        }

        private void RequireSafeOutputPath(string outputPath)
        {
            string[] protectedNames =
                {
                    CorpusManifestFileName,
                    PlacementIndexFileName,
                    PlacementSummaryFileName,
                    AcgHashInventoryFileName
                };
            foreach (string protectedName in protectedNames)
            {
                string protectedPath = Path.GetFullPath(Path.Combine(this.corpusRoot, protectedName));
                Require(
                    !string.Equals(outputPath, protectedPath, StringComparison.OrdinalIgnoreCase),
                    "Official placement validation output cannot overwrite corpus input: "
                    + protectedName);
            }

            string placementsPath = Path.GetFullPath(Path.Combine(this.corpusRoot, "placements"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Require(
                !outputPath.StartsWith(placementsPath, StringComparison.OrdinalIgnoreCase),
                "Official placement validation output cannot overwrite a placement shard.");
        }

        private void ValidateManifest()
        {
            Require(this.manifest != null, "Official placement corpus manifest root is null.");
            RequireValue(
                this.manifest.SchemaVersion,
                SupportedManifestSchemaVersion,
                "Official placement corpus manifest schema version is unsupported.");
            RequireText(this.manifest.CorpusVersion, "Official placement corpus version is missing.");
            Require(
                string.Equals(
                    this.manifest.SourceClientVariant,
                    "EP1_OLD_GRAPHICS_CLIENT",
                    StringComparison.Ordinal),
                "Official placement source client variant drifted.");
            Require(
                string.Equals(
                    this.manifest.SourceClientBuild,
                    "18.8.62_EP1",
                    StringComparison.Ordinal),
                "Official placement source client build drifted.");
            RequireValue(
                this.manifest.ResourceType,
                ExpectedResourceType,
                "Official placement resource type drifted.");
            RequireSha256(
                this.manifest.SourceManifestSha256,
                "Official placement source-manifest digest is invalid.");
            RequireSha256(
                this.manifest.IndexSha256,
                "Official placement index digest is invalid.");
            RequireSha256(
                this.manifest.SummarySha256,
                "Official placement summary digest is invalid.");
            RequireSha256(
                this.manifest.AcgHashInventorySha256,
                "Official placement ACGHash inventory digest is invalid.");

            OfficialPlayfieldPlacementCorpusMetrics metrics = this.manifest.Metrics;
            Require(metrics != null, "Official placement corpus metrics are missing.");
            RequireValue(metrics.ResourceCount, ExpectedResourceCount, "Official placement resource count drifted.");
            RequireValue(
                metrics.ParsedResourceCount,
                ExpectedParsedResourceCount,
                "Official placement parsed-resource count drifted.");
            RequireValue(
                metrics.ParserLimitedResourceCount,
                ExpectedParserLimitedResourceCount,
                "Official placement parser-limited count drifted.");
            RequireValue(metrics.DistrictCount, ExpectedDistrictCount, "Official placement district count drifted.");
            RequireValue(
                metrics.PlacementCount,
                ExpectedPlacementCount,
                "Official placement record count drifted.");
            RequireValue(
                metrics.UniqueAcgHashCount,
                ExpectedUniqueAcgHashCount,
                "Official placement unique ACGHash count drifted.");
            RequireValue(
                metrics.RuntimeActivationAuthorizedCount,
                ExpectedRuntimeActivationAuthorizedCount,
                "Official placement authorized-runtime count drifted.");

            Require(
                IntArraysEqual(
                    this.manifest.ParserLimitedPlayfieldIds,
                    ExpectedParserLimitedPlayfieldIds),
                "Official placement parser-limited playfield ids drifted.");

            OfficialPlayfieldPlacementCorpusPolicy policy = this.manifest.Policy;
            Require(policy != null, "Official placement corpus policy is missing.");
            RequireFalse(
                policy.MassPlacementActivation,
                "Official placement corpus cannot authorize mass placement activation.");
            RequireFalse(
                policy.UnresolvedAcgHashActivated,
                "Official placement corpus cannot activate an unresolved ACGHash.");
            RequireFalse(
                policy.ExistingRuntimeBehaviorChanged,
                "Official placement corpus cannot change existing runtime behavior.");

            OfficialPlayfieldPlacementManifestEntry[] entries = this.manifest.Playfields;
            Require(entries != null, "Official placement manifest playfields are missing.");
            Require(
                entries.Length == ExpectedResourceCount,
                "Official placement manifest playfield count drifted.");

            int parsedCount = 0;
            int parserLimitedCount = 0;
            int districtCount = 0;
            int placementCount = 0;
            int authorizedCount = 0;
            int previousPlayfieldId = -1;
            var parserLimitedIds = new List<int>();

            foreach (OfficialPlayfieldPlacementManifestEntry entry in entries)
            {
                Require(entry != null, "Official placement manifest contains a null playfield row.");
                Require(
                    entry.PlayfieldId.HasValue && entry.PlayfieldId.Value > previousPlayfieldId,
                    "Official placement manifest playfields are not strictly id-sorted.");
                int playfieldId = entry.PlayfieldId.Value;
                previousPlayfieldId = playfieldId;

                string expectedPath = "placements/pf_"
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".json";
                Require(
                    string.Equals(entry.Path, expectedPath, StringComparison.Ordinal),
                    "Official placement manifest shard path is not canonical for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Require(
                    entry.Path.IndexOf('\\') < 0,
                    "Official placement manifest shard paths must use forward slashes.");
                RequireSha256(
                    entry.ShardSha256,
                    "Official placement shard digest is invalid for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                if (entry.SourceResourceSha256 != null)
                {
                    RequireSha256(
                        entry.SourceResourceSha256,
                        "Official source-resource digest is invalid for playfield "
                        + playfieldId.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }

                Require(
                    entry.RuntimeActivationAuthorizedCount.HasValue
                    && entry.RuntimeActivationAuthorizedCount.Value >= 0,
                    "Official placement authorized count is missing for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                int expectedAuthorized = playfieldId == Pf4582PlayfieldId
                    ? ExpectedRuntimeActivationAuthorizedCount
                    : 0;
                Require(
                    entry.RuntimeActivationAuthorizedCount.Value == expectedAuthorized,
                    "Official placement activation authority drifted for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");

                if (string.Equals(entry.ParseStatus, ParsedStatus, StringComparison.Ordinal))
                {
                    parsedCount++;
                    Require(
                        entry.DistrictCount.HasValue && entry.DistrictCount.Value >= 0,
                        "Parsed official placement row lacks its district count.");
                    Require(
                        entry.PlacementCount.HasValue && entry.PlacementCount.Value >= 0,
                        "Parsed official placement row lacks its placement count.");
                    RequireSha256(
                        entry.SourceResourceSha256,
                        "Parsed official placement row lacks its source-resource digest.");
                    districtCount += entry.DistrictCount.Value;
                    placementCount += entry.PlacementCount.Value;
                }
                else if (string.Equals(entry.ParseStatus, ParserLimitedStatus, StringComparison.Ordinal))
                {
                    parserLimitedCount++;
                    parserLimitedIds.Add(playfieldId);
                    Require(
                        !entry.DistrictCount.HasValue && !entry.PlacementCount.HasValue,
                        "Parser-limited official placement counts must remain unavailable.");
                    Require(
                        entry.RuntimeActivationAuthorizedCount.Value == 0,
                        "Parser-limited official placement rows cannot authorize activation.");
                }
                else
                {
                    throw new InvalidDataException(
                        "Official placement manifest parse status is unsupported for playfield "
                        + playfieldId.ToString(CultureInfo.InvariantCulture)
                        + ".");
                }

                authorizedCount += entry.RuntimeActivationAuthorizedCount.Value;
            }

            Require(parsedCount == ExpectedParsedResourceCount, "Manifest parsed-resource total drifted.");
            Require(
                parserLimitedCount == ExpectedParserLimitedResourceCount,
                "Manifest parser-limited total drifted.");
            Require(districtCount == ExpectedDistrictCount, "Manifest district total drifted.");
            Require(placementCount == ExpectedPlacementCount, "Manifest placement total drifted.");
            Require(
                authorizedCount == ExpectedRuntimeActivationAuthorizedCount,
                "Manifest activation-authority total drifted.");
            Require(
                IntArraysEqual(parserLimitedIds.ToArray(), ExpectedParserLimitedPlayfieldIds),
                "Manifest parser-limited rows do not match the declared ids.");
        }

        private void ValidateGlobalArtifact(string fileName, string expectedSha256)
        {
            string path = RequireExactFile(this.corpusRoot, fileName);
            Require(
                string.Equals(HashFile(path), expectedSha256, StringComparison.Ordinal),
                "Official placement global artifact digest mismatch: " + fileName);
        }

        private void LoadAndValidateShards()
        {
            string placementsRoot = RequireExactDirectory(this.corpusRoot, "placements");
            string[] files = Directory.GetFiles(placementsRoot, "*", SearchOption.TopDirectoryOnly);
            string[] directories = Directory.GetDirectories(
                placementsRoot,
                "*",
                SearchOption.TopDirectoryOnly);
            Require(directories.Length == 0, "Official placement shard directory cannot contain subdirectories.");
            Require(
                files.Length == ExpectedResourceCount,
                "Official placement shard file count drifted.");

            var filesByName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Require(
                    !filesByName.ContainsKey(fileName),
                    "Official placement shard directory contains a duplicate exact filename: "
                    + fileName);
                filesByName.Add(fileName, file);
            }

            int totalDistricts = 0;
            int totalPlacements = 0;
            int totalAuthorized = 0;
            int totalCurrentActive = 0;
            var acgHashes = new HashSet<uint>();

            foreach (OfficialPlayfieldPlacementManifestEntry entry in this.manifest.Playfields)
            {
                int playfieldId = entry.PlayfieldId.Value;
                string expectedFileName = "pf_"
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".json";
                string shardPath;
                Require(
                    filesByName.TryGetValue(expectedFileName, out shardPath),
                    "Official placement shard is missing or has incorrect casing: "
                    + expectedFileName);
                Require(
                    string.Equals(HashFile(shardPath), entry.ShardSha256, StringComparison.Ordinal),
                    "Official placement shard digest mismatch for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");

                OfficialPlayfieldPlacementShard shard =
                    DeserializeJson<OfficialPlayfieldPlacementShard>(shardPath);
                int shardAuthorized;
                int shardCurrentActive;
                this.ValidateShard(
                    shard,
                    entry,
                    acgHashes,
                    out shardAuthorized,
                    out shardCurrentActive);
                this.shards.Add(playfieldId, shard);

                totalDistricts += shard.Districts.Length;
                totalPlacements += shard.Records.Length;
                totalAuthorized += shardAuthorized;
                totalCurrentActive += shardCurrentActive;
            }

            Require(this.shards.Count == ExpectedResourceCount, "Loaded placement resource count drifted.");
            Require(totalDistricts == ExpectedDistrictCount, "Loaded placement district count drifted.");
            Require(totalPlacements == ExpectedPlacementCount, "Loaded placement record count drifted.");
            Require(
                acgHashes.Count == ExpectedUniqueAcgHashCount,
                "Loaded placement unique ACGHash count drifted.");
            Require(
                totalAuthorized == ExpectedRuntimeActivationAuthorizedCount,
                "Loaded placement activation-authority count drifted.");
            Require(
                totalCurrentActive == ExpectedRuntimeActivationAuthorizedCount,
                "Loaded placement current-active count drifted.");

            this.ValidatePf4582Regression();
            this.ValidateParserLimitedRegression();
        }

        private void ValidateShard(
            OfficialPlayfieldPlacementShard shard,
            OfficialPlayfieldPlacementManifestEntry entry,
            HashSet<uint> acgHashes,
            out int authorizedCount,
            out int currentActiveCount)
        {
            int playfieldId = entry.PlayfieldId.Value;
            Require(shard != null, "Official placement shard root is null.");
            RequireValue(
                shard.SchemaVersion,
                SupportedShardSchemaVersion,
                "Official placement shard schema version is unsupported.");
            Require(
                string.Equals(
                    shard.SourceClientVariant,
                    this.manifest.SourceClientVariant,
                    StringComparison.Ordinal),
                "Official placement shard source client variant drifted.");
            Require(
                string.Equals(
                    shard.SourceClientBuild,
                    this.manifest.SourceClientBuild,
                    StringComparison.Ordinal),
                "Official placement shard source client build drifted.");
            RequireValue(
                shard.ResourceType,
                this.manifest.ResourceType.Value,
                "Official placement shard resource type drifted.");
            RequireValue(
                shard.ResourceInstance,
                playfieldId,
                "Official placement shard resource instance drifted.");
            RequireValue(
                shard.PlayfieldId,
                playfieldId,
                "Official placement shard playfield id drifted.");
            Require(
                string.Equals(shard.ParseStatus, entry.ParseStatus, StringComparison.Ordinal),
                "Official placement shard parse status drifted.");
            Require(shard.Districts != null, "Official placement shard districts are missing.");
            Require(shard.Records != null, "Official placement shard records are missing.");

            authorizedCount = 0;
            currentActiveCount = 0;
            if (string.Equals(shard.ParseStatus, ParserLimitedStatus, StringComparison.Ordinal))
            {
                Require(
                    !shard.DistrictCount.HasValue && !shard.OfficialSpawnCount.HasValue,
                    "Parser-limited official placement counts must remain unavailable.");
                Require(
                    shard.Districts.Length == 0 && shard.Records.Length == 0,
                    "Parser-limited official placement shards cannot contain synthetic data.");
                RequireText(shard.ParseError, "Parser-limited official placement error is missing.");
                return;
            }

            Require(
                string.Equals(shard.ParseStatus, ParsedStatus, StringComparison.Ordinal),
                "Official placement shard parse status is unsupported.");
            RequireValue(
                shard.DistrictCount,
                entry.DistrictCount.Value,
                "Official placement shard district count drifted.");
            RequireValue(
                shard.OfficialSpawnCount,
                entry.PlacementCount.Value,
                "Official placement shard record count drifted.");
            Require(
                shard.Districts.Length == shard.DistrictCount.Value,
                "Official placement typed district count drifted.");
            Require(
                shard.Records.Length == shard.OfficialSpawnCount.Value,
                "Official placement record array count drifted.");

            var districts = new Dictionary<int, OfficialPlayfieldPlacementDistrict>();
            for (int index = 0; index < shard.Districts.Length; index++)
            {
                OfficialPlayfieldPlacementDistrict district = shard.Districts[index];
                this.ValidateDistrict(district, shard, index);
                districts.Add(index, district);
            }

            var districtRecordCounts = new Dictionary<int, int>();
            foreach (OfficialPlayfieldPlacement placement in shard.Records)
            {
                OfficialPlayfieldPlacementDistrict district;
                this.ValidateRecord(placement, shard, districts, acgHashes, out district);
                int districtIndex = placement.DistrictIndex.Value;
                int count;
                districtRecordCounts.TryGetValue(districtIndex, out count);
                districtRecordCounts[districtIndex] = count + 1;

                if (placement.RuntimeActivationAuthorized.Value)
                {
                    authorizedCount++;
                }

                if (placement.CurrentRuntimeActive == true)
                {
                    currentActiveCount++;
                }
            }

            foreach (KeyValuePair<int, OfficialPlayfieldPlacementDistrict> row in districts)
            {
                int actualCount;
                districtRecordCounts.TryGetValue(row.Key, out actualCount);
                Require(
                    row.Value.HashSpawnRecordCount.Value == actualCount,
                    "Official placement district record count drifted: "
                    + row.Value.OfficialDistrictId);
            }

            Require(
                authorizedCount == entry.RuntimeActivationAuthorizedCount.Value,
                "Official placement shard activation-authority count drifted.");
        }

        private void ValidateDistrict(
            OfficialPlayfieldPlacementDistrict district,
            OfficialPlayfieldPlacementShard shard,
            int expectedIndex)
        {
            Require(district != null, "Official placement shard contains a null district.");
            RequireValue(
                district.DistrictIndex,
                expectedIndex,
                "Official placement districts are not index ordered.");
            Require(
                district.DistrictName != null,
                "Official placement district name is missing.");
            Require(
                district.DistrictRecordOffset.HasValue
                && district.DistrictRecordOffset.Value >= 0,
                "Official placement district source offset is missing.");
            Require(
                district.DistrictSerializedSize.HasValue
                && district.DistrictSerializedSize.Value > 0,
                "Official placement district serialized size is missing.");
            Require(
                district.HashSpawnRecordCount.HasValue
                && district.HashSpawnRecordCount.Value >= 0,
                "Official placement district record count is missing.");
            RequireSha256(
                district.RecordSha256,
                "Official placement district digest is invalid.");

            string expectedResourceId = string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}",
                shard.SourceClientBuild,
                shard.ResourceType.Value,
                shard.PlayfieldId.Value);
            string expectedDistrictId = expectedResourceId
                + ":district-"
                + expectedIndex.ToString(CultureInfo.InvariantCulture);
            Require(
                string.Equals(
                    district.OfficialResourceId,
                    expectedResourceId,
                    StringComparison.Ordinal),
                "Official placement district resource id drifted.");
            Require(
                string.Equals(
                    district.OfficialDistrictId,
                    expectedDistrictId,
                    StringComparison.Ordinal),
                "Official placement district stable id drifted.");
            Require(
                district.OtherCollectionCountsWhereDecoded != null,
                "Official placement district decoded collection counts are missing.");
            foreach (KeyValuePair<string, int> count in district.OtherCollectionCountsWhereDecoded)
            {
                RequireText(count.Key, "Official placement district collection name is missing.");
                Require(count.Value >= 0, "Official placement district collection count is negative.");
            }
        }

        private void ValidateRecord(
            OfficialPlayfieldPlacement placement,
            OfficialPlayfieldPlacementShard shard,
            Dictionary<int, OfficialPlayfieldPlacementDistrict> districts,
            HashSet<uint> acgHashes,
            out OfficialPlayfieldPlacementDistrict district)
        {
            Require(placement != null, "Official placement shard contains a null record.");
            RequireText(placement.OfficialSpawnRecordId, "Official placement stable id is missing.");
            Require(
                !this.placementsByStableId.ContainsKey(placement.OfficialSpawnRecordId),
                "Official placement corpus contains a duplicate stable id: "
                + placement.OfficialSpawnRecordId);
            this.placementsByStableId.Add(placement.OfficialSpawnRecordId, placement);
            Require(
                string.Equals(
                    placement.SourceClientVariant,
                    shard.SourceClientVariant,
                    StringComparison.Ordinal),
                "Official placement source client variant drifted.");
            Require(
                string.Equals(
                    placement.SourceClientBuild,
                    shard.SourceClientBuild,
                    StringComparison.Ordinal),
                "Official placement source client build drifted.");
            RequireValue(
                placement.ResourceType,
                shard.ResourceType.Value,
                "Official placement resource type drifted.");
            RequireValue(
                placement.ResourceInstance,
                shard.ResourceInstance.Value,
                "Official placement resource instance drifted.");
            RequireValue(
                placement.PlayfieldId,
                shard.PlayfieldId.Value,
                "Official placement playfield id drifted.");
            district = null;
            Require(
                placement.DistrictIndex.HasValue
                && districts.TryGetValue(placement.DistrictIndex.Value, out district),
                "Official placement district index is unavailable.");
            Require(
                string.Equals(
                    placement.DistrictName,
                    district.DistrictName,
                    StringComparison.Ordinal),
                "Official placement record district name drifted.");
            Require(
                placement.DistrictRecordOrdinal.HasValue
                && placement.DistrictRecordOrdinal.Value >= 0,
                "Official placement district ordinal is missing.");
            Require(
                string.Equals(placement.ParseStatus, ParsedStatus, StringComparison.Ordinal),
                "Official placement record parse status is unsupported.");

            string expectedIdentity = string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:district-{3}:record-{4}",
                placement.SourceClientBuild,
                placement.ResourceType.Value,
                placement.PlayfieldId.Value,
                placement.DistrictIndex.Value,
                placement.DistrictRecordOrdinal.Value);
            Require(
                string.Equals(
                    placement.OfficialSpawnRecordId,
                    expectedIdentity,
                    StringComparison.Ordinal),
                "Official placement stable id does not match its source coordinates: "
                + placement.OfficialSpawnRecordId);
            Require(
                placement.SerializedSize.HasValue && placement.SerializedSize.Value > 0,
                "Official placement serialized size is missing.");
            Require(
                placement.PositionX.HasValue
                && placement.PositionY.HasValue
                && placement.PositionZ.HasValue,
                "Official placement position is missing.");

            if (placement.LevelMinimum.HasValue && placement.LevelMaximum.HasValue)
            {
                Require(
                    placement.LevelMinimum.Value <= placement.LevelMaximum.Value,
                    "Official placement level range is inverted: "
                    + placement.OfficialSpawnRecordId);
            }

            uint acgHash = ValidateAcgHash(placement);
            acgHashes.Add(acgHash);

            Require(
                placement.PlacementKnown.HasValue && placement.PlacementKnown.Value,
                "Official placement availability must be explicit and true.");
            Require(
                placement.IdentityResolved.HasValue,
                "Official placement identity-resolution state is missing.");
            Require(
                placement.BehaviorReady.HasValue,
                "Official placement behavior-readiness state is missing.");
            Require(
                placement.RuntimeActivationAuthorized.HasValue,
                "Official placement runtime-activation state is missing.");
            RequireText(
                placement.IdentityResolutionStatus,
                "Official placement identity-resolution status is missing.");
            RequireText(
                placement.BehaviorReadiness,
                "Official placement behavior-readiness description is missing.");

            bool authorized = placement.RuntimeActivationAuthorized.Value;
            if (authorized)
            {
                Require(
                    placement.PlayfieldId.Value == Pf4582PlayfieldId,
                    "Only the existing PF4582 active set may retain placement authorization.");
                Require(
                    placement.CurrentRuntimeActive == true,
                    "Authorized official placement is not in the existing active set.");
                Require(
                    placement.IdentityResolved.Value && placement.BehaviorReady.Value,
                    "Unresolved official placement cannot be runtime authorized.");
            }

            Require(
                placement.CurrentRuntimeActive != true || authorized,
                "Existing active official placement lost its activation authority.");
            Require(
                placement.IdentityResolved.Value || !authorized,
                "Identity-unresolved official placement cannot be runtime authorized.");
            Require(
                placement.BehaviorReady.Value || !authorized,
                "Behavior-unready official placement cannot be runtime authorized.");

            if (placement.PlayfieldId.Value == Pf4582PlayfieldId)
            {
                Require(
                    placement.CurrentRuntimeActive.HasValue,
                    "PF4582 current-runtime state must remain explicit.");
            }
            else
            {
                Require(
                    !placement.CurrentRuntimeActive.HasValue && !authorized,
                    "Official placements outside PF4582 cannot gain runtime activation state.");
            }
        }

        private void ValidatePf4582Regression()
        {
            OfficialPlayfieldPlacementShard shard = this.GetPlayfield(Pf4582PlayfieldId);
            Require(
                shard.Records.Length == Pf4582OfficialPlacementCount,
                "PF4582 official placement count drifted.");

            OfficialPlayfieldPlacement ncnn = null;
            foreach (OfficialPlayfieldPlacement placement in shard.Records)
            {
                if (string.Equals(
                    placement.CanonicalAcgHashText,
                    "NCNN",
                    StringComparison.Ordinal))
                {
                    Require(ncnn == null, "PF4582 contains more than one NCNN official placement.");
                    ncnn = placement;
                }
            }

            Require(ncnn != null, "PF4582 NCNN official placement is missing.");
            Require(
                ncnn.CurrentRuntimeActive == false,
                "PF4582 NCNN current-runtime state must remain inactive.");
            Require(
                ncnn.RuntimeActivationAuthorized == false,
                "PF4582 NCNN cannot be runtime authorized.");
            Require(
                ncnn.IdentityResolved == false,
                "PF4582 NCNN identity must remain unresolved.");
        }

        private void ValidateParserLimitedRegression()
        {
            foreach (int playfieldId in ExpectedParserLimitedPlayfieldIds)
            {
                OfficialPlayfieldPlacementShard shard = this.GetPlayfield(playfieldId);
                Require(
                    string.Equals(shard.ParseStatus, ParserLimitedStatus, StringComparison.Ordinal),
                    "Parser-limited official placement status drifted for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
                Require(
                    !shard.DistrictCount.HasValue
                    && !shard.OfficialSpawnCount.HasValue
                    && shard.Districts.Length == 0
                    && shard.Records.Length == 0,
                    "Parser-limited official placement gained synthetic data for playfield "
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private string CreateCanonicalBuildManifest(string sourceSha)
        {
            var builder = new StringBuilder(256000);
            builder.Append("{\"SchemaVersion\":1,\"SourceSHA\":");
            AppendJsonString(builder, sourceSha);
            builder.Append(",\"CorpusVersion\":");
            AppendJsonString(builder, this.manifest.CorpusVersion);
            builder.Append(",\"SourceClientVariant\":");
            AppendJsonString(builder, this.manifest.SourceClientVariant);
            builder.Append(",\"SourceClientBuild\":");
            AppendJsonString(builder, this.manifest.SourceClientBuild);
            builder.Append(",\"ResourceType\":");
            builder.Append(this.manifest.ResourceType.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"CorpusManifestSha256\":");
            AppendJsonString(builder, this.corpusManifestSha256);
            builder.Append(",\"SourceManifestSha256\":");
            AppendJsonString(builder, this.manifest.SourceManifestSha256);
            builder.Append(",\"IndexSha256\":");
            AppendJsonString(builder, this.manifest.IndexSha256);
            builder.Append(",\"SummarySha256\":");
            AppendJsonString(builder, this.manifest.SummarySha256);
            builder.Append(",\"AcgHashInventorySha256\":");
            AppendJsonString(builder, this.manifest.AcgHashInventorySha256);
            builder.Append(",\"Metrics\":");
            AppendCanonicalMetrics(builder, this.manifest.Metrics);
            builder.Append(",\"ParserLimitedPlayfieldIds\":[103,615,4805]");
            builder.Append(",\"Policy\":");
            AppendCanonicalPolicy(builder, this.manifest.Policy);
            builder.Append(",\"Playfields\":[");

            for (int index = 0; index < this.manifest.Playfields.Length; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }

                AppendCanonicalPlayfield(builder, this.manifest.Playfields[index]);
            }

            builder.Append("]}\n");
            return builder.ToString();
        }

        private string CreateCanonicalProvenance(
            string sourceSha,
            string buildPlatform,
            string buildManifestSha256)
        {
            var builder = new StringBuilder(1024);
            AppendEnv(builder, "SOURCE_SHA", sourceSha);
            AppendEnv(builder, "BUILD_PLATFORM", buildPlatform);
            AppendEnv(builder, "PLACEMENT_CORPUS_VERSION", this.manifest.CorpusVersion);
            AppendEnv(builder, "PLACEMENT_CORPUS_MANIFEST_SHA256", this.corpusManifestSha256);
            AppendEnv(builder, "PLACEMENT_CORPUS_SUMMARY_SHA256", this.manifest.SummarySha256);
            AppendEnv(builder, "PLACEMENT_CORPUS_INDEX_SHA256", this.manifest.IndexSha256);
            AppendEnv(
                builder,
                "PLACEMENT_ACGHASH_INVENTORY_SHA256",
                this.manifest.AcgHashInventorySha256);
            AppendEnv(builder, "PLACEMENT_BUILD_MANIFEST_SHA256", buildManifestSha256);
            AppendEnv(
                builder,
                "PLACEMENT_RESOURCE_COUNT",
                this.manifest.Metrics.ResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_PARSED_RESOURCE_COUNT",
                this.manifest.Metrics.ParsedResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT",
                this.manifest.Metrics.ParserLimitedResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_DISTRICT_COUNT",
                this.manifest.Metrics.DistrictCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_RECORD_COUNT",
                this.manifest.Metrics.PlacementCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_UNIQUE_ACGHASH_COUNT",
                this.manifest.Metrics.UniqueAcgHashCount.Value.ToString(CultureInfo.InvariantCulture));
            AppendEnv(
                builder,
                "PLACEMENT_RUNTIME_AUTHORIZED_COUNT",
                this.manifest.Metrics.RuntimeActivationAuthorizedCount.Value.ToString(
                    CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void AppendCanonicalMetrics(
            StringBuilder builder,
            OfficialPlayfieldPlacementCorpusMetrics metrics)
        {
            builder.Append("{\"ResourceCount\":");
            builder.Append(metrics.ResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"ParsedResourceCount\":");
            builder.Append(metrics.ParsedResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"ParserLimitedResourceCount\":");
            builder.Append(metrics.ParserLimitedResourceCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"DistrictCount\":");
            builder.Append(metrics.DistrictCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"PlacementCount\":");
            builder.Append(metrics.PlacementCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"UniqueAcgHashCount\":");
            builder.Append(metrics.UniqueAcgHashCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"RuntimeActivationAuthorizedCount\":");
            builder.Append(
                metrics.RuntimeActivationAuthorizedCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append('}');
        }

        private static void AppendCanonicalPolicy(
            StringBuilder builder,
            OfficialPlayfieldPlacementCorpusPolicy policy)
        {
            builder.Append("{\"MassPlacementActivation\":false");
            builder.Append(",\"UnresolvedAcgHashActivated\":false");
            builder.Append(",\"ExistingRuntimeBehaviorChanged\":false}");
        }

        private static void AppendCanonicalPlayfield(
            StringBuilder builder,
            OfficialPlayfieldPlacementManifestEntry entry)
        {
            builder.Append("{\"PlayfieldId\":");
            builder.Append(entry.PlayfieldId.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"Path\":");
            AppendJsonString(builder, entry.Path);
            builder.Append(",\"ParseStatus\":");
            AppendJsonString(builder, entry.ParseStatus);
            builder.Append(",\"DistrictCount\":");
            AppendNullableInt(builder, entry.DistrictCount);
            builder.Append(",\"PlacementCount\":");
            AppendNullableInt(builder, entry.PlacementCount);
            builder.Append(",\"SourceResourceSha256\":");
            if (entry.SourceResourceSha256 == null)
            {
                builder.Append("null");
            }
            else
            {
                AppendJsonString(builder, entry.SourceResourceSha256);
            }

            builder.Append(",\"ShardSha256\":");
            AppendJsonString(builder, entry.ShardSha256);
            builder.Append(",\"RuntimeActivationAuthorizedCount\":");
            builder.Append(
                entry.RuntimeActivationAuthorizedCount.Value.ToString(CultureInfo.InvariantCulture));
            builder.Append('}');
        }

        private static void AppendNullableInt(StringBuilder builder, int? value)
        {
            if (value.HasValue)
            {
                builder.Append(value.Value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append("null");
            }
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            Require(value != null, "Canonical JSON string cannot be null.");
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20 || character > 0x7e)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static void AppendEnv(StringBuilder builder, string name, string value)
        {
            RequireText(name, "Provenance environment key is missing.");
            Require(value != null, "Provenance environment value is missing for " + name + ".");
            Require(
                value.IndexOf('\r') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('=') < 0,
                "Provenance environment value contains an unsupported character for " + name + ".");
            builder.Append(name);
            builder.Append('=');
            builder.Append(value);
            builder.Append('\n');
        }

        private static uint ValidateAcgHash(OfficialPlayfieldPlacement placement)
        {
            Require(
                placement.CanonicalAcgHashText != null
                && placement.CanonicalAcgHashText.Length == 4,
                "Official placement canonical ACGHash text must contain four bytes.");
            Require(
                placement.OfficialAcgHashWireBytes != null
                && placement.OfficialAcgHashWireBytes.Length == 11,
                "Official placement ACGHash wire bytes are missing.");
            string[] parts = placement.OfficialAcgHashWireBytes.Split(' ');
            Require(parts.Length == 4, "Official placement ACGHash wire bytes are malformed.");

            var bytes = new byte[4];
            for (int index = 0; index < bytes.Length; index++)
            {
                Require(
                    parts[index].Length == 2
                    && parts[index] == parts[index].ToUpperInvariant(),
                    "Official placement ACGHash wire bytes are not canonical.");
                byte parsed;
                Require(
                    byte.TryParse(
                        parts[index],
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out parsed),
                    "Official placement ACGHash wire bytes are invalid.");
                bytes[index] = parsed;
            }

            uint nativeValue = (uint)(bytes[0]
                | (bytes[1] << 8)
                | (bytes[2] << 16)
                | (bytes[3] << 24));
            Require(
                placement.OfficialAcgHashNativeUInt32.HasValue
                && placement.OfficialAcgHashNativeUInt32.Value == nativeValue,
                "Official placement ACGHash native scalar drifted.");
            for (int index = 0; index < bytes.Length; index++)
            {
                Require(
                    placement.CanonicalAcgHashText[index] == bytes[3 - index],
                    "Official placement ACGHash canonical text/wire byte order drifted.");
            }

            return nativeValue;
        }

        private static T DeserializeJson<T>(string path)
        {
            try
            {
                var serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue,
                };
                return serializer.Deserialize<T>(File.ReadAllText(path, StrictUtf8));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Official placement JSON load failed for " + Path.GetFileName(path) + ": "
                    + exception.Message,
                    exception);
            }
        }

        private static string RequireExactDirectory(string parent, string expectedName)
        {
            foreach (string directory in Directory.GetDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(
                    Path.GetFileName(directory),
                    expectedName,
                    StringComparison.Ordinal))
                {
                    return directory;
                }
            }

            throw new DirectoryNotFoundException(
                "Required exact-cased official placement directory is missing: "
                + expectedName);
        }

        private static string RequireExactFile(string directory, string expectedName)
        {
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.Ordinal))
                {
                    return file;
                }
            }

            throw new FileNotFoundException(
                "Required exact-cased official placement file is missing: " + expectedName);
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BytesToLowerHex(sha256.ComputeHash(stream));
            }
        }

        private static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BytesToLowerHex(sha256.ComputeHash(bytes));
            }
        }

        private static string BytesToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void WriteUtf8NoBom(string path, string value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, value, OutputUtf8);
        }

        private static string NormalizeSourceSha(string sourceSha)
        {
            Require(
                sourceSha != null && sourceSha.Length == 40 && IsLowerOrUpperHex(sourceSha),
                "Official placement validation requires a full 40-character source SHA.");
            return sourceSha.ToLowerInvariant();
        }

        private static string NormalizeBuildPlatform(string buildPlatform)
        {
            RequireText(buildPlatform, "Official placement build platform is required.");
            string normalized = buildPlatform.ToLowerInvariant();
            Require(
                string.Equals(normalized, "windows", StringComparison.Ordinal)
                || string.Equals(normalized, "linux", StringComparison.Ordinal)
                || string.Equals(
                    normalized,
                    "windows-hosted-linux-publish",
                    StringComparison.Ordinal),
                "Official placement build platform must be windows, linux, or windows-hosted-linux-publish.");
            return normalized;
        }

        private static bool IsLowerOrUpperHex(string value)
        {
            foreach (char character in value)
            {
                bool digit = character >= '0' && character <= '9';
                bool lower = character >= 'a' && character <= 'f';
                bool upper = character >= 'A' && character <= 'F';
                if (!digit && !lower && !upper)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireSha256(string value, string message)
        {
            Require(
                value != null
                && value.Length == 64
                && value == value.ToLowerInvariant()
                && IsLowerOrUpperHex(value),
                message);
        }

        private static bool IntArraysEqual(int[] left, int[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireValue(int? actual, int expected, string message)
        {
            Require(actual.HasValue && actual.Value == expected, message);
        }

        private static void RequireFalse(bool? actual, string message)
        {
            Require(actual.HasValue && !actual.Value, message);
        }

        private static void RequireText(string value, string message)
        {
            Require(!string.IsNullOrWhiteSpace(value), message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }
    }
}
