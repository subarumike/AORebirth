using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core;

internal static class FlintKneecappingTipWire
{
	private const int MissionIdentityType = 56003;

	private const int CapturedCharacterInstance = 1985636618;

	private const int CapturedDeleteMissionInstance = 1431734586;

	private const int CaptureKneecappingPlayerInstance = 2038313001;

	private const string Action59DeleteHex = "0135000A0001003700000DB6765A690A5E4777700000C350765A690A000000003B000000000000DAC35556893A0000DAC35556893A0000";

	private const string QuestDeleteHex = "0136000A0001003500000DB6765A690A212C487A0000C350765A690A0000000001000000000000DAC35556893A0000000000000000";

	private const string KneecappingQfuHex = "02DE000A000102DE00000DC1797E3029465A40610000C350797E302901000007E20000DAC3555A4E3D0000000F0000000000000000000000024B6E656563617070696E672061204B6E6565627265616B6572000000014E4B6E656563617070696E672061204B6E6565627265616B65723C42523E3C42523E5768696C65206D6F6E69746F72696E672074686520617564696F20616E6420766964656F206665656473206F66204465736D6F6E642043616C697472692C20697420626563616D6520636C656172207468617420686520696E74656E647320746F2073656E642022546865204B6E6565627265616B6572222C20416C666F6E7A6F2052697A7A6F6C6F2C20746F206465616C207769746820616E207570737461727420446F636B776F726B65722077686F206973206669676874696E6720666F72206661697220776F726B696E6720636F6E646974696F6E732E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E4B696C6C2022546865204B6E6565627265616B6572222E3C2F666F6E743E000000C35078E0FC6300000006000000000000000000000000000003F1000003F1000003F1564B315800000000000000000000000000000000000000000000000000000000000000000000C350797E302900002C420000000000000000000007E20000000100000000000000000000000000000000000111D3554E41460000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2FC1C6A106000009C5000001999000186A0000186A0455FC0000000000044504000000007E20000C350797E302900000001046A1060000000000000000000000006000007E20000C350797E302900000000000199E6000000000000000000000000000000000000000000000007000003F101";

	private static readonly int[] TipsToClear = new int[10] { 1431980617, 1431981627, 1431981628, 1432044389, 1432044390, 1432044391, 1427419549, 1427419550, 1427419551, 1427419552 };

	public static bool TrySendDeliverToKneecappingHandoff(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		if (!TryGetClient(source, out var client))
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		for (int i = 0; i < TipsToClear.Length; i++)
		{
			TryDeleteTip(source, TipsToClear[i]);
		}
		TryDeleteTip(source, 1431981629);
		TryDeleteTip(source, 1427419552);
		byte[] array = Hex("02DE000A000102DE00000DC1797E3029465A40610000C350797E302901000007E20000DAC3555A4E3D0000000F0000000000000000000000024B6E656563617070696E672061204B6E6565627265616B6572000000014E4B6E656563617070696E672061204B6E6565627265616B65723C42523E3C42523E5768696C65206D6F6E69746F72696E672074686520617564696F20616E6420766964656F206665656473206F66204465736D6F6E642043616C697472692C20697420626563616D6520636C656172207468617420686520696E74656E647320746F2073656E642022546865204B6E6565627265616B6572222C20416C666F6E7A6F2052697A7A6F6C6F2C20746F206465616C207769746820616E207570737461727420446F636B776F726B65722077686F206973206669676874696E6720666F72206661697220776F726B696E6720636F6E646974696F6E732E3C42523E3C42523E3C666F6E7420636F6C6F723D2223464630303030223E4D697373696F6E204F626A6563746976653A3C42523E4B696C6C2022546865204B6E6565627265616B6572222E3C2F666F6E743E000000C35078E0FC6300000006000000000000000000000000000003F1000003F1000003F1564B315800000000000000000000000000000000000000000000000000000000000000000000C350797E302900002C420000000000000000000007E20000000100000000000000000000000000000000000111D3554E41460000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000D2FC1C6A106000009C5000001999000186A0000186A0455FC0000000000044504000000007E20000C350797E302900000001046A1060000000000000000000000006000007E20000C350797E302900000000000199E6000000000000000000000000000000000000000000000007000003F101");
		ReplaceInstance(array, 2038313001, instance);
		client.EnqueueOutboundCompressedBuffer(array);
		identity = ((IEntity)source).Identity;
		LogUtil.Debug((DebugInfoDetail)512, "FlintKneecappingTipWire Deliver→Kneecapping handoff character=" + ((Identity)(ref identity)).ToString(true));
		return true;
	}

	public static bool TryDeleteTip(ICharacter source, int missionInstance)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!TryGetClient(source, out var client) || missionInstance == 0)
		{
			return false;
		}
		ZoneClient client2 = client;
		Identity identity = ((IEntity)source).Identity;
		EnqueueTipDelete(client2, source, ((Identity)(ref identity)).Instance, missionInstance);
		return true;
	}

	public static void ClearChainTips(ZoneClient client, Character character)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		if (client != null && character != null)
		{
			Identity identity = ((PooledObject)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			for (int i = 0; i < TipsToClear.Length; i++)
			{
				EnqueueTipDelete(client, (ICharacter)(object)character, instance, TipsToClear[i]);
			}
			EnqueueTipDelete(client, (ICharacter)(object)character, instance, 1431981629);
			EnqueueTipDelete(client, (ICharacter)(object)character, instance, 1427419552);
		}
	}

	public static void SendKneecappingTip(ZoneClient client, Character character)
	{
		if (client != null && character != null)
		{
			TrySendDeliverToKneecappingHandoff((ICharacter)(object)character);
		}
	}

	private static void EnqueueTipDelete(ZoneClient client, ICharacter character, int recipientInstance, int missionInstance)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		byte[] array = Hex("0135000A0001003700000DB6765A690A5E4777700000C350765A690A000000003B000000000000DAC35556893A0000DAC35556893A0000");
		ReplaceInstance(array, 1985636618, recipientInstance);
		ReplaceInstance(array, 1431734586, missionInstance);
		byte[] array2 = Hex("0136000A0001003500000DB6765A690A212C487A0000C350765A690A0000000001000000000000DAC35556893A0000000000000000");
		ReplaceInstance(array2, 1985636618, recipientInstance);
		ReplaceInstance(array2, 1431734586, missionInstance);
		client.EnqueueOutboundCompressedBuffer(array);
		client.EnqueueOutboundCompressedBuffer(array2);
		QuestMessage val = new QuestMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (QuestAction)1,
			Unknown1 = 0
		};
		Identity mission = default(Identity);
		((Identity)(ref mission)).Type = (IdentityType)56003;
		((Identity)(ref mission)).Instance = missionInstance;
		val.Mission = mission;
		val.Unknown2 = 0;
		val.Unknown3 = 0;
		client.SendCompressed((MessageBody)val);
	}

	private static bool TryGetClient(ICharacter source, out ZoneClient client)
	{
		client = null;
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return false;
		}
		client = ((IDynel)source).Controller.Client as ZoneClient;
		return client != null;
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
				packet[i] = (byte)(to >> 24);
				packet[i + 1] = (byte)(to >> 16);
				packet[i + 2] = (byte)(to >> 8);
				packet[i + 3] = (byte)to;
				i += 3;
			}
		}
	}

	private static byte[] Hex(string hex)
	{
		byte[] array = new byte[hex.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
