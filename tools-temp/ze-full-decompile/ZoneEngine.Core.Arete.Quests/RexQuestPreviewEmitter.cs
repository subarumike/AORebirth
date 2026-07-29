using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexQuestPreviewEmitter
{
	public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW";

	private const int AreteLandingPlayfieldId = 6553;

	private const int RexLarssonInstance = 2016273768;

	private const string B18CPreviewSourceNodeId = "rex_194454_004";

	private const int B18CPreviewAnswerIndex = 0;

	public static bool IsQuestPreviewEnabled => AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW");

	public static RexQuestPreviewEmissionResult TryEmitB18CPreview(ICharacter source, Identity npcIdentity, string previousNodeId, int answerIndex, bool dialogueGateEnabled)
	{
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		if (!IsB18CPreviewOption(previousNodeId, answerIndex))
		{
			return RexQuestPreviewEmissionResult.NotApplicable();
		}
		bool isQuestPreviewEnabled = IsQuestPreviewEnabled;
		if (!dialogueGateEnabled || !isQuestPreviewEnabled)
		{
			return RexQuestPreviewEmissionResult.Skipped("B18C quest preview skipped dialogueGate=" + dialogueGateEnabled + " questPreviewGate=" + isQuestPreviewEnabled + " attempted=false noPersistence=true noRewards=true noCompletion=true");
		}
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed: source character missing.");
		}
		RexMissionChainState state = RexMissionChainStateStore.GetState(source);
		if (state != 0)
		{
			RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = TryResendActiveMissionWindow(source, state);
			if (rexQuestPreviewEmissionResult.Emitted)
			{
				return rexQuestPreviewEmissionResult;
			}
			return RexQuestPreviewEmissionResult.Skipped("B18C quest preview skipped because Rex chain state is " + state.ToString() + ". duplicateOfferBlocked=true noPersistence=true noRewards=true noCompletion=true");
		}
		if (!IsRexLarsson(npcIdentity))
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed: target is not Rex Larsson.");
		}
		if (!IsInAreteLanding(source))
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed: source character is not in Arete Landing 6553.");
		}
		if (!MissionRuntime.IsInitialized)
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed: persistent mission runtime is not initialized.");
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionOperationResult missionOperationResult = service.OfferMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (IsPersistenceFailure(missionOperationResult))
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest preview failed before packet projection: " + missionOperationResult.Message);
		}
		PersistentMissionService service2 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionOperationResult missionOperationResult2 = service2.AcceptMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (IsPersistenceFailure(missionOperationResult2))
		{
			return RexQuestPreviewEmissionResult.Failed("B18C quest acceptance failed before packet projection: " + missionOperationResult2.Message);
		}
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult2 = SafeQuestFullUpdateSender.TrySendB18CPreview(source);
		RexB18CObjectiveProgressTracker.TryActivateFromPreview(source, rexQuestPreviewEmissionResult2);
		return rexQuestPreviewEmissionResult2;
	}

	private static bool IsB18CPreviewOption(string previousNodeId, int answerIndex)
	{
		return string.Equals(previousNodeId, "rex_194454_004", StringComparison.OrdinalIgnoreCase) && answerIndex == 0;
	}

	private static RexQuestPreviewEmissionResult TryResendActiveMissionWindow(ICharacter source, RexMissionChainState chainState)
	{
		switch (chainState)
		{
		case RexMissionChainState.B18CPreviewed:
			return SafeQuestFullUpdateSender.TrySendB18CPreview(source);
		case RexMissionChainState.B18CObjectiveComplete:
		case RexMissionChainState.B18DPreviewed:
			return SafeQuestFullUpdateSender.TrySendB18DPreview(source);
		case RexMissionChainState.B18DObjectiveComplete:
		case RexMissionChainState.B18EPreviewed:
			return SafeQuestFullUpdateSender.TrySendB18EPreview(source);
		case RexMissionChainState.B18ECompleted:
		case RexMissionChainState.B18FPreviewed:
			return SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source);
		default:
			return RexQuestPreviewEmissionResult.NotApplicable();
		}
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}

	private static bool IsRexLarsson(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref identity)).Type == 50000 && ((Identity)(ref identity)).Instance == 2016273768;
	}

	private static bool IsInAreteLanding(ICharacter source)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (source != null && ((IInstancedEntity)source).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)source).Playfield).Identity;
			result = ((((Identity)(ref identity)).Instance == 6553) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}
}
