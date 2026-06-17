namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public enum AreteMissionState
    {
        NotStarted,
        Offered,
        Active,
        Completed,
        Failed,
        Abandoned
    }

    public sealed class MissionStateRecord
    {
        public string QuestId { get; set; }

        public string CurrentStepId { get; set; }

        public AreteMissionState State { get; set; }

        public string UnlockedByQuestId { get; set; }

        public string LastTransition { get; set; }
    }

    public sealed class MissionStateResult
    {
        public MissionStateResult(
            MissionStateRecord record,
            IEnumerable<AreteRecordedAction> recordedActions,
            AreteValidationResult validation)
        {
            this.Record = record;
            this.RecordedActions = new List<AreteRecordedAction>(
                recordedActions ?? Enumerable.Empty<AreteRecordedAction>());
            this.Validation = validation ?? new AreteValidationResult();
        }

        public MissionStateRecord Record { get; private set; }

        public IList<AreteRecordedAction> RecordedActions { get; private set; }

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
