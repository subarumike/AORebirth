namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    #endregion

    public sealed class MissionStateStore
    {
        private readonly Dictionary<string, MissionStateRecord> recordsByCharacterAndQuest =
            new Dictionary<string, MissionStateRecord>(System.StringComparer.OrdinalIgnoreCase);

        public MissionStateRecord GetOrCreate(int characterId, string questId)
        {
            ValidateCharacterId(characterId);
            string key = MakeKey(characterId, questId);
            MissionStateRecord record;
            if (this.recordsByCharacterAndQuest.TryGetValue(key, out record))
            {
                return record;
            }

            record = new MissionStateRecord
            {
                CharacterId = characterId,
                QuestId = questId,
                State = AreteMissionState.NotStarted
            };

            this.recordsByCharacterAndQuest[key] = record;
            return record;
        }

        public bool TryGetRecord(int characterId, string questId, out MissionStateRecord record)
        {
            ValidateCharacterId(characterId);
            if (string.IsNullOrWhiteSpace(questId))
            {
                record = null;
                return false;
            }

            return this.recordsByCharacterAndQuest.TryGetValue(MakeKey(characterId, questId), out record);
        }

        private static string MakeKey(int characterId, string questId)
        {
            return characterId + "|" + (questId ?? string.Empty);
        }

        private static void ValidateCharacterId(int characterId)
        {
            if (characterId <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    "characterId",
                    "Stable character identity must be positive.");
            }
        }
    }
}
