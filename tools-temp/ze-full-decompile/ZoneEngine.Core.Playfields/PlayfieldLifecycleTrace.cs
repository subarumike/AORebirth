using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

public static class PlayfieldLifecycleTrace
{
	public const string FlowPrivateCityReadyInit = "private-city-ready-init";

	public const string FlowSamePlayfieldVisibility = "same-playfield-visibility";

	public const string FlowCleaningRobotDeathCorpseDespawn = "cleaning-robot-death-corpse-despawn";

	public const string FlowCleaningRobotNpcAttack = "cleaning-robot-npc-attack";

	public const string FlowCapturedAreteRobotSpawn = "captured-arete-robot-spawn";

	public const string MessageAttack = "Attack";

	public const string MessageAttackInfo = "AttackInfo";

	public const string MessageCapturedAreteRobotPatrolReplayAssigned = "CapturedAreteRobotPatrolReplayAssigned";

	public const string MessageCapturedAreteRobotSpawnCreated = "CapturedAreteRobotSpawnCreated";

	public const string MessageCapturedAreteRobotSpawnRowsLoaded = "CapturedAreteRobotSpawnRowsLoaded";

	public const string MessageCharInPlay = "CharInPlay";

	public const string MessageCharacterActionDeath = "CharacterAction Death";

	public const string MessageCorpseFullUpdate = "CorpseFullUpdate";

	public const string MessageFullCharacter = "FullCharacter";

	public const string MessageOrgInfoPacket = "OrgInfoPacket";

	public const string MessagePlayfieldAllCities = "PlayfieldAllCities";

	public const string MessagePlayfieldAllTowers = "PlayfieldAllTowers";

	public const string MessagePrivateCityOrgInitSent = "PrivateCityOrgInitSent";

	public const string MessagePrivateCityReadyBlockBegin = "PrivateCityReadyBlockBegin";

	public const string MessagePrivateCityReadyBlockEnd = "PrivateCityReadyBlockEnd";

	public const string MessagePrivateCityTowersCitiesSent = "PrivateCityTowersCitiesSent";

	public const string MessageSimpleCharFullUpdate = "SimpleCharFullUpdate";

	public const string MessageStat = "Stat";

	public const string MessageSpecialAttackWeapon = "SpecialAttackWeapon";

	public const string MessageStopFight = "StopFight";

	public const string StagePrivateCitySimpleCharFullUpdateBroadcast = "private-city-simple-char-full-update-broadcast";

	public const string StagePrivateCityReadyBlockBegin = "private-city-ready-block-begin";

	public const string StagePrivateCityOrgInfoPacket = "private-city-org-info-packet";

	public const string StagePrivateCitySocialStatus = "private-city-social-status";

	public const string StagePrivateCityClan = "private-city-clan";

	public const string StagePrivateCityClanLevel = "private-city-clan-level";

	public const string StagePrivateCityFullCharacter = "private-city-full-character";

	public const string StagePrivateCityOrgInitSent = "private-city-org-init-sent";

	public const string StagePrivateCityPlayfieldAllTowers = "private-city-playfield-all-towers";

	public const string StagePrivateCityPlayfieldAllCities = "private-city-playfield-all-cities";

	public const string StagePrivateCityTowersCitiesSent = "private-city-towers-cities-sent";

	public const string StagePrivateCityReadyBlockEnd = "private-city-ready-block-end";

	public const string StageCharInPlayReceived = "char-in-play-received";

	public const string StageCharInPlayAnnounce = "char-in-play-announce";

	public const string StageCharInPlayReady = "char-in-play-ready";

	public const string StageStaticDynelSnapshot = "static-dynel-snapshot";

	public const string StageWeaponDefinitions = "weapon-definitions";

	public const string StageTimersEnabled = "timers-enabled";

	public const string StageVisibilityJoinerReady = "visibility-joiner-ready";

	public const string StageJoiningCharacterSimpleCharFullUpdateBroadcast = "joining-character-simple-char-full-update-broadcast";

	public const string StageJoiningCharacterCharInPlayBroadcast = "joining-character-char-in-play-broadcast";

	public const string StageExistingCharacterSimpleCharFullUpdate = "existing-character-simple-char-full-update";

	public const string StageExistingCharacterCharInPlay = "existing-character-char-in-play";

	public const string StageAttackerStopFight = "attacker-stop-fight";

	public const string StageRobotStopFight = "robot-stop-fight";

	public const string StageCharacterActionDeathParameter2 = "character-action-death-parameter2";

	public const string StageCorpseSpawnScheduled = "corpse-spawn-scheduled";

	public const string StageDeadNpcDespawnScheduled = "dead-npc-despawn-scheduled";

	public const string StageCorpseFullUpdate = "corpse-full-update";

	public const string StageRobotSpecialAttackWeaponContext = "robot-special-attack-weapon-context";

	public const string StageRobotAttackStartContext = "robot-attack-start-context";

	public const string StageRobotAttackInfo = "robot-attack-info";

	public const string StageCapturedAreteRobotSpawnRowsLoaded = "captured-arete-robot-spawn-rows-loaded";

	public const string StageCapturedAreteRobotSpawnCreated = "captured-arete-robot-spawn-created";

	public const string StageCapturedAreteRobotPatrolReplayAssigned = "captured-arete-robot-patrol-replay-assigned";

	public const string StageCapturedAreteRobotSimpleCharFullUpdateBroadcast = "captured-arete-robot-simple-char-full-update-broadcast";

