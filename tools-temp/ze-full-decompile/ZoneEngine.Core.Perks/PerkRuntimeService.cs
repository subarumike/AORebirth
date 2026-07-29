using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Perks;

public sealed class PerkRuntimeService
{
	public static readonly PerkRuntimeService Default = new PerkRuntimeService();

	private const int QueuePerkParameter1 = 2;

	private const int QueuePerkParameter2 = 100;

	private const int FallbackPerkAvailableDelayMilliseconds = 750;

	public const int FullPerkResetCooldownSeconds = 172800;

	public const int EarlyFullPerkResetCreditCost = 20000000;

	public bool IsFullPerkResetFree(Character character)
	{
		if (character == null)
		{
			return false;
		}
		int baseValue = (int)((Dynel)character).Stats[(StatIds)577].BaseValue;
		if (baseValue <= 0)
		{
			return true;
		}
		long num = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		return num - baseValue >= 172800;
	}

	public int GetFullPerkResetCooldownRemainingSeconds(Character character)
	{
		if (character == null)
		{
			return 0;
		}
		int baseValue = (int)((Dynel)character).Stats[(StatIds)577].BaseValue;
		if (baseValue <= 0)
		{
			return 0;
		}
		long num = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		long num2 = 172800 - (num - baseValue);
		if (num2 <= 0)
		{
			return 0;
		}
		return (int)((num2 > int.MaxValue) ? int.MaxValue : num2);
	}

	public bool TryResetAllPerks(Character character, bool chargeEarlyFee)
	{
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((Dynel)character).Controller == null || ((Dynel)character).Controller.Client == null)
		{
			return false;
		}
		character.EnsureTrainedPerks();
		bool flag = IsFullPerkResetFree(character);
		if (!flag && !chargeEarlyFee)
		{
			return false;
		}
		if (!flag && chargeEarlyFee)
		{
			int num = CashStatRules.Clamp(((Dynel)character).Stats[(StatIds)61].BaseValue);
			if (num < 20000000)
			{
				return false;
			}
			int num2 = CashStatRules.Clamp((long)num - 20000000L);
			((Dynel)character).Stats[(StatIds)61].Set((uint)num2, false);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle((ICharacter)(object)character, 61, (uint)num2);
		}
		List<int> list = character.TrainedPerkPacketIds.ToList();
		foreach (int item in list)
		{
			if (PerkCatalog.TryGet(item, out var definition) && definition != null && definition.GrantsPerkAction)
			{
				SendRemovePerkAction(character, definition);
			}
		}
		character.TrainedPerkPacketIds.Clear();
		if (character.LockedPerkPacketIdsUntilUtc != null)
		{
			character.LockedPerkPacketIdsUntilUtc.Clear();
		}
		Identity identity;
		try
		{
			CharacterPerksDao instance = Dao<DBCharacterPerk, CharacterPerksDao>.Instance;
			identity = ((PooledObject)character).Identity;
			instance.DeleteAllPerks(((Identity)(ref identity)).Instance);
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
		}
		SendClearAllPerks(character);
		int num3 = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		((Dynel)character).Stats[(StatIds)577].Set((uint)num3, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle((ICharacter)(object)character, 577, (uint)num3);
		try
		{
			((IDatabaseObject)((Dynel)character).Stats).Write();
		}
		catch (Exception ex2)
		{
			LogUtil.ErrorException(ex2);
		}
		PerkResetMissionSender.SendResetCooldownMission(character, 172800);
		string[] obj = new string[6] { "PERK_RESET char=", null, null, null, null, null };
		identity = ((PooledObject)character).Identity;
		obj[1] = ((Identity)(ref identity)).Instance.ToString();
		obj[2] = " removed=";
		obj[3] = list.Count.ToString();
		obj[4] = " paid=";
		obj[5] = (!flag && chargeEarlyFee).ToString();
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		return true;
	}

