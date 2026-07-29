using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
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

public static class MarcusWoundedWorkersQuestRuntime
{
	private sealed class HealRecoveryState
	{
		public float HomeX;

		public float HomeY;

		public float HomeZ;

		public DateTime RelapseUtc;

		public bool WalkedHome;
	}

	public const string WoundedOfferNodeId = "marcus_wounded_001";

	public const string WoundedAcceptedNodeId = "marcus_wounded_002";

	public const string HealReturnNodeId = "marcus_heal_001";

	public const string HealTradeNodeId = "marcus_heal_trade";

	public const string HealThanksNodeId = "marcus_heal_002";

	private const int AreteLandingPlayfieldId = 6553;

	private const int HealthRegenStimItemId = 297044;

	private const int RechargerItemId = 291082;

	private const int RechargerQuantity = 50;

	private const int NanoRechargerItemId = 291043;

	private const int NanoRechargerQuantity = 25;

	private const int StimReturnXpReward = 1281;

	private const int StimReturnCreditReward = 1040;

	private const string StimReturnRewardFeedback = "~&!!!\":$'O\"ui!!!0'i!!!-5~";

	private const string StimGrantedFlag = "marcus-wounded-stim-297044";

	private const string StimReturnRewardsFlag = "marcus-wounded-rechargers";

	private const string WoundedDockworkerName = "Wounded Dockworker";

	private const string DockworkerThankYou = "Wounded Dockworker: Thank you for saving me.";

	private const int HealedRelapseSeconds = 60;

	private const int WoundedCurrentHealth = 12;

	private static readonly object HealRecoverySync = new object();

	private static readonly Dictionary<int, HealRecoveryState> HealRecoveries = new Dictionary<int, HealRecoveryState>();

	public static bool IsHealthRegenStim(IItem item)
	{
		return item != null && (item.LowID == 297044 || item.HighID == 297044);
	}

	public static bool IsStimReturnTip(ICharacter source)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (HasCompletedStimReturn(source))
		{
			return false;
		}
		if (RexMarcusChainCoordinator.GetPhase(source) == RexMarcusChainPhase.ReturnMarcusStim)
		{
			return true;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19A");
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	public static bool HasCompletedStimReturn(ICharacter source)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		if (mission != null && mission.State == MissionLifecycleState.Completed)
		{
			return true;
		}
		return MissionRuntime.Service.GetFlag(instance, "Mission:5514B19A", "marcus-wounded-rechargers") != null;
	}

