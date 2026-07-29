using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
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

public static class SurveillanceUplinkQuestRuntime
{
	private sealed class BillTradeSession
	{
		public Identity NpcIdentity;

		public Identity StagedContainer;
	}

	public const string BillTradeOfferNodeId = "bill_105157_001";

	public const string BillTradeHoldNodeId = "bill_105157_trade";

	public const string BillKneecappingOfferNodeId = "bill_105157_002";

	public const int RebuiltHc12SecTecMonitorItemId = 295800;

	public const int RcPAudioRecordingDeviceItemId = 295801;

	private const int SurveillanceDroidInstance = 2028010634;

	private const int BillInstance = 2028010598;

	private const int PrizedHouseplantInstance = 1463912423;

	private const int PrizedHouseplantTemplateId = 295738;

	private const int AreteLandingPlayfieldId = 6553;

	private const int BillTurnInXpReward = 2229;

	private const int BillTurnInCreditReward = 1160;

	private const string UplinkFeedback = "~&!!!\":!!!)<s\u001dHC-12 SecTec: Camera feed activated.";

	private const string BillTurnInRewardFeedback = "~&!!!\":$'O\"ui!!!;4i!!!.X~";

	private const string DroidBeepChat = "Surveillance Droid: Beep Beep Beep!";

	private const string RcPGrantFlag = "rcp-audio-granted";

	private static readonly object TradeSyncRoot = new object();

	private static readonly Dictionary<int, BillTradeSession> TradeSessionsByCharacter = new Dictionary<int, BillTradeSession>();

	private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

	public static bool TryHandleSecTecUse(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null || (int)message.Action != 3)
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (!IsValidPlayerInArete(val) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		IItem item = ResolveInventoryItem(val, target);
		if (!IsSecTecMonitor(item))
		{
			return false;
		}
		ICharacter val2 = ResolveTargetedSurveillanceDroid(val);
		if (val2 == null || !IsCaptureSurveillanceDroid(val2))
		{
			return false;
		}
		if (!IsSelectedTarget(val, ((IEntity)val2).Identity))
		{
			return false;
		}
		bool flag = IsSurveillanceUplinkActive(val);
		bool flag2 = IsPlantBugActive(val);
		bool flag3 = HasRcPDevice(val);
		Identity identity;
		if (!flag && flag2 && !flag3)
		{
			BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
			try
			{
				SendUplinkFeedback(val);
				SendDroidBeep(val);
				if (!TryGrantRcPDevice(val))
				{
					identity = ((IEntity)val).Identity;
					Log("sectec-use recovery grant RC-P failed character=" + ((Identity)(ref identity)).ToString(true));
				}
			}
			catch (Exception ex)
			{
				Log("sectec-use recovery EXCEPTION: " + ex);
			}
			return true;
		}
		if (!flag)
		{
			return false;
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
		try
		{
			SendUplinkFeedback(val);
			SendDroidBeep(val);
			CompleteUplinkAndOfferPlantBug(val);
			if (!TryGrantRcPDevice(val))
			{
				identity = ((IEntity)val).Identity;
				Log("sectec-use grant RC-P failed character=" + ((Identity)(ref identity)).ToString(true));
			}
		}
		catch (Exception ex2)
		{
			Log("sectec-use EXCEPTION: " + ex2);
		}
		return true;
	}

	public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || message == null || message.Target == null || message.Target.Length < 2)
		{
			return false;
		}
		if (UseItemOnItemInteractionRules.ResolveRouteMode(message.Action) != UseItemOnItemInteractionRouteMode.UseItemOnItem)
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (!IsValidPlayerInArete(val) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity val2 = message.Target[0];
		IItem val3 = ResolveInventoryItem(val, val2);
		if (val3 == null || !IsRcPDevice(val3))
		{
			return false;
		}
		if (!IsPlantBugActive(val))
		{
			return false;
		}
		if (!IsPrizedHouseplantTarget(val, message.Target[1]))
		{
			return false;
		}
		BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.Acknowledge(val, message);
		TryConsumeInventoryItem(val, val2, 295801);
		CompletePlantBugAndOfferDeliverBill(val);
		return true;
	}

