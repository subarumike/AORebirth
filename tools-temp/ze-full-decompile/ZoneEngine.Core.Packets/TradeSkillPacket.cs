using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core.Packets;

public static class TradeSkillPacket
{
	public static void SendNotTradeskill(ICharacter character)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Action = (CharacterActionType)225,
			Unknown = 0,
			Target = default(Identity),
			Parameter1 = 0,
			Parameter2 = 0
		};
		((IDynel)character).Send((MessageBody)(object)val, false);
	}

	public static void SendOutOfRange(ICharacter character, int min)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Action = (CharacterActionType)226,
			Unknown = 0,
			Target = default(Identity),
			Parameter1 = 0,
			Parameter2 = min
		};
		((IDynel)character).Send((MessageBody)(object)val, false);
	}

	public static void SendRequirement(ICharacter character, int tradeSkillStatId, int tradeSkillRequirement)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Action = (CharacterActionType)227,
			Identity = ((IEntity)character).Identity,
			Unknown1 = 0,
			Target = default(Identity),
			Parameter1 = tradeSkillStatId,
			Parameter2 = tradeSkillRequirement
		};
		((IDynel)character).Send((MessageBody)(object)val, false);
	}

	public static void SendResult(ICharacter character, int min, int max, int low, int high)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Action = (CharacterActionType)228,
			Identity = ((IEntity)character).Identity,
			Unknown1 = 0
		};
		Identity target = default(Identity);
		((Identity)(ref target)).Type = (IdentityType)max;
		((Identity)(ref target)).Instance = high;
		val.Target = target;
		val.Parameter1 = min;
		val.Parameter2 = low;
		CharacterActionMessage val2 = val;
		((IDynel)character).Send((MessageBody)(object)val2, false);
	}

	public static void SendSource(ICharacter character, int count)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Action = (CharacterActionType)223,
			Unknown = 0,
			Target = default(Identity),
			Parameter1 = 0,
			Parameter2 = count
		};
		((IDynel)character).Send((MessageBody)(object)val, false);
	}

	public static void SendTarget(ICharacter character, int count)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		CharacterActionMessage val = new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Action = (CharacterActionType)224,
			Unknown = 0,
			Target = default(Identity),
			Parameter1 = 0,
			Parameter2 = count
		};
		((IDynel)character).Send((MessageBody)(object)val, false);
	}
}