	public static bool TryHandleDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
	{
		if (source == null || string.IsNullOrWhiteSpace(previousNodeId))
		{
			return false;
		}
		if (string.Equals(previousNodeId, "marcus_wounded_001", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			AcceptWoundedWorkersBranch(source);
			return true;
		}
		return false;
	}

	public static bool TryBeginStimReturnTrade(ICharacter source, Identity marcusIdentity)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		Identity val;
		if ((int)((Identity)(ref marcusIdentity)).Type != 50000 || ((Identity)(ref marcusIdentity)).Instance == 0)
		{
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = 2016273767;
			marcusIdentity = val;
		}
		RexMarcusChainCoordinator.BeginMarcusTradeSession(source, marcusIdentity, RexMarcusChainCoordinator.MarcusTradeKind.Stim);
		BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, marcusIdentity, "Drag and drop the item(s) you want to give to Marcus Stone into one of the slots available and press \"accept\"", 1);
		val = ((IEntity)source).Identity;
		Log("stim-return-trade-opened character=" + ((Identity)(ref val)).ToString(true) + " target=" + ((Identity)(ref marcusIdentity)).ToString(true));
		return true;
	}

	public static bool TryHandleStimUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Invalid comparison between Unknown and I4
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null || (int)message.Action != 3)
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val != null && ((IDynel)val).Controller is PlayerController && ((IInstancedEntity)val).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (((Identity)(ref identity)).Instance == 6553 && MissionRuntime.IsInitialized)
			{
				if ((int)((Identity)(ref target)).Type == 0)
				{
					return false;
				}
				IItem item = ResolveInventoryItem(val, target);
				if (!IsHealthRegenStim(item))
				{
					return false;
				}
				PersistentMissionService service = MissionRuntime.Service;
				identity = ((IEntity)val).Identity;
				ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B199");
				PersistentMissionService service2 = MissionRuntime.Service;
				identity = ((IEntity)val).Identity;
				ZoneEngine.Core.Missions.MissionStateRecord mission2 = service2.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19A");
				bool flag = mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered || mission.State == MissionLifecycleState.Completed);
				if (!flag && mission2 != null && (mission2.State == MissionLifecycleState.Active || mission2.State == MissionLifecycleState.Offered))
				{
					flag = true;
				}
				if (!flag)
				{
					return false;
				}
				ICharacter val2 = ResolveTargetedWoundedDockworker(val);
				if (val2 == null)
				{
					val2 = ResolveWoundedDockworkerIdentity(val, target);
				}
				if (val2 == null)
				{
					identity = ((IEntity)val).Identity;
					Log("stim-use rejected: no Wounded Dockworker selected character=" + ((Identity)(ref identity)).ToString(true));
					return false;
				}
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
				try
				{
					ApplyHealedDockworkerVisual(val2);
					CompleteStimUseHandoff(val);
					SendDockworkerThankYou(val);
				}
				catch (Exception ex)
				{
					Log("stim-use EXCEPTION: " + ex);
				}
				return true;
			}
		}
		return false;
	}

	public static void CompleteStimReturnTurnIn(ICharacter source, Identity marcusTarget, Identity stagedContainer, string trigger)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		Log("stim-return-turnin begin character=" + ((Identity)(ref identity)).ToString(true) + " trigger=" + trigger);
		TryConsumeStim(source, stagedContainer);
		try
		{
			BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, marcusTarget, (IEnumerable<Item>)(object)new Item[0], 0);
		}
		catch (Exception ex)
		{
			Log("stim-return rejecteditems failed: " + ex.Message);
		}
		ApplyStimReturnRewards(source);
		SendStimReturnRewardFeedback(source);
		TryGrantRechargerRewards(source);
		if (MissionRuntime.IsInitialized)
		{
			try
			{
				identity = ((IEntity)source).Identity;
				ForceCompleteMission(((Identity)(ref identity)).Instance, "Mission:5514B19A");
			}
			catch (Exception ex2)
			{
				Log("stim-return persistence failed: " + ex2.Message);
			}
		}
		SafeQuestFullUpdateSender.TrySendB19ACompletionCleanup(source);
		SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
		try
		{
			ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, marcusTarget);
		}
		catch (Exception ex3)
		{
			Log("stim-return resume-dialogue failed: " + ex3.Message);
		}
		SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
		identity = ((IEntity)source).Identity;
		Log("stim-return-turnin done character=" + ((Identity)(ref identity)).ToString(true) + " phaseNow=" + RexMarcusChainCoordinator.GetPhase(source));
	}

	private static void AcceptWoundedWorkersBranch(ICharacter source)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized)
		{
			Identity identity = ((IEntity)source).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			ClearPrematureB19A(source);
			try
			{
				MissionRuntime.Service.OfferMission(instance, "Mission:5514B199");
				MissionRuntime.Service.AcceptMission(instance, "Mission:5514B199");
			}
			catch (Exception ex)
			{
				Log("b199 offer/accept failed: " + ex.Message);
			}
			TryGrantHealthRegenStim(source);
			SafeQuestFullUpdateSender.TrySendB199Preview(source);
			SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
			identity = ((IEntity)source).Identity;
			Log("wounded-workers accepted (stacked beside Flint) character=" + ((Identity)(ref identity)).ToString(true));
		}
	}

	private static void ClearPrematureB19A(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		if (mission == null || (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Offered))
		{
			return;
		}
		try
		{
			MissionRuntime.Service.AbandonMission(instance, "Mission:5514B19A");
			identity = ((IEntity)source).Identity;
			Log("cleared premature B19A character=" + ((Identity)(ref identity)).ToString(true));
		}
		catch (Exception ex)
		{
			Log("clear premature B19A failed: " + ex.Message);
		}
	}

	private static void CompleteStimUseHandoff(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		try
		{
			ForceCompleteMission(instance, "Mission:5514B199");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B19A");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19A");
		}
		catch (Exception ex)
		{
			Log("stim-use persistence failed: " + ex.Message);
		}
		SafeQuestFullUpdateSender.TrySendB199ToB19AHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("stim-use handoff B199→B19A character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void TryGrantHealthRegenStim(ICharacter source)
	{
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 297044))
		{
			MissionRuntime.Service.SetFlag(instance, "Mission:5514B199", "marcus-wounded-stim-297044", "item:" + 297044);
			return;
		}
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(297044))
		{
			Log("stim grant skipped: inventory-or-itemloader missing item=" + 297044 + " inItemList=" + ItemLoader.ItemList.ContainsKey(297044));
			return;
		}
		try
		{
			if (ItemLoader.ItemList.TryGetValue(297044, out var value) && value != null)
			{
				int num = (value.Stats.ContainsKey(0) ? value.Stats[0] : 0);
				if (((uint)num & 0x8000000u) != 0)
				{
					value.Stats[0] = num & -134217729;
				}
			}
		}
		catch (Exception ex)
		{
			Log("stim unique-clear failed: " + ex.Message);
		}
		Item val;
		try
		{
			val = new Item(1, 297044, 297044);
			if (val.MultipleCount < 1)
			{
				val.MultipleCount = 1;
			}
		}
		catch (Exception ex2)
		{
			Log("stim create failed: " + ex2.Message);
			return;
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, val);
		if (questRewardInventoryGrantResult.Status != 0)
		{
			Log("stim grant failed status=" + questRewardInventoryGrantResult.Status);
			return;
		}
		try
		{
			SendOverflowTemplateAction(source, 297044, 1);
			BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(source, 110, 108871108);
		}
		catch (Exception ex3)
		{
			Log("stim notify failed: " + ex3.Message);
		}
		MissionRuntime.Service.SetFlag(instance, "Mission:5514B199", "marcus-wounded-stim-297044", "item:" + 297044);
	}

	private static void TryGrantRechargerRewards(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized)
		{
			Identity identity = ((IEntity)source).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B19A", "marcus-wounded-rechargers") == null)
			{
				GrantStackedRewardItem(source, 291082, 50);
				GrantStackedRewardItem(source, 291043, 25);
				MissionRuntime.Service.SetFlag(instance, "Mission:5514B19A", "marcus-wounded-rechargers", "items:" + 291082 + "x" + 50 + "+" + 291043 + "x" + 25);
			}
		}
	}

	private static void GrantStackedRewardItem(ICharacter source, int itemId, int quantity)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(itemId))
		{
			Log("reward grant skipped item=" + itemId);
			return;
		}
		Item val;
		try
		{
			val = new Item(1, itemId, itemId);
			val.MultipleCount = quantity;
		}
		catch (Exception ex)
		{
			Log("reward create failed item=" + itemId + " err=" + ex.Message);
			return;
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, val);
		if (questRewardInventoryGrantResult.Status != 0)
		{
			Log("reward grant failed item=" + itemId + " status=" + questRewardInventoryGrantResult.Status);
			return;
		}
		SendOverflowTemplateAction(source, itemId, quantity);
		BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(source, 110, 108871108);
	}

	private static void SendOverflowTemplateAction(ICharacter source, int itemId, int unknown1Quantity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Expected O, but got Unknown
		TemplateActionMessage val = new TemplateActionMessage
		{
			Identity = ((IEntity)source).Identity,
			Unknown = 0,
			ItemLowId = itemId,
			ItemHighId = itemId,
			Quality = 1,
			Unknown1 = unknown1Quantity,
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
	}

	private static void ApplyStimReturnRewards(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-marcus-stim-return-xp-credits";
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
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:5514B19A", definition, "capture:20260719-224226:marcus-b19a-xp-credits");
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

	private static void SendStimReturnRewardFeedback(ICharacter source)
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
				FormattedMessage = "~&!!!\":$'O\"ui!!!0'i!!!-5~",
				Unknown2 = 0
			});
		}
		catch (Exception ex)
		{
			Log("stim reward feedback failed: " + ex.Message);
		}
	}

	private static void TryConsumeStim(ICharacter source, Identity stagedContainer)
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
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
			if (IsHealthRegenStim(val))
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
				if (!IsHealthRegenStim(item.Value))
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
				value2.Add(item.Key, item.Value);
			}
		}
	}

	private static ICharacter ResolveTargetedWoundedDockworker(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Identity selected = ((ITargetingEntity)source).SelectedTarget;
		if ((int)((Identity)(ref selected)).Type == 0 || ((Identity)(ref selected)).Instance == 0)
		{
			selected = ((ITargetingEntity)source).FightingTarget;
		}
		return ResolveWoundedDockworkerIdentity(source, selected);
	}

	private static ICharacter ResolveWoundedDockworkerIdentity(ICharacter source, Identity selected)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref selected)).Type == 0 || ((Identity)(ref selected)).Instance == 0 || ((IInstancedEntity)source).Playfield == null)
		{
			return null;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, selected);
		if (@object == null || string.IsNullOrWhiteSpace(((INamedEntity)@object).Name))
		{
			return null;
		}
		return string.Equals(((INamedEntity)@object).Name.Trim(), "Wounded Dockworker", StringComparison.OrdinalIgnoreCase) ? @object : null;
	}

	private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected I4, but got Unknown
		if (character == null || ((IItemContainer)character).BaseInventory == null)
		{
			return null;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref itemIdentity)).Type, out var value) || value == null)
		{
			return null;
		}
		return value[((Identity)(ref itemIdentity)).Instance];
	}

	private static void SendDockworkerThankYou(ICharacter source)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return;
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new ChatTextMessage
			{
				Identity = ((IEntity)source).Identity,
				Text = "Wounded Dockworker: Thank you for saving me."
			});
		}
		catch (Exception ex)
		{
			Log("dockworker thank-you failed: " + ex.Message);
		}
	}

	public static void TickHealRecoveries(Playfield playfield)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Expected O, but got Unknown
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		if (playfield == null)
		{
			return;
		}
		Identity val = ((PooledObject)playfield).Identity;
		if (((Identity)(ref val)).Instance != 6553)
		{
			return;
		}
		List<int> list = null;
		DateTime utcNow = DateTime.UtcNow;
		lock (HealRecoverySync)
		{
			foreach (KeyValuePair<int, HealRecoveryState> healRecovery in HealRecoveries)
			{
				if (healRecovery.Value.RelapseUtc <= utcNow)
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(healRecovery.Key);
				}
				else
				{
					if (healRecovery.Value.WalkedHome)
					{
						continue;
					}
					Pool instance = Pool.Instance;
					Identity identity = ((PooledObject)playfield).Identity;
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)50000;
					((Identity)(ref val)).Instance = healRecovery.Key;
					ICharacter @object = instance.GetObject<ICharacter>(identity, val);
					if (@object != null && ((IDynel)@object).Controller is NPCController nPCController)
					{
						float num = ((IDynel)@object).RawCoordinates.X - healRecovery.Value.HomeX;
						float num2 = ((IDynel)@object).RawCoordinates.Z - healRecovery.Value.HomeZ;
						if (num * num + num2 * num2 < 0.25f)
						{
							healRecovery.Value.WalkedHome = true;
							continue;
						}
						nPCController.MoveTo(new Vector3
						{
							X = healRecovery.Value.HomeX,
							Y = healRecovery.Value.HomeY,
							Z = healRecovery.Value.HomeZ
						});
						healRecovery.Value.WalkedHome = true;
					}
				}
			}
		}
		if (list == null)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			int num3 = list[i];
			HealRecoveryState value;
			lock (HealRecoverySync)
			{
				if (!HealRecoveries.TryGetValue(num3, out value))
				{
					continue;
				}
				HealRecoveries.Remove(num3);
				goto IL_0270;
			}
			IL_0270:
			Pool instance2 = Pool.Instance;
			Identity identity2 = ((PooledObject)playfield).Identity;
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = num3;
			ICharacter object2 = instance2.GetObject<ICharacter>(identity2, val);
			if (object2 != null)
			{
				ApplyWoundedDockworkerRelapse(object2, value);
			}
		}
	}

	private static void ApplyHealedDockworkerVisual(ICharacter dockworker)
	{
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected O, but got Unknown
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected O, but got Unknown
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Expected O, but got Unknown
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Expected O, but got Unknown
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Expected O, but got Unknown
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Expected O, but got Unknown
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		if (dockworker != null && ((IInstancedEntity)dockworker).Playfield != null)
		{
			float x = ((IDynel)dockworker).RawCoordinates.X;
			float y = ((IDynel)dockworker).RawCoordinates.Y;
			float z = ((IDynel)dockworker).RawCoordinates.Z;
			AnnounceEmptySpellList(dockworker);
			AnnounceEmptySpellList(dockworker);
			Character val = (Character)(object)((dockworker is Character) ? dockworker : null);
			if (val != null)
			{
				val.UpdateMoveType((byte)37);
				val.MoveMode = (MoveModes)3;
			}
			((IStats)dockworker).Stats[(StatIds)173].Value = 3;
			((IStats)dockworker).Stats[(StatIds)173].BaseValue = 3u;
			((IStats)dockworker).Stats[(StatIds)174].Value = 3;
			((IStats)dockworker).Stats[(StatIds)174].BaseValue = 3u;
			((IInstancedEntity)dockworker).Playfield.Announce((MessageBody)new CharacterActionMessage
			{
				Identity = ((IEntity)dockworker).Identity,
				Unknown = 0,
				Action = (CharacterActionType)87,
				Unknown1 = 0,
				Target = Identity.None,
				Parameter1 = 0,
				Parameter2 = 0,
				Unknown2 = 0
			});
			((IInstancedEntity)dockworker).Playfield.Announce((MessageBody)new CharDCMoveMessage
			{
				Identity = ((IEntity)dockworker).Identity,
				Unknown = 0,
				MoveType = 37,
				Heading = new Quaternion
				{
					X = ((IDynel)dockworker).Heading.xf,
					Y = ((IDynel)dockworker).Heading.yf,
					Z = ((IDynel)dockworker).Heading.zf,
					W = ((IDynel)dockworker).Heading.wf
				},
				Coordinates = new Vector3
				{
					X = ((IDynel)dockworker).RawCoordinates.X,
					Y = ((IDynel)dockworker).RawCoordinates.Y,
					Z = ((IDynel)dockworker).RawCoordinates.Z
				},
				Unknown1 = 0,
				Unknown2 = 0f,
				Unknown3 = 0f
			});
			int num = ((IStats)dockworker).Stats[(StatIds)1].Value;
			if (num <= 0)
			{
				num = 32;
			}
			int value = ((IStats)dockworker).Stats[(StatIds)27].Value;
			int num2 = num - value;
			if (num2 <= 0)
			{
				num2 = 20;
			}
			((IStats)dockworker).Stats[(StatIds)27].Value = num;
			((IStats)dockworker).Stats[(StatIds)27].BaseValue = (uint)num;
			((IInstancedEntity)dockworker).Playfield.Announce((MessageBody)new HealthDamageMessage
			{
				Identity = ((IEntity)dockworker).Identity,
				Unknown = 0,
				Unknown1 = num2,
				Unknown2 = 0,
				Unknown3 = num,
				Unknown4 = 0,
				Target = ((IEntity)dockworker).Identity,
				Unknown5 = 0
			});
			AnnounceEmptySpellList(dockworker);
			((IDynel)dockworker).SendChangedStats();
			if (((IInstancedEntity)dockworker).Playfield is Playfield playfield)
			{
				playfield.AnnounceSpawnedCharacterVisibility(dockworker, Identity.None);
			}
			if (((IDynel)dockworker).Controller is NPCController nPCController)
			{
				nPCController.MoveTo(new Vector3
				{
					X = x,
					Y = y,
					Z = z
				});
			}
			Identity identity;
			lock (HealRecoverySync)
			{
				Dictionary<int, HealRecoveryState> healRecoveries = HealRecoveries;
				identity = ((IEntity)dockworker).Identity;
				healRecoveries[((Identity)(ref identity)).Instance] = new HealRecoveryState
				{
					HomeX = x,
					HomeY = y,
					HomeZ = z,
					RelapseUtc = DateTime.UtcNow.AddSeconds(60.0),
					WalkedHome = true
				};
			}
			string[] obj = new string[7] { "healed-visual StandUp+HP dockworker=", null, null, null, null, null, null };
			identity = ((IEntity)dockworker).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " hp=";
			obj[3] = num.ToString();
			obj[4] = " relapseSec=";
			obj[5] = 60.ToString();
			obj[6] = " source=20260720-064523";
			Log(string.Concat(obj));
		}
	}

	private static void ApplyWoundedDockworkerRelapse(ICharacter dockworker, HealRecoveryState state)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Expected O, but got Unknown
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Expected O, but got Unknown
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Expected O, but got Unknown
		//IL_029e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		if (dockworker != null && ((IInstancedEntity)dockworker).Playfield != null && state != null)
		{
			if (((IDynel)dockworker).Controller is NPCController nPCController)
			{
				nPCController.MoveTo(new Vector3
				{
					X = state.HomeX,
					Y = state.HomeY,
					Z = state.HomeZ
				});
			}
			((IDynel)dockworker).RawCoordinates = Vector3.op_Implicit(new Vector3((double)state.HomeX, (double)state.HomeY, (double)state.HomeZ));
			Character val = (Character)(object)((dockworker is Character) ? dockworker : null);
			if (val != null)
			{
				val.UpdateMoveType((byte)30);
				val.MoveMode = (MoveModes)8;
			}
			((IStats)dockworker).Stats[(StatIds)173].Value = 8;
			((IStats)dockworker).Stats[(StatIds)173].BaseValue = 8u;
			((IStats)dockworker).Stats[(StatIds)174].Value = 3;
			((IStats)dockworker).Stats[(StatIds)174].BaseValue = 3u;
			((IStats)dockworker).Stats[(StatIds)27].Value = 12;
			((IStats)dockworker).Stats[(StatIds)27].BaseValue = 12u;
			((IInstancedEntity)dockworker).Playfield.Announce((MessageBody)new CharacterActionMessage
			{
				Identity = ((IEntity)dockworker).Identity,
				Unknown = 0,
				Action = (CharacterActionType)263,
				Unknown1 = 0,
				Target = Identity.None,
				Parameter1 = 0,
				Parameter2 = 0,
				Unknown2 = 0
			});
			((IInstancedEntity)dockworker).Playfield.Announce((MessageBody)new CharDCMoveMessage
			{
				Identity = ((IEntity)dockworker).Identity,
				Unknown = 0,
				MoveType = 30,
				Heading = new Quaternion
				{
					X = ((IDynel)dockworker).Heading.xf,
					Y = ((IDynel)dockworker).Heading.yf,
					Z = ((IDynel)dockworker).Heading.zf,
					W = ((IDynel)dockworker).Heading.wf
				},
				Coordinates = new Vector3
				{
					X = state.HomeX,
					Y = state.HomeY,
					Z = state.HomeZ
				},
				Unknown1 = 0,
				Unknown2 = 0f,
				Unknown3 = 0f
			});
			((IDynel)dockworker).SendChangedStats();
			if (((IInstancedEntity)dockworker).Playfield is Playfield playfield)
			{
				playfield.AnnounceSpawnedCharacterVisibility(dockworker, Identity.None);
			}
			Identity identity = ((IEntity)dockworker).Identity;
			Log("healed-relapse Sit+HP12 dockworker=" + ((Identity)(ref identity)).ToString(true));
		}
	}

	private static void AnnounceEmptySpellList(ICharacter dockworker)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected O, but got Unknown
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			IPlayfield playfield = ((IInstancedEntity)dockworker).Playfield;
			SpellListMessage val = new SpellListMessage();
			((N3Message)val).Identity = ((IEntity)dockworker).Identity;
			val.Character = ((IEntity)dockworker).Identity;
			val.NanoEffects = (NanoEffect[])(object)new NanoEffect[0];
			playfield.Announce((MessageBody)(object)val);
		}
		catch (Exception ex)
		{
			Log("spelllist announce failed: " + ex.Message);
		}
	}

	private static void ForceCompleteMission(int characterId, string questId)
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
					ObservationKey = "marcus-wounded-workers",
					Amount = 1,
					EventType = "MarcusWoundedWorkers",
					SourceIdentity = string.Empty,
					TargetIdentity = string.Empty
				});
				MissionRuntime.Service.CompleteMission(characterId, questId);
			}
		}
	}

	private static void Log(string message)
	{
		LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_WOUNDED_WORKERS " + message);
	}
}
