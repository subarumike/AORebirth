namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

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

    public sealed class CapturedAreteMovementCatalog
    {
        public const int CapturedPlayfieldId = 1044525;

        public const int RuntimePlayfieldId = 6553;

        public const int PromotableObservationCount = 8229;

        public const int RuntimeRowCount = 8121;

        public const string RuntimeDatasetRelativePath =
            @"Content\Captured\Arete\movement";

        public const string RuntimeDatasetSourceRelativePath =
            @"AORebirth\Server\ZoneEngine\Content\Captured\Arete\movement";

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
            string[] candidates =
            {
                Path.Combine(baseDirectory, RuntimeDatasetRelativePath),
                Path.Combine(baseDirectory, RuntimeDatasetSourceRelativePath),
                RuntimeDatasetRelativePath,
                RuntimeDatasetSourceRelativePath
            };

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return Load(candidate, PromotableObservationCount, RuntimeRowCount);
                }
            }

            return Invalid("runtime-dataset-directory-missing");
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
                "ObservationId", "EquivalentObservationCount", "CapturedUtc", "Sequence",
                "Behavior", "NpcFamily", "MonsterData", "Level", "CapturedPlayfieldId",
                "RuntimePlayfieldId", "Name", "SourceIdentity", "SourceGeneration",
                "RouteSignature", "StartX", "StartY", "StartZ", "EndX", "EndY", "EndZ",
                "DelayAfterSeconds", "PathCount"
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
                if (!Enum.TryParse(columns[4], true, out behavior)
                    || behavior != expectedBehavior)
                {
                    throw new InvalidDataException("behavior-mismatch:" + Path.GetFileName(path));
                }

                int capturedPlayfield = ParseInt(columns[8]);
                int runtimePlayfield = ParseInt(columns[9]);
                string sourceIdentity = columns[11];
                if (capturedPlayfield != CapturedPlayfieldId
                    || runtimePlayfield != RuntimePlayfieldId
                    || !sourceIdentity.StartsWith("SimpleChar:", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(columns[10]))
                {
                    throw new InvalidDataException("identity-evidence-mismatch:" + Path.GetFileName(path));
                }

                int equivalentCount = ParseInt(columns[1]);
                int sourceGeneration = ParseInt(columns[12]);
                int pathCount = ParseInt(columns[21]);
                double delay = ParseDouble(columns[20]);
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
                        equivalentCount,
                        DateTime.Parse(
                            columns[2],
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                        long.Parse(columns[3], CultureInfo.InvariantCulture),
                        behavior,
                        ParseInt(columns[5]),
                        ParseInt(columns[6]),
                        ParseInt(columns[7]),
                        capturedPlayfield,
                        runtimePlayfield,
                        columns[10],
                        sourceIdentity,
                        sourceGeneration,
                        columns[13],
                        new CapturedAreteMovementPoint(
                            ParseDouble(columns[14]),
                            ParseDouble(columns[15]),
                            ParseDouble(columns[16])),
                        new CapturedAreteMovementPoint(
                            ParseDouble(columns[17]),
                            ParseDouble(columns[18]),
                            ParseDouble(columns[19])),
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
        public const double ActivationDistance = 6.0;

        public const double ContinuationDistance = 2.5;

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
                this.states.Remove(actor == null ? 0 : actor.RuntimeIdentity);
                return false;
            }

            var variants = matches
                .GroupBy(x => x.SourceIdentity + "|" + x.SourceGeneration.ToString(CultureInfo.InvariantCulture))
                .Select(
                    group =>
                        new
                        {
                            Key = group.Key,
                            SourceIdentity = group.First().SourceIdentity,
                            SourceGeneration = group.First().SourceGeneration,
                            Distance = group.Min(x => x.Start.Distance2D(actor.Position))
                        })
                .Where(x => x.Distance <= ActivationDistance)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(x => x.SourceGeneration)
                .ToArray();
            if (variants.Length == 0)
            {
                this.states.Remove(actor.RuntimeIdentity);
                return false;
            }

            int selectedIndex = PositiveModulo(actor.SpawnGeneration - 1, variants.Length);
            var selected = variants[selectedIndex];
            this.states[actor.RuntimeIdentity] =
                new RuntimeState(
                    CopyIdentity(actor),
                    selected.SourceIdentity,
                    selected.SourceGeneration);
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
                || !this.states.TryGetValue(actor.RuntimeIdentity, out state)
                || !state.IdentityMatches(actor)
                || !ConditionMatches(actor, behavior))
            {
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            if (state.ActiveBehavior != behavior)
            {
                state.ActiveBehavior = behavior;
                state.NextIndex = -1;
                state.NextEligibleUtc = DateTime.MinValue;
                state.LastEnd = null;
            }

            if (utcNow < state.NextEligibleUtc)
            {
                return CapturedAreteMovementDecisionKind.Waiting;
            }

            CapturedAreteMovementObservation[] candidates = this.catalog
                .Matching(actor, behavior)
                .Where(
                    x =>
                        string.Equals(x.SourceIdentity, state.SourceIdentity, StringComparison.Ordinal)
                        && x.SourceGeneration == state.SourceGeneration)
                .OrderBy(x => x.CapturedUtc)
                .ThenBy(x => x.Sequence)
                .ThenBy(x => x.ObservationId, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
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
                candidateIndex = 0;
            }

            CapturedAreteMovementObservation selected = candidates[candidateIndex];
            if (selected.Start.Distance2D(actor.Position) > ContinuationDistance
                || (state.LastEnd != null
                    && state.LastEnd.Distance2D(selected.Start) > 0.5)
                || !DirectionMatches(actor, selected))
            {
                state.NextIndex = -1;
                state.NextEligibleUtc = DateTime.MinValue;
                state.LastEnd = null;
                return CapturedAreteMovementDecisionKind.Fallback;
            }

            observation = selected;
            state.NextIndex = candidateIndex + 1;
            state.NextEligibleUtc = utcNow + TimeSpan.FromSeconds(selected.DelayAfterSeconds);
            state.LastEnd = selected.End;
            return CapturedAreteMovementDecisionKind.Movement;
        }

        public void Interrupt(int runtimeIdentity)
        {
            RuntimeState state;
            if (this.states.TryGetValue(runtimeIdentity, out state))
            {
                state.ActiveBehavior = null;
                state.NextIndex = -1;
                state.NextEligibleUtc = DateTime.MinValue;
                state.LastEnd = null;
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
            if (this.states.TryGetValue(runtimeIdentity, out state))
            {
                sourceIdentity = state.SourceIdentity;
                sourceGeneration = state.SourceGeneration;
                return true;
            }

            sourceIdentity = string.Empty;
            sourceGeneration = 0;
            return false;
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
            internal RuntimeState(
                CapturedAreteMovementActorEvidence identity,
                string sourceIdentity,
                int sourceGeneration)
            {
                this.Identity = identity;
                this.SourceIdentity = sourceIdentity;
                this.SourceGeneration = sourceGeneration;
                this.NextIndex = -1;
            }

            internal CapturedAreteMovementActorEvidence Identity { get; private set; }

            internal string SourceIdentity { get; private set; }

            internal int SourceGeneration { get; private set; }

            internal CapturedAreteMovementBehavior? ActiveBehavior { get; set; }

            internal int NextIndex { get; set; }

            internal DateTime NextEligibleUtc { get; set; }

            internal CapturedAreteMovementPoint LastEnd { get; set; }

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
}
