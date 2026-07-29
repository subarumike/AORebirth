using System;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using AORebirth.Stats.SpecialStats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core;

internal static class AlienXpRuntimeService
{
	public const int AlienMobFlagsBit = 16384;

	private const int MaxAlienLevel = 30;

	private const int GreyMobMinLevelAdvantage = 7;

	private const uint UnsetStatSentinel = 1234567890u;

	private const int AlienSpiderTestAixpReward = 5000;

	private static readonly int[] MinRubikaLevelForAlienLevel = new int[31]
	{
		0, 5, 15, 25, 35, 45, 55, 65, 75, 85,
		95, 105, 110, 115, 120, 125, 130, 135, 140, 145,
		150, 155, 160, 165, 170, 175, 180, 185, 190, 195,
		200
	};

	internal static void AwardAlienXpOnKill(ICharacter attacker, ICharacter target)
	{
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || target == null || !IsAlienTarget(target))
		{
			return;
		}
		ICharacter val = ResolveXpRecipient(attacker);
		if (val == null || !(((IDynel)val).Controller is PlayerController) || ((IDynel)val).Controller.Client == null)
		{
			return;
		}
		uint progress = GetAlienBarProgress(val);
		ClampProgressBehindRkGate(val, ref progress);
		int blockedByRkLevel;
		bool flag = ApplyPendingAlienLevelUps(val, ref progress, out blockedByRkLevel);
		int num = CalculateAlienXpReward(val, target);
		int num2 = CapRewardToBarRoom(val, progress, num);
		if (num2 <= 0 && !flag)
		{
			IncrementInvadersKilled(val);
			if (IsBarFullBehindRkGate(val, progress) && TryGetBlockedNextAlienRkGate(val, out var requiredRk))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, string.Format(CultureInfo.InvariantCulture, "Alien XP bar is full. Reach Rubi-Ka level {0} to advance.", requiredRk), 0, 0);
			}
			((IDatabaseObject)((IStats)val).Stats).Write();
			return;
		}
		int alienLevel = GetAlienLevel(val);
		if (num2 > 0)
		{
			progress = AddClamped(progress, (uint)num2);
		}
		bool flag2 = ApplyPendingAlienLevelUps(val, ref progress, out blockedByRkLevel) || flag;
		ClampProgressBehindRkGate(val, ref progress);
		SetAlienStat(val, (StatIds)40, progress);
		EnsureAlienNextXp(val);
		IncrementInvadersKilled(val);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(val, 40, progress);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(val, 178, (uint)Math.Max(0, ((IStats)val).Stats[(StatIds)178].Value));
		if (flag2)
		{
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(val, 169, (uint)GetAlienLevel(val));
		}
		if (num2 > 0)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, string.Format(CultureInfo.InvariantCulture, "You gained {0} new Alien Experience Points.", num2), 0, 0);
		}
		if (flag2)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, string.Format(CultureInfo.InvariantCulture, "You have advanced to Alien Level {0}.", GetAlienLevel(val)), 0, 0);
		}
		else if (blockedByRkLevel > 0 && IsBarFullBehindRkGate(val, progress))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, string.Format(CultureInfo.InvariantCulture, "Alien XP bar is full. Reach Rubi-Ka level {0} to advance to Alien Level {1}.", blockedByRkLevel, GetAlienLevel(val) + 1), 0, 0);
		}
		((IDatabaseObject)((IStats)val).Stats).Write();
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		object[] array = new object[8];
		Identity identity = ((IEntity)val).Identity;
		array[0] = ((Identity)(ref identity)).ToString(true);
		array[1] = ((INamedEntity)target).Name ?? string.Empty;
		array[2] = num;
		array[3] = num2;
		array[4] = alienLevel;
		array[5] = GetAlienLevel(val);
		array[6] = progress;
		array[7] = flag2;
		LogUtil.Debug((DebugInfoDetail)512, string.Format(invariantCulture, "AIXP kill char={0} target={1} reward={2} awarded={3} alienLevel={4}->{5} progress={6} leveledUp={7}", array));
	}

	internal static void RecordPlayerKilledByInvader(ICharacter killer, ICharacter deadPlayer)
	{
		if (deadPlayer != null && ((IDynel)deadPlayer).Controller is PlayerController && killer != null && IsAlienTarget(killer))
		{
			int num = Math.Max(0, ((IStats)deadPlayer).Stats[(StatIds)616].Value) + 1;
			SetAlienStat(deadPlayer, (StatIds)616, (uint)num);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(deadPlayer, 616, (uint)num);
			((IDatabaseObject)((IStats)deadPlayer).Stats).Write();
		}
	}

	internal static void TryApplyBankedAlienLevelUps(ICharacter character)
	{
		if (character != null && ((IDynel)character).Controller is PlayerController && ((IDynel)character).Controller.Client != null)
		{
			uint progress = GetAlienBarProgress(character);
			ClampProgressBehindRkGate(character, ref progress);
			int blockedByRkLevel;
			bool flag = ApplyPendingAlienLevelUps(character, ref progress, out blockedByRkLevel);
			ClampProgressBehindRkGate(character, ref progress);
			if (!flag)
			{
				SetAlienStat(character, (StatIds)40, progress);
				EnsureAlienNextXp(character);
				return;
			}
			SetAlienStat(character, (StatIds)40, progress);
			EnsureAlienNextXp(character);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 40, progress);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 178, (uint)Math.Max(0, ((IStats)character).Stats[(StatIds)178].Value));
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 169, (uint)GetAlienLevel(character));
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, string.Format(CultureInfo.InvariantCulture, "You have advanced to Alien Level {0}.", GetAlienLevel(character)), 0, 0);
			((IDatabaseObject)((IStats)character).Stats).Write();
		}
	}

	internal static bool IsAlienTarget(ICharacter target)
	{
		if (target == null || ((IDynel)target).Controller is PlayerController)
		{
			return false;
		}
		int value = ((IStats)target).Stats[(StatIds)0].Value;
		if (((uint)value & 0x4000u) != 0)
		{
			return true;
		}
		CombatTestMobArchetype.Entry entry;
		return CombatTestMobArchetype.TryGetByName(((INamedEntity)target).Name, out entry) && entry == CombatTestMobArchetype.AlienSpiderZix;
	}

	private static bool IsAlienSpiderTestMob(ICharacter target)
	{
		CombatTestMobArchetype.Entry entry;
		return CombatTestMobArchetype.TryGetByName(((INamedEntity)target).Name, out entry) && entry == CombatTestMobArchetype.AlienSpiderZix;
	}

	private static int CalculateAlienXpReward(ICharacter attacker, ICharacter target)
	{
		if (IsAlienSpiderTestMob(target))
		{
			return 5000;
		}
		int num = Math.Max(1, (int)((IStats)target).Stats[(StatIds)54].BaseValue);
		int num2 = Math.Max(1, (int)((IStats)attacker).Stats[(StatIds)54].BaseValue);
		if (num2 - num >= 7)
		{
			return 1;
		}
		int num3 = Math.Max(10, num * 25);
		if (CombatTestMobArchetype.TryGetByName(((INamedEntity)target).Name, out var entry) && entry.XpReward > 0)
		{
			num3 = Math.Max(num3, entry.XpReward);
		}
		int baseValue = (int)((IStats)target).Stats[(StatIds)52].BaseValue;
		if (baseValue > 0)
		{
			num3 = Math.Max(num3, baseValue);
		}
		return num3;
	}

	private static int CapRewardToBarRoom(ICharacter character, uint progress, int reward)
	{
		if (reward <= 0)
		{
			return 0;
		}
		if (!TryGetFillCapWhileRkBlocked(character, out var fillCap))
		{
			return reward;
		}
		if (progress >= (uint)fillCap)
		{
			return 0;
		}
		uint val = (uint)fillCap - progress;
		return (int)Math.Min((uint)reward, val);
	}

	private static bool TryGetFillCapWhileRkBlocked(ICharacter character, out int fillCap)
	{
		fillCap = 0;
		int alienLevel = GetAlienLevel(character);
		if (alienLevel >= 30)
		{
			return false;
		}
		int num = Math.Max(1, (int)((IStats)character).Stats[(StatIds)54].BaseValue);
		int num2 = alienLevel + 1;
		int num3 = MinRubikaLevelForAlienLevel[num2];
		if (num >= num3)
		{
			return false;
		}
		fillCap = GetNextAlienXpRequiredForLevel(alienLevel);
		return fillCap > 0;
	}

	private static bool TryGetBlockedNextAlienRkGate(ICharacter character, out int requiredRk)
	{
		requiredRk = 0;
		int alienLevel = GetAlienLevel(character);
		if (alienLevel >= 30)
		{
			return false;
		}
		int num = Math.Max(1, (int)((IStats)character).Stats[(StatIds)54].BaseValue);
		int num2 = alienLevel + 1;
		requiredRk = MinRubikaLevelForAlienLevel[num2];
		return num < requiredRk;
	}

	private static bool IsBarFullBehindRkGate(ICharacter character, uint progress)
	{
		int fillCap;
		return TryGetFillCapWhileRkBlocked(character, out fillCap) && fillCap > 0 && progress >= (uint)fillCap;
	}

	private static void ClampProgressBehindRkGate(ICharacter character, ref uint progress)
	{
		if (TryGetFillCapWhileRkBlocked(character, out var fillCap) && progress > (uint)fillCap)
		{
			progress = (uint)fillCap;
			SetAlienStat(character, (StatIds)40, progress);
		}
	}

	private static bool ApplyPendingAlienLevelUps(ICharacter character, ref uint progress, out int blockedByRkLevel)
	{
		bool result = false;
		int num = 0;
		int num2 = Math.Max(1, (int)((IStats)character).Stats[(StatIds)54].BaseValue);
		blockedByRkLevel = 0;
		while (num++ < 30)
		{
			int alienLevel = GetAlienLevel(character);
			if (alienLevel >= 30)
			{
				break;
			}
			int num3 = alienLevel + 1;
			int num4 = MinRubikaLevelForAlienLevel[num3];
			if (num2 < num4)
			{
				int nextAlienXpRequiredForLevel = GetNextAlienXpRequiredForLevel(alienLevel);
				if (nextAlienXpRequiredForLevel > 0 && progress >= (uint)nextAlienXpRequiredForLevel)
				{
					blockedByRkLevel = num4;
				}
				break;
			}
			int nextAlienXpRequiredForLevel2 = GetNextAlienXpRequiredForLevel(alienLevel);
			if (nextAlienXpRequiredForLevel2 <= 0 || progress < (uint)nextAlienXpRequiredForLevel2)
			{
				break;
			}
			progress -= (uint)nextAlienXpRequiredForLevel2;
			SetAlienStat(character, (StatIds)169, (uint)num3);
			result = true;
		}
		SetAlienStat(character, (StatIds)40, progress);
		return result;
	}

	private static void IncrementInvadersKilled(ICharacter recipient)
	{
		int num = Math.Max(0, ((IStats)recipient).Stats[(StatIds)615].Value) + 1;
		SetAlienStat(recipient, (StatIds)615, (uint)num);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(recipient, 615, (uint)num);
	}

	private static uint GetAlienBarProgress(ICharacter character)
	{
		return NormalizeStatValue(GetAlienXp(character));
	}

	private static int GetNextAlienXpRequiredForLevel(int alienLevel)
	{
		if (alienLevel < 0 || alienLevel >= 30)
		{
			return 0;
		}
		return Convert.ToInt32(XPTable.TableAlienXP[alienLevel, 2]);
	}

	private static void EnsureAlienNextXp(ICharacter character)
	{
		int alienLevel = GetAlienLevel(character);
		uint nextAlienXpRequiredForLevel = (uint)GetNextAlienXpRequiredForLevel(alienLevel);
		SetAlienStat(character, (StatIds)178, nextAlienXpRequiredForLevel);
	}

	private static ICharacter ResolveXpRecipient(ICharacter attacker)
	{
		if (attacker == null)
		{
			return null;
		}
		if (((IDynel)attacker).Controller is PlayerController)
		{
			return attacker;
		}
		if (PetCombatRules.IsPlayerOwnedPet(attacker))
		{
			return PetCombatRules.ResolvePetOwner(attacker);
		}
		return null;
	}

	private static int GetAlienLevel(ICharacter character)
	{
		int value = ((IStats)character).Stats[(StatIds)169].Value;
		if (value < 0)
		{
			return 0;
		}
		if (value > 30)
		{
			return 30;
		}
		return value;
	}

	private static uint GetAlienXp(ICharacter character)
	{
		return NormalizeStatValue(((IStats)character).Stats[(StatIds)40].BaseValue);
	}

	private static void SetAlienStat(ICharacter character, StatIds statId, uint newValue)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			((IStats)character).Stats[statId].Set(newValue, false);
		}
	}

	private static uint AddClamped(uint value, uint delta)
	{
		if (value > (uint)(-1 - (int)delta))
		{
			return uint.MaxValue;
		}
		return value + delta;
	}

	private static uint NormalizeStatValue(uint value)
	{
		return (value != 1234567890) ? value : 0u;
	}
}
