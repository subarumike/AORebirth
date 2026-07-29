using System;
using AORebirth.Core.Entities;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Perks;

public static class PerkResetMissionSender
{
	private const int CapturedCharacterInstance = 2036789339;

	private const int ExpiryOffset = 503;

	private const int MissionIdentityType = 56003;

	private const int MissionInstance = 1431409111;

	private const int MissionDurationSeconds = 172800;

	private const long ClientClockBaseSeconds = 1201445827L;

	private const string CapturedQuestFullUpdateHex = "03A3000A0001027400000DB67966F05B465A40610000C3507966F05B01000007E20000DAC3555191D70000000F00000000000000000000040246756C6C205065726B20506F696E7473205265736574205365727669636500000000DF46756C6C205065726B20506F696E747320526573657420536572766963653C42523E3C42523E596F7520686176652063686F73656E20746F20756E747261696E20616C6C206F6620796F7572207065726B20706F696E747320726563656E746C792E205468697320736572766963652063616E206F6E6C79206265206163636573736564206F6E636520647572696E67206120706572696F64206F6620343820686F7572732C20617320746869732022717569636B20616E64206469727479222070726F6365737320737472657373657320796F75722073797374656D2E000000C35078A4C5B100000006000000000000000000000000000003F1000003F1000003F1534F395100000000000000000000000000000000000000000000000000000000000000000000C3507966F05B0003BC5200000B4000000B40000007E20000001800000000000000000000000000000000000111D300019534000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5B0089000000000000D2F14D4DD5E300000000000000000000000000000000000000000000000000000000000007E20000C3507966F05B00000001054DD5E3000000000000000000000006000007E20000C3507966F05B0000000000019534000000000000000000000000000000000000000000000007000003F101";

	public static void SendResetCooldownMission(Character character, int remainingSeconds)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Expected O, but got Unknown
		if (character == null || ((Dynel)character).Controller == null || !(((Dynel)character).Controller.Client is ZoneClient zoneClient) || remainingSeconds <= 0)
		{
			return;
		}
		if (remainingSeconds > 172800)
		{
			remainingSeconds = 172800;
		}
		try
		{
			byte[] array = HexToBytes("03A3000A0001027400000DB67966F05B465A40610000C3507966F05B01000007E20000DAC3555191D70000000F00000000000000000000040246756C6C205065726B20506F696E7473205265736574205365727669636500000000DF46756C6C205065726B20506F696E747320526573657420536572766963653C42523E3C42523E596F7520686176652063686F73656E20746F20756E747261696E20616C6C206F6620796F7572207065726B20706F696E747320726563656E746C792E205468697320736572766963652063616E206F6E6C79206265206163636573736564206F6E636520647572696E67206120706572696F64206F6620343820686F7572732C20617320746869732022717569636B20616E64206469727479222070726F6365737320737472657373657320796F75722073797374656D2E000000C35078A4C5B100000006000000000000000000000000000003F1000003F1000003F1534F395100000000000000000000000000000000000000000000000000000000000000000000C3507966F05B0003BC5200000B4000000B40000007E20000001800000000000000000000000000000000000111D300019534000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000006A5B0089000000000000D2F14D4DD5E300000000000000000000000000000000000000000000000000000000000007E20000C3507966F05B00000001054DD5E3000000000000000000000006000007E20000C3507966F05B0000000000019534000000000000000000000000000000000000000000000007000003F101");
			Identity mission = ((PooledObject)character).Identity;
			int instance = ((Identity)(ref mission)).Instance;
			ReplaceInstance(array, 2036789339, instance);
			double num = (DateTime.UtcNow - zoneClient.LastGameTimeSyncUtc).TotalSeconds;
			if (num < 0.0)
			{
				num = 0.0;
			}
			long num2 = 1201445827 + (long)num;
			long num3 = num2 + remainingSeconds;
			WriteInt32BigEndian(array, 503, (int)num3);
			QuestMessage val = new QuestMessage
			{
				Identity = ((PooledObject)character).Identity,
				Unknown = 0,
				Action = (QuestAction)1,
				Unknown1 = 0
			};
			mission = default(Identity);
			((Identity)(ref mission)).Type = (IdentityType)56003;
			((Identity)(ref mission)).Instance = 1431409111;
			val.Mission = mission;
			val.Unknown2 = 0;
			val.Unknown3 = 0;
			zoneClient.SendCompressed((MessageBody)val);
			zoneClient.EnqueueOutboundCompressedBuffer(array);
			LogUtil.Debug((DebugInfoDetail)128, "PerkResetMission sent reset-cooldown mission char=" + instance + " remainingSeconds=" + remainingSeconds + " sinceSync=" + (long)num + " expiry=" + num3);
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
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
