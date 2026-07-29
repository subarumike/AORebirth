using System;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecQuestRuntime
{
	private sealed class PersonalResearchAllocationEffect : IMissionRewardEffect
	{
		private readonly int characterId;

		public PersonalResearchAllocationEffect(int characterId)
		{
			this.characterId = characterId;
		}

		public MissionRewardEffectResult Apply(MissionRewardExecutionContext context)
		{
			MissionFlagRecord flag = MissionRuntime.Service.GetFlag(characterId, "Mission:55579381", "personal-research-xp-allocation");
			if (flag != null)
			{
				return MissionRewardEffectResult.AlreadyApplied("mission-flag:personal-research-xp-allocation:5000");
			}
			MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(characterId, "Mission:55579381", "personal-research-xp-allocation", 5000.ToString());
			return (missionOperationResult.Status == MissionOperationStatus.Applied || missionOperationResult.Status == MissionOperationStatus.AlreadyApplied) ? MissionRewardEffectResult.Applied("mission-flag:personal-research-xp-allocation:5000") : MissionRewardEffectResult.RetryableFailure(missionOperationResult.Message);
		}
	}

	internal const int SubwayPlayfieldId = 655;

	internal const int WindcallerKarrecInstance = 2036555963;

	internal const int AnnoyingDudeInstance = 2036555965;

	internal const int MaddyCardileInstance = 2036555964;

	internal const int BrontoBurgerItemId = 297042;

	internal const int MaddyCreditCardItemId = 297043;

	internal const int SideTokenStatId = 75;

	internal const int SideTokenReward = 2;

	internal const int PersonalResearchXpAllocation = 5000;

	internal const string AccountAccessFlagKey = "totw-wall-access";

	internal const string QuestId = "Mission:55579381";

	private const string ObjectiveId = "mission_55579381_deliver_offerings";

	private const string BurgerGrantFlag = "bronto-burger-granted";

	private const string CardGrantFlag = "maddy-credit-card-granted";

	private const string ResearchAllocationFlag = "personal-research-xp-allocation";

	private const string LevelXpRewardFlag = "one-level-xp-reward";

	internal static bool IsActive(ICharacter source)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:55579381");
		return mission != null && mission.State == MissionLifecycleState.Active;
	}

	internal static bool IsCompleted(ICharacter source)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		MissionStateRecord mission = service.GetMission(((Identity)(ref identity)).Instance, "Mission:55579381");
		return mission != null && mission.State == MissionLifecycleState.Completed;
	}

	internal static MissionOperationResult Accept(ICharacter source)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
		{
			return new MissionOperationResult
			{
				Status = MissionOperationStatus.Unresolved,
				Message = "Karrec acceptance requires an initialized mission runtime and a player in Subway 655."
			};
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionOperationResult result = MissionRuntime.Service.OfferMission(instance, "Mission:55579381");
		if (IsPersistenceFailure(result))
		{
			return result;
		}
		return MissionRuntime.Service.AcceptMission(instance, "Mission:55579381");
	}

	internal static bool TryGrantBurger(ICharacter source)
	{
		return TryGrantObjectiveItem(source, 297042, "bronto-burger-granted");
	}

	internal static bool TryGrantCreditCard(ICharacter source)
	{
		return TryGrantObjectiveItem(source, 297043, "maddy-credit-card-granted");
	}

	internal static bool HasBothOfferingItems(ICharacter source)
	{
		return IsActive(source) && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 297042) && InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 297043);
	}

	internal static KarrecCompletionResult CompleteAfterOfferingsConsumed(ICharacter source)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerInSubway(source) || !MissionRuntime.IsInitialized)
		{
			return KarrecCompletionResult.Failed("invalid-player-playfield-or-mission-runtime");
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:55579381");
		if (mission == null || (mission.State != MissionLifecycleState.Active && mission.State != MissionLifecycleState.Completed))
		{
			return KarrecCompletionResult.Failed("karrec-mission-not-active");
		}
		string text = MissionRuntime.ResolveAccountKey(instance);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "character:" + instance.ToString(CultureInfo.InvariantCulture);
		}
		if (mission.State == MissionLifecycleState.Active)
		{
			MissionOperationResult missionOperationResult = ObserveOffering(source, 297042, "trade-offering:297042");
			MissionOperationResult missionOperationResult2 = ObserveOffering(source, 297043, "trade-offering:297043");
			if (IsPersistenceFailure(missionOperationResult) || IsPersistenceFailure(missionOperationResult2))
			{
				return KarrecCompletionResult.Failed("offering-observation-failed:" + (IsPersistenceFailure(missionOperationResult) ? missionOperationResult.Message : missionOperationResult2.Message));
			}
			MissionOperationResult missionOperationResult3 = MissionRuntime.Service.CompleteMission(instance, "Mission:55579381");
			if (missionOperationResult3.Status != MissionOperationStatus.Applied && missionOperationResult3.Status != MissionOperationStatus.AlreadyApplied)
			{
				return KarrecCompletionResult.Failed("completion-failed:" + missionOperationResult3.Message);
			}
		}
		if (MissionRuntime.Service.GetAccountFlag(text, "totw-wall-access") == null)
		{
			MissionOperationResult missionOperationResult4 = MissionRuntime.Service.SetAccountFlag(instance, text, "Mission:55579381", "totw-wall-access", "completed:Mission:55579381");
			if (missionOperationResult4.Status != MissionOperationStatus.Applied && missionOperationResult4.Status != MissionOperationStatus.AlreadyApplied)
			{
				return KarrecCompletionResult.Failed("account-flag-persistence-failed:" + missionOperationResult4.Message);
			}
		}
		TryAwardOneLevelXpReward(source, instance);
		MissionRewardExecutionResult missionRewardExecutionResult = ApplySideTokenReward(source);
		MissionRewardExecutionStatus researchStatus = MissionRewardExecutionStatus.Unresolved;
		MissionRewardDefinition definition = new MissionRewardDefinition
		{
			RewardKey = "personal-research-xp-5000",
			RewardType = "personal-research-allocation",
			IsResolved = true
		};
		MissionRewardExecutionResult missionRewardExecutionResult2 = MissionRuntime.Rewards.ExecuteExternal(instance, "Mission:55579381", definition, new PersonalResearchAllocationEffect(instance));
		if (missionRewardExecutionResult2 != null && missionRewardExecutionResult2.Succeeded)
		{
			researchStatus = missionRewardExecutionResult2.Status;
		}
		long sideTokenValue = ((IStats)source).Stats[(StatIds)75].BaseValue;
		if (missionRewardExecutionResult != null && missionRewardExecutionResult.Succeeded && missionRewardExecutionResult.StatValues != null)
		{
			foreach (MissionCharacterStatValue statValue in missionRewardExecutionResult.StatValues)
			{
				if (statValue.StatId == 75)
				{
					sideTokenValue = statValue.Value;
					((IStats)source).Stats[(StatIds)75].Set((uint)((statValue.Value > 0) ? Math.Min(statValue.Value, 4294967295L) : 0u), false);
				}
			}
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(source);
		}
		return KarrecCompletionResult.Succeeded(sideTokenValue, (missionRewardExecutionResult != null && missionRewardExecutionResult.Succeeded) ? missionRewardExecutionResult.Status : MissionRewardExecutionStatus.Unresolved, researchStatus);
	}

	internal static bool HasAccountAccess(ICharacter source)
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
		if (!string.IsNullOrWhiteSpace(text) && MissionRuntime.Service.GetAccountFlag(text, "totw-wall-access") != null)
		{
			return true;
		}
		string accountKey = "character:" + instance.ToString(CultureInfo.InvariantCulture);
		return MissionRuntime.Service.GetAccountFlag(accountKey, "totw-wall-access") != null;
	}

	private static void TryAwardOneLevelXpReward(ICharacter source, int characterId)
	{
		if (source != null && MissionRuntime.IsInitialized && MissionRuntime.Service.GetFlag(characterId, "Mission:55579381", "one-level-xp-reward") == null)
		{
			int xpNeededForNextLevel = CombatXpRuntimeService.GetXpNeededForNextLevel(source);
			if (xpNeededForNextLevel <= 0)
			{
				MissionRuntime.Service.SetFlag(characterId, "Mission:55579381", "one-level-xp-reward", "skipped-max-level");
				return;
			}
			bool flag = CombatXpRuntimeService.AwardDirectXp(source, xpNeededForNextLevel, "karrec-quest");
			MissionRuntime.Service.SetFlag(characterId, "Mission:55579381", "one-level-xp-reward", flag ? ("awarded:" + xpNeededForNextLevel.ToString(CultureInfo.InvariantCulture)) : ("failed:" + xpNeededForNextLevel.ToString(CultureInfo.InvariantCulture)));
		}
	}

	private static MissionOperationResult ObserveOffering(ICharacter source, int itemId, string observationKey)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		PersistentMissionService service = MissionRuntime.Service;
		MissionObjectiveObservation missionObjectiveObservation = new MissionObjectiveObservation();
		Identity identity = ((IEntity)source).Identity;
		missionObjectiveObservation.CharacterId = ((Identity)(ref identity)).Instance;
		missionObjectiveObservation.QuestId = "Mission:55579381";
		missionObjectiveObservation.ObjectiveId = "mission_55579381_deliver_offerings";
		missionObjectiveObservation.ObservationKey = observationKey;
		missionObjectiveObservation.Amount = 1;
		missionObjectiveObservation.EventType = "KnuBotTrade:OfferingConsumed";
		identity = ((IEntity)source).Identity;
		missionObjectiveObservation.SourceIdentity = ((Identity)(ref identity)).ToString(true);
		missionObjectiveObservation.TargetIdentity = "Item:" + itemId;
		return service.ObserveObjective(missionObjectiveObservation);
	}

	private static MissionRewardExecutionResult ApplySideTokenReward(ICharacter source)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "side-tokens-2";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[1]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 75,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 2L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		return rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:55579381", definition, "capture:20260717-223626:stat-75-plus-2");
	}

	private static bool TryGrantObjectiveItem(ICharacter source, int itemId, string flagKey)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Expected O, but got Unknown
		if (!IsActive(source))
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:55579381", flagKey) != null)
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
			SendObjectiveItemNotifications(source, item);
		}
		MissionOperationResult missionOperationResult = MissionRuntime.Service.SetFlag(instance, "Mission:55579381", flagKey, "item:" + itemId);
		return missionOperationResult.Status == MissionOperationStatus.Applied || missionOperationResult.Status == MissionOperationStatus.AlreadyApplied;
	}

	private static void SendObjectiveItemNotifications(ICharacter source, Item item)
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

	private static bool IsPlayerInSubway(ICharacter source)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (source != null)
		{
			Identity identity = ((IEntity)source).Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				identity = ((IEntity)source).Identity;
				if (((Identity)(ref identity)).Instance != 0 && ((IInstancedEntity)source).Playfield != null)
				{
					identity = ((IEntity)((IInstancedEntity)source).Playfield).Identity;
					result = ((((Identity)(ref identity)).Instance == 655) ? 1 : 0);
					goto IL_004e;
				}
			}
		}
		result = 0;
		goto IL_004e;
		IL_004e:
		return (byte)result != 0;
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}
}
