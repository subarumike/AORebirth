using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Missions;

internal static class MissionCompleteService
{
	private const int MissionIdentityType = 56003;

	private const int MissionCompleteAction = 59;

	private const int OverflowNextFreeSlot = 111;

	private const int TemplateActionUnknown1 = 1;

	private const int TemplateActionUnknown2 = 87;

	private const int ClanTokenLowId = 103910;

	private const int ClanTokenHighId = 103911;

	private const int OmniTokenLowId = 103908;

	private const int OmniTokenHighId = 103909;

	private static readonly object Gate = new object();

	private static readonly HashSet<string> InFlight = new HashSet<string>();

	public static bool TryCompleteLatest(IZoneClient client, ICharacter character, string reason)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return false;
		}
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		if (all.Count == 0)
		{
			return false;
		}
		MissionAcceptedStore.AcceptedMission entry = all[all.Count - 1];
		return TryComplete(client, character, entry, reason);
	}

	public static bool TryCompleteIfInInstance(IZoneClient client, ICharacter character, string reason)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance))
		{
			return false;
		}
		return TryCompleteLatest(client, character, reason);
	}

	public static bool TryCompleteIfMissionTargetKilled(ICharacter attacker, ICharacter victim, string reason)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || victim == null || !MissionTargetTracker.IsMissionTarget(((IEntity)victim).Identity))
		{
			return false;
		}
		if (((IInstancedEntity)victim).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)victim).Playfield).Identity;
			if (MissionInstanceService.IsMissionInstancePlayfield(((Identity)(ref identity)).Instance))
			{
				if (((IDynel)attacker).Controller == null || !(((IDynel)attacker).Controller is PlayerController))
				{
					return false;
				}
				if (!(((IDynel)attacker).Controller.Client is ZoneClient client))
				{
					return false;
				}
				MissionTargetTracker.Unregister(((IEntity)victim).Identity);
				return TryCompleteLatest((IZoneClient)(object)client, attacker, reason ?? "KillTarget");
			}
		}
		return false;
	}

	public static bool TryComplete(IZoneClient client, ICharacter character, MissionAcceptedStore.AcceptedMission entry, string reason)
	{
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || character == null || entry == null)
		{
			return false;
		}
		Identity identity = ((IEntity)character).Identity;
		string item = ((Identity)(ref identity)).Instance.ToString("X") + ":" + ((Identity)(ref entry.QuestIdentity)).Instance.ToString("X");
		lock (Gate)
		{
			if (!InFlight.Add(item))
			{
				return false;
			}
		}
		try
		{
			int num = ResolveCashReward(entry);
			int num2 = ResolveXpReward(entry);
			GrantCredits(character, num);
			SendRewardFeedback(character, num2, num);
			int quality = ((entry.Quality <= 0) ? 1 : entry.Quality);
			TryGrantSideToken(character, quality);
			SendMissionCompleteAction(character, entry.QuestIdentity);
			SendQuestDelete(character, entry.QuestIdentity);
			bool flag = false;
			identity = ((IEntity)character).Identity;
			if (MissionKeyStore.TryTakeLatest(((Identity)(ref identity)).Instance, out var keyInstance))
			{
				flag = MissionKeyGrantService.TryRemoveMissionKey(client, character, keyInstance);
			}
			identity = ((IEntity)character).Identity;
			bool flag2 = MissionAcceptedStore.Remove(((Identity)(ref identity)).Instance, entry.QuestIdentity);
			object[] array = new object[7];
			identity = ((IEntity)character).Identity;
			array[0] = ((Identity)(ref identity)).Instance;
			array[1] = ((Identity)(ref entry.QuestIdentity)).Instance;
			array[2] = reason ?? string.Empty;
			array[3] = num;
			array[4] = num2;
			array[5] = flag;
			array[6] = flag2;
			MissionDiagnostics.Log("COMPLETE char={0} mission={1:X8} reason={2} cash={3} xp={4} keyRemoved={5} storeRemoved={6}", array);
			return true;
		}
		catch (Exception ex)
		{
			object[] array2 = new object[2];
			identity = ((IEntity)character).Identity;
			array2[0] = ((Identity)(ref identity)).Instance;
			array2[1] = ex.Message;
			MissionDiagnostics.Log("COMPLETE-FAIL char={0} err={1}", array2);
			return false;
		}
		finally
		{
			lock (Gate)
			{
				InFlight.Remove(item);
			}
		}
	}

	private static int ResolveCashReward(MissionAcceptedStore.AcceptedMission entry)
	{
		if (entry.Offer != null && entry.Offer.CashReward > 0)
		{
			return entry.Offer.CashReward;
		}
		int num = ((entry.Quality <= 0) ? 1 : entry.Quality);
		return Math.Max(100, num * 90);
	}

	private static int ResolveXpReward(MissionAcceptedStore.AcceptedMission entry)
	{
		if (entry.Offer != null && entry.Offer.ExperienceReward > 0)
		{
			return entry.Offer.ExperienceReward;
		}
		return 0;
	}

	private static void GrantCredits(ICharacter character, int cashReward)
	{
		if (cashReward > 0 && character != null)
		{
			long num = ((IStats)character).Stats[(StatIds)61].BaseValue;
			if (num < 0)
			{
				num = 0L;
			}
			long num2 = num + cashReward;
			if (num2 > int.MaxValue)
			{
				num2 = 2147483647L;
			}
			((IStats)character).Stats[(StatIds)61].Set((uint)num2, false);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num2);
		}
	}

	private static void SendRewardFeedback(ICharacter character, int xp, int cash)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		((IDynel)character).Send((MessageBody)new FormatFeedbackMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 1,
			Unknown1 = 0,
			Unknown2 = 0,
			FormattedMessage = $"Received reward: {xp} XP, {cash} credits."
		}, false);
	}

	private static void TryGrantSideToken(ICharacter character, int quality)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Expected O, but got Unknown
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Expected O, but got Unknown
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Invalid comparison between Unknown and I4
		Side val = (Side)((IStats)character).Stats[(StatIds)33].Value;
		int num;
		int num2;
		Identity val2;
		if ((int)val == 1)
		{
			num = 103910;
			num2 = 103911;
		}
		else
		{
			if ((int)val != 2)
			{
				object[] array = new object[2];
				val2 = ((IEntity)character).Identity;
				array[0] = ((Identity)(ref val2)).Instance;
				array[1] = val;
				MissionDiagnostics.Log("TOKEN-SKIP char={0} side={1} (neutral/other → no token)", array);
				return;
			}
			num = 103908;
			num2 = 103909;
		}
		int num3 = MissionLevelTable.GetTokenReward(((IStats)character).Stats[(StatIds)54].Value);
		if (num3 <= 0)
		{
			num3 = 1;
		}
		bool flag = false;
		try
		{
			if (ItemLoader.ItemList.ContainsKey(num) && ItemLoader.ItemList.ContainsKey(num2) && ((IItemContainer)character).BaseInventory != null && ((IItemContainer)character).BaseInventory.Pages.TryGetValue(((IItemContainer)character).BaseInventory.StandardPage, out var value))
			{
				int num4 = value.FindFreeSlot();
				if (num4 >= 0)
				{
					Item val3 = new Item(quality, num, num2)
					{
						MultipleCount = num3,
						Flags = 1
					};
					if ((int)value.Add(num4, (IItem)(object)val3) == 0)
					{
						((IItemContainer)character).BaseInventory.Write();
						flag = true;
					}
				}
			}
		}
		catch (Exception ex)
		{
			object[] array2 = new object[3];
			val2 = ((IEntity)character).Identity;
			array2[0] = ((Identity)(ref val2)).Instance;
			array2[1] = val;
			array2[2] = ex.Message;
			MissionDiagnostics.Log("TOKEN-INV-FAIL char={0} side={1} err={2}", array2);
		}
		TemplateActionMessage val4 = new TemplateActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			ItemLowId = num,
			ItemHighId = num2,
			Quality = quality,
			Unknown1 = 1,
			Unknown2 = 87
		};
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		((Identity)(ref val2)).Instance = 0;
		val4.Placement = val2;
		val4.Unknown3 = 0;
		val4.Unknown4 = 0;
		((IDynel)character).Send((MessageBody)val4, false);
		ContainerAddItemMessage val5 = new ContainerAddItemMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0
		};
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		((Identity)(ref val2)).Instance = 0;
		val5.SourceContainer = val2;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)110;
		Identity identity = ((IEntity)character).Identity;
		((Identity)(ref val2)).Instance = ((Identity)(ref identity)).Instance;
		val5.Target = val2;
		val5.TargetPlacement = 111;
		((IDynel)character).Send((MessageBody)val5, false);
		object[] array3 = new object[7];
		val2 = ((IEntity)character).Identity;
		array3[0] = ((Identity)(ref val2)).Instance;
		array3[1] = val;
		array3[2] = num;
		array3[3] = num2;
		array3[4] = quality;
		array3[5] = num3;
		array3[6] = flag;
		MissionDiagnostics.Log("TOKEN-GRANT char={0} side={1} low={2} high={3} ql={4} count={5} invOk={6}", array3);
	}

	private static void SendMissionCompleteAction(ICharacter character, Identity mission)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)(((int)((Identity)(ref mission)).Type == 0) ? 56003 : ((int)((Identity)(ref mission)).Type));
		((Identity)(ref val)).Instance = ((Identity)(ref mission)).Instance;
		Identity target = val;
		((IDynel)character).Send((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)59,
			Unknown1 = 0,
			Target = target,
			Parameter1 = 56003,
			Parameter2 = ((Identity)(ref target)).Instance,
			Unknown2 = 0
		}, false);
	}

	private static void SendQuestDelete(ICharacter character, Identity mission)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		QuestMessage val = new QuestMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (QuestAction)1,
			Unknown1 = 0
		};
		Identity mission2 = default(Identity);
		((Identity)(ref mission2)).Type = (IdentityType)(((int)((Identity)(ref mission)).Type == 0) ? 56003 : ((int)((Identity)(ref mission)).Type));
		((Identity)(ref mission2)).Instance = ((Identity)(ref mission)).Instance;
		val.Mission = mission2;
		val.Unknown2 = 0;
		val.Unknown3 = 0;
		((IDynel)character).Send((MessageBody)val, false);
	}
}
