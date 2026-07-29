using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
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

public static class KneecappingQuestRuntime
{
	public const string AlexReportRootNodeId = "alex_171317_001";

	public const string AlexTradeskillOfferNodeId = "alex_171317_002";

	private const string KillTargetName = "Kneebreaker Alfonzo Rizzolo";

	private const int EncryptionCompilerItemId = 296571;

	private const int EncryptionCodesItemId = 287041;

	private const int EncryptionCodesQuality = 25;

	private const int ReportTurnInXpReward = 2581;

	private const int ReportTurnInCreditReward = 1200;

	private const string ReportRewardFeedback = "You gained 2581 experience points and 1200 credits.";

	private const string ReportRewardFlag = "report-alex-rewards-granted";

	private const int AreteLandingPlayfieldId = 6553;

	public static string ResolveAlexStartNodeId(ICharacter source)
	{
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return null;
		}
		if (IsMissionActive(source, "Mission:555B4365"))
		{
			return "alex_171317_001";
		}
		if (IsMissionActive(source, "Mission:555B4366") && !IsMissionActive(source, "Mission:555B4367") && !IsMissionCompleted(source, "Mission:555B4367"))
		{
			return "alex_171317_002";
		}
		return null;
	}

	public static bool TryHandleAlexDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
	{
		if (source == null || answerIndex != 0)
		{
			return false;
		}
		if (string.Equals(previousNodeId, "alex_171317_001", StringComparison.OrdinalIgnoreCase))
		{
			CompleteReportToAlexAndOfferTalkToStan(source);
			return true;
		}
		if (string.Equals(previousNodeId, "alex_171317_002", StringComparison.OrdinalIgnoreCase))
		{
			OfferTradeskillNanoSensorTip(source);
			return true;
		}
		return false;
	}

	public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || target == null || !(((IDynel)attacker).Controller is PlayerController))
		{
			return false;
		}
		if (!IsInAreteLanding(attacker) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (!string.Equals(EffectiveName(target), "Kneebreaker Alfonzo Rizzolo", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)attacker).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B1A0");
		if (mission == null || mission.State != MissionLifecycleState.Active)
		{
			return false;
		}
		CompleteKneecappingAndOfferReportToAlex(attacker);
		return true;
	}

	private static void CompleteKneecappingAndOfferReportToAlex(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B1A0", "Mission:555B4365");
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B1A0");
			MissionRuntime.Service.OfferMission(instance, "Mission:555B4365");
			MissionRuntime.Service.AcceptMission(instance, "Mission:555B4365");
		}
		SafeQuestFullUpdateSender.TrySendKneecappingToReportAlexHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("kneecapping-complete→report-alex character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void CompleteReportToAlexAndOfferTalkToStan(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ApplyReportTurnInXpCredits(source);
		TrySendReportRewardFeedback(source);
		TryGrantReportRewardItems(source);
		MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:555B4365", "Mission:555B4366");
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:555B4365");
			MissionRuntime.Service.OfferMission(instance, "Mission:555B4366");
			MissionRuntime.Service.AcceptMission(instance, "Mission:555B4366");
		}
		SafeQuestFullUpdateSender.TrySendReportAlexToTalkStanHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("report-alex-complete→talk-stan character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void OfferTradeskillNanoSensorTip(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:555B4367");
		if (mission == null || mission.State != MissionLifecycleState.Active)
		{
			MissionRuntime.Service.OfferMission(instance, "Mission:555B4367");
			MissionRuntime.Service.AcceptMission(instance, "Mission:555B4367");
		}
		SafeQuestFullUpdateSender.TrySendTradeskillNanoSensorTip(source);
		identity = ((IEntity)source).Identity;
		Log("tradeskill-nano-sensor tip character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void ApplyReportTurnInXpCredits(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-alex-report-calitri-xp-credits";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[4]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 61,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1200L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 52,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2581L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2581L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 2581L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:555B4365", definition, "capture:20260720-171317:alex-report-xp-credits");
		if (!missionRewardExecutionResult.Succeeded || missionRewardExecutionResult.StatValues == null)
		{
			return;
		}
		foreach (MissionCharacterStatValue statValue in missionRewardExecutionResult.StatValues)
		{
			uint num = (uint)((statValue.Value > 0) ? Math.Min(statValue.Value, 4294967295L) : 0u);
			((IStats)source).Stats[(StatIds)statValue.StatId].Set(num, false);
		}
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(source);
	}

	private static void TrySendReportRewardFeedback(ICharacter source)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return;
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "You gained 2581 experience points and 1200 credits.",
				Unknown2 = 0
			});
		}
		catch (Exception ex)
		{
			Log("report reward feedback failed: " + ex.Message);
		}
	}

	private static void TryGrantReportRewardItems(ICharacter source)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		if (service.GetFlag(((Identity)(ref identity)).Instance, "Mission:555B4365", "report-alex-rewards-granted") == null)
		{
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, new Item(1, 296571, 296571));
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult2 = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, new Item(25, 287041, 287041));
			bool flag = questRewardInventoryGrantResult != null && questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.Success;
			bool flag2 = questRewardInventoryGrantResult2 != null && questRewardInventoryGrantResult2.Status == QuestRewardInventoryGrantStatus.Success;
			if (flag || flag2)
			{
				PersistentMissionService service2 = MissionRuntime.Service;
				identity = ((IEntity)source).Identity;
				service2.SetFlag(((Identity)(ref identity)).Instance, "Mission:555B4365", "report-alex-rewards-granted", "items:" + 296571 + "," + 287041);
			}
			string[] obj = new string[6]
			{
				"report rewards compiler=",
				flag.ToString(),
				" codes=",
				flag2.ToString(),
				" character=",
				null
			};
			identity = ((IEntity)source).Identity;
			obj[5] = ((Identity)(ref identity)).ToString(true);
			Log(string.Concat(obj));
		}
	}

	private static bool IsMissionActive(ICharacter source, string questId)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, questId);
		return mission != null && mission.State == MissionLifecycleState.Active;
	}

	private static bool IsMissionCompleted(ICharacter source, string questId)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, questId);
		return mission != null && mission.State == MissionLifecycleState.Completed;
	}

	private static bool IsInAreteLanding(ICharacter source)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (((IInstancedEntity)source).Playfield != null)
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

	private static string EffectiveName(ICharacter character)
	{
		return (character == null) ? string.Empty : (((INamedEntity)character).Name ?? string.Empty).Trim();
	}

	private static void Log(string message)
	{
		LogUtil.Debug((DebugInfoDetail)512, "KneecappingQuestRuntime " + message);
	}
}
