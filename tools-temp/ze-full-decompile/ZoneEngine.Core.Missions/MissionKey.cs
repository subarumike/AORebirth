using System;

namespace ZoneEngine.Core.Missions;

public struct MissionKey : IEquatable<MissionKey>
{
	public int CharacterId { get; private set; }

	public string QuestId { get; private set; }

	public MissionKey(int characterId, string questId)
	{
		if (characterId <= 0)
		{
			throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
		}
		if (string.IsNullOrWhiteSpace(questId))
		{
			throw new ArgumentException("Quest identity is required.", "questId");
		}
		CharacterId = characterId;
		QuestId = questId.Trim();
	}

	public bool Equals(MissionKey other)
	{
		return CharacterId == other.CharacterId && string.Equals(QuestId, other.QuestId, StringComparison.OrdinalIgnoreCase);
	}

	public override bool Equals(object obj)
	{
		return obj is MissionKey && Equals((MissionKey)obj);
	}

	public override int GetHashCode()
	{
		return (CharacterId * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(QuestId ?? string.Empty);
	}

	public override string ToString()
	{
		return CharacterId + "|" + QuestId;
	}
}
