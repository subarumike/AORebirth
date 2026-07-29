using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexMissionChainStateStore
{
	public static RexMissionChainState GetState(ICharacter character)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return RexMissionChainState.NoRexMission;
		}
		return GetState(((IEntity)character).Identity);
	}

	public static RexMissionChainState GetState(Identity identity)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		if (!MissionRuntime.IsInitialized || (int)((Identity)(ref identity)).Type != 50000 || ((Identity)(ref identity)).Instance == 0)
		{
			return RexMissionChainState.NoRexMission;
		}
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B18F");
		if (IsOfferedOrLater(mission))
		{
			return RexMissionChainState.B18FPreviewed;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18E");
		if (mission2 != null && mission2.State == MissionLifecycleState.Completed)
		{
			return RexMissionChainState.B18ECompleted;
		}
		if (IsOfferedOrLater(mission2))
		{
			return RexMissionChainState.B18EPreviewed;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18D");
		if (mission3 != null && mission3.State == MissionLifecycleState.Completed)
		{
			return RexMissionChainState.B18DObjectiveComplete;
		}
		if (IsOfferedOrLater(mission3))
		{
			return RexMissionChainState.B18DPreviewed;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission4 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18C");
		if (mission4 != null && mission4.State == MissionLifecycleState.Completed)
		{
			return RexMissionChainState.B18CObjectiveComplete;
		}
		if (IsOfferedOrLater(mission4))
		{
			return RexMissionChainState.B18CPreviewed;
		}
		return RexMissionChainState.NoRexMission;
	}

	public static void AdvanceAtLeast(ICharacter character, RexMissionChainState targetState, string reason)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		if (!MissionRuntime.IsInitialized || character == null)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		if ((int)((Identity)(ref identity)).Type != 50000)
		{
			return;
		}
		identity = ((IEntity)character).Identity;
		if (((Identity)(ref identity)).Instance != 0)
		{
			identity = ((IEntity)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			RexMissionChainState state = GetState(character);
			MissionOperationResult missionOperationResult = null;
			if (targetState >= RexMissionChainState.B18CPreviewed && state < RexMissionChainState.B18CPreviewed)
			{
				missionOperationResult = EnsureActive(instance, "Mission:5514B18C");
			}
			if (targetState >= RexMissionChainState.B18CObjectiveComplete)
			{
				missionOperationResult = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B18C");
			}
			if (targetState >= RexMissionChainState.B18DPreviewed)
			{
				missionOperationResult = EnsureActive(instance, "Mission:5514B18D");
			}
			if (targetState >= RexMissionChainState.B18DObjectiveComplete)
			{
				missionOperationResult = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B18D");
			}
			if (targetState >= RexMissionChainState.B18EPreviewed)
			{
				missionOperationResult = EnsureActive(instance, "Mission:5514B18E");
			}
			if (targetState >= RexMissionChainState.B18ECompleted)
			{
				missionOperationResult = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B18E");
			}
			if (targetState >= RexMissionChainState.B18FPreviewed)
			{
				missionOperationResult = EnsureActive(instance, "Mission:5514B18F");
			}
			RexMissionChainState state2 = GetState(character);
			if (state2 != state || (missionOperationResult != null && missionOperationResult.Status == MissionOperationStatus.Rejected))
			{
				string[] obj = new string[13]
				{
					"ARETE_REX_CHAIN_STATE character=", null, null, null, null, null, null, null, null, null,
					null, null, null
				};
				identity = ((IEntity)character).Identity;
				obj[1] = ((Identity)(ref identity)).ToString(true);
				obj[2] = " from=";
				obj[3] = state.ToString();
				obj[4] = " to=";
				obj[5] = state2.ToString();
				obj[6] = " target=";
				obj[7] = targetState.ToString();
				obj[8] = " status=";
				obj[9] = ((missionOperationResult == null) ? "none" : missionOperationResult.Status.ToString());
				obj[10] = " reason=\"";
				obj[11] = reason ?? string.Empty;
				obj[12] = "\" persistent=true";
				LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
			}
		}
	}

	private static MissionOperationResult EnsureActive(int characterId, string questId)
	{
		MissionOperationResult missionOperationResult = MissionRuntime.Service.OfferMission(characterId, questId);
		if (missionOperationResult.Status == MissionOperationStatus.Rejected || missionOperationResult.Status == MissionOperationStatus.Unresolved || missionOperationResult.Status == MissionOperationStatus.NotFound)
		{
			return missionOperationResult;
		}
		return MissionRuntime.Service.AcceptMission(characterId, questId);
	}

	private static bool IsOfferedOrLater(ZoneEngine.Core.Missions.MissionStateRecord mission)
	{
		return mission != null && (mission.State == MissionLifecycleState.Offered || mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed);
	}
}
