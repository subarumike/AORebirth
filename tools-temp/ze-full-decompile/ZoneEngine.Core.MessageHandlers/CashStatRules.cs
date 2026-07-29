using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Stats;

namespace ZoneEngine.Core.MessageHandlers;

public static class CashStatRules
{
	public const int ClientSafeMaxCash = 999999999;

	public static int Clamp(long cash)
	{
		if (cash < 0)
		{
			return 0;
		}
		if (cash > 999999999)
		{
			return 999999999;
		}
		return (int)cash;
	}

	public static uint Normalize(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)61].BaseValue;
		uint num = (uint)Clamp(baseValue);
		if (baseValue != num)
		{
			((IStats)character).Stats[(StatIds)61].Set(num, false);
		}
		return num;
	}
}
