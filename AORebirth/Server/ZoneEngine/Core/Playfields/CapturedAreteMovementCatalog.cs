namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    #endregion

    public enum CapturedAreteMovementBehavior
    {
        Patrol,
        Spawn,
        Chase,
        Flee,
        Leash
    }

    public enum CapturedAreteMovementDecisionKind
    {
        Fallback,
        Waiting,
        Movement
    }

    public sealed class CapturedAreteMovementPoint
    {
        public CapturedAreteMovementPoint(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public double X { get; private set; }

        public double Y { get; private set; }

        public double Z { get; private set; }

        public double Distance2D(CapturedAreteMovementPoint other)
        {
            double dx = this.X - other.X;
            double dz = this.Z - other.Z;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }
    }

    public sealed class CapturedAreteMovementObservation
    {
        internal CapturedAreteMovementObservation(
            string observationId,
            string captureId,
            int equivalentObservationCount,
            DateTime capturedUtc,
            long sequence,
            CapturedAreteMovementBehavior behavior,
            int npcFamily,
            int monsterData,
            int level,
            int capturedPlayfieldId,
            int runtimePlayfieldId,
            string name,
            string sourceIdentity,
            int sourceGeneration,
            string routeSignature,
            CapturedAreteMovementPoint start,
            CapturedAreteMovementPoint end,
            double delayAfterSeconds,
            int pathCount)
        {
            this.ObservationId = observationId;
            this.CaptureId = captureId;
            this.EquivalentObservationCount = equivalentObservationCount;
            this.CapturedUtc = capturedUtc;
            this.Sequence = sequence;
            this.Behavior = behavior;
            this.NpcFamily = npcFamily;
            this.MonsterData = monsterData;
            this.Level = level;
            this.CapturedPlayfieldId = capturedPlayfieldId;
            this.RuntimePlayfieldId = runtimePlayfieldId;
            this.Name = name;
            this.SourceIdentity = sourceIdentity;
            this.SourceGeneration = sourceGeneration;
            this.RouteSignature = routeSignature;
            this.Start = start;
            this.End = end;
            this.DelayAfterSeconds = delayAfterSeconds;
            this.PathCount = pathCount;
        }

        public string ObservationId { get; private set; }

        public string CaptureId { get; private set; }

        public int EquivalentObservationCount { get; private set; }

        public DateTime CapturedUtc { get; private set; }

        public long Sequence { get; private set; }

        public CapturedAreteMovementBehavior Behavior { get; private set; }

        public int NpcFamily { get; private set; }

        public int MonsterData { get; private set; }

        public int Level { get; private set; }

        public int CapturedPlayfieldId { get; private set; }

        public int RuntimePlayfieldId { get; private set; }

        public string Name { get; private set; }

        public string SourceIdentity { get; private set; }

        public int SourceGeneration { get; private set; }

        public string RouteSignature { get; private set; }

        public CapturedAreteMovementPoint Start { get; private set; }

        public CapturedAreteMovementPoint End { get; private set; }

        public double DelayAfterSeconds { get; private set; }

        public int PathCount { get; private set; }
    }

    public sealed class CapturedAreteMovementActorEvidence
    {
        public int RuntimeIdentity { get; set; }

        public int SpawnGeneration { get; set; }

        public int NpcFamily { get; set; }

        public int MonsterData { get; set; }

        public int Level { get; set; }

        public int PlayfieldId { get; set; }

        public string Name { get; set; }

        public CapturedAreteMovementPoint Position { get; set; }

        public bool Fighting { get; set; }

        public bool ReturningHome { get; set; }

        public CapturedAreteMovementPoint TargetPosition { get; set; }

        public CapturedAreteMovementPoint HomePosition { get; set; }
    }

    internal sealed class CapturedAreteMovementManifest
    {
        public int schemaVersion { get; set; }

        public int capturedPlayfieldId { get; set; }

        public int runtimePlayfieldId { get; set; }

        public int sourcePromotableObservations { get; set; }

        public int deduplicatedRuntimeRows { get; set; }

        public int scriptedRuntimeRows { get; set; }

        public string[] captureIds { get; set; }

        public Dictionary<string, CapturedAreteMovementManifestBehavior> behaviors { get; set; }
    }

    internal sealed class CapturedAreteMovementManifestBehavior
    {
        public int sourceObservations { get; set; }

        public int runtimeRows { get; set; }
    }

    public sealed class CapturedAreteMovementCatalog
    {
        public const int CapturedPlayfieldId = 1044525;

        public const int RuntimePlayfieldId = 6553;

        public const int PromotableObservationCount = 20573;

        public const int RuntimeRowCount = 20267;

        public const string RuntimeDatasetRelativePath =
            @"Content\Captured\Arete\movement-full";

        public const string RuntimeDatasetSourceRelativePath =
            @"AORebirth\Server\ZoneEngine\Content\Captured\Arete\movement-full";

        private static readonly CapturedAreteMovementBehavior[] Behaviors =
        {
            CapturedAreteMovementBehavior.Patrol,
            CapturedAreteMovementBehavior.Spawn,
            CapturedAreteMovementBehavior.Chase,
            CapturedAreteMovementBehavior.Flee,
            CapturedAreteMovementBehavior.Leash
        };

        private readonly CapturedAreteMovementObservation[] observations;

        private CapturedAreteMovementCatalog(
            CapturedAreteMovementObservation[] observations,
            bool isValid,
            string failureReason,
            int sourceObservationCount)
        {
            this.observations = observations ?? new CapturedAreteMovementObservation[0];
            this.IsValid = isValid;
            this.FailureReason = failureReason ?? string.Empty;
            this.SourceObservationCount = sourceObservationCount;
        }

        public bool IsValid { get; private set; }

        public string FailureReason { get; private set; }

        public int RuntimeObservationCount
        {
            get { return this.observations.Length; }
        }

        public int SourceObservationCount { get; private set; }

        public int Count(CapturedAreteMovementBehavior behavior)
        {
            return this.observations.Count(x => x.Behavior == behavior);
        }

        public static CapturedAreteMovementCatalog LoadDefault()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string candidate in EnumerateDefaultCandidates(
                baseDirectory,
                RuntimeDatasetRelativePath,
                RuntimeDatasetSourceRelativePath))
            {
                if (Directory.Exists(candidate))
                {
                    return Load(candidate);
                }
            }

            return Invalid("runtime-dataset-directory-missing");
        }

        internal static IEnumerable<string> EnumerateDefaultCandidates(
            string baseDirectory,
            params string[] relativePaths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DirectoryInfo cursor = new DirectoryInfo(Path.GetFullPath(baseDirectory));
            while (cursor != null)
            {
                for (int index = 0; index < relativePaths.Length; index++)
                {
                    string candidate = Path.GetFullPath(
                        Path.Combine(cursor.FullName, relativePaths[index]));
                    if (seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                cursor = cursor.Parent;
            }
        }

        public static CapturedAreteMovementCatalog Load(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Invalid("runtime-dataset-directory-missing");
            }

            string manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return Invalid("runtime-dataset-manifest-missing");
            }

            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                CapturedAreteMovementManifest manifest =
                    serializer.Deserialize<CapturedAreteMovementManifest>(
                        File.ReadAllText(manifestPath));
                if (manifest == null
                    || manifest.schemaVersion != 4
                    || manifest.capturedPlayfieldId != CapturedPlayfieldId
                    || manifest.runtimePlayfieldId != RuntimePlayfieldId
                    || manifest.sourcePromotableObservations <= 0
                    || manifest.deduplicatedRuntimeRows <= 0
                    || manifest.scriptedRuntimeRows != 0
                    || manifest.captureIds == null
                    || manifest.captureIds.Length == 0
                    || manifest.captureIds.Any(string.IsNullOrWhiteSpace))
                {
                    return Invalid("runtime-dataset-manifest-invalid");
                }

                CapturedAreteMovementCatalog catalog = Load(
                    directory,
                    manifest.sourcePromotableObservations,
                    manifest.deduplicatedRuntimeRows);
                if (!catalog.IsValid)
                {
                    return catalog;
                }

                var captureIds = new HashSet<string>(
                    manifest.captureIds,
                    StringComparer.Ordinal);
                if (captureIds.Count != manifest.captureIds.Length
                    || catalog.observations.Any(x => !captureIds.Contains(x.CaptureId)))
                {
                    return Invalid("runtime-dataset-manifest-capture-mismatch");
                }

                if (manifest.behaviors == null)
                {
                    return Invalid("runtime-dataset-manifest-behaviors-missing");
                }

                foreach (CapturedAreteMovementBehavior behavior in Behaviors)
                {
                    CapturedAreteMovementManifestBehavior evidence;
                    string key = behavior.ToString().ToLowerInvariant();
                    if (!manifest.behaviors.TryGetValue(key, out evidence)
                        || evidence == null
                        || evidence.runtimeRows != catalog.Count(behavior)
                        || evidence.sourceObservations
                           != catalog.observations
                               .Where(x => x.Behavior == behavior)
                               .Sum(x => x.EquivalentObservationCount))
                    {
                        return Invalid("runtime-dataset-manifest-behavior-mismatch:" + key);
                    }
                }

                return catalog;
            }
            catch (Exception ex)
            {
                return Invalid("runtime-dataset-manifest-invalid:" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        public static CapturedAreteMovementCatalog Load(
            string directory,
            int expectedSourceObservationCount,
            int expectedRuntimeRowCount)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return Invalid("runtime-dataset-directory-missing");
            }

            try
            {
                var observations = new List<CapturedAreteMovementObservation>();
                foreach (CapturedAreteMovementBehavior behavior in Behaviors)
                {
                    string path = Path.Combine(
                        directory,
                        behavior.ToString().ToLowerInvariant() + ".csv");
                    if (!File.Exists(path))
                    {
                        return Invalid("runtime-dataset-file-missing:" + Path.GetFileName(path));
                    }

                    LoadFile(path, behavior, observations);
                }

                int sourceCount = observations.Sum(x => x.EquivalentObservationCount);
                if (observations
                    .GroupBy(x => x.ObservationId, StringComparer.Ordinal)
                    .Any(x => x.Count() != 1))
                {
                    return Invalid("runtime-observation-id-duplicate");
                }

                if (observations.Count != expectedRuntimeRowCount)
                {
                    return Invalid(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "runtime-row-count-mismatch:{0}!={1}",
                            observations.Count,
                            expectedRuntimeRowCount));
                }

                if (sourceCount != expectedSourceObservationCount)
                {
                    return Invalid(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "source-observation-count-mismatch:{0}!={1}",
                            sourceCount,
                            expectedSourceObservationCount));
                }

                CapturedAreteMovementObservation[] ordered = observations
                    .OrderBy(x => x.Behavior)
                    .ThenBy(x => x.CaptureId, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceIdentity, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceGeneration)
                    .ThenBy(x => x.CapturedUtc)
                    .ThenBy(x => x.Sequence)
                    .ThenBy(x => x.ObservationId, StringComparer.Ordinal)
                    .ToArray();
                return new CapturedAreteMovementCatalog(ordered, true, string.Empty, sourceCount);
            }
            catch (Exception ex)
            {
                return Invalid("runtime-dataset-invalid:" + ex.GetType().Name + ":" + ex.Message);
            }
        }

        internal CapturedAreteMovementObservation[] Matching(
            CapturedAreteMovementActorEvidence actor,
            CapturedAreteMovementBehavior? behavior)
        {
            if (!this.IsValid || !MatchesIdentity(actor))
            {
                return new CapturedAreteMovementObservation[0];
            }

            return this.observations
                .Where(
                    x =>
                        (!behavior.HasValue || x.Behavior == behavior.Value)
                        && x.NpcFamily == actor.NpcFamily
                        && x.MonsterData == actor.MonsterData
                        && x.Level == actor.Level
                        && x.RuntimePlayfieldId == actor.PlayfieldId
                        && string.Equals(x.Name, actor.Name, StringComparison.Ordinal))
                .ToArray();
        }

        private static bool MatchesIdentity(CapturedAreteMovementActorEvidence actor)
        {
            return actor != null
                   && actor.RuntimeIdentity > 0
                   && actor.SpawnGeneration > 0
                   && actor.NpcFamily >= 0
                   && actor.MonsterData > 0
                   && actor.Level > 0
                   && actor.PlayfieldId == RuntimePlayfieldId
                   && !string.IsNullOrWhiteSpace(actor.Name)
                   && actor.Position != null;
        }

        private static void LoadFile(
            string path,
            CapturedAreteMovementBehavior expectedBehavior,
            ICollection<CapturedAreteMovementObservation> destination)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 1)
            {
                throw new InvalidDataException("empty-file:" + Path.GetFileName(path));
            }

            string[] expectedHeader =
            {
                "ObservationId", "CaptureId", "EquivalentObservationCount", "CapturedUtc",
                "Sequence", "Behavior", "NpcFamily", "MonsterData", "Level",
                "CapturedPlayfieldId", "RuntimePlayfieldId", "Name", "SourceIdentity",
                "SourceGeneration", "RouteSignature", "StartX", "StartY", "StartZ",
                "EndX", "EndY", "EndZ", "DelayAfterSeconds", "PathCount"
            };
            string[] header = SplitCsvLine(lines[0]);
            if (!header.SequenceEqual(expectedHeader))
            {
                throw new InvalidDataException("header-mismatch:" + Path.GetFileName(path));
            }

            for (int index = 1; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                string[] columns = SplitCsvLine(lines[index]);
                if (columns.Length != expectedHeader.Length)
                {
                    throw new InvalidDataException("column-count:" + Path.GetFileName(path));
                }

                CapturedAreteMovementBehavior behavior;
                if (!Enum.TryParse(columns[5], true, out behavior)
                    || behavior != expectedBehavior)
                {
                    throw new InvalidDataException("behavior-mismatch:" + Path.GetFileName(path));
                }

                int capturedPlayfield = ParseInt(columns[9]);
                int runtimePlayfield = ParseInt(columns[10]);
                string sourceIdentity = columns[12];
                if (capturedPlayfield != CapturedPlayfieldId
                    || runtimePlayfield != RuntimePlayfieldId
                    || !sourceIdentity.StartsWith("SimpleChar:", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(columns[1])
                    || string.IsNullOrWhiteSpace(columns[11]))
                {
                    throw new InvalidDataException("identity-evidence-mismatch:" + Path.GetFileName(path));
                }

                int equivalentCount = ParseInt(columns[2]);
                int sourceGeneration = ParseInt(columns[13]);
                int pathCount = ParseInt(columns[22]);
                double delay = ParseDouble(columns[21]);
                if (equivalentCount <= 0
                    || sourceGeneration < 0
                    || pathCount <= 0
                    || delay < 0.0)
                {
                    throw new InvalidDataException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "invalid-observation:{0}:{1}:equivalent={2}:generation={3}:pathCount={4}:delay={5}",
                        Path.GetFileName(path),
                        columns[0],
                            equivalentCount,
                            sourceGeneration,
                            pathCount,
                            delay));
                }

                destination.Add(
                    new CapturedAreteMovementObservation(
                        columns[0],
                        columns[1],
                        equivalentCount,
                        DateTime.Parse(
                            columns[3],
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                        long.Parse(columns[4], CultureInfo.InvariantCulture),
                        behavior,
                        ParseInt(columns[6]),
                        ParseInt(columns[7]),
                        ParseInt(columns[8]),
                        capturedPlayfield,
                        runtimePlayfield,
                        columns[11],
                        sourceIdentity,
                        sourceGeneration,
                        columns[14],
                        new CapturedAreteMovementPoint(
                            ParseDouble(columns[15]),
                            ParseDouble(columns[16]),
                            ParseDouble(columns[17])),
                        new CapturedAreteMovementPoint(
                            ParseDouble(columns[18]),
                            ParseDouble(columns[19]),
                            ParseDouble(columns[20])),
                        delay,
                        pathCount));
            }
        }

        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var field = new System.Text.StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (current == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(current);
                }
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static CapturedAreteMovementCatalog Invalid(string reason)
        {
            return new CapturedAreteMovementCatalog(
                new CapturedAreteMovementObservation[0],
                false,
                reason,
                0);
        }
    }

    public sealed class CapturedAreteMovementRuntimeCoordinator
    {
        public static bool PatrolConditionMatches(
            bool hasNpcController,
            bool hasFightingTarget)
        {
            return hasNpcController && !hasFightingTarget;
        }

        private readonly CapturedAreteMovementCatalog catalog;

        private readonly Dictionary<int, RuntimeState> states =
            new Dictionary<int, RuntimeState>();

        public CapturedAreteMovementRuntimeCoordinator(CapturedAreteMovementCatalog catalog)
        {
            this.catalog = catalog;
        }

        public bool Activate(CapturedAreteMovementActorEvidence actor)
        {
            CapturedAreteMovementObservation[] matches = this.catalog.Matching(actor, null);
            if (matches.Length == 0)
            {
                if (actor != null)
                {
                    this.states.Remove(actor.RuntimeIdentity);
                }

                return false;
            }

            this.states[actor.RuntimeIdentity] =
                new RuntimeState(CopyIdentity(actor));
            return true;
        }

        public CapturedAreteMovementDecisionKind Select(
            CapturedAreteMovementActorEvidence actor,
            CapturedAreteMovementBehavior behavior,
            DateTime utcNow,
            out CapturedAreteMovementObservation observation)
        {
            observation = null;
            RuntimeState state;
            if (actor == null
                || !this.states.TryGetValue(actor.RuntimeIdentity, out state))
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (!state.IdentityMatches(actor))
            {
                this.states.Remove(actor.RuntimeIdentity);
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (!ConditionMatches(actor, behavior))
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (state.InterruptedBehavior == behavior)
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (state.ActiveBehavior != behavior)
            {
                state.BeginBehavior(behavior);
            }

            if (utcNow < state.NextEligibleUtc)
            {
                return CapturedAreteMovementDecisionKind.Waiting;
            }

            CapturedAreteMovementObservation[] behaviorCandidates =
                this.catalog.Matching(actor, behavior);
            if (behaviorCandidates.Length == 0)
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (!state.HasSelectedVariant)
            {
                var variants = behaviorCandidates
                    .GroupBy(
                        x =>
                            x.CaptureId + "|" + x.SourceIdentity + "|"
                            + x.SourceGeneration.ToString(CultureInfo.InvariantCulture))
                    .Select(
                        group =>
                            new
                            {
                                CaptureId = group.First().CaptureId,
                                SourceIdentity = group.First().SourceIdentity,
                                SourceGeneration = group.First().SourceGeneration,
                                Distance = group.Min(x => x.Start.Distance2D(actor.Position))
                            })
                    .OrderBy(x => x.Distance)
                    .ThenBy(x => x.CaptureId, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceIdentity, StringComparer.Ordinal)
                    .ThenBy(x => x.SourceGeneration)
                    .ToArray();
                double nearestVariantDistance = variants[0].Distance;
                var nearestVariants = variants
                    .Where(x => Math.Abs(x.Distance - nearestVariantDistance) <= 0.001)
                    .ToArray();
                var selectedVariant = nearestVariants[
                    PositiveModulo(actor.SpawnGeneration - 1, nearestVariants.Length)];
                state.SelectVariant(
                    selectedVariant.CaptureId,
                    selectedVariant.SourceIdentity,
                    selectedVariant.SourceGeneration);
            }

            CapturedAreteMovementObservation[] candidates = behaviorCandidates
                .Where(
                    x =>
                        string.Equals(x.CaptureId, state.CaptureId, StringComparison.Ordinal)
                        && string.Equals(x.SourceIdentity, state.SourceIdentity, StringComparison.Ordinal)
                        && x.SourceGeneration == state.SourceGeneration)
                .OrderBy(x => x.CapturedUtc)
                .ThenBy(x => x.Sequence)
                .ThenBy(x => x.ObservationId, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                state.ClearVariant();
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            int candidateIndex = state.NextIndex;
            if (candidateIndex < 0)
            {
                double nearest = candidates.Min(x => x.Start.Distance2D(actor.Position));
                int[] nearestIndexes = candidates
                    .Select((candidate, index) => new { candidate, index })
                    .Where(x => Math.Abs(x.candidate.Start.Distance2D(actor.Position) - nearest) <= 0.001)
                    .Select(x => x.index)
                    .ToArray();
                candidateIndex =
                    nearestIndexes[PositiveModulo(actor.SpawnGeneration - 1, nearestIndexes.Length)];
            }
            else if (candidateIndex >= candidates.Length)
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            CapturedAreteMovementObservation selected = candidates[candidateIndex];
            if (!DirectionMatches(actor, selected))
            {
                state.ClearVariant();
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            observation = selected;
            state.NextIndex = candidateIndex + 1;
            DateTime scheduleAnchor = state.NextEligibleUtc == DateTime.MinValue
                ? utcNow
                : state.NextEligibleUtc;
            state.NextEligibleUtc =
                scheduleAnchor + TimeSpan.FromSeconds(selected.DelayAfterSeconds);
            return CapturedAreteMovementDecisionKind.Movement;
        }

        public void Interrupt(int runtimeIdentity)
        {
            RuntimeState state;
            if (this.states.TryGetValue(runtimeIdentity, out state))
            {
                state.Reset();
            }
        }

        public void Remove(int runtimeIdentity)
        {
            this.states.Remove(runtimeIdentity);
        }

        public void Clear()
        {
            this.states.Clear();
        }

        public bool TryGetCapturedIdentity(
            int runtimeIdentity,
            out string sourceIdentity,
            out int sourceGeneration)
        {
            RuntimeState state;
            if (this.states.TryGetValue(runtimeIdentity, out state)
                && state.HasSelectedVariant)
            {
                sourceIdentity = state.SourceIdentity;
                sourceGeneration = state.SourceGeneration;
                return true;
            }

            sourceIdentity = string.Empty;
            sourceGeneration = 0;
            return false;
        }

        public bool HasActiveSequence(
            int runtimeIdentity,
            CapturedAreteMovementBehavior behavior)
        {
            RuntimeState state;
            return this.states.TryGetValue(runtimeIdentity, out state)
                   && state.ActiveBehavior == behavior
                   && state.HasSelectedVariant;
        }

        private static bool ConditionMatches(
            CapturedAreteMovementActorEvidence actor,
            CapturedAreteMovementBehavior behavior)
        {
            switch (behavior)
            {
                case CapturedAreteMovementBehavior.Spawn:
                    return !actor.Fighting && !actor.ReturningHome;
                case CapturedAreteMovementBehavior.Patrol:
                    return !actor.Fighting && !actor.ReturningHome;
                case CapturedAreteMovementBehavior.Chase:
                case CapturedAreteMovementBehavior.Flee:
                    return actor.Fighting
                           && !actor.ReturningHome
                           && actor.TargetPosition != null;
                case CapturedAreteMovementBehavior.Leash:
                    return !actor.Fighting
                           && actor.ReturningHome
                           && actor.HomePosition != null;
                default:
                    return false;
            }
        }

        private static bool DirectionMatches(
            CapturedAreteMovementActorEvidence actor,
            CapturedAreteMovementObservation observation)
        {
            if (observation.Behavior == CapturedAreteMovementBehavior.Chase)
            {
                return observation.End.Distance2D(actor.TargetPosition)
                       < observation.Start.Distance2D(actor.TargetPosition);
            }

            if (observation.Behavior == CapturedAreteMovementBehavior.Flee)
            {
                return observation.End.Distance2D(actor.TargetPosition)
                       > observation.Start.Distance2D(actor.TargetPosition);
            }

            if (observation.Behavior == CapturedAreteMovementBehavior.Leash)
            {
                return observation.End.Distance2D(actor.HomePosition)
                       < observation.Start.Distance2D(actor.HomePosition);
            }

            return true;
        }

        private static CapturedAreteMovementActorEvidence CopyIdentity(
            CapturedAreteMovementActorEvidence actor)
        {
            return new CapturedAreteMovementActorEvidence
                   {
                       RuntimeIdentity = actor.RuntimeIdentity,
                       SpawnGeneration = actor.SpawnGeneration,
                       NpcFamily = actor.NpcFamily,
                       MonsterData = actor.MonsterData,
                       Level = actor.Level,
                       PlayfieldId = actor.PlayfieldId,
                       Name = actor.Name,
                       Position = actor.Position
                   };
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private sealed class RuntimeState
        {
            internal RuntimeState(CapturedAreteMovementActorEvidence identity)
            {
                this.Identity = identity;
                this.NextIndex = -1;
            }

            internal CapturedAreteMovementActorEvidence Identity { get; private set; }

            internal string CaptureId { get; private set; }

            internal string SourceIdentity { get; private set; }

            internal int SourceGeneration { get; private set; }

            internal CapturedAreteMovementBehavior? ActiveBehavior { get; set; }

            internal CapturedAreteMovementBehavior? InterruptedBehavior { get; private set; }

            internal int NextIndex { get; set; }

            internal DateTime NextEligibleUtc { get; set; }

            internal bool HasSelectedVariant
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(this.CaptureId)
                           && !string.IsNullOrWhiteSpace(this.SourceIdentity);
                }
            }

            internal void BeginBehavior(CapturedAreteMovementBehavior behavior)
            {
                if (this.InterruptedBehavior != behavior)
                {
                    this.InterruptedBehavior = null;
                }

                this.ActiveBehavior = behavior;
                this.ClearVariant();
            }

            internal void SelectVariant(
                string captureId,
                string sourceIdentity,
                int sourceGeneration)
            {
                this.CaptureId = captureId;
                this.SourceIdentity = sourceIdentity;
                this.SourceGeneration = sourceGeneration;
                this.NextIndex = -1;
                this.NextEligibleUtc = DateTime.MinValue;
            }

            internal void ClearVariant()
            {
                this.CaptureId = string.Empty;
                this.SourceIdentity = string.Empty;
                this.SourceGeneration = 0;
                this.NextIndex = -1;
                this.NextEligibleUtc = DateTime.MinValue;
            }

            internal void Reset()
            {
                this.InterruptedBehavior = this.ActiveBehavior;
                this.ActiveBehavior = null;
                this.ClearVariant();
            }

            internal bool IdentityMatches(CapturedAreteMovementActorEvidence actor)
            {
                return actor.RuntimeIdentity == this.Identity.RuntimeIdentity
                       && actor.SpawnGeneration == this.Identity.SpawnGeneration
                       && actor.NpcFamily == this.Identity.NpcFamily
                       && actor.MonsterData == this.Identity.MonsterData
                       && actor.Level == this.Identity.Level
                       && actor.PlayfieldId == this.Identity.PlayfieldId
                       && string.Equals(actor.Name, this.Identity.Name, StringComparison.Ordinal);
            }
        }
    }

    public sealed class CapturedAreteAggroObservation
    {
        public string Name { get; set; }

        public int NpcFamily { get; set; }

        public int MonsterData { get; set; }

        public int Level { get; set; }

        public int CapturedPlayfieldId { get; set; }

        public int RuntimePlayfieldId { get; set; }

        public int NpcFirstAttackStarts { get; set; }

        public bool AutomaticAggroEligible { get; set; }

        public double? ObservedAutomaticAggroRadiusMeters { get; set; }

        public string RadiusEvidenceKind { get; set; }

        public string RadiusEvidenceCaptureId { get; set; }

        public string ContributingCaptureIds { get; set; }

        public bool Matches(CapturedAreteMovementActorEvidence actor)
        {
            return actor != null
                   && actor.NpcFamily == this.NpcFamily
                   && actor.MonsterData == this.MonsterData
                   && actor.Level == this.Level
                   && actor.PlayfieldId == this.RuntimePlayfieldId
                   && string.Equals(actor.Name, this.Name, StringComparison.Ordinal);
        }
    }

    public sealed class CapturedAreteAggroCatalog
    {
        private const string RuntimeRelativePath =
            @"Content\Captured\Arete\aggro.csv";

        private const string SourceRelativePath =
            @"AORebirth\Server\ZoneEngine\Content\Captured\Arete\aggro.csv";

        private readonly CapturedAreteAggroObservation[] observations;

        private CapturedAreteAggroCatalog(
            CapturedAreteAggroObservation[] observations,
            bool isValid,
            string failureReason)
        {
            this.observations = observations ?? new CapturedAreteAggroObservation[0];
            this.IsValid = isValid;
            this.FailureReason = failureReason ?? string.Empty;
        }

        public bool IsValid { get; private set; }

        public string FailureReason { get; private set; }

        public int Count
        {
            get { return this.observations.Length; }
        }

        public static CapturedAreteAggroCatalog LoadDefault()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string candidate in CapturedAreteMovementCatalog.EnumerateDefaultCandidates(
                baseDirectory,
                RuntimeRelativePath,
                SourceRelativePath))
            {
                if (File.Exists(candidate))
                {
                    return Load(candidate);
                }
            }

            return Invalid("aggro-dataset-missing");
        }

        public static CapturedAreteAggroCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return Invalid("aggro-dataset-missing");
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                const string expectedHeader =
                    "Name,NpcFamily,MonsterData,Level,CapturedPlayfieldId,"
                    + "RuntimePlayfieldId,NpcFirstAttackStarts,"
                    + "AutomaticAggroEligible,ObservedAutomaticAggroRadiusMeters,"
                    + "RadiusEvidenceKind,RadiusEvidenceCaptureId,"
                    + "RadiusEvidenceCapturedUtc,RadiusEvidenceSequence,"
                    + "ContributingCaptureIds";
                if (lines.Length == 0
                    || !string.Equals(lines[0], expectedHeader, StringComparison.Ordinal))
                {
                    return Invalid("aggro-header-mismatch");
                }

                var rows = new List<CapturedAreteAggroObservation>();
                for (int index = 1; index < lines.Length; index++)
                {
                    if (string.IsNullOrWhiteSpace(lines[index]))
                    {
                        continue;
                    }

                    string[] columns = lines[index].Split(',');
                    int family;
                    int template;
                    int level;
                    int capturedPlayfield;
                    int runtimePlayfield;
                    int starts;
                    bool eligible;
                    double parsedRadius;
                    double? radius = null;
                    long evidenceSequence;
                    DateTime evidenceCapturedUtc;
                    if (columns.Length != 14
                        || string.IsNullOrWhiteSpace(columns[0])
                        || !int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out family)
                        || !int.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out template)
                        || !int.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)
                        || !int.TryParse(columns[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out capturedPlayfield)
                        || !int.TryParse(columns[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out runtimePlayfield)
                        || !int.TryParse(columns[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out starts)
                        || !bool.TryParse(columns[7], out eligible)
                        || family <= 0
                        || template <= 0
                        || level <= 0
                        || starts <= 0
                        || !eligible
                        || capturedPlayfield != CapturedAreteMovementCatalog.CapturedPlayfieldId
                        || runtimePlayfield != CapturedAreteMovementCatalog.RuntimePlayfieldId
                        || string.IsNullOrWhiteSpace(columns[9])
                        || string.IsNullOrWhiteSpace(columns[13]))
                    {
                        return Invalid("aggro-row-invalid:" + index);
                    }

                    string[] contributingCaptureIds = columns[13].Split(
                        new[] { ';' },
                        StringSplitOptions.RemoveEmptyEntries);
                    if (contributingCaptureIds.Length == 0
                        || contributingCaptureIds.Any(string.IsNullOrWhiteSpace)
                        || contributingCaptureIds.Distinct(StringComparer.Ordinal).Count()
                           != contributingCaptureIds.Length)
                    {
                        return Invalid("aggro-row-invalid:" + index);
                    }

                    if (string.Equals(columns[9], "measured-lower-bound", StringComparison.Ordinal))
                    {
                        if (!double.TryParse(
                                columns[8],
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out parsedRadius)
                            || parsedRadius <= 0.0d
                            || double.IsNaN(parsedRadius)
                            || double.IsInfinity(parsedRadius)
                            || string.IsNullOrWhiteSpace(columns[10])
                            || !contributingCaptureIds.Contains(columns[10], StringComparer.Ordinal)
                            || !DateTime.TryParse(
                                columns[11],
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                out evidenceCapturedUtc)
                            || !long.TryParse(
                                columns[12],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out evidenceSequence)
                            || evidenceSequence <= 0)
                        {
                            return Invalid("aggro-radius-evidence-invalid:" + index);
                        }

                        radius = parsedRadius;
                    }
                    else if (!string.Equals(columns[9], "eligibility-only", StringComparison.Ordinal)
                             || !string.IsNullOrWhiteSpace(columns[8])
                             || !string.IsNullOrWhiteSpace(columns[10])
                             || !string.IsNullOrWhiteSpace(columns[11])
                             || !string.IsNullOrWhiteSpace(columns[12]))
                    {
                        return Invalid("aggro-radius-evidence-invalid:" + index);
                    }

                    rows.Add(
                        new CapturedAreteAggroObservation
                        {
                            Name = columns[0],
                            NpcFamily = family,
                            MonsterData = template,
                            Level = level,
                            CapturedPlayfieldId = capturedPlayfield,
                            RuntimePlayfieldId = runtimePlayfield,
                            NpcFirstAttackStarts = starts,
                            AutomaticAggroEligible = eligible,
                            ObservedAutomaticAggroRadiusMeters = radius,
                            RadiusEvidenceKind = columns[9],
                            RadiusEvidenceCaptureId = columns[10],
                            ContributingCaptureIds = columns[13]
                        });
                }

                if (rows
                    .GroupBy(
                        value => new
                                 {
                                     value.Name,
                                     value.NpcFamily,
                                     value.MonsterData,
                                     value.Level,
                                     value.CapturedPlayfieldId,
                                     value.RuntimePlayfieldId
                                 })
                    .Any(group => group.Count() != 1))
                {
                    return Invalid("aggro-duplicate-constraint");
                }

                return rows.Count == 0
                    ? Invalid("aggro-dataset-empty")
                    : new CapturedAreteAggroCatalog(rows.ToArray(), true, string.Empty);
            }
            catch (Exception exception)
            {
                return Invalid("aggro-load-failed:" + exception.GetType().Name);
            }
        }

        public bool TryGetRadius(
            CapturedAreteMovementActorEvidence actor,
            out double radius)
        {
            radius = 0.0d;
            if (!this.IsValid || actor == null)
            {
                return false;
            }

            CapturedAreteAggroObservation match = this.FindMatch(actor);
            if (match == null || !match.ObservedAutomaticAggroRadiusMeters.HasValue)
            {
                return false;
            }

            radius = match.ObservedAutomaticAggroRadiusMeters.Value;
            return true;
        }

        public bool TryGetEligibility(
            CapturedAreteMovementActorEvidence actor,
            out int npcFirstAttackStarts)
        {
            npcFirstAttackStarts = 0;
            if (!this.IsValid || actor == null)
            {
                return false;
            }

            CapturedAreteAggroObservation match = this.FindMatch(actor);
            if (match == null || !match.AutomaticAggroEligible)
            {
                return false;
            }

            npcFirstAttackStarts = match.NpcFirstAttackStarts;
            return true;
        }

        private CapturedAreteAggroObservation FindMatch(
            CapturedAreteMovementActorEvidence actor)
        {
            return this.observations.FirstOrDefault(value => value.Matches(actor));
        }

        private static CapturedAreteAggroCatalog Invalid(string reason)
        {
            return new CapturedAreteAggroCatalog(
                new CapturedAreteAggroObservation[0],
                false,
                reason);
        }
    }
}
