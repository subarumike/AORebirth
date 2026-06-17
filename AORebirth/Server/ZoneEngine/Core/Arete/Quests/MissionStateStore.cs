namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public sealed class MissionStateStore
    {
        private readonly Dictionary<string, MissionStateRecord> recordsByQuestId =
            new Dictionary<string, MissionStateRecord>(System.StringComparer.OrdinalIgnoreCase);

        public MissionStateRecord GetOrCreate(string questId)
        {
            MissionStateRecord record;
            if (this.recordsByQuestId.TryGetValue(questId, out record))
            {
                return record;
            }

            record = new MissionStateRecord
            {
                QuestId = questId,
                State = AreteMissionState.NotStarted
            };

            this.recordsByQuestId[questId] = record;
            return record;
        }

        public bool TryGetRecord(string questId, out MissionStateRecord record)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                record = null;
                return false;
            }

            return this.recordsByQuestId.TryGetValue(questId, out record);
        }
    }
}
