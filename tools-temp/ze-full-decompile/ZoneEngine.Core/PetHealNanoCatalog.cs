using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Nanos;
using AORebirth.Enums;
using AORebirth.Stats;
using MsgPack;

namespace ZoneEngine.Core;

internal static class PetHealNanoCatalog
{
	public const int BelamorteBlessingNanoId = 125720;

	public const int ValentyiaHeatNanoId = 125721;

	public const int SalvinousTouchNanoId = 125722;

	public const int SanooPulseNanoId = 125723;

	public const int MedinosWhisperNanoId = 125728;

	public const int PetNanoExecutedWithinOwnerNcuAction = 129;

	private static readonly int HitFunctionId = 53002;

	private static readonly int HealthStatId = 27;

	private static readonly Dictionary<int, int> HealNanoBySummonNano = new Dictionary<int, int>
	{
		{ 125738, 125728 },
		{ 125743, 125723 },
		{ 125744, 125721 },
		{ 125745, 125722 },
		{ 125746, 125720 }
	};

	private static readonly Dictionary<string, int> HealNanoByPetHash = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "MT01", 125728 },
		{ "MT02", 125722 },
		{ "MT03", 125721 },
		{ "MT04", 125723 },
		{ "BSLX", 125720 }
	};

	private static readonly Dictionary<int, string> HealNanoDisplayName = new Dictionary<int, string>
	{
		{ 125720, "Belamorte's Blessing" },
		{ 125721, "Valentyia's Heat" },
		{ 125722, "Touch of Salvinous" },
		{ 125723, "Pulse of Sanoo" },
		{ 125728, "Whisper of Medinos" }
	};

	private static readonly Dictionary<string, int> HealingPetNanoPoolByHash = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "MT01", 379 },
		{ "MT02", 1207 },
		{ "MT03", 2370 },
		{ "MT04", 3767 },
		{ "BSLX", 13184 }
	};

	private static readonly Dictionary<int, double> HealRechargeSecondsByNano = new Dictionary<int, double>
	{
		{ 125728, 8.0 },
		{ 125722, 8.9 },
		{ 125721, 12.0 },
		{ 125723, 8.7 },
		{ 125720, 6.0 }
	};

	public static bool TryResolveHealNano(int summonNanoId, string petHash, out int healNanoId)
	{
		if (summonNanoId > 0 && HealNanoBySummonNano.TryGetValue(summonNanoId, out healNanoId))
		{
			return true;
		}
		healNanoId = 0;
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return false;
		}
		return HealNanoByPetHash.TryGetValue(petHash, out healNanoId);
	}

	public static string GetHealNanoDisplayName(int healNanoId)
	{
		string value;
		return HealNanoDisplayName.TryGetValue(healNanoId, out value) ? value : "Heal";
	}

	public static bool TryGetHealingPetNanoPool(string petHash, out int currentNano, out int maxNano)
	{
		currentNano = 0;
		maxNano = 0;
		if (string.IsNullOrWhiteSpace(petHash))
		{
			return false;
		}
		if (!HealingPetNanoPoolByHash.TryGetValue(petHash, out currentNano))
		{
			return false;
		}
		maxNano = currentNano;
		return true;
	}

	public static double GetHealRechargeSeconds(int healNanoId)
	{
		double value;
		return HealRechargeSecondsByNano.TryGetValue(healNanoId, out value) ? value : 9.0;
	}

	public static int GetNanoCastCost(NanoFormula nano)
	{
		if (nano == null)
		{
			return 0;
		}
		int itemAttribute = nano.getItemAttribute(407);
		return (itemAttribute > 0) ? itemAttribute : nano.NCUCost();
	}

	public static bool TryRollHealAmount(NanoFormula nano, ICharacter target, out int healRoll, out int healApplied)
	{
		healRoll = 0;
		healApplied = 0;
		if (nano == null || target == null)
		{
			return false;
		}
		if (!TryGetHealthHitRange(nano, out var minHeal, out var maxHeal))
		{
			return false;
		}
		if (minHeal > maxHeal)
		{
			int num = minHeal;
			minHeal = maxHeal;
			maxHeal = num;
		}
		healRoll = ((minHeal == maxHeal) ? minHeal : new Random().Next(minHeal, maxHeal));
		int num2 = ((IStats)target).Stats[(StatIds)1].Value - ((IStats)target).Stats[(StatIds)27].Value;
		if (num2 <= 0)
		{
			return true;
		}
		healApplied = Math.Min(healRoll, num2);
		return true;
	}

	private static bool TryGetHealthHitRange(NanoFormula nano, out int minHeal, out int maxHeal)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		minHeal = 0;
		maxHeal = 0;
		if (nano.Events == null)
		{
			return false;
		}
		foreach (Event @event in nano.Events)
		{
			if ((int)@event.EventType != 0 || @event.Functions == null)
			{
				continue;
			}
			foreach (Function function in @event.Functions)
			{
				if (function.FunctionType == HitFunctionId && function.Arguments != null && function.Arguments.Values.Count >= 3)
				{
					MessagePackObject val = function.Arguments.Values[0];
					if (((MessagePackObject)(ref val)).AsInt32() == HealthStatId)
					{
						val = function.Arguments.Values[1];
						minHeal = ((MessagePackObject)(ref val)).AsInt32();
						val = function.Arguments.Values[2];
						maxHeal = ((MessagePackObject)(ref val)).AsInt32();
						return true;
					}
				}
			}
		}
		return false;
	}
}
