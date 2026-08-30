namespace AORebirth.Core.Playfields.OfficialPlacements
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;

    internal enum AcgDevelopmentPlaceholderMode
    {
        Off,
        CapturePlan,
        CurrentPlayfieldPrimary,
        CurrentPlayfieldAllPoints,
        ResolvedComparison
    }

    internal enum AcgVisualEvidenceGrade
    {
        ExactOfficial,
        CaptureCorrelated,
        CaptureCorrelatedMultipleVariants,
        Unresolved
    }

    internal enum AcgPlaceholderLocationKind
    {
        Primary,
        AdditionalPoint
    }

    internal sealed class AcgDevelopmentPlaceholderOptions
    {
        internal const string ModeEnvironmentVariable = "AO_REBIRTH_ACG_PLACEHOLDER_MODE";

        internal const string PlayfieldEnvironmentVariable = "AO_REBIRTH_ACG_PLACEHOLDER_PLAYFIELD";

        internal AcgDevelopmentPlaceholderOptions(
            AcgDevelopmentPlaceholderMode mode,
            int? selectedPlayfield)
        {
            if (mode == AcgDevelopmentPlaceholderMode.Off)
            {
                selectedPlayfield = null;
            }
            else if (!selectedPlayfield.HasValue || selectedPlayfield.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "selectedPlayfield",
                    "A development placeholder mode requires one positive playfield ResourceInstance.");
            }

            this.Mode = mode;
            this.SelectedPlayfield = selectedPlayfield;
        }

        internal AcgDevelopmentPlaceholderMode Mode { get; private set; }

        internal int? SelectedPlayfield { get; private set; }

        internal bool IsOff
        {
            get { return this.Mode == AcgDevelopmentPlaceholderMode.Off; }
        }

        internal static AcgDevelopmentPlaceholderOptions FromEnvironment()
        {
#if DEBUG
            const bool developmentBuild = true;
#else
            const bool developmentBuild = false;
#endif
            return Parse(Environment.GetEnvironmentVariable, developmentBuild);
        }

        internal static AcgDevelopmentPlaceholderOptions Parse(
            Func<string, string> readEnvironment,
            bool developmentBuild)
        {
            if (readEnvironment == null)
            {
                throw new ArgumentNullException("readEnvironment");
            }

            string rawMode = readEnvironment(ModeEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(rawMode)
                || string.Equals(rawMode.Trim(), "Off", StringComparison.OrdinalIgnoreCase))
            {
                return new AcgDevelopmentPlaceholderOptions(
                    AcgDevelopmentPlaceholderMode.Off,
                    null);
            }

            AcgDevelopmentPlaceholderMode mode;
            if (!Enum.TryParse(rawMode.Trim(), true, out mode)
                || mode == AcgDevelopmentPlaceholderMode.Off)
            {
                throw new InvalidDataException(
                    ModeEnvironmentVariable
                    + " must be Off, CapturePlan, CurrentPlayfieldPrimary, "
                    + "CurrentPlayfieldAllPoints, or ResolvedComparison.");
            }

            if (!developmentBuild)
            {
                throw new InvalidOperationException(
                    "ACG placement placeholders are compiled fail-closed outside Debug builds.");
            }

            int selectedPlayfield;
            string rawPlayfield = readEnvironment(PlayfieldEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(rawPlayfield)
                || !int.TryParse(
                    rawPlayfield.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out selectedPlayfield)
                || selectedPlayfield <= 0)
            {
                throw new InvalidDataException(
                    PlayfieldEnvironmentVariable
                    + " must be one positive ResourceInstance when placeholders are enabled.");
            }

            return new AcgDevelopmentPlaceholderOptions(mode, selectedPlayfield);
        }
    }

    internal sealed class AcgDevelopmentPlaceholderCatalog
    {
        internal const string DefaultPlaceholderVisualSource = "default_monster.cir";

        internal const int DefaultPlaceholderCatMeshId = 26884;

        internal const int ExactFdqoCatMeshId = 15222;

        private const int SupportedSchemaVersion = 1;

        private const int ExpectedResourceType = 1000014;

        private const int ExpectedEnumeratedResources = 630;

        private const int ExpectedParsedResources = 627;

        private const int ExpectedMalformedResources = 3;

        private const int ExpectedPlayfieldsWithPlacements = 459;

        private const int ExpectedPrimaryRecords = 32805;

        private const int ExpectedAdditionalPoints = 32737;

        private const int ExpectedTotalCoordinates = 65542;

        private const int ExpectedUniqueAcgHashes = 4016;

        private const int ExpectedCapturePlanPlayfields = 238;

        private const int ExpectedCapturePlanTargets = 4016;

        private const int ExpectedExactOfficial = 1;

        private const int ExpectedCaptureCorrelated = 4;

        private const int ExpectedUnresolved = 4011;

        private const int Pf4582 = 4582;

        private const int ExpectedPf4582Records = 207;

        private const int ExpectedPf4582FdqoPlacements = 9;

        private const string ExpectedBuildId = "18.8.62_EP1";

        private const string ExpectedPackageSha256 =
            "379e39cf3a2a697b5613316ff2a7da66a9d5f0ecc30d1b75efe0a4dffc7d093e";

        private const string ManifestFileName = "acg-development-placeholder-manifest.json";

        private const string VisualRegistryFileName = "acg-visual-resolution-registry.json";

        private static readonly int[] ExpectedMalformedPlayfields = { 103, 615, 4805 };

        private readonly string corpusRoot;

        private readonly AcgDevelopmentPlaceholderManifest manifest;

        private readonly Dictionary<int, AcgDevelopmentPlaceholderManifestPlayfield> manifestPlayfields;

        private readonly Dictionary<uint, AcgVisualResolution> visuals;

        private readonly HashSet<int> loadedPlayfields = new HashSet<int>();

        internal AcgDevelopmentPlaceholderCatalog(string corpusRoot)
        {
            if (string.IsNullOrWhiteSpace(corpusRoot))
            {
                throw new ArgumentException("An ACG placeholder corpus root is required.", "corpusRoot");
            }

            this.corpusRoot = Path.GetFullPath(corpusRoot);
            Require(Directory.Exists(this.corpusRoot), "ACG placeholder corpus directory is missing.");
            this.manifest = DeserializeJson<AcgDevelopmentPlaceholderManifest>(
                RequireExactFile(this.corpusRoot, ManifestFileName));
            this.ValidateManifest();
            this.manifestPlayfields = this.manifest.Playfields.ToDictionary(
                entry => entry.ResourceInstance.Value);
            this.visuals = this.LoadVisualRegistry();
        }

        internal AcgDevelopmentPlaceholderManifest Manifest
        {
            get { return this.manifest; }
        }

        internal IList<int> LoadedPlayfields
        {
            get
            {
                return new ReadOnlyCollection<int>(
                    this.loadedPlayfields.OrderBy(value => value).ToArray());
            }
        }

        internal static string ResolveRuntimeCorpusRoot(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("ZoneEngine base directory is required.", "baseDirectory");
            }

            string root = RequireExactDirectory(Path.GetFullPath(baseDirectory), "Content");
            root = RequireExactDirectory(root, "Official");
            return RequireExactDirectory(root, "AcgDevelopmentPlaceholders");
        }

        internal AcgVisualResolution GetVisual(uint nativeAcgHash)
        {
            AcgVisualResolution visual;
            if (!this.visuals.TryGetValue(nativeAcgHash, out visual))
            {
                throw new KeyNotFoundException(
                    "Native ACG is absent from the visual registry: 0x"
                    + nativeAcgHash.ToString("X8", CultureInfo.InvariantCulture));
            }

            return visual;
        }

        internal IList<AcgDevelopmentPlaceholderPlanEntry> CreatePlan(
            AcgDevelopmentPlaceholderOptions options,
            int currentPlayfield)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.IsOff
                || !options.SelectedPlayfield.HasValue
                || options.SelectedPlayfield.Value != currentPlayfield)
            {
                return new ReadOnlyCollection<AcgDevelopmentPlaceholderPlanEntry>(
                    new AcgDevelopmentPlaceholderPlanEntry[0]);
            }

            AcgDevelopmentPlaceholderShard shard = this.LoadPlayfield(currentPlayfield);
            var plan = new List<AcgDevelopmentPlaceholderPlanEntry>();
            foreach (AcgDevelopmentPlaceholderRecord record in shard.Records)
            {
                if (options.Mode == AcgDevelopmentPlaceholderMode.CapturePlan
                    && record.CapturePlanTarget != true)
                {
                    continue;
                }

                AcgVisualResolution visual = this.GetVisual(record.AcgHashNativeUInt32.Value);
                plan.Add(CreatePlanEntry(record, record.Primary, null, visual));
                if (options.Mode != AcgDevelopmentPlaceholderMode.CurrentPlayfieldAllPoints)
                {
                    continue;
                }

                foreach (AcgDevelopmentPlaceholderAdditionalPoint point in record.AdditionalPoints)
                {
                    plan.Add(CreatePlanEntry(record, point, point.Ordinal, visual));
                }
            }

            return new ReadOnlyCollection<AcgDevelopmentPlaceholderPlanEntry>(plan);
        }

        internal AcgDevelopmentPlaceholderCorpusAudit AuditAllShards()
        {
            int primary = 0;
            int additional = 0;
            int captureTargets = 0;
            int pf4582Records = 0;
            int pf4582Fdqo = 0;
            bool ncnnUnresolved = false;
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateCoordinates = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (AcgDevelopmentPlaceholderManifestPlayfield entry in this.manifest.Playfields)
            {
                AcgDevelopmentPlaceholderShard shard = this.LoadPlayfield(entry.ResourceInstance.Value);
                primary += shard.Records.Length;
                additional += shard.Records.Sum(record => record.AdditionalPoints.Length);
                captureTargets += shard.Records.Count(record => record.CapturePlanTarget == true);
                foreach (AcgDevelopmentPlaceholderRecord record in shard.Records)
                {
                    Require(stableIds.Add(record.OfficialSpawnRecordId), "Duplicate stable source id loaded.");
                    string coordinateKey = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}|{2:R}|{3:R}|{4:R}",
                        record.ResourceInstance.Value,
                        record.AcgHashNativeUInt32.Value,
                        record.Primary.PositionX.Value,
                        record.Primary.PositionY.Value,
                        record.Primary.PositionZ.Value);
                    int coordinateCount;
                    duplicateCoordinates.TryGetValue(coordinateKey, out coordinateCount);
                    duplicateCoordinates[coordinateKey] = coordinateCount + 1;

                    if (record.ResourceInstance.Value == Pf4582)
                    {
                        pf4582Records++;
                        if (record.AcgHashNativeUInt32.Value == 0x4644514F)
                        {
                            pf4582Fdqo++;
                        }

                        if (string.Equals(record.AcgHashText, "NCNN", StringComparison.Ordinal)
                            && string.Equals(record.EvidenceGrade, "Unresolved", StringComparison.Ordinal))
                        {
                            ncnnUnresolved = true;
                        }
                    }
                }
            }

            int duplicateRows = duplicateCoordinates.Values.Where(value => value > 1).Sum(value => value - 1);
            Require(primary == ExpectedPrimaryRecords, "Audited primary count drifted.");
            Require(additional == ExpectedAdditionalPoints, "Audited additional-point count drifted.");
            Require(primary + additional == ExpectedTotalCoordinates, "Audited coordinate count drifted.");
            Require(captureTargets == ExpectedCapturePlanTargets, "Audited capture-target count drifted.");
            Require(pf4582Records == ExpectedPf4582Records, "Audited PF4582 count drifted.");
            Require(pf4582Fdqo == ExpectedPf4582FdqoPlacements, "Audited PF4582 FDQO count drifted.");
            Require(ncnnUnresolved, "Audited PF4582 NCNN boundary drifted.");
            Require(
                duplicateRows == this.manifest.Metrics.DuplicatePrimaryCoordinateRowCount.Value,
                "Audited duplicate-row count drifted.");

            return new AcgDevelopmentPlaceholderCorpusAudit
            {
                PrimaryRecordCount = primary,
                AdditionalPointCount = additional,
                TotalCoordinateCount = primary + additional,
                CapturePlanTargetCount = captureTargets,
                DuplicatePrimaryCoordinateRowCount = duplicateRows,
                Pf4582PrimaryRecordCount = pf4582Records,
                Pf4582FdqoPlacementCount = pf4582Fdqo,
                Pf4582NcnnUnresolvedPresent = ncnnUnresolved
            };
        }

        private AcgDevelopmentPlaceholderShard LoadPlayfield(int playfieldId)
        {
            if (ExpectedMalformedPlayfields.Contains(playfieldId))
            {
                throw new InvalidDataException(
                    "ACG placeholder materialization is unavailable for explicitly malformed PF"
                    + playfieldId.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            AcgDevelopmentPlaceholderManifestPlayfield entry;
            if (!this.manifestPlayfields.TryGetValue(playfieldId, out entry))
            {
                this.loadedPlayfields.Add(playfieldId);
                return new AcgDevelopmentPlaceholderShard
                {
                    SchemaVersion = SupportedSchemaVersion,
                    BuildId = ExpectedBuildId,
                    ResourceType = ExpectedResourceType,
                    ResourceInstance = playfieldId,
                    PrimaryRecordCount = 0,
                    AdditionalPointCount = 0,
                    CapturePlanTargetCount = 0,
                    Records = new AcgDevelopmentPlaceholderRecord[0]
                };
            }

            string path = RequireRelativeExactFile(this.corpusRoot, entry.Path);
            Require(string.Equals(HashFile(path), entry.Sha256, StringComparison.Ordinal),
                "ACG placeholder shard digest mismatch for PF" + playfieldId + ".");
            AcgDevelopmentPlaceholderShard shard = DeserializeJson<AcgDevelopmentPlaceholderShard>(path);
            ValidateShard(shard, entry);
            this.loadedPlayfields.Add(playfieldId);
            return shard;
        }

        private Dictionary<uint, AcgVisualResolution> LoadVisualRegistry()
        {
            string path = RequireExactFile(this.corpusRoot, VisualRegistryFileName);
            Require(string.Equals(HashFile(path), this.manifest.VisualRegistrySha256, StringComparison.Ordinal),
                "ACG visual registry digest mismatch.");
            AcgVisualResolutionRegistry registry = DeserializeJson<AcgVisualResolutionRegistry>(path);
            RequireValue(registry.SchemaVersion, SupportedSchemaVersion, "ACG visual registry schema drifted.");
            RequireText(registry.BuildId, "ACG visual registry build is missing.");
            Require(string.Equals(registry.BuildId, ExpectedBuildId, StringComparison.Ordinal),
                "ACG visual registry build drifted.");
            RequireValue(registry.ResourceType, ExpectedResourceType, "ACG visual resource type drifted.");
            RequireValue(registry.Count, ExpectedUniqueAcgHashes, "ACG visual count drifted.");
            Require(registry.Entries != null && registry.Entries.Length == ExpectedUniqueAcgHashes,
                "ACG visual entries drifted.");

            var result = new Dictionary<uint, AcgVisualResolution>();
            var gradeCounts = new Dictionary<AcgVisualEvidenceGrade, int>();
            foreach (AcgVisualResolution visual in registry.Entries)
            {
                Require(visual != null && visual.AcgHashNativeUInt32.HasValue,
                    "ACG visual registry contains an incomplete row.");
                Require(!result.ContainsKey(visual.AcgHashNativeUInt32.Value),
                    "ACG visual registry contains a duplicate native key.");
                result.Add(visual.AcgHashNativeUInt32.Value, visual);
                ValidateVisual(visual);
                AcgVisualEvidenceGrade grade;
                Require(Enum.TryParse(visual.EvidenceGrade, false, out grade),
                    "ACG visual evidence grade is unsupported.");
                int count;
                gradeCounts.TryGetValue(grade, out count);
                gradeCounts[grade] = count + 1;
            }

            Require(result.ContainsKey(0x20202020) && result.ContainsKey(0x9F9F9F9F),
                "Non-printable native ACG keys are not both retained.");
            Require(gradeCounts[AcgVisualEvidenceGrade.ExactOfficial] == ExpectedExactOfficial,
                "ExactOfficial visual count drifted.");
            Require(
                gradeCounts[AcgVisualEvidenceGrade.CaptureCorrelated]
                + gradeCounts[AcgVisualEvidenceGrade.CaptureCorrelatedMultipleVariants]
                == ExpectedCaptureCorrelated,
                "Capture-correlated visual count drifted.");
            Require(gradeCounts[AcgVisualEvidenceGrade.Unresolved] == ExpectedUnresolved,
                "Unresolved visual count drifted.");
            return result;
        }

        private void ValidateManifest()
        {
            Require(this.manifest != null, "ACG placeholder manifest is null.");
            RequireValue(this.manifest.SchemaVersion, SupportedSchemaVersion, "ACG placeholder schema drifted.");
            Require(string.Equals(this.manifest.BuildId, ExpectedBuildId, StringComparison.Ordinal),
                "ACG placeholder build id drifted.");
            RequireValue(this.manifest.ResourceType, ExpectedResourceType, "ACG placeholder resource type drifted.");
            Require(string.Equals(this.manifest.PortablePackageSha256, ExpectedPackageSha256, StringComparison.Ordinal),
                "ACG placeholder package SHA-256 drifted.");
            RequireSha256(this.manifest.VisualRegistrySha256, "ACG visual registry SHA-256 is invalid.");
            Require(string.Equals(this.manifest.VisualRegistryPath, VisualRegistryFileName, StringComparison.Ordinal),
                "ACG visual registry path drifted.");

            AcgDevelopmentPlaceholderMetrics metrics = this.manifest.Metrics;
            Require(metrics != null, "ACG placeholder metrics are missing.");
            RequireValue(metrics.EnumeratedResourceCount, ExpectedEnumeratedResources, "Enumerated resource count drifted.");
            RequireValue(metrics.ParsedResourceCount, ExpectedParsedResources, "Parsed resource count drifted.");
            RequireValue(metrics.MalformedResourceCount, ExpectedMalformedResources, "Malformed resource count drifted.");
            RequireValue(metrics.PlayfieldsWithPlacements, ExpectedPlayfieldsWithPlacements, "Placement playfield count drifted.");
            RequireValue(metrics.PrimaryRecordCount, ExpectedPrimaryRecords, "Primary record count drifted.");
            RequireValue(metrics.AdditionalPointCount, ExpectedAdditionalPoints, "Additional-point count drifted.");
            RequireValue(metrics.TotalCoordinateCount, ExpectedTotalCoordinates, "Coordinate count drifted.");
            RequireValue(metrics.UniqueAcgHashCount, ExpectedUniqueAcgHashes, "Unique ACG count drifted.");
            RequireValue(metrics.CapturePlanPlayfieldCount, ExpectedCapturePlanPlayfields, "Capture-plan playfield count drifted.");
            RequireValue(metrics.CapturePlanTargetCount, ExpectedCapturePlanTargets, "Capture-plan target count drifted.");
            RequireValue(metrics.ExactOfficialCount, ExpectedExactOfficial, "ExactOfficial count drifted.");
            RequireValue(metrics.CaptureCorrelatedCount, ExpectedCaptureCorrelated, "Capture-correlated count drifted.");
            RequireValue(metrics.UnresolvedCount, ExpectedUnresolved, "Unresolved count drifted.");
            RequireValue(metrics.Pf4582PrimaryRecordCount, ExpectedPf4582Records, "PF4582 count drifted.");
            RequireValue(metrics.Pf4582FdqoPlacementCount, ExpectedPf4582FdqoPlacements, "PF4582 FDQO count drifted.");
            Require(metrics.DuplicatePrimaryCoordinateRowCount.HasValue
                    && metrics.DuplicatePrimaryCoordinateRowCount.Value > 0,
                "Duplicate placement retention is not explicit.");

            Require(this.manifest.Policy != null, "ACG placeholder policy is missing.");
            Require(string.Equals(this.manifest.Policy.DefaultMode, "Off", StringComparison.Ordinal),
                "ACG placeholder default mode drifted.");
            Require(this.manifest.Policy.DevelopmentBuildOnly == true,
                "ACG placeholder development-build boundary drifted.");
            Require(this.manifest.Policy.ProductionActivation == false,
                "ACG placeholder production activation must remain false.");
            Require(this.manifest.Policy.RuntimeIdentityUsesSourceIdentity == false,
                "ACG placeholder runtime identity boundary drifted.");
            Require(this.manifest.Policy.CaptureCorrelationPromotesExactIdentity == false,
                "Capture correlation cannot promote exact identity.");
            Require(this.manifest.Policy.AdditionalPointRuntimeSemanticsProven == false,
                "Additional-point runtime semantics cannot be promoted.");
            Require(string.Equals(
                    this.manifest.Policy.DefaultPlaceholderVisualSource,
                    DefaultPlaceholderVisualSource,
                    StringComparison.Ordinal),
                "Default placeholder visual source drifted.");
            RequireValue(
                this.manifest.Policy.DefaultPlaceholderCatMeshId,
                DefaultPlaceholderCatMeshId,
                "Default placeholder CatMesh drifted.");
            Require(string.Equals(this.manifest.Policy.RespawnChanceFieldName, "RespawnChanceRaw", StringComparison.Ordinal),
                "Respawn chance raw-field boundary drifted.");

            Require(this.manifest.MalformedResources != null
                    && this.manifest.MalformedResources.Select(row => row.ResourceInstance.Value).OrderBy(value => value)
                        .SequenceEqual(ExpectedMalformedPlayfields),
                "Malformed playfield boundary drifted.");
            Require(this.manifest.Playfields != null
                    && this.manifest.Playfields.Length == ExpectedPlayfieldsWithPlacements,
                "ACG placeholder manifest shard count drifted.");

            int previous = -1;
            int primary = 0;
            int additional = 0;
            int targets = 0;
            foreach (AcgDevelopmentPlaceholderManifestPlayfield entry in this.manifest.Playfields)
            {
                Require(entry != null && entry.ResourceInstance.HasValue
                        && entry.ResourceInstance.Value > previous,
                    "ACG placeholder shard entries are not strictly playfield-sorted.");
                previous = entry.ResourceInstance.Value;
                Require(string.Equals(entry.Path, "playfields/pf_" + previous + ".json", StringComparison.Ordinal),
                    "ACG placeholder shard path is not canonical.");
                RequireSha256(entry.Sha256, "ACG placeholder shard SHA-256 is invalid.");
                primary += entry.PrimaryRecordCount.Value;
                additional += entry.AdditionalPointCount.Value;
                targets += entry.CapturePlanTargetCount.Value;
            }

            Require(primary == ExpectedPrimaryRecords, "Manifest shard primary total drifted.");
            Require(additional == ExpectedAdditionalPoints, "Manifest shard additional total drifted.");
            Require(targets == ExpectedCapturePlanTargets, "Manifest shard capture-target total drifted.");
        }

        private static void ValidateShard(
            AcgDevelopmentPlaceholderShard shard,
            AcgDevelopmentPlaceholderManifestPlayfield entry)
        {
            Require(shard != null, "ACG placeholder shard is null.");
            RequireValue(shard.SchemaVersion, SupportedSchemaVersion, "ACG placeholder shard schema drifted.");
            Require(string.Equals(shard.BuildId, ExpectedBuildId, StringComparison.Ordinal),
                "ACG placeholder shard build drifted.");
            RequireValue(shard.ResourceType, ExpectedResourceType, "ACG placeholder shard resource type drifted.");
            RequireValue(shard.ResourceInstance, entry.ResourceInstance.Value, "ACG placeholder shard playfield drifted.");
            RequireValue(shard.PrimaryRecordCount, entry.PrimaryRecordCount.Value, "ACG placeholder shard primary count drifted.");
            RequireValue(shard.AdditionalPointCount, entry.AdditionalPointCount.Value, "ACG placeholder shard additional count drifted.");
            RequireValue(shard.CapturePlanTargetCount, entry.CapturePlanTargetCount.Value, "ACG placeholder shard target count drifted.");
            Require(shard.Records != null && shard.Records.Length == shard.PrimaryRecordCount.Value,
                "ACG placeholder shard records drifted.");

            int additional = 0;
            int targets = 0;
            foreach (AcgDevelopmentPlaceholderRecord record in shard.Records)
            {
                Require(record != null && record.Primary != null && record.AdditionalPoints != null,
                    "ACG placeholder shard contains an incomplete record.");
                Require(record.ResourceInstance == shard.ResourceInstance,
                    "ACG placeholder record playfield drifted.");
                Require(record.AcgHashNativeUInt32.HasValue,
                    "ACG placeholder native key is missing.");
                RequireText(record.AcgHashWireBytes, "ACG placeholder wire bytes are missing.");
                RequireText(record.OfficialSpawnRecordId, "ACG placeholder stable id is missing.");
                additional += record.AdditionalPoints.Length;
                if (record.CapturePlanTarget == true)
                {
                    targets++;
                }

                for (int index = 0; index < record.AdditionalPoints.Length; index++)
                {
                    RequireValue(record.AdditionalPoints[index].Ordinal, index + 1,
                        "ACG placeholder additional-point ordinal drifted.");
                }
            }

            Require(additional == shard.AdditionalPointCount.Value,
                "ACG placeholder shard decoded additional total drifted.");
            Require(targets == shard.CapturePlanTargetCount.Value,
                "ACG placeholder shard decoded target total drifted.");
        }

        private static void ValidateVisual(AcgVisualResolution visual)
        {
            RequireText(visual.AcgHashWireBytes, "ACG visual wire bytes are missing.");
            RequireText(visual.EvidenceGrade, "ACG visual evidence grade is missing.");
            if (string.Equals(visual.EvidenceGrade, "ExactOfficial", StringComparison.Ordinal))
            {
                Require(visual.AcgHashNativeUInt32 == 0x4644514F,
                    "Only FDQO may be ExactOfficial in this registry revision.");
                RequireValue(visual.ServerTemplateId, 43296, "FDQO server template drifted.");
                Require(string.Equals(visual.ServerTemplateHash, "A004", StringComparison.Ordinal),
                    "FDQO server template hash drifted.");
                RequireValue(visual.MonsterDataType, 1040023, "FDQO MonsterData type drifted.");
                RequireValue(visual.MonsterDataInstance, 17655, "FDQO MonsterData instance drifted.");
                RequireValue(visual.ExactMeshType, 1010002, "FDQO mesh type drifted.");
                RequireValue(visual.ExactMeshInstance, 15222, "FDQO mesh instance drifted.");
            }
            else
            {
                Require(!visual.ServerTemplateId.HasValue
                        && string.IsNullOrEmpty(visual.ServerTemplateHash)
                        && !visual.MonsterDataInstance.HasValue
                        && !visual.ExactMeshInstance.HasValue,
                    "Non-exact ACG visual gained an exact runtime identity.");
            }
        }

        private static AcgDevelopmentPlaceholderPlanEntry CreatePlanEntry(
            AcgDevelopmentPlaceholderRecord record,
            AcgDevelopmentPlaceholderPoint point,
            int? additionalPointOrdinal,
            AcgVisualResolution visual)
        {
            bool additional = additionalPointOrdinal.HasValue;
            return new AcgDevelopmentPlaceholderPlanEntry
            {
                BuildId = record.BuildId,
                ResourceType = record.ResourceType.Value,
                ResourceInstance = record.ResourceInstance.Value,
                PlayfieldName = record.PlayfieldName,
                DistrictIndex = record.DistrictIndex.Value,
                DistrictName = record.DistrictName,
                AcgHashNativeUInt32 = record.AcgHashNativeUInt32.Value,
                AcgHashText = record.AcgHashText,
                AcgHashDisplay = record.AcgHashDisplay,
                AcgHashWireBytes = record.AcgHashWireBytes,
                OfficialSpawnRecordId = record.OfficialSpawnRecordId,
                LocationKind = additional
                    ? AcgPlaceholderLocationKind.AdditionalPoint
                    : AcgPlaceholderLocationKind.Primary,
                AdditionalPointOrdinal = additionalPointOrdinal,
                EvidenceGrade = (AcgVisualEvidenceGrade)Enum.Parse(
                    typeof(AcgVisualEvidenceGrade),
                    record.EvidenceGrade,
                    false),
                KnownVisualEvidence = record.KnownVisualEvidence,
                VisualEvidenceNote = record.VisualEvidenceNote,
                PositionX = point.PositionX.Value,
                PositionY = point.PositionY.Value,
                PositionZ = point.PositionZ.Value,
                Radius = point.Radius.Value,
                RotationMidEncoded = point.RotationMidEncoded.Value,
                RotationWidthEncoded = point.RotationWidthEncoded.Value,
                RespawnChanceRaw = record.RespawnChanceRaw.Value,
                RespawnTimeRaw = record.RespawnTimeRaw.Value,
                VisibleName = CreateVisibleName(record, additionalPointOrdinal),
                UseExactOfficialVisual = visual.EvidenceGrade == "ExactOfficial",
                SelectedCatMeshId = visual.EvidenceGrade == "ExactOfficial"
                    ? ExactFdqoCatMeshId
                    : DefaultPlaceholderCatMeshId,
                SelectedVisualSource = visual.EvidenceGrade == "ExactOfficial"
                    ? "ExactOfficial FDQO"
                    : DefaultPlaceholderVisualSource,
                CanAttack = false,
                CanAggro = false,
                AwardsXp = false,
                ExposesLoot = false,
                Invulnerable = true,
                Stationary = true,
                Neutral = true,
                CollisionSuppressionProven = false
            };
        }

        private static string CreateVisibleName(
            AcgDevelopmentPlaceholderRecord record,
            int? additionalPointOrdinal)
        {
            string display = string.IsNullOrWhiteSpace(record.AcgHashDisplay)
                ? "0x" + record.AcgHashNativeUInt32.Value.ToString("X8", CultureInfo.InvariantCulture)
                : record.AcgHashDisplay;
            string prefix;
            if (additionalPointOrdinal.HasValue)
            {
                prefix = "[ADD]";
            }
            else if (string.Equals(record.EvidenceGrade, "ExactOfficial", StringComparison.Ordinal))
            {
                prefix = "[EXACT]";
            }
            else if (record.EvidenceGrade.StartsWith("CaptureCorrelated", StringComparison.Ordinal))
            {
                prefix = "[CORR]";
            }
            else
            {
                prefix = "[UNRES]";
            }

            string name = prefix + " ACG " + display;
            if (additionalPointOrdinal.HasValue)
            {
                name += " #" + additionalPointOrdinal.Value.ToString(CultureInfo.InvariantCulture);
            }

            return name.Length <= 31 ? name : name.Substring(0, 31);
        }

        private static T DeserializeJson<T>(string path)
        {
            string text = File.ReadAllText(path, new UTF8Encoding(false, true));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            try
            {
                return serializer.Deserialize<T>(text);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidDataException("Invalid ACG placeholder JSON: " + path, exception);
            }
        }

        private static string RequireRelativeExactFile(string root, string relative)
        {
            RequireText(relative, "ACG placeholder relative path is missing.");
            Require(relative.IndexOf('\\') < 0 && !Path.IsPathRooted(relative),
                "ACG placeholder relative path is not canonical.");
            string current = root;
            string[] parts = relative.Split('/');
            for (int index = 0; index < parts.Length - 1; index++)
            {
                current = RequireExactDirectory(current, parts[index]);
            }

            return RequireExactFile(current, parts[parts.Length - 1]);
        }

        private static string RequireExactDirectory(string root, string expectedName)
        {
            Require(Directory.Exists(root), "Required parent directory is missing: " + root);
            string match = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .SingleOrDefault(path => string.Equals(Path.GetFileName(path), expectedName, StringComparison.Ordinal));
            Require(match != null, "Required exact-cased directory is missing: " + expectedName);
            return match;
        }

        private static string RequireExactFile(string root, string expectedName)
        {
            string match = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly)
                .SingleOrDefault(path => string.Equals(Path.GetFileName(path), expectedName, StringComparison.Ordinal));
            Require(match != null, "Required exact-cased file is missing: " + expectedName);
            return match;
        }

        private static string HashFile(string path)
        {
            using (var sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void RequireSha256(string value, string message)
        {
            Require(value != null && value.Length == 64 && value == value.ToLowerInvariant()
                    && value.All(character => char.IsDigit(character) || (character >= 'a' && character <= 'f')),
                message);
        }

        private static void RequireValue(int? actual, int expected, string message)
        {
            Require(actual.HasValue && actual.Value == expected, message);
        }

        private static void RequireText(string actual, string message)
        {
            Require(!string.IsNullOrWhiteSpace(actual), message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }
    }

    internal sealed class AcgDevelopmentPlaceholderPlanEntry
    {
        public string BuildId { get; set; }
        public int ResourceType { get; set; }
        public int ResourceInstance { get; set; }
        public string PlayfieldName { get; set; }
        public int DistrictIndex { get; set; }
        public string DistrictName { get; set; }
        public uint AcgHashNativeUInt32 { get; set; }
        public string AcgHashText { get; set; }
        public string AcgHashDisplay { get; set; }
        public string AcgHashWireBytes { get; set; }
        public string OfficialSpawnRecordId { get; set; }
        public AcgPlaceholderLocationKind LocationKind { get; set; }
        public int? AdditionalPointOrdinal { get; set; }
        public AcgVisualEvidenceGrade EvidenceGrade { get; set; }
        public string KnownVisualEvidence { get; set; }
        public string VisualEvidenceNote { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public double Radius { get; set; }
        public int RotationMidEncoded { get; set; }
        public int RotationWidthEncoded { get; set; }
        public int RespawnChanceRaw { get; set; }
        public double RespawnTimeRaw { get; set; }
        public string VisibleName { get; set; }
        public bool UseExactOfficialVisual { get; set; }
        public int SelectedCatMeshId { get; set; }
        public string SelectedVisualSource { get; set; }
        public bool CanAttack { get; set; }
        public bool CanAggro { get; set; }
        public bool AwardsXp { get; set; }
        public bool ExposesLoot { get; set; }
        public bool Invulnerable { get; set; }
        public bool Stationary { get; set; }
        public bool Neutral { get; set; }
        public bool CollisionSuppressionProven { get; set; }
    }

    internal class AcgDevelopmentPlaceholderPoint
    {
        public double? PositionX { get; set; }
        public double? PositionY { get; set; }
        public double? PositionZ { get; set; }
        public double? Radius { get; set; }
        public int? RotationMidEncoded { get; set; }
        public int? RotationWidthEncoded { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderAdditionalPoint : AcgDevelopmentPlaceholderPoint
    {
        public int? Ordinal { get; set; }
        public int? RecordOffset { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderRecord
    {
        public string OfficialSpawnRecordId { get; set; }
        public string BuildId { get; set; }
        public int? ResourceType { get; set; }
        public int? ResourceInstance { get; set; }
        public string PlayfieldName { get; set; }
        public int? DistrictIndex { get; set; }
        public string DistrictName { get; set; }
        public uint? AcgHashNativeUInt32 { get; set; }
        public string AcgHashText { get; set; }
        public string AcgHashDisplay { get; set; }
        public string AcgHashWireBytes { get; set; }
        public string EvidenceGrade { get; set; }
        public string KnownVisualEvidence { get; set; }
        public string VisualEvidenceNote { get; set; }
        public bool? CapturePlanTarget { get; set; }
        public int? LevelMinimum { get; set; }
        public int? LevelMaximum { get; set; }
        public int? RespawnChanceRaw { get; set; }
        public double? RespawnTimeRaw { get; set; }
        public int? AssistanceRadius { get; set; }
        public int? NativeFlags { get; set; }
        public int? MoreFlags { get; set; }
        public int? UnknownOptionalU8 { get; set; }
        public long? RecordOffsetInDatabase { get; set; }
        public int? RecordOffsetInResource { get; set; }
        public string RecordSha256 { get; set; }
        public AcgDevelopmentPlaceholderPoint Primary { get; set; }
        public AcgDevelopmentPlaceholderAdditionalPoint[] AdditionalPoints { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderShard
    {
        public int? SchemaVersion { get; set; }
        public string BuildId { get; set; }
        public int? ResourceType { get; set; }
        public int? ResourceInstance { get; set; }
        public int? PrimaryRecordCount { get; set; }
        public int? AdditionalPointCount { get; set; }
        public int? CapturePlanTargetCount { get; set; }
        public AcgDevelopmentPlaceholderRecord[] Records { get; set; }
    }

    internal sealed class AcgVisualResolution
    {
        public uint? AcgHashNativeUInt32 { get; set; }
        public string AcgHashNativeUInt32Hex { get; set; }
        public string AcgHashText { get; set; }
        public string AcgHashDisplay { get; set; }
        public string AcgHashWireBytes { get; set; }
        public string EvidenceGrade { get; set; }
        public string KnownVisualEvidence { get; set; }
        public string VisualEvidenceNote { get; set; }
        public int[] AppearanceIds { get; set; }
        public string[] MeshResourceIds { get; set; }
        public bool? AdditionalVariantUnresolved { get; set; }
        public int? ServerTemplateId { get; set; }
        public string ServerTemplateHash { get; set; }
        public int? MonsterDataType { get; set; }
        public int? MonsterDataInstance { get; set; }
        public int? ExactMeshType { get; set; }
        public int? ExactMeshInstance { get; set; }
    }

    internal sealed class AcgVisualResolutionRegistry
    {
        public int? SchemaVersion { get; set; }
        public string BuildId { get; set; }
        public int? ResourceType { get; set; }
        public int? Count { get; set; }
        public Dictionary<string, int> EvidenceGradeCounts { get; set; }
        public AcgVisualResolution[] Entries { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderManifest
    {
        public int? SchemaVersion { get; set; }
        public string CorpusVersion { get; set; }
        public string PortablePackageName { get; set; }
        public string PortablePackageSha256 { get; set; }
        public string BuildId { get; set; }
        public int? ResourceType { get; set; }
        public AcgDevelopmentPlaceholderMetrics Metrics { get; set; }
        public AcgMalformedResource[] MalformedResources { get; set; }
        public string VisualRegistryPath { get; set; }
        public string VisualRegistrySha256 { get; set; }
        public AcgDevelopmentPlaceholderManifestPlayfield[] Playfields { get; set; }
        public AcgDevelopmentPlaceholderPolicy Policy { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderMetrics
    {
        public int? EnumeratedResourceCount { get; set; }
        public int? ParsedResourceCount { get; set; }
        public int? MalformedResourceCount { get; set; }
        public int? PlayfieldsWithPlacements { get; set; }
        public int? PrimaryRecordCount { get; set; }
        public int? AdditionalPointCount { get; set; }
        public int? TotalCoordinateCount { get; set; }
        public int? UniqueAcgHashCount { get; set; }
        public int? CapturePlanPlayfieldCount { get; set; }
        public int? CapturePlanTargetCount { get; set; }
        public int? DuplicatePrimaryCoordinateRowCount { get; set; }
        public int? ExactOfficialCount { get; set; }
        public int? CaptureCorrelatedCount { get; set; }
        public int? UnresolvedCount { get; set; }
        public int? Pf4582PrimaryRecordCount { get; set; }
        public int? Pf4582FdqoPlacementCount { get; set; }
    }

    internal sealed class AcgMalformedResource
    {
        public int? ResourceInstance { get; set; }
        public string ParseStatus { get; set; }
        public Dictionary<string, object> ParseError { get; set; }
        public string SourceFile { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderManifestPlayfield
    {
        public int? ResourceInstance { get; set; }
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public int? PrimaryRecordCount { get; set; }
        public int? AdditionalPointCount { get; set; }
        public int? CapturePlanTargetCount { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderPolicy
    {
        public string DefaultMode { get; set; }
        public bool? DevelopmentBuildOnly { get; set; }
        public bool? ProductionActivation { get; set; }
        public bool? RuntimeIdentityUsesSourceIdentity { get; set; }
        public bool? CaptureCorrelationPromotesExactIdentity { get; set; }
        public bool? AdditionalPointRuntimeSemanticsProven { get; set; }
        public string DefaultPlaceholderVisualSource { get; set; }
        public int? DefaultPlaceholderCatMeshId { get; set; }
        public string RespawnChanceFieldName { get; set; }
    }

    internal sealed class AcgDevelopmentPlaceholderCorpusAudit
    {
        public int PrimaryRecordCount { get; set; }
        public int AdditionalPointCount { get; set; }
        public int TotalCoordinateCount { get; set; }
        public int CapturePlanTargetCount { get; set; }
        public int DuplicatePrimaryCoordinateRowCount { get; set; }
        public int Pf4582PrimaryRecordCount { get; set; }
        public int Pf4582FdqoPlacementCount { get; set; }
        public bool Pf4582NcnnUnresolvedPresent { get; set; }
    }
}
