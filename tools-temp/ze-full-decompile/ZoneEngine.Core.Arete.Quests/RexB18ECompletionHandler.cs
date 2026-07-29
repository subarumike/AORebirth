using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexB18ECompletionHandler
{
	private sealed class RewardFeedbackResult
	{
		public bool Sent { get; set; }

		public string Message { get; set; }
	}

	public const string EnableEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION";

	private const int AreteLandingPlayfieldId = 6553;

	private const int RexLarssonInstance = 2016273768;

	private const int XpReward = 290;

	private const int CreditReward = 1040;

	private const int RewardMessageDisplayXp = 1281;

	private const string RewardFeedbackText = "Received reward: 1281 XP, 1040 credits.";

	private const string MissionId = "Mission:5514B18E";

	private const string ObjectiveId = "mission_5514B18E_objective_questfullupdate";

	private const string RewardKey = "captured-xp-and-credits";

	public static bool IsCompletionEnabled => AreteEnvironmentGate.IsDefaultEnabled("AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION");

	public static RexB18ECompletionResult TryCompleteOnReturn(ICharacter source, Identity npcIdentity, bool dialogueGateEnabled)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRexLarsson(npcIdentity))
		{
			return RexB18ECompletionResult.NotApplicable();
		}
		bool isQuestPreviewEnabled = RexQuestPreviewEmitter.IsQuestPreviewEnabled;
		bool isPreviewCompletionEnabled = RexB18DBoxProgressTracker.IsPreviewCompletionEnabled;
		bool isCompletionEnabled = IsCompletionEnabled;
		if (!dialogueGateEnabled || !isQuestPreviewEnabled || !isPreviewCompletionEnabled || !isCompletionEnabled)
		{
			return RexB18ECompletionResult.Skipped("B18E completion skipped dialogueGate=" + dialogueGateEnabled + " questPreviewGate=" + isQuestPreviewEnabled + " b18dPreviewGate=" + isPreviewCompletionEnabled + " b18eCompletionGate=" + isCompletionEnabled + " attempted=false noAction59=true noCreditGrant=true noItems=true noInventory=true noDbMissionPersistence=true noMarcusStoneImplementation=true");
		}
		if (!IsValidPlayerInArete(source))
		{
			return RexB18ECompletionResult.Failed("B18E completion failed: source is missing, not a player, or not in Arete Landing 6553.");
		}
		if (!MissionRuntime.IsInitialized)
		{
			return RexB18ECompletionResult.Failed("B18E completion failed: persistent mission runtime is not initialized.");
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord missionStateRecord = EnsureB18EReadyForReturn(instance);
		if (missionStateRecord == null || (missionStateRecord.State != MissionLifecycleState.Active && missionStateRecord.State != MissionLifecycleState.Completed))
		{
			return RexB18ECompletionResult.Skipped("B18E completion skipped because the persistent mission is not active.");
		}
		if (missionStateRecord.State == MissionLifecycleState.Active)
		{
			PersistentMissionService service = MissionRuntime.Service;
			MissionObjectiveObservation obj = new MissionObjectiveObservation
			{
				CharacterId = instance,
				QuestId = "Mission:5514B18E",
				ObjectiveId = "mission_5514B18E_objective_questfullupdate",
				ObservationKey = "dialogue-return:" + ((Identity)(ref npcIdentity)).ToString(true),
				Amount = 1,
				EventType = "NpcDialogueOpen"
			};
			identity = ((IEntity)source).Identity;
			obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			obj.TargetIdentity = ((Identity)(ref npcIdentity)).ToString(true);
			MissionOperationResult missionOperationResult = service.ObserveObjective(obj);
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied && missionOperationResult.Status != MissionOperationStatus.DuplicateObservation)
			{
				return RexB18ECompletionResult.Failed("B18E objective persistence failed: " + missionOperationResult.Message);
			}
			MissionOperationResult missionOperationResult2 = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B18E");
			if (missionOperationResult2.Status != MissionOperationStatus.Applied && missionOperationResult2.Status != MissionOperationStatus.AlreadyApplied)
			{
				return RexB18ECompletionResult.Failed("B18E completion persistence failed: " + missionOperationResult2.Message);
			}
		}
		MissionRewardExecutionResult missionRewardExecutionResult = ApplyPersistentRewards(source);
		if (!missionRewardExecutionResult.Succeeded)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ARETE_REX_B18E_COMPLETION reward status=\"" + missionRewardExecutionResult.Message + "\" — continuing B18F client handoff");
		}
		MissionOperationResult missionOperationResult3 = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B18E", "Mission:5514B18F");
		if (IsPersistenceFailure(missionOperationResult3))
		{
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B18F");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B18F");
			LogUtil.Debug((DebugInfoDetail)512, "ARETE_REX_B18E_COMPLETION B18F handoff status=" + ((missionOperationResult3 == null) ? "null" : missionOperationResult3.Status.ToString()) + " message=\"" + ((missionOperationResult3 == null) ? "" : missionOperationResult3.Message) + "\" — forced offer/accept + client projection");
		}
		RewardFeedbackResult rewardFeedbackResult = null;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B18E", "reward-feedback-projected") == null)
		{
			rewardFeedbackResult = SendCapturedRewardFeedback(source);
			if (rewardFeedbackResult.Sent)
			{
				MissionRuntime.Service.SetFlag(instance, "Mission:5514B18E", "reward-feedback-projected", "true");
			}
		}
		bool flag = SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source)?.Emitted ?? false;
		if (!flag)
		{
			SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
			flag = SafeQuestFullUpdateSender.TrySendB18FPreview(source).Emitted;
		}
		if (!flag)
		{
			return RexB18ECompletionResult.Failed("B18E state and rewards are durable, but a client quest projection remains retryable.");
		}
		return RexB18ECompletionResult.Succeeded("B18E completion applied persistently mission=Mission:5514B18E rewardStatus=" + missionRewardExecutionResult.Status.ToString() + " xpDelta=" + 290 + " creditDelta=" + 1040 + " displayXp=" + 1281 + " b18fMission=Mission:5514B18F handoffProjected=true rewardFeedback=" + ((rewardFeedbackResult == null) ? "already-projected" : rewardFeedbackResult.Message));
	}

	private static ZoneEngine.Core.Missions.MissionStateRecord EnsureB18EReadyForReturn(int characterId)
	{
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, "Mission:5514B18E");
		if (mission != null && mission.State == MissionLifecycleState.Offered)
		{
			MissionRuntime.Service.AcceptMission(characterId, "Mission:5514B18E");
			mission = MissionRuntime.Service.GetMission(characterId, "Mission:5514B18E");
		}
		if (mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed))
		{
			return mission;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(characterId, "Mission:5514B18D");
		if (mission2 == null || mission2.State != MissionLifecycleState.Completed)
		{
			return mission;
		}
		MissionRuntime.Service.OfferMission(characterId, "Mission:5514B18E");
		MissionRuntime.Service.AcceptMission(characterId, "Mission:5514B18E");
		return MissionRuntime.Service.GetMission(characterId, "Mission:5514B18E");
	}

	private static MissionRewardExecutionResult ApplyPersistentRewards(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-xp-and-credits";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[4]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 61,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1040L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 52,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 290L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 290L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 290L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:5514B18E", definition, "capture:20260618-083035:rex-b18e-xp-credits");
		if (missionRewardExecutionResult.Succeeded && missionRewardExecutionResult.StatValues != null)
		{
			foreach (MissionCharacterStatValue statValue in missionRewardExecutionResult.StatValues)
			{
				uint num = (uint)((statValue.Value > 0) ? Math.Min(statValue.Value, 4294967295L) : 0u);
				((IStats)source).Stats[(StatIds)statValue.StatId].Set(num, false);
			}
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(source);
		}
		return missionRewardExecutionResult;
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}

	private static RewardFeedbackResult SendCapturedRewardFeedback(ICharacter source)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return new RewardFeedbackResult
			{
				Sent = false,
				Message = "Reward feedback skipped because source client is missing."
			};
		}
		((IDynel)source).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
		{
			Identity = ((IEntity)source).Identity,
			Unknown = 1,
			Unknown1 = 0,
			FormattedMessage = "Received reward: 1281 XP, 1040 credits.",
			Unknown2 = 0
		});
		Identity identity = ((IEntity)source).Identity;
		LogUtil.Debug((DebugInfoDetail)128, "ARETE_REX_B18E_COMPLETION reward feedback sent character=" + ((Identity)(ref identity)).ToString(true) + " message=\"Received reward: 1281 XP, 1040 credits.\" displayXp=1281 actualXpDelta=290 creditReward=1040 source=20260618-083035/events.log:1076,system-messages.log:281 safeFormatFeedback=true noAction59=true noItems=true noInventory=true");
		return new RewardFeedbackResult
		{
			Sent = true,
			Message = "Reward feedback sent using existing FormatFeedbackMessage path."
		};
	}

	private static bool IsRexLarsson(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref identity)).Type == 50000 && ((Identity)(ref identity)).Instance == 2016273768;
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
