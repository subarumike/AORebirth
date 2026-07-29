using System;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class MarcusB18FCompletionHandler
{
	private sealed class AlreadyPresentFireSuppressantRewardEffect : IMissionRewardEffect
	{
		public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
		{
			return MissionRewardEffectResult.AlreadyApplied("inventory-item:296780:character:" + context.CharacterId.ToString(CultureInfo.InvariantCulture));
		}
	}

	private sealed class CompactFireSuppressantRewardEffect : IMissionRewardEffect
	{
		private readonly ICharacter source;

		public CompactFireSuppressantRewardEffect(ICharacter source)
		{
			this.source = source;
		}

		public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
		{
			int num = InventoryContainerRuntimeService.Default.CountCharacterItemInCarriedInventory(source, 296780);
			MissionFlagRecord flag = MissionRuntime.Service.GetFlag(context.CharacterId, "Mission:5514B18F", "compact-fire-suppressant-baseline-count");
			int result;
			if (flag == null)
			{
				result = num;
				MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(context.CharacterId, "Mission:5514B18F", "compact-fire-suppressant-baseline-count", result.ToString(CultureInfo.InvariantCulture));
				if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
				{
					return MissionRewardEffectResult.RetryableFailure("Unable to persist the item reward inventory baseline: " + missionOperationResult.Message);
				}
			}
			else if (!int.TryParse(flag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < 0)
			{
				return MissionRewardEffectResult.RetryableFailure("The persisted item reward inventory baseline is invalid.");
			}
			if (num > result)
			{
				return MissionRewardEffectResult.AlreadyApplied("inventory-item:296780:character:" + context.CharacterId.ToString(CultureInfo.InvariantCulture));
			}
			MarcusItemHandoutResult marcusItemHandoutResult = TryGrantCompactFireSuppressant(source);
			if (!marcusItemHandoutResult.Completed)
			{
				return MissionRewardEffectResult.RetryableFailure(marcusItemHandoutResult.Message);
			}
			string effectReference = "inventory-item:296780:character:" + context.CharacterId.ToString(CultureInfo.InvariantCulture);
			return MissionRewardEffectResult.Applied(effectReference);
		}
	}

	private sealed class MarcusItemHandoutResult
	{
		public bool Completed { get; private set; }

		public string Message { get; private set; }

		private MarcusItemHandoutResult()
		{
		}

		public static MarcusItemHandoutResult Succeeded(string message)
		{
			return new MarcusItemHandoutResult
			{
				Completed = true,
				Message = message
			};
		}

		public static MarcusItemHandoutResult Failed(string message)
		{
			return new MarcusItemHandoutResult
			{
				Completed = false,
				Message = message
			};
		}
	}

	private const int AreteLandingPlayfieldId = 6553;

	private const int MarcusStoneInstance = 2016273767;

	private const string B18FCompletionSourceNodeId = "marcus_195107_b18f_002";

	private const int B18FCompletionAnswerIndex = 0;

	private const string B18FCompletionOptionText = "So, let me guess... You need some help with the fire?";

	private const int CompactFireSuppressantItemId = 296780;

	private const int CompactFireSuppressantQuality = 1;

	private const int CapturedTemplateActionUnknown1 = 1;

	private const int CapturedTemplateActionUnknown2 = 87;

	private const int CapturedOverflowNextFreeSlot = 111;

	private const string MissionId = "Mission:5514B18F";

	private const string ObjectiveId = "mission_5514b18f_objective_questfullupdate";

	private const string ItemRewardKey = "compact-fire-suppressant-296780";

	private const string ItemRewardBaselineFlag = "compact-fire-suppressant-baseline-count";

	public static MarcusB18FCompletionResult TryCompleteFromDialogue(ICharacter source, Identity npcIdentity, string previousNodeId, int answerIndex, string optionText, bool dialogueGateEnabled)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0521: Unknown result type (might be due to invalid IL or missing references)
		if (!IsB18FCompletionOption(previousNodeId, answerIndex))
		{
			return MarcusB18FCompletionResult.NotApplicable();
		}
		if (!string.IsNullOrWhiteSpace(optionText) && !string.Equals(optionText.Trim(), "So, let me guess... You need some help with the fire?", StringComparison.Ordinal))
		{
			LogUtil.Debug((DebugInfoDetail)128, "ARETE_MARCUS_B18F_COMPLETION option text drift node=" + previousNodeId + " answer=" + answerIndex + " got=\"" + optionText + "\" expected=\"So, let me guess... You need some help with the fire?\" proceeding=true");
		}
		if (!dialogueGateEnabled)
		{
			return MarcusB18FCompletionResult.Skipped("Marcus B18F completion skipped because dialogue routing gate is disabled. attempted=false noQuestDelete=true noB194=true noItem296780=true");
		}
		if (!IsMarcusStone(npcIdentity) && !IsMarcusStoneNameBound(source, npcIdentity))
		{
			return MarcusB18FCompletionResult.Failed("Marcus B18F completion failed: target is not Marcus Stone. noQuestDelete=true noB194=true");
		}
		if (!IsValidPlayerInArete(source))
		{
			return MarcusB18FCompletionResult.Failed("Marcus B18F completion failed: source is missing, not a player, or not in Arete Landing 6553. noQuestDelete=true noB194=true");
		}
		if (!MissionRuntime.IsInitialized)
		{
			return MarcusB18FCompletionResult.Failed("Marcus B18F completion failed: persistent mission runtime is not initialized.");
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
		if ((mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed || mission.State == MissionLifecycleState.Offered)) || (mission2 != null && mission2.State == MissionLifecycleState.Completed))
		{
			return MarcusB18FCompletionResult.Skipped("Marcus fire handout blocked: fire chain already finished. noItem296780=true noB194=true");
		}
		bool flag = InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780);
		bool flag2 = EnsureB18FActive(instance);
		if (!flag2)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B18F_COMPLETION EnsureB18FActive failed — continuing item+B194 client projection");
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18F");
		if (mission3 != null && mission3.State == MissionLifecycleState.Active)
		{
			PersistentMissionService service = MissionRuntime.Service;
			MissionObjectiveObservation obj = new MissionObjectiveObservation
			{
				CharacterId = instance,
				QuestId = "Mission:5514B18F",
				ObjectiveId = "mission_5514b18f_objective_questfullupdate",
				ObservationKey = "dialogue-fire-option:" + previousNodeId + ":" + answerIndex,
				Amount = 1,
				EventType = "NpcDialogueAnswer"
			};
			identity = ((IEntity)source).Identity;
			obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
			obj.TargetIdentity = ((Identity)(ref npcIdentity)).ToString(true);
			service.ObserveObjective(obj);
			MissionOperationResult missionOperationResult = MissionRuntime.Service.CompleteMission(instance, "Mission:5514B18F");
			if (missionOperationResult.Status != MissionOperationStatus.Applied && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
			{
				LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B18F_COMPLETION CompleteMission status=" + missionOperationResult.Status.ToString() + " message=\"" + missionOperationResult.Message + "\" — continuing item+B194 projection");
			}
		}
		MarcusItemHandoutResult marcusItemHandoutResult = (flag ? MarcusItemHandoutResult.Succeeded("item296780AlreadyPresent=true skipGrant=true") : TryGrantCompactFireSuppressant(source));
		bool flag3 = marcusItemHandoutResult.Completed || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780);
		if (!flag3)
		{
			MissionRewardExecutionResult missionRewardExecutionResult = MissionRuntime.Rewards.ExecuteExternal(instance, "Mission:5514B18F", new MissionRewardDefinition
			{
				RewardKey = "compact-fire-suppressant-296780",
				RewardType = "inventory-item",
				IsResolved = true
			}, new CompactFireSuppressantRewardEffect(source));
			flag3 = InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780);
			if (!flag3)
			{
				LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B18F_COMPLETION item still missing after ledger direct=\"" + marcusItemHandoutResult.Message + "\" ledger=\"" + missionRewardExecutionResult.Message + "\" — retrying direct grant");
				marcusItemHandoutResult = TryGrantCompactFireSuppressant(source);
				flag3 = marcusItemHandoutResult.Completed || InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780);
			}
		}
		MissionOperationResult missionOperationResult2 = MissionRuntime.Service.CompleteAndActivateNextMission(instance, "Mission:5514B18F", "Mission:5514B194");
		if (IsPersistenceFailure(missionOperationResult2))
		{
			ForceCompleteB18FIfNeeded(instance);
			MissionRuntime.Service.OfferMission(instance, "Mission:5514B194");
			MissionRuntime.Service.AcceptMission(instance, "Mission:5514B194");
			LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_B18F_COMPLETION B194 handoff status=" + ((missionOperationResult2 == null) ? "null" : missionOperationResult2.Status.ToString()) + " message=\"" + ((missionOperationResult2 == null) ? "" : missionOperationResult2.Message) + "\" — forced offer/accept + client projection");
		}
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendB18FToB194Handoff(source);
		bool flag4 = rexQuestPreviewEmissionResult?.Emitted ?? false;
		if (!flag4)
		{
			SafeQuestFullUpdateSender.TrySendB18FQuestDelete(source);
			flag4 = SafeQuestFullUpdateSender.TrySendB194Preview(source).Emitted;
		}
		string[] obj2 = new string[18]
		{
			"ARETE_MARCUS_B18F_COMPLETION transition applied character=", null, null, null, null, null, null, null, null, null,
			null, null, null, null, null, null, null, null
		};
		identity = ((IEntity)source).Identity;
		obj2[1] = ((Identity)(ref identity)).ToString(true);
		obj2[2] = " node=";
		obj2[3] = previousNodeId;
		obj2[4] = " answer=";
		obj2[5] = answerIndex.ToString();
		obj2[6] = " itemGrant=";
		obj2[7] = marcusItemHandoutResult.Message;
		obj2[8] = " itemOk=";
		obj2[9] = flag3.ToString();
		obj2[10] = " hadItem=";
		obj2[11] = flag.ToString();
		obj2[12] = " b18fReady=";
		obj2[13] = flag2.ToString();
		obj2[14] = " handoff=";
		obj2[15] = ((rexQuestPreviewEmissionResult == null) ? "null" : rexQuestPreviewEmissionResult.Message);
		obj2[16] = " projected=";
		obj2[17] = flag4.ToString();
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj2));
		if (!flag4)
		{
			return MarcusB18FCompletionResult.Failed("Marcus B18F→B194 client projection failed. itemOk=" + flag3);
		}
		if (!flag3)
		{
			return MarcusB18FCompletionResult.Failed("Marcus B194 projected but Compact Fire Suppressant 296780 missing. direct=\"" + marcusItemHandoutResult.Message + "\"");
		}
		return MarcusB18FCompletionResult.Succeeded("Marcus B18F completion applied item296780=true b194QuestFullUpdateProjected=true");
	}

	private static void ForceCompleteB18FIfNeeded(int characterId)
	{
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, "Mission:5514B18F");
		if (mission != null && mission.State != MissionLifecycleState.Completed && mission.State == MissionLifecycleState.Active)
		{
			MissionRuntime.Service.ObserveObjective(new MissionObjectiveObservation
			{
				CharacterId = characterId,
				QuestId = "Mission:5514B18F",
				ObjectiveId = "mission_5514b18f_objective_questfullupdate",
				ObservationKey = "force-complete-before-b194",
				Amount = 1,
				EventType = "NpcDialogueAnswer",
				SourceIdentity = string.Empty,
				TargetIdentity = string.Empty
			});
			MissionRuntime.Service.CompleteMission(characterId, "Mission:5514B18F");
		}
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}

	private static bool IsB18FCompletionOption(string previousNodeId, int answerIndex)
	{
		return string.Equals(previousNodeId, "marcus_195107_b18f_002", StringComparison.OrdinalIgnoreCase) && answerIndex == 0;
	}

	private static bool IsMarcusStone(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref identity)).Type == 50000 && ((Identity)(ref identity)).Instance == 2016273767;
	}

	private static bool IsMarcusStoneNameBound(ICharacter source, Identity npcIdentity)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IInstancedEntity)source).Playfield == null || (int)((Identity)(ref npcIdentity)).Type != 50000 || ((Identity)(ref npcIdentity)).Instance == 0)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, npcIdentity);
		return @object != null && string.Equals(((INamedEntity)@object).Name, "Marcus Stone", StringComparison.OrdinalIgnoreCase);
	}

	private static bool EnsureB18FActive(int characterId)
	{
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, "Mission:5514B18F");
		if (mission != null && (mission.State == MissionLifecycleState.Active || mission.State == MissionLifecycleState.Completed))
		{
			return true;
		}
		MissionOperationResult missionOperationResult = MissionRuntime.Service.OfferMission(characterId, "Mission:5514B18F");
		if (IsPersistenceFailure(missionOperationResult) && missionOperationResult.Status != MissionOperationStatus.AlreadyApplied)
		{
			return false;
		}
		MissionOperationResult missionOperationResult2 = MissionRuntime.Service.AcceptMission(characterId, "Mission:5514B18F");
		return !IsPersistenceFailure(missionOperationResult2) || missionOperationResult2.Status == MissionOperationStatus.AlreadyApplied;
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

	private static MarcusItemHandoutResult TryGrantCompactFireSuppressant(ICharacter source)
	{
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source))
		{
			return MarcusItemHandoutResult.Failed("sourceInventoryAvailable=false");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return MarcusItemHandoutResult.Failed("sourceClientAvailable=false");
		}
		if (!ItemLoader.ItemList.ContainsKey(296780))
		{
			return MarcusItemHandoutResult.Failed("itemTemplate296780Available=false");
		}
		EnsureSuppressantTemplateAllowsGrant();
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780))
		{
			return MarcusItemHandoutResult.Succeeded("item296780AlreadyPresent=true carried=true noClientNotify=true");
		}
		Item val;
		try
		{
			val = new Item(1, 296780, 296780);
			if (val.MultipleCount < 1)
			{
				val.MultipleCount = 1;
			}
		}
		catch (Exception ex)
		{
			return MarcusItemHandoutResult.Failed("item296780CreateFailed=true error=\"" + ex.Message + "\"");
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, val);
		if (questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.InventoryAddFailed)
		{
			if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780))
			{
				return MarcusItemHandoutResult.Succeeded("item296780AlreadyPresent=true carried=true afterAddFail=true");
			}
			InventoryError inventoryError = questRewardInventoryGrantResult.InventoryError;
			return MarcusItemHandoutResult.Failed("item296780InventoryAddFailed=true error=" + ((object)(InventoryError)(ref inventoryError)).ToString());
		}
		if (questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.PersistFailed)
		{
			return MarcusItemHandoutResult.Failed("item296780InventoryPersistFailed=true error=\"" + questRewardInventoryGrantResult.ExceptionMessage + "\"");
		}
		if (questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.PersistReturnedFalse)
		{
			return MarcusItemHandoutResult.Failed("item296780InventoryPersistFailed=true writeReturnedFalse=true");
		}
		try
		{
			SendCompactFireSuppressantNotifications(source, val);
		}
		catch (Exception ex2)
		{
			return MarcusItemHandoutResult.Failed("item296780ClientNotifyFailed=true error=\"" + ex2.Message + "\"");
		}
		return MarcusItemHandoutResult.Succeeded("item296780Granted=true inventoryPersisted=true notifications=TemplateAction,ContainerAddItem");
	}

	private static void EnsureSuppressantTemplateAllowsGrant()
	{
		if (ItemLoader.ItemList.TryGetValue(296780, out var value) && value != null && value.Stats != null && value.Stats.ContainsKey(0))
		{
			int num = value.Stats[0];
			if (((uint)num & 0x8000000u) != 0)
			{
				value.Stats[0] = num & -134217729;
				LogUtil.Debug((DebugInfoDetail)128, "ARETE_MARCUS_B18F cleared Unique flag on template 296780 for quest handout");
			}
		}
	}

	private static void SendCompactFireSuppressantNotifications(ICharacter source, Item item)
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
