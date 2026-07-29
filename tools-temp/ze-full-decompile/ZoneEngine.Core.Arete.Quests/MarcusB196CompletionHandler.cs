using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class MarcusB196CompletionHandler
{
	private const int AreteLandingPlayfieldId = 6553;

	private const int MarcusStoneInstance = 2016273767;

	private const string MissionId = "Mission:5514B196";

	private const string ObjectiveId = "mission_5514b196_objective_questfullupdate";

	public static MarcusB196CompletionResult TryCompleteOnReturn(ICharacter source, Identity npcIdentity, bool dialogueGateEnabled)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		if (!IsMarcusStone(npcIdentity) && !IsMarcusStoneNameBound(source, npcIdentity))
		{
			return MarcusB196CompletionResult.NotApplicable();
		}
		if (!dialogueGateEnabled)
		{
			return MarcusB196CompletionResult.Skipped("Marcus B196 return skipped because dialogue routing gate is disabled.");
		}
		if (!IsValidPlayerInArete(source))
		{
			return MarcusB196CompletionResult.Failed("Marcus B196 return failed: source is missing, not a player, or not in Arete Landing 6553.");
		}
		if (!MissionRuntime.IsInitialized)
		{
			return MarcusB196CompletionResult.Failed("Marcus B196 return failed: persistent mission runtime is not initialized.");
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		if (mission != null && mission.State == MissionLifecycleState.Offered)
		{
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B196");
			mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		}
		if (mission == null || (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Completed))
		{
			return MarcusB196CompletionResult.NotApplicable();
		}
		if (mission.State == MissionLifecycleState.Active)
		{
			PersistentMissionService service = MissionRuntime.Service;
			MissionObjectiveObservation obj = new MissionObjectiveObservation
			{
				CharacterId = instance,
				QuestId = "Mission:5514B196",
				ObjectiveId = "mission_5514b196_objective_questfullupdate",
				ObservationKey = "dialogue-return-marcus:" + ((Identity)(ref npcIdentity)).ToString(true),
				Amount = 1,
				EventType = "NpcDialogueOpen"
			};
			identity = ((IEntity)source).Identity;
			obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			obj.TargetIdentity = ((Identity)(ref npcIdentity)).ToString(true);
			MissionOperationResult missionOperationResult = service.ObserveObjective(obj);
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied && missionOperationResult.Status != MissionOperationStatus.DuplicateObservation)
			{
				return MarcusB196CompletionResult.Failed("Marcus B196 objective persistence failed: " + missionOperationResult.Message);
			}
			MissionOperationResult missionOperationResult2 = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B196");
			if (missionOperationResult2.Status != MissionOperationStatus.Applied && missionOperationResult2.Status != MissionOperationStatus.AlreadyApplied)
			{
				return MarcusB196CompletionResult.Failed("Marcus B196 completion persistence failed: " + missionOperationResult2.Message);
			}
		}
		ForceCompleteIfNeeded(instance, "Mission:5514B18F");
		ForceCompleteIfNeeded(instance, "Mission:5514B194");
		ForceCompleteIfNeeded(instance, "Mission:5514B18E");
		bool flag = SafeQuestFullUpdateSender.TrySendB196CompletionCleanup(source)?.Emitted ?? false;
		if (!flag)
		{
			SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
			SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
			SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
			flag = true;
		}
		identity = ((IEntity)source).Identity;
		LogUtil.Debug((DebugInfoDetail)128, "ARETE_MARCUS_B196_COMPLETION applied character=" + ((Identity)(ref identity)).ToString(true) + " projected=" + flag);
		return MarcusB196CompletionResult.Succeeded("Marcus B196 Return to Marcus completed; removed B196/B18F/B194/B18E from mission window.");
	}

	private static void ForceCompleteIfNeeded(int characterId, string questId)
	{
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
		if (mission != null && mission.State != MissionLifecycleState.Completed)
		{
			if (mission.State == MissionLifecycleState.Offered)
			{
				MissionRuntime.Service.AcceptMission(characterId, questId);
				mission = MissionRuntime.Service.GetMission(characterId, questId);
			}
			if (mission != null && mission.State == MissionLifecycleState.Active)
			{
				string objectiveId = "mission_" + questId.Replace("Mission:", string.Empty).ToLowerInvariant() + "_objective_questfullupdate";
				MissionRuntime.Service.ObserveObjective(new MissionObjectiveObservation
				{
					CharacterId = characterId,
					QuestId = questId,
					ObjectiveId = objectiveId,
					ObservationKey = "marcus-b196-force-complete",
					Amount = 1,
					EventType = "NpcDialogueOpen",
					SourceIdentity = string.Empty,
					TargetIdentity = string.Empty
				});
				MissionRuntime.Service.CompleteMission(characterId, questId);
			}
		}
	}

	private static bool IsMarcusStone(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref identity)).Type == 50000 && ((Identity)(ref identity)).Instance == 2016273767;
	}

	private static bool IsMarcusStoneNameBound(ICharacter source, Identity npcIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IInstancedEntity)source).Playfield == null || (int)((Identity)(ref npcIdentity)).Type != 50000 || ((Identity)(ref npcIdentity)).Instance == 0)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, npcIdentity);
		return @object != null && !string.IsNullOrWhiteSpace(((INamedEntity)@object).Name) && ((INamedEntity)@object).Name.IndexOf("Marcus Stone", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool IsValidPlayerInArete(ICharacter source)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (source != null && ((IDynel)source).Controller is PlayerController)
		{
			Identity identity = ((IEntity)source).Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				identity = ((IEntity)source).Identity;
				if (((Identity)(ref identity)).Instance != 0 && ((IInstancedEntity)source).Playfield != null)
				{
					identity = ((IEntity)((IInstancedEntity)source).Playfield).Identity;
					result = ((((Identity)(ref identity)).Instance == 6553) ? 1 : 0);
					goto IL_005b;
				}
			}
		}
		result = 0;
		goto IL_005b;
		IL_005b:
		return (byte)result != 0;
	}
}
