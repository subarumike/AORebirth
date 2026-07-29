using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class FlintBioComQuestRuntime
{
	private sealed class AlexTradeSession
	{
		public Identity NpcIdentity { get; set; }

		public Identity StagedContainer { get; set; }
	}

	public const string AcceptNodeId = "flint_072904_002";

	public const string AlexTradeOfferNodeId = "alex_074847_001";

	public const string AlexTradeHoldNodeId = "alex_074847_trade";

	public const int BioAnalyzingComputerItemId = 156020;

	public const int BioAnalyzingComputerItemHighId = 156021;

	public const int BlankInfoChipItemId = 296570;

	public const int RebuiltHc12SecTecMonitorItemId = 295800;

	private const int AlexGibbsInstance = 2028010593;

	private const string FindObjectiveId = "mission_5514B19B_kill_junkyard_robots";

	private const string KillTargetName = "Cleaning Robot";

	private const string FeedbackTargetName = "Junkyard Robots";

	private const int RequiredKillCount = 7;

	private const string BioComGrantFlag = "bio-com-granted";

	private const string AlexTurnInRewardsFlag = "alex-turnin-rewards";

	private const int TurnInXpReward = 2076;

	private const int TurnInCreditReward = 1120;

	private const string TurnInRewardFeedback = "~&!!!\":$'O\"ui!!!9Ei!!!.0~";

	private const int AreteLandingPlayfieldId = 6553;

	private static readonly object TradeSyncRoot = new object();

	private static readonly Dictionary<int, AlexTradeSession> TradeSessionsByCharacter = new Dictionary<int, AlexTradeSession>();

	private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

	public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
	{
		if (source == null || !string.Equals(previousNodeId, "flint_072904_002", StringComparison.OrdinalIgnoreCase) || answerIndex != 0)
		{
			return false;
		}
		return TryAcceptFindQuest(source);
	}

	public static bool TryBeginAlexTrade(ICharacter source, Identity alexIdentity)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		Identity val;
		if ((int)((Identity)(ref alexIdentity)).Type != 50000 || ((Identity)(ref alexIdentity)).Instance == 0)
		{
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = 2028010593;
			alexIdentity = val;
		}
		BeginAlexTrade(source, alexIdentity);
		BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, alexIdentity, "Drag and drop the item(s) you want to give to Alex Gibbs into one of the slots available and press \"accept\"", 1);
		val = ((IEntity)source).Identity;
		Log("alex-trade-opened character=" + ((Identity)(ref val)).ToString(true) + " target=" + ((Identity)(ref alexIdentity)).ToString(true));
		return true;
	}

	public static bool TryStageAlexTradeItem(ICharacter source, KnuBotTradeMessage message)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || message == null || !IsAlexGibbsNpc(source, message.Target))
		{
			return false;
		}
		if (!IsDeliverTipActive(source) && GetTradeSession(source) == null)
		{
			return false;
		}
		BeginAlexTrade(source, message.Target);
		AlexTradeSession tradeSession = GetTradeSession(source);
		if (tradeSession == null)
		{
			return true;
		}
		tradeSession.NpcIdentity = message.Target;
		Identity container = message.Container;
		if ((int)((Identity)(ref container)).Type != 0)
		{
			container = message.Container;
			if (((Identity)(ref container)).Instance > 0)
			{
				tradeSession.StagedContainer = message.Container;
			}
		}
		return true;
	}

	public static bool TryFinishAlexTrade(ICharacter source, KnuBotFinishTradeMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || message == null)
		{
			return false;
		}
		bool flag = IsAlexGibbsNpc(source, message.Target);
		AlexTradeSession tradeSession = GetTradeSession(source);
		bool flag2 = IsDeliverTipActive(source);
		if (!flag && tradeSession == null && !flag2)
		{
			return false;
		}
		if (message.Decline != 0)
		{
			ForgetTradeSession(source);
			return true;
		}
		if (tradeSession == null)
		{
			BeginAlexTrade(source, message.Target);
			tradeSession = GetTradeSession(source);
		}
		Identity stagedContainer = tradeSession?.StagedContainer ?? Identity.None;
		ApplyAlexTradeTurnIn(source, message.Target, stagedContainer);
		return true;
	}

	public static bool TryAcceptFindQuest(ICharacter source)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		if (!IsValidPlayerInArete(source) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult missionOperationResult = MissionRuntime.Service.OfferMission(instance, "Mission:5514B19B");
		if (IsTerminalFailure(missionOperationResult))
		{
			Log("find offer failed status=" + missionOperationResult.Status.ToString() + " msg=" + missionOperationResult.Message);
			return false;
		}
		MissionOperationResult missionOperationResult2 = MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19B");
		if (IsTerminalFailure(missionOperationResult2))
		{
			Log("find accept failed status=" + missionOperationResult2.Status.ToString() + " msg=" + missionOperationResult2.Message);
			return false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		if (mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered))
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B198");
		}
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendFlintToFindBioHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("find accepted character=" + ((Identity)(ref identity)).ToString(true) + " handoff=" + ((rexQuestPreviewEmissionResult == null) ? "null" : rexQuestPreviewEmissionResult.Message));
		return rexQuestPreviewEmissionResult?.Emitted ?? false;
	}

	public static bool TryObserveNpcDeath(ICharacter attacker, ICharacter target)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || target == null || !(((IDynel)attacker).Controller is PlayerController))
		{
			return false;
		}
		if (!IsInAreteLanding(attacker) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (!string.Equals(EffectiveName(target), "Cleaning Robot", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)attacker).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19B");
		if (mission == null || mission.State != MissionLifecycleState.Active)
		{
			return false;
		}
		identity = ((IEntity)target).Identity;
		string observationKey = "npc-death:" + ((Identity)(ref identity)).ToString(true);
		PersistentMissionService service2 = MissionRuntime.Service;
		MissionObjectiveObservation missionObjectiveObservation = new MissionObjectiveObservation();
		identity = ((IEntity)attacker).Identity;
		missionObjectiveObservation.CharacterId = ((Identity)(ref identity)).Instance;
		missionObjectiveObservation.QuestId = "Mission:5514B19B";
		missionObjectiveObservation.ObjectiveId = "mission_5514B19B_kill_junkyard_robots";
		missionObjectiveObservation.ObservationKey = observationKey;
		missionObjectiveObservation.Amount = 1;
		missionObjectiveObservation.EventType = "KillNpcTarget:CharacterAction:Death";
		identity = ((IEntity)attacker).Identity;
		missionObjectiveObservation.SourceIdentity = ((Identity)(ref identity)).ToString(true);
		identity = ((IEntity)target).Identity;
		missionObjectiveObservation.TargetIdentity = ((Identity)(ref identity)).ToString(true);
		MissionOperationResult missionOperationResult = service2.ObserveObjective(missionObjectiveObservation);
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied && missionOperationResult.Status != MissionOperationStatus.DuplicateObservation)
		{
			return false;
		}
		MissionObjectiveProgressRecord objective = missionOperationResult.Objective;
		if (objective == null)
		{
			PersistentMissionService service3 = MissionRuntime.Service;
			identity = ((IEntity)attacker).Identity;
			objective = service3.GetObjective(((Identity)(ref identity)).Instance, "Mission:5514B19B", "mission_5514B19B_kill_junkyard_robots");
		}
		MissionObjectiveProgressRecord missionObjectiveProgressRecord = objective;
		int num = missionObjectiveProgressRecord?.Progress ?? 0;
		int num2 = ((missionObjectiveProgressRecord == null || missionObjectiveProgressRecord.RequiredCount <= 0) ? 7 : missionObjectiveProgressRecord.RequiredCount);
		TrySendKillFeedback(attacker, num, num2);
		if (num >= num2)
		{
			CompleteFindAndOfferDeliver(attacker);
		}
		return true;
	}

	public static bool TryResendActiveTip(ICharacter source)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B19D");
		if (IsActiveOrOffered(mission))
		{
			return SafeQuestFullUpdateSender.TrySendSurveillanceUplinkPreview(source)?.Emitted ?? false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19C");
		if (IsActiveOrOffered(mission2))
		{
			return SafeQuestFullUpdateSender.TrySendDeliverBioPreview(source)?.Emitted ?? false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19B");
		if (IsActiveOrOffered(mission3))
		{
			return SafeQuestFullUpdateSender.TrySendFindBioPreview(source)?.Emitted ?? false;
		}
		return false;
	}

	private static void ApplyAlexTradeTurnIn(ICharacter source, Identity alexTarget, Identity stagedContainer)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		lock (TradeSyncRoot)
		{
			if (!TurnInInFlightByCharacter.Add(instance))
			{
				return;
			}
		}
		try
		{
			TryConsumeBioCom(source, stagedContainer);
			try
			{
				BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, alexTarget, (IEnumerable<Item>)(object)new Item[0], 0);
			}
			catch (Exception ex)
			{
				Log("alex-rejecteditems failed: " + ex.Message);
			}
			ApplyAlexTurnInXpCredits(source);
			TrySendTurnInRewardFeedback(source);
			TryGrantAlexTurnInItems(source);
			CompleteDeliverAndOfferUplink(source);
			ForgetTradeSession(source);
			try
			{
				ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, alexTarget);
			}
			catch (Exception ex2)
			{
				Log("alex-resume-dialogue failed: " + ex2.Message);
			}
			identity = ((IEntity)source).Identity;
			Log("alex-turnin done character=" + ((Identity)(ref identity)).ToString(true));
		}
		finally
		{
			lock (TradeSyncRoot)
			{
				TurnInInFlightByCharacter.Remove(instance);
			}
		}
	}

	private static void CompleteDeliverAndOfferUplink(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19C", "Mission:5514B19D");
		if (IsTerminalFailure(result))
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19C");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B19D");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19D");
		}
		SafeQuestFullUpdateSender.TrySendDeliverBioToSurveillanceUplinkHandoff(source);
	}

	private static void CompleteFindAndOfferDeliver(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		TryGrantBioCom(source);
		MissionOperationResult result = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19B", "Mission:5514B19C");
		if (IsTerminalFailure(result))
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19B");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B19C");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19C");
		}
		SafeQuestFullUpdateSender.TrySendFindBioToDeliverHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("find→deliver handoff character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void TryGrantAlexTurnInItems(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized)
		{
			Identity identity = ((IEntity)source).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B19C", "alex-turnin-rewards") == null)
			{
				GrantSingleRewardItem(source, 296570);
				GrantSingleRewardItem(source, 295800);
				MissionRuntime.Service.SetFlag(instance, "Mission:5514B19C", "alex-turnin-rewards", "items:" + 296570 + "+" + 295800);
			}
		}
	}

	private static void GrantSingleRewardItem(ICharacter source, int itemId)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Expected O, but got Unknown
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Expected O, but got Unknown
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(itemId))
		{
			Log("reward grant skipped item=" + itemId);
		}
		else
		{
			if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
			{
				return;
			}
			Item item;
			try
			{
				item = new Item(1, itemId, itemId);
			}
			catch (Exception ex)
			{
				Log("reward create failed item=" + itemId + " err=" + ex.Message);
				return;
			}
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
			if (questRewardInventoryGrantResult.Status != 0)
			{
				Log("reward grant failed item=" + itemId + " status=" + questRewardInventoryGrantResult.Status);
				return;
			}
			TemplateActionMessage val = new TemplateActionMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 0,
				ItemLowId = itemId,
				ItemHighId = itemId,
				Quality = 1,
				Unknown1 = 1,
				Unknown2 = 87
			};
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			((Identity)(ref val2)).Instance = 0;
			val.Placement = val2;
			val.Unknown3 = 0;
			val.Unknown4 = 0;
			((IDynel)source).Send((MessageBody)val, false);
			ContainerAddItemMessage val3 = new ContainerAddItemMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 0
			};
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			((Identity)(ref val2)).Instance = 0;
			val3.SourceContainer = val2;
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			Identity identity = ((IEntity)source).Identity;
			((Identity)(ref val2)).Instance = ((Identity)(ref identity)).Instance;
			val3.Target = val2;
			val3.TargetPlacement = 111;
			((IDynel)source).Send((MessageBody)val3, false);
			if (itemId == 296570)
			{
				BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(source, 110, 108871108);
			}
		}
	}

	private static void ApplyAlexTurnInXpCredits(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-alex-bio-com-turnin-xp-credits";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[4]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 61,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1120L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 52,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2076L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2076L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 2076L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:5514B19C", definition, "capture:20260720-074847:alex-turnin-xp-credits");
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

	private static void TrySendTurnInRewardFeedback(ICharacter source)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
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
				FormattedMessage = "~&!!!\":$'O\"ui!!!9Ei!!!.0~",
				Unknown2 = 0
			});
		}
		catch (Exception ex)
		{
			Log("alex reward feedback failed: " + ex.Message);
		}
	}

	private static bool TryGrantBioCom(ICharacter source)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Expected O, but got Unknown
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity placement = ((IEntity)source).Identity;
		int instance = ((Identity)(ref placement)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B19B", "bio-com-granted") != null)
		{
			return true;
		}
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(156020))
		{
			Log("bio-com grant skipped: inventory/item missing id=" + 156020);
			return false;
		}
		if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 156020))
		{
			Item item;
			try
			{
				item = new Item(1, 156020, 156020);
			}
			catch (Exception ex)
			{
				Log("bio-com item create failed: " + ex.Message);
				return false;
			}
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
			if (questRewardInventoryGrantResult.Status != 0)
			{
				Log("bio-com grant failed status=" + questRewardInventoryGrantResult.Status);
				return false;
			}
			TemplateActionMessage val = new TemplateActionMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 0,
				ItemLowId = 156020,
				ItemHighId = 156020,
				Quality = 1,
				Unknown1 = 1,
				Unknown2 = 87
			};
			placement = default(Identity);
			((Identity)(ref placement)).Type = (IdentityType)110;
			((Identity)(ref placement)).Instance = 0;
			val.Placement = placement;
			val.Unknown3 = 0;
			val.Unknown4 = 0;
			((IDynel)source).Send((MessageBody)val, false);
		}
		MissionRuntime.Service.SetFlag(instance, "Mission:5514B19B", "bio-com-granted", "item:" + 156020);
		return true;
	}

	private static void TryConsumeBioCom(ICharacter source, Identity stagedContainer)
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected I4, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected I4, but got Unknown
		if (source == null || ((IItemContainer)source).BaseInventory == null)
		{
			return;
		}
		if ((int)((Identity)(ref stagedContainer)).Type != 0 && ((Identity)(ref stagedContainer)).Instance > 0 && ((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref stagedContainer)).Type, out var value) && value != null)
		{
			IItem val = value[((Identity)(ref stagedContainer)).Instance];
			if (IsBioCom(val))
			{
				value.Remove(((Identity)(ref stagedContainer)).Instance);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, (int)((Identity)(ref stagedContainer)).Type, ((Identity)(ref stagedContainer)).Instance);
						return;
					}
				}
				catch (Exception)
				{
				}
				value.Add(((Identity)(ref stagedContainer)).Instance, val);
			}
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)source).BaseInventory.Pages)
		{
			IInventoryPage value2 = page.Value;
			if (value2 == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item in value2.List())
			{
				IItem value3 = item.Value;
				if (!IsBioCom(value3))
				{
					continue;
				}
				value2.Remove(item.Key);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, page.Key, item.Key);
						return;
					}
				}
				catch (Exception)
				{
				}
				value2.Add(item.Key, value3);
			}
		}
	}

	private static void TrySendKillFeedback(ICharacter character, int currentCount, int requiredCount)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null && currentCount > 0 && currentCount < requiredCount)
		{
			string capturedRemainingCountFeedback = GetCapturedRemainingCountFeedback(currentCount);
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
		}
	}

	private static string GetCapturedRemainingCountFeedback(int currentCount)
	{
		return currentCount switch
		{
			1 => "~&!!!\":$nZiAi!!!!'s\u001eJunkyard Robots", 
			2 => "~&!!!\":$nZiAi!!!!&s\u001eJunkyard Robots", 
			3 => "~&!!!\":$nZiAi!!!!%s\u001eJunkyard Robots", 
			4 => "~&!!!\":$nZiAi!!!!$s\u001eJunkyard Robots", 
			5 => "~&!!!\":$nZiAi!!!!#s\u001eJunkyard Robots", 
			6 => "~&!!!\":$nZiAi!!!!\"s\u001eJunkyard Robots", 
			_ => null, 
		};
	}

	private static bool IsDeliverTipActive(ICharacter source)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19C");
		return IsActiveOrOffered(mission);
	}

	private static bool IsAlexGibbsNpc(ICharacter source, Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref target)).Type == 50000 && ((Identity)(ref target)).Instance == 2028010593)
		{
			return true;
		}
		if (source == null || ((IInstancedEntity)source).Playfield == null)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, target);
		return @object != null && string.Equals(((INamedEntity)@object).Name, "Alex Gibbs", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsBioCom(IItem item)
	{
		return item != null && (item.LowID == 156020 || item.HighID == 156020 || item.LowID == 156021 || item.HighID == 156021);
	}

	private static void BeginAlexTrade(ICharacter source, Identity alexIdentity)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		lock (TradeSyncRoot)
		{
			Dictionary<int, AlexTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter[((Identity)(ref identity)).Instance] = new AlexTradeSession
			{
				NpcIdentity = alexIdentity,
				StagedContainer = Identity.None
			};
		}
	}

	private static AlexTradeSession GetTradeSession(ICharacter source)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		lock (TradeSyncRoot)
		{
			Dictionary<int, AlexTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter.TryGetValue(((Identity)(ref identity)).Instance, out var value);
			return value;
		}
	}

	private static void ForgetTradeSession(ICharacter source)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		lock (TradeSyncRoot)
		{
			Dictionary<int, AlexTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter.Remove(((Identity)(ref identity)).Instance);
		}
	}

	private static bool IsValidPlayerInArete(ICharacter source)
	{
		return source != null && ((IDynel)source).Controller is PlayerController && IsInAreteLanding(source);
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

	private static bool IsActiveOrOffered(ZoneEngine.Core.Missions.MissionStateRecord mission)
	{
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	private static bool IsTerminalFailure(MissionOperationResult result)
	{
		return result == null || (result.Status != MissionOperationStatus.Applied && result.Status != MissionOperationStatus.AlreadyApplied);
	}

	private static string EffectiveName(ICharacter character)
	{
		return (character == null) ? string.Empty : (((INamedEntity)character).Name ?? string.Empty).Trim();
	}

	private static void Log(string message)
	{
		LogUtil.Debug((DebugInfoDetail)128, "FlintBioCom " + message);
	}
}
