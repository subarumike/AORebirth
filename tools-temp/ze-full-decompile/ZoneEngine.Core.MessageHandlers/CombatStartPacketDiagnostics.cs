using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.MessageHandlers;

internal static class CombatStartPacketDiagnostics
{
	private const string EnvironmentVariableName = "AOREBIRTH_COMBAT_PACKET_DIAGNOSTICS";

	private const string FlagFileName = "combat-packet-diagnostics.enabled";

	private static readonly TimeSpan CombatStartWindow = TimeSpan.FromSeconds(6.0);

	private static DateTime diagnosticWindowUntilUtc = DateTime.MinValue;

	internal static bool Enabled
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("AOREBIRTH_COMBAT_PACKET_DIAGNOSTICS");
			if (IsEnabledValue(environmentVariable))
			{
				return true;
			}
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			if (string.IsNullOrEmpty(baseDirectory))
			{
				return false;
			}
			return File.Exists(Path.Combine(baseDirectory, "combat-packet-diagnostics.enabled"));
		}
	}

	internal static void LogAttackCommand(ICharacter character, Identity target, byte action, ICharacter resolvedTarget)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		if (Enabled)
		{
			diagnosticWindowUntilUtc = DateTime.UtcNow.Add(CombatStartWindow);
			Log(string.Format(CultureInfo.InvariantCulture, "direction=IN route=AttackMessageHandler.Read message=AttackCommand source={0} target={1} action={2} resolvedTarget={3} targetHealth={4} sourceState={5} sourceCurrentState={6} sourceActionCategory={7} sourceAggDef={8}", IdentityText((character == null) ? Identity.None : ((IEntity)character).Identity), IdentityText(target), action, resolvedTarget != null, StatValue(resolvedTarget, (StatIds)27), StatValue(character, (StatIds)7), StatValue(character, (StatIds)423), StatValue(character, (StatIds)588), StatValue(character, (StatIds)51)));
		}
	}

	internal static void LogOutbound(string route, MessageBody body, Identity recipient)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (Enabled)
		{
			N3Message val = (N3Message)(object)((body is N3Message) ? body : null);
			if (val != null && ShouldLogN3Message(val))
			{
				Log(string.Format(CultureInfo.InvariantCulture, "direction=OUT route={0} recipient={1} {2}", route, IdentityText(recipient), DescribeN3Message(val)));
			}
		}
	}

	internal static void LogSerializedOutbound(string route, MessageBody body, int sender, Identity receiver, byte[] buffer)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		if (Enabled)
		{
			N3Message val = (N3Message)(object)((body is N3Message) ? body : null);
			if (val != null && ShouldLogN3Message(val))
			{
				Log(string.Format(CultureInfo.InvariantCulture, "direction=OUT_RAW route={0} sender={1} receiver={2} {3} rawLen={4} rawHex={5}", route, sender, IdentityText(receiver), DescribeN3Message(val), (buffer != null) ? buffer.Length : 0, HexExcerpt(buffer)));
			}
		}
	}

	internal static void LogStatBulk(string route, ICharacter character, Dictionary<int, uint> stats, bool announceToPlayfield)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if (Enabled && stats != null && stats.Count != 0 && IsInCombatStartWindow())
		{
			Log(string.Format(CultureInfo.InvariantCulture, "direction=OUT route={0} message=Stat source={1} announceToPlayfield={2} len=unavailable stats={3}", route, IdentityText((character == null) ? Identity.None : ((IEntity)character).Identity), announceToPlayfield ? 1 : 0, DescribeStats(stats)));
		}
	}

	private static string DescribeN3Message(N3Message message)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		AttackMessage val = (AttackMessage)(object)((message is AttackMessage) ? message : null);
		if (val != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=Attack n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} action={4}", ((N3Message)val).N3MessageType, IdentityText(((N3Message)val).Identity), IdentityText(val.Target), ((N3Message)val).Unknown, val.Action);
		}
		AttackInfoMessage val2 = (AttackInfoMessage)(object)((message is AttackInfoMessage) ? message : null);
		if (val2 != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=AttackInfo n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} damage={4} ammoCount={5} weaponSlot={6} unk4={7} hitType={8} weaponInstance={9}", ((N3Message)val2).N3MessageType, IdentityText(((N3Message)val2).Identity), IdentityText(val2.Target), ((N3Message)val2).Unknown, val2.Unknown1, val2.Unknown2, val2.Unknown3, val2.Unknown4, val2.Unknown5, val2.Unknown6);
		}
		SpecialAttackWeaponMessage val3 = (SpecialAttackWeaponMessage)(object)((message is SpecialAttackWeaponMessage) ? message : null);
		if (val3 != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=SpecialAttackWeapon n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} specials={4} u1={5} u2={6} u3={7} u4={8} u5={9} specialList={10}", ((N3Message)val3).N3MessageType, IdentityText(((N3Message)val3).Identity), IdentityText(Identity.None), ((N3Message)val3).Unknown, (val3.Specials != null) ? val3.Specials.Length : 0, val3.Unknown1, val3.Unknown2, val3.Unknown3, val3.Unknown4, val3.Unknown5, DescribeSpecials(val3.Specials));
		}
		StatMessage val4 = (StatMessage)(object)((message is StatMessage) ? message : null);
		if (val4 != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=Stat n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} stats={4}", ((N3Message)val4).N3MessageType, IdentityText(((N3Message)val4).Identity), IdentityText(Identity.None), ((N3Message)val4).Unknown, DescribeStats(val4.Stats));
		}
		CharacterActionMessage val5 = (CharacterActionMessage)(object)((message is CharacterActionMessage) ? message : null);
		if (val5 != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=CharacterAction n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} action={4} p1={5} p2={6} u1={7} u2={8}", ((N3Message)val5).N3MessageType, IdentityText(((N3Message)val5).Identity), IdentityText(val5.Target), ((N3Message)val5).Unknown, val5.Action, val5.Parameter1, val5.Parameter2, val5.Unknown1, val5.Unknown2);
		}
		FormatFeedbackMessage val6 = (FormatFeedbackMessage)(object)((message is FormatFeedbackMessage) ? message : null);
		if (val6 != null)
		{
			return string.Format(CultureInfo.InvariantCulture, "message=FormatFeedback n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} u1={4} u2={5} text={6}", ((N3Message)val6).N3MessageType, IdentityText(((N3Message)val6).Identity), IdentityText(Identity.None), ((N3Message)val6).Unknown, val6.Unknown1, val6.Unknown2, val6.FormattedMessage);
		}
		return string.Format(CultureInfo.InvariantCulture, "message={0} n3Type={1} len=unavailable source={2} target={3} headerUnknown={4}", ((object)message).GetType().Name, message.N3MessageType, IdentityText(message.Identity), IdentityText(Identity.None), message.Unknown);
	}

	private static bool ShouldLogN3Message(N3Message message)
	{
		return IsInCombatStartWindow() || IsCombatPacket(message);
	}

	private static bool IsInCombatStartWindow()
	{
		return DateTime.UtcNow <= diagnosticWindowUntilUtc;
	}

	private static bool IsCombatPacket(N3Message message)
	{
		return message is AttackMessage || message is AttackInfoMessage || message is SpecialAttackWeaponMessage || message is StopFightMessage || message is CharacterActionMessage || message is FormatFeedbackMessage || message is FightModeUpdateMessage;
	}

	private static string DescribeStats(GameTuple<CharacterStat, uint>[] stats)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (stats == null || stats.Length == 0)
		{
			return "[]";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[");
		for (int i = 0; i < stats.Length; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(";");
			}
			stringBuilder.Append(stats[i].Value1);
			stringBuilder.Append("=");
			stringBuilder.Append(stats[i].Value2);
		}
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}

	private static string DescribeStats(Dictionary<int, uint> stats)
	{
		if (stats == null || stats.Count == 0)
		{
			return "[]";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[");
		bool flag = true;
		foreach (KeyValuePair<int, uint> stat in stats)
		{
			if (!flag)
			{
				stringBuilder.Append(";");
			}
			flag = false;
			stringBuilder.Append((object)(CharacterStat)stat.Key);
			stringBuilder.Append("=");
			stringBuilder.Append(stat.Value);
		}
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}

	private static string DescribeSpecials(SpecialAttack[] specials)
	{
		if (specials == null || specials.Length == 0)
		{
			return "[]";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[");
		for (int i = 0; i < specials.Length; i++)
		{
			SpecialAttack val = specials[i];
			if (i > 0)
			{
				stringBuilder.Append(";");
			}
			stringBuilder.Append(val.Unknown4);
			stringBuilder.Append(":");
			stringBuilder.Append(val.Unknown1);
			stringBuilder.Append("/");
			stringBuilder.Append(val.Unknown2);
			stringBuilder.Append("/");
			stringBuilder.Append(val.Unknown3);
		}
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}

	private static int StatValue(ICharacter character, StatIds statId)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return 0;
		}
		return ((IStats)character).Stats[statId].Value;
	}

	private static string IdentityText(Identity identity)
	{
		return ((object)(Identity)(ref identity)).ToString();
	}

	private static string HexExcerpt(byte[] buffer)
	{
		if (buffer == null || buffer.Length == 0)
		{
			return string.Empty;
		}
		int num = Math.Min(buffer.Length, 160);
		StringBuilder stringBuilder = new StringBuilder(num * 2 + 3);
		for (int i = 0; i < num; i++)
		{
			stringBuilder.Append(buffer[i].ToString("X2", CultureInfo.InvariantCulture));
		}
		if (buffer.Length > num)
		{
			stringBuilder.Append("...");
		}
		return stringBuilder.ToString();
	}

	private static bool IsEnabledValue(string value)
	{
		return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
	}

	private static void Log(string details)
	{
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "COMBAT_START_DIAG utc={0:o} {1}", DateTime.UtcNow, details));
	}
}
