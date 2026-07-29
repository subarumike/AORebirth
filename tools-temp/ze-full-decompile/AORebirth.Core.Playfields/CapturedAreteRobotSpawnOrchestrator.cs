using System;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.NPCHandler;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedAreteRobotSpawnOrchestrator
{
	private const int PrivateAretePlayfieldInstance = 6553;

	private readonly CapturedAreteRobotContentProvider capturedRobotContent;

	private readonly NpcPatrolReplayCoordinator patrolReplay;

	private readonly Action<ICharacter> activateNpc;

	internal CapturedAreteRobotSpawnOrchestrator(CapturedAreteRobotContentProvider capturedRobotContent, NpcPatrolReplayCoordinator patrolReplay, Action<ICharacter> activateNpc)
	{
		this.capturedRobotContent = capturedRobotContent;
		this.patrolReplay = patrolReplay;
		this.activateNpc = activateNpc;
	}

	internal void SpawnForPlayfield(Playfield playfield, Identity playfieldIdentity)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (((Identity)(ref playfieldIdentity)).Instance == 6553)
		{
			CapturedAreteRobotSpawnDefinition[] spawnDefinitions = capturedRobotContent.GetSpawnDefinitions();
			PlayfieldLifecycleTrace.Record("captured-arete-robot-spawn", "captured-arete-robot-spawn-rows-loaded", "CapturedAreteRobotSpawnRowsLoaded", playfieldIdentity, PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(spawnDefinitions.Length, 297023));
			CapturedAreteRobotSpawnDefinition[] array = spawnDefinitions;
			foreach (CapturedAreteRobotSpawnDefinition spawn in array)
			{
				SpawnCapturedAreteCleaningRobot(playfield, playfieldIdentity, spawn);
			}
		}
	}

	private void SpawnCapturedAreteCleaningRobot(Playfield playfield, Identity playfieldIdentity, CapturedAreteRobotSpawnDefinition spawn)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_007e: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Expected O, but got Unknown
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		NPCController npcController = new NPCController();
		Character val = NonPlayerCharacterHandler.SpawnMobFromTemplate("A004", playfieldIdentity, new Coordinate
		{
			x = spawn.X,
			y = spawn.Y,
			z = spawn.Z
		}, new Quaternion(0.0, 0.0, 0.0, 1.0), (IController)(object)npcController, spawn.Level);
		if (val == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, $"Captured Arete robot spawn failed source=20260629-193121 sourceIdentity=SimpleChar:{spawn.SourceInstance:X8}");
			return;
		}
		((Dynel)val).Name = "Malfunctioning Cleaning Robot";
		((Dynel)val).Playfield = (IPlayfield)(object)playfield;
		CombatTestMobArchetype.Prepare((ICharacter)(object)val, CombatTestMobArchetype.MalfunctioningCleaningRobot);
		SetCapturedMobStat((ICharacter)(object)val, (StatIds)1, spawn.Health);
		SetCapturedMobStat((ICharacter)(object)val, (StatIds)27, spawn.Health);
		SetCapturedMobStat((ICharacter)(object)val, (StatIds)54, spawn.Level);
		SetCapturedMobStat((ICharacter)(object)val, (StatIds)156, spawn.RunSpeed);
		((Dynel)val).Coordinates(new Coordinate
		{
			x = spawn.X,
			y = spawn.Y,
			z = spawn.Z
		});
		AssignCapturedPatrolWaypoints((ICharacter)(object)val, spawn);
		PlayfieldLifecycleTrace.Record("captured-arete-robot-spawn", "captured-arete-robot-spawn-created", "CapturedAreteRobotSpawnCreated", ((PooledObject)val).Identity, PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(spawn.SourceInstance, 297023, spawn.Health, spawn.Level, spawn.RunSpeed, spawn.X, spawn.Y, spawn.Z, spawn.PatrolX, spawn.PatrolY, spawn.PatrolZ));
		int replaySegmentCount = 0;
		patrolReplay.AssignCapturedAreteRobotReplay(spawn.SourceInstance, delegate(NpcPatrolReplaySegment[] segments)
		{
			replaySegmentCount = ((segments != null) ? segments.Length : 0);
			npcController.SetCapturedPatrolReplaySegments(segments);
		});
		PlayfieldLifecycleTrace.Record("captured-arete-robot-spawn", "captured-arete-robot-patrol-replay-assigned", "CapturedAreteRobotPatrolReplayAssigned", ((PooledObject)val).Identity, PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(spawn.SourceInstance, replaySegmentCount));
		((Dynel)val).DoNotDoTimers = false;
		activateNpc((ICharacter)(object)val);
		PlayfieldLifecycleTrace.Record("captured-arete-robot-spawn", "captured-arete-robot-simple-char-full-update-broadcast", "SimpleCharFullUpdate", ((PooledObject)val).Identity, PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));
		playfield.AnnounceSpawnedCharacterVisibility((ICharacter)(object)val, Identity.None);
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Captured Arete robot spawned source=20260629-193121 sourceIdentity=SimpleChar:{0:X8} serverIdentity={1} pos=({2},{3},{4}) health={5} level={6} runSpeed={7}", spawn.SourceInstance, ((PooledObject)val).Identity, spawn.X, spawn.Y, spawn.Z, spawn.Health, spawn.Level, spawn.RunSpeed));
	}

	private static void AssignCapturedPatrolWaypoints(ICharacter mobCharacter, CapturedAreteRobotSpawnDefinition spawn)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		mobCharacter.Waypoints.Clear();
		mobCharacter.AddWaypoint(new Vector3((double)spawn.X, (double)spawn.Y, (double)spawn.Z), false);
		mobCharacter.AddWaypoint(new Vector3((double)spawn.PatrolX, (double)spawn.PatrolY, (double)spawn.PatrolZ), false);
		((IDynel)mobCharacter).Controller.State = (CharacterState)4;
	}

	private static void SetCapturedMobStat(ICharacter mobCharacter, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		((IStats)mobCharacter).Stats[stat].Value = value;
		((IStats)mobCharacter).Stats[stat].BaseValue = (uint)value;
	}
}
