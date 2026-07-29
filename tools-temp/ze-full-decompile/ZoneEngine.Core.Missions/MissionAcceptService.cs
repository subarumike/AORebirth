using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Missions;

internal static class MissionAcceptService
{
	private const long ClientClockBaseSeconds = 1201445827L;

	private const int MissionDurationSeconds = 172800;

	private const int MissionIdentityType = 56003;

	private const int MissionInstance = 1431344275;

	private const int QuestIdInstanceOffset = 37;

	private const int MissionIconIdOffset = 563;

	private const float GameTimeUnknown1 = 30024f;

	private const int GameTimeUnknown3 = 185408;

	private const float GameTimeUnknown4 = 80183.31f;

	public static bool TryResendForLogin(ICharacter character)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return false;
		}
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		bool flag = MissionKeyGrantService.HasMissionKey(character);
		if (all.Count == 0 && !flag)
		{
			object[] array = new object[1];
			identity = ((IEntity)character).Identity;
			array[0] = ((Identity)(ref identity)).Instance;
			MissionDiagnostics.Log("LOGIN-RESYNC skip char={0} hasKey=false count=0", array);
			return false;
		}
		if (all.Count == 0)
		{
			object[] array2 = new object[1];
			identity = ((IEntity)character).Identity;
			array2[0] = ((Identity)(ref identity)).Instance;
			MissionDiagnostics.Log("LOGIN-RESYNC skip char={0} hasKey=true count=0", array2);
			return false;
		}
		ReanchorGameTime(character);
		int num = 0;
		foreach (MissionAcceptedStore.AcceptedMission item in all)
		{
			if (SendOneMissionWindow(character, item.Offer, item, register: false))
			{
				num++;
			}
		}
		object[] array3 = new object[4];
		identity = ((IEntity)character).Identity;
		array3[0] = ((Identity)(ref identity)).Instance;
		array3[1] = flag;
		array3[2] = all.Count;
		array3[3] = num;
		MissionDiagnostics.Log("LOGIN-RESYNC char={0} hasKey={1} count={2} sent={3}", array3);
		return num > 0;
	}

	public static bool SendAcceptedMission(ICharacter character, QuestInfo offer)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		if (offer == null)
		{
			return false;
		}
		ReanchorGameTime(character);
		MissionAcceptedStore.AcceptedMission acceptedMission = new MissionAcceptedStore.AcceptedMission
		{
			QuestIdentity = offer.QuestIdentity,
			MissionIconId = offer.MissionIconId,
			Quality = offer.Quality,
			ShortInfo = offer.ShortInfo,
			ExpiryUtc = DateTime.UtcNow.AddSeconds(172800.0),
			Offer = offer
		};
		if (offer.QuestActions != null && offer.QuestActions.Length != 0 && offer.QuestActions[0] != null)
		{
			QuestActionList val = offer.QuestActions[0];
			Identity playfield = val.Playfield;
			acceptedMission.MarkerPlayfield = ((Identity)(ref playfield)).Instance;
			acceptedMission.EntranceLow = val.Unknown18;
			acceptedMission.EntranceHigh = val.Unknown19;
			acceptedMission.MarkerX = val.X;
			acceptedMission.MarkerY = val.Y;
			acceptedMission.MarkerZ = val.Z;
		}
		return SendOneMissionWindow(character, offer, acceptedMission, register: true);
	}

	public static void RefreshAllMissionTimers(ICharacter character)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return;
		}
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		if (all.Count == 0)
		{
			return;
		}
		ReanchorGameTime(character);
		int num = 0;
		foreach (MissionAcceptedStore.AcceptedMission item in all)
		{
			if (SendOneMissionWindow(character, item.Offer, item, register: false))
			{
				num++;
			}
		}
		object[] array = new object[3];
		identity = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref identity)).Instance;
		array[1] = all.Count;
		array[2] = num;
		MissionDiagnostics.Log("TIMER-REFRESH char={0} count={1} sent={2}", array);
	}

	private static void ReanchorGameTime(ICharacter character)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		ZoneClient zoneClient = ((character != null && ((IDynel)character).Controller != null) ? (((IDynel)character).Controller.Client as ZoneClient) : null);
		if (zoneClient != null && character != null)
		{
			GameTimeMessage val = new GameTimeMessage();
			Identity identity = default(Identity);
			((Identity)(ref identity)).Type = (IdentityType)50000;
			Identity identity2 = ((IEntity)character).Identity;
			((Identity)(ref identity)).Instance = ((Identity)(ref identity2)).Instance;
			((N3Message)val).Identity = identity;
			val.Unknown1 = 30024f;
			val.Unknown3 = 185408;
			val.Unknown4 = 80183.31f;
			zoneClient.SendCompressed((MessageBody)val);
			zoneClient.LastGameTimeSyncUtc = DateTime.UtcNow;
		}
	}

	private static bool SendOneMissionWindow(ICharacter character, QuestInfo offer, MissionAcceptedStore.AcceptedMission stored, bool register, bool deleteBeforeSend = false)
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0301: Unknown result type (might be due to invalid IL or missing references)
		//IL_0309: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0319: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Expected O, but got Unknown
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IDynel)character).Controller == null)
		{
			return false;
		}
		if (!(((IDynel)character).Controller.Client is ZoneClient zoneClient))
		{
			return false;
		}
		try
		{
			int num = ((offer != null) ? offer.MissionIconId : (stored?.MissionIconId ?? 11330));
			if (num == 0)
			{
				num = 11330;
			}
			Identity val;
			Identity val2;
			if (offer == null)
			{
				if (stored == null)
				{
					val = default(Identity);
					((Identity)(ref val)).Type = (IdentityType)56003;
					((Identity)(ref val)).Instance = 1431344275;
					val2 = val;
				}
				else
				{
					val2 = stored.QuestIdentity;
				}
			}
			else
			{
				val2 = offer.QuestIdentity;
			}
			Identity val3 = val2;
			if ((int)((Identity)(ref val3)).Type == 0 || ((Identity)(ref val3)).Instance == 0)
			{
				val = default(Identity);
				((Identity)(ref val)).Type = (IdentityType)56003;
				((Identity)(ref val)).Instance = 1431344275;
				val3 = val;
			}
			int num2 = 172800;
			if (stored != null)
			{
				double totalSeconds = (stored.ExpiryUtc - DateTime.UtcNow).TotalSeconds;
				if (totalSeconds <= 0.0)
				{
					val = ((IEntity)character).Identity;
					MissionAcceptedStore.Remove(((Identity)(ref val)).Instance, val3);
					object[] array = new object[2];
					val = ((IEntity)character).Identity;
					array[0] = ((Identity)(ref val)).Instance;
					array[1] = ((Identity)(ref val3)).Instance;
					MissionDiagnostics.Log("ACCEPT-WINDOW expired char={0} quest={1:X8}", array);
					return false;
				}
				num2 = (int)totalSeconds;
				if (num2 > 172800)
				{
					num2 = 172800;
				}
				if (num2 < 1)
				{
					num2 = 1;
				}
			}
			byte[] array2 = HexToBytes("0032000A0001034000000DB10013945A465A40610000C3500013945A01000007E20000DAC3555094930000000F00000000000000000000000247726561742120436F6D65206261636B20666F7220616E6F746865722E2E2E000000017647726561742120436F6D65206261636B20666F7220616E6F74686572206D697373696F6E2C2077696C6C20796F753F20416E20696D706F7274616E74206F666669636572206F662074686520636C616E2073696465202853757A6965204D69726162656C6C69292069732061626F757420746F206C61756E636820616E2061747461636B206F6E206F757220696E7465726573747320696E20526F6D6520426C75652064697374726963742E204865206F706572617465732066726F6D2074686520526F6D6520686F75736520696E207468652061726561206F6620526F6D6520426C75652E20506C6561736520676F2074686572652C20636C65616E206F757420686973207374726F6E67686F6C642C20616E642068696D20776974682069742E204869732061747461636B2073686F756C6420737461727420696E20343820686F7572732C20736F20706C656173652062652073776966742061626F75742069742E2E2E2054616B652063617265206E6F772E000000DAC1C00402DF00000006000007DC0000000000000107000003F1000003F1000007E200018D9700018D98000000120000000000000000000000000000000051534F5200000012000000000000000000000000000000000000C3500013945A00002C4200000B4000000B40000007E200000001000000000000000000000000000000000000C35079725395000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5CE568000000000000D2FC1C679EB100009C50000002DF0000847D000076A14434267341B20E084396D286000007E20000C3500013945A0000000104679EB1000000000000000000000001000007E20000C3500013945A0000000000000C490000C79F00D728480000000000000000000000030000C76D00F6706A000000080000C76D00F6706A000000040000C76D00F6706A0000000200000003000003F101");
			val = ((IEntity)character).Identity;
			ReplaceInstance(array2, 1283162, ((Identity)(ref val)).Instance);
			double num3 = (DateTime.UtcNow - zoneClient.LastGameTimeSyncUtc).TotalSeconds;
			if (num3 < 0.0)
			{
				num3 = 0.0;
			}
			if (num3 > 172800.0)
			{
				num3 = 0.0;
				zoneClient.LastGameTimeSyncUtc = DateTime.UtcNow;
			}
			long num4 = 1201445827 + (long)num3;
			long num5 = num4 + num2;
			WriteInt32BigEndian(array2, 671, (int)num5);
			WriteInt32BigEndian(array2, 37, ((Identity)(ref val3)).Instance);
			WriteInt32BigEndian(array2, 563, num);
			ApplyMarkerLocation(array2, offer, stored);
			DateTime expiryUtc = stored?.ExpiryUtc ?? DateTime.UtcNow.AddSeconds(172800.0);
			if (register && offer != null)
			{
				val = ((IEntity)character).Identity;
				MissionAcceptedStore.Register(((Identity)(ref val)).Instance, offer, expiryUtc);
			}
			if (deleteBeforeSend)
			{
				zoneClient.SendCompressed((MessageBody)new QuestMessage
				{
					Identity = ((IEntity)character).Identity,
					Unknown = 0,
					Action = (QuestAction)1,
					Unknown1 = 0,
					Mission = val3,
					Unknown2 = 0,
					Unknown3 = 0
				});
			}
			zoneClient.SendCompressed(array2);
			int num6 = 0;
			float num7 = 0f;
			float num8 = 0f;
			if (stored != null && stored.MarkerPlayfield != 0)
			{
				num6 = stored.MarkerPlayfield;
				num7 = stored.MarkerX;
				num8 = stored.MarkerZ;
			}
			else if (offer != null && offer.QuestActions != null && offer.QuestActions.Length != 0 && offer.QuestActions[0] != null)
			{
				val = offer.QuestActions[0].Playfield;
				num6 = ((Identity)(ref val)).Instance;
				num7 = offer.QuestActions[0].X;
				num8 = offer.QuestActions[0].Z;
			}
			object[] array3 = new object[12];
			val = ((IEntity)character).Identity;
			array3[0] = ((Identity)(ref val)).Instance;
			array3[1] = ((Identity)(ref val3)).Instance;
			array3[2] = num;
			array3[3] = ((offer != null) ? offer.Quality : (stored?.Quality ?? 0));
			array3[4] = num2;
			array3[5] = num5;
			array3[6] = (long)num3;
			array3[7] = register;
			array3[8] = deleteBeforeSend;
			array3[9] = num6;
			array3[10] = num7;
			array3[11] = num8;
			MissionDiagnostics.Log("ACCEPT-WINDOW char={0} quest={1:X8} icon={2} ql={3} remainSec={4} expiry={5} sinceSync={6} register={7} deleteFirst={8} markerPf={9} xz=({10:0.###},{11:0.###})", array3);
			return true;
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
			return false;
		}
	}

	private static void ApplyMarkerLocation(byte[] packet, QuestInfo offer, MissionAcceptedStore.AcceptedMission stored)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		int num = 0;
		int value = 0;
		int value2 = 0;
		float value3 = 0f;
		float value4 = 0f;
		float value5 = 0f;
		if (stored != null && stored.MarkerPlayfield != 0)
		{
			num = stored.MarkerPlayfield;
			value = stored.EntranceLow;
			value2 = stored.EntranceHigh;
			value3 = stored.MarkerX;
			value4 = stored.MarkerY;
			value5 = stored.MarkerZ;
		}
		else if (offer != null && offer.QuestActions != null && offer.QuestActions.Length != 0 && offer.QuestActions[0] != null)
		{
			QuestActionList val = offer.QuestActions[0];
			Identity playfield = val.Playfield;
			num = ((Identity)(ref playfield)).Instance;
			value = val.Unknown18;
			value2 = val.Unknown19;
			value3 = val.X;
			value4 = val.Y;
			value5 = val.Z;
		}
		if (num != 0)
		{
			WriteInt32BigEndian(packet, 691, num);
			WriteInt32BigEndian(packet, 695, value);
			WriteInt32BigEndian(packet, 699, value2);
			WriteFloatBigEndian(packet, 703, value3);
			WriteFloatBigEndian(packet, 707, value4);
			WriteFloatBigEndian(packet, 711, value5);
		}
	}

	private static void ReplaceInstance(byte[] packet, int from, int to)
	{
		byte b = (byte)(from >> 24);
		byte b2 = (byte)(from >> 16);
		byte b3 = (byte)(from >> 8);
		byte b4 = (byte)from;
		for (int i = 0; i + 4 <= packet.Length; i++)
		{
			if (packet[i] == b && packet[i + 1] == b2 && packet[i + 2] == b3 && packet[i + 3] == b4)
			{
				WriteInt32BigEndian(packet, i, to);
				i += 3;
			}
		}
	}

	private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
	}

	private static void WriteFloatBigEndian(byte[] buffer, int offset, float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (BitConverter.IsLittleEndian)
		{
			buffer[offset] = bytes[3];
			buffer[offset + 1] = bytes[2];
			buffer[offset + 2] = bytes[1];
			buffer[offset + 3] = bytes[0];
		}
		else
		{
			buffer[offset] = bytes[0];
			buffer[offset + 1] = bytes[1];
			buffer[offset + 2] = bytes[2];
			buffer[offset + 3] = bytes[3];
		}
	}

	private static byte[] HexToBytes(string hex)
	{
		int num = hex.Length / 2;
		byte[] array = new byte[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
