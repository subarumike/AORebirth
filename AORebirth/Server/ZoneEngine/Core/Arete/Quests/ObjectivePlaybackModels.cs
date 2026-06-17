namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public static class ObjectivePlaybackObservationTypes
    {
        public const string EnemyDeathObserved = "EnemyDeathObserved";

        public const string UseInteractionObserved = "UseInteractionObserved";

        public const string NpcTalkObserved = "NpcTalkObserved";
    }

    public sealed class ObjectivePlaybackObservation
    {
        public ObjectivePlaybackObservation()
        {
            this.Parameters = new Dictionary<string, string>();
        }

        public string ObservationType { get; set; }

        public string EvidenceReference { get; set; }

        public string TargetName { get; set; }

        public string TargetIdentity { get; set; }

        public string CapturedSignal { get; set; }

        public string ActionName { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
    }

    public sealed class ObjectiveProgressRecord
    {
        public string MissionId { get; set; }

        public string ObjectiveId { get; set; }

        public string ObjectiveType { get; set; }

        public int CurrentCount { get; set; }

        public int RequiredCount { get; set; }

        public bool Completed { get; set; }

        public int MatchedEvidenceCount { get; set; }

        public int IgnoredEvidenceCount { get; set; }

        public string LastMatchedEvidenceReference { get; set; }
    }

    public sealed class ObjectivePlaybackObservationResult
    {
        public ObjectivePlaybackObservationResult(
            ObjectivePlaybackObservation observation,
            IEnumerable<ObjectiveProgressRecord> matchedProgress,
            IEnumerable<ObjectiveProgressRecord> ignoredProgress,
            AreteValidationResult validation)
        {
            this.Observation = observation;
            this.MatchedProgress = new List<ObjectiveProgressRecord>(
                matchedProgress ?? Enumerable.Empty<ObjectiveProgressRecord>());
            this.IgnoredProgress = new List<ObjectiveProgressRecord>(
                ignoredProgress ?? Enumerable.Empty<ObjectiveProgressRecord>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public ObjectivePlaybackObservation Observation { get; private set; }

        public IList<ObjectiveProgressRecord> MatchedProgress { get; private set; }

        public IList<ObjectiveProgressRecord> IgnoredProgress { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }

    public sealed class ObjectivePlaybackReplayResult
    {
        public ObjectivePlaybackReplayResult(
            IEnumerable<ObjectivePlaybackObservationResult> observationResults,
            IEnumerable<ObjectiveProgressRecord> progress,
            AreteValidationResult validation)
        {
            this.ObservationResults = new List<ObjectivePlaybackObservationResult>(
                observationResults ?? Enumerable.Empty<ObjectivePlaybackObservationResult>());
            this.Progress = new List<ObjectiveProgressRecord>(
                progress ?? Enumerable.Empty<ObjectiveProgressRecord>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public IList<ObjectivePlaybackObservationResult> ObservationResults { get; private set; }

        public IList<ObjectiveProgressRecord> Progress { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }
}
