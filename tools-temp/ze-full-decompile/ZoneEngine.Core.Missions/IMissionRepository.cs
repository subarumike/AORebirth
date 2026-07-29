using System;
using System.Collections.Generic;

namespace ZoneEngine.Core.Missions;

public interface IMissionRepository
{
	MissionStateRecord GetMission(MissionKey key);

	IList<MissionStateRecord> GetMissions(int characterId);

	MissionCharacterSnapshot ReadCharacter(int characterId);

	MissionAccountFlagRecord GetAccountFlag(string accountKey, string flagKey);

	IList<MissionAccountFlagRecord> GetAccountFlags(string accountKey);

	T Execute<T>(int characterId, Func<IMissionRepositoryTransaction, T> operation);

	T Execute<T>(int characterId, string accountKey, Func<IMissionRepositoryTransaction, T> operation);
}
