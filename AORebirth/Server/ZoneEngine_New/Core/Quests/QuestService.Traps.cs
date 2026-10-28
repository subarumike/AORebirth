namespace ZoneEngine_New.Core.Quests;

using System;
using ZoneEngine_New.Core.Quests.Dungeons;

public sealed partial class QuestService
{
    internal MissionTrapPopulation? CreateTrapPopulation(int ownerId, string questId, long assigned,
        string frozenJson, QuestDungeonParameters parameters)
    {
        if (parameters.Traps?.Enabled != true)
            return null;
        if (_completionDao == null)
            throw new InvalidOperationException("Configured mission traps require the existing mission DAO.");
        return new MissionTrapPopulation(_completionDao, ownerId, questId, assigned,
            frozenJson, parameters, _logger);
    }
}
