using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Functions.GameFunctions;

namespace ZoneEngine.Core;

internal static class MongoSlamRuntimeService
{
	internal const int UploadedMongoSlamNanoId = 287046;

	internal const int MongoSlamEffectNanoId = 100198;

	internal const int MongoSlamNestedTauntNanoId = 100194;

	internal const int MongoSlamStrain = 51;

	internal const float SlamRadiusMeters = 20f;

	internal const int SelfHealAmount = 12;

	private static readonly TimeSpan HotTickInterval = TimeSpan.FromSeconds(10.0);

	private static readonly ConcurrentDictionary<int, DateTime> NextHotTickUtc = new ConcurrentDictionary<int, DateTime>();

	internal static bool IsMongoSlamNano(int nanoId)
	{
		return nanoId == 287046 || nanoId == 100198;
	}

	internal static void ApplyCaptureBackedSlamEffects(Character caster, int castNanoId)
	{
		if (caster != null)
		{
			switch (castNanoId)
			{
			case 287046:
				ApplySlamEffectNano(caster);
				BeginHotWhileProgramActive(caster);
				break;
			case 100198:
				BeginHotWhileProgramActive(caster);
				break;
			}
		}
	}

	internal static bool ProcessHotTick(ICharacter character)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((character is Character) ? character : null);
		if (val == null || ((Dynel)val).Controller == null || !(((Dynel)val).Controller is PlayerController))
		{
			return false;
		}
		Identity identity;
		if (!IsMongoSlamProgramActive((ICharacter)(object)val))
		{
			ConcurrentDictionary<int, DateTime> nextHotTickUtc = NextHotTickUtc;
			identity = ((PooledObject)val).Identity;
			nextHotTickUtc.TryRemove(((Identity)(ref identity)).Instance, out var _);
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		ConcurrentDictionary<int, DateTime> nextHotTickUtc2 = NextHotTickUtc;
		identity = ((PooledObject)val).Identity;
		if (!nextHotTickUtc2.TryGetValue(((Identity)(ref identity)).Instance, out var value2))
		{
			ConcurrentDictionary<int, DateTime> nextHotTickUtc3 = NextHotTickUtc;
			identity = ((PooledObject)val).Identity;
			nextHotTickUtc3[((Identity)(ref identity)).Instance] = utcNow + HotTickInterval;
			return false;
		}
		if (utcNow < value2)
		{
			return false;
		}
		ConcurrentDictionary<int, DateTime> nextHotTickUtc4 = NextHotTickUtc;
		identity = ((PooledObject)val).Identity;
		nextHotTickUtc4[((Identity)(ref identity)).Instance] = utcNow + HotTickInterval;
		ApplySelfHeal(val, 12);
		return true;
	}

	private static void ApplySlamEffectNano(Character caster)
	{
		if (NanoLoader.NanoList.TryGetValue(100198, out var value) && value != null && value.Events != null && value.Events.Count > 0)
		{
			NanoEventRuntimeService.Default.ExecuteOnUseEvents((ICharacter)(object)caster, value);
			LogUtil.Debug((DebugInfoDetail)256, "MongoSlam applied dat effect nano=" + 100198);
		}
		else
		{
			ApplyFallbackHealAndTauntAoe(caster);
		}
	}

	internal static void BeginHotWhileProgramActive(Character caster)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		if (caster != null)
		{
			ConcurrentDictionary<int, DateTime> nextHotTickUtc = NextHotTickUtc;
			Identity identity = ((PooledObject)caster).Identity;
			nextHotTickUtc[((Identity)(ref identity)).Instance] = DateTime.UtcNow + HotTickInterval;
		}
	}

	private static bool IsMongoSlamProgramActive(ICharacter caster)
	{
		if (caster == null || caster.ActiveNanos == null)
		{
			return false;
		}
		if (caster.ActiveNanos.TryGetValue(51, out var value) && value != null)
		{
			return value.ID == 287046;
		}
		return ActiveNanoRuntimeService.Default.HasActiveNanoInStrain(caster, 287046, 51);
	}

	private static void ApplyFallbackHealAndTauntAoe(Character caster)
	{
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		ApplySelfHeal(caster, 12);
		if (!(((Dynel)caster).Playfield is Playfield playfield))
		{
			return;
		}
		IList<ICharacter> list = playfield.FindCharacterInRange((IDynel)(object)caster, 20f);
		int num = 0;
		foreach (ICharacter item in list)
		{
			Character val = (Character)(object)((item is Character) ? item : null);
			if (val != null && val != caster && (((Dynel)val).Stats[(StatIds)455].BaseValue != 0 || ((Dynel)val).Stats[(StatIds)359].BaseValue != 0 || ((Dynel)val).Controller is NPCController || (PlayerVersusPlayerCombatRules.IsProtectedPlayerVersusPlayerTarget((ICharacter)(object)val) && PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat((ICharacter)(object)caster, (ICharacter)(object)val))))
			{
				MessagePackObject[] arguments = (MessagePackObject[])(object)new MessagePackObject[1] { MessagePackObject.op_Implicit(4000) };
				if (new tauntnpc().Execute((INamedEntity)(object)caster, (IEntity)(object)caster, (IInstancedEntity)(object)val, arguments))
				{
					num++;
				}
			}
		}
		Identity identity = ((PooledObject)caster).Identity;
		LogUtil.Debug((DebugInfoDetail)256, "MongoSlam fallback heal+tauntAoE caster=" + ((object)(Identity)(ref identity)).ToString() + " hits=" + num);
	}

	private static void ApplySelfHeal(Character caster, int amount)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (caster != null && amount > 0)
		{
			MessagePackObject[] arguments = (MessagePackObject[])(object)new MessagePackObject[4]
			{
				MessagePackObject.op_Implicit(27),
				MessagePackObject.op_Implicit(amount),
				MessagePackObject.op_Implicit(amount),
				MessagePackObject.op_Implicit(0)
			};
			new hit().Execute((INamedEntity)(object)caster, (IEntity)(object)caster, (IInstancedEntity)(object)caster, arguments);
		}
	}
}
