using System;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexB18CObjectiveProgressTracker
{
	private sealed class RexB18CProgressState
	{
		public Identity CharacterIdentity { get; set; }

		public string CharacterIdentityText { get; set; }

		public ObjectiveProgressRecord Progress { get; set; }

		public DateTime ActivatedAtUtc { get; set; }

		public bool CompletionHandoffSent { get; set; }
	}

	private static class RexB18CProgressFeedbackSender
	{
		private const int FeedbackCategoryId = 110;

		private const int FeedbackMessageId = 249817907;

		public static bool TrySend(ICharacter character, ObjectiveProgressRecord progress)
		{
			//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Expected O, but got Unknown
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0098: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Expected O, but got Unknown
			if (character == null || ((IDynel)character).Controller == null || ((IDynel)character).Controller.Client == null)
			{
				return false;
			}
			if (progress == null || !RexB18CFeedbackPolicy.ShouldSendPerKillFeedback(progress.CurrentCount, progress.RequiredCount))
			{
				return false;
			}
			string capturedRemainingCountFeedback = GetCapturedRemainingCountFeedback(progress.CurrentCount);
			if (!string.IsNullOrEmpty(capturedRemainingCountFeedback))
			{
				((IDynel)character).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
				{
					Identity = ((IEntity)character).Identity,
					Unknown = 1,
					Unknown1 = 0,
					FormattedMessage = capturedRemainingCountFeedback,
					Unknown2 = 0
				});
			}
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)new FeedbackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Unknown1 = 0,
				CategoryId = 110,
				MessageId = 249817907
			});
			Log("feedback sent mission={0} character={1} progress={2}/{3} remainingFormat={4} sender=server", "Mission:5514B18C", IdentityText(character), progress.CurrentCount, progress.RequiredCount, !string.IsNullOrEmpty(capturedRemainingCountFeedback));
			return true;
		}

		private static string GetCapturedRemainingCountFeedback(int currentCount)
		{
			return currentCount switch
			{
				1 => "~&!!!\":$nZiAi!!!!%s\u001eMalfunctioning Cleaning Robot", 
				2 => "~&!!!\":$nZiAi!!!!$s\u001eMalfunctioning Cleaning Robot", 
				3 => "~&!!!\":$nZiAi!!!!#s\u001eMalfunctioning Cleaning Robot", 
				4 => "~&!!!\":$nZiAi!!!!\"s\u001eMalfunctioning Cleaning Robot", 
				_ => null, 
			};
		}
	}

	public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS";

	private const string DialogueGateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

	private const string QuestPreviewGateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW";

	private const int AreteLandingPlayfieldId = 6553;

	private const string MissionId = "Mission:5514B18C";

	private const string ObjectiveId = "mission_5514B18C_objective_questfullupdate";

	private const string ObjectiveType = "CapturedKillCountObjective";

	private const string TargetName = "Malfunctioning Cleaning Robot";

	private const int RequiredCount = 5;

	public static bool IsProgressEnabled => AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS");

	public static bool AreAllGatesEnabled => AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING") && AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW") && IsProgressEnabled;

	public static bool TryActivateFromPreview(ICharacter source, RexQuestPreviewEmissionResult previewResult)
	{
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		if (previewResult == null || !previewResult.Emitted)
		{
			return false;
		}
		if (!AreAllGatesEnabled)
		{
			Log("activation skipped mission={0} allGates=false dialogueGate={1} questPreviewGate={2} progressGate={3} noPersistence=true noCompletion=true noQuestDelete=true", "Mission:5514B18C", AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING"), AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW"), IsProgressEnabled);
			return false;
		}
		if (!IsValidPlayerInArete(source))
		{
			Log("activation failed mission={0} reason=invalid-player-or-playfield source={1} noPersistence=true noCompletion=true noQuestDelete=true", "Mission:5514B18C", IdentityText(source));
			return false;
		}
		if (!MissionRuntime.IsInitialized)
		{
			Log("activation failed mission={0} reason=mission-runtime-not-initialized", "Mission:5514B18C");
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionOperationResult missionOperationResult = service.OfferMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (IsTerminalFailure(missionOperationResult))
		{
			Log("activation failed mission={0} status={1} message=\"{2}\"", "Mission:5514B18C", missionOperationResult.Status, missionOperationResult.Message);
			return false;
		}
		PersistentMissionService service2 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionOperationResult missionOperationResult2 = service2.AcceptMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (IsTerminalFailure(missionOperationResult2))
		{
			Log("activation failed mission={0} status={1} message=\"{2}\"", "Mission:5514B18C", missionOperationResult2.Status, missionOperationResult2.Message);
			return false;
		}
		object[] obj = new object[3] { "Mission:5514B18C", null, null };
		identity = ((IEntity)source).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = 5;
		Log("activated mission={0} character={1} progress=0/{2} persistent=true", obj);
		RexMissionChainStateStore.AdvanceAtLeast(source, RexMissionChainState.B18CPreviewed, "B18C preview activated");
		return true;
	}

	public static bool HasActiveProgress(ICharacter source)
	{
		return GetProgressForCharacter(source) != null;
	}

	public static RexB18CProgressUpdateResult TryObserveNpcDeath(ICharacter attacker, ICharacter target)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		if (!AreAllGatesEnabled)
		{
			return RexB18CProgressUpdateResult.NotApplicable();
		}
		if (attacker == null || target == null)
		{
			return RexB18CProgressUpdateResult.Ignored("missing attacker or target");
		}
		if (!(((IDynel)attacker).Controller is PlayerController))
		{
			return RexB18CProgressUpdateResult.Ignored("attacker is not a player");
		}
		if (!IsInAreteLanding(attacker))
		{
			return RexB18CProgressUpdateResult.Ignored("attacker is not in Arete Landing");
		}
		if (!MissionRuntime.IsInitialized)
		{
			return RexB18CProgressUpdateResult.Ignored("mission runtime is not initialized");
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)attacker).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (mission == null || (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Completed))
		{
			return RexB18CProgressUpdateResult.Ignored("no active or completed B18C mission for attacker");
		}
		string a = EffectiveName(target);
		if (!string.Equals(a, "Malfunctioning Cleaning Robot", StringComparison.OrdinalIgnoreCase))
		{
			return RexB18CProgressUpdateResult.Ignored("target name did not match");
		}
		identity = ((IEntity)target).Identity;
		string text = "npc-death:" + ((Identity)(ref identity)).ToString(true);
		ObjectiveProgressRecord objectiveProgressRecord;
		if (mission.State == MissionLifecycleState.Active)
		{
			PersistentMissionService service2 = MissionRuntime.Service;
			MissionObjectiveObservation missionObjectiveObservation = new MissionObjectiveObservation();
			identity = ((IEntity)attacker).Identity;
			missionObjectiveObservation.CharacterId = ((Identity)(ref identity)).Instance;
			missionObjectiveObservation.QuestId = "Mission:5514B18C";
			missionObjectiveObservation.ObjectiveId = "mission_5514B18C_objective_questfullupdate";
			missionObjectiveObservation.ObservationKey = text;
			missionObjectiveObservation.Amount = 1;
			missionObjectiveObservation.EventType = "KillNpcTarget:CharacterAction:Death";
			identity = ((IEntity)attacker).Identity;
			missionObjectiveObservation.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)target).Identity;
			missionObjectiveObservation.TargetIdentity = ((Identity)(ref identity)).ToString(true);
			MissionOperationResult missionOperationResult = service2.ObserveObjective(missionObjectiveObservation);
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied && missionOperationResult.Status != MissionOperationStatus.DuplicateObservation)
			{
				return RexB18CProgressUpdateResult.Ignored(missionOperationResult.Message ?? missionOperationResult.Status.ToString());
			}
			objectiveProgressRecord = ToRuntimeProgress(missionOperationResult.Objective, text);
		}
		else
		{
			PersistentMissionService service3 = MissionRuntime.Service;
			identity = ((IEntity)attacker).Identity;
			MissionObjectiveProgressRecord objective = service3.GetObjective(((Identity)(ref identity)).Instance, "Mission:5514B18C", "mission_5514B18C_objective_questfullupdate");
			objectiveProgressRecord = ToRuntimeProgress(objective, (objective == null) ? text : objective.LastObservationKey);
		}
		LogProgress(attacker, target, objectiveProgressRecord);
		RexB18CProgressFeedbackSender.TrySend(attacker, objectiveProgressRecord);
		if (objectiveProgressRecord != null && objectiveProgressRecord.Completed)
		{
			PersistentMissionService service4 = MissionRuntime.Service;
			identity = ((IEntity)attacker).Identity;
			MissionOperationResult missionOperationResult2 = service4.CompleteAndActivateNextMission(((Identity)(ref identity)).Instance, "Mission:5514B18C", "Mission:5514B18D");
			if (missionOperationResult2.Status == MissionOperationStatus.Applied || missionOperationResult2.Status == MissionOperationStatus.AlreadyApplied)
			{
				RexMissionChainStateStore.AdvanceAtLeast(attacker, RexMissionChainState.B18DPreviewed, "B18C completion activated B18D persistently");
				EnsureCompletionHandoffProjection(attacker);
			}
		}
		return RexB18CProgressUpdateResult.MatchedProgress(objectiveProgressRecord);
	}

	private static bool EnsureCompletionHandoffProjection(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B18C", "b18c-completion-handoff-projected") != null)
		{
			return true;
		}
		if (!SafeQuestFullUpdateSender.TrySendB18CCompletionHandoff(source))
		{
			return false;
		}
		MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(instance, "Mission:5514B18C", "b18c-completion-handoff-projected", "true");
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
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B18C");
		if (mission == null)
		{
			return null;
		}
		PersistentMissionService service2 = MissionRuntime.Service;
		identity = ((IEntity)source).Identity;
		MissionObjectiveProgressRecord objective = service2.GetObjective(((Identity)(ref identity)).Instance, "Mission:5514B18C", "mission_5514B18C_objective_questfullupdate");
		return ToRuntimeProgress(objective, objective?.LastObservationKey);
	}

	private static void LogProgress(ICharacter attacker, ICharacter target, ObjectiveProgressRecord progress)
	{
		if (progress.Completed)
		{
			Log("progress mission={0} character={1} target={2} targetName=\"{3}\" progress={4}/{5} complete=true inMemoryOnly=true capturedCompletionHandoffPending=true noRewards=true noDbWrites=true noPersistence=true", "Mission:5514B18C", IdentityText(attacker), IdentityText(target), EffectiveName(target), progress.CurrentCount, progress.RequiredCount);
		}
		else
		{
			Log("progress mission={0} character={1} target={2} targetName=\"{3}\" progress={4}/{5} complete=false inMemoryOnly=true noMissionCompletion=true noQuestDelete=true noRewards=true noDbWrites=true", "Mission:5514B18C", IdentityText(attacker), IdentityText(target), EffectiveName(target), progress.CurrentCount, progress.RequiredCount);
		}
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

	private static bool IsValidPlayerInArete(ICharacter source)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (source != null && ((IDynel)source).Controller is PlayerController)
		{
			Identity identity = ((IEntity)source).Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				identity = ((IEntity)source).Identity;
				if (((Identity)(ref identity)).Instance != 0)
				{
					result = (IsInAreteLanding(source) ? 1 : 0);
					goto IL_003f;
				}
			}
		}
		result = 0;
		goto IL_003f;
		IL_003f:
		return (byte)result != 0;
	}

	private static bool IsInAreteLanding(ICharacter character)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (character != null && ((IInstancedEntity)character).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			result = ((((Identity)(ref identity)).Instance == 6553) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static string EffectiveName(ICharacter character)
	{
		if (character == null)
		{
			return string.Empty;
		}
		if (!string.IsNullOrWhiteSpace(((INamedEntity)character).Name))
		{
			return ((INamedEntity)character).Name;
		}
		return ((character.FirstName ?? string.Empty) + " " + (character.LastName ?? string.Empty)).Trim();
	}

	private static ObjectiveProgressRecord ToRuntimeProgress(MissionObjectiveProgressRecord progress, string evidenceReference)
	{
		if (progress == null)
		{
			return null;
		}
		return new ObjectiveProgressRecord
		{
			MissionId = "Mission:5514B18C",
			ObjectiveId = "mission_5514B18C_objective_questfullupdate",
			ObjectiveType = "CapturedKillCountObjective",
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
		LogUtil.Debug((DebugInfoDetail)128, "ARETE_REX_B18C_PROGRESS " + string.Format(CultureInfo.InvariantCulture, format, args));
	}
}