	public static bool TryBeginBillTrade(ICharacter source, Identity billIdentity)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		EnsureBillDeliverAvailable(source);
		Identity val;
		if ((int)((Identity)(ref billIdentity)).Type != 50000 || ((Identity)(ref billIdentity)).Instance == 0)
		{
			val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = 2028010598;
			billIdentity = val;
		}
		BeginBillTrade(source, billIdentity);
		BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, billIdentity, "Drag and drop the item(s) you want to give to ICC Immigration Officer Bill into one of the slots available and press \"accept\"", 1);
		val = ((IEntity)source).Identity;
		Log("bill-trade-opened character=" + ((Identity)(ref val)).ToString(true) + " hasHc12=" + HasSecTecMonitor(source));
		return true;
	}

	public static bool TryStageBillTradeItem(ICharacter source, KnuBotTradeMessage message)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || message == null || !IsBillNpc(source, message.Target))
		{
			return false;
		}
		if (!HasSecTecMonitor(source) && !IsBillDeliverActive(source) && GetTradeSession(source) == null)
		{
			return false;
		}
		EnsureBillTradeSession(source, message.Target);
		BillTradeSession tradeSession = GetTradeSession(source);
		if (tradeSession == null)
		{
			return true;
		}
		tradeSession.NpcIdentity = message.Target;
		Identity val = message.Container;
		if ((int)((Identity)(ref val)).Type != 0)
		{
			val = message.Container;
			if (((Identity)(ref val)).Instance > 0)
			{
				tradeSession.StagedContainer = message.Container;
				val = ((IEntity)source).Identity;
				string text = ((Identity)(ref val)).ToString(true);
				val = message.Container;
				Log("bill-trade-staged character=" + text + " container=" + ((Identity)(ref val)).ToString(true));
			}
		}
		return true;
	}

	public static bool ShouldSuppressGenericBillTradeRemove(ICharacter source, Identity target)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !IsBillNpc(source, target))
		{
			return false;
		}
		return HasSecTecMonitor(source) || IsBillDeliverActive(source) || GetTradeSession(source) != null;
	}

	public static bool TryFinishBillTrade(ICharacter source, KnuBotFinishTradeMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || message == null)
		{
			return false;
		}
		bool flag = IsBillNpc(source, message.Target);
		BillTradeSession tradeSession = GetTradeSession(source);
		bool flag2 = IsBillDeliverActive(source);
		bool flag3 = HasSecTecMonitor(source);
		if (!flag && tradeSession == null && !flag2 && !flag3)
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
			EnsureBillTradeSession(source, message.Target);
			tradeSession = GetTradeSession(source);
		}
		Identity stagedContainer = tradeSession?.StagedContainer ?? Identity.None;
		ApplyBillTradeTurnIn(source, message.Target, stagedContainer);
		return true;
	}

	private static void ApplyBillTradeTurnIn(ICharacter source, Identity billTarget, Identity stagedContainer)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Invalid comparison between Unknown and I4
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		Identity val = ((IEntity)source).Identity;
		int instance = ((Identity)(ref val)).Instance;
		lock (TradeSyncRoot)
		{
			if (!TurnInInFlightByCharacter.Add(instance))
			{
				return;
			}
		}
		try
		{
			if (!TryConsumeInventoryItem(source, stagedContainer, 295800))
			{
				string[] obj = new string[6] { "bill-turnin ABORTED — HC-12 not consumed character=", null, null, null, null, null };
				val = ((IEntity)source).Identity;
				obj[1] = ((Identity)(ref val)).ToString(true);
				obj[2] = " staged=";
				obj[3] = ((Identity)(ref stagedContainer)).ToString(true);
				obj[4] = " hasItem=";
				obj[5] = HasSecTecMonitor(source).ToString();
				Log(string.Concat(obj));
				Identity val2 = billTarget;
				if ((int)((Identity)(ref val2)).Type != 50000 || ((Identity)(ref val2)).Instance == 0)
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)50000;
					((Identity)(ref val)).Instance = 2028010598;
					val2 = val;
				}
				BeginBillTrade(source, val2);
				BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, val2, "Drag and drop the item(s) you want to give to ICC Immigration Officer Bill into one of the slots available and press \"accept\"", 1);
			}
			else
			{
				try
				{
					BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, billTarget, (IEnumerable<Item>)(object)new Item[0], 0);
				}
				catch (Exception ex)
				{
					Log("bill-rejecteditems failed: " + ex.Message);
				}
				ApplyBillTurnInXpCredits(source);
				TrySendBillTurnInRewardFeedback(source);
				CompleteDeliverBillAndClearTips(source);
				ForgetTradeSession(source);
				try
				{
					ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, billTarget);
				}
				catch (Exception ex2)
				{
					Log("bill-resume-dialogue failed: " + ex2.Message);
				}
				val = ((IEntity)source).Identity;
				Log("bill-turnin done character=" + ((Identity)(ref val)).ToString(true));
			}
		}
		finally
		{
			lock (TradeSyncRoot)
			{
				TurnInInFlightByCharacter.Remove(instance);
			}
		}
	}

	private static void CompleteUplinkAndOfferPlantBug(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19D", "Mission:5514B19E");
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B19E");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19E");
		}
		SafeQuestFullUpdateSender.TrySendUplinkToPlantBugHandoff(source);
		TryGrantRcPDevice(source);
	}

	private static void CompletePlantBugAndOfferDeliverBill(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19E", "Mission:5514B19F");
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B19F");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B19F");
		}
		SafeQuestFullUpdateSender.TrySendPlantBugToDeliverBillHandoff(source);
	}

	private static void CompleteDeliverBillAndClearTips(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19F", "Mission:5514B1A0");
		if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19F");
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B1A0");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B1A0");
		}
		MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
		MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
		SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
		identity = ((IEntity)source).Identity;
		Log("bill-deliver-to-kneecapping character=" + ((Identity)(ref identity)).ToString(true));
	}

	public static bool TryHandleBillDialogueAnswer(ICharacter source, string previousNodeId, int answerIndex)
	{
		if (source == null || answerIndex != 0)
		{
			return false;
		}
		bool flag = string.Equals(previousNodeId, "bill_105157_002", StringComparison.OrdinalIgnoreCase);
		bool flag2 = string.Equals(previousNodeId, "bill_105157_trade", StringComparison.OrdinalIgnoreCase);
		if (!flag && !flag2)
		{
			return false;
		}
		OfferKneecappingMission(source);
		return true;
	}

	private static void OfferKneecappingMission(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (HasSecTecMonitor(source) && !TryConsumeInventoryItem(source, Identity.None, 295800))
		{
			identity = ((IEntity)source).Identity;
			Log("bill-kneecapping consume-retry failed character=" + ((Identity)(ref identity)).ToString(true));
		}
		if (!IsKneecappingActive(source))
		{
			MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B19F", "Mission:5514B1A0");
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
			{
				MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19F");
				MissionRuntime.Service.OfferMission(instance, "Mission:5514B1A0");
				MissionRuntime.Service.AcceptMission(instance, "Mission:5514B1A0");
			}
		}
		MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19E");
		MissionRuntime.Service.CompleteMission(instance, "Mission:5514B19D");
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
		string[] obj = new string[6] { "bill-kneecapping-offered character=", null, null, null, null, null };
		identity = ((IEntity)source).Identity;
		obj[1] = ((Identity)(ref identity)).ToString(true);
		obj[2] = " tipEmitted=";
		obj[3] = (rexQuestPreviewEmissionResult?.Emitted ?? false).ToString();
		obj[4] = " tipMsg=";
		obj[5] = ((rexQuestPreviewEmissionResult == null || rexQuestPreviewEmissionResult.Message == null) ? "" : rexQuestPreviewEmissionResult.Message);
		Log(string.Concat(obj));
	}

	private static bool HasRcPDevice(ICharacter source)
	{
		return source != null && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 295801);
	}

	private static bool TryGrantRcPDevice(ICharacter source)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (HasRcPDevice(source))
		{
			return true;
		}
		EnsureRcPTemplateAllowsGrant();
		if (!GrantSingleRewardItem(source, 295801))
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		service.SetFlag(((Identity)(ref identity)).Instance, "Mission:5514B19D", "rcp-audio-granted", "item:" + 295801);
		identity = ((IEntity)source).Identity;
		Log("granted RC-P 295801 character=" + ((Identity)(ref identity)).ToString(true));
		return true;
	}

	private static void EnsureRcPTemplateAllowsGrant()
	{
		if (ItemLoader.ItemList.TryGetValue(295801, out var value) && value != null && value.Stats != null && value.Stats.ContainsKey(0))
		{
			int num = value.Stats[0];
			if (((uint)num & 0x8000000u) != 0)
			{
				value.Stats[0] = num & -134217729;
				Log("cleared Unique flag on template 295801 for quest handout");
			}
		}
	}

	private static bool GrantSingleRewardItem(ICharacter source, int itemId)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Expected O, but got Unknown
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Expected O, but got Unknown
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			Log("grant skipped item=" + itemId + " reason=no-inventory-or-client");
			return false;
		}
		if (!ItemLoader.ItemList.ContainsKey(itemId))
		{
			Log("grant skipped item=" + itemId + " reason=missing-ItemLoader-template");
			return false;
		}
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
		{
			return true;
		}
		Item item;
		try
		{
			item = new Item(1, itemId, itemId);
		}
		catch (Exception ex)
		{
			Log("grant create failed item=" + itemId + " err=" + ex.Message);
			return false;
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
		if (questRewardInventoryGrantResult.Status != 0)
		{
			string[] obj = new string[8]
			{
				"grant failed item=",
				itemId.ToString(),
				" status=",
				questRewardInventoryGrantResult.Status.ToString(),
				" invErr=",
				null,
				null,
				null
			};
			InventoryError inventoryError = questRewardInventoryGrantResult.InventoryError;
			obj[5] = ((object)(InventoryError)(ref inventoryError)).ToString();
			obj[6] = " ex=";
			obj[7] = questRewardInventoryGrantResult.ExceptionMessage;
			Log(string.Concat(obj));
			return false;
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
		return true;
	}

	private static void ApplyBillTurnInXpCredits(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-bill-hc12-turnin-xp-credits";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[4]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 61,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1160L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 52,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2229L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2229L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 2229L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:5514B19F", definition, "capture:20260720-105157:bill-turnin-xp-credits");
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

	private static void SendUplinkFeedback(ICharacter source)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
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
		if (obj != null)
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "~&!!!\":!!!)<s\u001dHC-12 SecTec: Camera feed activated.",
				Unknown2 = 0
			});
		}
	}

	private static void TrySendBillTurnInRewardFeedback(ICharacter source)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
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
		if (obj != null)
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "~&!!!\":$'O\"ui!!!;4i!!!.X~",
				Unknown2 = 0
			});
		}
	}

	private static void SendDroidBeep(ICharacter source)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
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
		if (obj != null)
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new ChatTextMessage
			{
				Identity = ((IEntity)source).Identity,
				Text = "Surveillance Droid: Beep Beep Beep!"
			});
		}
	}

	private static bool IsSurveillanceUplinkActive(ICharacter source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19D");
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	private static bool IsPlantBugActive(ICharacter source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19E");
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	private static bool IsBillDeliverActive(ICharacter source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19F");
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	private static void EnsureBillDeliverAvailable(ICharacter source)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized && !IsBillDeliverActive(source) && !IsBillDeliverCompleted(source))
		{
			PersistentMissionService service = MissionRuntime.Service;
			Identity identity = ((IEntity)source).Identity;
			service.OfferMission(((Identity)(ref identity)).Instance, "Mission:5514B19F");
			PersistentMissionService service2 = MissionRuntime.Service;
			identity = ((IEntity)source).Identity;
			service2.AcceptMission(((Identity)(ref identity)).Instance, "Mission:5514B19F");
			identity = ((IEntity)source).Identity;
			Log("bill-deliver-ensured-active character=" + ((Identity)(ref identity)).ToString(true));
		}
	}

	private static bool IsBillDeliverCompleted(ICharacter source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B19F");
		return mission != null && mission.State == MissionLifecycleState.Completed;
	}

	private static bool IsKneecappingActive(ICharacter source)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		ZoneEngine.Core.Missions.MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:5514B1A0");
		return mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Offered);
	}

	private static bool HasSecTecMonitor(ICharacter source)
	{
		return source != null && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 295800);
	}

	private static bool IsPrizedHouseplantTarget(ICharacter source, Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref target)).Type != 51005 || ((source != null) ? ((IInstancedEntity)source).Playfield : null) == null)
		{
			return false;
		}
		if (((Identity)(ref target)).Instance == 1463912423)
		{
			return true;
		}
		StaticDynel @object = Pool.Instance.GetObject<StaticDynel>(((IEntity)((IInstancedEntity)source).Playfield).Identity, target);
		if (@object == null)
		{
			return false;
		}
		if (@object.Template != null && @object.Template.ID == 295738)
		{
			return true;
		}
		if (@object.Stats != null && (@object.Stats.TryGetValue(702, out var value) || @object.Stats.TryGetValue(23, out value)))
		{
			return value == 295738;
		}
		return false;
	}

	private static ICharacter ResolveTargetedSurveillanceDroid(ICharacter source)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		Identity val = ((ITargetingEntity)source).SelectedTarget;
		if ((int)((Identity)(ref val)).Type == 0)
		{
			val = ((ITargetingEntity)source).FightingTarget;
		}
		if ((int)((Identity)(ref val)).Type != 50000 || ((Identity)(ref val)).Instance == 0)
		{
			return null;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, val);
		if (!IsCaptureSurveillanceDroid(@object))
		{
			return null;
		}
		return @object;
	}

	private static bool IsSelectedTarget(ICharacter source, Identity expected)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || (int)((Identity)(ref expected)).Type == 0)
		{
			return false;
		}
		Identity val = ((ITargetingEntity)source).SelectedTarget;
		if ((int)((Identity)(ref val)).Type == 0)
		{
			val = ((ITargetingEntity)source).FightingTarget;
		}
		return ((Identity)(ref val)).Type == ((Identity)(ref expected)).Type && ((Identity)(ref val)).Instance == ((Identity)(ref expected)).Instance;
	}

	private static bool IsCaptureSurveillanceDroid(ICharacter npc)
	{
		return npc != null && ((IStats)npc).Stats[(StatIds)27].Value > 0 && IsSurveillanceDroidName(((INamedEntity)npc).Name) && ((IStats)npc).Stats[(StatIds)359].Value == 210238;
	}

	private static bool IsBillNpc(ICharacter source, Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref target)).Type == 50000 && ((Identity)(ref target)).Instance == 2028010598)
		{
			return true;
		}
		if (((source != null) ? ((IInstancedEntity)source).Playfield : null) == null || ((Identity)(ref target)).Instance == 0)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, target);
		if (@object != null && string.Equals(((INamedEntity)@object).Name, "ICC Immigration Officer Bill", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity))
		{
			if (item != null)
			{
				Identity identity = ((IEntity)item).Identity;
				if (((Identity)(ref identity)).Instance == ((Identity)(ref target)).Instance && string.Equals(((INamedEntity)item).Name, "ICC Immigration Officer Bill", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsSurveillanceDroidName(string name)
	{
		return string.Equals(name, "Surveillance Droid", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSecTecMonitor(IItem item)
	{
		return item != null && (item.LowID == 295800 || item.HighID == 295800);
	}

	private static bool IsRcPDevice(IItem item)
	{
		return item != null && (item.LowID == 295801 || item.HighID == 295801);
	}

	private static IItem ResolveInventoryItem(ICharacter character, Identity itemIdentity)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected I4, but got Unknown
		if (character == null || (int)((Identity)(ref itemIdentity)).Type != 104)
		{
			return null;
		}
		if (((IItemContainer)character).BaseInventory == null)
		{
			return null;
		}
		if (!((IItemContainer)character).BaseInventory.Pages.TryGetValue((int)((Identity)(ref itemIdentity)).Type, out var value) || value == null)
		{
			return null;
		}
		return value[((Identity)(ref itemIdentity)).Instance];
	}

	private static bool TryConsumeInventoryItem(ICharacter source, Identity stagedContainer, int itemId)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected I4, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected I4, but got Unknown
		if (source == null || ((IItemContainer)source).BaseInventory == null || itemId <= 0)
		{
			return false;
		}
		if ((int)((Identity)(ref stagedContainer)).Type != 0 && ((Identity)(ref stagedContainer)).Instance > 0 && ((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref stagedContainer)).Type, out var value) && value != null)
		{
			IItem val = value[((Identity)(ref stagedContainer)).Instance];
			if (val != null && (val.LowID == itemId || val.HighID == itemId) && TryRemoveInventorySlot(source, value, (int)((Identity)(ref stagedContainer)).Type, ((Identity)(ref stagedContainer)).Instance, val))
			{
				return true;
			}
		}
		int[] array = new int[2] { 104, 110 };
		for (int i = 0; i < array.Length; i++)
		{
			if (((IItemContainer)source).BaseInventory.Pages.TryGetValue(array[i], out var value2) && value2 != null && TryConsumeFromPage(source, array[i], value2, itemId))
			{
				return true;
			}
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)source).BaseInventory.Pages)
		{
			if (page.Key == 104 || page.Key == 110 || !TryConsumeFromPage(source, page.Key, page.Value, itemId))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	private static bool TryConsumeFromPage(ICharacter source, int pageType, IInventoryPage page, int itemId)
	{
		if (page == null)
		{
			return false;
		}
		foreach (KeyValuePair<int, IItem> item in page.List())
		{
			IItem value = item.Value;
			if (value != null && (value.LowID == itemId || value.HighID == itemId) && TryRemoveInventorySlot(source, page, pageType, item.Key, value))
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryRemoveInventorySlot(ICharacter source, IInventoryPage page, int pageType, int slot, IItem item)
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		page.Remove(slot);
		try
		{
			if (((IItemContainer)source).BaseInventory.Write())
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, pageType, slot);
				string[] obj = new string[6]
				{
					"bill-consumed item slot page=",
					pageType.ToString("X"),
					" slot=",
					slot.ToString(),
					" character=",
					null
				};
				Identity identity = ((IEntity)source).Identity;
				obj[5] = ((Identity)(ref identity)).ToString(true);
				Log(string.Concat(obj));
				return true;
			}
		}
		catch (Exception ex)
		{
			Log("bill-consume write failed: " + ex.Message);
		}
		page.Add(slot, item);
		return false;
	}

	private static bool IsValidPlayerInArete(ICharacter source)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (source != null && ((IDynel)source).Controller is PlayerController && ((IInstancedEntity)source).Playfield != null)
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

	private static void BeginBillTrade(ICharacter source, Identity billIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		EnsureBillTradeSession(source, billIdentity);
	}

	private static void EnsureBillTradeSession(ICharacter source, Identity billIdentity)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		lock (TradeSyncRoot)
		{
			Dictionary<int, BillTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			if (tradeSessionsByCharacter.TryGetValue(((Identity)(ref identity)).Instance, out var value) && value != null)
			{
				value.NpcIdentity = billIdentity;
				return;
			}
			Dictionary<int, BillTradeSession> tradeSessionsByCharacter2 = TradeSessionsByCharacter;
			identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter2[((Identity)(ref identity)).Instance] = new BillTradeSession
			{
				NpcIdentity = billIdentity
			};
		}
	}

	private static BillTradeSession GetTradeSession(ICharacter source)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		lock (TradeSyncRoot)
		{
			Dictionary<int, BillTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
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
			Dictionary<int, BillTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter.Remove(((Identity)(ref identity)).Instance);
		}
	}

	private static void Log(string message)
	{
		LogUtil.Debug((DebugInfoDetail)512, "ARETE_SURVEILLANCE_UPLINK " + message);
	}
}
