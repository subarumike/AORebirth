using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Database.Dao;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using AORebirth.Stats.SpecialStats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core;

internal static class CombatXpRuntimeService
{
	private const int MaxLevel = 220;

	private const int MaxRubikaLevel = 200;

	private const int UnsetStatSentinel = 1234567890;

	private const int CapturedMalfunctioningCleaningRobotXp = 260;

	private const byte CapturedLevelUpStatUnknown = 1;

	private const int GreyMobMinLevelAdvantage = 7;

	private const int LevelUpFeedbackCategoryId = 110;

	private const int XpFeedbackMessageId = 249817907;

	private const int CapturedNewLevelUnknown2 = 4;

	private const string XpTracePrefix = "COMBAT_XP_TRACE";

	internal const string XpWireTracePrefix = "XP_WIRE_TRACE";

	private static readonly int[] WireManagedXpStatIds = new int[7] { 52, 54, 57, 334, 350, 372, 592 };

	private const int ShadowLevelStart = 201;

	internal static void RemoveWireManagedStatsFromBulk(Dictionary<int, uint> stats)
	{
		if (stats != null && stats.Count != 0)
		{
			for (int i = 0; i < WireManagedXpStatIds.Length; i++)
			{
				stats.Remove(WireManagedXpStatIds[i]);
			}
		}
	}

	internal static void AwardCombatXp(ICharacter attacker, ICharacter target, Action<ICharacter, string> sendFeedback)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if (attacker == null || target == null)
		{
			return;
		}
		ICharacter val = ResolveXpRecipient(attacker);
		if (val == null || !(((IDynel)val).Controller is PlayerController))
		{
			return;
		}
		IZoneClient client = ((IDynel)val).Controller.Client;
		if (client == null)
		{
			return;
		}
		int num = CalculateCombatXpReward(val, target);
		if (num <= 0)
		{
			LogXpTrace(val, "kill-skip", "reason=zero-reward");
			AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
			return;
		}
		LogXpRewardSource(val, target, num);
		int currentLevel = GetCurrentLevel(val);
		string text = num.ToString(CultureInfo.InvariantCulture);
		Identity identity = ((IEntity)attacker).Identity;
		LogXpTrace(val, "kill-start", "reward=" + text + " sourceAttacker=" + ((object)(Identity)(ref identity)).ToString());
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint barProgress = GetBarProgress(val);
		uint deathXpPool = GetDeathXpPool(val);
		int num2 = 0;
		if (currentLevel < 220 && deathXpPool != 0)
		{
			num2 = (int)(deathXpPool * 5 / 100);
			if (num2 <= 0 && deathXpPool != 0)
			{
				num2 = 1;
			}
			if ((uint)num2 > deathXpPool)
			{
				num2 = (int)deathXpPool;
			}
		}
		int num3 = num + num2;
		uint num4 = AddClamped(barProgress, num3);
		uint num5 = ((deathXpPool > (uint)num2) ? (deathXpPool - (uint)num2) : 0u);
		SetXpStat(val, (StatIds)52, cumulativeXpForLevelStart + num4, "kill-add-cumulative");
		SetXpStat(val, (StatIds)57, (uint)num3, "kill-add-lastxp");
		if (num5 != 0)
		{
			SetXpStat(val, (StatIds)592, num5, "kill-death-pool-remain");
		}
		else
		{
			SetXpStat(val, (StatIds)592, num4, "kill-add-unsaved");
		}
		EnsureLevelXpThresholds(val, "kill-add-thresholds");
		LogXpTrace(val, "kill-after-add", "progressBefore=" + barProgress.ToString(CultureInfo.InvariantCulture) + " progressAfter=" + num4.ToString(CultureInfo.InvariantCulture) + " poolBefore=" + deathXpPool.ToString(CultureInfo.InvariantCulture) + " poolRecover=" + num2.ToString(CultureInfo.InvariantCulture) + " poolAfter=" + num5.ToString(CultureInfo.InvariantCulture) + " cumulative=" + ((IStats)val).Stats[(StatIds)52].BaseValue.ToString(CultureInfo.InvariantCulture));
		bool flag = ApplyPendingLevelUps(val, currentLevel);
		if (sendFeedback != null)
		{
			LogXpTrace(val, "xp-chat-deferred", "source=captured-feedback-message");
		}
		if (flag)
		{
			SendLevelUpPreFeedbackPackets(client, val, currentLevel);
			PersistLevelStat(val);
		}
		else
		{
			if (GetBarProgress(val) >= (uint)GetNextXpRequiredForLevel(currentLevel))
			{
				LogXpTrace(val, "levelup-missed", "reason=progress-met-but-ApplyPendingLevelUps-returned-false progress=" + GetBarProgress(val).ToString(CultureInfo.InvariantCulture) + " required=" + GetNextXpRequiredForLevel(currentLevel).ToString(CultureInfo.InvariantCulture));
			}
			SendNormalKillXpPacket(client, val);
		}
		ClearManualXpWireStatChangedFlags(val, flag);
		WriteXpStatsToDb(val, flag ? "kill-complete-levelup" : "kill-complete");
		AlienXpRuntimeService.AwardAlienXpOnKill(attacker, target);
		if (flag)
		{
			AlienXpRuntimeService.TryApplyBankedAlienLevelUps(val);
		}
		LogXpTrace(val, flag ? "kill-complete-levelup" : "kill-complete", "levelBefore=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " levelAfter=" + GetCurrentLevel(val).ToString(CultureInfo.InvariantCulture) + " leveledUp=" + flag.ToString(CultureInfo.InvariantCulture) + " wire=" + (flag ? "levelup-packets" : "xp-only"));
	}

