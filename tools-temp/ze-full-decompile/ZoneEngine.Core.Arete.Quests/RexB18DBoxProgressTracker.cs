using System;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexB18DBoxProgressTracker
{
	public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW";

	private const int AreteLandingPlayfieldId = 6553;

	private const int CargoBoxInstance = 1457108143;

	private const string MissionId = "Mission:5514B18D";

	private const string ObjectiveId = "mission_5514B18D_objective_questfullupdate";

	private const string ObjectiveType = "CapturedUseInteractObjective";

	private const int RequiredCount = 1;

	public static bool IsPreviewCompletionEnabled => AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW");

	public static bool AreAllGatesEnabled => RexB18CObjectiveProgressTracker.AreAllGatesEnabled && IsPreviewCompletionEnabled;

	public static bool TryActivateFromPreview(ICharacter source)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		if (!AreAllGatesEnabled)
		{
			Log("activation skipped mission={0} allGates=false b18dPreviewGate={1} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true noRewards=true", "Mission:5514B18D", IsPreviewCompletionEnabled);
			return false;
		}
		if (!IsValidPlayerInArete(source))
		{
			Log("activation failed mission={0} reason=invalid-player-or-playfield source={1} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true", "Mission:5514B18D", IdentityText(source));
			return false;
		}
		if (!MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionOperationResult result = service.OfferMission(((Identity)(ref identity)).Instance, "Mission:5514B18D");
		if (IsTerminalFailure(result))
		{
			return false;
		}
		PersistentMissionService service2 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionOperationResult result2 = service2.AcceptMission(((Identity)(ref identity)).Instance, "Mission:5514B18D");
		if (IsTerminalFailure(result2))
		{
			return false;
		}
		object[] obj = new object[3] { "Mission:5514B18D", null, null };
		identity = ((IEntity)source).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = 1;
		Log("activated mission={0} character={1} progress=0/{2} previewReceived=true persistent=true", obj);
		RexMissionChainStateStore.AdvanceAtLeast(source, RexMissionChainState.B18DPreviewed, "B18D preview activated from B18C handoff");
		return true;
	}

	public static bool TryObserveBoxUse(ICharacter source, Identity target)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		if (!IsCargoBoxTarget(target))
		{
			return false;
		}
		if (!AreAllGatesEnabled)
		{
			Log("use ignored mission={0} reason=gates-disabled character={1} target={2} b18dPreviewGate={3} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true", "Mission:5514B18D", IdentityText(source), ((Identity)(ref target)).ToString(true), IsPreviewCompletionEnabled);
			return false;
		}
		if (!IsValidPlayerInArete(source))
		{
			Log("use ignored mission={0} reason=invalid-player-or-playfield character={1} target={2} inMemoryOnly=true noQuestFullUpdateRefresh=true noQuestDelete=true noB18E=true", "Mission:5514B18D", IdentityText(source), ((Identity)(ref target)).ToString(true));
			return true;
		}
		if (!MissionRuntime.IsInitialized)
		{
			return true;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B18D");
		if (mission == null || (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Completed))
		{
			return false;
		}
		string text = "terminal-use:" + ((Identity)(ref target)).ToString(true);
		ObjectiveProgressRecord objectiveProgressRecord;
		if (mission.State == MissionLifecycleState.Active)
		{
			PersistentMissionService service2 = MissionRuntime.Service;
			MissionObjectiveObservation missionObjectiveObservation = new MissionObjectiveObservation();
			identity = ((IEntity)source).Identity;
			missionObjectiveObservation.CharacterId = ((Identity)(ref identity)).Instance;
			missionObjectiveObservation.QuestId = "Mission:5514B18D";
			missionObjectiveObservation.ObjectiveId = "mission_5514B18D_objective_questfullupdate";
			missionObjectiveObservation.ObservationKey = text;
			missionObjectiveObservation.Amount = 1;
			missionObjectiveObservation.EventType = "GenericCmd:Use";
			identity = ((IEntity)source).Identity;
			missionObjectiveObservation.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			missionObjectiveObservation.TargetIdentity = ((Identity)(ref target)).ToString(true);
			MissionOperationResult missionOperationResult = service2.ObserveObjective(missionObjectiveObservation);
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied && missionOperationResult.Status != MissionOperationStatus.DuplicateObservation)
			{
				return true;
			}
			objectiveProgressRecord = ToRuntimeProgress(missionOperationResult.Objective, text);
		}
		else
		{
			PersistentMissionService service3 = MissionRuntime.Service;
			identity = ((IEntity)source).Identity;
			MissionObjectiveProgressRecord objective = service3.GetObjective(((Identity)(ref identity)).Instance, "Mission:5514B18D", "mission_5514B18D_objective_questfullupdate");
			objectiveProgressRecord = ToRuntimeProgress(objective, (objective == null) ? text : objective.LastObservationKey);
		}
		if (objectiveProgressRecord == null || !objectiveProgressRecord.Completed)
		{
			return true;
		}
		PersistentMissionService service4 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionOperationResult missionOperationResult2 = service4.CompleteAndActivateNextMission(((Identity)(ref identity)).Instance, "Mission:5514B18D", "Mission:5514B18E");
		bool flag = missionOperationResult2.Status == MissionOperationStatus.Applied || missionOperationResult2.Status == MissionOperationStatus.AlreadyApplied;
		if (flag)
		{
			RexMissionChainStateStore.AdvanceAtLeast(source, RexMissionChainState.B18EPreviewed, "B18D completion activated B18E persistently");
		}
		bool flag2 = flag && EnsureQuestProjection(source, "b18d-delete-projected", () => SafeQuestFullUpdateSender.TrySendB18DQuestDelete(source));
		bool flag3 = flag && EnsureQuestProjection(source, "b18e-preview-projected", () => SafeQuestFullUpdateSender.TrySendB18EPreview(source));
		object[] obj = new object[7] { "Mission:5514B18D", null, null, null, null, null, null };
		identity = ((IEntity)source).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = ((Identity)(ref target)).ToString(true);
		obj[3] = objectiveProgressRecord.CurrentCount;
		obj[4] = objectiveProgressRecord.RequiredCount;
		obj[5] = flag2;
		obj[6] = flag3;
		Log("objective observed mission={0} character={1} target={2} signal=\"GenericCmd Action=Use\" evidence=20260614-194454/events.log:6327,6333 progress={3}/{4} complete=true persistent=true b18dQuestDeleteProjected={5} b18eQuestFullUpdateProjected={6}", obj);
		return true;
	}

	private static bool EnsureQuestProjection(ICharacter source, string flagKey, Func<RexQuestPreviewEmissionResult> sender)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B18D", flagKey) != null)
		{
			return true;
		}
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = sender();
		if (rexQuestPreviewEmissionResult == null || !rexQuestPreviewEmissionResult.Emitted)
		{
			return false;
		}
		MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(instance, "Mission:5514B18D", flagKey, "true");
		return missionOperationResult.Status == MissionOperationStatus.Applied || missionOperationResult.Status == MissionOperationStatus.AlreadyApplied;
	}

	public static ObjectiveProgressRecord GetProgressForCharacter(ICharacter source)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		if (!MissionRuntime.IsInitialized)
		{
			return null;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B18D");
		if (mission == null)
		{
			return null;
		}
		PersistentMissionService service2 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionObjectiveProgressRecord objective = service2.GetObjective(((Identity)(ref identity)).Instance, "Mission:5514B18D", "mission_5514B18D_objective_questfullupdate");
		return ToRuntimeProgress(objective, objective?.LastObservationKey);
	}

	private static ObjectiveProgressRecord CopyProgress(ObjectiveProgressRecord progress)
	{
		if (progress == null)
		{
			return null;
		}
		return new ObjectiveProgressRecord
		{
			MissionId = progress.MissionId,
			ObjectiveId = progress.ObjectiveId,
			ObjectiveType = progress.ObjectiveType,
			CurrentCount = progress.CurrentCount,
			RequiredCount = progress.RequiredCount,
			Completed = progress.Completed,
			MatchedEvidenceCount = progress.MatchedEvidenceCount,
			IgnoredEvidenceCount = progress.IgnoredEvidenceCount,
			LastMatchedEvidenceReference = progress.LastMatchedEvidenceReference
		};
	}

	private static bool IsCargoBoxTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref target)).Type == 51005 && ((Identity)(ref target)).Instance == 1457108143;
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

	private static ObjectiveProgressRecord ToRuntimeProgress(MissionObjectiveProgressRecord progress, string evidenceReference)
	{
		if (progress == null)
		{
			return null;
		}
		return new ObjectiveProgressRecord
		{
			MissionId = "Mission:5514B18D",
			ObjectiveId = "mission_5514B18D_objective_questfullupdate",
			ObjectiveType = "CapturedUseInteractObjective",
			CurrentCount = progress.Progress,
			RequiredCount = progress.RequiredCount,
			Completed = (progress.Progress >= progress.RequiredCount),
			MatchedEvidenceCount = progress.Progress,
			IgnoredEvidenceCount = 0,
			LastMatchedEvidenceReference = evidenceReference
		};
	}

	private static bool IsTerminalFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}

	private static string IdentityText(ICharacter character)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		object result;
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			result = ((Identity)(ref identity)).ToString(true);
		}
		else
		{
			result = "<null>";
		}
		return (string)result;
	}

	private static void Log(string format, params object[] args)
	{
		LogUtil.Debug((DebugInfoDetail)128, "ARETE_REX_B18D_PREVIEW " + string.Format(CultureInfo.InvariantCulture, format, args));
	}
}
