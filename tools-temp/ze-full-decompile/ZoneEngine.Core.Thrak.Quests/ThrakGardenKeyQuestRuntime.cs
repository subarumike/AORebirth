using System;
using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core.Thrak.Quests;

internal static class ThrakGardenKeyQuestRuntime
{
	internal static bool IsMissionActive(ICharacter source, string questId)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, questId);
		return mission != null && mission.State == MissionLifecycleState.Active;
	}

	internal static bool IsMissionCompleted(ICharacter source, string questId)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, questId);
		return mission != null && mission.State == MissionLifecycleState.Completed;
	}

	internal static bool HasProphetDeviceInspected(ICharacter source)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		int result;
		if (service.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556893A", "thrak-prophet-device-inspected") == null)
		{
			PersistentMissionService service2 = MissionRuntime.Service;
			identity = ((IEntity)source).Identity;
			result = ((service2.GetFlag(((Identity)(ref identity)).Instance, "Mission:55563C16", "thrak-prophet-device-inspected") != null) ? 1 : 0);
		}
		else
		{
			result = 1;
		}
		return (byte)result != 0;
	}

	internal static void MarkProphetDeviceInspected(ICharacter source)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized)
		{
			Identity identity = ((IEntity)source).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			string questId = (IsMissionActive(source, "Mission:55563C16") ? "Mission:55563C16" : "Mission:5556893A");
			MissionRuntime.Service.SetFlag(instance, questId, "thrak-prophet-device-inspected", "1");
		}
	}

	internal static MissionOperationResult AcceptQuest(ICharacter source, string questId)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return new MissionOperationResult
			{
				Status = MissionOperationStatus.Unresolved,
				Message = "thrak-quest-runtime-unavailable"
			};
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (IsMissionActive(source, questId))
		{
			if (string.Equals(questId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase))
			{
				ApplyInsigniaCommitmentHandoff(source);
			}
			else if (string.Equals(questId, "Mission:55563C18", StringComparison.OrdinalIgnoreCase))
			{
				ApplyGardenHandoff(source);
			}
			else
			{
				ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, questId);
			}
			return new MissionOperationResult
			{
				Status = MissionOperationStatus.AlreadyApplied,
				Message = "thrak-quest-already-active"
			};
		}
		MissionOperationResult result = MissionRuntime.Service.OfferMission(instance, questId);
		if (!IsClientEmitSuccess(result) && IsPersistenceFailure(result))
		{
			return result;
		}
		MissionOperationResult result2 = MissionRuntime.Service.AcceptMission(instance, questId);
		if (IsClientEmitSuccess(result2))
		{
			if (string.Equals(questId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase))
			{
				ApplyInsigniaCommitmentHandoff(source);
			}
			else if (string.Equals(questId, "Mission:55563C18", StringComparison.OrdinalIgnoreCase))
			{
				ApplyGardenHandoff(source);
			}
			else
			{
				ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, questId);
			}
		}
		return result2;
	}

	internal static void ApplyInsigniaCommitmentHandoff(ICharacter source)
	{
		if (source != null && MissionRuntime.IsInitialized)
		{
			ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C16");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893A");
			ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C17");
		}
	}

	private static void ApplyGardenHandoff(ICharacter source)
	{
		if (source != null && MissionRuntime.IsInitialized)
		{
			ForceCloseQuest(source, "Mission:5556893A", "mission_5556893A_find", "garden-handoff");
			ForceCloseQuest(source, "Mission:55563C16", "mission_55563C16_insignia", "garden-handoff");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C17");
			ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C18");
		}
	}

	internal static void TryAdvanceToGardenOnStatueEntry(ICharacter source)
	{
		if (source != null && MissionRuntime.IsInitialized && !IsMissionActive(source, "Mission:55563C18") && !IsMissionCompleted(source, "Mission:55563C18") && (IsMissionActive(source, "Mission:55563C16") || IsMissionCompleted(source, "Mission:55563C16")))
		{
			AcceptQuest(source, "Mission:55563C18");
		}
	}

	internal static bool TryResendActiveMissionsForLogin(ICharacter source)
	{
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (HasCompletedGardenKeyQuest(source))
		{
			ClearFinishedThrakChainJournal(source);
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		IList<MissionStateRecord> missions = service.GetMissions(((Identity)(ref identity)).Instance);
		if (missions == null || missions.Count == 0)
		{
			return false;
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		bool flag5 = false;
		bool flag6 = false;
		for (int i = 0; i < missions.Count; i++)
		{
			MissionStateRecord missionStateRecord = missions[i];
			if (missionStateRecord != null && missionStateRecord.State == MissionLifecycleState.Active)
			{
				if (string.Equals(missionStateRecord.QuestId, "Mission:5556893A", StringComparison.OrdinalIgnoreCase))
				{
					flag3 = true;
				}
				else if (string.Equals(missionStateRecord.QuestId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase))
				{
					flag2 = true;
				}
				else if (string.Equals(missionStateRecord.QuestId, "Mission:55563C18", StringComparison.OrdinalIgnoreCase))
				{
					flag4 = true;
				}
				else if (string.Equals(missionStateRecord.QuestId, "Mission:5556591A", StringComparison.OrdinalIgnoreCase))
				{
					flag5 = true;
				}
				else if (string.Equals(missionStateRecord.QuestId, "Mission:5556893D", StringComparison.OrdinalIgnoreCase))
				{
					flag6 = true;
				}
			}
		}
		if (flag6)
		{
			ClearEarlierThrakJournalBefore(source, "Mission:5556893D");
			flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:5556893D");
		}
		else if (flag5)
		{
			ClearEarlierThrakJournalBefore(source, "Mission:5556591A");
			int soulCount = GetSoulCount(source);
			string questId = "Mission:5556591A";
			if (soulCount >= 2)
			{
				questId = "Mission:5556893C";
			}
			else if (soulCount >= 1)
			{
				questId = "Mission:5556893B";
			}
			flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, questId);
		}
		else if (flag4)
		{
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893A");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C17");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C16");
			flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C18");
		}
		else if (flag2)
		{
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893A");
			flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C16");
			if (flag3)
			{
				flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:55563C17");
			}
		}
		else if (flag3)
		{
			flag |= ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:5556893A");
		}
		return flag;
	}

	internal static void ClearFinishedThrakChainJournal(ICharacter source)
	{
		if (source != null && MissionRuntime.IsInitialized)
		{
			ForceCloseQuest(source, "Mission:5556893A", "mission_5556893A_find", "finished-chain-cleanup");
			ForceCloseQuest(source, "Mission:55563C16", "mission_55563C16_insignia", "finished-chain-cleanup");
			ForceCloseQuest(source, "Mission:55563C18", "mission_55563C18_garden", "finished-chain-cleanup");
			ForceCloseQuest(source, "Mission:5556591A", "mission_5556591A_souls", "finished-chain-cleanup");
			ForceCloseQuest(source, "Mission:5556893D", "mission_5556893D_return", "finished-chain-cleanup");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C17");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893B");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893C");
		}
	}

	private static void ClearEarlierThrakJournalBefore(ICharacter source, string keepQuestId)
	{
		if (source != null)
		{
			if (!string.Equals(keepQuestId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase) && !string.Equals(keepQuestId, "Mission:5556893A", StringComparison.OrdinalIgnoreCase))
			{
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C17");
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893A");
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C16");
			}
			if (string.Equals(keepQuestId, "Mission:5556893D", StringComparison.OrdinalIgnoreCase) || string.Equals(keepQuestId, "Mission:5556591A", StringComparison.OrdinalIgnoreCase))
			{
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C18");
			}
			if (string.Equals(keepQuestId, "Mission:5556893D", StringComparison.OrdinalIgnoreCase))
			{
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556591A");
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893B");
				ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893C");
			}
		}
	}

	internal static MissionOperationResult CompleteQuest(ICharacter source, string questId)
	{
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return new MissionOperationResult
			{
				Status = MissionOperationStatus.Unresolved,
				Message = "thrak-quest-runtime-unavailable"
			};
		}
		string objectiveId = ResolveObjectiveId(questId);
		MissionOperationResult result = ForceCloseQuest(source, questId, objectiveId, "complete-quest");
		if (string.Equals(questId, "Mission:55563C18", StringComparison.OrdinalIgnoreCase) || string.Equals(questId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase))
		{
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:55563C17");
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893A");
		}
		if (string.Equals(questId, "Mission:5556893D", StringComparison.OrdinalIgnoreCase))
		{
			ClearFinishedThrakChainJournal(source);
		}
		return result;
	}

	private static MissionOperationResult ForceCloseQuest(ICharacter source, string questId, string objectiveId, string reason)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || string.IsNullOrWhiteSpace(questId) || !MissionRuntime.IsInitialized)
		{
			return new MissionOperationResult
			{
				Status = MissionOperationStatus.Unresolved,
				Message = "thrak-force-close-unavailable"
			};
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (IsMissionActive(source, questId) && !string.IsNullOrWhiteSpace(objectiveId))
		{
			PersistentMissionService service = MissionRuntime.Service;
			MissionObjectiveObservation obj = new MissionObjectiveObservation
			{
				CharacterId = instance,
				QuestId = questId,
				ObjectiveId = objectiveId,
				ObservationKey = "thrak:" + reason + ":" + questId,
				Amount = 1,
				EventType = "ThrakGardenKey:ForceClose"
			};
			identity = ((IEntity)source).Identity;
			obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			obj.TargetIdentity = questId;
			service.ObserveObjective(obj);
		}
		MissionOperationResult result = MissionRuntime.Service.CompleteMission(instance, questId);
		if (IsPersistenceFailure(result) && IsMissionActive(source, questId))
		{
			result = MissionRuntime.Service.AbandonMission(instance, questId);
		}
		ThrakGardenKeyPacketSender.TrySendQuestDelete(source, questId);
		return result;
	}

	private static string ResolveObjectiveId(string questId)
	{
		if (string.Equals(questId, "Mission:5556893A", StringComparison.OrdinalIgnoreCase))
		{
			return "mission_5556893A_find";
		}
		if (string.Equals(questId, "Mission:55563C16", StringComparison.OrdinalIgnoreCase))
		{
			return "mission_55563C16_insignia";
		}
		if (string.Equals(questId, "Mission:55563C18", StringComparison.OrdinalIgnoreCase))
		{
			return "mission_55563C18_garden";
		}
		if (string.Equals(questId, "Mission:5556591A", StringComparison.OrdinalIgnoreCase))
		{
			return "mission_5556591A_souls";
		}
		return null;
	}

	private static bool IsClientEmitSuccess(MissionOperationResult result)
	{
		return result != null && (result.Status == MissionOperationStatus.Applied || result.Status == MissionOperationStatus.AlreadyApplied);
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result != null && result.Status != MissionOperationStatus.Applied && result.Status != MissionOperationStatus.AlreadyApplied && result.Status != MissionOperationStatus.Unresolved;
	}

	internal static bool TryGrantAnalyzer(ICharacter source)
	{
		return TryGrantItem(source, "Mission:5556893A", 214998, "thrak-analyzer-granted");
	}

	internal static bool TryGrantInsignia(ICharacter source)
	{
		return TryGrantItem(source, "Mission:5556591A", 214789, "thrak-insignia-granted");
	}

	internal static bool TryGrantInspectedAnalyzer(ICharacter source)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		if (HasInspectedAnalyzer(source) || HasFavoredAnalyzer(source))
		{
			return true;
		}
		TryConsumeCarriedItem(source, 214998);
		if (TryRestoreItem(source, 214783, 1))
		{
			if (MissionRuntime.IsInitialized)
			{
				PersistentMissionService service = MissionRuntime.Service;
				Identity identity = ((IEntity)source).Identity;
				service.SetFlag(((Identity)(ref identity)).Instance, "Mission:5556591A", "thrak-inspected-analyzer-granted", "item:" + 214783);
			}
			return true;
		}
		return TryRestoreItem(source, 214998, 1);
	}

	internal static bool TryGrantFavoredAnalyzer(ICharacter source)
	{
		if (HasFavoredAnalyzer(source))
		{
			return true;
		}
		return TryRestoreItem(source, 214785, 1);
	}

	internal static bool TryGrantGardenKey(ICharacter source)
	{
		if (!TryGrantItem(source, "Mission:5556893D", 226994, "thrak-key-granted") && !TryGrantItem(source, "Mission:5556591A", 226994, "thrak-key-granted"))
		{
			return false;
		}
		SetAccountKeyFlag(source);
		return true;
	}

	internal static bool HasGardenKey(ICharacter source)
	{
		if (source == null)
		{
			return false;
		}
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 226994))
		{
			return true;
		}
		if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue(101, out var value) || value == null)
		{
			return false;
		}
		for (int i = value.FirstSlotNumber; i < value.FirstSlotNumber + value.MaxSlots; i++)
		{
			IItem val = value[i];
			if (val != null && ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val.LowID, val.HighID))
			{
				return true;
			}
		}
		return false;
	}

	internal static bool TryRestoreGardenKeyIfMissing(ICharacter source)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (HasGardenKey(source))
		{
			return true;
		}
		if (!HasAccountGardenKeyFlag(source))
		{
			PersistentMissionService service = MissionRuntime.Service;
			Identity identity = ((IEntity)source).Identity;
			if (service.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556893D", "thrak-key-granted") == null)
			{
				PersistentMissionService service2 = MissionRuntime.Service;
				identity = ((IEntity)source).Identity;
				if (service2.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556591A", "thrak-key-granted") == null)
				{
					return false;
				}
			}
		}
		return TryRestoreItem(source, 226994, 1);
	}

	internal static bool TryMoveSacredGardenKeyFromHudToInventory(ICharacter source)
	{
		if (source == null || ((IItemContainer)source).BaseInventory == null)
		{
			return false;
		}
		if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue(101, out var value) || value == null)
		{
			return false;
		}
		if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue(104, out var value2) || value2 == null)
		{
			return false;
		}
		bool flag = false;
		for (int i = value.FirstSlotNumber; i < value.FirstSlotNumber + value.MaxSlots; i++)
		{
			IItem val = value[i];
			if (val == null || !ThrakGardenKeyInteractionRules.IsSacredGardenKeyItem(val.LowID, val.HighID))
			{
				continue;
			}
			int num = value2.FindFreeSlot();
			if (num < 0)
			{
				break;
			}
			try
			{
				IItemSlotHandler val2 = (IItemSlotHandler)(object)((value is IItemSlotHandler) ? value : null);
				if (val2 == null)
				{
					break;
				}
				val2.Unequip(i, value2, num);
				if (((IDynel)source).Controller != null && ((IDynel)source).Controller.Client != null)
				{
					UnEquip.Send(((IDynel)source).Controller.Client, value, i);
				}
				flag = true;
				continue;
			}
			catch (Exception)
			{
				continue;
			}
		}
		if (flag)
		{
			try
			{
				((IItemContainer)source).BaseInventory.Write();
			}
			catch (Exception)
			{
			}
		}
		return flag;
	}

	internal static bool TryForceReturnGardenKey(ICharacter source)
	{
		if (source == null)
		{
			return false;
		}
		if (HasGardenKey(source))
		{
			return true;
		}
		return TryRestoreItem(source, 226994, 1);
	}

	internal static bool HasCompletedGardenKeyQuest(ICharacter source)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		if (HasGardenKey(source) || HasAccountGardenKeyFlag(source))
		{
			return true;
		}
		if (!MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		int result;
		if (service.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556893D", "thrak-key-granted") == null)
		{
			PersistentMissionService service2 = MissionRuntime.Service;
			identity = ((IEntity)source).Identity;
			result = ((service2.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556591A", "thrak-key-granted") != null) ? 1 : 0);
		}
		else
		{
			result = 1;
		}
		return (byte)result != 0;
	}

	private static bool HasAccountGardenKeyFlag(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		string text = MissionRuntime.ResolveAccountKey(instance);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "character:" + instance.ToString(CultureInfo.InvariantCulture);
		}
		MissionAccountFlagRecord accountFlag = MissionRuntime.Service.GetAccountFlag(text, "thrak-garden-key");
		return accountFlag != null;
	}

	internal static int GetSoulCount(ICharacter source)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return 0;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionFlagRecord flag = service.GetFlag(((Identity)(ref identity)).Instance, "Mission:5556591A", "thrak-soul-count");
		if (flag == null || string.IsNullOrWhiteSpace(flag.Value))
		{
			return 0;
		}
		int result;
		return int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? Math.Max(0, result) : 0;
	}

	internal static int IncrementSoulCount(ICharacter source)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return 0;
		}
		int soulCount = GetSoulCount(source);
		if (soulCount >= 3)
		{
			return 3;
		}
		int num = soulCount + 1;
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		service.SetFlag(((Identity)(ref identity)).Instance, "Mission:5556591A", "thrak-soul-count", num.ToString(CultureInfo.InvariantCulture));
		PersistentMissionService service2 = MissionRuntime.Service;
		MissionObjectiveObservation missionObjectiveObservation = new MissionObjectiveObservation();
		identity = ((IEntity)source).Identity;
		missionObjectiveObservation.CharacterId = ((Identity)(ref identity)).Instance;
		missionObjectiveObservation.QuestId = "Mission:5556591A";
		missionObjectiveObservation.ObjectiveId = "mission_5556591A_souls";
		missionObjectiveObservation.ObservationKey = "cursed-silvertail-soul:" + num.ToString(CultureInfo.InvariantCulture);
		missionObjectiveObservation.Amount = 1;
		missionObjectiveObservation.EventType = "ThrakGardenKey:SoulClaimed";
		identity = ((IEntity)source).Identity;
		missionObjectiveObservation.SourceIdentity = ((Identity)(ref identity)).ToString(true);
		missionObjectiveObservation.TargetIdentity = "Cursed Silvertail";
		service2.ObserveObjective(missionObjectiveObservation);
		switch (num)
		{
		case 1:
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556591A");
			ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:5556893B");
			break;
		case 2:
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893B");
			ThrakGardenKeyPacketSender.TrySendQuestFullUpdate(source, "Mission:5556893C");
			break;
		default:
		{
			ThrakGardenKeyPacketSender.TrySendQuestDelete(source, "Mission:5556893C");
			PersistentMissionService service3 = MissionRuntime.Service;
			identity = ((IEntity)source).Identity;
			service3.CompleteMission(((Identity)(ref identity)).Instance, "Mission:5556591A");
			AcceptQuest(source, "Mission:5556893D");
			break;
		}
		}
		return num;
	}

	internal static bool TryForceReturnFavoredAnalyzer(ICharacter source)
	{
		if (source == null)
		{
			return false;
		}
		TryConsumeCarriedItem(source, 214785);
		return TryRestoreItem(source, 214785, 1);
	}

	internal static bool HasFavoredAnalyzer(ICharacter source)
	{
		return source != null && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 214785);
	}

	internal static bool HasInspectedAnalyzer(ICharacter source)
	{
		return source != null && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 214783);
	}

	internal static bool HasAnalyzer(ICharacter source)
	{
		return source != null && (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 214998) || HasInspectedAnalyzer(source) || HasFavoredAnalyzer(source));
	}

	internal static bool HasInsignia(ICharacter source)
	{
		return source != null && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 214789);
	}

	internal static bool TryForceReturnAncientDevice(ICharacter source)
	{
		if (source == null)
		{
			return false;
		}
		if (HasFavoredAnalyzer(source))
		{
			return true;
		}
		TryConsumeCarriedItem(source, 214783);
		TryConsumeCarriedItem(source, 214998);
		return TryRestoreItem(source, 214998, 1);
	}

	internal static bool TryRestoreAncientDeviceIfMissing(ICharacter source)
	{
		if (source == null)
		{
			return false;
		}
		if (HasFavoredAnalyzer(source) || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 214998))
		{
			return true;
		}
		return TryForceReturnAncientDevice(source);
	}

	internal static bool TryRestoreAnalyzerIfMissing(ICharacter source)
	{
		return TryRestoreAncientDeviceIfMissing(source);
	}

	internal static bool TryRestoreItem(ICharacter source, int itemId, int quality)
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		if (source == null || itemId <= 0 || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
		{
			return true;
		}
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(itemId))
		{
			return false;
		}
		Item item;
		try
		{
			item = new Item((quality <= 0) ? 1 : quality, itemId, itemId);
		}
		catch (Exception)
		{
			return false;
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
		if (questRewardInventoryGrantResult.Status != 0)
		{
			return false;
		}
		SendItemNotifications(source, item);
		return true;
	}

	private static void TryConsumeCarriedItem(ICharacter source, int itemId)
	{
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || itemId <= 0 || ((IItemContainer)source).BaseInventory == null)
		{
			return;
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)source).BaseInventory.Pages)
		{
			IInventoryPage value = page.Value;
			if (value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item in value.List())
			{
				IItem value2 = item.Value;
				if (value2 == null || (value2.LowID != itemId && value2.HighID != itemId))
				{
					continue;
				}
				value.Remove(item.Key);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						try
						{
							BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, page.Key, item.Key);
							return;
						}
						catch (Exception)
						{
							return;
						}
					}
					value.Add(item.Key, value2);
					return;
				}
				catch (Exception)
				{
					try
					{
						value.Add(item.Key, value2);
						return;
					}
					catch (Exception)
					{
						return;
					}
				}
			}
		}
	}

	private static void SetAccountKeyFlag(ICharacter source)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (source != null && MissionRuntime.IsInitialized)
		{
			Identity identity = ((IEntity)source).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			string text = MissionRuntime.ResolveAccountKey(instance);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "character:" + instance.ToString(CultureInfo.InvariantCulture);
			}
			MissionRuntime.Service.SetAccountFlag(instance, text, "Mission:5556893D", "thrak-garden-key", "item:" + 226994);
		}
	}

	private static bool TryGrantItem(ICharacter source, string questId, int itemId, string flagKey)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, questId, flagKey) != null)
		{
			return true;
		}
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(itemId))
		{
			return false;
		}
		if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
		{
			Item item;
			try
			{
				item = new Item(1, itemId, itemId);
			}
			catch (Exception)
			{
				return false;
			}
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
			if (questRewardInventoryGrantResult.Status != 0)
			{
				return false;
			}
			SendItemNotifications(source, item);
		}
		MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(instance, questId, flagKey, "item:" + itemId);
		return missionOperationResult.Status == MissionOperationStatus.Applied || missionOperationResult.Status == MissionOperationStatus.AlreadyApplied;
	}

	private static void SendItemNotifications(ICharacter source, Item item)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Expected O, but got Unknown
		TemplateActionMessage val = new TemplateActionMessage
		{
			Identity = ((IEntity)source).Identity,
			Unknown = 0,
			ItemLowId = item.LowID,
			ItemHighId = item.HighID,
			Quality = item.Quality,
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
	}
}
