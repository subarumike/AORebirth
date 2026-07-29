using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class MissionStateStore
{
	private readonly Dictionary<string, MissionStateRecord> recordsByCharacterAndQuest = new Dictionary<string, MissionStateRecord>(StringComparer.OrdinalIgnoreCase);

	public MissionStateRecord GetOrCreate(int characterId, string questId)
	{
		ValidateCharacterId(characterId);
		string key = MakeKey(characterId, questId);
		if (recordsByCharacterAndQuest.TryGetValue(key, out var value))
		{
			return value;
		}
		value = new MissionStateRecord
		{
			CharacterId = characterId,
			QuestId = questId,
			State = AreteMissionState.NotStarted
		};
		recordsByCharacterAndQuest[key] = value;
		return value;
	}

	public bool TryGetRecord(int characterId, string questId, out MissionStateRecord record)
	{
		ValidateCharacterId(characterId);
		if (string.IsNullOrWhiteSpace(questId))
		{
			record = null;
			return false;
		}
		return recordsByCharacterAndQuest.TryGetValue(MakeKey(characterId, questId), out record);
	}

	private static string MakeKey(int characterId, string questId)
	{
		return characterId + "|" + (questId ?? string.Empty);
	}

	private static void ValidateCharacterId(int characterId)
	{
		if (characterId <= 0)
		{
			throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
		}
	}
}