	public bool TryHandleTrainPerk(IZoneClient client, CharacterActionMessage message)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			return false;
		}
		int parameter = message.Parameter2;
		val.EnsureTrainedPerks();
		val.TrainedPerkPacketIds.Add(parameter);
		try
		{
			CharacterPerksDao instance = Dao<DBCharacterPerk, CharacterPerksDao>.Instance;
			Identity identity = ((PooledObject)val).Identity;
			instance.WritePerk(((Identity)(ref identity)).Instance, parameter);
			identity = ((PooledObject)val).Identity;
			LogUtil.Debug((DebugInfoDetail)128, "PERK_PERSIST write char=" + ((Identity)(ref identity)).Instance + " packetId=" + parameter);
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
		}
		PerkCatalog.TryGet(parameter, out var definition);
		if (definition != null && definition.GrantsPerkAction)
		{
			SendAddPerkAction(val, definition);
		}
		SendTrainPerkAck(val, parameter);
		return true;
	}

	public bool TryHandleUsePerk(IZoneClient client, CharacterActionMessage message)
	{
		if (client == null || client.Controller == null || client.Controller.Character == null)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null)
		{
			return false;
		}
		PerkDefinition perkDefinition = ResolveUseDefinition(message);
		int num = perkDefinition?.PacketId ?? ResolvePacketIdFromUse(message);
		val.EnsureTrainedPerks();
		if (num > 0 && !val.TrainedPerkPacketIds.Contains(num))
		{
			val.TrainedPerkPacketIds.Add(num);
		}
		if (num > 0 && val.IsPerkLocked(num))
		{
			SendPerkUnavailable(val, num);
			return true;
		}
		SendQueuePerk(val);
		bool flag = false;
		if (perkDefinition != null && perkDefinition.GrantsPerkAction && perkDefinition.ActionTemplateId.HasValue)
		{
			flag = ExecuteActionOnUse(val, perkDefinition.ActionTemplateId.Value);
		}
		else
		{
			string perkActionName = ((perkDefinition != null && !string.IsNullOrEmpty(perkDefinition.Name)) ? StripTierSuffix(perkDefinition.Name) : "Perk");
			SendPerformFeedback(val, perkActionName);
		}
		if (!flag && num > 0)
		{
			val.LockPerkPacket(num, 1);
			SendPerkUnavailable(val, num);
			SchedulePerkAvailable(val, num, 750);
		}
		return true;
	}

	public void ResendPerkActions(Character character)
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		character.EnsureTrainedPerks();
		foreach (int trainedPerkPacketId in character.TrainedPerkPacketIds)
		{
			SendTrainPerkAck(character, trainedPerkPacketId);
			if (PerkCatalog.TryGet(trainedPerkPacketId, out var definition) && definition.GrantsPerkAction)
			{
				SendAddPerkAction(character, definition);
			}
		}
		Identity identity = ((PooledObject)character).Identity;
		LogUtil.Debug((DebugInfoDetail)128, "PERK_PERSIST resync char=" + ((Identity)(ref identity)).Instance + " count=" + character.TrainedPerkPacketIds.Count);
		int fullPerkResetCooldownRemainingSeconds = GetFullPerkResetCooldownRemainingSeconds(character);
		if (fullPerkResetCooldownRemainingSeconds > 0)
		{
			PerkResetMissionSender.SendResetCooldownMission(character, fullPerkResetCooldownRemainingSeconds);
		}
	}

	private bool ExecuteActionOnUse(Character character, int actionTemplateId)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Invalid comparison between Unknown and I4
		if (!ItemLoader.ItemList.TryGetValue(actionTemplateId, out var value) || value.Events == null)
		{
			LogUtil.Debug((DebugInfoDetail)256, "Perk action template missing id=" + actionTemplateId);
			return false;
		}
		PrepareCombatTarget(character);
		bool result = false;
		foreach (Event @event in value.Events)
		{
			if ((int)@event.EventType > 0)
			{
				continue;
			}
			foreach (Function function in @event.Functions)
			{
				if (function.FunctionType == 53187)
				{
					result = true;
				}
			}
			@event.Perform((ICharacter)(object)character, (IEntity)(object)character);
		}
		return result;
	}

	private static void PrepareCombatTarget(Character character)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		Identity target = Identity.None;
		Identity val = character.SelectedTarget;
		if (((Identity)(ref val)).Instance != 0)
		{
			val = character.SelectedTarget;
			int instance = ((Identity)(ref val)).Instance;
			val = ((PooledObject)character).Identity;
			if (instance != ((Identity)(ref val)).Instance)
			{
				target = character.SelectedTarget;
				goto IL_009b;
			}
		}
		val = character.FightingTarget;
		if (((Identity)(ref val)).Instance != 0)
		{
			val = character.FightingTarget;
			int instance2 = ((Identity)(ref val)).Instance;
			val = ((PooledObject)character).Identity;
			if (instance2 != ((Identity)(ref val)).Instance)
			{
				target = character.FightingTarget;
			}
		}
		goto IL_009b;
		IL_009b:
		if (((Identity)(ref target)).Instance != 0)
		{
			character.SetTarget(target);
		}
	}

	private static PerkDefinition ResolveUseDefinition(CharacterActionMessage message)
	{
		if (message.Parameter2 != 0 && PerkCatalog.TryGetByActionHash(message.Parameter2, out var definition))
		{
			return definition;
		}
		int num = ResolvePacketIdFromUse(message);
		if (num > 0 && PerkCatalog.TryGet(num, out definition))
		{
			return definition;
		}
		return null;
	}

	private static int ResolvePacketIdFromUse(CharacterActionMessage message)
	{
		if (message.Parameter1 >= 10000)
		{
			return message.Parameter1 - 10000;
		}
		return message.Parameter1;
	}

	private static string StripTierSuffix(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}
		int num = name.LastIndexOf(' ');
		if (num <= 0)
		{
			return name;
		}
		string s = name.Substring(num + 1);
		if (int.TryParse(s, out var _))
		{
			return name.Substring(0, num);
		}
		return name;
	}

	private void SendAddPerkAction(Character character, PerkDefinition def)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		IZoneClient client = ((Dynel)character).Controller.Client;
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)180,
			Unknown1 = 0
		};
		Identity target = default(Identity);
		((Identity)(ref target)).Type = (IdentityType)0;
		((Identity)(ref target)).Instance = def.ActionTemplateId.Value;
		val.Target = target;
		val.Parameter1 = def.ActionSlotId;
		val.Parameter2 = def.ActionHash.Value;
		val.Unknown2 = 0;
		client.SendCompressed((MessageBody)val);
	}

	private void SendRemovePerkAction(Character character, PerkDefinition def)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		IZoneClient client = ((Dynel)character).Controller.Client;
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)182,
			Unknown1 = 0
		};
		Identity target = default(Identity);
		((Identity)(ref target)).Type = (IdentityType)0;
		((Identity)(ref target)).Instance = def.ActionTemplateId.Value;
		val.Target = target;
		val.Parameter1 = def.ActionSlotId;
		val.Parameter2 = def.ActionHash.Value;
		val.Unknown2 = 0;
		client.SendCompressed((MessageBody)val);
	}

	private void SendClearAllPerks(Character character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)201,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = 0,
			Unknown2 = 0
		});
	}

	private void SendTrainPerkAck(Character character, int packetId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)187,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = packetId,
			Unknown2 = 0
		});
	}

	private void SendQueuePerk(Character character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)80,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 2,
			Parameter2 = 100,
			Unknown2 = 0
		});
	}

	private void SendPerkUnavailable(Character character, int packetId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)207,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = packetId,
			Parameter2 = 1,
			Unknown2 = 0
		});
	}

	private void SendPerkAvailable(Character character, int packetId)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)206,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = packetId,
			Unknown2 = 0
		});
	}

	private void SendPerformFeedback(Character character, string perkActionName)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		((Dynel)character).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
		{
			Identity = ((PooledObject)character).Identity,
			Unknown = 1,
			Unknown1 = 0,
			FormattedMessage = "~&!!!\":!!!)<s'You successfully perform " + perkActionName + ".",
			Unknown2 = 0
		});
	}

	private void SchedulePerkAvailable(Character character, int packetId, int delayMs)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(delayMs);
			if (character != null && ((Dynel)character).Controller != null && ((Dynel)character).Controller.Client != null)
			{
				SendPerkAvailable(character, packetId);
			}
		});
	}
}