	internal static int GetXpNeededForNextLevel(ICharacter character)
	{
		if (character == null)
		{
			return 0;
		}
		int currentLevel = GetCurrentLevel(character);
		int nextXpRequiredForLevel = GetNextXpRequiredForLevel(currentLevel);
		if (nextXpRequiredForLevel <= 0)
		{
			return 0;
		}
		uint barProgress = GetBarProgress(character);
		if (barProgress >= (uint)nextXpRequiredForLevel)
		{
			return nextXpRequiredForLevel;
		}
		return nextXpRequiredForLevel - (int)barProgress;
	}

	internal static bool AwardDirectXp(ICharacter character, int xpReward, string source)
	{
		if (character == null || xpReward <= 0 || !(((IDynel)character).Controller is PlayerController))
		{
			return false;
		}
		IZoneClient client = ((IDynel)character).Controller.Client;
		if (client == null)
		{
			return false;
		}
		int currentLevel = GetCurrentLevel(character);
		if (currentLevel >= 200)
		{
			return false;
		}
		string text = (string.IsNullOrWhiteSpace(source) ? "direct" : source.Trim());
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint barProgress = GetBarProgress(character);
		uint num = AddClamped(barProgress, xpReward);
		SetXpStat(character, (StatIds)52, cumulativeXpForLevelStart + num, text + "-add-cumulative");
		SetXpStat(character, (StatIds)57, (uint)xpReward, text + "-add-lastxp");
		SetXpStat(character, (StatIds)592, num, text + "-add-unsaved");
		EnsureLevelXpThresholds(character, text + "-thresholds");
		bool flag = ApplyPendingLevelUps(character, currentLevel);
		if (flag)
		{
			SendLevelUpPreFeedbackPackets(client, character, currentLevel);
			PersistLevelStat(character);
		}
		else
		{
			SendNormalKillXpPacket(client, character);
		}
		ClearManualXpWireStatChangedFlags(character, flag);
		WriteXpStatsToDb(character, flag ? (text + "-levelup") : (text + "-complete"));
		LogXpTrace(character, flag ? (text + "-levelup") : (text + "-complete"), "reward=" + xpReward.ToString(CultureInfo.InvariantCulture) + " levelBefore=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " levelAfter=" + GetCurrentLevel(character).ToString(CultureInfo.InvariantCulture));
		return true;
	}

	internal static void ApplyDeathUninsuredXpLoss(ICharacter character)
	{
		if (character != null && ((IDynel)character).Controller is PlayerController)
		{
			int currentLevel = GetCurrentLevel(character);
			uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
			uint num = NormalizeStatValue(((IStats)character).Stats[(StatIds)52].BaseValue);
			uint num2 = NormalizeStatValue(((IStats)character).Stats[(StatIds)334].BaseValue);
			uint deathXpPool = GetDeathXpPool(character);
			uint num3 = ((num2 > cumulativeXpForLevelStart) ? num2 : cumulativeXpForLevelStart);
			if (num3 > num)
			{
				num3 = num;
			}
			uint num4 = ((num > num3) ? (num - num3) : 0u);
			uint num5 = ((num3 >= cumulativeXpForLevelStart) ? (num3 - cumulativeXpForLevelStart) : 0u);
			uint num6 = deathXpPool;
			if (currentLevel < 220 && num4 != 0)
			{
				num6 = AddClamped(deathXpPool, (int)num4);
			}
			LogXpTrace(character, "death-xp-loss", "level=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " xpBefore=" + num.ToString(CultureInfo.InvariantCulture) + " watermark=" + num2.ToString(CultureInfo.InvariantCulture) + " floor=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture) + " xpAfter=" + num3.ToString(CultureInfo.InvariantCulture) + " lost=" + num4.ToString(CultureInfo.InvariantCulture) + " poolAfter=" + num6.ToString(CultureInfo.InvariantCulture));
			SetXpStat(character, (StatIds)52, num3, "death-xp-loss");
			if (num6 != 0)
			{
				SetXpStat(character, (StatIds)592, num6, "death-xp-pool");
			}
			else
			{
				SetXpStat(character, (StatIds)592, num5, "death-xp-progress");
			}
			IZoneClient client = ((IDynel)character).Controller.Client;
			if (client != null)
			{
				BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 52, num3);
				BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 592, (num6 != 0) ? num6 : num5);
			}
			WriteXpStatsToDb(character, "death-xp-loss");
		}
	}

	internal static uint ApplyInsuranceTerminalSave(ICharacter character, out uint savedSk)
	{
		savedSk = 0u;
		if (character == null || !(((IDynel)character).Controller is PlayerController))
		{
			return 0u;
		}
		uint num = NormalizeStatValue(((IStats)character).Stats[(StatIds)52].BaseValue);
		uint deathXpPool = GetDeathXpPool(character);
		int currentLevel = GetCurrentLevel(character);
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint num2 = ((num >= cumulativeXpForLevelStart) ? (num - cumulativeXpForLevelStart) : 0u);
		SetXpStat(character, (StatIds)334, num, "insurance-save-watermark");
		if (deathXpPool != 0)
		{
			SetXpStat(character, (StatIds)592, deathXpPool, "insurance-save-keep-death-pool");
		}
		else
		{
			SetXpStat(character, (StatIds)592, num2, "insurance-save-unsaved-progress");
		}
		savedSk = NormalizeStatValue(((IStats)character).Stats[(StatIds)573].BaseValue);
		SetXpStat(character, (StatIds)574, savedSk, "insurance-save-lastsk");
		LogXpTrace(character, "insurance-save", "savedXp=" + num.ToString(CultureInfo.InvariantCulture) + " progress=" + num2.ToString(CultureInfo.InvariantCulture) + " deathPool=" + deathXpPool.ToString(CultureInfo.InvariantCulture) + " savedSk=" + savedSk.ToString(CultureInfo.InvariantCulture));
		WriteXpStatsToDb(character, "insurance-save");
		IZoneClient client = ((IDynel)character).Controller.Client;
		if (client != null)
		{
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 334, num);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 592, (deathXpPool != 0) ? deathXpPool : num2);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 574, savedSk);
		}
		ClearManualXpWireStatChangedFlags(character, leveled: false);
		ClearStatChangedFlag(character, (StatIds)574);
		ClearStatChangedFlag(character, (StatIds)573);
		return num;
	}

	internal static string BuildSaveRewardText(int level, uint savedXp, uint savedSk)
	{
		if (level >= 220)
		{
			return "Character stored.";
		}
		if (level >= 201)
		{
			return string.Format(CultureInfo.InvariantCulture, "Character stored. {0} Shadowknowledge saved.", savedSk);
		}
		if (savedSk != 0)
		{
			return string.Format(CultureInfo.InvariantCulture, "Character stored. {0} XP saved. {1} Shadowknowledge saved.", savedXp, savedSk);
		}
		return string.Format(CultureInfo.InvariantCulture, "Character stored. {0} XP saved.", savedXp);
	}

	internal static void PrepareXpStatsForLogin(ICharacter character)
	{
		if (character != null && ((IDynel)character).Controller is PlayerController)
		{
			LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-before");
			int currentLevel = GetCurrentLevel(character);
			NormalizeXpStatsFromPersistedLevel(character);
			int currentLevel2 = GetCurrentLevel(character);
			IController controller = ((IDynel)character).Controller;
			IZoneClient val = ((controller != null) ? controller.Client : null);
			if (currentLevel2 > currentLevel && val != null)
			{
				LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-levelup-wire", "levelBefore=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " levelAfter=" + currentLevel2.ToString(CultureInfo.InvariantCulture));
				SendLevelUpPreFeedbackPackets(val, character, currentLevel);
				PersistLevelStat(character);
			}
			WriteXpStatsToDb(character, "login-complete");
			AlienXpRuntimeService.TryApplyBankedAlienLevelUps(character);
			LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-prepare-after");
		}
	}

	internal static void SendLoginXpBarSync(ICharacter character)
	{
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		if (character != null)
		{
			IController controller = ((IDynel)character).Controller;
			if (((controller != null) ? controller.Client : null) != null)
			{
				IZoneClient client = ((IDynel)character).Controller.Client;
				int currentLevel = GetCurrentLevel(character);
				uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
				uint barProgress = GetBarProgress(character);
				uint num = cumulativeXpForLevelStart + barProgress;
				uint nextXpRequiredForLevel = (uint)GetNextXpRequiredForLevel(currentLevel);
				LogXpWireSnapshot(character, "CombatXpRuntimeService", "login-bar-sync-before", "cumulative=" + num.ToString(CultureInfo.InvariantCulture));
				uint nextLevelXp = ((currentLevel < 200) ? GetCumulativeXpForLevelStart(currentLevel + 1) : 0u);
				NewLevelMessage val = new NewLevelMessage
				{
					Identity = ((IEntity)character).Identity,
					Unknown = 0,
					Level = currentLevel,
					Ip = Math.Max(0, ((IStats)character).Stats[(StatIds)53].Value),
					Xp = (int)num,
					LastSaveXp = (int)cumulativeXpForLevelStart,
					NextLevelXp = (int)nextLevelXp,
					Unknown1 = 0,
					Unknown2 = 4,
					LastXp = Math.Max(0, ((IStats)character).Stats[(StatIds)57].Value)
				};
				LogXpWireNewLevel("CombatXpRuntimeService", "login-bar-sync-newlevel", character, val);
				client.SendCompressed((MessageBody)(object)val);
				SendClientStatWithUnknown(client, character, (CharacterStat)372, cumulativeXpForLevelStart, 1, "login-bar-sync");
				SendSocialStatusReset(client, character);
				SendClientStatWithUnknown(client, character, (CharacterStat)52, num, 0, "login-bar-sync-xp-baseline");
				LogXpTrace(character, "login-bar-sync", "wire=after-fullchar cumulative=" + num.ToString(CultureInfo.InvariantCulture) + " floor=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture) + " progress=" + barProgress.ToString(CultureInfo.InvariantCulture) + " next=" + nextXpRequiredForLevel.ToString(CultureInfo.InvariantCulture) + " level=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " newLevelReplay=true feedback=none");
			}
		}
	}

	internal static void SyncXpBarStatsOnLogin(ICharacter character)
	{
		SendLoginXpBarSync(character);
	}

	private static int CalculateCombatXpReward(ICharacter attacker, ICharacter target)
	{
		int xpReward = ResolveBaseCombatXpReward(target);
		return ApplyGreyMobXpCap(attacker, target, xpReward);
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

	private static int ResolveBaseCombatXpReward(ICharacter target)
	{
		if (CombatTestMobArchetype.TryGetByName(((INamedEntity)target).Name, out var entry) && entry.XpReward > 0)
		{
			return entry.XpReward;
		}
		int baseValue = (int)((IStats)target).Stats[(StatIds)52].BaseValue;
		if (baseValue > 0)
		{
			return baseValue;
		}
		return 260;
	}

	private static int ApplyGreyMobXpCap(ICharacter attacker, ICharacter target, int xpReward)
	{
		int num = Math.Max(1, (int)((IStats)target).Stats[(StatIds)54].BaseValue);
		int currentLevel = GetCurrentLevel(attacker);
		if (currentLevel - num >= 7)
		{
			return 1;
		}
		return xpReward;
	}

	private static bool ApplyPendingLevelUps(ICharacter character, int levelBefore)
	{
		int num = levelBefore;
		int num2 = 0;
		while (num2++ < 20)
		{
			int currentLevel = GetCurrentLevel(character);
			if (currentLevel >= 220)
			{
				break;
			}
			int nextXpRequiredForLevel = GetNextXpRequiredForLevel(currentLevel);
			if (nextXpRequiredForLevel <= 0)
			{
				break;
			}
			uint barProgress = GetBarProgress(character);
			if (barProgress < (uint)nextXpRequiredForLevel)
			{
				LogXpTrace(character, "levelup-skip", "currentLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " progress=" + barProgress.ToString(CultureInfo.InvariantCulture) + " required=" + nextXpRequiredForLevel.ToString(CultureInfo.InvariantCulture));
				break;
			}
			int num3 = currentLevel + 1;
			uint num4 = barProgress - (uint)nextXpRequiredForLevel;
			uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(num3);
			uint deathXpPool = GetDeathXpPool(character);
			LogXpTrace(character, "levelup-apply", "fromLevel=" + currentLevel.ToString(CultureInfo.InvariantCulture) + " toLevel=" + num3.ToString(CultureInfo.InvariantCulture) + " progressBefore=" + barProgress.ToString(CultureInfo.InvariantCulture) + " threshold=" + nextXpRequiredForLevel.ToString(CultureInfo.InvariantCulture) + " remainder=" + num4.ToString(CultureInfo.InvariantCulture) + " newFloor=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture));
			SetXpStat(character, (StatIds)54, (uint)num3, "levelup-apply-level");
			ClearDbManagedFloorStats(character, "levelup-apply");
			SetXpStat(character, (StatIds)350, (uint)GetNextXpRequiredForLevel(num3), "levelup-apply-next");
			SetXpStat(character, (StatIds)52, cumulativeXpForLevelStart + num4, "levelup-apply-cumulative");
			if (deathXpPool != 0)
			{
				SetXpStat(character, (StatIds)592, deathXpPool, "levelup-keep-death-pool");
			}
			else
			{
				SetXpStat(character, (StatIds)592, num4, "levelup-apply-unsaved");
			}
			num = num3;
		}
		if (num <= levelBefore)
		{
			return false;
		}
		character.CalculateSkills();
		int num5 = Math.Max(1, ((IStats)character).Stats[(StatIds)1].Value);
		int num6 = Math.Max(0, ((IStats)character).Stats[(StatIds)221].Value);
		((IStats)character).Stats[(StatIds)27].Set((uint)num5, false);
		((IStats)character).Stats[(StatIds)214].Set((uint)num6, false);
		return true;
	}

	private static void NormalizeXpStatsFromPersistedLevel(ICharacter character)
	{
		int currentLevel = GetCurrentLevel(character);
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint num = NormalizeStatValue(((IStats)character).Stats[(StatIds)52].BaseValue);
		uint num2 = NormalizeStatValue(((IStats)character).Stats[(StatIds)592].BaseValue);
		LogXpTrace(character, "login-normalize-before", "dbXp=" + num.ToString(CultureInfo.InvariantCulture) + " dbUnsaved=" + num2.ToString(CultureInfo.InvariantCulture) + " dbLastsave=" + NormalizeStatValue(((IStats)character).Stats[(StatIds)372].BaseValue).ToString(CultureInfo.InvariantCulture) + " dbSaved=" + NormalizeStatValue(((IStats)character).Stats[(StatIds)334].BaseValue).ToString(CultureInfo.InvariantCulture) + " floor=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture));
		ClearDbManagedFloorStats(character, "login-normalize-clear-db-floor");
		uint num3 = ((num >= cumulativeXpForLevelStart) ? (num - cumulativeXpForLevelStart) : 0u);
		uint num4 = 0u;
		if (currentLevel < 220 && num2 != 0 && num2 != num3)
		{
			num4 = num2;
		}
		uint num5 = num3;
		if (num4 == 0 && num2 != 0 && num2 == num3)
		{
			num5 = num2;
		}
		else if (num4 == 0 && num2 != 0 && num3 == 0 && num == 0)
		{
			num5 = ResolveStoredProgress(currentLevel, cumulativeXpForLevelStart, num, num2);
		}
		SetXpStat(character, (StatIds)52, cumulativeXpForLevelStart + num5, "login-normalize-cumulative");
		if (num4 != 0)
		{
			SetXpStat(character, (StatIds)592, num4, "login-normalize-death-pool");
		}
		else
		{
			SetXpStat(character, (StatIds)592, num5, "login-normalize-unsaved");
		}
		EnsureLevelXpThresholds(character, "login-normalize-thresholds");
		LogXpTrace(character, "login-normalize-after", "resolvedProgress=" + num5.ToString(CultureInfo.InvariantCulture) + " deathPool=" + num4.ToString(CultureInfo.InvariantCulture));
		ApplyPendingLevelUps(character, currentLevel);
	}

	private static uint ResolveStoredProgress(int level, uint floor, uint xp, uint unsavedXp)
	{
		if (unsavedXp != 0)
		{
			return unsavedXp;
		}
		if (xp == 0)
		{
			return 0u;
		}
		if (xp >= floor)
		{
			return xp - floor;
		}
		return xp;
	}

	private static uint NormalizeStatValue(uint value)
	{
		if (value == 1234567890)
		{
			return 0u;
		}
		return value;
	}

	private static uint GetBarProgress(ICharacter character)
	{
		int currentLevel = GetCurrentLevel(character);
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint num = NormalizeStatValue(((IStats)character).Stats[(StatIds)52].BaseValue);
		if (num >= cumulativeXpForLevelStart)
		{
			return num - cumulativeXpForLevelStart;
		}
		uint num2 = NormalizeStatValue(((IStats)character).Stats[(StatIds)592].BaseValue);
		if (num2 != 0 && GetDeathXpPool(character) == 0)
		{
			return num2;
		}
		return num;
	}

	private static uint GetDeathXpPool(ICharacter character)
	{
		if (character == null)
		{
			return 0u;
		}
		int currentLevel = GetCurrentLevel(character);
		if (currentLevel >= 220)
		{
			return 0u;
		}
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint num = NormalizeStatValue(((IStats)character).Stats[(StatIds)52].BaseValue);
		uint num2 = ((num >= cumulativeXpForLevelStart) ? (num - cumulativeXpForLevelStart) : 0u);
		uint num3 = NormalizeStatValue(((IStats)character).Stats[(StatIds)592].BaseValue);
		if (num3 != 0 && num3 != num2)
		{
			return num3;
		}
		return 0u;
	}

	private static void ClearManualXpWireStatChangedFlags(ICharacter character, bool leveled)
	{
		ClearStatChangedFlag(character, (StatIds)52);
		ClearStatChangedFlag(character, (StatIds)54);
		ClearStatChangedFlag(character, (StatIds)57);
		ClearStatChangedFlag(character, (StatIds)334);
		ClearStatChangedFlag(character, (StatIds)350);
		ClearStatChangedFlag(character, (StatIds)372);
		ClearStatChangedFlag(character, (StatIds)592);
		if (leveled)
		{
			ClearStatChangedFlag(character, (StatIds)1);
			ClearStatChangedFlag(character, (StatIds)221);
			ClearStatChangedFlag(character, (StatIds)214);
			ClearStatChangedFlag(character, (StatIds)27);
			ClearStatChangedFlag(character, (StatIds)53);
		}
	}

	private static void ClearStatChangedFlag(ICharacter character, StatIds statId)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected I4, but got Unknown
		((IStats)character).Stats[(int)statId].Changed = false;
	}

	private static void EnsureLevelXpThresholds(ICharacter character, string source)
	{
		int currentLevel = GetCurrentLevel(character);
		ClearDbManagedFloorStats(character, source);
		SetXpStat(character, (StatIds)350, (uint)GetNextXpRequiredForLevel(currentLevel), source + ":next");
	}

	private static void ClearDbManagedFloorStats(ICharacter character, string source)
	{
		SetXpStat(character, (StatIds)372, 0u, source + ":lastsave");
	}

	private static void SendNormalKillXpPacket(IZoneClient client, ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)52].BaseValue;
		LogXpTrace(character, "wire-normal-kill", "stat=52 value=" + baseValue.ToString(CultureInfo.InvariantCulture));
		LogXpWireOutbound("CombatXpRuntimeService", "kill-xp-stat", character, 52, baseValue, "StatMessage", "unknown=0");
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 52, baseValue);
		LogXpWireFeedbackOutbound("CombatXpRuntimeService", "kill-xp-feedback", character, 110, 249817907);
		BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(character, 110, 249817907);
	}

	private static void SendSocialStatusReset(IZoneClient client, ICharacter character)
	{
		SendClientStatWithUnknown(client, character, (CharacterStat)521, 0u, 1);
	}

	private static uint GetCumulativeXpForLevelStart(int level)
	{
		if (level <= 1)
		{
			return 0u;
		}
		if (level > 200)
		{
			return (uint)XPTable.TableRKXP[199, 1];
		}
		return (uint)XPTable.TableRKXP[level - 1, 1];
	}

	private static int GetNextXpRequiredForLevel(int level)
	{
		if (level < 1 || level >= 200)
		{
			return 0;
		}
		return (int)XPTable.TableRKXP[level - 1, 2];
	}

	private static void SendLevelUpPreFeedbackPackets(IZoneClient client, ICharacter character, int levelBefore)
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		int statValue = Math.Max(1, ((IStats)character).Stats[(StatIds)1].Value);
		int statValue2 = Math.Max(0, ((IStats)character).Stats[(StatIds)221].Value);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 1, (uint)statValue);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 221, (uint)statValue2);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 214, (uint)statValue2);
		int currentLevel = GetCurrentLevel(character);
		for (int i = levelBefore + 1; i <= currentLevel; i++)
		{
			uint baseValue = ((IStats)character).Stats[(StatIds)52].BaseValue;
			uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(i);
			uint nextLevelXp = ((i < 200) ? GetCumulativeXpForLevelStart(i + 1) : 0u);
			NewLevelMessage val = new NewLevelMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 0,
				Level = i,
				Ip = Math.Max(0, ((IStats)character).Stats[(StatIds)53].Value),
				Xp = (int)baseValue,
				LastSaveXp = (int)cumulativeXpForLevelStart,
				NextLevelXp = (int)nextLevelXp,
				Unknown1 = 0,
				Unknown2 = 4,
				LastXp = Math.Max(0, ((IStats)character).Stats[(StatIds)57].Value)
			};
			LogXpWireNewLevel("CombatXpRuntimeService", "levelup-newlevel", character, val);
			client.SendCompressed((MessageBody)(object)val);
			LogXpTrace(character, "wire-newlevel", "level=" + i.ToString(CultureInfo.InvariantCulture) + " ip=" + ((IStats)character).Stats[(StatIds)53].Value.ToString(CultureInfo.InvariantCulture) + " xp=" + baseValue.ToString(CultureInfo.InvariantCulture) + " lastSaveXp=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture) + " nextLevelXp=" + nextLevelXp.ToString(CultureInfo.InvariantCulture) + " lastXp=" + ((IStats)character).Stats[(StatIds)57].Value.ToString(CultureInfo.InvariantCulture) + " index=" + (i - levelBefore).ToString(CultureInfo.InvariantCulture) + " totalGained=" + (currentLevel - levelBefore).ToString(CultureInfo.InvariantCulture));
		}
		SendLevelUpXpWireSync(client, character);
	}

	private static void PersistLevelStat(ICharacter character)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		int currentLevel = GetCurrentLevel(character);
		Identity identity = ((IEntity)character).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		DBStats val = ((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).GetAll((object)new
		{
			Type = 50000,
			Instance = instance,
			StatId = 54
		}).FirstOrDefault();
		if (val == null)
		{
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Add(new DBStats
			{
				Type = 50000,
				Instance = instance,
				StatId = 54,
				StatValue = currentLevel
			}, (IDbConnection)null, (IDbTransaction)null, true);
		}
		else
		{
			val.StatValue = currentLevel;
			((Dao<DBStats, StatDao>)(object)Dao<DBStats, StatDao>.Instance).Save(val, (object)null, (IDbConnection)null, (IDbTransaction)null);
		}
		LogXpTrace(character, "db-level-persist", "stat54=" + currentLevel.ToString(CultureInfo.InvariantCulture));
	}

	private static void SendLevelUpXpWireSync(IZoneClient client, ICharacter character)
	{
		int currentLevel = GetCurrentLevel(character);
		uint cumulativeXpForLevelStart = GetCumulativeXpForLevelStart(currentLevel);
		uint barProgress = GetBarProgress(character);
		uint baseValue = ((IStats)character).Stats[(StatIds)52].BaseValue;
		uint num = ((currentLevel < 200) ? GetCumulativeXpForLevelStart(currentLevel + 1) : 0u);
		LogXpTrace(character, "wire-levelup", "lastsave372=" + cumulativeXpForLevelStart.ToString(CultureInfo.InvariantCulture) + " xp52wire=" + baseValue.ToString(CultureInfo.InvariantCulture) + " unsaved592=" + barProgress.ToString(CultureInfo.InvariantCulture) + " nextCumulative=" + num.ToString(CultureInfo.InvariantCulture) + " xp52db=" + ((IStats)character).Stats[(StatIds)52].BaseValue.ToString(CultureInfo.InvariantCulture));
		SendClientStatWithUnknown(client, character, (CharacterStat)372, cumulativeXpForLevelStart, 1, "levelup-wire-sync");
		SendSocialStatusReset(client, character);
		LogXpWireOutbound("CombatXpRuntimeService", "levelup-xp-stat", character, 52, baseValue, "StatMessage", "unknown=0");
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 52, baseValue);
		LogXpWireFeedbackOutbound("CombatXpRuntimeService", "levelup-xp-feedback", character, 110, 249817907);
		BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(character, 110, 249817907);
	}

	private static void SendClientStatWithUnknown(IZoneClient client, ICharacter character, CharacterStat stat, uint value, byte unknown, string stage = "stat-unknown")
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected I4, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		LogXpWireOutbound("CombatXpRuntimeService", stage, character, (int)stat, value, "StatMessage", "unknown=" + unknown.ToString(CultureInfo.InvariantCulture));
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)character).Identity;
		((N3Message)val).Unknown = unknown;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = stat,
				Value2 = value
			}
		};
		client.SendCompressed((MessageBody)(object)val);
	}

	private static int GetCurrentLevel(ICharacter character)
	{
		uint baseValue = ((IStats)character).Stats[(StatIds)54].BaseValue;
		if (baseValue == 1234567890 || baseValue == 0 || baseValue > 220)
		{
			return 1;
		}
		return (int)baseValue;
	}

	private static uint AddClamped(uint value, int delta)
	{
		uint num = (uint)Math.Max(0, delta);
		if (value > (uint)(-1 - (int)num))
		{
			return uint.MaxValue;
		}
		return value + num;
	}

	private static void LogXpRewardSource(ICharacter attacker, ICharacter target, int xpReward)
	{
		string text;
		if (CombatTestMobArchetype.TryGetByName(((INamedEntity)target).Name, out var entry) && entry.XpReward > 0)
		{
			text = "archetype:" + ((INamedEntity)target).Name + "=" + entry.XpReward.ToString(CultureInfo.InvariantCulture);
		}
		else
		{
			int baseValue = (int)((IStats)target).Stats[(StatIds)52].BaseValue;
			text = ((baseValue <= 0) ? ("fallback-robot:" + 260.ToString(CultureInfo.InvariantCulture)) : ("target-stat-xp:" + baseValue.ToString(CultureInfo.InvariantCulture)));
		}
		int currentLevel = GetCurrentLevel(attacker);
		int num = Math.Max(1, (int)((IStats)target).Stats[(StatIds)54].BaseValue);
		text = ((currentLevel - num < 7) ? (text + " final=" + xpReward.ToString(CultureInfo.InvariantCulture)) : (text + " grey-cap-applied final=" + xpReward.ToString(CultureInfo.InvariantCulture)));
		LogXpTrace(attacker, "reward-source", text + " target=\"" + (((INamedEntity)target).Name ?? string.Empty) + "\"");
	}

	private static void SetXpStat(ICharacter character, StatIds statId, uint newValue, string source)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			uint num = NormalizeStatValue(((IStats)character).Stats[statId].BaseValue);
			((IStats)character).Stats[statId].Set(newValue, false);
			if (num != newValue)
			{
				LogXpStatChange(character, source, statId, num, newValue);
			}
		}
	}

	private static void LogXpStatChange(ICharacter character, string source, StatIds statId, uint before, uint after)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected I4, but got Unknown
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} stage=stat-set source={1} char={2} stat={3} statId={4} before={5} after={6} delta={7}", "COMBAT_XP_TRACE", source, ((IEntity)character).Identity, GetXpStatName(statId), (int)statId, before, after, (long)after - (long)before));
	}

	private static void WriteXpStatsToDb(ICharacter character, string source)
	{
		if (character != null)
		{
			LogXpTrace(character, "db-write-before", "source=" + source + " snapshot=" + BuildXpStatSnapshot(character));
			((IDatabaseObject)((IStats)character).Stats).Write();
			LogXpTrace(character, "db-write-after", "source=" + source + " persisted=true");
		}
	}

	private static string BuildXpStatSnapshot(ICharacter character)
	{
		return string.Format(CultureInfo.InvariantCulture, "level54={0} xp52={1} unsaved592={2} lastsave372={3} saved334={4} next350={5} lastxp57={6}", ((IStats)character).Stats[(StatIds)54].BaseValue, ((IStats)character).Stats[(StatIds)52].BaseValue, ((IStats)character).Stats[(StatIds)592].BaseValue, ((IStats)character).Stats[(StatIds)372].BaseValue, ((IStats)character).Stats[(StatIds)334].BaseValue, ((IStats)character).Stats[(StatIds)350].BaseValue, ((IStats)character).Stats[(StatIds)57].BaseValue);
	}

	private static string GetXpStatName(StatIds statId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Invalid comparison between Unknown and I4
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected I4, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Invalid comparison between Unknown and I4
		StatIds val = statId;
		StatIds val2 = val;
		if ((int)val2 <= 334)
		{
			switch (val2 - 52)
			{
			default:
				if ((int)val2 != 334)
				{
					break;
				}
				return "SavedXP";
			case 0:
				return "XP";
			case 1:
				return "IP";
			case 2:
				return "Level";
			case 5:
				return "LastXP";
			case 3:
			case 4:
				break;
			}
		}
		else
		{
			if ((int)val2 == 350)
			{
				return "NextXP";
			}
			if ((int)val2 == 372)
			{
				return "LastSaveXP";
			}
			if ((int)val2 == 592)
			{
				return "UnsavedXP";
			}
		}
		return ((object)(StatIds)(ref statId)).ToString();
	}

	private static void LogXpTrace(ICharacter character, string stage, string details)
	{
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			int currentLevel = GetCurrentLevel(character);
			uint baseValue = ((IStats)character).Stats[(StatIds)54].BaseValue;
			uint baseValue2 = ((IStats)character).Stats[(StatIds)52].BaseValue;
			uint baseValue3 = ((IStats)character).Stats[(StatIds)592].BaseValue;
			uint baseValue4 = ((IStats)character).Stats[(StatIds)372].BaseValue;
			uint baseValue5 = ((IStats)character).Stats[(StatIds)334].BaseValue;
			uint baseValue6 = ((IStats)character).Stats[(StatIds)350].BaseValue;
			uint baseValue7 = ((IStats)character).Stats[(StatIds)57].BaseValue;
			uint barProgress = GetBarProgress(character);
			int nextXpRequiredForLevel = GetNextXpRequiredForLevel(currentLevel);
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} stage={1} char={2} level54={3} levelRaw={4} xp52={5} unsaved592={6} lastsave372={7} saved334={8} next350={9} lastxp57={10} progress={11} nextRequired={12} bar={13}/{14} {15}", "COMBAT_XP_TRACE", stage, ((IEntity)character).Identity, currentLevel, baseValue, baseValue2, baseValue3, baseValue4, baseValue5, baseValue6, baseValue7, barProgress, nextXpRequiredForLevel, barProgress, nextXpRequiredForLevel, details ?? string.Empty));
		}
	}

	internal static bool IsXpWireStatId(int statId)
	{
		return statId == 52 || statId == 53 || statId == 54 || statId == 57 || statId == 334 || statId == 350 || statId == 372 || statId == 592;
	}

	internal static bool IsXpFeedbackMessage(int categoryId, int messageId)
	{
		return categoryId == 110 && messageId == 249817907;
	}

	internal static void LogXpWireOutbound(string source, string stage, ICharacter character, int statId, uint value, string wireKind, string details = "")
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && IsXpWireStatId(statId))
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} source={1} stage={2} char={3} wire={4} statId={5} stat={6} value={7} {8}", "XP_WIRE_TRACE", source ?? string.Empty, stage ?? string.Empty, ((IEntity)character).Identity, wireKind ?? string.Empty, statId, GetXpStatName((StatIds)statId), value, details ?? string.Empty));
		}
	}

	internal static void LogXpWireFeedbackOutbound(string source, string stage, ICharacter character, int categoryId, int messageId, string details = "")
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && IsXpFeedbackMessage(categoryId, messageId))
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} source={1} stage={2} char={3} wire=FeedbackMessage category={4} messageId={5} {6}", "XP_WIRE_TRACE", source ?? string.Empty, stage ?? string.Empty, ((IEntity)character).Identity, categoryId, messageId, details ?? string.Empty));
		}
	}

	internal static void LogXpWireNewLevel(string source, string stage, ICharacter character, NewLevelMessage message)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && message != null)
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} source={1} stage={2} char={3} wire=NewLevel level={4} ip={5} xp={6} lastSaveXp={7} nextLevelXp={8} lastXp={9}", "XP_WIRE_TRACE", source ?? string.Empty, stage ?? string.Empty, ((IEntity)character).Identity, message.Level, message.Ip, message.Xp, message.LastSaveXp, message.NextLevelXp, message.LastXp));
		}
	}

	internal static void LogXpWireSnapshot(ICharacter character, string source, string stage, string details = "")
	{
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		if (character != null)
		{
			int currentLevel = GetCurrentLevel(character);
			uint baseValue = ((IStats)character).Stats[(StatIds)54].BaseValue;
			uint baseValue2 = ((IStats)character).Stats[(StatIds)52].BaseValue;
			uint baseValue3 = ((IStats)character).Stats[(StatIds)592].BaseValue;
			uint baseValue4 = ((IStats)character).Stats[(StatIds)372].BaseValue;
			uint baseValue5 = ((IStats)character).Stats[(StatIds)334].BaseValue;
			uint baseValue6 = ((IStats)character).Stats[(StatIds)350].BaseValue;
			uint baseValue7 = ((IStats)character).Stats[(StatIds)57].BaseValue;
			uint barProgress = GetBarProgress(character);
			int nextXpRequiredForLevel = GetNextXpRequiredForLevel(currentLevel);
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "{0} source={1} stage={2} char={3} level54={4} levelRaw={5} xp52={6} unsaved592={7} lastsave372={8} saved334={9} next350={10} lastxp57={11} progress={12} nextRequired={13} bar={14}/{15} {16}", "XP_WIRE_TRACE", source ?? string.Empty, stage ?? string.Empty, ((IEntity)character).Identity, currentLevel, baseValue, baseValue2, baseValue3, baseValue4, baseValue5, baseValue6, baseValue7, barProgress, nextXpRequiredForLevel, barProgress, nextXpRequiredForLevel, details ?? string.Empty));
		}
	}
}