	public static readonly string[] ExpectedPrivateCityReadyInitOrder = new string[15]
	{
		"private-city-ready-block-begin", "private-city-simple-char-full-update-broadcast", "private-city-org-info-packet", "private-city-social-status", "private-city-clan", "private-city-clan-level", "private-city-social-status", "private-city-social-status", "private-city-social-status", "private-city-org-init-sent",
		"private-city-full-character", "private-city-playfield-all-towers", "private-city-playfield-all-cities", "private-city-towers-cities-sent", "private-city-ready-block-end"
	};

	public static readonly string[] ExpectedCharInPlayEntryOrder = new string[6] { "char-in-play-received", "char-in-play-announce", "char-in-play-ready", "static-dynel-snapshot", "weapon-definitions", "timers-enabled" };

	public static readonly string[] ExpectedSamePlayfieldVisibilityOrder = new string[5] { "visibility-joiner-ready", "joining-character-simple-char-full-update-broadcast", "joining-character-char-in-play-broadcast", "existing-character-simple-char-full-update", "existing-character-char-in-play" };

	public static readonly string[] ExpectedCleaningRobotDeathOrder = new string[6] { "attacker-stop-fight", "robot-stop-fight", "character-action-death-parameter2", "corpse-spawn-scheduled", "dead-npc-despawn-scheduled", "corpse-full-update" };

	public static readonly string[] ExpectedCleaningRobotNpcAttackOrder = new string[3] { "robot-special-attack-weapon-context", "robot-attack-start-context", "robot-attack-info" };

	public static readonly string[] ExpectedCapturedAreteRobotSpawnOrder = new string[4] { "captured-arete-robot-spawn-rows-loaded", "captured-arete-robot-spawn-created", "captured-arete-robot-patrol-replay-assigned", "captured-arete-robot-simple-char-full-update-broadcast" };

	[ThreadStatic]
	private static IPlayfieldLifecycleRecorder currentRecorder;

	[ThreadStatic]
	private static int currentOrder;

	public static PlayfieldLifecycleCapture Capture()
	{
		PlayfieldLifecycleCapture result = (PlayfieldLifecycleCapture)(currentRecorder = new PlayfieldLifecycleCapture(currentRecorder, currentOrder));
		currentOrder = 0;
		return result;
	}

	public static bool ContainsExpectedOrder(IList<PlayfieldLifecycleEvent> events, string flow, string[] expectedStages, out string failure)
	{
		failure = string.Empty;
		if (events == null)
		{
			failure = "No lifecycle events were supplied.";
			return false;
		}
		if (expectedStages == null || expectedStages.Length == 0)
		{
			return true;
		}
		int num = 0;
		for (int i = 0; i < events.Count; i++)
		{
			if (num >= expectedStages.Length)
			{
				break;
			}
			PlayfieldLifecycleEvent playfieldLifecycleEvent = events[i];
			if (playfieldLifecycleEvent != null && !(playfieldLifecycleEvent.Flow != flow) && playfieldLifecycleEvent.Stage == expectedStages[num])
			{
				num++;
			}
		}
		if (num == expectedStages.Length)
		{
			return true;
		}
		failure = "Missing lifecycle stage '" + expectedStages[num] + "' in flow '" + flow + "'.";
		return false;
	}

	public static void Record(string flow, string stage, string messageType, Identity identity)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		Record(flow, stage, messageType, identity, string.Empty);
	}

	public static void Record(string flow, string stage, string messageType, Identity identity, string detail)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		IPlayfieldLifecycleRecorder playfieldLifecycleRecorder = currentRecorder;
		if (playfieldLifecycleRecorder != null)
		{
			int order = Interlocked.Increment(ref currentOrder);
			playfieldLifecycleRecorder.Record(new PlayfieldLifecycleEvent(order, flow, stage, messageType, identity, detail));
		}
	}

	public static string FormatCapturedAreteRobotSpawnRowsDetail(int count, int monsterData)
	{
		return string.Format(CultureInfo.InvariantCulture, "count={0} monsterData={1}", count, monsterData);
	}

	public static string FormatCapturedAreteRobotSpawnCreatedDetail(int sourceInstance, int monsterData, int health, int level, int runSpeed, float x, float y, float z, float patrolX, float patrolY, float patrolZ)
	{
		return string.Format(CultureInfo.InvariantCulture, "sourceInstance={0:X8} monsterData={1} hp={2} level={3} runSpeed={4} pos={5:0.#####},{6:0.#####},{7:0.#####} patrol={8:0.#####},{9:0.#####},{10:0.#####}", sourceInstance, monsterData, health, level, runSpeed, x, y, z, patrolX, patrolY, patrolZ);
	}

	public static string FormatCapturedAreteRobotPatrolReplayAssignedDetail(int sourceInstance, int segmentCount)
	{
		return string.Format(CultureInfo.InvariantCulture, "sourceInstance={0:X8} segments={1}", sourceInstance, segmentCount);
	}

	public static string FormatCapturedAreteRobotSimpleCharFullUpdateDetail(int sourceInstance)
	{
		return string.Format(CultureInfo.InvariantCulture, "sourceInstance={0:X8}", sourceInstance);
	}

	internal static void Restore(IPlayfieldLifecycleRecorder recorder, int order)
	{
		currentRecorder = recorder;
		currentOrder = order;
	}
}
