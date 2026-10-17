-- Recurring quests: the Nascense DOJA chip turn-in moved from the authored mission system (Mission:55AA2421, with
-- the cooldown mission Mission:55AA2803) to the Quests.json recurring quest DJNS, cooldown group "doja", one chip a
-- day per account. Carries over running account cooldowns and chips accepted but not yet handed in.
-- The old mission rows are left as history. Safe to re-run.

INSERT IGNORE INTO `missionaccountflags`
	(`AccountKey`, `FlagKey`, `Value`, `SourceQuestId`, `CreatedAtUtcTicks`, `UpdatedAtUtcTicks`, `Version`)
SELECT `AccountKey`, 'quest-cooldown:doja', `Value`, 'DJNS', `CreatedAtUtcTicks`, `UpdatedAtUtcTicks`, 1
FROM `missionaccountflags`
WHERE `FlagKey` = 'doja-shared-cooldown-until-utc';

-- State 2 is MissionLifecycleState.Active; characterquests State 1 is QuestState.Active, Source 0 a template quest.
INSERT IGNORE INTO `characterquests`
	(`CharacterId`, `QuestId`, `Source`, `State`, `Progress`, `RequiredCount`, `AssignedAtUtcTicks`, `UpdatedAtUtcTicks`)
SELECT `CharacterId`, 'DJNS', 0, 1, 0, 1,
	CASE WHEN `AcceptedAtUtcTicks` > 0 THEN `AcceptedAtUtcTicks` ELSE `UpdatedAtUtcTicks` END, `UpdatedAtUtcTicks`
FROM `missionstates`
WHERE `QuestId` = 'Mission:55AA2421' AND `State` = 2;
